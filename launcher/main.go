package main

import (
	"archive/zip"
	"bufio"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

var (
	manifestURL   = "https://warband.inhouseboyz.com/releases/warband-latest-win64.json"
	launcherToken = ""
)

type manifest struct {
	Version     string `json:"version"`
	ContentVer  string `json:"contentVersion"`
	Channel     string `json:"channel"`
	File        string `json:"file"`
	URL         string `json:"url"`
	SHA256      string `json:"sha256"`
	Size        int64  `json:"size"`
	Exe         string `json:"exe"`
	PublishedAt string `json:"publishedAt"`
}

func main() {
	if err := run(); err != nil {
		fmt.Fprintln(os.Stderr)
		fmt.Fprintln(os.Stderr, "Warband launcher failed:")
		fmt.Fprintln(os.Stderr, "  "+err.Error())
		fmt.Fprintln(os.Stderr)
		fmt.Fprintln(os.Stderr, "Press Enter to close.")
		_, _ = bufio.NewReader(os.Stdin).ReadString('\n')
		os.Exit(1)
	}
}

func run() error {
	if v := strings.TrimSpace(os.Getenv("WARBAND_LAUNCHER_MANIFEST_URL")); v != "" {
		manifestURL = v
	}
	if v := strings.TrimSpace(os.Getenv("WARBAND_LAUNCHER_TOKEN")); v != "" {
		launcherToken = v
	}
	// The token is OPTIONAL here, unlike Shoota's launcher. Shoota's Go site checks the header, so a
	// missing token there is a hard error; warband publishes a static manifest behind Caddy, so the
	// token is sent when present and simply unused until Caddy is configured to require it. That
	// means the gate can be added later WITHOUT reissuing launchers.

	root, err := appRoot()
	if err != nil {
		return err
	}
	clientDir := filepath.Join(root, "Client")
	versionFile := filepath.Join(clientDir, ".warband-version")
	fmt.Println("Warband Launcher")
	fmt.Println("Install:", clientDir)

	m, err := fetchManifest()
	if err != nil {
		exe := filepath.Join(clientDir, "Warband.exe")
		if _, statErr := os.Stat(exe); statErr == nil {
			fmt.Println("Could not check for updates, launching installed client:", err)
			return launch(clientDir, exe)
		}
		return fmt.Errorf("could not check for updates: %w", err)
	}
	if m.Exe == "" {
		m.Exe = "Warband.exe"
	}
	if m.File == "" || m.Version == "" || m.SHA256 == "" {
		return fmt.Errorf("update manifest is incomplete")
	}
	if m.URL == "" {
		// Static publishing: the zip sits beside its manifest.
		resolved, err := resolveSibling(manifestURL, m.File)
		if err != nil {
			return fmt.Errorf("manifest has no url and one could not be derived: %w", err)
		}
		m.URL = resolved
	}

	current := readVersion(versionFile)
	exe := filepath.Join(clientDir, m.Exe)
	if current == m.Version {
		if _, err := os.Stat(exe); err == nil {
			fmt.Println("Up to date:", m.Version)
			return launch(clientDir, exe)
		}
		fmt.Println("Version marker is current, but the game executable is missing. Reinstalling.")
	}

	fmt.Printf("Installing Warband %s", m.Version)
	if current != "" {
		fmt.Printf(" (current %s)", current)
	}
	fmt.Println()

	zipPath, err := downloadUpdate(root, m)
	if err != nil {
		return err
	}
	if err := verifySHA256(zipPath, m.SHA256); err != nil {
		return err
	}
	if err := installUpdate(root, clientDir, zipPath, m); err != nil {
		return err
	}

	fmt.Println("Installed:", m.Version)
	if m.ContentVer != "" {
		// Worth printing: a save made against different content is refused by design, and this is
		// the value the refusal message compares against.
		fmt.Println("Content:", m.ContentVer)
	}
	return launch(clientDir, exe)
}

func appRoot() (string, error) {
	if runtime.GOOS == "windows" {
		if dir := os.Getenv("LOCALAPPDATA"); dir != "" {
			return filepath.Join(dir, "Warband"), nil
		}
		home, err := os.UserHomeDir()
		if err != nil {
			return "", err
		}
		return filepath.Join(home, "AppData", "Local", "Warband"), nil
	}
	dir, err := os.UserCacheDir()
	if err != nil {
		return "", err
	}
	return filepath.Join(dir, "Warband"), nil
}

func fetchManifest() (manifest, error) {
	req, err := http.NewRequest(http.MethodGet, manifestURL, nil)
	if err != nil {
		return manifest{}, err
	}
	req.Header.Set("X-Warband-Launcher-Token", launcherToken)
	req.Header.Set("User-Agent", "WarbandLauncher/1")

	client := &http.Client{Timeout: 20 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return manifest{}, err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return manifest{}, fmt.Errorf("manifest returned HTTP %d", resp.StatusCode)
	}
	var m manifest
	if err := json.NewDecoder(resp.Body).Decode(&m); err != nil {
		return manifest{}, err
	}
	return m, nil
}

func readVersion(path string) string {
	data, err := os.ReadFile(path)
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(data))
}

func downloadUpdate(root string, m manifest) (string, error) {
	downloads := filepath.Join(root, "Downloads")
	if err := os.MkdirAll(downloads, 0o755); err != nil {
		return "", err
	}
	file := filepath.Base(m.File)
	if file == "." || file == string(filepath.Separator) || !strings.HasSuffix(strings.ToLower(file), ".zip") {
		return "", fmt.Errorf("manifest file is not a zip: %q", m.File)
	}
	finalPath := filepath.Join(downloads, file)
	tmpPath := finalPath + ".tmp"

	req, err := http.NewRequest(http.MethodGet, m.URL, nil)
	if err != nil {
		return "", err
	}
	req.Header.Set("X-Warband-Launcher-Token", launcherToken)
	req.Header.Set("User-Agent", "WarbandLauncher/1")

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("download returned HTTP %d", resp.StatusCode)
	}

	out, err := os.Create(tmpPath)
	if err != nil {
		return "", err
	}
	progress := &progressWriter{total: resp.ContentLength}
	_, copyErr := io.Copy(out, io.TeeReader(resp.Body, progress))
	closeErr := out.Close()
	progress.finish()
	if copyErr != nil {
		_ = os.Remove(tmpPath)
		return "", copyErr
	}
	if closeErr != nil {
		_ = os.Remove(tmpPath)
		return "", closeErr
	}
	if err := os.Rename(tmpPath, finalPath); err != nil {
		_ = os.Remove(tmpPath)
		return "", err
	}
	return finalPath, nil
}

type progressWriter struct {
	total int64
	seen  int64
	last  time.Time
}

func (p *progressWriter) Write(b []byte) (int, error) {
	p.seen += int64(len(b))
	now := time.Now()
	if now.Sub(p.last) < 500*time.Millisecond {
		return len(b), nil
	}
	p.last = now
	if p.total > 0 {
		fmt.Printf("\rDownloading: %3d%%", int(float64(p.seen)*100/float64(p.total)))
	} else {
		fmt.Printf("\rDownloading: %.1f MB", float64(p.seen)/(1024*1024))
	}
	return len(b), nil
}

func (p *progressWriter) finish() {
	if p.total > 0 {
		fmt.Print("\rDownloading: 100%\n")
	} else {
		fmt.Printf("\rDownloading: %.1f MB\n", float64(p.seen)/(1024*1024))
	}
}

func verifySHA256(path, want string) error {
	want = strings.ToLower(strings.TrimSpace(want))
	f, err := os.Open(path)
	if err != nil {
		return err
	}
	defer f.Close()
	hash := sha256.New()
	if _, err := io.Copy(hash, f); err != nil {
		return err
	}
	got := hex.EncodeToString(hash.Sum(nil))
	if got != want {
		return fmt.Errorf("download hash mismatch: got %s, want %s", got, want)
	}
	fmt.Println("Verified download hash.")
	return nil
}

func installUpdate(root, clientDir, zipPath string, m manifest) error {
	staging := filepath.Join(root, "Staging-"+safeName(m.Version))
	backup := filepath.Join(root, "Client.old")
	if err := os.RemoveAll(staging); err != nil {
		return err
	}
	if err := os.RemoveAll(backup); err != nil {
		return err
	}
	if err := os.MkdirAll(root, 0o755); err != nil {
		return err
	}
	if err := unzip(zipPath, staging); err != nil {
		_ = os.RemoveAll(staging)
		return err
	}
	if _, err := os.Stat(filepath.Join(staging, m.Exe)); err != nil {
		_ = os.RemoveAll(staging)
		return fmt.Errorf("extracted build is missing %s", m.Exe)
	}

	if _, err := os.Stat(clientDir); err == nil {
		if err := os.Rename(clientDir, backup); err != nil {
			return fmt.Errorf("could not move the old client; close Warband.exe and run the launcher again: %w", err)
		}
	} else if !os.IsNotExist(err) {
		return err
	}
	if err := os.Rename(staging, clientDir); err != nil {
		if _, backupErr := os.Stat(backup); backupErr == nil {
			_ = os.Rename(backup, clientDir)
		}
		return err
	}
	if err := os.WriteFile(filepath.Join(clientDir, ".warband-version"), []byte(m.Version+"\n"), 0o644); err != nil {
		return err
	}
	_ = os.RemoveAll(backup)
	return nil
}

func unzip(src, dest string) error {
	r, err := zip.OpenReader(src)
	if err != nil {
		return err
	}
	defer r.Close()
	dest, err = filepath.Abs(dest)
	if err != nil {
		return err
	}
	for _, f := range r.File {
		clean := filepath.Clean(f.Name)
		if clean == "." || filepath.IsAbs(clean) || strings.HasPrefix(clean, ".."+string(filepath.Separator)) || clean == ".." {
			return fmt.Errorf("unsafe zip path: %s", f.Name)
		}
		target := filepath.Join(dest, clean)
		if !strings.HasPrefix(target, dest+string(filepath.Separator)) && target != dest {
			return fmt.Errorf("unsafe zip target: %s", f.Name)
		}
		if f.FileInfo().IsDir() {
			if err := os.MkdirAll(target, 0o755); err != nil {
				return err
			}
			continue
		}
		if err := os.MkdirAll(filepath.Dir(target), 0o755); err != nil {
			return err
		}
		in, err := f.Open()
		if err != nil {
			return err
		}
		out, err := os.OpenFile(target, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, f.Mode())
		if err != nil {
			_ = in.Close()
			return err
		}
		_, copyErr := io.Copy(out, in)
		closeInErr := in.Close()
		closeOutErr := out.Close()
		if copyErr != nil {
			return copyErr
		}
		if closeInErr != nil {
			return closeInErr
		}
		if closeOutErr != nil {
			return closeOutErr
		}
	}
	return nil
}

func launch(dir, exe string) error {
	fmt.Println("Launching Warband.")
	cmd := exec.Command(exe)
	cmd.Dir = dir
	return cmd.Start()
}

// resolveSibling rewrites the last path segment of base with file, so
// ".../releases/warband-latest-win64.json" + "warband-latest-win64.zip" resolves inside the same
// directory. Rejects anything with a path separator so a manifest cannot redirect the download
// somewhere else on the host.
func resolveSibling(base, file string) (string, error) {
	if strings.ContainsAny(file, "/\\") {
		return "", fmt.Errorf("manifest file must be a bare name, got %q", file)
	}
	u, err := url.Parse(base)
	if err != nil {
		return "", err
	}
	idx := strings.LastIndex(u.Path, "/")
	if idx < 0 {
		return "", fmt.Errorf("manifest url has no path: %q", base)
	}
	u.Path = u.Path[:idx+1] + file
	u.RawQuery = ""
	return u.String(), nil
}

func safeName(s string) string {
	var b strings.Builder
	for _, r := range s {
		if (r >= 'a' && r <= 'z') || (r >= 'A' && r <= 'Z') || (r >= '0' && r <= '9') || r == '.' || r == '-' || r == '_' {
			b.WriteRune(r)
		}
	}
	if b.Len() == 0 {
		return fmt.Sprint(time.Now().Unix())
	}
	return b.String()
}
