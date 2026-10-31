export class NotesApiError extends Error {
    constructor(message, status) {
        super(message);
        this.name = "NotesApiError";
        this.status = status;
    }
}

export function appUrl(path) {
    const root = document.querySelector("[data-notesv3]");
    const prefix = root?.dataset.appRoot || "/";

    return prefix + path.replace(/^\/+/, "");
}

export function verificationToken() {
    const token = document.querySelector(
        '[data-notesv3] input[name="__RequestVerificationToken"]'
    )?.value;

    if (!token) {
        throw new Error("Thiếu token xác thực. Hãy tải lại trang.");
    }

    return token;
}

export function setStatus(element, message, state = "info") {
    if (!element) return;

    element.textContent = message;
    element.dataset.state = state;
}

export async function api(path, {
    method = "GET",
    body,
    signal
} = {}) {
    method = method.toUpperCase();

    const headers = {
        Accept: "application/json"
    };

    if (!["GET", "HEAD", "OPTIONS"].includes(method)) {
        headers.RequestVerificationToken = verificationToken();
    }

    let requestBody;

    if (body instanceof FormData) {
        requestBody = body;
    } else if (body !== undefined) {
        headers["Content-Type"] = "application/json";
        requestBody = JSON.stringify(body);
    }

    const response = await fetch(
        appUrl(`api/notesv3/${path.replace(/^\/+/, "")}`),
        {
            method,
            headers,
            body: requestBody,
            credentials: "same-origin",
            cache: "no-store",
            signal
        }
    );

    if (response.redirected) {
        throw new NotesApiError(
            "Phiên đăng nhập đã thay đổi. Giữ bản nháp và tải lại trang.",
            401
        );
    }

    if (response.status === 204) {
        return null;
    }

    const contentType = response.headers.get("content-type") || "";
    let data = null;

    if (contentType.includes("json")) {
        data = await response.json();
    }

    if (!response.ok) {
        const validationMessages = data?.errors
            ? Object.values(data.errors).flat().join(" ")
            : "";

        throw new NotesApiError(
            validationMessages ||
            data?.title ||
            data?.message ||
            `Yêu cầu thất bại (${response.status}).`,
            response.status
        );
    }

    if (!contentType.includes("json")) {
        throw new NotesApiError(
            "Server trả dữ liệu không đúng định dạng.",
            response.status
        );
    }

    return data;
}

const library = document.querySelector("[data-notesv3-library]");

if (library) {
    initializeLibrary(library);
}

function initializeLibrary(root) {
    const status = root.querySelector("[data-status]");

    const createForm = root.querySelector("[data-create-notebook]");
    const uploadForm = root.querySelector("[data-upload-pdf]");

    createForm.addEventListener("submit", async event => {
        event.preventDefault();

        const button = createForm.querySelector('button[type="submit"]');
        button.disabled = true;

        try {
            setStatus(status, "Đang tạo notebook…");

            const title = new FormData(createForm)
                .get("title")
                ?.toString()
                .trim();

            const result = await api("notebooks", {
                method: "POST",
                body: {
                    title,
                    groupId: null
                }
            });

            window.location.assign(
                appUrl(`NotesV3/Notebook/${result.id}`)
            );
        } catch (error) {
            setStatus(status, error.message, "error");
        } finally {
            button.disabled = false;
        }
    });

    uploadForm.addEventListener("submit", async event => {
        event.preventDefault();

        const button = uploadForm.querySelector('button[type="submit"]');
        const formData = new FormData(uploadForm);
        const file = formData.get("File");

        if (!(file instanceof File) || file.size === 0) {
            setStatus(status, "Hãy chọn một file PDF.", "error");
            return;
        }

        if (file.size > 25 * 1024 * 1024) {
            setStatus(status, "PDF tối đa 25 MiB.", "error");
            return;
        }

        button.disabled = true;

        try {
            setStatus(status, "Đang tải lên và kiểm tra PDF…");

            const result = await api("pdfs", {
                method: "POST",
                body: formData
            });

            window.location.assign(
                appUrl(`NotesV3/Pdf/${result.documentId}`)
            );
        } catch (error) {
            setStatus(status, error.message, "error");
        } finally {
            button.disabled = false;
        }
    });

    root.addEventListener("click", async event => {
        if (!(event.target instanceof Element)) return;

        const button = event.target.closest("button[data-action]");
        if (!button || !root.contains(button)) return;

        const card = button.closest(
            "[data-notebook-id], [data-pdf-id]"
        );

        if (!card) return;

        button.disabled = true;

        try {
            const changed = await handleResourceAction(
                root,
                card,
                button
            );

            if (changed === "reload") {
                window.location.reload();
            } else if (changed === "shared") {
                setStatus(
                    status,
                    "Đã chia sẻ PDF vào nhóm.",
                    "success"
                );
            }
        } catch (error) {
            setStatus(status, error.message, "error");
        } finally {
            button.disabled = false;
        }
    });
}

async function handleResourceAction(root, card, button) {
    const action = button.dataset.action;
    const currentTitle = card.querySelector(
        "[data-resource-title]"
    ).textContent.trim();

    if (action === "rename-notebook") {
        const title = window.prompt("Tên notebook mới:", currentTitle);
        if (title === null) return;

        await api(`notebooks/${card.dataset.notebookId}/title`, {
            method: "PATCH",
            body: { title }
        });

        return "reload";
    }

    if (action === "delete-notebook") {
        if (!window.confirm(
            `Xóa notebook “${currentTitle}”? Notebook sẽ được ẩn cùng các trang bên trong.`
        )) return;

        await api(`notebooks/${card.dataset.notebookId}`, {
            method: "DELETE"
        });

        return "reload";
    }

    if (action === "rename-pdf") {
        const title = window.prompt("Tên PDF mới:", currentTitle);
        if (title === null) return;

        await api(`pdfs/${card.dataset.pdfId}/title`, {
            method: "PATCH",
            body: {
                title,
                versionToken: card.dataset.versionToken
            }
        });

        return "reload";
    }

    if (action === "archive-pdf") {
        const archived = button.dataset.archived !== "true";

        await api(`pdfs/${card.dataset.pdfId}/archive`, {
            method: "PUT",
            body: {
                archived,
                versionToken: card.dataset.versionToken
            }
        });

        return "reload";
    }

    if (action === "share-pdf") {
        const groupId = root.querySelector(
            "[data-share-group]"
        )?.value;

        if (!groupId) {
            throw new Error("Hãy chọn nhóm nhận PDF trước.");
        }

        await api(`groups/${groupId}/pdf-shares`, {
            method: "POST",
            body: {
                documentId: Number(card.dataset.pdfId)
            }
        });

        return "shared";
    }
}