using UnityEngine;

/// <summary>
/// A VFX recipe: what an effect id LOOKS like. Recipes are C# (see fx-runtime.md "Core decisions"):
/// gradients and AnimationCurves don't survive JSON, and code syncs to the Windows editor exactly
/// like the rest of the client. tuning.json stays the BINDING layer — which id fires when, and the
/// per-tell motionColor/motionGlow/motionScale that tint any recipe — so the F1 loop never needs a
/// recompile to retune a tell.
///
/// Every element steps off the Director's clock through <see cref="VfxInstance.Step"/>. There is no
/// Update, no coroutine, and no Time.deltaTime below this line, which is what makes a frozen contact
/// sheet reproduce a live frame by construction.
/// </summary>
public sealed class VfxDef
{
    public string Id = "";
    /// <summary>Seconds the instance lives. For a <see cref="Sustained"/> recipe this is instead the
    /// fade-out length AFTER EndSustain() — the recipe itself runs as long as the caller holds it.</summary>
    public float Duration = 0.5f;
    /// <summary>Runs until the caller calls EndSustain (a cast aura burning through a windup).</summary>
    public bool Sustained;
    public VfxElement[] Elements = new VfxElement[0];
}

/// <summary>World = the effect stays where it was played. FollowUnit = it rides the unit's Root
/// (the caster's aura), re-anchored every Step.</summary>
public enum VfxAnchor { World, FollowUnit }

/// <summary>Ground = flat on the board (hex decals, cast circles). Billboard = faces the camera
/// (release flashes, motes). UpFacing = a vertical sheet standing on the ground, turned to face the
/// play direction — a slash arc or a wall of flame seen edge-on from the side it travels.</summary>
public enum QuadOrientation { Ground, Billboard, UpFacing }

public enum ParticleShape { Point, Cone, Sphere, Hemisphere, Circle }

/// <summary>Shared by every element kind. <see cref="Tier"/> is the combat-spectacle intensity tier
/// (T0 ≤0.8 · T1 ≤1.0 · T2 1.3-1.8 · T3 2.5-4.0) expressed as an HDR multiplier at the AUTHORING
/// glow (<see cref="VfxLibrary.GlowRef"/>, the TellDef default) — so a recipe's bloom eligibility is
/// authored here, and a tell that dials motionGlow up or down scales it proportionally.
/// <see cref="Tint"/> null = wear the tell's motionColor; set = the recipe owns its lane color.</summary>
public abstract class VfxElement
{
    public VfxAnchor Anchor = VfxAnchor.World;
    public Vector3 Offset;
    public float Delay;
    public float Tier = 1f;
    public Color? Tint;
}

/// <summary>A pooled ParticleSystem, configured entirely from these fields at Create and stepped by
/// Simulate() from the Director clock. Curves are in NORMALIZED particle life (0..1), unlike the
/// quad tracks which are in seconds.</summary>
public sealed class ParticleElement : VfxElement
{
    public int Burst = 10;              // emitted at t=0
    public float Rate;                  // per second — trails and sustained emitters
    public float LifeMin = 0.25f, LifeMax = 0.45f;
    public float SpeedMin = 1.2f, SpeedMax = 2.2f;
    public float SizeMin = 0.08f, SizeMax = 0.16f;
    public float Gravity;               // >0 falls, <0 rises
    public float Drag;                  // velocity damping per second, 0..1
    public ParticleShape Shape = ParticleShape.Cone;
    public float ShapeAngle = 25f;
    public float ShapeRadius = 0.1f;
    /// <summary>Euler rotation of the emission shape. A cone/circle points along the system's local
    /// +Z, which the instance aims at the target; (-90,0,0) turns it to point UP instead (rising
    /// wisps, a ground ring of dust).</summary>
    public Vector3 ShapeRotation;
    /// <summary>Simulate in LOCAL space so the particles ride their anchor (an aura's wisps), rather
    /// than being left behind in the world (a trail).</summary>
    public bool Local;
    /// <summary>Stretched billboard along velocity — arrows, sparks, tracer cores.</summary>
    public bool Stretch;
    public float StretchScale = 2f;
    public bool Fade = true;            // alpha ramps out over the back of each particle's life
    public AnimationCurve SizeOverLife; // optional, x = 0..1 of particle life
    public bool Trails;                 // ParticleSystem Trails module (NOT TrailRenderer — banned)
    public float TrailRatio = 1f, TrailLifetime = 0.25f, TrailWidth = 0.4f;
    public string Texture;              // Resources path; null = the generated soft dot
    public int TilesX = 1, TilesY = 1;  // flipbook grid (1×1 = off)
    public int MaxParticles = 64;
}

/// <summary>A mesh quad (or UV'd hex) carrying one of the Warband shaders. The curve tracks are
/// evaluated in SECONDS since Play and written to a MaterialPropertyBlock — a track left null is
/// simply never written, so the shader keeps the static value set at Play. Writing a property a
/// shader doesn't declare is a harmless no-op, which is what lets one code path drive all five
/// quad shaders.</summary>
public sealed class QuadElement : VfxElement
{
    public string Shader = VfxLibrary.ShaderGlow;
    public bool Hex;                    // UV'd hex mesh instead of a quad (hex-snapped ground decals)
    public QuadOrientation Orientation = QuadOrientation.Billboard;
    public float Size = 1f;             // world diameter before the instance scale

    // Static shader inputs, set once at Play.
    public float Thickness = 0.12f, Softness = 0.15f, Intensity = 1f, EdgeFade = 0.35f, Falloff = 2.5f;
    public string Texture;              // _MainTex for the sigil shader (Resources path)
    public bool Noise;                  // bind the generated noise to _NoiseTex

    // Animated tracks (seconds). Set postWrapMode = Loop on a track to keep it running under a
    // sustained recipe (a spinning cast sigil, a scrolling floor).
    public AnimationCurve Radius, Arc, Alpha, Rotation, Phase, Scale;
}

/// <summary>A pooled point light. Deliberately rare: the live count is capped at
/// <see cref="VfxInstance.MaxLights"/> board-wide, and an instance that can't get one simply plays
/// without it (the recipe still reads — the light is seasoning, never the silhouette).</summary>
public sealed class LightElement : VfxElement
{
    public float Range = 4f;
    public AnimationCurve Intensity;    // seconds; null = flat 1
}
