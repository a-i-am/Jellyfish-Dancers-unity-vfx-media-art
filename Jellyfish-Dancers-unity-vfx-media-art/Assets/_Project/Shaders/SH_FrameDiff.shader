Shader "TheLivingFrame/FrameDiff"
{
    Properties
    {
        _MainTex ("Current Frame", 2D) = "white" {}
        _PrevTex ("Previous Frame", 2D) = "black" {}
        _Sensitivity ("Sensitivity", Range(0, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FrameDiff"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PrevTex);
            SAMPLER(sampler_PrevTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Sensitivity;
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
                half3 currentFrame = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half3 previousFrame = SAMPLE_TEXTURE2D(_PrevTex, sampler_PrevTex, input.uv).rgb;
                half3 diff = abs(currentFrame - previousFrame) * _Sensitivity;
                half motion = saturate(dot(diff, half3(0.3333h, 0.3333h, 0.3333h)));
                return half4(motion, motion, motion, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}