using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>피격 강도 — 연출 세기 분기.</summary>
    public enum HitSeverity { Light, Heavy, Crit }

    /// <summary>
    /// [2026-09-15 Phase C] 피격자(몬스터/병사/영주) 피격 리액션 단일 드라이버.
    ///
    /// 배경(테스트17 실측): 플레이어가 때려도 피격자가 전혀 반응하지 않음(HP바만 감소) — 액션감 부족의 최대 갭.
    /// 제약: 적 애니메이터(Monster_Animator / Soldier_Animator)에는 Hit/Death 상태가 전혀 없음
    ///       (상태 = Attack/AttackTrigger/Base/Idle/Run/State/Walk 뿐) → 컨트롤러 편집 없이 절차(코드) 반응으로 처리.
    ///
    /// 적용 범위:
    ///   - 플린치(flinch): 시각 모델(자식) 트랜스폼을 타격 반대 방향으로 살짝 젖히고 복원(0.15~0.25s).
    ///   - 넉백(knockback): Heavy/Crit 시 Rigidbody 임펄스 또는 CharacterController 이동(0.8~1.5m).
    ///   - Animator 에 HitLight/Hit 트리거가 있으면(플레이어급 리깅) 함께 발화, 없으면 스킵.
    ///
    /// 안전 규칙(39차 교훈): 이 드라이버는 연출 전용이며 HP 차감/어그로/Die() 경로를 절대 방해하지 않는다.
    ///   호출부는 try-catch 로 격리하고, 파괴 대상에는 스스로 Destroy 를 걸지 않는다(사망 처리는 기존 Die() 담당).
    /// </summary>
    public static class HitReactionDriver
    {
        private const float LightDuration = 0.18f;
        private const float HeavyDuration = 0.26f;
        private const float CritDuration = 0.34f;
        private const float LightLeanDeg = 9f;
        private const float HeavyLeanDeg = 16f;
        private const float CritLeanDeg = 22f;
        private const float LightPush = 0.09f;
        private const float HeavyPush = 0.22f;
        private const float CritPush = 0.32f;

        /// <summary>
        /// 피격 리액션 적용. 대상이 플레이어면 스킵(플레이어는 자체 HitLight 경로 사용).
        /// </summary>
        public static void Apply(GameObject target, Vector3 hitDirection, HitSeverity severity, bool isFatal)
        {
            if (target == null) return;
            if (target.CompareTag("Player")) return;

            // [TEST21-FOLLOWUP] inactive(사망·SetActive(false) 잔여) 대상은 코루틴 시작 불가 →
            // 'Coroutine couldn't be started because ... is inactive' 스팸 방지 + 사망 처리는 Die() 담당이므로 스킵.
            if (!target.activeInHierarchy) return;

            Vector3 dir = hitDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -target.transform.forward;
            dir.Normalize();

            // Animator 트리거(있으면) — 적 컨트롤러엔 없으므로 hasParameter 로 반드시 확인.
            TryAnimatorTrigger(target, isFatal ? "Hit" : "HitLight");

            // 전용 러너 부착(중복 방지) — 코루틴을 위해 MonoBehaviour 필요.
            var runner = target.GetComponent<HitReactionRunner>();
            if (runner == null) runner = target.AddComponent<HitReactionRunner>();

            float dur = severity == HitSeverity.Crit ? CritDuration : (severity == HitSeverity.Heavy ? HeavyDuration : LightDuration);
            float lean = severity == HitSeverity.Crit ? CritLeanDeg : (severity == HitSeverity.Heavy ? HeavyLeanDeg : LightLeanDeg);
            float push = severity == HitSeverity.Crit ? CritPush : (severity == HitSeverity.Heavy ? HeavyPush : LightPush);
            if (isFatal) { lean *= 1.25f; push *= 1.2f; }

            runner.PlayFlinch(dir, dur, lean, push, isFatal);

            // 넉백 — Heavy/Crit 및 사망 시.
            if (severity != HitSeverity.Light || isFatal)
            {
                float mag = severity == HitSeverity.Crit ? 1.5f : (severity == HitSeverity.Heavy ? 0.9f : 0.5f);
                ApplyKnockback(runner, target, dir, mag);
            }
        }

        private static void TryAnimatorTrigger(GameObject target, string trigger)
        {
            try
            {
                var anim = target.GetComponentInChildren<Animator>();
                if (anim == null || anim.runtimeAnimatorController == null) return;
                var ps = anim.parameters;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].type == AnimatorControllerParameterType.Trigger && ps[i].name == trigger)
                    {
                        anim.SetTrigger(trigger);
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HitReaction] Animator 트리거 스킵: {e.GetType().Name}");
            }
        }

        private static void ApplyKnockback(HitReactionRunner runner, GameObject target, Vector3 dir, float magnitude)
        {
            try
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Unity 6: linearVelocity, 구버전 폴백 velocity — 리플렉션 없이 property 존재로 판정 불가하므로
                    // AddForce(Impulse) 로 통일(질량 무관 체감 위해 ForceMode.VelocityChange).
                    rb.AddForce(dir * magnitude, ForceMode.VelocityChange);
                    return;
                }
                var cc = target.GetComponent<CharacterController>();
                if (cc != null)
                {
                    runner.PlayKnockbackCC(cc, dir, magnitude);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HitReaction] 넉백 스킵: {e.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// [2026-09-15 Phase C] 피격 리액션 러너 — 절차 플린치/넉백 코루틴을 대상 오브젝트 위에서 실행.
    /// 시각 모델 자식 트랜스폼의 로컬 pos/rot 를 캡처 → 오프셋 → 원복(대상 파괴 시 즉시 중단).
    /// </summary>
    public class HitReactionRunner : MonoBehaviour
    {
        private Transform _visual;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private bool _baseCaptured;
        private bool _logOnce;
        private Coroutine _flinchCo;

        /// <summary>플린치 시작 — 진행 중이면 재시작(연타 대응).</summary>
        public void PlayFlinch(Vector3 worldHitDir, float duration, float leanDeg, float pushDist, bool isFatal)
        {
            if (!TryCaptureVisual()) return;   // 시각 자식이 없으면 플린치 스킵(루트 이동 = AI 방해 위험)
            if (_flinchCo != null) StopCoroutine(_flinchCo);
            _flinchCo = StartCoroutine(FlinchCo(worldHitDir, duration, leanDeg, pushDist, isFatal));
        }

        /// <summary>CharacterController 넉백 — 짧은 시간 프레임 분산 이동.</summary>
        public void PlayKnockbackCC(CharacterController cc, Vector3 dir, float magnitude)
        {
            StartCoroutine(KnockbackCo(cc, dir, magnitude, 0.16f));
        }

        private bool TryCaptureVisual()
        {
            if (_baseCaptured && _visual != null) return true;
            // 루트가 아닌 자식 모델 트랜스폼을 우선 선택(Animator 자식 → 이름 힌트 → 유일 자식).
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.transform != transform) _visual = anim.transform;
            if (_visual == null)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var c = transform.GetChild(i);
                    string n = c.name.ToLowerInvariant();
                    if (n.Contains("model") || n.Contains("visual") || n.Contains("mesh") || n.Contains("body"))
                    { _visual = c; break; }
                }
            }
            if (_visual == null && transform.childCount == 1) _visual = transform.GetChild(0);
            // [2026-09-15 Phase F] 폴백 강화 — 이름 힌트/유일자식 실패 시 '렌더러를 가장 많이 가진 자식' 선택.
            // (테스트18에서 flinch가 안 보인 원인 중 하나: 시각 자식 탐지 실패 → 적용 자체가 스킵)
            if (_visual == null)
            {
                int best = -1, bestCount = 0;
                for (int i = 0; i < transform.childCount; i++)
                {
                    int c = transform.GetChild(i).GetComponentsInChildren<Renderer>(true).Length;
                    if (c > bestCount) { bestCount = c; best = i; }
                }
                if (best >= 0) _visual = transform.GetChild(best);
            }
            if (_visual == null)
            {
                if (!_logOnce) { _logOnce = true; Debug.Log("[HitReaction] 시각 자식 탐지 실패 — 플린치 스킵(넉백만 적용)"); }
                return false;
            }

            _baseLocalPos = _visual.localPosition;
            _baseLocalRot = _visual.localRotation;
            _baseCaptured = true;
            return true;
        }

        private IEnumerator FlinchCo(Vector3 worldHitDir, float duration, float leanDeg, float pushDist, bool isFatal)
        {
            if (_visual == null) yield break;
            Transform parent = _visual.parent != null ? _visual.parent : transform;
            // 타격 반대(뒤로 젖힘) 방향을 시각 부모 로컬로 환산.
            Vector3 backLocal = parent.InverseTransformDirection(-worldHitDir);
            backLocal.y = 0f;
            if (backLocal.sqrMagnitude < 0.0001f) backLocal = Vector3.back;
            backLocal.Normalize();

            Quaternion leanRot = Quaternion.AngleAxis(leanDeg, parent.InverseTransformDirection(Vector3.Cross(Vector3.up, worldHitDir).normalized));
            Vector3 targetPos = _baseLocalPos + backLocal * pushDist;

            float t = 0f;
            // 1) 젖힘 (빠르게)
            while (t < duration && _visual != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ease = k * k * (3f - 2f * k);           // smoothstep
                _visual.localPosition = Vector3.Lerp(_baseLocalPos, targetPos, ease);
                _visual.localRotation = Quaternion.Slerp(_baseLocalRot, _baseLocalRot * leanRot, ease);
                yield return null;
            }
            // 2) 원복 (조금 더 느리게)
            float backDur = duration * 0.85f;
            float r = 0f;
            Vector3 fromPos = _visual != null ? _visual.localPosition : targetPos;
            Quaternion fromRot = _visual != null ? _visual.localRotation : _baseLocalRot;
            while (r < backDur && _visual != null)
            {
                r += Time.deltaTime;
                float k = Mathf.Clamp01(r / backDur);
                _visual.localPosition = Vector3.Lerp(fromPos, _baseLocalPos, k);
                _visual.localRotation = Quaternion.Slerp(fromRot, _baseLocalRot, k);
                yield return null;
            }
            if (_visual != null)
            {
                _visual.localPosition = _baseLocalPos;
                _visual.localRotation = _baseLocalRot;
            }
            _flinchCo = null;
        }

        private IEnumerator KnockbackCo(CharacterController cc, Vector3 dir, float magnitude, float duration)
        {
            float t = 0f;
            float speed = magnitude / Mathf.Max(0.05f, duration);
            while (t < duration && cc != null)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);   // 감쇠
                cc.Move(dir * speed * k * Time.deltaTime);
                yield return null;
            }
        }
    }
}
