Shader "Custom/MagicMirror"
{
    Properties
    {
        [MainTexture] _WebcamTex ("Webcam Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "black" {}
        _EffectTex ("Effect Texture (Background)", 2D) = "white" {}
        _Threshold ("Mask Threshold", Range(0.01, 1.0)) = 0.95
        _PersonScale ("Person Scale", Range(0.1, 3.0)) = 0.6
        _PersonOffset ("Person Offset (X,Y)", Vector) = (0, -0.2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _WebcamTex;
            sampler2D _MaskTex;
            sampler2D _EffectTex;
            float _Threshold;
            float _PersonScale;
            float4 _PersonOffset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;


                float2 centeredUV = uv - 0.5;
                float2 personUV = (centeredUV / _PersonScale) + 0.5 - _PersonOffset.xy;

                fixed4 webcam = tex2D(_WebcamTex, personUV);
                fixed mask = tex2D(_MaskTex, personUV).r;


                if (personUV.x < 0 || personUV.x > 1 || personUV.y < 0 || personUV.y > 1) {
                    mask = 0;
                }

                fixed4 effect = tex2D(_EffectTex, uv);




                fixed4 col = lerp(effect, webcam, saturate(mask / _Threshold));

                return col;
            }
            ENDCG
        }
    }
}
