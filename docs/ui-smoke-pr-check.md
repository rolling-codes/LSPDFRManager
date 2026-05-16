# UI Smoke PR Check

Use this checklist for control-layer, install, Browse/WebView, and event-subscription changes. It should take less than 10 minutes when a Windows desktop/WebView environment is available.

Automation-only PRs may record `not run: no WPF/WebView UI available` if automated tests pass.

```md
UI smoke run: <done by name/date OR not run: no WPF/WebView UI available>

Environment:
- Windows:
- Build: Debug/Release
- WebView2 available: yes/no
- GTA path: temp GTA-like folder

Checklist:
- [ ] Launch app; no startup crash.
- [ ] Set GTA path to a writable temp folder.
- [ ] Open Browse tab; WebView initializes or shows clear unavailable message.
- [ ] Trigger/download a tiny ZIP through Browse/WebView.
- [ ] Verify Browse status says staged/review in Install tab.
- [ ] Verify no install starts before explicit confirm.
- [ ] Open Install tab; detected mod is staged once.
- [ ] Build/review install plan from Install button.
- [ ] Confirm install; verify exactly one queued/install log sequence.
- [ ] Navigate Browse -> Install -> Dashboard -> Browse -> Install.
- [ ] Repeat one staging action; verify no duplicate stage/log/prompt behavior.
- [ ] Close app; no crash on shutdown.
```
