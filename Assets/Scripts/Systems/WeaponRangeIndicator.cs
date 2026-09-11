using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 공격 범위 표시기 (2026-09-11 신규):
    /// 플레이어 발 아래 지면에 무기 타입별 사거리 원형 링(LineRenderer 64분할) + 전방 사거리 방향
    /// 화살 표시를 부착한다. 활(Bow)은 링 가장자리(10m)까지 이어지는 전방 방향 선, 근접(검/창)은
    /// 링 앞쪽 짧은 화살촉(닫힌 V자 외곽선)으로 전방을 강조한다.
    /// - 반경 소스: WeaponEquipManager.CurrentType → WeaponData 정적 스탯 사거리
    ///   (Sword 2.5m / Spear 4m / Bow 10m / Fist 2m — 타입 고정, 등급 배율은 dmg에만 반영되므로 무시)
    /// - 표시 조건: 무기 장착 중(CurrentType != Fist)만 표시, 맨손은 숨김. CurrentType 변화 엣지로 반경 갱신.
    /// - 지면 y: 플레이어 루트 y 기준 추적(각 씬 계약상 플레이어가 이미 접지되므로 지형 샘플링 없음).
    ///   단, PlayerMovement 계약상 루트 = 캡슐 중심(position.y = 표면 + height/2)이므로
    ///   CharacterController가 있으면 발 위치(루트 - height/2 + center.y)로 재보정한다.
    /// - 플레이어 중심 LateUpdate 추적, 링/화살은 항상 수평(루트 기울임 정렬 무시 — BlobShadow 선례).
    /// - 머티리얼: Shader "Sprites/Default"(URP 호환, 절차 생성 — 에셋 의존 없음), static 캐시
    ///   (HideAndDontSave) + 파기 금지 — 프로젝트 절차 리소스 관례 준수.
    /// - 부팅 안전: try-catch, 실패 시 경고 1회 후 컴포넌트 비활성화.
    /// - 숨은 호스트 아님: 플레이어 루트에 직접 부착, 링/화살은 플레이어 자식.
    /// 부착: TestAllInOneSetup / TestTerritoryCombatSetup의 플레이어 생성 직후
    /// WeaponRangeIndicator.EnsureOn(플레이어루트) 1줄 호출.
    /// </summary>
    public class WeaponRangeIndicator : MonoBehaviour
    {
        const int SEGMENTS = 64;              // 원형 링 분할 수
        const float RING_WIDTH = 0.06f;       // 링 선폭(m) — 규격 0.05~0.08
        const float ARROW_WIDTH = 0.08f;      // 전방 화살 선폭(m)
        const float Y_LIFT = 0.04f;           // 지면 여유 — 선폭 절반(0.03)보다 커서 아래 절반이 지면에 묻히지 않음
        const float BOW_HEAD_DEPTH = 1.0f;    // 활 화살촉 깊이(m) — 10m 원거리 선에서 식별 가능한 크기
        const float BOW_HEAD_HALF = 0.5f;     // 활 화살촉 좌우 반폭(m)
        const float MELEE_HEAD_DEPTH = 0.7f;  // 근접 화살촉 깊이(m) — 링 앞쪽 짧은 화살
        const float MELEE_HEAD_HALF = 0.4f;   // 근접 화살촉 좌우 반폭(m)

        /// <summary>스타일: 반투명 스카이블루 — 플랫 UI 톤 일치.</summary>
        static readonly Color IndicatorColor = new Color(0.35f, 0.65f, 0.90f, 0.55f);

        /// <summary>로컬 XY 평면(라인 경로) → 월드 XZ(지면), 로컬 Z(폭) → 월드 수직이 되는 회전.</summary>
        static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>절차 머티리얼 static 캐시 — HideAndDontSave, 파기 금지(씬 전환 재사용).</summary>
        static Material _sharedMat;

        LineRenderer _ring;
        LineRenderer _arrow;
        CharacterController _cc;
        WeaponType _lastType = WeaponType.Fist;
        float _radius;
        bool _isBow;
        bool _bootFailed;
        readonly Vector3[] _arrowPts = new Vector3[5]; // 전방 화살 폴리라인(매 프레임 재구성, 할당 없음)

        /// <summary>부착 헬퍼 — 중복 부착 방지. 플레이어 루트에 직접 AddComponent(숨은 호스트 아님).</summary>
        public static void EnsureOn(Transform playerRoot)
        {
            if (playerRoot == null) return;
            if (playerRoot.GetComponentInChildren<WeaponRangeIndicator>(true) != null) return;
            playerRoot.gameObject.AddComponent<WeaponRangeIndicator>();
            Debug.Log($"[WeaponRangeIndicator] ✅ 공격 범위 표시기 부착: {playerRoot.name}");
        }

        void Start()
        {
            try
            {
                _cc = GetComponent<CharacterController>();
                Build();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WeaponRangeIndicator] 생성 실패 — 비활성화: {e}");
                _bootFailed = true;
                enabled = false;
            }
        }

        void Build()
        {
            var mat = GetSharedMaterial();
            _ring = CreateLine("WeaponRange_Ring", RING_WIDTH, mat, transform);
            _arrow = CreateLine("WeaponRange_Front", ARROW_WIDTH, mat, transform);

            // 초기 타입 반영 — 부팅 시 이미 무기 장착 중이면 첫 LateUpdate 엣지에서 갱신+로그
            RefreshIfNeeded();
        }

        void LateUpdate()
        {
            if (_bootFailed || _ring == null || _arrow == null) return;
            try
            {
                RefreshIfNeeded();   // CurrentType 변화 엣지 → 반경/형상 갱신
                ApplyVisibility();   // 맨손(비장착) 숨김
                if (_ring.enabled)
                    TrackGround();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WeaponRangeIndicator] 갱신 실패 — 비활성화: {e}");
                _bootFailed = true;
                enabled = false;
            }
        }

        /// <summary>CurrentType 변화 엣지 감지 → 링 반경/전방 화살 모드 갱신 + 확인 로그 1줄.</summary>
        void RefreshIfNeeded()
        {
            var type = WeaponEquipManager.CurrentType;
            if (type == _lastType) return;
            _lastType = type;
            _radius = RangeOf(type);
            _isBow = type == WeaponType.Bow;

            // 링: 64분할 원 (로컬 XY 평면 → FlatRotation으로 월드 XZ 지면)
            var pts = new Vector3[SEGMENTS];
            for (int i = 0; i < SEGMENTS; i++)
            {
                float a = (float)i / SEGMENTS * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a) * _radius, 0f);
            }
            _ring.loop = true;
            _ring.positionCount = SEGMENTS;
            _ring.SetPositions(pts);

            Debug.Log($"[Range] 사거리 표시 갱신: {type} {_radius:0.#}m");
        }

        /// <summary>표시 조건: 무기 장착 중(CurrentType != Fist)만 표시, 맨손은 숨김.</summary>
        void ApplyVisibility()
        {
            bool show = _lastType != WeaponType.Fist;
            if (_ring.enabled != show) _ring.enabled = show;
            if (_arrow.enabled != show) _arrow.enabled = show;
        }

        /// <summary>플레이어 중심 추적 — 지면 y = 플레이어 발 위치, 전방 화살은 루트 forward로 매 프레임 재구성.</summary>
        void TrackGround()
        {
            var p = transform.position;
            float footY = p.y;
            if (_cc != null)
                footY -= _cc.height * 0.5f - _cc.center.y;   // 루트=캡슐 중심 계약 → 발 위치 보정
            footY += Y_LIFT;

            var center = new Vector3(p.x, footY, p.z);
            _ring.transform.position = center;
            _ring.transform.rotation = FlatRotation;         // 항상 수평(경사 기울임 무시)

            var fwd = transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            var side = new Vector3(fwd.z, 0f, -fwd.x);       // 전방 좌측 수직

            var tip = fwd * _radius;
            if (_isBow)
            {
                // 활: 중심 → 사거리 가장자리 전방 선(10m 길게) + 끝 V자 촉 (끝점 미닫음)
                _arrowPts[0] = Vector3.zero;
                _arrowPts[1] = tip;
                _arrowPts[2] = tip - fwd * BOW_HEAD_DEPTH + side * BOW_HEAD_HALF;
                _arrowPts[3] = tip - fwd * BOW_HEAD_DEPTH;
                _arrowPts[4] = tip - fwd * BOW_HEAD_DEPTH - side * BOW_HEAD_HALF;
            }
            else
            {
                // 근접: 링 앞쪽 짧은 화살촉(닫힌 마름모 외곽선) — 전방 방향 강조
                _arrowPts[0] = tip;
                _arrowPts[1] = tip - fwd * MELEE_HEAD_DEPTH + side * MELEE_HEAD_HALF;
                _arrowPts[2] = tip - fwd * (MELEE_HEAD_DEPTH + 0.1f);
                _arrowPts[3] = tip - fwd * MELEE_HEAD_DEPTH - side * MELEE_HEAD_HALF;
                _arrowPts[4] = tip;
            }

            _arrow.transform.position = center;
            _arrow.transform.rotation = FlatRotation;
            // 로컬 XY → 월드 XZ 매핑: (dx, dz) 오프셋 → (dx, dz, 0)
            for (int i = 0; i < _arrowPts.Length; i++)
                _arrowPts[i] = new Vector3(_arrowPts[i].x, _arrowPts[i].z, 0f);
            _arrow.SetPositions(_arrowPts);
        }

        /// <summary>타입별 사거리 — WeaponData 정적 스탯 단일 소스(타입 고정, 등급 배율 무관).</summary>
        static float RangeOf(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Sword: return WeaponData.Sword.range;   // 2.5m
                case WeaponType.Spear: return WeaponData.Spear.range;   // 4m
                case WeaponType.Bow:   return WeaponData.Bow.range;     // 10m
                default:               return WeaponData.Fist.range;    // 2m
            }
        }

        /// <summary>지면 부착용 LineRenderer 생성 — 플레이어 자식(호스트와 생애주기 동일), 로컬 XY 경로 + TransformZ 정렬(폭축 = 로컬 Z = 월드 수직).</summary>
        static LineRenderer CreateLine(string goName, float width, Material mat, Transform parent)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, worldPositionStays: false); // 플레이어 자식 부착 — 위치/회전은 TrackGround에서 월드 기준 강제
            go.hideFlags = HideFlags.None;                             // 숨은 호스트 아님 — 일반 오브젝트

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sharedMaterial = mat;
            lr.startColor = Color.white;   // 정점색 흰색 유지 → 머티리얼 틴트(IndicatorColor)가 최종색
            lr.endColor = Color.white;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.enabled = false;            // RefreshIfNeeded/ApplyVisibility가 타입 엣지에서 제어
            return lr;
        }

        /// <summary>Sprites/Default(URP 호환) 절차 머티리얼 — static 캐시, 파기 금지 관례.</summary>
        static Material GetSharedMaterial()
        {
            if (_sharedMat != null) return _sharedMat;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new System.InvalidOperationException("Shader 'Sprites/Default' 미발견");
            _sharedMat = new Material(shader);
            _sharedMat.name = "WeaponRangeIndicator_Mat";
            _sharedMat.hideFlags = HideFlags.HideAndDontSave;   // 절차 리소스 관례(에셋 의존 없음)
            _sharedMat.color = IndicatorColor;
            return _sharedMat;
        }
    }
}
