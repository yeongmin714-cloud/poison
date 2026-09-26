using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-09-26 젤다 화살예시 방패 막기] 적 병사가 방패로 화살을 막아냈을 때의 명중점 VFX.
    /// 영상 실측(BotW): 청록/흰 별섬광 + 확장하는 흰 원형 링 + 노란 스파크 — 짧고 또렷(과장 금지).
    /// 구성: ① 확장 링(ShockwaveRingFX 재사용 — 탑다운에서 원형 충격파) ② 별 섬광(StarFlare Quad)
    /// ③ 노란 스파크(shadow_glow Hemisphere 파티클). 모두 1회성 자기소멸.
    /// </summary>
    public static class ArrowShieldBlockFX
    {
        /// <summary>
        /// 방패 막기 연출 생성 — 명중점에서 확장 링 + 별 섬광 + 노란 스파크를 짧게 뿌린다.
        /// </summary>
        public static void Play(Vector3 pos)
        {
            try
            {
                SpawnRing(pos);
                SpawnStar(pos);
                SpawnSparks(pos);
            }
            catch (System.Exception e)
            {
                Debug.Log("[ArrowShieldBlock] 방패 막기 FX 생성 실패: " + e.ToString());
            }
        }

        /// <summary>① 확장 링 — 지면(발모양) 흰 충격파, 탑다운 뷰에서 또렷.</summary>
        private static void SpawnRing(Vector3 pos)
        {
            ShockwaveRingFX.Spawn(pos + Vector3.up * 0.1f, 0.9f, new Color(1f, 1f, 1f, 0.9f), 0.25f);
        }

        /// <summary>② 별 섬광 — StarFlare Quad 빌보드, 흰~청록 발광, 0.4s 스케일업+페이드(자기소멸).</summary>
        private static void SpawnStar(Vector3 pos)
        {
            // ArrowProjectile.SpawnStarFlare는 public static — 재사용(재구현 금지).
            // 내부 +up0.7 적용이라 명중점 기준 약간 위에 뜨다.
            ArrowProjectile.SpawnStarFlare(pos + Vector3.up * 0.3f);
        }

        /// <summary>③ 노란 스파크 — shadow_glow 소프트 파티클 5입자, Hemisphere 방사, 0.25s, 자기소멸.</summary>
        private static void SpawnSparks(Vector3 pos)
        {
            var soft = Resources.Load<Texture2D>("UI/shadow_glow");
            var go = new GameObject("ArrowShieldSpark");
            go.transform.position = pos + Vector3.up * 0.5f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = 0.22f;
            main.startSpeed = 2.2f;
            main.startSize = 0.11f;
            main.startColor = new Color(1f, 0.85f, 0.2f, 0.95f);   // 노랑
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.08f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
            var tex = soft != null ? soft : Texture2D.whiteTexture;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = tex;
            mat.color = new Color(1f, 0.9f, 0.35f, 0.9f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = mat;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.9f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col2.color = grad;
            Object.Destroy(go, 0.6f);
        }
    }
}