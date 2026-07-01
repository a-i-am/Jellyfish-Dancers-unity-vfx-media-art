Shader "Custom/VerticalGradient"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0, 0, 0, 1)
        _TopColor ("Top Color", Color) = (0.5, 0.5, 0.5, 1)
        _MinY ("Min Y (Local Height)", Float) = 0.0
        _MaxY ("Max Y (Local Height)", Float) = 2.0
    }
    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float localY        : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BottomColor;
                half4 _TopColor;
                float _MinY;
                float _MaxY;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);

                output.localY = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {

                float t = saturate((input.localY - _MinY) / (_MaxY - _MinY));


                half4 finalColor = lerp(_BottomColor, _TopColor, t);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
