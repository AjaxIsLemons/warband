// Additive masked sigil — cast circles and emblems. _MainTex is a MONO mask (the era sigils are
// 512² mono per combat-spectacle §2); its red channel × alpha is the mask, so both a plain
// grayscale mask and an RGBA cutout read the same. _Rotation spins the UVs about (0.5,0.5) and is
// written per-instance from VfxInstance.Step — never _Time.
Shader "Warband/Sigil"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Mask", 2D) = "white" {}
        _Rotation ("Rotation (turns)", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
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

            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Rotation;
                float _Alpha;
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
                float2 p = IN.uv - 0.5;
                float s, c;
                sincos(_Rotation * 6.2831853, s, c);
                float2 uv = float2(p.x * c - p.y * s, p.x * s + p.y * c) + 0.5;

                // A rotated square samples outside its own footprint at the corners; drop those
                // rather than relying on the texture's wrap mode.
                float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);

                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float mask = t.r * t.a * inside;
                return half4(_Color.rgb * (_Color.a * _Alpha * mask), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
