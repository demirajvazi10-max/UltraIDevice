# Ultra iDevice

> Accessible restore, update, backup and device management for iPhone and iPad on Windows — a file manager, app install/uninstall, live device log and crash-log reader, battery diagnostics, VoiceOver toggle, activation, plus a spoken, guided DFU/recovery mode and plain-language error explanations. Part of the Ultra family of apps.

**Version 2.0.0**

---

## What is this?

A WPF desktop app (.NET 8) that restores, updates and backs up Apple devices from Windows, built for the keyboard and screen readers (JAWS, NVDA, Narrator) and equally comfortable with a mouse. It is a friendlier front end over the open-source [libimobiledevice](https://libimobiledevice.org/) tools and [idevicerestore](https://github.com/libimobiledevice/idevicerestore) — the same job apps like 3uTools or Apple Devices do, but accessible.

It exists because those tools are either hard to use with a screen reader or give bare numeric error codes with no explanation.

## Features

### Core actions
- **Device information** — a full report read out clearly: model, iOS version and build, device name, **battery level and charging state**, **storage used/free**, phone number, model number, region, CPU architecture, activation state, Wi-Fi/Bluetooth addresses, serial number and UDID.
- **Diagnostics** — a detailed service report via `idevicediagnostics`: **battery health estimate, cycle count, design vs current capacity, voltage, temperature, battery serial/manufacture date**, storage breakdown, IMEI, hardware and network details, plus a raw dump for reference. (Full genuine/replaced-parts history like an Apple Store needs Apple's GSX and is not available through these open tools; the report shows whatever the device itself exposes.)
- **Update iOS** — install the latest signed iOS **without erasing** data.
- **Restore iOS** — a clean install that **erases** the device (with a clear confirmation first). During a restore the progress is reported by **named phase** (downloading iOS, verifying firmware, sending the system to the device, installing) with a percentage for the long stages, and when it finishes the app waits for the device to **reboot** and come back on its own before reporting it again.
- **Back up** the device to a folder, and **restore from a backup**.
- **Enter recovery mode** (software) and **exit recovery / DFU** back to normal boot.
- **Files on device** — a built-in, fully accessible file manager over AFC: browse folders (arrow keys, Enter to open, Backspace to go up), **download** a file to the PC, **add** a file, **delete**, and **make a new folder**. Shows what Apple exposes (DCIM/photos, Downloads, app documents); the whole system is not reachable without a jailbreak.
- **Apps on device** — list the installed apps, **install an `.ipa`**, and **uninstall** an app, all from an accessible list (over `ideviceinstaller`).
- **Trust / pair** the device (for the services that need it), **rename** the device, and **restart** or **shut it down** from the PC.
- **Device log (live)** — stream the device's system log (`idevicesyslog`) in an accessible window with Start/Stop/Clear/Save, handy for diagnosing a misbehaving device.
- **Export crash logs** — pull the device's crash reports to a folder (`idevicecrashreport`), then read them right away in a built-in **accessible crash-log reader**: pick a file from the list and its contents show below. Binary `.plist` reports are converted to readable XML automatically (`plistutil`); `.ips`/`.crash`/`.log` show as plain text.
- **Activate device** — activate a freshly restored device (`ideviceactivation`).
- **Turn VoiceOver on (or off) on the phone, from the PC** — the same thing the old iTunes "Configure Accessibility" did, over the lockdown `com.apple.Accessibility` domain. Meant for the case where you need the phone talking but have no one around to set it up. The device must be paired/trusted first; on a brand-new "Hello" setup screen with no pairing yet it may not go through.
- Firmware source is your choice: **latest signed iOS, downloaded automatically**, or an **IPSW file you already have**.

### Spoken, guided DFU / recovery mode
The standout feature. DFU mode needs button presses held for exact numbers of seconds — hard for anyone, and hard to get right without seeing the screen. The guided window:
- Picks the right button sequence for the device family (iPhone 8 and newer / iPhone 7 / iPhone 6s and older), and lets you change it.
- On **Start**, walks the steps **hands-free**: it speaks each action at the right moment and **counts down the holds out loud**, so you can keep both hands on the phone.
- **Detects** when the device has entered DFU or recovery and announces success.
- Also has **Previous / Next** for a manual pace.

### Plain-language error codes
When a restore fails, Windows and Apple's own tools show a bare number (4013, 9, 14…). Ultra iDevice turns the common ones into an explanation and a likely fix (cable, USB port, unsigned iOS, and so on). The full list is under **Help → Error code explanations**.

### Accessibility
- Every control has an accessible name; status and progress are raised as screen-reader live regions, so JAWS and NVDA read them without moving focus.
- **View menu**: Dark / Light / High-contrast theme, text and control size, and **Language** (English / Serbian). On first start with a screen reader running, the high-contrast theme is chosen automatically.
- Progress is announced on step changes and at sensible milestones, not on every percent, so it does not chatter. This can be turned off in **Tools → Speak progress out loud**.
- Unexpected errors are shown instead of the app closing silently.

## Requirements

| Component | Note |
|---|---|
| Windows | 10 / 11, 64-bit |
| .NET | 8.0 (`net8.0-windows`) — included in the installer build, no NuGet packages needed |
| Apple USB driver | **Required.** Comes with the free **Apple Devices** app from the Microsoft Store (or with iTunes). Install it and connect the device once. Use **Tools → Check Apple driver** to verify. |
| Device tools | `libimobiledevice` + `idevicerestore` (x64 Windows builds). Auto-installed via **Tools → Install device tools**, or placed manually (see below). |

### Where the device tools come from
Ultra iDevice looks for the command-line tools in this order:
1. a `tools` folder next to the app,
2. `%LOCALAPPDATA%\UltraIDevice\tools` (where auto-install puts them),
3. the system `PATH`.

The required tools are: `idevice_id`, `ideviceinfo`, `idevicerestore`, `irecovery`, `ideviceenterrecovery`, `idevicebackup2` (plus their DLLs).

**Three ways to get them in place:**

- **Automatic (Tools → Install device tools).** Downloads the x64 Windows bundle `ultraidevice-tools-win-x64.zip` from this project's `tools-latest` release and unpacks the `.exe`/`.dll` files into `%LOCALAPPDATA%\UltraIDevice\tools`. That bundle is produced automatically by the **`Build device tools bundle`** GitHub Action (`.github/workflows/build-tools.yml`): on every push to `main` it builds the whole libimobiledevice chain — `libplist`, `libimobiledevice-glue`, `libtatsu`, `libusbmuxd`, `libimobiledevice`, `libirecovery` and `idevicerestore` — from the official **git master** (not the stale MSYS2 packages, which lack `libtatsu` and fail on current iOS), then publishes it to the `tools-latest` release. Building `libtatsu` in is what lets the signing (TSS) request succeed on current iOS. So once that workflow has run once, every user's auto-install just works — nothing to host by hand.

- **Running from Visual Studio (no install).** Drop the tool `.exe`/`.dll` files into a `tools` folder in the **project root** (`…\source\repos\UltraIDevice\tools\`). The project copies that folder next to the built `.exe` on every build, so the app finds the tools automatically — no install step. The `tools` folder is git-ignored, so the binaries are not committed.

- **Change the source without rebuilding.** Create `%LOCALAPPDATA%\UltraIDevice\tools-source.txt` containing either a URL to a zip, a path to a local zip, or a path to a folder of tools. **Install device tools** will use that instead of the default. If some tools are still missing after an install, the app names exactly which ones.

## What it cannot do
- **Restore to an unsigned iOS.** Apple stops signing older versions; no tool can get past that (error 3194). Only the latest signed iOS can be installed.
- **Bypass iCloud Activation Lock.** By design — restoring a device still requires the owner's Apple ID afterwards.
- **Update (keep data) on the very newest iOS.** On recent iOS releases the update-without-erasing path can fail with *"Unable to create preboard manifest"* — the bundled `idevicerestore` can't build the preboard manifest for the newest filesystem components. This is a limitation of the underlying tool, not the app. Use **Restore iOS** (erase) instead, which skips that step; the app detects this error and tells you so.

## Important
- **Downloaded firmware is cleaned up automatically.** When you use *Latest signed iOS*, the IPSW (often 10+ GB) is downloaded to `%LOCALAPPDATA%\UltraIDevice\firmware` and, together with the extracted files, deleted after the install finishes. Your own IPSW files are never touched.
- **Restore erases the device.** Always confirm there is a backup first.
- Keep the device connected with a good, short cable straight into the computer (no hub) for the whole operation. Most restore failures are cable/port problems.

## Build from source

```bash
git clone https://github.com/demirajvazi10-max/UltraIDevice.git
cd UltraIDevice
dotnet build -c Release
```

Or open `UltraIDevice.slnx` in Visual Studio.

Self-contained, single-file publish for the installer:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Logs
All raw output from the device tools is written to `%LOCALAPPDATA%\UltraIDevice\logs`. **Tools → Open logs folder** opens it. These logs are the best way to see exactly what a tool reported on a real device — useful when tuning progress and error handling.

## Supporting the project

Ultra iDevice is free and will stay free. If you'd like to support its development:

- **By card on [Ko-fi](https://ko-fi.com/ultracreativesuite)** — no account needed: type an amount, press *Donate* and pay with a Visa or Mastercard (credit or debit) or PayPal. The easiest way from abroad.
- **[GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max)** — monthly or one-time, paid with an ordinary payment card (no PayPal needed); needs a free GitHub account.
- **Bank transfer from Serbia** — recipient Demir Ajvazi, account `265-0000006185877-28`, Raiffeisen banka, purpose *Donacija za Ultra programe*. Meant for domestic transfers; from abroad, Ko-fi costs far less in fees.

The same guide is inside the app: **Help → How to support this project**, in English and Serbian.

---

## Author

Created by **Demir Ajvazi**. Support development on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max).

## License

GPL-3.0 License — see the [LICENSE](./LICENSE) file. The bundled `libimobiledevice` and `idevicerestore` tools are LGPL/GPL open-source projects, used unmodified.
