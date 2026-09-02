using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사 절차적 애니메이터 (soldier GLB — 27본 리그, 클립 0개).
    /// - Idle: 팔 내림(A-pose) + 미세 호흡
    /// - Walk: 위치 변화 자동 감지 → 다리 스윙/무릎 굽힘/팔 반대 스윙 + 이동 방향 회전
    /// - Attack: TriggerAttack() — 우팔 오버헤드 슬래시 (내부 쿨다운 1.2s, 매 프레임 호출 안전)
    /// 본 회전은 world-delta 방식(AngleAxis × parentRot × baseLocal) — 부모 회전 추종.
    /// </summary>
    public class SoldierIdlePose : MonoBehaviour
    {
        private const float ARM_DOWN_STRENGTH = 0.85f;
        private const float BOB_AMPLITUDE = 0.045f;
        private const float BOB_SPEED = 1.4f;
        private const float WALK_DETECT_SPEED = 0.6f;   // 이 속도(m/s) 이상이면 걷기
        private const float STRIDE_SPEED = 1.8f;        // 보폭 위상 계수
        private const float LEG_SWING_DEG = 28f;
        private const float KNEE_BEND_DEG = 20f;
        private const float ARM_SWING_DEG = 15f;
        private const float ATTACK_DURATION = 0.5f;
        private const float ATTACK_COOLDOWN = 1.2f;
        private const float TURN_SPEED = 8f;

        private Transform _thighL, _thighR, _shinL, _shinR;
        private Transform _upperArmL, _upperArmR, _forearmL, _forearmR;
        private Quaternion _thighLBase, _thighRBase, _shinLBase, _shinRBase;
        private Quaternion _armLBase, _armRBase;
        private Vector3 _basePos;
        private Quaternion _baseRot;
        private float _phase;
        private float _walkBlendCur;      // 걷기 블렌드 (스무딩)
        private float _attackT = 999f;
        private float _lastAttackReal = -10f;
        private Vector3 _lastPos;
        private bool _poseApplied;

        private void Start()
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
            _lastPos = transform.position;
            CacheBones();
            ApplyArmsDown();
            CachePoseBases();
        }

        private void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // ── 걷기 감지 (수평 위치 변화) ──
            Vector3 dp = transform.position - _lastPos;
            dp.y = 0f;
            _lastPos = transform.position;
            float speed = dp.magnitude / dt;
            bool moving = speed > WALK_DETECT_SPEED;
            _walkBlendCur = Mathf.MoveTowards(_walkBlendCur, moving ? 1f : 0f, dt * 6f);
            if (moving) _phase += speed * STRIDE_SPEED * dt;

            // 이동 중이면 이동 방향을 바라봄 (LookRotation 스무딩)
            if (moving && dp.sqrMagnitude > 0.00001f)
            {
                var targetRot = Quaternion.LookRotation(dp.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(_baseRot * Quaternion.identity, targetRot, TURN_SPEED * dt);
                _baseRot = transform.rotation; // base 갱신 (bob 기준 유지)
            }

            // ── 공격 타이머 ──
            _attackT += dt;
            bool attacking = _attackT < ATTACK_DURATION;

            // ── 본 구동 ──
            var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 axis = parentRot * Vector3.right; // 병사 좌우축 (전후 스윙)

            if (_thighL != null && _thighR != null)
            {
                float s = Mathf.Sin(_phase) * LEG_SWING_DEG * _walkBlendCur;
                SwingBone(_thighL, _thighLBase, axis, s);
                SwingBone(_thighR, _thighRBase, axis, -s);

                if (_shinL != null && _shinR != null)
                {
                    float bend = Mathf.Max(0f, -Mathf.Sin(_phase)) * KNEE_BEND_DEG * _walkBlendCur;
                    SwingBone(_shinL, _shinLBase, axis, -bend);
                    SwingBone(_shinR, _shinRBase, axis, bend);
                }
            }

            if (_upperArmL != null && _upperArmR != null)
            {
                if (attacking)
                {
                    // 공격: 우팔 오버헤드 슬래시
                    float t01 = Mathf.Clamp01(_attackT / ATTACK_DURATION);
                    float eased = Mathf.SmoothStep(0f, 1f, t01);
                    float swing = Mathf.Lerp(-115f, 75f, eased);
                    SwingBone(_upperArmR, _armRBase, axis, swing);
                    SwingBone(_upperArmL, _armLBase, axis, -swing * 0.25f);
                }
                else
                {
                    float s = Mathf.Sin(_phase) * ARM_SWING_DEG * _walkBlendCur;
                    SwingBone(_upperArmL, _armLBase, axis, -s * 0.8f);
                    SwingBone(_upperArmR, _armRBase, axis, s * 0.8f);
                }
            }

            // ── idle/walk bob (호흡) ──
            // 주의: 이동 중엔 transform.position이 AI가 제어하므로 bob은 baseRot만 미세 적용
            if (!moving)
            {
                float t = Time.time * BOB_SPEED + (GetInstanceID() % 628) * 0.01f;
                Vector3 p = _basePos;
                p.y += Mathf.Sin(t) * BOB_AMPLITUDE;
                transform.position = p;
                _basePos = p;
            }
        }

        /// <summary>공격 모션 트리거 (내부 쿨다운 1.2초 — 매 프레임 호출 안전). GuardCombatAI 등에서 호출.</summary>
        public void TriggerAttack()
        {
            if (Time.time - _lastAttackReal < ATTACK_COOLDOWN) return;
            _lastAttackReal = Time.time;
            _attackT = 0f;
        }

        /// <summary>본 회전: AngleAxis(angle, axis) * parentWorldRot * baseLocal</summary>
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
