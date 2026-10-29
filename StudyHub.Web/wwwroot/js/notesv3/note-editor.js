import Quill from "https://esm.sh/quill@2.0.3";
import {
    api,
    appUrl,
    setStatus
} from "./notes-ui.js";

import {
    GroupTextSession,
    createNotebookSeed
} from "./collab-yjs.js";

const root = document.querySelector("[data-notesv3-notebook]");

if (root) {
    initializeNotebook(root).catch(error => {
        setStatus(
            root.querySelector("[data-status]"),
            error.message,
            "error"
        );
    });
}

async function initializeNotebook(root) {
    const notebookId = root.dataset.notebookId;
    const isGroup = root.dataset.isGroup === "true";
    const status = root.querySelector("[data-status]");
    const sectionPicker = root.querySelector("[data-section]");
    const movePicker = root.querySelector("[data-move-section]");
    const pageList = root.querySelector("[data-page-list]");
    const pageTitle = root.querySelector("[data-page-title]");

    const editor = new Quill(root.querySelector("[data-editor]"), {
        theme: "snow",
        placeholder: "Viết ghi chú…",
        formats: [
            "bold", "italic", "underline",
            "header", "list", "link"
        ],
        modules: {
            toolbar: [
                [{ header: [1, 2, 3, false] }],
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

    // Paste plain text, không giữ HTML/format từ nguồn ngoài.
    editor.root.addEventListener("paste", event => {
        const text = event.clipboardData?.getData("text/plain");
        if (text === undefined) return;

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

    let details;
    let sectionId = null;
    let page = null;
    let groupSession = null;
    let revision = 0;
    let savedRevision = 0;
    let savePromise = null;
    let saveTimer;
    let personalBlocked = false;
    let working = false;

    const dirty = () =>
        !isGroup && page && revision !== savedRevision;

    editor.on("text-change", (_delta, _old, source) => {
        if (source !== "user" || !page || isGroup) return;

        revision++;
        setStatus(status, "Chưa lưu…");

        clearTimeout(saveTimer);

        if (!personalBlocked) {
            saveTimer = setTimeout(() => {
                savePersonal().catch(() => {});
            }, 1000);
        }
    });

    async function savePersonal() {
        if (!page || isGroup || !dirty()) return;

        if (personalBlocked) {
            throw new Error(
                "Không thể ghi đè bản mới trên server. " +
                "Hãy sao chép bản nháp trước khi tải lại."
            );
        }

        if (savePromise) return savePromise;

        savePromise = (async () => {
            try {
                while (dirty()) {
                    const savingRevision = revision;

                    const body = {
                        deltaJson: JSON.stringify(editor.getContents()),
                        versionToken: page.versionToken
                    };

                    setStatus(status, "Đang lưu…");

                    const result = await api(`pages/${page.id}`, {
                        method: "PUT",
                        body
                    });

                    page.versionToken = result.versionToken;
                    savedRevision = savingRevision;
                }

                setStatus(status, "Đã lưu", "success");
            } catch (error) {
                if ([400, 401, 403, 404, 409, 413].includes(error.status)) {
                    personalBlocked = true;
                }

                setStatus(status, error.message, "error");
                throw error;
            }
        })().finally(() => {
            savePromise = null;
        });

        return savePromise;
    }

    async function flushCurrent() {
        clearTimeout(saveTimer);

        if (groupSession) {
            await groupSession.flush();

            if (groupSession.hasPendingChanges) {
                throw new Error(
                    "Còn thay đổi chưa được ACK. " +
                    "Kết nối lại hoặc sao chép bản nháp trước."
                );
            }
        } else {
            await savePersonal();
        }
    }

    async function closeCurrent() {
        clearTimeout(saveTimer);

        if (groupSession) {
            await groupSession.dispose();
            groupSession = null;
        }

        page = null;
        revision = 0;
        savedRevision = 0;
        personalBlocked = false;

        editor.disable();
        editor.setContents([{ insert: "\n" }], "silent");
        editor.history.clear();
    }

    function renderStructure() {
        sectionPicker.replaceChildren();
        movePicker.replaceChildren();

        for (const section of details.sections) {
            sectionPicker.add(
                new Option(section.title, String(section.id))
            );

            movePicker.add(
                new Option(section.title, String(section.id))
            );
        }

        if (!details.sections.some(item => item.id === sectionId)) {
            sectionId = details.sections[0]?.id ?? null;
        }

        sectionPicker.value = sectionId ? String(sectionId) : "";

        if (sectionId) {
            movePicker.value = String(sectionId);
        }

        pageList.replaceChildren();

        for (const item of currentPages()) {
            const button = document.createElement("button");

            button.type = "button";
            button.className = "notesv3-page-button";
            button.dataset.pageId = String(item.id);
            button.textContent = item.title;
            button.classList.toggle("active", item.id === page?.id);
            button.setAttribute(
                "aria-pressed",
                String(item.id === page?.id)
            );

            pageList.append(button);
        }
    }

    function currentPages() {
        return details.pages.filter(
            item => item.sectionId === sectionId
        );
    }

    async function refresh() {
        details = await api(`notebooks/${notebookId}`);
        renderStructure();
    }

    async function openPage(id) {
        await closeCurrent();

        pageTitle.textContent = "Đang tải…";

        page = await api(`pages/${id}`);
        pageTitle.textContent = page.title;
        renderStructure();

        if (page.isGroup) {
            groupSession = new GroupTextSession({
                editor,
                statusElement: status,
                pageId: page.id
            });

            await groupSession.start();
        } else {
            editor.setContents(
                JSON.parse(page.deltaJson),
                "silent"
            );

            editor.history.clear();
            editor.enable();

            setStatus(status, "Đã tải", "success");
        }
    }

    async function perform(action) {
        if (working) return;

        working = true;
        editor.disable();

        try {
            await flushCurrent();
            await action();
        } catch (error) {
            setStatus(status, error.message, "error");
        } finally {
            working = false;

            if (page &&
                !personalBlocked &&
                !groupSession?.blocked) {
                editor.enable();
            }
        }
    }

    async function refreshPage(preferredPageId = page?.id) {
        await refresh();

        const next = currentPages().find(
            item => item.id === preferredPageId
        ) || currentPages()[0];

        if (next) {
            await openPage(next.id);
        } else {
            await closeCurrent();
            pageTitle.textContent = "Chọn hoặc tạo một trang";
            setStatus(status, "");
        }
    }

    sectionPicker.addEventListener("change", () => {
        const selected = Number(sectionPicker.value);

        void perform(async () => {
            sectionId = selected || null;
            await refreshPage(null);
        });
    });

    pageList.addEventListener("click", event => {
        const button = event.target.closest("[data-page-id]");
        if (!button) return;

        void perform(() => openPage(Number(button.dataset.pageId)));
    });

    // Lưu trước khi rời notebook bằng link.
    root.addEventListener("click", event => {
        const link = event.target.closest("a[href]");
        if (!link) return;

        event.preventDefault();

        void perform(async () => {
            window.location.assign(link.href);
        });
    });

    root.addEventListener("click", event => {
        const button = event.target.closest("[data-command]");
        if (!button) return;

        const command = button.dataset.command;

        if (command === "copy-draft") {
            navigator.clipboard.writeText(editor.getText())
                .then(() => setStatus(
                    status, "Đã sao chép văn bản bản nháp.", "success"
                ))
                .catch(() => setStatus(
                    status,
                    "Không truy cập được clipboard. " +
                    "Hãy chọn văn bản trong editor và nhấn Ctrl+C.",
                    "error"
                ));
            return;
        }

        if (command === "reconnect") {
            if (groupSession) {
                groupSession.reconnect()
                    .catch(error => setStatus(
                        status, error.message, "error"
                    ));
            }
            return;
        }

        void perform(() => executeCommand(command));
    });

    async function executeCommand(command) {
        const section = details.sections.find(
            item => item.id === sectionId
        );

        if (command === "save") return;

        if (command === "create-section") {
            const title = window.prompt("Tên section:");
            if (title === null) return;

            const result = await api(
                `notebooks/${notebookId}/sections`,
                { method: "POST", body: { title } }
            );

            sectionId = result.id;
            await refreshPage(null);
            return;
        }

        if (command === "rename-section") {
            if (!section) throw new Error("Hãy chọn section.");

            const title = window.prompt("Tên section:", section.title);
            if (title === null) return;

            await api(`sections/${section.id}/title`, {
                method: "PATCH",
                body: { title }
            });

            await refresh();
            return;
        }

        if (command === "delete-section") {
            if (!section) throw new Error("Hãy chọn section.");

            if (!window.confirm(
                `Xóa section “${section.title}” và ẩn các trang bên trong?`
            )) return;

            await api(`sections/${section.id}`, { method: "DELETE" });
            await refreshPage(null);
            return;
        }

        if (command === "create-page") {
            if (!section) throw new Error("Hãy tạo hoặc chọn section.");

            const title = window.prompt("Tên trang:");
            if (title === null) return;

            const result = await api(`sections/${section.id}/pages`, {
                method: "POST",
                body: { title }
            });

            await refreshPage(result.id);
            return;
        }

        if (command === "load-trash") {
            const deleted = await api(`notebooks/${notebookId}/trash`);
            const picker = root.querySelector("[data-trash]");

            picker.replaceChildren();

            for (const item of deleted) {
                const option = new Option(item.title, String(item.id));
                option.dataset.versionToken = item.versionToken;
                picker.add(option);
            }

            root.querySelector("[data-trash-panel]").hidden = false;

            setStatus(
                status,
                deleted.length ? "Chọn trang để khôi phục." : "Thùng rác trống."
            );

            return;
        }

        if (command === "restore-page") {
            const selected = root.querySelector("[data-trash]")
                .selectedOptions[0];

            if (!selected) throw new Error("Hãy chọn trang đã xóa.");

            await api(`pages/${selected.value}/restore`, {
                method: "POST",
                body: { versionToken: selected.dataset.versionToken }
            });

            root.querySelector("[data-trash-panel]").hidden = true;
            await refreshPage();
            return;
        }

        if (command === "copy-notebook") {
            await copyNotebook();
            return;
        }

        if (command.startsWith("section-")) {
            if (!section) throw new Error("Hãy chọn section.");

            const ids = reorderedIds(
                details.sections,
                section.id,
                command.endsWith("up") ? -1 : 1
            );

            await api(`notebooks/${notebookId}/sections/order`, {
                method: "PUT",
                body: { orderedIds: ids }
            });

            await refresh();
            return;
        }

        if (!page) throw new Error("Hãy chọn một trang.");

        if (command === "rename-page") {
            const title = window.prompt("Tên trang:", page.title);
            if (title === null) return;

            await api(`pages/${page.id}/title`, {
                method: "PATCH",
                body: {
                    title,
                    versionToken: page.versionToken
                }
            });

            await refreshPage();
            return;
        }

        if (command === "delete-page") {
            if (!window.confirm(`Xóa trang “${page.title}”?`)) return;

            await api(`pages/${page.id}`, {
                method: "DELETE",
                body: { versionToken: page.versionToken }
            });

            await refreshPage(null);
            return;
        }

        if (command === "move-page") {
            const targetSectionId = Number(movePicker.value);
            if (!targetSectionId) throw new Error("Hãy chọn section đích.");

            const id = page.id;

            await api(`pages/${id}/move`, {
                method: "POST",
                body: {
                    targetSectionId,
                    versionToken: page.versionToken
                }
            });

            sectionId = targetSectionId;
            await refreshPage(id);
            return;
        }

        if (command === "page-up" || command === "page-down") {
            const ids = reorderedIds(
                currentPages(),
                page.id,
                command.endsWith("up") ? -1 : 1
            );

            await api(`sections/${sectionId}/pages/order`, {
                method: "PUT",
                body: { orderedIds: ids }
            });

            await refreshPage();
        }
    }

    async function copyNotebook() {
        const groupId = Number(
            root.querySelector("[data-copy-group]")?.value
        );

        if (!groupId) throw new Error("Hãy chọn nhóm nhận.");

        setStatus(status, "Đang chuẩn bị bản chép…");

        const source = await api(`notebooks/${notebookId}`);
        const seeds = [];

        for (const item of source.pages) {
            const sourcePage = await api(`pages/${item.id}`);

            seeds.push({
                sourcePageId: sourcePage.id,
                sourceVersionToken: sourcePage.versionToken,
                initialUpdate: createNotebookSeed(sourcePage.deltaJson)
            });
        }

        const result = await api(
            `groups/${groupId}/notebook-copies`,
            {
                method: "POST",
                body: {
                    sourceNotebookId: Number(notebookId),
                    seeds
                }
            }
        );

        window.location.assign(
            appUrl(`NotesV3/Notebook/${result.notebookId}`)
        );
    }

    window.addEventListener("beforeunload", event => {
        if (!dirty() && !groupSession?.hasPendingChanges) return;

        event.preventDefault();
        event.returnValue = "";
    });

    root.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) &&
            event.key.toLowerCase() === "s") {
            event.preventDefault();
            void perform(async () => {});
        }
    });

    await refresh();
    await refreshPage(null);
}

function reorderedIds(items, selectedId, direction) {
    const ids = items.map(item => item.id);
    const index = ids.indexOf(selectedId);
    const target = index + direction;

    if (index >= 0 && target >= 0 && target < ids.length) {
        [ids[index], ids[target]] = [ids[target], ids[index]];
    }

    return ids;
}
