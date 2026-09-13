using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-13(45차 P1): FX 전역 색 팔레트 + 런타임 파티클 머티리얼 정규화.
    /// BOTW 팔레트 통일 — 흰/골드/주황(+블러드) 상수만 사용한다.
    /// 근거: 테스트 7 영상 히트 프레임 픽셀 실측 — 보라 파티클 평균 RGB (219,19,219)
    /// = Unity 셰이더 에러 마젠타. 원인은 런타임 생성 파티클(CombatVFXController 6종)이
    /// 머티리얼 미지정 → 기본 Particles/Standard Unlit → URP 미지원이었으므로,
    /// 모든 런타임 파티클 렌더러는 FXPalette.ApplyTo()로 정규화 머티리얼을 강제한다.
    /// </summary>
    public static class FXPalette
    {
        // ================================================================
        // BOTW 팔레트 상수 (흰/골드/주황 3색 + 블러드)
        // ================================================================
        /// <summary>코어 흰색 — 플래시/일반 데미지 숫자.</summary>
        public static readonly Color Core = new Color(1f, 1f, 1f);
        /// <summary>골드 — 크리/강타 데미지 숫자, 히트 스파크(44차 골드화이트).</summary>
        public static readonly Color Accent = new Color(1f, 0.9f, 0.5f);
        /// <summary>옅은 주황 — 외곽 스파크/기타 강조.</summary>
        public static readonly Color Edge = new Color(1f, 0.55f, 0.2f);
        /// <summary>블러드 붉은색 — 출혈/암살 계열(순수 Color.red 대체).</summary>
        public static readonly Color Blood = new Color(0.75f, 0.08f, 0.08f);

        // ================================================================
        // 정규화 파티클 머티리얼 (지연 캐시)
        // ================================================================
        private static Material _particleMaterial;
        private static bool _shaderResolved; // 해상 로그 1회 스팸 가드

        /// <summary>
        /// 정규화된 파티클 머티리얼 — 최초 접근 시 1회 생성 후 캐시.
        /// 셰이더 폴백: Sprites/Default(빌트인, URP 확실 렌더) → URP Particles/Unlit
        /// → Particles/Standard Unlit. 기본색 흰색 세팅.
        /// </summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMaterial == null)
                    _particleMaterial = CreateParticleMaterial();
                return _particleMaterial;
            }
        }

        private static Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");

            if (shader == null)
            {
                // 폴백 전부 실패 — 마젠타 위험 경고 1회(스팸 가드), null 반환 시 ApplyTo가 무시한다.
                if (!_shaderResolved)
                {
                    _shaderResolved = true;
                    Debug.LogWarning("[FXPalette] 파티클 셰이더 폴백 전부 실패(마젠타 위험) — 머티리얼 미적용");
                }
                return null;
            }

            var mat = new Material(shader);
            mat.name = "FXPalette_ParticleMaterial";
            // 기본색 흰색 — Sprites/Default는 _Color, URP Particles/Unlit은 _BaseColor(둘 다 기록).
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            if (!_shaderResolved)
            {
                _shaderResolved = true;
                Debug.Log($"[FXPalette] 파티클 머티리얼 정규화 — shader={shader.name}");
            }
            return mat;
        }

        /// <summary>
        /// 파티클 렌더러에 정규화 머티리얼 적용. rnd가 null이면 무시(안전 반환).
        /// sharedMaterial 사용 — 머티리얼 인스턴스 난립 방지(단일 공유 캐시).
        /// </summary>
        public static void ApplyTo(ParticleSystemRenderer rnd)
        {
            if (rnd == null) return;
            var mat = ParticleMaterial;
            if (mat == null) return;
            rnd.sharedMaterial = mat;
        }
    }
}
