using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [113차 C] 영주 선호 음식 미지급 페널티 계약:
    /// ①문이 열린 LordOwned 영지만 대상(줄 수 없는 영지 페널티 금지) ②미지급 이력 없음(-1)은 첫 관측일
    /// 앵커 초기화(감소 없음) ③앵커 이후 GRACE(7일) 초과 시 일일 -2, 앵커 미리셋(지속 부담)
    /// ④TerritorySaveData.lastPreferredFeedDay 세이브 왕복 보존 + 구세이브 누락 시 -1 유지.
    /// 문 개방은 TerritoryState.drugContamination ≥ 50(TerritoryDrugSystem.GetLordAddiction = 오염도 실측).
    /// </summary>
    [TestFixture]
    public class LordFeedPenaltyTests
    {
        private TimeManager _tm;
        private TerritoryState _state;          // East_01 — 테스트 전용 영지(전역 싱글턴, TearDown 복원)
        private float _savedLoyalty;
        private TerritoryOwnership _savedOwnership;
        private int _savedDay;                  // [113차] 전역 TimeManager 격리 — 리플렉션 직접 복원(QA 2차 실측:
        private float _savedGameTime;           //   GameTime 세터는 FloorToInt(value/86400)를 dayDelta로 누산하므로
                                                //   정규화 값 재대입은 dayDelta 0 = no-op. SaveManager.SetPrivateField
                                                //   ("_currentDay") 선례와 동일한 필드 직접 복원으로 격리 계약을 충족한다.)
        private bool _tmExisted;                // [113차] 진입 시 TimeManager 존재 여부 — 내가 생성한 경우에만 소각
                                                //   (QA 3차 실측: GetOrCreate가 만든 관리자가 존속하면 "TimeManager 부재 시
                                                //   CurrentDay()=1" 전제를 가진 타 fixture(MercenaryContractDueTests) 기대가 무너진다)

        [SetUp]
        public void SetUp()
        {
            _tm = TimeManager.Instance;
            _tmExisted = _tm != null;
            if (_tm == null) _tm = TimeManager.GetOrCreate();
            (_savedDay, _savedGameTime) = SnapshotTime(_tm);   // 원본 스냅샷 — TearDown에서 필드 직접 복원
            _tm.GameTime += 10f * 86400f;       // 델타 경과(EnvoyIntelTests 패턴) — CurrentDay ≥ 10 보장

            _state = TerritoryDatabase.Instance.GetState("East_01");
            Assume.That(_state, Is.Not.Null, "East_01 상태 존재");

            // 원상 복구용 스냅샷 — 전역 싱글턴 오염 방지
            _savedLoyalty = _state.loyaltyToPlayer;
            _savedOwnership = _state.ownership;

            // 페널티 대상 조건 — LordOwned + 문 개방(오염도 60 ≥ 임계 50)
            _state.ownership = TerritoryOwnership.LordOwned;
            _state.drugContamination = 60f;
            _state.lastPreferredFeedDay = -1;
        }

        [TearDown]
        public void TearDown()
        {
            // 전역 싱글턴 상태 복원 — 타 fixture(FlagTakeover/VillageEconomy/Mercenary 등) 오염 방지
            if (_state != null)
            {
                _state.loyaltyToPlayer = _savedLoyalty;
                _state.ownership = _savedOwnership;
                _state.drugContamination = 0f;
                _state.lastPreferredFeedDay = -1;
            }
            RestoreTime(_tm, _savedDay, _savedGameTime); // [113차] 전역 시각 원복 — 후속 fixture 일차 보존
            if (_tm != null && !_tmExisted)
            {
                // [113차] 내가 생성한 TimeManager는 소각 — "TimeManager 부재 시 CurrentDay()=1" 전제를
                // 가진 타 fixture(MercenaryContractDueTests 등)의 원상을 복원한다.
                // QA 3차 실측: 관리자 존속만으로 CurrentDay()가 관리자 일차(0)를 읽어 기대값 8이 7로 무너짐.
                Object.DestroyImmediate(_tm.gameObject);
                typeof(TimeManager).GetProperty("Instance").SetValue(null, null); // private setter — 리플렉션 원복
            }
            _tm = null;
        }

        /// <summary>[113차] TimeManager 전역 시각 스냅샷/복원 — 비공개 필드 직접 접근
        /// (GameTime 세터는 dayDelta 누산 구조라 정규화 값 재대입으로 일차를 되돌릴 수 없음.
        /// SaveManager.ApplyTimeData의 SetPrivateField("_currentDay") 선례와 동일 계약).</summary>
        private static (int day, float gameTime) SnapshotTime(TimeManager tm)
        {
            var dayField = typeof(TimeManager).GetField("_currentDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var timeField = typeof(TimeManager).GetField("_gameTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return ((int)dayField.GetValue(tm), (float)timeField.GetValue(tm));
        }

        private static void RestoreTime(TimeManager tm, int day, float gameTime)
        {
            var dayField = typeof(TimeManager).GetField("_currentDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var timeField = typeof(TimeManager).GetField("_gameTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dayField.SetValue(tm, day);
            timeField.SetValue(tm, gameTime);
        }

        [Test]
        public void TickFeedLoyalty_NeverFed_AnchorInitializedWithoutDecay()
        {
            float before = _state.loyaltyToPlayer;

            TerritoryLordDoorSystem.TickFeedLoyalty();

            Assert.AreEqual(_tm.CurrentDay, _state.lastPreferredFeedDay,
                "미지급 이력 없음(-1) → 첫 관측일로 앵커 초기화");
            Assert.AreEqual(before, _state.loyaltyToPlayer, "앵커 초기화 날에는 충성도 감소 없음");
        }

        [Test]
        public void TickFeedLoyalty_WithinGrace_NoDecay()
        {
            _state.lastPreferredFeedDay = _tm.CurrentDay - 3;   // 경과 3일 ≤ 유예 7일
            float before = _state.loyaltyToPlayer;

            TerritoryLordDoorSystem.TickFeedLoyalty();

            Assert.AreEqual(before, _state.loyaltyToPlayer, "유예 기간 내에는 충성도 불변");
        }

        [Test]
        public void TickFeedLoyalty_OverGrace_DecaysDaily_AnchorKept()
        {
            _state.lastPreferredFeedDay = _tm.CurrentDay - 10;  // 경과 10일 > 유예 7일
            float before = _state.loyaltyToPlayer;

            TerritoryLordDoorSystem.TickFeedLoyalty();

            Assert.AreEqual(before - TerritoryLordDoorSystem.FEED_PENALTY_PER_DAY, _state.loyaltyToPlayer,
                "유예 초과 시 일일 페널티(-2) 적용");
            Assert.AreEqual(_tm.CurrentDay - 10, _state.lastPreferredFeedDay,
                "페널티 틱은 앵커를 리셋하지 않는다 — 미지급이 계속되면 매일 추가 감소(지속 부담 계약)");
        }

        [Test]
        public void TickFeedLoyalty_DoorClosed_NoAnchorNoDecay()
        {
            // 문이 잠긴 영지는 줄 수 없으므로 앵커도 감소도 없음(공정성 계약)
            _state.drugContamination = 0f;
            _state.lastPreferredFeedDay = _tm.CurrentDay - 10;  // 문잠김이어도 이 값은 불변
            float before = _state.loyaltyToPlayer;

            TerritoryLordDoorSystem.TickFeedLoyalty();

            Assert.AreEqual(before, _state.loyaltyToPlayer, "문 잠김 영지는 페널티 없음");
            Assert.AreEqual(_tm.CurrentDay - 10, _state.lastPreferredFeedDay, "문 잠김 영지는 앵커도 갱신하지 않음");
        }

        [Test]
        public void TickFeedLoyalty_PlayerOwnedTerritory_NoPenalty()
        {
            // 플레이어 점령 영지의 충성도는 영주 선호 음식과 무관 — 페널티 제외
            _state.ownership = TerritoryOwnership.PlayerOwned;
            _state.lastPreferredFeedDay = _tm.CurrentDay - 10;
            float before = _state.loyaltyToPlayer;

            TerritoryLordDoorSystem.TickFeedLoyalty();

            Assert.AreEqual(before, _state.loyaltyToPlayer, "PlayerOwned 영지는 페널티 대상 아님");
        }

        [Test]
        public void TerritorySaveData_LastPreferredFeedDay_RoundTripAndLegacyDefault()
        {
            // 세이브 왕복 — 신규 필드 값 보존
            var dto = new TerritorySaveData { lastPreferredFeedDay = 42 };
            var restored = JsonUtility.FromJson<TerritorySaveData>(JsonUtility.ToJson(dto));
            Assert.AreEqual(42, restored.lastPreferredFeedDay, "지급 이력 일차 세이브 왕복 보존");

            // 구세이브 — 필드 누락 JSON은 초기값 -1(미지급) 유지가 계약
            var legacy = JsonUtility.FromJson<TerritorySaveData>("{\"territoryId\":\"East_01\"}");
            Assert.AreEqual(-1, legacy.lastPreferredFeedDay, "구세이브 누락 필드는 -1(미지급) 유지");
        }

        [Test]
        public void TerritoryState_LastPreferredFeedDay_ClampsBelowMinusOne()
        {
            _state.lastPreferredFeedDay = -99;
            Assert.AreEqual(-1, _state.lastPreferredFeedDay, "프로퍼티 세터는 -1 하한 클램프(센티널 보존)");
            _state.lastPreferredFeedDay = -1; // 원복
        }
    }
}