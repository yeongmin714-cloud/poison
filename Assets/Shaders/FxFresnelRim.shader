Shader "ProjectName/FX/FresnelRim"
{
    // [무번호 29-D3] 프레넬 림 오버레이 — 뷰 의존 가장자리 발광(몬스터 충전/히트·무기 플래시·공격자 림).
    // 계약: 가산 블렌드 투명(ZWrite Off), 몸색 계약 무손상(오버레이 별도 패스 — 틴트 경유),
    //       SRP Batcher 호환(CBuffer), 머티리얼 인스턴스는 FxRimOverlay 소유(누출 금지).
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 0.6, 0.2, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "FresnelRim"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewWS = GetWorldSpaceViewDir(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float fres = pow(saturate(1.0 - dot(normalize(IN.normalWS), normalize(IN.viewWS))), _RimPower);
                half a = saturate(fres * _RimIntensity) * _RimColor.a;
                return half4(_RimColor.rgb * _RimIntensity, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
