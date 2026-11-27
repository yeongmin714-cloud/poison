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
        // [84차 고품질] 이중 링 — 주 밴드 외곽의 얇은 보조 링(RTS 마커 표준 리딩).
        _SecondRingOffset ("Second Ring Offset", Range(0, 0.5)) = 0.13
        _SecondRingAlpha  ("Second Ring Alpha", Range(0, 1)) = 0.55
        // [84차 고품질] 스폰 확산 웨이브 — 명령 ack 순간 중심→외곽으로 달려나가는 1회성 링. <0 = off.
        _WaveT ("Spawn Wave T", Range(-1, 1)) = -1
        // [무번호 19-C1] 회전 세그먼트 링 — 4~6분할 아크가 회전. ≤2 = 연속 링(기존).
        _Segments ("Spin Segments (4~6; <=2 continuous)", Range(0, 8)) = 0
        // [무번호 19-C1] 중앙 그라디언트 필 — 링 내부를 중심→외곽으로 흐리게 채움(선택 링 가독성).
        _CenterFillAlpha ("Center Gradient Fill Alpha", Range(0, 1)) = 0.12
        // [무번호 19-C1] 선택 순간 확산 펄스 — 스폰 순간 0.15s 이징 확산(SelectionRingController 구동). <0 = off.
        _BurstT ("Selection Burst T", Range(-1, 1)) = -1
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
                half  _SecondRingOffset;
                half  _SecondRingAlpha;
                half  _WaveT;
                half  _Segments;
                half  _CenterFillAlpha;
                half  _BurstT;
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
                // [85차 부드러움] 회전 대시 — 대비 완화(0.80+0.20) + 넓은 페이드 에지(계단감 제거).
                float arcPos = frac(ang * 4.0 * 0.15915494309 + _Time.y * _ArcSpeed);
                float dash = smoothstep(0.00, 0.18, arcPos) * (1.0 - smoothstep(0.45, 0.62, arcPos));
                float highlight = _ArcSpeed > 0.001 ? (0.80 + 0.20 * dash) : 1.0;

                // [입체감] rim 하이라이트 — 화면-상 방향(카메라 전방 XZ → 로컬 XY) 밴드가 밝게 빛난다.
                float2 rimDir = normalize(_RimDirection.xy + float2(1e-5, 0.0));
                float rim = smoothstep(0.30, 0.95, dot(normalize(c + float2(1e-5, 0.0)), rimDir));

                // [무번호 65-P2] 강조색 포화 캡 — _Intensity×highlight×rim 배수가 HDR 영역으로 밀어
                //   프레임버퍼 클램프에서 순백으로 포화되는 것을 차단(테스트 12 영상 실측: 명령 웨이브 링이
                //   #f8f8f8 순백 렌더). 팀색 위계는 유지, 채도만 LDR로 고정.
                half3 accentCol = saturate(_TeamColor.rgb * _Intensity * highlight * (1.0 + _RimIntensity * rim));
                half coreA = saturate((core + glow * 0.40) * pulse * _TeamColor.a);

                // [무번호 19-C1] 회전 세그먼트 링 — 4~6분할 아크가 회전(_ArcSpeed 공유). ≤2 = 연속 링(무변경).
                if (_Segments > 2)
                {
                    float seg = frac((ang / 6.28318530) * _Segments + _Time.y * _ArcSpeed);
                    float segMask = smoothstep(0.00, 0.10, seg) * (1.0 - smoothstep(0.72, 0.92, seg));
                    segMask = lerp(0.30, 1.0, segMask);   // 세그먼트 갭도 완전 소멸 금지(0.30 유지)
                    coreA = saturate(coreA * segMask);
                }

                // [무번호 19-C1] 중앙 그라디언트 필 — 링 내부를 중심→외곽으로 흐리게(선택 링 가독성).
                if (_CenterFillAlpha > 0.001)
                {
                    float inner = saturate(1.0 - d / _RingRadius);
                    coreA = saturate(coreA + inner * inner * _CenterFillAlpha * _TeamColor.a);
                }

                // [84차 고품질] 이중 링 — 주 밴드 외곽 얇은 보조 링(같은 액센트, 낮은 알파).
                if (_SecondRingAlpha > 0.001 && _SecondRingOffset > 0.001)
                {
                    float r2 = min(_RingRadius + _SecondRingOffset, 0.96);
                    float t2 = max(_RingThickness * 0.40, aa * 1.5);
                    float core2 = smoothstep(r2 - t2 - aa, r2 - t2 + aa, d)
                                * (1.0 - smoothstep(r2 + t2 - aa, r2 + t2 + aa, d));
                    coreA = saturate(max(coreA, core2 * _SecondRingAlpha * pulse * _TeamColor.a));
                }

                // [84차 고품질] 스폰 확산 웨이브 — 명령 ack 순간 중심→외곽 1회성 확산 링(액센트색).
                if (_WaveT >= 0.0)
                {
                    float wr = lerp(_RingRadius * 0.30, 0.97, _WaveT);
                    float wth = 0.035 + 0.045 * _WaveT;
                    float wave = smoothstep(wr - wth - aa, wr - wth + aa, d)
                               * (1.0 - smoothstep(wr + wth - aa, wr + wth + aa, d));
                    coreA = saturate(coreA + wave * (1.0 - _WaveT) * 0.55);
                }

                // [무번호 19-C1] 선택 순간 확산 펄스 — 스폰 순간 0.15s 이징 확산(SelectionRingController 구동).
                if (_BurstT >= 0.0)
                {
                    float br = lerp(_RingRadius * 0.42, 0.97, _BurstT);
                    float bth = 0.05 + 0.06 * _BurstT;
                    float burst = smoothstep(br - bth - aa, br - bth + aa, d)
                                * (1.0 - smoothstep(br + bth - aa, br + bth + aa, d));
                    coreA = saturate(coreA + burst * (1.0 - _BurstT) * 0.70);
                }

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
