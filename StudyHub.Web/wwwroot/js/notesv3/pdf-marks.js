const page = document.getElementById("pdf-page");
const canvas = document.getElementById("pdf-canvas");
const layer = document.getElementById("mark-layer");
const status = document.getElementById("pdf-status");
const form = document.getElementById("mark-form");
const saveButton = document.getElementById("save-mark");
const tool = document.getElementById("mark-tool");
const color = document.getElementById("mark-color");
const comment = document.getElementById("mark-text");
const zoom = document.getElementById("pdf-zoom");
const zoomIn = document.getElementById("pdf-zoom-in");
const zoomOut = document.getElementById("pdf-zoom-out");
const eraseForm = document.getElementById("erase-form");
const undoButton = document.getElementById("undo-mark");
const svgNamespace = "http://www.w3.org/2000/svg";
let pdfPage;
let rendering = false;
let renderAgain = false;
const zoomLevels = [0.5, 0.75, 1, 1.25, 1.5, 2, 3];

function rememberReaderPosition() {
    try {
        const parent = window.parent;
        const key = `notesv3-reader-position:${parent.location.pathname.toLowerCase()}`;
        sessionStorage.setItem(key, JSON.stringify({
            scrollY: parent.scrollY,
            notesOpen: parent.document.getElementById("notesv3-note-toggle").checked
        }));
    } catch {
        // Viewer vẫn chuyển trang nếu trình duyệt chặn quyền truy cập trang cha.
    }
}

document.querySelectorAll("[data-reader-page-link]").forEach(link =>
    link.addEventListener("click", rememberReaderPosition));
document.querySelector(".notesv3-viewer-page-form")
    ?.addEventListener("submit", rememberReaderPosition);

async function renderPage() {
    const naturalSize = pdfPage.getViewport({ scale: 1 });
    const surface = page.parentElement;
    const oldWidth = page.clientWidth;
    const oldHeight = page.clientHeight;
    const pageRect = page.getBoundingClientRect();
    const surfaceRect = surface.getBoundingClientRect();
    const centerX = oldWidth
        ? Math.max(0, Math.min(1,
            (surfaceRect.left + surface.clientWidth / 2 - pageRect.left) / oldWidth))
        : 0.5;
    const centerY = oldHeight
        ? Math.max(0, Math.min(1,
            (surfaceRect.top + surface.clientHeight / 2 - pageRect.top) / oldHeight))
        : 0;
    const availableWidth = Math.max(100, surface.clientWidth - 44);
    const scale = (availableWidth / naturalSize.width) * Number(zoom.value);
    const viewport = pdfPage.getViewport({ scale });
    const pixelRatio = window.devicePixelRatio || 1;

    canvas.width = Math.round(viewport.width * pixelRatio);
    canvas.height = Math.round(viewport.height * pixelRatio);
    canvas.style.width = `${viewport.width}px`;
    canvas.style.height = `${viewport.height}px`;
    page.style.width = `${viewport.width}px`;
    page.style.height = `${viewport.height}px`;
    if (oldWidth) {
        const leftMargin = Math.max(0, (surface.clientWidth - viewport.width) / 2);
        surface.scrollLeft = leftMargin + centerX * viewport.width - surface.clientWidth / 2;
        surface.scrollTop = centerY * viewport.height - surface.clientHeight / 2;
    }

    await pdfPage.render({
        canvasContext: canvas.getContext("2d"),
        viewport,
        transform: pixelRatio === 1
            ? null : [pixelRatio, 0, 0, pixelRatio, 0, 0]
    }).promise;
    layer.classList.add("ready");
}

async function requestRender() {
    if (!pdfPage) return;
    if (rendering) {
        renderAgain = true;
        return;
    }
    rendering = true;
    try {
        do {
            renderAgain = false;
            await renderPage();
        } while (renderAgain);
    } catch {
        status.textContent = "Không thể phóng to trang PDF. Hãy tải lại trang.";
    } finally {
        rendering = false;
    }
}

function updateZoomButtons() {
    const index = zoomLevels.indexOf(Number(zoom.value));
    zoomOut.disabled = index <= 0;
    zoomIn.disabled = index >= zoomLevels.length - 1;
}

function stepZoom(direction) {
    const index = zoomLevels.indexOf(Number(zoom.value));
    const next = Math.max(0, Math.min(zoomLevels.length - 1, index + direction));
    if (next === index) return;
    zoom.value = String(zoomLevels[next]);
    updateZoomButtons();
    requestRender();
}

async function openPage() {
    try {
        const pdfjs = await import(
            "https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.mjs"
        );
        pdfjs.GlobalWorkerOptions.workerSrc =
            "https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.worker.mjs";

        const pdf = await pdfjs.getDocument({
            url: page.dataset.fileUrl,
            withCredentials: true
        }).promise;
        pdfPage = await pdf.getPage(Number(page.dataset.page));
        await requestRender();
        status.textContent = "Chọn công cụ, đánh dấu trên PDF rồi bấm Lưu dấu.";
    } catch {
        status.textContent = "Không mở được trang PDF. Hãy tải lại trang.";
    }
}

if (page && canvas && layer) {
    updateZoomButtons();
    openPage();
    zoom?.addEventListener("change", () => {
        updateZoomButtons();
        requestRender();
    });
    zoomIn.addEventListener("click", () => stepZoom(1));
    zoomOut.addEventListener("click", () => stepZoom(-1));
    page.parentElement.addEventListener("wheel", event => {
        if (!event.ctrlKey) return;
        event.preventDefault();
        stepZoom(event.deltaY < 0 ? 1 : -1);
    }, { passive: false });
    let resizeTimer;
    new ResizeObserver(() => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            requestRender();
        }, 150);
    }).observe(page.parentElement);
}

if (form) {
    let start = null;
    let lastPoint = null;
    let points = [];
    let preview = null;
    const drafts = [];
    let activeTextDraft = null;
    let activeTextPreview = null;

    const field = name => form.elements.namedItem(name);
    const coordinate = event => {
        const rect = layer.getBoundingClientRect();
        return {
            x: Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)),
            y: Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height))
        };
    };

    const cancelDrawing = () => {
        preview?.remove();
        preview = null;
        start = null;
        lastPoint = null;
        points = [];
    };
    const updateSaveButton = () => {
        field("marksJson").value = JSON.stringify(drafts);
        saveButton.disabled = drafts.length === 0;
        undoButton.disabled = drafts.length === 0;
    };

    const draw = current => {
        preview?.remove();
        const kind = tool.value;
        preview = document.createElementNS(svgNamespace,
            kind === "Ink" ? "polyline" : kind === "Comment" ? "text" : "rect");

        if (kind === "Ink") {
            preview.setAttribute("points", points.map(point =>
                `${point.x * 1000},${point.y * 1000}`).join(" "));
            preview.setAttribute("fill", "none");
            preview.setAttribute("stroke", color.value);
            preview.setAttribute("stroke-width", "4");
        } else if (kind === "Comment") {
            preview.setAttribute("x", String(start.x * 1000));
            preview.setAttribute("y", String(start.y * 1000 + 20));
            preview.setAttribute("fill", color.value);
            preview.setAttribute("font-size", "20");
            preview.textContent = comment.value || "Aa";
        } else {
            preview.setAttribute("x", String(Math.min(start.x, current.x) * 1000));
            preview.setAttribute("y", String(Math.min(start.y, current.y) * 1000));
            preview.setAttribute("width", String(Math.abs(current.x - start.x) * 1000));
            preview.setAttribute("height", String(Math.abs(current.y - start.y) * 1000));
            preview.setAttribute("fill", color.value);
            preview.setAttribute("opacity", "0.42");
        }
        layer.append(preview);
    };

    tool.addEventListener("change", cancelDrawing);
    document.querySelectorAll("[data-mark-tool]").forEach(button => {
        button.addEventListener("click", () => {
            tool.value = button.dataset.markTool;
            document.body.dataset.tool = tool.value;
            document.querySelectorAll("[data-mark-tool]").forEach(item =>
                item.classList.toggle("active", item === button));
            cancelDrawing();
            status.textContent = tool.value === "Pan"
                ? "Kéo hoặc cuộn để đọc PDF."
                : tool.value === "Eraser"
                    ? "Chạm vào dấu đã lưu để tẩy."
                    : "Đánh dấu trên PDF rồi bấm Lưu dấu.";
        });
    });
    document.body.dataset.tool = tool.value;
    undoButton.addEventListener("click", () => {
        const removed = drafts.pop();
        if (!removed) return;
        layer.querySelectorAll("[data-draft]").item(drafts.length)?.remove();
        if (activeTextDraft === removed) {
            activeTextDraft = null;
            activeTextPreview = null;
        }
        updateSaveButton();
        status.textContent = drafts.length
            ? `${drafts.length} dấu chưa lưu.`
            : "Chưa có dấu nháp.";
    });
    color.addEventListener("change", () => {
        if (preview) preview.setAttribute(tool.value === "Ink" ? "stroke" : "fill", color.value);
    });
    comment.addEventListener("input", () => {
        if (activeTextDraft) {
            activeTextDraft.Text = comment.value;
            activeTextPreview.textContent = comment.value || "Aa";
            updateSaveButton();
        } else if (preview?.tagName.toLowerCase() === "text") {
            preview.textContent = comment.value || "Aa";
        }
    });

    layer.addEventListener("pointerdown", event => {
        if (!layer.classList.contains("ready")) return;
        if (tool.value === "Eraser") {
            const mark = event.target.closest("[data-mark-id]");
            if (!mark) {
                status.textContent = "Bấm trực tiếp lên dấu đã lưu để tẩy.";
                return;
            }
            eraseForm.elements.namedItem("markId").value = mark.dataset.markId;
            eraseForm.elements.namedItem("versionToken").value = mark.dataset.version;
            eraseForm.requestSubmit();
            return;
        }
        if (drafts.length >= 100) {
            status.textContent = "Đã có 100 dấu. Hãy lưu trước khi vẽ thêm.";
            return;
        }
        cancelDrawing();
        start = coordinate(event);
        lastPoint = start;
        points.push(start);
        layer.setPointerCapture(event.pointerId);
        draw(start);
    });

    layer.addEventListener("pointermove", event => {
        if (!start || tool.value === "Comment") return;
        const current = coordinate(event);
        lastPoint = current;
        if (tool.value === "Ink" && points.length < 1500) points.push(current);
        draw(current);
    });

    const finishDrawing = end => {
        if (!start) return;
        const kind = tool.value;
        if (kind === "Ink" && points.length < 1500) points.push(end);
        draw(end);

        const xs = kind === "Ink" ? points.map(point => point.x) : [start.x, end.x];
        const ys = kind === "Ink" ? points.map(point => point.y) : [start.y, end.y];
        const x = kind === "Comment" ? Math.min(start.x, .98)
            : kind === "Ink" ? Math.min(Math.min(...xs), .999) : Math.min(...xs);
        const y = kind === "Comment" ? Math.min(start.y, .98)
            : kind === "Ink" ? Math.min(Math.min(...ys), .999) : Math.min(...ys);
        const width = kind === "Comment" ? .02
            : kind === "Ink" ? Math.min(1 - x, Math.max(.001, Math.max(...xs) - x))
                : Math.max(...xs) - x;
        const height = kind === "Comment" ? .02
            : kind === "Ink" ? Math.min(1 - y, Math.max(.001, Math.max(...ys) - y))
                : Math.max(...ys) - y;

        if (width > 0 && height > 0) {
            const draft = {
                Kind: kind, Color: color.value, X: x, Y: y,
                Width: width, Height: height,
                Points: kind === "Ink" ? [...points] : null,
                Text: kind === "Comment" ? comment.value : null
            };
            drafts.push(draft);
            preview.dataset.draft = "true";
            if (kind === "Comment") {
                activeTextDraft = draft;
                activeTextPreview = preview;
                comment.focus();
            }
            preview = null;
            updateSaveButton();
            status.textContent = `${drafts.length} dấu chưa lưu. Bạn có thể vẽ tiếp rồi lưu một lần.`;
        } else {
            cancelDrawing();
        }
        start = null;
        lastPoint = null;
        points = [];
    };
    layer.addEventListener("pointerup", event => finishDrawing(coordinate(event)));
    layer.addEventListener("pointercancel", () => finishDrawing(lastPoint ?? start));

    form.addEventListener("submit", event => {
        if (drafts.some(draft => draft.Kind === "Comment" && !draft.Text?.trim())) {
            event.preventDefault();
            comment.focus();
            status.textContent = "Hãy nhập nội dung cho dấu chữ trước khi lưu.";
        }
    });
}
