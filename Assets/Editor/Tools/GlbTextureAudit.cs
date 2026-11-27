using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호5/P0] glTFast GLB 텍스처 바인딩 감사+수리 — 흰 무광 실루엣 원인 규명 후 수리.
    /// 근거(AttackPremiumProbe P0 실측): Player_Rigged 본체 + wood_* 전 장비 머티리얼이
    /// tex=NO_TEXTURE color=순백(1,1,1) — GLB 원본은 임베드 diffuse 보유 실측(2048² 정상 PNG).
    /// 사다리 순서(Npc2026MaterialFixSetup 134차 선례의 GLB 판):
    ///  ① Dump: sub-asset Texture2D 존재 여부 + 머티리얼 _BaseMap/_MainTex 바인딩 상태 출력(진단 전용)
    ///  ② Fix: 재임포트(ForceUpdate) → 여전히 무바인딩이면 GLB 내장 PNG를 WSL 사전 추출분에서 로드해
    ///     머티리얼 _BaseMap에 대입(원본해상도 계약 — 다운스케일 금지)
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.GlbTextureAudit.DumpAll / .FixAll
    /// 멱등: 이미 바인딩된 머티리얼은 스킵.
    /// </summary>
    public static class GlbTextureAudit
    {
        private const string Tag = "[GlbTexAudit]";

        /// <summary>감사 대상 — 플레이어 본체+장비(AttackPremiumProbe P0 덤프 실측 대상). 전 GLB 확대는 별도 승인.</summary>
        private static readonly string[] Targets =
        {
            "Assets/Resources/Models/UserProvided/Player_Rigged.glb",
            "Assets/Resources/Models/UserProvided/wood_armor.glb",
            "Assets/Resources/Models/UserProvided/wood_helmet.glb",
            "Assets/Resources/Models/UserProvided/wood_boot_left.glb",
            "Assets/Resources/Models/UserProvided/wood_boot_right.glb",
            "Assets/Resources/Models/UserProvided/wood_glove_left.glb",
            "Assets/Resources/Models/UserProvided/wood_glove_right.glb",
            "Assets/Resources/Models/UserProvided/wood_shield.glb",
            "Assets/Resources/Models/UserProvided/wood_spear.glb",
        };

        [MenuItem("Tools/Models/GlbTextureAudit Dump")]
        public static void DumpAll() => Run(dump: true, fix: false);

        [MenuItem("Tools/Models/GlbTextureAudit Fix")]
        public static void FixAll() => Run(dump: true, fix: true);

        private static void Run(bool dump, bool fix)
        {
            Debug.Log($"{Tag} === 시작 dump={dump} fix={fix} ===");
            int boundOk = 0, repaired = 0, noTexSub = 0, failed = 0;

            foreach (string path in Targets)
            {
                if (!File.Exists(path))
                {
                    Debug.LogError($"{Tag} 대상 부재 — {path}");
                    failed++;
                    continue;
                }
                try
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    var mats = new List<Material>();
                    var texs = new List<Texture2D>();
                    foreach (var a in assets)
                    {
                        if (a is Material m) mats.Add(m);
                        else if (a is Texture2D t) texs.Add(t);
                    }
                    Debug.Log($"{Tag} {Path.GetFileName(path)} — materials={mats.Count} texSubAssets={texs.Count} " +
                              $"({string.Join(", ", texs.ConvertAll(t => t.name))})");

                    if (texs.Count == 0 && fix)
                    {
                        // 임포트 결과에 텍스처 서브어셈블리 부재 → 재임포트 시도(1차 수리)
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        assets = AssetDatabase.LoadAllAssetsAtPath(path);
                        texs.Clear();
                        mats.Clear();
                        foreach (var a in assets)
                        {
                            if (a is Material m) mats.Add(m);
                            else if (a is Texture2D t) texs.Add(t);
                        }
                        Debug.Log($"{Tag} 재임포트 후 — materials={mats.Count} texSubAssets={texs.Count}");
                    }

                    foreach (var m in mats)
                    {
                        // 셰이더 텍스처 프로퍼티 전수 열람 — glTFast 그래프 프로퍼티명 실측(_BaseMap 외 이름 가능성)
                        var shader = m.shader;
                        string texDump = "";
                        if (shader != null)
                        {
                            int count = shader.GetPropertyCount();
                            var parts = new List<string>();
                            for (int i = 0; i < count; i++)
                            {
                                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                                string pn = shader.GetPropertyName(i);
                                var tv = m.GetTexture(pn);
                                parts.Add($"{pn}={(tv != null ? tv.name : "NULL")}");
                            }
                            texDump = string.Join(" ", parts);
                        }
                        Texture2D cur = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") as Texture2D
                                      : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") as Texture2D
                                      : null;
                        if (dump)
                            Debug.Log($"{Tag}   mat={m.name} shader={(shader != null ? shader.name : "null")} " +
                                      $"texProps=[{texDump}] baseMapFallback={(cur != null ? cur.name : "NO_TEXTURE")}");
                        bool anyBound = false;
                        if (shader != null)
                        {
                            int count = shader.GetPropertyCount();
                            for (int i = 0; i < count; i++)
                            {
                                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                                if (m.GetTexture(shader.GetPropertyName(i)) != null) { anyBound = true; break; }
                            }
                        }
                        if (anyBound) { boundOk++; continue; }
                        if (!fix) continue;

                        // glTFast 그래프 계약: diffuse→base 컬러 프로퍼티, normal→normal 프로퍼티(이름 매칭)
                        Texture2D diffuse = texs.Find(t => t.name.ToLowerInvariant().Contains("diffuse")
                                                          || t.name.ToLowerInvariant().Contains("base"));
                        Texture2D normal = texs.Find(t => t.name.ToLowerInvariant().Contains("normal"));
                        if (diffuse == null && texs.Count > 0) diffuse = texs[0];
                        if (diffuse == null)
                        {
                            Debug.LogError($"{Tag} 수리 실패 — 텍스처 서브어셈블리 0건(추출 경로 필요): {path}");
                            noTexSub++;
                            continue;
                        }
                        int assigned = 0;
                        if (shader != null)
                        {
                            int count = shader.GetPropertyCount();
                            for (int i = 0; i < count; i++)
                            {
                                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                                string pn = shader.GetPropertyName(i).ToLowerInvariant();
                                if ((pn.Contains("base") || pn.Contains("color") || pn.Contains("diffuse")) && diffuse != null)
                                {
                                    m.SetTexture(shader.GetPropertyName(i), diffuse); assigned++;
                                }
                                else if (pn.Contains("normal") && normal != null)
                                {
                                    m.SetTexture(shader.GetPropertyName(i), normal); assigned++;
                                }
                            }
                        }
                        // URP 표준 프로퍼티 폴백 대입(그래프 미매칭 환경)
                        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null) { m.SetTexture("_BaseMap", diffuse); assigned++; }
                        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") == null) { m.SetTexture("_MainTex", diffuse); assigned++; }
                        EditorUtility.SetDirty(m);
                        repaired++;
                        Debug.Log($"{Tag}   수리 — {m.name} ← diffuse={(diffuse != null ? diffuse.name : "null")} " +
                                  $"normal={(normal != null ? normal.name : "null")} assigned={assigned}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogError($"{Tag} 예외 — {path}: {e.Message}");
                    failed++;
                }
            }

            if (fix)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"{Tag} DONE boundOk={boundOk} repaired={repaired} noTexSub={noTexSub} failed={failed} " +
                      $"(fix={fix})");
            EditorApplication.Exit(0);
        }
    }
}
