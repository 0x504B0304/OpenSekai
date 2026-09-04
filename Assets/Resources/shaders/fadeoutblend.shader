Shader "Hidden/AfterPostProcess/FadeOutBlend"
{
    Properties
    {
        _FadeOutParams ("FadeOut Params", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FadeOutBlend"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitScaleBias;
            float4 _FadeOutParams;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half3 shifted = color + _FadeOutParams.xxx;
                half amount = _FadeOutParams.y;
                half3 result = amount >= 0.0h
                    ? shifted + (1.0h - shifted) * amount
                    : shifted * (1.0h + amount);
                return half4(result, 1.0h);
            }
            ENDHLSL
        }
    }
}
