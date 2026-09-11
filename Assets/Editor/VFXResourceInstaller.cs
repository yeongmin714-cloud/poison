using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 3단계 이펙트: Free Slash VFX + Matthew Guz Hits Effects FREE 프리팹을
    /// Assets/Resources/FX/ 하위로 복사하는 에디터 인스톨러.
    ///
    /// 두 프리팹 원본은 Assets/Resources 밖에 있어 Resources.Load가 불가능하므로,
    /// 이 인스톨러가 복사본을 만들어 런타임 로드를 가능하게 한다 (멱등 — 재실행 안전).
    ///
    /// 무인 실행 (부모 자동화):
    ///   Unity.exe -quit -batchmode -projectPath ... -executeMethod ProjectName.EditorTools.VFXResourceInstaller.InstallAll
    /// 수동 실행: 메뉴 Tools → VFX → Install Slash+Impact to Resources
    ///
    /// 복사 후 Matthew Guz Impact 복사본의 머티리얼을 순회해 Unity 빌트인 파티클 셰이더
    /// (Particles/*, Mobile/Particles/*, Legacy Shaders/*, InternalErrorShader 등)를 URP 머티리얼로
    /// 자동 변환/교체한다 — 새 .mat 에셋을 만들어 renderer.sharedMaterial(s)/trailMaterial만 교체하므로
    /// 원본 에셋(Assets/Matthew Guz/**)은 수정되지 않는다 (URP 마젠타 방지).
    /// 이후 각 프리팹의 Renderer.sharedMaterial.shader 이름을 수집해 로그하며,
    /// shader == null 또는 "Hidden/InternalErrorShader"면 마젠타(셰이더 미해석)로 판단해
    /// LogError로 나열한다.
    /// </summary>
    public static class VFXResourceInstaller
    {
        private const string SlashSource = "Assets/Free Slash VFX/Prefabs/Slash VFX.prefab";
        private const string MultipleSlashesSource = "Assets/Free Slash VFX/Prefabs/Multiple Slashes.prefab";
        private const string BasicHitSource = "Assets/Matthew Guz/Hits Effects FREE/Prefab/Basic Hit .prefab";
        private const string BasicHit2Source = "Assets/Matthew Guz/Hits Effects FREE/Prefab/Basic Hit 2.prefab";

        private const string SlashFolder = "Assets/Resources/FX/Slash";
        private const string ImpactFolder = "Assets/Resources/FX/Impact";

        private const string SlashDest = "Assets/Resources/FX/Slash/Slash VFX.prefab";
        private const string MultipleSlashesDest = "Assets/Resources/FX/Slash/Multiple Slashes.prefab";
        private const string BasicHitDest = "Assets/Resources/FX/Impact/BasicHit.prefab";
        private const string BasicHit2Dest = "Assets/Resources/FX/Impact/BasicHit2.prefab";

        [MenuItem("Tools/VFX/Install Slash+Impact to Resources")]
        public static void InstallAll()
        {
            int installed = 0;

            // 1) 대상 폴더 보장 (없으면 체인으로 생성)
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "FX");
            EnsureFolder("Assets/Resources/FX", "Slash");
            EnsureFolder("Assets/Resources/FX", "Impact");

            // 2) 복사 (멱등: 대상 존재 시 삭제 후 복사, 소스 없으면 경고 후 계속)
            installed += CopyIdempotent(SlashSource, SlashDest);
            installed += CopyIdempotent(MultipleSlashesSource, MultipleSlashesDest);
            installed += CopyIdempotent(BasicHitSource, BasicHitDest);
            installed += CopyIdempotent(BasicHit2Source, BasicHit2Dest);

            // 3) 빌트인→URP 파티클 셰이더 자동 변환 (Matthew Guz 마젠타 방지)
            //    Impact 프리팹 복사본 2개에만 적용 — 새 URP .mat 에셋을 만들어
            //    renderer.sharedMaterial(s)/trailMaterial을 교체하므로
            //    원본 에셋(Assets/Matthew Guz/**)은 절대 수정되지 않는다.
            int converted = ConvertBuiltInMaterialsToUrp(new[] { BasicHitDest, BasicHit2Dest });

            // 4) 검증: 복사본 로드 → 셰이더 이름 수집 → 마젠타 감지
            ValidateCopiedPrefab(SlashDest, "Slash VFX");
            ValidateCopiedPrefab(MultipleSlashesDest, "Multiple Slashes (십자 다중 슬래시)");
            ValidateCopiedPrefab(BasicHitDest, "BasicHit (Organic)");
            ValidateCopiedPrefab(BasicHit2Dest, "BasicHit2 (Construct)");

            // 5) 저장 + 리프레시
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VFXInstaller] ✅ 설치 완료: {installed}/4 프리팹이 Resources 하위에 준비됨, 빌트인→URP 머티리얼 변환 {converted}종 (Assets/Resources/FX/Slash, Assets/Resources/FX/Impact)");
        }

        /// <summary>parentFolder 아래에 folderName이 없으면 생성.</summary>
        private static void EnsureFolder(string parentFolder, string folderName)
        {
            string path = $"{parentFolder}/{folderName}";
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        /// <summary>대상이 이미 있으면 삭제 후 복사(멱등). 소스 없으면 경고 1회 후 건너뜀. 성공 시 1 반환.</summary>
        private static int CopyIdempotent(string source, string dest)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(source) == null)
            {
                Debug.LogWarning($"[VFXInstaller] ⚠️ 소스 없음, 건너뜀: {source}");
                return 0;
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(dest) != null)
                AssetDatabase.DeleteAsset(dest);

            if (!AssetDatabase.CopyAsset(source, dest))
            {
                Debug.LogError($"[VFXInstaller] ❌ 복사 실패: {source} → {dest}");
                return 0;
            }

            Debug.Log($"[VFXInstaller] 복사: {source} → {dest}");
            return 1;
        }

        /// <summary>복사본의 모든 Renderer 셰이더 이름을 수집/로그하고, 마젠타(InternalErrorShader/null)면 LogError.</summary>
        private static void ValidateCopiedPrefab(string path, string label)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFXInstaller] ⚠️ 검증 스킵 (로드 실패): {path}");
                return;
            }

            var shaderNames = new SortedSet<string>();
            var broken = new List<string>();

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null) continue;

                foreach (Material mat in materials)
                {
                    if (mat == null) continue;

                    Shader shader = mat.shader;
                    if (shader == null)
                    {
                        broken.Add($"{mat.name}: shader == null");
                        continue;
                    }

                    shaderNames.Add(shader.name);
                    if (shader.name == "Hidden/InternalErrorShader")
                        broken.Add($"{mat.name}: Hidden/InternalErrorShader (마젠타)");
                }
            }

            var sb = new StringBuilder();
            sb.Append($"[VFXInstaller] {label} 셰이더 수집 ({shaderNames.Count}종):");
            foreach (string name in shaderNames)
                sb.Append($"  {name};");
            Debug.Log(sb.ToString());

            if (broken.Count > 0)
                Debug.LogError($"[VFXInstaller] ❌ {label} 마젠타 감지 — 셰이더 오류 {broken.Count}건:\n  {string.Join("\n  ", broken)}");
        }

        #region 빌트인→URP 파티클 셰이더 자동 변환 (Matthew Guz 마젠타 방지)

        private const string UrpParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string ImpactMatsFolder = "Assets/Resources/FX/Impact/Mats";

        /// <summary>변환 대상으로 보는 Unity 빌트인 셰이더 이름 접두사.</summary>
        private static readonly string[] BuiltInShaderPrefixes = { "Particles/", "Mobile/Particles/", "Legacy Shaders/" };

        /// <summary>이번 실행에서 이미 사용한 변환 머티리얼 에셋 경로 (같은 이름 다른 원본 충돌 방지).</summary>
        private static readonly HashSet<string> UsedMatPaths = new HashSet<string>();

        /// <summary>URP Particles/Unlit에 세팅할 블렌딩 모드 (원본 셰이더 이름 기반 결정).</summary>
        private enum UrpBlendMode { Opaque, Alpha, Premultiply, Additive }

        /// <summary>
        /// 복사된 Impact 프리팹 복사본의 모든 Renderer(MeshRenderer/ParticleSystemRenderer/BillboardRenderer 등
        /// Renderer 파생 전부)를 순회해 Unity 빌트인 파티클 셰이더를 URP 머티리얼로 교체한다.
        ///
        /// 빌트인 판별은 실행 중 shader.name 기반 — Unity 기본 리소스(m_Shader: fileID 211, guid f000…) 참조는
        /// 로드 시점에 실제 셰이더 이름으로 해석되므로 이름 판별이 맞다.
        /// shader == null / Hidden/InternalErrorShader 등 판별이 애매한 경우도 변환 대상에 포함.
        /// URP/ShaderGraph 셰이더는 건드리지 않는다.
        ///
        /// 원본 보호 핵심: 복사본 프리팹의 렌더러는 원본 .mat 에셋을 공유 참조하므로 절대 수정하지 않고,
        /// 새 URP 머티리얼 "에셋"을 만들어 renderer.sharedMaterial(s)/trailMaterial에 할당한다.
        /// 이렇게 해야 프리팹 저장 시 원본 머티리얼 에셋이 함께 변경되지 않는다.
        /// </summary>
        /// <returns>변환(신규 생성)된 머티리얼 종수.</returns>
        private static int ConvertBuiltInMaterialsToUrp(string[] prefabPaths)
        {
            EnsureFolder(ImpactFolder, "Mats");
            UsedMatPaths.Clear();

            var cache = new Dictionary<int, Material>(); // 원본 머티리얼 instanceID → 변환 결과 (중복 변환 방지)
            var logs = new List<string>();
            int replacedRefs = 0;

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[VFXInstaller] ⚠️ 셰이더 변환 스킵 (로드 실패): {path}");
                    continue;
                }

                bool prefabModified = false;

                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;

                    // 1) sharedMaterials 배열 요소별 교체 (모든 Renderer 타입 공통 경로)
                    Material[] mats = renderer.sharedMaterials;
                    bool arrayChanged = false;
                    if (mats != null)
                    {
                        for (int i = 0; i < mats.Length; i++)
                        {
                            Material urp = GetOrCreateUrpMaterial(mats[i], cache, logs);
                            if (urp != null && urp != mats[i])
                            {
                                mats[i] = urp;
                                arrayChanged = true;
                                replacedRefs++;
                            }
                        }
                    }
                    if (arrayChanged)
                    {
                        renderer.sharedMaterials = mats;
                        EditorUtility.SetDirty(renderer);
                        prefabModified = true;
                    }

                    // 2) 파티클 트레일 머티리얼 (ParticleSystemRenderer 전용)
                    if (renderer is ParticleSystemRenderer psr)
                    {
                        Material trail = psr.trailMaterial;
                        Material urpTrail = GetOrCreateUrpMaterial(trail, cache, logs);
                        if (urpTrail != null && urpTrail != trail)
                        {
                            psr.trailMaterial = urpTrail;
                            EditorUtility.SetDirty(psr);
                            prefabModified = true;
                            replacedRefs++;
                        }
                    }
                }

                if (prefabModified)
                    EditorUtility.SetDirty(prefab);
            }

            AssetDatabase.SaveAssets();

            // 변환 결과 로그: 변환된 머티리얼 수 + "원본셰이더 → URP셰이더" 목록
            if (logs.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[VFXInstaller] 🔧 빌트인→URP 머티리얼 변환 완료: {logs.Count}종 / 참조 교체 {replacedRefs}건 → {ImpactMatsFolder}");
                foreach (string line in logs)
                    sb.AppendLine("  " + line);
                Debug.Log(sb.ToString().TrimEnd());
            }
            else
            {
                Debug.Log("[VFXInstaller] 빌트인→URP 변환 대상 없음 (모든 머티리얼이 URP/ShaderGraph)");
            }

            return logs.Count;
        }

        /// <summary>원본 머티리얼이 빌트인 셰이더면 URP 머티리얼을 만들어(캐시) 반환, 아니면 null (URP/ShaderGraph는 무시).</summary>
        private static Material GetOrCreateUrpMaterial(Material source, Dictionary<int, Material> cache, List<string> logs)
        {
            if (source == null) return null;

            Shader srcShader = source.shader;
            string srcShaderName = srcShader != null ? srcShader.name : "(null)";

            if (!IsBuiltInShaderName(srcShaderName))
                return null;

            int key = source.GetInstanceID();
            if (cache.TryGetValue(key, out Material cached))
                return cached;

            UrpBlendMode blend = DetermineBlendMode(srcShaderName); // 순수 이름 매핑 — try 밖에서 결정
            Material urpMat;
            try
            {
                urpMat = CreateConvertedMaterial(source, srcShaderName, blend);
            }
            catch (System.Exception e)
            {
                // 개별 머티리얼 변환 실패가 전체 인스톨을 깨지 않도록 한다 (원본 유지 = 기존 동작).
                Debug.LogWarning($"[VFXInstaller] ⚠️ '{source.name}' 변환 실패, 원본 유지: {e.Message}");
                return null;
            }

            string assetPath = SaveAsNewAsset(urpMat, source.name);
            if (assetPath == null)
            {
                Debug.LogError($"[VFXInstaller] ❌ 변환 머티리얼 에셋 생성 실패, 원본 유지: {source.name}");
                return null;
            }

            logs.Add($"{source.name}: {srcShaderName} → {(urpMat.shader != null ? urpMat.shader.name : "(null)")} [{blend}] → {assetPath}");
            cache[key] = urpMat;
            return urpMat;
        }

        /// <summary>빌트인 셰이더 판별: 접두사 매칭 + 판별 애매한 경우(shader null/InternalErrorShader) 포함.</summary>
        private static bool IsBuiltInShaderName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName) || shaderName == "(null)") return true;
            if (shaderName == "Hidden/InternalErrorShader") return true;
            foreach (string prefix in BuiltInShaderPrefixes)
            {
                if (shaderName.StartsWith(prefix)) return true;
            }
            return false;
        }

        /// <summary>원본 셰이더 이름으로 URP 블렌딩 모드 결정 (이름 기반 매핑).</summary>
        private static UrpBlendMode DetermineBlendMode(string shaderName)
        {
            string n = shaderName ?? string.Empty;
            if (n.Contains("Additive")) return UrpBlendMode.Additive;        // Particles/Additive, Mobile/Particles/Additive 등
            if (n.Contains("Premultiply")) return UrpBlendMode.Premultiply;  // Particles/Alpha Blended Premultiply 포함
            if (n.Contains("Alpha Blended") || n.Contains("Transparent")) return UrpBlendMode.Alpha;
            if (n.Contains("Multiply")) return UrpBlendMode.Alpha;           // _DstBlend=Zero 특수 처리는 생략하고 Alpha로 통일
            return UrpBlendMode.Opaque;                                      // 그 외(Opaque/Standard/판별 불가)
        }

        /// <summary>
        /// 원본 머티리얼에서 텍스처(_MainTex)/색(_TintColor 또는 _Color)/ST를 추출해
        /// URP 셰이더 머티리얼을 생성한다. 프로퍼티/키워드 세팅은 URP 버전 차이로 실패할 수 있으므로
        /// try-catch로 감싸고, 실패 시 텍스처만 복사한 기본 머티리얼로 폴백 (마젠타보다 낫다).
        /// </summary>
        private static Material CreateConvertedMaterial(Material source, string srcShaderName, UrpBlendMode blend)
        {
            Texture baseMap = null;
            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;
            bool hasTex = false;
            Color baseColor = Color.white;

            try
            {
                // 원본 에셋은 읽기만 한다 (수정 금지)
                hasTex = source.HasProperty("_MainTex");
                if (hasTex)
                {
                    baseMap = source.GetTexture("_MainTex");
                    scale = source.GetTextureScale("_MainTex");
                    offset = source.GetTextureOffset("_MainTex");
                }
                if (source.HasProperty("_TintColor")) baseColor = source.GetColor("_TintColor");
                else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");

                // 대상 셰이더: 빌트인 Standard 원본이면 URP/Lit, 그 외는 URP Particles/Unlit
                bool useLit = blend == UrpBlendMode.Opaque && srcShaderName == "Standard";
                string targetShaderName = useLit ? UrpLitShaderName : UrpParticleShaderName;
                Shader targetShader = Shader.Find(targetShaderName);
                if (targetShader == null)
                    throw new System.Exception($"URP 셰이더를 찾을 수 없음: {targetShaderName}");

                var mat = new Material(targetShader) { name = source.name + "_URP" };

                // 텍스처/색 (_BaseMap 없는 URP 버전 대비 _MainTex 폴백)
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", baseMap);
                    if (hasTex && baseMap != null)
                    {
                        mat.SetTextureScale("_BaseMap", scale);
                        mat.SetTextureOffset("_BaseMap", offset);
                    }
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", baseMap);
                    if (hasTex && baseMap != null)
                    {
                        mat.SetTextureScale("_MainTex", scale);
                        mat.SetTextureOffset("_MainTex", offset);
                    }
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

                // 블렌딩 매핑
                switch (blend)
                {
                    case UrpBlendMode.Additive:
                        mat.SetFloat("_Surface", 3f);                                   // Additive
                        mat.SetFloat("_Blend", 2f);                                     // Additive
                        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)BlendMode.One);
                        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                        break;

                    case UrpBlendMode.Premultiply:
                        mat.SetFloat("_Surface", 2f);                                   // Premultiply
                        mat.SetFloat("_Blend", 1f);                                     // Premultiply
                        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                        break;

                    case UrpBlendMode.Alpha:
                        mat.SetFloat("_Surface", 1f);                                   // Alpha
                        mat.SetFloat("_Blend", 0f);                                     // Alpha
                        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                        break;

                    default: // Opaque
                        mat.SetFloat("_Surface", 0f);
                        mat.SetFloat("_Blend", 0f);
                        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.SetOverrideTag("RenderType", "Opaque");
                        mat.renderQueue = 2000;
                        break;
                }

                // 깊이 쓰기: 투명 계열은 끈다 (프로퍼티 부재 가능 — 가드)
                if (mat.HasProperty("_ZWrite"))
                    mat.SetFloat("_ZWrite", blend == UrpBlendMode.Opaque ? 1f : 0f);

                return mat;
            }
            catch (System.Exception e)
            {
                // 키워드/프로퍼티 세팅 실패(URP 버전 차이 등) → 텍스처만 복사한 기본 머티리얼로라도 교체
                Debug.LogWarning($"[VFXInstaller] ⚠️ '{source.name}' URP 변환 세팅 실패, 기본 머티리얼로 폴백: {e.Message}");
                Shader fallbackShader = Shader.Find(UrpParticleShaderName) ?? Shader.Find("Sprites/Default");
                if (fallbackShader == null)
                    throw; // URP/URP호환 셰이더 모두 부재 — 상위에서 원본 유지 처리

                var fallback = new Material(fallbackShader) { name = source.name + "_URP" };
                if (fallback.HasProperty("_BaseMap")) fallback.SetTexture("_BaseMap", baseMap);
                else if (fallback.HasProperty("_MainTex")) fallback.SetTexture("_MainTex", baseMap);
                if (fallback.HasProperty("_BaseColor")) fallback.SetColor("_BaseColor", baseColor);
                else if (fallback.HasProperty("_Color")) fallback.SetColor("_Color", baseColor);
                return fallback;
            }
        }

        /// <summary>변환 머티리얼을 Impact/Mats 아래 새 에셋으로 저장. 같은 이름 에셋이 있으면 삭제 후 재생성(멱등).</summary>
        private static string SaveAsNewAsset(Material mat, string sourceName)
        {
            string baseName = SanitizeFileName(sourceName) + "_URP";
            string path = $"{ImpactMatsFolder}/{baseName}.mat";

            // 같은 실행 내 이름 충돌(서로 다른 원본이 같은 이름) 방지
            if (UsedMatPaths.Contains(path))
            {
                int n = 2;
                string candidate;
                do
                {
                    candidate = $"{ImpactMatsFolder}/{baseName}_{n}.mat";
                    n++;
                } while (UsedMatPaths.Contains(candidate));
                path = candidate;
            }

            // 이전 실행 결과가 남아 있으면 삭제 후 새로 생성 (멱등 — 항상 최신 변환 결과로 교체)
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(mat, path);

            UsedMatPaths.Add(path);
            return path;
        }

        /// <summary>파일 이름으로 쓸 수 없는 문자를 '_'로 치환.</summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Material";

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool invalid = c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|' || char.IsControl(c);
                sb.Append(invalid ? '_' : c);
            }

            string result = sb.ToString().Trim();
            return result.Length == 0 ? "Material" : result;
        }

        #endregion
    }
}