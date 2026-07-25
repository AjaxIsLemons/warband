// Additive particle material — _Color × vertex color × _MainTex. Every ParticleElement renders
// through this: the per-instance lane tint arrives as the ParticleSystem's start color (vertex
// color), so one shared material serves the whole board with no per-instance material copies.
//
// Flipbook-ready: Unity's Texture Sheet Animation module rewrites TEXCOORD0 in place for the
// standard particle vertex layout, so a 4×4 sheet animates with no extra streams here. Frame
// BLENDING would need the AnimBlend/UV2 streams and is deliberately not supported.
Shader "Warband/Particle"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c = t * IN.color * _Color;
                return half4(c.rgb * c.a, 0);   // premultiplied: alpha carries the mote's shape
            }
            ENDHLSL
        }
    }
    Fallback Off
}
