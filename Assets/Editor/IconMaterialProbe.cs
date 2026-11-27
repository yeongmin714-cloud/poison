using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [189차 후속 진단] 폭탄 아이콘 "초록색으로 깨짐" 판별 프로브 — 배치 에디터(비Play)에서
    /// bomb_explosive.glb 프리팹을 인스턴스화해 ①머티리얼 슬롯 바인딩 사실(_BaseMap/_BumpMap 텍스처)을
    /// 콘솔로 덤프하고 ②동기베이크와 동일 프레이밍(스케일 1.8/maxDim·FOV30·정면)로 아이콘 PNG를 저장한다.
    /// 실행: Unity -batchmode -executeMethod ProjectName.EditorTools.IconMaterialProbe.DumpBombMaterialFacts
    /// 출력: TestOutput/bomb_icon_probe/ (facts.log + icon.png + basemap.png)
    /// </summary>
    public static class IconMaterialProbe
    {
        private const string BombPath = "Assets/Resources/Models/UserProvided/bomb_explosive.glb";

        public static void DumpBombMaterialFacts()
        {
            var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestOutput", "bomb_icon_probe");
            Directory.CreateDirectory(outDir);
            var log = new StringWriter();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombPath);
                if (prefab == null) { Debug.LogError($"[Probe] prefab null: {BombPath}"); File.WriteAllText(Path.Combine(outDir, "facts.log"), "prefab null"); return; }

                var node = Object.Instantiate(prefab);
                node.name = "IconProbe_bomb_explosive";
                var remote = new Vector3(10000f, 1000f, 10000f);
                node.transform.position = remote;

                var rends = node.GetComponentsInChildren<Renderer>();
                log.WriteLine($"renderers={rends.Length}");
                Texture baseMap = null;
                foreach (var r in rends)
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        log.WriteLine($"MAT name={mat.name} shader={mat.shader?.name}");
                        string[] propNames = new string[] { "_BaseMap", "_BaseColorMap", "_MainTex", "_BumpMap" };
                        foreach (var prop in propNames)
                        {
                            if (!mat.HasProperty(prop)) continue;
                            var t = mat.GetTexture(prop);
                            log.WriteLine($"  tex slot={prop} name={(t != null ? t.name : "NULL")} size={(t != null ? $"{t.width}x{t.height}" : "-")}");
                            if (t != null)
                                baseMap = t;
                        }
                        // 텍스처 크기 불일치/교차바인딩 단서 — 슬롯별 GUID 비교를 로그에 남긴다
                        var bm = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                        var bp = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                        if (bm != null && bp != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bm, out string g1, out long _)
                            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bp, out string g2, out long _))
                            log.WriteLine($"  BINDING_CHECK baseMapGUID={g1} bumpGUID={g2} SAME={(g1 == g2)}");
                    }
                }

                // ── 동기베이크 동일 프레이밍 아이콘 렌더 ──
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.01f) node.transform.localScale *= 1.8f / maxDim;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                node.transform.position += new Vector3(0f, remote.y - b.min.y, 0f);
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                var camGo = new GameObject("IconProbeCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f);
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.fieldOfView = 30f;
                float halfV = Mathf.Max(0.5f, b.extents.y * 1.25f);
                float halfH = Mathf.Max(0.5f, b.extents.x * 1.25f);
                float dist = Mathf.Max(halfV, halfH) / Mathf.Tan(30f * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = b.center + new Vector3(0f, 0f, -dist);
                cam.transform.LookAt(b.center);
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var copy = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, "icon.png"), copy.EncodeToPNG());

                if (baseMap != null)
                {
                    var brt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
                    var bprev = RenderTexture.active;
                    RenderTexture.active = brt;
                    Graphics.Blit(baseMap, brt);
                    var btex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    btex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    btex.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, "basemap.png"), btex.EncodeToPNG());
                    RenderTexture.active = bprev;
                    RenderTexture.ReleaseTemporary(brt);
                    Object.DestroyImmediate(btex);
                }
                log.WriteLine($"iconRendered=True baseMap={(baseMap != null ? baseMap.name : "NULL")}");

                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(node);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            catch (System.Exception ex)
            {
                log.WriteLine("EXCEPTION: " + ex);
                Debug.LogError("[Probe] " + ex);
            }
            finally
            {
                var content = log.ToString();
                File.WriteAllText(Path.Combine(outDir, "facts.log"), content);
                Debug.Log("[Probe] facts:\n" + content);
            }
        }
    }
}
