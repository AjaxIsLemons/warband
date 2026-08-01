using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public enum RevisionFractureQuality
{
    Full,
    Mobile,
}

/// <summary>
/// Composites the Revision temporal fault after the complete board (including transparent VFX)
/// and before URP post-processing, so its HDR seam participates in the game's authored bloom.
/// The feature is installed into renderer assets by RevisionFractureTools; never hand-edit those
/// serialized assets.
/// </summary>
public sealed class RevisionFractureRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        public Shader shader;
        public RevisionFractureQuality quality = RevisionFractureQuality.Full;
        public RenderPassEvent injectionPoint =
            RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    private RevisionFracturePass _pass;

    public override void Create()
    {
        _pass?.Dispose();
        _pass = settings.shader == null
            ? null
            : new RevisionFracturePass(settings.shader, settings.quality)
            {
                renderPassEvent = settings.injectionPoint,
            };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (_pass == null) return;

        RevisionScreenEffect.Frame frame = RevisionScreenEffect.Current;
        _pass.Prepare(frame);
        Camera camera = renderingData.cameraData.camera;
        if (!frame.Active ||
            camera == null ||
            camera.cameraType != CameraType.Game ||
            !camera.CompareTag("MainCamera"))
            return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;
    }

    private sealed class RevisionFracturePass : ScriptableRenderPass, IDisposable
    {
        private static readonly int FutureTextureId =
            Shader.PropertyToID("_RevisionFutureTexture");
        private static readonly int PhaseId = Shader.PropertyToID("_RevisionPhase");
        private static readonly int ProgressId = Shader.PropertyToID("_RevisionProgress");
        private static readonly int StrengthId = Shader.PropertyToID("_RevisionStrength");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_RevisionEdgeWidthPx");
        private static readonly int EdgeGlowId = Shader.PropertyToID("_RevisionEdgeGlow");
        private static readonly int RefractionId = Shader.PropertyToID("_RevisionRefractionPx");
        private static readonly int PlateSlipId = Shader.PropertyToID("_RevisionPlateSlipPx");
        private static readonly int ChromaticId = Shader.PropertyToID("_RevisionChromaticPx");
        private static readonly int FutureOpacityId =
            Shader.PropertyToID("_RevisionFutureOpacity");
        private static readonly int HeldSeamId =
            Shader.PropertyToID("_RevisionHeldSeamStrength");
        private static readonly int SandFlowId = Shader.PropertyToID("_RevisionSandFlow");
        private static readonly int ScreenParamsId =
            Shader.PropertyToID("_RevisionScreenParams");
        private static readonly int TargetsId = Shader.PropertyToID("_RevisionTargetUVs");
        private static readonly int TargetCountId = Shader.PropertyToID("_RevisionTargetCount");
        private static readonly int HasFutureId = Shader.PropertyToID("_RevisionHasFuture");
        private static readonly int FullRuptureId = Shader.PropertyToID("_RevisionFullRupture");
        private static readonly int ReducedMotionId =
            Shader.PropertyToID("_RevisionReducedMotion");
        private static readonly int LineageId = Shader.PropertyToID("_RevisionLineage");
        private static readonly int QualityId = Shader.PropertyToID("_RevisionQuality");

        private readonly Material _material;
        private readonly RevisionFractureQuality _quality;
        private RTHandle _future;
        private RevisionScreenEffect.Frame _frame;
        private int _lastResetVersion = -1;
        private int _capturedRequest;

        internal RevisionFracturePass(Shader shader, RevisionFractureQuality quality)
        {
            _material = CoreUtils.CreateEngineMaterial(shader);
            _quality = quality;
            profilingSampler = new ProfilingSampler("Warband Revision Temporal Fault");
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        internal void Prepare(RevisionScreenEffect.Frame frame)
        {
            _frame = frame;
            if (_lastResetVersion == frame.ResetVersion) return;
            _lastResetVersion = frame.ResetVersion;
            ReleaseFuture();
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_material == null) return;

            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            RenderTextureDescriptor cameraDescriptor =
                cameraData.cameraTargetDescriptor;
            cameraDescriptor.depthBufferBits = 0;
            cameraDescriptor.msaaSamples = 1;
            cameraDescriptor.useMipMap = false;
            cameraDescriptor.autoGenerateMips = false;

            bool wantsCapture =
                _frame.CaptureRequest != 0 &&
                _frame.CaptureRequest != _capturedRequest;
            if (wantsCapture)
            {
                RenderTextureDescriptor futureDescriptor = cameraDescriptor;
                if (_quality == RevisionFractureQuality.Mobile)
                {
                    futureDescriptor.width =
                        Mathf.Max(1, futureDescriptor.width / 2);
                    futureDescriptor.height =
                        Mathf.Max(1, futureDescriptor.height / 2);
                }
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref _future,
                    futureDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_WarbandRevisionWitnessedFuture");
            }

            TextureHandle future = default;
            if (_future != null)
            {
                future = renderGraph.ImportTexture(_future);
                if (wantsCapture)
                {
                    RenderGraphUtils.BlitMaterialParameters captureParameters =
                        new(source, future, _material, 1);
                    renderGraph.AddBlitPass(
                        captureParameters,
                        passName: "Warband Revision Capture Witnessed Future");
                    _capturedRequest = _frame.CaptureRequest;
                }
            }

            TextureDesc destinationDescriptor =
                renderGraph.GetTextureDesc(source);
            destinationDescriptor.name = "_WarbandRevisionComposite";
            destinationDescriptor.clearBuffer = false;
            destinationDescriptor.depthBufferBits = DepthBits.None;
            TextureHandle destination =
                renderGraph.CreateTexture(destinationDescriptor);

            ApplyMaterialState(cameraDescriptor);
            RenderGraphUtils.BlitMaterialParameters compositeParameters =
                new(source, destination, _material, 0);
            renderGraph.AddBlitPass(
                compositeParameters,
                passName: "Warband Revision Temporal Fault");

            // Point later URP passes at the composite instead of paying for a second blit back
            // into camera color. The witnessed-future RT remains external because it must survive
            // across frames; the composite remains transient to this frame.
            resourceData.cameraColor = destination;
        }

        private void ApplyMaterialState(RenderTextureDescriptor descriptor)
        {
            RevisionPresentationTune tune = _frame.Tune;
            bool reduced = _frame.ReducedMotion;
            bool mobile = _quality == RevisionFractureQuality.Mobile;
            bool hasFuture =
                _future != null &&
                _frame.CaptureRequest != 0 &&
                _capturedRequest == _frame.CaptureRequest;

            _material.SetTexture(
                FutureTextureId,
                hasFuture ? _future.rt : Texture2D.blackTexture);
            _material.SetFloat(PhaseId, (float)_frame.Phase);
            _material.SetFloat(ProgressId, _frame.Progress);
            _material.SetFloat(StrengthId, tune.fractureStrength);
            _material.SetFloat(EdgeWidthId, tune.fractureEdgeWidthPx);
            _material.SetFloat(EdgeGlowId, tune.fractureEdgeGlow);
            _material.SetFloat(
                RefractionId,
                reduced ? 0f : tune.fractureRefractionPx);
            _material.SetFloat(
                PlateSlipId,
                reduced ? 0f : tune.fracturePlateSlipPx);
            _material.SetFloat(
                ChromaticId,
                reduced || mobile ? 0f : tune.fractureChromaticPx);
            _material.SetFloat(FutureOpacityId, tune.fractureFutureOpacity);
            _material.SetFloat(HeldSeamId, tune.fractureHeldSeamStrength);
            _material.SetFloat(SandFlowId, reduced ? 0f : tune.fractureSandFlow);
            _material.SetVector(
                ScreenParamsId,
                new Vector4(
                    descriptor.width,
                    descriptor.height,
                    1f / Mathf.Max(1, descriptor.width),
                    1f / Mathf.Max(1, descriptor.height)));
            _material.SetVector(TargetsId, _frame.TargetViewportPositions);
            _material.SetFloat(TargetCountId, _frame.TargetCount);
            _material.SetFloat(HasFutureId, hasFuture ? 1f : 0f);
            _material.SetFloat(FullRuptureId, _frame.FullRupture ? 1f : 0f);
            _material.SetFloat(ReducedMotionId, reduced ? 1f : 0f);
            _material.SetFloat(
                LineageId,
                _frame.Lineage == Warband.Sim.RevisionEffectKind.BorrowedFuture
                    ? 0f
                    : 1f);
            _material.SetFloat(QualityId, mobile ? 1f : 0f);
        }

        private void ReleaseFuture()
        {
            _future?.Release();
            _future = null;
            _capturedRequest = 0;
        }

        public void Dispose()
        {
            ReleaseFuture();
            CoreUtils.Destroy(_material);
        }
    }
}
