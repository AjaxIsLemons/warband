// Additive procedural radial glow — release flashes and soft halos, zero texture dependency (this
// is the one that must work before any asset exists). _Falloff shapes the radial ramp: 1 = linear
// cone, 3-4 = a tight hot core. Size/alpha animate from VfxInstance.Step — never _Time.
Shader "Warband/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Falloff ("Falloff", Range(0.1,8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Falloff;
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
                float d = saturate(length(IN.uv - 0.5) * 2.0);
                float a = pow(saturate(1.0 - d), max(_Falloff, 0.1));
                return half4(_Color.rgb * (_Color.a * a), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
