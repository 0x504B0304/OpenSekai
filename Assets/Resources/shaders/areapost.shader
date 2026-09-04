Shader "Area/Post"
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
            float2 vignetteCoord : TEXCOORD1;
        };

        TEXTURE2D_X(_BlitTexture);
        float4 _BlitScaleBias;
        float4 _BlitTexture_TexelSize;
        half4 _FilterParams;
        float _BrightnessIntensity;
        float _DownSamplingDelta;
        half4 _BloomColor;
        float _Intensity;
        float _VignettePower;
        half4 _VignetteColor;
        float _VignetteTop;
        float _VignetteBottom;

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            float2 fullscreenUv = GetFullScreenTriangleTexCoord(input.vertexID);
            output.texcoord = fullscreenUv * _BlitScaleBias.xy + _BlitScaleBias.zw;
            float2 clipPosition = fullscreenUv * 2.0 - 1.0;
            output.vignetteCoord = float2(clipPosition.x * _BlitTexture_TexelSize.y * _BlitTexture_TexelSize.z, clipPosition.y);
            return output;
        }

        half3 SampleBlit(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        half3 SampleFour(float2 uv, float2 offset)
        {
            half3 color = SampleBlit(uv + float2(-offset.x, -offset.y));
            color += SampleBlit(uv + float2(offset.x, -offset.y));
            color += SampleBlit(uv + float2(-offset.x, offset.y));
            color += SampleBlit(uv + float2(offset.x, offset.y));
            return color * 0.25h;
        }

        half4 FragFilter(Varyings input) : SV_Target
        {
            half3 source = SampleBlit(input.texcoord);
            float brightness = pow(max(dot(half3(0.212599993h, 0.715200007h, 0.0722000003h), source), 0.000001), _BrightnessIntensity);
            half2 threshold = half2(brightness, brightness) - _FilterParams.yx;
            half soft = min(max(threshold.x, 0.0h), _FilterParams.z);
            soft = soft * soft * _FilterParams.w;
            half contribution = max(threshold.y, soft);
            return half4(source * contribution, contribution);
        }

        half4 FragBloomDownsample(Varyings input) : SV_Target
        {
            float2 offset = _BlitTexture_TexelSize.xy * _DownSamplingDelta;
            return half4(SampleFour(input.texcoord, offset) * _BloomColor.rgb, _BloomColor.a);
        }

        half4 FragDownsample(Varyings input) : SV_Target
        {
            return half4(SampleFour(input.texcoord, _BlitTexture_TexelSize.xy * 0.5), 1.0h);
        }

        half4 FragIntensity(Varyings input) : SV_Target
        {
            return half4(SampleBlit(input.texcoord) * _Intensity, 1.0h);
        }

        half4 FragDownsampleIntensity(Varyings input) : SV_Target
        {
            half3 color = SampleFour(input.texcoord, _BlitTexture_TexelSize.xy * 0.5);
            return half4(color * _Intensity, 1.0h);
        }

        float GetVignetteFactor(Varyings input)
        {
            float vertical = lerp(_VignetteBottom, _VignetteTop, input.texcoord.y);
            return dot(input.vignetteCoord, input.vignetteCoord) * _VignettePower * vertical;
        }

        half4 FragVignetteMultiply(Varyings input) : SV_Target
        {
            return GetVignetteFactor(input) * _VignetteColor;
        }

        half4 FragVignetteScreen(Varyings input) : SV_Target
        {
            return lerp(half4(1.0h, 1.0h, 1.0h, 1.0h), _VignetteColor, GetVignetteFactor(input));
        }
        ENDHLSL

        Pass
        {
            Name "Filter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFilter
            ENDHLSL
        }

        Pass
        {
            Name "BloomDownsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBloomDownsample
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            ENDHLSL
        }

        Pass
        {
            Name "Intensity"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragIntensity
            ENDHLSL
        }

        Pass
        {
            Name "DownsampleIntensity"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsampleIntensity
            ENDHLSL
        }

        Pass
        {
            Name "VignetteMultiply"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVignetteMultiply
            ENDHLSL
        }

        Pass
        {
            Name "VignetteScreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVignetteScreen
            ENDHLSL
        }
    }
}
