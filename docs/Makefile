# Minimal makefile for Sphinx documentation with i18n support
#

# You can set these variables from the command line, and also
# from the environment for the first two.
SPHINXOPTS    ?=
SPHINXBUILD   ?= sphinx-build
SOURCEDIR     = .
BUILDDIR      = _build

# Note: Source files are in docs/ subdirectory, but conf.py is in root
# Sphinx will automatically find files referenced in index.rst

# Languages supported
LANGUAGES     = en ru

# Put it first so that "make" without argument is like "make help".
help:
	@echo "Available targets:"
	@echo "  gettext      - Generate .pot files for translation"
	@echo "  update-po    - Update .po files from .pot files"
	@echo "  html         - Build English documentation"
	@echo "  html-ru      - Build Russian documentation"
	@echo "  html-all     - Build all language versions"
	@echo "  clean        - Clean build directory"
	@echo "  livehtml     - Build with live reload (English)"

.PHONY: help Makefile

# Generate .pot files for translation
gettext:
	@$(SPHINXBUILD) -b gettext $(SOURCEDIR) $(BUILDDIR)/gettext $(SPHINXOPTS) $(O)
	@echo ""
	@echo "Gettext files generated in $(BUILDDIR)/gettext"

# Update .po files from .pot files (requires sphinx-intl)
update-po:
	@sphinx-intl update -p $(BUILDDIR)/gettext -l ru $(SPHINXOPTS)
	@echo ""
	@echo ".po files updated in locale/"

# Build English HTML (default language)
html:
	@$(SPHINXBUILD) -b html -D language=en $(SOURCEDIR) $(BUILDDIR)/html/en $(SPHINXOPTS) $(O)
	@echo ""
	@echo "Build finished! The HTML pages are in $(BUILDDIR)/html/en."

# Build Russian HTML
html-ru:
	@$(SPHINXBUILD) -b html -D language=ru $(SOURCEDIR) $(BUILDDIR)/html/ru $(SPHINXOPTS) $(O)
	@echo ""
	@echo "Build finished! The HTML pages are in $(BUILDDIR)/html/ru."

# Build all language versions
html-all: html html-ru
	@echo ""
	@echo "All documentation built!"

# Build HTML with live reload (English)
livehtml:
	@$(SPHINXBUILD) -b html -D language=en $(SOURCEDIR) $(BUILDDIR) $(SPHINXOPTS) -W --keep-going --watch $(SOURCEDIR)

# Clean build directory
clean:
	@rm -rf $(BUILDDIR)
	@echo "Build directory cleaned."

# Catch-all target: route all unknown targets to Sphinx using the new
# "make mode" option.  $(O) is meant as a shortcut for $(SPHINXOPTS).
%: Makefile
	@$(SPHINXBUILD) -M $@ $(SOURCEDIR) $(BUILDDIR) $(SPHINXOPTS) $(O)
