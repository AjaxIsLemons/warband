// Additive polar ring — telegraphs, shockwaves, status countdown rings, field rims.
// Polar coordinates are taken from UV around (0.5,0.5), so it needs a UV'd quad/hex; _Radius is
// in "half-quad" units (1 = the quad's edge). Every animated input (_Radius/_ArcFill/_Rotation)
// is written per-instance from VfxInstance.Step via a MaterialPropertyBlock — NO _Time, ever
// (fx-runtime law: one clock drives play mode and frozen captures alike).
Shader "Warband/Ring"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Radius ("Radius", Range(0,1)) = 0.7
        _Thickness ("Thickness", Range(0,1)) = 0.12
        _Softness ("Softness", Range(0,1)) = 0.15
        _ArcFill ("Arc Fill", Range(0,1)) = 1
        _Rotation ("Rotation (turns)", Range(0,1)) = 0
    }

    SubShader
    {
        // Queue 2900 (Transparent-100): after opaque, before the transparent crowd, so ground
        // overlays never z-fight the Lit board tiles (Offset pulls them toward the camera).
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Radius;
                float _Thickness;
                float _Softness;
                float _ArcFill;
                float _Rotation;
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
                float d = length(p) * 2.0;                       // 0 at centre, 1 at the quad edge
                float halfT = max(_Thickness, 1e-4) * 0.5;
                float aa = max(_Softness, 1e-4) * 0.5;
                float band = 1.0 - smoothstep(halfT, halfT + aa, abs(d - _Radius));

                // Angle in turns, CCW from -X, offset by _Rotation. _ArcFill >= 1 keeps the whole
                // circle (the countdown ring drains by lowering it).
                float ang = frac(atan2(p.y, p.x) * 0.15915494 + 0.5 - _Rotation);
                float arc = 1.0 - smoothstep(_ArcFill, _ArcFill + 0.01, ang);

                return half4(_Color.rgb * (_Color.a * band * arc), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
