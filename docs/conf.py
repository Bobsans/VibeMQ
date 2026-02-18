# Configuration file for the Sphinx documentation builder.
#
# This file is configured for i18n support using gettext.
# For the full list of built-in configuration values, see the documentation:
# https://www.sphinx-doc.org/en/master/usage/configuration.html

import os
import sys

# -- Project information -----------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#project-information

project = 'VibeMQ'
copyright = '2026, Darkboy'
author = 'Darkboy'
release = '1.0.0'
version = '1.0.0'

# -- General configuration ---------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#general-configuration

extensions = [
    'sphinx.ext.duration',
    'sphinx.ext.doctest',
    'sphinx.ext.autodoc',
    'sphinx.ext.autosummary',
    'sphinx.ext.intersphinx',
    'sphinx.ext.viewcode',
    'sphinx.ext.githubpages',
    'sphinx_copybutton',
]

intersphinx_mapping = {
    'rtd': ('https://docs.readthedocs.io/en/stable/', None),
    'python': ('https://docs.python.org/3/', None),
}

intersphinx_disabled_domains = ['std']

templates_path = ['_templates']
exclude_patterns = ['_build', 'Thumbs.db', '.DS_Store', '**.ipynb_checkpoints', 'locale', 'docs']

# -- Options for i18n / gettext ----------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-internationalization

# Directory containing translation files
locale_dirs = ['locale']

# Languages to build
language = 'en'

# Gettext settings for better translation management
# UUID helps track changes in source files
gettext_uuid = True

# Non-compact format makes it easier to work with translations
gettext_compact = False

# -- Options for HTML output -------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-html-output

html_theme = 'sphinx_rtd_theme'
html_static_path = ['_static']
html_logo = '_static/logo.png' if os.path.exists('_static/logo.png') else None
html_favicon = '_static/favicon.ico' if os.path.exists('_static/favicon.ico') else None

# HTML theme options
html_theme_options = {
    'logo_only': False,
    'display_version': True,
    'prev_next_buttons_location': 'bottom',
    'style_external_links': True,
    'navigation_depth': 4,
    'collapse_navigation': False,
    'sticky_navigation': True,
    'includehidden': True,
    'titles_only': False,
}

# -- Options for EPUB output
epub_show_urls = 'footnote'

# -- Read the Docs specific settings -----------------------------------------
if os.environ.get('READTHEDOCS'):
    html_context = {
        'READTHEDOCS': True,
        'display_lower_left': True,
        'version': version,
    }
