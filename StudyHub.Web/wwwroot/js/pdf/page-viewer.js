import * as pdfjs from "/lib/pdfjs/pdf.min.mjs";

pdfjs.GlobalWorkerOptions.workerSrc = "/lib/pdfjs/pdf.worker.min.mjs";

const host = document.querySelector("[data-pdf-page]");

if (host) {
    const canvas = host.querySelector("canvas");
    const loading = host.querySelector(".pdf-page-loading");

    try {
        const pdf = await pdfjs.getDocument({
            url: host.dataset.pdfUrl,
            withCredentials: true
        }).promise;

        const page = await pdf.getPage(Number(host.dataset.pageNumber));
        const original = page.getViewport({ scale: 1 });
        const availableWidth = Math.max(host.clientWidth - 32, 320);
        const scale = Math.min(2, availableWidth / original.width);
        const viewport = page.getViewport({ scale });
        const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

        canvas.width = Math.floor(viewport.width * pixelRatio);
        canvas.height = Math.floor(viewport.height * pixelRatio);
        canvas.style.width = `${viewport.width}px`;
        canvas.style.height = `${viewport.height}px`;

        await page.render({
            canvasContext: canvas.getContext("2d"),
            viewport,
            transform: pixelRatio === 1
                ? null
                : [pixelRatio, 0, 0, pixelRatio, 0, 0]
        }).promise;

        loading.remove();
        canvas.hidden = false;
    } catch {
        loading.innerHTML =
            '<i class="bi bi-exclamation-triangle"></i> Không thể tải trang PDF.';
        loading.classList.add("is-error");
    }
}
