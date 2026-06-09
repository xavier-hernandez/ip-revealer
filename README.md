# Ip Revealer

> [!CAUTION]
> **Heads up:** This is a vibe coding project — built quickly for fun, not
> hardened or thoroughly tested. **Use at your own risk.** No warranty, no
> guarantees; review the code before running it on anything you care about.


A tiny always-on-top Windows overlay that shows your **public (WAN)** and
**local (LAN)** IP addresses in a corner of the screen.

![overlay](https://placehold.co/220x52/141414/00dc78?text=WAN:+203.0.113.5%0ALAN:+192.168.1.42)

## Features
- Borderless, always-on-top window — stays visible over other apps
- Shows both your **WAN** (public) and **LAN** (preferred outbound) addresses
- Auto-refreshes every 60s, with multiple IP providers tried as fallbacks
- **Drag** it anywhere with the left mouse button
- **Double-click** to choose which IP service supplies the WAN address
- **Right-click** menu: Refresh now, Copy IP, Start with Windows, Exit
- Remembers its position and chosen service; clamps back on-screen if a monitor is removed
- "Start with Windows" toggle (per-user, no admin required)

## Usage
| Action | What it does |
| --- | --- |
| Left-drag | Move the overlay anywhere on screen |
| Double-click | Open the IP-service picker |
| Right-click | Context menu (refresh / copy / startup / exit) |

The chosen service is tried **first**; the others remain as fallbacks, so a single
provider being down never leaves you stuck on `WAN: offline`. Pick **Auto** to just
try them all in order.

Settings (window position + chosen service) are stored at
`%AppData%\IpRevealer\settings.json`.

## Build & run
```powershell
dotnet build -c Release
dotnet run -c Release
```

## Publish a standalone single .exe
```powershell
dotnet publish -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
The exe lands in `bin\Release\net10.0-windows\win-x64\publish\IpRevealer.exe`.
(Self-contained means it runs on machines without the .NET runtime installed.)

## Notes
- WAN is fetched from public services (ipreveal.cc, ifconfig.io, ipify, ifconfig.me,
  icanhazip, ipinfo). Behind a restrictive corporate proxy it may show `WAN: offline`.
- LAN is the address of whichever interface would actually route to the internet
  (resolved via a connected UDP socket — no packets are sent). Shows `n/a` when offline.
- A regular window cannot draw over a full-screen exclusive game or the secure
  desktop (UAC / lock screen) — that's a Windows restriction, not a bug.
- To change the refresh interval, edit `Interval = 60_000` (milliseconds) in `Program.cs`.

## Roadmap
See [FUTURE_ENHANCEMENTS.md](FUTURE_ENHANCEMENTS.md) for planned improvements.
