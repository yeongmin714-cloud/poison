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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TeamColor;
                half  _Intensity;
                half  _RingRadius;
                half  _RingThickness;
                half  _GlowWidth;
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

                // 링 밴드: 내측 페이드 → 두께 밴드 → 외측 글로우
                float inner = smoothstep(_RingRadius - _RingThickness - _GlowWidth,
                                         _RingRadius - _RingThickness, d);
                float outer = 1.0 - smoothstep(_RingRadius + _RingThickness,
                                               _RingRadius + _RingThickness + _GlowWidth, d);
                float ring = inner * outer;

                // Gentle, low-amplitude breathing; alpha stays zero outside the annulus.
                float pulse = 0.94 + 0.06 * sin(_Time.y * _PulseSpeed);
                // Keep optional arc animation for existing command-marker consumers;
                // selection rings set _ArcSpeed to zero for an even Fluent appearance.
                float ang = atan2(c.y, c.x);
                float arc = frac(ang * 0.15915494309 + _Time.y * _ArcSpeed);
                float highlight = _ArcSpeed > 0.001 ? (0.55 + 0.45 * step(0.72, arc)) : 1.0;
                half3 col = _TeamColor.rgb * _Intensity * highlight;
                half alpha = saturate(ring * pulse * _TeamColor.a);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
