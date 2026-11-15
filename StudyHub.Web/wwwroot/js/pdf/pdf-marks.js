const page = document.getElementById("pdf-page");
const canvas = document.getElementById("pdf-canvas");
const layer = document.getElementById("mark-layer");
const status = document.getElementById("pdf-status");
const form = document.getElementById("mark-form");
const eraseForm = document.getElementById("erase-form");
const tool = document.getElementById("mark-tool");
const color = document.getElementById("mark-color");
const comment = document.getElementById("mark-text");
const undoButton = document.getElementById("undo-mark");
const colorControl = document.querySelector("[data-color-control]");
const commentEditor = document.querySelector("[data-comment-editor]");
const zoom = document.getElementById("pdf-zoom");
const zoomIn = document.getElementById("pdf-zoom-in");
const zoomOut = document.getElementById("pdf-zoom-out");
const readOnly = status?.dataset.readOnly === "true";
const svgNamespace = "http://www.w3.org/2000/svg";
const zoomLevels = [0.5, 0.75, 1, 1.25, 1.5, 2, 3];
let pdfPage;
let rendering = false;
let renderAgain = false;
let hasRenderedPage = false;
let flushMarkAutosave = async () => true;
const scrollStorageKey = eraseForm
    ? `pdf:position:${eraseForm.elements.namedItem("documentId").value}:${eraseForm.elements.namedItem("pageNumber").value}`
    : null;
let pendingScrollRestore = null;

if (scrollStorageKey) {
    try {
        const savedPosition = JSON.parse(sessionStorage.getItem(scrollStorageKey));
        sessionStorage.removeItem(scrollStorageKey);
        if (savedPosition && Date.now() - savedPosition.savedAt < 30_000) {
            pendingScrollRestore = savedPosition;
            if (zoomLevels.includes(Number(savedPosition.zoom))) {
                zoom.value = String(savedPosition.zoom);
            }
        }
    } catch {
        sessionStorage.removeItem(scrollStorageKey);
    }
}

const setStatus = (message, state = "") => {
    status.textContent = message;
    status.dataset.state = state;
};

async function requestPageNavigation(url) {
    if (window.parent === window) return false;
    if (!await flushMarkAutosave()) return true;
    window.parent.postMessage({ type: "pdf:page-request", url }, window.location.origin);
    return true;
}

document.querySelectorAll("[data-reader-page-link]").forEach(link => {
    link.addEventListener("click", event => {
        if (window.parent === window) return;
        event.preventDefault();
        requestPageNavigation(link.href);
    });
});

document.querySelector(".pdf-viewer-page-form")?.addEventListener("submit", event => {
    if (window.parent === window) return;
    event.preventDefault();
    const pageForm = event.currentTarget;
    const url = new URL(pageForm.action, window.location.origin);
    for (const [name, value] of new FormData(pageForm)) {
        if (String(value)) url.searchParams.set(name, String(value));
    }
    requestPageNavigation(url.toString());
});

async function renderPage() {
    const natural = pdfPage.getViewport({ scale: 1 });
    const surface = page.parentElement;
    const preservePosition = hasRenderedPage;
    const oldWidth = preservePosition ? page.clientWidth : 0;
    const oldHeight = preservePosition ? page.clientHeight : 0;
    const pageRect = page.getBoundingClientRect();
    const surfaceRect = surface.getBoundingClientRect();
    const centerX = oldWidth
        ? Math.max(0, Math.min(1, (surfaceRect.left + surface.clientWidth / 2 - pageRect.left) / oldWidth))
        : 0.5;
    const centerY = oldHeight
        ? Math.max(0, Math.min(1, (surfaceRect.top + surface.clientHeight / 2 - pageRect.top) / oldHeight))
        : 0;
    const scale = (Math.max(100, surface.clientWidth - 64) / natural.width) * Number(zoom.value);
    const viewport = pdfPage.getViewport({ scale });
    const pixelRatio = window.devicePixelRatio || 1;

    canvas.width = Math.round(viewport.width * pixelRatio);
    canvas.height = Math.round(viewport.height * pixelRatio);
    canvas.style.width = `${viewport.width}px`;
    canvas.style.height = `${viewport.height}px`;
    page.style.width = `${viewport.width}px`;
    page.style.height = `${viewport.height}px`;
    if (preservePosition && oldWidth) {
        const leftMargin = Math.max(0, (surface.clientWidth - viewport.width) / 2);
        surface.scrollLeft = leftMargin + centerX * viewport.width - surface.clientWidth / 2;
        surface.scrollTop = centerY * viewport.height - surface.clientHeight / 2;
    }
    await pdfPage.render({
        canvasContext: canvas.getContext("2d"),
        viewport,
        transform: pixelRatio === 1 ? null : [pixelRatio, 0, 0, pixelRatio, 0, 0]
    }).promise;
    if (!hasRenderedPage) {
        surface.scrollTop = pendingScrollRestore?.top ?? 0;
        surface.scrollLeft = pendingScrollRestore?.left ?? 0;
        pendingScrollRestore = null;
        hasRenderedPage = true;
    }
    layer.classList.add("ready");
}

async function requestRender() {
    if (!pdfPage) return;
    if (rendering) { renderAgain = true; return; }
    rendering = true;
    try {
        do { renderAgain = false; await renderPage(); } while (renderAgain);
    } catch {
        setStatus("Không thể hiển thị trang PDF. Hãy tải lại trang.", "error");
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
        const pdfjs = await import("/lib/pdfjs/pdf.mjs");
        pdfjs.GlobalWorkerOptions.workerSrc = "/lib/pdfjs/pdf.worker.mjs";
        const pdf = await pdfjs.getDocument({ url: page.dataset.fileUrl, withCredentials: true }).promise;
        pdfPage = await pdf.getPage(Number(page.dataset.page));
        await requestRender();
        setStatus(readOnly
            ? "Tài liệu đang ở chế độ chỉ xem."
            : "Dùng Bàn tay để đọc hoặc chọn một công cụ để đánh dấu.");
    } catch {
        setStatus("Không mở được trang PDF. Hãy tải lại trang.", "error");
    }
}

if (page && canvas && layer) {
    updateZoomButtons();
    openPage();
    zoom.addEventListener("change", () => { updateZoomButtons(); requestRender(); });
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
        resizeTimer = setTimeout(requestRender, 150);
    }).observe(page.parentElement);
}

if (form) {
    const field = name => form.elements.namedItem(name);
    const drafts = [];
    let start = null;
    let lastPoint = null;
    let points = [];
    let preview = null;
    let activeTextEntry = null;
    let saveTimer = null;
    let savePromise = null;

    const coordinate = event => {
        const rect = layer.getBoundingClientRect();
        return {
            x: Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)),
            y: Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height))
        };
    };
    const cancelDrawing = () => {
        preview?.remove(); preview = null; start = null; lastPoint = null; points = [];
    };
    const updateDraftState = () => {
        field("marksJson").value = JSON.stringify(drafts.map(entry => entry.payload));
        undoButton.disabled = drafts.length === 0;
    };
    const scheduleSave = () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => saveDrafts(), 650);
    };
    const saveDrafts = async () => {
        clearTimeout(saveTimer);
        if (savePromise) {
            await savePromise;
            return drafts.length ? saveDrafts() : true;
        }
        if (!drafts.length) return true;
        if (drafts.some(entry => entry.payload.Kind === "Comment" && !entry.payload.Text?.trim())) {
            comment.focus();
            setStatus("Hãy nhập nội dung trước khi đặt chữ lên PDF.", "error");
            return false;
        }
        const batch = [...drafts];
        field("marksJson").value = JSON.stringify(batch.map(entry => entry.payload));
        setStatus("Đang lưu dấu…", "saving");
        savePromise = (async () => {
            try {
                const response = await fetch(form.action, {
                    method: "POST", body: new FormData(form), credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json().catch(() => ({}));
                if (!response.ok) throw new Error(result.message || "Không thể lưu dấu.");
                if (!Array.isArray(result.marks) || result.marks.length !== batch.length) {
                    throw new Error("Kết quả lưu dấu không hợp lệ.");
                }
                batch.forEach((entry, index) => {
                    entry.element.removeAttribute("data-draft");
                    entry.element.dataset.markId = result.marks[index].id;
                    entry.element.dataset.version = result.marks[index].versionToken;
                    const position = drafts.indexOf(entry);
                    if (position >= 0) drafts.splice(position, 1);
                });
                if (batch.includes(activeTextEntry)) activeTextEntry = null;
                updateDraftState();
                setStatus("Đã lưu tự động.", "saved");
                return true;
            } catch (error) {
                setStatus(`${error.message} Nét vẫn được giữ lại để thử lưu lại.`, "error");
                return false;
            } finally {
                savePromise = null;
            }
        })();
        const saved = await savePromise;
        if (saved && drafts.length) scheduleSave();
        return saved;
    };
    flushMarkAutosave = async () => {
        clearTimeout(saveTimer);
        while (drafts.length) if (!await saveDrafts()) return false;
        return true;
    };
    const updateToolControls = () => {
        colorControl?.classList.toggle("d-none", !["Highlight", "Ink", "Comment"].includes(tool.value));
        commentEditor?.classList.toggle("d-none", tool.value !== "Comment");
    };
    const draw = current => {
        preview?.remove();
        const kind = tool.value;
        preview = document.createElementNS(svgNamespace, kind === "Ink" ? "polyline" : kind === "Comment" ? "text" : "rect");
        if (kind === "Ink") {
            preview.setAttribute("points", points.map(point => `${point.x * 1000},${point.y * 1000}`).join(" "));
            preview.setAttribute("fill", "none"); preview.setAttribute("stroke", color.value); preview.setAttribute("stroke-width", "4");
        } else if (kind === "Comment") {
            preview.setAttribute("x", String(start.x * 1000)); preview.setAttribute("y", String(start.y * 1000 + 20));
            preview.setAttribute("fill", color.value); preview.setAttribute("font-size", "20"); preview.textContent = comment.value || "Aa";
        } else {
            preview.setAttribute("x", String(Math.min(start.x, current.x) * 1000));
            preview.setAttribute("y", String(Math.min(start.y, current.y) * 1000));
            preview.setAttribute("width", String(Math.abs(current.x - start.x) * 1000));
            preview.setAttribute("height", String(Math.abs(current.y - start.y) * 1000));
            preview.setAttribute("fill", color.value); preview.setAttribute("opacity", "0.42");
        }
        layer.append(preview);
    };

    document.querySelectorAll("[data-mark-tool]").forEach(button => {
        button.addEventListener("click", () => {
            tool.value = button.dataset.markTool;
            document.body.dataset.tool = tool.value;
            document.querySelectorAll("[data-mark-tool]").forEach(item => {
                const active = item === button;
                item.classList.toggle("active", active); item.setAttribute("aria-pressed", String(active));
            });
            updateToolControls(); cancelDrawing();
            setStatus(tool.value === "Pan" ? "Kéo hoặc cuộn để đọc PDF."
                : tool.value === "Eraser" ? "Chạm trực tiếp vào dấu để tẩy."
                    : "Thay đổi sẽ được lưu tự động.");
        });
    });
    document.body.dataset.tool = tool.value;
    updateToolControls();
    undoButton.addEventListener("click", () => {
        const entry = drafts.pop();
        if (!entry) return;
        entry.element.remove();
        if (activeTextEntry === entry) activeTextEntry = null;
        updateDraftState();
        drafts.length ? scheduleSave() : clearTimeout(saveTimer);
        setStatus(drafts.length ? "Đang chờ lưu tự động…" : "Đã bỏ dấu vừa tạo.");
    });
    color.addEventListener("change", () => {
        if (preview) preview.setAttribute(tool.value === "Ink" ? "stroke" : "fill", color.value);
    });
    comment.addEventListener("input", () => {
        if (!activeTextEntry) return;
        activeTextEntry.payload.Text = comment.value;
        activeTextEntry.element.textContent = comment.value || "Aa";
        updateDraftState(); scheduleSave();
    });

    layer.addEventListener("pointerdown", async event => {
        if (!layer.classList.contains("ready")) return;
        if (tool.value === "Eraser") {
            const mark = event.target.closest("[data-mark-id]");
            if (!mark) { setStatus("Chạm trực tiếp lên dấu muốn tẩy."); return; }
            if (!await flushMarkAutosave()) return;
            eraseForm.elements.namedItem("markId").value = mark.dataset.markId;
            eraseForm.elements.namedItem("versionToken").value = mark.dataset.version;
            if (scrollStorageKey) {
                const surface = page.parentElement;
                sessionStorage.setItem(scrollStorageKey, JSON.stringify({
                    top: surface.scrollTop,
                    left: surface.scrollLeft,
                    zoom: zoom.value,
                    savedAt: Date.now()
                }));
            }
            eraseForm.requestSubmit();
            return;
        }
        if (drafts.length >= 100) { setStatus("Đang có quá nhiều dấu chờ lưu.", "error"); return; }
        cancelDrawing(); start = coordinate(event); lastPoint = start; points.push(start);
        layer.setPointerCapture(event.pointerId); draw(start);
    });
    layer.addEventListener("pointermove", event => {
        if (!start || tool.value === "Comment") return;
        const current = coordinate(event); lastPoint = current;
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
        const x = kind === "Comment" ? Math.min(start.x, .98) : kind === "Ink" ? Math.min(Math.min(...xs), .999) : Math.min(...xs);
        const y = kind === "Comment" ? Math.min(start.y, .98) : kind === "Ink" ? Math.min(Math.min(...ys), .999) : Math.min(...ys);
        const width = kind === "Comment" ? .02 : kind === "Ink" ? Math.min(1 - x, Math.max(.001, Math.max(...xs) - x)) : Math.max(...xs) - x;
        const height = kind === "Comment" ? .02 : kind === "Ink" ? Math.min(1 - y, Math.max(.001, Math.max(...ys) - y)) : Math.max(...ys) - y;
        if (width > 0 && height > 0) {
            const payload = { Kind: kind, Color: color.value, X: x, Y: y, Width: width, Height: height,
                Points: kind === "Ink" ? [...points] : null, Text: kind === "Comment" ? comment.value : null };
            const entry = { payload, element: preview };
            drafts.push(entry); preview.dataset.draft = "true";
            if (kind === "Comment") { activeTextEntry = entry; comment.focus(); }
            preview = null; updateDraftState(); scheduleSave(); setStatus("Đang chờ lưu tự động…", "saving");
        } else cancelDrawing();
        start = null; lastPoint = null; points = [];
    };
    layer.addEventListener("pointerup", event => finishDrawing(coordinate(event)));
    layer.addEventListener("pointercancel", () => finishDrawing(lastPoint ?? start));
    window.addEventListener("beforeunload", event => {
        if (!drafts.length && !savePromise) return;
        event.preventDefault();
    });
}
