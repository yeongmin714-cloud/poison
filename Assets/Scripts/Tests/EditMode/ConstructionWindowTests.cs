using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [90차 갱신] 건설 창 기존 계약 테스트 — O8 C-O8-04 원본 계약을 [90차 B1] Figma 239:683 재작성 UI에 맞춰 갱신.
    ///
    /// 원본(2024 O8) 계약 → 신규 UI 반영 매핑:
    ///  · 진입점 static Open()/Ensure()/Toggle() — 유지(Open은 UIRoot 미준비 가드 추가).
    ///  · U키 토글/Updater 폴링 — 유지(RefreshInterval 0.25 → 0.5s, 스펙 B1 '폴링 500ms').
    ///  · 배치 흐름 GetPlacePosition → ResolveTerritoryAt → TryPlace → 반환 메시지 표시 — 유지
    ///    (표시 매체가 상태 라벨 → UTKToastService 토스트로 교체).
    ///  · StructureCompleted Subscribe/Unsubscribe 쌍 — 유지(상태 라벨 → 완료 토스트).
    ///  · 설계도 나열 8행 → 시설 6종([152차 P0] 훈련소 포함) + 기타 8종(탭) — ConstructionManagementFigmaTests가 진실치 담당,
    ///    본 파일은 계약 경로(데이터 소비/흐름)를 소스 어서션으로 고정.
    ///  · [90차 B2] FacilityOpenRequested 구독/해제 + SelectBlueprint 진입점 신설 계약.
    ///
    /// ※ TryPlace 실소비/시공 경로는 Play 게이트(ConstructionTests O8 선례) — 본 파일은 순수 로직 + 소스 어서션.
    /// </summary>
    public sealed class ConstructionWindowTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs";
        private const string HologramSourcePath = "Assets/Scripts/Systems/ConstructionHologram.cs";

        private static string Src() => File.ReadAllText(SourcePath);
        private static string HoloSrc() => File.ReadAllText(HologramSourcePath);

        // ── 1. 진입점 계약 (static Open/Ensure/Toggle — 시그니처 불변) ──

        [Test]
        public void EntryPoints_Source_SignaturesPreserved()
        {
            string src = Src();

            Assert.That(src, Does.Contain("public static void Ensure()"), "Ensure() — 멱등 팩토리 계약.");
            Assert.That(src, Does.Contain("public static void Open()"), "Open() — 개방 진입점 계약.");
            Assert.That(src, Does.Contain("public static void Toggle()"), "Toggle() — 토글 계약(U키 경로).");
            Assert.That(src, Does.Contain("public static ConstructionWindowUTK Instance => _instance"), "Instance 접근자 계약.");
            Assert.That(src, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"),
                "Bootstrap AfterSceneLoad 자가부트 계약.");
            Assert.That(src, Does.Contain("go.AddComponent<Updater>().window = _instance"),
                "Updater 부착 계약 — 기존 O8 패턴 유지.");
        }

        // ── 2. 배치 흐름 계약 (GetPlacePosition → ResolveTerritoryAt → TryPlace → 결과 표시) ──

        [Test]
        public void PlaceFlow_Source_ContractPathPreserved()
        {
            string src = Src();
            string holo = HoloSrc();

            // [104차 수리2] 계약 경로 보존 — 즉시 배치 → 홀로그램 모드 전환(BeginPlacement) + 좌클릭 확정 경로.
            int begin = src.IndexOf("ConstructionHologram.BeginPlacement(blueprintId);");
            int toast = src.IndexOf("UTKToastService.Show(\"위치를 선택하세요 — 클릭 확정 / Esc 취소\");");
            Assert.That(begin, Is.GreaterThan(0), "① 배치 진입 — 홀로그램 모드 전환(104차).");
            Assert.That(toast, Is.GreaterThan(begin), "② 위치 선택 안내 토스트 — BeginPlacement 이후.");

            // 홀로그램 확정 경로 — 영지 판정 → TryPlace(기존 계약 순서 유지)
            int holoResolve = holo.IndexOf("db.ResolveTerritoryAt(pos)");
            int holoTryPlace = holo.IndexOf("TryPlace(territoryId, _blueprintId, pos)");
            Assert.That(holoResolve, Is.GreaterThan(0), "③ 영지 자동 판정(ResolveTerritoryAt) — 홀로그램 확정 경로.");
            Assert.That(holoTryPlace, Is.GreaterThan(holoResolve), "④ TryPlace — 영지 판정 이후(기존 계약 순서).");

            Assert.That(src, Does.Contain("cam.transform.position + cam.transform.forward * 5f"),
                "배치 위치 = 카메라 forward×5(기존 계약 — GetPlacePosition 정의 유지).");
            Assert.That(src, Does.Contain("return Vector3.zero;"), "카메라 없음 폴백(기존 계약).");
            Assert.That(src, Does.Contain("⚠️ ConstructionManager가 없습니다"), "Manager 부재 가드 유지(기존 계약).");
        }

        // ── 3. 데이터 소비 계약 (복제 금지 — Systems API 직접 소비) ──

        [Test]
        public void DataConsumption_Source_SystemsApiDirect()
        {
            string src = Src();

            Assert.That(src, Does.Contain("BlueprintData.GetAll()"), "설계도 열거 — BlueprintData.GetAll 단일소스.");
            Assert.That(src, Does.Contain("BlueprintData.TryGet("), "설계도 조회 — TryGet 단일소스.");
            Assert.That(src, Does.Contain("ConstructionManager.Instance"), "ConstructionManager 직접 소비(복제 금지).");
            Assert.That(src, Does.Contain("PlayerStats.Instance"), "소지금 — PlayerStats 실측.");
            Assert.That(src, Does.Contain("TimeManager.Instance"), "헤더 시각 — TimeManager 실측.");
            Assert.That(src, Does.Contain("tm.GetFormattedTime()"), "HH:MM — GetFormattedTime 실측.");
            Assert.That(src, Does.Contain("tm.CurrentDay"), "일차 — CurrentDay 실측.");
            // 구조물/해체 — 기존 구조물 목록 조회(기타 탭 설치 여부/기존 계약)
            Assert.That(src, Does.Contain("mgr.Structures"), "구조물 목록 — Structures 실측 조회.");
            Assert.That(src, Does.Contain("s.isComplete"), "설치 여부 — isComplete 실측.");
        }

        // ── 4. 구독/폴링 계약 (Subscribe/Unsubscribe 쌍 + Updater 폴링) ──

        [Test]
        public void SubscriptionAndPolling_Source_PairsAndUpdater()
        {
            string src = Src();

            Assert.That(src, Does.Contain("private void SubscribeStructureCompleted()"), "구독 진입 메서드.");
            Assert.That(src, Does.Contain("private void UnsubscribeStructureCompleted()"), "해제 진입 메서드(쌍 계약).");
            Assert.That(src, Does.Contain("if (_structureSubscribed) return;"), "중복 구독 가드(기존 계약).");
            Assert.That(src, Does.Contain("window._refreshTimer += Time.unscaledDeltaTime"), "폴링 타이머 — unscaledDeltaTime(기존 계약).");
            Assert.That(src, Does.Contain("window.OnUpdaterPoll();"), "폴링 경로 — OnUpdaterPoll(ApplyCanvasFit+Refresh).");
            Assert.That(src, Does.Contain("window.RemoveFromHierarchy();"), "OnDestroy 정리(기존 계약).");
            // [90차 B2] 시설 개방 이벤트 — 구독/해제 쌍(실측: 이벤트는 FacilityStationInteractable 소유)
            Assert.That(src, Does.Contain("FacilityStationInteractable.FacilityOpenRequested -= OnFacilityOpenRequested"), "FacilityOpenRequested 해제(구독 전).");
            Assert.That(src, Does.Contain("FacilityStationInteractable.FacilityOpenRequested += OnFacilityOpenRequested"), "FacilityOpenRequested 구독.");
        }

        // ── 5. [90차] 신규 계약 — SelectBlueprint/음식점 선택/토스트 결과 ──

        [Test]
        public void NewContracts_Source_SelectBlueprintAndToast()
        {
            string src = Src();

            Assert.That(src, Does.Contain("public static void SelectBlueprint(string blueprintId)"),
                "[90차 B2] SelectBlueprint 신설 — 공격조 UI '음식점 배치' 진입점.");
            Assert.That(src, Does.Contain("_instance.Refresh();"), "SelectBlueprint — 우측 패널 즉시 갱신.");
            Assert.That(src, Does.Contain("UTKToastService.Show(result)"), "TryPlace 반환 메시지 = 토스트(상태 라벨 대체).");
            Assert.That(src, Does.Contain("IsOpen) Refresh();"), "완료 이벤트 — 개방 중 갱신(기존 계약).");
        }

        // ── 6. 시설 6종 설계도 실측 정합 (BlueprintData 단일소스 — [90차 A2]/[125차] 확정치) ──

        [Test]
        public void FacilityBlueprints_Source_MatchesBlueprintData()
        {
            // 시설 6종 — BlueprintData 등록 id 실측(ConstructionManagementFigmaTests는 소스 고정, 본 테스트는 데이터 정합).
            // [152차 P0] 훈련소(bp_traininghall) 추가 — 5종→6종.
            string[] facilityIds = { "bp_cooking", "bp_forge", "bp_restaurant", "bp_barracks", "bp_alchemy", "bp_traininghall" };
            foreach (string id in facilityIds)
                Assert.That(BlueprintData.TryGet(id, out _), Is.True, $"시설 설계도 등록 실측: {id}");

            Assert.That(BlueprintData.TryGet("bp_restaurant", out var restaurant), Is.True);
            Assert.That(restaurant.materialWoodCost, Is.EqualTo(5), "음식점 재료 기존 계약 유지(나무 5 — 88차 D4 보호).");
            Assert.That(restaurant.materialStoneCost, Is.EqualTo(3), "음식점 재료 기존 계약 유지(돌 3).");
            Assert.That(restaurant.goldCost, Is.EqualTo(200), "음식점 골드 기존 계약 유지.");

            // 기존 8종 — 신규 재료 시스템 미적용(0 유지, 기존 골드 비용만)
            Assert.That(BlueprintData.TryGet("bp_wall", out var wall), Is.True);
            Assert.That(wall.materialWoodCost + wall.materialStoneCost
                + wall.materialIronCost + wall.materialLeatherCost + wall.materialManaCost,
                Is.EqualTo(0), "기존 8종 재료 0 유지(90차 확정 — 신규 재료 시스템 대상 아님).");

            // 전체 14종 = 기존 8 + 음식점 + 신규 4 + [125차] 훈련소 — UI 탭 카운트의 데이터 근거.
            Assert.That(BlueprintData.Count, Is.EqualTo(14), "전체 설계도 14종.");
        }

        // ── 6-1. [152차 P0] 훈련소(bp_traininghall) 건설창 좌 목록 카드 — 시설 5종→6종 ──

        /// <summary>
        /// [152차 P0] 소스계약 — 근거: 사용자 지시 '건설 창에 훈련장(훈련소) 항목이 없다'. 원인 =
        /// ConstructionWindowUTK.FacilityIds에 bp_traininghall 누락(시설 5종). 수리 = ①FacilityIds 6종 확장
        /// (기존 시설 뒤 말미) ②FacilityRoleLabel/FacilityUnlockText 훈련소 케이스(기존 '분류 · 설명'/'… 해금'
        /// 형식 준수) ③좌 목록/탭 카운트는 FacilityIds 루프·Length 단일소스라 자동 반영(RefreshBlueprintList
        /// 로직 무변경). 실물 빌더/FacilityOpenRequested 라우팅은 125차 완성 — 본 수리는 UI 목록 누락만.
        /// </summary>
        [Test]
        public void ConstructionWindow_TrainingHall_Listed_SourceContract_152()
        {
            string src = Src();

            // ① FacilityIds — bp_traininghall 포함(기존 5종 뒤 말미), 6종 전체 라인 고정.
            Assert.That(src, Does.Contain(
                    "\"bp_cooking\", \"bp_forge\", \"bp_restaurant\", \"bp_barracks\", \"bp_alchemy\", \"bp_traininghall\","),
                "[152차 P0] FacilityIds 6종 — 훈련소 말미 추가(5종→6종).");

            // ② 역할/효과 문구 — 기존 형식 준수.
            Assert.That(src, Does.Contain("\"bp_traininghall\" => \"훈련 · 병사 훈련과 전투 훈련\","),
                "[152차 P0] FacilityRoleLabel 훈련소 케이스 — '훈련 · 병사 훈련과 전투 훈련'.");
            Assert.That(src, Does.Contain("\"bp_traininghall\" => \"병사 훈련 · 전투 능력 강화 해금\","),
                "[152차 P0] FacilityUnlockText 훈련소 케이스 — '… 해금' 형식.");

            // ③ 목록/탭 카운트 단일소스 — RefreshBlueprintList 시설 탭 = FacilityIds 루프(BuildFacilityItem),
            //    탭 라벨 = Length 기반(전체 6/기타 14−6=8 자동 산출).
            Assert.That(src, Does.Contain("$\"전체 {FacilityIds.Length}\""),
                "[152차 P0] 전체 탭 = FacilityIds.Length 단일소스(6 자동 반영).");
            Assert.That(src, Does.Contain("$\"기타 {Mathf.Max(BlueprintData.Count - FacilityIds.Length, 0)}\""),
                "[152차 P0] 기타 탭 = 14 − 6 = 8 불변.");
            int listMethod = src.IndexOf("private void RefreshBlueprintList()");
            int facilityLoop = src.IndexOf("foreach (string id in FacilityIds)", listMethod);
            int buildCall = src.IndexOf("BuildFacilityItem(def, materialsOk)", listMethod);
            Assert.That(listMethod, Is.GreaterThan(0), "RefreshBlueprintList 존재.");
            Assert.That(facilityLoop, Is.GreaterThan(listMethod),
                "[152차 P0] 시설 탭 = FacilityIds 루프 — 훈련소 카드가 동일 스타일/그리드/폰트로 생성.");
            Assert.That(buildCall, Is.GreaterThan(facilityLoop),
                "[152차 P0] 시설 항목 = BuildFacilityItem 단일 빌더(스타일 자동 동일).");

            // ④ 런타임/데이터 정합 — FacilityIds 실 포함 + BlueprintData 실측치(위조 금지).
            Assert.That(ProjectName.UI.Toolkit.ConstructionWindowUTK.FacilityIds,
                Does.Contain("bp_traininghall"), "[152차 P0] FacilityIds 런타임 포함.");
            Assert.That(BlueprintData.TryGet("bp_traininghall", out var trainingHall), Is.True,
                "[152차 P0] bp_traininghall BlueprintData 등록 실측.");
            Assert.That(trainingHall.goldCost, Is.EqualTo(600), "훈련소 골드 실측.");
            Assert.That(trainingHall.buildTimeSeconds, Is.EqualTo(120), "훈련소 건설 시간 실측.");
            Assert.That(trainingHall.materialWoodCost, Is.EqualTo(12), "훈련소 나무 실측.");
            Assert.That(trainingHall.materialStoneCost, Is.EqualTo(8), "훈련소 돌 실측.");
        }

        // ── 7. 위조 방지 가드 — Figma 목업 수치 미채용 ──

        [Test]
        public void NoMockupNumbers_Source_Guard()
        {
            string src = Src();

            // Figma 목업 보유치(180/120/24/18/8)·유휴 3명 고정치 미채용 — 전부 실측(GetItemCount/CountAvailableGuards).
            Assert.That(src, Does.Not.Contain("heldWood = 180"), "보유 나무 목업치 금지.");
            Assert.That(src, Does.Not.Contain("heldStone = 120"), "보유 돌 목업치 금지.");
            Assert.That(src, Does.Not.Contain("유휴 인원 3명\""), "유휴 인원 고정치 금지 — FormatWorkforce 실측.");
            Assert.That(src, Does.Contain("CountAvailableGuards()"), "유휴 인원 = 가용 병사 실측 호출.");
        }

        // ── 8. [93차 F2] 개방 진단(재판정 장치) + ApplyCanvasFit 폴백 방어 ──

        /// <summary>
        /// [93차 F2] U키 건설창 "안뜸" — 개방 자체는 정상, 개방 순간 리페인트 정지 체감(영상4: 열림 로그
        /// attached=True 직후 픽셀 diff 0.000 구간). 원인 단정 보류(유저 환경 확인 후 별도) — 본 수리는
        /// ①Show 개방 직후 화면 상태 진단 로그(ApplyCanvasFit k/배치 실측 즉격) ②폴백 방어(k≤0.2 또는
        /// 오프스크린 배치 → 중앙 배치 + k=panel.min/WinW 상한 재적용)만. 동작 외 변경 금지 계약.
        /// </summary>
        [Test]
        public void OpenDiagnostics_And_FitFallback_93()
        {
            string src = Src();

            Assert.That(src, Does.Contain("[93차 F2]"), "[93차 F2] 마커 주석.");
            // ① 개방 직후 진단 로그 — panel/k/windowRect/onScreen 실측(스케일 0·오프스크린 즉격).
            Assert.That(src, Does.Contain("개방 직후 화면 상태 — panel=("),
                "[93차 F2] 개방 진단 로그 형식(재판정 장치).");
            Assert.That(src, Does.Contain("private void LogOpenScreenState()"),
                "[93차 F2] 진단 메서드 존재.");
            // 호출 순서 — Show에서 ApplyCanvasFit 직후(적용값을 인용해야 실측이 된다).
            int fitCall = src.IndexOf("ApplyCanvasFit();   // 개방 즉시 1회");
            int logCall = src.IndexOf("LogOpenScreenState();");
            Assert.That(fitCall, Is.GreaterThan(0), "Show의 ApplyCanvasFit 직접 호출 존재.");
            Assert.That(logCall, Is.GreaterThan(fitCall),
                "[93차 F2] 진단 로그는 ApplyCanvasFit 직후 호출(적용 k 실측).");
            // 미적용 판정 — 레이아웃 미확정으로 ApplyCanvasFit이 건너뛰면 진단 로그가 이를 즉격한다.
            Assert.That(src, Does.Contain("k=미적용(레이아웃 미확정)"),
                "[93차 F2] 미적용 상태 진단(개방 시 k 미확정 감별).");

            // ② 폴백 방어 — k≤0.2(소멸 스케일) 또는 전체 오프스크린 배치 감지.
            Assert.That(src, Does.Contain("if (k <= 0.2f || fullyOutside)"),
                "[93차 F2] 폴백 트리거 — k≤0.2 또는 패널 밖 배치.");
            Assert.That(src, Does.Contain("k = panelMin / WinW;"),
                "[93차 F2] 폴백 스케일 = panel.min/WinW 상한(창 폭이 패널 짧은 변 이하 보장).");
            Assert.That(src, Does.Contain("ApplyCanvasFit 폴백"),
                "[93차 F2] 폴백 감지 로그(재발 추적).");

            // ③ 동작 외 변경 금지 — 기존 등배수 산식/중앙 배치 계약 그대로(84차 계약 회귀 없음).
            Assert.That(src, Does.Contain("float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);"),
                "[84차 계약 유지] k=min(rootW/1920, rootH/1080) 산식 불변.");
            Assert.That(src, Does.Contain("style.width = WinW * k;"),
                "[84차 계약 유지] 창박스=figma×k 적용 불변.");
            Assert.That(src, Does.Contain("_content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));"),
                "[84차 계약 유지] 내부 raw 유지+scale=k 불변.");
        }

        // ── 9. [94차 F2] 코드 배경 폴백 + DOM 조상 체인 진단 ──

        /// <summary>
        /// [94차 F2] 소스 어서션 — 근거: 열림 로그 3회 사이클에서 레이아웃 완전 정상(panel 1440×909·
        /// onScreen=True)인데 렌더 부재(요소가 그려지지 않는 것 — 가림/죽은 패널/시각 리소스 누락 계열).
        /// 수리 = ①창 패널 인라인 단색+보더 폴백(ThemeUss 배경 PNG 누락과 무관한 가시성 보장 — 누락 곳만)
        /// ②개방 직후 DOM 조상 체인 실측(부착 위치/display·visibility·opacity·pickingMode 즉격).
        /// </summary>
        [Test]
        public void BackgroundFallback_And_DomAncestorChain_94()
        {
            string src = Src();

            Assert.That(src, Does.Contain("[94차 F2]"), "[94차 F2] 마커 주석.");
            // ① 인라인 배경 폴백 — 스펙 지정 다크 톤(패널 #1B1F27 알파 0.92, 보더 #3A4150 1px).
            Assert.That(src, Does.Contain("FallbackPanelBg"), "[94차 F2] 폴백 패널색 상수 존재.");
            Assert.That(src, Does.Contain("0x1B / 255f, 0x1F / 255f, 0x27 / 255f, 0.92f"),
                "[94차 F2] 폴백 패널 #1B1F27 알파 0.92(스펙 지정 Figma 다크 톤).");
            Assert.That(src, Does.Contain("new Color32(0x3A, 0x41, 0x50, 0xFF)"),
                "[94차 F2] 폴백 보더 #3A4150.");
            Assert.That(src, Does.Contain("private static void EnsureInlineBackgroundFallback(VisualElement ve)"),
                "[94차 F2] 인라인 배경 폴백 헬퍼 존재.");
            Assert.That(src, Does.Contain("ve.style.backgroundColor.keyword != StyleKeyword.Null"),
                "[94차 F2] 멱등 가드 — 이미 인라인 배경이 있으면 무수정(누락 곳만 적용).");
            // 4개 빌더 — 좌/우 패널은 폴백 호출 유지(가드 무수정), 헤더/도움말은 [112차 F-A]로 폴백 제거
            // (Figma fills=[] — 루트 캔버스 #0B0E14 불투명화로 헤더/도움말이 캔버스를 그대로 비춤).
            Assert.That(src, Does.Not.Contain("EnsureInlineBackgroundFallback(header);"),
                "[112차 F-A] 헤더바 폴백 제거(Figma fills=[] — 유령 바 방지).");
            Assert.That(src, Does.Not.Contain("EnsureInlineBackgroundFallback(help);"),
                "[112차 F-A] 도움말바 폴백 제거(Figma fills=[] — 유령 바 방지).");
            Assert.That(src, Does.Contain("EnsureInlineBackgroundFallback(panel);"),
                "[94차 F2] 좌/우 패널 폴백 호출 유지(가드 — PanelBg 명시라 무수정).");
            // [112차 F-A] 창 루트 — Figma 루트 프레임 fill SOLID #0B0E14(투명 Color.clear 폐지) + 창 rim 섀도우 off.
            Assert.That(src, Does.Contain("private static readonly Color FigmaRootBg = new Color32(0x0B, 0x0E, 0x14, 0xFF);"),
                "[112차 F-A] Figma 루트 캔버스 fill #0B0E14 상수 존재.");
            // [125차 후속37] Figma 239:683 현재값 재실측 원복(후속12 역전) — 루트 fill SOLID #0B0E14@1.00
            // 불투명 + 좌/우 패널 fill #13181F@1.00(ApplyStroke 보더 유지). Figma API 실측 근거.
            Assert.That(src, Does.Contain("style.backgroundColor = new StyleColor(FigmaRootBg);"),
                "[125차 후속37] 창 루트 배경 = Figma 루트 캔버스 fill #0B0E14 불투명(후속12 투명 원복).");
            Assert.That(src, Does.Contain("new StyleColor(PanelBg);"),
                "[125차 후속37] 좌/우 판 배경 = Figma #13181F 불투명(후속12 글래스 원복).");
            Assert.That(src, Does.Not.Contain("UTKTheme.GlassPanel"),
                "[125차 후속37] 글래스 판 잔존 금지 — 좌/우 패널 전부 PanelBg 원복.");
            Assert.That(src, Does.Contain("DisableWindowShadow();"),
                "[112차 F-A] 창 레벨 12px rim 섀도우 비활성(Figma 프레임 drop shadow 없음).");

            // ② DOM 조상 체인 진단 — 부착 패널/자식 인덱스/형제 수/display·visibility·opacity·pickingMode.
            Assert.That(src, Does.Contain("private void LogDomAncestorChain()"), "[94차 F2] 조상 체인 진단 메서드 존재.");
            Assert.That(src, Does.Contain("DOM 조상 체인["), "[94차 F2] 조상 체인 로그 형식.");
            Assert.That(src, Does.Contain("el.parent.IndexOf(el)"), "자식 인덱스 실측.");
            Assert.That(src, Does.Contain("el.parent.childCount"), "형제 수 실측.");
            Assert.That(src, Does.Contain("rs.display"), "display 실측.");
            Assert.That(src, Does.Contain("rs.visibility"), "visibility 실측.");
            Assert.That(src, Does.Contain("rs.opacity"), "opacity 실측.");
            Assert.That(src, Does.Contain("el.pickingMode"), "pickingMode 실측.");
            // 호출 — 개방 직후 진단(LogOpenScreenState) 경유(레이아웃 미적용 분기 포함 2곳).
            Assert.That(src, Does.Contain("LogDomAncestorChain();"), "[94차 F2] 개방 진단 경로 호출.");
        }

        // ── 10. [96차 F3] worldBound 재실측 + 기하 0 방어 ──

        /// <summary>
        /// [96차 F3] 소스 어서션 — 근거: DOM 진단(94차) 성공 — `DOM 조상 체인 [0]UTKWindow_건설 관리
        /// display=Flex...` 전부 정상인데 `개방 직후 world=(0,0,0x0)`(2세션 연속 — 위치는 있고 크기 0x0)
        /// → 요소 실제 기하가 0(시각 리소스 설 기각 확정). 수리 = ①ApplyCanvasFit 전 호출 경로(개방 즉시+
        /// 16/120/400ms 스케줄)마다 적용 style치+resolvedStyle+worldBound+직계 부모 worldBound 재실측 로그
        /// ②worldBound 0/비정상 소 감지 시 창 루트에 position=absolute+width/height 재주입("레이아웃은 있고
        /// 기하 0" 탈출 — 93차 폴백과 조합). 기존 폴백/84차 산식/94차 DOM 진단 회귀 금지.
        /// </summary>
        [Test]
        public void WorldBoundRemeasure_And_GeometryReinforce_96()
        {
            string src = Src();

            Assert.That(src, Does.Contain("[96차 F3]"), "[96차 F3] 마커 주석.");
            // ① 재실측 로그 — style/resolved/world/parent 즉격(스펙 결함 3 수리 1).
            Assert.That(src, Does.Contain("private void MeasureGeometryAndDefend(float k, float left, float top)"),
                "[96차 F3] 재실측+방어 메서드 존재.");
            Assert.That(src, Does.Contain("worldBound 재실측 — style=("),
                "[96차 F3] 재실측 로그 형식(적용 style치+resolvedStyle+worldBound+부모 worldBound).");
            Assert.That(src, Does.Contain("MeasureGeometryAndDefend(k, left, top);"),
                "[96차 F3] ApplyCanvasFit 말단 실측 — 개방 즉시+16/120/400ms 스케줄 전 호출 경로 공통.");
            Assert.That(src, Does.Contain("parent.worldBound"), "[96차 F3] 직계 부모 worldBound 실측.");
            Assert.That(src, Does.Contain("resolvedStyle"), "[96차 F3] resolvedStyle 병기(레이아웃 패스 전후 대조).");
            // ② 방어 — 기하 0/panel 대비 비정상 소 감지 → position=absolute+width/height 재주입(스펙 수리 2).
            Assert.That(src, Does.Contain("bool zeroGeom = wb.width <= 0.5f || wb.height <= 0.5f;"),
                "[96차 F3] 기하 0 감지(worldBound 0x0).");
            Assert.That(src, Does.Contain("wb.width < appliedW * 0.5f || wb.height < appliedH * 0.5f"),
                "[96차 F3] panel(적용치=figma×k) 대비 비정상 소 감지(반 이하).");
            Assert.That(src, Does.Contain("style.position = Position.Absolute;"),
                "[96차 F3] 기하 재주입 — position=absolute.");
            Assert.That(src, Does.Contain("기하 재주입 — worldBound"),
                "[96차 F3] 기하 재주입 경고 로그(재발 추적).");
            // ③ 기존 계약 유지 — 93차 폴백/84차 산식/94차 DOM 진단·배경 폴백 회귀 없음.
            Assert.That(src, Does.Contain("if (k <= 0.2f || fullyOutside)"),
                "[93차 F2] 폴백 트리거 유지(k≤0.2/오프스크린 → 중앙 재배치 — 96차 방어와 조합).");
            Assert.That(src, Does.Contain("float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);"),
                "[84차 계약 유지] k=min(rootW/1920, rootH/1080) 산식 불변.");
            Assert.That(src, Does.Contain("private void LogDomAncestorChain()"),
                "[94차 F2] DOM 체인 진단 유지.");
            Assert.That(src, Does.Not.Contain("EnsureInlineBackgroundFallback(header);"),
                "[112차 F-A] 헤더바 폴백 제거 유지(Figma fills=[]).");
        }

        // ── 11. [99차 F-E] 마젠타 진단 제거(E-3) + 콘텐츠 렌더 수리(E-1/E-4) + UIDocument 전수 진단 유지 ──

        /// <summary>
        /// [99차 F-E] 계약진화(94차 선례) — [97차 F4] 마젠타 강제 표시(임시 진단)는 원인 확정 완료(마젠타 렌더=
        /// 창 루트 정상, 내용물 0=콘텐츠 미부착)로 제거(E-3)되고, 그 자리에 ①E-1 콘텐츠 부착 수리(4개 블록
        /// _content 직결 — NPCDialoguePanelUTK 패턴 준용) ②E-3 개방 시 콘텐츠 worldBound+자식 display 실측 로그
        /// ③E-4 기하 재주입 후에도 콘텐츠 0x0이면 콘텐츠 루트 UIRoot 직결 재부착 폴백의 소스 단정으로 대체한다.
        /// UIDocument 전수 진단(97차 F4 — 마젠타와 무관)과 93/94/96차 계약은 무손상 유지.
        /// </summary>
        [Test]
        public void MagentaBisect_And_UidocumentInventory_97()
        {
            string src = Src();

            Assert.That(src, Does.Contain("[97차 F4]"), "[97차 F4] 마커 주석(UIDocument 전수 진단 유지).");

            // ① [99차 F-E] E-3 마젠타 진단 제거 — 원인 확정 완료(마젠타 렌더=창 루트 정상/내용물 0=콘텐츠 미부착).
            Assert.That(src, Does.Not.Contain("ArmMagentaDiagnostic"), "[99차 F-E] E-3 마젠타 가동 진단 제거.");
            Assert.That(src, Does.Not.Contain("RestoreMagentaDiagnostic"), "[99차 F-E] E-3 마젠타 원복 진단 제거.");
            Assert.That(src, Does.Not.Contain("MagentaDiagBg"), "[99차 F-E] E-3 마젠타 상수 제거.");
            Assert.That(src, Does.Not.Contain("MagentaDiagMs"), "[99차 F-E] E-3 마젠타 0.6s 상수 제거.");

            // ② [99차 F-E] E-1 콘텐츠 부착 수리 — NPCDialoguePanelUTK 패턴 준용(_content 직결 부착).
            Assert.That(src, Does.Contain("_content.Add(header);"), "[99차 F-E] E-1 헤더 블록 _content 직결 부착.");
            Assert.That(src, Does.Contain("_content.Add(panel);"), "[99차 F-E] E-1 좌/우 패널 블록 _content 직결 부착.");
            Assert.That(src, Does.Contain("_content.Add(help);"), "[99차 F-E] E-1 도움말 블록 _content 직결 부착.");
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK"), "[99차 F-E] E-1 선례 패턴 준용 표기.");

            // ③ [99차 F-E] E-3 개방 시 콘텐츠 실측 로그 — content worldBound+children+display 요약.
            Assert.That(src, Does.Contain("[99차 F-E] 콘텐츠 렌더 — worldBound=("),
                "[99차 F-E] 콘텐츠 실측 로그 형식(worldBound 실측).");
            Assert.That(src, Does.Contain("_content.worldBound"), "[99차 F-E] 콘텐츠 worldBound 실측 경로.");
            Assert.That(src, Does.Contain("children={_content.childCount}"), "[99차 F-E] 자식 수 실측.");
            Assert.That(src, Does.Contain("BuildContentDisplaySummary()"), "[99차 F-E] 자식 display 요약 경로.");

            // ④ [99차 F-E] E-4 폴백 — 재주입 후에도 콘텐츠 0x0이면 콘텐츠 루트 UIRoot 직결 재부착.
            Assert.That(src, Does.Contain("private void ReattachContentToUiRootFallback("),
                "[99차 F-E] 콘텐츠 루트 UIRoot 직결 재부착 폴백 메서드.");
            Assert.That(src, Does.Contain("UIRoot 직결 재부착 폴백 — 재주입 후에도 콘텐츠 0x0"),
                "[99차 F-E] 폴백 발동 경고 로그(재발 추적).");
            Assert.That(src, Does.Contain("private void RestoreContentToWindowIfFallback()"),
                "[99차 F-E] 폴백 해제(닫힘 시 창 복귀) 메서드.");
            Assert.That(src, Does.Contain("RestoreContentToWindowIfFallback();"),
                "[99차 F-E] Hide 경로 폴백 해제 호출(화면 잔존 방지).");
            Assert.That(src, Does.Contain("_geometryReinjected"), "[99차 F-E] 재주입 실행 표적(E-4 폴백 게이트).");
            Assert.That(src, Does.Contain("_contentZeroCycles"), "[99차 F-E] 콘텐츠 0x0 연속 실측 카운터.");

            // ⑤ [97차 F4] UIDocument 전수 진단 — 마젠타와 무관, 유지. 개방 진단(LogOpenScreenState) 이후 호출.
            int showIdx = src.IndexOf("public override void Show()");
            int logOpenCall = src.IndexOf("LogOpenScreenState();");
            int invCall = src.IndexOf("LogUidocumentInventory();");
            Assert.That(showIdx, Is.GreaterThan(0), "Show 오버라이드 존재.");
            Assert.That(logOpenCall, Is.GreaterThan(showIdx), "[93차 F2] 개방 진단 호출 유지.");
            Assert.That(invCall, Is.GreaterThan(logOpenCall),
                "[97차 F4] UIDocument 전수 진단이 개방 진단 이후 호출(개방 시 1회).");

            // ⑥ [97차 F4] UIDocument 전수 진단 — 이름/패널 크기/sortingOrder/enabled + 창 루트 소속 대조.
            Assert.That(src, Does.Contain("private void LogUidocumentInventory()"),
                "[97차 F4] UIDocument 전수 진단 메서드 존재.");
            Assert.That(src, Does.Contain("Object.FindObjectsOfType<UIDocument>()"),
                "[97차 F4] FindObjectsOfType 전수(스펙 지정 — non-alloc 아님).");
            Assert.That(src, Does.Contain("sortingOrder="), "[97차 F4] UIDocument 로그 필드 — sortingOrder.");
            Assert.That(src, Does.Contain("panelSize=("), "[97차 F4] UIDocument 로그 필드 — 패널 크기.");
            Assert.That(src, Does.Contain("enabled={doc.enabled} goActive={doc.gameObject.activeInHierarchy}"),
                "[97차 F4] UIDocument 로그 필드 — enabled(컴포넌트+GO 활성).");
            Assert.That(src, Does.Contain("panelSettings={psName}"),
                "[97차 F4] UIDocument 로그 필드 — PanelSettings 소속명.");
            Assert.That(src, Does.Contain("ReferenceEquals(docRoot, winRoot)"),
                "[97차 F4] 창 루트 소속 대조 — rootVisualElement 참조 일치 판정.");
            Assert.That(src, Does.Contain("창루트소속="), "[97차 F4] 소속 대조 결과 로그 필드.");
            Assert.That(src, Does.Contain("private static VisualElement GetTopAncestor(VisualElement ve)"),
                "[97차 F4] 창 루트(최상위 조상) 탐색 헬퍼 존재.");

            // ⑦ 기존 계약 무손상 — 개방 진단(93차)/폴백 방어(93차)/DOM 체인+배경 폴백(94차)/재실측(96차).
            Assert.That(src, Does.Contain("private void LogOpenScreenState()"), "[93차 F2] 개방 진단 유지.");
            Assert.That(src, Does.Contain("if (k <= 0.2f || fullyOutside)"), "[93차 F2] 폴백 방어 유지.");
            Assert.That(src, Does.Contain("private void LogDomAncestorChain()"), "[94차 F2] DOM 체인 진단 유지.");
            Assert.That(src, Does.Contain("MeasureGeometryAndDefend(k, left, top);"), "[96차 F3] worldBound 재실측 유지.");
            Assert.That(src, Does.Not.Contain("EnsureInlineBackgroundFallback(header);"),
                "[112차 F-A] 헤더바 폴백 제거 유지(Figma fills=[]).");
        }

        // ── 12. [125차 후속37] RefreshHeaderTime 널가드 + Figma 239:683 현재값 재정합 ──

        /// <summary>
        /// [125차 후속37] 소스 어서션 — 근거: 사용자 스크린샷 '왼쪽 목록이 하나도 안 뜸'. Refresh()가
        /// RefreshHeaderTime()를 선두 호출하고 BuildHeaderBar 미호출(후속13 유지) 상태에서
        /// _headerDayLabel/_headerClockLabel 널 역참조 NRE → RefreshBlueprintList 도달 불가가 원인.
        /// 수리 = 널가드 조기 반환(헤더는 소실 상태 유지가 정답 — BuildHeaderBar 호출 복원 금지) +
        /// Figma 현재값 재정합(루트/패널 불투명 원복, 항목 420, 스크롤바 트랙/썸 신설).
        /// </summary>
        [Test]
        public void RefreshHeaderTime_NullGuard_And_FigmaRealign_125_37()
        {
            string src = Src();

            // ① 널가드 — 헤더 라벨 미빌드 시 조기 반환(좌 목록 갱신 경로 보호).
            Assert.That(src, Does.Contain("if (_headerDayLabel == null || _headerClockLabel == null) return;"),
                "[125차 후속37] RefreshHeaderTime 널가드 — 헤더 미빌드 NRE 조기 차단.");
            int guardMethod = src.IndexOf("private void RefreshHeaderTime()");
            int guardLine = src.IndexOf("if (_headerDayLabel == null || _headerClockLabel == null) return;");
            Assert.That(guardMethod, Is.GreaterThan(0), "RefreshHeaderTime 메서드 존재.");
            Assert.That(guardLine, Is.GreaterThan(guardMethod),
                "[125차 후속37] 널가드는 RefreshHeaderTime 본문 선두(라벨 역참조 전) 위치.");
            // 갱신 순서 계약 유지 — 헤더 시각 → 필터 탭 → 좌 목록(가드 통과 시 기존 순서 그대로).
            int refreshMethod = src.IndexOf("public void Refresh()");
            int headerCall = src.IndexOf("RefreshHeaderTime();", refreshMethod);
            int listCall = src.IndexOf("RefreshBlueprintList();", refreshMethod);
            Assert.That(refreshMethod, Is.GreaterThan(0), "Refresh 엔트리 존재.");
            Assert.That(headerCall, Is.GreaterThan(refreshMethod), "Refresh 헤더 시각 갱신 호출 존재.");
            Assert.That(listCall, Is.GreaterThan(headerCall), "Refresh 순서 유지 — 헤더 시각 다음 좌 목록.");
            // 헤더 소실 유지 — BuildHeaderBar 정의만 남고 호출부 없음(후속13 계약).
            Assert.That(src, Does.Contain("private void BuildHeaderBar()"), "BuildHeaderBar 정의 유지(미호출).");
            Assert.That(src, Does.Not.Contain("BuildHeaderBar();"),
                "[125차 후속37] BuildHeaderBar 호출 복원 금지 — 헤더 소실 상태 유지가 정답.");

            // ② Figma 재정합 — 항목 폭 420(사용자 수정 -12px, 스크롤바 공간 — 내부 앵커 기반 -12px 전파).
            Assert.That(src, Does.Contain("private const float FacilityItemW = 420f"),
                "[125차 후속37] Figma 항목 폭 420×101(432→420 — 시설 정보 396/소개 264/재료행 396 전파).");

            // ③ 스크롤바 가시화 — Figma scrollbar-track 4×537 r2 #20262E / scrollbar-thumb 4×208 r2 #58A6FF 실측.
            Assert.That(src, Does.Contain("\"ItemScrollbarTrack\""),
                "[125차 후속37] 스크롤바 트랙 요소 — Figma scrollbar-track 반영.");
            Assert.That(src, Does.Contain("\"ItemScrollbarThumb\""),
                "[125차 후속37] 스크롤바 썸 요소 — Figma scrollbar-thumb 반영.");
            Assert.That(src, Does.Contain("new StyleColor(ScrollTrackCol)"),
                "[125차 후속37] 트랙 fill #20262E(Figma 실측 — 썸은 Accent #58A6FF 동일 실측).");
            Assert.That(src, Does.Contain("private void UpdateItemScrollbar()"),
                "[125차 후속37] 썸 실측 동기화 경로(content/viewport 비율 — 레이아웃 미확정 안전).");
        }
    }
}
