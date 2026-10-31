import * as Y from "https://esm.sh/yjs@13.6.27";

import * as pdfjs from
    "https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.mjs";

import Quill from "https://esm.sh/quill@2.0.3";

import {
    api,
    appUrl,
    setStatus
} from "./notes-ui.js";

import {
    GroupTextSession,
    fromBase64
} from "./collab-yjs.js";

const PDFJS_BASE =
    "https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/";

pdfjs.GlobalWorkerOptions.workerSrc =
    `${PDFJS_BASE}build/pdf.worker.mjs`;

const readerRoot = document.querySelector(
    "[data-notesv3-reader]"
);

if (readerRoot) {
    initializeReader(readerRoot).catch(error => {
        setStatus(
            readerRoot.querySelector("[data-reader-status]"),
            error.message,
            "error"
        );
    });
}

async function initializeReader(root) {
    const $ = selector => root.querySelector(selector);

    const versionId = Number(root.dataset.versionId);
    const shareId = root.dataset.shareId
        ? Number(root.dataset.shareId)
        : null;

    const isArchived = root.dataset.isArchived === "true";

    const readerStatus = $("[data-reader-status]");
    const noteStatus = $("[data-note-status]");
    const pageInput = $("[data-page-number]");
    const pageFrame = $("[data-pdf-page]");
    const scrollArea = $("[data-pdf-scroll]");
    const canvas = $("[data-pdf-canvas]");
    const textContainer = $("[data-pdf-text]");
    const annotationLayer = $("[data-annotation-layer]");
    const annotationList = $("[data-annotation-list]");
    const referenceList = $("[data-reference-list]");
    const sidebar = $("[data-sidebar]");
    const grid = $("[data-reader-grid]");

    const editor = new Quill($("[data-note-editor]"), {
        theme: "snow",
        placeholder: "Ghi chú cho trang PDF hiện tại…",
        formats: [
            "bold", "italic", "underline",
            "header", "list", "link"
        ],
        modules: {
            toolbar: [
                [{ header: [1, 2, false] }],
                ["bold", "italic", "underline"],
                [{ list: "ordered" }, { list: "bullet" }],
                ["link", "clean"]
            ],
            history: {
                userOnly: true
            }
        }
    });

    editor.disable();

    const Delta = Quill.import("delta");

    let currentPage = 0;
    let scale = 1;
    let note = null;
    let groupSession = null;

    let revision = 0;
    let savedRevision = 0;
    let savePromise = null;
    let saveTimer;

    let blocked = false;
    let busy = false;
    let viewReady = false;
    let permissionLost = false;

    let selectedPdfText = null;
    let annotationRows = [];
    let annotationLoadVersion = 0;
    let referenceLoadVersion = 0;
    let annotationPolling = false;
    let annotationTimer;

    const dirty = () =>
        !shareId && note && revision !== savedRevision;

    editor.root.addEventListener("paste", event => {
        const text = event.clipboardData?.getData("text/plain");

        if (text === undefined ||
            !viewReady ||
            permissionLost ||
            !editor.isEnabled()) return;

        event.preventDefault();
        event.stopImmediatePropagation();

        const range = editor.getSelection(true) || {
            index: Math.max(0, editor.getLength() - 1),
            length: 0
        };

        editor.updateContents(
            new Delta()
                .retain(range.index)
                .delete(range.length)
                .insert(text),
            "user"
        );

        editor.setSelection(
            range.index + text.length,
            0,
            "silent"
        );
    }, true);

    setStatus(readerStatus, "Đang mở PDF…");

    const loadingTask = pdfjs.getDocument({
        url: root.dataset.fileUrl,
        withCredentials: true,
        isEvalSupported: false,
        cMapUrl: `${PDFJS_BASE}cmaps/`,
        cMapPacked: true,
        standardFontDataUrl: `${PDFJS_BASE}standard_fonts/`,
        wasmUrl: `${PDFJS_BASE}wasm/`
    });

    const pdf = await loadingTask.promise;
    const expectedCount = Number(root.dataset.pageCount);

    if (expectedCount > 0 &&
        expectedCount !== pdf.numPages) {
        throw new Error(
            "Số trang PDF không khớp metadata. " +
            "Hãy kiểm tra lại tài liệu."
        );
    }

    pageInput.max = String(pdf.numPages);

    $("[data-total-pages]").textContent =
        `/ ${pdf.numPages}`;

    editor.on("text-change", (_delta, _old, source) => {
        if (source !== "user" ||
            shareId ||
            !note ||
            note.isReadOnly) return;

        revision++;
        setStatus(noteStatus, "Chưa lưu…");

        clearTimeout(saveTimer);

        if (!blocked) {
            saveTimer = setTimeout(() => {
                savePersonal().catch(() => {});
            }, 1000);
        }
    });

    async function savePersonal() {
        if (!dirty()) return;

        if (blocked) {
            throw new Error(
                "Không thể ghi đè ghi chú. " +
                "Sao chép bản nháp trước khi tải lại."
            );
        }

        if (savePromise) return savePromise;

        savePromise = (async () => {
            try {
                while (dirty()) {
                    const savingRevision = revision;

                    setStatus(noteStatus, "Đang lưu…");

                    // Dùng trang của note, không dùng trạng thái
                    // điều hướng có thể đã thay đổi.
                    const result = await api(
                        `pdfs/versions/${versionId}` +
                        `/pages/${note.physicalPageNumber}/note`,
                        {
                            method: "PUT",
                            body: {
                                deltaJson: JSON.stringify(
                                    editor.getContents()
                                ),
                                versionToken: note.versionToken
                            }
                        }
                    );

                    note.pageId = result.pageId;
                    note.versionToken = result.versionToken;
                    savedRevision = savingRevision;
                }

                setStatus(
                    noteStatus,
                    "Đã lưu",
                    "success"
                );
            } catch (error) {
                if ([400, 401, 403, 404, 409, 413]
                    .includes(error.status)) {
                    blocked = true;
                    editor.disable();
                }

                setStatus(
                    noteStatus,
                    error.message,
                    "error"
                );

                throw error;
            }
        })().finally(() => {
            savePromise = null;
        });

        return savePromise;
    }

    async function flushNote() {
        clearTimeout(saveTimer);

        if (groupSession) {
            await groupSession.flush();

            if (groupSession.hasPendingChanges) {
                throw new Error(
                    "Còn cập nhật chưa được xác nhận. " +
                    "Đồng bộ hoặc sao chép bản nháp trước."
                );
            }
        } else {
            await savePersonal();
        }
    }

    async function loadNote(pageNumber) {
        const query = shareId
            ? `?shareId=${shareId}`
            : "";

        let loaded;

        try {
            loaded = await api(
                `pdfs/versions/${versionId}` +
                `/pages/${pageNumber}/note${query}`
            );
        } catch (error) {
            handleAccessError(error);
            throw error;
        }

        // Giữ editor cũ cho tới khi đã kiểm tra được
        // quyền truy cập ghi chú tiếp theo.
        if (groupSession) {
            await groupSession.dispose();
            groupSession = null;
        }

        note = loaded;
        revision = 0;
        savedRevision = 0;
        blocked = false;

        editor.disable();
        editor.setContents([{ insert: "\n" }], "silent");
        editor.history.clear();

        $("[data-note-title]").textContent =
            `Ghi chú trang ${pageNumber}`;

        if (loaded.isGroup) {
            const sessionNote = loaded;

            groupSession = new GroupTextSession({
                editor,
                statusElement: noteStatus,
                pageId: loaded.pageId,
                pdfScope: {
                    documentVersionId: versionId,
                    shareId,
                    physicalPageNumber: pageNumber
                },
                onPageCreated: pageId => {
                    if (note === sessionNote) {
                        note.pageId = pageId;
                    }
                }
            });

            await groupSession.start();
        } else {
            editor.setContents(
                JSON.parse(loaded.deltaJson),
                "silent"
            );

            editor.history.clear();

            setStatus(
                noteStatus,
                loaded.isReadOnly ? "Chỉ xem" : "Đã tải"
            );
        }

        // perform() chỉ bật lại khi PDF và sidebar cùng trang.
        editor.disable();

        await loadReferences();
    }

    async function renderPage(pageNumber) {
        const page = await pdf.getPage(pageNumber);
        const viewport = page.getViewport({ scale });
        const pixelRatio = window.devicePixelRatio || 1;

        canvas.width = Math.floor(
            viewport.width * pixelRatio
        );

        canvas.height = Math.floor(
            viewport.height * pixelRatio
        );

        canvas.style.width = `${viewport.width}px`;
        canvas.style.height = `${viewport.height}px`;

        pageFrame.style.width = `${viewport.width}px`;
        pageFrame.style.height = `${viewport.height}px`;

        pageFrame.style.setProperty(
            "--scale-factor",
            String(scale)
        );

        pageFrame.style.setProperty(
            "--user-unit",
            String(page.userUnit || 1)
        );

        pageFrame.style.setProperty(
            "--total-scale-factor",
            String(scale * (page.userUnit || 1))
        );

        pageFrame.dataset.mainRotation =
            String(viewport.rotation);

        selectedPdfText = null;
        annotationLayer.replaceChildren();
        textContainer.replaceChildren();

        await page.render({
            canvasContext: canvas.getContext("2d"),
            viewport,
            transform: pixelRatio === 1
                ? null
                : [pixelRatio, 0, 0, pixelRatio, 0, 0]
        }).promise;

        const textLayer = new pdfjs.TextLayer({
            textContentSource: page.streamTextContent(),
            container: textContainer,
            viewport
        });

        await textLayer.render();
        await loadAnnotations(pageNumber);

        $("[data-zoom]").textContent =
            `${Math.round(scale * 100)}%`;
    }

    async function showPage(pageNumber) {
        if (!Number.isInteger(pageNumber) ||
            pageNumber < 1 ||
            pageNumber > pdf.numPages) {
            throw new Error("Số trang không hợp lệ.");
        }

        await flushNote();

        viewReady = false;
        currentPage = pageNumber;
        pageInput.value = String(pageNumber);

        await renderPage(pageNumber);
        await loadNote(pageNumber);

        viewReady = true;
        scrollArea.scrollTop = 0;

        const url = new URL(window.location.href);

        url.searchParams.set(
            "page",
            String(pageNumber)
        );

        window.history.replaceState(null, "", url);

        setStatus(readerStatus, "");
    }

    async function perform(action) {
        if (busy) return;

        busy = true;
        updateControls();
        editor.disable();

        try {
            await action();
        } catch (error) {
            pageInput.value = String(currentPage || 1);

            setStatus(
                readerStatus,
                error.message,
                "error"
            );
        } finally {
            busy = false;

            if (viewReady &&
                note &&
                note.physicalPageNumber === currentPage &&
                !note.isReadOnly &&
                !permissionLost &&
                !blocked &&
                !groupSession?.blocked) {
                editor.enable();
            }

            updateControls();
        }
    }

    function updateControls() {
        root.querySelectorAll("[data-reader-command]")
            .forEach(button => {
                const command = button.dataset.readerCommand;

                if (command === "copy-draft") {
                    button.disabled = !note;
                    return;
                }

                if (command === "reconnect") {
                    button.disabled =
                        busy ||
                        !groupSession ||
                        permissionLost;

                    return;
                }

                button.disabled =
                    busy ||
                    permissionLost ||
                    (!viewReady &&
                        !["previous", "next"].includes(command));

                if (command === "previous" &&
                    currentPage <= 1 &&
                    viewReady) {
                    button.disabled = true;
                }

                if (command === "next" &&
                    currentPage >= pdf.numPages &&
                    viewReady) {
                    button.disabled = true;
                }

                if (command === "save" &&
                    (isArchived ||
                     note?.isReadOnly ||
                     blocked ||
                     groupSession?.blocked)) {
                    button.disabled = true;
                }
            });

        const canMark =
            viewReady &&
            !busy &&
            !permissionLost &&
            !isArchived;

        root.querySelectorAll("[data-markup-command]")
            .forEach(button => {
                button.disabled =
                    !canMark ||
                    !selectedPdfText ||
                    selectedPdfText.pageNumber !== currentPage;
            });

        root.querySelectorAll("[data-annotation-action]")
            .forEach(button => {
                button.disabled = !canMark;
            });

        $("[data-markup-color]").disabled = !canMark;

        pageInput.disabled = busy || permissionLost;

        $("[data-search-form] button").disabled =
            busy ||
            permissionLost ||
            !viewReady;
    }

    function handleAccessError(error) {
        if (![401, 403, 404].includes(error.status)) {
            return;
        }

        permissionLost = true;
        blocked = true;
        editor.disable();

        if (groupSession) {
            groupSession.blocked = true;
        }

        updateControls();
    }

    function rememberPdfSelection() {
        if (!viewReady || busy || permissionLost) return;

        const selection = window.getSelection();

        if (!selection || selection.rangeCount === 0) {
            return;
        }

        const range = selection.getRangeAt(0);

        if (!textContainer.contains(
                range.commonAncestorContainer)) {
            return;
        }

        const text = selection.toString().trim();

        if (!text) {
            selectedPdfText = null;
            updateControls();
            return;
        }

        const bounds = pageFrame.getBoundingClientRect();
        const selected = range.getBoundingClientRect();

        if (bounds.width <= 0 ||
            bounds.height <= 0 ||
            selected.width <= 0 ||
            selected.height <= 0) {
            return;
        }

        const left = Math.max(bounds.left, selected.left);
        const top = Math.max(bounds.top, selected.top);
        const right = Math.min(bounds.right, selected.right);
        const bottom = Math.min(bounds.bottom, selected.bottom);

        if (right <= left || bottom <= top) return;

        const x = Math.max(
            0,
            Math.min(1, (left - bounds.left) / bounds.width)
        );

        const y = Math.max(
            0,
            Math.min(1, (top - bounds.top) / bounds.height)
        );

        const width = Math.min(
            1 - x,
            (right - left) / bounds.width
        );

        const height = Math.min(
            1 - y,
            (bottom - top) / bounds.height
        );

        if (width <= 0 || height <= 0) return;

        selectedPdfText = {
            pageNumber: currentPage,
            text,
            x,
            y,
            width,
            height
        };

        updateControls();
    }

    textContainer.addEventListener(
        "pointerup",
        rememberPdfSelection
    );

    textContainer.addEventListener(
        "keyup",
        rememberPdfSelection
    );

    async function loadAnnotations(pageNumber) {
        const generation = ++annotationLoadVersion;

        const query = new URLSearchParams({
            versionId: String(versionId),
            pageNumber: String(pageNumber)
        });

        if (shareId) {
            query.set("shareId", String(shareId));
        }

        let rows;

        try {
            rows = await api(`annotations?${query}`);
        } catch (error) {
            handleAccessError(error);
            throw error;
        }

        if (generation !== annotationLoadVersion ||
            pageNumber !== currentPage) {
            return;
        }

        const changed =
            JSON.stringify(rows) !==
            JSON.stringify(annotationRows);

        annotationRows = rows;

        if (changed ||
            annotationLayer.childElementCount !== rows.length ||
            annotationList.childElementCount === 0) {
            renderAnnotations();
        }
    }

    function renderAnnotations() {
        annotationLayer.replaceChildren();
        annotationList.replaceChildren();

        if (annotationRows.length === 0) {
            const message = document.createElement("p");

            message.className = "notesv3-meta";
            message.textContent = "Chưa có đánh dấu.";

            annotationList.append(message);
            updateControls();
            return;
        }

        for (const annotation of annotationRows) {
            const payload = annotation.payload;
            const rectangle = document.createElement("div");

            rectangle.className =
                "notesv3-annotation-rectangle";

            rectangle.style.left = `${payload.x * 100}%`;
            rectangle.style.top = `${payload.y * 100}%`;
            rectangle.style.width = `${payload.width * 100}%`;
            rectangle.style.height = `${payload.height * 100}%`;

            if (payload.kind === "Highlight") {
                rectangle.classList.add("is-highlight");
                rectangle.style.backgroundColor = payload.color;
            } else {
                rectangle.classList.add("is-comment");
                rectangle.style.borderColor = payload.color;
            }

            annotationLayer.append(rectangle);

            const row = document.createElement("div");
            row.className = "notesv3-annotation-item";

            const label = document.createElement("p");

            label.textContent = payload.kind === "Comment"
                ? payload.text
                : "Highlight";

            const actions = document.createElement("div");
            actions.className = "notesv3-button-row";

            for (const [action, title] of [
                ["edit", "Sửa"],
                ["delete", "Xóa"]
            ]) {
                const button = document.createElement("button");

                button.type = "button";
                button.className = action === "delete"
                    ? "btn btn-sm btn-outline-danger"
                    : "btn btn-sm btn-outline-secondary";

                button.textContent = title;
                button.dataset.annotationAction = action;
                button.dataset.annotationId =
                    String(annotation.annotationId);

                actions.append(button);
            }

            row.append(label, actions);
            annotationList.append(row);
        }

        updateControls();
    }

    async function captureSelection(selected) {
        const text = selected.text.trim();

        if (!text || text.length > 4000) {
            throw new Error(
                "Đoạn capture phải có từ 1 đến 4000 ký tự."
            );
        }

        if (!note ||
            note.physicalPageNumber !== currentPage) {
            throw new Error(
                "Ghi chú chưa khớp trang PDF hiện tại."
            );
        }

        await flushNote();

        const source = {
            documentVersionId: versionId,
            physicalPageNumber: currentPage,
            groupDocumentShareId: shareId,
            capturedText: text,
            anchor: {
                x: selected.x,
                y: selected.y,
                width: selected.width,
                height: selected.height
            }
        };

        if (shareId) {
            if (!groupSession) {
                throw new Error(
                    "Editor nhóm chưa sẵn sàng."
                );
            }

            await groupSession.captureText(source);
            note.pageId = groupSession.pageId;
        } else {
            const nextDelta = editor.getContents().compose(
                new Delta()
                    .retain(Math.max(
                        0,
                        editor.getLength() - 1
                    ))
                    .insert(text + "\n")
            );

            // Không kích hoạt endpoint autosave thường.
            editor.setContents(nextDelta, "silent");

            revision++;
            const capturedRevision = revision;

            setStatus(noteStatus, "Đang lưu capture…");

            try {
                const result = await api(
                    `pdfs/versions/${versionId}` +
                    `/pages/${currentPage}/capture`,
                    {
                        method: "POST",
                        body: {
                            deltaJson: JSON.stringify(nextDelta),
                            versionToken: note.versionToken,
                            source
                        }
                    }
                );

                note.pageId = result.pageId;
                note.versionToken = result.versionToken;
                savedRevision = capturedRevision;

                setStatus(
                    noteStatus,
                    "Đã lưu",
                    "success"
                );
            } catch (error) {
                // Không tự retry capture cá nhân:
                // response có thể bị mất sau khi SQL commit.
                blocked = true;
                editor.disable();

                setStatus(
                    noteStatus,
                    error.message +
                    " Capture chưa được xác nhận. " +
                    "Sao chép bản nháp và tải lại trước khi chụp tiếp.",
                    "error"
                );

                throw error;
            }
        }

        selectedPdfText = null;

        await loadReferences();

        setStatus(
            readerStatus,
            "Đã lưu văn bản và tham chiếu nguồn.",
            "success"
        );
    }

    async function loadReferences() {
        const generation = ++referenceLoadVersion;
        const sourceNote = note;
        const sourcePageId = sourceNote?.pageId;

        let rows = [];

        if (sourcePageId) {
            try {
                rows = await api(
                    `references/page/${sourcePageId}`
                );
            } catch (error) {
                if (generation !== referenceLoadVersion ||
                    note !== sourceNote) {
                    return;
                }

                handleAccessError(error);
                throw error;
            }
        }

        if (generation !== referenceLoadVersion ||
            note !== sourceNote) {
            return;
        }

        referenceList.replaceChildren();

        if (rows.length === 0) {
            const message = document.createElement("p");

            message.className = "notesv3-meta";
            message.textContent = "Chưa có tham chiếu.";

            referenceList.append(message);
            return;
        }

        for (const reference of rows) {
            const button = document.createElement("button");

            button.type = "button";
            button.className =
                "btn btn-sm btn-outline-secondary " +
                "notesv3-reference-link";

            button.dataset.referenceId =
                String(reference.referenceId);

            const excerpt = reference.capturedText.length > 120
                ? reference.capturedText.slice(0, 120) + "…"
                : reference.capturedText;

            button.textContent =
                `Trang ${reference.physicalPageNumber} — ${excerpt}`;

            button.title =
                "Mở nguồn PDF sau khi kiểm tra quyền";

            referenceList.append(button);
        }
    }

    root.addEventListener("click", event => {
        const button = event.target.closest(
            "[data-reader-command]"
        );

        if (!button) return;

        const command = button.dataset.readerCommand;

        if (command === "copy-draft") {
            if (!navigator.clipboard?.writeText) {
                setStatus(
                    noteStatus,
                    "Hãy chọn văn bản và nhấn Ctrl+C.",
                    "error"
                );

                return;
            }

            navigator.clipboard.writeText(editor.getText())
                .then(() => setStatus(
                    noteStatus,
                    "Đã sao chép văn bản bản nháp.",
                    "success"
                ))
                .catch(() => setStatus(
                    noteStatus,
                    "Hãy chọn văn bản trong editor và nhấn Ctrl+C.",
                    "error"
                ));

            return;
        }

        if (command === "reconnect") {
            groupSession?.reconnect()
                .catch(error => setStatus(
                    noteStatus,
                    error.message,
                    "error"
                ));

            return;
        }

        void perform(async () => {
            if (permissionLost) {
                throw new Error(
                    "Không còn quyền truy cập. " +
                    "Hãy sao chép bản nháp."
                );
            }

            if (!viewReady &&
                ["zoom-in", "zoom-out", "fit", "sidebar"]
                    .includes(command)) {
                throw new Error(
                    "Hãy tải thành công một trang PDF trước."
                );
            }

            if (command === "previous") {
                await showPage(
                    Math.max(1, currentPage - 1)
                );
            } else if (command === "next") {
                await showPage(
                    Math.min(pdf.numPages, currentPage + 1)
                );
            } else if (command === "zoom-in") {
                scale = Math.min(3, scale + 0.2);
                await renderPage(currentPage);
            } else if (command === "zoom-out") {
                scale = Math.max(0.4, scale - 0.2);
                await renderPage(currentPage);
            } else if (command === "fit") {
                const page = await pdf.getPage(currentPage);
                const viewport = page.getViewport({
                    scale: 1
                });

                scale = Math.max(
                    0.4,
                    Math.min(
                        3,
                        (scrollArea.clientWidth - 32) /
                        viewport.width
                    )
                );

                await renderPage(currentPage);
            } else if (command === "sidebar") {
                await flushNote();

                sidebar.hidden = !sidebar.hidden;

                grid.classList.toggle(
                    "notes-hidden",
                    sidebar.hidden
                );
            } else if (command === "save") {
                await flushNote();
            }
        });
    });

    root.addEventListener("click", event => {
        const button = event.target.closest(
            "[data-markup-command]"
        );

        if (!button) return;

        const selected = selectedPdfText
            ? { ...selectedPdfText }
            : null;

        void perform(async () => {
            if (isArchived ||
                permissionLost ||
                !viewReady) {
                throw new Error(
                    "PDF hiện không cho phép thêm đánh dấu."
                );
            }

            if (!selected ||
                selected.pageNumber !== currentPage) {
                throw new Error(
                    "Hãy chọn chữ trên trang PDF hiện tại."
                );
            }

            if (button.dataset.markupCommand === "capture") {
                await captureSelection(selected);
                return;
            }

            const kind =
                button.dataset.markupCommand === "comment"
                    ? "Comment"
                    : "Highlight";

            let text = null;

            if (kind === "Comment") {
                text = window.prompt("Nội dung bình luận:");

                if (text === null) return;

                text = text.trim();

                if (!text || text.length > 2000) {
                    throw new Error(
                        "Bình luận phải có từ 1 đến 2000 ký tự."
                    );
                }
            }

            await api("annotations", {
                method: "POST",
                body: {
                    documentVersionId: versionId,
                    physicalPageNumber: currentPage,
                    groupDocumentShareId: shareId,
                    payload: {
                        kind,
                        x: selected.x,
                        y: selected.y,
                        width: selected.width,
                        height: selected.height,
                        text,
                        color: $("[data-markup-color]").value
                    }
                }
            });

            selectedPdfText = null;
            await loadAnnotations(currentPage);

            setStatus(
                readerStatus,
                "Đã lưu đánh dấu.",
                "success"
            );
        });
    });

    annotationList.addEventListener("click", event => {
        const button = event.target.closest(
            "[data-annotation-action]"
        );

        if (!button) return;

        void perform(async () => {
            if (isArchived ||
                permissionLost ||
                !viewReady) {
                throw new Error(
                    "PDF hiện không cho phép sửa đánh dấu."
                );
            }

            const id = Number(button.dataset.annotationId);

            const annotation = annotationRows.find(
                item => item.annotationId === id
            );

            if (!annotation) {
                throw new Error(
                    "Hãy tải lại danh sách đánh dấu."
                );
            }

            if (button.dataset.annotationAction === "delete") {
                if (!window.confirm("Xóa đánh dấu này?")) {
                    return;
                }

                await api(`annotations/${id}`, {
                    method: "DELETE",
                    body: {
                        versionToken: annotation.versionToken
                    }
                });
            } else {
                const payload = {
                    ...annotation.payload
                };

                if (payload.kind === "Comment") {
                    const text = window.prompt(
                        "Nội dung bình luận:",
                        payload.text || ""
                    );

                    if (text === null) return;

                    payload.text = text.trim();

                    if (!payload.text ||
                        payload.text.length > 2000) {
                        throw new Error(
                            "Bình luận phải có từ 1 đến 2000 ký tự."
                        );
                    }
                } else {
                    const color = window.prompt(
                        "Màu highlight dạng #RRGGBB:",
                        payload.color
                    );

                    if (color === null) return;

                    if (!/^#[0-9a-f]{6}$/i.test(color)) {
                        throw new Error(
                            "Màu phải có dạng #RRGGBB."
                        );
                    }

                    payload.color = color;
                }

                await api(`annotations/${id}`, {
                    method: "PUT",
                    body: {
                        payload,
                        versionToken: annotation.versionToken
                    }
                });
            }

            await loadAnnotations(currentPage);

            setStatus(
                readerStatus,
                "Đã cập nhật đánh dấu.",
                "success"
            );
        });
    });

    referenceList.addEventListener("click", event => {
        const button = event.target.closest(
            "[data-reference-id]"
        );

        if (!button) return;

        void perform(async () => {
            await flushNote();

            const destination = await api(
                `references/${button.dataset.referenceId}/source`
            );

            if (!destination.available) {
                setStatus(
                    readerStatus,
                    "Source unavailable — nguồn không còn khả dụng.",
                    "error"
                );

                return;
            }

            if (!destination.documentId ||
                !destination.physicalPageNumber) {
                throw new Error(
                    "Nguồn tham chiếu chưa có đủ metadata."
                );
            }

            const query = new URLSearchParams({
                page: String(destination.physicalPageNumber)
            });

            if (destination.groupDocumentShareId) {
                query.set(
                    "shareId",
                    String(destination.groupDocumentShareId)
                );
            }

            window.location.assign(
                appUrl(
                    `NotesV3/Pdf/${destination.documentId}?${query}`
                )
            );
        });
    });

    pageInput.addEventListener("change", () => {
        const requestedPage = Number(pageInput.value);

        void perform(() => showPage(requestedPage));
    });

    $("[data-search-form]").addEventListener("submit", event => {
        event.preventDefault();

        const term = $("[data-search]").value
            .trim()
            .toLocaleLowerCase();

        if (!term) return;

        void perform(async () => {
            if (permissionLost || !viewReady) {
                throw new Error(
                    "Trang PDF chưa sẵn sàng hoặc không còn quyền truy cập."
                );
            }

            await flushNote();

            const searchStatus = $("[data-search-status]");
            searchStatus.textContent = "Đang tìm…";

            for (let offset = 1;
                 offset <= pdf.numPages;
                 offset++) {
                const number =
                    ((currentPage - 1 + offset) %
                        pdf.numPages) + 1;

                const page = await pdf.getPage(number);
                const content = await page.getTextContent();

                const text = content.items
                    .filter(item =>
                        typeof item.str === "string")
                    .map(item => item.str)
                    .join(" ")
                    .toLocaleLowerCase();

                if (text.includes(term)) {
                    await showPage(number);

                    searchStatus.textContent =
                        `Tìm thấy ở trang ${number}.`;

                    return;
                }
            }

            searchStatus.textContent =
                "Không tìm thấy văn bản. " +
                "PDF scan có thể không có lớp chữ.";
        });
    });

    root.addEventListener("click", event => {
        const link = event.target.closest(
            "[data-leave-reader]"
        );

        if (!link) return;

        event.preventDefault();

        void perform(async () => {
            await flushNote();
            window.location.assign(link.href);
        });
    });

    window.addEventListener("beforeunload", event => {
        if (!dirty() &&
            !groupSession?.hasPendingChanges) {
            return;
        }

        event.preventDefault();
        event.returnValue = "";
    });

    root.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) &&
            event.key.toLowerCase() === "s") {
            event.preventDefault();

            void perform(() => flushNote());
        }
    });

    annotationTimer = window.setInterval(async () => {
        if (busy ||
            !viewReady ||
            permissionLost ||
            annotationPolling) {
            return;
        }

        annotationPolling = true;

        try {
            await loadAnnotations(currentPage);
            await loadReferences();
        } catch (error) {
            setStatus(
                readerStatus,
                error.message,
                "error"
            );
        } finally {
            annotationPolling = false;
        }
    }, 5000);

    window.addEventListener("pagehide", () => {
        window.clearInterval(annotationTimer);
        window.clearTimeout(saveTimer);
    });

    updateControls();

    await perform(() => showPage(
        Number(root.dataset.initialPage) || 1
    ));
}

// Notes Book dùng chung file module này.
const bookRoot = document.querySelector(
    "[data-notesv3-book]"
);

if (bookRoot) {
    initializeBook(bookRoot).catch(error => {
        bookRoot.querySelector("[data-book-content]")
            .replaceChildren();

        bookRoot.querySelector("[data-book-empty]")
            .hidden = true;

        setStatus(
            bookRoot.querySelector("[data-book-status]"),
            error.message,
            "error"
        );
    });
}

async function initializeBook(root) {
    const status = root.querySelector("[data-book-status]");
    const emptyMessage = root.querySelector("[data-book-empty]");

    const entries = [
        ...root.querySelectorAll("[data-book-entry]")
    ];

    root.querySelector("[data-refresh-book]")
        .addEventListener("click", () => {
            window.location.reload();
        });

    let visibleCount = 0;

    for (let index = 0; index < entries.length; index++) {
        const entry = entries[index];
        const pageNumber = entry.dataset.pageNumber;

        setStatus(
            status,
            `Đang dựng trang ${pageNumber} ` +
            `(${index + 1}/${entries.length})…`
        );

        const delta =
            entry.dataset.requiresReplay === "true"
                ? await readGroupPageDelta(entry.dataset.pageId)
                : JSON.parse(
                    entry.querySelector("[data-book-delta]").value
                );

        const safeDelta = bookTextDelta(delta);

        const text = safeDelta.ops
            .map(operation => operation.insert)
            .join("");

        if (!text.trim()) continue;

        const viewer = new Quill(
            entry.querySelector("[data-book-editor]"),
            {
                theme: "snow",
                readOnly: true,
                formats: [
                    "bold", "italic", "underline",
                    "header", "list", "link"
                ],
                modules: {
                    toolbar: false
                }
            }
        );

        viewer.setContents(safeDelta, "silent");
        viewer.disable();

        entry.hidden = false;
        visibleCount++;
    }

    emptyMessage.hidden = visibleCount !== 0;

    setStatus(
        status,
        visibleCount === 0
            ? "Không có ghi chú chứa văn bản."
            : `${visibleCount} trang có ghi chú.`,
        visibleCount === 0 ? "info" : "success"
    );
}

async function readGroupPageDelta(pageId) {
    const document = new Y.Doc();
    let cursor = 0;

    try {
        let hasMore;

        do {
            const batch = await api(
                `pages/${pageId}/updates?after=${cursor}`
            );

            for (const update of batch.updates) {
                Y.applyUpdate(
                    document,
                    fromBase64(update.updateBytes)
                );

                cursor = update.updateId;
            }

            hasMore = batch.hasMore;
        } while (hasMore);

        return {
            ops: document.getText("quill").toDelta()
        };
    } finally {
        document.destroy();
    }
}

function bookTextDelta(delta) {
    if (!Array.isArray(delta?.ops)) {
        throw new Error(
            "Nội dung ghi chú không hợp lệ."
        );
    }

    const ops = [];

    for (const operation of delta.ops) {
        if (typeof operation.insert !== "string") {
            continue;
        }

        const attributes = {};
        const source = operation.attributes || {};

        for (const name of [
            "bold",
            "italic",
            "underline"
        ]) {
            if (source[name] === true) {
                attributes[name] = true;
            }
        }

        if (Number.isInteger(source.header) &&
            source.header >= 1 &&
            source.header <= 6) {
            attributes.header = source.header;
        }

        if (["ordered", "bullet"].includes(source.list)) {
            attributes.list = source.list;
        }

        if (typeof source.link === "string") {
            try {
                const link = new URL(source.link);

                if (["http:", "https:", "mailto:"]
                    .includes(link.protocol)) {
                    attributes.link = link.href;
                }
            } catch {
                // Giữ chữ, bỏ link không hợp lệ.
            }
        }

        const item = {
            insert: operation.insert
        };

        if (Object.keys(attributes).length > 0) {
            item.attributes = attributes;
        }

        ops.push(item);
    }

    const text = ops
        .map(item => item.insert)
        .join("");

    if (!text.endsWith("\n")) {
        ops.push({ insert: "\n" });
    }

    return { ops };
}
