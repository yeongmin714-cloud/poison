using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 43-DIAG] 병사/플레이어 FBX 내부 머티리얼 셰이더명 실측 — 파이프라인 정합 진단.
    /// 뿌리 가설: GuardManager 병사(soldier_lv*.fbx) 내부 머티리얼이 **빌트인 'Standard' 등 비(非)URP 셰이더**를
    /// 참조하면 URP에서 **불투명 마젠타**로 렌더(ShaderUtil 에러 0 — 프루브 기존 조건으로 미검출).
    /// 픽셀 실측 근거: 공격씬 영상 마젠타 = #FF00FF 불투명, 병사1 전신, 영상 전 구간 지속.
    /// </summary>
    public class SoldierModelShaderDiagTests
    {
        [Test]
        public void Diag_SoldierFbx_Shaders_AreUrp()
        {
            string[] paths = {
                "Assets/Resources/Models/UserProvided/fbx/soldier_lv1-20_rigged.fbx",
                "Assets/Resources/Models/UserProvided/fbx/soldier_lv20-40_rigged.fbx",
                "Assets/Resources/Models/UserProvided/fbx/soldier_lv40-50_rigged.fbx",
                "Assets/Resources/Models/UserProvided/fbx/Player_Rigged.fbx",
            };
            foreach (string p in paths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (model == null) { Assert.Fail($"[43-DIAG] 로드 실패: {p}"); continue; }

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null)
                        {
                            Assert.Fail($"[43-DIAG] {p} :: {r.name} :: null 머티리얼/셰이더");
                            continue;
                        }
                        // URP 정합 판정 — 비URP 셰이더(빌트인 Standard 등)는 URP에서 마젠타 렌더
                        bool isUrp = m.shader.name.StartsWith("Universal Render Pipeline")
                                     || m.shader.name.StartsWith("Shader Graphs/")
                                     || m.shader.name.StartsWith("ProjectName/");
                        Debug.Log($"[43-DIAG] {System.IO.Path.GetFileName(p)} :: {r.name} :: '{m.name}' shader='{m.shader.name}' URP정합={isUrp}");
                        Assert.That(isUrp, Is.True,
                            $"[43-DIAG] 비URP 셰이더(URP 마젠타 렌더 근원): {p} :: {r.name} :: '{m.name}' shader='{m.shader.name}'");
                    }
                }
            }
        }
    }
}
