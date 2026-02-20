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
- **SemVer:** Major.Minor.Patch; prefix `v` is required (release workflow expects it)
- Version is not stored in repo; it is taken from the tag in [release workflow](.github/workflows/release.yml)

## Workflow

1. **Confirm version**
   - If the user did not specify a version, suggest the next one:
     - List existing tags: `git tag -l 'v*' --sort=-v:refname`
     - Propose next version (e.g. after `v1.2.0` → `v1.2.1` or `v1.3.0` depending on context)
   - Ensure format is `vX.Y.Z` (integers only; no leading zeros).

2. **Ensure clean state (recommended)**
   - Check for uncommitted changes: `git status`
   - If there are uncommitted changes, either commit them first or confirm with the user that tagging current HEAD is intended.

3. **Create the tag**
   - Annotated tag (preferred for releases):
     ```powershell
     git tag -a vX.Y.Z -m "Release vX.Y.Z"
     ```
   - Or in WSL/bash:
     ```bash
     git tag -a vX.Y.Z -m "Release vX.Y.Z"
     ```
   - Use the exact version string the user requested (e.g. `v1.0.0`).

4. **Push the tag**
   - Push single tag:
     ```powershell
     git push origin vX.Y.Z
     ```
   - Pushing the tag triggers the [Release](.github/workflows/release.yml) workflow (build, test, pack, publish to NuGet, GitHub Release).

## Commands summary (PowerShell)

```powershell
# List version tags (newest first)
git tag -l 'v*' --sort=-v:refname

# Create annotated tag
git tag -a v1.0.0 -m "Release v1.0.0"

# Push tag to origin
git push origin v1.0.0
```

## Edge cases

- **Tag already exists:** `git tag -a` will fail. Either use another version or delete the local tag with `git tag -d vX.Y.Z` (and remove remote with `git push origin --delete vX.Y.Z` if it was pushed by mistake).
- **User on a branch other than main:** Tagging still works (tag points to current HEAD). For a release it is common to tag from `main` after merge; mention this if relevant.
- **Windows:** Prefer PowerShell; if the user prefers WSL, use `bash` and the same git commands.

## Optional: delete a mistaken tag (before push)

```powershell
git tag -d vX.Y.Z
```

To delete an already pushed tag (use with care):

```powershell
git push origin --delete vX.Y.Z
```
