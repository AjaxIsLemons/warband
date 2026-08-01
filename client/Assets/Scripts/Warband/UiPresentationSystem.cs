using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One tunable motion recipe. UI truth never lives here: these values only describe how an
/// already-authoritative state change is presented.
/// </summary>
[Serializable]
internal sealed class UiMotionRecipe
{
    [Min(0)] public int durationMs = 180;
    [Min(0)] public int settleMs = 90;
    [Min(0f)] public float distancePx = 10f;
    [Min(0.8f)] public float scale = 1.025f;
    [Range(0f, 1f)] public float startOpacity = 0.12f;
    [Min(0)] public int particles = 5;
    [Min(0)] public int staggerMs = 35;
    [Min(0)] public int staggerCapMs = 175;
}

[Serializable]
internal sealed class UiMusterTuning
{
    [Min(0)] public int lensInitialDelayMs = 220;
    [Min(0)] public int lensReshowMs = 70;
    [Min(0)] public int lensCloseMs = 90;
    [Min(0)] public int revealInfoDelayMs = 70;
    [Min(0)] public int slotCompactMs = 120;
    [Min(0)] public int reducedFadeMs = 80;

    public UiMotionRecipe reveal = new UiMotionRecipe
    {
        durationMs = 240, settleMs = 90, distancePx = 18f, scale = 0.985f,
        startOpacity = 0f, particles = 0, staggerMs = 55, staggerCapMs = 220,
    };
    public UiMotionRecipe select = new UiMotionRecipe
    {
        durationMs = 220, settleMs = 160, distancePx = 8f, scale = 1.045f,
        startOpacity = 1f, particles = 7, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe deselect = new UiMotionRecipe
    {
        durationMs = 160, settleMs = 120, distancePx = 6f, scale = 1.025f,
        startOpacity = 1f, particles = 4, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe blocked = new UiMotionRecipe
    {
        durationMs = 155, settleMs = 90, distancePx = 5f, scale = 1f,
        startOpacity = 1f, particles = 3, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe ready = new UiMotionRecipe
    {
        durationMs = 220, settleMs = 120, distancePx = 2f, scale = 1.025f,
        startOpacity = 1f, particles = 4, staggerMs = 0, staggerCapMs = 0,
    };
}

[Serializable]
internal sealed class UiFxTuning
{
    [Min(0.5f)] public float lineWidth = 2f;
    [Min(0f)] public float arcHeight = 64f;
    [Min(1f)] public float grainSize = 3.5f;
    [Min(0)] public int maxEffects = 24;
    [Range(0f, 1f)] public float trailOpacity = 0.42f;
    public Color previewColor = new Color(0.41f, 0.65f, 0.91f, 1f);
    public Color sandColor = new Color(0.85f, 0.64f, 0.23f, 1f);
    public Color positiveColor = new Color(0.40f, 0.79f, 0.60f, 1f);
    public Color negativeColor = new Color(0.71f, 0.30f, 0.26f, 1f);
}

[Serializable]
internal sealed class HallEnvironmentTuning
{
    public bool enabled = true;
    public bool highQuality = true;
    [Min(80)] public int cameraDurationMs = 280;
    [Range(0f, 1f)] public float cameraOvershoot = 0.08f;
    [Range(0f, 2f)] public float parallax = 0.55f;
    [Min(0f)] public float ambientRingDegreesPerSecond = 1.8f;
    [Range(0f, 2f)] public float ambientPulse = 0.22f;
    [Range(0f, 4f)] public float routePulse = 1.25f;
    [Range(0, 64)] public int ambientMotes = 24;
    [Range(0, 64)] public int transactionMotes = 12;
}

[Serializable]
internal sealed class UiAudioTuning
{
    public bool enabled = false;
    [Range(0f, 1f)] public float volume = 0.72f;
    [Range(0f, 0.2f)] public float pitchVariance = 0.025f;
    // hoverCooldownMs / ambienceVolume / commitDuck retired 2026-07-27: hover is silent by law and
    // the Hall ambience bed is cut (D1), so there is nothing left to rate-limit or duck against.
    // The board duck now lives on the mixer (`SfxDucker`), not on an ambience source.
}

[Serializable]
internal sealed class UiHapticTuning
{
    public bool enabled = true;
    [Range(1, 60)] public int selectMs = 8;
    [Range(1, 120)] public int successMs = 18;
    [Range(1, 120)] public int errorMs = 24;
}

[Serializable]
internal sealed class UiTooltipTuning
{
    [Min(0)] public int pointerDelayMs = 280;
    [Min(0)] public int focusDelayMs = 220;
    [Min(0)] public int reshowDelayMs = 80;
    [Min(0)] public int closeDelayMs = 100;
    [Min(0)] public int revealMs = 130;
    [Min(0)] public int dismissMs = 85;
    [Min(0f)] public float offsetPx = 12f;
    [Min(0f)] public float safeMarginPx = 16f;
    [Min(220f)] public float maxWidthPx = 360f;
    [Min(240f)] public float equipmentMaxWidthPx = 390f;
}

[Serializable]
internal sealed class UiInteractionRecipes
{
    public UiMotionRecipe tooltipReveal = new UiMotionRecipe
    {
        durationMs = 130, settleMs = 45, distancePx = 7f, scale = 0.99f,
        startOpacity = 0f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe tooltipDismiss = new UiMotionRecipe
    {
        durationMs = 85, settleMs = 20, distancePx = 4f, scale = 0.995f,
        startOpacity = 1f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe pin = new UiMotionRecipe
    {
        durationMs = 180, settleMs = 100, distancePx = 5f, scale = 1.035f,
        startOpacity = 1f, particles = 4, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe unpin = new UiMotionRecipe
    {
        durationMs = 130, settleMs = 60, distancePx = 4f, scale = 1.015f,
        startOpacity = 1f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe drawerExpand = new UiMotionRecipe
    {
        durationMs = 190, settleMs = 70, distancePx = 28f, scale = 0.99f,
        startOpacity = 0.06f, particles = 3, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe drawerCollapse = new UiMotionRecipe
    {
        durationMs = 150, settleMs = 50, distancePx = 22f, scale = 0.995f,
        startOpacity = 0.12f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe socketWake = new UiMotionRecipe
    {
        durationMs = 210, settleMs = 100, distancePx = 3f, scale = 1.04f,
        startOpacity = 1f, particles = 5, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe projectedTarget = new UiMotionRecipe
    {
        durationMs = 260, settleMs = 110, distancePx = 8f, scale = 1.045f,
        startOpacity = 1f, particles = 7, staggerMs = 0, staggerCapMs = 0,
    };
}

[Serializable]
internal sealed class UiTransactionRecipes
{
    public UiMotionRecipe recruit = new UiMotionRecipe
    {
        durationMs = 410, settleMs = 150, distancePx = 12f, scale = 1.065f,
        startOpacity = 1f, particles = 12, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe rank = new UiMotionRecipe
    {
        durationMs = 480, settleMs = 170, distancePx = 14f, scale = 1.075f,
        startOpacity = 1f, particles = 14, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe weapon = new UiMotionRecipe
    {
        durationMs = 360, settleMs = 120, distancePx = 9f, scale = 1.045f,
        startOpacity = 1f, particles = 8, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe trinket = new UiMotionRecipe
    {
        durationMs = 390, settleMs = 130, distancePx = 10f, scale = 1.05f,
        startOpacity = 1f, particles = 10, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe inscription = new UiMotionRecipe
    {
        durationMs = 520, settleMs = 180, distancePx = 15f, scale = 1.07f,
        startOpacity = 1f, particles = 16, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe capacity = new UiMotionRecipe
    {
        durationMs = 440, settleMs = 150, distancePx = 12f, scale = 1.06f,
        startOpacity = 1f, particles = 12, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe rankChoice = new UiMotionRecipe
    {
        durationMs = 560, settleMs = 190, distancePx = 16f, scale = 1.08f,
        startOpacity = 1f, particles = 18, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe equip = new UiMotionRecipe
    {
        durationMs = 300, settleMs = 110, distancePx = 8f, scale = 1.04f,
        startOpacity = 1f, particles = 7, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe reforge = new UiMotionRecipe
    {
        durationMs = 620, settleMs = 210, distancePx = 18f, scale = 1.085f,
        startOpacity = 1f, particles = 20, staggerMs = 0, staggerCapMs = 0,
    };
}

/// <summary>
/// Shared, hot-reloadable UI-presentation source of truth. Load returns the same object every
/// time, so live debug edits immediately reach every director already holding the configuration.
/// </summary>
[Serializable]
internal sealed class HubPresentationConfig
{
    public bool effectsEnabled = true;
    public bool ambientMotion = true;
    [Min(0)] public int reducedFadeMs = 80;

    public UiMotionRecipe press = new UiMotionRecipe
    {
        durationMs = 70, settleMs = 75, distancePx = 0f, scale = 0.982f,
        startOpacity = 1f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe hover = new UiMotionRecipe
    {
        durationMs = 90, settleMs = 90, distancePx = 3f, scale = 1.012f,
        startOpacity = 1f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe focus = new UiMotionRecipe
    {
        durationMs = 120, settleMs = 90, distancePx = 2f, scale = 1.014f,
        startOpacity = 1f, particles = 2, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe select = new UiMotionRecipe
    {
        durationMs = 145, settleMs = 110, distancePx = 5f, scale = 1.045f,
        startOpacity = 1f, particles = 5, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe reveal = new UiMotionRecipe
    {
        durationMs = 230, settleMs = 80, distancePx = 18f, scale = 0.975f,
        startOpacity = 0f, particles = 2, staggerMs = 42, staggerCapMs = 210,
    };
    public UiMotionRecipe detailSwap = new UiMotionRecipe
    {
        durationMs = 165, settleMs = 70, distancePx = 12f, scale = 0.992f,
        startOpacity = 0.18f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe shelfExpand = new UiMotionRecipe
    {
        durationMs = 180, settleMs = 70, distancePx = 28f, scale = 0.99f,
        startOpacity = 0.08f, particles = 3, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe shelfCollapse = new UiMotionRecipe
    {
        durationMs = 150, settleMs = 50, distancePx = 22f, scale = 0.99f,
        startOpacity = 1f, particles = 0, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe shelfFocus = new UiMotionRecipe
    {
        durationMs = 120, settleMs = 80, distancePx = 3f, scale = 1.035f,
        startOpacity = 1f, particles = 2, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe route = new UiMotionRecipe
    {
        durationMs = 260, settleMs = 90, distancePx = 30f, scale = 0.99f,
        startOpacity = 0.18f, particles = 7, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe attention = new UiMotionRecipe
    {
        durationMs = 280, settleMs = 130, distancePx = 3f, scale = 1.035f,
        startOpacity = 1f, particles = 4, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe commit = new UiMotionRecipe
    {
        durationMs = 340, settleMs = 120, distancePx = 10f, scale = 1.055f,
        startOpacity = 1f, particles = 9, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe error = new UiMotionRecipe
    {
        durationMs = 155, settleMs = 90, distancePx = 5f, scale = 1f,
        startOpacity = 1f, particles = 3, staggerMs = 0, staggerCapMs = 0,
    };
    public UiMotionRecipe result = new UiMotionRecipe
    {
        durationMs = 450, settleMs = 120, distancePx = 22f, scale = 0.98f,
        startOpacity = 0f, particles = 8, staggerMs = 45, staggerCapMs = 180,
    };
    public UiMotionRecipe boss = new UiMotionRecipe
    {
        durationMs = 750, settleMs = 180, distancePx = 28f, scale = 0.97f,
        startOpacity = 0f, particles = 12, staggerMs = 60, staggerCapMs = 240,
    };
    public UiMusterTuning muster = new UiMusterTuning();
    public UiTransactionRecipes transactions = new UiTransactionRecipes();
    public UiFxTuning fx = new UiFxTuning();
    public HallEnvironmentTuning environment = new HallEnvironmentTuning();
    public UiAudioTuning audio = new UiAudioTuning();
    public UiHapticTuning haptics = new UiHapticTuning();
    public UiTooltipTuning tooltip = new UiTooltipTuning();
    public UiInteractionRecipes interactions = new UiInteractionRecipes();

    private static readonly HubPresentationConfig Shared = new HubPresentationConfig();
    private static bool s_loaded;

    public static int Revision { get; private set; }
    public static event Action Changed;

    public static HubPresentationConfig Load()
    {
        if (!s_loaded) Reload();
        return Shared;
    }

    public static void Reload()
    {
        // Reset the stable object before overlaying JSON so removing a field from the file restores
        // its authored default instead of preserving a stale value from a previous hot edit.
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new HubPresentationConfig()), Shared);
        var asset = Resources.Load<TextAsset>("UI/HubPresentation");
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(asset.text, Shared);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UI Polish] Invalid HubPresentation.json; using defaults. {ex.Message}");
            }
        }

        Sanitize();
        s_loaded = true;
        Revision++;
        Changed?.Invoke();
    }

    public static void NotifyChanged()
    {
        Sanitize();
        Changed?.Invoke();
    }

    public static bool Save()
    {
        Sanitize();
#if UNITY_EDITOR
        try
        {
            string path = Path.Combine(Application.dataPath, "Resources", "UI",
                "HubPresentation.json");
            File.WriteAllText(path, JsonUtility.ToJson(Shared, true));
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/UI/HubPresentation.json");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UI Polish] Could not save presentation tuning. {ex.Message}");
            return false;
        }
#else
        Debug.LogWarning("[UI Polish] Saving presentation tuning is Editor-only.");
        return false;
#endif
    }

    private static void Sanitize()
    {
        Shared.reducedFadeMs = Mathf.Max(0, Shared.reducedFadeMs);
        Sanitize(Shared.press);
        Sanitize(Shared.hover);
        Sanitize(Shared.focus);
        Sanitize(Shared.select);
        Sanitize(Shared.reveal);
        Sanitize(Shared.detailSwap);
        Sanitize(Shared.shelfExpand);
        Sanitize(Shared.shelfCollapse);
        Sanitize(Shared.shelfFocus);
        Sanitize(Shared.route);
        Sanitize(Shared.attention);
        Sanitize(Shared.commit);
        Sanitize(Shared.error);
        Sanitize(Shared.result);
        Sanitize(Shared.boss);
        Shared.muster ??= new UiMusterTuning();
        Shared.muster.lensInitialDelayMs = Mathf.Max(0, Shared.muster.lensInitialDelayMs);
        Shared.muster.lensReshowMs = Mathf.Max(0, Shared.muster.lensReshowMs);
        Shared.muster.lensCloseMs = Mathf.Max(0, Shared.muster.lensCloseMs);
        Shared.muster.revealInfoDelayMs = Mathf.Max(0, Shared.muster.revealInfoDelayMs);
        Shared.muster.slotCompactMs = Mathf.Max(0, Shared.muster.slotCompactMs);
        Shared.muster.reducedFadeMs = Mathf.Max(0, Shared.muster.reducedFadeMs);
        Sanitize(Shared.muster.reveal);
        Sanitize(Shared.muster.select);
        Sanitize(Shared.muster.deselect);
        Sanitize(Shared.muster.blocked);
        Sanitize(Shared.muster.ready);
        Sanitize(Shared.transactions.recruit);
        Sanitize(Shared.transactions.rank);
        Sanitize(Shared.transactions.weapon);
        Sanitize(Shared.transactions.trinket);
        Sanitize(Shared.transactions.inscription);
        Sanitize(Shared.transactions.capacity);
        Sanitize(Shared.transactions.rankChoice);
        Sanitize(Shared.transactions.equip);
        Sanitize(Shared.transactions.reforge);
        Shared.fx.lineWidth = Mathf.Max(0.5f, Shared.fx.lineWidth);
        Shared.fx.arcHeight = Mathf.Max(0f, Shared.fx.arcHeight);
        Shared.fx.grainSize = Mathf.Max(1f, Shared.fx.grainSize);
        Shared.fx.maxEffects = Mathf.Clamp(Shared.fx.maxEffects, 1, 64);
        Shared.fx.trailOpacity = Mathf.Clamp01(Shared.fx.trailOpacity);
        Shared.environment.cameraDurationMs = Mathf.Max(80,
            Shared.environment.cameraDurationMs);
        Shared.environment.cameraOvershoot = Mathf.Clamp01(
            Shared.environment.cameraOvershoot);
        Shared.environment.parallax = Mathf.Clamp(Shared.environment.parallax, 0f, 2f);
        Shared.environment.ambientRingDegreesPerSecond = Mathf.Max(0f,
            Shared.environment.ambientRingDegreesPerSecond);
        Shared.environment.ambientPulse = Mathf.Clamp(Shared.environment.ambientPulse, 0f, 2f);
        Shared.environment.routePulse = Mathf.Clamp(Shared.environment.routePulse, 0f, 4f);
        Shared.environment.ambientMotes = Mathf.Clamp(Shared.environment.ambientMotes, 0, 64);
        Shared.environment.transactionMotes = Mathf.Clamp(
            Shared.environment.transactionMotes, 0, 64);
        Shared.audio.volume = Mathf.Clamp01(Shared.audio.volume);
        Shared.audio.pitchVariance = Mathf.Clamp(Shared.audio.pitchVariance, 0f, 0.2f);
        Shared.haptics.selectMs = Mathf.Clamp(Shared.haptics.selectMs, 1, 60);
        Shared.haptics.successMs = Mathf.Clamp(Shared.haptics.successMs, 1, 120);
        Shared.haptics.errorMs = Mathf.Clamp(Shared.haptics.errorMs, 1, 120);
        Shared.tooltip ??= new UiTooltipTuning();
        Shared.tooltip.pointerDelayMs = Mathf.Max(0, Shared.tooltip.pointerDelayMs);
        Shared.tooltip.focusDelayMs = Mathf.Max(0, Shared.tooltip.focusDelayMs);
        Shared.tooltip.reshowDelayMs = Mathf.Max(0, Shared.tooltip.reshowDelayMs);
        Shared.tooltip.closeDelayMs = Mathf.Max(0, Shared.tooltip.closeDelayMs);
        Shared.tooltip.revealMs = Mathf.Max(0, Shared.tooltip.revealMs);
        Shared.tooltip.dismissMs = Mathf.Max(0, Shared.tooltip.dismissMs);
        Shared.tooltip.offsetPx = Mathf.Max(0f, Shared.tooltip.offsetPx);
        Shared.tooltip.safeMarginPx = Mathf.Max(0f, Shared.tooltip.safeMarginPx);
        Shared.tooltip.maxWidthPx = Mathf.Max(220f, Shared.tooltip.maxWidthPx);
        Shared.tooltip.equipmentMaxWidthPx = Mathf.Max(
            240f, Shared.tooltip.equipmentMaxWidthPx);
        Shared.interactions ??= new UiInteractionRecipes();
        Sanitize(Shared.interactions.tooltipReveal);
        Sanitize(Shared.interactions.tooltipDismiss);
        Sanitize(Shared.interactions.pin);
        Sanitize(Shared.interactions.unpin);
        Sanitize(Shared.interactions.drawerExpand);
        Sanitize(Shared.interactions.drawerCollapse);
        Sanitize(Shared.interactions.socketWake);
        Sanitize(Shared.interactions.projectedTarget);
    }

    private static void Sanitize(UiMotionRecipe recipe)
    {
        if (recipe == null) return;
        recipe.durationMs = Mathf.Max(0, recipe.durationMs);
        recipe.settleMs = Mathf.Max(0, recipe.settleMs);
        recipe.distancePx = Mathf.Max(0f, recipe.distancePx);
        recipe.scale = Mathf.Clamp(recipe.scale, 0.8f, 1.25f);
        recipe.startOpacity = Mathf.Clamp01(recipe.startOpacity);
        recipe.particles = Mathf.Clamp(recipe.particles, 0, 24);
        recipe.staggerMs = Mathf.Max(0, recipe.staggerMs);
        recipe.staggerCapMs = Mathf.Max(0, recipe.staggerCapMs);
    }
}

internal sealed class UiTargetRegistry
{
    private readonly Dictionary<string, VisualElement> _targets =
        new Dictionary<string, VisualElement>(StringComparer.Ordinal);

    public void Register(string id, VisualElement target)
    {
        if (string.IsNullOrEmpty(id) || target == null) return;
        _targets[id] = target;
        if (!id.StartsWith("card:", StringComparison.Ordinal))
            return;
        _targets[id.Substring("card:".Length)] = target;
    }

    public void Unregister(string id, VisualElement target)
    {
        if (string.IsNullOrEmpty(id) || target == null) return;
        RemoveIfSame(id, target);
        if (id.StartsWith("card:", StringComparison.Ordinal))
            RemoveIfSame(id.Substring("card:".Length), target);
    }

    public VisualElement Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_targets.TryGetValue(id, out var target) && target?.panel != null) return target;
        if (!id.StartsWith("card:", StringComparison.Ordinal) &&
            _targets.TryGetValue("card:" + id, out target) && target?.panel != null)
            return target;
        return null;
    }

    public VisualElement FirstVisible(string className)
    {
        foreach (var target in _targets.Values)
            if (target?.panel != null && target.ClassListContains(className) &&
                target.resolvedStyle.display != DisplayStyle.None)
                return target;
        return null;
    }

    private void RemoveIfSame(string id, VisualElement target)
    {
        if (_targets.TryGetValue(id, out var current) && current == target)
            _targets.Remove(id);
    }
}

/// <summary>
/// Code-native station sigil family. All five marks share the same stroke, proportions, and
/// cut-metal construction, so they remain coherent and crisp without platform-dependent emoji.
/// </summary>
internal sealed class UiStationSigil : VisualElement
{
    // Keep the code-native mark self-contained. Reading resolvedStyle.color from
    // generateVisualContent can return an unresolved StyleKeyword during the panel's first
    // geometry pass in Unity 6.3, which logs a native UIElements type error before USS settles.
    private static readonly Color StrokeColor = new Color32(197, 209, 221, 255);
    private readonly HallStation _station;

    public UiStationSigil(HallStation station)
    {
        _station = station;
        name = "sigil-vector-" + station.ToString().ToLowerInvariant();
        AddToClassList("hub-station__sigil-vector");
        AddToClassList("hub-station__sigil-vector--" + station.ToString().ToLowerInvariant());
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        var painter = context.painter2D;
        painter.strokeColor = StrokeColor;
        painter.lineWidth = Mathf.Max(1.5f, Mathf.Min(rect.width, rect.height) * 0.045f);
        Vector2 c = rect.center;
        float r = Mathf.Min(rect.width, rect.height) * 0.31f;

        switch (_station)
        {
            case HallStation.Market:
                Poly(painter, c, r, 4, 45f);
                Line(painter, c + new Vector2(0f, -r * 0.72f),
                    c + new Vector2(0f, r * 0.72f));
                Line(painter, c + new Vector2(-r * 0.38f, 0f),
                    c + new Vector2(r * 0.38f, 0f));
                break;
            case HallStation.Warband:
                Line(painter, c + new Vector2(-r * 0.55f, r), c + new Vector2(-r * 0.55f, -r));
                Path(painter,
                    c + new Vector2(-r * 0.5f, -r * 0.78f),
                    c + new Vector2(r * 0.72f, -r * 0.44f),
                    c + new Vector2(r * 0.08f, r * 0.08f),
                    c + new Vector2(-r * 0.5f, -r * 0.1f));
                Diamond(painter, c + new Vector2(-r * 0.45f, r * 0.72f), r * 0.14f);
                Diamond(painter, c + new Vector2(0f, r * 0.72f), r * 0.14f);
                Diamond(painter, c + new Vector2(r * 0.45f, r * 0.72f), r * 0.14f);
                break;
            case HallStation.Armory:
                Line(painter, c + new Vector2(-r * 0.78f, r * 0.82f),
                    c + new Vector2(r * 0.64f, -r * 0.72f));
                Line(painter, c + new Vector2(r * 0.78f, r * 0.82f),
                    c + new Vector2(-r * 0.64f, -r * 0.72f));
                Diamond(painter, c + new Vector2(r * 0.58f, -r * 0.65f), r * 0.24f);
                Diamond(painter, c + new Vector2(-r * 0.58f, -r * 0.65f), r * 0.24f);
                break;
            case HallStation.Hourstone:
                Poly(painter, c, r, 12, 15f);
                Path(painter,
                    c + new Vector2(-r * 0.48f, -r * 0.62f),
                    c + new Vector2(r * 0.48f, -r * 0.62f),
                    c + new Vector2(-r * 0.42f, r * 0.62f),
                    c + new Vector2(r * 0.42f, r * 0.62f),
                    c + new Vector2(-r * 0.48f, -r * 0.62f));
                Line(painter, c + new Vector2(-r * 0.5f, 0f),
                    c + new Vector2(r * 0.5f, 0f));
                break;
            case HallStation.Breach:
                Path(painter,
                    c + new Vector2(-r * 0.9f, -r * 0.72f),
                    c + new Vector2(-r * 0.28f, -r),
                    c + new Vector2(-r * 0.42f, r * 0.2f),
                    c + new Vector2(-r * 0.82f, r * 0.72f));
                Path(painter,
                    c + new Vector2(r * 0.9f, -r * 0.72f),
                    c + new Vector2(r * 0.28f, -r),
                    c + new Vector2(r * 0.42f, r * 0.2f),
                    c + new Vector2(r * 0.82f, r * 0.72f));
                Line(painter, c + new Vector2(0f, -r * 0.88f),
                    c + new Vector2(r * 0.28f, r * 0.64f));
                Diamond(painter, c + new Vector2(0f, r * 0.05f), r * 0.13f);
                break;
        }
    }

    private static void Diamond(Painter2D painter, Vector2 center, float radius) =>
        Poly(painter, center, radius, 4, 45f);

    private static void Poly(Painter2D painter, Vector2 center, float radius, int sides,
                             float rotationDegrees)
    {
        painter.BeginPath();
        for (int i = 0; i <= sides; i++)
        {
            float angle = (rotationDegrees + i * 360f / sides) * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }
        painter.Stroke();
    }

    private static void Line(Painter2D painter, Vector2 from, Vector2 to)
    {
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }

    private static void Path(Painter2D painter, params Vector2[] points)
    {
        if (points == null || points.Length < 2) return;
        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
        painter.Stroke();
    }
}

/// <summary>
/// A single retained-mode overlay for selection rings and meaningful transfers. Effects snapshot
/// target geometry when fired, but are still scoped to the current layout: a navigation boundary
/// clears them so stale geometry can never fly over the destination screen. No effect owns a
/// GameObject or mutates the run.
/// </summary>
internal sealed class UiFxLayer : VisualElement
{
    private enum EffectKind { Pulse, Transfer, Acquire, Wipe, Error }

    private sealed class Effect
    {
        public EffectKind Kind;
        public Rect Source;
        public Rect Target;
        public float Start;
        public float Duration;
        public int Particles;
        public Color Color;
    }

    private readonly HubPresentationConfig _config;
    private readonly List<Effect> _effects = new List<Effect>();
    private IVisualElementScheduledItem _ticker;

    internal int ActiveEffectCount => _effects.Count;

    public UiFxLayer(HubPresentationConfig config)
    {
        _config = config;
        name = "ui-fx-layer";
        AddToClassList("ui-fx-layer");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        _ticker = schedule.Execute(Tick).Every(16);
        _ticker.Pause();
    }

    public void Pulse(VisualElement target, UiFeedbackTone tone, UiMotionRecipe recipe)
    {
        if (!CanPlay(target, recipe)) return;
        Add(new Effect
        {
            Kind = EffectKind.Pulse,
            Source = LocalRect(target),
            Target = LocalRect(target),
            Start = Time.unscaledTime,
            Duration = Seconds(recipe.durationMs + recipe.settleMs),
            Particles = recipe.particles,
            Color = ToneColor(tone),
        });
    }

    public void Transfer(VisualElement source, VisualElement target, UiFeedbackTone tone,
                         UiMotionRecipe recipe, int delayMs = 0)
    {
        if (!CanPlay(source, recipe) || !CanPlay(target, recipe)) return;
        Add(new Effect
        {
            Kind = EffectKind.Transfer,
            Source = LocalRect(source),
            Target = LocalRect(target),
            Start = Time.unscaledTime + Mathf.Max(0, delayMs) / 1000f,
            Duration = Seconds(recipe.durationMs),
            Particles = recipe.particles,
            Color = ToneColor(tone),
        });
    }

    public void Error(VisualElement target, UiMotionRecipe recipe)
    {
        if (!CanPlay(target, recipe)) return;
        Add(new Effect
        {
            Kind = EffectKind.Error,
            Source = LocalRect(target),
            Target = LocalRect(target),
            Start = Time.unscaledTime,
            Duration = Seconds(recipe.durationMs + recipe.settleMs),
            Particles = recipe.particles,
            Color = _config.fx.negativeColor,
        });
    }

    public void Wipe(VisualElement target, UiFeedbackTone tone, UiMotionRecipe recipe)
    {
        if (!CanPlay(target, recipe)) return;
        Add(new Effect
        {
            Kind = EffectKind.Wipe,
            Source = LocalRect(target),
            Target = LocalRect(target),
            Start = Time.unscaledTime,
            Duration = Seconds(recipe.durationMs),
            Particles = recipe.particles,
            Color = ToneColor(tone),
        });
    }

    public void Acquire(VisualElement source, VisualElement target, UiFeedbackTone tone,
                        UiMotionRecipe recipe, int delayMs = 0)
    {
        if (!CanPlay(source, recipe) || !CanPlay(target, recipe)) return;
        Add(new Effect
        {
            Kind = EffectKind.Acquire,
            Source = LocalRect(source),
            Target = LocalRect(target),
            Start = Time.unscaledTime + Mathf.Max(0, delayMs) / 1000f,
            Duration = Seconds(recipe.durationMs + recipe.settleMs),
            Particles = recipe.particles,
            Color = ToneColor(tone),
        });
    }

    public void ClearEffects()
    {
        _effects.Clear();
        _ticker?.Pause();
        MarkDirtyRepaint();
    }

    private bool CanPlay(VisualElement target, UiMotionRecipe recipe) =>
        _config.effectsEnabled && target != null && target.panel != null &&
        target.resolvedStyle.display != DisplayStyle.None &&
        target.resolvedStyle.visibility == Visibility.Visible &&
        !float.IsNaN(target.resolvedStyle.width) && !float.IsInfinity(target.resolvedStyle.width) &&
        !float.IsNaN(target.resolvedStyle.height) && !float.IsInfinity(target.resolvedStyle.height) &&
        target.resolvedStyle.width > 3f && target.resolvedStyle.height > 3f &&
        recipe != null && recipe.durationMs > 0;

    private void Add(Effect effect)
    {
        while (_effects.Count >= _config.fx.maxEffects) _effects.RemoveAt(0);
        _effects.Add(effect);
        _ticker?.Resume();
        MarkDirtyRepaint();
    }

    private void Tick()
    {
        float now = Time.unscaledTime;
        for (int i = _effects.Count - 1; i >= 0; i--)
            if (now - _effects[i].Start >= _effects[i].Duration)
                _effects.RemoveAt(i);
        MarkDirtyRepaint();
        if (_effects.Count == 0) _ticker?.Pause();
    }

    private void Draw(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        float now = Time.unscaledTime;
        foreach (var effect in _effects)
        {
            if (now < effect.Start) continue;
            float t = Mathf.Clamp01((now - effect.Start) / Mathf.Max(0.001f, effect.Duration));
            switch (effect.Kind)
            {
                case EffectKind.Transfer:
                    DrawTransfer(painter, effect, t);
                    break;
                case EffectKind.Error:
                    DrawPulse(painter, effect.Target, effect.Color, t, 1.5f);
                    break;
                case EffectKind.Wipe:
                    DrawWipe(painter, effect.Target, effect.Color, t, effect.Particles);
                    break;
                case EffectKind.Acquire:
                    DrawAcquisition(painter, effect, t);
                    break;
                default:
                    DrawPulse(painter, effect.Target, effect.Color, t, 1f);
                    break;
            }
        }
    }

    private void DrawPulse(Painter2D painter, Rect rect, Color color, float t, float weight)
    {
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float expansion = Mathf.Lerp(1f, 13f * weight, eased);
        var pulse = new Rect(rect.xMin - expansion, rect.yMin - expansion,
            rect.width + expansion * 2f, rect.height + expansion * 2f);
        color.a *= 1f - eased;
        painter.strokeColor = color;
        painter.lineWidth = _config.fx.lineWidth * weight;
        DrawBrackets(painter, pulse, Mathf.Clamp(Mathf.Min(pulse.width, pulse.height) * 0.17f,
            8f, 24f));
        if (t < 0.52f)
        {
            Color echo = color;
            echo.a *= 0.44f;
            painter.strokeColor = echo;
            painter.lineWidth = Mathf.Max(1f, _config.fx.lineWidth * 0.65f);
            float inset = 5f + eased * 3f;
            DrawBrackets(painter, new Rect(pulse.x + inset, pulse.y + inset,
                Mathf.Max(1f, pulse.width - inset * 2f),
                Mathf.Max(1f, pulse.height - inset * 2f)), 7f);
        }

        int grains = Mathf.Clamp((int)(4f * weight), 2, 8);
        for (int i = 0; i < grains; i++)
        {
            float angle = i * Mathf.PI * 2f / grains + eased * 0.7f;
            Vector2 radius = new Vector2(pulse.width * 0.5f, pulse.height * 0.5f);
            Vector2 point = pulse.center + new Vector2(Mathf.Cos(angle) * radius.x,
                Mathf.Sin(angle) * radius.y);
            Color grain = color;
            grain.a *= 0.72f;
            DrawGrain(painter, point, _config.fx.grainSize * (0.55f + weight * 0.18f), grain);
        }
    }

    private void DrawTransfer(Painter2D painter, Effect effect, float t)
    {
        Vector2 a = effect.Source.center;
        Vector2 b = effect.Target.center;
        Vector2 mid = Vector2.Lerp(a, b, 0.5f) + Vector2.up * -_config.fx.arcHeight;

        Color trail = effect.Color;
        trail.a *= _config.fx.trailOpacity * Mathf.Sin(Mathf.PI * t);
        painter.strokeColor = trail;
        painter.lineWidth = _config.fx.lineWidth;
        painter.BeginPath();
        for (int i = 0; i <= 18; i++)
        {
            float p = i / 18f;
            Vector2 point = Quadratic(a, mid, b, p);
            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }
        painter.Stroke();

        Vector2 tangent = (b - a).normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x) * 3.5f;
        Color echo = trail;
        echo.a *= 0.35f;
        painter.strokeColor = echo;
        painter.lineWidth = Mathf.Max(1f, _config.fx.lineWidth * 0.6f);
        painter.BeginPath();
        for (int i = 0; i <= 12; i++)
        {
            float p = i / 12f;
            Vector2 point = Quadratic(a, mid, b, p) + normal * Mathf.Sin(Mathf.PI * p);
            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }
        painter.Stroke();

        int grains = Mathf.Max(1, effect.Particles);
        for (int i = 0; i < grains; i++)
        {
            float offset = i / (float)grains * 0.28f;
            float p = Mathf.Clamp01(t - offset);
            Vector2 point = Quadratic(a, mid, b, p);
            float wobble = Mathf.Sin((p * 17f + i * 2.37f) * Mathf.PI) *
                           _config.fx.grainSize * 1.2f;
            point += new Vector2(wobble, -wobble * 0.35f);
            Color grain = effect.Color;
            grain.a *= Mathf.SmoothStep(0f, 1f, p) * (1f - Mathf.Clamp01((p - 0.88f) / 0.12f));
            DrawGrain(painter, point, _config.fx.grainSize, grain);
        }

        if (t > 0.68f)
            DrawPulse(painter, effect.Target, effect.Color, (t - 0.68f) / 0.32f, 0.8f);
    }

    private void DrawWipe(Painter2D painter, Rect rect, Color color, float t, int particles)
    {
        float eased = t * t * (3f - 2f * t);
        float x = Mathf.Lerp(rect.xMin - 18f, rect.xMax + 18f, eased);
        Color edge = color;
        edge.a *= Mathf.Sin(Mathf.PI * t) * 0.76f;
        painter.strokeColor = edge;
        painter.lineWidth = _config.fx.lineWidth * 1.35f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(x - 12f, rect.yMin));
        painter.LineTo(new Vector2(x + 12f, rect.yMax));
        painter.Stroke();

        int grains = Mathf.Clamp(particles, 3, 12);
        for (int i = 0; i < grains; i++)
        {
            float p = (i + 0.5f) / grains;
            Vector2 point = new Vector2(x + Mathf.Sin(i * 2.1f) * 9f,
                Mathf.Lerp(rect.yMin, rect.yMax, p));
            Color grain = edge;
            grain.a *= 0.65f;
            DrawGrain(painter, point, _config.fx.grainSize, grain);
        }
    }

    private void DrawAcquisition(Painter2D painter, Effect effect, float t)
    {
        float hold = Mathf.Clamp01(t / 0.24f);
        float travel = Mathf.Clamp01((t - 0.20f) / 0.80f);
        float eased = 1f - Mathf.Pow(1f - travel, 3f);
        Vector2 center = Quadratic(effect.Source.center,
            Vector2.Lerp(effect.Source.center, effect.Target.center, 0.5f) + Vector2.up * -42f,
            effect.Target.center, eased);
        Vector2 startSize = effect.Source.size;
        Vector2 endSize = new Vector2(
            Mathf.Clamp(effect.Target.width * 0.34f, 28f, 58f),
            Mathf.Clamp(effect.Target.height * 0.34f, 28f, 58f));
        Vector2 size = Vector2.Lerp(startSize, endSize, eased);
        var ghost = new Rect(center - size * 0.5f, size);
        Color frame = effect.Color;
        frame.a *= (1f - Mathf.Clamp01((t - 0.82f) / 0.18f)) * (0.40f + hold * 0.50f);
        painter.strokeColor = frame;
        painter.lineWidth = _config.fx.lineWidth * 1.35f;
        DrawBrackets(painter, ghost, Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.18f, 7f, 22f));

        if (t < 0.42f)
        {
            Color stamp = frame;
            stamp.a *= Mathf.SmoothStep(0f, 1f, hold);
            painter.strokeColor = stamp;
            painter.lineWidth = _config.fx.lineWidth;
            float slash = Mathf.Min(ghost.width, ghost.height) * 0.30f;
            painter.BeginPath();
            painter.MoveTo(ghost.center + new Vector2(-slash, slash * 0.45f));
            painter.LineTo(ghost.center + new Vector2(slash, -slash * 0.45f));
            painter.Stroke();
            DrawGrain(painter, ghost.center, _config.fx.grainSize * 1.4f, stamp);
        }

        int grains = Mathf.Clamp(effect.Particles, 4, 12);
        for (int i = 0; i < grains; i++)
        {
            float angle = i * Mathf.PI * 2f / grains + t * 2.4f;
            Vector2 point = ghost.center + new Vector2(Mathf.Cos(angle) * ghost.width * 0.46f,
                Mathf.Sin(angle) * ghost.height * 0.46f);
            Color grain = frame;
            grain.a *= 0.74f;
            DrawGrain(painter, point, _config.fx.grainSize, grain);
        }

        if (t > 0.72f)
            DrawPulse(painter, effect.Target, effect.Color, (t - 0.72f) / 0.28f, 0.7f);
    }

    private static void DrawGrain(Painter2D painter, Vector2 point, float size, Color color)
    {
        painter.strokeColor = color;
        painter.lineWidth = Mathf.Max(1f, size * 0.7f);
        painter.BeginPath();
        painter.MoveTo(point + new Vector2(-size, 0f));
        painter.LineTo(point + new Vector2(0f, -size));
        painter.LineTo(point + new Vector2(size, 0f));
        painter.LineTo(point + new Vector2(0f, size));
        painter.LineTo(point + new Vector2(-size, 0f));
        painter.Stroke();
    }

    private static void StrokeRect(Painter2D painter, Rect rect)
    {
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMin));
        painter.LineTo(new Vector2(rect.xMax, rect.yMax));
        painter.LineTo(new Vector2(rect.xMin, rect.yMax));
        painter.LineTo(new Vector2(rect.xMin, rect.yMin));
        painter.Stroke();
    }

    private static void DrawBrackets(Painter2D painter, Rect rect, float length)
    {
        length = Mathf.Min(length, Mathf.Min(rect.width, rect.height) * 0.45f);
        Path(painter, new Vector2(rect.xMin, rect.yMin + length),
            new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin + length, rect.yMin));
        Path(painter, new Vector2(rect.xMax - length, rect.yMin),
            new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMin + length));
        Path(painter, new Vector2(rect.xMin, rect.yMax - length),
            new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin + length, rect.yMax));
        Path(painter, new Vector2(rect.xMax - length, rect.yMax),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMax - length));
    }

    private static void Path(Painter2D painter, params Vector2[] points)
    {
        if (points == null || points.Length < 2) return;
        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
        painter.Stroke();
    }

    private Rect LocalRect(VisualElement target)
    {
        Vector2 min = target.ChangeCoordinatesTo(this, Vector2.zero);
        Vector2 max = target.ChangeCoordinatesTo(this,
            new Vector2(target.resolvedStyle.width, target.resolvedStyle.height));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Color ToneColor(UiFeedbackTone tone) =>
        tone == UiFeedbackTone.Sand ? _config.fx.sandColor :
        tone == UiFeedbackTone.Positive ? _config.fx.positiveColor :
        tone == UiFeedbackTone.Negative ? _config.fx.negativeColor :
        _config.fx.previewColor;

    private static Vector2 Quadratic(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float Seconds(int milliseconds) => Mathf.Max(0.001f, milliseconds / 1000f);
}

internal interface IUiHaptics
{
    void Play(UiFeedbackEvent feedback);
}

internal interface IUiAudioFeedback
{
    void Play(UiFeedbackEvent feedback);
}

internal sealed class NullUiHaptics : IUiHaptics
{
    public void Play(UiFeedbackEvent feedback) { }
}

internal sealed class NullUiAudioFeedback : IUiAudioFeedback
{
    public void Play(UiFeedbackEvent feedback) { }
}

/// <summary>
/// Interruption-safe motion and semantic feedback director. Every operation is generation-guarded:
/// rapid pointer or route input can invalidate stale scheduled phases, and cleanup always settles
/// the target back onto USS-authored state.
/// </summary>
internal sealed class UiFeedbackDirector : IDisposable
{
    private readonly VisualElement _root;
    private readonly HubPresentationConfig _config;
    private readonly UiFxLayer _fx;
    private readonly UiTargetRegistry _targets = new UiTargetRegistry();
    private readonly Dictionary<VisualElement, int> _generations =
        new Dictionary<VisualElement, int>();
    private readonly Dictionary<Label, int> _countGenerations =
        new Dictionary<Label, int>();
    private readonly HashSet<VisualElement> _interactables = new HashSet<VisualElement>();
    private readonly HashSet<VisualElement> _registeredTargets = new HashSet<VisualElement>();
    private readonly IUiHaptics _haptics;
    private readonly IUiAudioFeedback _audio;
    private bool _reducedMotion;
    private bool _active = true;
    private bool _disposed;

    public UiFeedbackDirector(VisualElement root, HubPresentationConfig config, UiFxLayer fx,
                              IUiHaptics haptics = null, IUiAudioFeedback audio = null)
    {
        _root = root;
        _config = config;
        _fx = fx;
        _haptics = haptics ?? new NullUiHaptics();
        _audio = audio ?? new NullUiAudioFeedback();
        UiPolishSignals.Emitted += OnFeedback;
        UiPolishSignals.PreviewRequested += Preview;
        UiPolishSignals.TransactionPreviewRequested += PreviewTransaction;
    }

    public void SetReducedMotion(bool reduced) => _reducedMotion = reduced;

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (!active) CancelPresentation();
    }

    /// <summary>
    /// End presentation tied to the outgoing layout before a route swaps visible geography.
    /// Resource transfers may snapshot geometry, but they must never survive into another screen.
    /// </summary>
    public void BeginLayoutTransition() => _fx?.ClearEffects();

    public void CancelPresentation()
    {
        _fx?.ClearEffects();
        foreach (VisualElement target in _registeredTargets)
        {
            Next(target);
            ClearInlineMotion(target);
        }
        foreach (VisualElement target in _interactables)
        {
            Next(target);
            ClearInlineMotion(target);
        }
    }

    public void RegisterTarget(string id, VisualElement target)
    {
        _targets.Register(Normalize(id), target);
        if (target != null) _registeredTargets.Add(target);
    }

    public void UnregisterTarget(string id, VisualElement target) =>
        _targets.Unregister(Normalize(id), target);

    public void AttachInteractable(VisualElement target, Func<string> id)
    {
        if (target == null || !_interactables.Add(target)) return;
        target.usageHints |= UsageHints.DynamicTransform;
        target.AddManipulator(new UiInteractableFeedbackManipulator(this, id));
    }

    public void Hover(VisualElement target, bool active)
    {
        if (!_config.effectsEnabled || target == null) return;
        if (!active)
        {
            Settle(target, Next(target), _config.hover.settleMs);
            return;
        }
        ApplyState(target, _config.hover, -_config.hover.distancePx,
            _config.hover.scale, 1f);
    }

    public void Preview(string id)
    {
        if (!_config.effectsEnabled || string.IsNullOrEmpty(id)) return;
        UiPolishSignals.Emit(UiPolishSignals.Cue.Preview, targetId: id,
            tone: UiFeedbackTone.Preview);
    }

    public void Focus(VisualElement target, bool active)
    {
        if (!_config.effectsEnabled || target == null) return;
        if (!active)
        {
            Settle(target, Next(target), _config.focus.settleMs);
            return;
        }
        ApplyState(target, _config.focus, -_config.focus.distancePx,
            _config.focus.scale, 1f);
        if (!_reducedMotion) _fx.Pulse(target, UiFeedbackTone.Preview, _config.focus);
    }

    public void Press(VisualElement target, bool active)
    {
        if (!_config.effectsEnabled || target == null) return;
        if (!active)
        {
            Settle(target, Next(target), _config.press.settleMs);
            return;
        }
        ApplyState(target, _config.press, 0f, _config.press.scale, 1f);
    }

    public void Reveal(VisualElement target, int index = 0, int direction = 1)
    {
        Reveal(target, _config.reveal, index, direction);
    }

    public void Reveal(VisualElement target, UiMotionRecipe recipe, int index = 0,
                       int direction = 1)
    {
        if (!_config.effectsEnabled || target == null) return;
        recipe ??= _config.reveal;
        int generation = Next(target);
        int delay = _reducedMotion ? 0 :
            Mathf.Min(index * recipe.staggerMs, recipe.staggerCapMs);
        ClearTransitions(target);
        target.style.opacity = recipe.startOpacity;
        target.style.translate = _reducedMotion
            ? new Translate(0f, 0f)
            : new Translate(0f, recipe.distancePx * Mathf.Sign(direction));
        target.style.scale = _reducedMotion
            ? new Scale(Vector2.one)
            : new Scale(new Vector2(recipe.scale, recipe.scale));

        target.schedule.Execute(() =>
        {
            if (!Current(target, generation)) return;
            int duration = _reducedMotion ? _config.reducedFadeMs : recipe.durationMs;
            ConfigureTransition(target, duration, EasingMode.EaseOut);
            target.style.opacity = 1f;
            target.style.translate = new Translate(0f, 0f);
            target.style.scale = new Scale(Vector2.one);
            target.schedule.Execute(() =>
            {
                if (Current(target, generation)) ClearInlineMotion(target);
            }).ExecuteLater(duration + recipe.settleMs);
        }).ExecuteLater(delay + 16);
    }

    public void RevealBatch(IReadOnlyList<VisualElement> targets, int direction = 1)
    {
        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++) Reveal(targets[i], i, direction);
    }

    public void Select(VisualElement target)
    {
        if (!_config.effectsEnabled || target == null) return;
        Punch(target, _config.select);
        if (!_reducedMotion) _fx.Pulse(target, UiFeedbackTone.Preview, _config.select);
    }

    public void Attention(VisualElement target)
    {
        if (!_config.effectsEnabled || target == null) return;
        Punch(target, _config.attention);
        if (!_reducedMotion) _fx.Pulse(target, UiFeedbackTone.Sand, _config.attention);
    }

    public void Route(VisualElement target)
    {
        if (!_config.effectsEnabled || target == null) return;
        Punch(target, _config.route);
        if (_reducedMotion) ReducedFlash(target, UiFeedbackTone.Sand);
        else _fx.Pulse(target, UiFeedbackTone.Sand, _config.route);
    }

    public void Commit(VisualElement source, VisualElement target, UiFeedbackTone tone,
                       UiMotionRecipe recipe = null)
    {
        if (!_config.effectsEnabled) return;
        recipe ??= _config.commit;
        if (source != null) Punch(source, recipe);
        if (target != null) Punch(target, recipe);
        if (_reducedMotion)
        {
            if (target != null) ReducedFlash(target, tone);
        }
        else if (source != null && target != null)
        {
            _fx.Transfer(source, target, tone, recipe);
        }
        else if (target != null)
        {
            _fx.Pulse(target, tone, recipe);
        }
    }

    public void Error(VisualElement target)
    {
        if (!_config.effectsEnabled || target == null) return;
        if (_reducedMotion)
        {
            ReducedFlash(target, UiFeedbackTone.Negative);
            return;
        }

        int generation = Next(target);
        ConfigureTransition(target, Mathf.Max(1, _config.error.durationMs / 3),
            EasingMode.EaseInOut);
        target.style.translate = new Translate(-_config.error.distancePx, 0f);
        target.schedule.Execute(() =>
        {
            if (!Current(target, generation)) return;
            target.style.translate = new Translate(_config.error.distancePx, 0f);
        }).ExecuteLater(Mathf.Max(1, _config.error.durationMs / 3));
        target.schedule.Execute(() =>
        {
            if (!Current(target, generation)) return;
            Settle(target, generation, _config.error.settleMs);
        }).ExecuteLater(Mathf.Max(2, _config.error.durationMs * 2 / 3));
        _fx.Error(target, _config.error);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UiPolishSignals.Emitted -= OnFeedback;
        UiPolishSignals.PreviewRequested -= Preview;
        UiPolishSignals.TransactionPreviewRequested -= PreviewTransaction;
        _fx?.ClearEffects();
    }

    private void OnFeedback(UiFeedbackEvent feedback)
    {
        if (_disposed || !_active || !_config.effectsEnabled) return;
        // Capture click and transaction geometry at the authoritative boundary. Deferring every
        // cue until after Rebuild made navigation effects resolve against the destination layout,
        // which produced arcs from fallback geometry after the screen had already changed.
        if (feedback.Cue != UiPolishSignals.Cue.Error)
        {
            Handle(feedback);
            return;
        }

        // Error copy becomes visible during the following bind, so this one cue deliberately
        // resolves afterward. It has no source-to-target transfer and cannot leak route geometry.
        _root.schedule.Execute(() => Handle(feedback)).ExecuteLater(1);
    }

    private void Handle(UiFeedbackEvent feedback)
    {
        if (_disposed || _root.panel == null) return;
        VisualElement source = _targets.Find(Normalize(feedback.SourceId));
        VisualElement target = VisibleTarget(feedback.TargetId);
        VisualElement resource = VisibleTarget(feedback.ResourceId);

        switch (feedback.Cue)
        {
            case UiPolishSignals.Cue.Preview:
                break;
            case UiPolishSignals.Cue.Select:
            case UiPolishSignals.Cue.Tab:
                Select(source ??
                       _targets.FirstVisible("muster-card--selected") ??
                       _targets.FirstVisible("market-offer-card--selected") ??
                       _targets.FirstVisible("wb-card--selected"));
                break;
            case UiPolishSignals.Cue.TooltipReveal:
                // The tooltip service owns placement/fade so it can clamp after wrapped layout.
                // This semantic cue still reaches audio/haptic adapters and preview tooling.
                break;
            case UiPolishSignals.Cue.TooltipDismiss:
                break;
            case UiPolishSignals.Cue.Pin:
                Punch(target ?? source, _config.interactions.pin);
                if (!_reducedMotion)
                    _fx.Pulse(target ?? source, UiFeedbackTone.Preview,
                        _config.interactions.pin);
                break;
            case UiPolishSignals.Cue.Unpin:
                Punch(target ?? source, _config.interactions.unpin);
                break;
            case UiPolishSignals.Cue.DrawerExpand:
                Reveal(target ?? source, _config.interactions.drawerExpand);
                break;
            case UiPolishSignals.Cue.DrawerCollapse:
                Reveal(target ?? source, _config.interactions.drawerCollapse, direction: -1);
                break;
            case UiPolishSignals.Cue.SocketWake:
                Punch(target ?? source, _config.interactions.socketWake);
                if (!_reducedMotion)
                    _fx.Pulse(target ?? source, UiFeedbackTone.Positive,
                        _config.interactions.socketWake);
                break;
            case UiPolishSignals.Cue.ProjectedTarget:
                Commit(source, target ?? source, UiFeedbackTone.Positive,
                    _config.interactions.projectedTarget);
                break;
            case UiPolishSignals.Cue.Route:
                // Navigation is not a resource transfer. Confirm the destination and let the
                // route transition carry the eye; bolts are reserved for stable-layout commits.
                Route(target ?? source ?? _targets.Find("hub-workspace"));
                break;
            case UiPolishSignals.Cue.Attention:
                Attention(target ?? source ?? _targets.Find("station-market"));
                break;
            case UiPolishSignals.Cue.Error:
                Error(target ?? source ?? _targets.Find("hub-workspace"));
                break;
            case UiPolishSignals.Cue.Purchase:
                Purchase(resource, source, target, feedback.Amount, feedback.Tone,
                    TransactionRecipe(feedback.Transaction));
                break;
            case UiPolishSignals.Cue.Reroll:
                if (resource is Label rerollLedger) Count(rerollLedger, feedback.Amount);
                if (resource != null) Punch(resource, _config.commit);
                if (source != null) Punch(source, _config.select);
                if (!_reducedMotion)
                    _fx.Wipe(target ?? _targets.Find("hub-workspace"),
                        UiFeedbackTone.Sand, _config.reveal);
                break;
            case UiPolishSignals.Cue.Reward:
            case UiPolishSignals.Cue.RankUp:
            case UiPolishSignals.Cue.Confirm:
            case UiPolishSignals.Cue.Result:
                Commit(source, target ?? source, feedback.Tone,
                    TransactionRecipe(feedback.Transaction));
                break;
        }

        _audio.Play(feedback);
        _haptics.Play(feedback);
    }

    private void Purchase(VisualElement resource, VisualElement source, VisualElement target,
                          int amount, UiFeedbackTone tone, UiMotionRecipe recipe)
    {
        recipe ??= _config.commit;
        if (resource is Label ledger) Count(ledger, amount);
        if (resource != null) Punch(resource, recipe);
        if (source != null) Punch(source, recipe);
        if (target != null) Punch(target, recipe);
        if (_reducedMotion)
        {
            if (source != null) ReducedFlash(source, tone);
            if (target != null) ReducedFlash(target, tone);
            return;
        }

        if (resource != null && source != null)
            _fx.Transfer(resource, source, UiFeedbackTone.Sand, recipe);
        if (source != null && target != null)
            _fx.Acquire(source, target, tone, recipe,
                Mathf.Min(120, Mathf.Max(55, recipe.durationMs / 4)));
        else if (target != null)
            _fx.Pulse(target, tone, recipe);
    }

    private void Count(Label label, int amount)
    {
        if (label == null || amount == 0) return;
        string text = label.text ?? "";
        int space = text.IndexOf(' ');
        string number = space >= 0 ? text.Substring(0, space) : text;
        string suffix = space >= 0 ? text.Substring(space) : "";
        if (!int.TryParse(number, out int start)) return;
        int end = start + amount;
        int generation = _countGenerations.TryGetValue(label, out int current)
            ? current + 1
            : 1;
        _countGenerations[label] = generation;
        float started = Time.unscaledTime;
        float duration = Mathf.Clamp(Mathf.Abs(amount) * 0.055f, 0.20f, 0.38f);
        IVisualElementScheduledItem ticker = null;
        ticker = label.schedule.Execute(() =>
        {
            if (!_countGenerations.TryGetValue(label, out int live) || live != generation)
            {
                ticker?.Pause();
                return;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - started) / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            label.text = Mathf.RoundToInt(Mathf.Lerp(start, end, eased)) + suffix;
            if (t < 1f) return;
            label.text = end + suffix;
            ticker?.Pause();
        }).Every(16);
    }

    private VisualElement VisibleTarget(string id)
    {
        string normalized = Normalize(id);
        VisualElement target = _targets.Find(normalized);
        if (Visible(target)) return target;
        if (normalized.StartsWith("station-", StringComparison.Ordinal))
        {
            VisualElement anchor = _targets.Find("anchor-" +
                normalized.Substring("station-".Length));
            if (Visible(anchor)) return anchor;
        }
        return target;
    }

    private static bool Visible(VisualElement target) =>
        target != null && target.panel != null &&
        target.resolvedStyle.display != DisplayStyle.None &&
        target.resolvedStyle.visibility == Visibility.Visible;

    private void Preview(UiPolishSignals.Cue cue)
    {
        if (_disposed || !_active) return;
        switch (cue)
        {
            case UiPolishSignals.Cue.Preview:
                VisualElement preview = _targets.Find("station-market") ??
                                        _targets.FirstVisible("muster-card") ??
                                        _targets.FirstVisible("market-offer-card") ??
                                        _targets.FirstVisible("wb-card");
                Focus(preview, true);
                if (preview != null)
                    preview.schedule.Execute(() => Focus(preview, false)).ExecuteLater(180);
                break;
            case UiPolishSignals.Cue.Reveal:
                var visible = new List<VisualElement>();
                foreach (string id in new[]
                         {
                             "station-breach", "station-market", "station-hourstone",
                             "station-armory", "station-warband",
                         })
                {
                    VisualElement station = _targets.Find(id);
                    if (station != null && station.resolvedStyle.display != DisplayStyle.None)
                        visible.Add(station);
                }
                if (visible.Count == 0)
                {
                    VisualElement card = _targets.FirstVisible("muster-card") ??
                                         _targets.FirstVisible("market-offer-card") ??
                                         _targets.FirstVisible("wb-card");
                    if (card != null) visible.Add(card);
                }
                RevealBatch(visible);
                break;
            case UiPolishSignals.Cue.Select:
                Select(_targets.FirstVisible("muster-card--selected") ??
                       _targets.FirstVisible("market-offer-card--selected") ??
                       _targets.FirstVisible("wb-card--selected") ??
                       _targets.Find("station-hourstone"));
                break;
            case UiPolishSignals.Cue.TooltipReveal:
                Focus(_targets.Find("workbench-dossier"), true);
                break;
            case UiPolishSignals.Cue.TooltipDismiss:
                Focus(_targets.Find("workbench-dossier"), false);
                break;
            case UiPolishSignals.Cue.Pin:
                Punch(_targets.Find("workbench-dossier"), _config.interactions.pin);
                break;
            case UiPolishSignals.Cue.Unpin:
                Punch(_targets.Find("workbench-dossier"), _config.interactions.unpin);
                break;
            case UiPolishSignals.Cue.DrawerExpand:
                Reveal(_targets.Find("workbench-armory"),
                    _config.interactions.drawerExpand);
                break;
            case UiPolishSignals.Cue.DrawerCollapse:
                Reveal(_targets.Find("workbench-market"),
                    _config.interactions.drawerCollapse, direction: -1);
                break;
            case UiPolishSignals.Cue.SocketWake:
                Attention(_targets.Find("workbench-dossier"));
                break;
            case UiPolishSignals.Cue.ProjectedTarget:
                Commit(null, _targets.Find("workbench-dossier"),
                    UiFeedbackTone.Positive, _config.interactions.projectedTarget);
                break;
            case UiPolishSignals.Cue.Route:
                Route(_targets.Find("station-market"));
                break;
            case UiPolishSignals.Cue.Attention:
                Attention(_targets.Find("station-market"));
                break;
            case UiPolishSignals.Cue.Error:
                Error(_targets.Find("hub-workspace"));
                break;
            case UiPolishSignals.Cue.Purchase:
            case UiPolishSignals.Cue.Reward:
            case UiPolishSignals.Cue.Confirm:
                Commit(_targets.Find("ledger-sand"), _targets.Find("station-warband"),
                    UiFeedbackTone.Sand);
                break;
            case UiPolishSignals.Cue.Reroll:
                _fx.Wipe(_targets.Find("hub-workspace"), UiFeedbackTone.Sand, _config.reveal);
                break;
        }
    }

    private void PreviewTransaction(UiTransactionKind transaction)
    {
        if (_disposed || !_active || transaction == UiTransactionKind.None) return;
        UiMotionRecipe recipe = TransactionRecipe(transaction);
        if (transaction == UiTransactionKind.MusterSelect ||
            transaction == UiTransactionKind.MusterDeselect)
        {
            VisualElement card = _targets.FirstVisible("muster-card--selected") ??
                                 _targets.FirstVisible("muster-card");
            VisualElement slot = _targets.FirstVisible("muster-slot--filled") ??
                                 _targets.FirstVisible("muster-slot");
            Commit(transaction == UiTransactionKind.MusterSelect ? card : slot,
                transaction == UiTransactionKind.MusterSelect ? slot : card,
                transaction == UiTransactionKind.MusterSelect
                    ? UiFeedbackTone.Positive
                    : UiFeedbackTone.Preview,
                recipe);
            return;
        }
        VisualElement ledger = _targets.Find("ledger-sand");
        VisualElement source = _targets.FirstVisible("market-offer-card--selected") ??
                               _targets.FirstVisible("market-offer-card") ??
                               _targets.FirstVisible("wb-card--selected") ??
                               _targets.Find("station-market");
        VisualElement target =
            transaction == UiTransactionKind.BindInscription
                ? VisibleTarget("station-hourstone") :
            transaction == UiTransactionKind.BuyWeapon ||
            transaction == UiTransactionKind.BuyTrinket ||
            transaction == UiTransactionKind.Equip ||
            transaction == UiTransactionKind.Reforge
                ? VisibleTarget("station-armory") :
                VisibleTarget("station-warband");
        UiFeedbackTone tone =
            transaction == UiTransactionKind.Reforge ||
            transaction == UiTransactionKind.RankChoice ||
            transaction == UiTransactionKind.BindInscription
                ? UiFeedbackTone.Major
                : UiFeedbackTone.Positive;

        if (transaction == UiTransactionKind.Equip ||
            transaction == UiTransactionKind.RankChoice ||
            transaction == UiTransactionKind.Reforge)
            // Flow Lab must preview anywhere in the Hall. Real equip/rank/forge events carry
            // concrete endpoints; the non-mutating preview pulses the visible specimen instead
            // of depending on an Armory or blocking-choice target that may not exist here.
            Commit(null, source ?? target, tone, recipe);
        else
            Purchase(ledger, source, target, 0, tone, recipe);
    }

    private UiMotionRecipe TransactionRecipe(UiTransactionKind transaction)
    {
        UiTransactionRecipes recipes = _config.transactions;
        if (recipes == null) return _config.commit;
        return transaction switch
        {
            UiTransactionKind.BuyRecruit => recipes.recruit,
            UiTransactionKind.BuyRank => recipes.rank,
            UiTransactionKind.BuyWeapon => recipes.weapon,
            UiTransactionKind.BuyTrinket => recipes.trinket,
            UiTransactionKind.BindInscription => recipes.inscription,
            UiTransactionKind.BuyCapacity => recipes.capacity,
            UiTransactionKind.RankChoice => recipes.rankChoice,
            UiTransactionKind.Equip => recipes.equip,
            UiTransactionKind.Reforge => recipes.reforge,
            UiTransactionKind.MusterSelect => _config.muster.select,
            UiTransactionKind.MusterDeselect => _config.muster.deselect,
            _ => _config.commit,
        };
    }

    private void Punch(VisualElement target, UiMotionRecipe recipe)
    {
        if (target == null || recipe == null) return;
        int generation = Next(target);
        int duration = _reducedMotion ? _config.reducedFadeMs : recipe.durationMs;
        ConfigureTransition(target, Mathf.Max(1, duration / 2), EasingMode.EaseOut);
        target.style.scale = new Scale(_reducedMotion
            ? Vector2.one
            : new Vector2(recipe.scale, recipe.scale));
        target.style.translate = new Translate(0f,
            _reducedMotion ? 0f : -recipe.distancePx * 0.35f);
        target.schedule.Execute(() =>
        {
            if (Current(target, generation)) Settle(target, generation, recipe.settleMs);
        }).ExecuteLater(Mathf.Max(1, duration / 2));
    }

    private void ApplyState(VisualElement target, UiMotionRecipe recipe, float y,
                            float scale, float opacity)
    {
        Next(target);
        ConfigureTransition(target, recipe.durationMs, EasingMode.EaseOut);
        target.style.translate = new Translate(0f, _reducedMotion ? 0f : y);
        target.style.scale = new Scale(_reducedMotion
            ? Vector2.one
            : new Vector2(scale, scale));
        target.style.opacity = opacity;
    }

    private void ReducedFlash(VisualElement target, UiFeedbackTone tone)
    {
        string className = tone == UiFeedbackTone.Negative ? "ui-feedback--negative" :
            tone == UiFeedbackTone.Positive ? "ui-feedback--positive" :
            "ui-feedback--sand";
        target.AddToClassList(className);
        target.schedule.Execute(() => target.RemoveFromClassList(className))
            .ExecuteLater(Mathf.Max(1, _config.reducedFadeMs));
    }

    private void Settle(VisualElement target, int generation, int duration)
    {
        if (!Current(target, generation)) return;
        ConfigureTransition(target, _reducedMotion ? _config.reducedFadeMs : duration,
            EasingMode.EaseOut);
        target.style.translate = new Translate(0f, 0f);
        target.style.scale = new Scale(Vector2.one);
        target.style.opacity = 1f;
        target.schedule.Execute(() =>
        {
            if (Current(target, generation)) ClearInlineMotion(target);
        }).ExecuteLater(Mathf.Max(1, duration) + 16);
    }

    private int Next(VisualElement target)
    {
        int value = _generations.TryGetValue(target, out int current) ? current + 1 : 1;
        _generations[target] = value;
        return value;
    }

    private bool Current(VisualElement target, int generation) =>
        target != null && _generations.TryGetValue(target, out int current) &&
        current == generation;

    private static void ConfigureTransition(VisualElement target, int durationMs,
                                            EasingMode easing)
    {
        var properties = new List<StylePropertyName>
        {
            new StylePropertyName("opacity"),
            new StylePropertyName("translate"),
            new StylePropertyName("scale"),
        };
        var duration = new TimeValue(Mathf.Max(0, durationMs), TimeUnit.Millisecond);
        target.style.transitionProperty = properties;
        target.style.transitionDuration = new List<TimeValue> { duration, duration, duration };
        target.style.transitionTimingFunction = new List<EasingFunction>
        {
            new EasingFunction(easing),
            new EasingFunction(easing),
            new EasingFunction(easing),
        };
    }

    private static void ClearTransitions(VisualElement target)
    {
        var zero = new TimeValue(0f, TimeUnit.Millisecond);
        target.style.transitionDuration = new List<TimeValue> { zero };
    }

    private static void ClearInlineMotion(VisualElement target)
    {
        target.style.opacity = StyleKeyword.Null;
        target.style.translate = StyleKeyword.Null;
        target.style.scale = StyleKeyword.Null;
        target.style.transitionProperty = StyleKeyword.Null;
        target.style.transitionDuration = StyleKeyword.Null;
        target.style.transitionTimingFunction = StyleKeyword.Null;
    }

    private static string Normalize(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (id.StartsWith("card:", StringComparison.Ordinal) ||
            id.StartsWith("station-", StringComparison.Ordinal) ||
            id.StartsWith("anchor-", StringComparison.Ordinal))
            return id;
        if (id.StartsWith("market:", StringComparison.Ordinal) ||
            id.StartsWith("hero:", StringComparison.Ordinal) ||
            id.StartsWith("item:", StringComparison.Ordinal) ||
            id.StartsWith("inscription:", StringComparison.Ordinal))
            return "card:" + id;
        return id;
    }
}

internal sealed class UiInteractableFeedbackManipulator : Manipulator
{
    private readonly UiFeedbackDirector _director;
    private readonly Func<string> _id;
    private bool _pointerOver;
    private bool _focused;

    public UiInteractableFeedbackManipulator(UiFeedbackDirector director, Func<string> id)
    {
        _director = director;
        _id = id;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerEnterEvent>(OnEnter);
        target.RegisterCallback<PointerLeaveEvent>(OnLeave);
        target.RegisterCallback<PointerDownEvent>(OnDown);
        target.RegisterCallback<PointerUpEvent>(OnUp);
        target.RegisterCallback<PointerCancelEvent>(OnCancel);
        target.RegisterCallback<FocusInEvent>(OnFocusIn);
        target.RegisterCallback<FocusOutEvent>(OnFocusOut);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerEnterEvent>(OnEnter);
        target.UnregisterCallback<PointerLeaveEvent>(OnLeave);
        target.UnregisterCallback<PointerDownEvent>(OnDown);
        target.UnregisterCallback<PointerUpEvent>(OnUp);
        target.UnregisterCallback<PointerCancelEvent>(OnCancel);
        target.UnregisterCallback<FocusInEvent>(OnFocusIn);
        target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
    }

    private void OnEnter(PointerEnterEvent evt)
    {
        if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse &&
            evt.pointerType != UnityEngine.UIElements.PointerType.pen) return;
        _pointerOver = true;
        _director.Preview(_id?.Invoke());
        _director.Hover(target, true);
    }

    private void OnLeave(PointerLeaveEvent evt)
    {
        _pointerOver = false;
        if (!_focused) _director.Hover(target, false);
    }

    private void OnDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || !target.enabledInHierarchy) return;
        string id = _id?.Invoke();
        if (!string.IsNullOrEmpty(id)) _director.RegisterTarget(id, target);
        _director.Press(target, true);
    }

    private void OnUp(PointerUpEvent evt)
    {
        if (evt.button == 0) _director.Press(target, false);
    }

    private void OnCancel(PointerCancelEvent evt) => _director.Press(target, false);

    private void OnFocusIn(FocusInEvent evt)
    {
        _focused = true;
        string id = _id?.Invoke();
        if (!string.IsNullOrEmpty(id)) _director.RegisterTarget(id, target);
        _director.Preview(id);
        _director.Focus(target, true);
    }

    private void OnFocusOut(FocusOutEvent evt)
    {
        _focused = false;
        if (!_pointerOver) _director.Focus(target, false);
    }
}

internal static class UiPresentationContract
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Warband/Verify Decision Cards")]
    private static void VerifyDecisionCardsMenu()
    {
        Validate();
        Debug.Log("[Decision Cards] semantic facts, card profiles, offer fixtures, and motion " +
                  "recipes passed.");
    }
#endif

    public static void Validate()
    {
        HallStationPresentationCatalog.Validate();
        DecisionCardPresentation.Validate();
        MarketOfferPresentationContract.ValidateFixtures();
        OfferFactProfiles.Validate();
        HubPresentationConfig config = HubPresentationConfig.Load();
        Require(config.reveal.durationMs >= 0, "reveal duration cannot be negative");
        Require(config.detailSwap.durationMs >= 0,
            "selected-card detail swap duration cannot be negative");
        Require(config.shelfExpand.durationMs > 0,
            "Warband Shelf expansion must remain perceptible");
        Require(config.shelfCollapse.durationMs > 0,
            "Warband Shelf collapse must remain perceptible");
        Require(config.shelfFocus.durationMs > 0,
            "Warband Shelf focus must remain perceptible");
        Require(config.reveal.staggerCapMs >= config.reveal.staggerMs,
            "reveal stagger cap must allow at least one stagger");
        Require(config.commit.particles <= config.fx.maxEffects,
            "commit particle count must stay within the bounded FX budget");
        Require(config.transactions != null, "semantic transaction recipes must exist");
        Require(config.muster != null, "Muster presentation tuning must exist");
        Require(config.muster.reveal.durationMs > 0,
            "Muster reveal must remain perceptible");
        Require(config.muster.reveal.staggerMs > 0,
            "Muster offer needs a readable reveal cadence");
        Require(config.muster.lensInitialDelayMs >= config.muster.lensReshowMs,
            "Muster lens re-show must be faster than its first disclosure");
        Require(config.muster.select.durationMs >= config.muster.deselect.durationMs,
            "Muster select should carry at least as much weight as deselect");
        Require(config.tooltip != null, "shared semantic tooltip tuning must exist");
        Require(config.tooltip.pointerDelayMs >= config.tooltip.reshowDelayMs,
            "tooltip re-show must be faster than first pointer disclosure");
        Require(config.tooltip.focusDelayMs >= config.tooltip.reshowDelayMs,
            "tooltip re-show must be faster than first focus disclosure");
        Require(config.tooltip.equipmentMaxWidthPx >= config.tooltip.maxWidthPx,
            "equipment tooltips must have room for their fact tiles");
        Require(config.tooltip.safeMarginPx >= 8f,
            "tooltips need a meaningful safe-edge margin");
        Require(config.interactions != null,
            "reusable interaction recipes must exist");
        foreach (UiMotionRecipe recipe in new[]
                 {
                     config.interactions.tooltipReveal,
                     config.interactions.tooltipDismiss,
                     config.interactions.pin,
                     config.interactions.unpin,
                     config.interactions.drawerExpand,
                     config.interactions.drawerCollapse,
                     config.interactions.socketWake,
                     config.interactions.projectedTarget,
                 })
        {
            Require(recipe != null, "every reusable interaction needs a motion recipe");
            Require(recipe.durationMs > 0,
                "reusable interaction recipes must remain perceptible");
            Require(recipe.particles <= config.fx.maxEffects,
                "interaction particles must stay inside the bounded FX budget");
        }
        foreach (UiMotionRecipe recipe in new[]
                 {
                     config.transactions.recruit,
                     config.transactions.rank,
                     config.transactions.weapon,
                     config.transactions.trinket,
                     config.transactions.inscription,
                     config.transactions.capacity,
                     config.transactions.rankChoice,
                     config.transactions.equip,
                     config.transactions.reforge,
                 })
        {
            Require(recipe != null, "every semantic transaction needs a motion recipe");
            Require(recipe.durationMs > 0, "semantic transactions must remain perceptible");
            Require(recipe.particles <= config.fx.maxEffects,
                "transaction particle count must stay inside the bounded FX budget");
        }
        Require(config.press.scale <= 1f, "press must compress, not expand");
        Require(config.select.scale >= 1f, "selection must read as a positive punch");
        Require(config.error.distancePx > 0f, "error feedback needs a visible non-colour cue");
        Require(config.environment.cameraDurationMs >= 80,
            "Hall camera motion must remain perceptible and interruption-safe");
        Require(config.environment.ambientMotes <= 64,
            "Hall ambient particles must stay inside the mobile-safe bound");
        Require(config.environment.transactionMotes <= 64,
            "Hall transaction particles must stay inside the bounded FX budget");
        Require(config.audio.volume >= 0f && config.audio.volume <= 1f,
            "UI audio volume must remain normalized");
        Require(config.haptics.selectMs <= config.haptics.successMs,
            "selection haptics must stay lighter than a successful commit");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[UiPresentationContract] " + message);
    }
}
