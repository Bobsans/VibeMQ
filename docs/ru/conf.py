# Configuration file for the Sphinx documentation builder (Russian version).
#
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

templates_path = ['../_templates']
exclude_patterns = ['_build', 'Thumbs.db', '.DS_Store', '**.ipynb_checkpoints']

# -- Options for HTML output -------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-html-output

html_theme = 'sphinx_rtd_theme'
html_static_path = ['../_static']
html_logo = '../_static/logo.png' if os.path.exists('../_static/logo.png') else None
html_favicon = '../_static/favicon.ico' if os.path.exists('../_static/favicon.ico') else None

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

# Custom CSS and JS files for language selector
html_css_files = [
    'language-selector.css',
]

html_js_files = [
    'language-selector.js',
]

# -- Options for EPUB output
epub_show_urls = 'footnote'

# -- Language settings -------------------------------------------------------
language = 'ru'

# Available languages for the language selector
html_context = {
    'display_language_selector': True,
    'language_code': 'ru',
    'available_languages': {
        'ru': 'Русский',
        'en': 'English',
    },
    'current_language': 'ru',
}

# -- Version settings --------------------------------------------------------
# Versioning for documentation
html_context['version'] = version
html_context['display_lower_left'] = True

# If you're building multiple versions, configure them here
# For ReadTheDocs, this is usually auto-configured
if os.environ.get('READTHEDOCS'):
    html_context['READTHEDOCS'] = True
