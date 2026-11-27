using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [70차] 골드 비아이템화(지갑 단일소스) + 핫바 코인 배지 계약.
    /// 계획서: docs/2026-10-15_70TH_GOLD_WALLET_UNIFICATION_PLAN.md Phase 5.
    /// EditMode AddComponent Awake 미호출 규약(67차 실측) — 싱글턴/슬롯은 리플렉션 세팅, 수료 후 원복.
    /// </summary>
    public class GoldEconomyTests
    {
        private PlayerStats _stats;
        private PlayerInventory _inv;
        private GameObject _statsGo;
        private GameObject _invGo;
        private PlayerStats _prevStats;
        private PlayerInventory _prevInv;

        [SetUp]
        public void SetUp()
        {
            _prevStats = PlayerStats.Instance;
            _prevInv = PlayerInventory.Instance;

            _statsGo = new GameObject("GoldEconomyTest_PlayerStats");
            _stats = _statsGo.AddComponent<PlayerStats>();
            typeof(PlayerStats)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, _stats);
            SetPrivateField(_stats, "_gold", 0);

            _invGo = new GameObject("GoldEconomyTest_PlayerInventory");
            _inv = _invGo.AddComponent<PlayerInventory>();
            typeof(PlayerInventory)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, _inv);
            // Awake 미호출 → 슬롯 배열 리플렉션 초기화.
            int maxSlots = (int)GetPrivateField(_inv, "_maxSlots");
            SetPrivateField(_inv, "_slots", new PlayerInventory.ItemSlot[maxSlots]);
        }

        [TearDown]
        public void TearDown()
        {
            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
            if (_invGo != null) Object.DestroyImmediate(_invGo);
            typeof(PlayerStats)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, _prevStats);
            typeof(PlayerInventory)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, _prevInv);
        }

        // ── 1. AddItem 골드 인터셉트 ──

        [Test]
        public void AddItem_Gold_RoutesToWallet_NotInventory()
        {
            PlayerStats.GoldLedgerEntry entry = default;
            int fired = 0;
            System.Action<PlayerStats.GoldLedgerEntry> handler = e => { fired++; entry = e; };
            PlayerStats.GoldChanged += handler;
            try
            {
                Assert.That(_inv.AddItem(PlayerInventory.Gold, 10), Is.True,
                    "[70차] 골드는 인벤 저장 없이 지갑 라우팅 성공.");
                Assert.That((int)GetPrivateField(_stats, "_gold"), Is.EqualTo(10),
                    "지갑(PlayerStats._gold) +10.");
                Assert.That(_inv.GetItemCount("gold"), Is.EqualTo(0),
                    "인벤에 골드 아이템 슬롯 저장 없음.");
                Assert.That(fired, Is.EqualTo(1), "지갑 원장 GoldChanged 1회 발화.");
                Assert.That(entry.isIncome, Is.True, "획득 = 수입 항목.");
                Assert.That(entry.amount, Is.EqualTo(10));
            }
            finally
            {
                PlayerStats.GoldChanged -= handler;
            }

            // 대조: 일반 아이템은 기존대로 인벤 저장 유지.
            var herb = new PlayerInventory.ItemData
            {
                id = "test_herb_70",
                displayName = "테스트 허브",
                description = "70차 대조 케이스",
                category = PlayerInventory.ItemCategory.Herb,
                maxStack = 10
            };
            Assert.That(_inv.AddItem(herb, 3), Is.True);
            Assert.That(_inv.GetItemCount("test_herb_70"), Is.EqualTo(3),
                "일반 아이템은 인벤 슬롯 저장 유지(대조).");
        }

        // ── 2. Instance null 경로 (EditMode 등) ──

        [Test]
        public void RouteGoldToWallet_NoInstance_NoThrow()
        {
            typeof(PlayerStats)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);

            Assert.DoesNotThrow(() => _inv.AddItem(PlayerInventory.Gold, 5),
                "PlayerStats.Instance null 경로 예외 금지.");
            Assert.That(_inv.GetItemCount("gold"), Is.EqualTo(0),
                "Instance 없어도 인벤에 골드 슬롯 생성 금지.");
        }

        // ── 3. 인벤 그리드 골드 미표시 가드 ──

        [Test]
        public void InventoryGrid_SkipsGoldSlots()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(src, Does.Contain("[70차]"), "70차 골드 스킵 가드 마커.");
            Assert.That(src, Does.Contain("item.id == \"gold\""), "골드 id 스킵 조건.");
            int guard = src.IndexOf("item.id == \"gold\"");
            int used = src.IndexOf("usedSlots++;");
            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(used, Is.GreaterThan(guard),
                "골드 스킵 가드가 usedSlots++(셀 생성)보다 앞서야 한다 — visibleIndices 미편입 계약.");
        }

        // ── 4. 지갑 조회 단일화 (9파일) ──

        [Test]
        public void WalletReaders_Unified()
        {
            string[] files =
            {
                "Assets/Scripts/Systems/FastTravelSystem.cs",
                "Assets/Scripts/UI/FastTravelUI.cs",
                "Assets/Scripts/UI/RepairStationUI.cs",
                // [144차] ChurchUTK/FastTravelUTK/RepairStationUTK 폐기(D5+D6) — 스캔 목록에서 제거(파일 소멸).
                "Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs",
                "Assets/Scripts/UI/Toolkit/PlayerFlagUTK.cs",
                "Assets/Scripts/UI/Toolkit/MercenaryHireUTK.cs",
            };
            foreach (var path in files)
            {
                var lines = System.IO.File.ReadAllLines(path);
                foreach (var raw in lines)
                {
                    var line = raw.TrimStart();
                    bool comment = line.StartsWith("//") || line.StartsWith("///") || line.StartsWith("*");
                    Assert.That(comment || !line.Contains("GetItemCount(\"gold\")"),
                        $"{path}에 인벤 골드 조회 잔존(실제 코드 라인 기준): {raw}");
                }
            }

            var fts = System.IO.File.ReadAllText("Assets/Scripts/Systems/FastTravelSystem.cs");
            Assert.That(fts, Does.Contain("SpendGold(cost, \"fast_travel\")"),
                "빠른이동 결제는 지갑 원장 SpendGold여야 한다.");
        }

        // ── 5. [71차] GoldBalance 위젯 수용(별도 배지 폐기) ──

        [Test]
        public void StandaloneWalletBadge_Removed()
        {
            // [71차] 사용자 요구 — "지갑을 따로 두지 말고": 별도 핫바 지갑 배지(GoldBadgeUTK)는 폐기.
            Assert.That(System.IO.File.Exists("Assets/Scripts/UI/Toolkit/GoldBadgeUTK.cs"), Is.False,
                "70차 별도 지갑 배지(GoldBadgeUTK)는 삭제되어야 한다.");
        }

        [Test]
        public void UtkGoldBalance_SingleSource_SharedByTwoWindows()
        {
            var widget = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKGoldBalance.cs");
            Assert.That(widget, Does.Contain("public static VisualElement Build(out Label valueLabel"),
                "공용 위젯 빌더(인벤/창고 단일소스).");
            Assert.That(widget, Does.Contain("PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0"),
                "골드 값은 지갑 단일소스 조회.");
            Assert.That(widget, Does.Contain("#,##0"), "천단위 콤마 포맷.");

            var inv = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(inv, Does.Contain("UTKGoldBalance.Build(out _goldValue)"),
                "인벤 푸터가 공용 위젯을 수용해야 한다.");
            var wh = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/WarehouseWindowUTK.cs");
            Assert.That(wh, Does.Contain("UTKGoldBalance.Build(out _goldValueWh)"),
                "창고 StorageStats가 공용 위젯을 수용해야 한다.");
        }

        [Test]
        public void FooterGoldBalance_FigmaTruth()
        {
            // 피그마 225:4~7 — GoldBalance WeightRow-로컬 (0,0) 97.4×26.6, pill 46.2×26.6 #FFFFFF/4%,
            // "골드" 14.4/400, GoldValue (53.4,3.8) 14.4/700 #F0F6FC. 푸터 본체 (24,843.6) 456×48.2.
            var regions = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryClusterPanelRegions.cs");
            Assert.That(regions, Does.Contain("new Rect(24f, 843.6f, 456f, 48.2f)"),
                "푸터 본체는 71차 개정 진실치.");
            Assert.That(regions, Does.Contain("new Rect(0f, 7.2f, 456f, 26.6f)"),
                "WeightRow footer-로컬 (0,7.2) 456×26.6.");
            Assert.That(regions, Does.Contain("new Rect(53.4f, 3.8f, 44f, 19f)"),
                "GoldValue WeightRow-로컬 (53.4,3.8).");
            Assert.That(regions, Does.Contain("new Rect(0f, 41f, 456f, 7.2f)"),
                "ProgressBar footer-로컬 (0,41).");
            Assert.That(regions, Does.Contain("new Rect(147.4f, 0f, 97.4f, 26.6f)"),
                "창고 GoldBalance storageStats-로컬 (147.4,0).");

            var widget = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKGoldBalance.cs");
            Assert.That(widget, Does.Contain("new Color(1f, 1f, 1f, 0.04f)"),
                "pill bg #FFFFFF/4%.");
            Assert.That(widget, Does.Contain("\"골드\""), "배지 라벨 텍스트.");

            var inv = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(inv, Does.Contain("PlayerStats.GoldChanged -= OnGoldChangedForFooter"),
                "GoldChanged 구독 — 개방 중 수령 즉시 반영(전리품 골드 → 인벤 UI 골드 증가).");
            Assert.That(inv, Does.Contain("PlayerStats.GoldChanged += OnGoldChangedForFooter"),
                "구독 경로 존재.");
            Assert.That(inv, Does.Contain("UnsubscribeGold();"),
                "창 닫힘 시 구독 해제(누수 방지).");
            Assert.That(inv, Does.Contain("new Color(1f, 1f, 1f, 0.08f)"),
                "ProgressBar bg #FFFFFF/8%.");
            Assert.That(inv, Does.Contain("BuildDumbbellIcon"),
                "WeightTitle dumbbell 절차 아이콘.");
        }

        // ── 헬퍼 ──

        private static object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        }

        private static void SetPrivateField(object obj, string name, object value)
        {
            obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }
    }
}
