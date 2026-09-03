using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G1-09: Static utility for upgrading WaterBody and LakeGenerator materials
    /// to high-quality URP Lit water with reflection probe support, SSR compatibility,
    /// 2-axis wave animation, and depth-based color blending.
    /// </summary>
    public static class WaterMaterialUpgrader
    {
        /// <summary>Shallow water base color.</summary>
        public static readonly Color ShallowColor = new Color(0.1f, 0.4f, 0.7f, 0.6f);

        /// <summary>Deep water base color.</summary>
        public static readonly Color DeepColor = new Color(0.0f, 0.1f, 0.3f, 0.8f);

        /// <summary>Target smoothness for reflection clarity.</summary>
        private const float TargetSmoothness = 0.8f;

        /// <summary>Target metallic (0 = non-metallic for proper reflections).</summary>
        private const float TargetMetallic = 0.0f;

        /// <summary>Render queue for transparent materials.</summary>
        private const int TransparentQueue = 3000;

        // ─────────────────────────────────────────────────────────────────
        // P-3: Idyllic fantasy water (translucent lake surface)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Bright, clear fantasy lake color (semi-transparent).</summary>
        public static readonly Color FantasyLakeColor = new Color(0.3f, 0.6f, 0.75f, 0.55f);

        /// <summary>Default UV flow speed along U (UV units per second).</summary>
        public const float DefaultFlowSpeedX = 0.02f;

        /// <summary>Default UV flow speed along V (UV units per second).</summary>
        public const float DefaultFlowSpeedZ = 0.012f;

        /// <summary>Default tiling of the water base map on the lake surface plane.</summary>
        public static readonly Vector2 WaterTextureTiling = new Vector2(3f, 3f);

        private const string IdyllicTextureSourceDir = "Assets/Idyllic Fantasy Nature/Textures/Water";
        private const string IdyllicTextureResourceDir = "Assets/Resources/Water";
        private const string IdyllicMainTextureResource = "Water/Water_Normal_01";

        private static bool _resourcesEnsured;
        private static Texture2D _idyllicWaterTexture;
        private static Texture2D _tintedWaterTexture;
        private static Color _tintedWaterCacheColor = new Color(0f, 0f, 0f, 0f);
        private static bool _pipelineFlagsEnsured;
        private static bool _idyllicShaderUnavailableLogged;

        /// <summary>
        /// Creates a URP Lit water material configured for reflection probes,
        /// SSR support, depth-based coloring, and transparent rendering.
        /// </summary>
        /// <param name="materialName">Name for the new material asset.</param>
        /// <param name="shallowWeight">Blend weight toward shallow color (0 = deep, 1 = shallow).</param>
        /// <returns>A fully configured URP Lit water material, or a fallback material if URP Lit shader is unavailable.</returns>
        public static Material CreateUpgradedWaterMaterial(string materialName, float shallowWeight = 0.5f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat;

            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                // Fallback: try URP pipeline default material
                var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null && pipeline.defaultMaterial != null)
                {
                    mat = new Material(pipeline.defaultMaterial);
                }
                else
                {
                    shader = Shader.Find("Standard");
                    mat = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
                }
            }

            mat.name = string.IsNullOrEmpty(materialName) ? "Upgraded_Water_Mat" : materialName;

            // Depth-based color: blend between shallow and deep
            Color waterColor = Color.Lerp(DeepColor, ShallowColor, Mathf.Clamp01(shallowWeight));
            // URP Lit uses _BaseColor; also set legacy _Color for fallback shaders
            mat.SetColor("_BaseColor", waterColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", waterColor);

            // Smoothness and Metallic for reflection fidelity
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", TargetSmoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", TargetMetallic);

            // Reflection probe keywords
            mat.EnableKeyword("_REFLECTION_PROBE_BLENDING");
            mat.EnableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");

            // Transparent surface type (URP Lit manages blend state internally via _Blend)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);         // 0 = Alpha blending
            mat.SetFloat("_BlendMode", 0f);     // legacy blend alias (no-op on URP Lit, kept for tooling)
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = TransparentQueue;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }

        /// <summary>
        /// Creates a simple transparent material (original fallback style).
        /// Used by the Reset operation to restore the pre-upgrade appearance.
        /// </summary>
        /// <param name="materialName">Name for the material.</param>
        /// <param name="color">Base color (with alpha for transparency).</param>
        /// <returns>A simple transparent material.</returns>
        public static Material CreateSimpleWaterMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat;

            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null && pipeline.defaultMaterial != null)
                {
                    mat = new Material(pipeline.defaultMaterial);
                }
                else
                {
                    shader = Shader.Find("Standard");
                    mat = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
                }
            }

            mat.name = string.IsNullOrEmpty(materialName) ? "Simple_Water_Mat" : materialName;
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            // Simple transparent setup without reflection keywords (URP Lit manages blend state via _Blend)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);         // 0 = Alpha blending
            mat.SetFloat("_BlendMode", 0f);     // legacy blend alias (no-op on URP Lit, kept for tooling)
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = TransparentQueue;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // Disable reflection probe keywords
            mat.DisableKeyword("_REFLECTION_PROBE_BLENDING");
            mat.DisableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.5f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.0f);

            return mat;
        }

        /// <summary>
        /// Computes a 2-axis sine wave offset using both X and Z spatial dimensions.
        /// Produces a more natural water surface animation than single-axis waves.
        /// </summary>
        /// <param name="time">Current time value (typically Time.time).</param>
        /// <param name="speed">Wave animation speed.</param>
        /// <param name="amplitude">Wave height amplitude.</param>
        /// <param name="xPos">X position of the surface point.</param>
        /// <param name="zPos">Z position of the surface point.</param>
        /// <returns>Y-offset value combining X-axis and Z-axis wave contributions.</returns>
        public static float Compute2AxisWaveOffset(float time, float speed, float amplitude, float xPos, float zPos)
        {
            // X-axis wave: sine based on time and X position
            float waveX = Mathf.Sin(time * speed + xPos * 0.5f) * amplitude;

            // Z-axis wave: cosine-based with slightly different speed and Z position
            float waveZ = Mathf.Cos(time * speed * 0.8f + zPos * 0.3f) * amplitude;

            // Blend both axes for a combined wave effect
            return (waveX + waveZ) * 0.5f;
        }

        /// <summary>
        /// Applies a subtle vertex color normal-map effect to a mesh by varying
        /// vertex colors with a slight blue-green tint offset. This creates a
        /// perceived normal variation on URP Lit surfaces that use vertex colors.
        /// Operates on a copy of the mesh to avoid mutating the source asset.
        /// </summary>
        /// <param name="mesh">The mesh to modify. Must be readable (isReadable = true).</param>
        /// <param name="offsetMagnitude">Magnitude of the color offset (default 0.05).</param>
        /// <returns>The modified mesh copy, or null if the mesh is null/not readable.</returns>
        public static Mesh ApplyVertexColorNormalEffect(Mesh mesh, float offsetMagnitude = 0.05f)
        {
            if (mesh == null) return null;
            if (!mesh.isReadable) return null;

            // Create a writable copy to avoid modifying the source asset
            Mesh writableMesh = InstantiateMesh(mesh);

            Vector3[] vertices = writableMesh.vertices;
            Color[] colors = new Color[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                // Slight color variation based on vertex position to simulate normal variation
                float rOffset = Mathf.Sin(vertices[i].x * 2.3f + vertices[i].z * 1.7f) * offsetMagnitude;
                float gOffset = Mathf.Cos(vertices[i].z * 2.1f + vertices[i].x * 1.3f) * offsetMagnitude;
                float bOffset = Mathf.Sin((vertices[i].x + vertices[i].z) * 1.9f) * offsetMagnitude;

                colors[i] = new Color(
                    0.5f + rOffset,
                    0.5f + gOffset,
                    0.5f + bOffset,
                    1.0f
                );
            }

            writableMesh.colors = colors;
            writableMesh.UploadMeshData(false);
            return writableMesh;
        }

        /// <summary>
        /// Creates a writable copy of a mesh.
        /// </summary>
        private static Mesh InstantiateMesh(Mesh source)
        {
            Mesh clone = new Mesh();
            clone.name = source.name + " (WaterColorFX)";
            clone.vertices = source.vertices;
            clone.triangles = source.triangles;
            clone.normals = source.normals;
            clone.uv = source.uv;
            clone.tangents = source.tangents;
            clone.bindposes = source.bindposes;
            clone.boneWeights = source.boneWeights;
            clone.colors = source.colors;
            clone.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
                clone.SetTriangles(source.GetTriangles(i), i);
            clone.RecalculateBounds();
            return clone;
        }

        // ─────────────────────────────────────────────────────────────────
        // T-R5: turquoise/emerald lake palette + nation tint
        // ─────────────────────────────────────────────────────────────────

        /// <summary>표준 터쿼이즈 색 (그 외 국가 — 동/서/황제국).</summary>
        public static readonly Color TurquoiseShallow = new Color(0.30f, 0.74f, 0.66f, 0.60f);
        /// <summary>에메랄드 deep 그라데이션 끝 (표준).</summary>
        public static readonly Color EmeraldDeep = new Color(0.05f, 0.42f, 0.32f, 0.85f);

        /// <summary>남쪽 = 따뜻한 청록 (r/g 상향, b 소폭 낮음).</summary>
        public static readonly Color SouthTurquoiseShallow = new Color(0.38f, 0.76f, 0.60f, 0.60f);
        /// <summary>남쪽 deep — 따뜻한 에메랄드.</summary>
        public static readonly Color SouthEmeraldDeep = new Color(0.12f, 0.47f, 0.30f, 0.85f);

        /// <summary>북쪽 = 차가운 청색 (b 상향, g/r 낮음).</summary>
        public static readonly Color NorthCoolShallow = new Color(0.26f, 0.66f, 0.80f, 0.60f);
        /// <summary>북쪽 deep — 차가운 심청.</summary>
        public static readonly Color NorthCoolDeep = new Color(0.03f, 0.32f, 0.52f, 0.85f);

        /// <summary>
        /// 호수 중심 위치에 따라 국가별 터쿼이즈/에메랄드 틴트를 반환한다.
        /// - 남(South, z-) = 따뜻한 청록 / - 북(North, z+) = 차가운 청색 / - 그 외 = 표준 터쿼이즈.
        /// 판정은 NationTerrainController.GetNationFromPosition (Empire 포함 시 표준 팔레트).
        /// </summary>
        public static void GetNationWaterGradient(Vector3 center, out Color shallow, out Color deep)
        {
            NationType nation = NationTerrainController.GetNationFromPosition(center);
            switch (nation)
            {
                case NationType.South:
                    shallow = SouthTurquoiseShallow;
                    deep = SouthEmeraldDeep;
                    break;
                case NationType.North:
                    shallow = NorthCoolShallow;
                    deep = NorthCoolDeep;
                    break;
                default: // East / West / Empire / None → 표준 터쿼이즈
                    shallow = TurquoiseShallow;
                    deep = EmeraldDeep;
                    break;
            }
        }

        /// <summary>
        /// 호수 중심 위치 기준 국가별 틴트를 입힌 판타지 물 재질을 생성한다 (T-R5).
        /// GetNationWaterGradient로 shallow/deep 두 끝을 구하고 CreateFantasyWaterMaterial로
        /// shadergraph 또는 URP Lit 반투명 폴백 중 하나를 만든다.
        /// </summary>
        public static Material CreateNationTintedWaterMaterial(string materialName, Vector3 center, float shallowWeight = 0.65f)
        {
            GetNationWaterGradient(center, out Color shallow, out Color deep);
            return CreateFantasyWaterMaterial(materialName, shallow, deep, shallowWeight);
        }

        /// <summary>
        /// Returns true if the given material has all the upgraded water material properties.
        /// </summary>
        public static bool IsUpgradedWaterMaterial(Material mat)
        {
            if (mat == null) return false;

            bool hasReflectionBlending = mat.IsKeywordEnabled("_REFLECTION_PROBE_BLENDING");
            bool isTransparent = mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            bool hasCorrectQueue = mat.renderQueue == TransparentQueue;

            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            bool hasSmoothness = Mathf.Abs(smoothness - TargetSmoothness) < 0.01f;
            bool hasMetallic = Mathf.Abs(metallic - TargetMetallic) < 0.01f;

            return hasReflectionBlending && isTransparent && hasCorrectQueue && hasSmoothness && hasMetallic;
        }

        // ─────────────────────────────────────────────────────────────────
        // P-3: Idyllic fantasy water implementation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the translucent fantasy lake water material (P-3).
        /// 1) Tries the Idyllic "Water.shadergraph" via Shader.Find (depth fade, waves,
        ///    translucent coast opacity). 2) Falls back to a robust translucent URP Lit
        ///    material with the Idyllic water texture on _BaseMap and animated UV flow.
        /// Lake path only (GenerateAllLakes) — rivers/seas keep their own materials.
        /// </summary>
        /// <param name="materialName">Name for the new material.</param>
        /// <param name="waterColor">Bright water color (alpha = translucency).</param>
        /// <param name="shallowWeight">Blend weight toward shallow color (URP Lit fallback).</param>
        /// <returns>Fantasy water material (shadergraph or URP Lit fallback), or null if creation failed.</returns>
        public static Material CreateFantasyWaterMaterial(string materialName, Color waterColor, float shallowWeight = 0.5f)
        {
            return CreateFantasyWaterMaterial(materialName, waterColor, Color.Lerp(waterColor, Color.white, 0.15f),
                Color.Lerp(waterColor, new Color(0f, 0.15f, 0.3f), 0.55f), shallowWeight);
        }

        /// <summary>
        /// Creates a fantasy water material with an explicit shallow→deep gradient. Used by the
        /// T-R5 turquoise/emerald lake palette so the shadergraph (and fallback) get per-nation
        /// tinted shallow and deep ends instead of deriving both from a single input color.
        /// </summary>
        /// <param name="materialName">Name for the new material.</param>
        /// <param name="shallowColor">Bright shallow/turquoise base color.</param>
        /// <param name="deepColor">Emerald/deep water gradient end.</param>
        /// <param name="shallowWeight">Blend weight toward shallow color (URP Lit fallback only).</param>
        public static Material CreateFantasyWaterMaterial(string materialName, Color shallowColor, Color deepColor, float shallowWeight = 0.65f)
        {
            Color waterColor = shallowColor; // kept for texture tinting / translucency fallback
            // Preferred path: Idyllic Water shadergraph (if compiled and reachable by name)
            Material graphMat = TryCreateIdyllicShaderGraphMaterial(materialName, shallowColor, deepColor);
            if (graphMat != null)
                return graphMat;

            // Robust fallback: translucent URP Lit + Idyllic water texture + UV flow
            Material mat = CreateUpgradedWaterMaterial(materialName, shallowWeight);
            if (mat == null)
                return null;

            Color color = shallowColor;
            color.a = Mathf.Clamp(shallowColor.a, 0.35f, 0.85f);

            Texture2D waterTex = GetOrCreateTintedWaterTexture(color);
            if (waterTex != null)
            {
                // Texture carries the water color; base color modulates only translucency
                mat.SetTexture("_BaseMap", waterTex);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", waterTex);
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTextureScale("_BaseMap", WaterTextureTiling);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTextureScale("_MainTex", WaterTextureTiling);

                Color whiteTint = new Color(1f, 1f, 1f, color.a);
                mat.SetColor("_BaseColor", whiteTint);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", whiteTint);
            }
            else
            {
                // Texture unavailable — keep the plain translucent water color
                mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_BlendMode", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = TransparentQueue;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }

        /// <summary>
        /// Attempts to build a material from the Idyllic "Water.shadergraph"
        /// (shader name "Shader Graphs/Water"). Returns null (no exception) when the
        /// shader cannot be found/supported — the caller then uses the URP Lit fallback.
        /// </summary>
        private static Material TryCreateIdyllicShaderGraphMaterial(string materialName, Color waterColor)
        {
            return TryCreateIdyllicShaderGraphMaterial(materialName, waterColor,
                Color.Lerp(waterColor, new Color(0f, 0.15f, 0.3f), 0.55f));
        }

        /// <summary>
        /// Attempts to build a material from the Idyllic "Water.shadergraph"
        /// (shader name "Shader Graphs/Water"). Returns null (no exception) when the
        /// shader cannot be found/supported — the caller then uses the URP Lit fallback.
        /// </summary>
        private static Material TryCreateIdyllicShaderGraphMaterial(string materialName, Color baseColor, Color deepColor)
        {
            Shader graphShader = null;
            string[] candidates =
            {
                "Shader Graphs/Water",
                "Idyllic Fantasy Nature/Water",
                "Shader Graphs/Water.shadergraph"
            };
            foreach (string candidate in candidates)
            {
                graphShader = Shader.Find(candidate);
                if (graphShader != null)
                    break;
            }

            if (graphShader == null)
            {
                if (!_idyllicShaderUnavailableLogged)
                {
                    _idyllicShaderUnavailableLogged = true;
                    Debug.Log("[WaterMaterialUpgrader] Idyllic Water shadergraph unavailable at runtime — URP Lit 반투명 폴백 사용");
                }
                return null;
            }

            try
            {
                if (!graphShader.isSupported)
                {
                    Debug.Log("[WaterMaterialUpgrader] Idyllic Water shadergraph not supported on this pipeline — URP Lit 폴백 사용");
                    return null;
                }

                Material mat = new Material(graphShader);
                mat.name = string.IsNullOrEmpty(materialName) ? "Idyllic_FantasyWater_Mat" : materialName;

                // Graph exposes: _Shallow_Color, _Deep_Color, _Water_Speed, _Normal_Strength,
                // _Smoothness, _Refraction_Normal, _Second_Refraction_Normal, foam/coast props.
                // 여기서 waterColor(단일) 대신 nation 틴트 shallow→deep 그라데이션 두 끝을 주입.
                Color shallow = baseColor; shallow.a = 1f;
                Color deep = deepColor; deep.a = 1f;
                SetColorIfPresent(mat, "_Shallow_Color", shallow);
                SetColorIfPresent(mat, "_Deep_Color", deep);
                SetFloatIfPresent(mat, "_Water_Speed", 0.35f);
                SetFloatIfPresent(mat, "_Normal_Strength", 0.6f);
                SetFloatIfPresent(mat, "_Smoothness", 0.85f);
                // NOTE: graph normal-map texture properties are left unassigned — the graph's
                // procedural Gradient Noise waves render correctly with flat default normals,
                // while raw runtime-loaded textures would break UnpackNormal swizzling.

                mat.renderQueue = TransparentQueue;
                mat.SetOverrideTag("RenderType", "Transparent");
                EnsureUrpDepthAndOpaqueTextures();

                Debug.Log($"[WaterMaterialUpgrader] '{mat.name}' → Idyllic Water shadergraph 재질 생성");
                return mat;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] Idyllic shadergraph material creation failed — URP Lit 폴백: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// The shadergraph uses Scene Depth/Scene Color (depth fade + refraction);
        /// make sure the URP asset samples depth and the opaque texture.
        /// </summary>
        private static void EnsureUrpDepthAndOpaqueTextures()
        {
            if (_pipelineFlagsEnsured) return;
            _pipelineFlagsEnsured = true;
            try
            {
                if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    if (!urp.supportsCameraDepthTexture)
                    {
                        urp.supportsCameraDepthTexture = true;
                        Debug.Log("[WaterMaterialUpgrader] URP asset: Depth Texture 활성화 (Idyllic water depth fade용)");
                    }
                    if (!urp.supportsCameraOpaqueTexture)
                    {
                        urp.supportsCameraOpaqueTexture = true;
                        Debug.Log("[WaterMaterialUpgrader] URP asset: Opaque Texture 활성화 (Idyllic water refraction용)");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] URP depth/opaque texture 설정 실패: " + e.Message);
            }
        }

        /// <summary>
        /// Copies the Idyllic water textures into Assets/Resources/Water (editor only) so that
        /// Resources.Load works in builds. Import .meta files are generated by Unity automatically.
        /// </summary>
        public static void EnsureWaterTextureResources()
        {
            if (_resourcesEnsured) return;
            _resourcesEnsured = true;
#if UNITY_EDITOR
            try
            {
                if (!Directory.Exists(IdyllicTextureSourceDir))
                    return;
                if (!Directory.Exists(IdyllicTextureResourceDir))
                    Directory.CreateDirectory(IdyllicTextureResourceDir);

                string[] fileNames = { "Water_Normal_01.png", "Water_Normal_02.png" };
                bool copied = false;
                foreach (string fileName in fileNames)
                {
                    string source = Path.Combine(IdyllicTextureSourceDir, fileName);
                    string destination = Path.Combine(IdyllicTextureResourceDir, fileName);
                    if (File.Exists(source) && !File.Exists(destination))
                    {
                        File.Copy(source, destination, false);
                        copied = true;
                    }
                }
                if (copied)
                    Debug.Log("[WaterMaterialUpgrader] Idyllic water textures copied to Assets/Resources/Water (Resources.Load 용)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] Water texture copy failed: " + e.Message);
            }
#endif
        }

        /// <summary>
        /// Loads the Idyllic water texture: Resources.Load first (build-safe), then a direct
        /// disk read fallback (editor, works before the imported copy is indexed).
        /// Returns null when unavailable — materials then stay plain-colored.
        /// </summary>
        public static Texture2D LoadIdyllicWaterTexture()
        {
            if (_idyllicWaterTexture != null)
                return _idyllicWaterTexture;

            EnsureWaterTextureResources();

            _idyllicWaterTexture = Resources.Load<Texture2D>(IdyllicMainTextureResource);
            if (_idyllicWaterTexture != null)
                return _idyllicWaterTexture;

            try
            {
                string path = Path.Combine(IdyllicTextureSourceDir, "Water_Normal_01.png");
                if (!File.Exists(path))
                    return null;

                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                if (tex.LoadImage(bytes))
                {
                    tex.name = "Idyllic_Water_Normal_01_Runtime";
                    tex.wrapMode = TextureWrapMode.Repeat;
                    _idyllicWaterTexture = tex;
                    Debug.Log("[WaterMaterialUpgrader] Idyllic water texture loaded from disk (Resources 인덱싱 전 임시 경로)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] Water texture disk load failed: " + e.Message);
            }

            return _idyllicWaterTexture;
        }

        /// <summary>
        /// Creates a readable copy of a non-readable texture via RenderTexture blit,
        /// enabling GetPixelBilinear-based processing at runtime.
        /// </summary>
        public static Texture2D MakeReadableCopy(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                int width = source.width;
                int height = source.height;
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(source, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var readable = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply(false, false);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                readable.name = source.name + "_Readable";
                readable.wrapMode = TextureWrapMode.Repeat;
                return readable;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] MakeReadableCopy failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Tints the Idyllic water (normal map) texture toward the lake color using its
        /// luminance as a wave-detail ramp. Returns null when the source is unavailable.
        /// </summary>
        public static Texture2D TintWaterTexture(Texture2D source, Color waterColor, int outputSize = 256)
        {
            if (source == null) return null;
            if (!source.isReadable)
            {
                source = MakeReadableCopy(source);
                if (source == null) return null;
            }

            try
            {
                int size = Mathf.Clamp(outputSize, 32, 1024);
                var tinted = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
                tinted.name = "Idyllic_Water_Tinted";
                tinted.wrapMode = TextureWrapMode.Repeat;

                Color dark = Color.Lerp(waterColor, Color.black, 0.35f);
                Color bright = Color.Lerp(waterColor, Color.white, 0.3f);
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;
                        Color sample = source.GetPixelBilinear(u, v);
                        // Normal-map pixels wobble around (0.5, 0.5) — use that as wave detail
                        float detail = (sample.r + sample.g) * 0.5f;
                        float t = Mathf.Clamp01((detail - 0.25f) * 2f);
                        Color c = Color.Lerp(dark, bright, t);
                        pixels[y * size + x] = new Color32(
                            (byte)(Mathf.Clamp01(c.r) * 255f),
                            (byte)(Mathf.Clamp01(c.g) * 255f),
                            (byte)(Mathf.Clamp01(c.b) * 255f),
                            255);
                    }
                }

                tinted.SetPixels32(pixels);
                tinted.Apply(true, false);
                return tinted;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WaterMaterialUpgrader] TintWaterTexture failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Cached tinted water texture (shared by all lakes with the same color).
        /// </summary>
        public static Texture2D GetOrCreateTintedWaterTexture(Color waterColor, int size = 256)
        {
            Color cacheKey = waterColor;
            cacheKey.a = 1f;
            float diff = Mathf.Abs(cacheKey.r - _tintedWaterCacheColor.r)
                       + Mathf.Abs(cacheKey.g - _tintedWaterCacheColor.g)
                       + Mathf.Abs(cacheKey.b - _tintedWaterCacheColor.b);
            if (_tintedWaterTexture != null && diff < 0.02f)
                return _tintedWaterTexture;

            Texture2D source = LoadIdyllicWaterTexture();
            Texture2D tinted = TintWaterTexture(source, waterColor, size);
            if (tinted == null)
                return null;

            _tintedWaterTexture = tinted;
            _tintedWaterCacheColor = cacheKey;
            return tinted;
        }

        /// <summary>
        /// Computes the current UV flow offset from a base offset and time.
        /// Stateless (safe per-frame): offset = Repeat(base + time * speed, 1).
        /// </summary>
        public static Vector2 ComputeWaterFlow(Vector2 baseOffset, float time, float speedX, float speedZ)
        {
            return new Vector2(
                Mathf.Repeat(baseOffset.x + time * speedX, 1f),
                Mathf.Repeat(baseOffset.y + time * speedZ, 1f));
        }

        /// <summary>
        /// Applies the UV flow offset to the material's main texture
        /// (URP Lit: [MainTexture] _BaseMap — same property material.mainTextureOffset resolves to).
        /// Materials without a base map (e.g. Idyllic shadergraph with its own animation) are ignored.
        /// </summary>
        public static void AnimateWaterFlow(Material mat, Vector2 baseOffset, float time, float speedX, float speedZ)
        {
            if (mat == null) return;
            if (!mat.HasProperty("_BaseMap") && !mat.HasProperty("_MainTex")) return;

            Vector2 offset = ComputeWaterFlow(baseOffset, time, speedX, speedZ);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTextureOffset("_BaseMap", offset);
            if (mat.HasProperty("_MainTex"))
                mat.SetTextureOffset("_MainTex", offset);
        }

        private static void SetColorIfPresent(Material mat, string propertyName, Color value)
        {
            if (mat.HasProperty(propertyName))
                mat.SetColor(propertyName, value);
        }

        private static void SetFloatIfPresent(Material mat, string propertyName, float value)
        {
            if (mat.HasProperty(propertyName))
                mat.SetFloat(propertyName, value);
        }
    }
}