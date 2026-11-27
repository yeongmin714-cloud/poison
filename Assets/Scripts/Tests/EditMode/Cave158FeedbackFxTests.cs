using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.UI.Toolkit;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [158차 P5] 신규 기능 계약테스트 — CodeOnly(소스 판독 판정) + 결정론 API 직접 판정.
    ///  · P1-A: PlayerNotify 버스(Notify/Broadcast/ResetSubscriptionsForTest) + GemChest 게이트 피드백 연결.
    ///  · P2-B: NPCDialoguePanelUTK PlayerNotify 구독(ShowTransientMessage 라우팅) + 3개 시스템 발화 연결.
    ///  · P3-C: 지면 경로 돌판 도장(BFS) + 자갈 스탬프 + 벽 받침 밴드(CaveInteriorBuilder/CaveMaterialFactory).
    ///  · P4-D: 별빛 4점 텍스처(사각 금지) + 지면 링 파동 2겹(지연 스태거) + 코어 펄스(과노출 완화) + 등급 차등.
    /// 근거: docs/plans/2026-11-06_158-chest-interact-dialogue-quality-fx-plan.md
    /// </summary>
    public class Cave158FeedbackFxTests
    {
        private const string CinemaPath = "Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs";
        private const string NotifyPath = "Assets/Scripts/Systems/PlayerNotify.cs";
        private const string PanelPath = "Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs";
        private const string ChestPath = "Assets/Scripts/Systems/GemChest.cs";
        private const string FishingPath = "Assets/Scripts/Systems/FishingSystem.cs";
        private const string MercPath = "Assets/Scripts/Systems/MercenaryManager.cs";
        private const string BuilderPath = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";
        private const string FactoryPath = "Assets/Scripts/Systems/CaveMaterialFactory.cs";

        private static string Src(string path) => File.ReadAllText(path);

        // ── P1-A: PlayerNotify 버스 ──

        [Test]
        public void P1_PlayerNotify_Bus_HasNotifyBroadcastAndReset()
        {
            string src = Src(NotifyPath);
            Assert.That(src, Does.Contain("static event Action<string> Broadcast"),
                "[158차 P1] PlayerNotify.Broadcast 정적 이벤트 — Systems→UI 단방향 계약(FarmStatusBroadcast 선례).");
            Assert.That(src, Does.Contain("public static void Notify(string message)"),
                "[158차 P1] Notify 진입점 — Systems 계층 발화 유일 경로.");
            Assert.That(src, Does.Contain("public static void ResetSubscriptionsForTest()"),
                "[158차 P1] ResetSubscriptionsForTest — 테스트 전용 구독 정리(프로덕션 무영향).");
            Assert.That(src, Does.Not.Contain("Random."),
                "[158차 P1] 결정론 계약 — Random API 금지.");
        }

        [Test]
        public void P1_GemChest_GateBlock_RoutesToPlayerNotify()
        {
            string src = Src(ChestPath);
            Assert.That(src, Does.Contain("PlayerNotify.Notify(msg)"),
                "[158차 P1] GemChest 게이트 차단 시 PlayerNotify 발화 — 콘솔 전용에서 화면 안내로(사용자 체감 수리).");
            Assert.That(src, Does.Contain("Debug.LogWarning(\"[GemChest] \" + msg)"),
                "[158차 P1] 콘솔 경고 병행 유지(진단 계약) — 화면 전용으로 전환 금지.");
        }

        // ── P2-B: 대화 패널 구독 + 시스템 발화 ──

        [Test]
        public void P2_DialoguePanel_SubscribesPlayerNotify()
        {
            string src = Src(PanelPath);
            Assert.That(src, Does.Contain("PlayerNotify.Broadcast += ShowTransientMessage"),
                "[158차 P2] PlayerNotify 구독 — ShowTransientMessage 라우팅(멱등 가드, FarmStatusBroadcast 선례 준용).");
            Assert.That(src, Does.Contain("private static bool _playerNotifySubscribed"),
                "[158차 P2] 멱등 가드 — 정적 버스 구독 1회(해제 불요, FarmStatusBroadcast 선례).");
            Assert.That(src, Does.Contain("public static void ShowTransientMessage(string message)"),
                "[158차 P2] 라우팅 — ShowTransientMessage(3.5s 자동 닫힘) 단일 경로.");
        }

        [Test]
        public void P2_FishingSystem_WaterRefill_EmitsNotify()
        {
            string src = Src(FishingPath);
            Assert.That(src, Does.Contain("PlayerNotify.Notify(\"물뿌리개에 물을 담으시오. (R키)\")"),
                "[158차 P2] 물가 진입 사전 힌트(사용자 예시 문안) — 물가 이탈 시 리셋.");
            Assert.That(src, Does.Contain("PlayerNotify.Notify(\"물뿌리개에 물을 가득 담았다.\")"),
                "[158차 P2] R키 물담기 완료 안내.");
        }

        [Test]
        public void P2_Systems_Notify_MessageContracts()
        {
            string merc = Src(MercPath);
            Assert.That(merc, Does.Contain("PlayerNotify.Notify(msg)"),
                "[158차 P2] 용병 고용 차단 — 게이트 안내 화면 표출.");
            string fishing = Src(FishingPath);
            Assert.That(fishing, Does.Not.Contain("PlayerNotify.Notify(null)"),
                "[158차 P2] null 발화 금지 — Notify 계약(비어 있으면 무시).");
        }

        // ── P3-C: 바닥 경로/자갈/벽 받침 ──

        [Test]
        public void P3_Builder_BfsPathAndGravelDecor_Exist()
        {
            string src = Src(BuilderPath);
            Assert.That(src, Does.Contain("ComputeStonePathCells("),
                "[158차 P3-C] BFS 지면 경로 셀 집합 — S(스폰)→D/U(포탈) 결정론 경로.");
            Assert.That(src, Does.Contain("CreateStonePath(room)"),
                "[158차 P3-C] 경로 돌판 도장(빌드 흐름 호출) — 흙 단일소스 무손상(동굴 전용 파생 레이어).");
            Assert.That(src, Does.Contain("CreateDirtDetail(room)"),
                "[158차 P3-C] 비경로 흙셀 자갈 스탬프 — 해시 결정론.");
            Assert.That(src, Does.Contain("CreateWallBaseBand(room, run, length, i)"),
                "[158차 P3-C] 벽 받침 밴드 — 벽 런 하단 짙은돌선(높이 무수정 계약).");
            Assert.That(src, Does.Not.Contain("Random.Range"),
                "[158차 P3-C] 결정론 계약 — Random API 금지(해시/LCG만).");
        }

        [Test]
        public void P3_Factory_StonePathAndGravelMaterials_Exist()
        {
            string src = Src(FactoryPath);
            Assert.That(src, Does.Contain("CreateStonePathMat()"),
                "[158차 P3-C] 돌판 경로 머티리얼 — 절차 텍스처(에셋 불요).");
            Assert.That(src, Does.Contain("CreateGravelMat()"),
                "[158차 P3-C] 자갈 머티리얼 — 절차 텍스처.");
            Assert.That(src, Does.Contain("CreateBaseBandMat(float length)"),
                "[158차 P3-C] 벽 받침 밴드 머티리얼.");
        }

        // ── P4-D: 획득 이펙트 고급화 ──

        [Test]
        public void P4_StarTexture_ProceduralFourPoint_NoSquare()
        {
            string src = Src(CinemaPath);
            Assert.That(src, Does.Contain("private static Texture2D GetStarTexture()"),
                "[158차 P4-D] 별빛 4점 교차 절차 텍스처 — 사각 금지 계약(계획 §P4-D).");
            Assert.That(src, Does.Contain("mat.mainTexture = GetStarTexture();"),
                "[158차 P4-D] 스파클 파티클 텍스처 별빛 교체 — radial 소프트닛 대체.");
            Assert.That(src, Does.Contain("Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(nx)), 14f)"),
                "[158차 P4-D] x축 스파이크 좁은 감쇠 — 4점 별 수학(결정론, Random 불요).");
        }

        [Test]
        public void P4_GroundRing_TierContract()
        {
            // 결정론 API 직접 판정 — Common 1겹, Rare+ 2겹
            Assert.AreEqual(1, AcquisitionCinema.GroundRingCount(0), "Common(0) — 링 1겹(계획 §P4-D).");
            Assert.AreEqual(2, AcquisitionCinema.GroundRingCount(1), "Rare(1+) — 링 2겹.");
            Assert.AreEqual(2, AcquisitionCinema.GroundRingCount(5), "Unique(5) — 링 2겹(상한 유지).");

            // 끝 반경 — 티어당 +0.12m 단조 증가
            Assert.AreEqual(1.6f, AcquisitionCinema.GroundRingEndRadius(0), 0.0001f, "Common — 1.6m 기본.");
            Assert.AreEqual(1.6f + 0.12f, AcquisitionCinema.GroundRingEndRadius(1), 0.0001f, "Rare — +0.12.");
            Assert.AreEqual(1.6f + 0.12f * 5f, AcquisitionCinema.GroundRingEndRadius(5), 0.0001f, "Unique — 최대.");

            // 범위 밖 클램프 — 표 밖 값 반환 금지(RarityShaftLength 선례)
            Assert.AreEqual(AcquisitionCinema.GroundRingEndRadius(0), AcquisitionCinema.GroundRingEndRadius(-3), 0.0001f,
                "범위 밖(음수) — 클램프 0.");
            Assert.AreEqual(AcquisitionCinema.GroundRingEndRadius(5), AcquisitionCinema.GroundRingEndRadius(9), 0.0001f,
                "범위 밖(초과) — 클램프 5.");
        }

        [Test]
        public void P4_CorePulse_AmplitudeAndEpicBoost()
        {
            // 코어 펄스 — 계획 §P4-D 확정: 1.6↔2.4(주기 0.8s), 에픽+(3+) 진폭 ↑
            Assert.AreEqual(0.4f, AcquisitionCinema.CorePulseAmplitude(0), 0.0001f, "Common — 진폭 ±0.4(중심 2.0 → 1.6~2.4).");
            Assert.AreEqual(0.4f, AcquisitionCinema.CorePulseAmplitude(2), 0.0001f, "Rare 미만 — 진폭 유지.");
            Assert.AreEqual(0.5f, AcquisitionCinema.CorePulseAmplitude(3), 0.0001f, "에픽+(3+) — 진폭 +0.1.");
            Assert.AreEqual(0.5f, AcquisitionCinema.CorePulseAmplitude(5), 0.0001f, "Unique — 진폭 상한.");

            // 에픽 스파클 강화 — 표 무손상 연장(154차 SparkleRateTable/SizeMulTable 유지)
            Assert.AreEqual(1.2f, AcquisitionCinema.SparkleEpicBoostSizeMul, 0.0001f, "에픽+ 크기 ×1.2.");
            Assert.AreEqual(6, AcquisitionCinema.SparkleEpicBoostBurst, "에픽+ 추가 버스트 +6.");

            // 소스 계약 — 펄스 루프 존재 + 기존 정적 2.0 상수 소멸
            string src = Src(CinemaPath);
            Assert.That(src, Does.Contain("pm.SetFloat(\"_CoreIntensity\", pulse)"),
                "[158차 P4-D] AnimateFloat 펄스 적용 — 정적 강도 대체.");
            Assert.That(src, Does.Contain("FxCorePulseMid"),
                "[158차 P4-D] 펄스 중심 상수(2.0) — 계획 §P4-D.");
            Assert.That(src, Does.Not.Contain("SetFloat(\"_CoreIntensity\", 2.0f)"),
                "[158차 P4-D] 정적 강도 소멸 — 과노출 완화(펄스로만 구동).");
        }

        [Test]
        public void P4_GroundRing_SpawnAndStagger_Contract()
        {
            string src = Src(CinemaPath);
            Assert.That(src, Does.Contain("private static void SpawnGroundRings(GameObject player)"),
                "[158차 P4-D] 지면 링 스폰 — player 자식 루트(발화 지점 수평 확산).");
            Assert.That(src, Does.Contain("SpawnGroundRings(player);"),
                "[158차 P4-D] StartPresentation 발화 연결 — 연출 시작과 동기.");
            Assert.That(src, Does.Contain("AnimateGroundRing(_groundRingA, elapsed, 0f)"),
                "[158차 P4-D] 제1파동 즉시 발화.");
            Assert.That(src, Does.Contain("AnimateGroundRing(_groundRingB, elapsed, FxGroundRingDelayS)"),
                "[158차 P4-D] 제2파동 지연 스태거 — 2중 파동(계획 §P4-D).");
            Assert.That(src, Does.Contain("Object.Destroy(_groundRingRoot);"),
                "[158차 P4-D] End 명시 파괴(누수 금지) — cheer와 동일 소유권 계약.");
        }

        [Test]
        public void P4_Test_21_Level_Boost_Exists()
        {
            string setup = Src("Assets/Scripts/Systems/CaveInteriorTestSetup.cs");
            Assert.That(setup, Does.Contain("AddEXP("),
                "[158차 P1] Test_21 한정 Lv10 부스팅 — ContentGate gem_chest(Lv10) 실동작 판정 가능.");
        }
    }
}
