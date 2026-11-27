using System.IO;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [90차 B3 — 공격조 UI 건설 진입 버튼] 소스 문자열 어서션 테스트(AttackFormationUTK.cs).
    ///
    /// 사용자 요구 4건(90차) — 병사 배치 UI(공격조 전투 배치 창)에 '건물짓기'/'음식점 배치' 항목 추가.
    /// Figma 204:1963 기존 구현 보존 계약: 계획 도구 행(PlanToolsRow) 우측 — 구분선 1×22 #30363D/0.6 후
    /// UTKButton.Secondary(높이 32, 글자 13/500) 버튼 2개.
    ///  · '건물짓기' → ConstructionWindowUTK.Open()
    ///  · '음식점 배치' → ConstructionWindowUTK.Open() 후 SelectBlueprint("bp_restaurant") 음식점 자동 선택
    ///  · 기존 명부/배치도 로직 무변경 — 버튼 추가만(계획 도구 행 tools.Add 후순위 삽입)
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class AttackFormationButtonTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Toolkit/AttackFormationUTK.cs";

        private static string FormationSrc() => File.ReadAllText(SourcePath);

        [Test]
        public void PlanToolsRow_Source_TwoConstructionEntryButtons()
        {
            string src = FormationSrc();

            Assert.That(src, Does.Contain("[90차 B2]"), "[90차 B2] 마커 주석 — 버튼 추가 위치 식별.");
            Assert.That(src, Does.Contain("건물짓기"), "버튼 1 문구 — '건물짓기'.");
            Assert.That(src, Does.Contain("음식점 배치"), "버튼 2 문구 — '음식점 배치'.");
            Assert.That(src, Does.Contain("PlanToolsBuildButton"), "버튼 1 name — PlanToolsBuildButton.");
            Assert.That(src, Does.Contain("PlanToolsRestaurantButton"), "버튼 2 name — PlanToolsRestaurantButton.");
        }

        [Test]
        public void PlanToolsRow_Source_OpenAndSelectBlueprintWiring()
        {
            string src = FormationSrc();

            Assert.That(src, Does.Contain("() => ConstructionWindowUTK.Open()"),
                "'건물짓기' → ConstructionWindowUTK.Open() 직접 호출(동일 UI 어셈블리).");
            Assert.That(src, Does.Contain("ConstructionWindowUTK.Open();"),
                "'음식점 배치' → 건설 관리 창 개방.");
            Assert.That(src, Does.Contain("ConstructionWindowUTK.SelectBlueprint(\"bp_restaurant\")"),
                "'음식점 배치' → 음식점 자동 선택(선택 시설 = bp_restaurant 우측 패널 갱신).");
        }

        [Test]
        public void PlanToolsRow_Source_DividerAndSecondaryStyle()
        {
            string src = FormationSrc();

            Assert.That(src, Does.Contain("PlanToolsConstructionDivider"),
                "구분선 — 버튼 2개 앞 1×22 #30363D/0.6.");
            Assert.That(src, Does.Contain("0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.6f"),
                "구분선 색 #30363D/0.6.");
            Assert.That(src, Does.Contain("UTKButton.Variant.Secondary"),
                "버튼 2개 모두 UTKButton.Secondary.");
            Assert.That(src, Does.Contain("buildButton.style.height = 32f"),
                "버튼 높이 32.");
            Assert.That(src, Does.Contain("restaurantButton.style.height = 32f"),
                "버튼 높이 32(음식점 배치).");
            Assert.That(src, Does.Contain("buildButton.style.fontSize = 13f"),
                "버튼 글자 13/500 — FontStyle.Medium 부재 → Normal 규약.");
        }

        [Test]
        public void PlanToolsRow_Source_InsertedAfterResetChip_ExistingLogicUntouched()
        {
            string src = FormationSrc();

            // 삽입 순서 계약 — 기존 '배치 초기화' 칩 이후(계획 도구 행 우측), 적 방향 스트립 이전.
            int resetChip = src.IndexOf("tools.Add(resetChip);");
            int divider = src.IndexOf("tools.Add(constructionDivider);");
            int buildBtn = src.IndexOf("tools.Add(buildButton);");
            int restaurantBtn = src.IndexOf("tools.Add(restaurantButton);");
            int enemyStrip = src.IndexOf("EnemyDirectionStrip");

            Assert.That(resetChip, Is.GreaterThan(0), "기존 '배치 초기화' 칩 유지.");
            Assert.That(divider, Is.GreaterThan(resetChip), "구분선은 초기화 칩 이후(행 우측).");
            Assert.That(buildBtn, Is.GreaterThan(divider), "'건물짓기'는 구분선 이후.");
            Assert.That(restaurantBtn, Is.GreaterThan(buildBtn), "'음식점 배치'는 '건물짓기' 이후.");
            Assert.That(enemyStrip, Is.GreaterThan(restaurantBtn),
                "적 방향 스트립은 버튼 추가 블록 이후 — 기존 배치도 로직 순서 무변경.");

            // 기존 로직 보존 가드 — 명부/배치도 핵심 요소 잔존
            Assert.That(src, Does.Contain("BuildRosterPanel"), "기존 명부 패널 유지.");
            Assert.That(src, Does.Contain("BuildPlanPanel"), "기존 배치 계획 패널 유지.");
            Assert.That(src, Does.Contain("ApplyFormationNow"), "기존 '전투 배치 적용' 유지.");
            Assert.That(src, Does.Contain("AttackFormationSystem.ApplyFormation"), "기존 편성 적용 유지.");
        }
    }
}
