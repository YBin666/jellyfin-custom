(function() {
  var script = document.createElement('script');
  script.src = '/HubBar/main.js';
  script.type = 'module';
  script.onload = function() {
    console.log('[HubBar] React app loaded successfully');
  };
  script.onerror = function(err) {
    console.error('[HubBar] Failed to load React app:', err);
  };
  document.head.appendChild(script);
})();