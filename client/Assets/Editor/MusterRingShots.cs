using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Warband.Content;
using Warband.Sim;

/// <summary>
/// Frozen captures of the deployment muster rings (ADR 0014).
///
/// The UI QA deploy fixture builds a <c>DeployModel</c> and nothing else — it exercises the panels
/// and never touches the board — so it cannot see a ring at all. This drives the real board API the
/// deployment screen drives (<see cref="ReplayPlayer.SetMusterRings"/>) against real composed kits,
/// which is the only way to look at this without a live run in Play Mode.
///
/// Edit-mode only and MCP-drivable, so a capture never has to steal Play Mode or window focus.
/// </summary>
public static class MusterRingShots
{
    /// <summary>Long enough for the rim to finish tracing (0.2 s) and the floor to fade up.</summary>
    private const float SettleSeconds = 1.2f;

    [MenuItem("Warband/MCP/Capture Muster Rings")]
    public static void CaptureMusterRings()
    {
        var player = Object.FindFirstObjectByType<ReplayPlayer>();
        if (player == null) { Debug.LogError("[MusterRings] no ReplayPlayer in the scene"); return; }
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[MusterRings] no Camera.main to render from"); return; }

        // A deliberately awkward formation: the Banneret's Company is r1 and the Cleric's Mercy
        // Aura is r2, so the two rings OVERLAP — which is the busiest case the board can produce
        // with today's content, and the one worth looking at before calling the palette calm.
        var placed = new (string Chassis, Hex At, string[] Nodes)[]
        {
            ("banneret",   Hex.FromRowCol(1, 2), new string[0]),
            ("pyromancer", Hex.FromRowCol(1, 3), new string[0]),   // adjacent → sworn in
            ("cleric",     Hex.FromRowCol(2, 1), new string[0]),
            ("berserker",  Hex.FromRowCol(0, 0), new string[0]),   // out of everything
        };

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TempCaptures"));
        Directory.CreateDirectory(outDir);

        // Selected = the Banneret, then nothing: the two states the deployment screen alternates
        // between, and the pair that shows whether "quiet" is actually quiet.
        foreach (var (label, selected) in new[] { ("banner-selected", 0), ("none-selected", -1) })
        {
            var units = new List<PlaybackUnit>();
            var rings = new List<ReplayPlayer.MusterRing>();
            for (int i = 0; i < placed.Length; i++)
            {
                var def = Loadout.Compose(Kits.Chassis[placed[i].Chassis],
                    nodes: placed[i].Nodes.Select(n => Kits.Nodes[n])).Def;
                units.Add(PlaybackUnit.From(UnitState.Spawn(i, 0, def, placed[i].At)));
                var seats = MechanicalRulePresenter.MusterSeats(def, placed[i].At);
                if (seats.Count > 0)
                    rings.Add(new ReplayPlayer.MusterRing(i, seats.ToList(), i == selected));
            }

            player.ShowSnapshot(units);
            player.SetPlanningSelection(selected);
            player.SetMusterRings(rings);
            // No frames elapse inside a menu item, so advance the ring clock by hand or the
            // capture lands on age 0 — an untraced rim and an invisible floor.
            player.StepMusterRings(SettleSeconds);

            string path = Path.Combine(outDir, $"muster-rings-{label}.png");
            RenderShots.RenderTo(cam, path, 1600, 900);
            Debug.Log($"[MusterRings] wrote {path} ({rings.Count} rings, selected={selected})");
        }
    }
}
