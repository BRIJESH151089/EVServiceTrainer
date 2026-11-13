// Soft contact shadow under a held part: a dark radial blob drawn in the opaque queue with alpha-to-coverage
// (smooth edges from 4x MSAA, no transparent overdraw). Strength comes from _BaseColor.a (set per renderer).
Shader "EVTrainer/BlobShadow"
{
    Properties
    {
        _BaseColor ("Colour (alpha = strength)", Color) = (0, 0, 0, 0.5)
        _Softness ("Edge softness", Range(0.05, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }
            AlphaToMask On
            ZWrite Off
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half r = length(input.uv * 2.0h - 1.0h);                 // 0 centre .. 1 edge
                half falloff = saturate((1.0h - r) / _Softness);
                return half4(_BaseColor.rgb, falloff * falloff * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
