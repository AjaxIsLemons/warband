// warband download site — Discord sign-in gates the LAUNCHER, nothing else.
//
// Jake's call (2026-07-26): "no gate needed since this doesn't really need a server. I think if
// someone signs in with discord, they can dl the launcher."
//
// So the gate sits at exactly one layer, and the layering is the whole design:
//
//	/            landing page; signed out → sign-in button, signed in → launcher download
//	/launcher    WarbandLauncher.exe — REQUIRES a Discord session (a human, in a browser, once)
//	/releases/*  manifest + client zip — OPEN, because the launcher is not a browser and has no
//	             session. This is the same conclusion Shoota reached the hard way: its README notes
//	             the update zip is deliberately no longer tied to a browser Discord session.
//
// Gating /releases would mean shipping a secret inside every launcher binary to un-gate it, which is
// theatre — anyone with the exe can read it out. Gating the exe download instead is honest about
// what it buys: friend-scale friction, not DRM.
//
// Copied in shape from Shoota's site/main.go (HMAC-signed cookie sessions, state-cookie CSRF,
// Discord token exchange) and cut down hard: no database, no accounts, no telemetry, no admin.
package main

import (
	"context"
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"html/template"
	"io"
	"log"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

const (
	discordTokenURL = "https://discord.com/api/oauth2/token"
	discordUserURL  = "https://discord.com/api/users/@me"
	maxBody         = 1 << 20
	sessionCookie   = "warband_session"
	stateCookie     = "warband_state"
	launcherName    = "WarbandLauncher.exe"
	manifestName    = "warband-latest-win64.json"
)

type config struct {
	addr         string
	baseURL      string
	clientID     string
	clientSecret string
	cookieSecret []byte
	releasesDir  string
}

var (
	cfg        config
	httpClient = &http.Client{Timeout: 15 * time.Second}
)

func main() {
	cfg = config{
		addr:         envOr("WARBAND_ADDR", "127.0.0.1:8090"),
		baseURL:      strings.TrimRight(envOr("WARBAND_BASE_URL", "https://warband.inhouseboyz.com"), "/"),
		clientID:     os.Getenv("WARBAND_DISCORD_CLIENT_ID"),
		clientSecret: os.Getenv("WARBAND_DISCORD_CLIENT_SECRET"),
		cookieSecret: []byte(os.Getenv("WARBAND_COOKIE_SECRET")),
		releasesDir:  envOr("WARBAND_RELEASES_DIR", "/srv/warband-releases"),
	}
	if len(cfg.cookieSecret) < 16 {
		log.Fatal("WARBAND_COOKIE_SECRET missing or too short (need 16+ chars)")
	}
	if cfg.clientID == "" || cfg.clientSecret == "" {
		log.Print("WARN: Discord OAuth not configured — sign-in will fail until " +
			"WARBAND_DISCORD_CLIENT_ID/SECRET are set")
	}

	mux := http.NewServeMux()
	mux.HandleFunc("GET /{$}", handleHome)
	mux.HandleFunc("GET /auth/discord", handleLogin)
	mux.HandleFunc("GET /auth/discord/callback", handleCallback)
	mux.HandleFunc("GET /logout", handleLogout)
	mux.HandleFunc("GET /launcher", handleLauncher)
	// Open by design — see the package comment.
	mux.Handle("GET /releases/", http.StripPrefix("/releases/",
		http.FileServer(http.Dir(cfg.releasesDir))))
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintln(w, "ok")
	})

	log.Printf("warband-site on %s (base %s, releases %s)", cfg.addr, cfg.baseURL, cfg.releasesDir)
	server := &http.Server{
		Addr:              cfg.addr,
		Handler:           mux,
		ReadHeaderTimeout: 10 * time.Second,
	}
	log.Fatal(server.ListenAndServe())
}

func envOr(key, fallback string) string {
	if v := strings.TrimSpace(os.Getenv(key)); v != "" {
		return v
	}
	return fallback
}

// --- sessions: value = id|name|exp, signed with HMAC-SHA256 (Shoota's scheme) ---

func sign(payload string) string {
	mac := hmac.New(sha256.New, cfg.cookieSecret)
	mac.Write([]byte(payload))
	return hex.EncodeToString(mac.Sum(nil))
}

func setSession(w http.ResponseWriter, id, name string) {
	exp := time.Now().Add(30 * 24 * time.Hour).Unix()
	payload := fmt.Sprintf("%s|%s|%d", id, name, exp)
	http.SetCookie(w, &http.Cookie{
		Name:     sessionCookie,
		Value:    url.QueryEscape(payload + "|" + sign(payload)),
		Path:     "/",
		Expires:  time.Unix(exp, 0),
		HttpOnly: true,
		Secure:   secureCookies(),
		SameSite: http.SameSiteLaxMode,
	})
}

// Local development over plain HTTP cannot set Secure cookies, and silently failing to log in is a
// miserable way to discover that.
func secureCookies() bool { return strings.HasPrefix(cfg.baseURL, "https://") }

func getSession(r *http.Request) (id, name string, ok bool) {
	c, err := r.Cookie(sessionCookie)
	if err != nil {
		return "", "", false
	}
	raw, err := url.QueryUnescape(c.Value)
	if err != nil {
		return "", "", false
	}
	parts := strings.Split(raw, "|")
	if len(parts) != 4 {
		return "", "", false
	}
	payload := strings.Join(parts[:3], "|")
	if !hmac.Equal([]byte(sign(payload)), []byte(parts[3])) {
		return "", "", false
	}
	exp, err := strconv.ParseInt(parts[2], 10, 64)
	if err != nil || time.Now().Unix() > exp {
		return "", "", false
	}
	return parts[0], parts[1], true
}

// --- Discord OAuth ---

func handleLogin(w http.ResponseWriter, r *http.Request) {
	buf := make([]byte, 16)
	if _, err := rand.Read(buf); err != nil {
		http.Error(w, "could not start sign-in", http.StatusInternalServerError)
		return
	}
	state := hex.EncodeToString(buf)
	http.SetCookie(w, &http.Cookie{
		Name: stateCookie, Value: state, Path: "/", MaxAge: 600,
		HttpOnly: true, Secure: secureCookies(), SameSite: http.SameSiteLaxMode,
	})
	q := url.Values{
		"client_id":     {cfg.clientID},
		"redirect_uri":  {cfg.baseURL + "/auth/discord/callback"},
		"response_type": {"code"},
		"scope":         {"identify"}, // identify only: a name and an id, nothing else
		"state":         {state},
	}
	http.Redirect(w, r, "https://discord.com/oauth2/authorize?"+q.Encode(), http.StatusFound)
}

func handleCallback(w http.ResponseWriter, r *http.Request) {
	state := r.URL.Query().Get("state")
	c, err := r.Cookie(stateCookie)
	if err != nil || state == "" || state != c.Value {
		http.Error(w, "state mismatch — try signing in again", http.StatusBadRequest)
		return
	}
	http.SetCookie(w, &http.Cookie{Name: stateCookie, Value: "", Path: "/", MaxAge: -1})

	code := r.URL.Query().Get("code")
	if code == "" {
		http.Error(w, "missing code", http.StatusBadRequest)
		return
	}
	id, name, err := exchangeDiscordProfile(r.Context(), code)
	if err != nil {
		log.Printf("discord callback: %v", err)
		http.Error(w, "discord authentication failed", http.StatusBadGateway)
		return
	}
	log.Printf("sign-in: %s (%s)", name, id)
	setSession(w, id, name)
	http.Redirect(w, r, "/", http.StatusFound)
}

func handleLogout(w http.ResponseWriter, r *http.Request) {
	http.SetCookie(w, &http.Cookie{Name: sessionCookie, Value: "", Path: "/", MaxAge: -1})
	http.Redirect(w, r, "/", http.StatusFound)
}

func exchangeDiscordProfile(ctx context.Context, code string) (id, name string, err error) {
	form := url.Values{
		"client_id":     {cfg.clientID},
		"client_secret": {cfg.clientSecret},
		"grant_type":    {"authorization_code"},
		"code":          {code},
		"redirect_uri":  {cfg.baseURL + "/auth/discord/callback"},
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, discordTokenURL,
		strings.NewReader(form.Encode()))
	if err != nil {
		return "", "", err
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	resp, err := httpClient.Do(req)
	if err != nil {
		return "", "", fmt.Errorf("token exchange: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return "", "", fmt.Errorf("token exchange status %d", resp.StatusCode)
	}
	var token struct {
		AccessToken string `json:"access_token"`
	}
	if err := json.NewDecoder(io.LimitReader(resp.Body, maxBody)).Decode(&token); err != nil ||
		token.AccessToken == "" {
		return "", "", errors.New("invalid token response")
	}

	userReq, err := http.NewRequestWithContext(ctx, http.MethodGet, discordUserURL, nil)
	if err != nil {
		return "", "", err
	}
	userReq.Header.Set("Authorization", "Bearer "+token.AccessToken)
	userResp, err := httpClient.Do(userReq)
	if err != nil {
		return "", "", fmt.Errorf("user lookup: %w", err)
	}
	defer userResp.Body.Close()
	if userResp.StatusCode != http.StatusOK {
		return "", "", fmt.Errorf("user lookup status %d", userResp.StatusCode)
	}
	var profile struct {
		ID       string `json:"id"`
		Username string `json:"username"`
	}
	if err := json.NewDecoder(io.LimitReader(userResp.Body, maxBody)).Decode(&profile); err != nil {
		return "", "", err
	}
	if profile.ID == "" {
		return "", "", errors.New("discord returned no user id")
	}
	return profile.ID, profile.Username, nil
}

// --- the one gated thing ---

func handleLauncher(w http.ResponseWriter, r *http.Request) {
	_, name, ok := getSession(r)
	if !ok {
		http.Redirect(w, r, "/auth/discord", http.StatusFound)
		return
	}
	path := filepath.Join(cfg.releasesDir, launcherName)
	f, err := os.Open(path)
	if err != nil {
		log.Printf("launcher download for %s: %v", name, err)
		http.Error(w, "the launcher has not been published yet", http.StatusNotFound)
		return
	}
	defer f.Close()
	info, err := f.Stat()
	if err != nil {
		http.Error(w, "could not read the launcher", http.StatusInternalServerError)
		return
	}
	log.Printf("launcher download: %s", name)
	w.Header().Set("Content-Type", "application/octet-stream")
	w.Header().Set("Content-Disposition", `attachment; filename="`+launcherName+`"`)
	http.ServeContent(w, r, launcherName, info.ModTime(), f)
}

// --- landing page ---

type releaseInfo struct {
	Version     string
	ContentVer  string
	SizeMB      string
	PublishedAt string
}

func publishedRelease() (releaseInfo, bool) {
	data, err := os.ReadFile(filepath.Join(cfg.releasesDir, manifestName))
	if err != nil {
		return releaseInfo{}, false
	}
	var m struct {
		Version     string `json:"version"`
		ContentVer  string `json:"contentVersion"`
		Size        int64  `json:"size"`
		PublishedAt string `json:"publishedAt"`
	}
	if err := json.Unmarshal(data, &m); err != nil || m.Version == "" {
		return releaseInfo{}, false
	}
	return releaseInfo{
		Version:     m.Version,
		ContentVer:  m.ContentVer,
		SizeMB:      fmt.Sprintf("%.0f MB", float64(m.Size)/(1024*1024)),
		PublishedAt: m.PublishedAt,
	}, true
}

func handleHome(w http.ResponseWriter, r *http.Request) {
	_, name, signedIn := getSession(r)
	release, hasRelease := publishedRelease()
	launcherReady := false
	if _, err := os.Stat(filepath.Join(cfg.releasesDir, launcherName)); err == nil {
		launcherReady = true
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := homeTemplate.Execute(w, map[string]any{
		"SignedIn":      signedIn,
		"Username":      name,
		"HasRelease":    hasRelease,
		"Release":       release,
		"LauncherReady": launcherReady,
	}); err != nil {
		log.Printf("render home: %v", err)
	}
}

var homeTemplate = template.Must(template.New("home").Parse(`<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>warband — friend build</title>
<style>
  :root { color-scheme: dark; }
  body { margin:0; background:#12100e; color:#e8e2d6;
         font:16px/1.55 ui-sans-serif,system-ui,-apple-system,Segoe UI,sans-serif;
         display:flex; justify-content:center; padding:clamp(1.5rem,5vw,4rem); }
  main { width:100%; max-width:34rem; }
  h1 { font-size:clamp(1.9rem,6vw,2.6rem); margin:0 0 .35rem; letter-spacing:.02em; }
  .sub { color:#a89e8c; margin:0 0 2rem; }
  .card { background:#1b1815; border:1px solid #2e2823; border-radius:10px;
          padding:1.25rem 1.35rem; margin-bottom:1rem; }
  .card h2 { font-size:.78rem; letter-spacing:.14em; text-transform:uppercase;
             color:#a89e8c; margin:0 0 .7rem; font-weight:600; }
  a.btn { display:block; text-align:center; text-decoration:none; padding:.85rem 1rem;
          border-radius:8px; font-weight:600; background:#c8a04a; color:#1a1510; }
  a.btn.discord { background:#5865f2; color:#fff; }
  a.btn:hover { filter:brightness(1.08); }
  dl { display:grid; grid-template-columns:auto 1fr; gap:.3rem .9rem; margin:0; }
  dt { color:#a89e8c; } dd { margin:0; font-variant-numeric:tabular-nums; }
  .muted { color:#8d8375; font-size:.85rem; }
  ol { margin:.4rem 0 0; padding-left:1.2rem; } li { margin:.3rem 0; }
  footer { color:#6f675c; font-size:.78rem; margin-top:2rem; }
</style></head><body><main>
<h1>warband</h1>
<p class="sub">Hex autobattler — friend playtest build. Windows only.</p>

{{if .SignedIn}}
  <div class="card">
    <h2>Signed in as {{.Username}}</h2>
    {{if .LauncherReady}}
      <a class="btn" href="/launcher">Download the launcher</a>
      <p class="muted" style="margin:.8rem 0 0">Run it once. It installs the game, keeps it
      updated, and starts it. No installer, no admin rights.</p>
    {{else}}
      <p class="muted" style="margin:0">The launcher has not been published yet. Check back.</p>
    {{end}}
  </div>
{{else}}
  <div class="card">
    <h2>Sign in to download</h2>
    <a class="btn discord" href="/auth/discord">Sign in with Discord</a>
    <p class="muted" style="margin:.8rem 0 0">One click, no password. Only your Discord name and id
    are read, and nothing is stored server-side.</p>
  </div>
{{end}}

{{if .HasRelease}}
<div class="card">
  <h2>Current build</h2>
  <dl>
    <dt>Version</dt><dd>{{.Release.Version}}</dd>
    <dt>Download</dt><dd>{{.Release.SizeMB}}</dd>
    <dt>Content</dt><dd>{{.Release.ContentVer}}</dd>
    <dt>Published</dt><dd>{{.Release.PublishedAt}}</dd>
  </dl>
</div>
{{end}}

<div class="card">
  <h2>What to expect</h2>
  <ol>
    <li>One run is three acts. <strong>Losing any fight ends the run.</strong></li>
    <li>You can quit mid-run and continue later — progress is saved between beats.</li>
    <li>Placement is the only order you give. Everything else is decided before the fight.</li>
  </ol>
</div>

{{if .SignedIn}}<p class="muted"><a href="/logout" style="color:#8d8375">Sign out</a></p>{{end}}
<footer>Discord sign-in only gates the launcher download. No tracking, no accounts.</footer>
</main></body></html>`))
