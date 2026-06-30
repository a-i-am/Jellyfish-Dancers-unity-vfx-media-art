Shader "Shader Graphs/NeuroPattern"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.5)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission Color", Color) = (0, 3, 3, 1)
        _PatternType ("Pattern Type", Float) = 0
        _PatternSpeed ("Pattern Speed", Vector) = (0.5, 0.5, 0, 0)
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _PatternType)
                UNITY_DEFINE_INSTANCED_PROP(float4, _PatternSpeed)
            UNITY_INSTANCING_BUFFER_END(Props)

            float2 voronoi_hash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float voronoi(float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float F1 = 8.0;
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(float(i), float(j));
                        float2 o = voronoi_hash(n + g);
                        float2 r = g - f + o;
                        float d = dot(r, r);
                        if (d < F1)
                        {
                            F1 = d;
                        }
                    }
                }
                return sqrt(F1);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 rimCol = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                float4 emissionCol = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor);
                float patType = UNITY_ACCESS_INSTANCED_PROP(Props, _PatternType);
                float4 patSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _PatternSpeed);

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = 1.0 - saturate(dot(viewDir, normalize(input.normalWS)));
                fresnel = pow(fresnel, 2.2);

                float2 scrolledUV = input.uv + patSpeed.xy * _Time.y;
                float patternVal = 0.0;

                if (patType < 0.5)
                {
                    patternVal = smoothstep(0.05, 0.15, abs(voronoi(scrolledUV * 8.0) - 0.5));
                }
                else if (patType < 1.5)
                {
                    float2 dotsUV = frac(scrolledUV * 12.0) - 0.5;
                    patternVal = smoothstep(0.2, 0.15, length(dotsUV));
                }
                else
                {
                    patternVal = smoothstep(0.4, 0.45, frac((scrolledUV.x + scrolledUV.y) * 8.0));
                }

                half3 finalColor = baseCol.rgb + rimCol.rgb * fresnel + emissionCol.rgb * patternVal;
                float alpha = saturate(baseCol.a + fresnel * 0.5 + patternVal * 0.3);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
