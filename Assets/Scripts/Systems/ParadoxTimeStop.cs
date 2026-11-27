using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호5/P3] Time Paradox 엔진 기반 PERFECT 패링 타임 스톱 연출 — 계획 docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md §P3.
    /// 1차 범위: "PERFECT 패링 → 주변 몬스터 정지, 플레이어만 움직임"(Test_10 한정 opt-in).
    ///
    /// 소유권 분리 계약(계획 §C):
    ///  - Time Paradox(TimeWarpIdentity.LocalTimeScale) = 연출용 시간 상태 소유자 — 정지 여부/해제 타이밍 전담.
    ///  - MagicTime(MagicTimeUser, 95차 F1) = 패링 개인 슬로우 — 기존 경로 무수정(공존).
    ///  - HitStopManager = 판정용 로컬 히트스톱 — 무수정.
    ///  - 전역 Time.timeScale은 어느 쪽도 건드리지 않는다(EngineMode.LocalZonesOnly — 플레이어 이동 자동 보존).
    ///
    /// AnimalAI 통합: Update 게이트에서 IsActiveFor(component)가 true면 이동/어그로 스킵(
    ///   IndoorActive 게이트 선례 동일 패턴) — Asset의 EffectiveTimeScale만 읽는 단방향 의존.
    ///
    /// 결정론 계약: Random API 0건, 게이트=씬명 상수 비교, 반경/지속시간 상수.
    /// </summary>
    public static class ParadoxTimeStop
    {
        private const string Tag = "[ParadoxTimeStop]";

        /// <summary>[P3-E] Test_10 한정 opt-in — 타 씬 완전 무동작(본씬/기타 테스트씬 보호).</summary>
        internal const string OptInSceneName = "Test_10_TerritoryCombat";

        internal const float RadiusMeters = 7f;      // 정지 적용 반경
        internal const float BlendInSec = 0.12f;     // 1→0 램프 인
        internal const float HoldSec = 1.0f;         // 완전 정지 유지
        internal const float BlendOutSec = 0.3f;     // 0→1 램프 아웃

        private static TimeWarpManager _manager;

        /// <summary>
        /// PERFECT 패링 시 호출 — 반경 내 AnimalAI에 TimeWarpIdentity를 부착하고 LocalTimeScale 0(스테이시스).
        /// 게이트(타 씬)에서 즉시 무시 — 호출부 무수정 보장.
        /// </summary>
        public static void Trigger(Vector3 center)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != OptInSceneName) return;   // opt-in 계약 — 조용히 무시
            try
            {
                EnsureManager();
                if (_manager == null)
                {
                    Debug.LogWarning($"{Tag} TimeWarpManager 확보 실패 — 연출 생략(게임 로직 무영향)");
                    return;
                }

                var hosts = Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
                var identities = new System.Collections.Generic.List<TimeWarpIdentity>();
                foreach (var ai in hosts)
                {
                    if (ai == null) continue;
                    if ((ai.transform.position - center).sqrMagnitude > RadiusMeters * RadiusMeters) continue;
                    var id = ai.GetComponent<TimeWarpIdentity>();
                    if (id == null)
                    {
                        id = ai.gameObject.AddComponent<TimeWarpIdentity>();
                        id.FreeMemoryOnDisable = true;   // 해제 시 버퍼 즉시 반환 — 누수 없음
                    }
                    identities.Add(id);
                }
                if (identities.Count == 0)
                {
                    Debug.Log($"{Tag} 반경 {RadiusMeters:F1}m 내 대상 없음 — 연출 생략");
                    return;
                }

                var hostGo = new GameObject("ParadoxTimeStop_Runtime");
                var runner = hostGo.AddComponent<ParadoxTimeStopRunner>();
                runner.Run(identities);
                Debug.Log($"{Tag} 타임 스톱 개시 — 대상 {identities.Count}체 반경 {RadiusMeters:F1}m " +
                          $"(hold {HoldSec:F2}s, blend {BlendInSec:F2}/{BlendOutSec:F2}s)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} 트리거 실패 — 연출 생략(게임 로직 무영향): {e.GetType().Name} {e.Message}");
            }
        }

        /// <summary>
        /// 대상이 현재 타임 스톱 중인지 — Asset 소유 상태(EffectiveTimeScale) 읽기 전용 판정.
        /// AnimalAI Update 게이트용. 미등록/부재=false(오동작 방지).
        /// </summary>
        public static bool IsActiveFor(Component target)
        {
            if (target == null) return false;
            var id = target.GetComponent<TimeWarpIdentity>();
            if (id == null || !id.IsRegistered) return false;
            return id.EffectiveTimeScale <= 0.01f;
        }

        private static void EnsureManager()
        {
            _manager = Object.FindFirstObjectByType<TimeWarpManager>();
            if (_manager != null) return;
            var go = new GameObject("TimeWarpManager_Runtime");
            _manager = go.AddComponent<TimeWarpManager>();
            _manager.Mode = EngineMode.LocalZonesOnly;   // 전역 timescale 무접촉 — 플레이어 이동 보존
            Object.DontDestroyOnLoad(go);
        }
    }

    /// <summary>[무번호5/P3] 스테이시스 램프 러너 — blend-in/hold/blend-out 후 identity 제거(자기 정리).</summary>
    public class ParadoxTimeStopRunner : MonoBehaviour
    {
        private System.Collections.Generic.List<TimeWarpIdentity> _identities;
        private VignettePulse _visual;   // [무번호 23-W1] 시간 정지 시각 언어(청록 비네트 — 진입/유지/해제 3박자)

        public void Run(System.Collections.Generic.List<TimeWarpIdentity> identities)
        {
            _identities = identities;
            // [무번호 23-W1] 정지 진입 연출 — 청록 역색 비네트 진입(0.12s 램프와 동기). 시각 실패해도 시간 로직 무영향.
            try
            {
                _visual = ScreenVignetteFX.BeginManual(Camera.main,
                    new Color(0.16f, 0.85f, 0.78f), 0.5f, ParadoxTimeStop.BlendInSec);
            }
            catch (System.Exception) { _visual = null; }
            StartCoroutine(RampRoutine());
        }

        private System.Collections.IEnumerator RampRoutine()
        {
            foreach (var id in _identities) if (id != null) id.LocalTimeScale = 1f;

            float t = 0f;
            while (t < ParadoxTimeStop.BlendInSec)   // 1→0
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / ParadoxTimeStop.BlendInSec);
                foreach (var id in _identities) if (id != null) id.LocalTimeScale = k;
                yield return null;
            }
            foreach (var id in _identities) if (id != null) id.LocalTimeScale = 0f;

            yield return new WaitForSeconds(ParadoxTimeStop.HoldSec);

            // [무번호 23-W3] 해제 릴 — 진입 역색의 해제 이징(블렌드아웃과 동기)
            if (_visual != null) _visual.Release();

            t = 0f;
            while (t < ParadoxTimeStop.BlendOutSec)  // 0→1
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / ParadoxTimeStop.BlendOutSec);
                foreach (var id in _identities) if (id != null) id.LocalTimeScale = k;
                yield return null;
            }
            foreach (var id in _identities) if (id != null) id.LocalTimeScale = 1f;

            if (_visual != null) _visual.ForceEnd();   // [무번호 23-W] 비주얼 잔존 방지(안전망)
            Destroy(gameObject);   // 러너 자가 파괴 — identity는 FreeMemoryOnDisable로 정리는 Destroy 시
            foreach (var id in _identities) if (id != null) Destroy(id);   // Unregister+버퍼 반환
        }
    }
}
