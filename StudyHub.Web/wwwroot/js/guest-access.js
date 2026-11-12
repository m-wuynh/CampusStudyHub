// Guest pages render the same views as signed-in pages, without personal data.
// Server authorization remains the authority for protected routes and mutations.
(() => {
    const login = () => window.location.assign('/Login?returnUrl=' +
        encodeURIComponent(window.location.pathname + window.location.search));

    document.addEventListener('click', event => {
        const control = event.target.closest('button, input, select, textarea, [data-requires-login]');
        if (!control) return;
        // Navigation, tabs, filters and calendar view controls remain available.
        if (control.closest('#app-sidebar, #app-topbar')) return;
        if (control.matches('[data-bs-dismiss], .nav-link, .group-tab, .fc-button')) return;
        if (control.closest('form[method="get"], form[method="GET"]')) return;
        const mutation = control.matches('[data-requires-login], [data-bs-toggle="modal"], button[onclick]') ||
            (control.matches('button') && control.type === 'submit');
        if (!mutation) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        login();
    }, true);

    document.addEventListener('submit', event => {
        if (event.target.method.toLowerCase() === 'get') return;
        event.preventDefault();
        event.stopImmediatePropagation();
        login();
    }, true);
})();
