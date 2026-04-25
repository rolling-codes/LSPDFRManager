# LSPDFRManager v1.2.0 Release Notes

**Release Date:** 2026-04-25  
**Version:** 3.2.0  
**Commit:** 1cc4467

---

## 🎯 Overview

This release introduces a **comprehensive UI/UX risk analysis layer** for installed mods. Users can now visualize mod safety levels, detect conflicts, and filter their library by risk tiers—all without any changes to the core installation system.

---

## ✨ New Features

### 1. Risk-Based Mod Visualization

Each installed mod now displays a visual risk tier:

- **🟢 Safe** (DetectionScore 70–100)
- **🟡 Medium** (DetectionScore 40–69)
- **🔴 High** (DetectionScore 0–39)

Risk tiers are calculated based on mod structure analysis (LSPDFR plugin paths, file types, etc.) and displayed as colored badges in the mod card.

### 2. Conflict Detection

Mods that share files with other installed mods now display a **Conflict detected** indicator, helping users understand potential installation issues before they occur.

### 3. Library Filtering

Users can now filter their mod library by risk level:

- **All** – Show all mods
- **Safe** – Show only Safe-tier mods
- **Medium** – Show only Medium-tier mods
- **High** – Show only High-tier mods

Filters are applied instantly with no performance impact.

---

## 🔧 Technical Details

### Architecture

Added a **read-only analytical layer** between the core installer and UI:

```
Installer Core (untouched)
    ↓
LspdfrValidator (stateless analysis)
    ↓
ModItemViewModel (risk interpretation)
    ↓
LibraryViewModel (filter projection)
    ↓
UI (visualization)
```

### Key Components

- **Services/LspdfrValidator.cs** – Pure, stateless domain scorer
  - `CalculateDetectionScore(files)` – Returns 0–100 confidence
  - `IsValidLspdfrStructure(files)` – Validates LSPDFR paths
  
- **ViewModels/ModItemViewModel.cs** – Risk tier computation
  - `RiskTier` – Safe/Medium/High based on score
  - `RiskBrush` – Color mapping for UI
  - `RiskSummary` – Human-readable risk summary
  
- **ViewModels/LibraryViewModel.cs** – Filter projection
  - `SetFilterCommand` – Filter by risk tier
  - `RefreshFiltered()` – Apply filters to mod list
  
- **Views/ModCard.xaml** – Visual updates
  - Risk badge (colored, primary signal)
  - Conflict indicator (red, secondary signal)
  - Risk summary line (descriptive text)
  
- **Views/LibraryView.xaml** – Filter UI
  - Risk filter button group (All/Safe/Medium/High)

### Test Coverage

- **LspdfrValidatorTests.cs** – 13 new tests
  - Detection scoring validation
  - Real archive analysis
  - Structure validation
- **All existing tests** – 98 tests remain passing
- **Total:** 111 tests passing, zero regressions

---

## 🛡️ Safety & Guarantees

✅ **Installer untouched** – No changes to FileInstaller, PathSafety, or rollback logic  
✅ **No enforcement** – System remains in analytical mode (evaluates, does not block)  
✅ **Read-only analysis** – Conflict detection uses existing ModLibraryService (no mutations)  
✅ **Backward compatible** – No breaking changes to API or persistence layer  
✅ **Fully tested** – 111 tests passing, including real archive scenarios  

---

## 🚀 Installation & Usage

1. **Update** to v1.2.0
2. **Open Library tab** – Installed mods now show risk tiers
3. **View risk badges** – Green/Amber/Red badges indicate safety levels
4. **Check conflicts** – Red "Conflict detected" badge on overlapping mods
5. **Filter by risk** – Use the new "Risk:" filter bar to browse by tier

No configuration required. Risk analysis runs automatically on startup.

---

## 📊 Performance

- Risk calculation: < 1ms per mod
- Filter updates: instant (no database queries)
- Memory impact: negligible (read-only analysis)
- UI responsiveness: unchanged

---

## 🐛 Known Limitations

- Risk tiers are **advisory only** (no install blocking yet)
- Conflict detection shows **overlapping files** but no resolution suggestions
- Risk scoring is based on **LSPDFR structure patterns** (not all mod types)

These are planned for future releases.

---

## 📝 Upgrade Notes

### For Users

- Risk tiers are newly computed for all installed mods
- No mods are removed or modified during upgrade
- Existing enable/disable states are preserved
- Install/uninstall behavior is unchanged

### For Developers

See `CLAUDE.md` section "Installer Safety & Testing (Phase A/B Hardening)" for:
- Core invariant guarantees
- Extraction safety contracts
- Rollback requirements
- Testing patterns

---

## 🎓 What's Next

Future releases may include:

- **Enforcement layer** (optional install blocking based on risk)
- **Conflict resolution** (suggested disable/replace actions)
- **Mod manifests** (dependency tracking)
- **Risk distribution** (dashboard summary)

---

## 📞 Support

For issues or feedback:
- Check `app.log` in `%APPDATA%\LSPDFRManager\`
- Review mod detection scoring in Library tab
- Verify LSPDFR paths match expected structure

---

**Built with:** .NET 8, C#, WPF  
**Tested on:** Windows 11 Pro x64  
**Tested with:** 111 unit + integration tests
