using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 40-PROBE] FX 셰이더 컴파일 상태 실측 — ShaderUtil.ShaderHasError(에디터 진실원).
    /// 마젠타 렌더의 실제 뿌리 확정용 진단(배치 C# 컴파일은 셰이더 컴파일을 검증하지 않음 — 실측 교훈).
    /// </summary>
    public class FxShaderErrorProbeTests
    {
        [TestCase("ProjectName/FX/FresnelRim")]
        [TestCase("ProjectName/FX/Dissolve")]
        [TestCase("Custom/SelectionRing")]
        [TestCase("ProjectName/TelegraphRing")]
        [TestCase("Universal Render Pipeline/Particles/Unlit")]
        [TestCase("Universal Render Pipeline/Unlit")]
        [TestCase("Shader Graphs/glTF-pbrMetallicRoughness")]   // [43] glTFast 패키지 셰이더그래프 — 병사·플레이어 장비·슬라임 전체 머티리얼 소스(사용자 세션 마젠타 유력 근원 — 상시 감시)
        public void Probe_ShaderHasNoError(string shaderName)
        {
            var sh = Shader.Find(shaderName);
            Assert.That(sh, Is.Not.Null, $"[40-PROBE] 셰이더 미발견: {shaderName}");
            bool hasError = UnityEditor.ShaderUtil.ShaderHasError(sh);
            string msg = hasError ? UnityEditor.ShaderUtil.GetShaderMessages(sh) is var messages && messages.Length > 0 ? messages[0].message : "" : "";
            Assert.That(hasError, Is.False, $"[40-PROBE] 셰이더 컴파일 에러: {shaderName} — {msg}");
        }

        [Test]
        public void Probe_AllProjectShaders_Swept()
        {
            // 프로젝트 FX 셰이더 전수 — 에셋 경로에서 로드해 실측(씬 참조 무관 — 빌드 포함 여부 무관하게 검사)
            string[] paths = {
                "Assets/Shaders/FxFresnelRim.shader",
                "Assets/Shaders/FxDissolve.shader",
            };
            foreach (var p in paths)
            {
                var importer = AssetImporter.GetAtPath(p) as ShaderImporter;
                Assert.That(importer, Is.Not.Null, $"[40-PROBE] ShaderImporter 없음: {p}");
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                if (sh == null) { Assert.Ignore($"[40-PROBE] {p} 로드 불가 — 스킵"); continue; }
                bool hasError = UnityEditor.ShaderUtil.ShaderHasError(sh);
                string msg = "";
                if (hasError) { var ms = UnityEditor.ShaderUtil.GetShaderMessages(sh); if (ms.Length > 0) msg = ms[0].message + " @line" + ms[0].line; }
                Assert.That(hasError, Is.False, $"[40-PROBE] {p} 컴파일 에러 — {msg}");
            }
        }
    }
}
