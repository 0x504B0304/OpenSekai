Shader "Hidden/Sekai/Scenario/Post"
{
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

        HLSLINCLUDE
        #pragma target 3.5
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

        struct BlurVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord0 : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2;
            float2 texcoord3 : TEXCOORD3;
            float2 texcoord4 : TEXCOORD4;
        };

        TEXTURE2D_X(_BlitTexture);
        float4 _BlitScaleBias;
        float4 _BlitTexture_TexelSize;
        float _BlurSize;
        half _Influence;
        half3 _Monochrome;
        half3 _ToneRatio;

        float2 GetBlitTexcoord(uint vertexID)
        {
            return GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord = GetBlitTexcoord(input.vertexID);
            return output;
        }

        BlurVaryings VertBlurVertical(Attributes input)
        {
            BlurVaryings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord0 = GetBlitTexcoord(input.vertexID);
            float2 stepUv = float2(0.0, _BlitTexture_TexelSize.y * _BlurSize);
            output.texcoord1 = output.texcoord0 + stepUv;
            output.texcoord2 = output.texcoord0 - stepUv;
            output.texcoord3 = output.texcoord0 + stepUv * 2.0;
            output.texcoord4 = output.texcoord0 - stepUv * 2.0;
            return output;
        }

        BlurVaryings VertBlurHorizontal(Attributes input)
        {
            BlurVaryings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord0 = GetBlitTexcoord(input.vertexID);
            float2 stepUv = float2(_BlitTexture_TexelSize.x * _BlurSize, 0.0);
            output.texcoord1 = output.texcoord0 + stepUv;
            output.texcoord2 = output.texcoord0 - stepUv;
            output.texcoord3 = output.texcoord0 + stepUv * 2.0;
            output.texcoord4 = output.texcoord0 - stepUv * 2.0;
            return output;
        }

        half4 FragTone(Varyings input) : SV_Target
        {
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            half luminance = dot(source.rgb, _Monochrome) * source.a;
            half3 toned = luminance * _ToneRatio;
            return half4(lerp(source.rgb, toned, _Influence), 1.0h);
        }

        half4 FragBlur(BlurVaryings input) : SV_Target
        {
            half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord0).rgb * 0.40259999h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord1).rgb * 0.244200006h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord2).rgb * 0.244200006h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord3).rgb * 0.0544999987h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord4).rgb * 0.0544999987h;
            return half4(color, 1.0h);
        }
        ENDHLSL

        Pass
        {
            Name "Tone"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTone
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex VertBlurVertical
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex VertBlurHorizontal
            #pragma fragment FragBlur
            ENDHLSL
        }
    }
}
