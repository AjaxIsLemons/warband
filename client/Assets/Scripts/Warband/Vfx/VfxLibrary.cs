using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The recipe table + shared render resources for the VFX runtime. Recipes are authored HERE, in
/// code, because gradients and AnimationCurves don't survive JSON (fx-runtime "Core decisions");
/// tuning.json stays the binding layer that says which id fires when and tints it.
///
/// Colors and intensity tiers come from combat-spectacle §1: hue = WHAT, undertone = WHOSE,
/// saturation+bloom = HOW MUCH. One lane per effect, never a cross-lane gradient, and an element's
/// <see cref="VfxElement.Tier"/> is its authored bloom eligibility (bloom threshold is 1.1):
/// T0 ≤0.8 ambient + ALL defensives · T1 ≤1.0 autos/riders · T2 1.3-1.8 signature casts ·
/// T3 2.5-4.0 S-crowns and deaths.
///
/// Nothing here depends on a generated asset: the noise and the soft mote sprite are built
/// procedurally at first use, so P1 lands before the GenerateAsset batch.
/// </summary>
public static class VfxLibrary
{
    public const string ShaderRing = "Warband/Ring";
    public const string ShaderGroundFill = "Warband/GroundFill";
    public const string ShaderSigil = "Warband/Sigil";
    public const string ShaderGlow = "Warband/Glow";
    public const string ShaderParticle = "Warband/Particle";
    public const string ShaderDissolve = "Warband/Dissolve";

    /// <summary>The authoring reference for a tell's motionGlow (the TellDef default). An element's
    /// Tier IS its final HDR multiplier at this glow; a tell that dials motionGlow away from it
    /// scales its whole recipe proportionally, which is what keeps the F1 loop useful.</summary>
    public const float GlowRef = 3f;

    // ---- palette lanes (combat-spectacle §1) ---------------------------------
    private static readonly Color Bone = new Color(0.910f, 0.886f, 0.831f);      // #E8E2D4
    private static readonly Color BoneHot = new Color(1.000f, 0.965f, 0.890f);   // #FFF6E3
    private static readonly Color Blood = new Color(0.541f, 0.184f, 0.141f);     // #8A2F24
    private static readonly Color BloodHot = new Color(0.839f, 0.271f, 0.184f);  // #D6452F
    private static readonly Color EmberDark = new Color(0.494f, 0.227f, 0.114f); // #7E3A1D
    private static readonly Color Ember = new Color(0.886f, 0.345f, 0.122f);     // #E2581F
    private static readonly Color EmberHot = new Color(1.000f, 0.620f, 0.271f);  // #FF9E45
    private static readonly Color Verdant = new Color(0.498f, 0.659f, 0.361f);   // #7FA85C
    private static readonly Color VerdantHot = new Color(0.639f, 0.827f, 0.494f);// #A3D37E
    private static readonly Color VioletHot = new Color(0.655f, 0.561f, 0.827f); // #A78FD3
    private static readonly Color Sand = new Color(0.847f, 0.776f, 0.604f);      // #D8C69A
    private static readonly Color SandLit = new Color(1.000f, 0.906f, 0.659f);   // #FFE7A8

    private const string RecipeOverrideResourcesPath = "VFX/Recipes";

    private static Dictionary<string, VfxDef> _builtIns;
    private static Dictionary<string, VfxDef> _assetOverrides;
    private static Dictionary<string, VfxDef> _previewOverrides =
        new Dictionary<string, VfxDef>();
    private static List<string> _allIds;
    private static readonly HashSet<string> _warned = new HashSet<string>();

    /// <summary>The recipe for an id, or null — an unknown id logs ONCE and the caller falls back to
    /// the Tracer/Burst primitives, so tuning.json can name effects before they are authored (the
    /// same law the SFX names already follow).</summary>
    public static VfxDef Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        if (_previewOverrides.TryGetValue(id, out VfxDef preview)) return preview;
        if (_assetOverrides.TryGetValue(id, out VfxDef authored)) return authored;
        if (_builtIns.TryGetValue(id, out VfxDef def)) return def;
        if (_warned.Add(id)) Debug.LogWarning($"[Vfx] unknown recipe '{id}' — falling back to primitives");
        return null;
    }

    /// <summary>The immutable code-authored fallback, ignoring Lab drafts and asset overrides.</summary>
    public static VfxDef GetBuiltIn(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        return _builtIns.TryGetValue(id, out VfxDef def) ? def : null;
    }

    /// <summary>Every usable id, including asset-only recipes, in stable ordinal order.</summary>
    public static IReadOnlyList<string> AllIds
    {
        get
        {
            EnsureLoaded();
            if (_allIds == null)
            {
                var ids = new HashSet<string>(_builtIns.Keys);
                foreach (string id in _assetOverrides.Keys) ids.Add(id);
                foreach (string id in _previewOverrides.Keys) ids.Add(id);
                _allIds = new List<string>(ids);
                _allIds.Sort(System.StringComparer.Ordinal);
            }
            return _allIds;
        }
    }

    public static bool HasAssetOverride(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && _assetOverrides.ContainsKey(id);
    }

    /// <summary>
    /// Install an unsaved Lab draft at the top of the resolution stack. This is process-local and
    /// deliberately never touches an asset or tuning.json.
    /// </summary>
    public static void SetPreviewOverride(string id, VfxDef def)
    {
        if (string.IsNullOrWhiteSpace(id) || def == null) return;
        _previewOverrides[id] = def;
        _allIds = null;
    }

    public static void ClearPreviewOverride(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_previewOverrides.Remove(id)) _allIds = null;
    }

    public static void ClearPreviewOverrides()
    {
        if (_previewOverrides.Count == 0) return;
        _previewOverrides.Clear();
        _allIds = null;
    }

    /// <summary>Re-read applied recipe assets after an editor Apply/import.</summary>
    public static void ReloadAssetOverrides()
    {
        EnsureBuiltIns();
        _assetOverrides = new Dictionary<string, VfxDef>();
        VfxRecipeAsset[] assets =
            Resources.LoadAll<VfxRecipeAsset>(RecipeOverrideResourcesPath);
        System.Array.Sort(
            assets,
            (a, b) => System.StringComparer.Ordinal.Compare(
                a != null ? a.name : "", b != null ? b.name : ""));
        foreach (VfxRecipeAsset asset in assets)
        {
            if (asset == null || !asset.enabledOverride ||
                string.IsNullOrWhiteSpace(asset.recipeId))
                continue;
            string id = asset.recipeId.Trim();
            if (_assetOverrides.ContainsKey(id) && _warned.Add("override:" + id))
                Debug.LogWarning(
                    $"[Vfx] duplicate recipe override '{id}' — '{asset.name}' wins.");
            VfxDef def = asset.Compile();
            def.Id = id;
            _assetOverrides[id] = def;
        }
        _allIds = null;
    }

    private static void EnsureBuiltIns()
    {
        if (_builtIns == null) _builtIns = BuildRecipes();
    }

    private static void EnsureLoaded()
    {
        EnsureBuiltIns();
        if (_assetOverrides == null) ReloadAssetOverrides();
    }

    // ---- recipes -------------------------------------------------------------

    private static Dictionary<string, VfxDef> BuildRecipes()
    {
        var d = new Dictionary<string, VfxDef>();
        void Add(VfxDef def) => d[def.Id] = def;

        // Autos and riders — T1, one bright frame, never bigger than a cast (Riot proportionality).
        Add(Def("slash-arc", 0.24f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 1.6f, Tier = 1.0f, Tint = BoneHot, Offset = new Vector3(0f, 0.95f, 0.25f),
                Thickness = 0.14f, Softness = 0.35f,
                Radius = Curve(0f, 0.45f, 0.18f, 0.85f),
                Arc = Curve(0f, 0.08f, 0.07f, 0.30f),
                Rotation = Curve(0f, 0.05f, 0.24f, 0.15f),
                Alpha = Curve(0f, 1f, 0.10f, 0.9f, 0.24f, 0f),
            },
            new ParticleElement
            {
                Burst = 6, Tier = 1.0f, Tint = Bone, Offset = new Vector3(0f, 0.95f, 0.3f),
                LifeMin = 0.10f, LifeMax = 0.22f, SpeedMin = 2.2f, SpeedMax = 4.2f,
                SizeMin = 0.05f, SizeMax = 0.10f, ShapeAngle = 35f, Stretch = true, Drag = 0.3f,
            }));

        // ---- per-weapon auto language (combat-spectacle §6) ----------------------
        // Unreachable from data until the byWeapon tell filter (fx-runtime S5): an Attack row can
        // now name "Greataxe" and get a different swing than "Twin Daggers", where before every
        // auto keyed on chassis alone. All T1 — an auto must never outshine a cast (proportionality,
        // §1) — and all SHAPE, no lane: these carry no Tint, so the tell's motionColor paints them
        // and the F1 loop can recolour the whole weapon set without a recompile.

        // Daggers: two crossing nicks, not one committed swing. The second arc is the first
        // delayed and swept the other way — the crossing IS the read, at half a sabre's reach.
        Add(Def("nick-cross", 0.20f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 1.15f, Tier = 1.0f, Offset = new Vector3(-0.10f, 0.95f, 0.22f),
                Thickness = 0.09f, Softness = 0.30f,
                Radius = Curve(0f, 0.40f, 0.11f, 0.80f),
                Arc = Curve(0f, 0.05f, 0.06f, 0.17f),
                Rotation = Curve(0f, 0.06f, 0.14f, 0.20f),
                Alpha = Curve(0f, 1f, 0.07f, 0.85f, 0.15f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing, Delay = 0.06f,
                Size = 1.15f, Tier = 1.0f, Offset = new Vector3(0.10f, 1.05f, 0.22f),
                Thickness = 0.09f, Softness = 0.30f,
                Radius = Curve(0f, 0.40f, 0.11f, 0.80f),
                Arc = Curve(0f, 0.05f, 0.06f, 0.17f),
                Rotation = Curve(0f, 0.62f, 0.14f, 0.48f),   // swept back the other way
                Alpha = Curve(0f, 1f, 0.07f, 0.85f, 0.15f, 0f),
            },
            new ParticleElement
            {
                Burst = 4, Tier = 0.95f, Offset = new Vector3(0f, 1f, 0.28f),
                LifeMin = 0.06f, LifeMax = 0.14f, SpeedMin = 2.6f, SpeedMax = 4.6f,
                SizeMin = 0.035f, SizeMax = 0.07f, ShapeAngle = 28f, Stretch = true, Drag = 0.35f,
            }));

        // Sabre: one thin elegant stroke — the longest reach of the light weapons, least debris.
        Add(Def("slash-thin", 0.22f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 1.7f, Tier = 1.0f, Offset = new Vector3(0f, 1.0f, 0.26f),
                Thickness = 0.065f, Softness = 0.40f,
                Radius = Curve(0f, 0.48f, 0.16f, 0.92f),
                Arc = Curve(0f, 0.06f, 0.09f, 0.26f),
                Rotation = Curve(0f, 0.04f, 0.22f, 0.17f),
                Alpha = Curve(0f, 1f, 0.09f, 0.9f, 0.22f, 0f),
            },
            new ParticleElement
            {
                Burst = 3, Tier = 0.9f, Offset = new Vector3(0f, 1.0f, 0.32f),
                LifeMin = 0.08f, LifeMax = 0.16f, SpeedMin = 2.2f, SpeedMax = 3.8f,
                SizeMin = 0.035f, SizeMax = 0.065f, ShapeAngle = 20f, Stretch = true, Drag = 0.3f,
            }));

        // Mace: blunt. Short fat arc low on the body, and the debris falls instead of spraying —
        // weight reads as things dropping, not sparks flying.
        Add(Def("slash-blunt", 0.26f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 1.45f, Tier = 1.0f, Offset = new Vector3(0f, 0.80f, 0.24f),
                Thickness = 0.22f, Softness = 0.22f,
                Radius = Curve(0f, 0.38f, 0.19f, 0.72f),
                Arc = Curve(0f, 0.08f, 0.11f, 0.21f),
                Rotation = Curve(0f, 0.10f, 0.26f, 0.19f),
                Alpha = Curve(0f, 1f, 0.13f, 0.8f, 0.26f, 0f),
            },
            new ParticleElement
            {
                Burst = 7, Tier = 0.95f, Offset = new Vector3(0f, 0.85f, 0.3f),
                LifeMin = 0.14f, LifeMax = 0.30f, SpeedMin = 1.4f, SpeedMax = 2.8f,
                SizeMin = 0.055f, SizeMax = 0.11f, ShapeAngle = 42f, Gravity = 2.2f, Drag = 0.4f,
            }));

        // Greataxe: the widest arc in the game, and the only auto with a HANG — the rotation track
        // holds nearly still for the first 0.07 s at full alpha, then sweeps. That pause is what
        // sells the weight, and it is why the axe's tell also carries a windup. Cleave continues
        // the SAME arc (§6), so the recipe stays one stroke rather than two.
        Add(Def("slash-wide", 0.34f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 2.25f, Tier = 1.0f, Offset = new Vector3(0f, 0.95f, 0.28f),
                Thickness = 0.17f, Softness = 0.28f,
                Radius = Curve(0f, 0.50f, 0.26f, 0.97f),
                Arc = Curve(0f, 0.09f, 0.07f, 0.12f, 0.26f, 0.44f),
                Rotation = Curve(0f, 0.02f, 0.07f, 0.035f, 0.30f, 0.24f),  // hold, then sweep
                Alpha = Curve(0f, 1f, 0.07f, 1f, 0.20f, 0.75f, 0.34f, 0f),
            },
            new ParticleElement
            {
                Burst = 11, Tier = 1.0f, Delay = 0.07f, Offset = new Vector3(0f, 0.95f, 0.36f),
                LifeMin = 0.14f, LifeMax = 0.32f, SpeedMin = 3.2f, SpeedMax = 6.0f,
                SizeMin = 0.06f, SizeMax = 0.13f, ShapeAngle = 50f,
                Stretch = true, StretchScale = 2.6f, Drag = 0.32f,
            }));

        // Tower Shield: not a swing at all — a flat slab shoved forward, plus ground dust. No arc
        // anywhere, because the shield's whole language is "this did not cut you, it moved you".
        // The glow sits IN FRONT of the shield rather than over the body, and stays under T1: a soft
        // round shape is the least weapon-like thing on the board, so it has to stay small enough to
        // read as a shove and not as fog.
        Add(Def("shield-shove", 0.26f,
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.UpFacing,
                Size = 1.35f, Tier = 0.95f, Offset = new Vector3(0f, 0.80f, 0.45f), Falloff = 1.9f,
                Scale = Curve(0f, 0.55f, 0.10f, 1.15f, 0.26f, 1.25f),
                Alpha = Curve(0f, 0.85f, 0.10f, 0.7f, 0.26f, 0f),
            },
            new ParticleElement
            {
                Burst = 9, Tier = 0.85f, Offset = new Vector3(0f, 0.12f, 0.35f),
                LifeMin = 0.18f, LifeMax = 0.36f, SpeedMin = 1.2f, SpeedMax = 2.6f,
                SizeMin = 0.07f, SizeMax = 0.15f, ShapeAngle = 55f, Gravity = 0.6f, Drag = 0.5f,
            }));

        // Pike: a 2-hex thrust LINE, not a sweep — a tight cone of fast stretched sparks straight
        // down the attack direction, capped by a small ring where the point arrives.
        Add(Def("thrust-line", 0.24f,
            new ParticleElement
            {
                Burst = 9, Tier = 1.0f, Offset = new Vector3(0f, 1.0f, 0.3f),
                LifeMin = 0.10f, LifeMax = 0.18f, SpeedMin = 7f, SpeedMax = 10f,
                SizeMin = 0.05f, SizeMax = 0.09f, ShapeAngle = 3.5f,
                Stretch = true, StretchScale = 3.5f, Drag = 0.1f,
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 0.85f, Tier = 1.0f, Offset = new Vector3(0f, 1.0f, 0.9f),
                Thickness = 0.13f, Softness = 0.30f,
                Radius = Curve(0f, 0.25f, 0.14f, 0.85f),
                Alpha = Curve(0f, 0.95f, 0.16f, 0f),
            }));

        // Company Standard: a pole swipe with the banner cloth following a beat behind — the
        // second arc is bigger, softer and slower, and that lag is the ripple.
        Add(Def("pole-swipe", 0.34f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing,
                Size = 1.8f, Tier = 1.0f, Offset = new Vector3(0f, 1.05f, 0.26f),
                Thickness = 0.10f, Softness = 0.30f,
                Radius = Curve(0f, 0.45f, 0.18f, 0.90f),
                Arc = Curve(0f, 0.07f, 0.10f, 0.25f),
                Rotation = Curve(0f, 0.06f, 0.24f, 0.20f),
                Alpha = Curve(0f, 1f, 0.11f, 0.85f, 0.24f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.UpFacing, Delay = 0.08f,
                Size = 2.0f, Tier = 0.55f, Offset = new Vector3(0f, 1.25f, 0.22f),
                Thickness = 0.26f, Softness = 0.60f,                     // soft + fat = cloth
                Radius = Curve(0f, 0.40f, 0.26f, 0.88f),
                Arc = Curve(0f, 0.10f, 0.14f, 0.30f),
                Rotation = Curve(0f, 0.04f, 0.26f, 0.18f),
                // dim: the ripple is a suggestion trailing the pole, never a second bright stroke
                Alpha = Curve(0f, 0.40f, 0.12f, 0.30f, 0.26f, 0f),
            }));

        // Musket: the muzzle flash at the shooter. Brightest single frame any auto is allowed,
        // and it is over in 0.12 s — the crack and the smoke do the rest of the work.
        Add(Def("muzzle-flash", 0.18f,
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.9f, Tier = 1.0f, Tint = BoneHot, Offset = new Vector3(0f, 1.0f, 0.35f),
                Falloff = 2.2f,
                Scale = Curve(0f, 0.35f, 0.04f, 1.2f, 0.18f, 0.9f),
                Alpha = Curve(0f, 1f, 0.06f, 0.7f, 0.18f, 0f),
            },
            new ParticleElement
            {
                Burst = 7, Tier = 1.0f, Tint = BoneHot, Offset = new Vector3(0f, 1.0f, 0.4f),
                LifeMin = 0.06f, LifeMax = 0.16f, SpeedMin = 4f, SpeedMax = 8f,
                SizeMin = 0.04f, SizeMax = 0.09f, ShapeAngle = 14f,
                Stretch = true, StretchScale = 3f, Drag = 0.5f,
            }));

        // Musket shot: an INSTANT line. The tell flies this over ~0.04 s, and because the smoke
        // emits in world space at a high rate with a long life, what is left behind after the ball
        // lands is the line itself — the shot is already resolved, the smoke is the evidence.
        Add(Def("smoke-line", 0.65f,
            new ParticleElement
            {
                Rate = 420f, Burst = 0, Tier = 0.55f, MaxParticles = 90,
                LifeMin = 0.35f, LifeMax = 0.60f, SpeedMin = 0f, SpeedMax = 0.25f,
                SizeMin = 0.09f, SizeMax = 0.20f, Shape = ParticleShape.Sphere, ShapeRadius = 0.06f,
                Gravity = -0.18f, Drag = 0.6f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.30f, Tier = 1.0f, Tint = BoneHot, Falloff = 3f,
                Alpha = Curve(0f, 1f, 0.06f, 0.6f, 0.12f, 0f),   // the ball outruns its own glow
            }));

        // Censer: a warm mote lobbed to the lowest ally (the heal-auto is a real Attack event with
        // an ALLY target, ADR 0012), trailing thurible smoke. Sand, not Verdant — the healing lane
        // belongs to the heal itself, which lands on arrival; this is only the censer swinging.
        Add(Def("censer-mote", 0.5f,
            new ParticleElement
            {
                Rate = 60f, Burst = 0, Tier = 0.6f, Tint = Sand, MaxParticles = 36,
                LifeMin = 0.25f, LifeMax = 0.45f, SpeedMin = 0.1f, SpeedMax = 0.4f,
                SizeMin = 0.06f, SizeMax = 0.13f, Shape = ParticleShape.Sphere, ShapeRadius = 0.08f,
                Gravity = -0.5f, Drag = 0.4f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.5f, Tier = 1.0f, Tint = SandLit, Falloff = 2.4f,
            }));

        // Staff: a plain wisp, deliberately UNTINTED so the tell's motionColor carries the caster's
        // lane (§6 "tinted by chassis lane") — a byWeapon+byChassis row repaints it per caster
        // without touching this recipe.
        Add(Def("staff-wisp", 0.35f,
            new ParticleElement
            {
                Rate = 100f, Burst = 0, Tier = 0.9f, MaxParticles = 48,
                LifeMin = 0.14f, LifeMax = 0.28f, SpeedMin = 0.1f, SpeedMax = 0.5f,
                SizeMin = 0.05f, SizeMax = 0.11f, Shape = ParticleShape.Sphere, ShapeRadius = 0.09f,
                Gravity = -0.35f, Drag = 0.2f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.55f, Tier = 1.0f, Falloff = 2.6f,
            }));

        Add(Def("arrow-streak", 0.3f,
            new ParticleElement
            {
                Rate = 90f, Burst = 0, Tier = 0.9f, Tint = Bone, MaxParticles = 48,
                LifeMin = 0.10f, LifeMax = 0.20f, SpeedMin = 0f, SpeedMax = 0.3f,
                SizeMin = 0.05f, SizeMax = 0.09f, Shape = ParticleShape.Sphere, ShapeRadius = 0.05f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.45f, Tier = 1.0f, Tint = BoneHot, Falloff = 2.5f,
            }));

        Add(Def("fire-bolt", 0.3f,
            new ParticleElement
            {
                Rate = 120f, Burst = 0, Tier = 1.5f, Tint = Ember, MaxParticles = 80,
                LifeMin = 0.20f, LifeMax = 0.40f, SpeedMin = 0.2f, SpeedMax = 0.7f,
                SizeMin = 0.07f, SizeMax = 0.15f, Shape = ParticleShape.Sphere, ShapeRadius = 0.12f,
                Gravity = -0.3f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.75f, Tier = 1.6f, Tint = EmberHot, Falloff = 2.0f,
            }));

        // Signature releases — T2, ≤0.3 s of peak. The ring IS the hitbox (combat-spectacle §3).
        Add(Def("fire-release", 0.45f,
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 1.6f, Tier = 1.7f, Tint = EmberHot, Falloff = 2.2f,
                Offset = new Vector3(0f, 0.9f, 0f),
                Scale = Curve(0f, 0.4f, 0.12f, 1.2f, 0.45f, 1.4f),
                Alpha = Curve(0f, 1f, 0.12f, 0.85f, 0.45f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 2.6f, Tier = 1.4f, Tint = Ember, Thickness = 0.10f, Softness = 0.30f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.15f, 0.35f, 0.95f),
                Alpha = Curve(0f, 0.9f, 0.35f, 0f),
            },
            new ParticleElement
            {
                Burst = 22, Tier = 1.6f, Tint = EmberHot, Offset = new Vector3(0f, 0.5f, 0f),
                LifeMin = 0.30f, LifeMax = 0.60f, SpeedMin = 2f, SpeedMax = 4.5f,
                SizeMin = 0.08f, SizeMax = 0.18f, ShapeAngle = 55f, ShapeRotation = Up,
                Gravity = 0.6f, Drag = 0.2f,
            },
            new LightElement
            {
                Range = 5f, Tier = 1.6f, Tint = Ember, Offset = new Vector3(0f, 1f, 0f),
                Intensity = Curve(0f, 0f, 0.06f, 1f, 0.45f, 0f),
            }));

        Add(Def("holy-release", 0.5f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 3.2f, Tier = 1.5f, Tint = BoneHot, Thickness = 0.09f, Softness = 0.25f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.10f, 0.40f, 1.0f),
                Alpha = Curve(0f, 1f, 0.40f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 1.3f, Tier = 1.4f, Tint = BoneHot, Falloff = 3f,
                Offset = new Vector3(0f, 1.0f, 0f),
                Scale = Curve(0f, 0.5f, 0.15f, 1.1f),
                Alpha = Curve(0f, 1f, 0.25f, 0f),
            },
            new ParticleElement
            {
                Burst = 14, Tier = 1.2f, Tint = VerdantHot, Offset = new Vector3(0f, 0.4f, 0f),
                LifeMin = 0.40f, LifeMax = 0.70f, SpeedMin = 1f, SpeedMax = 2f,
                SizeMin = 0.06f, SizeMax = 0.12f, ShapeAngle = 40f, ShapeRotation = Up,
                Gravity = -0.5f,
            },
            new LightElement
            {
                Range = 5f, Tier = 1.4f, Tint = BoneHot, Offset = new Vector3(0f, 1f, 0f),
                Intensity = Curve(0f, 0f, 0.08f, 1f, 0.5f, 0f),
            }));

        // The windup half of the cast sentence: T0→T1 under the caster, running until the release
        // ends it. Two counter-spinning rims stand in for the era sigils until P4 generates them.
        Add(Sustained("cast-aura", 0.25f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
                Size = 2.0f, Tier = 0.9f, Thickness = 0.06f, Softness = 0.20f,
                Offset = new Vector3(0f, 0.06f, 0f),
                Radius = Curve(0f, 0.85f),
                Arc = Curve(0f, 0.85f),
                Rotation = Loop(AnimationCurve.Linear(0f, 0f, 3f, 1f)),
                Alpha = Curve(0f, 0f, 0.15f, 1f),
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
                Size = 1.3f, Tier = 0.7f, Thickness = 0.05f, Softness = 0.18f,
                Offset = new Vector3(0f, 0.06f, 0f),
                Radius = Curve(0f, 0.8f),
                Arc = Curve(0f, 0.6f),
                Rotation = Loop(AnimationCurve.Linear(0f, 1f, 4f, 0f)),
                Alpha = Curve(0f, 0f, 0.20f, 0.9f),
            },
            new ParticleElement
            {
                Anchor = VfxAnchor.FollowUnit, Rate = 8f, Burst = 0, Tier = 1.0f, MaxParticles = 24,
                LifeMin = 0.6f, LifeMax = 1.0f, SpeedMin = 0.6f, SpeedMax = 1.1f,
                SizeMin = 0.05f, SizeMax = 0.10f, ShapeAngle = 12f, ShapeRadius = 0.45f,
                ShapeRotation = Up, Gravity = -0.25f,
            }));

        // ...and the eight era sigils that specialize it, one per chassis (combat-spectacle §2:
        // "one 4-beat sentence, eight sigils").
        Add(CastAura("cleric"));
        Add(CastAura("bulwark"));
        Add(CastAura("shade"));
        Add(CastAura("sharpshot"));
        Add(CastAura("pyromancer"));
        Add(CastAura("berserker"));
        Add(CastAura("phalanx"));
        Add(CastAura("banneret"));

        // The trigger-rider echo's attribution (combat-spectacle §6): a thin spark that travels from
        // the rider's ROOT to the unit its result actually landed on, inside the beat. It is an
        // ECHO, so it stays T1 and carries no lane Tint of its own — a rider wears the color of the
        // engine that fired it, which is what makes six different riders readable as six engines.
        // The line is the ParticleSystem Trails MODULE (simulated inside Simulate(), unlike the
        // banned TrailRenderer), dragged behind a head that PlayProjectile walks root→target.
        Add(Def("spark-link", 0.16f,
            new ParticleElement
            {
                Rate = 150f, Burst = 2, Tier = 1.0f, MaxParticles = 32,
                LifeMin = 0.05f, LifeMax = 0.10f, SpeedMin = 0f, SpeedMax = 0.25f,
                SizeMin = 0.03f, SizeMax = 0.06f, Shape = ParticleShape.Sphere, ShapeRadius = 0.04f,
                Trails = true, TrailRatio = 1f, TrailLifetime = 0.09f, TrailWidth = 0.20f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.26f, Tier = 1.0f, Falloff = 3f,
                Alpha = Curve(0f, 0.9f, 0.16f, 0.45f),
            }));

        // Deathless (combat-spectacle §7.2). T3, and the only red rune on the board: the ring snaps
        // open on his hex, the rune wheel burns inside it, and one flare goes off over his head while
        // the rest of the board is dimming around him. Every element rides FollowUnit, so the survivor
        // wears it — a Frenzy bursting out of the freeze takes the rune with it.
        Add(Def("deathless-rune", 0.85f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
                Size = 2.6f, Tier = 2.8f, Tint = BloodHot, Thickness = 0.16f, Softness = 0.22f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.15f, 0.18f, 0.95f),
                Alpha = Curve(0f, 1f, 0.55f, 0.8f, 0.85f, 0f),
            },
            new QuadElement
            {
                // The berserker's rune wheel — Deathless is his dive, so it is his engraving that
                // refuses. RequireTexture: before the sigil batch this element simply isn't there,
                // rather than stamping a white disc under him.
                Shader = ShaderSigil, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
                Size = 2.0f, Tier = 2.6f, Tint = BloodHot,
                Texture = "Board/FX/Sigils/berserker", RequireTexture = true,
                Offset = new Vector3(0f, 0.055f, 0f),
                Rotation = Curve(0f, 0f, 0.85f, 0.22f),
                Scale = Curve(0f, 0.5f, 0.14f, 1.1f, 0.85f, 1f),
                Alpha = Curve(0f, 0f, 0.10f, 1f, 0.60f, 0.9f, 0.85f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard, Anchor = VfxAnchor.FollowUnit,
                Size = 1.7f, Tier = 3.2f, Tint = BloodHot, Falloff = 2.2f,
                Offset = new Vector3(0f, 1.0f, 0f),
                Scale = Curve(0f, 0.35f, 0.10f, 1.25f, 0.85f, 1.4f),
                Alpha = Curve(0f, 1f, 0.10f, 0.9f, 0.70f, 0f),
            },
            new ParticleElement
            {
                Anchor = VfxAnchor.FollowUnit, Burst = 18, Tier = 2.0f, Tint = EmberHot,
                Offset = new Vector3(0f, 0.6f, 0f),
                LifeMin = 0.45f, LifeMax = 0.85f, SpeedMin = 1.2f, SpeedMax = 2.6f,
                SizeMin = 0.07f, SizeMax = 0.14f, ShapeAngle = 22f, ShapeRadius = 0.35f,
                ShapeRotation = Up, Gravity = -0.6f, Drag = 0.2f,
            },
            new LightElement
            {
                Anchor = VfxAnchor.FollowUnit, Range = 6f, Tier = 2.4f, Tint = BloodHot,
                Offset = new Vector3(0f, 1f, 0f),
                Intensity = Curve(0f, 0f, 0.08f, 1f, 0.85f, 0f),
            }));

        // Impacts.
        Add(Def("impact-spark", 0.2f,
            new ParticleElement
            {
                Burst = 8, Tier = 1.0f, Tint = BoneHot,
                LifeMin = 0.10f, LifeMax = 0.22f, SpeedMin = 2.5f, SpeedMax = 5f,
                SizeMin = 0.05f, SizeMax = 0.10f, ShapeAngle = 45f,
                Stretch = true, StretchScale = 2.5f, Drag = 0.3f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.6f, Tier = 1.0f, Tint = BoneHot, Falloff = 3f,
                Scale = Curve(0f, 0.6f, 0.08f, 1.1f),
                Alpha = Curve(0f, 1f, 0.14f, 0f),
            }));

        Add(Def("impact-heavy", 0.35f,
            new ParticleElement
            {
                Burst = 18, Tier = 1.5f, Tint = BoneHot,
                LifeMin = 0.15f, LifeMax = 0.35f, SpeedMin = 3.5f, SpeedMax = 7f,
                SizeMin = 0.07f, SizeMax = 0.15f, ShapeAngle = 55f,
                Stretch = true, StretchScale = 2.5f, Drag = 0.35f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 1.1f, Tier = 1.6f, Tint = BoneHot, Falloff = 2.6f,
                Scale = Curve(0f, 0.5f, 0.09f, 1.3f),
                Alpha = Curve(0f, 1f, 0.20f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 2.2f, Tier = 1.3f, Tint = Bone, Thickness = 0.07f, Softness = 0.25f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.2f, 0.30f, 0.95f),
                Alpha = Curve(0f, 0.9f, 0.30f, 0f),
            }));

        // Verdant is healing ONLY — never a buff, never a lane it can be confused with.
        Add(Def("heal-motes", 0.7f,
            new ParticleElement
            {
                Burst = 12, Tier = 1.0f, Tint = VerdantHot,
                LifeMin = 0.45f, LifeMax = 0.75f, SpeedMin = 0.7f, SpeedMax = 1.5f,
                SizeMin = 0.06f, SizeMax = 0.12f, ShapeAngle = 18f, ShapeRadius = 0.35f,
                ShapeRotation = Up, Gravity = -0.8f, Drag = 0.1f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.9f, Tier = 0.9f, Tint = Verdant, Falloff = 3f,
                Offset = new Vector3(0f, 0.8f, 0f),
                Scale = Curve(0f, 0.4f, 0.20f, 1.0f),
                Alpha = Curve(0f, 0.8f, 0.70f, 0f),
            }));

        // Control + debuff wear the Waning's ash-violet.
        Add(Def("status-pop", 0.3f,
            new ParticleElement
            {
                Burst = 7, Tier = 1.0f, Tint = VioletHot, Offset = new Vector3(0f, 1.5f, 0f),
                LifeMin = 0.20f, LifeMax = 0.35f, SpeedMin = 1.2f, SpeedMax = 2.2f,
                SizeMin = 0.05f, SizeMax = 0.10f, Shape = ParticleShape.Sphere, ShapeRadius = 0.1f,
                Drag = 0.25f,
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 0.5f, Tier = 0.9f, Tint = VioletHot, Falloff = 3f,
                Offset = new Vector3(0f, 1.5f, 0f),
                Scale = Curve(0f, 0.5f, 0.10f, 1.1f),
                Alpha = Curve(0f, 1f, 0.22f, 0f),
            }));

        // Ash-death (combat-spectacle §7.1): the body goes up as ash, the ground keeps a mark.
        Add(Def("death-dissolve-burst", 0.9f,
            new ParticleElement
            {
                Burst = 24, Tier = 1.2f, Tint = Bone, Offset = new Vector3(0f, 0.5f, 0f),
                LifeMin = 0.50f, LifeMax = 0.90f, SpeedMin = 0.8f, SpeedMax = 2.0f,
                SizeMin = 0.07f, SizeMax = 0.16f, ShapeAngle = 35f, ShapeRadius = 0.25f,
                ShapeRotation = Up, Gravity = -0.35f, Drag = 0.15f,
            },
            new ParticleElement
            {
                Burst = 10, Tier = 1.4f, Tint = BloodHot, Offset = new Vector3(0f, 0.7f, 0f),
                LifeMin = 0.20f, LifeMax = 0.40f, SpeedMin = 2f, SpeedMax = 4f,
                SizeMin = 0.06f, SizeMax = 0.12f, ShapeAngle = 60f,
                Stretch = true, Gravity = 1.2f,
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 1.8f, Tier = 0.9f, Tint = Blood, Thickness = 0.12f, Softness = 0.35f,
                Offset = new Vector3(0f, 0.04f, 0f),
                Radius = Curve(0f, 0.25f, 0.50f, 0.9f),
                Alpha = Curve(0f, 0.9f, 0.90f, 0f),
            }));

        // Ground language (combat-spectacle §4): edge traces the perimeter FIRST, then the floor —
        // boundary before body, so the extent is legible before the pretty part arrives.
        Add(Def("ground-ignite", 0.9f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground, Hex = true,
                Size = 1.9f, Tier = 1.3f, Tint = Ember, Thickness = 0.10f, Softness = 0.20f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.95f),
                Arc = Curve(0f, 0f, 0.20f, 1f),
                Alpha = Curve(0f, 1f, 0.75f, 1f, 0.90f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderGroundFill, Orientation = QuadOrientation.Ground, Hex = true,
                Size = 1.9f, Tier = 1.0f, Tint = EmberDark, Noise = true,
                Intensity = 1.4f, EdgeFade = 0.40f, Delay = 0.12f,
                Offset = new Vector3(0f, 0.045f, 0f),
                Phase = AnimationCurve.Linear(0f, 0f, 3f, 0.9f),
                Alpha = Curve(0f, 0f, 0.20f, 1f, 0.70f, 0.9f, 0.90f, 0f),
            },
            new ParticleElement
            {
                Burst = 10, Tier = 1.5f, Tint = EmberHot, Delay = 0.2f,
                LifeMin = 0.35f, LifeMax = 0.60f, SpeedMin = 0.8f, SpeedMax = 1.6f,
                SizeMin = 0.08f, SizeMax = 0.16f, ShapeAngle = 10f, ShapeRadius = 0.5f,
                ShapeRotation = Up, Gravity = -0.5f,
            }));

        // Grace: a candle-wax pool. QUIET (T0) until something pulses it — defensives get their
        // rank from area and audio, never from brightness.
        Add(Def("ground-bless", 0.9f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground, Hex = true,
                Size = 1.9f, Tier = 0.8f, Tint = SandLit, Thickness = 0.07f, Softness = 0.18f,
                Offset = new Vector3(0f, 0.05f, 0f),
                Radius = Curve(0f, 0.95f),
                Arc = Curve(0f, 0f, 0.20f, 1f),
                Alpha = Curve(0f, 1f, 0.75f, 1f, 0.90f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderGroundFill, Orientation = QuadOrientation.Ground, Hex = true,
                Size = 1.9f, Tier = 0.75f, Tint = Sand, Noise = true,
                Intensity = 0.8f, EdgeFade = 0.45f, Delay = 0.12f,
                Offset = new Vector3(0f, 0.045f, 0f),
                Phase = AnimationCurve.Linear(0f, 0f, 6f, 0.5f),
                Alpha = Curve(0f, 0f, 0.25f, 1f, 0.70f, 0.9f, 0.90f, 0f),
            }));

        Add(Def("leap-dust", 0.4f,
            new ParticleElement
            {
                Burst = 10, Tier = 0.8f, Tint = Bone, Offset = new Vector3(0f, 0.08f, 0f),
                LifeMin = 0.25f, LifeMax = 0.45f, SpeedMin = 1.2f, SpeedMax = 2.4f,
                SizeMin = 0.09f, SizeMax = 0.18f, Shape = ParticleShape.Circle, ShapeRadius = 0.2f,
                ShapeRotation = Up, Gravity = 0.2f, Drag = 0.4f,
            },
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 1.4f, Tier = 0.9f, Tint = Bone, Thickness = 0.09f, Softness = 0.30f,
                Offset = new Vector3(0f, 0.04f, 0f),
                Radius = Curve(0f, 0.2f, 0.25f, 0.9f),
                Alpha = Curve(0f, 0.8f, 0.35f, 0f),
            }));

        // Revision landings are deliberately T3: this is the run's single direct intervention in
        // an autonomous battle. Sand is the temporal language; the second lane names what changed.
        Add(Def("revision-land-future", 0.82f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 4.2f, Tier = 3.2f, Tint = SandLit, Thickness = 0.11f, Softness = 0.18f,
                Offset = new Vector3(0f, 0.07f, 0f),
                Radius = Curve(0f, 0.04f, 0.20f, 0.85f, 0.82f, 1f),
                Alpha = Curve(0f, 1f, 0.34f, 0.72f, 0.82f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderGlow, Orientation = QuadOrientation.Billboard,
                Size = 2.8f, Tier = 3.4f, Tint = new Color(0.30f, 0.72f, 1f), Falloff = 2.5f,
                Offset = new Vector3(0f, 1.0f, 0f),
                Scale = Curve(0f, 0.25f, 0.12f, 1.25f, 0.82f, 1.55f),
                Alpha = Curve(0f, 1f, 0.18f, 0.86f, 0.82f, 0f),
            },
            new ParticleElement
            {
                Burst = 30, Tier = 2.7f, Tint = new Color(0.34f, 0.78f, 1f),
                Offset = new Vector3(0f, 0.22f, 0f),
                LifeMin = 0.35f, LifeMax = 0.78f, SpeedMin = 1.4f, SpeedMax = 3.8f,
                SizeMin = 0.05f, SizeMax = 0.13f, ShapeAngle = 18f, ShapeRadius = 0.7f,
                ShapeRotation = Up, Gravity = -0.8f, Drag = 0.25f,
            },
            new LightElement
            {
                Range = 7f, Tier = 3f, Tint = new Color(0.34f, 0.72f, 1f),
                Offset = new Vector3(0f, 1f, 0f),
                Intensity = Curve(0f, 0f, 0.08f, 1f, 0.42f, 0.45f, 0.82f, 0f),
            }));

        Add(Def("revision-land-recall", 0.88f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 4.4f, Tier = 3.2f, Tint = SandLit, Thickness = 0.12f, Softness = 0.16f,
                Offset = new Vector3(0f, 0.07f, 0f),
                Radius = Curve(0f, 1f, 0.16f, 0.62f, 0.88f, 0.12f),
                Alpha = Curve(0f, 1f, 0.38f, 0.78f, 0.88f, 0f),
            },
            new QuadElement
            {
                Shader = ShaderSigil, Orientation = QuadOrientation.Ground,
                Size = 2.8f, Tier = 2.8f, Tint = VioletHot,
                Offset = new Vector3(0f, 0.075f, 0f),
                Rotation = Curve(0f, 0.8f, 0.88f, 0.05f),
                Scale = Curve(0f, 1.4f, 0.18f, 0.9f, 0.88f, 0.72f),
                Alpha = Curve(0f, 0.9f, 0.58f, 0.72f, 0.88f, 0f),
            },
            new ParticleElement
            {
                Burst = 26, Tier = 2.5f, Tint = VioletHot,
                Offset = new Vector3(0f, 1.3f, 0f),
                LifeMin = 0.34f, LifeMax = 0.82f, SpeedMin = 0.7f, SpeedMax = 2.4f,
                SizeMin = 0.06f, SizeMax = 0.15f, ShapeAngle = 160f, ShapeRadius = 0.7f,
                ShapeRotation = Up, Gravity = 0.8f, Drag = 0.35f,
            },
            new LightElement
            {
                Range = 7f, Tier = 2.8f, Tint = VioletHot,
                Offset = new Vector3(0f, 1f, 0f),
                Intensity = Curve(0f, 0f, 0.08f, 1f, 0.48f, 0.38f, 0.88f, 0f),
            }));

        Add(Def("revision-return", 0.52f,
            new QuadElement
            {
                Shader = ShaderRing, Orientation = QuadOrientation.Ground,
                Size = 2.6f, Tier = 2.2f, Tint = SandLit, Thickness = 0.10f, Softness = 0.2f,
                Offset = new Vector3(0f, 0.07f, 0f),
                Radius = Curve(0f, 0.05f, 0.18f, 0.92f),
                Alpha = Curve(0f, 1f, 0.30f, 0.72f, 0.52f, 0f),
            },
            new ParticleElement
            {
                Burst = 14, Tier = 1.8f, Tint = VioletHot,
                Offset = new Vector3(0f, 0.16f, 0f),
                LifeMin = 0.24f, LifeMax = 0.48f, SpeedMin = 0.8f, SpeedMax = 2.0f,
                SizeMin = 0.04f, SizeMax = 0.10f, ShapeAngle = 20f, ShapeRadius = 0.42f,
                ShapeRotation = Up, Gravity = -0.45f,
            }));

        return d;
    }

    /// <summary>The windup half of ONE chassis's cast sentence: the same double rim and rising wisps
    /// as the generic <c>cast-aura</c>, plus that chassis's era sigil turning slowly under the
    /// caster. Every hero therefore speaks the same sentence and is told apart by its engraving and
    /// by the lane color the tell brings — which is why no element here authors a Tint: the tell's
    /// motionColor IS the lane (combat-spectacle §1-2).
    ///
    /// The sigil is <see cref="QuadElement.RequireTexture"/>: until the GenerateAsset batch has
    /// produced <c>Board/FX/Sigils/&lt;chassis&gt;</c> this recipe degrades to exactly today's
    /// cast-aura instead of drawing a white disc.
    ///
    /// Tiers sit a little under the generic recipe's (0.6-0.85 vs 0.7-1.0) because these recipes are
    /// shared with the S-crowns, whose motionGlow runs to ~5: at the T2 authoring glow
    /// (<see cref="GlowRef"/>) the windup reads T0/T1 exactly as §2 asks, and at a T3 glow it lifts
    /// to ~1.3 rather than ~2 — a windup that warns without out-shouting its own release.</summary>
    private static VfxDef CastAura(string chassis) => Sustained("cast-aura-" + chassis, 0.25f,
        new QuadElement
        {
            Shader = ShaderSigil, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
            Size = 1.75f, Tier = 0.8f,
            Texture = "Board/FX/Sigils/" + chassis, RequireTexture = true,
            Offset = new Vector3(0f, 0.05f, 0f),
            Rotation = Loop(AnimationCurve.Linear(0f, 0f, 9f, 1f)),   // one slow turn per 9 s
            Alpha = Curve(0f, 0f, 0.22f, 1f),
        },
        new QuadElement
        {
            Shader = ShaderRing, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
            Size = 2.0f, Tier = 0.8f, Thickness = 0.06f, Softness = 0.20f,
            Offset = new Vector3(0f, 0.06f, 0f),
            Radius = Curve(0f, 0.85f),
            Arc = Curve(0f, 0.85f),
            Rotation = Loop(AnimationCurve.Linear(0f, 0f, 3f, 1f)),
            Alpha = Curve(0f, 0f, 0.15f, 1f),
        },
        new QuadElement
        {
            Shader = ShaderRing, Orientation = QuadOrientation.Ground, Anchor = VfxAnchor.FollowUnit,
            Size = 1.3f, Tier = 0.6f, Thickness = 0.05f, Softness = 0.18f,
            Offset = new Vector3(0f, 0.06f, 0f),
            Radius = Curve(0f, 0.8f),
            Arc = Curve(0f, 0.6f),
            Rotation = Loop(AnimationCurve.Linear(0f, 1f, 4f, 0f)),
            Alpha = Curve(0f, 0f, 0.20f, 0.9f),
        },
        new ParticleElement
        {
            Anchor = VfxAnchor.FollowUnit, Rate = 8f, Burst = 0, Tier = 0.85f, MaxParticles = 24,
            LifeMin = 0.6f, LifeMax = 1.0f, SpeedMin = 0.6f, SpeedMax = 1.1f,
            SizeMin = 0.05f, SizeMax = 0.10f, ShapeAngle = 12f, ShapeRadius = 0.45f,
            ShapeRotation = Up, Gravity = -0.25f,
        });

    private static readonly Vector3 Up = new Vector3(-90f, 0f, 0f); // aim a cone/circle at the sky

    private static VfxDef Def(string id, float duration, params VfxElement[] els) =>
        new VfxDef { Id = id, Duration = duration, Elements = els };

    private static VfxDef Sustained(string id, float fadeSeconds, params VfxElement[] els) =>
        new VfxDef { Id = id, Duration = fadeSeconds, Sustained = true, Elements = els };

    /// <summary>An AnimationCurve from (time, value) pairs, in SECONDS since the effect played.
    /// Past its last key a curve holds — which is what lets a sustained recipe fade in and stay.</summary>
    private static AnimationCurve Curve(params float[] timeValuePairs)
    {
        var keys = new Keyframe[timeValuePairs.Length / 2];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    private static AnimationCurve Loop(AnimationCurve c)
    {
        c.preWrapMode = WrapMode.Loop;
        c.postWrapMode = WrapMode.Loop;
        return c;
    }

    // ---- shared render resources --------------------------------------------

    private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
    private static Texture2D _noise, _softDot;
    private static Mesh _quad, _hex;

    /// <summary>One shared material per (shader, texture) — per-instance variation goes through
    /// MaterialPropertyBlocks (quads) or the ParticleSystem's own start color (particles), never
    /// through material copies.</summary>
    public static Material MaterialFor(string shaderName, Texture texture)
    {
        string key = shaderName + "|" + (texture != null ? texture.name : "");
        if (_materials.TryGetValue(key, out var mat) && mat != null) return mat;

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            if (_warned.Add(shaderName))
                Debug.LogWarning($"[Vfx] shader '{shaderName}' not found — using URP/Unlit");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        if (texture != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        _materials[key] = mat;
        return mat;
    }

    /// <summary>Tileable fBm value noise, built once. Procedural on purpose (fx-runtime risks list):
    /// it keeps P1 free of generated-asset dependencies and out of the repo.</summary>
    public static Texture2D Noise
    {
        get
        {
            if (_noise != null) return _noise;
            const int n = 256;
            _noise = new Texture2D(n, n, TextureFormat.RGBA32, true)
            { name = "~vfx_noise", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float amp = 0.55f, f = 4f, sum = 0f;
                    for (int o = 0; o < 3; o++) { sum += Tileable(u, v, f) * amp; amp *= 0.5f; f *= 2f; }
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(sum * 255f), 0, 255);
                    px[y * n + x] = new Color32(b, b, b, 255);
                }
            _noise.SetPixels32(px);
            _noise.Apply();
            return _noise;
        }
    }

    /// <summary>Perlin sampled on a torus: the four wrapped corners are blended by (u,v), so the
    /// result tiles seamlessly and a scrolling _Phase never shows a seam.</summary>
    private static float Tileable(float u, float v, float f)
    {
        float P(float x, float y) => Mathf.PerlinNoise(x + 128.7f, y + 71.3f);
        float a = P(u * f, v * f);
        float b = P((u - 1f) * f, v * f);
        float c = P(u * f, (v - 1f) * f);
        float e = P((u - 1f) * f, (v - 1f) * f);
        return a * (1f - u) * (1f - v) + b * u * (1f - v) + c * (1f - u) * v + e * u * v;
    }

    /// <summary>The default particle sprite: a soft radial mote in the alpha channel. Additive
    /// premultiply in the shader means alpha IS the shape, so one 64² texture serves every recipe.</summary>
    public static Texture2D SoftDot
    {
        get
        {
            if (_softDot != null) return _softDot;
            const int n = 64;
            _softDot = new Texture2D(n, n, TextureFormat.RGBA32, true)
            { name = "~vfx_dot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Pow(1f - d, 2.2f) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            _softDot.SetPixels32(px);
            _softDot.Apply();
            return _softDot;
        }
    }

    /// <summary>Particle sprite by Resources path; empty or missing falls back to the soft dot, so a
    /// recipe can name a flipbook before the GenerateAsset batch produces it.</summary>
    public static Texture2D ParticleTexture(string path)
    {
        if (string.IsNullOrEmpty(path)) return SoftDot;
        var t = Resources.Load<Texture2D>(path);
        if (t != null) return t;
        if (_warned.Add(path)) Debug.LogWarning($"[Vfx] particle texture '{path}' missing — using the soft dot");
        return SoftDot;
    }

    /// <summary>Sigil/mask texture by Resources path, or null to leave the shader's white default
    /// (an un-authored sigil then reads as a plain disc instead of vanishing).</summary>
    public static Texture2D SigilTexture(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var t = Resources.Load<Texture2D>(path);
        if (t == null && _warned.Add(path)) Debug.LogWarning($"[Vfx] sigil texture '{path}' missing");
        return t;
    }

    /// <summary>Unit quad in the XY plane, UV 0..1 — the polar shaders read radius 1 at its edge.</summary>
    public static Mesh QuadMesh
    {
        get
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "~vfx_quad", hideFlags = HideFlags.DontSave };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
            };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            _quad.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
            return _quad;
        }
    }

    /// <summary>Unit hex in the XY plane WITH UVs (centre 0.5,0.5, corners on the unit circle) — the
    /// board's own tile mesh has no UVs, so polar/scrolling shaders need this second one. Corner
    /// angles match the board's convention (60°·i + 30°), so a Ground-oriented hex decal lands
    /// exactly on a board tile.</summary>
    public static Mesh HexMesh
    {
        get
        {
            if (_hex != null) return _hex;
            _hex = new Mesh { name = "~vfx_hex", hideFlags = HideFlags.DontSave };
            var verts = new Vector3[7];
            var uvs = new Vector2[7];
            var norms = new Vector3[7];
            verts[0] = Vector3.zero; uvs[0] = new Vector2(0.5f, 0.5f); norms[0] = -Vector3.forward;
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i + 30f);
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                verts[i + 1] = new Vector3(cx * 0.5f, cy * 0.5f, 0f);
                uvs[i + 1] = new Vector2(0.5f + cx * 0.5f, 0.5f + cy * 0.5f);
                norms[i + 1] = -Vector3.forward;
            }
            var tris = new int[18];
            for (int i = 0; i < 6; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + i;
                tris[i * 3 + 2] = 1 + (i + 1) % 6;
            }
            _hex.vertices = verts; _hex.uv = uvs; _hex.normals = norms; _hex.triangles = tris;
            _hex.RecalculateBounds();
            return _hex;
        }
    }
}
