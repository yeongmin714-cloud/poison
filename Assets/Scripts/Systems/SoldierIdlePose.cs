using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// soldier GLB는 리깅만 있고 애니메이션 클립이 없어 T-pose로 서 있는 문제를
    /// 절차적(procedural)으로 해결하는 컴포넌트.
    /// 1) T-pose → 팔을 몸 옆으로 내림 (world-space FromToRotation — 본 로컬축 관례 무관)
    /// 2) 미세 호흡/체중 이동 idle 애니메이션 (정적 병사용)
    /// 본이 없는 폴백 프리미티브에 부착되면 idle bob만 적용.
    /// </summary>
    public class SoldierIdlePose : MonoBehaviour
    {
        // 팔 내림 강도 (1.0 = 완전 수직, 0.85 = 살짝 벌어진 자연스러운 A-pose)
        private const float ARM_DOWN_STRENGTH = 0.85f;
        private const float BOB_AMPLITUDE = 0.045f;   // 상하 미세 흔들림 (m)
        private const float BOB_SPEED = 1.4f;         // 호흡 속도
        private const float SWAY_AMPLITUDE = 0.6f;    // 척추 좌우 미세 기울기 (도)

        private Transform _upperArmL, _upperArmR, _forearmL, _forearmR;
        private Vector3 _basePos;
        private Quaternion _baseRot;
        private float _phase;
        private bool _poseApplied;

        private void Start()
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
            _phase = (transform.GetInstanceID() % 628) * 0.01f; // 0~6.28 위상 (결정론적, Random 아님)
            CacheBones();
            ApplyArmsDown();
        }

        private void Update()
        {
            // 미세 호흡: 상하 bob + 좌우 미세 roll (base 기준 — LookRotation 방향 보존)
            float t = Time.time * BOB_SPEED + _phase;
            Vector3 p = _basePos;
            p.y += Mathf.Sin(t) * BOB_AMPLITUDE;
            transform.position = p;

            float sway = Mathf.Sin(t * 0.45f) * SWAY_AMPLITUDE; // 도
            transform.rotation = _baseRot * Quaternion.Euler(0f, 0f, sway * 0.1f);
        }

        /// <summary>본 이름으로 팔 관절 캐시 (Blender Rigify 계열: upper_arm.L 등)</summary>
        private void CacheBones()
        {
            var all = GetComponentsInChildren<Transform>();
            foreach (var tr in all)
            {
                string n = tr.name;
                if (n.Contains("upper_arm")) { if (n.EndsWith(".L") || n.Contains("L")) _upperArmL = tr; else _upperArmR = tr; }
                else if (n.Contains("forearm")) { if (n.EndsWith(".L") || n.Contains("L")) _forearmL = tr; else _forearmR = tr; }
            }
        }

        /// <summary>T-pose 팔을 몸 옆으로 내림.
        /// 어깨→손 방향 벡터를 아래쪽으로 회전 (world-space delta — 로컬축 관례 무관하게 동작).
        /// 전완(forearm)은 추가로 살짝 안쪽으로 굽혀 자연스러운 정지 자세.</summary>
        private void ApplyArmsDown()
        {
            if (_poseApplied) return;
            _poseApplied = true;
            RotateArmDown(_upperArmL, _forearmL);
            RotateArmDown(_upperArmR, _forearmR);
        }

        private static void RotateArmDown(Transform upper, Transform fore)
        {
            if (upper == null) return;

            // 어깨 → 손(또는 팔끝) 방향
            Transform tip = fore != null ? fore : null;
            if (tip == null) return;

            // tip의 자식이 있으면 그 끝을, 없으면 forearm 위치 기준
            Vector3 shoulderPos = upper.position;
            Vector3 armDir = (tip.position - shoulderPos);
            if (armDir.sqrMagnitude < 0.0001f) return;
            armDir.Normalize();

            // 목표: 살짝 벌어진 채 아래로 (x/z 성분 15%만 유지)
            Vector3 target = new Vector3(armDir.x * 0.15f, -1f, armDir.z * 0.15f).normalized;

            Quaternion delta = Quaternion.FromToRotation(armDir, target);
            upper.rotation = delta * upper.rotation; // world-space 회전 적용

            // 전완 살짝 굽힘 (팔꿈치 자연스러움)
            if (fore != null)
            {
                Vector3 fStart = fore.position;
                // upper 회전 후 forearm 끝 방향을 다시 계산해 약간 아래로
                Vector3 fDir = (fStart - upper.position);
                if (fDir.sqrMagnitude > 0.0001f)
                {
                    fDir.Normalize();
                    Vector3 fTarget = new Vector3(fDir.x * 0.7f, fDir.y - 0.3f, fDir.z * 0.7f).normalized;
                    Quaternion fDelta = Quaternion.FromToRotation(fDir, fTarget);
                    fore.rotation = fDelta * fore.rotation;
                }
            }
        }
    }
}
