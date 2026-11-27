using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [F3] 살려준 영주 배신 돌발 이벤트 — SpareLord된 영주가 확률적으로 배신해
    /// 영지를 다시 영주 소속(LordOwned)으로 되돌린다. 플레이어는 실내전투로 재탈환해야 한다.
    ///
    /// - 판정: TimeManager.OnDayStart(아침 6시)마다 spare 영지 각각 1회, 결정론 시드
    ///   (TerritoryLootSystem.RollTerritoryGold 선례 — 영지+일차 해시 → 재시작해도 같은 결과)
    /// - 확률: base 0.01 + 성향 가산(Cruel 0.04 ~ Cowardly 0.001) + (1 − 충성도/100) × 0.03
    ///   → 최대 ~7%/일 (평균 2~3주에 한 번). 충성도 높으면 거의 배신 안 함.
    /// - 반복 배신자: betrayalCount ≥ 2 → 확률 ×1.5 (상한 12%)
    /// - 균형 가드: 하루 동시 배신 최대 1곳 (몰아치기 방지 — 두 번째는 다음날로 연기)
    /// - 발생 시: SetOwnership(LordOwned) → OwnershipLost 발화 → 실내 재구성/공물 해제/깃발 회수
    ///   수비 병사+영주 재스폰은 기존 실내 빌더 소유 분기(IndoorSceneTransition)가 자동 처리.
    /// </summary>
    public static class LordBetrayalSystem
    {
        // ===== 확률 상수 =====
        /// <summary>기본 배신 확률 (하루)</summary>
        public const float BASE_PROBABILITY = 0.01f;
        /// <summary>충성도 0 영주의 충성도 가산 최대치</summary>
        public const float LOYALTY_FACTOR = 0.03f;
        /// <summary>2회 이상 배신한 영주 배신 확률 배수</summary>
        public const float REPEAT_BETRAYER_MULTIPLIER = 1.5f;
        /// <summary>배신 확률 상한 (하루)</summary>
        public const float MAX_PROBABILITY = 0.12f;
        /// <summary>하루 동시 배신 가능 영지 수 (균형 가드)</summary>
        public const int MAX_BETRAYALS_PER_DAY = 1;

        /// <summary>영주가 배신했을 때 발생 (territoryId, lordName)</summary>
        public static event System.Action<TerritoryId, string> OnLordBetrayed;

        private static bool _subscribed;

        // ===== 구독 =====
        /// <summary>TimeManager 구독 보장 (멱등). SparedLordTributeSystem과 동일 부트스트랩 패턴.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureSubscribed();

        /// <summary>구독 보장 (공개 — SaveManager 로드 후 확정 구독용).</summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            var tm = TimeManager.GetOrCreate();
            if (tm == null) return;
            tm.OnDayStart += OnDayStart;
            _subscribed = true;
        }

        // ===== 확률 산식 =====

        /// <summary>성향별 배신 가산 — 잔인/용감/탐욕 영주가 배신 잘함.</summary>
        public static float GetPersonalityBetrayalFactor(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Cruel: return 0.04f;
                case LordPersonality.Brave: return 0.03f;
                case LordPersonality.Greedy: return 0.03f;
                case LordPersonality.Suspicious: return 0.02f;
                case LordPersonality.Neutral: return 0.01f;
                case LordPersonality.Wise: return 0.005f;
                case LordPersonality.Cowardly: return 0.001f;
                default: return 0.01f;
            }
        }

        /// <summary>
        /// 배신 확률 (하루) — base + 성향 + 충성도 역비례, 반복 배신자 ×1.5, 상한 클램프.
        /// </summary>
        public static float GetBetrayalProbability(float loyaltyToPlayer, LordPersonality personality,
            int betrayalCount)
        {
            float loyaltyFactor = (1f - Mathf.Clamp01(loyaltyToPlayer / 100f)) * LOYALTY_FACTOR;
            float p = BASE_PROBABILITY + GetPersonalityBetrayalFactor(personality) + loyaltyFactor;
            if (betrayalCount >= 2) p *= REPEAT_BETRAYER_MULTIPLIER;
            return Mathf.Min(MAX_PROBABILITY, p);
        }

        // ===== 일일 판정 =====

        /// <summary>
        /// TerritoryLootSystem.DeterministicHash와 동일 알고리즘의 결정론적 문자열 해시.
        /// (string.GetHashCode()는 .NET Core 5+에서 프로세스마다 값이 바뀌므로 사용 불가)
        /// </summary>
        private static int DeterministicHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        private static void OnDayStart()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;
            int day = tm.CurrentDay;

            int betrayalsToday = 0;
            // 등록 목록 사본 — 배신 중 Unregister로 컬렉션 수정 방지
            var spared = new List<TerritoryId>(SparedLordTributeSystem.GetSparedTerritories());

            foreach (var territoryId in spared)
            {
                if (betrayalsToday >= MAX_BETRAYALS_PER_DAY) break; // 균형 가드 — 다음날로 연기

                var db = TerritoryDatabase.Instance;
                if (db == null) return;
                var state = db.GetState(territoryId);
                if (state == null || state.ownership != TerritoryOwnership.PlayerOwned || !state.lordSpared)
                    continue; // 이미 소유 상실/등록 불일치

                var def = db.GetDefinition(territoryId);

                // 결정론 롤 — 영지+일차 시드 (재시작해도 같은 판정)
                var rng = new System.Random(DeterministicHash($"{territoryId}_betrayal_{day}"));
                double roll = rng.NextDouble();
                float p = GetBetrayalProbability(state.loyaltyToPlayer, def.lord.personality, state.betrayalCount);

                if (roll >= p) continue;
                betrayalsToday++;
                ExecuteBetrayal(territoryId, state, def);
            }
        }

        /// <summary>배신 실행 — 소유 역전 + 상태 복구 + 공물 해제 + 깃발 회수 + 알림.</summary>
        private static void ExecuteBetrayal(TerritoryId territoryId, TerritoryState state, TerritoryDefinition def)
        {
            var db = TerritoryDatabase.Instance;

            // 1) 영주 배신 상태 갱신 — 항복 상태 해제(재탈환 흐름에서 다시 항복 대화 가능)
            LordSurrenderSystem.MarkLordBetrayed(territoryId);

            // 2) 소유 역전 — OwnershipLost 발화 → 실내 재구성(UI)/공물 해제/알림 통지
            db.SetOwnership(territoryId, TerritoryOwnership.LordOwned);

            // [123차] 소유 영주 복귀 — 배신한 원래 영주(def.lord.lordName)로 소유 영주 기록 복원.
            // 배신은 "원래 영주가 플레이어에게서 되찾는" 경로이므로 승리 영주 = 원래 영주.
            db.SetOwnerLord(territoryId.ToString(), def.lord.lordName);

            // 3) 영지 상태 복구 — spare 해제, 충성도 리셋, 배신 카운트 누적
            state.lordSpared = false;
            state.loyaltyToPlayer = 0f;
            state.betrayalCount++;

            // 4) 공물 등록 해제 (SetOwnership 직후 상태 기준 — 명시 해제로 확정)
            SparedLordTributeSystem.UnregisterSparedLord(territoryId, "배신");

            // 5) 깃발 회수 — 플레이어 깃발을 영주 국가색으로 되돌림
            TerritoryCaptureSystem.OnTerritoryLost(territoryId);

            // 6) 알림 — WarNotificationUTK는 WarNotificationUI를 폴링하므로 원본 소스에 직접 발행
            string lordName = def.lord.lordName;
            string territoryName = def.territoryName;
            string msg = $"⚠️ {lordName}이(가) 배신했습니다! {territoryName}을(를) 되찾으려면 성을 다시 공격하십시오";
            WarNotificationUI.ShowNotification(msg, WarNotificationUI.NotificationType.TerritoryLost);
            CombatLog.AddEntry(msg, LogType.Warning);
            Debug.Log($"[LordBetrayalSystem] 🐍 영주 배신: {lordName} — {territoryName} 영주 소속 역전 (누적 배신 {state.betrayalCount}회)");

            OnLordBetrayed?.Invoke(territoryId, lordName);
        }

        // ===== 리셋 (테스트) =====
        /// <summary>구독 해제 포함 전체 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            var tm = TimeManager.GetOrCreate();
            if (tm != null && _subscribed)
            {
                tm.OnDayStart -= OnDayStart;
                _subscribed = false;
            }
            Debug.Log("[LordBetrayalSystem] 모든 상태 초기화 완료");
        }
    }
}
