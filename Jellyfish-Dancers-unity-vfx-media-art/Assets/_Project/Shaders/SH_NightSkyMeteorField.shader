Shader "TheLivingFrame/NightSkyMeteorField"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.015, 0.025, 0.075, 1)
        _BottomColor ("Bottom Color", Color) = (0.01, 0.055, 0.11, 1)
        _StarColor ("Star Color", Color) = (0.75, 0.92, 1.0, 1)
        _MeteorColor ("Meteor Color", Color) = (0.35, 0.95, 1.0, 1)
        _StarIntensity ("Star Intensity", Range(0, 6)) = 2.2
        _MeteorIntensity ("Meteor Intensity", Range(0, 12)) = 5.0
        _MeteorDensity ("Meteor Density", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _StarColor;
                half4 _MeteorColor;
                half _StarIntensity;
                half _MeteorIntensity;
                half _MeteorDensity;
            CBUFFER_END

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float StarLayer(float2 uv, float scale, float threshold)
            {
                float2 grid = uv * scale;
                float2 cell = floor(grid);
                float2 local = frac(grid) - 0.5;
                float sparkle = Hash21(cell);
                float radius = lerp(0.018, 0.055, Hash21(cell + 17.7));
                float star = smoothstep(radius, 0.0, length(local));
                float twinkle = 0.65 + 0.35 * sin(_Time.y * lerp(1.2, 3.5, sparkle) + sparkle * 18.0);
                return star * step(threshold, sparkle) * twinkle;
            }

            float MeteorLayer(float2 uv, float laneOffset, float speed, float width, float meteorLen)
            {
                float t = frac(_Time.y * speed + laneOffset);
                float2 head = float2(1.18 - t * 1.55, 0.92 - laneOffset * 0.72 - t * 0.36);
                head = frac(head);

                float2 direction = normalize(float2(-1.0, -0.34));
                float2 perpendicular = float2(-direction.y, direction.x);
                float2 rel = uv - head;
                rel.x -= round(rel.x);
                rel.y -= round(rel.y);

                float along = dot(rel, direction);
                float across = abs(dot(rel, perpendicular));

                float clampedAlong = clamp(along, 0.0, meteorLen);
                float distToSegment = length(float2(along - clampedAlong, across));
                float localRadius = lerp(width, width * 0.05, clampedAlong / meteorLen);

                float streak = smoothstep(localRadius, 0.0, distToSegment);
                float glow = smoothstep(width * 5.0, 0.0, distToSegment) * smoothstep(meteorLen * 1.8, 0.0, clampedAlong) * 0.24;

                return (streak + glow) * _MeteorDensity;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half3 sky = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(uv.y));
                sky += 0.045h * half3(0.05h, 0.35h, 0.55h) * sin((uv.x * 8.0 + uv.y * 3.5 + _Time.y * 0.16));

                float stars =
                    StarLayer(uv + float2(_Time.y * 0.003, 0.0), 58.0, 0.965) +
                    StarLayer(uv + float2(0.13, _Time.y * 0.002), 96.0, 0.982) +
                    StarLayer(uv + float2(0.37, 0.19), 145.0, 0.991);

                float meteors =
                    MeteorLayer(uv, 0.11, 0.072, 0.0055, 0.18) +
                    MeteorLayer(uv, 0.47, 0.048, 0.0045, 0.14) +
                    MeteorLayer(uv, 0.78, 0.096, 0.0040, 0.12);

                half3 color = sky;
                color += _StarColor.rgb * stars * _StarIntensity;
                color += _MeteorColor.rgb * meteors * _MeteorIntensity;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
