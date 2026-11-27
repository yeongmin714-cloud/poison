// Qa71ShaderProbe.cs — [QA 전용 진단 · 무번호 71] 셰이더 컴파일 에러 + URP opaque 텍스처 플래그 에디터 진실원 프로브.
// 배경: 배치 C# 컴파일(-quit -batchmode)은 .shader 파일을 검증하지 않음 → ShaderUtil.ShaderHasError 실측 필수.
// 실행: Unity.exe -quit -batchmode -projectPath C:/Unity/code -executeMethod Qa71ShaderProbe.Run -logFile TestOutput/qa_71_shader_probe.log
// 결과: TestOutput/qa_71_shader_probe.txt (기계판독용) + Unity 로그 [QA71] 라인.
// 성격: 읽기 전용 진단 — 게임플레이 코드·에셋 무접촉, 씬 미오픈. 검증 후 이 파일은 폐기 가능(진단 전용).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class Qa71ShaderProbe
{
    const string ShaderAssetPath = "Assets/Shaders/FXDistortionRing.shader";
    const string ShaderFindName = "ProjectName/FXDistortionRing";
    const string UrpAssetRelPath = "Assets/URP/URPAsset.asset";

    public static void Run()
    {
        var sb = new StringBuilder();
        Action<string> emit = line =>
        {
            sb.AppendLine(line);
            Debug.Log("[QA71] " + line);
        };

        emit("=== QA71 Shader/URP Probe (editor ground truth) ===");
        emit("unity_version=" + Application.unityVersion);

        // ── 1) 셰이더 에러 실측 (에셋 로드 + Shader.Find 이중 확인) ──
        bool anyError = false;
        try
        {
            var byPath = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            emit("shader_asset=" + ShaderAssetPath);
            emit("shader_asset_loaded=" + (byPath != null));

            var byName = Shader.Find(ShaderFindName);
            emit("shader_find=" + ShaderFindName);
            emit("shader_find_found=" + (byName != null));

            var target = byPath != null ? byPath : byName;
            if (target != null)
            {
                bool hasError = UnityEditor.ShaderUtil.ShaderHasError(target);
                emit("ShaderUtil.ShaderHasError=" + hasError);
                anyError |= hasError;

                try
                {
                    int msgCount = UnityEditor.ShaderUtil.GetShaderMessageCount(target);
                    emit("ShaderUtil.GetShaderMessageCount=" + msgCount);
                }
                catch (Exception me)
                {
                    emit("GetShaderMessageCount_unavailable=" + me.GetType().Name);
                }
            }
            else
            {
                emit("ShaderUtil.ShaderHasError=NOT_EVALUATED (shader missing — FAIL)");
                anyError = true;
            }
        }
        catch (Exception e)
        {
            emit("SHADER_PROBE_EXCEPTION=" + e.GetType().Name + ": " + e.Message);
            anyError = true;
        }

        // ── 2) URP 자산 opaque 텍스처 플래그 — 원본 YAML + 리플렉션 이중 실측 ──
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string urpFullPath = Path.Combine(projectRoot, UrpAssetRelPath.Replace('/', Path.DirectorySeparatorChar));
            emit("urp_asset=" + UrpAssetRelPath);
            emit("urp_asset_exists=" + File.Exists(urpFullPath));

            if (File.Exists(urpFullPath))
            {
                var lines = File.ReadAllLines(urpFullPath);
                var flagLines = lines
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith("m_RequireOpaqueTexture") || l.StartsWith("m_RequireDepthTexture"))
                    .ToList();
                foreach (var l in flagLines)
                    emit("yaml_" + l);

                // 리플렉션 실측 — UniversalRenderPipelineAsset.supportsCameraOpaqueTexture
                var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetRelPath);
                emit("urp_loaded=" + (rp != null) + " type=" + (rp != null ? rp.GetType().FullName : "n/a"));
                if (rp != null)
                {
                    var t = rp.GetType();
                    var prop = t.GetProperty("supportsCameraOpaqueTexture", BindingFlags.Public | BindingFlags.Instance)
                            ?? t.GetProperty("supportsOpaqueTexture", BindingFlags.Public | BindingFlags.Instance);
                    emit("reflection_property=" + (prop != null ? prop.Name : "MISSING"));
                    if (prop != null)
                        emit("supportsCameraOpaqueTexture=" + prop.GetValue(rp, null));

                    // 활성 파이프라인 자산 동일성 확인 — 이 자산이 실제 적용 중인지
                    try
                    {
                        var active = GraphicsSettings.currentRenderPipeline;
                        emit("active_pipeline_type=" + (active != null ? active.GetType().FullName : "null"));
                        emit("urp_asset_is_active=" + (active != null && ReferenceEquals(active, rp)));
                    }
                    catch (Exception ge)
                    {
                        emit("active_pipeline_check_failed=" + ge.GetType().Name);
                    }
                }
            }
        }
        catch (Exception e)
        {
            emit("URP_PROBE_EXCEPTION=" + e.GetType().Name + ": " + e.Message);
            anyError = true;
        }

        emit("QA71_VERDICT=" + (anyError ? "FAIL" : "PASS"));

        // 기계판독용 결과 파일 — 프로젝트 루트/TestOutput
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, "TestOutput");
            Directory.CreateDirectory(outDir);
            string outFile = Path.Combine(outDir, "qa_71_shader_probe.txt");
            File.WriteAllText(outFile, sb.ToString());
            Debug.Log("[QA71] result_written=" + outFile);
        }
        catch (Exception we)
        {
            Debug.LogError("[QA71] result_write_failed=" + we.Message);
        }
    }
}
