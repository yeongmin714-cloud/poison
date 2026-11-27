using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [F2] 살려준 영주 주간 공물 시스템 — SpareLord된 영지에서 게임일 7일마다
    /// 살아남은 영주가 플레이어에게 골드를 바친다 (FLAG_TAKEOVER_PLAN F2).
    ///
    /// - 등록: LordSurrenderSystem.SpareLord → RegisterSparedLord
    /// - 지급: TimeManager.OnDayStart(아침 6시 경계) → day % 7 == 0 주차 결산
    /// - 액수: 살려주기 즉시 보상(MercyUI.GetSpareGoldReward 실측 표) × 40% × 성향 계수 — 최소 10G
    /// - 충성도: 지급마다 loyaltyToPlayer +2 (공물 = 충성 표시)
    /// - 해제: 배신(LordBetrayalSystem)/소유 상실(TerritoryDatabase.OwnershipLost)
    /// - 멱등: lastTributeWeek 주차 가드 (세이브 v5로 영속 — GuardSalary._lastPaidDay 선례)
    /// 결정론적 산식만 사용 (무작위 없음, System.Random 미사용).
    /// </summary>
    public static class SparedLordTributeSystem
    {
        // ===== 공물 산식 상수 =====
        /// <summary>주간 기본액 = 살려주기 즉시 보상의 40%</summary>
        public const float TRIBUTE_RATIO = 0.40f;
        /// <summary>공물 최소 보장액 (링1+겁많은 영주조차 의미 있는 액수)</summary>
        public const int MIN_TRIBUTE = 10;
        /// <summary>공물 결산 주기 (게임일)</summary>
        public const int TRIBUTE_INTERVAL_DAYS = 7;
        /// <summary>공물 지급 1회당 충성도 상승량</summary>
        public const int LOYALTY_PER_TRIBUTE = 2;

        // ===== 내부 상태 =====
        private class SparedLordEntry
        {
            public LordPersonality personality;
            public TerritoryDifficulty difficulty;
        }

        /// <summary>살려준 영주 등록 테이블 (key = territoryId.ToString()).</summary>
        private static readonly Dictionary<string, SparedLordEntry> _sparedLords =
            new Dictionary<string, SparedLordEntry>();

        /// <summary>마지막으로 공물을 지급한 주차 인덱스 (게임일/7). -1 = 미지급.</summary>
        private static int _lastPaidWeek = -1;

        private static bool _subscribed;

        // ===== 구독 =====
        /// <summary>TimeManager 구독 보장 (멱등). RuntimeInitializeOnLoadMethod로 확정 구독.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureSubscribed();

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            var tm = TimeManager.GetOrCreate();
            if (tm == null) return;
            tm.OnDayStart += OnDayStart;
            _subscribed = true;
        }

        // ===== 등록/해제 =====

        /// <summary>
        /// 살려준 영주 공물 등록 — SpareLord 훅에서 호출. 멱등(재등록은 내용 갱신).
        /// </summary>
        public static void RegisterSparedLord(TerritoryId territoryId, LordPersonality personality,
            TerritoryDifficulty difficulty)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;
            var state = db.GetState(territoryId);
            if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
            {
                Debug.LogWarning($"[SparedLordTribute] 등록 실패 — 플레이어 소유 아님: {territoryId}");
                return;
            }

            _sparedLords[territoryId.ToString()] = new SparedLordEntry
            {
                personality = personality,
                difficulty = difficulty
            };
            EnsureSubscribed();

            int weekly = CalculateWeeklyTribute(difficulty, personality);
            var def = db.GetDefinition(territoryId);
            Debug.Log($"[SparedLordTribute] 🤝 공물 등록: {def.territoryName} {def.lord.lordName} — 주 {weekly}G ({TRIBUTE_INTERVAL_DAYS}일마다)");

            WarNotificationUI.ShowNotification(
                $"🤝 {def.lord.lordName}이(가) {def.territoryName}의 공물로 매주 {weekly}G을(를) 바치기로 했습니다",
                WarNotificationUI.NotificationType.TerritoryGained);
        }

        /// <summary>공물 등록 해제 — 배신/소유 상실/영주 사망 시. 없으면 무시(멱등).</summary>
        public static void UnregisterSparedLord(TerritoryId territoryId, string reason)
        {
            if (!_sparedLords.Remove(territoryId.ToString())) return;
            Debug.Log($"[SparedLordTribute] 공물 해제: {territoryId} ({reason})");
        }

        /// <summary>현재 공물 등록된 영지 키 목록 (배신 시스템 판정 대상 열거용).</summary>
        public static IReadOnlyCollection<TerritoryId> GetSparedTerritories()
        {
            var list = new List<TerritoryId>();
            var db = TerritoryDatabase.Instance;
            if (db == null) return list;
            foreach (var key in _sparedLords.Keys)
            {
                var state = db.GetState(key);
                if (state != null)
                    list.Add(state.id);
            }
            return list;
        }

        /// <summary>영지가 공물 등록 상태인지.</summary>
        public static bool IsRegistered(TerritoryId territoryId)
            => _sparedLords.ContainsKey(territoryId.ToString());

        // ===== 액수 산식 =====

        /// <summary>살려주기 즉시 보상 표 (MercyUI.GetSpareGoldReward 실측 — 난이도 기반).</summary>
        public static int GetSpareGoldReward(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 50;
                case TerritoryDifficulty.Ring2: return 150;
                case TerritoryDifficulty.Ring3: return 300;
                case TerritoryDifficulty.Ring4: return 600;
                case TerritoryDifficulty.Empire: return 1200;
                default: return 50;
            }
        }

        /// <summary>성향별 공물 계수 — 욕심쟁이/잔인한 영주는 덜 바침 (성격 반영).</summary>
        public static float GetPersonalityTributeFactor(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Cowardly: return 1.2f;
                case LordPersonality.Neutral: return 1.0f;
                case LordPersonality.Wise: return 1.0f;
                case LordPersonality.Suspicious: return 0.9f;
                case LordPersonality.Brave: return 0.8f;
                case LordPersonality.Cruel: return 0.7f;
                case LordPersonality.Greedy: return 0.6f;
                default: return 1.0f;
            }
        }

        /// <summary>주간 공물 액수 — 보상표 × 40% × 성향 계수, Round, 최소 10G.</summary>
        public static int CalculateWeeklyTribute(TerritoryDifficulty difficulty, LordPersonality personality)
        {
            float baseWeekly = GetSpareGoldReward(difficulty) * TRIBUTE_RATIO;
            int amount = Mathf.RoundToInt(baseWeekly * GetPersonalityTributeFactor(personality));
            return Mathf.Max(MIN_TRIBUTE, amount);
        }

        // ===== 주간 결산 =====

        private static void OnDayStart()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;
            int day = tm.CurrentDay;
            if (day % TRIBUTE_INTERVAL_DAYS != 0) return;

            int week = day / TRIBUTE_INTERVAL_DAYS;
            if (week == _lastPaidWeek)
            {
                Debug.Log($"[SparedLordTribute] {day}일차(주 {week}) 공물은 이미 지급됨 — 스킵");
                return;
            }

            var player = PlayerStats.Instance;
            if (player == null)
            {
                Debug.LogWarning("[SparedLordTribute] PlayerStats.Instance 없음 — 공물 지급 불가");
                return;
            }

            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            int totalPaid = 0;
            int paidCount = 0;

            foreach (var kvp in _sparedLords)
            {
                var state = db.GetState(kvp.Key);
                // 소유 상실/상태 불일치 — 등록 정리
                if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
                {
                    _sparedLords.Remove(kvp.Key);
                    continue;
                }

                var def = db.GetDefinition(state.id);
                int amount = CalculateWeeklyTribute(kvp.Value.difficulty, kvp.Value.personality);

                player.AddGold(amount, "lord_tribute");
                state.tributeWeeksPaid++;
                state.loyaltyToPlayer += LOYALTY_PER_TRIBUTE;
                totalPaid += amount;
                paidCount++;

                string msg = $"🏛️ {def.territoryName} {def.lord.lordName}이(가) 주간 공물 {amount}G을(를) 바쳤습니다";
                WarNotificationUI.ShowNotification(msg, WarNotificationUI.NotificationType.TerritoryGained);
                CombatLog.AddEntry(msg, LogType.Normal);
                Debug.Log($"[SparedLordTribute] 💰 {msg} (누적 {state.tributeWeeksPaid}주, 충성도 +{LOYALTY_PER_TRIBUTE})");
            }

            if (paidCount > 0)
                Debug.Log($"[SparedLordTribute] {day}일차 주간 공물 결산 완료 — {paidCount}영지 합계 {totalPaid}G");

            _lastPaidWeek = week;
        }

        // ===== 세이브 (v5) =====

        /// <summary>공물 멱등 상태 내보내기 (항상 non-null — 레거시 판별은 로드 측).</summary>
        public static SparedLordSaveData ExportSaveData()
        {
            return new SparedLordSaveData { lastTributeWeek = _lastPaidWeek };
        }

        /// <summary>
        /// 공물 상태 복원 — 등록은 세이브된 lordSpared/PlayerOwned 상태에서 재구성하고,
        /// lastTributeWeek는 블록 복원. 레거시(null) 세이브는 현재 주차로 시딩해 소급 청구를 막는다
        /// (LaborMarketSystem.ImportSaveData 선례).
        /// </summary>
        public static void ImportSaveData(SparedLordSaveData data, int currentDay)
        {
            _lastPaidWeek = data?.lastTributeWeek ?? currentDay / TRIBUTE_INTERVAL_DAYS;

            _sparedLords.Clear();
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                foreach (var state in db.GetAllStates())
                {
                    if (!state.lordSpared || state.ownership != TerritoryOwnership.PlayerOwned) continue;
                    var def = db.GetDefinition(state.id);
                    _sparedLords[state.id.ToString()] = new SparedLordEntry
                    {
                        personality = def.lord.personality,
                        difficulty = def.difficulty
                    };
                }
            }

            EnsureSubscribed();
            Debug.Log($"[SparedLordTribute] 세이브 복원 — 공물 영주 {_sparedLords.Count}곳, lastPaidWeek={_lastPaidWeek}");
        }

        // ===== 리셋 (테스트) =====
        /// <summary>모든 static 상태 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            _sparedLords.Clear();
            _lastPaidWeek = -1;
            Debug.Log("[SparedLordTribute] 모든 상태 초기화 완료");
        }
    }
}
