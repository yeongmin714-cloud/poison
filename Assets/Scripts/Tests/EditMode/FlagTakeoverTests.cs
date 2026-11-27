using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [FLAG_TAKEOVER_PLAN F1~F4] 신규 시스템 EditMode 강제 테스트 (QA 위임 역할 — 구현/QA 분리:
    /// 구현 코드 수정 금지, 본 테스트 파일만 유지보수. docs/AGENT_DELEGATION_POLICY.md).
    ///
    /// 검증 범위:
    ///  1) SparedLordTributeSystem.CalculateWeeklyTribute 산식 — 보상표×0.4×성향계수, 최소 10G 보장
    ///     (모든 성향×링 조합이 10G 미만이 될 수 없으므로 "최소 보장"은 전 조합 스윕으로 검증)
    ///  2) LordBetrayalSystem.GetBetrayalProbability — base 0.01 + 성향 가산 + 충성도 역비례 0.03,
    ///     반복 배신자(≥2회) ×1.5, 상한 클램프 0.12
    ///  3) RegisterSparedLord/UnregisterSparedLord 멱등/IsRegistered — PlayerOwned 가드,
    ///     주간 결산(_lastPaidWeek 멱등), 세이브 v5 ImportSaveData 라운드트립,
    ///     F1 플레이어 깃발 색상 폴백 + 점령 게양/상실 회수 데이터 동기화
    ///
    /// EditMode 함정 대응 (unity-qa-runner):
    ///  - AddComponent 후 Awake 미실행 대비: TimeManager는 GetOrCreate(Instance 직접 배정),
    ///    PlayerStats는 AddComponent 후 Instance 불일치 시 리플렉션 주입 (MercenaryContractDueTests 선례)
    ///  - TimeManager.OnDayStart는 이벤트(외부 Invoke 불가) → 백킹 필드 리플렉션 발화
    ///  - TerritoryDatabase는 코드 생성 DB — Instance 항상 유효 (East_01/West_01 정의 존재)
    ///  - 결정론: 무작위 미사용 산식만 검증 (UnityEngine.Random.InitState 불필요)
    /// </summary>
    public sealed class FlagTakeoverTests
    {
        private static readonly TerritoryId East1Id = new TerritoryId(NationType.East, 1);
        private static readonly TerritoryId West1Id = new TerritoryId(NationType.West, 1);
        private const string EastKey = "East_01";
        private const string WestKey = "West_01";
        private const float Day = 86400f; // 게임일 1일 = 86,400초 (TimeManager.GameTime 기준)

        private TimeManager _tm;
        private GameObject _statsGo;
        private PlayerStats _stats;
        private System.Delegate _savedGoldListeners;

        [OneTimeSetUp]
        public void OneTimeInit()
        {
            // 편집 모드 무해 로그(DontDestroyOnLoad 등)가 UTF 실패로 이어지지 않도록 차단.
            // 행위 검증은 어설션으로 수행 — 로그 실패 차단은 편집 모드 노이즈 한정.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            _tm = TimeManager.GetOrCreate(); // Instance를 직접 배정 — Awake 의존 없음
            Assert.IsNotNull(_tm, "TimeManager GetOrCreate 실패");
        }

        [OneTimeTearDown]
        public void OneTimeCleanup()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            // [도메인 위생] 본 테스트가 만든 TimeManager는 풀런에서 뒤따르는 파일(MercenaryContract 등
            // "TM 부재/오늘=1" 전제)을 오염시킨다 — 실측: StaleSafe + Approve/Due/Notify 17건 파생 실패.
            // GO 파괴 + Instance 리셋(리플렉션 — private setter)으로 도메인 원복.
            if (_tm != null) Object.DestroyImmediate(_tm.gameObject);
            typeof(TimeManager).GetProperty("Instance")?.SetValue(null, null);
            _tm = null;
        }

        [SetUp]
        public void SetUp()
        {
            SparedLordTributeSystem.ResetAll();
            LordBetrayalSystem.ResetAll(); // 배신 구독 해제 — 결산 발화 간섭 차단
            TerritoryCaptureSystem.ResetAll();
            LordSurrenderSystem.ResetAll();
            TerritoryBuilder.RemoveOutdoorCraftStation(EastKey);
            TerritoryBuilder.RemoveOutdoorCraftStation(WestKey);

            // 영지 런타임 상태 리셋 — TerritoryDatabase는 도메인 수명 싱글턴이라 테스트 간 잔존
            // (실측: Import 테스트가 남긴 tributeWeeksPaid=2가 Pays 테스트의 "But was: 3" 실패의 근원)
            ResetTerritoryRuntimeState(East1Id);
            ResetTerritoryRuntimeState(West1Id);

            _savedGoldListeners = GetGoldListeners();
            SetGoldListeners(null);

            SetStaticInstance<PlayerStats>(null);
            _statsGo = new GameObject("FlagTakeover_PlayerStats");
            _stats = _statsGo.AddComponent<PlayerStats>();
            if (PlayerStats.Instance != _stats) SetStaticInstance<PlayerStats>(_stats);

            SetStaticInstance<EmblemManager>(null); // F1 폴백 경로 결정론 고정
        }

        [TearDown]
        public void TearDown()
        {
            SparedLordTributeSystem.ResetAll();
            LordBetrayalSystem.ResetAll();
            TerritoryCaptureSystem.ResetAll();
            LordSurrenderSystem.ResetAll();
            TerritoryBuilder.RemoveOutdoorCraftStation(EastKey);
            TerritoryBuilder.RemoveOutdoorCraftStation(WestKey);

            var db = TerritoryDatabase.Instance;
            db.SetOwnership(East1Id, TerritoryOwnership.LordOwned); // 소유 복구
            db.SetOwnership(West1Id, TerritoryOwnership.LordOwned);

            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
            PlayerStats.ResetInstance();
            SetStaticInstance<PlayerStats>(null);

            SetGoldListeners(FilterLiveListeners(_savedGoldListeners));
            _savedGoldListeners = null;

            SweepRootObjects("[Flag]", "[CaptureParticle]", "Outdoor_CraftStation_");
        }

        /// <summary>영지 런타임 상태 전면 복귀 — 도메인 수명 싱글턴의 테스트 간 잔존 방지.</summary>
        private static void ResetTerritoryRuntimeState(TerritoryId id)
        {
            var s = TerritoryDatabase.Instance.GetState(id);
            s.lordSpared = false;
            s.lordSurrendered = false;
            s.lordDefeated = false;
            s.lordExecuted = false;
            s.tributeWeeksPaid = 0;
            s.betrayalCount = 0;
            s.loyaltyToPlayer = 0f;
            s.flagRaised = false;
            s.territoryGold = 0;
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. 주간 공물 산식 — 보상표 × 0.4 × 성향 계수 (결정론)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void CalculateWeeklyTribute_Ring1Greedy_Yields12()
        {
            // Ring1 즉시 보상 50G × 0.4 = 20 × 탐욕 0.6 = 12
            Assert.That(SparedLordTributeSystem.GetSpareGoldReward(TerritoryDifficulty.Ring1), Is.EqualTo(50),
                "보상표 Ring1 = 50G");
            Assert.That(SparedLordTributeSystem.TRIBUTE_RATIO, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(SparedLordTributeSystem.GetPersonalityTributeFactor(LordPersonality.Greedy),
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(
                SparedLordTributeSystem.CalculateWeeklyTribute(TerritoryDifficulty.Ring1, LordPersonality.Greedy),
                Is.EqualTo(12), "Ring1×Greedy = 50×0.4×0.6 = 12G");
        }

        [Test]
        public void CalculateWeeklyTribute_Ring1Cowardly_Yields24()
        {
            // 50G × 0.4 = 20 × 겁많음 1.2 = 24
            Assert.That(
                SparedLordTributeSystem.CalculateWeeklyTribute(TerritoryDifficulty.Ring1, LordPersonality.Cowardly),
                Is.EqualTo(24), "Ring1×Cowardly = 50×0.4×1.2 = 24G");
        }

        [Test]
        public void CalculateWeeklyTribute_Ring4Neutral_Yields240()
        {
            // Ring4 즉시 보상 600G × 0.4 = 240 × 보통 1.0 = 240
            Assert.That(SparedLordTributeSystem.GetSpareGoldReward(TerritoryDifficulty.Ring4), Is.EqualTo(600),
                "보상표 Ring4 = 600G");
            Assert.That(
                SparedLordTributeSystem.CalculateWeeklyTribute(TerritoryDifficulty.Ring4, LordPersonality.Neutral),
                Is.EqualTo(240), "Ring4×Neutral = 600×0.4×1.0 = 240G");
        }

        [Test]
        public void CalculateWeeklyTribute_AllCombinations_NeverBelowMinimumGuarantee()
        {
            // 최소 10G 보장 — 전 링 × 전 성향 조합 스윕 (산식상 10 미만 조합이 없음을 계약으로 고정).
            // (QA 지시: min 미달 케이스가 성립하지 않아 상한계 검증은 배신 확률 클램프 테스트로 대체됨)
            var difficulties = new[]
            {
                TerritoryDifficulty.Ring1, TerritoryDifficulty.Ring2, TerritoryDifficulty.Ring3,
                TerritoryDifficulty.Ring4, TerritoryDifficulty.Empire
            };
            var personalities = new[]
            {
                LordPersonality.Neutral, LordPersonality.Greedy, LordPersonality.Suspicious,
                LordPersonality.Brave, LordPersonality.Cowardly, LordPersonality.Wise, LordPersonality.Cruel
            };

            foreach (var difficulty in difficulties)
            foreach (var personality in personalities)
            {
                Assert.That(
                    SparedLordTributeSystem.CalculateWeeklyTribute(difficulty, personality),
                    Is.GreaterThanOrEqualTo(SparedLordTributeSystem.MIN_TRIBUTE),
                    $"{difficulty}×{personality} — 최소 {SparedLordTributeSystem.MIN_TRIBUTE}G 보장");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 배신 확률 산식 — base 0.01 + 성향 + 충성도 역비례, ×1.5, 클램프 0.12
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void GetBetrayalProbability_Loyalty0Cruel_Returns008()
        {
            // 0.01(base) + 0.04(Cruel) + (1-0/100)×0.03(충성도 0) = 0.08
            Assert.That(LordBetrayalSystem.GetPersonalityBetrayalFactor(LordPersonality.Cruel),
                Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(0f, LordPersonality.Cruel, 0),
                Is.EqualTo(0.08f).Within(0.0001f), "충성도 0 + Cruel + 배신 0회 = 0.08");
        }

        [Test]
        public void GetBetrayalProbability_RepeatBetrayer_ClampedToMax012()
        {
            // 0.08 × 1.5 = 0.12 = 상한 (클램프 — 초과 안 함), 3회 이상도 상한 유지
            Assert.That(LordBetrayalSystem.MAX_PROBABILITY, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(0f, LordPersonality.Cruel, 2),
                Is.EqualTo(0.12f).Within(0.0001f), "배신 2회 → ×1.5 = 0.12 (상한)");
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(0f, LordPersonality.Cruel, 3),
                Is.EqualTo(0.12f).Within(0.0001f), "배신 3회 — 상한 초과 금지");
        }

        [Test]
        public void GetBetrayalProbability_Loyalty100Wise_Returns0015()
        {
            // 0.01(base) + 0.005(Wise) + (1-1)×0.03(충성도 100) = 0.015
            Assert.That(LordBetrayalSystem.GetPersonalityBetrayalFactor(LordPersonality.Wise),
                Is.EqualTo(0.005f).Within(0.0001f));
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(100f, LordPersonality.Wise, 0),
                Is.EqualTo(0.015f).Within(0.0001f), "충성도 100 + Wise = 0.015 근처");
        }

        [Test]
        public void GetBetrayalProbability_RepeatMultiplier_X1Point5_UnderClamp()
        {
            // 클램프에 걸리지 않는 조합에서 배수 검증 — 충성도 0 Wise: (0.01+0.005+0.03)×1.5 = 0.0675
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(0f, LordPersonality.Wise, 0),
                Is.EqualTo(0.045f).Within(0.0001f), "배신 0회 기준 0.045");
            Assert.That(LordBetrayalSystem.GetBetrayalProbability(0f, LordPersonality.Wise, 2),
                Is.EqualTo(0.0675f).Within(0.0001f), "배신 2회 → 0.045×1.5 = 0.0675 (상한 미달)");
        }

        [Test]
        public void GetBetrayalProbability_AllCombinations_ClampedBetweenBaseAndMax()
        {
            // 전 성향 × 충성도 {0, 50, 100} × 배신 횟수 {0,1,2,5} 스윕 — [기본치, 상한 0.12] 박스 고정
            var personalities = new[]
            {
                LordPersonality.Neutral, LordPersonality.Greedy, LordPersonality.Suspicious,
                LordPersonality.Brave, LordPersonality.Cowardly, LordPersonality.Wise, LordPersonality.Cruel
            };
            float[] loyalties = { 0f, 50f, 100f };
            int[] counts = { 0, 1, 2, 5 };

            foreach (var personality in personalities)
            foreach (float loyalty in loyalties)
            foreach (int count in counts)
            {
                float p = LordBetrayalSystem.GetBetrayalProbability(loyalty, personality, count);
                Assert.That(p, Is.GreaterThanOrEqualTo(LordBetrayalSystem.BASE_PROBABILITY - 0.0001f),
                    $"{personality}(충성도 {loyalty}, {count}회) — 기본치 미만 금지");
                Assert.That(p, Is.LessThanOrEqualTo(LordBetrayalSystem.MAX_PROBABILITY + 0.0001f),
                    $"{personality}(충성도 {loyalty}, {count}회) — 상한 0.12 초과 금지");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 공물 등록/해제 — PlayerOwned 가드, 멱등, IsRegistered
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RegisterSparedLord_PlayerOwnedRequired_AndRegisterUnregisterIdempotent()
        {
            var db = TerritoryDatabase.Instance;

            // 가드: 비-PlayerOwned 영지 등록 거부
            db.SetOwnership(West1Id, TerritoryOwnership.LordOwned);
            SparedLordTributeSystem.RegisterSparedLord(West1Id, LordPersonality.Neutral, TerritoryDifficulty.Ring1);
            Assert.That(SparedLordTributeSystem.IsRegistered(West1Id), Is.False,
                "PlayerOwned가 아닌 영지 등록 거부");
            Assert.That(TerritoryDatabase.OwnershipRestoreMode, Is.False,
                "SetOwnership은 OwnershipRestoreMode를 건드리지 않는다");

            // 정상 등록
            db.SetOwnership(East1Id, TerritoryOwnership.PlayerOwned);
            SparedLordTributeSystem.RegisterSparedLord(East1Id, LordPersonality.Greedy, TerritoryDifficulty.Ring1);
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.True, "등록 성공");
            Assert.That(SparedLordTributeSystem.GetSparedTerritories(), Does.Contain(East1Id));

            // 멱등 재등록 — 사전 갱신, 중복 생성 없음
            SparedLordTributeSystem.RegisterSparedLord(East1Id, LordPersonality.Cowardly, TerritoryDifficulty.Ring1);
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.True, "재등록 멱등");
            Assert.That(SparedLordTributeSystem.GetSparedTerritories().Count, Is.EqualTo(1),
                "재등록 시 중복 항목 생성 없음");

            // 해제 + 재해제 멱등
            SparedLordTributeSystem.UnregisterSparedLord(East1Id, "테스트");
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.False, "해제 성공");
            Assert.DoesNotThrow(() => SparedLordTributeSystem.UnregisterSparedLord(East1Id, "재해제"),
                "미등록 재해제는 무시(멱등)");
            Assert.That(SparedLordTributeSystem.GetSparedTerritories(), Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 주간 결산 — OnDayStart(주 경계) 지급 + _lastPaidWeek 멱등
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void WeeklySettlement_PaysOnWeekBoundary_IsIdempotentPerWeek()
        {
            var db = TerritoryDatabase.Instance;
            db.SetOwnership(East1Id, TerritoryOwnership.PlayerOwned);
            var state = db.GetState(East1Id);
            state.lordSpared = true; // SpareLord 경유 상태 재현 (데이터 수준)
            SparedLordTributeSystem.RegisterSparedLord(East1Id, LordPersonality.Greedy, TerritoryDifficulty.Ring1);
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.True, "전제: 공물 등록 성공");

            SetDay(_tm, 14); // 14일차 = 주 2 결산일 (day % 7 == 0)
            int gold0 = _stats.Gold;
            float loyalty0 = state.loyaltyToPlayer;

            FireOnDayStart(_tm);
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 12), "주 2 결산 — 50×0.4×0.6 = 12G 지급");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(1), "지급 누적 1주");
            Assert.That(state.loyaltyToPlayer, Is.EqualTo(loyalty0 + 2f), "공물 1회당 충성도 +2");

            FireOnDayStart(_tm); // 같은 주 재결산
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 12), "_lastPaidWeek 멱등 — 같은 주 재지급 없음");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(1), "멱등 — 누적 불변");

            SetDay(_tm, 15); // 15 % 7 ≠ 0 — 주 경계 아님
            FireOnDayStart(_tm);
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 12), "중간일 무지급");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(1));

            SetDay(_tm, 21); // 주 3 — 새 주차
            FireOnDayStart(_tm);
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 24), "다음 주차 새 지급 (누적 24G)");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(2), "누적 2주");
            Assert.That(state.loyaltyToPlayer, Is.EqualTo(loyalty0 + 4f), "충성도 누적 +4");
            Assert.That(SparedLordTributeSystem.ExportSaveData().lastTributeWeek, Is.EqualTo(3),
                "마지막 지급 주차 = 3 (세이브 v5 내보내기)");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. 세이브 v5 — ImportSaveData 주차 가드 복원 + 등록 재구성 + 레거시 시딩
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void WeeklySettlement_ImportSaveData_RestoresWeekGuardAndReseeds()
        {
            var db = TerritoryDatabase.Instance;
            db.SetOwnership(East1Id, TerritoryOwnership.PlayerOwned);
            var state = db.GetState(East1Id);
            state.lordSpared = true;
            SparedLordTributeSystem.RegisterSparedLord(East1Id, LordPersonality.Neutral, TerritoryDifficulty.Ring1);

            SetDay(_tm, 14);
            int gold0 = _stats.Gold;
            FireOnDayStart(_tm);
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 20), "주 2 결산 — 50×0.4×1.0 = 20G");

            var saved = SparedLordTributeSystem.ExportSaveData();
            Assert.That(saved.lastTributeWeek, Is.EqualTo(2), "내보내기 — 주 2");

            // 리셋 → 임포트 라운드트립
            SparedLordTributeSystem.ResetAll();
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.False, "리셋 후 미등록");
            Assert.That(SparedLordTributeSystem.ExportSaveData().lastTributeWeek, Is.EqualTo(-1), "리셋 후 미지급");

            SparedLordTributeSystem.ImportSaveData(saved, 21);
            Assert.That(SparedLordTributeSystem.IsRegistered(East1Id), Is.True,
                "lordSpared+PlayerOwned 상태에서 등록 재구성");
            Assert.That(SparedLordTributeSystem.ExportSaveData().lastTributeWeek, Is.EqualTo(2),
                "주차 가드 복원 — 소급 청구 방지");

            SetDay(_tm, 28); // 주 4 — 복원 가드(주 2)와 다른 주 → 신규 지급
            FireOnDayStart(_tm);
            var def = db.GetDefinition(East1Id);
            int expected = SparedLordTributeSystem.CalculateWeeklyTribute(def.difficulty, def.lord.personality);
            Assert.That(_stats.Gold, Is.EqualTo(gold0 + 20 + expected), "재구성 등록(정의 기반) 기준 지급");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(2), "누적 2주");

            // 레거시(null) 세이브 — 현재 주차로 시딩 → 같은 주 재결산 스킵 (소급 청구 방지)
            SparedLordTributeSystem.ImportSaveData(null, 28);
            Assert.That(SparedLordTributeSystem.ExportSaveData().lastTributeWeek, Is.EqualTo(4),
                "레거시 시딩 = currentDay / 7");
            int goldAfterSeed = _stats.Gold;
            FireOnDayStart(_tm);
            Assert.That(_stats.Gold, Is.EqualTo(goldAfterSeed), "레거시 시딩 주차와 동일 주 — 지급 없음");
            Assert.That(state.tributeWeeksPaid, Is.EqualTo(2), "소급 청구 없음 — 누적 불변");
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. [F1] 플레이어 깃발 색 — 문장 매니저 부재 폴백
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void GetPlayerFlagColor_WithoutEmblemManager_FallsBackToEastBlue()
        {
            SetStaticInstance<EmblemManager>(null); // 편집 모드 — 문장 매니저 부재 상태 강제
            Assert.That(TerritoryCaptureSystem.GetPlayerFlagColor(), Is.EqualTo(Color.blue),
                "EmblemManager 부재 시 East 파랑 폴백");
            Assert.That(TerritoryCaptureSystem.GetPlayerFlagSymbol(), Is.Null.Or.Empty,
                "문양 부재 시 null 폴백");
        }

        // ════════════════════════════════════════════════════════════════════
        // 7. [F1/F3/F4] 점령 게양 / 상실 회수 — 깃발·경계색·flagRaised 데이터 동기화
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TerritoryCapture_PlayerCaptureThenLoss_UpdatesFlagData()
        {
            var db = TerritoryDatabase.Instance;
            SetStaticInstance<EmblemManager>(null); // 폴백 색 결정론 고정 (East 파랑)
            var playerColor = TerritoryCaptureSystem.GetPlayerFlagColor();
            Assert.That(playerColor, Is.EqualTo(Color.blue), "폴백 전제 확인");

            // 플레이어 점령 — 깃발 게양 + 경계색(알파 0.6) + flagRaised
            TerritoryCaptureSystem.OnTerritoryCaptured(East1Id, NationType.East, isPlayerCapture: true);
            var eastState = db.GetState(East1Id);
            Assert.That(eastState.flagRaised, Is.True, "점령 → flagRaised = true");
            Assert.That(TerritoryCaptureSystem.Flags.ContainsKey(East1Id), Is.True, "깃발 데이터 등록");
            Assert.That(TerritoryCaptureSystem.Flags[East1Id].flagColor, Is.EqualTo(playerColor),
                "플레이어 점령 깃발 = 문장색(폴백 East 파랑)");
            var border = TerritoryCaptureSystem.GetBorderColor(East1Id);
            Assert.That(border.a, Is.EqualTo(TerritoryCaptureSystem.BORDER_ALPHA).Within(0.001f),
                "경계색 알파 = 0.6");
            Assert.That(border.r, Is.EqualTo(playerColor.r).Within(0.001f), "경계색 = 점령색");

            // 상실(배신 경로) — 플레이어 깃발 → 국가색 회수 + flagRaised 해제
            TerritoryCaptureSystem.OnTerritoryCaptured(West1Id, NationType.West, isPlayerCapture: true);
            TerritoryCaptureSystem.OnTerritoryLost(West1Id);
            var westState = db.GetState(West1Id);
            Assert.That(westState.flagRaised, Is.False, "상실 → flagRaised = false");
            Assert.That(TerritoryCaptureSystem.Flags[West1Id].flagColor, Is.EqualTo(Color.green),
                "상실 후 깃발 = West 국가색(초록) 회수");
        }

        // ── 헬퍼 (MercenaryContractDueTests 선례) ──────────────────────────

        /// <summary>목표 게임일로 이동 — GameTime 누적 세터(일차 증분)를 정확 산술로 사용.</summary>
        private static void SetDay(TimeManager tm, int targetDay)
        {
            tm.GameTime = Day * (targetDay - tm.CurrentDay);
            Assert.That(tm.CurrentDay, Is.EqualTo(targetDay), "게임일 이동 실패 — 정밀도/산술 오류");
        }

        /// <summary>TimeManager.OnDayStart 발화 — 이벤트는 외부 Invoke 불가 → 백킹 필드 리플렉션.</summary>
        private static void FireOnDayStart(TimeManager tm)
        {
            var field = typeof(TimeManager).GetField("OnDayStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "TimeManager.OnDayStart 백킹 필드");
            (field.GetValue(tm) as System.Action)?.Invoke();
        }

        private static System.Delegate GetGoldListeners()
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            return (System.Delegate)field.GetValue(null);
        }

        private static void SetGoldListeners(System.Delegate value)
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            field.SetValue(null, value);
        }

        private static System.Delegate FilterLiveListeners(System.Delegate chain)
        {
            if (chain == null) return null;
            System.Delegate live = null;
            foreach (var d in chain.GetInvocationList())
            {
                if (d.Target is Object uo && uo == null) continue; // 파괴된 Unity 대상 제거
                live = System.Delegate.Combine(live, d);
            }
            return live;
        }

        private static void SetStaticInstance<T>(T value) where T : class
        {
            var field = typeof(T).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, typeof(T).Name + ".Instance 백킹 필드");
            field.SetValue(null, value);
        }

        /// <summary>편집 모드 잔여 루트 오브젝트 정리 — Object.Destroy 지연분 DestroyImmediate 스윕.</summary>
        private static void SweepRootObjects(params string[] prefixes)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var prefix in prefixes)
                {
                    if (!string.IsNullOrEmpty(prefix) &&
                        root.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        Object.DestroyImmediate(root);
                        break;
                    }
                }
            }
        }
    }
}
