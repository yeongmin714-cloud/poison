using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase D: 영지 레벨 기반 재화·아이템·병사 몰수 시스템.
    ///
    /// - 영지 금고(TerritoryState.territoryGold)를 난이도 기반으로 결정론적 롤링(시딩) — 재시작 간 동일 값 보장.
    /// - 플레이어 점령 시(ExecuteLord/SpareLord/ExecutePoisonTakeover/ExecuteAssassination 훅) 금고 골드,
    ///   영지 창고 아이템, 생존 병사(포섭 상한 내)를 몰수/포섭한다.
    /// - 멱등 보장: 세션 내 이미 몰수한 영지는 재몰수하지 않음(중복 지급 방지).
    ///   소유권이 플레이어에게서 떠났다가 재점령되면 재몰수를 허용한다.
    /// - 몰수 대상은 플레이어 점령(PlayerOwned)뿐. AI-AI 전쟁(LordOwned 유지)·세이브 복원
    ///   (OwnershipRestoreMode)·영지 등록(RegisterPlayerTerritory)은 대상 아님.
    /// </summary>
    public static class TerritoryLootSystem
    {
        // ===== 난이도별 기본 금고 구간 (결정론 롤링) =====

        private static readonly Dictionary<TerritoryDifficulty, (int min, int max)> _goldRanges =
            new Dictionary<TerritoryDifficulty, (int, int)>
            {
                { TerritoryDifficulty.Ring1, (50, 150) },
                { TerritoryDifficulty.Ring2, (200, 400) },
                { TerritoryDifficulty.Ring3, (500, 900) },
                { TerritoryDifficulty.Ring4, (1000, 1800) },
                { TerritoryDifficulty.Empire, (3000, 3000) },   // 황제국 고정 금고
            };

        // ===== 기본 보급품 풀 (창고가 텅 빈 영지 몰수 시 1~3종 시딩) =====

        private static readonly PlayerInventory.ItemData[] _supplyPool =
        {
            PlayerInventory.Bomb_Explosive,
            PlayerInventory.Herb_Red,
            PlayerInventory.Herb_Yellow,
        };

        /// <summary>세션 내 몰수 완료 마킹 (멱등 — 중복 지급 방지). 소유권 이탈 시 제거되어 재점령 시 재몰수 허용.</summary>
        private static readonly HashSet<string> _confiscatedThisSession = new HashSet<string>();

        // ================================================================
        //  영지 금고 시딩 (결정론)
        // ================================================================

        /// <summary>
        /// 난이도별 금고 구간 조회. 미정의 난이도는 최하위 구간 폴백.
        /// </summary>
        public static (int min, int max) GetGoldRange(TerritoryDifficulty difficulty)
        {
            if (_goldRanges.TryGetValue(difficulty, out var range))
                return range;
            return _goldRanges[TerritoryDifficulty.Ring1];
        }

        /// <summary>
        /// TerritoryDatabase.GetDeterministicHash와 동일 알고리즘의 결정론적 문자열 해시.
        /// (string.GetHashCode()는 .NET Core 5+에서 프로세스마다 값이 바뀌므로 사용 불가 — 자체 정의)
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

        /// <summary>
        /// 영지 금고 기본값을 결정론적으로 롤링한다 (난이도 구간 내, 영지 ID 기반 고정 시드 →
        /// 재시작·재생성 간 항상 동일 금액).
        /// </summary>
        public static int RollTerritoryGold(TerritoryId id)
        {
            var def = TerritoryDatabase.Instance.GetDefinition(id);
            var (min, max) = GetGoldRange(def.difficulty);

            var rng = new System.Random(DeterministicHash($"{id}_territory_gold"));
            int rolled = min + (int)(rng.NextDouble() * (max - min + 1));
            return Mathf.Clamp(rolled, min, max);
        }

        /// <summary>
        /// 영지 금고가 0(미시딩)이면 난이도 기본값으로 채운다. 멱등 — 이미 0보다 크거나,
        /// 이 소유 구간에서 이미 몰수가 완료된 영지는 재시딩하지 않는다.
        /// 소유권이 플레이어에게서 떠난 영지는 몰수 마킹을 해제해 재점령 시 재시딩·재몰수를 허용한다.
        /// 점령 훅에서 몰수 직전 1회 호출.
        /// </summary>
        public static void EnsureTerritoryGold(TerritoryId id)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            try
            {
                var state = db.GetState(id);
                if (state == null) return;

                string key = id.ToString();
                if (state.ownership != TerritoryOwnership.PlayerOwned)
                {
                    _confiscatedThisSession.Remove(key);   // 소유 이탈 — 재점령 시 재몰수 허용
                }
                else if (_confiscatedThisSession.Contains(key))
                {
                    return;   // 이 소유 구간에서 몰수 완료 — 금고 재시딩 없음 (멱등)
                }

                if (state.territoryGold > 0) return;   // 이미 시딩됨 (멱등)

                int rolled = RollTerritoryGold(id);
                state.territoryGold = rolled;
                Debug.Log($"[TerritoryLoot] 영지 금고 시딩: {id} → {rolled}G ({db.GetDefinition(id).difficulty})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TerritoryLoot] 금고 시딩 실패: {id} — {ex.Message}");
            }
        }

        // ================================================================
        //  점령 몰수 (재화 / 아이템 / 병사)
        // ================================================================

        /// <summary>
        /// 플레이어 점령 시 몰수를 실행한다:
        ///  (a) 영지 금고 골드 → PlayerStats.AddGold(source: "confiscate") 후 금고 0 처리
        ///  (b) 영지 창고 아이템 → PlayerInventory 이관(개수 유지) 후 창고 슬롯 비움.
        ///      창고가 텅 비어 있으면 난이도 기반 기본 보급품(1~3종)을 시딩한 뒤 몰수.
        ///  (c) 생존 미포섭 병사 → GuardRecruitSystem.GetMaxRecruits(난이도 레벨) 상한 내 포섭(SetRecruited(true)).
        ///
        /// 반환: 총 몰수 골드. 멱등 — 동일 소유 구간 내 재호출 시 0 반환(중복 지급 없음).
        /// </summary>
        public static int ConfiscateOnCapture(TerritoryId id)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return 0;

            var state = db.GetState(id);
            if (state == null) return 0;

            // 몰수는 플레이어 점령(PlayerOwned)에서만. 소유 이탈 후 재점령이면 재몰수 허용.
            if (state.ownership != TerritoryOwnership.PlayerOwned)
            {
                _confiscatedThisSession.Remove(id.ToString());
                return 0;
            }

            // 멱등 가드 — 이 소유 구간에서 이미 몰수했다면 재지급 없음
            if (!_confiscatedThisSession.Add(id.ToString()))
                return 0;

            int totalGold = 0;
            int totalItems = 0;
            int totalRecruits = 0;

            // (a) 금고 골드 몰수
            try
            {
                totalGold = ConfiscateGold(state);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TerritoryLoot] 금고 몰수 실패: {id} — {ex.Message}");
            }

            // (b) 창고 아이템 몰수 (빈 창고면 기본 보급품 시딩 후 몰수)
            try
            {
                totalItems = ConfiscateWarehouseItems(id);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TerritoryLoot] 창고 아이템 몰수 실패: {id} — {ex.Message}");
            }

            // (c) 생존 병사 몰수 포섭 (상한 내)
            try
            {
                totalRecruits = RecruitSurvivingGuards(id);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TerritoryLoot] 병사 포섭 실패: {id} — {ex.Message}");
            }

            Debug.Log($"[TerritoryLoot] 점령 몰수 {totalGold}골드, {totalItems}아이템, {totalRecruits}병사 포섭 — {id}");
            return totalGold;
        }

        /// <summary>(a) 영지 금고 골드를 플레이어에게 이전하고 0 처리. 실패 시 금고 유지(손실 방지).</summary>
        private static int ConfiscateGold(TerritoryState state)
        {
            int gold = state.territoryGold;
            if (gold <= 0) return 0;

            var stats = PlayerStats.Instance;
            if (stats == null)
            {
                Debug.LogWarning("[TerritoryLoot] PlayerStats 부재 — 금고 골드 보존(몰수 보류)");
                return 0;
            }

            stats.AddGold(gold, "confiscate");
            state.territoryGold = 0;
            return gold;
        }

        /// <summary>(b) 영지 창고 아이템을 플레이어 인벤토리로 이관(개수 유지) 후 슬롯 비움.
        /// 텅 빈 창고는 난이도 기반 기본 보급품(1~3종) 시딩 후 몰수. 이관 실패 슬롯은 창고에 보존.</summary>
        private static int ConfiscateWarehouseItems(TerritoryId id)
        {
            var warehouse = WarehouseSystem.Instance;
            if (warehouse == null) return 0;

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[TerritoryLoot] PlayerInventory 부재 — 창고 아이템 보존(몰수 보류)");
                return 0;
            }

            string key = id.ToString();

            // 텅 빈 창고 → 난이도 기반 기본 보급품 시딩 후 몰수 (몰수 이후엔 재시딩 없음 — 멱등 가드가 담당)
            if (warehouse.GetItemCount(key) == 0)
                SeedDefaultSupplies(key, TerritoryDatabase.Instance.GetDefinition(id).difficulty);

            // 스냅샷(딥카피) 기준 뒤에서부터 이관 — 제거로 인한 인덱스 시프트 방지
            var snapshot = warehouse.GetItems(key);
            int transferred = 0;

            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                var slot = snapshot[i];
                if (slot == null || slot.item == null || slot.count <= 0) continue;

                // AddItem이 false(가득 참/일부 실패)면 창고 슬롯을 건드리지 않아 아이템 손실 방지
                if (!inventory.AddItem(slot.item, slot.count)) continue;

                if (warehouse.RemoveItem(key, i, slot.count))
                    transferred += slot.count;
            }

            return transferred;
        }

        /// <summary>난이도 기반 기본 보급품 시딩 (과하지 않게 1~3종, Ring4/Empire는 수량 2). 결정론적 종류 수.</summary>
        private static void SeedDefaultSupplies(string key, TerritoryDifficulty difficulty)
        {
            var warehouse = WarehouseSystem.Instance;
            if (warehouse == null) return;

            int diffIndex = Mathf.Clamp((int)difficulty, 0, 4);
            int kinds = Mathf.Clamp(1 + diffIndex / 2, 1, _supplyPool.Length);   // Ring1/2:1, Ring3/4:2, Empire:3
            int perKind = diffIndex >= 3 ? 2 : 1;                                 // Ring4/Empire 수량 2

            for (int i = 0; i < kinds; i++)
            {
                var item = _supplyPool[i];
                if (item == null) continue;
                warehouse.AddItem(key, item, perKind);
            }

            Debug.Log($"[TerritoryLoot] 빈 창고 기본 보급품 시딩: {key} ({kinds}종 ×{perKind})");
        }

        /// <summary>(c) 영지 생존 병사 중 미포섭을 포섭 상한(GetMaxRecruits(난이도 레벨)) 내에서 아군 전환.</summary>
        private static int RecruitSurvivingGuards(TerritoryId id)
        {
            var guardManager = GuardManager.Instance;
            if (guardManager == null) return 0;

            var def = TerritoryDatabase.Instance.GetDefinition(id);
            int level = Mathf.Max(1, (int)def.difficulty + 1);        // Ring1=1 … Empire=5
            int maxRecruits = GuardRecruitSystem.GetMaxRecruits(level);

            var guards = guardManager.GetGuardsInTerritory(id);
            if (guards == null || guards.Count == 0) return 0;

            int recruited = 0;
            foreach (var guard in guards)
            {
                if (recruited >= maxRecruits) break;
                if (guard == null || !guard.IsAlive || guard.IsRecruited) continue;

                guard.SetRecruited(true);
                recruited++;
            }

            if (recruited > 0)
                Debug.Log($"[TerritoryLoot] 생존 병사 포섭: {id} — {recruited}/{guards.Count}명 (상한 {maxRecruits}, 레벨 {level})");

            return recruited;
        }

        // ================================================================
        //  테스트/초기화
        // ================================================================

        /// <summary>세션 내 몰수 마킹 초기화 (테스트/전용). 실제 재화·창고·포섭 상태는 변경하지 않음.</summary>
        public static void ResetAll()
        {
            _confiscatedThisSession.Clear();
        }
    }
}
