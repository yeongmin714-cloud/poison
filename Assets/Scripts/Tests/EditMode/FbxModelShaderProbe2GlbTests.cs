using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 43-PROBE2] GLB(glTF) 에셋 임베디드 머티리얼 셰이더 무결성 진단.
    /// 1차 프루브(t:Model — FBX 131건)는 전부 정상이었고, 병사1 위치 마젠타(픽셀 실측 #FF00FF 불투명)는
    /// 런타임 지속 렌더이므로, t:Model로 잡히지 않는 glTF 임포트 결과(GameObject 프리팹 — glTFast)를 별도 스캔한다.
    /// 무기 GLB(steel_sword 등)는 병사/플레이어 손에 상시 부착 — 파손 시 "몬스터 피격(전투)시 보이는 지속 마젠타 리본".
    /// 진실원: AssetDatabase 로드 → 렌더러 sharedMaterial.shader 실측(읽기 전용 — 에셋 무변경 46/47차 계약).
    /// </summary>
    public class FbxModelShaderProbe2GlbTests
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        [Test]
        public void Probe_AllGlbAssets_NoErrorShader()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Resources/Models" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            // t:GameObject는 .glb 프리팹 외에도 잡힐 수 있으니 .glb/.fbx 확장자만 남김(t:Model 중복 허용 — 읽기만 하므로 무해)
            paths.RemoveAll(p => !p.EndsWith(".glb") && !p.EndsWith(".fbx"));
            // .glb이 정확히 스캔되는지: 하드 경로 폴백(Importer 우회)
            foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/Resources/Models/UserProvided" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".glb") && !paths.Contains(p)) paths.Add(p);
            }

            Assert.That(paths, Is.Not.Empty, "[43-PROBE2] GLB 경로 0건 — 스캔 대상 유실");
            Debug.Log($"[43-PROBE2] 스캔 대상 {paths.Count}개 (glb/fbx)");

            var broken = new List<string>();
            int rendererCount = 0;
            foreach (string path in paths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) { broken.Add($"{path} :: <로드 실패>"); continue; }

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

            Debug.Log($"[43-PROBE2] 스캔 {paths.Count}개 / 렌더러 {rendererCount}개 / 파손 {broken.Count}건");
            foreach (string b in broken) Debug.LogWarning($"[43-PROBE2] 파손: {b}");
            Assert.That(broken, Is.Empty, $"[43-PROBE2] InternalErrorShader/null 셰이더 렌더 {broken.Count}건 — 마젠타 근원");
        }
    }
}
