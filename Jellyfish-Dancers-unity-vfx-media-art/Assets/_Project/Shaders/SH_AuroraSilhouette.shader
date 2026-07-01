Shader "TheLivingFrame/AuroraSilhouette"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0, 1, 1, 1)
        _Color2 ("Color 2", Color) = (1, 0, 1, 1)
        _NoiseScale ("Noise Scale", Float) = 2.0
        _Speed ("Scroll Speed", Float) = 0.5
        _Intensity ("Intensity", Float) = 1.5
        _Alpha ("Alpha", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Color2;
                float _NoiseScale;
                float _Speed;
                float _Intensity;
                float _Alpha;
            CBUFFER_END

            float hash(float2 p) { return frac(1e4 * sin(17.0 * p.x + p.y * 0.1) * (0.1 + abs(sin(p.y * 13.0 + p.x)))); }

            float noise(float2 x) {
                float2 i = floor(x);
                float2 f = frac(x);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {

                float2 uv = input.positionWS.xy * _NoiseScale + float2(_Time.y * _Speed * 0.2, _Time.y * _Speed * 0.8);


                float n1 = noise(uv);
                float n2 = noise(uv + float2(n1, n1) * 2.0 - _Time.y * _Speed);


                float pattern = noise(uv + n2 * 3.0);


                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = 1.0 - saturate(dot(viewDir, normalize(input.normalWS)));
                fresnel = pow(fresnel, 2.0);


                half3 finalColor = lerp(_BaseColor.rgb, _Color2.rgb, pattern);


                float alpha = _Alpha * (0.3 + pattern * 0.7) * (0.4 + fresnel * 0.6);

                return half4(finalColor * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
