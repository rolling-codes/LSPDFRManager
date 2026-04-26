# Manual Test Scenarios for Install Pipeline

## Scenario 1: Path Traversal Attack (Malicious ZIP)

**Goal:** Verify traversal attempts are blocked and rolled back cleanly.

**Steps:**
1. Create a ZIP with mixed files:
   - `safe/plugin.dll` (legitimate)
   - `../../escape.exe` (traversal attack)
2. Point LSPDFRManager at the ZIP
3. Attempt install

**Expected Result:**
- Install fails with error message
- No `escape.exe` created outside target directory
- `safe/plugin.dll` rolled back (not present in GTA directory)
- Error logged to app.log mentioning "Path traversal detected"

**Test File Created:**
```powershell
# Run from repo root
$zipPath = "C:\temp\malicious.zip"
$tempDir = "C:\temp\malicious_extract"
mkdir -Force $tempDir > $null

# Create ZIP with traversal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
$zip.CreateEntry("safe/plugin.dll").Open().Close()
$zip.CreateEntry("../../escape.exe").Open().Close()
$zip.Dispose()

Write-Host "Created malicious.zip at $zipPath"
```

---

## Scenario 2: Locked File During Install

**Goal:** Verify install fails gracefully when GTA V holds file handles.

**Steps:**
1. Create a source directory with a few mod files
2. Pre-create one file in GTA directory and hold it open (via PowerShell `[System.IO.File]::Open()`)
3. Attempt install with mod containing a file with same name
4. Check that partial install rolled back

**Expected Result:**
- Install fails (cannot overwrite locked file)
- Newly written files removed
- App logs error about file lock
- GTA directory left in clean state

---

## Scenario 3: Corrupt ZIP Archive

**Goal:** Verify corrupt archives are handled without orphaned files.

**Steps:**
1. Create a valid ZIP
2. Truncate it (remove last 100 bytes)
3. Attempt install

**Expected Result:**
- Install fails with archive exception
- No partial files left in GTA directory
- Error logged to app.log

---

## Scenario 4: Successful Deep Nested Install

**Goal:** Verify legitimate deep paths are created correctly.

**Steps:**
1. Create ZIP with: `mods/a/b/c/d/e/f/plugin.dll`
2. Install to clean GTA directory

**Expected Result:**
- All directories created: `mods\a\b\c\d\e\f\`
- File installed at correct location
- No errors

---

## Scenario 5: Mid-Install Failure (Simulated)

**Goal:** Verify rollback removes all written files when failure occurs mid-stream.

**Steps:**
1. Create ZIP with 10 files
2. Modify FileInstaller to throw exception after 5 files (for testing only)
3. Attempt install

**Expected Result:**
- All 5 files removed from GTA directory
- Clean state (no orphans)

**Note:** This requires temporary code change; skip in production testing.

---

## Scenario 6: Large File Install

**Goal:** Verify large mods extract without memory/streaming issues.

**Steps:**
1. Create ZIP with 50+ MB of content
2. Install to GTA directory

**Expected Result:**
- Install succeeds
- Files present and correct size
- No hangs or memory spikes

---

## Quick Test Checklist

- [ ] Scenario 1: Traversal blocked, rollback verified
- [ ] Scenario 2: Locked files handled gracefully  
- [ ] Scenario 3: Corrupt archives fail cleanly
- [ ] Scenario 4: Deep paths created correctly
- [ ] Scenario 6: Large files handled
- [ ] app.log contains all error messages
- [ ] GTA directory clean after failures
