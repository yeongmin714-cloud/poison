using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests
{
    /// <summary>
    /// [104차 F1/F4] 건설 홀로그램 배치 시스템 + F4 레이아웃 교정 검증.
    /// EditMode 안전 — 런타임 오브젝트 생성 금지, 소스 문자열 매칭 위주
    /// (ConstructionManagementFigmaTests 동일 패턴: QA 서브에이전트 실행 판정).
    /// </summary>
    public sealed class ConstructionHologramTests
    {
        private const string HologramPath = "Assets/Scripts/Systems/ConstructionHologram.cs";
        private const string FacilityPath = "Assets/Scripts/Systems/FacilityPropBuilder.cs";
        private const string CommandMarkerPath = "Assets/Scripts/Systems/CommandMarker.cs";
        private const string RtsPath = "Assets/Scripts/Systems/RTSCommandSystem.cs";
        private const string WindowPath = "Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs";

        private static string Read(string path) => System.IO.File.ReadAllText(path);

        // ═══════════════ [104차 F1] 홀로그램 상태머신/게이트 ═══════════════

        [Test]
        public void F1_Hologram_Singleton_And_PlacementGate()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("public static ConstructionHologram Instance"), "싱글턴 Instance.");
            Assert.That(src, Does.Contain("public static bool IsPlacementActive"), "배치 활성 게이트 — CommandMarker/RTSCommandSystem 소비.");
        }

        [Test]
        public void F1_Hologram_StateMachine_Modes()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("enum PlacementMode"), "상태머신 enum.");
            Assert.That(src, Does.Contain("Inactive"), "상태: Inactive.");
            Assert.That(src, Does.Contain("Following"), "상태: Following (마우스 추적).");
            Assert.That(src, Does.Contain("Confirmed"), "상태: Confirmed (확정 펄스).");
        }

        [Test]
        public void F1_Hologram_Confirm_Flow_Uses_ResolveTerritoryAt_TryPlace()
        {
            var src = Read(HologramPath);
            // 확정 위치 해석 — ResolveTerritoryAt → TryPlace 순서 계약
            int resolve = src.IndexOf("ResolveTerritoryAt");
            int tryPlace = src.IndexOf("TryPlace(territoryId, _blueprintId, pos)");
            Assert.That(resolve, Is.GreaterThan(0), "확정 위치 영지 판정 — ResolveTerritoryAt.");
            Assert.That(tryPlace, Is.GreaterThan(resolve), "영지 판정 후 TryPlace 실행.");
        }

        [Test]
        public void F1_Hologram_Tracking_Uses_TerrainHeight()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("GetHeightAt"), "지형 높이 추적 — TerrainGenerator.GetHeightAt.");
            Assert.That(src, Does.Contain("LateUpdate"), "마우스 xz 추적 — LateUpdate.");
        }

        [Test]
        public void F1_Hologram_Valid_Invalid_Tint_Colors()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("new Color(0.35f, 0.9f, 1f)"), "유효 틴트 — cyan(0.35,0.9,1).");
            Assert.That(src, Does.Contain("new Color(1f, 0.25f, 0.25f)"), "무효 틴트 — red(1,0.25,0.25).");
        }

        [Test]
        public void F1_Hologram_StructureCompleted_Destroy_Contract()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("StructureCompleted"), "StructureCompleted 이벤트 구독 — 완성 시 파괴 계약.");
            Assert.That(src, Does.Contain("OnStructureCompletedForDestroy"), "완성 파괴 핸들러.");
        }

        [Test]
        public void F1_Hologram_Input_Cooldown_025s()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("0.25f"), "입력 쿨다운 0.25s — 버튼 클릭 직후 즉시 확정 방지.");
        }

        [Test]
        public void F1_Hologram_Escape_Cancels_Placement()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("KeyCode.Escape"), "Esc 취소.");
            Assert.That(src, Does.Contain("CancelPlacement"), "취소 메서드.");
        }

        [Test]
        public void F1_Hologram_Recipe_Identity_With_FacilityPropBuilder()
        {
            var src = Read(HologramPath);
            var facility = Read(FacilityPath);
            // 레시피 동일성 — 시설 4종 빌더 존재(FacilityPropBuilder 프라이빗 — 소스 문자열 매칭)
            Assert.That(facility, Does.Contain("BuildCookingStation"), "요리 레시피.");
            Assert.That(facility, Does.Contain("BuildForgeStation"), "장비 레시피.");
            Assert.That(facility, Does.Contain("BuildBarracksStation"), "병사배치 레시피.");
            Assert.That(facility, Does.Contain("BuildAlchemyStation"), "연금술 레시피.");
            // 홀로그램도 동일 레시피 참조 — [104차 수리2] CreatePart 복제 대신 FacilityPropBuilder와
            // 동일한 BlueprintData def 참조 경로(레시피 동일성 = 동일 BlueprintDef 소비).
            Assert.That(src, Does.Contain("BlueprintData.TryGet(blueprintId, out var def)"),
                "홀로그램 레시피 참조 — FacilityPropBuilder와 동일 BlueprintData def 경로.");
            Assert.That(facility, Does.Contain("BlueprintData.TryGet(blueprintId, out var def)"),
                "시설 레시피 참조 — 동일 def 경로(레시피 동일성).");
        }

        // ═══════════════ [104차 F1] RTS 게이트 ═══════════════

        [Test]
        public void F1_CommandMarker_Gate_Is_First_Line()
        {
            var src = Read(CommandMarkerPath);
            // [104차 수리2] 의도="사이드이펙트 전 게이트" — 문자 거리 대신 라인 순서 판정으로 갱신.
            var lines = src.Replace("\r", "").Split('\n');
            int spawnLine = -1, gateLine = -1, firstEffectLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (spawnLine < 0 && lines[i].Contains("public static CommandMarker Spawn")) spawnLine = i;
                if (gateLine < 0 && lines[i].Contains("if (ConstructionHologram.IsPlacementActive) return null;")) gateLine = i;
                // Spawn 본문 이후의 첫 사이드이펙트 라인(오브젝트 생성/파괴)
                if (spawnLine >= 0 && i > spawnLine && firstEffectLine < 0 &&
                    (lines[i].Contains("GameObject.CreatePrimitive") || lines[i].Contains("Instantiate(") || lines[i].Contains("Destroy(")))
                    firstEffectLine = i;
            }
            Assert.That(spawnLine, Is.GreaterThan(0), "CommandMarker.Spawn 존재.");
            Assert.That(gateLine, Is.GreaterThan(spawnLine), "Spawn 게이트 존재.");
            Assert.That(firstEffectLine, Is.GreaterThan(gateLine), "게이트가 메서드 내 첫 사이드이펙트 라인보다 앞 — 조기 리턴.");
        }

        [Test]
        public void F1_RTSCommandSystem_Gate_EarlyReturn()
        {
            var src = Read(RtsPath);
            int cmdIdx = src.IndexOf("public void IssueRightClickCommand");
            int gateIdx = src.IndexOf("if (ConstructionHologram.IsPlacementActive) return;");
            Assert.That(cmdIdx, Is.GreaterThan(0), "IssueRightClickCommand 존재.");
            Assert.That(gateIdx, Is.GreaterThan(cmdIdx), "명령 진입점 게이트 존재.");
            Assert.That(gateIdx - cmdIdx, Is.LessThan(200), "조기 리턴 게이트.");
        }

        // ═══════════════ [104차 F1] UI 연결 ═══════════════

        [Test]
        public void F1_Window_Hooks_BeginPlacement_And_Close()
        {
            var src = Read(WindowPath);
            Assert.That(src, Does.Contain("ConstructionHologram.BeginPlacement"), "UI → 홀로그램 진입.");
            Assert.That(src, Does.Contain("위치를 선택하세요 — 클릭 확정 / Esc 취소"), "배치 시작 안내 토스트.");
            Assert.That(src, Does.Contain("ConstructionHologram.ToastRequested"), "토스트 브리지 구독.");
        }

        // ═══════════════ [104차 F4] Figma 레이아웃 교정 ═══════════════

        [Test]
        public void F4_Layout_Corrections_5_Items()
        {
            var src = Read(WindowPath);
            Assert.That(src, Does.Contain("OverviewY = 90f"), "[104차 F4] 개요 y 92→90.");
            Assert.That(src, Does.Contain("ConditionY = 266f"), "[104차 F4] 조건 y 262→266.");
            Assert.That(src, Does.Contain("MaterialTableY = 378f"), "[104차 F4] 재료 표 y 394→378.");
            Assert.That(src, Does.Contain("matSub.style.top = 349.5f"), "[104차 F4] matSub y 370→349.5.");
            Assert.That(src, Does.Contain("listHeader.style.top = 140f"), "[104차 F4] 목록 헤더행 y=140 신설.");
        }

        [Test]
        public void F4_MatSub_Right_Aligned_And_Text()
        {
            var src = Read(WindowPath);
            Assert.That(src, Does.Contain("공용 창고 기준 · 수량 단위: 개"), "[104차 F4] figma 239:846 텍스트.");
            Assert.That(src, Does.Contain("matSub.style.right = PanelPad"), "[104차 F4] 우측 정렬 — x=1376(패널 우측 24 여백).");
        }

        [Test]
        public void F4_List_Header_Row_Labels()
        {
            var src = Read(WindowPath);
            Assert.That(src, Does.Contain("건설할 시설 선택"), "[104차 F4] 헤더행 좌측 라벨.");
            Assert.That(src, Does.Contain("재료 수치: 보유 / 필요"), "[104차 F4] 헤더행 우측 라벨.");
        }

        // ═══════════════ [105차 A] 건설 상태 수리 6건 ═══════════════

        [Test]
        public void A105_PlaceGate_Is_Following_Only()
        {
            var src = Read(HologramPath);
            // [105차 A] 결함1 — 게이트는 Following(배치 중)에서만 활성. 구 판정(!= Inactive)은
            // Confirmed(시공 중)까지 활성으로 취급해 시공 45~150초 동안 이동 명령을 전면 차단했다.
            Assert.That(src, Does.Contain("public static bool IsPlacementActive => Instance != null && Instance._mode == PlacementMode.Following;"),
                "게이트 = Following 전용 — 시공 중 이동 명령 차단 폐기.");
            Assert.That(src, Does.Not.Contain("Instance._mode != PlacementMode.Inactive"),
                "구 판정(!= Inactive) 잔존 금지 — Confirmed 포함 게이트 재발 방지.");
        }

        [Test]
        public void A105_Confirmed_Record_Guards_Hologram_From_Other_Completions()
        {
            var src = Read(HologramPath);
            // [105차 A] 결함2 — Confirm 성공 시 확정 기록(blueprintId+pos) → 완료 이벤트 일치 시만 파괴.
            Assert.That(src, Does.Contain("_confirmedBlueprintId = _blueprintId;"), "확정 설계도 기록 — Confirm 성공 시점.");
            Assert.That(src, Does.Contain("_confirmedPos = pos;"), "확정 위치 기록 — Confirm 성공 시점.");
            Assert.That(src, Does.Contain("entry.blueprintId != _confirmedBlueprintId"), "완료 이벤트 설계도 일치 판정.");
            Assert.That(src, Does.Contain("(entry.pos - _confirmedPos).magnitude > 0.5f"), "완료 이벤트 위치 일치 판정 — ε 0.5m.");
            Assert.That(src, Does.Contain("[105차 A] 완료 이벤트 불일치 무시"), "불일치 무시 로그 마커.");
            // 기록 무효화 — 새 배치 시작(StartPlacementInternal)은 이전 확정 기록을 null로 리셋(2번째 Following 보호).
            int beginClear = src.IndexOf("_confirmedBlueprintId = null;");
            int confirmRecord = src.IndexOf("_confirmedBlueprintId = _blueprintId;");
            Assert.That(beginClear, Is.GreaterThan(0), "새 배치 시작 시 확정 기록 무효화.");
            Assert.That(beginClear, Is.LessThan(confirmRecord), "기록 무효화가 배치 시작 경로(Confirm 이전)에 위치.");
        }

        [Test]
        public void A105_Fail_Toast_Carries_TryPlace_Reason()
        {
            var src = Read(HologramPath);
            // [105차 A] 결함3 — 실패 토스트 = TryPlace 실제 사유(out result 전달). 고정 문구 폐기.
            Assert.That(src, Does.Contain("bool TryConfirmPlacement(Vector3 pos, out string result)"), "TryConfirmPlacement out result 시그니처.");
            Assert.That(src, Does.Contain("ToastRequested?.Invoke(placeResult);"), "토스트 = TryPlace 실제 사유.");
            Assert.That(src, Does.Not.Contain("배치할 수 없는 위치입니다"), "고정 문구 폐기 — 사유 비노출 재발 금지.");
            Assert.That(src, Does.Contain("placeResult.StartsWith(\"건설 시작\")"), "성공 판정 — '건설 시작' 접두어 유지.");
        }

        [Test]
        public void A105_Ghost_Colliders_Disabled()
        {
            var src = Read(HologramPath);
            // [105차 A] 결함4 — 고스트 프리미티브 콜라이더 전부 비활성(레이캐스트 자기 히트/클릭 가로챔 방지).
            Assert.That(src, Does.Contain("private void DisableGhostColliders()"), "콜라이더 비활성 헬퍼.");
            Assert.That(src, Does.Contain("GetComponentsInChildren<Collider>(true)"), "고스트 하위 콜라이더 전수 조회.");
            Assert.That(src, Does.Contain("col.enabled = false;"), "콜라이더 비활성 대입.");
            int build = src.IndexOf("BuildHologramPrimitives(def);");
            int disable = src.IndexOf("DisableGhostColliders();");
            Assert.That(disable, Is.GreaterThan(build), "프리미티브 생성 이후 비활성 호출 — StartPlacementInternal 경로.");
        }

        [Test]
        public void A105_Escape_Ignored_While_Confirmed()
        {
            var src = Read(HologramPath);
            // [105차 A] 결함6 — Esc 취소는 Following에서만. Confirmed(시공 중)는 무시(고스트 유지).
            int escIdx = src.IndexOf("Input.GetKeyDown(KeyCode.Escape)");
            int gateIdx = src.IndexOf("if (_mode == PlacementMode.Following)", escIdx);
            Assert.That(gateIdx, Is.GreaterThan(escIdx), "Esc 처리 직후 Following 전용 게이트.");
            Assert.That(src, Does.Contain("[105차 A] 시공 중 — Esc 무시(고스트 유지)"), "시공 중 Esc 무시 로그.");
            Assert.That(src, Does.Contain("_escIgnoreLogged"), "무시 로그 1회성 플래그 — 매 프레임 스팸 금지.");
        }

        [Test]
        public void A105_Demolish_Resets_Restaurant_Flag_And_Destroys_Props()
        {
            var cm = Read("Assets/Scripts/Systems/ConstructionManager.cs");
            // [105차 A] 결함5 — 해체 후 잔재 정리: 음식점 플래그 리셋 + 실물 프롭 파괴.
            Assert.That(cm, Does.Contain("TryFindRestaurantVillage(s.pos, out var restaurantNation, out var restaurantVillageIndex)"), "해체 구조물 마을 매칭.");
            Assert.That(cm, Does.Contain("VillageDrugState.SetRestaurantBuilt(restaurantNation, restaurantVillageIndex, false)"), "음식점 플래그 리셋 — 재건설 허용.");
            Assert.That(cm, Does.Contain("RestaurantPropBuilder.TryDestroyProp(s.structureId);"), "음식점 실물 파괴 호출.");
            Assert.That(cm, Does.Contain("FacilityPropBuilder.TryDestroyProp(s.structureId);"), "시설 실물 파괴 호출.");
            int removeAt = cm.IndexOf("_structures.RemoveAt(i);");
            int flagReset = cm.IndexOf("VillageDrugState.SetRestaurantBuilt(restaurantNation, restaurantVillageIndex, false)");
            Assert.That(flagReset, Is.GreaterThan(removeAt), "RemoveAt 이후 정리 로직 — 해체 경로 내 위치.");
        }

        [Test]
        public void A105_TryDestroyProp_Contract_RuntimeOnly()
        {
            var restaurant = Read("Assets/Scripts/Systems/RestaurantPropBuilder.cs");
            var facility = Read(FacilityPath);
            // [105차 A] 결함5 — TryDestroyProp 계약: public static bool, 캐시 제거+Destroy, 런타임 전용.
            Assert.That(restaurant, Does.Contain("public static bool TryDestroyProp(string structureId)"), "음식점 TryDestroyProp 시그니처.");
            Assert.That(facility, Does.Contain("public static bool TryDestroyProp(string structureId)"), "시설 TryDestroyProp 시그니처.");
            Assert.That(restaurant, Does.Contain("if (!Application.isPlaying) return false;"), "음식점 — 런타임 전용 게이트(EditMode no-op).");
            Assert.That(facility, Does.Contain("if (!Application.isPlaying) return false;"), "시설 — 런타임 전용 게이트(EditMode no-op).");
            Assert.That(restaurant, Does.Contain("_props.Remove(structureId);"), "음식점 — 캐시 제거.");
            Assert.That(facility, Does.Contain("_props.Remove(structureId);"), "시설 — 캐시 제거.");
        }

        // ═══════════════ [170차] 홀로그램 실물 형태 패리티 ═══════════════

        [Test]
        public void P170_Hologram_Shape_Shares_RealBuilder_Visuals()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("RestaurantPropBuilder.BuildHologramVisual"), "음식점 — 실물 빌더 시각 공유(단일 소스).");
            Assert.That(src, Does.Contain("FacilityPropBuilder.BuildHologramVisual"), "시설 — 실물 빌더 시각 공유(단일 소스).");
            Assert.That(src, Does.Contain("FacilityPropBuilder.IsFacilityBlueprintId(def.id)"), "시설 5종 분기.");
            Assert.That(src, Does.Contain("RestaurantPropBuilder.RestaurantBlueprintId"), "음식점 분기.");
        }

        [Test]
        public void P170_Fallback_Placeholder_Kept_And_Footprint_Bar_Maintained()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("Hologram_Footprint"), "부지 바닥판 유지 계약(실물 형태 경로 포함 전 경로).");
            Assert.That(src, Does.Contain("Hologram_Pillar"), "시각 소스 없는 설계도 폴백 자리표시자 유지.");
            Assert.That(src, Does.Contain("[170차 P1] 실물 형태 패리티"), "패리티 경로 마커.");
        }

        [Test]
        public void P170_HologramVisual_NoInteraction_NoPlaceholder()
        {
            var facility = Read(FacilityPath);
            int holo = facility.IndexOf("public static GameObject BuildHologramVisual");
            int strip = facility.IndexOf("private static void StripColliders");
            Assert.That(holo, Is.GreaterThan(0), "시설 홀로그램 진입점 존재.");
            Assert.That(strip, Is.GreaterThan(holo), "StripColliders가 홀로그램 진입점 뒤에 위치.");
            var region = facility.Substring(holo, strip - holo);
            Assert.That(region, Does.Not.Contain("AddComponent"), "홀로그램 경로 — 상호작용/플레이스홀더 배제.");
            Assert.That(region, Does.Not.Contain("FacilityOpenRequested"), "홀로그램 경로 — 개방 이벤트 배제.");
            foreach (var part in new[] { "BuildCookingStation", "BuildForgeStation", "BuildBarracksStation", "BuildAlchemyStation", "BuildTrainingHallStation" })
                Assert.That(region, Does.Contain(part), "홀로그램도 실물과 동일 파트 빌더 — 형태 패리티: " + part);
        }

        // ═══════════════ [203차] 홀로그램 에셋 통합(Unity Hologram VFX) ═══════════════

        [Test]
        public void M203_Material_Chain_ResourceThenShaderThenLit()
        {
            var src = Read(HologramPath);
            // [203차] 3단 폴백 체인 순서 핀 — 1차 Resources → 2차 Shader.Find → 3차 기존 URP Lit.
            int resourcesIdx = src.IndexOf("Resources.Load<Material>");
            int shaderIdx = src.IndexOf("Shader.Find(\"Shader Graphs/Template_Hologram\")");
            int litIdx = src.IndexOf("CreateLitMaterial");
            Assert.That(resourcesIdx, Is.GreaterThan(0), "1차 — Resources.Load<Material> 로드 경로 존재.");
            Assert.That(shaderIdx, Is.GreaterThan(resourcesIdx), "2차 — Shader.Find 폴백이 Resources 뒤에 위치.");
            Assert.That(litIdx, Is.GreaterThan(shaderIdx), "3차 — CreateLitMaterial URP Lit 폴백이 Shader.Find 뒤에 위치.");
            Assert.That(src, Does.Contain("Holograms/ConstructionHologram_Mat"), "Resources 리소스 경로 문자열.");
        }

        [Test]
        public void M203_UpdateTint_Drives_Hologram_Color_First()
        {
            var src = Read(HologramPath);
            // UpdateTint 영역(substring — "private void UpdateTint"부터 다음 메서드 경계까지).
            int start = src.IndexOf("private void UpdateTint");
            Assert.That(start, Is.GreaterThan(0), "UpdateTint 메서드 존재.");
            int next = src.IndexOf("private void Update()", start);
            Assert.That(next, Is.GreaterThan(start), "UpdateTint 다음 메서드 경계 존재.");
            var region = src.Substring(start, next - start);
            int holoIdx = region.IndexOf("_Hologram_Color");
            int texIdx = region.IndexOf("_Texture_Tint_Color");
            int baseIdx = region.IndexOf("_BaseColor");
            Assert.That(holoIdx, Is.GreaterThan(0), "UpdateTint — _Hologram_Color 드라이브 존재.");
            Assert.That(texIdx, Is.GreaterThan(0), "UpdateTint — _Texture_Tint_Color 드라이브 존재.");
            Assert.That(baseIdx, Is.GreaterThan(holoIdx), "자산 속성 우선 — _Hologram_Color가 _BaseColor 폴백보다 앞.");
            Assert.That(src, Does.Contain("HologramTintAlpha"), "틴트 알파 상수 존재.");
            Assert.That(src, Does.Contain("HologramTextureAlpha"), "텍스처 틴트 알파 상수 존재.");
        }

        [Test]
        public void M203_Queue_And_Transparency_Contract_Kept()
        {
            var src = Read(HologramPath);
            // [203차] 반투명 queue 3000 계약 — 자산 경로 포함 전 경로 유지.
            Assert.That(src, Does.Contain("mat.renderQueue = 3000;"), "renderQueue 3000 계약.");
            Assert.That(src, Does.Contain("HologramTintAlpha = 0.35f"), "틴트 알파 리터럴 핀.");
            Assert.That(src, Does.Contain("HologramTextureAlpha = 0.2f"), "텍스처 틴트 알파 리터럴 핀.");
        }

        [Test]
        public void M203_Vfx_Projection_Ray_Guarded_Spawn()
        {
            var src = Read(HologramPath);
            Assert.That(src, Does.Contain("Resources.Load<GameObject>(\"Holograms/HologramVFX\")"), "VFX 프리팹 Resources 로드.");
            Assert.That(src, Does.Contain("Hologram_VFX"), "VFX 오브젝트 이름 마커.");
            Assert.That(src, Does.Contain("Mathf.Max(1f, def.footprintRadius / 2.5f)"), "footprint 기반 스케일 핀.");
            // BuildHologramPrimitives 메서드 영역 내 + shape 성공 경로 배치(폴백 자리표시자 뒤가 아님).
            int buildStart = src.IndexOf("private void BuildHologramPrimitives");
            int buildEnd = src.IndexOf("private void DisableGhostColliders", buildStart);
            Assert.That(buildStart, Is.GreaterThan(0), "BuildHologramPrimitives 존재.");
            Assert.That(buildEnd, Is.GreaterThan(buildStart), "BuildHologramPrimitives 다음 메서드 경계 존재.");
            var region = src.Substring(buildStart, buildEnd - buildStart);
            int loadIdx = region.IndexOf("Resources.Load<GameObject>");
            int shapeIdx = region.IndexOf("Hologram_Shape");
            Assert.That(loadIdx, Is.GreaterThan(0), "VFX 로드 — BuildHologramPrimitives 메서드 영역 내.");
            Assert.That(shapeIdx, Is.GreaterThan(0), "shape 성공 경로 존재.");
            Assert.That(loadIdx, Is.GreaterThan(shapeIdx), "VFX 스폰이 shape 성공 경로(Hologram_Shape) 뒤에 위치 — 폴백 경로 미적용.");
        }

        [Test]
        public void R203_Resource_Copies_Exist()
        {
            // [203차] 에셋 통합 증명 — Resources 사본 실존 어서션.
            Assert.That(System.IO.File.Exists("Assets/Resources/Holograms/ConstructionHologram_Mat.mat"), Is.True,
                "머티리얼 리소스 사본 존재.");
            Assert.That(System.IO.File.Exists("Assets/Resources/Holograms/HologramVFX.prefab"), Is.True,
                "VFX 프리팹 리소스 사본 존재.");
        }

        [Test]
        public void R203_Original_Assets_Untouched()
        {
            // [203차] 데모 무손상 증명 — 원본 에셋/폴더 그대로 존재.
            Assert.That(System.IO.File.Exists("Assets/Holograms/Materials/Examples/Basic/Grid_Hologram_Empty.mat"), Is.True,
                "원본 베이스 머티리얼 무손상.");
            Assert.That(System.IO.Directory.Exists("Assets/Holograms/Materials/Examples/Basic"), Is.True,
                "원본 데모 폴더 그대로 존재.");
        }
    }
}
