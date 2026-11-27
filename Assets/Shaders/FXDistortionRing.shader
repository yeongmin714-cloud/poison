Shader "ProjectName/FXDistortionRing"
{
    // [무번호 71-L2] 디스토션 히트 웨이브 링 — _CameraOpaqueTexture 샘플 + 링 밴드 내 방사 UV 오프셋(물결 룩).
    //  FXBandingRing.shader 규약 복제(URP UniversalPipeline/Transparent/ZWrite Off/Blend SrcAlpha OneMinusSrcAlpha/Cull Off/SRP Batcher CBUFFER).
    //  호환 계약 — FXBandingRing 패밀리 명명 승계: _Color(mat.color 매핑)·_MainTex(mat.mainTexture 매핑)·
    //  _EmissionColor(BL 경로)·_DepthFadeDist(깊이 페이드). + 링 형상 파라미터 _RingRadius/_RingWidth/_DistortStrength.
    //  [71-L2] HDR 계약 — 최종 rgb에 saturate 금지(씬 색 리프랙션·발광 합산 유지 — 클램프는 파이프라인 몫).
    //  폴백 계약: 셰이더 부재 시 FxDistortionWave가 null 폴백(무음 스킵 — 로직 무영향, Lit 폴백 없음).
    //  런타임 가드: 스포너(FxDistortionWave)가 URP opaque 텍스처 지원을 리플렉션 확인 — 미지원 환경은
    //  애초에 스폰을 차단하므로 본 셰이더가 _CameraOpaqueTexture 부재 환경에서 샘플되는 일이 없다(블랙 링 차단).
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Band", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _DepthFadeDist ("Soft Distance", Range(0.02, 0.5)) = 0.12
        _RingRadius ("Ring Radius", Range(0.05, 1.0)) = 0.8
        _RingWidth ("Ring Width", Range(0.02, 0.5)) = 0.18
        _DistortStrength ("Distort Strength", Range(0.0, 0.25)) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "FXDistortionRingUnlit"
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // [무번호 71-L2] 씬 불투명 색 — URPAsset m_RequireOpaqueTexture=1일 때 유효. 미제공 환경은
            //   스포너의 리플렉션 가드(FxDistortionWave.OpaqueTextureSupported)가 스폰 자체를 차단한다.
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _EmissionColor;
                float _DepthFadeDist;
                float _RingRadius;
                float _RingWidth;
                float _DistortStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 링 밴드 — UV 중심 기준 방사 거리(0=중심, 1=쿼드 가장자리). 밴드 = 웨이브 크레스트 부근 소프트 마스크.
                float2 centered = IN.uv - 0.5;
                float r = length(centered) * 2.0;
                float band = smoothstep(_RingRadius - _RingWidth, _RingRadius, r)
                           * (1.0 - smoothstep(_RingRadius, _RingRadius + _RingWidth, r));
                // [무번호 71-L2] 방사 UV 오프셋 — 밴드 내 씬 색 샘플 위치를 바깥쪽으로 밀어 리프랙션(물결 룩).
                //   중심에서 멀어질수록 오프셋 감쇠((1 - r) 가중) — 크레스트 안쪽이 강하게 물결.
                float invLen = r > 1e-4 ? 1.0 / (length(centered) + 1e-5) : 0.0;
                float2 radial = centered * invLen;
                float decay = 1.0 - saturate(r);
                float2 suv = IN.positionHCS.xy / _ScaledScreenParams.xy;
                float2 warp = radial * band * decay * _DistortStrength;
                half3 scene = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, suv + warp).rgb;

                // 밴드 텍스처 × 틴트 — 기존 "텍스처 × color 승수" 구조 동일 [FXBandingRing 26-DF2 승계]
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                half4 col;
                // [71-L2] 씬 색 리프랙션 + 발광 — HDR 유지(rgb 클램프 없음 — 계약).
                col.rgb = scene * tex.rgb + _EmissionColor.rgb;
                col.a = tex.a * band;
                // [FXBandingRing 26-DF1 수식 그대로 승계] 깊이 페이드 — fail-open(깊이 텍스처 부재 환경은 무페이드 — 안전).
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                col.a = saturate(col.a * saturate((sceneEye - IN.positionHCS.w) / _DepthFadeDist));
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
