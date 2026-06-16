using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-14: AI 국가 간 자동 전쟁 시스템 — AI 영지 간의 자동 전쟁을 관리합니다.
    /// 
    /// Tick 기반으로 작동: 일정 게임 시간 간격마다 AI 국가 간 전쟁을 시작/진행/완료합니다.
    /// 전쟁 진행도(progress 0~100)가 100에 도달하면 전쟁이 완료되고 영지 소유권이 이전됩니다.
    /// 
    /// 사용법:
    ///   AIWarSystem.StartAIWar(attackerId, defenderId);
    ///   AIWarSystem.UpdateAIWars(); // TimeManager 또는 코루틴에서 주기적 호출
    /// </summary>
    public static class AIWarSystem
    {
        // ===== 이벤트 =====

        /// <summary>전쟁이 시작되었을 때 발생 (AIWarData)</summary>
        public static event System.Action<AIWarData> OnWarStarted;

        /// <summary>전쟁 진행도가 변경되었을 때 발생 (AIWarData)</summary>
        public static event System.Action<AIWarData> OnWarProgressed;

        /// <summary>전쟁이 완료되었을 때 발생 (AIWarData)</summary>
        public static event System.Action<AIWarData> OnWarCompleted;

        // ===== 상수 =====

        /// <summary>전쟁 기본 지속 시간 (게임 시간 일)</summary>
        public const int DEFAULT_WAR_DURATION_DAYS = 7;

        /// <summary>전쟁 시작 체크 간격 (게임 시간 일)</summary>
        public const int DEFAULT_CHECK_INTERVAL_DAYS = 3;

        /// <summary>최대 동시 전쟁 수</summary>
        public const int MAX_CONCURRENT_WARS = 5;

        /// <summary>최소 전쟁 간격 (같은 영지, 게임 시간 일)</summary>
        public const int MIN_WAR_COOLDOWN_DAYS = 10;

        // ===== 데이터 =====

        /// <summary>AI 전쟁 데이터 구조체</summary>
        public struct AIWarData
        {
            /// <summary>전쟁 고유 ID</summary>
            public int warId;
            /// <summary>공격 영지 ID</summary>
            public TerritoryId attackerTerritoryId;
            /// <summary>방어 영지 ID</summary>
            public TerritoryId defenderTerritoryId;
            /// <summary>전쟁 시작 게임 일자</summary>
            public int startDay;
            /// <summary>전쟁 진행도 (0~100)</summary>
            public float progress;
            /// <summary>전쟁 최대 지속 일수</summary>
            public int warDurationDays;
            /// <summary>전쟁 완료 여부</summary>
            public bool isCompleted;
        }

        // ===== 내부 상태 =====

        private static readonly List<AIWarData> _activeWars = new List<AIWarData>();
        private static readonly Queue<AIWarData> _completedWars = new Queue<AIWarData>();
        private static readonly Dictionary<string, int> _warCooldowns = new Dictionary<string, int>(); // territoryKey → last war day
        private static int _nextWarId = 1;
        private static int _lastCheckDay = -1;

        /// <summary>활성 전쟁 목록 (읽기 전용 복사본)</summary>
        public static IReadOnlyList<AIWarData> ActiveWars => _activeWars.AsReadOnly();

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// AI 국가 간 전쟁을 시작합니다. 전쟁 중인 영지는 PlayerOwned 또는 Unoccupied가 아니어야 합니다.
        /// </summary>
        /// <param name="attacker">공격 영지 ID</param>
        /// <param name="defender">방어 영지 ID</param>
        /// <param name="currentDay">현재 게임 일자</param>
        /// <returns>전쟁이 성공적으로 시작되었으면 true</returns>
        public static bool StartAIWar(TerritoryId attacker, TerritoryId defender, int currentDay)
        {
            // 유효성 검사
            if (!ValidateWar(attacker, defender, currentDay))
                return false;

            var db = TerritoryDatabase.Instance;
            var defAttacker = db.GetDefinition(attacker);
            var defDefender = db.GetDefinition(defender);

            // 동일 국가 전쟁 금지
            if (defAttacker.nation == defDefender.nation)
            {
                Debug.Log($"[AIWarSystem] 동일 국가 내 전쟁 불가: {defAttacker.nation}");
                return false;
            }

            // 최대 동시 전쟁 수 체크
            if (_activeWars.Count >= MAX_CONCURRENT_WARS)
            {
                Debug.Log($"[AIWarSystem] 최대 동시 전쟁 수 도달 ({MAX_CONCURRENT_WARS})");
                return false;
            }

            // 두 영지가 모두 LordOwned 상태인지 확인
            var stateA = db.GetState(attacker);
            var stateD = db.GetState(defender);
            if (stateA == null || stateD == null) return false;

            if (stateA.ownership != TerritoryOwnership.LordOwned ||
                stateD.ownership != TerritoryOwnership.LordOwned)
            {
                Debug.Log($"[AIWarSystem] AI 전쟁은 LordOwned 영지 간에만 가능합니다.");
                return false;
            }

            // 전쟁 데이터 생성
            AIWarData warData = new AIWarData
            {
                warId = _nextWarId++,
                attackerTerritoryId = attacker,
                defenderTerritoryId = defender,
                startDay = currentDay,
                progress = 0f,
                warDurationDays = DEFAULT_WAR_DURATION_DAYS,
                isCompleted = false
            };

            _activeWars.Add(warData);

            // 쿨다운 등록
            string cooldownKey = GetCooldownKey(attacker, defender);
            _warCooldowns[cooldownKey] = currentDay;

            Debug.Log($"[AIWarSystem] ⚔️ 전쟁 시작! (ID:{warData.warId}) {defAttacker.territoryName} → {defDefender.territoryName}");

            OnWarStarted?.Invoke(warData);

            return true;
        }

        /// <summary>
        /// 모든 활성 전쟁을 업데이트합니다. 매 Tick(게임 일 단위)마다 호출됩니다.
        /// 진행도가 100에 도달하면 전쟁이 완료되고 영지 소유권이 이전됩니다.
        /// </summary>
        public static void UpdateAIWars()
        {
            // 완료된 전쟁 제거
            _activeWars.RemoveAll(w => w.isCompleted);

            for (int i = 0; i < _activeWars.Count; i++)
            {
                AIWarData war = _activeWars[i];

                // 진행도 증가 (하루에 약 100/warDurationDays% 씩)
                float dailyProgress = 100f / war.warDurationDays;
                // 랜덤 변동 추가 (±20%)
                float variation = Random.Range(-dailyProgress * 0.2f, dailyProgress * 0.2f);
                war.progress = Mathf.Clamp(war.progress + dailyProgress + variation, 0f, 100f);

                _activeWars[i] = war;

                OnWarProgressed?.Invoke(war);

                // 전쟁 완료 체크
                if (war.progress >= 100f)
                {
                    CompleteWar(war, i);
                }
            }
        }

        /// <summary>
        /// AI 자동 전쟁 시작을 체크합니다. 매 CHECK_INTERVAL_DAYS마다 호출됩니다.
        /// 무작위 AI 영지 쌍을 선택하여 전쟁을 시작합니다.
        /// </summary>
        /// <param name="currentDay">현재 게임 일자</param>
        public static void CheckAutoWars(int currentDay)
        {
            if (_lastCheckDay == currentDay) return; // 중복 체크 방지
            _lastCheckDay = currentDay;

            // AI 국가 소속의 LordOwned 영지 목록 수집
            var aiTerritories = new List<TerritoryId>();
            var db = TerritoryDatabase.Instance;
            foreach (var def in db.GetAllDefinitions())
            {
                var state = db.GetState(def.id);
                if (state != null && state.ownership == TerritoryOwnership.LordOwned)
                {
                    // PlayterOwned가 아닌 AI 영지
                    aiTerritories.Add(def.id);
                }
            }

            if (aiTerritories.Count < 2) return; // 전쟁에 필요한 영지 부족

            // 무작위 전쟁 쌍 선택 (3쌍 이하)
            int warCount = Mathf.Min(Random.Range(1, 4), aiTerritories.Count / 2);
            for (int w = 0; w < warCount; w++)
            {
                if (_activeWars.Count >= MAX_CONCURRENT_WARS) break;

                // 무작위로 공격자와 방어자 선택
                int attackerIdx = Random.Range(0, aiTerritories.Count);
                int defenderIdx;
                int tries = 0;
                do
                {
                    defenderIdx = Random.Range(0, aiTerritories.Count);
                    tries++;
                }
                while (defenderIdx == attackerIdx && tries < 10);

                if (attackerIdx == defenderIdx) continue;

                TerritoryId attacker = aiTerritories[attackerIdx];
                TerritoryId defender = aiTerritories[defenderIdx];

                StartAIWar(attacker, defender, currentDay);
            }
        }

        /// <summary>
        /// 특정 영지가 현재 전쟁에 참여 중인지 확인합니다.
        /// </summary>
        public static bool IsTerritoryAtWar(TerritoryId territoryId)
        {
            foreach (var war in _activeWars)
            {
                if (war.attackerTerritoryId.Equals(territoryId) ||
                    war.defenderTerritoryId.Equals(territoryId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 특정 영지의 현재 전쟁 데이터 반환 (없으면 null).
        /// </summary>
        public static AIWarData? GetWarForTerritory(TerritoryId territoryId)
        {
            foreach (var war in _activeWars)
            {
                if (war.attackerTerritoryId.Equals(territoryId) ||
                    war.defenderTerritoryId.Equals(territoryId))
                {
                    return war;
                }
            }
            return null;
        }

        /// <summary>
        /// 완료된 전쟁 중 가장 최근 전쟁을 반환하고 큐에서 제거합니다.
        /// </summary>
        public static AIWarData? DequeueCompletedWar()
        {
            if (_completedWars.Count == 0) return null;
            return _completedWars.Dequeue();
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            _activeWars.Clear();
            _completedWars.Clear();
            _warCooldowns.Clear();
            _nextWarId = 1;
            _lastCheckDay = -1;
        }

        // ===== 내부 메서드 =====

        /// <summary>
        /// 전쟁 유효성 검사 — 동일 영지, 플레이어 소유 영지, 쿨다운 등을 체크합니다.
        /// </summary>
        private static bool ValidateWar(TerritoryId attacker, TerritoryId defender, int currentDay)
        {
            // 동일 영지 전쟁 불가
            if (attacker.Equals(defender))
                return false;

            // 이미 전쟁 중인 영지 체크
            foreach (var war in _activeWars)
            {
                if (war.attackerTerritoryId.Equals(attacker) && war.defenderTerritoryId.Equals(defender))
                {
                    Debug.Log($"[AIWarSystem] 이미 동일한 전쟁이 진행 중입니다.");
                    return false;
                }
                if (war.attackerTerritoryId.Equals(defender) && war.defenderTerritoryId.Equals(attacker))
                {
                    Debug.Log($"[AIWarSystem] 역방향 전쟁이 이미 진행 중입니다.");
                    return false;
                }
                if (war.attackerTerritoryId.Equals(attacker) || war.attackerTerritoryId.Equals(defender) ||
                    war.defenderTerritoryId.Equals(attacker) || war.defenderTerritoryId.Equals(defender))
                {
                    Debug.Log($"[AIWarSystem] 영지가 이미 다른 전쟁에 참여 중입니다.");
                    return false;
                }
            }

            // 쿨다운 체크
            string cooldownKey = GetCooldownKey(attacker, defender);
            if (_warCooldowns.TryGetValue(cooldownKey, out int lastWarDay))
            {
                if (currentDay - lastWarDay < MIN_WAR_COOLDOWN_DAYS)
                {
                    Debug.Log($"[AIWarSystem] 쿨다운 중: {currentDay - lastWarDay}/{MIN_WAR_COOLDOWN_DAYS}일");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 전쟁 완료 처리 — 영지 소유권을 승자에게 이전합니다.
        /// </summary>
        private static void CompleteWar(AIWarData war, int index)
        {
            var db = TerritoryDatabase.Instance;
            var stateDefender = db.GetState(war.defenderTerritoryId);
            var stateAttacker = db.GetState(war.attackerTerritoryId);

            if (stateDefender == null || stateAttacker == null) return;

            // 승자는 공격자 (진행도가 100%에 도달 = 공격 성공)
            // 방어 영지 소유권을 공격자의 국가로 이전
            stateDefender.ownership = TerritoryOwnership.LordOwned;
            // 간단히: 공격자 국가의 영지로 표시 (nation은 유지, ownership만 LordOwned로 유지 — 영지 소속 변경은 별도 처리)
            // 완료된 전쟁 정보는 큐에 저장
            AIWarData completed = war;
            completed.isCompleted = true;
            completed.progress = 100f;
            _completedWars.Enqueue(completed);

            _activeWars[index] = completed;

            var defAttacker = db.GetDefinition(war.attackerTerritoryId);
            var defDefender = db.GetDefinition(war.defenderTerritoryId);

            Debug.Log($"[AIWarSystem] 🏁 전쟁 완료! (ID:{war.warId}) {defAttacker.territoryName} 승리 → {defDefender.territoryName} 점령");

            OnWarCompleted?.Invoke(completed);

            // WarNotification 연동
            WarNotificationUI.ShowNotification(
                $"⚔️ {defAttacker.territoryName} → {defDefender.territoryName} 점령!",
                WarNotificationUI.NotificationType.WarEnd);
            WarNotificationUI.ShowNotification(
                $"🏴 {defDefender.territoryName} 영토 상실",
                WarNotificationUI.NotificationType.TerritoryLost);
        }

        /// <summary>
        /// 공격자-방어자 쌍의 쿨다운 키를 생성합니다.
        /// </summary>
        private static string GetCooldownKey(TerritoryId attacker, TerritoryId defender)
        {
            return $"{attacker}_{defender}";
        }
    }
}