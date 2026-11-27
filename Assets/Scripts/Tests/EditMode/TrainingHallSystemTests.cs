// [125차 P3] 훈련소 시스템 EditMode 테스트 — 훈련 비용/정원/결정론 능력치/가드 체인/ApplyTrainingLevelUp.
// 근거: docs/125_TRAINING_HALL_PLAN.md P5 — 순수 산식 + 런타임 인스턴스 리플렉션 조립 선례(BowAimAlignmentTests).
// EditMode 제약 반영: TimeManager.GetOrCreate()로 시간 세팅, PlayerStats는 GameObject 조립 후
// 리플렉션으로 Instance 교체/복원(테스트 격리 — 세이브 무영향), GuardPlaceholder는 GameObject 조립 후
// private 필드(level/_statAttack/...) 직접 검증. TrainingHallSystem.ResetForTest()로 레지스트리 격리.
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class TrainingHallSystemTests
    {
        private const string SystemPath = "Assets/Scripts/Systems/TrainingHallSystem.cs";
        private const string GuardPath = "Assets/Scripts/Systems/GuardPlaceholder.cs";

        private GameObject _guardObj;
        private GameObject _statsObj;
        private GameObject _timeObj;
        private GuardPlaceholder _guard;
        private PlayerStats _stats;
        private TimeManager _time;

        private static PropertyInfo PlayerStatsInstance { get; } =
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        private static PropertyInfo TimeManagerInstance { get; } =
            typeof(TimeManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

        [SetUp]
        public void SetUp()
        {
            TrainingHallSystem.ResetForTest();

            _guardObj = new GameObject("TestGuard");
            _guard = _guardObj.AddComponent<GuardPlaceholder>();
            SetField(_guard, "level", 10);
            // _statsRolled=true 강제 — RollStats 랜덤 오차 배제 후 능력치 고정
            SetField(_guard, "_statsRolled", true);
            SetField(_guard, "_statAttack", 7);
            SetField(_guard, "_statDefense", 5);
            SetField(_guard, "_statVitality", 3);
            SetField(_guard, "_maxHP", 100f);
            SetField(_guard, "_currentHP", 80f);

            _statsObj = new GameObject("TestPlayerStats");
            _stats = _statsObj.AddComponent<PlayerStats>();
            PlayerStatsInstance.SetValue(null, _stats);
            _stats.AddGold(10000, "test_seed");

            _timeObj = new GameObject("TestTimeManager");
            _time = TimeManager.GetOrCreate();
        }

        [TearDown]
        public void TearDown()
        {
            TrainingHallSystem.ResetForTest();
            Object.DestroyImmediate(_guardObj);
            if (_statsObj != null) Object.DestroyImmediate(_statsObj);
            if (_timeObj != null) Object.DestroyImmediate(_timeObj);
        }

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"필드 없음: {name}");
            f.SetValue(obj, value);
        }

        private static T GetField<T>(object obj, string name)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"필드 없음: {name}");
            return (T)f.GetValue(obj);
        }

        // ════════════════════════════════════════════════════════════════
        // ① 비용 산식 — Lv × 20G
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void CostForLevel_IsLevelTimesTwenty()
        {
            Assert.AreEqual(20, TrainingHallSystem.CostForLevel(1), "Lv1 훈련 비용 = 20G");
            Assert.AreEqual(100, TrainingHallSystem.CostForLevel(5), "Lv5 훈련 비용 = 100G");
            Assert.AreEqual(760, TrainingHallSystem.CostForLevel(38), "Lv38 훈련 비용 = 760G");
        }

        [Test]
        public void CostForLevel_NonPositiveLevel_ClampsToZero()
        {
            Assert.AreEqual(0, TrainingHallSystem.CostForLevel(0), "Lv0 훈련 비용 = 0G (음수 방어)");
            Assert.AreEqual(0, TrainingHallSystem.CostForLevel(-5), "Lv-5 훈련 비용 = 0G (음수 방어)");
        }

        // ════════════════════════════════════════════════════════════════
        // ② 진행률 / 남은 시간 포맷
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void GetProgress01_Boundaries_AreClamped()
        {
            var s = new TrainingHallSystem.TrainingSession
            {
                StartGameTime = 36000f, EndGameTime = 39600f
            };
            Assert.AreEqual(0f, TrainingHallSystem.GetProgress01(s, 36000f), "시작 시점 진행률 = 0");
            Assert.AreEqual(0.5f, TrainingHallSystem.GetProgress01(s, 37800f), 0.001f, "1시간 중반 진행률 = 0.5");
            Assert.AreEqual(1f, TrainingHallSystem.GetProgress01(s, 39600f), "종료 시점 진행률 = 1");
            Assert.AreEqual(1f, TrainingHallSystem.GetProgress01(s, 40000f), "종료 초과 진행률 = 1 (clamp)");
        }

        [Test]
        public void GetProgress01_WrapsAcrossDayBoundary()
        {
            // 게임시간 랩(86400) 대응 — 23:00 시작 + 1시간 훈련 → 익일 00:00 완료
            var s = new TrainingHallSystem.TrainingSession
            {
                StartGameTime = 82800f, EndGameTime = 86400f
            };
            Assert.AreEqual(0.5f, TrainingHallSystem.GetProgress01(s, 84600f), 0.001f, "랩 중반 진행률 = 0.5");
            Assert.AreEqual(1f, TrainingHallSystem.GetProgress01(s, 90000f - 86400f), "랩 후(0초 시점) 진행률 = 1");
        }

        [Test]
        public void GetRemainingSeconds_DecreasesWithTime()
        {
            var s = new TrainingHallSystem.TrainingSession
            {
                StartGameTime = 100f, EndGameTime = 3700f
            };
            Assert.AreEqual(3600, TrainingHallSystem.GetRemainingSeconds(s, 100f), "시작 직후 남은 시간 = 3600초");
            Assert.AreEqual(1800, TrainingHallSystem.GetRemainingSeconds(s, 1900f), "중간 시점 남은 시간 = 1800초");
            Assert.AreEqual(0, TrainingHallSystem.GetRemainingSeconds(s, 3700f), "종료 시점 남은 시간 = 0초");
            Assert.AreEqual(0, TrainingHallSystem.GetRemainingSeconds(s, 5000f), "종료 초과 남은 시간 = 0초 (clamp)");
        }

        [Test]
        public void FormatRemaining_HHMMSS()
        {
            Assert.AreEqual("01:00:00", TrainingHallSystem.FormatRemaining(3600), "3600초 = 01:00:00");
            Assert.AreEqual("00:12:00", TrainingHallSystem.FormatRemaining(720), "720초 = 00:12:00");
            Assert.AreEqual("00:00:00", TrainingHallSystem.FormatRemaining(0), "0초 = 00:00:00");
            Assert.AreEqual("00:00:00", TrainingHallSystem.FormatRemaining(-10), "음수 = 00:00:00 (clamp)");
        }

        // ════════════════════════════════════════════════════════════════
        // ③ 결정론 능력치 — 시드 고정 재현 / 합계 최소 1
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void RollGains_SameSeed_Deterministic()
        {
            var first = TrainingHallSystem.RollGains(12345);
            var second = TrainingHallSystem.RollGains(12345);
            Assert.AreEqual(first.atk, second.atk, "동일 시드 → 동일 atkGain");
            Assert.AreEqual(first.def, second.def, "동일 시드 → 동일 defGain");
            Assert.AreEqual(first.vit, second.vit, "동일 시드 → 동일 vitGain");
        }

        [Test]
        public void RollGains_DifferentSeeds_CanDiffer()
        {
            // 다른 시드는 반드시 다를 필요는 없지만, 100개 시드 중 적어도 2가지 이상 조합 등장
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                var g = TrainingHallSystem.RollGains(i);
                seen.Add($"{g.atk},{g.def},{g.vit}");
            }
            Assert.GreaterOrEqual(seen.Count, 2, "다양한 시드 → 다양한 능력치 조합");
        }

        [Test]
        public void RollGains_RangeAndMinSumGuaranteed()
        {
            for (int i = 0; i < 500; i++)
            {
                var g = TrainingHallSystem.RollGains(i * 7919);
                Assert.GreaterOrEqual(g.atk, 0, $"seed={i * 7919} atkGain ≥ 0");
                Assert.LessOrEqual(g.atk, 2, $"seed={i * 7919} atkGain ≤ 2");
                Assert.GreaterOrEqual(g.def, 0, $"seed={i * 7919} defGain ≥ 0");
                Assert.LessOrEqual(g.def, 2, $"seed={i * 7919} defGain ≤ 2");
                Assert.GreaterOrEqual(g.vit, 0, $"seed={i * 7919} vitGain ≥ 0");
                Assert.LessOrEqual(g.vit, 2, $"seed={i * 7919} vitGain ≤ 2");
                Assert.GreaterOrEqual(g.atk + g.def + g.vit, 1, $"seed={i * 7919} 합계 최소 1 보장");
            }
        }

        [Test]
        public void SeedFor_StableIdAndCount_Deterministic()
        {
            int s1 = TrainingHallSystem.SeedFor("G000001", 0);
            int s2 = TrainingHallSystem.SeedFor("G000001", 0);
            int s3 = TrainingHallSystem.SeedFor("G000001", 1);
            int s4 = TrainingHallSystem.SeedFor("G000002", 0);
            Assert.AreEqual(s1, s2, "동일 입력 → 동일 시드");
            Assert.AreNotEqual(s1, s3, "훈련횟수 차이 → 다른 시드");
            Assert.AreNotEqual(s1, s4, "StableId 차이 → 다른 시드");
        }

        // ════════════════════════════════════════════════════════════════
        // ④ 가드 체인 — 중복 / 정원 20 / 골드 부족
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void StartTraining_NullGuard_Rejected()
        {
            string reason = TrainingHallSystem.StartTraining(null, "North_01", out var session);
            Assert.IsNotNull(reason, "null 병사 거부 사유 반환");
            Assert.IsNull(session, "null 병사 세션 미등록");
        }

        [Test]
        public void StartTraining_DuplicateTraining_Rejected()
        {
            string reason1 = TrainingHallSystem.StartTraining(_guard, "North_01", out var s1);
            Assert.IsNull(reason1, "1차 훈련 성공");
            string reason2 = TrainingHallSystem.StartTraining(_guard, "North_01", out var s2);
            Assert.IsNotNull(reason2, "중복 훈련 거부 사유 반환");
            Assert.IsNull(s2, "중복 훈련 세션 미등록");
            Assert.AreEqual(1, TrainingHallSystem.ActiveSessions.Count, "중복 거부 후 세션 1개 유지");
        }

        [Test]
        public void StartTraining_CapacityTwenty_Rejected()
        {
            // 정원 20 — 21번째 훈련 거부
            for (int i = 0; i < TrainingHallSystem.Capacity; i++)
            {
                var g = new GameObject($"Guard_{i}").AddComponent<GuardPlaceholder>();
                SetField(g, "level", 5 + i);
                SetField(g, "_statsRolled", true);
                string r = TrainingHallSystem.StartTraining(g, "North_01", out var s);
                Assert.IsNull(r, $"{i + 1}번째 훈련 성공");
                Object.DestroyImmediate(g.gameObject);
            }
            Assert.AreEqual(TrainingHallSystem.Capacity, TrainingHallSystem.ActiveSessions.Count, "정원 20명 도달");
            string reason = TrainingHallSystem.StartTraining(_guard, "North_01", out var session);
            Assert.IsNotNull(reason, "정원 초과 거부 사유 반환");
            Assert.IsNull(session, "정원 초과 세션 미등록");
        }

        [Test]
        public void StartTraining_InsufficientGold_Rejected()
        {
            SetField(_stats, "_gold", 5);   // Lv10 × 20G = 200G 필요 — 5G 부족
            string reason = TrainingHallSystem.StartTraining(_guard, "North_01", out var session);
            Assert.IsNotNull(reason, "골드 부족 거부 사유 반환");
            Assert.IsNull(session, "골드 부족 세션 미등록");
            Assert.AreEqual(5, _stats.Gold, "거부 시 골드 미차감");
            Assert.AreEqual(0, TrainingHallSystem.ActiveSessions.Count, "거부 시 세션 미등록");
        }

        [Test]
        public void StartTraining_Success_DeductsGoldAndRegistersSession()
        {
            SetField(_stats, "_gold", 1000);
            int before = _stats.Gold;
            int expectedCost = TrainingHallSystem.CostForLevel(_guard.Level);

            string reason = TrainingHallSystem.StartTraining(_guard, "North_01", out var session);
            Assert.IsNull(reason, "훈련 성공");
            Assert.IsNotNull(session, "세션 등록");
            Assert.AreEqual(_guard.StableId, session.StableId, "세션 = StableId 키");
            Assert.AreEqual("North_01", session.TerritoryKey, "세션 영지 키");
            Assert.AreEqual(_guard.Level, session.LevelAtStart, "세션 시작 레벨");
            Assert.AreEqual(expectedCost, session.CostGold, "세션 비용 = Lv × 20G");
            Assert.AreEqual(before - expectedCost, _stats.Gold, "훈련 시작 골드 차감");
            Assert.AreEqual(1, TrainingHallSystem.ActiveSessions.Count, "세션 1개 등록");
        }

        // ════════════════════════════════════════════════════════════════
        // ⑤ 훈련 취소 — 비용 환불 없음
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void CancelTraining_NoRefund_RequiresRegisteredSession()
        {
            SetField(_stats, "_gold", 1000);
            int before = _stats.Gold;
            TrainingHallSystem.StartTraining(_guard, "North_01", out var _);

            bool cancelled = TrainingHallSystem.CancelTraining(_guard.StableId);
            Assert.IsTrue(cancelled, "등록된 세션 취소 성공");
            Assert.AreEqual(0, TrainingHallSystem.ActiveSessions.Count, "취소 후 세션 0개");
            Assert.AreEqual(before - TrainingHallSystem.CostForLevel(_guard.Level), _stats.Gold, "취소 시 골드 환불 없음");
        }

        [Test]
        public void CancelTraining_UnknownSession_Fails()
        {
            Assert.IsFalse(TrainingHallSystem.CancelTraining("G999999"), "미등록 세션 취소 실패");
        }

        // ════════════════════════════════════════════════════════════════
        // ⑥ 완료 폴링 — PollCompletion
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void PollCompletion_BeforeEnd_DoesNotComplete()
        {
            TrainingHallSystem.StartTraining(_guard, "North_01", out var s);
            // nowGameTime = s.StartGameTime + 1799 — 종료 시각 이전
            TrainingHallSystem.PollCompletion(s.StartGameTime + TrainingHallSystem.TrainingDurationSeconds - 1);
            Assert.AreEqual(1, TrainingHallSystem.ActiveSessions.Count, "종료 이전 세션 유지");
            Assert.AreEqual(0, TrainingHallSystem.Completed.Count, "종료 이전 완료 0개");
            Assert.AreEqual(10, _guard.Level, "종료 이전 레벨 불변");
        }

        [Test]
        public void PollCompletion_AfterEnd_AppliesLevelUpAndMovesToCompleted()
        {
            TrainingHallSystem.StartTraining(_guard, "North_01", out var s);
            int expectedAtk = s.AtkGain;
            int expectedDef = s.DefGain;
            int expectedVit = s.VitGain;
            int expectedNewLevel = s.LevelAtStart + 1;

            TrainingHallSystem.PollCompletion(s.EndGameTime + 1f);

            Assert.AreEqual(0, TrainingHallSystem.ActiveSessions.Count, "완료 후 세션 0개");
            Assert.AreEqual(1, TrainingHallSystem.Completed.Count, "완료 후 완료 1개");
            Assert.AreEqual(expectedNewLevel, _guard.Level, "완료 후 레벨 +1");
            Assert.AreEqual(7 + expectedAtk, _guard.GetStatAttack(), "공격 가산");
            Assert.AreEqual(5 + expectedDef, _guard.GetStatDefense(), "방어 가산");
            Assert.AreEqual(3 + expectedVit, _guard.GetStatVitality(), "체력 가산");

            var done = TrainingHallSystem.Completed[0];
            Assert.AreEqual(_guard.StableId, done.StableId, "완료 항목 StableId");
            Assert.AreEqual(expectedNewLevel, done.NewLevel, "완료 항목 NewLevel");
            Assert.IsFalse(done.Deployed, "배치 전 Deployed = false");
        }

        [Test]
        public void PollCompletion_Event_RaisesOnCompleted()
        {
            TrainingHallSystem.StartTraining(_guard, "North_01", out var s);
            TrainingHallSystem.CompletedTraining received = null;
            TrainingHallSystem.OnTrainingCompleted += (c) => received = c;

            TrainingHallSystem.PollCompletion(s.EndGameTime + 1f);

            Assert.IsNotNull(received, "OnTrainingCompleted 이벤트 발생");
            Assert.AreEqual(_guard.StableId, received.StableId, "이벤트 StableId");
        }

        [Test]
        public void MarkDeployed_FlagsCompleted()
        {
            TrainingHallSystem.StartTraining(_guard, "North_01", out var s);
            TrainingHallSystem.PollCompletion(s.EndGameTime + 1f);

            Assert.IsFalse(TrainingHallSystem.Completed[0].Deployed, "배치 전 false");
            bool ok = TrainingHallSystem.MarkDeployed(_guard.StableId);
            Assert.IsTrue(ok, "배치 처리 성공");
            Assert.IsTrue(TrainingHallSystem.Completed[0].Deployed, "배치 후 true");
        }

        // ════════════════════════════════════════════════════════════════
        // ⑦ ApplyTrainingLevelUp — 레벨+1 · 능력치 가산 · MaxHP 재계산
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void ApplyTrainingLevelUp_IncrementsLevelAndAddsStats()
        {
            int prevLevel = _guard.Level;
            int prevAtk = _guard.GetStatAttack();
            int prevDef = _guard.GetStatDefense();
            int prevVit = _guard.GetStatVitality();

            _guard.ApplyTrainingLevelUp(2, 1, 1);

            Assert.AreEqual(prevLevel + 1, _guard.Level, "레벨 +1");
            Assert.AreEqual(prevAtk + 2, _guard.GetStatAttack(), "atkGain 가산");
            Assert.AreEqual(prevDef + 1, _guard.GetStatDefense(), "defGain 가산");
            Assert.AreEqual(prevVit + 1, _guard.GetStatVitality(), "vitGain 가산");
        }

        [Test]
        public void ApplyTrainingLevelUp_IncreasesMaxHPByVitTimesTwo()
        {
            float prevMaxHP = _guard.MaxHP;
            float prevHP = _guard.HP;
            int vitGain = 1;

            _guard.ApplyTrainingLevelUp(0, 0, vitGain);

            Assert.AreEqual(prevMaxHP + vitGain * 2f, _guard.MaxHP, "MaxHP + vit×2 (L903 산식 등가)");
            Assert.AreEqual(prevHP + vitGain * 2f, _guard.HP, "HP + vit×2 (기저 유지)");
        }

        [Test]
        public void ApplyTrainingLevelUp_ClampsHPCurrentToNewMax()
        {
            SetField(_guard, "_currentHP", 99f);   // MaxHP 100 — 현재 HP 99
            float prevMaxHP = _guard.MaxHP;
            _guard.ApplyTrainingLevelUp(0, 0, 1);   // MaxHP 102, HP = min(99+2, 102) = 101
            Assert.AreEqual(prevMaxHP + 2f, _guard.MaxHP, "MaxHP 가산");
            Assert.AreEqual(101f, _guard.HP, "HP = min(기존+vit*2, 새 MaxHP)");
        }

        [Test]
        public void ApplyTrainingLevelUp_NegativeGains_ClampedToZero()
        {
            int prevAtk = _guard.GetStatAttack();
            int prevDef = _guard.GetStatDefense();
            int prevVit = _guard.GetStatVitality();
            float prevMaxHP = _guard.MaxHP;

            _guard.ApplyTrainingLevelUp(-5, -5, -5);

            Assert.AreEqual(prevAtk, _guard.GetStatAttack(), "음수 atkGain clamp → 0 가산");
            Assert.AreEqual(prevDef, _guard.GetStatDefense(), "음수 defGain clamp → 0 가산");
            Assert.AreEqual(prevVit, _guard.GetStatVitality(), "음수 vitGain clamp → 0 가산");
            Assert.AreEqual(prevMaxHP, _guard.MaxHP, "음수 vitGain → MaxHP 불변");
        }

        [Test]
        public void ApplyTrainingLevelUp_ZeroGains_OnlyLevelUp()
        {
            int prevLevel = _guard.Level;
            float prevMaxHP = _guard.MaxHP;
            _guard.ApplyTrainingLevelUp(0, 0, 0);
            Assert.AreEqual(prevLevel + 1, _guard.Level, "0가산 → 레벨만 +1");
            Assert.AreEqual(prevMaxHP, _guard.MaxHP, "0가산 → MaxHP 불변");
        }

        // ════════════════════════════════════════════════════════════════
        // ⑧ 소스-텍스트 계약 (Figma 진실치 — P4 대비 계약 선반영)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void SourceContract_TrainingHallSystem_CoreConstants()
        {
            string src = System.IO.File.ReadAllText(SystemPath);
            StringAssert.Contains("public const int Capacity = 20", src, "정원 20 상수");
            StringAssert.Contains("public const int TrainingDurationSeconds = 3600", src, "훈련 1시간 상수");
            StringAssert.Contains("public const int CostPerLevel = 20", src, "레벨당 20G 상수");
            StringAssert.Contains("SourceGold = \"training_hall\"", src, "골드 원장 source 태그");
            StringAssert.Contains("ApplyTrainingLevelUp(s.AtkGain, s.DefGain, s.VitGain)", src, "ApplyTrainingLevelUp 완료 호출 배선");
        }

        [Test]
        public void SourceContract_GuardPlaceholder_ApplyTrainingLevelUpSignature()
        {
            string src = System.IO.File.ReadAllText(GuardPath);
            StringAssert.Contains("public void ApplyTrainingLevelUp(int atkGain, int defGain, int vitGain)", src, "ApplyTrainingLevelUp 시그니처");
            StringAssert.Contains("EnsureStatsRolled();", src, "EnsureStatsRolled 선처리");
            StringAssert.Contains("level++;", src, "레벨 +1");
            StringAssert.Contains("_maxHP += Mathf.Max(0, vitGain) * 2f;", src, "MaxHP 가산 (vit×2 산식)");
        }

        [Test]
        public void SourceContract_TrainingHallSystem_RollGainsDeterministic()
        {
            string src = System.IO.File.ReadAllText(SystemPath);
            StringAssert.Contains("new System.Random(seed)", src, "System.Random 결정론 랜덤");
            StringAssert.Contains("SeedFor(guard.StableId", src, "StableId+훈련횟수 시드");
            StringAssert.Contains("atk + def + vit == 0", src, "합계 0 방지 재설정 루프");
        }

        [Test]
        public void SourceContract_TrainingHallSystem_RegistryAndDriver()
        {
            string src = System.IO.File.ReadAllText(SystemPath);
            StringAssert.Contains("private static readonly List<TrainingSession> _sessions", src, "정적 세션 레지스트리");
            StringAssert.Contains("public static void ResetForTest()", src, "EditMode 격리용 ResetForTest");
            StringAssert.Contains("class Driver : MonoBehaviour", src, "300ms 폴링 드라이버 MonoBehaviour");
            StringAssert.Contains("PollInterval = 0.3f", src, "300ms 폴링 간격");
            StringAssert.Contains("DontDestroyOnLoad(go)", src, "DDOL — 씬 언로드 생존");
        }
    }
}
