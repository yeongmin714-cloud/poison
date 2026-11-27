Shader "ProjectName/FXBandingRing"
{
    // [무번호 26-DF1] FX 링 전용 Unlit 셰이더 — 밴드 텍스처 × 틴트 + BL 발광 + 깊이 페이드 소프트 교차.
    //  TelegraphRing.shader 규약 복제(URP UniversalPipeline/Transparent/ZWrite Off/Blend SrcAlpha OneMinusSrcAlpha/Cull Off/SRP Batcher CBUFFER).
    //  [무번호 26-DF2] 호환 계약 — 기존 ShockwaveRingFX 코드 무수정 원칙:
    //   _Color(mat.color 매핑)·_MainTex(mat.mainTexture 매핑)·_EmissionColor(BL ApplyEmissionBoost 그대로 유효)·_DepthFadeDist.
    //  폴백 계약: 셰이더 부재 시 EnsureAssets가 기존 URP Lit → Standard 폴백(로직 무영향).
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Band", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _DepthFadeDist ("Soft Distance", Range(0.02, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "FXRingUnlit"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _EmissionColor;
                float _DepthFadeDist;
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
                // 밴드 텍스처 × 틴트 — 기존 "텍스처 × color 승수" 구조 동일 [무번호 26-DF2]
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                // [무번호 26-DF2] BL 발광 경로 — [무번호 68-P1] saturate 캡으로 변경: 기존 saturate 없이
                //   HDR 합산하면 링색(α≥0.75·화이트 러프)에 ×2.0 발광이 더해져 프레임버퍼 클램프로
                //   **순백/암색 판**으로 포화(테스트 12~14 실측 — 피격 지점을 따라다니는 흰/검 사각의 뿌리).
                //   발광은 유지하되 최종 rgb를 LDR로 고정(블룸 흡수는 1.0 이하 강도로도 작동).
                col.rgb = saturate(col.rgb + _EmissionColor.rgb);
                // [무번호 26-DF1] 깊이 페이드 — TelegraphRing.shader L152-157 수식 그대로(fail-open:
                //   깊이 텍스처 부재 환경은 무페이드 — 안전). 지형/장애물 교차 지점에서 소프트 소멸.
                float2 suv = IN.positionHCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                col.a = saturate(col.a * saturate((sceneEye - IN.positionHCS.w) / _DepthFadeDist));
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
