// SelectionRing.shader — clean Fluent-style selection ring (URP, alpha blended)
// Annular UV mask with a soft edge glow, gentle pulse, and team color.
Shader "Custom/SelectionRing"
{
    Properties
    {
        _TeamColor   ("Team Color", Color) = (0.2, 0.5, 1, 1)
        _Intensity   ("Intensity", Range(0.5, 5)) = 1.0
        _RingRadius  ("Ring Radius", Range(0.4, 3)) = 1.0
        _RingThickness ("Ring Thickness", Range(0.02, 0.4)) = 0.035
        _GlowWidth   ("Glow Width", Range(0, 0.35)) = 0.06
        // [2026-10-11 Fluent] 액센트 밴드 아래 다크 패널 백킹(UTKTheme #161B22 톤) — 내측 넓게+외측 얇게
        //   (쿼드 가장자리(d=1.0) 클립 아티팩트 회피: R+T+OuterPad ≤ 0.98).
        _BackingColor ("Backing Color", Color) = (0.086, 0.106, 0.133, 0.5)
        _BackingInnerPad ("Backing Inner Pad", Range(0, 0.5)) = 0.24
        _BackingOuterPad ("Backing Outer Pad", Range(0, 0.1)) = 0.05
        // [2026-10-11 15차 입체감] rim 하이라이트(화면-상 방향 얇은 밝은 선 — 금속 베벨) + 접지 드롭섀도
        _RimDirection ("Rim Direction (local XY)", Vector) = (0, 1, 0, 0)
        _RimIntensity ("Rim Intensity", Range(0, 1)) = 0.35
        _DropColor ("Drop Shadow Color", Color) = (0, 0, 0, 0.28)
        _DropWidth ("Drop Shadow Width", Range(0, 0.5)) = 0.18
        _PulseSpeed  ("Pulse Speed", Range(0, 8)) = 0.9
        _ArcSpeed    ("Arc Speed", Range(0, 3)) = 0.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require derivatives

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TeamColor;
                half  _Intensity;
                half  _RingRadius;
                half  _RingThickness;
                half  _GlowWidth;
                half4 _BackingColor;
                half  _BackingInnerPad;
                half  _BackingOuterPad;
                half4 _RimDirection;
                half  _RimIntensity;
                half4 _DropColor;
                half  _DropWidth;
                half  _PulseSpeed;
                half  _ArcSpeed;
            CBUFFER_END

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float d = length(c);

                // [2026-10-11 15차 품질] 해상도 무관 에지 — fwidth 파생 폭으로 근거리 계단/원거리 흐림 동시 제거.
                float aa = max(fwidth(d), 1e-4);

                // 액센트 코어 — 크리스프(fwidth AA 에지).
                float core = smoothstep(_RingRadius - _RingThickness - aa, _RingRadius - _RingThickness + aa, d)
                           * (1.0 - smoothstep(_RingRadius + _RingThickness - aa, _RingRadius + _RingThickness + aa, d));
                // 소프트 글로우 — 밴드 양옆 넓은 페이드(원거리에서 LOD 폭 확대).
                float glowFade = max(_GlowWidth, aa * 3.0);
                float glow = smoothstep(_RingRadius - _RingThickness - glowFade, _RingRadius - _RingThickness, d)
                           * (1.0 - smoothstep(_RingRadius + _RingThickness, _RingRadius + _RingThickness + glowFade, d));

                // [Fluent] 다크 백킹 밴드 — 내측 넓게/외측 얇게(LOD 폭 확대).
                float backFadeI = max(_BackingInnerPad, aa * 4.0);
                float backFadeO = max(_BackingOuterPad, aa * 3.0);
                float backing = smoothstep(_RingRadius - _RingThickness - backFadeI, _RingRadius - _RingThickness, d)
                              * (1.0 - smoothstep(_RingRadius + _RingThickness, _RingRadius + _RingThickness + backFadeO, d));

                // [입체감] 접지 드롭섀도 — 백킹 바깥의 넓은 소프트 어두운 확산(링이 바닥에 앉은 느낌).
                float dropStart = _RingRadius + _RingThickness + _BackingOuterPad;
                float drop = 1.0 - smoothstep(dropStart, dropStart + max(_DropWidth, aa * 5.0), d);

                // Gentle, low-amplitude breathing; alpha stays zero outside the annulus.
                float pulse = 0.94 + 0.06 * sin(_Time.y * _PulseSpeed);
                float ang = atan2(c.y, c.x);
                float arc = frac(ang * 0.15915494309 + _Time.y * _ArcSpeed);
                float highlight = _ArcSpeed > 0.001 ? (0.55 + 0.45 * step(0.72, arc)) : 1.0;

                // [입체감] rim 하이라이트 — 화면-상 방향(카메라 전방 XZ → 로컬 XY) 밴드가 밝게 빛난다.
                float2 rimDir = normalize(_RimDirection.xy + float2(1e-5, 0.0));
                float rim = smoothstep(0.30, 0.95, dot(normalize(c + float2(1e-5, 0.0)), rimDir));

                half3 accentCol = _TeamColor.rgb * _Intensity * highlight * (1.0 + _RimIntensity * rim);
                half coreA = saturate((core + glow * 0.40) * pulse * _TeamColor.a);
                half backA = saturate(backing * _BackingColor.a);
                half dropA = saturate(drop * _DropColor.a);

                // 표준 over 합성 — 드롭섀도 → 다크 백킹 → 액센트 밴드 순.
                half alpha = saturate(max(dropA, max(coreA, backA)));
                half3 rgb = _DropColor.rgb;
                rgb = lerp(rgb, _BackingColor.rgb, saturate(backA / max(alpha, 0.0001)));
                rgb = lerp(rgb, accentCol, saturate(coreA / max(alpha, 0.0001)));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
