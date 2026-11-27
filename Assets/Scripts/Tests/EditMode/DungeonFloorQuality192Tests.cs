using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [192차] 던전 바닥 고품질화 계약 — 실내씬 수준 마감. 소스/아티팩트 어서션(형제 테스트 관례).
    /// 배경: 팩 Floor GLB는 albedo(팔레트 그라디언트 "Concrate")만 존재 — 노멀맵·오클루전·COLOR_0 전무.
    ///   벽(CreateWallSide)/돌판 경로(CreateStonePathMat)는 절차 줄눈+노멀 릴리프 보유 → 바닥만 평면 도장처럼 보임
    ///   (사용자 지적 "영지 실내씬처럼 바닥 고품질화"). 팔레트 UV는 면단위 붕괴 방식이라 노멀맵 파생은 무효 —
    ///   프로젝트 선례(172차 에지다크닝·174차 정점AO)인 정점색 수술로 릴리프 대체.
    /// 계약:
    ///  ① GLB 수술 — COLOR_0 주입(VEC3 f32) + 정점AO(174차 동일 공식 R절대5cm·s²/d²·AO_FLOOR 0.60)
    ///     × 에지다크닝(위치그룹 면법선 최대 상호편차 > 55° → ×0.84) + 팔레트 스펙클(저주파 ±3.5%·고주파 ±1.2%).
    ///  ② 원본 백업 — Tools/ao_prebake_backup/DungeonFloor/ 6종(Assets 밖 — 임포트 무관).
    ///  ③ 매니페스트 — Tools/ao_bake_manifest.json에 "DungeonFloor/Floor_1_XA" 6키.
    ///  ④ 회전 다양화 — (cell.x*13 + cell.y*29) & 3 → 90° 스텝(팔레트 UV 붕괴 스타일 무해, 격자 반복 타파).
    ///  ⑤ 수술 도구 — Tools/floor_quality_bake.py 계약 상수(스펙클/엣지/AO 재사용) 존재.
    /// </summary>
    public sealed class DungeonFloorQuality192Tests
    {
        private static readonly string[] FloorIds =
        {
            "Floor_1_1A", "Floor_1_2A", "Floor_1_3A", "Floor_1_4A", "Floor_1_5A", "Floor_1_6A",
        };

        private const string BakeToolRel = "Tools/floor_quality_bake.py";
        private const string ManifestRel = "Tools/ao_bake_manifest.json";
        private const string BackupRelDir = "Tools/ao_prebake_backup/DungeonFloor";
        private const string BuilderRel = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";

        private static string ProjRoot() => System.IO.Path.Combine(Application.dataPath, "..");

        private static string S(string rel) =>
            System.IO.File.ReadAllText(System.IO.Path.Combine(ProjRoot(), rel));

        /// <summary>GLB JSON 청크 추출 — 헤더(12B)+청크헤더(8B) 후 JSON.</summary>
        private static string GlbJsonChunk(string path)
        {
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using var br = new System.IO.BinaryReader(fs);
            br.ReadBytes(12);                       // magic+version+total
            uint jlen = br.ReadUInt32();
            uint jtype = br.ReadUInt32();
            Assert.That(jtype, Is.EqualTo(0x4E4F534Au), "[192] JSON 청크 타입: " + path);
            byte[] json = br.ReadBytes((int)jlen);
            return System.Text.Encoding.UTF8.GetString(json);
        }

        // ── ①+③ 수술 아티팩트 — 베이크된 GLB에 COLOR_0 존재, 원본 백업은 무주입 ──
        [Test]
        public void Baked_Glb_Has_Color0_And_Backup_Is_Original()
        {
            foreach (string fid in FloorIds)
            {
                string baked = System.IO.Path.Combine(ProjRoot(), "Assets/Resources/Models/Dungeon", fid + ".glb");
                string backup = System.IO.Path.Combine(ProjRoot(), BackupRelDir, fid + ".glb");
                Assert.That(System.IO.File.Exists(baked), Is.True, "[192] 베이크 GLB 존재: " + fid);
                Assert.That(System.IO.File.Exists(backup), Is.True, "[192] 원본 백업 존재: " + fid);
                Assert.That(GlbJsonChunk(baked), Does.Contain("COLOR_0"),
                    "[192] 수술 GLB COLOR_0 주입: " + fid);
                Assert.That(GlbJsonChunk(backup), Does.Not.Contain("COLOR_0"),
                    "[192] 백업은 수술 전 원본(COLOR_0 무주입): " + fid);
                long bakedLen = new System.IO.FileInfo(baked).Length;
                long backupLen = new System.IO.FileInfo(backup).Length;
                Assert.That(bakedLen, Is.GreaterThan(backupLen),
                    "[192] 수술 GLB는 BIN 성장(COLOR_0 주입+스펙클 PNG): " + fid);
            }
        }

        // ── ③ 매니페스트 — DungeonFloor 6키 ──
        [Test]
        public void Manifest_Has_DungeonFloor_Entries()
        {
            string manifest = S(ManifestRel);
            foreach (string fid in FloorIds)
            {
                Assert.That(manifest, Does.Contain($"\"DungeonFloor/{fid}\""),
                    "[192] AO 매니페스트 DungeonFloor 키: " + fid);
            }
        }

        // ── ④ 빌더 회전 다양화 ──
        [Test]
        public void Tile_Rotation_Hash_Is_Deterministic_4Way()
        {
            string src = S(BuilderRel);
            Assert.That(src, Does.Contain("(cell.x * 13 + cell.y * 29) & 3"),
                "[192] 회전 해시 — 변형 해시(x*31+y*17)와 독립 위상, Random 0건.");
            Assert.That(src, Does.Contain("Quaternion.Euler(0f, rotStep * 90f, 0f)"),
                "[192] 90° 스텝 4방위 적용.");
            Assert.That(src, Does.Not.Contain("Quaternion.identity;   // 회전 0° 고정"),
                "[192] 구 회전 0° 고정 잔존 금지(192차 대체).");
        }

        // ── ⑤ 수술 도구 계약 상수 ──
        [Test]
        public void Bake_Tool_Contract_Constants()
        {
            string tool = S(BakeToolRel);
            Assert.That(tool, Does.Contain("FLOOR_IDS"), "[192] 수술 대상 6종 목록.");
            Assert.That(tool, Does.Contain("DungeonFloor"), "[192] 백업/매니페스트 그룹 키.");
            Assert.That(tool, Does.Contain("EDGE_ANGLE_DEG = 55.0"), "[192] 에지다크닝 문턱.");
            Assert.That(tool, Does.Contain("EDGE_MULT = 0.84"), "[192] 에지다크닝 계수.");
            Assert.That(tool, Does.Contain("SPECKLE_LOW = 0.035"), "[192] 저주파 스펙클 진폭(실내 절차타일 0.06 정합).");
            Assert.That(tool, Does.Contain("SPECKLE_FINE = 0.012"), "[192] 고주파 스펙클 진폭.");
            Assert.That(tool, Does.Contain("compute_ao"), "[192] 174차 AO 공식 재사용(단일소스).");
            Assert.That(tool, Does.Contain("\"--apply\""), "[192] 적용 모드 계약.");
        }
    }
}
