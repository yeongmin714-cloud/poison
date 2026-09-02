using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 GLB(Player_Rigged, 27본 리그, 클립 0개)를 위한 절차적 애니메이터.
    /// 모션: Idle(호흡) / Walk / Run(스프린트) / Attack(우수 오버헤드 슬래시) / Roll(앞구르기 전회) / Jump(다리 턱)
    /// - 이동 감지: 부모 Player의 CharacterController.velocity
    /// - 구르기/점프: PlayerMovement.IsRolling/IsJumping 폴링
    /// - 공격: PlayerCombat.LastAttackTime 변화 감지
    /// - 본 회전은 world-delta 방식(bone.rotation = AngleAxis(angle, playerRight) * parentRot * baseLocal)
    ///   → 플레이어 선회 추종 + 부모 회전 영향 정상 반영
    /// </summary>
    public class PlayerCharacterAnimator : MonoBehaviour
    {
        [Header("걷기/달리기")]
        [SerializeField] private float _strideSpeed = 1.9f;      // 보폭 위상 계수 (phase += speed × 이값)
        [SerializeField] private float _walkSpeed = 4.5f;        // 걷기 풀 블렌드 속도 (m/s)
        [SerializeField] private float _runSpeed = 8f;           // 달리기 풀 블렌드 속도 (m/s)
        [SerializeField] private float _legSwingDeg = 30f;       // 걷기 허벅지 스윙
        [SerializeField] private float _runLegSwingDeg = 46f;    // 달리기 허벅지 스윙
        [SerializeField] private float _kneeBendDeg = 22f;       // 무릎 굽힘
        [SerializeField] private float _armSwingDeg = 16f;       // 걷기 팔 스윙
        [SerializeField] private float _runArmSwingDeg = 30f;    // 달리기 팔 스윙
        [SerializeField] private float _bobAmplitude = 0.055f;   // 걷기 bob (m)
        [SerializeField] private float _runBobAmplitude = 0.09f;
        [SerializeField] private float _idleBobAmplitude = 0.012f;

        [Header("공격/구르기/점프")]
        [SerializeField] private float _attackDuration = 0.45f;  // 공격 모션 길이 (s)
        [SerializeField] private bool _invertAttackSwing = false;// 스윙 방향 반전 (모델 정면 기준 조정용)
        [SerializeField] private bool _invertRollFlip = false;   // 구르기 전회 방향 반전
        [SerializeField] private float _jumpTuckBlend = 0.6f;    // 점프 다리 턱 강도 (0~1)

        private CharacterController _cc;
        private PlayerMovement _movement;
        private PlayerCombat _combat;
        private Transform _thighL, _thighR, _shinL, _shinR;
        private Transform _upperArmL, _upperArmR, _forearmL, _forearmR;
        private Quaternion _thighLBase, _thighRBase, _shinLBase, _shinRBase;
        private Quaternion _armLBase, _armRBase;
        private Quaternion _bodyBaseLocalRot;
        private Vector3 _bodyBaseLocal;
        private float _phase;
        private float _walkBlend, _runBlend;
        private bool _poseApplied;
        private bool _prevRolling;
        private float _rollT = -1f;            // -1 = 구르기 아님
        private float _attackT = 999f;         // >= _attackDuration = 공격 아님
        private float _prevCombatAttackTime = -999f;

        private void Start()
        {
            _cc = GetComponentInParent<CharacterController>();
            _bodyBaseLocal = transform.localPosition;
            _bodyBaseLocalRot = transform.localRotation;
            CacheBones();
            ApplyArmsDown();
            CachePoseBases();
        }

        private void Update()
        {
            // 상위 시스템 참조 (지연 캐시)
            if (_movement == null) _movement = GetComponentInParent<PlayerMovement>();
            if (_combat == null) _combat = GetComponentInParent<PlayerCombat>();

            float speed = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            _walkBlend = Mathf.Clamp01(speed / Mathf.Max(0.1f, _walkSpeed));
            _runBlend = Mathf.Clamp01((speed - _walkSpeed) / Mathf.Max(0.1f, _runSpeed - _walkSpeed));
            _phase += speed * _strideSpeed * Time.deltaTime;

            // ── 트리거 감지 ──
            bool rolling = _movement != null && _movement.IsRolling;
            if (rolling && !_prevRolling) _rollT = 0f;          // 구르기 시작
            if (!rolling) _rollT = -1f;                          // 구르기 종료
            _prevRolling = rolling;

            if (_combat != null)
            {
                float lat = _combat.LastAttackTime;
                if (!Mathf.Approximately(lat, _prevCombatAttackTime))
                {
                    _prevCombatAttackTime = lat;
                    _attackT = 0f;                                // 공격 모션 시작
                }
            }
            bool attacking = _attackT < _attackDuration;
            if (attacking) _attackT += Time.deltaTime;

            bool jumping = _movement != null && _movement.IsJumping;

            var player = transform.parent != null ? transform.parent : transform;
            Vector3 rightAxis = player.right; // 스윙 축: 플레이어 좌우축

            // ── 우선순위: Roll > Attack > Jump > Walk/Run/Idle ──
            if (rolling && _rollT >= 0f)
            {
                // 구르기: GLB 전체가 앞방향으로 360° 전회 + 웅크림(공 도는 실루엣) + 회전축 하강
                float dur = _movement != null ? Mathf.Max(0.05f, _movement.RollDuration) : 0.5f;
                float t01 = Mathf.Clamp01(_rollT / dur);
                // 단조 smoothstep (0→1) — 이전 (2t-1)² 공식은 t=0에서 1로 시작해 "숙였다 일어나는"처럼 보였던 버그
                float eased = t01 * t01 * (3f - 2f * t01);
                float dir = _invertRollFlip ? -1f : 1f;
                transform.localRotation = _bodyBaseLocalRot * Quaternion.Euler(dir * eased * 360f, 0f, 0f);

                // 회전축을 지면 쪽으로 낮춰 텀블링 (중앙 피벗 회전은 어색함)
                var lpRoll = _bodyBaseLocal;
                lpRoll.y -= 0.45f * Mathf.Sin(t01 * Mathf.PI);
                transform.localPosition = lpRoll;

                // 웅크림: 다리/팔을 플립 방향으로 접기 (몸이 공처럼 말림)
                var playerT = transform.parent != null ? transform.parent : transform;
                Vector3 rollAxis = playerT.right;
                float tuck = dir * 85f;
                SwingBone(_thighL, _thighLBase, rollAxis, tuck);
                SwingBone(_thighR, _thighRBase, rollAxis, tuck);
                SwingBone(_shinL, _shinLBase, rollAxis, -tuck * 0.75f);
                SwingBone(_shinR, _shinRBase, rollAxis, -tuck * 0.75f);
                SwingBone(_upperArmL, _armLBase, rollAxis, tuck * 0.55f);
                SwingBone(_upperArmR, _armRBase, rollAxis, tuck * 0.55f);
                return;
            }

            // 구르기 종료 후 자세 복원
            transform.localRotation = _bodyBaseLocalRot;

            float legSwing = Mathf.Lerp(_legSwingDeg, _runLegSwingDeg, _runBlend);
            float armSwing = Mathf.Lerp(_armSwingDeg, _runArmSwingDeg, _runBlend);

            if (_thighL != null && _thighR != null)
            {
                float s = Mathf.Sin(_phase) * legSwing * _walkBlend;
                SwingBone(_thighL, _thighLBase, rightAxis, s);
                SwingBone(_thighR, _thighRBase, rightAxis, -s);

                if (_shinL != null && _shinR != null)
                {
                    float bend = Mathf.Max(0f, -Mathf.Sin(_phase)) * _kneeBendDeg * _walkBlend;
                    SwingBone(_shinL, _shinLBase, rightAxis, -bend);
                    SwingBone(_shinR, _shinRBase, rightAxis, bend);
                }
            }

            if (_upperArmL != null && _upperArmR != null)
            {
                if (attacking)
                {
                    // 공격: 우팔 오버헤드 슬래시 (위→앞아래), 좌팔 반동
                    float t01 = Mathf.Clamp01(_attackT / _attackDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t01);
                    float swing = Mathf.Lerp(-115f, 75f, eased) * (_invertAttackSwing ? -1f : 1f);
                    SwingBone(_upperArmR, _armRBase, rightAxis, swing);
                    SwingBone(_upperArmL, _armLBase, rightAxis, -swing * 0.25f);
                }
                else
                {
                    // 걷기/달리기: 다리와 반대 위상 팔 스윙
                    float s = Mathf.Sin(_phase) * armSwing * _walkBlend;
                    SwingBone(_upperArmL, _armLBase, rightAxis, -s * 0.8f);
                    SwingBone(_upperArmR, _armRBase, rightAxis, s * 0.8f);
                }
            }

            // 점프: 공중에서 다리 턱 (구르기/공격보다 낮은 우선순위 — 위 블록에서 이미 스윙됐으면 소폭 블렌드)
            if (jumping && !attacking && _thighL != null && _thighR != null)
            {
                float tuck = _jumpTuckBlend * 70f;
                SwingBone(_thighL, _thighLBase, rightAxis, -tuck * 0.5f);
                SwingBone(_thighR, _thighRBase, rightAxis, tuck * 0.5f);
            }

            // 체중 bob: 걷기 2배 주파수 / 달리기 증폭 / 정지 호흡
            float bobAmp = Mathf.Lerp(_bobAmplitude, _runBobAmplitude, _runBlend);
            float bob = Mathf.Lerp(
                Mathf.Sin(Time.time * 1.5f) * _idleBobAmplitude,
                -Mathf.Abs(Mathf.Sin(_phase)) * bobAmp,
                _walkBlend);
            var lp = _bodyBaseLocal;
            lp.y += bob;
            transform.localPosition = lp;
        }

        /// <summary>본 회전: AngleAxis(angle, axis) * parentWorldRot * baseLocal
        /// — 부모(플레이어 루트)가 회전해도 스윙 축이 따라가고 base 자세가 유지된다.</summary>
        private static void SwingBone(Transform bone, Quaternion baseLocal, Vector3 axis, float angleDeg)
        {
            if (bone == null) return;
            var parentRot = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
            bone.rotation = Quaternion.AngleAxis(angleDeg, axis) * parentRot * baseLocal;
        }

        private void CacheBones()
        {
            var all = GetComponentsInChildren<Transform>();
            foreach (var tr in all)
            {
                string n = tr.name;
                bool isL = n.EndsWith(".L");
                if (n.Contains("thigh")) { if (isL) _thighL = tr; else _thighR = tr; }
                else if (n.Contains("shin")) { if (isL) _shinL = tr; else _shinR = tr; }
                else if (n.Contains("upper_arm")) { if (isL) _upperArmL = tr; else _upperArmR = tr; }
                else if (n.Contains("forearm")) { if (isL) _forearmL = tr; else _forearmR = tr; }
            }
        }

        /// <summary>T-pose → 자연스러운 A-pose (world FromToRotation — 로컬축 관례 무관)</summary>
        private void ApplyArmsDown()
        {
            if (_poseApplied) return;
            _poseApplied = true;
            DropArm(_upperArmL, _forearmL);
            DropArm(_upperArmR, _forearmR);
        }

        private static void DropArm(Transform upper, Transform fore)
        {
            if (upper == null || fore == null) return;
            Vector3 dir = fore.position - upper.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            Vector3 target = new Vector3(dir.x * 0.15f, -1f, dir.z * 0.15f).normalized;
            Quaternion delta = Quaternion.FromToRotation(dir, target);
            upper.rotation = delta * upper.rotation;
        }

        /// <summary>팔 내림 적용 후의 local rotation을 base로 저장 (이후 스윙은 이 base 기준)</summary>
        private void CachePoseBases()
        {
            if (_upperArmL != null) _armLBase = _upperArmL.localRotation;
            if (_upperArmR != null) _armRBase = _upperArmR.localRotation;
            if (_thighL != null) _thighLBase = _thighL.localRotation;
            if (_thighR != null) _thighRBase = _thighR.localRotation;
            if (_shinL != null) _shinLBase = _shinL.localRotation;
            if (_shinR != null) _shinRBase = _shinR.localRotation;
        }
    }
}
