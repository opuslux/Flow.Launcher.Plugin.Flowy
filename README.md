# Flowy for Flow Launcher

A lightning-fast, custom directory and file indexer for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher). 

Flowy is designed for power users who want absolute control over what gets indexed, without sacrificing search speed or system performance.

## 💖 Tribute to Launchy
Flowy was built out of deep nostalgia and respect for **[Launchy](https://www.launchy.net/)**, the legendary open-source keystroke launcher. Launchy pioneered the concept of a completely customizable, RAM-based user catalog where you could specify exact folders, scan depths, and file extensions. Flowy brings this exact "power user" cataloging philosophy into the modern era of Flow Launcher.

## ✨ Features

* **Blazing Fast Search:** Keeps your entire catalog in RAM (memory) for instant, millisecond-fast fuzzy search results.
* **Instant On:** Uses a persistent local cache (`cache.json`). Your search results are ready the millisecond you boot up Flow Launcher—no waiting for initial scans.
* **Background Auto-Scan:** Silently updates your catalog on a customizable timer without hogging your CPU.
* **Smart Paths:** Fully supports Windows environment variables (e.g., `%UserProfile%\Desktop` or `%AppData%`).
* **Deep Control:** Configure the exact scan depth, target specific file extensions (e.g., `*.lnk; *.exe; *.pdf`), and toggle subfolder inclusion per directory.
* **Clean UI:** A built-in settings panel with live statistics for tracked files and folders.

## 🚀 Installation

1. Download the latest release `.zip`.
2. Extract the folder into your Flow Launcher user plugins directory:
   `%AppData%\FlowLauncher\app-<version>\UserData\Plugins` (or open Flow Launcher settings -> Plugins -> Plugin Directory).
3. Restart Flow Launcher.

## ⚙️ Configuration

Open the Flow Launcher settings and navigate to the **Flowy** plugin.

* **Folder Path:** Click to add a directory, or double-click an existing entry to edit it manually (supports variables).
* **Depth:** `0` scans only the root folder, `1` includes immediate subfolders, etc.
* **File Types:** Semicolon-separated list of extensions to look for (e.g., `*.lnk; *.exe`). Use `*.*` for everything.
* **Folders?:** Check this box if you also want directory names to appear in your search results.
* **Auto-scan:** Set the interval (in minutes) for background rescans. Set to `0` to disable background scanning.

## 🛠️ Built With
* C# / .NET 9.0
* WPF (Windows Presentation Foundation)