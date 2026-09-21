using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// P30-E: 적 영지 영주실 문 개폐 시스템 — 영주 중독도(=영지 마약 오염도) 기반.
    ///
    /// - 영주실 문(LockedDoor, LocationId에 "lord" 포함 — HouseInteriorBuilder "house_lord_room" 등)을
    ///   간격 스캔(SCAN_INTERVAL_SECONDS)한다.
    /// - 도어가 속한 영지(TerritoryDatabase.ResolveTerritoryAt 위치 판별)의 영주 중독도
    ///   (TerritoryDrugSystem.GetLordAddiction — 영지 오염도와 일치)가
    ///   DOOR_OPEN_ADDICTION_THRESHOLD 이상이면 잠금 해제, 미만이면 재잠금.
    /// - 플레이어가 자물쇠 따기로 이미 연 문(LockedDoor.HasBeenOpened)은 재잠금하지 않는다.
    /// - 병사·영주 중독 일치 모델: 병사 중독은 TerritoryDrugSystem.ProcessAllContamination가
    ///   영지 오염도 비례로 상승하고, 영주 중독도(GetLordAddiction)는 영지 오염도와 일치 —
    ///   문 개방은 곧 "병사 중독도 높을 시" 조건을 함께 만족한다.
    /// - 영주 음식주기 게이트: IsLordDoorOpen — 문이 열린 영지에서만 영주 Placeholder
    ///   (LordFeedTarget)에 음식을 줄 수 있다(TryFeedLord).
    /// MonoBehaviour 아님 — TerritoryManager.Update에서 간격 스캔 라이프사이클.
    /// </summary>
    public static class TerritoryLordDoorSystem
    {
        /// <summary>영주실 문 개방 임계 — 영주 중독도(=영지 오염도) 50 이상이면 문 개방.</summary>
        public const float DOOR_OPEN_ADDICTION_THRESHOLD = 50f;

        /// <summary>도어 재스캔 간격(초) — ResolveTerritoryAt 탐색 비용 절감.</summary>
        public const float SCAN_INTERVAL_SECONDS = 0.5f;

        /// <summary>영주실 문 판정용 LocationId 키워드 (대소문자 무시).</summary>
        private const string LordDoorKeyword = "lord";

        /// <summary>개방 상태 이름표 텍스트.</summary>
        private const string DoorLabelOpen = "🚪 영주 방 (열림)";

        /// <summary>재잠금 상태 이름표 텍스트 (HouseInteriorBuilder 원본 라벨과 동일).</summary>
        private const string DoorLabelLocked = "🚪 2층 영주 방 (잠김)";

        /// <summary>마지막 스캔 시각 (Time.time 기준 간격 스캔).</summary>
        private static float _nextScanTime;

        /// <summary>
        /// 영주실 문 개폐 갱신 — TerritoryManager.Update에서 간격 호출.
        /// </summary>
        public static void UpdateDoors()
        {
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + SCAN_INTERVAL_SECONDS;

            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var doors = Object.FindObjectsByType<LockedDoor>();
            if (doors == null) return;

            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                if (door == null) continue;

                // ① 영주실 문 여부 — LocationId에 "lord" 포함(대소문자 무시)
                string locId = door.LocationId;
                if (string.IsNullOrEmpty(locId)) continue;
                if (locId.ToLowerInvariant().Contains(LordDoorKeyword) == false) continue;

                // ② 도어 위치 → 소속 영지 판별 (TerritoryId? — HasValue/Value 접근 규약)
                TerritoryId? territory = db.ResolveTerritoryAt(door.transform.position, 80f);
                if (!territory.HasValue) continue;

                // ③ 영주 중독도(=영지 오염도) 임계 판정 → 개폐
                float lordAddiction = TerritoryDrugSystem.GetLordAddiction(territory.Value);
                bool shouldOpen = lordAddiction >= DOOR_OPEN_ADDICTION_THRESHOLD;

                ApplyDoorState(door, shouldOpen, lordAddiction);
            }
        }

        /// <summary>
        /// 영지의 영주실 문이 열려 있는가 — 영주 음식주기 상호작용 게이트.
        /// 영주 중독도(=영지 오염도)가 DOOR_OPEN_ADDICTION_THRESHOLD 이상이면 열림.
        /// </summary>
        public static bool IsLordDoorOpen(TerritoryId territoryId)
        {
            return TerritoryDrugSystem.GetLordAddiction(territoryId) >= DOOR_OPEN_ADDICTION_THRESHOLD;
        }

        /// <summary>[P30-E] 문자열 영지 키 오버로드 ("East_01" 형식).</summary>
        public static bool IsLordDoorOpen(string territoryKey)
        {
            return TerritoryDrugSystem.GetLordAddiction(territoryKey) >= DOOR_OPEN_ADDICTION_THRESHOLD;
        }

        /// <summary>
        /// 영주에게 음식주기 — 문이 열린 영지(병사/영주 중독 임계 이상)에서만 가능.
        /// 인벤 음식(Food) 1개 제거 → 영지 loyaltyToPlayer +5 (P30-D NPC 선물주기와 동일 패턴).
        /// 영주 중독도는 영지 오염도와 일치(TerritoryDrugSystem.GetLordAddiction)하므로 별도 갱신 불필요.
        /// </summary>
        /// <param name="territoryKey">영지 키 ("East_01" 형식 string).</param>
        /// <param name="foodItemId">지급할 음식 아이템 ID (PlayerInventory.ItemData.id).</param>
        /// <returns>지급 성공 여부.</returns>
        public static bool TryFeedLord(string territoryKey, string foodItemId)
        {
            if (string.IsNullOrEmpty(territoryKey) || string.IsNullOrEmpty(foodItemId)) return false;

            // ① 문 개방 게이트 — 영주 중독도(=영지 오염도) 임계 이상 (병사 중독과 일치 모델)
            if (!IsLordDoorOpen(territoryKey))
            {
                Debug.Log($"[LordDoor] 🔒 영주실 문이 잠겨 있어 음식을 줄 수 없습니다: {territoryKey} " +
                          $"(영주 중독도 {TerritoryDrugSystem.GetLordAddiction(territoryKey):F1} < {DOOR_OPEN_ADDICTION_THRESHOLD})");
                return false;
            }

            // ② 인벤 음식 제거
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[LordDoor] PlayerInventory 없음 — 영주 음식주기 불가");
                return false;
            }
            bool removed = PlayerInventory.Instance.RemoveItem(foodItemId, 1);
            if (!removed)
            {
                Debug.LogWarning($"[LordDoor] 음식 제거 실패: {foodItemId}");
                return false;
            }

            // ③ 영지 충성도 +5
            var state = TerritoryDatabase.Instance.GetState(territoryKey);
            if (state != null)
            {
                state.loyaltyToPlayer = Mathf.Clamp(state.loyaltyToPlayer + 5f, 0f, 100f);
                Debug.Log($"[LordDoor] 🍗 영주에게 음식 지급: {foodItemId} → {territoryKey} 충성도 +5 → " +
                          $"{state.loyaltyToPlayer:F1} (영주 중독도 {TerritoryDrugSystem.GetLordAddiction(territoryKey):F1} = 영지 오염도와 일치)");
            }
            else
            {
                Debug.LogWarning($"[LordDoor] 영지 상태 없음: {territoryKey} — 충성도 미반영(음식은 소모됨)");
            }
            return true;
        }

        /// <summary>
        /// 도어 개폐 적용 — 상태 전환 시에만 잠금/이름표를 갱신한다.
        /// HasBeenOpened(자물쇠 따기 성공/마스터 키) 문은 재잠금하지 않는다.
        /// </summary>
        private static void ApplyDoorState(LockedDoor door, bool shouldOpen, float lordAddiction)
        {
            bool changed = false;

            if (shouldOpen && door.IsLocked)
            {
                door.IsLocked = false;   // LockedDoor 공개 세터 — 잠금 해제 API
                changed = true;
                Debug.Log($"[LordDoor] 🔓 영주실 문 개방: {door.LocationId} " +
                          $"(영주 중독도 {lordAddiction:F1} ≥ {DOOR_OPEN_ADDICTION_THRESHOLD})");
            }
            else if (!shouldOpen && !door.IsLocked && !door.HasBeenOpened)
            {
                door.IsLocked = true;
                changed = true;
                Debug.Log($"[LordDoor] 🔒 영주실 문 재잠금: {door.LocationId} " +
                          $"(영주 중독도 {lordAddiction:F1} < {DOOR_OPEN_ADDICTION_THRESHOLD})");
            }

            if (!changed) return;

            // 개폐 상태에 맞춰 이름표 갱신 (NameplateDisplay 없는 도어는 스킵)
            var doorLabel = door.GetComponent<NameplateDisplay>();
            if (doorLabel != null)
                doorLabel.DisplayName = door.IsLocked ? DoorLabelLocked : DoorLabelOpen;
        }
    }
}
