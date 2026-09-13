using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P3 전리품 스폰 반짝임 연출 (LootBasket.Create 훅용).
    /// - Resources.Load("FX/Loot/LootWipe") 프리팹이 있으면 그것을 사용 (URP Linear Wipe 계열).
    /// - 없으면 절차적 폴백: 작은 골드 파티클 스파이클을 런타임 생성.
    ///   (프로젝트 내 Linear Wipe 후보 프리팹 "Assets/Vefects/Invisible VFX URP/Demo/Resources/Prefabs/VFX_Linear_Wipe_Money_Coins_GS_Static_01 TH.prefab"
    ///    과 "_ VFX.prefab"은 모두 HDRP 셰이더 SH_Vefects_HDRP_VFX_Opaque_Dither_Linear_Wipe_01 참조 → URP 프로젝트에서 사용 불가)
    /// - static 진입점(코루틴 불가)이므로 자가 파괴 컴포넌트(AutoDestroy)로 수명 관리.
    /// - 0.1초 쿨다운 스팸 가드, 로드 실패 시 1회만 경고.
    /// </summary>
    public static class LootSpawnFX
    {
        const string ResourcePath = "FX/Loot/LootWipe";
        const float CooldownSeconds = 0.1f;
        const float LifetimeSeconds = 3f;
        const float PrefabScale = 0.6f;

        static float _lastPlayRealtime = -999f;
        static bool _warnedOnce;

        /// <summary>전리품 스폰 위치에서 반짝임 연출을 재생합니다. 실패해도 게임 로직에 영향 없음.</summary>
        public static void PlaySpawn(Vector3 position)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastPlayRealtime < CooldownSeconds)
                return;
            _lastPlayRealtime = now;

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab != null)
            {
                GameObject fx = Object.Instantiate(prefab, position, Quaternion.identity);
                fx.transform.localScale = Vector3.one * PrefabScale;
                AttachAutoDestroy(fx, LifetimeSeconds);
                return;
            }

            if (!_warnedOnce)
            {
                _warnedOnce = true;
                Debug.LogWarning(
                    "[LootSpawnFX] Resources/FX/Loot/LootWipe.prefab 없음 — 절차적 골드 스파이클 폴백 사용. " +
                    "후보 프리팹(Assets/Vefects/Invisible VFX URP/Demo/Resources/Prefabs/VFX_Linear_Wipe_Money_Coins_GS_Static_01 TH.prefab)은 " +
                    "HDRP 셰이더(SH_Vefects_HDRP_VFX_Opaque_Dither_Linear_Wipe_01)라 URP 프로젝트에 복사 불가. " +
                    "URP용 Linear Wipe 프리팹을 Resources/FX/Loot/LootWipe로 두면 자동 전환됨.");
            }

            SpawnProceduralGoldSpike(position);
        }

        // === 절차적 폴백: 골드 파티클 스파이클 + 골드 확장 링 ===
        // [2026-09-13 가시화 보강] 스펙 상향 — startSize 0.14→0.2m, burst 24→32발,
        // startLifetime 0.6→0.8s. 머티리얼 우선순위를 "Sprites/Default"(URP 확실 렌더,
        // 빌트인 호환 셰이더라 URP에서 무조건 렌더됨) → URP Particles/Unlit → 기본 순으로
        // 변경하고, 해상된 셰이더 이름을 1회성 진단 로그로 남긴다(반짝임 미표시 리포트 추적용).

        static bool _shaderLoggedOnce;

        static void SpawnProceduralGoldSpike(Vector3 position)
        {
            // 스폰 즉시 골드 링 1회 — 지면 근처(+0.1m)에서 0.8m 반경으로 0.4s 확장.
            // ShockwaveRingFX에는 자체 스팸 가드가 없으므로 PlaySpawn의 0.1s 쿨다운이 상위 가드 역할.
            ShockwaveRingFX.Spawn(position + Vector3.up * 0.1f, 0.8f, new Color(1f, 0.85f, 0.35f), 0.4f);

            // 발화 오리진을 지면 위로 — hit.point 그대로면 파티클 절반이 지하에서 발화해 안 보임
            Vector3 origin = position + Vector3.up * 0.4f;

            GameObject root = new GameObject("LootSpawnFX_GoldSpike");
            root.transform.position = origin;
            AttachAutoDestroy(root, LifetimeSeconds); // static에서 코루틴 불가 → 자가 파괴 컴포넌트

            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 3.0f;
            main.startSize = 0.2f;
            main.startColor = new Color(1f, 0.85f, 0.35f); // 골드
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.75f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            // 파티클 재질 — 우선순위: "Sprites/Default"(URP 확실 렌더) → URP Particles/Unlit →
            // Particles/Standard Unlit → 기본 머티리얼 유지. 해상 결과를 1회만 진단 로그로 남긴다.
            Color gold = new Color(1f, 0.85f, 0.35f, 1f);
            Shader unlit = Shader.Find("Sprites/Default")
                           ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Particles/Standard Unlit");
            if (unlit != null)
            {
                Material mat = new Material(unlit);
                // 셰이더별 틴트 프로퍼티 분기 — Sprites/Default는 _Color, URP 계열은 _BaseColor.
                // 미존재 프로퍼티 SetColor는 무해하므로 양쪽 모두 기록한다(분기 누락 방지).
                mat.SetColor("_Color", gold);      // Sprites/Default 틴트
                mat.SetColor("_BaseColor", gold);  // URP Particles/Unlit 틴트
                var rnd = root.GetComponent<ParticleSystemRenderer>();
                if (rnd != null)
                    rnd.material = mat;

                if (!_shaderLoggedOnce)
                {
                    _shaderLoggedOnce = true;
                    Debug.Log($"[LootSpawnFX] 폴백 FX 셰이더={unlit.name}");
                }
            }

            ps.Play(true);
        }

        // === 자가 파괴 (static 진입점에서 코루틴 불가) ===

        static void AttachAutoDestroy(GameObject go, float seconds)
        {
            if (go.GetComponent<AutoDestroy>() == null)
            {
                var ad = go.AddComponent<AutoDestroy>();
                ad.remaining = seconds;
            }
        }

        sealed class AutoDestroy : MonoBehaviour
        {
            public float remaining = 3f;

            void Update()
            {
                remaining -= Time.deltaTime;
                if (remaining <= 0f)
                {
                    var ps = GetComponent<ParticleSystem>();
                    if (ps != null && ps.IsAlive(true))
                    {
                        remaining = 0.5f; // 파티클 잔여분 소진 대기
                        return;
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}