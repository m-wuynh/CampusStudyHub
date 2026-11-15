(() => {
    const form = document.querySelector('[data-pdf-upload]');
    if (!form) return;

    const input = form.querySelector('input[type="file"]');
    const error = form.querySelector('#upload-file-error');
    const maxBytes = Number(form.dataset.maxFileBytes);
    const message = 'PDF tối đa 25 MiB. Vui lòng chọn file có dung lượng nhỏ hơn.';

    function validateFile() {
        const oversized = (input.files[0]?.size ?? 0) > maxBytes;
        error.textContent = oversized ? message : '';
        error.hidden = !oversized;
        input.classList.toggle('is-invalid', oversized);
        input.setAttribute('aria-invalid', String(oversized));
        if (oversized) input.setAttribute('aria-describedby', error.id);
        else input.removeAttribute('aria-describedby');
        return !oversized;
    }

    input.addEventListener('change', validateFile);
    form.addEventListener('submit', event => {
        if (!validateFile()) {
            event.preventDefault();
            input.focus();
        }
    });
})();
