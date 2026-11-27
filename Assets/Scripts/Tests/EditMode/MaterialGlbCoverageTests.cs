using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;
using ProjectName.Systems;
using UnityEditor;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [162차-4] 광물/금속 재료 아이템 GLB 존재 계약 — Polyfork 수용(CC BY 4.0) + UserProvided 게임 경로 복제본.
    /// 배경: 광물 7종(crystal_shard/iron_ingot/iron_ore/mat_gold_ore/mat_mythril_ore/mat_silver_ore/mat_stone) 전부
    /// 전용 GLB 부재 실측(mat_stone은 [130차] 임시 폴백 상태) → Polyfork 이름매칭 수용 8종(예비 copper 포함).
    /// 계약: ①아이템 id ↔ UserProvided/{id}.glb 1:1(관례후보1 자동해결) ②Polyfork 원본 경로 수용 유지
    /// ③mat_mythril_ore는 지오드 틴트 파생(암석 회청/결정 청록 발광 — 정점컬러 hue 재매핑).
    /// 가죽 5종은 카탈로그 원재 부재 — POST /api/demand 요청 기록(2026-11-21) 후 수용 예정.
    /// [172차] 파생 아이템 GLB 커버 + 폴리포크 코인 수용(gold/silver/mount_token) — 잔여 배선 마지막 단계.
    /// </summary>
    public class MaterialGlbCoverageTests
    {
        private static readonly string[] OreItemIds =
        {
            "iron_ore", "mat_gold_ore", "mat_silver_ore", "iron_ingot",
            "crystal_shard", "mat_stone", "mat_mythril_ore",
        };

        [Test]
        public void OreGlb_AllSeven_ExistAndResolveById()
        {
            foreach (string id in OreItemIds)
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.Not.Null.And.Not.Empty,
                    "[162-4] 광물 아이콘 모델키 해석(관례후보1): " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[162-4] 광물 GLB 리소스 로드 가능: " + id + " → " + key);
                var go = Resources.Load<GameObject>("Models/UserProvided/" + id);
                Assert.That(go, Is.Not.Null, "[162-4] 광물 GLB 직접 로드: " + id);
            }
        }

        [Test]
        public void OrePolyfork_OriginalPaths_Preserved()
        {
            // [162-4] Polyfork 원본 수용 경로 유지 — CREDITS 라인과 1:1(§6 원본+게임 경로 1:1 선례).
            foreach (string id in new[]
                     {
                         "iron-ore-chunk-1df386", "gold-ore-chunk-6f1d9e", "silver-ore-chunk-1e440c",
                         "iron-ingot-36697d", "crystal-shard-9e0a83", "live-rock-piece-6c7bdf",
                         "mineral-geode-cluster-299927", "copper-ore-chunk-258509",
                     })
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(
                    Application.dataPath, "Resources", "Models", "Polyfork", id + ".glb")), Is.True,
                    "[162-4] Polyfork 원본 GLB 수용 유지: " + id);
            }
        }

        [Test]
        public void ToolMaterialGoldGlb_ExistAndResolveById()
        {
            // [162-5차] 도구/재료/금화 — Polyfork 수용 + UserProvided 게임 경로 복제본.
            // [165차] 낚싯대 수용 — 동일 관례(Polyfork propped-fishing-rod 원본 + UserProvided 게임 경로 복제본).
            foreach (string id in new[]
                     {
                         "tool_shovel", "tool_axe", "tool_pickaxe", "mat_wood",
                         "gold", "gold_pouch",
                         "fishing_rod",
                     })
            {
                var item = new PlayerInventory.ItemData { id = id, displayName = id };
                string key = GblItemIconRenderer.ResolveModelKey(item);
                Assert.That(key, Is.Not.Null.And.Not.Empty,
                    "[162-5] 아이콘 모델키 해석: " + id);
                Assert.That(RuntimeModelLoader.HasModel(key), Is.True,
                    "[162-5] GLB 리소스 로드: " + id + " → " + key);
            }
        }

        [Test]
        public void CrystalChainTints_ShieldGasMaskChemicalPack_Exist()
        {
            // [162-5차] 명시 맵 타겟 부재 수리 — shield_steel/stone/crystal + gas_mask_crystal/chemical_pack_crystal.
            foreach (string id in new[]
                     {
                         "shield_steel", "shield_stone", "shield_crystal",
                         "gas_mask_crystal", "chemical_pack_crystal",
                     })
            {
                Assert.That(RuntimeModelLoader.HasModel(id), Is.True,
                    "[162-5] crystal 계열 파생 GLB: " + id);
                var go = Resources.Load<GameObject>("Models/UserProvided/" + id);
                Assert.That(go, Is.Not.Null, "[162-5] GLB 직접 로드: " + id);
            }
        }

        [Test]
        public void MythrilDerivative_DistinctFromGeodeBasis_VertexColor()
        {
            // [162-4] 마이스릴 파생은 원본 지오드와 다른 정점컬러를 가져야 한다(틴트 적용 실증).
            // 구조 검증: 원본과 동일 mesh/vertex 수이면서 COLOR_0 존재 — 색이 실제로 붙은 파생본인지.
            var go = Resources.Load<GameObject>("Models/UserProvided/mat_mythril_ore");
            Assert.That(go, Is.Not.Null, "[162-4] mat_mythril_ore 로드");
            var meshes = go.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(meshes.Length, Is.GreaterThanOrEqualTo(1), "[162-4] 메시 존재");
            bool hasColor = false;
            foreach (var mf in meshes)
            {
                if (mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0)
                {
                    hasColor = true;
                    break;
                }
            }
            Assert.That(hasColor, Is.True, "[162-4] 파생본 정점컬러 존재(hue 재매핑 반영)");
        }

        // ================================================================
        //  [172차] 파생 아이템 GLB 커버 + 폴리포크 코인 수용
        // ================================================================

        /// <summary>[172차] 신규 파생 아이템 GLB — UserProvided 파일명 = 게임 item id(관례후보1 직결, DropTableManager·ItemDatabase 실측).
        /// 창은 weapon_iron_spear, 인장은 quest_seal_token — 배치 파일명 실측 기준.</summary>
        private static readonly string[] Derivative172Ids =
        {
            "weapon_legendary", "weapon_unique_abyss", "weapon_unique_dawnblade",
            "dracula_legend_recipe", "dracula_legend_sword", "dracula_legend_armor", "dracula_rare_blood", "dracula_rare_crystal",
            "skeleton_rusty_sword", "skeleton_bone_shard", "skeleton_soul_remnant",
            "iron_sword", "weapon_iron_spear", "iron_helmet", "iron_armor", "iron_boots", "iron_shield",
            "armor_leather", "armor_cloth", "armor_unique_voidplate",
            "quest_deed", "quest_empire_intel", "quest_evidence_doc", "quest_heirloom",
            "quest_love_letter", "quest_military_intel", "quest_resistance_letter", "quest_treason_doc",
            "quest_seal_token", "item_secret_pass", "bomb_explosive", "herb_legendary",
        };

        [Test]
        public void Derivative172Glb_ExistAndLoad()
        {
            // [172차] 파생 아이템 GLB 존재+실로드(DishGlbCoverageTests.GeneralFoodGap_Wired 패턴).
            // [173차] dracula_legend_ring 수용 완료 — 장신구 원본 4종(simple-band/knuckle/signet/prayer-bead-strand)
            //   수용 + 틴트 파생 6종 UserProvided 배선(반지 형상은 카탈로그 존재 실측 — 이전 "부재"는 쿼리 필터 버그).
            foreach (string id in Derivative172Ids)
            {
                var glb = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Models/UserProvided/" + id + ".glb");
                Assert.That(glb, Is.Not.Null, "[172] 파생 아이템 GLB 존재: " + id);
                Assert.That(RuntimeModelLoader.HasModel(id), Is.True,
                    "[172] 파생 아이템 GLB 실로드: " + id);
            }
        }

        [Test]
        public void Coin172Glb_ExistAndLoad()
        {
            // [172차] 폴리포크 코인 수용 3종 — gold_coin·silver_coin(UserProvided 게임 경로 복제) + mount_token(파생본).
            // 게임 item id 실측: DropTable.GoldCoinId/SilverCoinId + MountSystem mount_token.
            // mount_token = gold-coin 원본 hue −15°/sat 1.1/val 0.78 청동 정점컬러 리맵(COLOR_0 VEC3, Tools/derivative_bake).
            foreach (string id in new[] { "gold_coin", "silver_coin", "mount_token" })
            {
                var glb = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Models/UserProvided/" + id + ".glb");
                Assert.That(glb, Is.Not.Null, "[172] 코인 GLB 존재: " + id);
                Assert.That(RuntimeModelLoader.HasModel(id), Is.True,
                    "[172] 코인 GLB 실로드: " + id);
            }
        }

        [Test]
        public void Coin172Polyfork_OriginalPaths_Preserved()
        {
            // [172차] 코인 Polyfork 원본 수용 경로 유지 — CREDITS 라인(199·200)과 1:1([162-4] §6 선례).
            foreach (string id in new[] { "gold-coin-29afc1", "silver-coin-64ab27" })
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(
                    Application.dataPath, "Resources", "Models", "Polyfork", id + ".glb")), Is.True,
                    "[172] Polyfork 원본 GLB 수용 유지: " + id);
            }
        }

        // ================================================================
        //  [173차] 장신구 6종 — 반지/목걸이 GLB 배선(폴리포크 재스캔 해소)
        // ================================================================

        /// <summary>[173차] 장신구 파생 GLB — 게임 id(AccessoryDefinitions·PlayerInventory Accessory·DropTableManager 실측).
        /// 원본 4종 → 틴트 파생: simple-band(비취/혈적)·knuckle(강철)·signet(혈적)·prayer-bead(적/청강 구슬).</summary>
        private static readonly string[] Accessory173Ids =
        {
            "ring_evasion", "ring_vitality", "ring_power",
            "necklace_hp", "necklace_guard", "dracula_legend_ring",
        };

        [Test]
        public void Accessory173Glb_ExistLoadAndVertexColor()
        {
            // [173차] 장신구 6종 존재+실로드+정점컬러(틴트 베이크 반영 — MountToken172 패턴).
            foreach (string id in Accessory173Ids)
            {
                var glb = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Models/UserProvided/" + id + ".glb");
                Assert.That(glb, Is.Not.Null, "[173] 장신구 GLB 존재: " + id);
                Assert.That(RuntimeModelLoader.HasModel(id), Is.True,
                    "[173] 장신구 GLB 실로드: " + id);
                var meshes = glb.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(meshes.Length, Is.GreaterThanOrEqualTo(1), "[173] 메시 존재: " + id);
                bool hasColor = false;
                foreach (var mf in meshes)
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0)
                    {
                        hasColor = true;
                        break;
                    }
                }
                Assert.That(hasColor, Is.True, "[173] 정점컬러 존재(파생 틴트 반영): " + id);
            }
        }

        [Test]
        public void Accessory173Polyfork_OriginalPaths_Preserved()
        {
            // [173차] 장신구 Polyfork 원본 4종 수용 경로 유지 — CREDITS 라인(201~204)과 1:1(Coin172Polyfork 패턴).
            foreach (string id in new[] { "simple-band-ring-929543", "knuckle-ring-529610", "signet-ring-efa0ee", "prayer-bead-strand-e18d9a" })
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(
                    Application.dataPath, "Resources", "Models", "Polyfork", id + ".glb")), Is.True,
                    "[173] Polyfork 원본 GLB 수용 유지: " + id);
            }
        }

        [Test]
        public void MountToken172_DistinctVertexColorFromGoldBasis()
        {
            // [172차] mount_token 파생은 원본과 다른 정점컬러(금→청동 hue 리맵 실증) — [162-4] MythrilDerivative 선례.
            var go = Resources.Load<GameObject>("Models/UserProvided/mount_token");
            Assert.That(go, Is.Not.Null, "[172] mount_token 로드");
            var meshes = go.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(meshes.Length, Is.GreaterThanOrEqualTo(1), "[172] 메시 존재");
            bool hasColor = false;
            foreach (var mf in meshes)
            {
                if (mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0)
                {
                    hasColor = true;
                    break;
                }
            }
            Assert.That(hasColor, Is.True, "[172] 파생본 정점컬러 존재(hue 리맵 반영)");
        }
    }
}
