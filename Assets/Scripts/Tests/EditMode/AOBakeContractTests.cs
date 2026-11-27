using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [174차 P1] 정점 AO 베이크 계약 — Tools/vertex_ao_bake.py 산출물.
    ///
    /// 원리: 포인트클라우드 오클루전(solid-angle 근사·절대 반경 5cm·밀도 정규화)을
    /// COLOR_0에 곱셈 베이크 — Polyfork 월드 소품만(게임 아이템 UserProvided는 스킵 —
    /// 물약 액체색·구슬 floor lift 등 가독성 계약 보존).
    /// 계약: ①매니페스트 수용 수 ②rigged 스킵 ③원본 백업 보존 ④BIN 청크 길이 불변.
    /// </summary>
    public class AOBakeContractTests
    {
        private const string ManifestRelPath = "Tools/ao_bake_manifest.json";
        private const string BackupRootRelPath = "Tools/ao_prebake_backup";
        private const int MinBakedCount = 160; // [174차] ls 실측 204종 중 rigged 42종 스킵 → 162 베이크
        private static readonly string[] SpotIds = { "overflowing-treasure-chest-3d6ee0", "anvil-1286cb", "looted-gilded-throne-008fc3" };
        private static readonly string[] RiggedSpotIds = { "campfire-parent-b1c0d5" };

        private static string ProjRoot()
        {
            return System.IO.Path.Combine(Application.dataPath, "..");
        }

        [Test]
        public void Manifest_BakedCount_And_Skips()
        {
            // [174차] 베이크 매니페스트 — 수용 수·rigged 스킵 실측.
            string path = System.IO.Path.Combine(ProjRoot(), ManifestRelPath);
            Assert.That(File.Exists(path), Is.True, "[174] AO 매니페스트 존재");
            var manifest = MiniJson.Parse(File.ReadAllText(path));
            Assert.That(manifest, Is.Not.Null, "[174] 매니페스트 파싱");
            int baked = 0, riggedSkip = 0;
            foreach (var kv in manifest.Entries)
            {
                if (kv.Value.ContainsKey("verts")) baked++;
                if (kv.Value.ContainsKey("skipped") && kv.Value["skipped"] == "rigged") riggedSkip++;
            }
            Assert.That(baked, Is.GreaterThanOrEqualTo(MinBakedCount), "[174] AO 베이크 수용 수");
            Assert.That(riggedSkip, Is.GreaterThanOrEqualTo(1), "[174] rigged GLB 스킵 계약");
        }

        [Test]
        public void OriginalBackups_Preserved()
        {
            // [174차] 원본 백업 보존 — 재베이크·롤백 가능(Assets 밖, 임포트 무관).
            foreach (string id in SpotIds)
            {
                string bak = System.IO.Path.Combine(ProjRoot(), BackupRootRelPath, "Polyfork", id + ".glb");
                Assert.That(File.Exists(bak), Is.True, "[174] AO 원본 백업 존재: " + id);
            }
        }

        [Test]
        public void BakedGlb_BinChunkLength_Invariant()
        {
            // [174차] BIN 청크 길이 불변 — COLOR_0 f32 곱셈만(§16 틴트 계약 동일).
            foreach (string id in SpotIds)
            {
                string baked = System.IO.Path.Combine(ProjRoot(), "Assets/Resources/Models/Polyfork", id + ".glb");
                string backup = System.IO.Path.Combine(ProjRoot(), BackupRootRelPath, "Polyfork", id + ".glb");
                Assert.That(GlbBinChunkLength(baked), Is.EqualTo(GlbBinChunkLength(backup)),
                    "[174] BIN 청크 길이 불변: " + id);
            }
        }

        private static long GlbBinChunkLength(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            br.ReadBytes(12); // magic+version+total
            while (fs.Position < fs.Length)
            {
                uint len = br.ReadUInt32();
                uint type = br.ReadUInt32();
                if (type == 0x004E4942) return len; // BIN
                fs.Position += len;
            }
            return -1;
        }

        /// <summary>최소 JSON 파서 — 매니페스트(평평한 1레벨 object) 전용.</summary>
        private class MiniJson
        {
            public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> Entries
                = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();

            public static MiniJson Parse(string text)
            {
                var result = new MiniJson();
                // 매니페스트 형태: {"Polyfork/<id>": {"verts": N, ...}, ...} — 간이 파서(키/문자열 값만).
                int i = 0;
                text = text.Trim();
                if (text.Length == 0 || text[0] != '{') return null;
                i = 1;
                while (i < text.Length)
                {
                    int keyStart = text.IndexOf('"', i);
                    if (keyStart < 0) break;
                    int keyEnd = text.IndexOf('"', keyStart + 1);
                    string key = text.Substring(keyStart + 1, keyEnd - keyStart - 1);
                    int objStart = text.IndexOf('{', keyEnd);
                    int objEnd = text.IndexOf('}', objStart);
                    string obj = text.Substring(objStart + 1, objEnd - objStart - 1);
                    var inner = new System.Collections.Generic.Dictionary<string, string>();
                    int j = 0;
                    while (j < obj.Length)
                    {
                        int ks = obj.IndexOf('"', j);
                        if (ks < 0) break;
                        int ke = obj.IndexOf('"', ks + 1);
                        int vs = obj.IndexOf(':', ke);
                        int comma = obj.IndexOf(',', vs);
                        int end = comma < 0 ? obj.Length : comma;
                        string val = obj.Substring(vs + 1, end - vs - 1).Trim().Trim('"');
                        inner[obj.Substring(ks + 1, ke - ks - 1)] = val;
                        j = end + 1;
                    }
                    result.Entries[key] = inner;
                    i = objEnd + 1;
                }
                return result;
            }
        }
    }
}
