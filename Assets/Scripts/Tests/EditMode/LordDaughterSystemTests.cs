using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [126차] 영주 딸 결혼 시스템 계약:
    /// ①등장 판정 결정론(FNV-1a %100&lt;40 — 동일 키 동일 결과, 분포 양쪽 존재)
    /// ②들꽃 선물 +10 / 100 클램프 / 100에서 거부(소비 없음) / 꽃 부족 시 무소비 무증가
    /// ③프로포즈 게이트(&lt;80 거부, ≥80 수락 + married + 충성도 +30, 재호출 멱등)
    /// ④TerritorySaveData 딸 필드 저장 왕복 + 구세이브 기본값
    /// ⑤flower_wild 아이템 정의 + HerbPickup Flower_Wild 매핑
    /// ⑥딸 스폰 위치 계약(3개 레이아웃 모두 Bedroom 존 내부)
    /// ⑦스폰 훅(생존 영주 비PlayerOwned && 결정론 롤 — [10-24 사용자 정정], 점령 성 미등장, 멱등)
    /// ⑧런타임 훅 게이트 소스 계약(InvadedLordEncounter 게이트 반전 + IndoorSceneTransition 훅 부재 — 소스 스캔).
    /// 전역 싱글턴 격리: East_01 상태 스냅샷/복원 + PlayerInventory ResetInstance(CraftBenchTests 선례).
    /// </summary>
    [TestFixture]
    public class LordDaughterSystemTests
    {
        private TerritoryState _state;              // East_01 — 테스트 전용 영지(전역 싱글턴, TearDown 복원)
        private float _savedLoyalty;
        private TerritoryOwnership _savedOwnership;
        private int _savedAffection;
        private bool _savedMarried;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _state = TerritoryDatabase.Instance.GetState("East_01");
            Assume.That(_state, Is.Not.Null, "East_01 상태 존재");

            _savedLoyalty = _state.loyaltyToPlayer;
            _savedOwnership = _state.ownership;
            _savedAffection = _state.daughterAffection;
            _savedMarried = _state.daughterMarried;

            _state.ownership = TerritoryOwnership.PlayerOwned;
            _state.daughterAffection = 0;
            _state.daughterMarried = false;
            _state.loyaltyToPlayer = 50f;

            PlayerInventory.ResetInstance();
            var invGo = new GameObject("TestInventory");
            _spawned.Add(invGo);
            var inv = invGo.AddComponent<PlayerInventory>();
            // [EditMode 규약] AddComponent는 Awake 미실행 — Instance/슬롯 리플렉션 보장(CraftBenchTests 선례).
            var instProp = typeof(PlayerInventory).GetProperty("Instance");
            if (instProp != null && instProp.GetValue(null) == null)
                instProp.SetValue(null, inv);
            if (inv.GetAllSlots() == null)
            {
                var f = typeof(PlayerInventory).GetField("_slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var maxF = typeof(PlayerInventory).GetField("_maxSlots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                int max = maxF != null ? (int)maxF.GetValue(inv) : 30;
                f.SetValue(inv, new PlayerInventory.ItemSlot[max]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_state != null)
            {
                _state.loyaltyToPlayer = _savedLoyalty;
                _state.ownership = _savedOwnership;
                _state.daughterAffection = _savedAffection;
                _state.daughterMarried = _savedMarried;
            }
            LordSurrenderSystem.ResetAll();   // [126차] LordOwned 스폰 시 침략 영주 등록 레지스트리 클리어(InvadedLordEncounterTests 선례)
            PlayerInventory.ResetInstance();
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ════════════════════════════════════════════════════════════════
        // 1. 결정론 등장 판정
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void HasLordDaughter_SameKey_AlwaysSameResult()
        {
            bool first = LordDaughterNPC.HasLordDaughter("East_01");
            Assert.AreEqual(first, LordDaughterNPC.HasLordDaughter("East_01"),
                "동일 키 재호출 — 결정론 계약(시간/Random/전역 상태 미사용)");
            Assert.AreEqual(first, LordDaughterNPC.HasLordDaughter("East_01"),
                "3회 호출 모두 동일");
            Assert.IsFalse(LordDaughterNPC.HasLordDaughter(null), "null 키는 false");
            Assert.IsFalse(LordDaughterNPC.HasLordDaughter(""), "빈 키는 false");
        }

        [Test]
        public void HasLordDaughter_RealTerritories_BothSidesPresent()
        {
            // 실영지 전수(4국가 × 1~15, 실존 정의만) — 결정론이므로 분포는 고정.
            // FNV-1a 계산 검증: 실 trues 26/60(양쪽 존재) — 전부 true/전부 false는 계약 위반.
            int trues = 0, reals = 0;
            foreach (var key in RealTerritoryKeys())
            {
                reals++;
                if (LordDaughterNPC.HasLordDaughter(key)) trues++;
            }
            Assert.Greater(reals, 0, "실영지 정의 존재");
            Assert.Greater(trues, 0, "딸이 있는 영지 존재(분포 하단)");
            Assert.Less(trues, reals, "딸이 없는 영지도 존재(전부 true 금지)");
        }

        [Test]
        public void HasLordDaughter_SyntheticKeys_BothSidesPresent()
        {
            // 합성 키 60개 — FNV 계산 검증: trues 24/60(양쪽 존재, 결정론 고정).
            int trues = 0;
            for (int i = 0; i < 60; i++)
                if (LordDaughterNPC.HasLordDaughter($"TestKey_{i:00}")) trues++;
            Assert.Greater(trues, 0, "분포 하단 존재");
            Assert.Less(trues, 60, "분포 상단 존재 — 전부 true 금지");
        }

        [Test]
        public void GetDaughterName_SameKey_Deterministic()
        {
            string name1 = LordDaughterNPC.GetDaughterName("East_01");
            Assert.AreEqual(name1, LordDaughterNPC.GetDaughterName("East_01"),
                "동일 키 이름 결정론(등장 롤과 무상관 재해시)");
            Assert.IsFalse(string.IsNullOrEmpty(LordDaughterNPC.GetDaughterName(null)),
                "무효 키는 풀 첫 이름 폴백(빈 이름 금지)");
        }

        // ════════════════════════════════════════════════════════════════
        // 2. 들꽃 선물 — 호감도
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void GiftFlower_Success_ConsumesOne_GainsTen()
        {
            var daughter = SpawnDaughterInTestRoom();
            var inv = PlayerInventory.Instance;
            inv.AddItem(PlayerInventory.Flower_Wild, 2);

            var result = daughter.GiftFlower(inv);

            Assert.AreEqual(LordDaughterNPC.GiftResult.Success, result);
            Assert.AreEqual(LordDaughterNPC.GiftAffectionGain, _state.daughterAffection,
                "들꽃 1개 선물 = 호감도 +10");
            Assert.AreEqual(1, inv.GetItemCount(LordDaughterNPC.FlowerItemId),
                "꽃 1개 소비(2 → 1)");
        }

        [Test]
        public void GiftFlower_ClampsAtHundred_ThenRejectsWithoutConsume()
        {
            var daughter = SpawnDaughterInTestRoom();
            var inv = PlayerInventory.Instance;

            _state.daughterAffection = 95;   // 95 + 10 → 100 클램프
            inv.AddItem(PlayerInventory.Flower_Wild, 1);
            Assert.AreEqual(LordDaughterNPC.GiftResult.Success, daughter.GiftFlower(inv));
            Assert.AreEqual(100, _state.daughterAffection, "세터 0~100 클램프");

            inv.AddItem(PlayerInventory.Flower_Wild, 1);   // 이제 꽃 1개 보유
            var result = daughter.GiftFlower(inv);
            Assert.AreEqual(LordDaughterNPC.GiftResult.AffectionMaxed, result,
                "호감도 100에서 추가 선물 거부");
            Assert.AreEqual(1, inv.GetItemCount(LordDaughterNPC.FlowerItemId),
                "거부 시 꽃 소비 없음(선물 계약 — 소비 없음)");
        }

        [Test]
        public void GiftFlower_NoFlower_NoGain_NoConsume()
        {
            var daughter = SpawnDaughterInTestRoom();
            var inv = PlayerInventory.Instance;

            _state.daughterAffection = 50;
            var result = daughter.GiftFlower(inv);

            Assert.AreEqual(LordDaughterNPC.GiftResult.NoFlower, result,
                "꽃 보유 0 — 선물 실패(피드백 계약 대상)");
            Assert.AreEqual(50, _state.daughterAffection, "호감도 무증가");
        }

        // ════════════════════════════════════════════════════════════════
        // 3. 프로포즈 — 게이트/멱등
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void TryPropose_BelowThreshold_Rejected_NoStateChange()
        {
            var daughter = SpawnDaughterInTestRoom();
            _state.daughterAffection = LordDaughterNPC.ProposeAffectionThreshold - 1;   // 79
            float before = _state.loyaltyToPlayer;

            var result = daughter.TryPropose();

            Assert.AreEqual(LordDaughterNPC.ProposeResult.NotReady, result, "호감도 80 미만은 거부");
            Assert.IsFalse(_state.daughterMarried, "married 불변");
            Assert.AreEqual(before, _state.loyaltyToPlayer, "충성도 재가 없음");
        }

        [Test]
        public void TryPropose_AtThreshold_Accepts_Once_Idempotent()
        {
            var daughter = SpawnDaughterInTestRoom();
            _state.daughterAffection = LordDaughterNPC.ProposeAffectionThreshold;   // 80
            float before = _state.loyaltyToPlayer;   // 50

            var result = daughter.TryPropose();

            Assert.AreEqual(LordDaughterNPC.ProposeResult.Accepted, result, "호감도 80 도달 — 수락");
            Assert.IsTrue(_state.daughterMarried, "결혼 상태 설정");
            Assert.AreEqual(before + LordDaughterNPC.MarriageLoyaltyBonus, _state.loyaltyToPlayer,
                "결혼 1회 충성도 +30");

            var again = daughter.TryPropose();
            Assert.AreEqual(LordDaughterNPC.ProposeResult.AlreadyMarried, again, "재호출 — 이미 부부");
            Assert.AreEqual(before + LordDaughterNPC.MarriageLoyaltyBonus, _state.loyaltyToPlayer,
                "멱등 — 충성도 재가 없음");
        }

        // ════════════════════════════════════════════════════════════════
        // 4. 저장 왕복
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void TerritorySaveData_DaughterFields_RoundTripAndLegacyDefault()
        {
            var dto = new TerritorySaveData { daughterAffection = 70, daughterMarried = true };
            var restored = JsonUtility.FromJson<TerritorySaveData>(JsonUtility.ToJson(dto));
            Assert.AreEqual(70, restored.daughterAffection, "호감도 세이브 왕복 보존");
            Assert.IsTrue(restored.daughterMarried, "결혼 상태 세이브 왕복 보존");

            var legacy = JsonUtility.FromJson<TerritorySaveData>("{\"territoryId\":\"East_01\"}");
            Assert.AreEqual(0, legacy.daughterAffection, "구세이브 누락 필드 = 0(기본값)");
            Assert.IsFalse(legacy.daughterMarried, "구세이브 누락 필드 = false(기본값)");
        }

        [Test]
        public void TerritoryState_DaughterAffection_ClampsToZeroHundred()
        {
            _state.daughterAffection = 150;
            Assert.AreEqual(100, _state.daughterAffection, "상한 100 클램프");
            _state.daughterAffection = -5;
            Assert.AreEqual(0, _state.daughterAffection, "하한 0 클램프");
        }

        // ════════════════════════════════════════════════════════════════
        // 5. 아이템 — 꽃 정의 / HerbPickup 매핑
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void FlowerWild_ItemDefinition_Contract()
        {
            Assert.AreEqual("flower_wild", PlayerInventory.Flower_Wild.id, "아이템 ID 계약");
            Assert.AreEqual("들꽃", PlayerInventory.Flower_Wild.displayName);
            Assert.AreEqual(PlayerInventory.ItemCategory.Material, PlayerInventory.Flower_Wild.category,
                "꽃은 재료(Material) 계열 — 향후 조리/제작 확장 여지");
            Assert.AreEqual(50, PlayerInventory.Flower_Wild.maxStack);
            Assert.AreEqual(LordDaughterNPC.FlowerItemId, PlayerInventory.Flower_Wild.id,
                "선물 아이템 ID 단일 소스 정합");
        }

        [Test]
        public void HerbPickup_HerbMap_MapsFlowerWildToFlowerItem()
        {
            var field = typeof(HerbPickup).GetField("HerbMap", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "HerbPickup.HerbMap 매핑 테이블 존재");
            var map = field.GetValue(null) as System.Collections.IEnumerable;
            Assert.That(map, Is.Not.Null, "HerbMap 순회 가능");

            bool found = false;
            foreach (var entry in map)
            {
                var t = entry.GetType();
                var herbType = (HerbPickup.HerbType)t.GetField("Item1").GetValue(entry);
                if (herbType != HerbPickup.HerbType.Flower_Wild) continue;
                var item = (PlayerInventory.ItemData)t.GetField("Item2").GetValue(entry);
                Assert.AreEqual("flower_wild", item.id, "Flower_Wild 매핑 → 들꽃 아이템");
                found = true;
            }
            Assert.IsTrue(found, "HerbMap에 Flower_Wild 항목 존재(꽃 노드 채집 파이프라인 계약)");
        }

        // ════════════════════════════════════════════════════════════════
        // 6. 위치 계약 — 3개 레이아웃 모두 Bedroom 존 내부
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void TryComputeSpawnPosition_AllLayouts_LandsInBedroomZone()
        {
            InteriorLayoutDefinition[] layouts =
            {
                InteriorLayoutDefinition.KeepOctagon,
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };
            foreach (var layout in layouts)
            {
                var room = MakeContextRoom(layout);
                _spawned.Add(room);
                Assert.IsTrue(LordDaughterNPC.TryComputeSpawnPosition(room, out var local),
                    $"{layout.LayoutId}: 오프셋 앵커가 Bedroom 밖 — 스폰 생략 계약 위반");
                Assert.IsTrue(InvadedLordEncounter.IsNorthernBedroomPosition(room, new Vector2(local.x, local.z)),
                    $"{layout.LayoutId}: 딸 스폰 위치는 Bedroom 존 내부 계약");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // 7. 스폰 훅 — PlayerOwned 게이트 + 멱등
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void SpawnHook_AliveLordTerritory_SpawnsDaughter_Idempotent()
        {
            // [126차 정정] East_01 결정론 롤 true(실측) — 생존 영주(LordOwned) 성 진입 경로에서만 딸 생성.
            Assume.That(LordDaughterNPC.HasLordDaughter("East_01"), Is.True, "East_01 결정론 롤 true 실측");
            _state.ownership = TerritoryOwnership.LordOwned;   // SetUp의 PlayerOwned → 생존 영주로 정정

            var room = new GameObject("TestRoom_Hook");
            _spawned.Add(room);

            // 영주 원본 스폰 경로(InvadedLordEncounter 106~114행)는 Play 검증된 원본 — 수정 없이
            // EditMode 에러 로그 2종(Destroy 자동콜/renderer.material 자동 인스턴스화)을 기대 처리.
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("(?s).*Destroy may not be called from edit mode.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("(?s).*Instantiating material due to calling renderer.material.*"));

            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, "East_01");
            Assert.IsNotNull(lord, "생존 영주(침략 경로) 스폰");
            var daughter = room.GetComponentInChildren<LordDaughterNPC>(true);
            Assert.IsNotNull(daughter, "생존 영주 성 진입 + 결정론 롤 true → 영주실에 딸 스폰");
            Assert.AreEqual("East_01", daughter.TerritoryKey);

            // 멱등 — 재진입/중복 훅에서 이중 스폰 금지
            var again = LordDaughterNPC.TrySpawnForTerritory(room, "East_01");
            Assert.AreSame(daughter, again, "같은 방/키 재호출은 기존 인스턴스 반환");
            Assert.AreEqual(1, room.GetComponentsInChildren<LordDaughterNPC>(true).Length,
                "딸 인스턴스 1개 유지");
        }

        [Test]
        public void SpawnHook_ConqueredTerritory_NoDaughter()
        {
            // [126차 정정] 점령(PlayerOwned) 완료 성에는 딸이 등장하지 않는다(생존 영주 전용 계약).
            _state.ownership = TerritoryOwnership.PlayerOwned;

            var room = new GameObject("TestRoom_HookConquered");
            _spawned.Add(room);

            // 영주 원본 스폰 경로의 EditMode 에러 로그 2종 — 기대 처리(원본 미수정).
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("(?s).*Destroy may not be called from edit mode.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("(?s).*Instantiating material due to calling renderer.material.*"));

            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, "East_01");
            Assert.IsNotNull(lord, "점령 성에서도 침략 영주 스폰 자체는 기존 계약(무간섭)");
            Assert.IsNull(room.GetComponentInChildren<LordDaughterNPC>(true),
                "PlayerOwned 성 진입 — 딸 미등장(생존 영주 전용)");
        }

        [Test]
        public void SpawnHook_TrySpawn_DirectCall_IsStateBacked()
        {
            // 훅 게이트(ownership/HasLordDaughter)는 호출부 소관 — TrySpawnForTerritory는
            // 상태(TerritoryState) 기반으로 스폰되며 네임플레이트에 결혼 상태가 반영된다.
            var room = new GameObject("TestRoom_Direct");
            _spawned.Add(room);

            _state.daughterMarried = true;
            var daughter = LordDaughterNPC.TrySpawnForTerritory(room, "East_01");
            Assert.IsNotNull(daughter);
            StringAssert.Contains("♥", daughter.GetComponent<NameplateDisplay>().DisplayName,
                "결혼 상태 재스폰 시에도 부부 네임플레이트 유지(저장 단일소스 계약)");
        }

        [Test]
        public void SpawnHook_CallSites_AliveLordGateAndDeterministicRoll()
        {
            // [126차 정정] 런타임 훅 게이트 소스 계약(전역 오염 회피 — 직접 실행 대신 소스 스캔):
            // ① InvadedLordEncounter.TrySpawnLordDaughterForTerritory — PlayerOwned 조기차단 + 결정론 롤 이중 게이트
            // ② IndoorSceneTransition — PlayerOwned 분기 훅 부재(딸은 생존 영주 경로 전용 — 오분기 재도입 방지)
            string encounter = System.IO.File.ReadAllText("Assets/Scripts/Systems/InvadedLordEncounter.cs");
            Assert.That(encounter, Does.Contain("state.ownership == TerritoryOwnership.PlayerOwned"),
                "딸 훅은 PlayerOwned(점령 완료)를 조기 차단해야 한다(생존 영주 전용)");
            Assert.That(encounter, Does.Contain("LordDaughterNPC.HasLordDaughter(territoryKey)"),
                "딸 훅은 결정론 롤 게이트 필수");

            string transition = System.IO.File.ReadAllText("Assets/Scripts/UI/IndoorSceneTransition.cs");
            Assert.That(transition, Does.Not.Contain("LordDaughterNPC"),
                "IndoorSceneTransition PlayerOwned 분기에는 딸 훅이 없다(점령 성 미등장 — 정정 계약)");
        }

        // ════════════════════════════════════════════════════════════════
        // 헬퍼
        // ════════════════════════════════════════════════════════════════

        /// <summary>East_01 딸을 단순 테스트 룸(기준형 KeepOctagon 폴백 존 판정)에 직접 스폰.</summary>
        private LordDaughterNPC SpawnDaughterInTestRoom()
        {
            var room = new GameObject("TestRoom_Daughter");
            _spawned.Add(room);
            var daughter = LordDaughterNPC.TrySpawnForTerritory(room, "East_01");
            Assert.IsNotNull(daughter, "테스트 룸 딸 스폰(기준형 존 판정 폴백 경로)");
            return daughter;
        }

        /// <summary>실존 영지 키 전수(4국가 × 1~15, 정의가 비어 있지 않은 키만).</summary>
        private static IEnumerable<string> RealTerritoryKeys()
        {
            var db = TerritoryDatabase.Instance;
            NationType[] nations = { NationType.East, NationType.West, NationType.South, NationType.North };
            foreach (var nation in nations)
            for (int index = 1; index <= 15; index++)
            {
                var key = new TerritoryId(nation, index).ToString();
                if (!string.IsNullOrEmpty(db.GetDefinition(key).territoryName))
                    yield return key;
            }
        }

        /// <summary>InteriorLayoutContext 구성 테스트 룸(LayoutAwareConsumerPlacementTests.MakeContextRoom 선례).</summary>
        private static GameObject MakeContextRoom(InteriorLayoutDefinition definition)
        {
            var room = new GameObject("TestRoom_" + definition.LayoutId);
            room.AddComponent<InteriorLayoutContext>().Configure(definition);
            return room;
        }
    }
}
