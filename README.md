# GreenGun 15kHz

[![Windows 10 / 11](https://img.shields.io/badge/Windows-10%20%2F%2011%20(64--bit)-0078D6?logo=windows)](https://github.com/Tupapa2712/GreenGun-15kHz)
[![Tested on GTX 1650](https://img.shields.io/badge/Tested%20on-NVIDIA%20GTX%201650%20LP-76B900?logo=nvidia)](https://github.com/Tupapa2712/GreenGun-15kHz)
[![15 kHz Modes](https://img.shields.io/badge/Modes-2560x240p%20%7C%202560x224p%20%40%2060Hz-orange)](https://github.com/Tupapa2712/GreenGun-15kHz)

Autonomous Windows installers and a lightweight resolution switcher (`Crt240pSwitch`) that enable **true 15 kHz progressive super resolutions (`2560×240 @ 60 Hz` and `2560×224 @ 60 Hz`)** on standard-definition **CRT TVs and monitors** using an **NVIDIA GPU** on **Windows 10 and Windows 11 (64-bit)**.

> **Project Scope:** This repository and its installers provide **only** the `240p`/`224p` resolution tools and display mode management (`Crt240pSwitch`).

---

## What Does This Do & Why "Super Resolutions" (`2560×240p` / `2560×224p`)?

Classic tube televisions (CRTs) expect a **15.7 kHz** analog video signal (around 240 or 224 real vertical scanlines at 60 Hz). However, modern NVIDIA graphics cards like the **GTX 1650** have a minimum speed limit on their HDMI port (known as the *minimum TMDS pixel clock*, around 25 MHz):

- **Simple analogy:** Think of a highway with a minimum speed limit of 25 mph. A classic resolution like `320×240` is too "slow" (~6 MHz), so the GPU's HDMI output rejects it.
- **The solution (Horizontal Super Resolution):** We multiply only the horizontal width by 8 (`320 × 8 = 2560`) while keeping the **240** or **224 real vertical lines** completely untouched. The signal reaches **~51 MHz** (passing smoothly through HDMI), while the CRT television still receives an authentic **15.72 kHz horizontal / 60 Hz vertical** progressive signal with razor-sharp, genuine scanlines.

### Important Limitation on Windows 10 / 11 & NVIDIA Turing
- **Works natively and stably:** Progressive **`2560×240 @ 60 Hz`** (PS1, NES, N64, Saturn, 240p video) and **`2560×224 @ 60 Hz`** (SNES, Sega Mega Drive/Genesis, Neo Geo, CPS1/2/3 Arcade).
- **Not supported over physical HDMI on Turing / WDDM:** **Interlaced `480i`** output is blocked at the driver/WDDM level by NVIDIA Turing GPUs on Windows 10 and Windows 11. This project focuses exclusively on **true progressive `240p` and `224p`**.

---

## Two Separate Installers (Windows 10 & Windows 11 — 64-bit)

Both installers are **100% guided and autonomous** (*Accept disclaimer → Next → confirm detected CRT monitor → Next → wait for progress bar → Finish*), with no manual command-line steps or manual Windows Registry editing required.

| Installer | Who Is It For? | Behavior |
| :--- | :--- | :--- |
| **`Setup_GTX1650_15kHz_240p_224p_Win10_Win11.exe`** | Users with an **NVIDIA GeForce GTX 1650** (Turing architecture) | **Tested Profile.** Checks whether your GPU is a GTX 1650 and displays a clear warning if a different GPU model is detected. |
| **`Setup_NVIDIA_Experimental_15kHz_240p_224p_Win10_Win11.exe`** | Users who want to **experiment on other NVIDIA GPUs** | **Experimental Profile.** Allows testing `240p`/`224p` super resolutions on other NVIDIA graphics cards at your own risk, with no compatibility guarantee. |

> Download the installer you need from the **[Releases](https://github.com/Tupapa2712/GreenGun-15kHz/releases)** page.

---

## Step-by-Step Installation (Beginner-Friendly)

1. Connect your CRT TV to your NVIDIA GPU's HDMI port through your HDMI-to-VGA converter and YPbPr transcoder. Set Windows Display Settings to **Extend these displays**, keeping your modern monitor as the **Main Display**.
2. Run your chosen installer. Windows will prompt for **Administrator privileges (UAC)** so it can safely back up and apply the CRT's `EDID_OVERRIDE` profile.
3. **Read and accept** the legal notice and full liability disclaimer.
4. On the **Automatic Monitor Detection** screen, the wizard automatically lists every monitor currently connected to your GPU:
   - Your main monitor is locked and labeled **`[PRINCIPAL - PROTEGIDO / HARD-LOCK]`** so it **cannot be modified**.
   - Your secondary CRT adapter is safely pre-selected by default (you can choose another secondary screen if needed).
5. Click **Next → Install** and wait for the progress bar to finish. Automatically behind the scenes, the installer:
   - Creates a full backup of your target display's original resolution, refresh rate, and EDID.
   - Installs `Crt240pSwitch` and applies **`2560×240 @ 60 Hz`** and **`2560×224 @ 60 Hz`** exclusively to your selected CRT display.
   - **Automatic Rollback on Failure:** If anything fails (registry write, PnP device restart, or mode application), the installer and `Crt240pSwitch` **automatically revert everything back to its original state** without breaking your Windows display setup, and write a clear diagnostic log to `%APPDATA%\Crt240pSwitch\installer.log`.
6. Click **Finish**! You can switch between `240p` and `224p` anytime from `Crt240pSwitch`, or use the Start Menu shortcut **"Restaurar EDID Original (1 Clic)"** to undo everything in one click.

---

## Exact Tested Hardware & Signal Chain

This setup has been tested and verified on real hardware using the following exact components:

1. **GPU:** **NVIDIA GeForce GTX 1650 Low Profile** (Turing architecture).
2. **HDMI-to-VGA Converter:** Active adapter using the **`MACROSILICON`** chip (identified in Windows as **`HJW9291` / `MACROSILICON`** — direct passthrough without internal scaling).
3. **Transcoder:** **`BitFunx VGA/SCART-to-YPbPr`** (converts RGBHV VGA signal to 15 kHz YPbPr component video).
4. **CRT Display:** **`Sony Trinitron WEGA KV-29FA540`** (29-inch, YPbPr component input at 15 kHz).

> **Note:** No personal paths or hardware IDs are hardcoded anywhere in the source code or installers — every user's monitors and adapters are detected dynamically at runtime.

---

## Safety, Windows 10/11 Compatibility & Automatic Rollback

- **Primary Monitor Hard-Lock:** Both the installer and `Crt240pSwitch` strictly refuse to alter your primary desktop monitor.
- **Transactional Backup & Automatic Rollback:** Before modifying `EDID_OVERRIDE` or switching display modes, the tool saves a pre-change snapshot (`pre_change_state.json`, `backup\edid_backup_*.bin`, and `.reg`). If any error occurs, the previous EDID and display resolution are restored automatically.
- **Windows 10 & Windows 11 (64-bit) Fallback:** Because display PnP node restarts can behave differently across Windows 10 and Windows 11 builds, `Crt240pSwitch` uses a 3-tier fallback chain (`SetupAPI` → `pnputil /restart-device` → `pnputil /disable-device` + `/enable-device`).
- **Diagnostic Logs:** Saved to `%APPDATA%\Crt240pSwitch\installer.log` and `%APPDATA%\Crt240pSwitch\crt240p.log`.

---

## Repository Structure

```text
├── src/
│   └── Crt240pSwitch/              # C# (.NET 9 WinForms) source code: NVAPI + EDID_OVERRIDE + Rollback
├── installer_scripts/
│   ├── AVISO_LEGAL_Y_CREDITOS.txt  # Installer legal notice & credits
│   ├── Common_Monitor_Logic.iss    # Shared GPU/monitor detection, Hard-Lock & automatic rollback
│   ├── Setup_GTX1650_15kHz_240p_224p.iss
│   ├── Setup_NVIDIA_Experimental_15kHz_240p_224p.iss
│   └── Build_All_Installers.bat    # One-click Inno Setup 6 build script
├── .gitignore
└── README.md
```

---

## Credits & Full Liability Disclaimer

### Credits
- **Author / Developer:** **Tupapa2712**
- **Development Assistance:** Built with the help of **Gemini**.

### Full Liability Disclaimer
> **IMPORTANT — PLEASE READ BEFORE USING:**
>
> This software is provided **"AS IS"**, without warranty of any kind, express or implied. Using custom 15 kHz horizontal timings, super resolutions (`2560×240p` / `2560×224p`), NVAPI calls, and `EDID_OVERRIDE` registry entries is done **entirely at your own risk**.
>
> The author and developer (**Tupapa2712 / DJ Kendo**) is **completely absolved and assumes zero responsibility or liability** for any direct, indirect, incidental, or consequential damage — including but not limited to sync loss, configuration loss, overheating, or hardware damage to graphics cards (GPUs), HDMI-to-VGA adapters, transcoders, LCD/OLED monitors, or CRT televisions/monitors used by the user.
