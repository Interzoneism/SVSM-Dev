# Task: Implement Mod Update Functionality

## Overview
Add first-class support for updating installed mods directly from the Improved Mod Menu. The feature must ensure version compatibility with the locally installed Vintage Story client, streamline replacing outdated mod versions, and offer a bulk update option.

## Goals
- Allow users to update individual mods from the mod list.
- Safely replace outdated mod files while preserving activation state and metadata.
- Enforce Vintage Story version compatibility when downloading newer mod releases.
- Provide a bulk "Update all" workflow that respects per-session user preferences.

## User Experience
- **Update button per mod**: Surface a new action button alongside the existing Mod DB, Config, and Delete buttons. The button is available whenever a newer version exists for that mod in the database.
- **Compatibility prompt**: When a mismatch occurs between the user's Vintage Story version and the latest mod release, show a dialog asking whether to install the absolute latest release or the latest version compatible with the detected Vintage Story build. Remember the choice for the remainder of the bulk update session.
- **Progress feedback**: Display progress and status updates while the download, extraction, and replacement steps run. Clear success or error messaging is required for each mod processed.
- **Update all menu entry**: Add an "Update all" command that queues updates for every installed mod with an available newer version. The compatibility prompt appears once, then the selected option applies to all queued mods.

## Functional Requirements
1. **Version detection**
   - Determine the installed Vintage Story version via the existing configuration/service layer.
   - Compare against both the latest mod version and the latest compatible version supplied by the Mod DB metadata.
2. **Update workflow**
   - Download the selected release (latest or latest compatible) into a temporary location.
   - Validate archives and required files before replacing the installed mod.
   - Remove or archive the previous version, then install the new payload.
   - Preserve activation status and any local configuration files whenever feasible.
3. **Error handling**
   - Abort gracefully if no compatible release exists.
   - Surface actionable errors (e.g., download failure, validation failure, missing compatibility metadata).
   - Restore the previous mod version if installation fails after deletion.
4. **Bulk updates**
   - Iterate through all mods needing updates, applying the remembered compatibility choice.
   - Summarise the results at the end of the batch (successes, failures, skipped items).

## Acceptance Criteria
- Individual mods expose an Update button that triggers the described workflow.
- The Update button is disabled when no newer release is available.
- Users can choose between "Latest" and "Latest compatible" when required, and the selection persists for the current batch operation.
- Bulk updates run through all eligible mods and report outcomes without leaving partially updated states.
- Logging captures each significant step to aid troubleshooting.
- Unit or integration coverage exists for version selection logic and error paths.
