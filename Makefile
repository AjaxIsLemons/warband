# warband — homeserv-side dev ops. (Unity itself builds/runs on the Windows box.)
# The sim is authored + tested here; the Unity client is driven over the remote-dev
# pipeline (Syncthing + official Unity MCP relay over SSH). See CLAUDE.md + docs/vault/.

.PHONY: help sync-status mcp-test test unity-sim replay scenarios coverage

WIN_SSH       ?= jwjwi@192.168.1.102
WIN_KEY       ?= $(HOME)/.ssh/homeserv_to_windows
WIN_DEVICE    ?= PBZDGYN-E7NY4CY-OPH2V7E-GYKUNQK-TIUKG4H-X23M7YM-K5FLC7K-AVR7WQB
ST_FOLDER     ?= warband
UNITY_PLUGINS ?= client/Assets/Plugins/Warband
REPLAY_OUT    ?= client/Assets/StreamingAssets/replay.bytes
SCENARIO_OUT  ?= client/Assets/StreamingAssets/replays

help: ## List targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*?## "}{printf "  %-14s %s\n", $$1, $$2}'

test: ## Run the sim test suite (headless, homeserv)
	@dotnet test Warband.slnx

replay: ## Run the sample fight and write a golden replay (+round-trip check) into the client's StreamingAssets
	@dotnet run --project sim/Warband.Viewer -c Release -- --dump $(REPLAY_OUT)

scenarios: ## Generate the data-driven scenario replay set (scenarios.json → real Catalog builds) into the client + print a coverage report
	@dotnet run --project sim/Warband.Viewer -c Release -- --scenarios $(SCENARIO_OUT)

coverage: ## Event-signature coverage of one replay: make coverage F=client/Assets/StreamingAssets/replays/castfest.bytes
	@dotnet run --project sim/Warband.Viewer -c Release -- --coverage $(F)

unity-sim: ## Build Warband.Sim as a netstandard2.1 DLL into the Unity client's Plugins/ (Syncthing carries it to Windows)
	@dotnet build sim/Warband.Sim/Warband.Sim.csproj -c Release -v quiet --nologo
	@mkdir -p $(UNITY_PLUGINS)
	@cp sim/Warband.Sim/bin/Release/netstandard2.1/Warband.Sim.dll $(UNITY_PLUGINS)/
	@cp sim/Warband.Sim/bin/Release/netstandard2.1/Warband.Sim.pdb $(UNITY_PLUGINS)/ 2>/dev/null || true
	@echo "-> $(UNITY_PLUGINS)/Warband.Sim.dll  ($$(du -h $(UNITY_PLUGINS)/Warband.Sim.dll | cut -f1))"

sync-status: ## Is the Windows peer caught up on the warband Syncthing folder?
	@CFG=$$(ls ~/.local/state/syncthing/config.xml ~/.config/syncthing/config.xml 2>/dev/null | head -1); \
	KEY=$$(grep -oPm1 '(?<=<apikey>)[^<]+' "$$CFG"); \
	curl -s -H "X-API-Key: $$KEY" "http://127.0.0.1:8384/rest/db/completion?folder=$(ST_FOLDER)&device=$(WIN_DEVICE)"

mcp-test: ## Can we reach the official Unity MCP relay on the Windows box (SSH)?
	@ssh -i $(WIN_KEY) -o ConnectTimeout=5 $(WIN_SSH) \
		"if exist C:\Users\jwjwi\.unity\relay\relay_win.exe (echo relay_win.exe present) else (echo MISSING relay_win.exe & exit 1)" \
		&& echo "SSH + relay OK (MCP itself is spawned per-session by Claude via .mcp.json)"
