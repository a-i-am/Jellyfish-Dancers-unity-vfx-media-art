Shader "Custom/SH_AuroraFloor"
{
    Properties
    {
        [HDR] _Color1 ("Color 1", Color) = (0.0, 1.0, 0.5, 1.0)
        [HDR] _Color2 ("Color 2", Color) = (0.0, 0.5, 1.0, 1.0)
        _Speed ("Speed", Float) = 0.5
        _Density ("Density", Float) = 3.0
        _Intensity ("Intensity", Float) = 1.0
        _EdgeFade ("Edge Fade", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color1;
                float4 _Color2;
                float _Speed;
                float _Density;
                float _Intensity;
                float _EdgeFade;
            CBUFFER_END

            float hash(float2 p) {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * (p.x + p.y));
            }

            float noise(float2 x) {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0.0, 0.0)),
                                 hash(i + float2(1.0, 0.0)), f.x),
                            lerp(hash(i + float2(0.0, 1.0)),
                                 hash(i + float2(1.0, 1.0)), f.x), f.y);
            }

            float fbm(float2 p) {
                float f = 0.0;
                float w = 0.5;
                for (int i = 0; i < 4; i++) {
                    f += w * noise(p);
                    p *= 2.0;
                    w *= 0.5;
                }
                return f;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                float fadeX = smoothstep(0.0, _EdgeFade, uv.x) * smoothstep(1.0, 1.0 - _EdgeFade, uv.x);
                float fadeY = smoothstep(0.0, _EdgeFade, uv.y) * smoothstep(1.0, 1.0 - _EdgeFade, uv.y);
                float edgeFade = fadeX * fadeY;

                float t = _Time.y * _Speed;

                float2 uv1 = uv * _Density + float2(t * 0.2, t * 0.1);
                float2 uv2 = uv * (_Density * 1.5) - float2(t * 0.15, t * 0.2);

                uv1.x += noise(uv2 + t) * 0.5;
                uv2.y += noise(uv1 - t) * 0.5;

                float n1 = fbm(uv1);
                float n2 = fbm(uv2 + float2(n1, n1));

                float ribbon = smoothstep(0.2, 0.8, n2) * smoothstep(0.8, 0.2, n1);
                ribbon *= 2.5;

                half3 color = lerp(_Color1.rgb, _Color2.rgb, n1);
                color *= ribbon * _Intensity * edgeFade;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
