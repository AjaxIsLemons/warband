using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Warband.Sim;

// Bridge smoke test: proves real project code (not the MCP sandbox) compiles
// against and runs the Warband.Sim managed plugin. Invoke via menu "Warband/Sim
// Smoke Test" or reflection. Throwaway — delete once the render driver exists.
public static class WarbandSimSmoke
{
    [MenuItem("Warband/Sim Smoke Test")]
    public static void Run()
    {
        var initial = new List<PlaybackUnit>
        {
            new PlaybackUnit { Id = 1, Team = 0, Name = "A", MaxHp = 100, Hp = 100, Pos = new Hex(0, 0) },
            new PlaybackUnit { Id = 2, Team = 1, Name = "B", MaxHp = 100, Hp = 100, Pos = new Hex(3, 0) },
        };
        var pb = PlaybackState.From(initial);
        pb.AdvanceToTick(new List<BattleEvent>(), 0);
        Debug.Log($"[WarbandSimSmoke] OK: units={pb.Units.Count} tick={pb.Tick} viewHash={pb.ViewHash()}");
    }
}
