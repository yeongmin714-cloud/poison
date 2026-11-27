using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [163차] 음식 아이템 GLB 커버리지 계약 — Food 카테고리 실빈틈 6종 배선 검증.
    /// 배경: cook_01/drugged_cook_01(마을 음식점 판매)·meat_grilled(고기구이)·meat_boar/rabbit/wolf(생고기)는
    /// GLB가 없어 절차 아이콘 폴백이었다. 기존 커버분(조리완성품 6종 FoodGlbByToken·crop_*·fish_glb_)은 제외.
    /// 소스: Polyfork charred-meat-haunch-be1ea2(고기 4종 원본)+silver-cloche-platter-25dd3e(요리 2종 원본).
    /// [163b차] demand 해소분 가산 — 물뿌리개 tool_watering_can(watering-can 원본 복제)+씨앗 6종
    ///   herb_seed_red/purple/yellow/silver/green(sunflower-seed 색 틴트 파생)+seed_herb(sunflower-seed 원본).
    /// [176차] 고기 5종 시각 교체+meat_raw 가산 — RG Poly Medieval Megapack(CC0) 원본 수용, 174차 잔여
    ///   "raw고기 원재" 해소. 교체분에 BIN 청크/파생원 대조 어서션은 미존재 — 기존 존재+해석체인 계약 그대로 유효.
    /// 해석 계약: GblItemIconRenderer.ResolveModelKey 관례 후보 1(id 그대로) — 코드 변경 0, 파일 수용만으로 자동 적용.
    /// </summary>
    public class FoodGlbCoverageTests
    {
        /// <summary>[163차+163b차] 배선 완료 음식·농자 id 전집 — 존재+해석체인 공통 목록.</summary>
        private static readonly string[] WiredFoodIds =
        {
            "meat_grilled", "meat_boar", "meat_rabbit", "meat_wolf", "meat_raw", "cook_01", "drugged_cook_01",
            "tool_watering_can", "herb_seed_red", "herb_seed_purple", "herb_seed_yellow",
            "herb_seed_silver", "herb_seed_green", "seed_herb",
        };

        [Test]
        public void FoodGlbResources_Exist()
        {
            foreach (var name in WiredFoodIds)
            {
                var go = Resources.Load<GameObject>("Models/UserProvided/" + name);
                Assert.That(go, Is.Not.Null, "[163] 음식 GLB 리소스: " + name);
            }
            var haunch = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Polyfork/charred-meat-haunch-be1ea2.glb");
            Assert.That(haunch, Is.Not.Null, "[163] 파생 원본(charred-meat-haunch) Polyfork 수용.");
            var cloche = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Polyfork/silver-cloche-platter-25dd3e.glb");
            Assert.That(cloche, Is.Not.Null, "[163] 파생 원본(silver-cloche-platter) Polyfork 수용.");
            var wcan = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Polyfork/watering-can-b22758.glb");
            Assert.That(wcan, Is.Not.Null, "[163b] 파생 원본(watering-can) Polyfork 수용.");
            var seed = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Polyfork/sunflower-seed-b4fbbd.glb");
            Assert.That(seed, Is.Not.Null, "[163b] 파생 원본(sunflower-seed) Polyfork 수용.");
        }

        [Test]
        public void ResolveModelKey_Food_IdConvention()
        {
            // 요리/고기/도구/씨앗 — id 그대로(관례 후보 1) 해석 통과.
            foreach (var id in WiredFoodIds)
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = ProjectName.UI.GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.Not.Null.And.Not.Empty, "[163] 아이콘 모델키 해석: " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[163] 해석된 모델키가 실제 로드 가능: " + id + " → " + key);
            }
        }

        /// <summary>[175차] 물고기 급여 3등급 커버리지 — UserProvided fish_common/fish_rare/fish_legendary.
        /// 원본: Polyfork koi-base·reef-grouper-964612·coelacanth-base 복제 수용. 실노출 경로: LordFoodCatalog
        /// SeaIds(영주 급여 선물) → 인벤 → 아이콘. 해석 체인: GblItemIconRenderer.ResolveModelKey 관례 후보 1
        /// (id 그대로) 통과 + 해석키 실로드 가능(ResolveModelKey_Food_IdConvention 기존 선례 동일). 파일 존재는
        /// File.Exists 문자열 기반 경로 계약으로 판정 — EditMode 에셋 로드 제약 존중. 참고: AcquisitionItemVisualResolver
        /// 음식 토큰 경로는 fish_* 미매핑(null → 아이콘+텍스트 폴백 [144차 P9-ACQ]) — 본 체인은 아이콘 해석이 주체.</summary>
        [Test]
        public void FishGlb_3Ranks_UserProvidedCoverage()
        {
            string[] fishIds = { "fish_common", "fish_rare", "fish_legendary" };
            string userDir = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", "UserProvided");
            foreach (var id in fishIds)
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(userDir, id + ".glb")), Is.True,
                    "[175] 물고기 GLB UserProvided 수용: " + id);
            }
            string[] polyforkSources = { "koi-base", "reef-grouper-964612", "coelacanth-base" };
            string polyDir = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", "Polyfork");
            foreach (var id in polyforkSources)
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(polyDir, id + ".glb")), Is.True,
                    "[175] 물고기 원본 Polyfork 수용: " + id);
            }
            // 해석 체인 — 관례 후보 1(id 그대로) 통과 + 해석키 실로드 가능(기존 선례와 동일).
            foreach (var id in fishIds)
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = ProjectName.UI.GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.EqualTo(id), "[175] 관례 후보 1(id 그대로) 해석 통과: " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[175] 해석된 모델키 실로드 가능: " + id + " → " + key);
            }
        }

        /// <summary>[176차] 화살 3종 시각 수용 — UserProvided arrow_regular/arrow_reinforced/arrow_magic.
        /// 원본: RG Poly Medieval Megapack(CC0) Arrow_1_A/Arrow_2_A/Arrow_2_B.glb 복제 수용 — 기존 화살은
        /// 트레일 시각만 존재(모델 부재). 해석 계약: GblItemIconRenderer.ResolveModelKey 관례 후보 1(id 그대로)
        /// 통과 + 해석키 실로드 가능. 파일 존재는 File.Exists 문자열 기반 경로 계약으로 판정 — EditMode 에셋
        /// 로드 제약 존중(FishGlb_3Ranks_UserProvidedCoverage 선례 동일).</summary>
        [Test]
        public void ArrowGlb_3Variants_UserProvidedCoverage()
        {
            string[] arrowIds = { "arrow_regular", "arrow_reinforced", "arrow_magic" };
            string userDir = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", "UserProvided");
            foreach (var id in arrowIds)
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(userDir, id + ".glb")), Is.True,
                    "[176] 화살 GLB UserProvided 수용: " + id);
            }
            // 해석 체인 — 관례 후보 1(id 그대로) 통과 + 해석키 실로드 가능([175차] 물고기 선례 동일).
            foreach (var id in arrowIds)
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = ProjectName.UI.GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.EqualTo(id), "[176] 관례 후보 1(id 그대로) 해석 통과: " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[176] 해석된 모델키 실로드 가능: " + id + " → " + key);
            }
        }

        /// <summary>GLB BIN 청크 길이 파싱 — 틴트 파생의 구조 불변 계약 판정용.
        /// [163차 QA 수리] 총 파일 길이는 JSON 청크 재직렬화(min/max 갱신 포맷차)로 달라질 수 있어
        /// 구조 불변의 참 계약은 정점 데이터(BIN 청크) 크기다.</summary>
        private static long GlbBinChunkLength(byte[] glb)
        {
            Assert.That(glb.Length, Is.GreaterThanOrEqualTo(20), "GLB 헤더 최소 크기.");
            int off = 12;
            while (off + 8 <= glb.Length)
            {
                int len = System.BitConverter.ToInt32(glb, off);
                int type = System.BitConverter.ToInt32(glb, off + 4);
                if (type == 0x004E4942) return len;   // BIN 청크
                off += 8 + len;
            }
            return -1;
        }

        private static byte[] ReadUserGlb(string name)
        {
            return System.IO.File.ReadAllBytes(
                System.IO.Path.Combine(Application.dataPath, "Resources/Models/UserProvided/" + name + ".glb"));
        }

        [Test]
        public void DruggedCook_TintDerived_NotByteIdentical()
        {
            // 파생 계약 — drugged_cook_01은 cook_01과 동일 구조(BIN 청크 동일 크기)에 색만 다른 틴트 파생본.
            byte[] cook = ReadUserGlb("cook_01");
            byte[] drugged = ReadUserGlb("drugged_cook_01");
            long cookBin = GlbBinChunkLength(cook);
            long druggedBin = GlbBinChunkLength(drugged);
            Assert.That(cookBin, Is.GreaterThan(0), "[163] cook_01 BIN 청크 존재.");
            Assert.That(druggedBin, Is.EqualTo(cookBin),
                "[163] 틴트 파생은 BIN 청크 크기 불변(정점 구조 동일).");
            Assert.That(drugged, Is.Not.EqualTo(cook), "[163] 마젠타 틴트가 실제 반영(바이트 상이).");
        }

        [Test]
        public void HerbSeed_TintVariants_DistinctFromBase()
        {
            // [163b] 씨앗 5색 틴트 파생 — seed_herb(원본)와 전부 바이트 상이, BIN 청크 크기 동일(구조 동일).
            byte[] baseSeed = ReadUserGlb("seed_herb");
            long baseBin = GlbBinChunkLength(baseSeed);
            Assert.That(baseBin, Is.GreaterThan(0), "[163b] seed_herb BIN 청크 존재.");
            foreach (var name in new[] { "herb_seed_red", "herb_seed_purple", "herb_seed_yellow", "herb_seed_silver", "herb_seed_green" })
            {
                byte[] tinted = ReadUserGlb(name);
                Assert.That(GlbBinChunkLength(tinted), Is.EqualTo(baseBin),
                    "[163b] 씨앗 틴트 BIN 청크 크기 불변(정점 구조 동일): " + name);
                Assert.That(tinted, Is.Not.EqualTo(baseSeed), "[163b] 색 틴트 실제 반영: " + name);
            }
        }
    }
}
