using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.Core
{
    public static class MaterialHelper
    {
        /// <summary>
        /// URP/Lit 셰이더를 찾거나, 실패 시 파이프라인의 기본 머티리얼을 복제한다.
        /// </summary>
        public static Material CreateLitMaterial(Color color, string name = "DynamicMat")
        {
            Material mat = null;

            // 1) Shader.Find 시도 (가장 일반적인 URP 셰이더)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                mat = new Material(shader);
            }

            // 2) RenderPipeline의 default material 사용
            if (mat == null)
            {
                var pipeline = GraphicsSettings.defaultRenderPipeline;
                if (pipeline != null)
                {
                    var defaultMat = pipeline.defaultMaterial;
                    if (defaultMat != null)
                        mat = new Material(defaultMat);
                }
            }

            // 3) 마지막 시도: Built-in Standard (URP가 없으면 기본)
            if (mat == null)
            {
                shader = Shader.Find("Standard");
                if (shader != null) mat = new Material(shader);
            }

            // 4) 최후의 수단: 아무 셰이더나
            if (mat == null)
            {
                shader = Shader.Find("Diffuse");
                if (shader != null) mat = new Material(shader);
            }

            if (mat != null)
            {
                mat.color = color;
                mat.name = name;
            }

            return mat;
        }
    }
}