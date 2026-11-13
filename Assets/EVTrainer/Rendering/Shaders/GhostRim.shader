// Ghost outline for target poses: unlit, transparent, a soft fill plus a fresnel rim glow with a slow pulse.
// Mobile-cheap (one pass, no textures, no lighting), single-pass-instanced stereo ready, SRP Batcher compatible.
// The colour comes from _BaseColor (cyan normally; the App tints it green for "release now", red for a reject).
Shader "EVTrainer/GhostRim"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.25, 0.85, 1.0, 0.35)
        _FillAlpha ("Fill opacity", Range(0, 1)) = 0.12
        _RimPower ("Rim sharpness", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim strength", Range(0, 3)) = 1.3
        _PulseSpeed ("Pulse speed (rad/s)", Range(0, 10)) = 2.2
        _PulseAmount ("Pulse amount", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GhostRim"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _FillAlpha;
                half _RimPower;
                half _RimStrength;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower) * _RimStrength;
                half pulse = 1.0h + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                half alpha = saturate((_FillAlpha + rim) * pulse) * saturate(_BaseColor.a * 2.0h);
                half3 colour = _BaseColor.rgb * (0.7h + rim * 0.8h);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
