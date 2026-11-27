Shader "ProjectName/FX/Dissolve"
{
    // [무번호 30-D4] 절차 디졸브 — 노이즈 임계 소멸 + 엣지 하이라이트(사망/소멸 연출).
    // 계약: 텍스처 불필요(셰이더 내 해시 노이즈 — 결정론, 난수 0건), URP Transparent,
    //       SRP Batcher 호환(CBuffer), 머티리얼 인스턴스는 FxDissolveUtility 소유(누출 금지).
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _EdgeColor ("Edge Color", Color) = (1, 0.5, 0.15, 1)
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.08
        _NoiseScale ("Noise Scale", Range(1, 40)) = 14
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "DissolveUnlit"
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _DissolveAmount;
                float4 _EdgeColor;
                float _EdgeWidth;
                float _NoiseScale;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // 결정론 해시 노이즈(value noise 2옥타브 — 시드 고정, 난수 0건)
            float Hash21(float2 p)
            {
                float s = sin(dot(p, float2(127.1, 311.7))) * 43758.5453;
                return s - floor(s);
            }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float DissolveNoise(float3 posWS, float scale)
            {
                float2 q = posWS.xz * scale + posWS.y * scale * 0.37;
                float n = 0.65 * VNoise(q) + 0.35 * VNoise(q * 2.13 + 7.7);
                return saturate(n);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // [무번호 39] 예약어 수리 — HLSL 예약어 `base` 사용 시 컴파일 실패 → 렌더가 마젠타로 깨짐(15:56 영상 실측).
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float n = DissolveNoise(IN.posWS, _NoiseScale);
                // 소멸: 노이즈 < 임계 영역 소거(알파컷), 임계 근처 밴드 = 엣지 하이라이트
                float th = _DissolveAmount * (1.0 + _EdgeWidth * 2.0) - _EdgeWidth;
                clip(n - th);
                float edge = 1.0 - saturate((n - th) / max(_EdgeWidth, 0.001));
                float shade = saturate(0.6 + 0.4 * dot(normalize(IN.normalWS), float3(0.3, 0.9, 0.2)));
                half3 rgb = lerp(baseCol.rgb * shade, _EdgeColor.rgb, edge);
                half a = baseCol.a * lerp(1.0, _EdgeColor.a, edge);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
