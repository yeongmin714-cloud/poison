// [157차 P-D1] 획득 연출 빛기둥/링 셰이더 — "사각형 파티클" 개선(사용자 지시).
//  요소: ①UV.x 가우시안 에지 소프트 페더(사각형 하드엣지 소멸) ②세로 그라데이션(하단 강조→상단 소멸)
//  ③스크롤 값 노이즈(흐르는 빛 — 텍스처 의존 0, 해시 결정론) ④HDR 코어 배수(기존 IndoorPostProcess 블룸 연동).
//  계약: 등급색/알파는 _BaseColor로만 수신 — RarityShaft 길이/알파 티어 표(153차)·스파클 표(154차) 무손상.
//  블렌딩: SrcAlpha/One(additive)·ZWrite off·큐 3000 — CreateFxQuad 기존 설정 동일. 폴백=기존 Particles/Unlit.
Shader "Custom/AcquisitionBeam"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _CoreIntensity ("Core HDR Intensity", Range(1, 4)) = 2.0
        _ScrollSpeed ("Noise Scroll Speed", Range(0, 2)) = 0.6
        _NoiseScale ("Noise Scale", Range(1, 12)) = 5.0
        _EdgeSoftness ("Edge Softness", Range(0.02, 1)) = 0.42
        _BottomBoost ("Bottom Boost", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _CoreIntensity;
                half _ScrollSpeed;
                half _NoiseScale;
                half _EdgeSoftness;
                half _BottomBoost;
            CBUFFER_END

            // 해시 값 노이즈 — 결정론(Random API 불필요, 텍스처 의존 0).
            float Hash12(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);   // smoothstep 보간
                float a = Hash12(i);
                float b = Hash12(i + float2(1.0, 0.0));
                float c = Hash12(i + float2(0.0, 1.0));
                float d = Hash12(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half2 uv = IN.uv;

                // ① 에지 소프트 페더 — 가우시안 근사(abs(dx)²/σ²): 사각형 하드엣지 소멸.
                half dx = abs(uv.x - 0.5h) * 2.0h;
                half sigma = max(_EdgeSoftness, 0.02h);
                half edge = exp(-(dx * dx) / (sigma * sigma));

                // ② 세로 그라데이션 — 하단 강조 → 상단 소멸(빛이 위로 샌다).
                half vFade = 1.0h - smoothstep(0.12h, 1.0h, uv.y);
                vFade = pow(saturate(vFade), 0.8h);
                half bottom = 1.0h + _BottomBoost * (1.0h - smoothstep(0.0h, 0.35h, uv.y));

                // ③ 스크롤 값 노이즈 — 2옥타브, 0.65~1.0 변조(어둡게만 — 과점 방지).
                float2 nUV = float2(uv.x * _NoiseScale, uv.y * _NoiseScale * 0.6 + _TimeParameters.x * _ScrollSpeed);
                half n = (half)ValueNoise(nUV) * 0.6h + (half)ValueNoise(nUV * 2.3 + 17.1) * 0.4h;
                half noiseMul = 0.65h + 0.35h * n;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half alpha = _BaseColor.a * tex.a;
                alpha *= edge * vFade * noiseMul * bottom;
                alpha = saturate(alpha);

                half3 rgb = _BaseColor.rgb * max(tex.rgb, half3(0.5h, 0.5h, 0.5h));   // 텍스처 어두움 계열 무해화
                rgb *= _CoreIntensity;   // ④ HDR — 기존 블룸 파이프라인 연동
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
