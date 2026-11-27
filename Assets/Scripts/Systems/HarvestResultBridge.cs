using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// F3 — 낚시/채집/광질 결과 팝업 공용 브리지 (Systems static).
    /// Systems가 UI.Toolkit을 직접 참조하면 asmdef 역전(CS0234)이므로,
    /// 이 브리지에 마지막 발생한 획득 정보를 publish하고 UI(HarvestResultUTK)가 16ms 폴링으로 소비한다.
    /// (BowAimState/HoverTargetClassifier 패턴과 동일 정신)
    ///
    /// throttle: 병사(GuardPlaceholder)가 채집/광질을 다수 동시에 수행하면 팝업이 스팸이 되므로,
    ///   non-immediate publish는 마지막 표시로부터 ThrottleSec(1.5s) 안이면 데이터만 교체하고
    ///   LatestToken은 갱신 안 해(UI 재표시 차단) 마지막 것만 한 번 표시한다.
    ///   낚시(플레이어 직접)는 immediate=true — 항상 즉시 표시.
    ///
    /// 시스템 호출: HarvestResultBridge.Publish(kind, verb, itemName, desc, rarity, count, tip, itemData, immediate=false)
    /// UI 소비: LatestToken 증가 감지 → LatestXXX 읽어 표시 → UI가 표시 후 MarkConsumed() 호출.
    /// </summary>
    public static class HarvestResultBridge
    {
        // [2026-10-11 28차] Woodcutting 신설 — 벌목(통나무)도 낚시/채집/채광과 같은 피그마 결과 창으로.
        public enum HarvestKind { Fishing, Gathering, Mining, Woodcutting }

        /// <summary>병사 작업 결과 팝업 최소 표시 간격(초). 이 안의 재발행은 데이터만 교체.</summary>
        public const float ThrottleSec = 1.5f;

        /// <summary>발생 시마다 증가 — UI가 이 값 변화로 새 결과 감지.</summary>
        public static int LatestToken { get; private set; }

        public static HarvestKind LatestKind { get; private set; } = HarvestKind.Fishing;
        public static string LatestResultVerb { get; private set; } = "";
        public static string LatestItemName { get; private set; } = "";
        public static string LatestDescription { get; private set; } = "";
        public static ItemRarity LatestRarity { get; private set; } = ItemRarity.Common;
        public static int LatestCount { get; private set; } = 1;
        public static string LatestTip { get; private set; } = "";
        public static PlayerInventory.ItemData LatestItemData { get; private set; }

        // UI 총중복 표시 방지를 위한 consume 플래그 (UI가 표시 후 세팅).
        private static bool _consumed = true;
        private static float _lastPublishedTime = -1f;

        public static bool IsConsumed => _consumed;

        /// <summary>새 획득 결과 publish. 호출 즉시 UI(열려 있으면/다음 폴링)가 표시.
        /// immediate=false(병사 자동작업 채집/광질)면 ThrottleSec 안 재발행은 데이터만 교체(스팸 방지).</summary>
        public static void Publish(HarvestKind kind, string resultVerb, string itemName,
            string description, ItemRarity rarity, int count, string tip,
            PlayerInventory.ItemData itemData = null, bool immediate = false)
        {
            // [133차] 획득 징글 — Publish 1회당 1회(immediate 여부·throttle 무관하게 호출 시점 1회).
            //   rarity 티어 산출(ItemRarity 실측: Common=0/Uncommon=1/Rare=2/Epic=3/Legendary=4/Unique=5):
            //   0=일반(Common) / 1=희귀(Uncommon·Rare) / 2=영웅 이상(Epic·Legendary·Unique).
            int jingleTier = rarity == ItemRarity.Common ? 0
                : (rarity == ItemRarity.Uncommon || rarity == ItemRarity.Rare) ? 1
                : 2;
            GatherFeedbackSystem.PlayAcquireJingle(jingleTier);

            // [133차 SourceContract] 병사 경로 배제 판정(130차 병사 배제 계약) — Publish 호출부 전수 grep 실측(133차 시점):
            //   ① FishingSystem.cs L381/L431 — 플레이어 낚시 성공(자동릴/TryCatch). 병사 낚시 경로 없음.
            //   ② GatheringSystem.cs L116 TryGather — 런타임 호출부 없음(GuardTaskSystem은 CanGather만 사용,
            //      L922 주석에 "TryGather는 ... 호출하지 않고" 명시 — 병사 원정은 자체 집계 경로).
            //   ③ HerbPickup.cs L340 Harvest — 플레이어 채널 완료 콜백(E키). 병사 자동채집 TryAutoGather는 Publish 미경유.
            //   ④ ResourceNode.cs L163 HarvestForPlayer — 플레이어 채널 완료 콜백(E키). 병사 TryAutoMine은 Publish 미경유.
            //   → 병사 전용 경로가 Publish를 경유하지 않으므로 태그 필터 불필요. 향후 병사가 Publish를
            //     경유하게 되면 이 주석 위치에 플레이어 태그 게이트를 추가할 것.
            LatestKind = kind;
            LatestResultVerb = resultVerb ?? "";
            LatestItemName = itemName ?? "";
            LatestDescription = description ?? "";
            LatestRarity = rarity;
            LatestCount = Mathf.Max(1, count);
            LatestTip = tip ?? "";
            LatestItemData = itemData;

            float now = Time.time;
            bool withinThrottle = !immediate && _lastPublishedTime >= 0f && (now - _lastPublishedTime) < ThrottleSec;
            _lastPublishedTime = now;

            if (withinThrottle)
                return;   // 데이터는 교체했지만 토큰/consume 플래그는 그대로 — UI 재표시 안 함.

            _consumed = false;
            LatestToken++;
        }

        /// <summary>UI가 표시를 마치고 호출 (다음 publish 전까지 중복 표시 방지).</summary>
        public static void MarkConsumed()
        {
            _consumed = true;
        }
    }
}
