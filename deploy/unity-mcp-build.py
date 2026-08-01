#!/usr/bin/env python3
"""Narrow MCP client for triggering WarbandBuild in the already-open Windows Editor."""

from __future__ import annotations

import argparse
import json
import os
import queue as queue_module
import subprocess
import sys
import threading
import time
from typing import Any


PROTOCOL_VERSION = "2025-06-18"


class McpError(RuntimeError):
    pass


class UnityMcp:
    def __init__(self, host: str, identity: str, project_path: str, timeout: int) -> None:
        remote = (
            r"C:\Users\jwjwi\.unity\relay\relay_win.exe --mcp --log error --project-path "
            + project_path
        )
        self._proc = subprocess.Popen(
            [
                "ssh",
                "-T",
                "-i",
                identity,
                "-o",
                "ConnectTimeout=5",
                host,
                remote,
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=1,
        )
        if (
            self._proc.stdin is None
            or self._proc.stdout is None
            or self._proc.stderr is None
        ):
            raise McpError("failed to open Unity MCP stdio")
        self._lines: queue_module.Queue[str] = queue_module.Queue()
        self._stderr_lines: list[str] = []
        self._reader = threading.Thread(target=self._read_lines, daemon=True)
        self._stderr_reader = threading.Thread(target=self._read_stderr, daemon=True)
        self._reader.start()
        self._stderr_reader.start()
        self._timeout = timeout
        self._next_id = 1
        self._initialize()

    def _read_lines(self) -> None:
        assert self._proc.stdout is not None
        for line in self._proc.stdout:
            self._lines.put(line)
        self._lines.put("")

    def _read_stderr(self) -> None:
        assert self._proc.stderr is not None
        for line in self._proc.stderr:
            self._stderr_lines.append(line.rstrip())

    def _diagnostics(self) -> str:
        tail = self._stderr_lines[-20:]
        return "\n".join(tail)

    def close(self) -> None:
        try:
            if self._proc.stdin is not None:
                self._proc.stdin.close()
        except BrokenPipeError:
            pass
        self._proc.terminate()
        try:
            self._proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            self._proc.kill()
            self._proc.wait()

    def _send(self, message: dict[str, Any]) -> None:
        assert self._proc.stdin is not None
        self._proc.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
        self._proc.stdin.flush()

    def _request(self, method: str, params: dict[str, Any]) -> dict[str, Any]:
        request_id = self._next_id
        self._next_id += 1
        self._send(
            {
                "jsonrpc": "2.0",
                "id": request_id,
                "method": method,
                "params": params,
            }
        )

        deadline = time.monotonic() + self._timeout
        while time.monotonic() < deadline:
            remaining = max(0.0, deadline - time.monotonic())
            try:
                line = self._lines.get(timeout=remaining)
            except queue_module.Empty:
                break
            if line == "":
                raise McpError(
                    f"Unity MCP relay exited while waiting for {method} "
                    f"(exit={self._proc.poll()})\n{self._diagnostics()}"
                )
            line = line.strip()
            if not line.startswith("{"):
                continue
            try:
                response = json.loads(line)
            except json.JSONDecodeError:
                continue
            if response.get("id") != request_id:
                continue
            if "error" in response:
                raise McpError(f"{method}: {response['error']}")
            return response
        raise McpError(
            f"Unity MCP timed out after {self._timeout}s waiting for {method}\n"
            f"{self._diagnostics()}"
        )

    def _initialize(self) -> None:
        self._request(
            "initialize",
            {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {},
                "clientInfo": {"name": "warband-release", "version": "1"},
            },
        )
        self._send(
            {
                "jsonrpc": "2.0",
                "method": "notifications/initialized",
                "params": {},
            }
        )

    def run_command(self, title: str, code: str) -> str:
        response = self._request(
            "tools/call",
            {
                "name": "Unity_RunCommand",
                "arguments": {"Title": title, "Code": code},
            },
        )
        result = response.get("result", {})
        texts = [
            item.get("text", "")
            for item in result.get("content", [])
            if item.get("type") == "text"
        ]
        detail = "\n".join(texts)
        if result.get("isError"):
            raise McpError(detail or "Unity_RunCommand returned an error")
        for text in texts:
            try:
                nested = json.loads(text)
            except json.JSONDecodeError:
                continue
            if nested.get("success") is False:
                raise McpError(nested.get("error") or text)
            data = nested.get("data", {})
            if data.get("isCompilationSuccessful") is False:
                raise McpError(data.get("compilationLogs") or text)
            if data.get("isExecutionSuccessful") is False:
                raise McpError(data.get("executionLogs") or text)
        return detail


def invoke(args: argparse.Namespace, title: str, code: str) -> str:
    client = UnityMcp(args.host, args.identity, args.project_path, args.timeout)
    try:
        return client.run_command(title, code)
    finally:
        client.close()


def probe(args: argparse.Namespace) -> int:
    code = r"""
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        System.Action<string> queueBuild = WarbandBuild.QueueWindowsClientBuild;
        result.Log("Warband release automation: {0}; queue={1}",
                   WarbandBuild.AutomationReadiness(), queueBuild != null);
    }
}
"""
    invoke(args, "Probe Warband release automation", code)
    print(">> Unity release automation probe: READY")
    return 0


def queue_build(args: argparse.Namespace) -> int:
    refresh = r"""
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Warband Editor is in Play Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport |
                              ImportAssetOptions.ForceUpdate);
        result.Log("Warband assets refreshed for release.");
    }
}
"""
    try:
        invoke(args, "Refresh Warband before release", refresh)
    except McpError as exc:
        # A script import can reload the domain and sever this short-lived command. The queue
        # retry below is the authoritative readiness check.
        print(f">> Unity refresh interrupted the relay ({exc}); waiting for compilation.")

    request_literal = json.dumps(args.request_id)
    build = f"""
using UnityEditor;

internal class CommandScript : IRunCommand
{{
    public void Execute(ExecutionResult result)
    {{
        string readiness = WarbandBuild.AutomationReadiness();
        if (readiness != "READY")
            throw new System.InvalidOperationException(
                "Warband release automation is not ready: " + readiness);
        WarbandBuild.QueueWindowsClientBuild({request_literal});
        result.Log("Queued Warband release build {args.request_id}.");
    }}
}}
"""

    deadline = time.monotonic() + args.ready_timeout
    last_error = ""
    while time.monotonic() < deadline:
        try:
            invoke(args, "Queue Warband Windows release build", build)
            print(f">> Unity accepted build request {args.request_id}.")
            return 0
        except McpError as exc:
            last_error = str(exc)
            if "PLAY_MODE" in last_error or "Play Mode" in last_error:
                raise McpError(
                    "Warband Unity is in Play Mode; exit Play Mode and run make release again."
                ) from exc
            print(f">> Unity not ready yet: {last_error}", file=sys.stderr)
            time.sleep(5)
    raise McpError(
        f"Unity did not become release-ready within {args.ready_timeout}s: {last_error}"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("probe", "queue"))
    parser.add_argument("--request-id", default="")
    parser.add_argument(
        "--host", default=os.environ.get("WIN_SSH", "jwjwi@192.168.1.102")
    )
    parser.add_argument(
        "--identity",
        default=os.environ.get(
            "WIN_KEY", os.path.expanduser("~/.ssh/homeserv_to_windows")
        ),
    )
    parser.add_argument(
        "--project-path", default=r"C:\Dev\game\warband\client"
    )
    parser.add_argument("--timeout", type=int, default=90)
    parser.add_argument("--ready-timeout", type=int, default=300)
    args = parser.parse_args()
    if args.command == "queue" and not args.request_id:
        parser.error("--request-id is required for queue")

    try:
        return probe(args) if args.command == "probe" else queue_build(args)
    except (McpError, OSError) as exc:
        print(f"Unity release trigger failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
