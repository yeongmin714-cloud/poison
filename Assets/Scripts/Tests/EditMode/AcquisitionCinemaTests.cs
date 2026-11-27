using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [130차] 동물의 숲식 획득 연출 — 계약 테스트.
    /// 진실 출처: docs/130_ACQUISITION_CINEMA_PLAN.md §2 설계 결정·§3 Phase.
    ///  · EditMode 규약: 런타임 인스턴스화 없이 순수 정적 판정 + File.ReadAllText 문자열 단정만 수행
    ///    (PlayerActionAnimContractTests 129차 소스계약 선례 준용).
    ///  · [149차 P2] 손끝 GLB 계약 → 얼굴 위 부유 GLB(전 종류) 계약으로 갱신 — docs/149_TEST15_FARM_ACQ_BUILD_RESTAURANT_PLAN.md §2.
    /// </summary>
    public class AcquisitionCinemaTests
    {
        // ===== [130차] 한국어 객체 조사(을/를) 종성 판정 — 결정론 =====

        [TestCase("나무", "를")]
        [TestCase("메기", "를")]
        [TestCase("약초", "를")]
        [TestCase("생선", "을")]
        [TestCase("돌", "을")]
        public void KoreanObjectParticle_BatchimContract_130(string noun, string expected)
        {
            Assert.That(AcquisitionCinema.KoreanObjectParticle(noun), Is.EqualTo(expected),
                $"[130차] '{noun}' 종성 판정 → '{expected}'.");
        }

        [Test]
        public void KoreanObjectParticle_NonKorean_Fallback_130()
        {
            Assert.That(AcquisitionCinema.KoreanObjectParticle(null), Is.EqualTo("을(를)"),
                "[130차] null → 보수 표기.");
            Assert.That(AcquisitionCinema.KoreanObjectParticle(""), Is.EqualTo("을(를)"),
                "[130차] 빈 문자열 → 보수 표기.");
        }

        // ===== [130차] 획득 문안 포맷 — kind별 동사 + x{count} =====

        [Test]
        public void FormatAcquiredText_Gathering_130()
        {
            Assert.That(AcquisitionCinema.FormatAcquiredText(
                HarvestResultBridge.HarvestKind.Gathering, "나무", 2),
                Is.EqualTo("나무 x2를 획득했다!"), "[130차] 채집/채광/벌목/농경 문안.");
        }

        [Test]
        public void FormatAcquiredText_Fishing_130()
        {
            Assert.That(AcquisitionCinema.FormatAcquiredText(
                HarvestResultBridge.HarvestKind.Fishing, "메기", 1),
                Is.EqualTo("메기를 낚았다!"), "[130차] 낚시 문안 — count 1은 수량 미표기.");
        }

        [Test]
        public void FormatAcquiredText_Woodcutting_130()
        {
            Assert.That(AcquisitionCinema.FormatAcquiredText(
                HarvestResultBridge.HarvestKind.Woodcutting, "목재", 3),
                Is.EqualTo("목재 x3를 획득했다!"), "[130차] 벌목 문안.");
        }

        // ===== [130차] 손끝 GLB 매핑 — 결정론(입력 같으면 경로 항상 같음) =====

        [Test]
        public void Resolver_HerbYakcho_DefaultGreen_130()
        {
            var item = new PlayerInventory.ItemData { id = "herb_yakcho", displayName = "약초" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item),
                Is.EqualTo("Assets/Resources/Models/UserProvided/herb_green.glb"),
                "[130차] 색 추정 불가 약초 → herb_green 기본.");
            Assert.That(AcquisitionItemVisualResolver.ResolveResourcesPath(item),
                Is.EqualTo("Models/UserProvided/herb_green"),
                "[130차] Resources 폴백 키 동일 매핑.");
        }

        [Test]
        public void Resolver_HerbColorKeyword_130()
        {
            var item = new PlayerInventory.ItemData { id = "herb_yakcho", displayName = "치유초", description = "빨간 열매빛 약초." };
            Assert.That(AcquisitionItemVisualResolver.ResolveHerbGlbKey(item.id, item.displayName, item.description),
                Is.EqualTo("herb_red"), "[130차] 설명 키워드 '빨간' → herb_red.");
        }

        [Test]
        public void Resolver_MatWood_TemporaryStake_130()
        {
            var item = new PlayerInventory.ItemData { id = "mat_wood", displayName = "목재" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item),
                Is.EqualTo("Assets/Resources/Models/UserProvided/stake.glb"),
                "[130차 임시] 목재 단품 GLB 부재 → stake.glb 폴백(사용자 GLB 준비 시 매핑 1줄 교체).");
        }

        [Test]
        public void Resolver_WeaponWoodSword_130()
        {
            var item = new PlayerInventory.ItemData { id = "weapon_sword_wood", displayName = "목검" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item),
                Is.EqualTo("Assets/Resources/Models/UserProvided/wood_sword.glb"),
                "[130차] weapon_{type}_{tier} → {tier}_{type}.glb.");
        }

        [Test]
        public void Resolver_UnknownItem_Null_130()
        {
            var item = new PlayerInventory.ItemData { id = "totally_unknown_item", displayName = "미상" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item), Is.Null,
                "[130차] 미상 id → null(연출 중단 없음 — GLB 생략 계약).");
            Assert.That(AcquisitionItemVisualResolver.ResolveResourcesPath(item), Is.Null,
                "[130차] 미상 id Resources 폴백 → null.");
        }

        [Test]
        public void Resolver_FishGlbPrefix_EditorOnlyPath_130()
        {
            var item = new PlayerInventory.ItemData { id = "fish_glb_붕어", displayName = "붕어" };
            string path = AcquisitionItemVisualResolver.ResolveEditorAssetPath(item);
            Assert.That(path, Does.StartWith("Assets/새로운 glb/crops-fish/fish-"),
                "[130차] 물고기 → FishCatalog GLB 직결(에디터 전용 경로).");
            Assert.That(path, Does.EndWith(".glb"), "[130차] GLB 확장자 계약.");
            Assert.That(AcquisitionItemVisualResolver.ResolveResourcesPath(item), Is.Null,
                "[130차] crops-fish GLB는 Resources 밖 — 폴백 후보 없음.");
        }

        // ===== [144차 P9-ACQ] 매핑 보강(GLB 실측 존재분) + 폴백 계약 =====

        [Test]
        public void Resolver_FruitApple_RealGlbMapping_144()
        {
            var item = new PlayerInventory.ItemData { id = "fruit_apple", displayName = "사과" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item),
                Is.EqualTo("Assets/새로운 glb/crops-fish/crop-apple.glb"),
                "[144차 P9-ACQ] fruit_apple → crop-apple.glb 실측 매핑(CropCatalog 경로 재사용).");
            Assert.That(AcquisitionItemVisualResolver.ResolveResourcesPath(item), Is.Null,
                "[144차 P9-ACQ] crops-fish GLB는 Resources 밖 — Resources 폴백 후보 없음.");
            Assert.That(System.IO.File.Exists(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item)), Is.True,
                "[144차 P9-ACQ] 위조 경로 금지 — 매핑된 GLB 파일 실측 존재.");
        }

        [Test]
        public void Resolver_FlowerWild_RealGlbMapping_144()
        {
            var item = new PlayerInventory.ItemData { id = "flower_wild", displayName = "들꽃" };
            Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item),
                Is.EqualTo(AcquisitionItemVisualResolver.FlowerWildEditorPath),
                "[144차 P9-ACQ] flower_wild → nature 데이지 클럼프 GLB(결정론 단일 경로).");
            Assert.That(AcquisitionItemVisualResolver.ResolveResourcesPath(item), Is.Null,
                "[144차 P9-ACQ] nature GLB도 Resources 밖 — Resources 폴백 후보 없음.");
            Assert.That(System.IO.File.Exists(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item)), Is.True,
                "[144차 P9-ACQ] 위조 경로 금지 — 매핑된 GLB 파일 실측 존재.");
        }

        [Test]
        public void Resolver_WoodDagger_GlbExists_144()
        {
            var item = new PlayerInventory.ItemData { id = "weapon_dagger_wood", displayName = "나무 단도" };
            Assert.That(AcquisitionItemVisualResolver.ResolveWeaponGlbKey(item.id), Is.EqualTo("wood_dagger"),
                "[144차 P9-ACQ] wood_dagger.glb 실측 존재 → MissingWeaponGlbKeys 제외 해제.");
            Assert.That(System.IO.File.Exists(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item)), Is.True,
                "[144차 P9-ACQ] 매핑된 GLB 파일 실측 존재(UserProvided/wood_dagger.glb).");
        }

        [Test]
        public void Resolver_NoGlbIds_StayNull_144()
        {
            // GLB 실측 부재 id — 매핑 null 유지(위조 경로 금지) → 호출처 폴백(아이콘+텍스트)이 담당.
            foreach (string id in new[] { "iron_ore", "mat_silver_ore", "mat_gold_ore", "mat_mythril_ore", "meat_rabbit", "herb_seed_red" })
            {
                var item = new PlayerInventory.ItemData { id = id };
                Assert.That(AcquisitionItemVisualResolver.ResolveEditorAssetPath(item), Is.Null,
                    $"[144차 P9-ACQ] {id} — GLB 실측 부재 → 매핑 null(폴백 담당, 위조 경로 금지).");
            }
        }

        [Test]
        public void Cinema_GlbFallback_SourceContract_144()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            // [149차 P2] 폴백은 얼굴 위 부유로 승계 — 메서드명 갱신(CreateFloatFallback), 구 계약 정신 유지.
            Assert.That(src, Does.Contain("CreateFloatFallback"),
                "[144차 P9-ACQ→149차] GLB 미매핑 폴백 구성 존재 — 조용한 생략 계약 폐기(143차 감사 §2-1①).");
            Assert.That(src, Does.Contain("ProceduralIconGenerator.GetOrCreateIcon"),
                "[144차 P9-ACQ] 폴백 아이콘 = 기존 절차적 아이콘 서비스 재사용.");
            Assert.That(src, Does.Contain("TextMesh"),
                "[144차 P9-ACQ] 폴백 텍스트(아이템명) 존재 — 아이콘 없는 id도 이름 표시(생략 금지).");
            Assert.That(src, Does.Not.Contain("if (visual == null) return;"),
                "[144차 P9-ACQ] 'visual==null → 생략' 구분기 폐기 확인.");
        }

        // ===== [130차] 배선/폐기 소스 계약 =====

        [Test]
        public void CameraBeginUpperBody_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            // [142차 I] frontView 파라미터 추가(획득 연출 = 플레이어 정면 촬영 — 후면 촬영 결함 수리) — 계약 갱신.
            Assert.That(src, Does.Contain("public static void BeginUpperBody(Component target, string displayName, bool frontView = false)"),
                "[130차] 상반신 클로즈업 진입점 존재([142차 I] frontView 확장).");
            Assert.That(src, Does.Contain("UpperBodyFrameH"),
                "[130차] 상반신 프레임 상수 존재(전신 1.8m 계약 무변경 — 별도 상수).");
        }

        [Test]
        public void DialoguePanelOpenForAcquisition_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");
            Assert.That(src, Does.Contain("public static void OpenForAcquisition(Component target, string line)"),
                "[130차] 획득 전용 패널 진입점 존재.");
        }

        [Test]
        public void HarvestResultUTK_BootstrapRetired_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HarvestResultUTK.cs");
            Assert.That(src, Does.Not.Contain("RuntimeInitializeOnLoadMethod"),
                "[130차] 결과 창 자가부트 폐기 — 소비자를 AcquisitionCinema로 교체(클래스 코드 잔존).");
        }

        [Test]
        public void CinemaBootstrap_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("RuntimeInitializeOnLoadMethod"),
                "[130차] AcquisitionCinema 자가부트 존재(HarvestResultUTK 선례 — 전 씬 소비 보장).");
            Assert.That(src, Does.Contain("BeginUpperBody"),
                "[130차] 상반신 카메라 오케스트레이션 존재.");
            Assert.That(src, Does.Contain("OpenForAcquisition"),
                "[130차] 획득 패널 오케스트레이션 존재.");
        }

        [Test]
        public void SoldierAutoWork_NoCinemaPublish_SourceContract_130()
        {
            // 병사 자동작업 미연출(사용자 확정) — 병사 경로 4종 전부 publish를 만들지 않는다.
            // ① 채굴 ResourceNode.TryAutoMine ② 농경(밭) FarmingSystem.Harvest ③ 채집 HerbPickup.TryAutoGather.
            var nodeSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/ResourceNode.cs");
            int begin = nodeSrc.IndexOf("public bool TryAutoMine");
            Assert.That(begin, Is.GreaterThan(0), "[130차] TryAutoMine(병사 경로) 존재.");
            int next = nodeSrc.IndexOf("/// <summary>", begin + 10);
            string body = next > begin ? nodeSrc.Substring(begin, next - begin) : nodeSrc.Substring(begin, 4000);
            Assert.That(body, Does.Not.Contain("HarvestResultBridge.Publish"),
                "[130차] 병사 자동 채굴 → 연출 publish 없음(인벤/저장고 반영만 — 사용자 확정).");

            var farmSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmingSystem.cs");
            Assert.That(farmSrc, Does.Not.Contain("HarvestResultBridge"),
                "[130차 후속] 병사 자동 농경(밭 수확) → 연출 publish 없음.");

            var herbSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/HerbPickup.cs");
            int autoBegin = herbSrc.IndexOf("public bool TryAutoGather");
            Assert.That(autoBegin, Is.GreaterThan(0), "[130차 후속] TryAutoGather(병사/미션 경로) 존재.");
            string autoBody = herbSrc.Substring(autoBegin);
            Assert.That(autoBody, Does.Not.Contain("HarvestResultBridge.Publish"),
                "[130차 후속] 병사/미션 자동 채집 → 연출 publish 없음(플레이어 채널 완료 콜백만 publish).");
        }

        [Test]
        public void Cinema_FloatAboveFace_SourceContract_149()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("AttachFloatItem"), "[149차 P2] 얼굴 위 부유 GLB 부착 메서드.");
            Assert.That(src, Does.Not.Contain("AttachHandItem"), "[149차 P2] 손끝 부착 대체 — 구 메서드 소멸.");
            Assert.That(src, Does.Not.Contain("_handItem"), "[149차 P2] 손끝 필드 소멸.");
            Assert.That(src, Does.Not.Contain("GetBoneTransform"), "[149차 P2] 손본 참조 소멸(부유는 루트 오프셋).");
            Assert.That(src, Does.Contain("FloatAnchorUpM"), "[149차 P2] 부유 앵커 높이 상수(얼굴 위 — 클로즈업 프레임 내).");
            Assert.That(src, Does.Contain("Mathf.Sin"), "[149차 P2] 부유 진동(사인).");
            Assert.That(src, Does.Contain("FloatFadeSeconds"), "[149차 P2] 종료 페이드 계약.");
            Assert.That(src, Does.Contain("Quaternion.Slerp"), "[149차 P2] 부드러운 빌보드.");
            Assert.That(src, Does.Contain("Camera.main"), "[149차 P2] 카메라 추적 빌보드.");
            Assert.That(src, Does.Contain("AcquisitionItemVisualResolver.LoadVisual"), "[149차 P2] GLB 경로 단일소스 재사용.");
            Assert.That(src, Does.Contain("[149차] 획득 GLB resolver"), "[149차 P2] resolver 진단 로그(표출 실패 원인 추적용).");
            Assert.That(src, Does.Contain("Object.Destroy(_floatItem)"), "[149차 P2] 세션 종료 정리(멱등).");

            // 연출 순서 계약 — StartPresentation 본문 내 패널 → 부유 GLB → 클로즈업.
            int start = src.IndexOf("internal static void StartPresentation");
            Assert.That(start, Is.GreaterThan(0), "[130차] 연출 시작 메서드 존재.");
            string body = src.Substring(start, System.Math.Min(src.Length - start, 4000));
            int panel = body.IndexOf("OpenForAcquisition");
            int floatItem = body.IndexOf("AttachFloatItem");
            int cam = body.IndexOf("BeginUpperBody(player");
            Assert.That(panel, Is.GreaterThan(0).And.LessThan(floatItem),
                "[149차 P2] 연출 순서 — 획득 패널이 부유 GLB에 선행.");
            Assert.That(cam, Is.GreaterThan(floatItem),
                "[149차 P2] 연출 순서 — 부유 GLB가 클로즈업 카메라에 선행.");
        }

        [Test]
        public void Resolver_LoadVisualInstantiatesSceneInstance_SourceContract_150()
        {
            // [150차 P1] LoadVisual 반환은 씬 인스턴스 — 프리팹 에셋 직반환 시 AttachFloatItem의
            // SetParent가 "Transform resides in a Prefab asset" 로 차단되어 GLB가 부유하지 않았다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AcquisitionItemVisualResolver.cs");
            Assert.That(src, Does.Contain("Object.Instantiate"),
                "[150차 P1] LoadVisual은 에셋을 Instantiate해 반환(씬 인스턴스 계약).");
            Assert.That(src, Does.Contain("InstantiateSceneInstance"),
                "[150차 P1] 반환 시점 유일화 — 공통 반환 헬퍼 경유.");
            Assert.That(src, Does.Not.Contain("if (go != null) return go;"),
                "[150차 P1] 에셋 직반환 분기 소멸(모든 경로 Instantiate 경유).");
        }

        // ===== [153차 P4] 획득 연출 VFX — 빛기둥/헤일로/스파클/등장 팝/환호 반짝 계약 =====
        // 진실 출처: docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md §2 + §3 P4(+§4 확정 결정 2·4번).

        [TestCase(0, 0.8f)]
        [TestCase(1, 1.0f)]
        [TestCase(2, 1.2f)]
        [TestCase(3, 1.4f)]
        [TestCase(4, 1.6f)]
        [TestCase(5, 1.8f)]
        public void RarityShaftLength_TierTable_Deterministic_153(int tier, float expectedM)
        {
            // 등급별 강약(사용자 확정 §4-2) — Common 0.8m + 0.2m×tier → Unique 1.8m. 결정론 표 판정.
            Assert.That(AcquisitionCinema.RarityShaftLength(tier), Is.EqualTo(expectedM).Within(0.0001f),
                $"[153차 P4] 빛기둥 길이 tier={tier} → {expectedM}m(Common 0.8 + 0.2×tier).");
        }

        [TestCase(0, 0.25f)]
        [TestCase(1, 0.35f)]
        [TestCase(2, 0.45f)]
        [TestCase(3, 0.55f)]
        [TestCase(4, 0.65f)]
        [TestCase(5, 0.75f)]
        public void RarityShaftAlpha_TierTable_Deterministic_153(int tier, float expected)
        {
            // 등급별 강약(사용자 확정 §4-2) — Common 0.25 + 0.1×tier → Unique 0.75. 결정론 표 판정.
            Assert.That(AcquisitionCinema.RarityShaftAlpha(tier), Is.EqualTo(expected).Within(0.0001f),
                $"[153차 P4] 빛기둥 알파 tier={tier} → {expected}(Common 0.25 + 0.1×tier).");
        }

        [Test]
        public void RarityShaft_MonotonicAndClamped_153()
        {
            // 단조 증가 — 티어↑ → 길이·알파 모두 증가(§4-2 계약).
            for (int tier = 0; tier < 5; tier++)
            {
                Assert.That(AcquisitionCinema.RarityShaftLength(tier + 1),
                    Is.GreaterThan(AcquisitionCinema.RarityShaftLength(tier)),
                    $"[153차 P4] 길이 단조 증가 tier {tier}→{tier + 1}.");
                Assert.That(AcquisitionCinema.RarityShaftAlpha(tier + 1),
                    Is.GreaterThan(AcquisitionCinema.RarityShaftAlpha(tier)),
                    $"[153차 P4] 알파 단조 증가 tier {tier}→{tier + 1}.");
            }
            // 클램프 결정론 — 범위 밖 입력도 경계 티어 표값 반환(0..5 고정 계약).
            Assert.That(AcquisitionCinema.RarityShaftLength(-1), Is.EqualTo(AcquisitionCinema.RarityShaftLength(0)),
                "[153차 P4] 음수 티어 → 티어 0 클램프.");
            Assert.That(AcquisitionCinema.RarityShaftLength(9), Is.EqualTo(AcquisitionCinema.RarityShaftLength(5)),
                "[153차 P4] 초과 티어 → 티어 5 클램프.");
            Assert.That(AcquisitionCinema.RarityShaftAlpha(-1), Is.EqualTo(AcquisitionCinema.RarityShaftAlpha(0)),
                "[153차 P4] 음수 티어 → 티어 0 클램프.");
            Assert.That(AcquisitionCinema.RarityShaftAlpha(9), Is.EqualTo(AcquisitionCinema.RarityShaftAlpha(5)),
                "[153차 P4] 초과 티어 → 티어 5 클램프.");
        }

        [Test]
        public void Cinema_FloatFx_SourceContract_153()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("AcqFloatFX"),
                "[153차 P4] 부유 GLB 주변 FX 그룹(pivot 자식) 존재.");
            Assert.That(src, Does.Contain("RarityShaftLength"),
                "[153차 P4] 빛기둥 길이 등급 표 헬퍼(public — 테스트 직접 판정 가능).");
            Assert.That(src, Does.Contain("RarityShaftAlpha"),
                "[153차 P4] 빛기둥 알파 등급 표 헬퍼.");
            Assert.That(src, Does.Contain("_fxRenderers"),
                "[153차 P4] FX 페이드 등록 별도 리스트(페이드 계약 ③).");
            Assert.That(src, Does.Contain("_fxBaseColors"),
                "[153차 P4] FX 페이드 기준색 별도 리스트.");
            Assert.That(src, Does.Contain("EquipmentRarityData.GetRarityColor"),
                "[153차 P4] 희귀도 색 단일 진실원 소비(HarvestResultUTK 선례 — 신규 색 매핑 표 금지).");
            Assert.That(src, Does.Contain("HarvestResultBridge.LatestRarity"),
                "[153차 P4] 등급 진실원 소비(ItemRarity 6단계).");
            Assert.That(src, Does.Contain("EnableKeyword(\"_EMISSION\")"),
                "[153차 P4] 아이템 자체 이징 — 인스턴스 머티리얼 발광(SpecialEffectsController 기법).");
            Assert.That(src, Does.Contain("playOnAwake = false"),
                "[153차 P4] 스파클 playOnAwake=false — 발화 소유권 명시화(소유 메서드가 Play()).");
            Assert.That(src, Does.Contain("FxPopSeconds"),
                "[153차 P4] 등장 팝 이징 상수(0.35s back-out — pivot 스케일).");
        }

        [Test]
        public void Cinema_CheerFx_OwnershipSourceContract_153()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("SpawnCheerSparkle"),
                "[153차 P4] P4b 환호 반짝 스폰 존재(§4-4 포함 확정).");
            Assert.That(src, Does.Contain("AcqCheerSparkle"),
                "[153차 P4] 환호 반짝 별도 루트(player 자식).");

            // 발화 지점 계약 무변경 — SetTrigger 호출형 부재.
            // (정합 확인: 기존 19행 주석 "이중 SetTrigger 방지" 문구가 'SetTrigger'를 포함하므로
            //  부정 단정은 호출형 "SetTrigger(" 로 한정 — 기존 계약과 충돌 없음.)
            Assert.That(src, Does.Not.Contain("SetTrigger("),
                "[153차 P4] 환호 SetTrigger 호출형 부재 — TriggerCheer 발화 지점 계약 무변경(§4-4).");

            // 파괴 소유권 — EndPresentationInternal 내 명시 파괴(누수 금지).
            int end = src.IndexOf("private static void EndPresentationInternal");
            Assert.That(end, Is.GreaterThan(0), "[153차 P4] 세션 종료 정리 메서드 존재.");
            string body = src.Substring(end, System.Math.Min(src.Length - end, 2000));
            Assert.That(body, Does.Contain("Object.Destroy(_cheerFxRoot)"),
                "[153차 P4] 환호 반짝 루트 명시 파괴 — pivot 경로 밖 별도 루트(누수 금지).");
        }

        // ===== [154차 P3] 획득 연출 고도화 — 다층 빛기둥/회전 링/링 웨이브/스파클 티어 배수 계약 =====
        // 진실 출처: docs/154_DIALOG_HOLE_FX_RESTAURANT_PRICE_PLAN.md §3 P3. 등급색 단일 소비 유지(색 표 무변경 — 사용자 확정).

        [TestCase(0, 0.4f)]    // 0.25 × 1.6
        [TestCase(2, 0.72f)]   // 0.45 × 1.6
        [TestCase(5, 1.0f)]    // 0.75 × 1.6 = 1.2 → 클램프 1.0
        public void RarityShaftCoreAlpha_MultiLayerFormula_154(int tier, float expected)
        {
            Assert.That(AcquisitionCinema.RarityShaftCoreAlpha(tier), Is.EqualTo(expected).Within(0.0001f),
                "[154차 P3] 코어 빛기둥 알파 = 외곽 표값 ×1.6(클램프 1.0).");
        }

        [Test]
        public void SparkleTables_MonotonicAndBounds_154()
        {
            Assert.That(AcquisitionCinema.SparkleRateTable, Has.Length.EqualTo(6), "[154차 P3] 티어 6단계 표.");
            Assert.That(AcquisitionCinema.SparkleRateTable[0], Is.EqualTo(13f).Within(0.0001f),
                "[154차 P3] Common 유지율 — 구 상수 승계값.");
            for (int i = 1; i < 6; i++)
            {
                Assert.That(AcquisitionCinema.SparkleRateTable[i],
                    Is.GreaterThanOrEqualTo(AcquisitionCinema.SparkleRateTable[i - 1]),
                    $"[154차 P3] rate 표 단조 증가(i={i}).");
                Assert.That(AcquisitionCinema.SparkleSizeMulTable[i],
                    Is.GreaterThanOrEqualTo(AcquisitionCinema.SparkleSizeMulTable[i - 1]),
                    $"[154차 P3] size 배수 표 단조 증가(i={i}).");
            }
            Assert.That(AcquisitionCinema.SparkleSizeMulTable[0], Is.EqualTo(1.0f).Within(0.0001f),
                "[154차 P3] Common 배수 1.0.");
            Assert.That(AcquisitionCinema.SparkleSizeMulTable[5], Is.EqualTo(1.7f).Within(0.0001f),
                "[154차 P3] Unique 배수 1.7.");
        }

        [TestCase(0, 8)]
        [TestCase(5, 28)]
        [TestCase(-1, 8)]
        [TestCase(99, 28)]
        public void SparkleBurstCount_TierScaledClamped_154(int tier, int expected)
        {
            Assert.That(AcquisitionCinema.SparkleBurstCount(tier), Is.EqualTo(expected),
                "[154차 P3] 초기 버스트 = 8 + 4×티어(클램프 0..5).");
        }

        [Test]
        public void RingWave_PopSync_AndSelfDrivenAlpha_SourceContract_154()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("private const float FxWaveDurationS = 0.35f"),
                "[154차 P3] 웨이브 수명 = 팝(0.35s)과 동일 — 등장 동기.");
            Assert.That(src, Does.Contain("FxWaveRadiusEndM, wt) * 2f"),
                "[154차 P3] 웨이브 스케일 = 시작→종료 반경 선형 확산(자체 구동).");
            Assert.That(src, Does.Contain("wc.a = FxWaveStartAlpha * (1f - wt)"),
                "[154차 P3] 웨이브 알파 0.6→0 자체 페이드(세션 페이드와 별개 단일 진실원).");
            Assert.That(src, Does.Not.Contain("RegisterFxRenderer(CreateFxQuad(\"AcqRingWave\""),
                "[154차 P3] 웨이브는 페이드 등록 경로를 쓰지 않는다(구 초안 소멸).");
        }

        [Test]
        public void FxQuads_MultiLayerAndRings_RegisteredForFade_SourceContract_154()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("\"AcqShaftCoreL\""),
                "[154차 P3] 코어 빛기둥(좌) — 외곽+코어 2겹 구성.");
            Assert.That(src, Does.Contain("RegisterFxRenderer(CreateFxQuad(\"AcqRingA\""),
                "[154차 P3] 링 A 페이드 등록(잔상 금지 계약).");
            Assert.That(src, Does.Contain("RegisterFxRenderer(CreateFxQuad(\"AcqRingB\""),
                "[154차 P3] 링 B 페이드 등록(잔상 금지 계약).");
            Assert.That(src, Does.Contain("SparkleBurstCount(tier))"),
                "[154차 P3] 두 스파클 계통 모두 초기 버스트 배선.");
        }

        [Test]
        public void FxCleanup_NullsAll154References_SourceContract_154()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("_floatFxRingA = null;"), "[154차 P3] 종료 시 링 A 레퍼런스 해제.");
            Assert.That(src, Does.Contain("_floatFxRingWave = null;"), "[154차 P3] 종료 시 웨이브 레퍼런스 해제.");
            Assert.That(src, Does.Contain("_floatSparkleCore = null;"), "[154차 P3] 종료 시 코어 스파클 해제.");
            Assert.That(src, Does.Contain("_floatFxShaftCoreL = null;"), "[154차 P3] 종료 시 코어 빛기둥 해제.");
        }

        [Test]
        public void RingTexture_ProceduralCached_SourceContract_154()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AcquisitionCinema.cs");
            Assert.That(src, Does.Contain("private static Texture2D GetRingTexture()"),
                "[154차 P3] 링 절차 텍스처(절차 생성 — 에셋 의존 없음).");
            Assert.That(src, Does.Contain("if (_ringTexture != null) return _ringTexture;"),
                "[154차 P3] 링 텍스처 캐시 결정론.");
        }
    }
}
