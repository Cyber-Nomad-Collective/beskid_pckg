window.pckgTurnstile = {
  render: function (elementId, siteKey, dotNetRef) {
    const el = document.getElementById(elementId);
    if (!el || !siteKey) {
      return;
    }
    const run = function () {
      if (!window.turnstile) {
        return;
      }
      window.turnstile.render(el, {
        sitekey: siteKey,
        callback: function (token) {
          dotNetRef.invokeMethodAsync('SetToken', token);
        },
        'error-callback': function () {
          dotNetRef.invokeMethodAsync('SetToken', null);
        },
        'expired-callback': function () {
          dotNetRef.invokeMethodAsync('SetToken', null);
        }
      });
    };
    if (window.turnstile) {
      run();
      return;
    }
    const existing = document.querySelector('script[data-pckg-turnstile]');
    if (existing) {
      existing.addEventListener('load', run);
      return;
    }
    const s = document.createElement('script');
    s.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    s.async = true;
    s.defer = true;
    s.dataset.pckgTurnstile = '1';
    s.onload = run;
    document.head.appendChild(s);
  }
};
