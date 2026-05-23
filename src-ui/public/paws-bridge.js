/* paws-bridge.js - Auto-injected into plugins to handle seamless theming */
(function() {
  const ensureLink = (id) => {
    let link = document.getElementById(id);
    if (!link) {
      link = document.createElement('link');
      link.id = id;
      link.rel = 'stylesheet';
      document.head.appendChild(link);
    }
    return link;
  };

  window.addEventListener('message', (event) => {
    const data = event.data;
    if (data && data.type === 'paws:theme-changed') {
      const baseLink = ensureLink('paws-base-link');
      const themeLink = ensureLink('paws-theme-link');
      const customLink = ensureLink('paws-custom-link');

      const apply = () => {
        baseLink.href = data.baseHref;
        themeLink.href = data.baseHref; // Often they are the same or one is redundant
        if (data.customHref) {
          customLink.href = data.customHref;
        } else {
          customLink.href = '';
        }
      };

      // Add transition class
      document.body.classList.add('paws-theme-transitioning');
      apply();

      // Remove after animation
      setTimeout(() => {
        document.body.classList.remove('paws-theme-transitioning');
      }, 350);
    }
  });

  // Signal Ready to parent
  if (document.readyState === 'complete' || document.readyState === 'interactive') {
    window.parent.postMessage({ channel: 'paws:client-ready', id: 0 }, '*');
  } else {
    window.addEventListener('DOMContentLoaded', () => {
      window.parent.postMessage({ channel: 'paws:client-ready', id: 0 }, '*');
    });
  }
})();
