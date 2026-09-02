using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 GLB(Player_Rigged, 27본 Rigify형 리그, 클립 0개)를 위한 절차적 애니메이터.
    /// - 정지: 호흡 bob + 팔 내림 유지
    /// - 이동: 다리 스윙(thigh) + 무릎 굽힘(shin) + 팔 반대 스윙 + 체중 bob
    /// - 이동 감지: 부모 Player의 CharacterController.velocity
    /// - 회전: Player 루트가 LookRotation으로 이미 선회하므로 본은 world-delta 방식
    ///   (bone.rotation = AngleAxis(angle, playerRight) * parentRot * baseLocal — 부모 회전 추종)
    /// </summary>
    public class PlayerCharacterAnimator : MonoBehaviour
    {
        [Header("걷기 파라미터")]
        [SerializeField] private float _strideSpeed = 1.9f;      // 보폭 위상 속도 계수 (phase += speed * 이값)
        [SerializeField] private float _legSwingDeg = 30f;       // 허벅지 스윙 각도
        [SerializeField] private float _kneeBendDeg = 22f;       // 무릎 굽힘
        [SerializeField] private float _armSwingDeg = 16f;       // 팔 반대 스윙
        [SerializeField] private float _bobAmplitude = 0.055f;   // 걷기 체중 bob (m)
        [SerializeField] private float _idleBobAmplitude = 0.012f;
        [SerializeField] private float _walkBlendSpeed = 4.5f;   // 이 속도(m/s)에서 walkBlend=1

        private CharacterController _cc;
        private Transform _thighL, _thighR, _shinL, _shinR;
        private Transform _upperArmL, _upperArmR, _forearmL, _forearmR;
        private Quaternion _thighLBase, _thighRBase, _shinLBase, _shinRBase;
        private Quaternion _armLBase, _armRBase; // 팔 내림 포즈 적용 후의 base local
        private float _phase;
        private float _walkBlend;
        private bool _poseApplied;
        private Vector3 _bodyBaseLocal;

        private void Start()
        {
            _cc = GetComponentInParent<CharacterController>();
            _bodyBaseLocal = transform.localPosition;
            CacheBones();
            ApplyArmsDown();          // T-pose → A-pose (1회)
            CachePoseBases();         // 팔 내림 후 base local 저장
        }

        private void Update()
        {
            float speed = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            _walkBlend = Mathf.Clamp01(speed / _walkBlendSpeed);
            _phase += speed * _strideSpeed * Time.deltaTime;

            var player = transform.parent != null ? transform.parent : transform;
            Vector3 rightAxis = player.right; // 스윙 축: 플레이어 좌우축 (전후 스윙)

            if (_thighL != null && _thighR != null)
            {
                float s = Mathf.Sin(_phase) * _legSwingDeg * _walkBlend;
                SwingBone(_thighL, _thighLBase, rightAxis, s);
                SwingBone(_thighR, _thighRBase, rightAxis, -s);

                if (_shinL != null && _shinR != null)
                {
                    // 무릎: 다리가 뒤로 갈 때(스윙 음수 구간) 굽힘
                    float bend = Mathf.Max(0f, -Mathf.Sin(_phase)) * _kneeBendDeg * _walkBlend;
                    SwingBone(_shinL, _shinLBase, rightAxis, -bend);
                    SwingBone(_shinR, _shinRBase, rightAxis, bend);
                }
            }

            if (_upperArmL != null && _upperArmR != null)
            {
                // 팔은 다리와 반대 위상
                float s = Mathf.Sin(_phase) * _armSwingDeg * _walkBlend;
                SwingBone(_upperArmL, _armLBase, rightAxis, -s * 0.8f);
                SwingBone(_upperArmR, _armRBase, rightAxis, s * 0.8f);
            }

            // 체중 bob: 걷기 2배 주파수, 정지 시 호흡
            float bob = Mathf.Lerp(
                Mathf.Sin(Time.time * 1.5f) * _idleBobAmplitude,
                -Mathf.Abs(Mathf.Sin(_phase)) * _bobAmplitude,
                _walkBlend);
            var lp = _bodyBaseLocal;
            lp.y += bob;
            transform.localPosition = lp;
        }

        /// <summary>본 회전: worldAngleAxis(angle, axis) * parentWorldRot * baseLocal
        /// — 부모(플레이어 루트)가 회전해도 스윙 축이 따라가고 base 자세가 유지된다.</summary>
        private static void SwingBone(Transform bone, Quaternion baseLocal, Vector3 axis, float angleDeg)
        {
            if (bone == null || Mathf.Approximately(angleDeg, 0f)) return;
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
