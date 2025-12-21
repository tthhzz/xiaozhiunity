Shader "XiaoZhi/PostProcessing/DecalFade"
{
    Properties
    {
        _MaskTex("Texture", 2D) = "white" {}
        _MaskColor("Mask Color", Color) = (1, 1, 1, 1)
        _MaskOffset("Mask Offset", Range(0, 1)) = 0
        _MaskScale("Mask Scale", Range(0, 1)) = 1
        _MaskFade("Mask Fade", Range(0, 1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BlitTexture);
    SAMPLER(sampler_BlitTexture);
    float4 _BlitTexture_TexelSize;

    TEXTURE2D(_MaskTex);
    SAMPLER(sampler_MaskTex);
    float4 _MaskTex_TexelSize;

    half4 _MaskColor;
    half _MaskOffset;
    half _MaskScale;
    half _MaskFade;

    static const float MAX_SCALE = 16;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 params : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
        output.params = float3(length(_BlitTexture_TexelSize.zw * 0.5), length(_MaskTex_TexelSize.xy),
                               rcp(min(_BlitTexture_TexelSize.z, _BlitTexture_TexelSize.w) * 0.5 * _MaskFade));
        return output;
    }

    half4 Frag(Varyings input) : SV_Target
    {
        half4 bg = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv);
        half2 p2c = (input.uv - 0.5) * _BlitTexture_TexelSize.zw;
        half2 uv_p2c = (p2c + 0.5 * _MaskTex_TexelSize.zw) * _MaskTex_TexelSize.xy;
        half2 c2c = floor(uv_p2c) * _MaskTex_TexelSize.zw;
        half mr = _MaskOffset * input.params.x;
        half scale = (length(c2c) - mr) * input.params.y;
        scale = clamp(scale, _MaskScale, MAX_SCALE);
        half2 uv = frac(uv_p2c);
        uv = (uv - 0.5) * scale + 0.5;
        half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).a;
        half fade = (length(p2c) - mr) * input.params.z;
        half alpha = fade > 0 ? step(0.1, mask) * saturate(1 - fade) : 1;
        return bg * (1 - alpha) + _MaskColor * alpha;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "DecalFade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}