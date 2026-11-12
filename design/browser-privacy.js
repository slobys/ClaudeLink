(() => {
  if (location.origin !== __CLAUDELINK_ORIGIN__ || !location.pathname.startsWith('/claude')) return;
  const style = document.createElement('style');
  style.textContent = '.ip-link,.ip-mask-target{visibility:hidden!important}';
  const mount = () => { if (!style.isConnected && !document.documentElement.dataset.claudeLinkIpHidden) document.documentElement.append(style); };
  const guard = new MutationObserver(mount);
  guard.observe(document, {childList:true,subtree:true});
  if (document.documentElement) mount();
  const enable = () => {
    const toggle = document.getElementById('ipMaskToggle');
    if (!toggle || typeof window.applyAllIpMasks !== 'function') return;
    toggle.checked = true;
    toggle.dispatchEvent(new Event('change', {bubbles:true}));
    window.__ipMaskOn = true;
    window.applyAllIpMasks();
    document.documentElement.dataset.claudeLinkIpHidden = 'true';
    guard.disconnect();
    style.remove();
    clearInterval(timer);
  };
  // Run after the site's DOMContentLoaded handlers, which otherwise reset its switch to off.
  document.addEventListener('DOMContentLoaded', () => setTimeout(enable, 0), {once:true});
  const timer = setInterval(() => { if (document.readyState !== 'loading') enable(); }, 200);
})();
