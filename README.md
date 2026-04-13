# Flowy for Flow Launcher ⚡

A lightning-fast, custom directory and file indexer for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher), built for power users and heavily inspired by the classic Launchy. Flowy gives you absolute control over what gets indexed, how deep the search goes, and what file types are included.

## 💡 The Perfect Multi-Device Setup (The Syncthing Workflow)
Tired of setting up your shortcuts and environment variables on every new PC? Flowy shines when combined with sync tools like **Syncthing**, Dropbox, or Google Drive. 

Just sync a folder full of your portable apps, scripts, or `.lnk` shortcuts (e.g., `D:\Sync\MyApps`), point Flowy to it, and instantly have your perfectly customized launch environment mirrored across all your machines. Since Flowy supports environment variables, a path like `%UserProfile%\Desktop\Work` will dynamically work on any PC.

## ✨ Features

* ⚡ **Blazing Fast Search:** Fully RAM-based catalog for instant, millisecond-fast fuzzy search results.
* 🧠 **Instant On & Smart Scan:** Uses a persistent local cache (`cache.json`) to be ready the second you boot Flow Launcher, while silently updating your catalog in the background on a customizable timer without hogging CPU.
* 🎯 **Deep Path & Filter Control:** * Precise search depth (e.g., `Depth: 0` to scan only the root folder, no subfolders).
  * Exact file type targeting (e.g., `*.lnk; *.exe`).
* 📁 **Folder-Only Mode:** Leave the file extension field blank to exclusively index directories – incredibly fast and clean.
* 🎛️ **Clean UI:** A built-in, native settings panel with helpful tooltips, rule indexing (`#1 | ` prefixes in results), and live statistics for tracked files and folders.

## ⌨️ Usage & Shortcuts

Once you have added a directory in the Flowy settings, simply open Flow Launcher and start typing. 

* **`Enter`**: Open the file or directory using the default Windows handler.
* **`Ctrl + Enter`**: Open Windows Explorer, locate the file, and highlight it directly.

## 🚀 Installation

### The Easy Way (via GUI)
1. Download the latest `Flow.Launcher.Plugin.Flowy-vX.X.X.zip` file from the [Releases](../../releases) page.
2. Open Flow Launcher.
3. Type `pm install ` (make sure there is a space after "install").
4. Drag & drop the downloaded `.zip` file directly into the Flow Launcher search bar and hit Enter.

### The Manual Way
1. Download the `.zip` from the [Releases](../../releases) page.
2. Extract the folder into your Flow Launcher user plugins directory:
   * Standard: `%AppData%\FlowLauncher\Plugins\`
   * Portable Mode: `<FlowLauncherFolder>\UserData\Plugins\`
3. Restart Flow Launcher.

## ⚙️ Configuration
Type `fs` (Flow Settings) in Flow Launcher, go to the **Plugins** tab, select **Flowy**, and start adding your custom directories. 

* **Tip:** Hover over the input fields in the settings panel to see helpful tooltips on how to format paths and file extensions.
