// Alpha-blended scrolling floor fill — burning / blessed ground, the "body" layer under a field's
// edge ring. _Phase is the scroll offset, written per-instance from VfxInstance.Step (never _Time).
// Two noise layers scroll at different rates/scales so the crawl never reads as a sliding texture.
Shader "Warband/GroundFill"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise", 2D) = "white" {}
        _Phase ("Phase", Float) = 0
        _Intensity ("Intensity", Range(0,4)) = 1
        _EdgeFade ("Edge Fade", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Phase;
                float _Intensity;
                float _EdgeFade;
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
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv1 = IN.uv * 3.0 + float2(_Phase, _Phase * 0.37);
                float2 uv2 = IN.uv * 1.7 - float2(_Phase * 0.61, _Phase * 0.23);
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r * 0.6
                        + SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r * 0.4;

                float d = length(IN.uv - 0.5) * 2.0;
                float fade = 1.0 - smoothstep(1.0 - max(_EdgeFade, 1e-4), 1.0, d);

                float a = saturate(n * _Intensity * fade * _Color.a);
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
