using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 43-PROBE] 모델 에셋(FBX/GLB) 임베디드 머티리얼 셰이더 무결성 전수 진단.
    /// 뿌리: 병사1(병사 FBX) 몸통이 Play에서 순수 마젠타(#FF00FF)로 렌더(영상 픽셀 실측 243,5,241 —
    /// InternalErrorShader 불투명 단색). 프로젝트 .mat 전수는 파이썬 YAML 스캔으로 전부 정상(38 guid 결손 0)이었으므로
    /// 결함은 **FBX/GLB 임베디드(내부 생성) 머티리얼**에 남는다 — .mat 스캔으로는 볼 수 없는 영역.
    /// 진실원: AssetDatabase 로드 → 렌더러 sharedMaterial.shader 실측(읽기 전용 — 에셋 무변경 46/47차 계약).
    /// </summary>
    public class FbxModelShaderProbeTests
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        [Test]
        public void Probe_AllResourceModels_NoErrorShader()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources" });
            Assert.That(guids.Length, Is.GreaterThan(0), "[43-PROBE] Resources 하위 모델 0건 — 스캔 대상 유실");

            var broken = new List<string>();
            int rendererCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    rendererCount++;
                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (Material m in mats)
                    {
                        // sharedMaterials만 읽기 — .material 접근(인스턴스화) 금지(에셋 무변경 계약)
                        if (m == null) { broken.Add($"{path} :: {r.name} :: <null material>"); continue; }
                        if (m.shader == null) { broken.Add($"{path} :: {r.name} :: {m.name} :: <null shader>"); continue; }
                        if (m.shader.name == ErrorShaderName)
                            broken.Add($"{path} :: {r.name} :: {m.name} :: {ErrorShaderName}");
                    }
                }
            }

            Debug.Log($"[43-PROBE] 스캔 모델 {guids.Length}개 / 렌더러 {rendererCount}개 / 파손 {broken.Count}건");
            foreach (string b in broken) Debug.LogWarning($"[43-PROBE] 파손: {b}");
            Assert.That(broken, Is.Empty, $"[43-PROBE] InternalErrorShader/null 셰이더 렌더 {broken.Count}건 — 마젠타 근원");
        }
    }
}
