window.DiyModule = (function() {
    var CONTAINER_ID = 'shortvideo-diy-view';

    function buildContainer(goBack) {
        var container = document.createElement('div');
        container.id = CONTAINER_ID;
        container.style.cssText = [
            'position: fixed',
            'top: 0', 'left: 0',
            'width: 100%', 'height: 100%',
            'z-index: 9997',
            'background: #101010',
            'overflow: hidden',
            'font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
            '-webkit-user-select: none', 'user-select: none',
            '-webkit-tap-highlight-color: transparent',
            'color: #fff'
        ].join(';');

        var style = document.createElement('style');
        style.textContent = [
            '#' + CONTAINER_ID + ' .diy-header { position: absolute; top: 0; left: 0; right: 0; height: 56px; display: flex; align-items: center; padding: 0 16px; background: rgba(0,0,0,0.4); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); z-index: 20; border-bottom: 1px solid rgba(255,255,255,0.08); }',
            '#' + CONTAINER_ID + ' .diy-back { width: 40px; height: 40px; border-radius: 50%; background: rgba(255,255,255,0.1); display: flex; align-items: center; justify-content: center; cursor: pointer; transition: background 0.15s ease; border: none; color: #fff; }',
            '#' + CONTAINER_ID + ' .diy-back:active { background: rgba(255,255,255,0.2); }',
            '#' + CONTAINER_ID + ' .diy-back svg { display: block; width: 24px; height: 24px; }',
            '#' + CONTAINER_ID + ' .diy-title { flex: 1; text-align: center; font-size: 16px; font-weight: 600; }',
            '#' + CONTAINER_ID + ' .diy-spacer { width: 40px; }',
            '#' + CONTAINER_ID + ' .diy-content { position: absolute; top: 56px; left: 0; right: 0; bottom: 0; overflow-y: auto; padding: 24px; color: #888; text-align: center; }'
        ].join('\n');
        container.appendChild(style);

        var header = document.createElement('div');
        header.className = 'diy-header';

        var backBtn = document.createElement('button');
        backBtn.className = 'diy-back';
        backBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"></polyline></svg>';
        backBtn.addEventListener('click', function(e) {
            e.preventDefault();
            if (goBack) goBack();
        });

        var title = document.createElement('div');
        title.className = 'diy-title';
        title.textContent = 'DIY';

        var spacer = document.createElement('div');
        spacer.className = 'diy-spacer';

        header.appendChild(backBtn);
        header.appendChild(title);
        header.appendChild(spacer);

        var content = document.createElement('div');
        content.className = 'diy-content';
        content.textContent = '这是一个空白的 DIY 页面，待填充内容。';

        container.appendChild(header);
        container.appendChild(content);
        return container;
    }

    return {
        buildContainer: buildContainer
    };
})();
