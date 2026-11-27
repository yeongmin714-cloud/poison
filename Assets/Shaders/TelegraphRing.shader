Shader "ProjectName/TelegraphRing"
{
    // [텔레그래프P1] 적 공격 텔레그래프 프리미엄 링 — 절차 셰이더(텍스처 불요).
    //  ①외곽 경계 링(날카로운 밴드+밝은 에지 코어 — 64px 블러 텍스처 폐기)
    //  ②방사형 채움 _Fill01(예고 진행 0→1 — 중심→외곽 위험영역 확장, 타이밍 가독성)
    //  ③채움 선행 에지 하이라이트(밝은 호 — 진행 방향 시인성)
    //  ④에지 발광 펄스 _EdgePulse(블룸 체인 흡수 유도 — HDR 계열 가산)
    // [무번호 16-B2] 셰이더 효과 확장:
    //  ⑤부채꼴 채움 _Wedge01(공격 방향을 중심으로 각도 방향 채움 — 플레이어가 "언제 맞는지"를 읽게)
    //  ⑥내부 스캔라인 흐름 _Scan(채움 영역 내부에 흐르는 선 — 위험 영역 가동감)
    //  ⑦시작 확산→수축 펄스 이징 _StartPulse(스폰 시점 링 확산→수축 — 이징)
    //  ⑧임팩트 순간 플래시 링 _Flash(임팩트 시점 밝은 링)
    // 폴백 계약: 셰이더 부재 시 AttackTelegraphIndicator가 기존 Unlit 경로로 폴백(시각 저하, 로직 무영향).
    Properties
    {
        _BaseColor ("Ring Color", Color) = (1, 0.3, 0.16, 0.45)
        _Fill01 ("Fill Progress", Range(0, 1)) = 0
        _EdgePulse ("Edge Pulse", Range(0, 1)) = 0.5
        _Wedge01 ("Wedge Progress", Range(0, 1)) = 0
        _AttackAngle ("Attack Angle", Range(0, 6.2832)) = 0
        _Scan ("Scan Flow", Range(0, 1)) = 0
        _StartPulse ("Start Pulse", Range(0, 1)) = 0
        _Flash ("Impact Flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "TelegraphRingUnlit"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Fill01;
                float _EdgePulse;
                float _Wedge01;
                float _AttackAngle;
                float _Scan;
                float _StartPulse;
                float _Flash;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;   // 중심 원점 -1..1
                float r = length(p);
                if (r > 1.0) discard;

                // [무번호 20-H2] 각도 좌표 — 셔머/램프 공용(UV 공간 각도)
                float angle = atan2(p.y, p.x);
                // ① 외곽 경계 링 — 절차 AA 날카로운 밴드 [H2 글로우 타이트화: 외곽 소프트 팔백 0.965~0.985 → 0.975~0.990]
                float band = smoothstep(0.885, 0.915, r) * (1.0 - smoothstep(0.975, 0.990, r));
                // 에지 코어 — 경계 중심의 밝은 라인(발광 소스)
                float coreBase = smoothstep(0.925, 0.942, r) * (1.0 - smoothstep(0.950, 0.967, r));
                // [무번호 20-H2] 에지 에너지 셔머 — 각도 해시 노이즈로 코어 라인 미세 요동(에너지 파동감, 결정론 해시)
                float angCell = floor(frac(angle / 6.2831853 + 0.5) * 48.0);
                float shimmer = frac(sin(angCell * 127.1) * 43758.5453);
                float core = coreBase * (0.78 + 0.22 * shimmer);
                float edgePulse = 0.7 + 0.3 * _EdgePulse;

                // ② 방사형 채움 — 예고 진행에 따라 중심→외곽으로 확장(0.90 반경 한정)
                float fillEdge = _Fill01 * 0.90;
                // [무번호 20-H2] 채움 방사 그라디언트 — 중심 진하고 가장자리 옅은 레이어드(평면 0.30 폐기)
                float fillMask = 1.0 - smoothstep(fillEdge - 0.02, fillEdge + 0.02, r);
                float fillGrad = lerp(0.42, 0.16, saturate(r / 0.90));
                // [무번호 20-H2] 위험 임박 램프 — 채움 80% 초과 시 긴장 상승(호흡 펄스 동기+휘도 부스트)
                float dangerRamp = smoothstep(0.80, 1.0, _Fill01);
                float fill = fillMask * fillGrad * (0.85 + 0.30 * _EdgePulse) * (1.0 + 0.55 * dangerRamp);
                // ③ 채움 선행 에지 — 진행 선두의 밝은 호 [H2: 임박 시 휘도 부스트]
                float sweep = smoothstep(fillEdge - 0.035, fillEdge - 0.005, r)
                            * (1.0 - smoothstep(fillEdge + 0.005, fillEdge + 0.035, r))
                            * (1.0 + 0.60 * dangerRamp);

                // [무번호 16-B2] ⑤부채꼴 채움 — 공격 방향을 중심으로 각도 방향 채움(0→90°) — angle은 상단에서 계산
                float da = angle - _AttackAngle;
                // 각도 정규화 — -π..π
                da = da - 6.2831853 * floor(da / 6.2831853 + 0.5);
                float daAbs = abs(da);
                float wedgeEdge = _Wedge01 * 1.5707963;   // 0→90°(π/2)
                float wedge = step(0.0, wedgeEdge) * (1.0 - smoothstep(wedgeEdge - 0.10, wedgeEdge + 0.10, daAbs));
                // 부채꼴 게이지 — 진행 방향 시인성
                float wedgeGlow = smoothstep(wedgeEdge - 0.12, wedgeEdge - 0.02, daAbs)
                                * (1.0 - smoothstep(wedgeEdge + 0.02, wedgeEdge + 0.12, daAbs));

                // [무번호 22-S4] 에지 순회 에너지 — 링 밴드 위를 도는 밝은 스팟(각도=_Scan×2π, 좁은 각 가우시안)
                float orbit = angle - _Scan * 6.2831853;
                orbit = orbit - 6.2831853 * floor(orbit / 6.2831853 + 0.5);   // -π..π 정규화
                float orbitSpot = exp(-orbit * orbit * 18.0) * band;
                // [무번호 16-B2] ⑥내부 스캔라인 흐름 — 채움 영역 내부에 흐르는 선
                float scan = step(0.88, sin(r * 22.0 - _Scan * 6.2831853) * 0.5 + 0.5) * 0.18;
                // 채움 영역 내부에서만 스캔라인
                float scanFill = (1.0 - smoothstep(fillEdge - 0.02, fillEdge + 0.02, r)) * scan;

                // [무번호 16-B2] ⑦시작 확산→수축 펄스 이징 — 스폰 시점 링 확산→수축
                float startPulse = 1.0 - smoothstep(0.0, 0.9, _StartPulse);
                float startRing = smoothstep(0.85 - 0.12 * startPulse, 0.92 - 0.12 * startPulse, r)
                                * (1.0 - smoothstep(0.95 - 0.12 * startPulse, 0.98 - 0.12 * startPulse, r));

                // [무번호 16-B2] ⑧임팩트 순간 플래시 링 — 밝은 링
                float flash = smoothstep(0.85, 0.95, r) * (1.0 - smoothstep(0.95, 0.99, r)) * _Flash;

                half4 col = _BaseColor;
                col.a *= (band + fill + wedge * 0.35);
                // 부채꼴 게이지 — 진행 방향 시인성
                col.a = saturate(col.a + wedgeGlow * 0.40);
                // 에지 코어/선행 호 — 밝기 가산(블룸 흡수 유도)
                col.rgb = saturate(col.rgb + core * edgePulse * 0.85 + sweep * 0.65);
                col.a = saturate(col.a + core * 0.55 + sweep * 0.35);
                // 스캔라인 흐름 — 채움 내부 밝기 가산
                col.rgb = saturate(col.rgb + scanFill * 0.30);
                col.a = saturate(col.a + scanFill * 0.25);
                // 시작 확산→수축 펄스 — 스폰 시점 링
                col.rgb = saturate(col.rgb + startRing * startPulse * 0.55);
                col.a = saturate(col.a + startRing * startPulse * 0.40);
                // [무번호 22-S4] 에지 순회 에너지 가산 — 밴드 위를 도는 밝은 에너지점
                col.rgb = saturate(col.rgb + orbitSpot * 0.85);
                col.a = saturate(col.a + orbitSpot * 0.50);
                // 임팩트 순간 플래시 링
                col.rgb = saturate(col.rgb + flash * 0.9);
                col.a = saturate(col.a + flash * 0.7);
                // [무번호 20-H2] 채움 컬러 램프 — 위험 임박일수록 채움 영역이 밝은 위험색으로 상승(젤다식 긴장 연출)
                col.rgb = lerp(col.rgb, saturate(col.rgb * 1.6 + 0.18), fillMask * dangerRamp * 0.60);
                // [무번호 23-U1] 깊이 페이드 — 지형/장애물과 만나는 지점에서 소프트 소멸(AAA 소프트 교차).
                //   경계 근거리 0.12m 이내에서 알파가 0으로 수렴. 깊이 텍스처 부재 환경은 fail-open(무페이드 — 안전).
                float2 suv = IN.positionHCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float depthFade = saturate((sceneEye - IN.positionHCS.w) / 0.12f);
                col.a *= depthFade;
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
