Shader "TimeWarp/Force Field"
{
    Properties
    {
        [Header(Main)]
        [HDR] _Color ("Main Color", Color) = (0.1, 0.6, 1.0, 1.0)
        [HDR] _RewindColor ("Rewind Color", Color) = (0.9, 0.3, 1.0, 1.0)

        [Header(Edges)]
        [Toggle(_USE_DEPTH_INTERSECTION)] _UseDepthIntersection ("Intersection Glow (needs Depth Texture)", Float) = 1
        _FresnelPower ("Fresnel Power", Range(0.1, 10.0)) = 2.5
        _IntersectionDistance ("Intersection Distance", Range(0.01, 5.0)) = 0.5
        [HDR] _IntersectionColor ("Intersection Color", Color) = (0.6, 0.9, 1.0, 1.0)

        [Header(Surface Pattern)]
        _PatternTex ("Pattern (RGB)", 2D) = "white" {}
        _PatternScale ("Pattern Scale", Range(0.1, 20.0)) = 4.0
        _PatternStrength ("Pattern Strength", Range(0.0, 2.0)) = 0.35
        _ScrollSpeed ("Scroll Speed", Vector) = (0.05, 0.12, 0, 0)

        [Header(Impacts)]
        [HDR] _RippleColor ("Ripple Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _RippleSpeed ("Ripple Speed (units per sec)", Range(0.1, 30.0)) = 6.0
        _RippleWidth ("Ripple Width", Range(0.01, 3.0)) = 0.35
        _RippleLifetime ("Ripple Lifetime (sec)", Range(0.1, 10.0)) = 1.2
        _RippleStrength ("Ripple Strength", Range(0.0, 8.0)) = 2.5
        _ImpactFlash ("Impact Flash Radius", Range(0.0, 5.0)) = 0.8

        [Header(Proximity)]
        [HDR] _ProximityColor ("Proximity Color", Color) = (1.0, 0.5, 0.2, 1.0)
        _ProximityRadius ("Proximity Radius", Range(0.0, 20.0)) = 3.0
        _ProximityStrength ("Proximity Strength", Range(0.0, 5.0)) = 1.5
        _ProximityPulseSpeed ("Proximity Pulse Speed", Range(0.0, 20.0)) = 4.0

        [Header(Distortion)]
        _WobbleAmount ("Surface Wobble", Range(0.0, 0.5)) = 0.03
        _WobbleSpeed ("Wobble Speed", Range(0.0, 10.0)) = 1.5

        [Header(Driven by TimeWarpForceField script)]
        _LocalTime ("Local Time", Float) = 0
        _RewindBlend ("Rewind Blend", Range(0, 1)) = 0
        _Overload ("Overload", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForceField"

            // Additive blending - the field ADDS light to the background, which is what
            // makes it read as energy rather than as tinted glass.
            Blend SrcAlpha One
            // No depth write - standard for transparency, otherwise the field would
            // occlude everything behind it.
            ZWrite Off
            // Two sided. This one matters: without it the field vanishes the moment the
            // player steps inside the bubble, which is exactly when you want to see it.
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // GLES3 / WebGL 2 baseline. Everything in this shader stays inside it - no
            // geometry stage, no compute buffers, no dynamic branching over textures.
            #pragma target 3.0

            // The intersection glow is the ONLY part that needs the camera depth texture,
            // which is also the only part that can quietly break on a platform or URP asset
            // where the depth texture is unavailable. Made optional so the field still looks
            // right without it instead of rendering garbage.
            #pragma shader_feature_local_fragment _USE_DEPTH_INTERSECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_USE_DEPTH_INTERSECTION)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            // How many simultaneous impacts the field can show. Four is plenty in practice
            // and keeps the per-pixel loop cheap. The C# side uses the same number.
            #define TW_MAX_HITS 4

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                // Needed for impacts - ripples are measured in world space, so a hit
                // lands in the same physical spot no matter how the mesh is scaled.
                float3 positionWS : TEXCOORD3;
                float2 uv         : TEXCOORD4;
            };

            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _RewindColor;
                half4  _IntersectionColor;
                half4  _RippleColor;

                float  _FresnelPower;
                float  _IntersectionDistance;

                float4 _PatternTex_ST;
                float  _PatternScale;
                float  _PatternStrength;
                float4 _ScrollSpeed;

                float  _RippleSpeed;
                float  _RippleWidth;
                float  _RippleLifetime;
                float  _RippleStrength;
                float  _ImpactFlash;

                half4  _ProximityColor;
                float  _ProximityRadius;
                float  _ProximityStrength;
                float  _ProximityPulseSpeed;

                float  _WobbleAmount;
                float  _WobbleSpeed;

                float  _LocalTime;
                float  _RewindBlend;
                float  _Overload;
            CBUFFER_END

            // Impact data, written by TimeWarpForceField through a MaterialPropertyBlock.
            // xyz = world position of the hit, w = the local time it happened at.
            // Deliberately outside the CBUFFER: it is per-instance data, not a material
            // setting, and MaterialPropertyBlock already opts this renderer out of the
            // SRP Batcher anyway.
            float4 _Hits[TW_MAX_HITS];

            // Objects currently touching or inside the field, written every frame.
            // xyz = world position, w = 0 when the slot is unused, 1 when occupied.
            // This is what makes the field react CONTINUOUSLY to a player standing in it,
            // as opposed to _Hits which is a one-off event at the moment of contact.
            float4 _Intruders[TW_MAX_HITS];

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Surface wobble. Driven by _LocalTime, so it slows, freezes and reverses
                // with the engine exactly like everything else on this shader.
                float3 positionOS = IN.positionOS.xyz;

                if (_WobbleAmount > 0.0001)
                {
                    float phase = _LocalTime * _WobbleSpeed;
                    float wave  = sin(positionOS.y * 6.0 + phase) * cos(positionOS.x * 5.0 - phase * 0.7);

                    // Overload swells the whole surface outward - the script raises it when
                    // the field is taking a lot of punishment at once.
                    float swell = 1.0 + _Overload * 0.15;
                    positionOS += IN.normalOS * (wave * _WobbleAmount + _Overload * 0.05);
                    positionOS *= swell;
                }

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                OUT.positionCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;

                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);
                OUT.normalWS = normalInput.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                OUT.screenPos = ComputeScreenPos(vertexInput.positionCS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _PatternTex);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ==========================================
                // 1. EDGE GLOW (Fresnel)
                // ==========================================
                float3 normal  = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                // abs(dot(...)) so the effect works on inner faces too - without it the
                // rim would disappear once the camera is inside the field.
                float fresnelTerm = pow(1.0 - saturate(abs(dot(normal, viewDir))), _FresnelPower);

                // ==========================================
                // 2. INTERSECTION GLOW (Depth Fade)
                // ==========================================
                // Where the field surface passes close to solid geometry, light it up.
                // This is what makes a bubble look like it is really cutting the floor
                // instead of floating in front of it.
                float intersection = 0.0;

                #if defined(_USE_DEPTH_INTERSECTION)
                    float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                    float sceneDepth       = SampleSceneDepth(screenUV);
                    float linearSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                    float surfaceDepth     = IN.screenPos.w;

                    float depthDifference = linearSceneDepth - surfaceDepth;
                    intersection = 1.0 - saturate(depthDifference / _IntersectionDistance);
                #endif

                // ==========================================
                // 3. SURFACE PATTERN
                // ==========================================
                // Scrolled with _LocalTime, NOT with Unity's _Time. That single choice is
                // what makes the field obey the engine: in slow motion the pattern crawls,
                // during a rewind it scrolls backwards, and in a time stop it freezes -
                // all for free, because _LocalTime is fed by the TimeWarp script.
                float2 patternUV = IN.uv * _PatternScale + _ScrollSpeed.xy * _LocalTime;
                half3  pattern   = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, patternUV).rgb;

                float patternTerm = pattern.r * _PatternStrength;

                // ==========================================
                // 4. IMPACTS (Ripples)
                // ==========================================
                // Every hit sends an expanding ring out from the point of contact, plus a
                // short flash right where it landed.
                float ripple = 0.0;

                [unroll]
                for (int i = 0; i < TW_MAX_HITS; i++)
                {
                    float3 hitPos  = _Hits[i].xyz;
                    float  hitTime = _Hits[i].w;

                    float age = _LocalTime - hitTime;

                    // age < 0 means the impact has not happened yet from this shader's
                    // point of view. During a rewind _LocalTime runs backwards, so hits
                    // literally un-happen in reverse order. No extra code needed for that.
                    if (age < 0.0 || age > _RippleLifetime) continue;

                    float dist   = distance(IN.positionWS, hitPos);
                    float radius = age * _RippleSpeed;

                    // The ring itself - a soft band at the expanding radius.
                    float ring = 1.0 - saturate(abs(dist - radius) / _RippleWidth);
                    ring = ring * ring; // squared for a tighter, brighter edge

                    // Fades out over the ripple's lifetime.
                    float fade = 1.0 - saturate(age / _RippleLifetime);

                    // The flash - a bright spot at the contact point that dies fast, so
                    // the hit reads as an impact and not just a ring appearing.
                    float flash = (1.0 - saturate(dist / max(_ImpactFlash, 0.0001))) * pow(fade, 4.0);

                    ripple += (ring * fade + flash) * _RippleStrength;
                }

                // ==========================================
                // 5. PROXIMITY GLOW
                // ==========================================
                // A soft, pulsing halo that follows anything standing in or near the field.
                // Unlike a ripple this does not fade on its own - it lives as long as the
                // object is there, so the field visibly "knows" the player is inside it.
                float proximity = 0.0;

                [unroll]
                for (int j = 0; j < TW_MAX_HITS; j++)
                {
                    // w acts as the occupied flag, so empty slots cost one compare.
                    if (_Intruders[j].w < 0.5) continue;

                    float d = distance(IN.positionWS, _Intruders[j].xyz);
                    float falloff = 1.0 - saturate(d / max(_ProximityRadius, 0.0001));

                    // Squared falloff keeps the glow tight around the intruder instead of
                    // washing over the whole bubble.
                    falloff *= falloff;

                    // Gentle pulse so it reads as "alive" rather than as a static stain.
                    float pulse = 0.75 + 0.25 * sin(_LocalTime * _ProximityPulseSpeed + d * 2.0);

                    proximity += falloff * pulse;
                }

                proximity = saturate(proximity) * _ProximityStrength;

                // ==========================================
                // 6. FINAL MIX
                // ==========================================
                // Rewind shifts the whole field towards a different hue, driven by the
                // script, so the player can see at a glance that time is running backwards.
                half4 baseColor = lerp(_Color, _RewindColor, saturate(_RewindBlend));

                float energy = saturate(fresnelTerm + patternTerm);

                half3 rgb = baseColor.rgb * energy;
                rgb += _IntersectionColor.rgb * intersection;
                rgb += _RippleColor.rgb * ripple;
                rgb += _ProximityColor.rgb * proximity;

                // Overload brightens everything - a field about to fail should look like it.
                rgb *= 1.0 + _Overload * 1.5;

                // Alpha drives the additive blend weight, so Main Color's alpha still
                // works as a master opacity slider.
                float alpha = saturate(energy + intersection + ripple + proximity) * baseColor.a;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
