using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [199차 옵션1] bow JPEG 감량 계약 — wood_bow/stone_bow 임베드 4096² JPEG(13MB×2) → 1024 재임베드.
    /// 시각 동등 전제(아이콘 1024RT·월드 노출 크기), 원본은 Tools/bow_prebake_backup199/ 보존(롤백 가능).
    /// </summary>
    public class BowSlim199Tests
    {
        private const string Dir = "Assets/Resources/Models/UserProvided";
        private const string BackupDir = "Tools/bow_prebake_backup199";

        private static (string magic, int w, int h, int bytes) PeekImage(string glbPath)
        {
            // GLB 헤더/청크 직독 — 임베드 첫 이미지의 매직/해상도/길이
            var data = File.ReadAllBytes(glbPath);
            Assert.That(data.Length, Is.GreaterThan(20));
            Assert.That(System.Text.Encoding.ASCII.GetString(data, 0, 4), Is.EqualTo("glTF"));
            int off = 12;
            string json = null;
            while (off + 8 <= data.Length)
            {
                int clen = System.BitConverter.ToInt32(data, off);
                int ctype = System.BitConverter.ToInt32(data, off + 4);
                if (ctype == 0x4E4F534A)
                    json = Encoding.UTF8.GetString(data, off + 8, clen);
                off += 8 + clen;
            }
            Assert.That(json, Is.Not.Null);
            int bOff = 0;
            off = 12;
            while (off + 8 <= data.Length)
            {
                int clen = System.BitConverter.ToInt32(data, off);
                int ctype = System.BitConverter.ToInt32(data, off + 4);
                if (ctype == 0x004E4942) { bOff = off + 8; break; }
                off += 8 + clen;
            }
            // images→bufferView 역추적은 원시 문자열 처리(193차 images[0] 무단추정 금지 준수) —
            // baseColorTexture 경유라 images 배열 순서와 무관하게 첫 이미지 뷰를 잠그지 않음.
            int view = ExtractFirstImageBufferView(json);
            Assert.That(view, Is.AtLeast(0), "[199] images[0].bufferView 역추적 실패: " + glbPath);
            int vOff = ExtractBufferViewField(json, view, "byteOffset");
            int vLen = ExtractBufferViewField(json, view, "byteLength");
            var blob = new byte[vLen];
            System.Array.Copy(data, bOff + vOff, blob, 0, vLen);
            Assert.That(blob.Length, Is.GreaterThan(2));
            string magic = (blob[0] == 0xFF && blob[1] == 0xD8) ? "jpeg"
                : (blob[0] == 0x89 && blob[1] == 0x50) ? "png" : "unknown";
            // 해상도 — PNG IHDR / JPEG SOF
            int w = 0, h = 0;
            if (magic == "png")
            {
                w = (blob[16] << 24) | (blob[17] << 16) | (blob[18] << 8) | blob[19];
                h = (blob[20] << 24) | (blob[21] << 16) | (blob[22] << 8) | blob[23];
            }
            else if (magic == "jpeg")
            {
                int i = 2;
                while (i < blob.Length - 8)
                {
                    if (blob[i] != 0xFF) { i++; continue; }
                    int marker = blob[i + 1];
                    if (marker >= 0xC0 && marker <= 0xC3)
                    {
                        h = (blob[i + 5] << 8) | blob[i + 6];
                        w = (blob[i + 7] << 8) | blob[i + 8];
                        break;
                    }
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                    int seg = (blob[i + 2] << 8) | blob[i + 3];
                    i += 2 + seg;
                }
            }
            return (magic, w, h, blob.Length);
        }

        private static int ExtractFirstImageBufferView(string json)
        {
            // "images":[{"bufferView":N, ...}] 첫 항목의 bufferView 정수
            int i = json.IndexOf("\"images\"");
            if (i < 0) return -1;
            int bv = json.IndexOf("\"bufferView\"", i);
            if (bv < 0) return -1;
            int colon = json.IndexOf(':', bv);
            int s = colon + 1;
            while (s < json.Length && (json[s] == ' ')) s++;
            int e = s;
            while (e < json.Length && char.IsDigit(json[e])) e++;
            return int.Parse(json.Substring(s, e - s));
        }

        private static int ExtractBufferViewField(string json, int viewIndex, string field)
        {
            // "bufferViews":[ ... ] 내 viewIndex번째 객체의 {field} 값
            int i = json.IndexOf("\"bufferViews\"");
            int depth = 0, seen = -1, k = i;
            while (k < json.Length)
            {
                char c = json[k];
                if (c == '[') { depth++; k++; continue; }
                if (c == '{' && depth == 1) { seen++; }
                if (c == ']' && depth == 1) break;
                if (seen == viewIndex && depth == 1)
                {
                    int f = json.IndexOf("\"" + field + "\"", k);
                    if (f < 0) return 0;
                    int colon = json.IndexOf(':', f);
                    int s = colon + 1;
                    while (json[s] == ' ') s++;
                    int e = s;
                    while (char.IsDigit(json[e])) e++;
                    return int.Parse(json.Substring(s, e - s));
                }
                if (c == '{') depth++;
                if (c == '}') depth--;
                k++;
            }
            return 0;
        }

        [Test]
        public void BowGlb_EmbeddedJpeg_SlimmedTo1024()
        {
            foreach (var name in new[] { "wood_bow", "stone_bow" })
            {
                var (magic, w, h, bytes) = PeekImage($"{Dir}/{name}.glb");
                Assert.That(magic, Is.EqualTo("jpeg"), "[199] bow 임베드는 JPEG 유지: " + name);
                Assert.That(System.Math.Max(w, h), Is.EqualTo(1024),
                    "[199] bow 임베드 해상도 1024 감량: " + name);
                Assert.That(bytes, Is.LessThan(600_000), "[199] bow 텍스처 ~0.4MB급: " + name);
                var fi = new FileInfo($"{Dir}/{name}.glb");
                Assert.That(fi.Length, Is.LessThan(4_500_000), "[199] bow GLB 전체 <4.5MB: " + name);
            }
        }

        [Test]
        public void BowBackup_Original4096_Preserved()
        {
            foreach (var name in new[] { "wood_bow", "stone_bow" })
            {
                var bak = $"{BackupDir}/{name}.glb";
                Assert.That(File.Exists(bak), Is.True, "[199] 감량 원본 백업 보존: " + name);
                var (magic, w, h, _) = PeekImage(bak);
                Assert.That(magic, Is.EqualTo("jpeg"), "[199] 백업은 원본 JPEG: " + name);
                Assert.That(System.Math.Max(w, h), Is.EqualTo(4096), "[199] 백업 원본 해상도 4096: " + name);
            }
        }
    }

    /// <summary>
    /// [199차 옵션2] dish COLOR_0 정점수술 계약 — 12종 UV붕괴 팔레트형에 AO×에지다크닝 주입.
    /// 팔레트 PNG는 바이트 무수정(백업 대조), 원본 백업 Tools/dish_prebake_backup199/ 보존,
    /// 충돌 프록시(POSITION 전용 프리미티브)는 수술 비대상.
    /// </summary>
    public class DishVertexSurgery199Tests
    {
        private const string Dir = "Assets/Resources/Models/UserProvided";
        private const string BackupDir = "Tools/dish_prebake_backup199";
        private static readonly string[] DishIds =
        {
            "dish_braise", "dish_jelly", "dish_jeon", "dish_pickle", "dish_pie", "dish_ricebowl",
            "dish_roll", "dish_sashimi", "dish_steak", "dish_steam", "dish_stew", "dish_stirfry",
        };

        private static string ParseJsonChunk(string glbPath, out int binOffset, out byte[] data)
        {
            data = File.ReadAllBytes(glbPath);
            int off = 12;
            string json = null;
            binOffset = 0;
            while (off + 8 <= data.Length)
            {
                int clen = System.BitConverter.ToInt32(data, off);
                int ctype = System.BitConverter.ToInt32(data, off + 4);
                if (ctype == 0x4E4F534A) json = Encoding.UTF8.GetString(data, off + 8, clen);
                else if (ctype == 0x004E4942) binOffset = off + 8;
                off += 8 + clen;
            }
            return json;
        }

        [Test]
        public void DishGlb_VisualPrims_HaveColor0()
        {
            foreach (var id in DishIds)
            {
                string json = ParseJsonChunk($"{Dir}/{id}.glb", out int bOff, out byte[] data);
                Assert.That(json, Is.Not.Null, "[199] JSON 청크: " + id);
                Assert.That(json.Contains("\"COLOR_0\""), Is.True,
                    "[199] 시각 메시 COLOR_0 주입 확인: " + id);
                // 충돌 프록시(POSITION 전용) 잔존 확인 — 수술이 전 프리미티브 훼손이 아님
                Assert.That(json.Contains("\"OMI_physics_body\"") || json.Contains("\"POSITION\""), Is.True,
                    "[199] GLB 구조 무손상: " + id);
            }
        }

        [Test]
        public void DishPalettePng_ByteIdenticalToBackup()
        {
            foreach (var id in DishIds)
            {
                string json = ParseJsonChunk($"{Dir}/{id}.glb", out int bOff, out byte[] data);
                string jsonBak = ParseJsonChunk($"{BackupDir}/{id}.glb", out int bOffB, out byte[] dataB);
                Assert.That(File.Exists($"{BackupDir}/{id}.glb"), Is.True, "[199] 수술 원본 백업: " + id);

                int vCur = ExtractImageBytes(json, data, bOff, out byte[] imgCur);
                int vBak = ExtractImageBytes(jsonBak, dataB, bOffB, out byte[] imgBak);
                Assert.That(vCur, Is.AtLeast(0).And.EqualTo(vBak), "[199] 이미지 버퍼뷰 동일성: " + id);
                Assert.That(imgCur.Length, Is.EqualTo(imgBak.Length), "[199] 팔레트 PNG 길이 무변경: " + id);
                for (int i = 0; i < imgCur.Length; i++)
                {
                    if (imgCur[i] != imgBak[i])
                        Assert.Fail($"[199] 팔레트 PNG 바이트 변경 감지 — {id} @byte{i} (무수정 계약 위반)");
                }
            }
        }

        [Test]
        public void DishManifest_SurgeryRecorded12()
        {
            var mpath = "Tools/dish_bake_manifest_199.json";
            Assert.That(File.Exists(mpath), Is.True, "[199] 수술 매니페스트 존재");
            var txt = File.ReadAllText(mpath);
            int keys = 0;
            foreach (var id in DishIds)
                if (txt.Contains($"UserProvided/{id}")) keys++;
            Assert.That(keys, Is.EqualTo(12), "[199] 매니페스트 12종 기록");
        }

        private static int ExtractImageBytes(string json, byte[] data, int bOff, out byte[] img)
        {
            // 첫 images[0].bufferView → bufferViews[N]의 byteOffset/byteLength → BIN 슬라이스
            int i = json.IndexOf("\"images\"");
            int bvIdx = -1;
            if (i >= 0)
            {
                int bv = json.IndexOf("\"bufferView\"", i);
                int colon = json.IndexOf(':', bv);
                int s = colon + 1;
                while (json[s] == ' ') s++;
                int e = s;
                while (char.IsDigit(json[e])) e++;
                bvIdx = int.Parse(json.Substring(s, e - s));
            }
            img = null;
            if (bvIdx < 0) return -1;
            int off = 0, len = 0, seen = -1, k = json.IndexOf("\"bufferViews\"");
            int depth = 0;
            while (k < json.Length)
            {
                char c = json[k];
                if (c == '[') { depth++; k++; continue; }
                if (c == '{' && depth == 1) seen++;
                if (c == ']' && depth == 1) break;
                if (seen == bvIdx && depth == 1)
                {
                    off = ReadIntField(json, ref k, "byteOffset");
                    len = ReadIntField(json, ref k, "byteLength");
                    break;
                }
                if (c == '{') depth++;
                if (c == '}') depth--;
                k++;
            }
            img = new byte[len];
            System.Array.Copy(data, bOff + off, img, 0, len);
            return bvIdx;
        }

        private static int ReadIntField(string json, ref int k, string field)
        {
            int f = json.IndexOf("\"" + field + "\"", k);
            if (f < 0) return 0;
            int colon = json.IndexOf(':', f);
            int s = colon + 1;
            while (json[s] == ' ') s++;
            int e = s;
            while (char.IsDigit(json[e])) e++;
            return int.Parse(json.Substring(s, e - s));
        }
    }
}
