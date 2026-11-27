using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [79-R19] 전보 비둘기 — 하루 전쟁 전보(DailyWarLogSystem.FlushDayLog)를 비둘기 비행 연출로 전달.
    /// Polyfork pigeon-base GLB(rig: LeftWing/RightWing/WingTip/Tail/Head/Neck)를 절차적으로 날갯짓.
    ///  · 비행: 플레이어 후방 고공(약 30m/18m) → 2차 베지어 경로 → 플레이어 머리 위(1.6m).
    ///  · 날갯짓: Wing z축 ±24° 사인 + WingTip 위상 지연(페더 랙), 상승 시 빈도↑·하강 시 글라이드(진폭↓).
    ///  · 뱅킹: 수평 선회율 → 롤, 수직 속도 → 피치. 꼬리(Tail)는 상승 시 하강(기수 들기 보조).
    ///  · 도착: 머리 위 호버 1.5s → 이륙·디스폰 → onArrive 콜백(전투로그 배너는 디스폰 직후 표시).
    ///  · 폴백: 플레이어 부재/GLB 부재/실내 씬/예외 → 스폰 실패(null) — 호출부가 즉시 배너 폴백.
    ///  · 안전: 최대 수명 25s 초과 시 강제 도착(전보 유실 방지).
    /// </summary>
    public class PigeonMessenger : MonoBehaviour
    {
        /// <summary>현재 비행 중인 전보 비둘기(중복 스폰 가드 — 새 전보는 기존 비둘기 콜백에 체인).</summary>
        public static PigeonMessenger Active;

        // ===== 뼈대 참조 =====
        private Transform _leftWing, _rightWing, _leftWingTip, _rightWingTip, _tail, _head, _neck;
        private Quaternion _leftWingRest, _rightWingRest, _leftTipRest, _rightTipRest, _tailRest;

        // ===== 비행 상태 =====
        private Vector3 _start, _ctrl, _end;
        private float _t;               // 진행도 0..1
        private float _flightDuration = 8f;
        private float _timer;
        private float _maxLifetime = 25f;
        private float _hoverTimer;
        private bool _arrived;
        private bool _departing;
        private System.Action _onArrive;

        // ===== 날갯짓 파라미터 =====
        private const float FlapFreqBase = 8f;      // 기본 날갯짓 Hz
        private const float FlapFreqClimb = 13f;    // 상승 시 빈도
        private const float FlapAmpDeg = 24f;       // Wing z축 진폭(deg — Polyfork range)
        private const float TipAmpDeg = 15f;        // WingTip x축 진폭(deg)
        private const float TipLag = 0.35f;         // 페더 랙(위상 지연 비율)
        private float _flapPhase;

        /// <summary>
        /// 비둘기 스폰 — 성공 시 컴포넌트(도착 콜백 보유), 실패 시 null(폴백).
        /// 도착 시퀀스: 호버 → 이륙 → 디스폰 직후 onArrive 콜백.
        /// </summary>
        public static PigeonMessenger Spawn(string summary, Action onArrive)
        {
            if (!Application.isPlaying) return null;   // EditMode 무가동(편집모드 코루틴 게이트 계약)

            // 중복 스폰 가드: 기존 비둘기가 날아오는 중이면 도착 콜백에 체인(전보 유실 없음)
            if (Active != null)
            {
                Active.Chain(onArrive);
                return Active;
            }

            Transform playerTr = ResolvePlayer();
            if (playerTr == null) return null;   // 폴백 — 플레이어 부재

            var prefab = Resources.Load<GameObject>("Models/Polyfork/pigeon-base");
            if (prefab == null)
            {
                Debug.LogWarning("[PigeonMessenger] Models/Polyfork/pigeon-base 로드 실패 — 즉시 배너 폴백");
                return null;
            }

            var go = Instantiate(prefab);
            go.name = "PigeonMessenger";
            // 렌더러 전용 — 콜라이더 제거(장식 가구 계약 선례)
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            // 실물 스케일 0.15×0.29×0.3m → 가시성 3배
            go.transform.localScale = Vector3.one * 3f;

            Vector3 playerPos = playerTr.position;
            Vector3 spawnOffset = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized * 30f;
            Vector3 startPos = playerPos + spawnOffset + Vector3.up * 18f;
            go.transform.position = startPos;

            var comp = go.AddComponent<PigeonMessenger>();
            comp._start = startPos;
            comp._end = playerPos + Vector3.up * 1.6f;
            comp._ctrl = Vector3.Lerp(startPos, comp._end, 0.5f) + Vector3.up * 6f;
            comp._onArrive = onArrive;
            Active = comp;

            Debug.Log($"[PigeonMessenger] 전보 비둘기 스폰 — 요약 길이 {summary?.Length ?? 0}, 시작 {startPos}, 도착 {comp._end}");
            return comp;
        }

        /// <summary>중복 전보 체인 — 도착 시 함께 콜백(전보 유실 방지).</summary>
        private void Chain(Action onArrive)
        {
            if (onArrive != null)
            {
                var existing = _onArrive;
                _onArrive = existing != null ? (Action)(() => { existing(); onArrive(); }) : onArrive;
            }
        }

        private void Start()
        {
            // 뼈대 탐색 — GLB 리그 노드명(LeftWing/RightWing/WingTip/Tail/Head/Neck)
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var tr in all)
            {
                switch (tr.name)
                {
                    case "LeftWing": _leftWing = tr; break;
                    case "RightWing": _rightWing = tr; break;
                    case "LeftWingTip": _leftWingTip = tr; break;
                    case "RightWingTip": _rightWingTip = tr; break;
                    case "Tail": _tail = tr; break;
                    case "Head": _head = tr; break;
                    case "Neck": _neck = tr; break;
                }
            }
            if (_leftWing == null || _rightWing == null)
                Debug.LogWarning("[PigeonMessenger] 날개 뼈 미발견 — 날갯짓 없이 직선 비행만");
            _leftWingRest = _leftWing != null ? _leftWing.localRotation : Quaternion.identity;
            _rightWingRest = _rightWing != null ? _rightWing.localRotation : Quaternion.identity;
            _leftTipRest = _leftWingTip != null ? _leftWingTip.localRotation : Quaternion.identity;
            _rightTipRest = _rightWingTip != null ? _rightWingTip.localRotation : Quaternion.identity;
            _tailRest = _tail != null ? _tail.localRotation : Quaternion.identity;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_arrived)
            {
                HoverAndDepart();
                return;
            }

            // 안전 게이트 — 최대 수명 초과 시 강제 도착(전보 유실 방지)
            if (_timer >= _maxLifetime)
            {
                Debug.LogWarning("[PigeonMessenger] 최대 수명 초과 — 강제 도착");
                _t = 1f;
            }

            // 경로 진행 — 시간 기반 + 안전 게이트
            float u = Mathf.Clamp01(_timer / _flightDuration);
            _t = Mathf.SmoothStep(0f, 1f, u);

            Vector3 prevPos = transform.position;
            Vector3 pos = EvaluateBezier(_start, _ctrl, _end, _t);
            transform.position = pos;

            // 자세 — 방향(피치/롤) 계산
            Vector3 vel = (pos - prevPos) / Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 hVel = new Vector3(vel.x, 0f, vel.z);
            if (hVel.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(hVel.normalized);
                float pitch = Mathf.Clamp(-vel.y * 2f, -30f, 45f);   // 하강 시 기수 내림, 상승 시 올림
                Quaternion pitchQ = Quaternion.Euler(pitch, 0f, 0f);
                transform.rotation = look * pitchQ;
            }

            // 날갯짓 — 수직 속도에 따라 빈도/진폭 조정
            float yVel = vel.y;
            float freq = yVel > 0.5f ? FlapFreqClimb : (yVel < -0.5f ? FlapFreqBase * 0.4f : FlapFreqBase);
            float amp = yVel < -0.5f ? 0.25f : 1f;   // 하강 시 글라이드(진폭 축소)
            _flapPhase += freq * 2f * Mathf.PI * Time.deltaTime;
            Flap(amp);

            // 꼬리 — 상승 시 내림(기수 보조), 하강 시 올림
            if (_tail != null)
            {
                float tailDeg = Mathf.Clamp(-yVel * 3f, -12f, 12f);
                _tail.localRotation = _tailRest * Quaternion.Euler(tailDeg, 0f, 0f);
            }

            if (u >= 1f)
            {
                _arrived = true;
                _hoverTimer = 0f;
                Debug.Log("[PigeonMessenger] 도착 — 호버 시작");
            }
        }

        /// <summary>도착 후: 머리 위 호버 1.5s → 이륙·디스폰 → onArrive(전투로그 배너는 디스폰 직후).</summary>
        private void HoverAndDepart()
        {
            _hoverTimer += Time.deltaTime;

            if (_hoverTimer < 1.5f)
            {
                // 호버 — 플레이어 머리 위 유지 + 미세 bob
                var playerTr = ResolvePlayer();
                if (playerTr != null)
                    transform.position = Vector3.Lerp(transform.position, playerTr.position + Vector3.up * 1.6f, 3f * Time.deltaTime);
                float bob = Mathf.Sin(Time.time * 6f) * 0.08f;
                transform.position += Vector3.up * bob;
                Flap(0.35f);   // 실호 플랩(약하게)
                return;
            }

            if (!_departing)
            {
                _departing = true;
                // 이륙 연출 — 상승하며 소멸(2초), 소멸(디스폰) 직후 onArrive 콜백(전투로그 배너)
                StartCoroutine(DepartUpward());
            }
        }

        private IEnumerator DepartUpward()
        {
            float t = 0f;
            Vector3 start = transform.position;
            while (t < 2f)
            {
                t += Time.deltaTime;
                float k = t / 2f;
                transform.position = start + Vector3.up * (12f * k) + transform.forward * (15f * k);
                _flapPhase += FlapFreqClimb * 2f * Mathf.PI * Time.deltaTime;
                Flap(1f);
                yield return null;
            }
            // 디스폰 → 전투로그 배너(콜백) — 사용자 지시: 디스폰 후 바로 표시
            var cb = _onArrive;
            _onArrive = null;
            Destroy(gameObject);
            if (cb != null) cb();
        }

        /// <summary>절차적 날갯짓 — Wing z축 사인 + WingTip 위상 지연.</summary>
        private void Flap(float amp)
        {
            if (_leftWing == null || _rightWing == null) return;
            float flap = Mathf.Sin(_flapPhase) * FlapAmpDeg * amp;
            // 좌/우 대칭 — 좌측 -z, 우측 +z(Polyfork range 방향)
            _leftWing.localRotation = _leftWingRest * Quaternion.Euler(0f, 0f, flap);
            _rightWing.localRotation = _rightWingRest * Quaternion.Euler(0f, 0f, -flap);
            if (_leftWingTip != null && _rightWingTip != null)
            {
                float tip = Mathf.Sin(_flapPhase - TipLag) * TipAmpDeg * amp;
                _leftWingTip.localRotation = _leftTipRest * Quaternion.Euler(tip, 0f, 0f);
                _rightWingTip.localRotation = _rightTipRest * Quaternion.Euler(-tip, 0f, 0f);
            }
        }

        /// <summary>2차 베지어 평가.</summary>
        private static Vector3 EvaluateBezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            float it = 1f - t;
            return it * it * a + 2f * it * t * c + t * t * b;
        }

        /// <summary>플레이어 해석 — PlayerController.Instance 우선, 태그 "Player" 폴백(테스트 씬 15 실측: SetupPlayer가 PlayerController를 안 붙임).</summary>
        private static Transform ResolvePlayer()
        {
            var pc = PlayerController.Instance;
            if (pc != null) return pc.transform;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
