using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [130차 P3] 획득 연출 손끝 GLB 리졸버(static) — 아이템 id → 시각 GLB 결정론 매핑 + 로드.
    ///
    /// 매핑 계약(결정론 — 입력 같으면 경로 항상 같음):
    ///  - id 접두 "fish_glb_" → FishCatalog.GlbPathByKey(키) — 물고기 333종 GLB 직결.
    ///  - id 접두 "crop_"(수확물/씨앗) → CropCatalog.GlbPath(인덱스) — 작물 100종 GLB 직결.
    ///  - 약초 계열(herb_yakcho/herb_{색}) → HerbType 색 매핑(red→herb_red, purple→herb_purple,
    ///    yellow→herb_gold, silver→herb_blue, green/기본→herb_green). 색 정보는 id 직결 → 표시명/설명
    ///    키워드 추정 → 추정 불가 시 기본 herb_green 순으로 판정.
    ///  - mat_wood → UserProvided/stake.glb([130차 임시] — 목재 단품 GLB 부재 폴백).
    ///  - mat_stone → Resources IdyllicPrefabs/Rocks 첫 프리팹([130차 임시] — 이름순 결정론).
    ///  - 무기(weapon_{type}_{tier}) → UserProvided/{tier}_{type}.glb(존재분만 — 파일 부재 조합은 null).
    ///  - 방어구/부속(id = GLB 파일명 관례) → 존재 화이트리스트 통과분만(존재분만).
    ///  - 음식 → 표 파일명 매핑(존재분만).
    ///  - [172차] 왕실 요리 dish_XXX → DishGlbCatalog.ModelKey(요리명 어미 → GLB 버킷 17종 공유).
    ///    표시 시 재료 카테고리 hue 틴트. 미매칭 null → 호출처 폴백.
    ///  - [144차 P9-ACQ] fruit_apple → CropCatalog "apple" GLB(crop-apple.glb 실측 존재).
    ///    flower_wild → nature 데이지 클럼프 GLB(실측 존재, 에디터 전용). wood_dagger → GLB 실측 존재
    ///    확인으로 MissingWeaponGlbKeys 제외 해제. GLB 실측 부재 id(광석류 등)는 매핑하지 않는다 —
    ///    null 반환 → 호출처(AcquisitionCinema) 아이콘+텍스트 폴백(143차 감사 §2-1① 승인 방향).
    ///
    /// 로드 계약: #if UNITY_EDITOR AssetDatabase 우선 → 실패 시 Resources 폴백(경로 매핑에 Resources
    /// 후보가 있을 때만) → 최종 실패 시 null 반환(연출 중단 없음 — [144차 P9-ACQ] 호출처 아이콘+텍스트 폴백).
    /// 매핑 실패/미상 id → null.
    ///
    /// [130차 임시] 매핑 테이블은 public static — 사용자 GLB 준비 시 해당 줄 1줄 교체로 대응.
    /// </summary>
    public static class AcquisitionItemVisualResolver
    {
        // ===== [130차 임시] 매핑 테이블(public static — 사용자 GLB 교체 1줄 대응) =====

        /// <summary>[130차 임시] 목재 단품 GLB 부재 → 말뚝 GLB 대용. 사용자 목재 GLB 준비 시 이 2줄만 교체.</summary>
        public static string MatWoodEditorPath = "Assets/Resources/Models/UserProvided/stake.glb";
        public static string MatWoodResourcesPath = "Models/UserProvided/stake";

        /// <summary>[130차 임시] 석재 단품 GLB 부재 → Resources 바위 프리팹 첫 종(이름순 결정론). 사용자 GLB 준비 시 교체.</summary>
        public static string MatStoneResourcesFolder = "IdyllicPrefabs/Rocks";

        /// <summary>UserProvided GLB 루트(Resources 키 / 에디터 에셋 경로) — GLB 추가 시 위치 계약.</summary>
        public const string UserProvidedResourcesDir = "Models/UserProvided";
        public const string UserProvidedEditorDir = "Assets/Resources/Models/UserProvided";

        /// <summary>[144차 P9-ACQ] 들꽃 폴백 GLB — nature 폴더 실측 파일(결정론 단일 경로). Resources 밖(에디터 전용).</summary>
        public const string FlowerWildEditorPath =
            "Assets/새로운 glb/nature/angular-common-nature-flowering-daisy-clump-streamline-normal.glb";

        /// <summary>방어구/부속 GLB 화이트리스트(id = GLB 파일명 관례 — PlayerInventory 4티어 전종 중 실존 파일만, 존재분만).</summary>
        public static readonly string[] KnownGearGlbKeys =
        {
            "wood_armor", "steel_armor", "stone_armor", "crystal_armor",
            "wood_helmet", "steel_helmet", "stone_helmet", "crystal_helmet",
            "wood_boot_left", "wood_boot_right", "steel_boot_left", "steel_boot_right",
            "stone_boot_left", "stone_boot_right", "crystal_boot_left", "crystal_boot_right",
            "wood_glove_left", "wood_glove_right", "steel_glove_left", "steel_glove_right",
            "stone_glove_left", "stone_glove_right", "crystal_glove_left", "crystal_glove_right",
            "wood_shield",                                                    // wood만 존재(PlayerInventory 주석 계약)
            "wood_gas_mask", "steel_gas_mask", "stone_gas_mask",              // crystal 부재(존재분만)
            "wood_chemical_pack", "steel_chemical_pack", "stone_chemical_pack",
        };

        /// <summary>무기 타입/티어 조합 — weapon_{type}_{tier} id 파싱 대상.</summary>
        public static readonly string[] WeaponTypes = { "sword", "spear", "bow", "dagger" };
        public static readonly string[] WeaponTiers = { "wood", "steel", "stone", "crystal" };

        /// <summary>조합은 가능하지만 GLB 파일이 없는 무기 키(존재분만 — 부재 조합 매핑 배제).</summary>
        /// <summary>[144차 P9-ACQ] wood_dagger.glb 실측 존재 확인 → 제외 해제(빈 배열 유지 — 향후 부재 조합 발견 시 추가).</summary>
        public static readonly string[] MissingWeaponGlbKeys = { };

        /// <summary>음식 토큰 → UserProvided GLB 파일명(확장자 제외, 존재분만).</summary>
        public static readonly (string token, string file)[] FoodGlbByToken =
        {
            ("grilled", "grilled_skin"),
            ("roasted", "roasted_chicken"),
            ("soup",    "soup"),
            ("tempura", "tempura"),
            ("yakitori", "yakitori_skewer"),
            ("puding",  "puding"),
            ("pudding", "puding"),
        };

        /// <summary>약초 id 직결 집합 — 이 집합만 약초 색 매핑 경로로 진입(씨앗/미상 id는 null).</summary>
        public static readonly string[] HerbGlbIds =
        {
            "herb_yakcho", "herb_red", "herb_purple", "herb_yellow",
            "herb_gold", "herb_silver", "herb_blue", "herb_green",
        };

        // ===== 공개 API =====

        /// <summary>손끝 연출용 시각 프리팹 로드 — 에디터 AssetDatabase → Resources 폴백 → null(GLB 생략).
        /// [150차 P1] 반환 계약 변경: 에셋이 아니라 씬 인스턴스를 반환한다. 기존에는 AssetDatabase/Resources 로드
        /// 결과(프리팹 에셋)를 그대로 반환해, 호출처(AttachFloatItem)의 SetParent가 "Transform resides in a
        /// Prefab asset and cannot be set" 로 차단되어 GLB가 화면에 부유하지 않았다(150차 Editor.log 4회 실측).</summary>
        public static GameObject LoadVisual(PlayerInventory.ItemData item)
        {
            if (item == null) return null;
            string id = item.id ?? string.Empty;

            GameObject go = null;
            string editorPath = ResolveEditorAssetPath(item);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(editorPath))
                go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
#endif

            // Resources 폴백 — 경로 매핑에 Resources 후보가 있을 때만(연출 중단 없음 계약).
            if (go == null)
            {
                if (id == "mat_stone")
                {
                    // [130차 임시] 석재 단품 GLB 부재 — Resources Rocks 첫 프리팹(이름순 Ordinal 결정론).
                    var rocks = Resources.LoadAll<GameObject>(MatStoneResourcesFolder);
                    if (rocks != null && rocks.Length > 0)
                    {
                        System.Array.Sort(rocks, (a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));
                        go = rocks[0];
                    }
                }
                else
                {
                    string resPath = ResolveResourcesPath(item);
                    if (!string.IsNullOrEmpty(resPath))
                        go = Resources.Load<GameObject>(resPath);
                }
            }

            // [150차 P1] 반환 시점 유일화 — 에셋 그대로가 아니라 씬 인스턴스로 복제해 반환
            // (에디터 AssetDatabase / Resources 폴백 경로 모두 동일 계약 — Load 결과도 에셋이므로).
            GameObject instance = InstantiateSceneInstance(go);   // 실패 시 null — 호출처(연출)는 GLB 생략 + 패널/카메라만 진행

            // [172차] 왕실 요리 dish_XXX — 재료 카테고리 hue 틴트(에디터/Resources 양 경로 공통, MPB만).
            if (instance != null && id.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
                DishGlbCatalog.ApplyTint(instance, DishGlbCatalog.TintFor(DishGlbCatalog.CatsFor(id)));

            return instance;
        }

        /// <summary>[150차 P1] 에셋 → 씬 인스턴스 복제 — LoadVisual 반환은 항상 씬 오브젝트(부모 재설정 가능).
        /// 프리팹 에셋 자체를 반환하면 호출처 SetParent가 유니티 데이터 무결성 가드로 차단된다(150차 실측).</summary>
        private static GameObject InstantiateSceneInstance(GameObject asset)
        {
            return asset != null ? Object.Instantiate(asset) : null;   // [150차 P1] Instantiate 일관 적용
        }

        /// <summary>에디터 AssetDatabase 로드용 전체 경로(없으면 null) — 결정론. 순수 판정(EditMode 테스트 대상).</summary>
        public static string ResolveEditorAssetPath(PlayerInventory.ItemData item)
        {
            string res = ResolveResourcesPath(item);
            if (item == null) return null;
            string id = item.id ?? string.Empty;

            if (id.StartsWith("fish_glb_"))
                return FishCatalog.GlbPathByKey(id.Substring("fish_glb_".Length));   // Assets/새로운 glb/crops-fish/...

            if (id.StartsWith("crop_"))
            {
                string key = id.Substring("crop_".Length);
                if (key.EndsWith("_seed")) key = key.Substring(0, key.Length - "_seed".Length);
                int idx = CropCatalog.IndexOf(key);
                return idx >= 0 ? CropCatalog.GlbPath(idx) : null;
            }

            // [144차 P9-ACQ] fruit_apple — 사과 GLB 실측 존재(crops-fish/crop-apple.glb) → CropCatalog 경로 재사용.
            if (id == "fruit_apple")
            {
                int appleIdx = CropCatalog.IndexOf("apple");
                return appleIdx >= 0 ? CropCatalog.GlbPath(appleIdx) : null;
            }

            // [144차 P9-ACQ] flower_wild — 들꽃 폴백 GLB 실측 존재(nature 데이지 클럼프) → 에디터 전용 경로.
            if (id == "flower_wild")
                return FlowerWildEditorPath;

            // [172차] 왕실 요리 dish_XXX — 요리명 어미 → GLB 버킷(DishGlbCatalog). 미매칭 null → 호출처 폴백.
            if (id.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
            {
                string dishKey = DishGlbCatalog.ModelKey(id);
                return !string.IsNullOrEmpty(dishKey) ? UserProvidedEditorDir + "/" + dishKey + ".glb" : null;
            }

            if (System.Array.IndexOf(HerbGlbIds, id) >= 0)
                return UserProvidedEditorDir + "/" + ResolveHerbGlbKey(id, item.displayName, item.description) + ".glb";

            if (id == "mat_wood") return MatWoodEditorPath;   // [130차 임시]
            if (id == "mat_stone") return null;               // Resources 폴더 스캔 전용 — 에디터 단일 경로 없음

            if (id.StartsWith("weapon_"))
            {
                string weaponKey = ResolveWeaponGlbKey(id);
                return weaponKey != null ? UserProvidedEditorDir + "/" + weaponKey + ".glb" : null;
            }

            if (System.Array.IndexOf(KnownGearGlbKeys, id) >= 0)
                return UserProvidedEditorDir + "/" + id + ".glb";

            string foodFile = ResolveFoodGlbFile(id);
            return foodFile != null ? UserProvidedEditorDir + "/" + foodFile + ".glb" : null;
        }

        /// <summary>Resources 로드 키(없으면 null) — Resources 폴백 경로 매핑. 순수 판정(EditMode 테스트 대상).</summary>
        public static string ResolveResourcesPath(PlayerInventory.ItemData item)
        {
            if (item == null) return null;
            string id = item.id ?? string.Empty;

            if (id.StartsWith("fish_glb_") || id.StartsWith("crop_"))
                return null;   // crops-fish GLB는 Resources 밖(에디터 전용 경로) — 폴백 후보 없음

            // [144차 P9-ACQ] fruit_apple(crops-fish)/flower_wild(nature) GLB도 Resources 밖 — 폴백 후보 없음.
            if (id == "fruit_apple" || id == "flower_wild")
                return null;

            // [172차] 왕실 요리 dish_XXX — UserProvided GLB 버킷(리소스 폴백 후보). 미매칭 null.
            if (id.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
            {
                string dishKey = DishGlbCatalog.ModelKey(id);
                return !string.IsNullOrEmpty(dishKey) ? UserProvidedResourcesDir + "/" + dishKey : null;
            }

            if (System.Array.IndexOf(HerbGlbIds, id) >= 0)
                return UserProvidedResourcesDir + "/" + ResolveHerbGlbKey(id, item.displayName, item.description);

            if (id == "mat_wood") return MatWoodResourcesPath;   // [130차 임시]
            if (id == "mat_stone") return null;                  // 폴더 스캔 — LoadVisual 전용 처리

            if (id.StartsWith("weapon_"))
            {
                string weaponKey = ResolveWeaponGlbKey(id);
                return weaponKey != null ? UserProvidedResourcesDir + "/" + weaponKey : null;
            }

            if (System.Array.IndexOf(KnownGearGlbKeys, id) >= 0)
                return UserProvidedResourcesDir + "/" + id;

            string foodFile = ResolveFoodGlbFile(id);
            return foodFile != null ? UserProvidedResourcesDir + "/" + foodFile : null;
        }

        /// <summary>
        /// 약초 색 매핑 결정론 — id 직결(red/purple/yellow|gold/silver|blue/green) → 표시명/설명 색 키워드
        /// 추정(빨간/보라/노란·황혼/은빛/초록) → 추정 불가 시 기본 herb_green([130차] 계약).
        /// </summary>
        public static string ResolveHerbGlbKey(string id, string displayName, string description)
        {
            switch (id)
            {
                case "herb_red": return "herb_red";
                case "herb_purple": return "herb_purple";
                case "herb_yellow":
                case "herb_gold": return "herb_gold";      // [130차] HerbType.Yellow ↔ GLB herb_gold
                case "herb_silver":
                case "herb_blue": return "herb_blue";      // [130차] HerbType.Silver ↔ GLB herb_blue
                case "herb_green": return "herb_green";
            }

            // [130차] 색 정보 추정 — publish displayName/description 텍스트에서 키워드 판정.
            string text = (displayName ?? string.Empty) + " " + (description ?? string.Empty);
            if (text.Contains("빨간") || text.Contains("빨강")) return "herb_red";
            if (text.Contains("보라")) return "herb_purple";
            if (text.Contains("노란") || text.Contains("노랑") || text.Contains("황혼")) return "herb_gold";
            if (text.Contains("은빛") || text.Contains("은색")) return "herb_blue";
            if (text.Contains("초록") || text.Contains("녹색")) return "herb_green";
            return "herb_green";   // [130차] 색 정보 추정 불가 → 기본 herb_green
        }

        /// <summary>weapon_{type}_{tier} → {tier}_{type} GLB 키(파일 부재 조합은 null — 존재분만).</summary>
        public static string ResolveWeaponGlbKey(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var parts = id.Split('_');
            if (parts.Length != 3) return null;
            string type = parts[1];
            string tier = parts[2];
            if (System.Array.IndexOf(WeaponTypes, type) < 0) return null;
            if (System.Array.IndexOf(WeaponTiers, tier) < 0) return null;
            string key = tier + "_" + type;
            if (System.Array.IndexOf(MissingWeaponGlbKeys, key) >= 0) return null;   // wood_dagger 등 파일 부재
            return key;
        }

        /// <summary>음식 id 토큰 → GLB 파일명(존재분만 — 미상 null).</summary>
        public static string ResolveFoodGlbFile(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string low = id.ToLowerInvariant();
            foreach (var pair in FoodGlbByToken)
                if (low.Contains(pair.token)) return pair.file;
            return null;
        }
    }
}
