// Test_11_AnimationShowcase (2026-09-22): 쇼케이스 애니메이션 가시성 보증 + 동결 진단.
// 뿌리(진단): GLB 동물 리그가 익명 뼈(bone_0..N)+임베디드 애니 0개 → ProceduralBoneMap 이름 매칭 3뼈뿐
//   → QuadrupedProceduralAnimation 다리 IK 대상(L_Foot/R_Foot/Knee) 미매핑 → 보행 애니 구동 불가(동결).
// 목적: 본 매핑 성공 여부와 무관하게 "화면에서 움직임"을 보증하는 관측자+폴백.
//   ① 0.5초 간격 관측 — 이동 중(ShowcaseWanderDriver가 position 변경) + 대표 본 localRotation Δ 무변화가
//      2.5초 연속 지속되면 폴백 호흡/걸음 진동을 구동하고 1회 경고 로그(진단 자산).
//   ② 폴백은 대표 본(rootBone→bones[0])의 localRotation에 sin 기반 미세 롤/피치만 곱한다.
//      루트 transform.position/rotation은 절대 건드리지 않는다(ShowcaseWanderDriver 소유 규약).
//   ③ HumanoidClip(병사/NPC)은 SoldierShield_AC가 Idle을 재생하는 정식 경로라 골격 오염 방지를 위해
//      무변화 시 경고 로그만 남기고 폴백 미적용.
// [P-ANIM6 (2026-09-22)] 진단 신뢰성 재설계: ①관측 본을 실제 구동 본으로 — Setup 오버로드로 전달받은
//   ProceduralBoneMap 워처 본(다리 체인/척추)을 직접 관측한다(SMR bones[0]/중앙 추정은 구동 본을
//   놓쳐 IK 정상 작동 중에도 폴백 오탐). ②폴백 스티키 해제 — 활성 중 3초 재검증으로 애니 정상화가
//   확인되면 폴백 해제+자세 복원(폴백이 흔드는 본의 Δ는 관측 제외해 자기 구동 오염 차단).
//   ③Special 스케일 관측 교정 — SpecialCreatureAnimator.CacheBody 미러링(Root 본 → 렌더러 자식
//   → 자기 transform)으로 실제 펄스 대상을 관측(SMR transform은 펄스 대상이 아니라 오탐 원천).
#pragma warning disable 0414
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_11_AnimationShowcase 씬 전용 애니메이션 가시성 보증 컴포넌트.
    /// 각 유닛(몬스터/병사/NPC)에 부착되어 본 변위를 관측하고, 무변화 동결을 감지하면
    /// 4족/2족/특수형 몬스터에 한해 폴백 호흡 애니로 시각적 움직임을 보장한다.
    /// </summary>
    public class ShowcaseMonitor : MonoBehaviour
    {
        public enum MonitorFamily { Quadruped, Biped, Special, HumanoidClip }

        [Header("Observation")]
        [SerializeField] private float _observeInterval = 0.5f;
        [SerializeField] private float _freezeThreshold = 2.5f;   // 이동 중 연속 무변화 판정 시간
        [SerializeField] private float _revalidateInterval = 3f;  // [P-ANIM6] 폴백 활성 중 재검증 주기

        [Header("Fallback Breath (몬스터 전용)")]
        [SerializeField] private float _moveAmpDeg = 2.5f;        // 이동 중 진폭
        [SerializeField] private float _moveHz = 2.2f;
        [SerializeField] private float _idleAmpDeg = 0.8f;        // 정지 중(호흡)
        [SerializeField] private float _idleHz = 1.1f;

        private string _label = "";
        private MonitorFamily _family = MonitorFamily.Quadruped;

        // 관측 상태
        private Vector3 _lastPos;
        private bool _lastObservedMoving;   // Update 관측 결과 — LateUpdate 폴백 진폭 분기용
        private float _observeTimer;
        private float _noChangeElapsed;
        private bool _fallbackActive;
        private bool _warnedFreeze;

        // 애니 변화 관측 대상 본
        // [P-ANIM6] 워처 목록 — Setup 오버로드로 전달된 실제 구동 본(다리 체인/척추)을 직접 관측.
        // 비어 있으면 기존 SMR 추정(_monitorBoneA/B) 경로로 폴백(매핑 없는 리그/휴머노이드 대비).
        private readonly List<Transform> _watchBones = new List<Transform>();
        private readonly List<Quaternion> _watchLastRots = new List<Quaternion>();
        private Transform _monitorBoneA;    // SMR 추정 폴백 경로 — 워처 목록이 비어 있을 때만 사용
        private Transform _monitorBoneB;
        private Quaternion _lastRotA = Quaternion.identity;
        private Quaternion _lastRotB = Quaternion.identity;
        private Vector3 _lastScale;
        private Transform _scaleProbe;  // [P-ANIM6] Special: 펄스 대상(Root 본) / 타 계열: SMR transform
        private bool _hasScaleProbe;

        // 폴백 재검증(P-ANIM6) — 스티키 폴백 해제용
        private float _revalidateTimer;        // 재검증 주기 누적
        private bool _revalidateAnimChanged;   // 재검증 주기 내 관측된 애니 변화(폴백 본 Δ 제외 상태)

        // 폴백 대상 본 + 기준 회전
        private Transform _breathBone;
        private Quaternion _breathBaseRot = Quaternion.identity;
        private bool _breathReady;

        private void Start()
        {
            _lastPos = transform.position;
            _observeTimer = 0f;

            // 관측/폴백 본 프리캐시 — 첫 SkinnedMeshRenderer의 뼈 사용(익명 리그 포함 무이름 의존 없음)
            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null && skinned.Length > 0)
            {
                // [P-ANIM5 수리] 관측 본 확장(앞/뒤 계열 2본) — bones[0]만 봐서는 실제 다리 IK가 움직이는 뼈를
                // 놓쳐 전 몬스터 폴백 오탐이 발생했다(중앙 부본은 후방 본이 있으면 덮어써진다).
                // [P-ANIM6] 워처 본이 전달되면 이 추정 경로는 사용하지 않는다(구동 본 직접 관측).
                var smr = skinned[0];
                Transform probe = smr.rootBone != null ? smr.rootBone
                    : (smr.bones != null && smr.bones.Length > 0 ? smr.bones[0] : null);

                if (probe != null)
                {
                    _monitorBoneA = probe;
                    _lastRotA = probe.localRotation;

                    // 중앙 부본 — 뼈 목록 중앙(스파인 계열 추정)
                    if (smr.bones != null && smr.bones.Length > 3)
                    {
                        var mid = smr.bones[smr.bones.Length / 2];
                        if (mid != null && mid != probe)
                        {
                            _monitorBoneB = mid;
                            _lastRotB = mid.localRotation;
                        }
                    }

                    // 후방 본 — 뼈 목록 끝(다리/꼬리 계열 추정)
                    if (smr.bones != null && smr.bones.Length > 2)
                    {
                        var tail = smr.bones[smr.bones.Length - 1];
                        if (tail != null && tail != probe && tail != _monitorBoneB)
                        {
                            _monitorBoneB = tail;
                            _lastRotB = tail.localRotation;
                        }
                    }

                    _breathBone = probe;
                    _breathBaseRot = probe.localRotation;
                    _breathReady = true;
                }
                else
                {
                    // SMR 뼈 전무 — 스케일 관측으로 대체(프리미티브 폴백 몬스터 등)
                    _hasScaleProbe = true;
                    _lastScale = transform.localScale;
                }

                // [2026-09-22] 스케일 관측 프루브 — 뼈 유무 무관 SMR transform을 관측(SpecialCreature 펄스)
                // [P-ANIM6] Special 계열은 펄스 실제 대상으로 교체(아래) — SMR transform은 오탐 원천.
                _scaleProbe = smr.transform;
                _lastScale = _scaleProbe.localScale;
                _hasScaleProbe = true;
            }
            else
            {
                _hasScaleProbe = true;
                _lastScale = transform.localScale;
            }

            // [P-ANIM6 ③] Special(slime 등) 스케일 관측 교정 — SpecialCreatureAnimator.CacheBody와
            // 동일한 해석(본 Root → 렌더러 자식 → 자기 transform 폴백)으로 실제 펄스 대상을 관측한다.
            // 기존 SMR transform은 펄스를 하지 않는 대상이라 무변화 오판(폴백 오탐)의 원천이었다.
            if (_family == MonitorFamily.Special)
            {
                Transform pulseTarget = null;
                var boneMap = GetComponent<ProceduralBoneMap>();
                if (boneMap != null && boneMap.Has(BoneRole.Root))
                    pulseTarget = boneMap.Get(BoneRole.Root);
                if (pulseTarget == null)
                {
                    Renderer rend = GetComponentInChildren<Renderer>();
                    if (rend != null) pulseTarget = rend.transform;
                }
                _scaleProbe = pulseTarget != null ? pulseTarget : transform;
                _lastScale = _scaleProbe.localScale;
                _hasScaleProbe = true;
            }
        }

        /// <summary>라벨/계열 설정. TestAnimationShowcaseSetup이 생성 직후 호출.</summary>
        public void Setup(string label, MonitorFamily family)
        {
            _label = label;
            _family = family;
        }

        /// <summary>
        /// [P-ANIM6] 라벨/계열 + 구동 본 관측 목록 설정.
        /// 워처 본은 셋업 쪽에서 ProceduralBoneMap 매핑 결과(L_Hip/R_Hip/L_HindHip/R_HindHip/Spine0)로
        /// 추출한 실제 구동 본 — 이 목록이 비어 있으면 기존 SMR 추정 경로로 폴백한다.
        /// </summary>
        public void Setup(string label, MonitorFamily family, Transform[] watchBones)
        {
            _label = label;
            _family = family;

            _watchBones.Clear();
            _watchLastRots.Clear();
            if (watchBones == null) return;

            foreach (var bone in watchBones)
            {
                if (bone == null || _watchBones.Contains(bone)) continue;
                _watchBones.Add(bone);
                _watchLastRots.Add(bone.localRotation);
            }
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_label)) return;

            _observeTimer += Time.deltaTime;
            if (_observeTimer < _observeInterval) return;
            _observeTimer = 0f;

            // (a) 이동 감지 — 수평 Δ (ShowcaseWanderDriver의 배회/정지)
            Vector3 deltaPos = transform.position - _lastPos;
            deltaPos.y = 0f;
            bool moving = deltaPos.sqrMagnitude > 0.000025f; // 0.005m 이상
            _lastPos = transform.position;
            _lastObservedMoving = moving;

            // (b) 폴백 활성 중 — 스티키 해제 재검증(3초 주기).
            //     폴백이 스스로 흔드는 본(_breathBone)의 Δ는 관측에서 제외 — 자기 구동 오염 차단.
            //     재검증 주기 내 정상 애니의 변화가 확인되면 폴백을 해제하고 자세를 복원한다.
            if (_fallbackActive)
            {
                float revalidateDelta = ObserveAnimDelta(excludeBreathBone: true);
                if (revalidateDelta > 0.05f) _revalidateAnimChanged = true;

                _revalidateTimer += _observeInterval;
                if (_revalidateTimer >= _revalidateInterval)
                {
                    _revalidateTimer = 0f;
                    if (_revalidateAnimChanged)
                    {
                        _fallbackActive = false;
                        _noChangeElapsed = 0f;
                        _revalidateAnimChanged = false;
                        if (_breathReady && _breathBone != null)
                            _breathBone.localRotation = _breathBaseRot; // 폴백 자세 잔존 방지 복원
                        Debug.Log($"[ShowcaseMonitor] 폴백 해제 — 애니 정상화: {_label} (family={_family})");
                    }
                }
                return; // 폴백 중엔 무변화 누적/재발동 판정 생략
            }

            // (c) 애니 변화 감지 — 관측 본 localRotation Δ 합(도) 또는 스케일 Δ
            float angleDelta = ObserveAnimDelta(excludeBreathBone: false);
            bool animChanged = angleDelta > 0.05f;

            // (d) 무변화 누적 — 이동 중에만 판정(정지 중 무변화는 정상 idle)
            if (moving && !animChanged)
            {
                _noChangeElapsed += _observeInterval;

                if (!_fallbackActive && _noChangeElapsed >= _freezeThreshold)
                {
                    bool canFallback = _family != MonitorFamily.HumanoidClip && _breathReady;
                    if (canFallback)
                    {
                        _fallbackActive = true;
                        _revalidateTimer = 0f;      // [P-ANIM6] 재검증 주기 초기화
                        _revalidateAnimChanged = false;
                        Debug.LogWarning(
                            $"[ShowcaseMonitor] ⚠️ 애니 무변화 감지 — 폴백 호흡 구동: {_label} (family={_family})");
                    }
                    else if (!_warnedFreeze)
                    {
                        _warnedFreeze = true;
                        string reason = (_family == MonitorFamily.HumanoidClip)
                            ? "휴머노이드 클립 경로 — 골격 오염 방지로 폴백 미적용(AC Idle 재생 확인 필요)"
                            : "관측 본 부재";
                        Debug.LogWarning(
                            $"[ShowcaseMonitor] ⚠️ 애니 무변화 감지 — 폴백 불가: {_label} (family={_family}, 사유={reason})");
                    }
                }
            }
            else if (animChanged)
            {
                _noChangeElapsed = 0f; // 회복 — 정상 애니 재생 중
            }
        }

        /// <summary>
        /// [P-ANIM6] 관측 본 회전 Δ 합(도) + 스케일 Δ. 워처 목록 우선, 비어 있으면 SMR 추정(A/B) 폴백.
        /// excludeBreathBone=true면 폴백이 스스로 구동하는 본의 회전 Δ를 제외(자기 구동 오판 차단).
        /// </summary>
        private float ObserveAnimDelta(bool excludeBreathBone)
        {
            float angleDelta = 0f;

            if (_watchBones.Count > 0)
            {
                for (int i = 0; i < _watchBones.Count; i++)
                {
                    var bone = _watchBones[i];
                    if (bone == null) continue;
                    if (excludeBreathBone && bone == _breathBone) continue;
                    angleDelta += Quaternion.Angle(_watchLastRots[i], bone.localRotation);
                    _watchLastRots[i] = bone.localRotation;
                }
            }
            else
            {
                bool useA = _monitorBoneA != null && !(excludeBreathBone && _monitorBoneA == _breathBone);
                bool useB = _monitorBoneB != null && !(excludeBreathBone && _monitorBoneB == _breathBone);
                if (useA) angleDelta += Quaternion.Angle(_lastRotA, _monitorBoneA.localRotation);
                if (useB) angleDelta += Quaternion.Angle(_lastRotB, _monitorBoneB.localRotation);
                if (useA) _lastRotA = _monitorBoneA.localRotation;
                if (useB) _lastRotB = _monitorBoneB.localRotation;
            }

            // [2026-09-22] 스케일 변화도 "애니"로 인정 — SpecialCreatureAnimator(슬라임 펄스 등)는
            // 본 스케일을 변형하므로 회전만 보면 오탐(무변화 오판)했다.
            // [P-ANIM6] 대상은 Special=펄스 실제 대상 본, 타 계열=SMR transform.
            if (_scaleProbe != null)
            {
                float sDelta = Vector3.Distance(_lastScale, _scaleProbe.localScale);
                angleDelta += sDelta * 100f;
                _lastScale = _scaleProbe.localScale;
            }

            return angleDelta;
        }

        private void LateUpdate()
        {
            if (!_fallbackActive || !_breathReady || _breathBone == null) return;

            bool moving = _lastObservedMoving;
            float amp = moving ? _moveAmpDeg : _idleAmpDeg;
            float hz = moving ? _moveHz : _idleHz;
            float phase = Time.time * hz * 2f * Mathf.PI;

            // 걸음 롤 + 호흡 피치 — localRotation 곱셈(루트 소유 규약 준수).
            // base는 Start에서 1회 포착 후 고정 — 폴백 활성 중 재포착하면 폴백 회전이 base에 누적(드리프트)된다.
            float roll = Mathf.Sin(phase) * amp;
            float pitch = Mathf.Sin(phase * 0.5f) * amp * 0.4f;
            _breathBone.localRotation = _breathBaseRot * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}