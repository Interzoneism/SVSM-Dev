# Simple VS Manager

Simple VS Manager is a desktop companion for Vintage Story that discovers installed mods, installs updates, and syncs curated modlists from the community. The application is built with WPF on .NET 8 and provides tooling for both offline mod organization and cloud-enabled preset sharing.

## Prerequisites

- .NET 8 SDK (the application targets `net8.0-windows`).
- A Vintage Story installation and data directory that the manager can index.

## Building and Testing

Run the standard .NET build and test commands from the repository root:

```bash
dotnet build -nologo -clp:Summary -warnaserror
dotnet test --nologo --verbosity=minimal
```

## Data and Configuration

Simple VS Manager stores lightweight settings such as the data directory, game directory, theme choice, cached version policy, and cloud uploader information in `SimpleVSManagerConfiguration.json` alongside `SimpleVSManagerModConfigPaths.json`. Both files live under the manager’s configuration directory and are maintained by `UserConfigurationService` whenever you change preferences in the UI. The service also tracks window sizing, modlist behaviour toggles, and whether internet access is suppressed.

## Application Layout and Interactions

### Navigation Tabs

The top navigation exposes three high-level workspaces:

- **Installed Mods** – the default, editable data grid with activation toggles and status information.
- **Mod DB** – an online catalogue with install buttons, download metrics, and tag badges.
- **Modlists (Beta)** – a cloud synchronised view that lists uploaded presets and batch operations.

### Search, Filters, and Metrics

- A context-aware search box switches between “Search installed mods” and “Search Mod Database” depending on the active tab, and disables itself when the Modlists view is active.
- Search options include toggles to include already installed mods in online results and to show only compatible database entries.
- Radio buttons configure the Mod DB automatic load metric (all-time, last 30 days, or “new in X months”) with a helper label explaining the current mode.
- When browsing the Mod DB design view, a load-more control appears once the scroll threshold is reached.

### Tag Filtering

Installed and Mod DB views expose tag headers that open popovers containing toggle buttons, letting you refine the result set by metadata tags. The header text indicates when a filter is active.

### Toolbar and Status

Above the mod list you can:

- Trigger **Update All**, **Open Logs Folder**, **Open Config Folder**, **Open Mods Folder**, or **Launch Vintage Story**.
- Observe the manager status message and busy indicator for long-running tasks.

### Installed Mod Grid

- Every row includes a toggle switch to activate/deactivate a mod, icon preview, version information, download statistics, authorship, and status icons with tooltips.
- The grid supports multiple selection. Holding Shift or Ctrl lets you toggle several mods in one gesture; selection drives the details panel.

### Mod Details Panel

When one or more mods are selected, the right-hand panel enables context actions:

- **Mod Page** – opens the mod’s online entry.
- **Edit Config** – launches the structured configuration editor.
- **Delete** – removes the mod (with multi-select aware tooltips).
- **Fix** – resolves dependency issues when available.
- **Install** – installs a version when viewing Mod DB results.
- **Version selector** – chooses from cached releases, highlighting compatible versions.
- **Update to latest** – schedules an upgrade according to compatibility metadata.

### Mod Database Cards

The Mod DB view renders cards with the mod icon, description, tag list, download and comment counts, and a one-click install button that is disabled when no downloadable release exists or the mod is already installed.

### Cloud Modlists Workspace

The Modlists tab offers:

- A data grid showing uploaded modlists with summary, ownership, and slot metadata.
- Buttons to save the current selection to the cloud, modify existing modlists (rename/delete), refresh from the server, and install the highlighted list.
- Authenticated username display and description preview for the selected cloud entry.

## Menu Reference

### File

- **Set Data Folder…** – select or change the VintagestoryData directory.
- **Set Game Folder…** – point to the Vintage Story installation to enable version detection and launching.
- **Set Custom Vintage Story Shortcut** – supply a custom executable or shortcut for launching the game.
- **Restore backup point** – choose from automatically created backups to revert your mod state.
- **Disable Internet Access** – place the manager into offline mode; required for working without network access.
- **Exit** – close the application.

### Mods

- **Refresh Mods** – re-query installed content using cached metadata.
- **Update All Mods** – install updates for every mod with a pending release, respecting per-mod version overrides.
- **Check mods compatibility** – download recent Vintage Story versions and report compatibility data.
- **Cache all versions locally** – toggle persistent storage of downloaded releases.
- **Delete Cached Mods** – clear the local cache to reclaim space.

### Presets and Modlists

- **Save Preset…** / **Load Preset** – snapshot or restore the active mod set.
- **Save Local Modlist…** / **Load Local Modlist…** – manage offline modlist files.
- **Always clear mods before loading Modlist** – enforce replace semantics when applying modlists.
- **Always add to current mods when loading Modlist** – switch to additive installs for modlists.

### View

- **Compact View** – shrink row height for denser mod tables.
- **ModDB Design** – toggle the card-based layout for Mod DB results.
- **Themes** – pick Vintage Story, Dark, Light, Custom, or a surprise palette. Choosing Custom opens the theme palette editor to fine-tune colours.

### Help / Advanced

- **Open manager config folder** – jump to the configuration directory on disk.
- **Open Mod DB page for the manager** – visit the Simple VS Manager project entry.
- **Documentation** – open this README from the installed binaries.
- **Enable debug logging** – persist verbose status logs for troubleshooting.
- **Experimental Comp Review** – launch the compatibility review workflow.
- **Delete all Simple VS Manager files (Uninstall)** – remove caches and configuration to factory reset the tool.

## Dialogs and Tools

- **Mod Config Editor** – a tree-based editor with Save, Cancel, and Browse actions for mod configuration files.
- **Bulk Compatibility Prompt** – assists with batch installing latest or compatible releases when incompatibilities are detected.
- **Theme Palette Editor** – surfaces when choosing the custom theme, allowing per-colour overrides.
- **Restore Backup Dialog** – confirms restoring a saved state and whether to include configuration files.
- **Cloud Modlist Management** – rename or delete cloud entries from a dedicated dialog.

## Logging and Diagnostics

Enable debug logging from Help → Advanced to persist diagnostic events through `StatusLogService`. The manager also exposes a rebuild button to clear caches and force a clean metadata refresh when issues arise. Mod actions report status updates in the lower status bar, making it easy to audit long-running operations.

## Documentation Access

The application bundles this README into the build output. Use Help → Documentation to open the file in your default Markdown viewer. If the packaged documentation is missing, the command surfaces a friendly message so you can fetch the README manually.

