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
        public enum HarvestKind { Fishing, Gathering, Mining }

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
