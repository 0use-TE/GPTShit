// Installed by the native WebView host in child documents. No host objects are exposed.
(() => {
  if (location.origin !== 'https://wokwi.com' || location.pathname !== '/experimental/embed') return;
  let parentOrigin;
  try { parentOrigin = new URL(document.referrer).origin; } catch { return; }
  if (!/^http:\/\/localhost:\d+$/.test(parentOrigin)) return;
  let desired, timer, resizeTimer, attempts = 0;
  // Wokwi's F shortcut fits the circuit after the in-page code panel resizes it.
  window.addEventListener('resize', () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => document.querySelector('svg[class*="connections"]')?.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'f', code: 'KeyF', bubbles: true })), 150);
  });
  function apply() {
    clearTimeout(timer);
    if (typeof desired !== 'boolean') return;
    const button = document.querySelector('button[aria-label="toggle dark mode"]');
    const canvas = document.querySelector('[class*="simulation_container"]');
    if (!button || !canvas) {
      if (++attempts < 200) timer = setTimeout(apply, 50);
      return;
    }
    document.documentElement.dataset.controlLabTheme = desired ? 'dark' : 'light';
    if (!document.querySelector('#control-lab-theme-style')) {
      const style = document.createElement('style'); style.id = 'control-lab-theme-style';
      style.textContent = `html[data-control-lab-theme="light"] body{background:#fff;color:#3b3b3b}
        html[data-control-lab-theme="light"] header,html[data-control-lab-theme="light"] header [role="banner"]{background:#fff!important;color:#3b3b3b!important}
        html[data-control-lab-theme="light"] header nav a{color:#3b3b3b!important}
        html[data-control-lab-theme="light"] header a[href="/"] svg g{fill:#3b3b3b}
        html[data-control-lab-theme="light"] [class*="simulation_container"]{background:#f5f5f5!important}`;
      document.head.append(style);
    }
    const dark = [...canvas.classList].some(name => name.includes('simulation_darkMode'));
    if (dark !== desired) { button.click(); timer = setTimeout(apply, 50); return; }
    window.parent.postMessage({ type: 'control-lab-theme-applied', dark }, parentOrigin);
  }
  window.addEventListener('message', event => {
    if (event.source !== window.parent || event.origin !== parentOrigin || event.data?.type !== 'control-lab-theme' || typeof event.data.dark !== 'boolean') return;
    desired = event.data.dark; attempts = 0; apply();
  });
  window.parent.postMessage({ type: 'control-lab-theme-ready' }, parentOrigin);
})();
