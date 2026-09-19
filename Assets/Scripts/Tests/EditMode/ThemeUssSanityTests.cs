using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// U9-W1 회귀 가드: USS에서 그라데이션 함수 사용 금지 + url() 대상 존재 검증.
    /// 뿌리: Unity 6는 background-image의 linear/radial-gradient(Function)를 텍스처로 읽지 못해
    /// "Invalid value for image texture Function" 경고 + 렌더 미적용. → 베이크 PNG만 허용.
    /// </summary>
    public class ThemeUssSanityTests
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static IEnumerable<string> EnumerateUssFiles()
        {
            var root = Path.Combine(ProjectRoot, "Assets");
            foreach (var file in Directory.GetFiles(root, "*.uss", SearchOption.AllDirectories))
                yield return file;
        }

        [Test]
        public void NoUssFile_UsesGradientFunctions()
        {
            foreach (var file in EnumerateUssFiles())
            {
                string content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains("linear-gradient("),
                    $"그라데이션 함수 금지(베이크 PNG 사용): {file}");
                Assert.IsFalse(content.Contains("radial-gradient("),
                    $"그라데이션 함수 금지(베이크 PNG 사용): {file}");
            }
        }

        [Test]
        public void EveryUssUrlReference_ExistsOnDisk()
        {
            int checkedCount = 0;
            foreach (var file in EnumerateUssFiles())
            {
                string ussDir = Path.GetDirectoryName(file);
                string content = File.ReadAllText(file);
                int idx = 0;
                while (true)
                {
                    int start = content.IndexOf("url(\"", idx);
                    if (start < 0) break;
                    int pathStart = start + 5;
                    int end = content.IndexOf("\")", pathStart);
                    Assert.Greater(end, pathStart, $"url 경로 파싱 실패: {file}");
                    string relPath = content.Substring(pathStart, end - pathStart);
                    // U9-W3: url()은 USS 파일 기준 '상대경로'만 허용 — 절대 "Assets/..." 경로는
                    // 런타임 패널에서 해석 실패 → 노란 경고 플레이스홀더 렌더의 뿌리.
                    Assert.IsFalse(relPath.StartsWith("Assets/"),
                        $"절대경로 url 금지(USS 파일 기준 상대경로 필수): {relPath} (in {file})");
                    string absPath = Path.Combine(ussDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                    Assert.IsTrue(File.Exists(absPath), $"url 대상 누락: {relPath} (in {file})");
                    checkedCount++;
                    idx = end;
                }
            }
            Assert.Greater(checkedCount, 0, "검증한 url 참조가 최소 1개 이상");
        }

        [Test]
        public void ThemeUss_HasBakedBackgroundPngs()
        {
            string themePath = Path.Combine(ProjectRoot,
                "Assets", "Resources", "UI", "Theme.uss");
            Assert.IsTrue(File.Exists(themePath), "Theme.uss 존재");

            string content = File.ReadAllText(themePath);
            StringAssert.Contains("bg_window.png", content, "윈도우 배경 베이크 PNG");
            StringAssert.Contains("bg_button.png", content, "버튼 배경 베이크 PNG");
            StringAssert.Contains("bg_slot.png", content, "슬롯 인셋 베이크 PNG");
            StringAssert.Contains("glow_hover_gold.png", content, "호버 골드 글로우 PNG");
            StringAssert.Contains("bg_tooltip.png", content, "툴팁/토스트 베이크 PNG");
            StringAssert.Contains("shadow_glow.png", content, "소프트 섀도우 PNG");
        }

        [Test]
        public void ThemeUss_BakedPngs_ImportAsSpriteOrTexture()
        {
            // url()로 참조되는 PNG가 임포트 자산으로 존재하는지 (잉여 0바이트/미임포트 방지)
            foreach (var pngName in new[] {
                "bg_window.png", "bg_tooltip.png", "bg_button.png",
                "bg_button_hover.png", "bg_slot.png", "glow_hover_gold.png", "shadow_glow.png" })
            {
                string path = Path.Combine(ProjectRoot, "Assets", "Resources", "UI", pngName);
                var metaPath = path + ".meta";
                Assert.IsTrue(File.Exists(path), $"PNG 존재: {pngName}");
                Assert.IsTrue(File.Exists(metaPath), $"meta 존재(임포트 완료): {pngName}");
                Assert.Greater(new FileInfo(path).Length, 100, $"PNG 내용 있음: {pngName}");
            }
        }
    }
}
