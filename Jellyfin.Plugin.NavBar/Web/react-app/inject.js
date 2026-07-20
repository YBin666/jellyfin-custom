(function() {
  var script = document.createElement('script');
  script.src = '/NavBar/main.js';
  script.type = 'module';
  script.onload = function() {
    console.log('[NavBar] React app loaded successfully');
  };
  script.onerror = function(err) {
    console.error('[NavBar] Failed to load React app:', err);
  };
  document.head.appendChild(script);
})();