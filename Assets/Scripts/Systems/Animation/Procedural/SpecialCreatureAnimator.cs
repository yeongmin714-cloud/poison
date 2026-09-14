using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems.Animation.Procedural
{
    /// <summary>
    /// 특수 생물(거미, 조개, 슬라임, 숲정령, 대형몬스터) 전용 프로시저럴 애니메이터.
    /// ModelAnimatorAssigner / MonsterSpawner에서 비4족·비2족 감지 시 부착됨.
    ///
    /// [2026-09-14(몬스터 애니 49차)] 비어 있던 Update() 뼈대를 CreatureType별 고유 몸놀림으로 구현.
    ///  - 이동 자체는 AnimalAI가 transform.position으로 처리하므로, 이 컴포넌트는 루트 위치를 절대 쓰지 않고
    ///    본(ProceduralBoneMap) 로컬 transform / 스케일 / 회전만으로 종별 모션을 표현한다.
    ///  - 본이 매핑되지 않은 GLB(또는 프리미티브 폴백 몬스터)는 자기 transform 스케일·자식 바디 부유로 최소 표현한다.
    ///  - 모든 종은 항상 동작한다(이동 여부와 무관하게 최소한의 몸놀림 유지). 보행 4족은 이 컴포넌트에 부착되지 않는다.
    ///  - [2026-09-14(49차 후속)] 이동 피드: AnimalAI가 SetMoving/SetMoveSpeed를 호출하지 않아도
    ///    LateUpdate에서 루트 위치 프레임 델타로 실속도를 '읽기만' 자체 산출한다(transform 쓰기 없음 → 이동 무충돌).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class SpecialCreatureAnimator : MonoBehaviour
    {
        public enum CreatureType { Spider, Clam, Slime, Spirit, LargeMonster }

        [Header("Creature Type")]
        public CreatureType creatureType = CreatureType.Spider;

        [Header("Locomotion")]
        [SerializeField] float _moveSpeed = 3f;    // 기준 최대 이동속도(피드 속도 정규화 분모)
        [SerializeField] float _turnSpeed = 360f;

        [Header("Motion Tuning (49차)")]
        [SerializeField] float _slimePulseFreq = 2.2f;   // 슬라임 점액 펄스 주파수(Hz)
        [SerializeField] float _spiritFloatFreq = 1.4f;  // 숲정령 부유 주파수
        [SerializeField] float _spiritGlowFreq = 1.8f;   // 숲정령 발광 펄스 주파수
        [SerializeField] float _clamCycleFreq = 0.5f;    // 조개 여닫이 주파수(느리게)
        [SerializeField] float _flapFreq = 6f;           // 날갯짓 주파수(박쥐/까마귀류)
        [SerializeField] float _crawlFreq = 5f;          // 거미 다리 보행 주파수

        Animator _animator;
        ProceduralBoneMap _boneMap;
        Rigidbody _rigidbody;

        // ── [2026-09-14(49차 후속)] 이동 상태 피드 — 2경로 하이브리드.
        //    1) AnimalAI가 SetMoving/SetMoveSpeed를 호출하면 그 값 우선(기존 public API 유지).
        //    2) 미호출이면 LateUpdate가 루트 위치 프레임 델타로 실속도를 '읽기만' 자체 산출한다.
        //       (transform 쓰기 없음 → AnimalAI position 직접 이동과 무충돌)
        bool _isMoving;      // SetMoving()으로 피드
        float _fedSpeed;     // SetMoveSpeed()로 피드된 실속도(미피드 0)
        float _moveT;        // 이동 블렌드 0~1(급격한 모션 전환 방지)

        // ── [49차 후속] 자체 산출 이동 상태(루트 위치 '읽기' 전용)
        Vector3 _lastRootPos;                  // 직전 프레임 루트 위치
        bool _lastRootPosValid;                // 첫 프레임/풀링 직후 델타 스킵 플래그
        float _selfSpeed;                      // 자체 산출 수평 실속도(m/s, 스무딩)
        Vector3 _selfVelDir = Vector3.forward; // 자체 산출 이동 방향(수평, 정규화)

        // ── [49차] 파라미터 진행용 누적 위상 — Time.time/deltaTime 기반(프레임레이트 독립)
        float _phase;

        // ── 모션 적용 대상 바디 캐시 (루트본 → 렌더러 자식 → 자기 transform 순 폴백)
        Transform _body;
        bool _bodyIsSelf;                 // 폴백으로 자기 transform을 쓰는 경우(위치/회전 쓰기 금지 — AnimalAI 충돌 방지)
        Vector3 _bodyBasePos, _bodyBaseScale;
        Quaternion _bodyBaseRot;

        // ── 거미 다리 / 박쥐·까마귀 날개 본 캐시 (본 부재 시 목록이 비어 폴백 경로 사용)
        readonly List<Transform> _legBones = new List<Transform>();
        readonly List<Quaternion> _legBaseRots = new List<Quaternion>();
        Transform _lWing, _rWing;
        Quaternion _lWingBaseRot, _rWingBaseRot;

        // ── [49차] 숲정령 발광 펄스용 머티리얼 인스턴스(프로젝트 공유 머티리얼 오염 방지)
        Material[] _glowMats;
        Color[] _glowBaseEmissions;
        bool _glowReady;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _rigidbody = GetComponent<Rigidbody>();

            // [49차] 런타임 AddComponent(프리미티브 폴백 몬스터)에서는 Animator가 없을 수 있다 — null 가드 필수
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.updateMode = AnimatorUpdateMode.Fixed;
                _animator.animatePhysics = true;
            }
            if (_boneMap != null) _boneMap.Initialize(_animator); // animator null이어도 Initialize가 자체 폴백 처리
        }

        void Start()
        {
            CacheBody();
            CacheLimbs();

            // [49차] 숲정령만 발광 펄스용 머티리얼 인스턴스 준비(공유 머티리얼 보호)
            if (creatureType == CreatureType.Spirit) SetupGlowMaterials();
        }

        void OnEnable()
        {
            // [2026-09-14(49차 후속)] 활성 직후 첫 프레임 델타 스킵 —
            // 스폰/풀링 위치 스냅이 속도 스파이크로 오인되는 것을 방지한다.
            _lastRootPos = transform.position;
            _lastRootPosValid = false;
        }

        /// <summary>
        /// [2026-09-14(49차 후속)] 이동 피드 자체 산출 — LateUpdate에서 루트 위치 프레임 델타로 실속도를 계산.
        /// AnimalAI 호출 없이도 슬라임 이동 부스트/거미 주기 가속/LargeMonster 기울임이 실제 이동에 반응한다.
        /// 위치는 절대 쓰지 않는다(읽기 전용). 정지 시 속도는 스무딩에 의해 0으로 자연 감쇠.
        /// </summary>
        void LateUpdate()
        {
            if (_lastRootPosValid && Time.deltaTime > 1e-5f)
            {
                Vector3 delta = transform.position - _lastRootPos;
                delta.y = 0f; // 수평 속도만 — 점프/중력 튐이 모션 피드를 오염하지 않게 한다
                float rawSpeed = delta.magnitude / Time.deltaTime;

                // 스무딩: 이동 개시 시 램프업, 정지 시 자연 감쇠
                _selfSpeed = Mathf.Lerp(_selfSpeed, rawSpeed, Mathf.Clamp01(Time.deltaTime * 10f));

                if (rawSpeed > 0.05f)
                {
                    Vector3 dir = delta.normalized;
                    _selfVelDir = Vector3.Slerp(_selfVelDir, dir, Mathf.Clamp01(Time.deltaTime * 8f));
                    if (_selfVelDir.sqrMagnitude < 1e-4f) _selfVelDir = dir; // 정반대 방향 전환 시 수치 보호
                    else _selfVelDir.Normalize();
                }
            }

            _lastRootPos = transform.position;
            _lastRootPosValid = true;
        }

        void Update()
        {
            if (_body == null) return; // 파괴/풀링 후 안전 가드

            // [49차 후속] 파라미터 진행 — 실속도(외부 피드 or 자체 델타 산출 중 큰 값)가 있으면 주기가 빨라진다.
            float effSpeed = Mathf.Max(_fedSpeed, _selfSpeed);
            float speed01 = Mathf.Clamp01(effSpeed / Mathf.Max(0.01f, _moveSpeed));
            _phase += Time.deltaTime * (1f + speed01 * 1.5f);
            _moveT = Mathf.MoveTowards(_moveT, (_isMoving || effSpeed > 0.01f) ? 1f : 0f, Time.deltaTime * 4f);

            switch (creatureType)
            {
                case CreatureType.Slime: AnimateSlime(); break;
                case CreatureType.Spirit: AnimateSpirit(); break;
                case CreatureType.Clam: AnimateClam(); break;
                case CreatureType.Spider: AnimateSpider(); break;
                case CreatureType.LargeMonster: AnimateLargeMonster(); break;
            }
        }

        // ════════════════════════ 종별 모션 (49차) ════════════════════════

        /// <summary>
        /// [49차] 슬라임 — 부드러운 점액 펄스: 스케일 y 0.9↔1.1 사인 반복.
        /// 부피감을 위해 xz는 역위상으로 살짝 수축. 이동 중(피드 시)엔 진폭이 커져
        /// 수직으로 뻗었다 찌그러지는 '점프 기대기' 주기가 강조된다.
        /// </summary>
        void AnimateSlime()
        {
            float pulse = Mathf.Sin(_phase * _slimePulseFreq * 2f * Mathf.PI);   // -1..1
            float sy = 1f + 0.1f * pulse;                                        // 0.9 ↔ 1.1
            float sxz = 1f - 0.06f * pulse;

            // 이동 블렌드: 점프 기대기 진폭 강화
            float boost = 1f + _moveT * 0.8f;
            sy = 1f + (sy - 1f) * boost;

            // 진행 방향 스쿼시: 자체 산출 이동 방향(루트 델타) 우선 — AnimalAI transform 직접 이동 중에도 반영.
            // 리지드바디 속도는 비AI 물리 이동 폴백. (구 velocity API는 Unity 6 CS0618 → linearVelocity 교체)
            Vector3 moveDirLocal = Vector3.zero;
            if (_selfSpeed > 0.1f && _selfVelDir.sqrMagnitude > 0.01f)
            {
                moveDirLocal = transform.InverseTransformDirection(_selfVelDir);
                moveDirLocal.y = 0f;
            }
            else if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 v = _rigidbody.linearVelocity; v.y = 0f;
                if (v.sqrMagnitude > 0.05f)
                {
                    moveDirLocal = transform.InverseTransformDirection(v.normalized);
                    moveDirLocal.y = 0f;
                }
            }

            Vector3 scale = new Vector3(sxz, sy, sxz);
            if (moveDirLocal.sqrMagnitude > 0.01f)
            {
                // 진행 방향 축으로 찌그러지고 수직으로 뻗는다(점프 기대)
                scale.x -= moveDirLocal.x * 0.15f * _moveT;
                scale.z -= moveDirLocal.z * 0.15f * _moveT;
                scale.y += 0.1f * _moveT;
            }
            ApplyBodyScale(scale);
        }

        /// <summary>
        /// [49차] 숲정령 — 떠다니는 부유 + 발광 펄스.
        /// 본 Root(폴백: 렌더러 자식)를 Sin으로 +y 0.2~0.4 부유시키고, 인스턴스 머티리얼의
        /// 이미션 강도를 Sin으로 펄스한다(공유 머티리얼은 절대 건드리지 않음).
        /// 본/자식이 전무하면 스케일 호흡으로 최소 표현한다.
        /// </summary>
        void AnimateSpirit()
        {
            // 부유: +y 0.2~0.4 (사인 상승 하강)
            float bob = 0.2f + 0.2f * (Mathf.Sin(_phase * _spiritFloatFreq * 2f * Mathf.PI) * 0.5f + 0.5f);

            if (!_bodyIsSelf)
            {
                _body.localPosition = _bodyBasePos + Vector3.up * bob;
                // 정령 기품: 미세 좌우 흔들림(y축 소폭 회전)
                _body.localRotation = _bodyBaseRot * Quaternion.Euler(0f, Mathf.Sin(_phase * 0.7f) * 4f, 0f);
            }
            else
            {
                // 폴백: 루트 위치는 AnimalAI가 쓰므로 스케일 호흡만
                float s = 1f + 0.04f * Mathf.Sin(_phase * _spiritFloatFreq * 2f * Mathf.PI);
                ApplyBodyScale(new Vector3(s, s, s));
            }

            // 발광 펄스 — 인스턴스 머티리얼 이미션 강도만 변경(투명 렌더모드 변경 없이 안전)
            if (_glowReady)
            {
                float glow = 0.5f + 0.5f * Mathf.Sin(_phase * _spiritGlowFreq * 2f * Mathf.PI);
                for (int i = 0; i < _glowMats.Length; i++)
                    _glowMats[i].SetColor("_EmissionColor", _glowBaseEmissions[i] * (0.25f + 0.75f * glow));
            }
        }

        /// <summary>
        /// [49차] 조개 — 스케일 y로 열림(1.0)↔닫힘(0.3) 여닫이 주기 반복.
        /// 박쥐류처럼 날개(어깨) 본이 매핑된 GLB는 조개 여닫이 대신 날개 펄럭으로 대체한다.
        /// </summary>
        void AnimateClam()
        {
            if (TryFlapWings()) return;   // 박쥐류 폴백: 날개 본 보유 시 펄럭

            float open = 0.5f + 0.5f * Mathf.Sin(_phase * _clamCycleFreq * 2f * Mathf.PI);   // 0..1
            float sy = Mathf.Lerp(0.3f, 1.0f, open);                                         // 닫힘 0.3 ↔ 열림 1.0
            ApplyBodyScale(new Vector3(1f, sy, 1f));
        }

        /// <summary>
        /// [49차] 거미(및 기본 폴백 타입: 박쥐/까마귀/독뱀 등) —
        /// 다리 본이 매핑되면 좌우 번갈아 관절 회전 보행, 없으면 날개 본(박쥐/까마귀) 펄럭,
        /// 그마저 없으면 자식 바디 미세 부유 또는 스케일 호흡으로 최소 표현.
        /// </summary>
        void AnimateSpider()
        {
            if (_legBones.Count >= 2) { CrawlLegs(); return; }
            if (TryFlapWings()) return;              // 박쥐/까마귀류: 다리 대신 날개 본
            FallbackBob(0.06f);                      // 본 전무 폴백(독뱀 등): 미세 부유/호흡
        }

        /// <summary>[49차] 거미 다리 보행 — 좌우 교차 위상, 무릎은 엉덩이보다 뒤늦게 따라옴.</summary>
        void CrawlLegs()
        {
            for (int i = 0; i < _legBones.Count; i++)
            {
                Transform leg = _legBones[i];
                if (leg == null) continue;

                float side = (i % 2 == 0) ? 1f : -1f;             // 좌/우 교차
                float jointDelay = (i >= 2) ? 0.9f : 0f;          // 무릎(L/R_Knee)은 위상 지연
                float swing = Mathf.Sin(_phase * _crawlFreq * 2f * Mathf.PI + jointDelay) * 14f * side;
                leg.localRotation = _legBaseRots[i] * Quaternion.Euler(swing, 0f, 0f);
            }

            // 보행 리듬에 맞춘 몸통 미세 상하 진동(본/자식 바디가 있을 때만)
            if (!_bodyIsSelf)
                _body.localPosition = _bodyBasePos + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase * _crawlFreq * Mathf.PI)) * 0.03f);
        }

        /// <summary>
        /// [49차 후속] 대형몬스터 — 실측 이동 속도(자체 델타 산출 우선, 외부 피드/리지드바디 폴백)가 있으면
        /// 진행 방향으로 몸을 살짝 기울인다. 정지 시 기본 자세 유지.
        /// </summary>
        void AnimateLargeMonster()
        {
            // [49차 후속] 자체 산출 실속도 우선 — AnimalAI transform 직접 이동 중에도 기울임이 발동한다.
            // 없으면 리지드바디 속도(비AI 물리 이동) 폴백. (구 velocity API는 linearVelocity로 교체 — CS0618)
            Vector3 vel = _selfVelDir * _selfSpeed;
            if (vel.sqrMagnitude < 0.01f && _rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 rb = _rigidbody.linearVelocity; rb.y = 0f;
                vel = rb;
            }

            float tiltDeg = 0f;
            float speed = vel.magnitude;
            if (speed > 0.2f)
            {
                Vector3 local = transform.InverseTransformDirection(vel.normalized);
                // 전진(+z) 성분에 비례해 앞으로 최대 8° 기울임
                tiltDeg = Mathf.Clamp(local.z, -1f, 1f) * 8f * Mathf.Clamp01(speed / 4f);
            }

            if (!_bodyIsSelf)
                _body.localRotation = _bodyBaseRot * Quaternion.Euler(tiltDeg, 0f, 0f);
        }

        // ════════════════════════ 공용 헬퍼 (49차) ════════════════════════

        /// <summary>[49차] 날개 본(좌우 어깨 매핑)이 있으면 펄럭이고 true 반환 — 박쥐/까마귀류 공용.</summary>
        bool TryFlapWings()
        {
            if (_lWing == null || _rWing == null) return false;

            float flap = Mathf.Sin(_phase * _flapFreq * 2f * Mathf.PI) * 32f;   // ±32°
            flap *= 1f + 0.5f * _moveT;                                        // 이동 중 날갯짓 강화
            _lWing.localRotation = _lWingBaseRot * Quaternion.Euler(0f, 0f, flap);
            _rWing.localRotation = _rWingBaseRot * Quaternion.Euler(0f, 0f, -flap);
            return true;
        }

        /// <summary>[49차] 바디 스케일 적용 — 기준 스케일 대비 배수(매 프레임 덮어쓰기로 누적 오차 없음).</summary>
        void ApplyBodyScale(Vector3 scaleMul)
        {
            if (_body == null) return;
            _body.localScale = Vector3.Scale(_bodyBaseScale, scaleMul);
        }

        /// <summary>[49차] 본 전무 폴백 — 자식 바디가 있으면 미세 위치 부유, 자기 transform뿐이면 스케일 호흡.</summary>
        void FallbackBob(float amp)
        {
            if (!_bodyIsSelf)
                _body.localPosition = _bodyBasePos + Vector3.up * (Mathf.Sin(_phase * 2f * Mathf.PI) * amp);
            else
            {
                float s = 1f + amp * 0.6f * Mathf.Sin(_phase * 2f * Mathf.PI);
                ApplyBodyScale(new Vector3(s, s, s));
            }
        }

        // ════════════════════════ 캐싱 / 정리 (49차) ════════════════════════

        /// <summary>[49차] 모션 적용 대상 바디 결정: 본 Root → 렌더러 자식 → 자기 transform(스케일 전용) 폴백.</summary>
        void CacheBody()
        {
            Transform t = (_boneMap != null) ? _boneMap.Get(BoneRole.Root) : null;

            if (t == null)
            {
                // 본 미매핑(GLB 자동 매핑 실패 or 프리미티브): 렌더러가 붙은 첫 자식을 몸통으로 사용
                Renderer rend = GetComponentInChildren<Renderer>();
                if (rend != null) t = rend.transform;
            }

            _body = (t != null) ? t : transform;
            _bodyIsSelf = (_body == transform);   // 자기 transform이면 위치/회전 쓰기 금지(AnimalAI 이동 충돌)
            _bodyBasePos = _body.localPosition;
            _bodyBaseScale = _body.localScale;
            _bodyBaseRot = _body.localRotation;
        }

        /// <summary>[49차] 다리/날개 본 프리캐시 — 본 부재 시 목록이 비어 폴백 경로가 동작한다.</summary>
        void CacheLimbs()
        {
            _legBones.Clear();
            _legBaseRots.Clear();
            AddLegBone(BoneRole.L_Hip);
            AddLegBone(BoneRole.R_Hip);
            AddLegBone(BoneRole.L_Knee);
            AddLegBone(BoneRole.R_Knee);

            _lWing = (_boneMap != null) ? _boneMap.Get(BoneRole.L_Shoulder) : null;
            _rWing = (_boneMap != null) ? _boneMap.Get(BoneRole.R_Shoulder) : null;
            if (_lWing != null) _lWingBaseRot = _lWing.localRotation;
            if (_rWing != null) _rWingBaseRot = _rWing.localRotation;
        }

        void AddLegBone(BoneRole role)
        {
            Transform t = (_boneMap != null) ? _boneMap.Get(role) : null;
            if (t == null) return;   // try 폴백: 해당 본 없으면 조용히 건너뜀
            _legBones.Add(t);
            _legBaseRots.Add(t.localRotation);
        }

        /// <summary>
        /// [49차] 숲정령 발광 펄스용 머티리얼 인스턴스 준비.
        /// renderer.materials 접근은 Unity가 자동으로 인스턴스를 만들어 프로젝트 공유 머티리얼은 오염되지 않는다.
        /// </summary>
        void SetupGlowMaterials()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) { _glowReady = false; return; }

            var mats = new List<Material>();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                // .materials 접근 시점에 인스턴스 복제됨(공유 안전)
                Material[] inst = r.materials;
                foreach (Material m in inst)
                {
                    if (m == null) continue;
                    m.EnableKeyword("_EMISSION");
                    mats.Add(m);
                }
            }
            if (mats.Count == 0) { _glowReady = false; return; }

            _glowMats = mats.ToArray();
            _glowBaseEmissions = new Color[_glowMats.Length];
            for (int i = 0; i < _glowMats.Length; i++)
            {
                Color baseEmis = _glowMats[i].HasProperty("_EmissionColor")
                    ? _glowMats[i].GetColor("_EmissionColor")
                    : Color.white;
                // 이미션 미설정 머티리얼은 숲정령 녹색 발광을 기본값으로 부여
                _glowBaseEmissions[i] = (baseEmis == Color.black) ? new Color(0.6f, 1f, 0.7f, 1f) : baseEmis;
            }
            _glowReady = true;
        }

        /// <summary>[49차] 비활성/파괴 시 바디 원복(풀링 재사용 대응).</summary>
        void OnDisable()
        {
            RestoreBody();
        }

        /// <summary>[49차] 인스턴스 머티리얼 폐기 + 바디 원복. 공유 머티리얼은 원본 그대로.</summary>
        void OnDestroy()
        {
            if (_glowMats != null)
            {
                for (int i = 0; i < _glowMats.Length; i++)
                {
                    if (_glowMats[i] != null) Destroy(_glowMats[i]);
                }
                _glowMats = null;
            }
            RestoreBody();
        }

        /// <summary>[49차] 바디를 원래 로컬 자세로 복원. 자기 transform은 스케일만(위치/회전은 AnimalAI 소유).</summary>
        void RestoreBody()
        {
            if (_body == null) return;
            _body.localScale = _bodyBaseScale;
            if (_bodyIsSelf) return;
            _body.localPosition = _bodyBasePos;
            _body.localRotation = _bodyBaseRot;
        }

        // ══════════ 외부 피드 API (49차) — 미호출 시 LateUpdate 자체 델타 산출이 뒤받친다 [49차 후속] ══════════

        /// <summary>[2026-09-14(49차)] AnimalAI 이동 시작/정지 피드. 미호출 시에도 자체 모션은 항상 동작.</summary>
        public void SetMoving(bool moving)
        {
            _isMoving = moving;
        }

        /// <summary>[2026-09-14(49차)] AnimalAI 실이동 속도 피드(m/s). 주기 가속·스쿼시 강도에 반영.</summary>
        public void SetMoveSpeed(float speed)
        {
            _fedSpeed = Mathf.Max(0f, speed);
        }
    }
}
