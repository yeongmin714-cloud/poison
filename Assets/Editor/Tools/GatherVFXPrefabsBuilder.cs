using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [133차 P1] 채집/낚시 타격 파티클 프리펩 5종 + 프레임링 텍스처·머티리얼 배치 빌더.
    /// 근거: docs/133_IMMERSION_FEEDBACK_PLAN.md §Phase 1 (1-C 타격 파티클 프리펩).
    /// ① 디렉터리 Assets/Resources/VFX/Gather 보장(부재 시 생성).
    /// ② FX_Ring.png 128x128 절차 생성 — 중심 투명/바깥 투명, r0(0.38)~r1(0.50) 사이 소프트 링
    ///    (거리 기반 알파 falloff), 흰색. 기존 존재 시 재생성 스킵(멱등).
    /// ③ 머티리얼 5종(Sprites/Default + mainTexture 통일) — 프리펩과 분리 저장해 재사용.
    /// ④ 파티클 프리펩 5종(GameObject + ParticleSystem 코드 구성 → SaveAsPrefabAsset → 씬 오브젝트 파기).
    ///    이미 프리펩 존재 시 스킵(멱등) — 재튜닝은 프리펩 수동 삭제 후 재실행.
    /// 129차 PlayerActionsAnimSetup / 132차 RollDirectionsAnimSetup의 멱등·검증로그 패턴 준용.
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.GatherVFXPrefabsBuilder.Run
    /// 완료 마커: "[133차 VFX] DONE"
    /// </summary>
    public static class GatherVFXPrefabsBuilder
    {
        private const string Dir = "Assets/Resources/VFX/Gather";
        private const string RingPng = Dir + "/FX_Ring.png";

        // 링 반경 정규화 기준: 텍스처 한 변(중심 0, 변 중점 0.5) — 스펙 r0=0.38, r1=0.50.
        private const float RingR0 = 0.38f;
        private const float RingR1 = 0.50f;

        private static int _texCreated, _texSkipped;
        private static int _matCreated, _matSkipped;
        private static int _prefabCreated, _prefabSkipped;

        [MenuItem("Tools/VFX/Build Gather VFX Prefabs")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            // ── ① 디렉터리 보장 ──
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                AssetDatabase.Refresh();
                Debug.Log($"[133차 VFX] 디렉터리 신규 생성 — {Dir}");
            }

            // ── ② 프레임링 텍스처 ──
            Texture2D ring = EnsureRingTexture();
            if (ring == null) { Debug.LogError("[133차 VFX] FX_Ring 로드 실패 — 중단"); return; }

            // ── ③ 머티리얼 5종(Sprites/Default + mainTexture 통일) ──
            var matWood   = EnsureMaterial("GatherVFX_Mat_WoodChips", null);
            var matStone  = EnsureMaterial("GatherVFX_Mat_StoneFragments", null);
            var matLeaf   = EnsureMaterial("GatherVFX_Mat_LeafPuff", null);
            var matWater  = EnsureMaterial("GatherVFX_Mat_WaterDroplets", null);
            var matRipple = EnsureMaterial("GatherVFX_Mat_FishRipple", ring);
            if (matWood == null || matStone == null || matLeaf == null || matWater == null || matRipple == null)
            {
                Debug.LogError("[133차 VFX] 머티리얼 확보 실패 — 중단");
                return;
            }

            // ── ④ 파티클 프리펩 5종 ──
            // ① 목재 칩 — 위쪽 60도 원뿔, 중력 낙하, 갈색 랜덤, sizeOverLifetime 끝 0.6배.
            BuildPrefab("GatherVFX_WoodChips", matWood, (go, ps) => ConfigureBurst(ps,
                burstCount: 12, speed: V(2.5f, 3.5f), size: V(0.06f, 0.11f), life: V(0.5f, 0.8f),
                gravity: 6f, grad: MakeGradient(C(0x6E4A22), C(0x8B5A2B), C(0xA06A32)),
                angularMax: 180f, solEndScale: 0.6f, drag: 0f, downCone: false, lifetimeFade: false));

            // ② 돌 파편 — 회색 랜덤, 강한 중력.
            BuildPrefab("GatherVFX_StoneFragments", matStone, (go, ps) => ConfigureBurst(ps,
                burstCount: 10, speed: V(3f, 4f), size: V(0.05f, 0.09f), life: V(0.4f, 0.7f),
                gravity: 9f, grad: MakeGradient(C(0x7A7A7A), C(0x8A8A8A), C(0x9C9C9C)),
                angularMax: 240f, solEndScale: 0f, drag: 0f, downCone: false, lifetimeFade: false));

            // ③ 풀잎 puff — 초록 3톤, 약한 중력 + drag 0.5, 느린 회전.
            BuildPrefab("GatherVFX_LeafPuff", matLeaf, (go, ps) => ConfigureBurst(ps,
                burstCount: 8, speed: V(0.6f, 1.0f), size: V(0.08f, 0.15f), life: V(0.9f, 1.4f),
                gravity: 0.8f, grad: MakeGradient(C(0x3E8E41), C(0x4CAF50), C(0x7CC47F)),
                angularMax: 60f, solEndScale: 0f, drag: 0.5f, downCone: false, lifetimeFade: false));

            // ④ 물방울 — 아래-바깥 원뿔, 청백색 수명 페이드.
            BuildPrefab("GatherVFX_WaterDroplets", matWater, (go, ps) => ConfigureBurst(ps,
                burstCount: 14, speed: V(1.8f, 2.6f), size: V(0.04f, 0.08f), life: V(0.35f, 0.6f),
                gravity: 9.8f, grad: null, angularMax: 0f, solEndScale: 0f, drag: 0f, downCone: true,
                lifetimeFade: true, solidColor: C(0x9ED9FF)));

            // ⑤ 물결 링 — FX_Ring 텍스처, 0.3m에서 1.6m로 확장, 수평 링(루트 x=90), 수명 0.9초.
            BuildPrefab("GatherVFX_FishRipple", matRipple, (go, ps) =>
            {
                ConfigureRipple(ps);
                // 파티클은 빌보드라 시각 차이는 없지만 스펙 계약(스폰 시 수평 링 + 추후 메시 렌더 전환 대비)에 따라
                // 프리펩 루트를 x=90으로 저장한다.
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 검증 실측: Resources 하위 파일 목록 + 에셋 로드 가능 여부 ──
            string[] files = Directory.GetFiles(Dir)
                .Select(Path.GetFileName)
                .Where(f => !f.EndsWith(".meta"))
                .OrderBy(f => f)
                .ToArray();
            Debug.Log($"[133차 VFX] 배치 결과 — 텍스처 +{_texCreated}/스킵 {_texSkipped} · " +
                      $"머티리얼 +{_matCreated}/스킵 {_matSkipped} · 프리펩 +{_prefabCreated}/스킵 {_prefabSkipped}");
            Debug.Log($"[133차 VFX] {Dir} 실측 {files.Length}건: {string.Join(", ", files)}");
            Debug.Log("[133차 VFX] 로드 검증: " + VerifyAssets(ring, matWood, matStone, matLeaf, matWater, matRipple));

            Debug.Log("[133차 VFX] DONE");
        }

        // ===== 텍스처/머티리얼 멱등 확보 =====

        /// <summary>FX_Ring.png 절차 생성 — 기존 존재 시 재생성 스킵(멱등). 로드 성공 텍스처 반환.</summary>
        private static Texture2D EnsureRingTexture()
        {
            if (!File.Exists(RingPng))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
                var pixels = new Color[size * size];
                float mid = (RingR0 + RingR1) * 0.5f; // 링 피크 반경
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // 정규화 거리: 중심 0, 변 중점 0.5(픽셀 중점 기준).
                        float dx = (x + 0.5f - size * 0.5f) / size;
                        float dy = (y + 0.5f - size * 0.5f) / size;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        // 소프트 링: r0에서 피크까지 상승, 피크에서 r1까지 하강(양쪽 투명).
                        float a = Mathf.SmoothStep(RingR0, mid, d) * (1f - Mathf.SmoothStep(mid, RingR1, d));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(RingPng, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                _texCreated++;

                AssetDatabase.ImportAsset(RingPng, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(RingPng) is TextureImporter ti)
                {
                    ti.filterMode = FilterMode.Bilinear; // 스펙: Bilinear
                    ti.sRGBTexture = true;               // 스펙: sRGB
                    ti.alphaIsTransparency = true;       // 링 알파 정합
                    // 압축은 기본값 유지(스펙).
                    ti.SaveAndReimport();
                }
                Debug.Log($"[133차 VFX] FX_Ring.png 절차 생성 완료 — {RingPng}");
            }
            else
            {
                _texSkipped++;
                Debug.Log($"[133차 VFX] FX_Ring.png 기존 존재 — 재생성 스킵(멱등)");
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(RingPng);
        }

        /// <summary>머티리얼 멱등 확보 — Sprites/Default + mainTexture 통일. 기존 존재 시 스킵.</summary>
        private static Material EnsureMaterial(string name, Texture2D mainTex)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                _matSkipped++;
                return existing;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError($"[133차 VFX] Sprites/Default 셰이더 부재 — {name} 생성 불가");
                return null;
            }
            var mat = new Material(shader);
            if (mainTex != null) mat.mainTexture = mainTex;
            AssetDatabase.CreateAsset(mat, path);
            _matCreated++;
            Debug.Log($"[133차 VFX] 머티리얼 생성 — {path}");
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>프리펩 멱등 생성 — 코드 구성 후 SaveAsPrefabAsset, 씬 오브젝트는 즉시 파기.
        /// 기존 프리펩 존재 시 스킵(재튜닝은 수동 삭제 후 재실행 안내).</summary>
        private static void BuildPrefab(string name, Material mat, System.Action<GameObject, ParticleSystem> configure)
        {
            string path = $"{Dir}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                _prefabSkipped++;
                Debug.Log($"[133차 VFX] {name} 기존 프리펩 존재 — 스킵(멱등). 재튜닝 시 프리펩 수동 삭제 후 재실행");
                return;
            }

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            configure(go, ps);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go); // 씬 오브젝트 파기 — 에셋만 잔존
            if (prefab == null)
                Debug.LogError($"[133차 VFX] {name} 프리펩 저장 실패 — {path}");
            else
                _prefabCreated++;
        }

        // ===== 파티클 구성 =====

        /// <summary>파편형 4종 공통 구성 — 0.05초 원샷 버스트 + 원뿔 방출 + 옵션(중력/회전/드래그/SOL/페이드).</summary>
        private static void ConfigureBurst(ParticleSystem ps, int burstCount, Vector2 speed, Vector2 size,
            Vector2 life, float gravity, Gradient grad, float angularMax, float solEndScale,
            bool downCone, bool lifetimeFade, float drag = 0f, Color? solidColor = null)
        {
            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.playOnAwake = false; // 호출부 Play() 계약 — 계획문 "Instantiate/Play" 패턴
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64; // 개체수 상한 — 저사양 프레임 방지(계획문 리스크 항목)
            if (solidColor.HasValue)
                main.startColor = new ParticleSystem.MinMaxGradient(solidColor.Value);
            else if (grad != null)
                main.startColor = new ParticleSystem.MinMaxGradient(grad)
                    { mode = ParticleSystemGradientMode.RandomColor }; // 그레디언트 톤 랜덤 픽

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(0f); // 버스트 전용
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f; // 위쪽(또는 아래) 60도 원뿔
            shape.radius = 0.05f;
            // 원뿔 기본 방출축 +Z → 위(-90) 또는 아래(+90)로 회전.
            shape.rotation = downCone ? new Vector3(90f, 0f, 0f) : new Vector3(-90f, 0f, 0f);

            if (angularMax > 0f)
            {
                // [133차 수리] ParticleSystem에 .rotation 부재 → rotationOverLifetime(Z축 빌보드 스핀).
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-angularMax, angularMax); // ±랜덤(프로젝트 선례: 2-float=TwoConstants)
            }

            if (solEndScale > 0f && !Mathf.Approximately(solEndScale, 1f))
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, solEndScale));
            }

            if (lifetimeFade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(MakeGradient(new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 1f, 1f, 0f))); // 수명 말 알파 0 페이드
            }

            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit = new ParticleSystem.MinMaxCurve(999f); // 속도 제한 미작동 보험 — 드래그만 유효하게
                lim.drag = drag;
            }
        }

        /// <summary>물결 링 전용 구성 — 확장 링 1버스트 2파티클, Local 시뮬(루트 x=90 회전과 짝).</summary>
        private static void ConfigureRipple(ParticleSystem ps)
        {
            var rippleShape = ps.shape; // [133차 수리] 모듈 구조체 임시변수 패턴(CS1612)
            rippleShape.enabled = false; // 점 방출 — 방향 무관(시작 속도 0)

            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.playOnAwake = false; // 호출부 Play() 계약
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f); // 실질 크기는 SOL 커브가 담당(아래)
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 스펙: Local
            main.maxParticles = 8;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.6f)); // 흰색 alpha 0.6

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)2) }); // 1회 2파티클

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            // 실질 크기 0.3m → 1.6m 확장(스펙: startSize 0.3 → 1.6m 커브).
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1.6f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGradient(new Color(1f, 1f, 1f, 1f),
                new Color(1f, 1f, 1f, 0f))); // 알파 0.6 → 0 페이드
        }

        // ===== 소형 헬퍼 =====

        private static Vector2 V(float min, float max) => new Vector2(min, max);

        /// <summary>16진수 → Color 변환(0xRRGGBB).</summary>
        private static Color C(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>색 톤들을 균등 키로 배치한 그레디언트(RandomColor 픽션용).</summary>
        private static Gradient MakeGradient(params Color[] colors)
        {
            var grad = new Gradient();
            var keys = new GradientColorKey[colors.Length];
            var alphaKeys = new GradientAlphaKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float t = colors.Length == 1 ? 0f : (float)i / (colors.Length - 1);
                keys[i] = new GradientColorKey(colors[i], t);
                alphaKeys[i] = new GradientAlphaKey(colors[i].a, t);
            }
            grad.SetKeys(keys, alphaKeys);
            return grad;
        }

        /// <summary>에셋 로드 가능 여부 실측 문자열 — 부재 시 "부재!"로 노출(조용한 실패 금지).</summary>
        private static string VerifyAssets(Texture2D ring, params Material[] mats)
        {
            string ringState = ring != null ? "OK" : "부재!";
            var parts = new System.Collections.Generic.List<string> { $"FX_Ring={ringState}" };
            foreach (var m in mats)
                parts.Add($"{(m != null ? m.name : "?")}={(m != null ? "OK" : "부재!")}");
            foreach (var name in new[]
                     {
                         "GatherVFX_WoodChips", "GatherVFX_StoneFragments", "GatherVFX_LeafPuff",
                         "GatherVFX_WaterDroplets", "GatherVFX_FishRipple"
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.prefab");
                parts.Add($"{name}={(prefab != null ? "OK" : "부재!")}");
            }
            return string.Join(", ", parts);
        }
    }
}
