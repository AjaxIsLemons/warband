package main

// POST /api/runlog — the playtest telemetry sink (roadmap item 19). The game uploads a finished
// run's JSONL lines fire-and-forget; this appends them to one file per UTC day and says nothing.
//
// Deliberately NOT the admin gate (friends' games can't sign in) and deliberately not open: the
// static key is a spam gate against drive-by scanners, the same trust level as accepting playtest
// logs at all. The body cap bounds abuse; the response is always empty — there is nothing a
// client can read back, so the endpoint can't be used to probe.

import (
	"bytes"
	"io"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"time"
)

const (
	runlogMaxBody = 1 << 20 // 1 MiB; a full run is tens of KB
	runlogKey     = "warband-playtest-1"
)

func handleRunlog(w http.ResponseWriter, r *http.Request) {
	if r.Header.Get("X-Warband-Key") != runlogKey {
		http.NotFound(w, r) // wrong key looks identical to no route
		return
	}
	body, err := io.ReadAll(http.MaxBytesReader(w, r.Body, runlogMaxBody))
	if err != nil {
		http.Error(w, "too large", http.StatusRequestEntityTooLarge)
		return
	}
	body = bytes.TrimSpace(body)
	if len(body) == 0 {
		w.WriteHeader(http.StatusNoContent)
		return
	}
	if err := os.MkdirAll(cfg.runlogDir, 0o755); err != nil {
		log.Printf("runlog: mkdir %s: %v", cfg.runlogDir, err)
		http.Error(w, "storage", http.StatusInternalServerError)
		return
	}
	name := filepath.Join(cfg.runlogDir,
		time.Now().UTC().Format("2006-01-02")+".jsonl")
	f, err := os.OpenFile(name, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o644)
	if err != nil {
		log.Printf("runlog: open %s: %v", name, err)
		http.Error(w, "storage", http.StatusInternalServerError)
		return
	}
	defer f.Close()
	// One Write so concurrent uploads can't interleave mid-run (O_APPEND is atomic per write).
	if _, err := f.Write(append(body, '\n')); err != nil {
		log.Printf("runlog: write %s: %v", name, err)
		http.Error(w, "storage", http.StatusInternalServerError)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}
