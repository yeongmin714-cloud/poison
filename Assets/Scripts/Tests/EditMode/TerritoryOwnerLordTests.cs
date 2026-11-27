// [123차] 영지 소유 영주 추적(Phase 1) EditMode 테스트 — QA 위임 역할(구현/QA 분리: docs/AGENT_DELEGATION_POLICY.md).
//
// 검증 범위 (계획서 docs/123_TERRITORY_LORD_TRACKING_TOOLTIP_COMBATLOG_SCRAP_PLAN.md P1-5):
//  ① 초기 상태 결정론 영주 매핑 — TerritoryDatabase.InitializeStates가 전 영지 ownerLordName
//     = def.lord.lordName으로 채우는지 (여러 영지 샘플)
//  ② SetOwnerLord 라운드트립 — 정상 갱신 + 멱등(동일 값 재설정 무시) + 가드(null/빈값/미등록 ID 무시)
//  ③ 세이브 라운드트립 — TerritorySaveData.ownerLordName 직렬화 복원 + 구세이브(필드 부재 = 빈 값)
//     원래 영주(def.lord.lordName) 폴백 소스 계약
//  ④ 점령 배선 소스-텍스트 계약 — AIWarSystem.CompleteWar / TerritoryWarManager.HandleVictory /
//     LordBetrayalSystem.ExecuteBetrayal에 SetOwnerLord 배선 존재 (WorldMapSceneWiringTests 선례)
//  ⑤ 뉴게임+ 리셋 복원 소스 계약 — NewGamePlusSystem 리셋이 전 영지 ownerLordName = 원래 영주
//
// EditMode 함정 대응 (FlagTakeoverTests 선례):
//  - TerritoryDatabase는 코드 생성 DB — Instance 항상 유효 (East_01/West_01 정의 존재)
//  - LogAssert.ignoreFailingMessages — SetOwnerLord 가드 경로의 무해 경고 로그 차단
//  - 결정론: 무작위 미사용 산식만 검증
//  - 세이브 복원은 소스-텍스트 계약으로 검증(ApplyTerritoryData가 private이며 Unity 라이프사이클 의존)

using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public sealed class TerritoryOwnerLordTests
    {
        private const string TerritoryDataPath = "Assets/Scripts/Core/Data/TerritoryData.cs";
        private const string TerritoryDatabasePath = "Assets/Scripts/Core/Data/TerritoryDatabase.cs";
        private const string AIWarSystemPath = "Assets/Scripts/Systems/AIWarSystem.cs";
        private const string TerritoryWarManagerPath = "Assets/Scripts/Systems/TerritoryWarManager.cs";
        private const string LordBetrayalSystemPath = "Assets/Scripts/Systems/LordBetrayalSystem.cs";
        private const string NewGamePlusSystemPath = "Assets/Scripts/Systems/NewGamePlusSystem.cs";
        private const string SaveDataPath = "Assets/Scripts/Systems/SaveData.cs";
        private const string SaveManagerPath = "Assets/Scripts/Systems/SaveManager.cs";
        private const string East01Key = "East_01"; // [123차] 표준 영지 키 상수화 — 파일 내 "East_01" 문자열 중복 제거

        // [123차] 테스트 간 싱글턴 격리 — TerritoryDatabase.Instance는 코드 생성 DB로 EditMode 세션
        // 전체에서 상태가 잔존하며, NUnit 기본 정렬(알파벳순)상 선행 테스트가 남긴 ownerLordName이
        // 후속 테스트의 사전조건(초기값 == def.lord.lordName)을 오염시킨다(123차 QA 실측 회귀 1건).
        // 113차 격리 계약 준용 — 스냅샷 → 테스트 → 원복.
        private TerritoryOwnership _savedOwnership; // [123차] East_01 ownership 진입 시점 스냅샷
        private string _savedOwnerLordName;         // [123차] East_01 ownerLordName 진입 시점 스냅샷

        [OneTimeSetUp]
        public void OneTimeInit()
        {
            // SetOwnerLord 가드 경로(무효 인자)에서 무해 경고 로그가 뜨지만
            // 편집 모드 노이즈이므로 실패로 취급하지 않는다.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [OneTimeTearDown]
        public void OneTimeCleanup()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [SetUp]
        public void PerTestInit() // [123차] 테스트 간 격리 — 선행 테스트의 싱글턴 오염 제거
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(new TerritoryId(NationType.East, 1));
            var state = db.GetState(East01Key);

            // [123차] 진입 시점 스냅샷 — TearDown에서 타 fixture 오염 방지용 원복에 사용
            _savedOwnership = state.ownership;
            _savedOwnerLordName = state.ownerLordName;

            // [123차] 결정론 재설정 — 선행 테스트가 바꾼 ownerLordName을 정의 영주로 되돌려
            // 각 테스트의 사전조건(초기값 == def.lord.lordName)을 보장한다
            db.SetOwnerLord(East01Key, def.lord.lordName);
        }

        [TearDown]
        public void PerTestCleanup() // [123차] 스냅샷 원복 — 진입 시점 값 복원(타 fixture 싱글턴 오염 방지)
        {
            var state = TerritoryDatabase.Instance.GetState(East01Key);
            if (state == null) return; // [123차] 이례 가드 — East_01은 코드 생성 DB에 상시 등록
            state.ownership = _savedOwnership;
            state.ownerLordName = _savedOwnerLordName;
        }

        // ================================================================
        // ① 초기 상태 결정론 영주 매핑 — 여러 영지 샘플 ownerLordName == def.lord.lordName
        // ================================================================
        [Test]
        public void InitializeStates_MapsOwnerLordToDefinitionLord_Deterministically()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null — 코드 생성 DB 초기화 실패");

            // 전 정의를 순회해 전 영지가 결정론 매핑을 따르는지 검증 (샘플 + 전수 스윕)
            int checkedCount = 0;
            foreach (var def in db.GetAllDefinitions())
            {
                var state = db.GetState(def.id);
                Assert.IsNotNull(state, $"상태 없음: {def.id}");
                Assert.AreEqual(def.lord.lordName, state.ownerLordName,
                    $"영지 {def.id} 소유 영주가 정의 영주와 불일치 — 결정론 매핑 위반");
                checkedCount++;
            }
            Assert.Greater(checkedCount, 0, "정의 0개 — TerritoryDatabase 초기화 실패");
        }

        [Test]
        public void InitializeStates_MapsKnownTerritoriesToKnownLords()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null");

            // 핵심 샘플 2건 — East_01/West_01 (FlagTakeoverTests에서도 사용하는 표준 키)
            var eastDef = db.GetDefinition(new TerritoryId(NationType.East, 1));
            var westDef = db.GetDefinition(new TerritoryId(NationType.West, 1));
            var eastState = db.GetState(new TerritoryId(NationType.East, 1));
            var westState = db.GetState(new TerritoryId(NationType.West, 1));

            Assert.AreEqual(eastDef.lord.lordName, eastState.ownerLordName, "East_01 소유 영주 불일치");
            Assert.AreEqual(westDef.lord.lordName, westState.ownerLordName, "West_01 소유 영주 불일치");
        }

        // ================================================================
        // ② SetOwnerLord 라운드트립 — 정상 갱신 + 멱등 + 가드
        // ================================================================
        [Test]
        public void SetOwnerLord_RoundTrip_UpdatesAndReflects()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null");

            var def = db.GetDefinition(new TerritoryId(NationType.East, 1));
            string key = new TerritoryId(NationType.East, 1).ToString();
            var state = db.GetState(key);
            string original = state.ownerLordName;
            Assert.AreEqual(def.lord.lordName, original, "사전 조건: 초기값이 def.lord.lordName이어야 함");

            const string newLord = "테스트 영주";
            db.SetOwnerLord(key, newLord);
            var after = db.GetState(key);
            Assert.AreEqual(newLord, after.ownerLordName, "SetOwnerLord 후 소유 영주 미반영");

            // 멱등 — 동일 값 재설정 시 무시되어도 결과는 동일해야 함
            db.SetOwnerLord(key, newLord);
            Assert.AreEqual(newLord, db.GetState(key).ownerLordName, "멱등 재설정 후 값 변질");

            // 복구 — 원래 영주로 되돌림(다른 테스트 오염 방지)
            db.SetOwnerLord(key, original);
            Assert.AreEqual(original, db.GetState(key).ownerLordName, "복구 후 원래 영주 미복원");
        }

        [Test]
        public void SetOwnerLord_GuardsRejectInvalidInputs()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null");

            var state = db.GetState(new TerritoryId(NationType.East, 1));
            string before = state.ownerLordName;

            // null/빈 lordName — 무시(값 불변)
            db.SetOwnerLord(East01Key, null);
            db.SetOwnerLord(East01Key, "");
            Assert.AreEqual(before, db.GetState(East01Key).ownerLordName, "null/빈 lordName 가드 미작동");

            // null/빈 territoryId — 무시(예외 없이 무시가 계약)
            Assert.DoesNotThrow(() => db.SetOwnerLord(null, "테스트 영주"));
            Assert.DoesNotThrow(() => db.SetOwnerLord("", "테스트 영주"));

            // 미등록 territoryId — 무시(예외 없이 무시가 계약)
            Assert.DoesNotThrow(() => db.SetOwnerLord("NonExistent_99", "테스트 영주"));

            Assert.AreEqual(before, db.GetState(East01Key).ownerLordName, "가드 통과 후 값 오염");
        }

        [Test]
        public void SetOwnerLord_DoesNotTouchOwnership()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null");

            var state = db.GetState(new TerritoryId(NationType.East, 1));
            TerritoryOwnership beforeOwnership = state.ownership;

            db.SetOwnerLord(East01Key, "소유권 무관 검증 영주");

            Assert.AreEqual(beforeOwnership, db.GetState(East01Key).ownership,
                "SetOwnerLord가 ownership을 건드림 — 소유 영주만 기록해야 함");
        }

        // ================================================================
        // ③ 세이브 라운드트립 — TerritorySaveData 직렬화 복원 + 구세이브 폴백 소스 계약
        // ================================================================
        [Test]
        public void TerritorySaveData_SerializationRoundTrip_PreservesOwnerLordName()
        {
            var sd = new TerritorySaveData
            {
                territoryId = East01Key,
                ownerNation = "LordOwned",
                isConquered = false,
                territoryGold = 100,
                lordSpared = false,
                tributeWeeksPaid = 2,
                betrayalCount = 1,
                loyaltyToPlayer = 30f,
                lastPreferredFeedDay = 5,
                ownerLordName = "세이브 복원 검증 영주"
            };
            string json = JsonUtility.ToJson(sd);
            var restored = JsonUtility.FromJson<TerritorySaveData>(json);

            Assert.AreEqual(East01Key, restored.territoryId);
            Assert.AreEqual("세이브 복원 검증 영주", restored.ownerLordName, "ownerLordName 직렬화 라운드트립 실패");
        }

        [Test]
        public void TerritorySaveData_OldSave_EmptyOwnerLordName_JsonUtilityFillsEmpty()
        {
            // 구세이브(v5) JSON에는 ownerLordName 필드가 없다 — JsonUtility가 빈 문자열로 채움
            string oldSaveJson = $"{{\"territoryId\":\"{East01Key}\",\"ownerNation\":\"LordOwned\"}}";
            var restored = JsonUtility.FromJson<TerritorySaveData>(oldSaveJson);
            Assert.IsTrue(string.IsNullOrEmpty(restored.ownerLordName),
                "구세이브 JSON에서 ownerLordName이 빈 값이 아님 — 폴백 전제 위반");
        }

        [Test]
        public void ApplyTerritoryData_ImplementsOwnerLordNameFallback()
        {
            // SaveManager.ApplyTerritoryData는 private이며 Unity 라이프사이클 의존이므로
            // 소스-텍스트 계약으로 폴백 로직 존재를 검증한다 (TerritoryTooltipContentTests 선례).
            string src = ReadSource(SaveManagerPath);
            StringAssert.Contains("string.IsNullOrEmpty(tData.ownerLordName)", src,
                "ApplyTerritoryData에 구세이브 폴백 가드(IsNullOrEmpty) 없음");
            StringAssert.Contains("def.lord.lordName", src,
                "ApplyTerritoryData에 원래 영주(def.lord.lordName) 폴백 없음");
            StringAssert.Contains("ownerLordName = state.ownerLordName", src,
                "CollectTerritoryData에 ownerLordName 저장 배선 없음");
        }

        [Test]
        public void TerritorySaveData_ContainsOwnerLordNameField()
        {
            // TerritorySaveData 필드 + v6 버전 애노테이션 규약
            string src = ReadSource(SaveDataPath);
            StringAssert.Contains("public string ownerLordName;", src,
                "TerritorySaveData에 ownerLordName 필드 없음");
            StringAssert.Contains("[123차] 소유 영주 이름 (v6+)", src,
                "TerritorySaveData.ownerLordName에 v6 버전 애노테이션 없음");
        }

        // ================================================================
        // ④ 점령 배선 소스-텍스트 계약 — 영주↔영주 점령 경로 SetOwnerLord 존재
        // ================================================================
        [Test]
        public void AIWarSystem_CompleteWar_WiresSetOwnerLord()
        {
            string src = ReadSource(AIWarSystemPath);
            StringAssert.Contains("db.SetOwnerLord(defKey, defAttacker.lord.lordName)", src,
                "AIWarSystem.CompleteWar에 정복 영지 SetOwnerLord 배선 없음 — 공격 영지의 원래 영주로 기록해야 함");
        }

        [Test]
        public void TerritoryWarManager_HandleVictory_WiresSetOwnerLord()
        {
            string src = ReadSource(TerritoryWarManagerPath);
            StringAssert.Contains("victoryDb.SetOwnerLord(territoryId, repDef.lord.lordName)", src,
                "TerritoryWarManager.HandleVictory에 정복 영지 SetOwnerLord 배선 없음");
            // 대표 영주 결정론 규칙 주석 문서화 계약 — 사전순 정렬 후 인덱스 0
            StringAssert.Contains("attackerNationTerritories.Sort();", src,
                "HandleVictory 대표 영주 결정론 정렬(Sort) 없음 — 사전 순회 순서 비결정론 위험");
            // 소유권 갱신(_currentNationOwners[territoryId] = attacker) 전에 산정하는 계약 — 오염 방지
            int sortIdx = src.IndexOf("attackerNationTerritories.Sort();");
            int ownershipIdx = src.IndexOf("_currentNationOwners[territoryId] = attacker;");
            Assert.Greater(sortIdx, 0, "Sort 배선 미발견");
            Assert.Greater(ownershipIdx, sortIdx,
                "대표 영주 산정이 소유권 갱신 이후에 있음 — 방금 정복한 영지가 목록에 섞여 방어측 영주 오염");
        }

        [Test]
        public void LordBetrayalSystem_ExecuteBetrayal_WiresSetOwnerLord()
        {
            string src = ReadSource(LordBetrayalSystemPath);
            StringAssert.Contains("db.SetOwnerLord(territoryId.ToString(), def.lord.lordName)", src,
                "LordBetrayalSystem.ExecuteBetrayal에 원래 영주 복귀 SetOwnerLord 배선 없음");
        }

        [Test]
        public void TerritoryState_ContainsOwnerLordNameField()
        {
            string src = ReadSource(TerritoryDataPath);
            StringAssert.Contains("public string ownerLordName", src,
                "TerritoryState에 ownerLordName 프로퍼티 없음");
            StringAssert.Contains("[SerializeField] private string _ownerLordName", src,
                "TerritoryState에 _ownerLordName 백킹 필드 없음");
        }

        [Test]
        public void TerritoryDatabase_ContainsSetOwnerLordApi()
        {
            string src = ReadSource(TerritoryDatabasePath);
            StringAssert.Contains("public void SetOwnerLord(string territoryId, string lordName)", src,
                "TerritoryDatabase.SetOwnerLord 공개 API 없음");
            // InitializeStates 결정론 매핑 계약
            StringAssert.Contains("state.ownerLordName = kvp.Value.lord.lordName;", src,
                "TerritoryDatabase.InitializeStates에 결정론 영주 매핑 없음");
        }

        // ================================================================
        // ⑤ 뉴게임+ 리셋 복원 소스 계약 — 전 영지 ownerLordName = 원래 영주
        // ================================================================
        [Test]
        public void NewGamePlusSystem_ResetRestoresOwnerLordNameToOriginal()
        {
            string src = ReadSource(NewGamePlusSystemPath);
            StringAssert.Contains("state.ownerLordName = def.lord.lordName;", src,
                "NewGamePlusSystem 리셋에 전 영지 원래 영주 복원 배선 없음");
            // 소유권 분기(if state.ownership == ...) 밖에서 실행되는 계약 — AI 점령 LordOwned까지 복원
            int resetIdx = src.IndexOf("state.ownerLordName = def.lord.lordName;");
            int branchIdx = src.IndexOf("state.ownership == TerritoryOwnership.PlayerOwned");
            Assert.Greater(resetIdx, 0, "리셋 배선 미발견");
            Assert.Greater(branchIdx, resetIdx,
                "ownerLordName 리셋이 PlayerOwned 분기 내부에 있음 — AI 점령 LordOwned 영지 누락");
        }

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }
    }
}
