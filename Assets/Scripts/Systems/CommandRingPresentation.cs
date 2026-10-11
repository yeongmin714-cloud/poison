using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.Systems
{
    /// <summary>
    /// Single visual owner for move, selected-guard, and explicit-target ground rings.
    /// [2026-10-11 사용자 요구 — 링 수정] 시각 소스를 Custom/SelectionRing 셰이더 단일 소스로 전환.
    ///   - 구 경로(베이크 MoveTargetRing 텍스처)는 파일 실측상 환형 밴드가 결손된 깨진 에셋(불투명 2%)
    ///     + 투명 경로 불안정(배경이 검은 사각형으로 렌더)으로 폐기. 셰이더는 ShaderLab 상태
    ///     (Blend SrcAlpha OneMinusSrcAlpha / ZWrite Off)가 확정이라 머티리얼 프로퍼티 우행 의존이 없다.
    ///   - 지름 절반(BaseScale 1.9→0.95) + 밴드 증두(쿼드 UV 거리 공간 코어 폭 2T=0.28).
    ///   - 3색(이동 골드/선택 파랑/타겟 빨강)은 틴트만 다른 인스턴스 머티리얼 — 공유 변이 금지 계약 유지.
    /// </summary>
    public static class CommandRingPresentation
    {
        public const string SelectionShaderName = "Custom/SelectionRing";
        // Archived Figma 156:18/172:2 core-ring footprint is 400×120; preserve the floor-plane oval ratio.
        // [2026-10-11 사용자 요구] 링 지름 = 현행의 절반 — 1.9 → 0.95.
        public const float BaseScale = 0.95f;
        // [2026-10-11 사용자 요구] 이동 포인트 링 = 현행보다 조금 크게(0.475→0.62 = BaseScale의 65%).
        public const float MoveMarkerScale = BaseScale * 0.65f;
        public const float FigmaCoreRingAspect = 400f / 120f;
        public const float PulseAmplitude = 0.04f;   // [2026-10-11 Fluent] 절제된 펄스
        public const float PulseFrequency = 2.6f;

        // [2026-10-11] 셰이더 링 형상 — d는 쿼드 UV*2-1 공간(가장자리=1.0). 밴드 코어 = R±T.
        // 구 베이크 밴드(0.68~0.90, 폭 0.22)보다 두껍게(코어 폭 0.28). 글로우는 Fluent 절제 톤(0.04).
        public const float ShaderRingRadius = 0.78f;
        public const float ShaderRingThickness = 0.14f;
        public const float ShaderGlowWidth = 0.04f;
        // [2026-10-11 Fluent] 다크 백킹 — UTKTheme 패널 #161B22 @0.5, 내측 넓게/외측 얇게.
        public static readonly Color RingBackingColor = new Color(0.086f, 0.106f, 0.133f, 0.5f);
        public const float RingBackingInnerPad = 0.24f;
        public const float RingBackingOuterPad = 0.05f;
        // [15차 입체감] rim 하이라이트 + 접지 드롭섀도.
        // [16차 사용자 요구] 드롭섀도 제거 — 알파 0(링 아래 미세한 검정 그림자 소멸). rim은 유지.
        public const float RingRimIntensity = 0.35f;
        public static readonly Color RingDropColor = new Color(0f, 0f, 0f, 0f);
        public const float RingDropWidth = 0.18f;
        // [15차] 명령 ack 확산 펄스 — 스폰 순간 1회 확산+페이드.
        public const float AckBurstDuration = 0.35f;
        public const float AckBurstStartScale = 1.30f;
        public const float AckBurstStartAlpha = 0.45f;

        // Replaced baked-texture thickness — documentation only, no longer referenced by the active path.
        public const float RingThickness = 0.22f;

        // [2026-10-11 사용자 요구] 이동 링 = 골드 → Fluent 액센트 파랑(#58A6FF). 선택 링과 동일 액센트 —
        //   구분은 크기(이동 마커 = 절반)와 백킹 대비가 담당.
        public static readonly Color MoveTargetColor = new Color(0.345f, 0.651f, 1f, 1f);
        public static readonly Color SelectedGuardColor = new Color(0.345f, 0.651f, 1f, 1f);
        public static readonly Color ExplicitTargetColor = new Color(0.973f, 0.318f, 0.286f, 1f);

        /// <summary>Create the shared shader-based ring material, differing only by team tint.</summary>
        public static Material CreateMaterial(Color tint)
        {
            var shader = Shader.Find(SelectionShaderName);
            if (shader != null)
            {
                var material = new Material(shader) { name = "CommandRing_" + ColorUtility.ToHtmlStringRGB(tint) };
                material.SetColor("_TeamColor", tint);
                material.SetFloat("_RingRadius", ShaderRingRadius);
                material.SetFloat("_RingThickness", ShaderRingThickness);
                material.SetFloat("_GlowWidth", ShaderGlowWidth);
                material.SetColor("_BackingColor", RingBackingColor);
                material.SetFloat("_BackingInnerPad", RingBackingInnerPad);
                material.SetFloat("_BackingOuterPad", RingBackingOuterPad);
                material.SetVector("_RimDirection", new Vector4(0f, 1f, 0f, 0f));
                material.SetFloat("_RimIntensity", RingRimIntensity);
                material.SetColor("_DropColor", RingDropColor);
                material.SetFloat("_DropWidth", RingDropWidth);
                // 펄스는 GetPulseScale(트랜스폼)이 단일 소유 — 셰이더 알파 펄스/호 스윕은 off(이중 애니메이션 방지).
                material.SetFloat("_PulseSpeed", 0f);
                material.SetFloat("_ArcSpeed", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
                return material;
            }

            // 폴백(셰이더 탐색 실패 환경): Sprites/Default + 절차 환형 텍스처 — ShaderLab 블렌드 확정 경로.
            var fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return null;
            var mat = new Material(fallback) { name = "CommandRing_Fallback_" + ColorUtility.ToHtmlStringRGB(tint) };
            mat.mainTexture = CommandMarker.BuildRingTexture(256);
            mat.color = tint;
            return mat;
        }

        /// <summary>Apply the common material to an existing colliderless ring renderer.</summary>
        public static Material Apply(Renderer renderer, Color tint)
        {
            if (renderer == null) return null;
            var material = CreateMaterial(tint);
            if (material != null) renderer.sharedMaterial = material;
            return material;
        }

        private static Vector3 _cachedCamForwardXZ = new Vector3(0f, 0f, 1f);
        private static float _nextRimRefresh;

        /// <summary>[15차 입체감] rim 방향 갱신 — 화면-상 = 카메라 전방 XZ(쿼드 X90 → 로컬 XY = 월드 XZ).
        ///   0.25s 스로틀 캐시. 폴백 머티리얼(Sprites/Default)은 속성 부재로 스킵.</summary>
        public static void UpdateRimDirection(Renderer renderer, Material instanceMaterial)
        {
            if (renderer == null || instanceMaterial == null) return;
            if (!instanceMaterial.HasProperty("_RimDirection")) return;
            float now = Time.time;
            if (now >= _nextRimRefresh)
            {
                _nextRimRefresh = now + 0.25f;
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 fwd = cam.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 1e-6f) _cachedCamForwardXZ = fwd.normalized;
                }
            }
            instanceMaterial.SetVector("_RimDirection", new Vector4(_cachedCamForwardXZ.x, _cachedCamForwardXZ.z, 0f, 0f));
        }

        /// <summary>Shared slow pulse; caller supplies the untinted visual's initial scale.</summary>
        public static Vector3 GetPulseScale(Vector3 baseScale, float elapsed)
        {
            float pulse = 1f + PulseAmplitude * Mathf.Sin(elapsed * PulseFrequency);
            return baseScale * pulse;
        }
    }
}
