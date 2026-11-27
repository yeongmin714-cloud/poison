using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [134차 A-2] Npc2026 텍스처 추출 + 머티리얼 알베도 바인딩 일괄 수리(배치모드).
    /// 근거(계획 항목 A / Editor.log 실측): 전원 NPC 로그 "텍스처 서브어셈블리 부재" — 임베드 텍스처가
    /// 서브어셈블리로 노출 안 됨(LoadAll&lt;Texture2D&gt; 0건) → 런타임 폴백(VillageNpcSpawner.
    /// EnsureNpcTexture)이 매번 스킵 = 일부 피부 하양 유지.
    /// 사다리 순서(앞 단계 실패 시 다음):
    ///  ① 각 rigged FBX의 머티리얼 임포트 모드 확인(None이면 ImportViaMaterialDescription로 수리) +
    ///     SaveAndReimport → 텍스처 재로드
    ///     ([134차 A-2 재수리] Unity 6000 실측: importMaterials 폐기(CS0619) → materialImportMode 사용)
    ///  ② 여전히 0건이면 ModelImporter.ExtractTextures로 캐릭터별 서브폴더 추출 → 추출 후 재로드
    ///     ([134차 A-2 재수리] 1차 실행 실측: 임베드 텍스처명이 "base_color"/"normal"로 캐릭터명 미포함 +
    ///     공유 단일 폴더 추출 시 24회 상호 덮어쓰기 → 전원이 마지막 캐릭터 텍스처로 교차 바인딩.
    ///     → 지시서의 "공유 Textures 폴더 + 이름 매칭" 전제가 실측에서 불성립하여 캐릭터별
    ///     Textures/&lt;stem&gt; 서브폴더로 변경. 부모 경로 LoadAll은 하위폴더 전부 포함하므로
    ///     런타임 폴백 키 "Models/UserProvided/Npc2026/Textures" 계약과 호환.)
    ///  ③ 텍스처 확보 후: 각 rigged FBX의 Material 서브어셈블리 전수 — base_color/알베도를
    ///     material.mainTexture에 대입(SetDirty + SaveAssets). normal 포함 다중이면 mainTexture=알베도만.
    ///     재수리: 1차 실행의 교차 바인딩(mainTexture.name == "base_color")도 자기 캐릭터 알베도로 재대입.
    ///  ④ 검증 로그: "[134차 A-2] DONE 머티리얼N개 바인딩, 텍스처추출M개, 실패K개" + 실패 캐릭터 목록.
    /// 런타임 EnsureNpcTexture는 폴백으로 유지(바인딩 성공 시 자동 스킵) — 추출 폴더 2차 시도는
    /// VillageNpcSpawner 측 [134차 A-2] 폴백 추가분(캐릭터별 서브폴더 키)이 담당.
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.Npc2026MaterialFixSetup.FixAll
    /// 멱등: 자기 캐릭터 알베도 재대입은 동일 참조 — 반복 실행 무영향.
    /// </summary>
    public static class Npc2026MaterialFixSetup
    {
        private const string Root = "Assets/Resources/Models/UserProvided/Npc2026";
        private const string ExtractFolder = Root + "/Textures";

        [MenuItem("Tools/Anim/Npc2026 Material Texture Fix")]
        public static void FixAll()
        {
            AssetDatabase.Refresh();
            CleanupStaleSharedTextures();   // 1차 실행의 공유 폴더 덮어쓰기 잔존물 제거(멱등)

            // 캐릭터 폴더 수집 — 폴더명 = 파일 스템 계약([128-2 P3] 진실 출처 준용)
            var stems = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith("_rigged.fbx")) continue;
                string stem = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(p));
                if (!string.IsNullOrEmpty(stem) && !stems.Contains(stem)) stems.Add(stem);
            }
            stems.Sort();
            Debug.Log($"[134차 A-2] rigged FBX 캐릭터 {stems.Count}종 — {string.Join(", ", stems)}");

            int boundMats = 0, reboundMats = 0, alreadyBound = 0, extractedCount = 0, failed = 0;
            var failedList = new List<string>();

            foreach (string stem in stems)
            {
                string fbxPath = $"{Root}/{stem}/{stem}_rigged.fbx";
                string resKey = $"Models/UserProvided/Npc2026/{stem}/{stem}_rigged";
                string charExtractFolder = $"{ExtractFolder}/{stem}";
                string charExtractKey = $"Models/UserProvided/Npc2026/Textures/{stem}";

                try
                {
                    // ── ① 머티리얼 임포트 모드 확인 + SaveAndReimport → 재로드 ──
                    // [134차 A-2] Unity 6000 실측: importMaterials 폐기(CS0619) → materialImportMode 사용.
                    // None = 머티리얼/텍스처 임포트 스킵 → ImportViaMaterialDescription(Unity 6 기본 임포트 경로)으로 수리.
                    if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter mi)
                    {
                        Debug.LogError($"[134차 A-2] ModelImporter 부재 — {fbxPath}");
                        failed++; failedList.Add(stem);
                        continue;
                    }
                    if (mi.materialImportMode == ModelImporterMaterialImportMode.None)
                    {
                        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        mi.SaveAndReimport();
                        Debug.Log($"[134차 A-2] materialImportMode=ImportViaMaterialDescription 재임포트 — {stem}");
                    }

                    // FBX 자체(임베드 서브어셈블리) 텍스처 재로드 — 런타임 폴백과 동일 키
                    Texture2D[] fbxTexs = Resources.LoadAll<Texture2D>(resKey);
                    Texture2D albedo = PickAlbedo(fbxTexs);

                    // ── ② 여전히 0건이면 캐릭터별 서브폴더 ExtractTextures 시도 → 추출 후 재로드 ──
                    // (API 부재 시 컴파일 단계에서 확인 후 주석 처리+보고)
                    if (albedo == null && fbxTexs.Length == 0)
                    {
                        EnsureExtractFolder(stem);
                        mi.ExtractTextures(charExtractFolder);
                        AssetDatabase.Refresh();
                        extractedCount++;
                        var extracted = Resources.LoadAll<Texture2D>(charExtractKey);
                        albedo = PickAlbedo(extracted);   // 서브폴더 전체가 해당 캐릭터 소유 — 이름 매칭 불요
                        Debug.Log($"[134차 A-2] 텍스처 추출 — {stem} 폴더={charExtractFolder} 텍스처={extracted.Length}개");
                    }

                    if (albedo == null)
                    {
                        Debug.LogError($"[134차 A-2] 알베도 텍스처 미확보 — {stem}(임베드 0건 + 추출 실패)");
                        failed++; failedList.Add(stem);
                        continue;
                    }

                    // ── ③ Material 서브어셈블리 전수 알베도 바인딩 ──
                    var mats = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().ToArray();
                    if (mats.Length == 0)
                    {
                        Debug.LogError($"[134차 A-2] Material 서브어셈블리 부재 — {fbxPath}");
                        failed++; failedList.Add(stem);
                        continue;
                    }
                    foreach (var mat in mats)
                    {
                        bool texMissing = mat.mainTexture == null;
                        Color c = mat.color;
                        bool nearWhite = c.r >= 0.98f && c.g >= 0.98f && c.b >= 0.98f;
                        // 재수리 판정: 1차 실행이 공유 폴더 덮어쓰기로 남긴 교차 바인딩(mainTexture가
                        // 캐릭터 무관 공용명 "base_color")도 자기 알베도로 재대입.
                        bool staleShared = mat.mainTexture != null && mat.mainTexture.name == "base_color";
                        if (!texMissing && !nearWhite && !staleShared) { alreadyBound++; continue; }
                        bool wasStale = staleShared && !texMissing;
                        mat.mainTexture = albedo;   // 색/셰이더 무수정 — 텍스처만(런타임 폴백 판정 동일)
                        EditorUtility.SetDirty(mat);
                        if (wasStale) reboundMats++; else boundMats++;
                    }
                    Debug.Log($"[134차 A-2] 바인딩 — {stem} 머티리얼={mats.Length}개 알베도='{albedo.name}'({albedo.GetInstanceID()})");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[134차 A-2] 예외 — {stem}: {ex.Message}");
                    failed++; failedList.Add(stem);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── ④ 검증 로그 ──
            string failText = failedList.Count > 0 ? " 실패목록=[" + string.Join(", ", failedList) + "]" : "";
            Debug.Log($"[134차 A-2] DONE 머티리얼{boundMats}개 바인딩, 텍스처추출{extractedCount}개, 실패{failed}개 " +
                      $"(교차재수리={reboundMats} 기존유효스킵={alreadyBound}){failText}");
        }

        /// <summary>알베도 선택 — base_color/BaseColor/albedo/diffuse 우선, 없으면 normal 아닌 첫 텍스처
        /// (런타임 PickNpcSkinTexture 판정과 동일 규격). 전부 normal이면 null.</summary>
        private static Texture2D PickAlbedo(Texture2D[] textures)
        {
            if (textures == null) return null;
            Texture2D fallback = null;
            foreach (var t in textures)
            {
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();
                bool isNormal = n.Contains("normal");
                if (!isNormal && fallback == null) fallback = t;   // normal 아닌 첫 텍스처
                if (n.Contains("base_color") || n.Contains("basecolor") || n.Contains("base color")
                    || n.Contains("albedo") || n.Contains("diffuse"))
                    return t;   // 우선 매칭 — 즉시 채택
            }
            return fallback;
        }

        /// <summary>추출 대상 폴더 보장 — Root/Textures 및 캐릭터별 서브폴더 생성(부재 시).</summary>
        private static void EnsureExtractFolder(string stem)
        {
            if (!AssetDatabase.IsValidFolder(ExtractFolder))
            {
                if (!AssetDatabase.IsValidFolder(Root))
                {
                    Debug.LogError($"[134차 A-2] 루트 폴더 부재 — {Root}");
                    return;
                }
                AssetDatabase.CreateFolder(Root, "Textures");
            }
            if (!AssetDatabase.IsValidFolder($"{ExtractFolder}/{stem}"))
                AssetDatabase.CreateFolder(ExtractFolder, stem);
        }

        /// <summary>[134차 A-2 재수리] 1차 실행 잔존물 정리 — 공유 Textures 루트에 바로 놓인
        /// 텍스처(캐릭터별 서브폴더 밖)는 24회 덮어쓰기의 교차 오염 원천이므로 삭제(멱등).
        /// 서브폴더 내 텍스처는 보존. [재수리 실측] GUIDToAssetPath는 상대경로("Assets/...")이므로
        /// GetFullPath 절대경로와 비교하면 무효 — 상대경로 정규화 비교로 수정.</summary>
        private static void CleanupStaleSharedTextures()
        {
            if (!AssetDatabase.IsValidFolder(ExtractFolder)) return;
            string rootNorm = ExtractFolder.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ExtractFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string dir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/')
                    .TrimEnd('/').ToLowerInvariant();
                if (dir == rootNorm)
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[134차 A-2] 교차 오염 잔존 텍스처 삭제 — {path}");
                }
            }
        }
    }
}
