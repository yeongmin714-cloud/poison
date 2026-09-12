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

        // === 절차적 폴백: 작은 골드 파티클 스파이클 ===

        static void SpawnProceduralGoldSpike(Vector3 position)
        {
            GameObject root = new GameObject("LootSpawnFX_GoldSpike");
            root.transform.position = position;
            AttachAutoDestroy(root, LifetimeSeconds); // static에서 코루틴 불가 → 자가 파괴 컴포넌트

            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 2.2f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 0.85f, 0.35f); // 골드
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

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

            // URP 파티클 언릿 재질 (Shader.Find 실패 시 기본 파티클 재질 유지)
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Particles/Standard Unlit");
            if (unlit != null)
            {
                Material mat = new Material(unlit);
                mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.35f, 1f));
                var rnd = root.GetComponent<ParticleSystemRenderer>();
                if (rnd != null)
                    rnd.material = mat;
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