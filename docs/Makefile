# Minimal makefile for Sphinx documentation
#

# You can set these variables from the command line, and also
# from the environment for the first two.
SPHINXOPTS    ?=
SPHINXBUILD   ?= sphinx-build
SOURCEDIR_RU  = ru
SOURCEDIR_EN  = en
BUILDDIR      = _build

# Put it first so that "make" without argument is like "make help".
help:
	@echo "Available targets:"
	@echo "  html       - Build Russian documentation"
	@echo "  html-en    - Build English documentation"
	@echo "  html-all   - Build both Russian and English documentation"
	@echo "  clean      - Clean build directory"
	@echo "  livehtml   - Build with live reload (Russian)"

.PHONY: help Makefile

# Build Russian HTML
html:
	@$(SPHINXBUILD) -M html "$(SOURCEDIR_RU)" "$(BUILDDIR)/html" -c "$(SOURCEDIR_RU)" $(SPHINXOPTS) $(O)
	@echo ""
	@echo "Build finished! The HTML pages are in $(BUILDDIR)/html."

# Build English HTML
html-en:
	@$(SPHINXBUILD) -M html "$(SOURCEDIR_EN)" "$(BUILDDIR)/en/html" -c "$(SOURCEDIR_EN)" $(SPHINXOPTS) $(O)
	@echo ""
	@echo "Build finished! The HTML pages are in $(BUILDDIR)/en/html."

# Build both languages
html-all: html html-en
	@echo ""
	@echo "All documentation built!"

# Build HTML with live reload (Russian)
livehtml:
	@$(SPHINXBUILD) -M html "$(SOURCEDIR_RU)" "$(BUILDDIR)" -c "$(SOURCEDIR_RU)" $(SPHINXOPTS) -W --keep-going --watch "$(SOURCEDIR_RU)"

# Clean build directory
clean:
	@rm -rf $(BUILDDIR)
	@echo "Build directory cleaned."

# Catch-all target: route all unknown targets to Sphinx using the new
# "make mode" option.  $(O) is meant as a shortcut for $(SPHINXOPTS).
%: Makefile
	@$(SPHINXBUILD) -M $@ "$(SOURCEDIR_RU)" "$(BUILDDIR)" -c "$(SOURCEDIR_RU)" $(SPHINXOPTS) $(O)
