// [124차 Phase A] 월드맵 Figma 정합 계약 테스트 — 소스 텍스트 계약(계획서 진실치) 검증.
// 목적: WorldMapWindowUTK의 124차 Impl-A 구현(배경 2레이어/HUDHeader/폐기/좌측 QuestListPanel)이
//       docs/124_WORLD_MAP_FIGMA_PARITY_PLAN.md 진실치를 소스 레벨로 유지하는지 단언한다.
//       (런타임 렌더/Play 판정은 QA 서브 몫 — EditMode 실행은 본 파일의 소스 텍스트 계약만)
// 패턴 소스: WorldMapSceneWiringTests.cs (ReadSource 헬퍼 동일).
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class WorldMapFigma124Tests
    {
        private const string WorldMapSourcePath = "Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        [Test]
        public void Figma124_Background_TwoLayers_ResourcePaths()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("\"UI/WorldMap/WorldMapBackground\"", src);
            StringAssert.Contains("\"UI/WorldMap/ParchmentTextureBackground\"", src);
            StringAssert.Contains("Resources.Load<Texture2D>", src);
            // 3레이어 명칭 — 배경1/오버레이/배경2 (Figma 95:168 + SOLID 오버레이 + 97:5)
            StringAssert.Contains("MapBackgroundLayer1", src);
            StringAssert.Contains("MapBackgroundOverlay", src);
            StringAssert.Contains("MapBackgroundLayer2", src);
        }

        [Test]
        public void Figma124_Background_OverlayColor_0B0E14_Alpha045()
        {
            string src = ReadSource(WorldMapSourcePath);
            // 계획서 진실치: SOLID #0B0E14/0.45 — 배경1과 배경2 사이
            StringAssert.Contains("0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 0.45f", src);
        }

        [Test]
        public void Figma124_Background_ExplicitSize_1920x1080()
        {
            string src = ReadSource(WorldMapSourcePath);
            // scale 모드 none/1:1 — PNG 실측 1920×1080 explicit (authored 좌표계 유지)
            StringAssert.Contains("new Length(1920f), new Length(1080f)", src);
        }

        [Test]
        public void Figma124_HudHeader_WorldMapSystem_Group()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("\"세계 지도\"", src);
            StringAssert.Contains("\"WORLD MAP SYSTEM\"", src);
            StringAssert.Contains("name = \"HudHeader\"", src);
            StringAssert.Contains("name = \"SystemGroup\"", src);
            StringAssert.Contains("name = \"CoordinatesGroup\"", src);
        }

        [Test]
        public void Figma124_HudHeader_Coordinates_GeistMono_F2_Live()
        {
            string src = ReadSource(WorldMapSourcePath);
            // Geist Mono 패밀리 로드 경로(에셋 부재 시 UIFont 폴백 — 위조 금지 주석 유지)
            StringAssert.Contains("Fonts/GeistMono", src);
            // 좌표값 F2 실시간 갱신 경로(UpdateHeaderAndCompass → _hudCoordValueLabel)
            StringAssert.Contains("$\"X: {p.x:F2} / Y: {p.z:F2}\"", src);
            StringAssert.Contains("_hudTerritoryNameLabel.text = loc;", src);
        }

        [Test]
        public void Figma124_QuestTab_Placeholder3Cards_RawTexts()
        {
            string src = ReadSource(WorldMapSourcePath);
            // 계획서 진실치 — 카드 3장 원문 전부
            StringAssert.Contains("\"고원 레이더 방해 해제\"", src);
            StringAssert.Contains("\"잊혀진 혹성 고원에서 송출되는 레이더 방해 전파를 차단하고 관측망을 복구\"", src);
            StringAssert.Contains("\"차원 문 요새 작전\"", src);
            StringAssert.Contains("\"부서진 차원의 문 요새에 침투해 크로노스 연료 장치를 회수하고 작전지 지휘소를 무력화하라.\"", src);   // [144차] Figma 전문 복원 계약
            StringAssert.Contains("\"공업지구 보급 지원\"", src);
            StringAssert.Contains("\"엘리시온 정착 공업지구로 중수소 운송선과 보급 물자를 안전하게 인도하라.\"", src);
            // Lv 배지 원문 3종
            StringAssert.Contains("\"Lv.35 - 45\"", src);
            StringAssert.Contains("\"Lv.45\"", src);
            StringAssert.Contains("\"Lv.25 - 30\"", src);
            // 카테고리 pill 원문 3종
            StringAssert.Contains("\"현재 진행\"", src);
            StringAssert.Contains("\"메인 임무\"", src);
            StringAssert.Contains("\"보조 임무\"", src);
        }

        [Test]
        public void Figma124_TierStrip_OwnershipContractColors()
        {
            string src = ReadSource(WorldMapSourcePath);
            // 소유 상태 계약색 — 플레이어=#39D353/분쟁=#FF9E2C 신설 상수 + 영주=기존 빨강(ColorWar) 재사용
            StringAssert.Contains("Hex(0x39D353)", src);
            StringAssert.Contains("Hex(0xFF9E2C)", src);
            StringAssert.Contains("case TerritoryOwnership.LordOwned: return ColorWar;", src);
            StringAssert.Contains("OwnershipContractColor", src);
        }

        [Test]
        public void Figma124_FilterTabs_ClickSwitch()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("text = \"퀘스트\"", src);
            StringAssert.Contains("text = \"지역\"", src);
            StringAssert.Contains("OnQuestTabClicked", src);
            StringAssert.Contains("OnRegionTabClicked", src);
            StringAssert.Contains("ApplyFilterTabStyles", src);   // 활성/비활성 스타일 스왑
        }

        [Test]
        public void Figma124_SelectTerritory_Signature_And_ImplBMarker()
        {
            string src = ReadSource(WorldMapSourcePath);
            // 공용 시그니처 — [124차-ImplB]에서 배선 완료(구 TODO 스텁 계약은 완성 상태 계약으로 갱신):
            //   ①핀/지역 행 클릭 → SelectTerritory 진입 ②선택 저장(_hasSelection 플래그 — TerritoryDefinition은 struct)
            //   ③선택 핀 강조(SetSelected(true)) ④우측 패널 즉시 갱신(RefreshQuestDetailPanel)
            StringAssert.Contains("public void SelectTerritory(TerritoryDefinition def)", src);
            StringAssert.Contains("_hasSelection = true;", src);
            StringAssert.Contains("_selectedMarker.SetSelected(true);", src);
            StringAssert.Contains("RefreshQuestDetailPanel();", src);
            // 지역 탭 행 클릭 진입점 배선
            StringAssert.Contains("evt => SelectTerritory(def)", src);
            // 지역 행 실데이터 — TerritoryDatabase 전수 + 소유색/영주명 계약 재사용
            StringAssert.Contains("RegionRowView", src);
            StringAssert.Contains("WorldMapTerritoryTooltipContent.OwnershipStateLine", src);
            StringAssert.Contains("WorldMapTerritoryTooltipContent.OwnerLordLine", src);
        }

        [Test]
        public void Figma124_Disposal_LegendStrip_LocationStrip_TerrainBakeCalls()
        {
            string src = ReadSource(WorldMapSourcePath);
            var lines = src.Split('\n').Select(l => l.TrimStart()).ToArray();

            // 범례 스트립 소멸 — 구 L134~163 블록(utk-title-label 클래스 포함) 전수 부재
            Assert.IsFalse(lines.Any(l => l.StartsWith("var legend")),
                "[124차] 범례 스트립이 부활했다 — 폐기 계약 위반");
            StringAssert.DoesNotContain("utk-title-label", src,
                "[124차] 범례 스트립 클래스 잔재 — 폐기 계약 위반");

            // 위치줄 글래스 스트립 소멸 — hudRow/_locLabel/_coordLabel 코드 부재(구 위치 데이터 경로는 HUDHeader로 승계)
            Assert.IsFalse(lines.Any(l => l.StartsWith("var hudRow")),
                "[124차] 위치줄 글래스 스트립이 부활했다 — 폐기 계약 위반");
            Assert.IsFalse(lines.Any(l => l.StartsWith("_locLabel = new Label")),
                "[124차] _locLabel 할당 잔재 — 폐기 계약 위반");
            Assert.IsFalse(lines.Any(l => l.StartsWith("_coordLabel = new Label")),
                "[124차] _coordLabel 할당 잔재 — 폐기 계약 위반");

            // 지형 베이크 호출부 소멸 — 실제 호출 라인(TrimStart 기준 호출문) 부재, 메서드 정의/주석 폐기 표기만 허용
            Assert.IsFalse(lines.Any(l => l.StartsWith("CreateTerrainOverlay();")),
                "[124차] CreateTerrainOverlay() 호출이 부활했다 — 폐기 계약 위반(메서드 잔존, 호출부만 폐기)");
            Assert.IsFalse(lines.Any(l => l.StartsWith("RefreshTerrainOverlay();")),
                "[124차] RefreshTerrainOverlay() 호출이 부활했다 — 폐기 계약 위반(메서드 잔존, 호출부만 폐기)");
            // 구 양피지 이미지 배경 소멸 — GetParchment 절차 텍스처 호출부 부재(Figma 아트 2레이어 대체)
            Assert.IsFalse(lines.Any(l => l.StartsWith("_mapCanvas.style.backgroundImage = UTKTextureSafe")),
                "[124차] 양피지 절차 배경 호출이 부활했다 — Figma 아트 2레이어 배경 계약 위반");
        }

        // ===== [124차-ImplB] 핀 신형/선택/우측 패널 계약 (docs/124_WORLD_MAP_FIGMA_PARITY_PLAN.md) =====

        [Test]
        public void Figma124_ImplB_PinStructure_TruthValues()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("SetCornerRadius(_base, 19.2f)", src);          // PinBase r19.2 (38×38)
            StringAssert.Contains("name = \"PinBase\"", src);
            StringAssert.Contains("name = \"PinDot\"", src);
            StringAssert.Contains("name = \"PinRing\"", src);                     // 플레이어 헤일로 53×53
            StringAssert.Contains("name = \"PinLabel\"", src);
            StringAssert.Contains("SetCornerRadius(labelChip, 4.8f)", src);           // 라벨 칩 r4.8 [144차] 칩 2행화(핀 Lv/난이도 라벨) — 크롬이 labelChip으로 이동
            StringAssert.Contains("0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f", src);   // #161B22/0.80
            StringAssert.Contains("new Length(-19f, LengthUnit.Pixel)", src);     // 앵커=PinBase 중심
        }

        [Test]
        public void Figma124_ImplB_Selection_WiredFromPinAndRegionRow()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("evt => SelectTerritory(def)", src);            // 핀 클릭 + 지역 행 클릭 공용
            StringAssert.Contains("public void SetSelected(bool selected)", src);
            StringAssert.Contains("GitHubDark.Accent", src);                      // 선택 강조 #58A6FF
        }

        [Test]
        public void Figma124_ImplB_QuestDetailPanel_TruthValues()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("name = \"QuestDetailPanel\"", src);
            StringAssert.Contains("_intelPanel.style.left = 1392f", src);
            StringAssert.Contains("_intelPanel.style.width = 480f", src);
            StringAssert.Contains("_intelPanel.style.height = 888f", src);
            StringAssert.Contains("\"선택 지역 정보\"", src);
            StringAssert.Contains("\"TACTICAL INTEL\"", src);
            StringAssert.Contains("\"해당 구역 주요 상태\"", src);
        }

        [Test]
        public void Figma124_ImplB_LordSection_FourItems()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("\"영주 정보 (LORD STATUS)\"", src);
            StringAssert.Contains("\"영주 이름\"", src);
            StringAssert.Contains("\"병사 수\"", src);
            StringAssert.Contains("\"병사 평균레벨\"", src);
            StringAssert.Contains("\"전투력\"", src);
            StringAssert.Contains("CalculateGuardCombatPower", src);              // 기존 산식 재사용(신산식 금지)
            StringAssert.Contains("GetGuardsInTerritory", src);                   // 실배치 집계(기존 API)
        }

        [Test]
        public void Figma124_ImplB_FastTravel_Wiring()
        {
            string src = ReadSource(WorldMapSourcePath);
            // [125차-38] 기존 텔레포트 위임 보존(통합 판단: 보존 — 유료/소유 영지 텔레포트는 FastTravelSystem 존중)
            StringAssert.Contains("ft.ExecuteFastTravel(_selectedDef.id)", src);
            StringAssert.Contains("ProjectName.Systems.FastTravelSystem.Instance", src);
            // [125차-38] 경로 표시 신계약 배선 — ①선택 없음=토스트 안내(상호작용불가=피드백 필수)
            //   ②창 닫기(지도 가림 방지) → ③미니맵 경로 표시 → ④토스트 '경로 표시: {영지명}' 순서 계약
            StringAssert.Contains("UTKToastService.Show(\"영지를 먼저 선택하세요", src);
            StringAssert.Contains("Vector3 dest = _selectedDef.worldPosition;", src);
            StringAssert.Contains("MinimapRouteOverlay.ShowRouteTo(dest, GitHubDark.Accent);", src);
            StringAssert.Contains("UTKToastService.Show($\"경로 표시: {_selectedDef.territoryName}\");", src);
        }

        // ===== [125차-38] 플루언트 글래스 패널 + 플레이어 현재지 핀 재실측 + 미니맵 경로 오버레이 =====
        // 진실 출처: docs/125_38_WORLDMAP_FLUENT_PINS_ROUTE.md (Figma 2026-10-26T03:19Z 재실측)

        [Test]
        public void Figma125_38_Panels_FluentGlass_161B22A080()
        {
            string src = ReadSource(WorldMapSourcePath);
            // 좌패널(95:188)/우패널(95:255) fill #161B22@0.80 — 불투명 Panel 폐기. 치수/데칼/스트로크 불변.
            StringAssert.Contains("panel.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));", src);
            StringAssert.Contains("_intelPanel.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));", src);
            // UTK 백드롭블러 미지원 → BACKGROUND_BLUR 알파 근사 주석 계약(GlassPanel 선례 동일 취급)
            StringAssert.Contains("BACKGROUND_BLUR", src);
            // CloseButton 32×32 #161B22@0.80 r=16
            StringAssert.Contains("closeBtn.style.width = 32f;", src);
            StringAssert.Contains("closeBtn.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));", src);
        }

        [Test]
        public void Figma125_38_PlayerPin_RemeasuredTruth()
        {
            string src = ReadSource(WorldMapSourcePath);
            // Figma 95:238 재실측 진실치 — PinBase 38.4 / 내부 원 14.4 / 외부 펄스 링 52.8 / PinLabel h=24.6·15px
            StringAssert.Contains("pBase.style.width = 38.4f;", src);
            StringAssert.Contains("pDot.style.width = 14.4f;", src);
            StringAssert.Contains("_playerRing.style.width = 52.8f;", src);
            StringAssert.Contains("_playerPinLabel.style.height = 24.6f;", src);
            StringAssert.Contains("_playerPinLabel.style.fontSize = 13.2f;", src);   // [144차] 재실측 진실치 13.2(95:241) 정정
            // 앵커 = PinBase 중심(38.4/2=19.2px) — TerritoryMarker 핀(-19px)과 분리된 재실측치
            StringAssert.Contains("new Length(-19.2f, LengthUnit.Pixel)", src);
            // 위치식 단일 소스 계약 — BuildTerritoryMarker/아레나 핀 공용 WorldU/WorldV(left%=u*100, top%=(1-v)*100)
            StringAssert.Contains("float u = WorldU(_playerTransform.position);", src);
            StringAssert.Contains("float v = WorldV(_playerTransform.position);", src);
            StringAssert.Contains("_playerMarker.style.left = Length.Percent(u * 100f);", src);
            StringAssert.Contains("_playerMarker.style.top = Length.Percent((1f - v) * 100f);", src);
            // 라벨 실시간 갱신 — ResolveTerritoryAt 단일 소스 + 250ms PollTick 배선(UpdatePlayerMarker)
            StringAssert.Contains("_playerPinLabel.text = loc + \" (현재지)\";", src);
            StringAssert.Contains("UpdatePlayerMarker();", src);
        }

        [Test]
        public void Figma125_38_MinimapRouteOverlay_SourceContract()
        {
            string src = ReadSource("Assets/Scripts/UI/Toolkit/MinimapRouteOverlay.cs");
            // 공개 API 계약 시그니처 — static ShowRouteTo(Vector3, Color) / ClearRoute()
            StringAssert.Contains("public static void ShowRouteTo(Vector3 targetWorldPos, Color color)", src);
            StringAssert.Contains("public static void ClearRoute()", src);
            // MinimapUTK 리프레시 훅 배선 + 플레이어 중심 크롭 동기 산식 공유
            StringAssert.Contains("internal static void OnMinimapRefresh(VisualElement mapFrame, Vector3? playerWorldPos, float localRadius, float fallbackSide)", src);
            StringAssert.Contains("float px = half + (dx / (2f * localRadius)) * side;", src);
            StringAssert.Contains("float py = half - (dz / (2f * localRadius)) * side;", src);
            // 기본색 #58A6FF(Figma Line w2.4)
            StringAssert.Contains("0x58 / 255f, 0xA6 / 255f, 0xFF / 255f", src);
            // 렌더 방식 — 얇은 회전 사각형 세그먼트(대시 폴리라인)
            StringAssert.Contains("new Rotate(new Angle(angleDeg, AngleUnit.Degree))", src);
            // ⚠벡터메시 금지 계약(84차) — generateVisualContent/Mesh 일절 부재
            StringAssert.DoesNotContain("generateVisualContent", src, "[125차-38] 벡터메시 계약 위반 — Mesh API 사용 금지");
            StringAssert.DoesNotContain("new Mesh", src, "[125차-38] 벡터메시 계약 위반 — Mesh 신설 금지");
            StringAssert.DoesNotContain("MeshGenerationContext", src, "[125차-38] 벡터메시 계약 위반 — MeshGenerationContext 금지");
            // 자동 숨김 계약 — 플레이어 부재/도착 판정
            StringAssert.Contains("ArrivalMeters", src);
        }

        [Test]
        public void Figma125_38_MinimapUTK_RouteHook_MinimalChange()
        {
            string src = ReadSource("Assets/Scripts/UI/Toolkit/MinimapUTK.cs");
            // MinimapUTK 최소 변경 계약 — 훅 1곳(Refresh 말단)만 배선
            StringAssert.Contains("MinimapRouteOverlay.OnMinimapRefresh(_mapFrame,", src);
            StringAssert.Contains("LocalRadius, Diameter - 4f);", src);
        }

        [Test]
        public void Figma124_ImplB_TrackingToggle_DefaultOn()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("private bool _trackingEnabled = true;", src);  // 기본 ON
            StringAssert.Contains("_trackingEnabled ? 26.4f : 2.4f", src);        // knob 오른쪽=ON
            StringAssert.Contains("if (_trackingEnabled)", src);                  // 플레이어 핀 위치 게이트
        }

        [Test]
        public void Figma124_ImplB_PlayerOwned_MapsToPlayer()
        {
            string src = ReadSource("Assets/Scripts/UI/Toolkit/WorldMapTerritoryIntelContent.cs");
            StringAssert.Contains("return \"플레이어\"", src);
            StringAssert.Contains("return \"무주\"", src);
        }

        [Test]
        public void Figma124_ImplB_HoverTooltip_ContractKept()
        {
            string src = ReadSource(WorldMapSourcePath);                          // [123차] 계약 무손상
            StringAssert.Contains("ShowTerritoryTooltip", src);
            StringAssert.Contains("WorldMapTerritoryTooltipContent.Build(Def, state)", src);
        }

        [Test]
        public void Figma124_ImplB_PlayerPin_TruthValues()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("name = \"PlayerPinBase\"", src);
            StringAssert.Contains("name = \"PlayerPinRing\"", src);
            StringAssert.Contains("\" (현재지)\"", src);
            StringAssert.Contains("GitHubDark.StatusGreen", src);
        }
    }
}
