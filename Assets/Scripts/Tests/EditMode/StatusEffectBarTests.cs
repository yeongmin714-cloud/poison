using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [63-B] 상태이상 표시 바 계약 (소스 어서션 — SleepPanelFigmaTests 선례).
    /// 계획서: docs/2026-11-16_STATUS_EFFECTS_14_PLAN.md §3+§5-2.
    /// ①시계 하단 배치 ②동그란 아크(HP/스태미너 동일 계열) ③여백 고정 ④호버 남은 시간
    /// ⑤벡터메시 금지(84차) ⑥시스템 단일 소스 소비.
    /// </summary>
    public class StatusEffectBarTests
    {
        private static string Bar() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/StatusEffectBarUTK.cs");
        private static string Wire() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKWireUp.cs");
        private static string Sys() => System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerStatusEffectSystem.cs");

        [Test]
        public void Bar_Class_Exists_With_IdempotentEnsure()
        {
            var src = Bar();
            Assert.That(src, Does.Contain("class StatusEffectBarUTK"), "바 클래스 존재.");
            Assert.That(src, Does.Contain("public static void Ensure()"), "멱등 Ensure — UTKWireUp/자가부트 경로.");
            Assert.That(src, Does.Contain("RuntimeInitializeOnLoadMethod"), "자가부트(AfterSceneLoad).");
        }

        [Test]
        public void Bar_Positioned_Below_TimeClock_SameColumn()
        {
            var src = Bar();
            // 시계(TimeClockGlassUTK) left=14과 동일 열 — 좌상단 정렬 계약.
            Assert.That(src, Does.Contain("BarLeft = 14f"), "시계와 동일 열(left=14).");
            // 시계 하단(top 12 + GlyphHeight46+30 = 88) + 8px 간격 = 96.
            Assert.That(src, Does.Contain("BarTop = 96f"), "시계 하단 + 8px 간격.");
        }

        [Test]
        public void Bar_UsesStripArc_SameFamilyAsHpStaminaGauges()
        {
            var src = Bar();
            // 체력바/스태미너 바와 동일 계열 — UTKGaugeStripArc(HP/스태미너/패망 아크 동일 소스, 84차 벡터메시 금지).
            Assert.That(src, Does.Contain("UTKGaugeStripArc"), "원형 아크 = HP/스태미너 동일 계열.");
            // 남은 시간 비율 → Fraction(시계방향 소모는 스트립 내장).
            Assert.That(src, Does.Contain("Fraction"), "아크 = 남은 시간 비율.");
            Assert.That(src, Does.Contain("GetRemainingSeconds"), "시스템 실측 — 남은 초.");
            Assert.That(src, Does.Contain("GetDurationSeconds"), "시스템 실측 — 전체 지속(분모).");
        }

        [Test]
        public void Bar_FixedGap_And_HoverRemaining()
        {
            var src = Bar();
            // 여백 고정 — 개수 무관 일정 간격(사용자 계약).
            Assert.That(src, Does.Contain("IconGap = 6f"), "아이콘 간 여백 6px 고정.");
            // 호버 — 커서 대면 남은 시간 표시(사용자 계약).
            Assert.That(src, Does.Contain("MouseEnterEvent"), "호버 진입 이벤트.");
            Assert.That(src, Does.Contain("MouseLeaveEvent"), "호버 이탈 이벤트.");
            Assert.That(src, Does.Contain("초\""), "남은 시간 초 단위 라벨.");
        }

        [Test]
        public void Bar_NoVectorMesh_84Contract()
        {
            var src = Bar();
            // 벡터메시 금지(84차 계약) — generateVisualContent/메시 경로 부재, 절차 조합만.
            Assert.That(src, Does.Not.Contain("Mesh"), "벡터메시 경로 부재(84차).");
            Assert.That(src, Does.Not.Contain("generateVisualContent"), "메시 재생성 경로 부재.");
        }

        [Test]
        public void Bar_CoversAll14Effects()
        {
            var src = Bar();
            // 14종 전부 절차 글리프 빌더 보유 — 모두 채택 계약.
            Assert.That(src, Does.Contain("BuildBleedingGlyph"));
            Assert.That(src, Does.Contain("BuildPoisoningGlyph"));
            Assert.That(src, Does.Contain("BuildSlownessGlyph"));
            Assert.That(src, Does.Contain("BuildExhaustionGlyph"));
            Assert.That(src, Does.Contain("BuildFractureGlyph"));
            Assert.That(src, Does.Contain("BuildDazeGlyph"));
            Assert.That(src, Does.Contain("BuildHungerGlyph"));
            Assert.That(src, Does.Contain("BuildBurnGlyph"));
            Assert.That(src, Does.Contain("BuildHypothermiaGlyph"));
            Assert.That(src, Does.Contain("BuildTerrorGlyph"));
            Assert.That(src, Does.Contain("BuildInfectionGlyph"));
            Assert.That(src, Does.Contain("BuildWeakenGlyph"));
            Assert.That(src, Does.Contain("BuildDrunkennessGlyph"));
            Assert.That(src, Does.Contain("BuildBrittleDefenseGlyph"));
        }

        [Test]
        public void System_PinnedApi_ConsumedByBar()
        {
            var sys = Sys();
            var bar = Bar();
            // 교차 시그니처(계획서 §1) — 시스템이 핀한 API를 바가 소비.
            Assert.That(sys, Does.Contain("public static IReadOnlyList<StatusEffectId> GetActiveEffects()"), "활성 목록 API 핀.");
            Assert.That(sys, Does.Contain("public static event Action OnChanged"), "변경 이벤트 핀.");
            Assert.That(bar, Does.Contain("PlayerStatusEffectSystem.GetActiveEffects()"), "바 → 시스템 소비(단일 소스).");
            Assert.That(bar, Does.Contain("PlayerStatusEffectSystem.OnChanged"), "바 → 변경 이벤트 구독.");
        }

        [Test]
        public void Wire_BootsBar()
        {
            var src = Wire();
            // UTKWireUp 배선 1줄 — 자가부트 보장(멱등).
            Assert.That(src, Does.Contain("StatusEffectBarUTK.Ensure()"), "Wire 경유 자가부트 보장.");
        }

        // [63-C] 툴팁 2행 — 1행 남은 시간 + 2행 해결 방법(CureHint) 계약.
        [Test]
        public void Bar_Tooltip_TwoLines_WithCureHints()
        {
            var src = Bar();
            Assert.That(src, Does.Contain("CureHint"), "2행 해결 방법 메서드 존재.");
            Assert.That(src, Does.Contain("초\\n{CureHint(id)}"), "툴팁 2행 — 1행 초 + 2행 CureHint(소스 리터럴 백슬래시n 검색).");
            Assert.That(src, Does.Contain("WhiteSpace.Normal"), "2행 줄바꿈 렌더 — Normal.");
            Assert.That(src, Does.Not.Contain("WhiteSpace.NoWrap"), "NoWrap 부재 — 2행 계약.");
            Assert.That(src, Does.Contain("붕대"), "출혈/감염/골절 — 붕대.");
            Assert.That(src, Does.Contain("해독제"), "중독 — 해독제.");
            Assert.That(src, Does.Contain("수면"), "탈진/골절/저체온/감염 — 수면.");
            Assert.That(src, Does.Contain("식사"), "허기 — 식사(갱신형, 시간 경과 미해제).");
            Assert.That(src, Does.Contain("부목"), "골절 — 부목(붕대).");
            Assert.That(src, Does.Contain("물 접촉"), "화상 — 물 접촉(수영).");
            Assert.That(src, Does.Contain("진정제"), "공포 — 진정제.");
            Assert.That(src, Does.Contain("마스크"), "혼탁 — 마스크 착용 시 면역.");
        }
    }
}
