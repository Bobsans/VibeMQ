---
name: update-docs
description: Update Sphinx documentation (.rst in docs/docs/) and Russian translations (.po in docs/locale/). Use when changing code that affects user docs, adding changelog entries, regenerating gettext templates, updating or translating .po files, or cleaning up docs build artifacts.
---

# Update documentation and translations

Apply this skill when you need to update user-facing documentation or Russian translations after code/config changes. Documentation uses **Read the Docs** (Sphinx); translations use gettext (`.po` files).

## Scope and format

- **Docs location:** `docs/`, format **Sphinx** (Read the Docs).
- **User-facing only:** Install/configure, API usage, typical scenarios, code examples, behavior and features.
- **Do not document:** Internal dev details, contributor setup, internal architecture.

## 1. Update source documentation

1. Edit `.rst` files in **`docs/docs/`** to reflect code or behavior changes.
2. For significant changes, add entries to **`docs/docs/changelog.rst`**.

## 2. Translation workflow

Run from project root. All commands below assume you are in the repo root; `docs/` is the Sphinx source directory.

### Step 1 — Generate translation templates

From `docs/`:

```powershell
cd docs
sphinx-build -b gettext . _build/gettext
```

This creates `.pot` files in `_build/gettext`.

### Step 2 — Update .po files

From `docs/`:

```powershell
sphinx-intl update -p _build/gettext -l ru
```

This updates `.po` files in `docs/locale/ru/LC_MESSAGES/`.

### Step 3 — Review and fix .po files (required)

After `sphinx-build -b gettext` and `sphinx-intl update`, **always** review changed `.po` files:

- Incorrect or irrelevant strings often appear in `.po`; fix or remove them before committing.
- Edit Russian translations in `.po` for any new or modified strings.

### Step 4 — Keep PO reference lines

Do **not** remove or alter `#:` reference lines (e.g. `#: ../../docs/examples.rst:3 b90cdb1602eb41d0b4027cd928290c3f`). Sphinx uses file path, line number and hash to match translations; keep these as generated.

### Step 5 — Repository rules

- **Commit only `.po` files.** Do not commit `.mo` files — Read the Docs compiles `.mo` during the documentation build (see `.readthedocs.yaml`).

## 3. Cleanup after testing

After gettext/update-po or a local docs build, remove generated artifacts so they are not committed:

**Remove build directory:**

```powershell
Remove-Item -Recurse -Force docs\_build
```

**Remove all .mo files:**

```powershell
Get-ChildItem -Path docs\locale -Filter *.mo -Recurse | Remove-Item
```

**Check git:** Ensure no `docs/_build/` or `docs/locale/**/*.mo` are staged or committed.

## Full workflow (PowerShell, from repo root)

```powershell
# 1. Make code changes (already done)
# 2. Update .rst files in docs/docs/ and changelog.rst as needed
# 3. Regenerate gettext and update .po
cd docs
sphinx-build -b gettext . _build/gettext
sphinx-intl update -p _build/gettext -l ru
# REQUIRED: Review changed .po files and fix incorrect/irrelevant translations
# 4. Edit .po files with new Russian translations (keep #: reference lines)
# 5. (Optional) Test HTML build
sphinx-build -b html -D language=en . _build/html/en
sphinx-build -b html -D language=ru . _build/html/ru
# 6. Clean up: remove _build and all .mo
Remove-Item -Recurse -Force _build
Get-ChildItem -Path locale -Filter *.mo -Recurse | Remove-Item
cd ..
```

## Important

- **Never commit** `docs/_build/` or any `docs/locale/**/*.mo`. Only `.po` files belong in the repo.
- Source of truth for this workflow: project rule [.cursor/rules/documentation.mdc](.cursor/rules/documentation.mdc).

## Checklist

- [ ] `.rst` in `docs/docs/` updated for code/behavior changes
- [ ] Changelog entry in `docs/docs/changelog.rst` for significant changes
- [ ] `sphinx-build -b gettext` and `sphinx-intl update -p _build/gettext -l ru` run from `docs/`
- [ ] Changed `.po` files reviewed; bad/irrelevant strings fixed or removed
- [ ] New/modified strings translated in `.po`; `#:` reference lines kept
- [ ] `docs/_build/` and all `docs/locale/**/*.mo` removed before commit
- [ ] Git status has no `_build/` or `.mo` staged
