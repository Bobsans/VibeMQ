// Language selector for ReadTheDocs
// This script adds a language selector to the navigation bar

(function() {
  'use strict';

  // Available languages
  const languages = {
    'ru': 'Русский',
    'en': 'English'
  };

  // Current language from URL or default
  function getCurrentLanguage() {
    const path = window.location.pathname;
    if (path.includes('/en/')) {
      return 'en';
    }
    if (path.includes('/ru/')) {
      return 'ru';
    }
    // Default to Russian if no language in path
    return 'ru';
  }

  // Get base URL without language prefix
  function getBaseURL() {
    const path = window.location.pathname;
    const langPath = path.match(/\/(en|ru)\//);
    
    if (langPath) {
      return path.replace(langPath[0], '/');
    }
    return path;
  }

  // Switch language
  function switchLanguage(lang) {
    const currentPath = window.location.pathname;
    const basePath = getBaseURL();
    
    // Remove current language prefix if exists
    let newPath = basePath.replace(/^\/(en|ru)\//, '/');
    if (!newPath.startsWith('/')) {
      newPath = '/' + newPath;
    }
    
    // Add new language prefix
    if (lang === 'ru') {
      // Russian is default, no prefix needed for root
      newPath = newPath.replace(/^\/ru\//, '/');
    } else {
      // English needs /en/ prefix
      newPath = newPath.replace(/^\/en\//, '/');
      newPath = '/en' + newPath;
    }
    
    window.location.href = newPath + window.location.search + window.location.hash;
  }

  // Create language selector
  function createLanguageSelector() {
    const currentLang = getCurrentLanguage();
    
    // Find the navigation bar
    const navBar = document.querySelector('.rst-versions') || 
                   document.querySelector('.wy-menu-vertical') ||
                   document.querySelector('nav');
    
    if (!navBar) {
      return;
    }

    // Create selector element
    const selector = document.createElement('div');
    selector.className = 'language-selector';
    selector.innerHTML = `
      <select id="language-select" style="margin: 10px; padding: 5px;">
        ${Object.entries(languages).map(([code, name]) => 
          `<option value="${code}" ${code === currentLang ? 'selected' : ''}>${name}</option>`
        ).join('')}
      </select>
    `;

    // Insert selector
    if (navBar.parentNode) {
      navBar.parentNode.insertBefore(selector, navBar.nextSibling);
    } else {
      document.body.appendChild(selector);
    }

    // Add event listener
    const select = document.getElementById('language-select');
    if (select) {
      select.addEventListener('change', function(e) {
        switchLanguage(e.target.value);
      });
    }
  }

  // Initialize when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', createLanguageSelector);
  } else {
    createLanguageSelector();
  }
})();
