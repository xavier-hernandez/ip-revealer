# Future Enhancements

Planned/proposed improvements for Ip Revealer. None of these are implemented yet
unless noted otherwise. Ordered roughly by effort.

---

## 1. Click-through mode

**What:** Make the overlay non-interactive so mouse clicks pass *through* it to the
window behind. You'd see the IP floating on screen, but clicking where it sits would
click whatever is underneath (browser, game HUD, etc.).

**Why:** Useful when the overlay sits over an area you interact with a lot and you
never need to drag/right-click it.

**How:**
- Add the extended window styles `WS_EX_LAYERED | WS_EX_TRANSPARENT` to the form.
  ```csharp
  protected override CreateParams CreateParams
  {
      get
      {
          const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20;
          var cp = base.CreateParams;
          if (_clickThrough) cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
          return cp;
      }
  }
  ```
- Because a click-through window can't be clicked, it **can't** be toggled back off
  via its own context menu. Pair it with a **global hotkey** (e.g. `Ctrl+Alt+L`)
  registered via `RegisterHotKey` to flip `_clickThrough` and call
  `RecreateHandle()` / re-apply the style.
- Persist the state in `settings.json`.

**Tradeoffs / gotchas:**
- Lose drag + right-click while active — the hotkey is mandatory, not optional.
- Document the hotkey clearly or users will think the app is frozen.

---

## 2. Adjustable transparency

**What:** Let the user change the overlay's opacity instead of the fixed `0.85`.

**Why:** Some backgrounds make the text hard to read; others make a solid box
distracting. User control covers both.

**How:**
- `Form.Opacity` already does the work (`0.0`–`1.0`).
- Expose it via:
  - a right-click submenu with presets (e.g. 50% / 70% / 85% / 100%), and/or
  - mouse-wheel over the overlay to nudge opacity up/down in 5% steps
    (`MouseWheel` → `Opacity = Math.Clamp(Opacity + e.Delta/120 * 0.05, 0.2, 1.0)`).
- Persist the chosen value in `settings.json` (`Opacity` field).

**Tradeoffs / gotchas:**
- Keep a sensible floor (~0.2) so the window can't become invisible *and*
  click-through-by-accident, leaving the user unable to find it.

---

## 3. Configurable refresh interval

**What:** Let the user pick how often the WAN address is re-fetched, instead of the
hard-coded 60s.

**Why:** Static IPs barely need polling; flaky/dynamic connections may want it more
often. Also reduces needless requests to the public IP services.

**How:**
- Add an `IntervalSeconds` field to `Settings` (default 60).
- Right-click submenu with presets: 15s / 30s / 60s / 5 min / Manual only.
- On change: `_refreshTimer.Interval = seconds * 1000;` and persist.
- "Manual only" = stop the timer; rely on the **Refresh now** menu item.

**Tradeoffs / gotchas:**
- Enforce a minimum (e.g. 10s) to stay friendly to the free IP-lookup endpoints.
- "Manual only" should make it obvious the value is stale (e.g. dim the text or
  show a small `*`).

---

## 4. Richer manual IP-service selection

> **Status:** A basic version already ships — double-click opens a picker to choose
> one of the built-in services (or Auto). These are enhancements on top of that.

**What:** Make the service list user-editable rather than hard-coded in `IpServices`.

**Why:** Power users may prefer a self-hosted endpoint, a corporate-internal
reflector, or want to remove a provider they don't trust.

**How:**
- Move the service list from the `IpServices` array into `settings.json` so it can be
  edited without recompiling.
- In the picker dialog, add **Add…**, **Edit…**, **Remove**, and reorder buttons.
- Validate each entry: must be `http(s)`, must return something `IPAddress.TryParse`
  can read. Show a quick test result ("✓ 203.0.113.5" / "✗ no valid IP") in the dialog.
- Optionally support endpoints that return JSON (e.g. `{"ip":"..."}`) by allowing a
  field selector per service.

**Tradeoffs / gotchas:**
- User-supplied URLs are an exfiltration vector in hostile environments — the app
  sends a request to whatever URL is configured. Fine for a personal tool; worth a
  note if this is ever distributed more widely.
- Keep the built-in defaults as a non-deletable fallback set so a bad edit can't
  brick WAN lookups.

---

## Cross-cutting: a real settings window

Several items above bolt new options onto the right-click menu. Once there are more
than a handful, consider a single **Settings** dialog (opacity slider, interval
dropdown, click-through + hotkey, service list editor) backed by the same
`settings.json`. Cleaner than an ever-growing context menu.
