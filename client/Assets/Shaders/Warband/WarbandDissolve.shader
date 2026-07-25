// Noise dissolve with a hot burning edge — the ash-death of combat-spectacle §7.1. The ONLY
// shader here that writes depth: a dissolving corpse is still a solid body until it is gone, so it
// must occlude correctly. _Cutoff is driven 0→1 from the death sequence's Step (never _Time), with
// the material cached per source texture and the cutoff pushed per renderer via a property block.
//
// Unlit on purpose: it replaces a lit character material for the ~0.8 s of the dissolve, and a
// corpse turning to ash at T3 brightness does not need the board's key light.
Shader "Warband/Dissolve"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0,1)) = 0
        [HDR] _EdgeColor ("Edge Color", Color) = (2,0.9,0.4,1)
        _EdgeWidth ("Edge Width", Range(0.001,0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _EdgeColor;
                float _Cutoff;
                float _EdgeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv * 4.0).r;

                float diff = n - _Cutoff;
                clip(diff);                                   // already burned away

                float edge = 1.0 - saturate(diff / max(_EdgeWidth, 1e-4));
                half3 rgb = lerp(b.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                return half4(rgb, b.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
