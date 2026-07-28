// SFX audition surface — the browser page for judging warband's sound families.
//
// Shape follows Shoota's site/sfx.go, with one deliberate simplification: this serves the page
// `tools/sfx/sfx.py sheet` already writes, rather than re-deriving it in Go. The measurements
// (onset, audible length, peak, crest) live in exactly one place — the Python tool that also gates
// and bakes them — so the page can never disagree with `make sfx-lint`. Regenerate with
// `make sfx` and refresh; there is no build step here.
//
// ADMIN-ONLY, and fail-closed. Note that the launcher gate is deliberately open to any signed-in
// Discord account (see main.go), so "signed in" is NOT a meaningful gate for work-in-progress
// audio — every friend would see it. With WARBAND_ADMIN_IDS unset, /sfx 404s rather than falling
// open, because an unconfigured allowlist must never mean "public".
package main

import (
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strings"
)

// isAdmin reports whether this request carries a session on the WARBAND_ADMIN_IDS list.
// Empty list = nobody, by design.
func isAdmin(r *http.Request) (name string, ok bool) {
	if len(cfg.adminIDs) == 0 {
		return "", false
	}
	id, name, signedIn := getSession(r)
	if !signedIn {
		return "", false
	}
	for _, allowed := range cfg.adminIDs {
		if allowed == id {
			return name, true
		}
	}
	return "", false
}

// handleSfx serves docs/audio/ — index.html at the root, plus the baked/ and src/ clips the page's
// <audio> elements reference. One FileServer covers all of it: the generated page uses relative
// URLs, so mounting it under /sfx/ makes `baked/ui/tick_1.wav` resolve to `/sfx/baked/ui/tick_1.wav`
// with no rewriting.
func handleSfx(w http.ResponseWriter, r *http.Request) {
	name, ok := isAdmin(r)
	if !ok {
		// 404, not 403: an unauthorised visitor learns nothing about what is here.
		http.NotFound(w, r)
		return
	}
	if _, err := os.Stat(filepath.Join(cfg.sfxDir, "index.html")); err != nil {
		http.Error(w, "no audition sheet yet — run `make sfx` on homeserv", http.StatusNotFound)
		return
	}
	if r.URL.Path == "/sfx" {
		http.Redirect(w, r, "/sfx/", http.StatusFound)
		return
	}
	if strings.HasSuffix(r.URL.Path, "/") || r.URL.Path == "/sfx/" {
		log.Printf("sfx audition: %s", name)
	}
	// The sheet is regenerated constantly while tuning; a cached page showing the previous bake is
	// the exact failure this tool exists to prevent.
	w.Header().Set("Cache-Control", "no-store, must-revalidate")
	// Go's mime table resolves .wav inconsistently across distros; be explicit so every browser
	// gets a playable <audio> source.
	if strings.HasSuffix(r.URL.Path, ".wav") {
		w.Header().Set("Content-Type", "audio/wav")
	}
	http.StripPrefix("/sfx/", http.FileServer(http.Dir(cfg.sfxDir))).ServeHTTP(w, r)
}
