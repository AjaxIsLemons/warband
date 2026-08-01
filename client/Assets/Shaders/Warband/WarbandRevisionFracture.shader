Shader "Warband/RevisionFracture"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Revision Temporal Fault"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_RevisionFutureTexture);

            CBUFFER_START(UnityPerMaterial)
                float _RevisionPhase;
                float _RevisionProgress;
                float _RevisionStrength;
                float _RevisionEdgeWidthPx;
                float _RevisionEdgeGlow;
                float _RevisionRefractionPx;
                float _RevisionPlateSlipPx;
                float _RevisionChromaticPx;
                float _RevisionFutureOpacity;
                float _RevisionHeldSeamStrength;
                float _RevisionSandFlow;
                float4 _RevisionScreenParams;
                float4 _RevisionTargetUVs;
                float _RevisionTargetCount;
                float _RevisionHasFuture;
                float _RevisionFullRupture;
                float _RevisionReducedMotion;
                float _RevisionLineage;
                float _RevisionQuality;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1.0, 0.0)), u.x),
                    lerp(Hash21(i + float2(0.0, 1.0)),
                         Hash21(i + float2(1.0, 1.0)), u.x),
                    u.y);
            }

            float2 AspectPoint(float2 uv)
            {
                float aspect = _RevisionScreenParams.x /
                    max(1.0, _RevisionScreenParams.y);
                return float2((uv.x - 0.5) * aspect + 0.5, uv.y);
            }

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(0.00001, dot(ba, ba)));
                return length(pa - ba * h);
            }

            float FaultY(float x)
            {
                // The primary fault is a descending, piecewise-linear scar. Quantized noise makes
                // it read as snapped glass instead of a sine wave while retaining a deliberately
                // composed silhouette at every resolution.
                float cells = 13.0;
                float cell = floor(x * cells);
                float local = frac(x * cells);
                float a = Hash21(float2(cell, 4.7));
                float b = Hash21(float2(cell + 1.0, 4.7));
                float angular = lerp(a, b, local) - 0.5;
                return 0.91 - x * 0.78 + angular * 0.055;
            }

            void FaultGeometry(
                float2 uv,
                out float primaryDistance,
                out float branchDistance,
                out float radial)
            {
                float n0 = ValueNoise(uv * 24.0 + 7.3);
                float n1 = ValueNoise(uv.yx * 39.0 + 19.1);
                float2 warpedUv = uv + (float2(n0, n1) - 0.5) * 0.0065;
                float aspect = _RevisionScreenParams.x /
                    max(1.0, _RevisionScreenParams.y);
                primaryDistance =
                    abs(warpedUv.y - FaultY(warpedUv.x)) /
                    sqrt(1.0 + 0.78 * 0.78);

                float2 p = AspectPoint(warpedUv);
                float2 b0 = AspectPoint(float2(0.34, FaultY(0.34)));
                float2 b1 = AspectPoint(float2(0.42, 0.91));
                float2 b2 = AspectPoint(float2(0.58, FaultY(0.58)));
                float2 b3 = AspectPoint(float2(0.80, 0.64));
                float2 b4 = AspectPoint(float2(0.72, FaultY(0.72)));
                float2 b5 = AspectPoint(float2(0.87, 0.17));
                branchDistance = SegmentDistance(p, b0, b1);
                branchDistance = min(
                    branchDistance,
                    SegmentDistance(p, b2, b3));
                branchDistance = min(
                    branchDistance,
                    SegmentDistance(p, b4, b5));

                float2 origin = AspectPoint(float2(0.50, FaultY(0.50)));
                radial = distance(p, origin) / max(1.0, aspect * 0.72);
            }

            float TargetFilamentDistance(float2 uv)
            {
                float2 p = AspectPoint(uv);
                float2 origin = AspectPoint(float2(0.49, 0.46));
                float d = 10.0;
                if (_RevisionTargetCount > 0.5)
                    d = SegmentDistance(
                        p, origin, AspectPoint(_RevisionTargetUVs.xy));
                if (_RevisionTargetCount > 1.5)
                    d = min(d, SegmentDistance(
                        p, origin, AspectPoint(_RevisionTargetUVs.zw)));
                return d;
            }

            float TargetPointDistance(float2 uv)
            {
                float2 p = AspectPoint(uv);
                float d = 10.0;
                if (_RevisionTargetCount > 0.5)
                    d = distance(p, AspectPoint(_RevisionTargetUVs.xy));
                if (_RevisionTargetCount > 1.5)
                    d = min(
                        d,
                        distance(p, AspectPoint(_RevisionTargetUVs.zw)));
                return d;
            }

            float FaultSignedSide(float2 uv)
            {
                return uv.y - FaultY(uv.x);
            }

            half4 SampleCurrent(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(
                    _BlitTexture, sampler_LinearClamp, saturate(uv));
            }

            half4 SampleFuture(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(
                    _RevisionFutureTexture, sampler_LinearClamp, saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float progress = saturate(_RevisionProgress);
                float2 uv = input.texcoord;
                float2 texel = _RevisionScreenParams.zw;
                const float2 plateNormal = float2(0.6139601, 0.7893371);

                float growth = 1.0;
                float separation = 0.0;
                float edgeAmount = 1.0;
                float futureAmount = 0.0;
                float vacuum = 0.0;
                float drift = 0.0;
                float targetAmount = 0.0;
                float landingPulse = 0.0;
                float openingPulse = 0.0;

                // RevisionPresentationPhase:
                // 1 Opening · 2 Held · 3 Tear · 4 Rewind · 5 Vacuum · 6 Landing.
                if (_RevisionPhase < 1.5)
                {
                    growth = lerp(
                        1.0,
                        saturate(progress / 0.55),
                        _RevisionFullRupture);
                    separation = smoothstep(0.18, 0.62, progress) *
                        lerp(0.62, 1.0, _RevisionFullRupture);
                    edgeAmount = 0.45 + sin(saturate(progress) * PI) * 1.65;
                    openingPulse =
                        sin(saturate(progress / 0.85) * PI) *
                        _RevisionFullRupture;
                }
                else if (_RevisionPhase < 2.5)
                {
                    separation = 0.0;
                    edgeAmount = _RevisionHeldSeamStrength;
                }
                else if (_RevisionPhase < 3.5)
                {
                    separation = smoothstep(0.0, 0.48, progress);
                    futureAmount = smoothstep(0.18, 0.82, progress);
                    edgeAmount = 1.0 + sin(progress * PI) * 1.25;
                    targetAmount = smoothstep(0.34, 0.88, progress);
                }
                else if (_RevisionPhase < 4.5)
                {
                    separation = 1.0;
                    futureAmount = 1.0;
                    edgeAmount = 1.15;
                    drift = progress * 0.245;
                    targetAmount = 0.72;
                }
                else if (_RevisionPhase < 5.5)
                {
                    separation = 1.0 - smoothstep(0.0, 1.0, progress);
                    futureAmount = 1.0 - smoothstep(0.0, 0.82, progress);
                    edgeAmount = lerp(1.2, 0.18, progress);
                    drift = 0.245;
                    vacuum = smoothstep(0.0, 1.0, progress);
                    targetAmount = 1.0 - progress;
                }
                else
                {
                    separation = 1.0 - smoothstep(0.0, 0.55, progress);
                    edgeAmount = 1.0 - smoothstep(0.28, 0.82, progress);
                    targetAmount = 1.0 - smoothstep(0.32, 0.72, progress);
                    landingPulse = sin(saturate(progress / 0.68) * PI);
                    drift = 0.245 * (1.0 - smoothstep(0.0, 0.55, progress));
                }

                // The fault migrates toward the top-right during rewind. The rejected side loses
                // territory while the live branch underneath takes the screen.
                float2 localUv = uv - plateNormal * drift;
                float primaryDistance;
                float branchDistance;
                float radial;
                FaultGeometry(
                    localUv,
                    primaryDistance,
                    branchDistance,
                    radial);
                float growthReveal =
                    1.0 - smoothstep(growth - 0.035, growth + 0.045, radial);
                growthReveal *= step(0.001, growth);

                float edgeWidth = max(
                    0.0004,
                    _RevisionEdgeWidthPx / max(1.0, _RevisionScreenParams.y));
                float gapHalf = edgeWidth * (0.55 + separation * 4.8);
                float distanceFromGap = abs(primaryDistance - gapHalf);
                float outsideGap = step(gapHalf * 0.92, primaryDistance);
                float primaryGap =
                    (1.0 - smoothstep(
                        gapHalf * 0.72,
                        gapHalf,
                        primaryDistance)) *
                    growthReveal;
                float primaryRim =
                    (1.0 - smoothstep(
                        edgeWidth * 0.35,
                        edgeWidth * 1.35,
                        distanceFromGap)) *
                    outsideGap *
                    growthReveal;
                float primaryHalo =
                    (1.0 - smoothstep(
                        edgeWidth * 0.75,
                        edgeWidth * 7.5,
                        distanceFromGap)) *
                    outsideGap *
                    growthReveal;
                float branchGap =
                    (1.0 - smoothstep(
                        edgeWidth * 0.18,
                        edgeWidth * 0.45,
                        branchDistance)) *
                    growthReveal;
                float branchRim =
                    (1.0 - smoothstep(
                        edgeWidth * 0.35,
                        edgeWidth * 1.05,
                        branchDistance)) *
                    growthReveal;
                float branchHalo =
                    (1.0 - smoothstep(
                        edgeWidth * 0.9,
                        edgeWidth * 3.4,
                        branchDistance)) *
                    growthReveal;
                float crackHalo = saturate(
                    primaryHalo + branchHalo * 0.32);

                float signedSide = FaultSignedSide(localUv);
                float sideSign = signedSide >= 0.0 ? 1.0 : -1.0;
                float edgeGuard = smoothstep(
                    0.0,
                    0.055,
                    min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)));
                float2 plateShift =
                    plateNormal * sideSign *
                    (_RevisionPlateSlipPx * separation * edgeGuard) * texel;
                float refractionNoise =
                    (ValueNoise(localUv * 46.0 + progress * 3.7) - 0.5) * 2.0;
                float2 refractShift =
                    plateNormal * sideSign *
                    (_RevisionRefractionPx * primaryHalo *
                     (0.65 + 0.35 * refractionNoise)) * texel;
                float2 currentUv = uv + plateShift + refractShift;

                half4 current = SampleCurrent(currentUv);
                if (_RevisionChromaticPx > 0.001)
                {
                    float2 chroma =
                        plateNormal * sideSign *
                        (_RevisionChromaticPx * primaryHalo) * texel;
                    half red = SampleCurrent(currentUv + chroma).r;
                    half blue = SampleCurrent(currentUv - chroma).b;
                    current.r = red;
                    current.b = blue;
                }

                float futureSide = smoothstep(
                    -edgeWidth * 2.2,
                    edgeWidth * 2.2,
                    signedSide);
                float futureMask =
                    futureSide *
                    futureAmount *
                    _RevisionFutureOpacity *
                    _RevisionHasFuture;
                half4 future = SampleFuture(
                    uv - plateShift * 0.52 - refractShift * 0.35);
                half3 color = lerp(current.rgb, future.rgb, futureMask);
                float plateGrade =
                    separation *
                    lerp(-0.035, 0.045, step(0.0, signedSide));
                color *= 1.0 + plateGrade;

                // Sand runs toward the origin during rewind, constrained to the temporal seam.
                float sandBand =
                    (1.0 - smoothstep(
                        edgeWidth * 2.0,
                        edgeWidth * 20.0,
                        primaryDistance)) *
                    growthReveal;
                float sandClock =
                    (localUv.x - localUv.y) * 54.0 -
                    progress * 24.0 * _RevisionSandFlow;
                float sandNoise =
                    ValueNoise(float2(sandClock, localUv.x * 17.0 + localUv.y * 11.0));
                float sand =
                    smoothstep(0.73, 0.96, sandNoise) *
                    sandBand *
                    saturate(_RevisionSandFlow) *
                    step(3.5, _RevisionPhase) *
                    step(_RevisionPhase, 5.5);

                float targetDistance = TargetFilamentDistance(localUv);
                float targetFilament =
                    (1.0 - smoothstep(
                        edgeWidth * 0.45,
                        edgeWidth * 2.1,
                        targetDistance)) *
                    targetAmount;
                float targetPointDistance = TargetPointDistance(localUv);
                float targetRingRadius =
                    lerp(0.018, 0.105, saturate(progress / 0.68));
                float targetRing =
                    (1.0 - smoothstep(
                        edgeWidth * 0.55,
                        edgeWidth * 1.8,
                        abs(targetPointDistance - targetRingRadius))) *
                    landingPulse *
                    step(5.5, _RevisionPhase) *
                    step(_RevisionPhase, 6.5);
                float targetBloom =
                    (1.0 - smoothstep(0.0, 0.13, targetPointDistance)) *
                    landingPulse *
                    step(5.5, _RevisionPhase) *
                    step(_RevisionPhase, 6.5);
                float openingBloom =
                    (1.0 - smoothstep(0.0, 0.31, radial)) *
                    openingPulse;

                half3 sandGold = half3(1.00, 0.72, 0.30);
                half3 lineage = lerp(
                    half3(0.22, 0.82, 1.00),
                    half3(0.70, 0.43, 0.96),
                    saturate(_RevisionLineage));
                half3 edgeColor = lerp(
                    sandGold,
                    lineage,
                    saturate(landingPulse * 0.92));

                color *= 1.0 -
                    saturate(primaryGap + branchGap * 0.62) *
                    0.92 *
                    _RevisionStrength;
                color += edgeColor *
                    (primaryRim * _RevisionEdgeGlow * edgeAmount +
                     primaryHalo * 0.12 * edgeAmount +
                     branchRim * 0.42 * edgeAmount +
                     branchHalo * 0.055 * edgeAmount +
                     targetFilament * 0.48 +
                     sand * 0.58) *
                    _RevisionStrength;
                color += sandGold * openingBloom * 0.48 * _RevisionStrength;
                color += lineage * landingPulse *
                    (primaryRim + primaryHalo * 0.15 + targetFilament * 0.6) *
                    (1.05 + _RevisionEdgeGlow * 0.25) * _RevisionStrength;
                color += lineage *
                    (targetRing * 1.6 + targetBloom * 0.28) *
                    _RevisionStrength;
                color *= lerp(1.0, 0.14, vacuum);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Revision Witnessed Future Copy"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopy

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragCopy(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    input.texcoord);
            }
            ENDHLSL
        }
    }
}
