// Project-owned bounded gas composite. This is not Mirza AERO's fog shader/material.
Shader "ProjectName/Rendering/LocalizedGasComposite"
{
    Properties
    {
        _GasColor ("Gas Color", Color) = (0.55, 0.82, 0.42, 1)
        _GasRadius ("World Radius", Float) = 4
        _GasHeight ("Vertical Half Height", Float) = 2
        _GasDensity ("Density", Float) = 1.2
        _GasAlpha ("Emission Fade", Range(0, 1)) = 0
        _GasPlumeLength ("Forward Plume Length", Float) = 2.5
        _GasNoiseScale ("World Noise Scale", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Project Localized Gas Composite"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _GasCenter;
            float4 _GasForward;
            float4 _GasColor;
            float _GasRadius;
            float _GasHeight;
            float _GasDensity;
            float _GasAlpha;
            float _GasPlumeLength;
            float _GasNoiseScale;
            float _GasTime;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash21(i.xy + i.z * 37.0);
                float n100 = hash21(i.xy + float2(1, 0) + i.z * 37.0);
                float n010 = hash21(i.xy + float2(0, 1) + i.z * 37.0);
                float n110 = hash21(i.xy + float2(1, 1) + i.z * 37.0);
                float n001 = hash21(i.xy + (i.z + 1.0) * 37.0);
                float n101 = hash21(i.xy + float2(1, 0) + (i.z + 1.0) * 37.0);
                float n011 = hash21(i.xy + float2(0, 1) + (i.z + 1.0) * 37.0);
                float n111 = hash21(i.xy + float2(1, 1) + (i.z + 1.0) * 37.0);
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_GasAlpha <= 0.001 || _GasDensity <= 0.0 || _GasRadius <= 0.0 || _GasHeight <= 0.0)
                    return sceneColor;

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 axis = normalize(_GasForward.xyz + float3(0, 0.00001, 0));
                float3 relative = positionWS - _GasCenter.xyz;
                float forwardDistance = dot(relative, axis);
                float3 radialVector = relative - axis * forwardDistance;
                float radialDistance = length(radialVector);
                float plumeRadius = max(0.25, _GasRadius * 0.48);
                float plumeAxis = smoothstep(-_GasRadius * 0.45, 0.0, forwardDistance) *
                    (1.0 - smoothstep(_GasPlumeLength, _GasPlumeLength + plumeRadius, forwardDistance));
                float radial = 1.0 - smoothstep(plumeRadius * 0.42, plumeRadius, radialDistance);
                float vertical = 1.0 - smoothstep(_GasHeight * 0.55, _GasHeight, abs(relative.y));
                float localField = (1.0 - smoothstep(_GasRadius * 0.72, _GasRadius, length(relative)));
                float shape = saturate(max(localField * 0.55, plumeAxis * radial) * vertical);
                float noise = noise3(positionWS * max(0.001, _GasNoiseScale) + float3(0, _GasTime * 0.16, _GasTime * 0.08));
                float density = shape * saturate(_GasDensity * (0.78 + 0.44 * noise)) * _GasAlpha;
                float opacity = 1.0 - exp(-density * 0.42);
                float3 gas = lerp(_GasColor.rgb, float3(0.87, 0.96, 0.75), saturate(noise * 0.25));
                return float4(lerp(sceneColor.rgb, gas, opacity), sceneColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
