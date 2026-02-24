---
name: release-version
description: Create a SemVer git tag (vX.Y.Z) and push it to origin, triggering release workflow. Use when the user asks to create a version tag, tag a release, push a tag, or release a new version.
---

# Release version (tag and push)

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

2. **Release only from `main` (mandatory)**
   - Versions are **always** created from the `main` branch. If the current branch is not `main`:
     - Switch to `main`: `git checkout main` (or `git switch main`)
     - Pull latest: `git pull origin main`
     - Do not create the release tag from another branch
   - If there are uncommitted changes on `main`, commit them first (see clean working tree below) before creating the tag

3. **Require clean working tree (mandatory)**
   - Run `git status`. If there are uncommitted changes (modified, untracked, or staged but not committed):
     - **Do not create the tag** until the working tree is clean
     - Commit all changes first: use the **git-commit** skill to create a proper commit message, or guide the user to stage and commit
     - After committing, proceed with the release workflow
   - Rationale: the tag must point to a committed state so the release is reproducible and matches what was tested

4. **Documentation and translations check (mandatory)**
   - **Changelog for the release version (mandatory):**
     - Open [docs/docs/changelog.rst](docs/docs/changelog.rst) and check that there is an entry for the version being released (e.g. section for `v1.2.3` or the corresponding date/version block)
     - If there is **no** entry for the release version — add it (release date, list of changes). Then update Russian translations in [docs/locale/ru/LC_MESSAGES/docs/changelog.po](docs/locale/ru/LC_MESSAGES/docs/changelog.po) for the new changelog strings
     - Do not create the tag until the changelog has an entry for this version and translations for it (if applicable)
   - Before releasing, also verify that new or changed functionality has:
     - **Documentation:** corresponding updates in `docs/docs/` (`.rst` files); new features, config options, and protocol changes must be documented
     - **Translations:** Russian translations in `docs/locale/ru/LC_MESSAGES/docs/` (`.po` files) updated for any new or modified doc strings (see project rule [documentation.mdc](.cursor/rules/documentation.mdc))
   - If documentation or translations are missing for recent changes, add or update them and include in the commit(s) before creating the tag
   - Do not create a release tag if the release would ship without changelog entry for the version or without docs/translations for new functionality

5. **Clean up unused translations (recommended before release)**
   - Before creating the tag, it is **recommended** to remove obsolete/unused entries from `.po` files in `docs/locale/ru/LC_MESSAGES/docs/`
   - After running `make gettext` and `make update-po` in `docs/`, some entries in `.po` may become obsolete (strings removed from docs). Remove them so the translation files stay clean
   - Example with gettext (in WSL or if gettext is available): `msgattrib --no-obsolete -o file.po file.po` for each `.po` file, or use your IDE/editor to delete obsolete blocks (entries starting with `#~`)
   - After any `make gettext` / `make update-po` / cleanup: run `make clean` in `docs/`, and do not commit `docs/_build/` or any `*.mo` files (see [documentation.mdc](.cursor/rules/documentation.mdc))

6. **Check existing tags**
   - Verify tag doesn't already exist:
     ```powershell
     git tag -l 'vX.Y.Z'
     ```
   - If tag exists, ask user to choose another version or delete existing tag

7. **Commit → tag locally → push together (mandatory order)**
   - **Order matters** so that documentation and packages build correctly from the same commit the tag points to.
   - **Step A — Commit:** Create the release commit with all changes (changelog, docs, code, translations). Do not push yet.
     ```powershell
     git commit -m "feat(release): prepare vX.Y.Z" -m "..."
     ```
   - **Step B — Tag locally:** Create the annotated tag on the commit you just made (tag points to that commit).
     ```powershell
     git tag -a vX.Y.Z -m "Release vX.Y.Z"
     ```
   - **Step C — Push together:** Push the branch and the tag in one go so the remote has both the commit and the tag. This ensures the release workflow sees the correct tree for building docs and NuGet packages.
     ```powershell
     git push origin main
     git push origin vX.Y.Z
     ```
     Or push branch and all tags: `git push origin main --follow-tags`
   - **Rationale:** If you push the tag before the commit, or push the tag from another machine, the tag might point to an older commit and the release build would use outdated docs/code. Always: commit first, then tag that commit locally, then push both.

8. **Create the tag (local only, see step 7 for order)**
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
   - Create the tag **only after** the release commit exists locally; do not push until step 9.

9. **Push branch and tag** (after commit and tag are created locally)
   - Push branch first, then tag:
     ```powershell
     git push origin main
     git push origin vX.Y.Z
     ```
   - Or: `git push origin main --follow-tags` (pushes `main` and any tags that point to commits on it)
   - Pushing the tag triggers the [Release workflow](.github/workflows/release.yml):
     - Builds and tests the project
     - Creates NuGet packages
     - Publishes to NuGet.org
     - Creates GitHub Release with artifacts

## Commands summary (PowerShell)

**Release sequence (commit → tag locally → push together):**

```powershell
# 1. Commit (all release changes already staged)
git commit -m "feat(release): prepare v1.5.0" -m "Queue declarations, compression, docs, changelog."

# 2. Tag locally (points to the commit above)
git tag -a v1.5.0 -m "Release v1.5.0"

# 3. Push branch and tag together
git push origin main
git push origin v1.5.0
# Or: git push origin main --follow-tags
```

**Other commands:**

```powershell
# List version tags (newest first, show top 10)
git tag -l 'v*' --sort=-v:refname | Select-Object -First 10

# Check if specific tag exists
git tag -l 'v1.0.0'

# Create annotated tag (after commit, before push)
git tag -a v1.0.0 -m "Release v1.0.0"

# Push tag to origin (after pushing main)
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
  
- **User on a branch other than main:** Do not create a release tag. Switch to `main` first (`git checkout main`), then run the release workflow.

- **Windows:** Prefer PowerShell commands. If user prefers WSL, use `bash` with the same git commands.

- **Invalid version format:** Release workflow validates format `^v[0-9]+\.[0-9]+\.[0-9]+`. Pre-release suffixes are allowed but may need special handling.

- **Uncommitted changes:** Do not create a release tag with uncommitted changes. Commit everything first (use git-commit skill if needed), then create the tag.

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

- **Changelog:** [docs/docs/changelog.rst](docs/docs/changelog.rst) — must be updated before release
- **Documentation:** [docs/docs/](docs/docs/) — new functionality must be documented in `.rst` files
- **Translations:** [docs/locale/ru/LC_MESSAGES/docs/](docs/locale/ru/LC_MESSAGES/docs/) — `.po` files must reflect new/changed doc strings
- **Release workflow:** [.github/workflows/release.yml](.github/workflows/release.yml) — automatically triggered on tag push
- **Version policy:** See changelog section "Versioning Policy" for release schedule and support policy

## Checklist before creating a release tag

- [ ] Current branch is `main` (switch with `git checkout main` if not)
- [ ] Working tree is clean (all changes committed)
- [ ] **Changelog:** [docs/docs/changelog.rst](docs/docs/changelog.rst) has an entry for the release version; if not — add it, then add/update translations in [docs/locale/ru/LC_MESSAGES/docs/changelog.po](docs/locale/ru/LC_MESSAGES/docs/changelog.po)
- [ ] New functionality has documentation in `docs/docs/`
- [ ] Russian translations in `docs/locale/ru/LC_MESSAGES/docs/` are updated for new/changed strings (including changelog)
- [ ] (Recommended) Unused/obsolete entries removed from `.po` files in `docs/locale/ru/LC_MESSAGES/docs/`
- [ ] Tag `vX.Y.Z` does not already exist
