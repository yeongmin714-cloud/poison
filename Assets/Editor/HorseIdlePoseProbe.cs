using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;   // AnimationMode — 에디터 클립 샘플링
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [168-P5 v2] 말(갈색말) 자세 진단 프로브 — HorseMountSystem.SpawnHorse 생산 파이프라인 1:1 재현.
    /// v1 교훈 반영: ①본 경로 비교를 Animator 루트(go 자체) 기준으로 계산(루트명 포함 오판 제거)
    /// ②AnimationMode.SampleAnimationClip으로 Idle 클립을 에디터에서 직접 샘플링해
    ///   "클립 미바인딩 → GLB 바인드 포즈" 가설을 포즈 차이로 증명.
    ///   - samplePose == bindPose (픽셀 동일) → 바인딩 실패 확정 → Rebind/아바타/본경로 수리 방향.
    ///   - samplePose != bindPose → 바인딩 성공 — 왜곡은 루트 오프셋/스케일 쪽.
    /// 산출: TestOutput/horse168_probe.log + horse168_bind.png + horse168_sample.png. Unity 실행은 부모 몫.
    /// </summary>
    public static class HorseIdlePoseProbe
    {
        public static void RunAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Horse168] === probe v2 start ===");
            try
            {
                var prefab = Resources.Load<GameObject>("Models/UserProvided/말/적토마");
                sb.AppendLine($"[Horse168] prefab load = {(prefab != null ? "OK" : "NULL")}");
                if (prefab == null) { Flush(sb); return; }

                var go = Object.Instantiate(prefab, new Vector3(0f, -100f, 0f), Quaternion.identity);
                go.name = "Horse168Probe";

                // ── 생산 경로(SpawnHorse) 1:1: Animator 부재 → AddComponent + 컨트롤러 ──
                var anim = go.GetComponentInChildren<Animator>();
                bool addedAtRuntime = anim == null;
                if (anim == null) anim = go.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/갈색말_AC");
                sb.AppendLine($"[Horse168] animatorAddedAtRuntime={addedAtRuntime} ctrl={(ctrl != null ? ctrl.name : "NULL")}");
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                var av = anim.avatar;
                sb.AppendLine($"[Horse168] avatar = {(av != null ? av.name + " isHuman=" + av.isHuman : "NULL(제너릭 경로 바인딩)")}");

                // ── ① 본 경로 대조 — Animator 루트 = go (AddComponent 위치) 기준 ──
                var paths = new System.Collections.Generic.List<string>();
                CollectPaths(go.transform, "", paths, skipRootName: true);
                sb.AppendLine($"[Horse168] hierarchy paths(animator-relative) = {paths.Count}");
                for (int i = 0; i < paths.Count && i < 6; i++) sb.AppendLine($"  H[{i}] {paths[i]}");

                var idle = LoadIdleClip();
                sb.AppendLine($"[Horse168] idle clip = {(idle != null ? idle.name : "NULL")}");
                int matched = -1;
                if (idle != null)
                {
                    var clipPaths = new System.Collections.Generic.HashSet<string>();
                    foreach (var b in AnimationUtility.GetCurveBindings(idle)) clipPaths.Add(b.path);
                    matched = 0;
                    foreach (var p in clipPaths) if (paths.Contains(p)) matched++;
                    sb.AppendLine($"[Horse168] clip curve paths = {clipPaths.Count}, BINDING MATCH = {matched}/{clipPaths.Count}");
                    int idx = 0;
                    foreach (var p in clipPaths) { if (idx++ < 5) sb.AppendLine($"  C[{idx - 1}] {p}"); }
                }

                // ── ② 에디터 클립 샘플링 — 바인드 포즈 vs Idle 샘플 포즈 비교 ──
                // 리바인드 후보 경로도 함께 검증: Rebind()가 바인딩을 살리는지.
                anim.Rebind();
                CapturePose(go, "horse168_bind.png", sb);          // 기본(바인드) 포즈
                if (idle != null)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, idle, 0.5f);
                    CapturePose(go, "horse168_sample.png", sb);    // Idle 0.5s 샘플 포즈
                    AnimationMode.EndSampling();
                }
                sb.AppendLine($"[Horse168] BINDING MATCH(final) = {matched}");
                sb.AppendLine("[Horse168] 판독 기준: bind.png vs sample.png 픽셀 동일 → 미바인딩(Rebind/아바타 수리), 상이 → 바인딩 성공(루트 오프셋 수리)");
                Object.DestroyImmediate(go);
                sb.AppendLine("[Horse168] DONE");
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"[Horse168] FAIL {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            Flush(sb);
        }

        static AnimationClip LoadIdleClip()
        {
            var p = AssetDatabase.GUIDToAssetPath("bbc998aa692603a4ea45f2b1f9db252b");
            if (string.IsNullOrEmpty(p)) return null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p)) if (a is AnimationClip c) return c;
            return null;
        }

        static void CollectPaths(Transform t, string prefix, System.Collections.Generic.List<string> paths, bool skipRootName)
        {
            var name = skipRootName ? "" : t.name;
            var p = prefix == "" ? name : prefix + "/" + name;
            if (t.childCount == 0 || true) { }   // 구조 유지용 — 하위만 순회
            for (int i = 0; i < t.childCount && paths.Count < 200; i++)
            {
                var c = t.GetChild(i);
                var cp = (p == "" || skipRootName && prefix == "") ? c.name : p + "/" + c.name;
                paths.Add(cp);
                CollectChild(c, cp, paths);
            }
        }

        static void CollectChild(Transform t, string prefix, System.Collections.Generic.List<string> paths)
        {
            for (int i = 0; i < t.childCount && paths.Count < 200; i++)
            {
                var c = t.GetChild(i);
                var cp = prefix + "/" + c.name;
                paths.Add(cp);
                CollectChild(c, cp, paths);
            }
        }

        static void CapturePose(GameObject go, string file, StringBuilder sb)
        {
            try
            {
                var camGo = new GameObject("Horse168Cam");
                var cam = camGo.AddComponent<Camera>();
                var rt = new RenderTexture(900, 640, 24);
                cam.targetTexture = rt;
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
                if (b.size == Vector3.zero) b.size = Vector3.one;
                var center = b.center;
                var size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                cam.transform.position = center + new Vector3(0f, size * 0.4f, -size * 1.4f);
                cam.transform.LookAt(center);
                cam.aspect = 900f / 640f;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(900, 640, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 900, 640), 0, 0);
                tex.Apply();
                Directory.CreateDirectory("TestOutput");
                File.WriteAllBytes("TestOutput/" + file, tex.EncodeToPNG());
                sb.AppendLine($"[Horse168] capture {file} (bounds size={b.size})");
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"[Horse168] capture fail({file}) {e.GetType().Name}: {e.Message}");
            }
        }

        static void Flush(StringBuilder sb)
        {
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/horse168_probe.log", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
