#!/usr/bin/env python3
"""Compile the Unity client's C# headlessly on homeserv, without Unity.

Unity lives on the Windows box and is a singleton shared with parallel agent sessions, so a syntax
or API error found only at import time costs a Syncthing round-trip AND the lock. This compiles
every script in `client/Assets/Scripts/` against real Unity reference assemblies borrowed from
Shoota's Linux server build, catching missing usings, wrong overloads and typos in seconds.

    make check-client        # or: python3 tools/check-client-compile.py

Runtime scripts are compiled with UNITY_EDITOR and DEVELOPMENT_BUILD defined (see the shim in
main()), so the `#if UNITY_EDITOR` fixture/report blocks inside them ARE checked. They were not
until 2026-07-29, and a stale UI QA fixture reached Unity green from here as a result.

What it CANNOT catch, by construction:
  * Editor scripts (`client/Assets/Editor/`) — a player build ships no `UnityEditor.dll`, so those
    are excluded. They still type-check only inside Unity.
  * MonoBehaviour wiring, serialized fields, asset GUIDs, anything the AssetDatabase owns.
  * Unity-version API differences: the reference assemblies are Shoota's Unity, not warband's.
    Close enough to catch real mistakes, not a substitute for the editor console.
"""

import glob
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANAGED = os.path.expanduser(
    "~/Work/Shoota/Shoota/Builds/LinuxServer/ShootaServer_Data/Managed")
OUT = os.path.join(os.environ.get("TMPDIR", "/tmp"), "warband-client-compile")

# Packages the client uses that are not part of the UnityEngine core set. Without these the run
# drowns in CS0246 noise and a real error is invisible.
EXTRA = [
    "Newtonsoft.Json.dll",
    "Unity.InputSystem.dll",
    "Unity.RenderPipelines.Universal.Runtime.dll",
    "Unity.RenderPipelines.Core.Runtime.dll",
    "Unity.Mathematics.dll",
    "Unity.Burst.dll",
]


def main():
    if not os.path.isdir(MANAGED):
        print(f"!! no reference assemblies at {MANAGED}\n"
              f"   They come from Shoota's LinuxServer build; build it or point MANAGED elsewhere.",
              file=sys.stderr)
        return 2

    srcs = sorted(glob.glob(os.path.join(ROOT, "client/Assets/Scripts/**/*.cs"), recursive=True))
    refs = sorted(glob.glob(os.path.join(MANAGED, "UnityEngine*.dll")))
    refs += [os.path.join(MANAGED, e) for e in EXTRA]
    refs += glob.glob(os.path.join(ROOT, "client/Assets/Plugins/Warband/Warband.*.dll"))
    refs = [r for r in refs if os.path.exists(r)]

    os.makedirs(OUT, exist_ok=True)

    # Runtime scripts touch exactly three UnityEditor symbols, all inside `#if UNITY_EDITOR`
    # debug paths. Stubbing them is what lets UNITY_EDITOR be defined below, which in turn is
    # what puts the Editor*Fixture methods (the UI QA fixtures) under the compiler at all.
    # If a fourth symbol ever appears the build fails loudly here — which is the right outcome,
    # because a runtime script reaching further into UnityEditor is itself worth a look.
    shim = os.path.join(OUT, "_unity_editor_shim.cs")
    with open(shim, "w") as f:
        f.write("namespace UnityEditor {\n"
                "  [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]\n"
                "  internal sealed class MenuItemAttribute : System.Attribute {\n"
                "    public MenuItemAttribute(string itemName) { }\n"
                "    public MenuItemAttribute(string itemName, bool isValidateFunction) { }\n"
                "    public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }\n"
                "  }\n"
                "  internal static class EditorApplication {\n"
                "    public static bool isPlaying { get; set; }\n"
                "    public static bool isCompiling { get; set; }\n"
                "  }\n"
                "  internal static class AssetDatabase {\n"
                "    public static void ImportAsset(string path) { }\n"
                "    public static void Refresh() { }\n"
                "  }\n}\n")
    srcs = srcs + [shim]

    proj = os.path.join(OUT, "client-check.csproj")
    with open(proj, "w") as f:
        f.write("<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                "  <PropertyGroup>\n"
                "    <TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion>\n"
                "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n"
                "    <AssemblyName>WarbandClientCheck</AssemblyName>\n"
                # DEVELOPMENT_BUILD turns on every `#if UNITY_EDITOR || DEVELOPMENT_BUILD` block
                # in a RUNTIME script — the Editor*Fixture methods on RunShell, the layout-report
                # helpers on each view. Those are ~15 blocks the check was blind to, and on
                # 2026-07-29 one of them (a stale DeployModel fixture) reached Unity green from
                # here. No `UnityEditor` symbol is legal in these files anyway (runtime asmdef),
                # so enabling the define costs nothing and closes the gap.
                "    <DefineConstants>UNITY_EDITOR;DEVELOPMENT_BUILD</DefineConstants>\n"
                "    <NoWarn>CS0169;CS0414;CS0649;CS0108;CS0114</NoWarn>\n"
                "  </PropertyGroup>\n  <ItemGroup>\n")
        for s in srcs:
            f.write(f'    <Compile Include="{s}" />\n')
        f.write("  </ItemGroup>\n  <ItemGroup>\n")
        for r in refs:
            f.write(f'    <Reference Include="{os.path.basename(r)[:-4]}">'
                    f'<HintPath>{r}</HintPath><Private>false</Private></Reference>\n')
        f.write("  </ItemGroup>\n</Project>\n")

    print(f"compiling {len(srcs)} scripts against {len(refs)} reference assemblies...")
    res = subprocess.run(["dotnet", "build", proj, "-v", "q", "--nologo"],
                         capture_output=True, text=True, cwd=OUT)
    errors = [l for l in res.stdout.splitlines() if "error CS" in l]
    if errors:
        prefix = os.path.join(ROOT, "client/Assets/Scripts/")
        for line in errors[:40]:
            print("  " + line.replace(prefix, "").split(" [")[0])
        print(f"\nFAILED — {len(errors)} error(s)")
        return 1
    print("PASS — 0 errors (editor scripts excluded; they type-check only in Unity)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
