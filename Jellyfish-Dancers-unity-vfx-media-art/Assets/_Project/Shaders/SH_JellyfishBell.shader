Shader "TheLivingFrame/JellyfishBell"
{
    Properties
    {
        _BaseColor ("Center Color", Color) = (0.35, 0.95, 1.0, 0.08)
        _RimColor ("Rim Color", Color) = (0.85, 0.45, 1.0, 0.62)
        _EmissionIntensity ("Emission Intensity", Range(0, 12)) = 1.8
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0.012
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.42
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 2.2
        _PulseFrequency ("Pulse Frequency", Float) = 1.15
        _PulseAmplitude ("Pulse Amplitude", Float) = 0.045
        _WaveSpeed ("Vertical Wave Speed", Float) = 7.0
        _ImpulsePhase ("Impulse Phase", Range(0, 1)) = 0.0
        _GonadBaseRadius ("Gonad Base Radius", Float) = 0.145
        _GonadPetalDepth ("Gonad Petal Depth", Float) = 0.04
        _GonadThickness ("Gonad Thickness", Float) = 0.028
        _GonadFeather ("Gonad Feather", Float) = 0.01
        _GonadEmissionColor ("Gonad Emission Color", Color) = (1.0, 0.35, 0.95, 1.0)
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _EmissionIntensity;
                half _CenterAlpha;
                half _RimAlpha;
                half _FresnelPower;
                float _PulseFrequency;
                float _PulseAmplitude;
                float _WaveSpeed;
                float _ImpulsePhase;
                float _GonadBaseRadius;
                float _GonadPetalDepth;
                float _GonadThickness;
                float _GonadFeather;
                half4 _GonadEmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float domeDepth : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float apexDistance = saturate(1.0 - input.positionOS.y);
                float rimWeight = smoothstep(0.08, 1.0, apexDistance);
                rimWeight *= rimWeight;

                float wave = sin(_Time.y * _PulseFrequency - input.positionOS.y * _WaveSpeed);
                float3 displaced = input.positionOS.xyz + input.normalOS * wave * _PulseAmplitude * rimWeight;

                float2 radial = displaced.xz;
                float radialLength = max(length(radial), 0.0001);
                float2 radialDir = radial / radialLength;
                float flare = lerp(0.18, -0.24, _ImpulsePhase);
                float downFold = _ImpulsePhase * 0.18;
                displaced.xz += radialDir * flare * rimWeight;
                displaced.y -= downFold * rimWeight;

                output.positionWS = TransformObjectToWorld(displaced);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.domeDepth = apexDistance;
                output.positionOS = displaced;
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = 1.0 - saturate(dot(viewDir, normalize(input.normalWS)));
                fresnel = pow(fresnel, _FresnelPower);

                float rimMask = saturate(max(fresnel, input.domeDepth * 0.75));
                half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, rimMask);
                half alpha = lerp(_CenterAlpha, _RimAlpha, rimMask) * saturate(_BaseColor.a * 0.55 + fresnel);
                alpha *= isFrontFace ? 1.0h : 0.5h;

                half glow = _EmissionIntensity * (0.16 + fresnel * 1.05 + input.domeDepth * 0.22);

                float2 localXZ = input.positionOS.xz;
                float apexMask = 1.0 - smoothstep(0.06, 0.62, input.domeDepth);

                float angle = atan2(localXZ.y, localXZ.x);
                float r = length(localXZ);
                float cloverRadius = _GonadBaseRadius + _GonadPetalDepth * cos(4.0 * angle);
                float outlineSDF = abs(r - cloverRadius) - _GonadThickness;
                float gonadAlpha = smoothstep(_GonadFeather, 0.0, outlineSDF);

                float gonads = saturate(gonadAlpha) * apexMask;

                half3 emission = color * glow + _GonadEmissionColor.rgb * gonads * 4.5h;
                alpha = saturate(alpha + gonads * _GonadEmissionColor.a * 0.48h);
                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
