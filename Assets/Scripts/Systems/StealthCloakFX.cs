using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P4 은신 클로ak VFX — "Invisible VFX URP" 팩 기반 은신 진입/해제 연출.
    ///
    /// URP 판정 결과 (2026-09-12 grep 기준):
    /// - Assets/Vefects/Invisible VFX URP/VFX/Materials/M_VFX_Invisible_01~05.mat 전부
    ///   셰이더 guid 7f225d053ab9d3641bfd28dd0eda2671 = SH_Vefects_VFX_Invisible_01_URP.shader
    ///   → Tags { "RenderPipeline"="UniversalPipeline" }, "LightMode"="UniversalForwardOnly" 패스 포함
    ///   → URP 셰이더 확정. HDRP 아님 → 절차 폴백 불필요, 프리팹 경로 채택.
    /// - 단, 팩 자체에 Invisible 계열 파티클 VFX 프리팹은 미수록(프로젝트 전체에서
    ///   M_VFX_Invisible_* 머티리얼을 참조하는 .prefab 0개 — 데모용 메시/빌보드 프리팹만 존재).
    ///   → Resources/FX/Cloak/StealthCloak.prefab 을 신규 제작(파티클 시스템 + M_VFX_Invisible_02
    ///   렌더러 참조)하여 Resources 로드 경로를 확보함.
    ///
    /// 동작:
    /// - Attach(player): Resources.Load("FX/Cloak/StealthCloak") → 플레이어 자식으로 부착
    ///   (로컬 위치 원점, 루프 동작). 자가 수명 60s 후 은신 유지 중이면 재안착(자동 갱신).
    /// - Detach(player): 클로ak 오브젝트 제거.
    /// - SFX: 은신 진입 시 루프 SFX 1개 (Vefects 원본 wav는 Resources 밖이라
    ///   Resources/FX/Cloak/StealthCloakLoop.wav 로 복사본 사용. 로드 불가 시 SFX 스킵 — 주석 대안).
    /// - 프리팹 부재 시 절차 폴백: 청백/골드 셰이머 파티클 링(URP 파티클 언릿) 생성 + 1회 경고.
    /// - 모든 실패는 격리(1회 경고)되며 은신 코어 로직에 영향 없음.
    /// </summary>
    public static class StealthCloakFX
    {
        const string PrefabPath = "FX/Cloak/StealthCloak";
        const string LoopSfxPath = "FX/Cloak/StealthCloakLoop";
        const string RootName = "StealthCloakFX_Root";
        const float ReanchorSeconds = 60f;

        static bool _warnedOnce;

        /// <summary>플레이어에게 은신 클로ak VFX를 부착합니다. 실패해도 게임 로직에 영향 없음.</summary>
        public static void Attach(GameObject player)
        {
            try
            {
                AttachInternal(player);
            }
            catch (System.Exception e)
            {
                WarnOnce($"[StealthCloakFX] 클로ak VFX 부착 실패 (은신 자체에는 영향 없음): {e.Message}");
            }
        }

        /// <summary>플레이어의 은신 클로ak VFX를 제거합니다.</summary>
        public static void Detach(GameObject player)
        {
            try
            {
                if (player == null || player.transform == null) return;
                var t = player.transform;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var child = t.GetChild(i);
                    if (child != null && child.name == RootName)
                        Object.Destroy(child.gameObject);
                }
            }
            catch (System.Exception e)
            {
                WarnOnce($"[StealthCloakFX] 클로ak VFX 해제 실패 (무시 가능): {e.Message}");
            }
        }

        static void AttachInternal(GameObject player)
        {
            if (player == null) return;

            // 중복 부착 정리 (토글 반복/재안착 대비) — 항상 단일 인스턴스 유지
            Detach(player);

            GameObject fx = null;
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab != null)
            {
                fx = Object.Instantiate(prefab, player.transform);
                fx.name = RootName;
                fx.transform.localPosition = Vector3.zero;   // 위치 원점 (플레이어 기준)
                fx.transform.localRotation = Quaternion.identity;
                fx.transform.localScale = Vector3.one;
            }
            else
            {
                WarnOnce(
                    "[StealthCloakFX] Resources/FX/Cloak/StealthCloak.prefab 로드 실패 — 절차 폴백(청백/골드 셰이머 파티클 링) 사용. " +
                    "원인: Resources 경로에 프리팹 없음. 프리팹을 두면 자동 전환됨.");
                fx = CreateProceduralShimmer(player.transform);
            }

            if (fx == null) return;

            // 자가 수명 60s + 은신 유지 중이면 재안착
            var re = fx.AddComponent<CloakReanchor>();
            re.owner = player.transform;

            // SFX: 은신 진입 루프 1개
            AttachLoopSfx(fx);
        }

        // === SFX ===

        static void AttachLoopSfx(GameObject fx)
        {
            // Vefects/Invisible VFX URP/SFX 원본 wav는 Resources 밖이라 Resources.Load 불가
            // → Resources/FX/Cloak/StealthCloakLoop.wav (SFX_Vefects_Invisible_VFX_Invisibility_Cloak_Loop_1 복사본) 사용.
            // 로드 불가 시 SFX 스킵 (은신 연출 자체는 유지).
            AudioClip clip = Resources.Load<AudioClip>(LoopSfxPath);
            if (clip == null) return; // SFX 스킵 — Resources에 루프 wav 없음

            var src = fx.GetComponent<AudioSource>();
            if (src == null) src = fx.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0.6f; // 반 3D — 플레이어 근처에서 은신 붕붕 소리
            src.volume = 0.7f;
            src.Play();
        }

        // === 절차 폴백: 청백/골드 셰이머 파티클 링 (프리팹 부재 시) ===

        static GameObject CreateProceduralShimmer(Transform parent)
        {
            GameObject root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;                       // 루프 동작
            main.startLifetime = 1f;
            main.startSpeed = 0.25f;
            main.startSize = 0.14f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.9f, 1f), 0f),   // 청백
                    new GradientColorKey(new Color(1f, 0.85f, 0.4f), 1f)    // 골드
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            // URP 파티클 언릿 재질 (Shader.Find 실패 시 기본 파티클 재질 유지)
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Particles/Standard Unlit");
            if (unlit != null)
            {
                Material mat = new Material(unlit);
                mat.SetColor("_BaseColor", new Color(0.8f, 0.95f, 1f, 1f));
                var rnd = root.GetComponent<ParticleSystemRenderer>();
                if (rnd != null)
                {
                    rnd.material = mat;
                    rnd.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }

            ps.Play(true);
            return root;
        }

        // === 자가 수명 60s + 재안착 ===

        static void WarnOnce(string msg)
        {
            if (_warnedOnce) return;
            _warnedOnce = true;
            Debug.LogWarning(msg);
        }

        sealed class CloakReanchor : MonoBehaviour
        {
            public Transform owner;
            float _remaining = ReanchorSeconds;

            void Update()
            {
                _remaining -= Time.deltaTime;
                if (_remaining > 0f) return;

                bool stillStealthed = StealthSystem.Instance != null && StealthSystem.Instance.IsStealthed;
                if (stillStealthed && owner != null)
                {
                    _remaining = ReanchorSeconds;
                    StealthCloakFX.Attach(owner.gameObject); // 재안착 (기존 오브젝트 교체)
                    return;
                }
                Destroy(gameObject);
            }
        }
    }
}