using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// SaveData v4 영속화 집중 테스트.
    /// - JsonUtility 왕복: v4 블록 + 영지 금고 보존.
    /// - v3 레거시 JSON: 신규 블록 null, 로드 시 현재 날짜 기준 시딩 → 소급 청구/과세 없음.
    /// - 용병 로스터/호감도 왕복, 알 수 없는 id 건너뜀, 골드 미청구(Import는 SpendGold를 호출하지 않음).
    /// - 병사 입단 카운트/계약 청구일 왕복.
    /// - 고용 시장(LaborMarketSystem) AI 카운트: JSON 왕복, Export/Import 정규화, null 리셋.
    /// </summary>
    public class SaveV4PersistenceTests
    {
        private static readonly TerritoryId TestId = new TerritoryId(NationType.North, 1);

        private GameObject _mercMgrGo;
        private MercenaryManager _mercMgr;

        [SetUp]
        public void SetUp()
        {
            SoldierWageSystem.ResetAll();
            AITerritoryEconomySystem.ResetAll();
            LaborMarketSystem.ResetAll();

            SetStaticInstance<MercenaryManager>(null);
            _mercMgrGo = new GameObject("PersistTest_MercManager");
            _mercMgr = _mercMgrGo.AddComponent<MercenaryManager>();
            _mercMgr.EnsureDatabase();
            SetStaticInstance(_mercMgr);
        }

        [TearDown]
        public void TearDown()
        {
            if (_mercMgrGo != null) Object.DestroyImmediate(_mercMgrGo);
            SetStaticInstance<MercenaryManager>(null);
            _mercMgr = null;
            _mercMgrGo = null;
            SoldierWageSystem.ResetAll();
            AITerritoryEconomySystem.ResetAll();
            LaborMarketSystem.ResetAll();
        }

        // ===== 고용 시장 AI 카운트 =====

        [Test]
        public void LaborMarketSaveData_JsonRoundTrip_PreservesCountsAndTotals()
        {
            var dto = new LaborMarketSaveData
            {
                lastProcessedDay = 40,
                totalHiredByAI = 5,
                hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                {
                    new TerritoryHireCountEntry { territoryId = "East_01", count = 3 },
                    new TerritoryHireCountEntry { territoryId = "North_12", count = 2 },
                },
            };

            string json = JsonUtility.ToJson(dto);
            LaborMarketSaveData back = JsonUtility.FromJson<LaborMarketSaveData>(json);

            Assert.AreEqual(40, back.lastProcessedDay);
            Assert.AreEqual(5, back.totalHiredByAI);
            Assert.AreEqual(2, back.hiredCounts.Count);
            Assert.AreEqual("East_01", back.hiredCounts[0].territoryId);
            Assert.AreEqual(3, back.hiredCounts[0].count);
            Assert.AreEqual("North_12", back.hiredCounts[1].territoryId);
            Assert.AreEqual(2, back.hiredCounts[1].count);
        }

        [Test]
        public void SaveData_LaborMarketBlock_SurvivesFullJsonRoundTrip()
        {
            var save = new SaveData
            {
                saveVersion = 5,
                time = new TimeSaveData { day = 12, gameTime = 0f },
                laborMarket = new LaborMarketSaveData
                {
                    lastProcessedDay = 11,
                    totalHiredByAI = 1,
                    hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                    {
                        new TerritoryHireCountEntry { territoryId = "West_04", count = 1 },
                    },
                },
            };

            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save, prettyPrint: true));

            Assert.IsNotNull(back.laborMarket);
            Assert.AreEqual(11, back.laborMarket.lastProcessedDay);
            Assert.AreEqual(1, back.laborMarket.totalHiredByAI);
            Assert.AreEqual("West_04", back.laborMarket.hiredCounts[0].territoryId);
            Assert.AreEqual(1, back.laborMarket.hiredCounts[0].count);

            // 로드 경로와 동일: 역직렬화된 블록을 시스템에 적용한 뒤 다시 내보내도 동일해야 한다
            LaborMarketSystem.ImportSaveData(back.laborMarket);
            LaborMarketSaveData reExported = LaborMarketSystem.ExportSaveData();
            Assert.AreEqual(11, reExported.lastProcessedDay);
            Assert.AreEqual(1, reExported.totalHiredByAI);
            Assert.AreEqual(1, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.West, 4)));
        }

        [Test]
        public void LaborMarket_ImportExport_NormalizesAndSortsEntries()
        {
            var dto = new LaborMarketSaveData
            {
                lastProcessedDay = -5, // -1로 클램프되어야 함
                totalHiredByAI = 7,
                hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                {
                    new TerritoryHireCountEntry { territoryId = "North_03", count = 1 },
                    new TerritoryHireCountEntry { territoryId = "East_01", count = 2 },
                    new TerritoryHireCountEntry { territoryId = "East_1", count = 4 },    // 비정규 표기 — 거부
                    new TerritoryHireCountEntry { territoryId = "bogus", count = 5 },     // 파싱 불가 — 거부
                    new TerritoryHireCountEntry { territoryId = "South_02", count = 0 },  // 0 — 제외
                    null,
                },
            };

            LaborMarketSystem.ImportSaveData(dto);
            LaborMarketSaveData exported = LaborMarketSystem.ExportSaveData();

            Assert.AreEqual(-1, exported.lastProcessedDay, "음수 처리일은 -1로 클램프");
            Assert.AreEqual(7, exported.totalHiredByAI);
            Assert.AreEqual(2, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.East, 1)));
            Assert.AreEqual(1, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.North, 3)));
            Assert.AreEqual(0, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.South, 2)));

            // 내보내기: 영지 ID 문자열 오름차순, 0 이하 카운트 제외
            Assert.AreEqual(2, exported.hiredCounts.Count);
            Assert.AreEqual("East_01", exported.hiredCounts[0].territoryId);
            Assert.AreEqual(2, exported.hiredCounts[0].count);
            Assert.AreEqual("North_03", exported.hiredCounts[1].territoryId);
            Assert.AreEqual(1, exported.hiredCounts[1].count);
        }

        [Test]
        public void LaborMarket_ExportJsonImport_RestoresCountsThroughSaveData()
        {
            LaborMarketSystem.ImportSaveData(new LaborMarketSaveData
            {
                lastProcessedDay = 18,
                totalHiredByAI = 4,
                hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                {
                    new TerritoryHireCountEntry { territoryId = "South_05", count = 1 },
                    new TerritoryHireCountEntry { territoryId = "East_01", count = 3 },
                },
            });

            // 저장 경로와 동일: SaveData에 블록 삽입 → JSON → 역직렬화
            var save = new SaveData
            {
                saveVersion = 5,
                time = new TimeSaveData { day = 18, gameTime = 0f },
                laborMarket = LaborMarketSystem.ExportSaveData(),
            };
            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save, prettyPrint: true));

            // 로드 경로: 초기화 후 복원
            LaborMarketSystem.ResetAll();
            LaborMarketSystem.ImportSaveData(back.laborMarket);

            Assert.AreEqual(18, LaborMarketSystem.ExportSaveData().lastProcessedDay);
            Assert.AreEqual(4, LaborMarketSystem.TotalHiredByAI());
            Assert.AreEqual(3, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.East, 1)));
            Assert.AreEqual(1, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.South, 5)));
            Assert.AreEqual(0, LaborMarketSystem.MarketPoolSize(), "방출 풀은 저장/복원 대상이 아니다");
        }

        [Test]
        public void LaborMarket_ImportNull_ResetsPriorState()
        {
            LaborMarketSystem.ImportSaveData(new LaborMarketSaveData
            {
                lastProcessedDay = 30,
                totalHiredByAI = 9,
                hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                {
                    new TerritoryHireCountEntry { territoryId = "East_01", count = 9 },
                },
            });

            // 레거시 세이브(laborMarket == null): 이전 인메모리 상태가 남지 않아야 한다
            LaborMarketSystem.ImportSaveData(null);
            LaborMarketSaveData exported = LaborMarketSystem.ExportSaveData();

            Assert.AreEqual(-1, exported.lastProcessedDay);
            Assert.AreEqual(0, exported.totalHiredByAI);
            Assert.AreEqual(0, exported.hiredCounts.Count);
            Assert.AreEqual(0, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.East, 1)));
        }

        // ===== v5: AI 고용 병사 로스터 (hiredGuards) =====

        [Test]
        public void AIHiredGuardEntry_JsonRoundTrip_InsideLaborMarketBlock_PreservesAllFields()
        {
            var save = new SaveData
            {
                saveVersion = 5,
                time = new TimeSaveData { day = 20, gameTime = 0f },
                laborMarket = new LaborMarketSaveData
                {
                    lastProcessedDay = 19,
                    totalHiredByAI = 1,
                    hiredCounts = new System.Collections.Generic.List<TerritoryHireCountEntry>
                    {
                        new TerritoryHireCountEntry { territoryId = "East_01", count = 1 },
                    },
                    hiredGuards = new System.Collections.Generic.List<AIHiredGuardSaveEntry>
                    {
                        new AIHiredGuardSaveEntry
                        {
                            stableId = "G000042",
                            territoryId = "East_01",
                            guardName = "테스트병",
                            level = 7,
                            nation = "동",
                            hp = 31.5f,
                            maxHP = 120f,
                            loyalty = 64.25f,
                            atk = 11,
                            def = 9,
                            vit = 13,
                            agi = 6,
                            isAlive = true,
                            gearDifficulty = 2,
                        },
                    },
                },
            };

            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save, prettyPrint: true));

            Assert.AreEqual(5, back.saveVersion);
            Assert.IsNotNull(back.laborMarket.hiredGuards);
            Assert.AreEqual(1, back.laborMarket.hiredGuards.Count);
            AIHiredGuardSaveEntry g = back.laborMarket.hiredGuards[0];
            Assert.AreEqual("G000042", g.stableId);
            Assert.AreEqual("East_01", g.territoryId);
            Assert.AreEqual("테스트병", g.guardName);
            Assert.AreEqual(7, g.level);
            Assert.AreEqual("동", g.nation);
            Assert.AreEqual(31.5f, g.hp, 0.0001f);
            Assert.AreEqual(120f, g.maxHP, 0.0001f);
            Assert.AreEqual(64.25f, g.loyalty, 0.0001f);
            Assert.AreEqual(11, g.atk);
            Assert.AreEqual(9, g.def);
            Assert.AreEqual(13, g.vit);
            Assert.AreEqual(6, g.agi);
            Assert.IsTrue(g.isAlive);
            Assert.AreEqual(2, g.gearDifficulty);
        }

        [Test]
        public void V4Json_WithoutHiredGuards_MigratesToEmptyRosterDefault()
        {
            // v4 세이브: laborMarket 블록은 있으나 hiredGuards 키 없음 → v5 기본값(빈 리스트)로 로드되어야 한다
            const string v4 = "{\"saveVersion\":4,\"time\":{\"day\":21,\"gameTime\":0},\"territories\":[],"
                + "\"laborMarket\":{\"lastProcessedDay\":20,\"totalHiredByAI\":2,"
                + "\"hiredCounts\":[{\"territoryId\":\"West_04\",\"count\":2}]}}";

            SaveData data = JsonUtility.FromJson<SaveData>(v4);

            Assert.AreEqual(4, data.saveVersion, "원본 버전 값은 그대로 보존된다 (SaveManager가 >=4로 금고 복원 판단)");
            Assert.IsNotNull(data.laborMarket);
            Assert.IsNotNull(data.laborMarket.hiredGuards, "v4 → v5 기본값은 null이 아닌 빈 리스트여야 한다");
            Assert.AreEqual(0, data.laborMarket.hiredGuards.Count);
            Assert.AreEqual(20, data.laborMarket.lastProcessedDay);
            Assert.AreEqual(2, data.laborMarket.totalHiredByAI);

            // 로드 경로: 빈 로스터 → 마커 병사 없음, 카운트는 기록용으로 복원
            LaborMarketSystem.ImportSaveData(data.laborMarket);
            LaborMarketSaveData exported = LaborMarketSystem.ExportSaveData();
            Assert.AreEqual(0, exported.hiredGuards.Count);
            Assert.AreEqual(2, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.West, 4)));
        }

        [Test]
        public void LaborMarket_ImportWithNullHiredGuards_IsSafeAndTreatedAsEmptyRoster()
        {
            // 방어적 경로: hiredGuards가 null로 주입되어도 예외 없이 빈 로스터로 처리
            var dto = new LaborMarketSaveData { lastProcessedDay = 7, totalHiredByAI = 3, hiredGuards = null };

            Assert.DoesNotThrow(() => LaborMarketSystem.ImportSaveData(dto));
            Assert.AreEqual(7, LaborMarketSystem.ExportSaveData().lastProcessedDay);
            Assert.AreEqual(3, LaborMarketSystem.TotalHiredByAI());
        }

        // ===== 용병 고용 상한 제거 (고유 카탈로그 ID 기준) =====

        [Test]
        public void MercenaryManager_AvailableMercenaryCount_EqualsDistinctCatalogEntries()
        {
            int distinct = new System.Collections.Generic.HashSet<string>(
                System.Linq.Enumerable.Select(_mercMgr.GetAllMercenaryData(), d => d.id)).Count;

            Assert.AreEqual(distinct, _mercMgr.AvailableMercenaryCount);
            Assert.AreEqual(_mercMgr.GetAllMercenaryData().Length, _mercMgr.AvailableMercenaryCount,
                "카탈로그 ID는 중복 없이 등록되어야 한다");
        }

        [Test]
        public void MercenaryRoster_NoFixedCeiling_RestoresEveryDistinctCatalogEntry()
        {
            // 고정 상한(구 _maxMercenaries=10) 제거 확인: 고유 카탈로그 전원을 상한 없이 복원할 수 있어야 한다.
            MercenaryData[] all = _mercMgr.GetAllMercenaryData();
            var dto = new MercenarySaveData
            {
                hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>(),
            };
            foreach (MercenaryData d in all)
            {
                dto.hired.Add(new MercenaryInstanceSaveData { mercenaryId = d.id, currentHP = d.maxHP, isAlive = true, affinity = 50f });
            }
            // 중복 id는 여전히 한 번만 적용
            dto.hired.Add(new MercenaryInstanceSaveData { mercenaryId = all[0].id, currentHP = 1f, isAlive = true });

            _mercMgr.ImportSaveData(dto);

            Assert.AreEqual(all.Length, _mercMgr.HiredCount);
            Assert.AreEqual(_mercMgr.AvailableMercenaryCount, _mercMgr.HiredCount);
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary(all[0].id, out var first));
            Assert.AreEqual(all[0].maxHP, first.currentHP, 0.0001f, "중복 id 추가 항목은 무시");
        }

        // ===== JSON 왕복 =====

        [Test]
        public void V4Json_RoundTrip_PreservesTerritoryGoldAndBlocks()
        {
            var save = new SaveData
            {
                saveVersion = 5,
                time = new TimeSaveData { day = 15, gameTime = 0f },
                territories = new System.Collections.Generic.List<TerritorySaveData>
                {
                    new TerritorySaveData { territoryId = TestId.ToString(), ownerNation = "LordOwned", territoryGold = 1234 }
                },
                soldierWage = new SoldierWageSaveData
                {
                    admissionCounts = new System.Collections.Generic.List<AdmissionCountSaveEntry>
                    {
                        new AdmissionCountSaveEntry { stableId = "G000007", count = 2 }
                    },
                    nextContractDays = new System.Collections.Generic.List<ContractDaySaveEntry>
                    {
                        new ContractDaySaveEntry { mercenaryId = "merc_soldier_01", nextContractDay = 22 }
                    }
                },
                aiEconomy = new AIEconomySaveData { lastTaxWeek = 2, lastMaintenanceDay = 15 },
                mercenaries = new MercenarySaveData
                {
                    hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>
                    {
                        new MercenaryInstanceSaveData { mercenaryId = "merc_high_01", currentHP = 77.5f, isAlive = true, affinity = 61f }
                    },
                    affinities = new System.Collections.Generic.List<AffinitySaveEntry>
                    {
                        new AffinitySaveEntry { mercenaryId = "merc_high_01", value = 61f }
                    }
                },
            };

            string json = JsonUtility.ToJson(save);
            SaveData back = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(5, back.saveVersion);
            Assert.AreEqual(1234, back.territories[0].territoryGold);
            Assert.AreEqual("G000007", back.soldierWage.admissionCounts[0].stableId);
            Assert.AreEqual(2, back.soldierWage.admissionCounts[0].count);
            Assert.AreEqual(22, back.soldierWage.nextContractDays[0].nextContractDay);
            Assert.AreEqual(2, back.aiEconomy.lastTaxWeek);
            Assert.AreEqual(15, back.aiEconomy.lastMaintenanceDay);
            Assert.AreEqual("merc_high_01", back.mercenaries.hired[0].mercenaryId);
            Assert.AreEqual(77.5f, back.mercenaries.hired[0].currentHP, 0.0001f);
            Assert.AreEqual(61f, back.mercenaries.hired[0].affinity, 0.0001f);
        }

        [Test]
        public void LegacyV3Json_DeserializesWithNullV4Blocks()
        {
            // v3 세이브: territoryGold/신규 블록 필드가 없는 JSON
            const string legacy = "{\"saveVersion\":3,\"time\":{\"day\":15,\"gameTime\":0},"
                + "\"territories\":[{\"territoryId\":\"North_01\",\"ownerNation\":\"LordOwned\",\"isConquered\":false}]}";

            SaveData data = JsonUtility.FromJson<SaveData>(legacy);

            // JsonUtility는 키가 없는 참조 필드를 생성자 기본값(SaveData는 초기화식 없음 → null)으로 둔다.
            // 다만 DTO 자체가 남아 있을 수 있으므로(필드 초기화식 보유 클래스) "null 또는 기본값(비어 있음)"을 검증한다.
            Assert.AreEqual(3, data.saveVersion);
            Assert.AreEqual(0, data.territories[0].territoryGold, "v3 영지 금고 필드는 0 (복원 시 무시)");

            Assert.IsTrue(IsSoldierWageAbsent(data.soldierWage), "입단 원장/계약일이 복원되면 안 된다");
            Assert.IsTrue(IsAIEconomyAbsent(data.aiEconomy), "AI 세수 멱등 마커가 복원되면 안 된다");
            Assert.IsTrue(IsMercenaryRosterAbsent(data.mercenaries), "용병 로스터가 복원되면 안 된다");
            Assert.IsTrue(IsLaborMarketAbsent(data.laborMarket), "고용 시장 카운트가 복원되면 안 된다");
        }

        [Test]
        public void LegacyV3Json_AbsentBlocks_SeedMarkersViaMigrationPath_NoReplay()
        {
            // 레거시 로드 경로 (SaveManager와 동일한 의미):
            //  - AI 세수: 블록이 없으면 null을 넘겨 현재 날짜 기준 마커를 시딩한다.
            //    (data != null 이면 -1 마커가 그대로 복원되므로, 기본값 DTO는 null로 정규화해야 한다.)
            //  - 병사 입단: 블록이 없으면 null (원장 비움 + 계약일 시딩).
            //  - 고용 시장: 블록이 없으면 lastProcessedDay=loadDay 기본 블록을 넘긴다.
            //    null을 직접 넘기면 lastProcessedDay가 -1로 리셋되어 같은 날 고용 틱이 재실행된다.
            SaveData data = JsonUtility.FromJson<SaveData>(
                "{\"saveVersion\":3,\"time\":{\"day\":9,\"gameTime\":0},\"territories\":[]}");

            const int loadDay = 9;

            AIEconomySaveData aiDto = IsAIEconomyAbsent(data.aiEconomy) ? null : data.aiEconomy;
            SoldierWageSaveData wageDto = IsSoldierWageAbsent(data.soldierWage) ? null : data.soldierWage;
            LaborMarketSaveData laborDto = IsLaborMarketAbsent(data.laborMarket)
                ? new LaborMarketSaveData { lastProcessedDay = loadDay }
                : data.laborMarket;

            AITerritoryEconomySystem.ImportSaveData(aiDto, loadDay);
            SoldierWageSystem.ImportSaveData(wageDto, loadDay);
            LaborMarketSystem.ImportSaveData(laborDto);

            AIEconomySaveData ai = AITerritoryEconomySystem.ExportSaveData();
            Assert.AreEqual(AITerritoryEconomySystem.GetTaxWeekIndex(loadDay), ai.lastTaxWeek,
                "레거시 로드일 주차가 과세 완료로 시딩되어 같은 주 소급 과세가 없어야 한다");
            Assert.AreEqual(loadDay, ai.lastMaintenanceDay,
                "레거시 로드일이 유지비 처리일로 시딩되어 같은 날 소급 유지비가 없어야 한다");

            Assert.AreEqual(0, SoldierWageSystem.ExportSaveData().admissionCounts.Count,
                "레거시 입단 원장은 비어 있어야 한다");
            Assert.AreEqual(0, LaborMarketSystem.TotalHiredByAI(),
                "레거시 고용 시장 누적 카운트는 0이어야 한다");
            Assert.AreEqual(loadDay, LaborMarketSystem.ExportSaveData().lastProcessedDay,
                "레거시 고용 시장 처리일은 로드일로 시딩되어 같은 날 고용 틱이 재실행되면 안 된다");
        }

        private static bool IsSoldierWageAbsent(SoldierWageSaveData dto)
        {
            return dto == null
                || ((dto.admissionCounts == null || dto.admissionCounts.Count == 0)
                    && (dto.nextContractDays == null || dto.nextContractDays.Count == 0));
        }

        private static bool IsAIEconomyAbsent(AIEconomySaveData dto)
        {
            // 기본값은 -1 (없음). 0으로 두면 "0주차 과세 완료"로 잘못 해석되므로 주의.
            return dto == null || (dto.lastTaxWeek == -1 && dto.lastMaintenanceDay == -1);
        }

        private static bool IsMercenaryRosterAbsent(MercenarySaveData dto)
        {
            return dto == null
                || ((dto.hired == null || dto.hired.Count == 0)
                    && (dto.affinities == null || dto.affinities.Count == 0));
        }

        private static bool IsLaborMarketAbsent(LaborMarketSaveData dto)
        {
            // lastProcessedDay 기본값 -1 (없음).
            return dto == null
                || (dto.lastProcessedDay == -1
                    && dto.totalHiredByAI == 0
                    && (dto.hiredCounts == null || dto.hiredCounts.Count == 0));
        }

        // ===== 병사 입단/계약 =====

        [Test]
        public void SoldierWage_RoundTrip_RestoresAdmissionAndContractDays()
        {
            var dto = new SoldierWageSaveData
            {
                admissionCounts = new System.Collections.Generic.List<AdmissionCountSaveEntry>
                {
                    new AdmissionCountSaveEntry { stableId = "G000003", count = 1 },
                    new AdmissionCountSaveEntry { stableId = "G000001", count = 3 },
                },
                nextContractDays = new System.Collections.Generic.List<ContractDaySaveEntry>
                {
                    new ContractDaySaveEntry { mercenaryId = "merc_elite_01", nextContractDay = 30 },
                },
            };

            SoldierWageSystem.ImportSaveData(dto, 25);
            SoldierWageSaveData exported = SoldierWageSystem.ExportSaveData();

            Assert.AreEqual(3, SoldierWageSystem.GetAdmissionCount("G000001"));
            Assert.AreEqual(1, SoldierWageSystem.GetAdmissionCount("G000003"));
            Assert.AreEqual(30, SoldierWageSystem.GetNextContractDay("merc_elite_01"));

            // 정렬된 순서로 내보내므로 결정론적
            Assert.AreEqual("G000001", exported.admissionCounts[0].stableId);
            Assert.AreEqual(3, exported.admissionCounts[0].count);
            Assert.AreEqual("G000003", exported.admissionCounts[1].stableId);
            Assert.AreEqual(30, exported.nextContractDays[0].nextContractDay);
        }

        [Test]
        public void SoldierWage_LegacyImport_SeedsHiredMercenaryToNextWeek_NoRetroactiveCharge()
        {
            // 레거시 세이브 적용: 현재 고용 용병(로스터 복원 후)은 오늘 + 7일부터 청구
            _mercMgr.ImportSaveData(new MercenarySaveData
            {
                hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>
                {
                    new MercenaryInstanceSaveData { mercenaryId = "merc_soldier_01", currentHP = 80f, isAlive = true, affinity = 50f }
                }
            });

            SoldierWageSystem.ImportSaveData(null, 12);

            Assert.AreEqual(12 + SoldierWageSystem.CONTRACT_INTERVAL_DAYS,
                SoldierWageSystem.GetNextContractDay("merc_soldier_01"));
            Assert.AreEqual(0, SoldierWageSystem.GetAdmissionCount("G-never-admitted"),
                "레거시 입단 원장은 비어 있어야 한다 (기존 편입 병사는 IsRecruited 가드로 중복 청구 없음)");
        }

        // ===== AI 영지 세수 멱등 상태 =====

        [Test]
        public void AIEconomy_RoundTrip_RestoresIdempotencyMarkers()
        {
            AITerritoryEconomySystem.ImportSaveData(new AIEconomySaveData { lastTaxWeek = 3, lastMaintenanceDay = 22 }, 22);
            AIEconomySaveData exported = AITerritoryEconomySystem.ExportSaveData();

            Assert.AreEqual(3, exported.lastTaxWeek);
            Assert.AreEqual(22, exported.lastMaintenanceDay);
        }

        [Test]
        public void AIEconomy_LegacyImport_BlocksSameWeekRetroactiveTax()
        {
            // 레거시 로드 at day 8 (경계일, 주차 1): 주차 1은 이미 과세된 것으로 시딩되어야 한다.
            AITerritoryEconomySystem.ImportSaveData(null, 8);
            AIEconomySaveData exported = AITerritoryEconomySystem.ExportSaveData();

            Assert.AreEqual(AITerritoryEconomySystem.GetTaxWeekIndex(8), exported.lastTaxWeek);
            Assert.AreEqual(8, exported.lastMaintenanceDay);

            // 같은 날 재호출 시 과세 없음 (금고 불변)
            TerritoryDatabase db = TerritoryDatabase.Instance;
            TerritoryState state = db.GetState(TestId);
            TerritoryOwnership previousOwnership = state.ownership;
            int previousGold = state.territoryGold;
            try
            {
                db.SetOwnership(TestId, TerritoryOwnership.LordOwned);
                state.territoryGold = 0;
                AITerritoryEconomySystem.ApplyDailyTax(8);
                Assert.AreEqual(0, state.territoryGold, "레거시 로드 직후 같은 주차 과세는 발생하면 안 된다");
            }
            finally
            {
                state.ownership = previousOwnership;
                state.territoryGold = previousGold;
            }
        }

        // ===== 용병 로스터 =====

        [Test]
        public void MercenaryRoster_RoundTrip_PreservesHPAliveAffinity()
        {
            var dto = new MercenarySaveData
            {
                hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>
                {
                    new MercenaryInstanceSaveData { mercenaryId = "merc_soldier_01", currentHP = 42.5f, isAlive = true, affinity = 70f },
                    new MercenaryInstanceSaveData { mercenaryId = "merc_legend_01", currentHP = 0f, isAlive = false, affinity = 12f },
                },
                affinities = new System.Collections.Generic.List<AffinitySaveEntry>
                {
                    new AffinitySaveEntry { mercenaryId = "merc_soldier_01", value = 70f },
                    new AffinitySaveEntry { mercenaryId = "merc_legend_01", value = 12f },
                    new AffinitySaveEntry { mercenaryId = "merc_elite_02", value = 88f }, // 고용 중 아님
                }
            };

            _mercMgr.ImportSaveData(dto);
            MercenarySaveData exported = _mercMgr.ExportSaveData();

            Assert.AreEqual(2, _mercMgr.HiredCount);
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary("merc_soldier_01", out var soldier));
            Assert.AreEqual(42.5f, soldier.currentHP, 0.0001f);
            Assert.IsTrue(soldier.isAlive);
            Assert.AreEqual(70f, soldier.affinity, 0.0001f);
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary("merc_legend_01", out var dead));
            Assert.IsFalse(dead.isAlive);

            Assert.AreEqual(88f, _mercMgr.GetAffinity("merc_elite_02"), 0.0001f, "고용 중이 아닌 용병 호감도도 보존");
            Assert.AreEqual(2, exported.hired.Count);
            Assert.AreEqual(3, exported.affinities.Count);
        }

        [Test]
        public void MercenaryRoster_UnknownAndDuplicateIds_AreSkipped()
        {
            var dto = new MercenarySaveData
            {
                hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>
                {
                    new MercenaryInstanceSaveData { mercenaryId = "merc_does_not_exist", currentHP = 1f, isAlive = true },
                    new MercenaryInstanceSaveData { mercenaryId = "merc_soldier_02", currentHP = 10f, isAlive = true },
                    new MercenaryInstanceSaveData { mercenaryId = "merc_soldier_02", currentHP = 99f, isAlive = true },
                }
            };

            _mercMgr.ImportSaveData(dto);

            Assert.AreEqual(1, _mercMgr.HiredCount);
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary("merc_soldier_02", out var m));
            Assert.AreEqual(10f, m.currentHP, 0.0001f, "중복 id는 첫 항목만 적용");
        }

        [Test]
        public void MercenaryRoster_Import_DoesNotChargeGold()
        {
            // Import 경로는 SoldierWageSystem.TryChargeMercenaryHire를 호출하지 않는다.
            // PlayerStats가 없어도 실패하지 않고, 있어도 골드 원장에 지출이 생기지 않아야 한다.
            var dto = new MercenarySaveData
            {
                hired = new System.Collections.Generic.List<MercenaryInstanceSaveData>
                {
                    new MercenaryInstanceSaveData { mercenaryId = "merc_high_01", currentHP = 150f, isAlive = true, affinity = 50f }
                }
            };

            _mercMgr.ImportSaveData(dto);

            Assert.AreEqual(1, _mercMgr.HiredCount);
            Assert.AreEqual(-1, SoldierWageSystem.GetNextContractDay("merc_high_01"),
                "Merc import 단독으로는 계약 청구일이 생기지 않는다 (SoldierWage Import가 담당)");
        }

        // ===== 리플렉션 헬퍼 (기존 SoldierWageSystemTests와 동일 패턴) =====

        private static void SetStaticInstance<T>(T value) where T : class
        {
            var field = typeof(T).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, typeof(T).Name + ".Instance 백킹 필드");
            field.SetValue(null, value);
        }
    }
}
