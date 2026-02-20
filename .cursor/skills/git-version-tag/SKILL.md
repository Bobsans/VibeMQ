---
name: git-version-tag
description: Create a SemVer git tag (vX.Y.Z) and push it to origin, triggering release workflow. Use when the user asks to create a version tag, tag a release, push a tag, or release a new version.
---

# Git version tag and push

## When to use

Apply this skill when the user wants to:
- Create a version tag in git
- Tag a release (e.g. v1.0.0)
- Push a version tag to origin
- Release a new version (create tag + push)

## Version format

- **Tag format:** `vX.Y.Z` (e.g. `v1.0.0`, `v2.1.3`)
- **SemVer 2.0:** Major.Minor.Patch; prefix `v` is required (release workflow expects it)
- **Pre-release versions:** Supported (e.g. `v1.0.0-alpha.1`, `v1.0.0-beta.1`, `v1.0.0-rc.1`)
- Version is not stored in repo; it is taken from the tag in [release workflow](.github/workflows/release.yml)
- The workflow validates format: `^v[0-9]+\.[0-9]+\.[0-9]+` (pre-release suffixes are allowed)

## Workflow

1. **Determine version**
   - If the user specified a version, use it exactly (e.g. `v1.2.3`)
   - If not specified, determine the next version:
     ```powershell
     # List existing tags (newest first)
     git tag -l 'v*' --sort=-v:refname | Select-Object -First 5
     ```
     - Parse the latest tag to get current version
     - Suggest next version based on context:
       - **PATCH** (`v1.2.0` → `v1.2.1`): bug fixes
       - **MINOR** (`v1.2.0` → `v1.3.0`): new features, backward compatible
       - **MAJOR** (`v1.2.0` → `v2.0.0`): breaking changes
     - Check [changelog](docs/docs/changelog.rst) for version history context
   - Validate format: must match `vX.Y.Z` where X, Y, Z are integers (no leading zeros)

2. **Check existing tags**
   - Verify tag doesn't already exist:
     ```powershell
     git tag -l 'vX.Y.Z'
     ```
   - If tag exists, ask user to choose another version or delete existing tag

3. **Ensure clean state (recommended)**
   - Check for uncommitted changes: `git status`
   - If there are uncommitted changes:
     - Recommend committing them first
     - Or confirm with the user that tagging current HEAD is intended
   - Check current branch: releases are typically tagged from `main` branch

4. **Remind about changelog**
   - Before creating tag, remind user to update [changelog](docs/docs/changelog.rst) if needed
   - Changelog should reflect changes in the new version

5. **Create the tag**
   - Use annotated tag (preferred for releases):
     ```powershell
     git tag -a vX.Y.Z -m "Release vX.Y.Z"
     ```
   - Or in WSL/bash:
     ```bash
     git tag -a vX.Y.Z -m "Release vX.Y.Z"
     ```
   - Use the exact version string (e.g. `v1.0.0`, `v1.0.0-alpha.1`)
   - Message format: `"Release vX.Y.Z"` (or `"Release candidate vX.Y.Z-rc.1"` for pre-releases)

6. **Push the tag**
   - Push single tag:
     ```powershell
     git push origin vX.Y.Z
     ```
   - Pushing the tag triggers the [Release workflow](.github/workflows/release.yml):
     - Builds and tests the project
     - Creates NuGet packages
     - Publishes to NuGet.org
     - Creates GitHub Release with artifacts

## Commands summary (PowerShell)

```powershell
# List version tags (newest first, show top 10)
git tag -l 'v*' --sort=-v:refname | Select-Object -First 10

# Check if specific tag exists
git tag -l 'v1.0.0'

# Create annotated tag
git tag -a v1.0.0 -m "Release v1.0.0"

# Push tag to origin
git push origin v1.0.0

# Verify tag was created
git show v1.0.0
```

## Pre-release versions

Pre-release tags follow SemVer 2.0 format:
- `v1.0.0-alpha.1` — alpha version
- `v1.0.0-beta.1` — beta version  
- `v1.0.0-rc.1` — release candidate

**Note:** Pre-release tags trigger the release workflow but may need manual handling in GitHub Release (set `prerelease: true`).

## Edge cases

- **Tag already exists:** `git tag -a` will fail with error. Options:
  - Use another version number
  - Delete local tag: `git tag -d vX.Y.Z`
  - Delete remote tag (if pushed by mistake): `git push origin --delete vX.Y.Z`
  
- **User on a branch other than main:** Tagging works (tag points to current HEAD), but releases are typically tagged from `main` after merge. Mention this if relevant.

- **Windows:** Prefer PowerShell commands. If user prefers WSL, use `bash` with the same git commands.

- **Invalid version format:** Release workflow validates format `^v[0-9]+\.[0-9]+\.[0-9]+`. Pre-release suffixes are allowed but may need special handling.

- **Uncommitted changes:** Tagging with uncommitted changes is allowed but not recommended. Always confirm with user.

## Delete a mistaken tag

**Before push (local tag only):**
```powershell
git tag -d vX.Y.Z
```

**After push (local + remote):**
```powershell
# Delete local tag
git tag -d vX.Y.Z

# Delete remote tag (use with care - this cannot be undone easily)
git push origin --delete vX.Y.Z
```

**Warning:** Deleting a pushed tag that triggered a release may cause issues. Only delete if the tag was created by mistake and no release was published.

## Integration with project

- **Changelog:** Located at [docs/docs/changelog.rst](docs/docs/changelog.rst) - should be updated before release
- **Release workflow:** [.github/workflows/release.yml](.github/workflows/release.yml) - automatically triggered on tag push
- **Version policy:** See changelog section "Versioning Policy" for release schedule and support policy
