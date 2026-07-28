#!/usr/bin/env python3
"""Compile the Unity client's C# headlessly on homeserv, without Unity.

Unity lives on the Windows box and is a singleton shared with parallel agent sessions, so a syntax
or API error found only at import time costs a Syncthing round-trip AND the lock. This compiles
every script in `client/Assets/Scripts/` against real Unity reference assemblies borrowed from
Shoota's Linux server build, catching missing usings, wrong overloads and typos in seconds.

    make check-client        # or: python3 tools/check-client-compile.py

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
    proj = os.path.join(OUT, "client-check.csproj")
    with open(proj, "w") as f:
        f.write("<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                "  <PropertyGroup>\n"
                "    <TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion>\n"
                "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n"
                "    <AssemblyName>WarbandClientCheck</AssemblyName>\n"
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
