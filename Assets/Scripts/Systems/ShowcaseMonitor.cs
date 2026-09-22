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
#pragma warning disable 0414
using UnityEngine;

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
        private Transform _monitorBoneA;
        private Transform _monitorBoneB;
        private Quaternion _lastRotA = Quaternion.identity;
        private Quaternion _lastRotB = Quaternion.identity;
        private Vector3 _lastScale;
        private bool _hasScaleProbe;

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
            }
            else
            {
                _hasScaleProbe = true;
                _lastScale = transform.localScale;
            }
        }

        /// <summary>라벨/계열 설정. TestAnimationShowcaseSetup이 생성 직후 호출.</summary>
        public void Setup(string label, MonitorFamily family)
        {
            _label = label;
            _family = family;
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

            // (b) 애니 변화 감지 — 관측 본 localRotation Δ 합(도) 또는 스케일 Δ
            float angleDelta = 0f;
            if (_monitorBoneA != null)
                angleDelta += Quaternion.Angle(_lastRotA, _monitorBoneA.localRotation);
            if (_monitorBoneB != null)
                angleDelta += Quaternion.Angle(_lastRotB, _monitorBoneB.localRotation);
            if (_monitorBoneA != null) _lastRotA = _monitorBoneA.localRotation;
            if (_monitorBoneB != null) _lastRotB = _monitorBoneB.localRotation;

            if (_hasScaleProbe)
            {
                float scaleDelta = Vector3.Distance(_lastScale, transform.localScale);
                angleDelta += scaleDelta * 100f; // 도 환산 대용량화 — 스케일 변화도 "변화"로 인정
                _lastScale = transform.localScale;
            }

            bool animChanged = angleDelta > 0.05f;

            // (c) 무변화 누적 — 이동 중에만 판정(정지 중 무변화는 정상 idle)
            if (moving && !animChanged)
            {
                _noChangeElapsed += _observeInterval;

                if (!_fallbackActive && _noChangeElapsed >= _freezeThreshold)
                {
                    bool canFallback = _family != MonitorFamily.HumanoidClip && _breathReady;
                    if (canFallback)
                    {
                        _fallbackActive = true;
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
