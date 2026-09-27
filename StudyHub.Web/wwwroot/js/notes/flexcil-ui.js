(() => {
// Library UI only. Reader controls are owned by reader.js.
const items = document.getElementById('library-items');
if (items) {
    document.querySelectorAll('[data-filter]').forEach(button => {
        button.addEventListener('click', () => {
            const filter = button.dataset.filter;
            document.querySelectorAll('[data-filter]').forEach(b => {
                const active = b === button;
                b.classList.toggle('active', active);
                b.setAttribute('aria-pressed', String(active));
            });
            let visible = 0;
            items.querySelectorAll('[data-kind]').forEach(item => {
                item.hidden = filter !== 'all' && item.dataset.kind !== filter;
                if (!item.hidden) visible++;
            });
            document.getElementById('library-empty').hidden = visible > 0;
        });
    });
    document.querySelectorAll('[data-view]').forEach(button => {
        button.addEventListener('click', () => {
            items.classList.toggle('is-list', button.dataset.view === 'list');
            document.querySelectorAll('[data-view]').forEach(b => {
                b.classList.toggle('active', b === button);
                b.setAttribute('aria-pressed', String(b === button));
            });
        });
    });
}

})();
