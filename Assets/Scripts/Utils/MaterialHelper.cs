using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.Core.Utils
{
    public static class MaterialHelper
    {
        /// <summary>
        /// URP/Lit 셰이더를 찾거나, 실패 시 파이프라인의 기본 머티리얼을 복제한다.
        /// </summary>
        public static Material CreateLitMaterial(Color color, string name = "DynamicMat")
        {
            Material mat;

            // 1) Shader.Find 시도 (URP Lit 셰이더)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    mat = new Material(shader);
                    goto ApplyColor;
                }
            }

            // 2) RenderPipeline의 default material 사용
            {
                var pipeline = GraphicsSettings.defaultRenderPipeline;
                if (pipeline != null)
                {
                    var defaultMat = pipeline.defaultMaterial;
                    if (defaultMat != null)
                    {
                        mat = new Material(defaultMat);
                        goto ApplyColor;
                    }
                }
            }

            // 3) Built-in Standard (URP가 없는 환경)
            {
                Shader shader = Shader.Find("Standard");
                if (shader != null)
                {
                    mat = new Material(shader);
                    goto ApplyColor;
                }
            }

            // 4) 최후의 수단
            {
                Shader shader = Shader.Find("Diffuse");
                if (shader != null)
                {
                    mat = new Material(shader);
                    goto ApplyColor;
                }
            }

            return null;

        ApplyColor:
            mat.color = color;
            mat.name = name;
            return mat;
        }
    }
}