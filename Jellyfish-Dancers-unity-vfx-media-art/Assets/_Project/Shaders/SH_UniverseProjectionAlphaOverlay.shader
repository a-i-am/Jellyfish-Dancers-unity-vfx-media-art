Shader "TheLivingFrame/UniverseProjectionAlphaOverlay"
{
    Properties
    {
        _MainTex ("Universe Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.35, 0.85, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 8)) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 0.35
        _Cutoff ("Black Cutoff", Range(0, 1)) = 0.02
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
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Intensity;
                half _Alpha;
                half _Cutoff;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sampleColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = max(sampleColor.r, max(sampleColor.g, sampleColor.b));
                half mask = saturate((luminance - _Cutoff) / max(0.0001h, 1.0h - _Cutoff));
                half edgeFadeX = smoothstep(0.0h, 0.12h, input.uv.x) * smoothstep(1.0h, 0.88h, input.uv.x);
                half edgeFadeY = smoothstep(0.0h, 0.12h, input.uv.y) * smoothstep(1.0h, 0.88h, input.uv.y);
                half edgeFade = edgeFadeX * edgeFadeY;
                half3 color = saturate(sampleColor.rgb * _Tint.rgb * _Intensity) * edgeFade;
                return half4(color, mask * _Alpha * sampleColor.a * edgeFade);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
