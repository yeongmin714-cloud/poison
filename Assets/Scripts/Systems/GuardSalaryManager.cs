using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase E-2: 유지비·고용 일일 틱 구동 매니저.
    /// TimeManager.OnDayStart(게임일 경계)를 구독해
    ///  1) 플레이어 소속 병사 일일 급료 청구(GuardSalarySystem.TryPayDailyWages)
    ///  2) AI 영주가 고용 시장에서 방출 병사 재고용(LaborMarketSystem.ProcessAILordHiring)
    ///  3) 고용비 인상 요구 발생(충성도 충족·미응답 병사 한정 — 스팸 방지)
    /// 를 매일 수행한다. 씬 배선은 GameSetup.EnsureGuardSalaryManager()가 담당(멱등).
    /// </summary>
    public sealed class GuardSalaryManager : MonoBehaviour
    {
        private static GuardSalaryManager _instance;

        public static GuardSalaryManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            var tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart += OnDayStart;
        }

        private void OnDisable()
        {
            var tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart -= OnDayStart;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>게임일 경계 — 급료 청구 → AI 고용 → 인상 요구 발생.</summary>
        private void OnDayStart()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            int day = tm.CurrentDay;

            try { GuardSalarySystem.TryPayDailyWages(day); }
            catch (System.Exception ex) { Debug.LogError($"[GuardSalaryManager] 급료 청구 실패: {ex.Message}"); }

            try { LaborMarketSystem.ProcessAILordHiring(day); }
            catch (System.Exception ex) { Debug.LogError($"[GuardSalaryManager] AI 고용 실패: {ex.Message}"); }

            TryTriggerPayRaiseRequests();
        }

        /// <summary>
        /// 살아있는 포섭 병사 중 아직 인상 결정이 없고(미응답·미인상) 요구 조건(충성도 등)을
        /// 충족한 병사에게 고용비 인상 요구를 발송한다. 응답(수락/거절)은 UI가 HandlePayRaiseResponse 호출.
        /// 미응답 요구가 남은 병사는 재요구하지 않아 낙일/스팸 방지.
        /// </summary>
        private void TryTriggerPayRaiseRequests()
        {
            var manager = GuardManager.Instance;
            if (manager == null) return;

            var guards = manager.GetAllPlayerGuards();
            if (guards == null || guards.Count == 0) return;

            for (int i = 0; i < guards.Count; i++)
            {
                var guard = guards[i];
                if (guard == null || !guard.IsAlive || !guard.IsRecruited) continue;
                if (GuardSalarySystem.HasRaised(guard) || GuardSalarySystem.HasPendingRaise(guard)) continue;

                GuardSalarySystem.RequestPayRaise(guard);
            }
        }

        /// <summary>테스트/진단용 — 일일 틱 수동 트리거.</summary>
        public void DebugRunDailyShutdown()
        {
            OnDayStart();
        }
    }
}