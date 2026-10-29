import * as Y from "https://esm.sh/yjs@13.6.27";
import {
    QuillBinding
} from "https://esm.sh/y-quill@1.0.0?deps=yjs@13.6.27,quill@2.0.3";

import {
    HubConnectionBuilder,
    HubConnectionState,
    LogLevel
} from "https://esm.sh/@microsoft/signalr@9.0.19";

import {
    api,
    appUrl,
    verificationToken,
    setStatus
} from "./notes-ui.js";

const REMOTE = Symbol("notesv3-remote");
const MAX_UPDATE_BYTES = 256 * 1024;

export function toBase64(bytes) {
    let binary = "";

    for (let offset = 0; offset < bytes.length; offset += 32768) {
        binary += String.fromCharCode(
            ...bytes.subarray(offset, offset + 32768)
        );
    }

    return btoa(binary);
}

export function fromBase64(value) {
    const binary = atob(value);
    const bytes = new Uint8Array(binary.length);

    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }

    return bytes;
}

function operationId() {
    if (globalThis.crypto.randomUUID) {
        return globalThis.crypto.randomUUID();
    }

    const bytes = globalThis.crypto.getRandomValues(
        new Uint8Array(16)
    );

    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;

    const hex = [...bytes]
        .map(value => value.toString(16).padStart(2, "0"))
        .join("");

    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-` +
        `${hex.slice(12, 16)}-${hex.slice(16, 20)}-` +
        hex.slice(20);
}

export function createNotebookSeed(deltaJson) {
    const document = new Y.Doc();

    try {
        document.getText("quill")
            .applyDelta(JSON.parse(deltaJson).ops);

        const update = Y.encodeStateAsUpdate(document);

        if (update.length > MAX_UPDATE_BYTES) {
            throw new Error(
                "Một trang vượt giới hạn seed 256 KiB."
            );
        }

        return toBase64(update);
    } finally {
        document.destroy();
    }
}

export class GroupTextSession {
    constructor({
        editor,
        statusElement,
        pageId = null,
        pdfScope = null,
        onPageCreated = () => {}
    }) {
        if (!pageId && !pdfScope) {
            throw new Error(
                "Thiếu trang hoặc scope PDF nhóm."
            );
        }

        this.editor = editor;
        this.statusElement = statusElement;
        this.pageId = pageId;
        this.pdfScope = pdfScope;
        this.onPageCreated = onPageCreated;

        this.document = new Y.Doc();
        this.binding = null;
        this.cursor = 0;
        this.pending = [];

        this.ready = false;
        this.disposed = false;
        this.blocked = false;
        this.flushing = false;

        this.flushPromise = null;
        this.replayPromise = null;
        this.replayAgain = false;

        this.lastError = "";
        this.captureReference = null;

        this.connection = new HubConnectionBuilder()
            .withUrl(appUrl("hubs/notesv3"))
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .configureLogging(LogLevel.Warning)
            .build();

        this.connection.on("PageChanged", message => {
            if (this.disposed || this.blocked) return;

            const relevant = this.pageId
                ? message.pageId === this.pageId
                : Boolean(this.pdfScope);

            if (relevant) {
                this.synchronize()
                    .catch(error => this.handleError(error));
            }
        });

        this.connection.onreconnecting(() => {
            this.lastError = "Mất kết nối — chưa đồng bộ.";
            this.renderStatus();
        });

        this.connection.onreconnected(() => {
            this.resume()
                .catch(error => this.handleError(error));
        });

        this.connection.onclose(() => {
            this.lastError =
                "Mất kết nối. Nhấn kết nối lại trước khi rời trang.";

            this.renderStatus();
        });

        this.beforeUnload = event => {
            if (!this.hasPendingChanges) return;

            event.preventDefault();
            event.returnValue = "";
        };
    }

    get hasPendingChanges() {
        return this.pending.length > 0;
    }

    get connected() {
        return this.connection.state ===
            HubConnectionState.Connected;
    }

    async start() {
        this.editor.disable();
        setStatus(this.statusElement, "Đang tải ghi chú nhóm…");

        try {
            await this.connection.start();
            await this.joinRooms();
            await this.synchronize();

            if (this.disposed || this.blocked) return;

            this.editor.setContents(
                [{ insert: "\n" }],
                "silent"
            );

            this.binding = new QuillBinding(
                this.document.getText("quill"),
                this.editor
            );

            this.onDocumentUpdate = (update, origin) => {
                if (origin !== REMOTE && !this.disposed) {
                    this.queueUpdate(update);
                }
            };

            this.document.on(
                "update",
                this.onDocumentUpdate
            );

            this.ready = true;
            this.editor.enable();

            window.addEventListener(
                "beforeunload",
                this.beforeUnload
            );

            this.pollTimer = window.setInterval(() => {
                if (!this.connected ||
                    this.blocked ||
                    this.disposed) return;

                this.synchronize()
                    .then(() => this.flush())
                    .catch(error => this.handleError(error));
            }, 5000);

            this.renderStatus();
        } catch (error) {
            this.blocked = true;

            await this.connection.stop().catch(() => {});

            this.handleError(error);
            throw error;
        }
    }

    async joinRooms() {
        if (this.pdfScope) {
            await this.connection.invoke(
                "JoinPdfPage",
                this.pdfScope.documentVersionId,
                this.pdfScope.shareId,
                this.pdfScope.physicalPageNumber
            );
        }

        if (this.pageId) {
            await this.connection.invoke(
                "JoinPage",
                this.pageId
            );
        }
    }

    async resume() {
        if (this.disposed || this.blocked) return;

        await this.joinRooms();
        await this.synchronize();

        this.lastError = "";
        this.renderStatus();

        await this.flush();
    }

    async reconnect() {
        if (this.disposed || this.blocked) return;

        if (this.connection.state ===
            HubConnectionState.Disconnected) {
            await this.connection.start();
        }

        if (this.connected) {
            await this.resume();
        }
    }

    async captureText(reference) {
        if (!this.ready ||
            this.disposed ||
            this.blocked) {
            throw new Error(
                "Editor nhóm chưa sẵn sàng hoặc không còn quyền sửa."
            );
        }

        const text = reference?.capturedText?.trim();

        if (!text || text.length > 4000) {
            throw new Error(
                "Đoạn capture phải có từ 1 đến 4000 ký tự."
            );
        }

        this.captureReference = {
            ...reference,
            capturedText: text
        };

        const wasEnabled = this.editor.isEnabled();

        try {
            // Toolbar tạm disable editor.
            // Quill bỏ qua API source="user" khi editor bị disable.
            this.editor.enable();

            this.editor.insertText(
                Math.max(0, this.editor.getLength() - 1),
                text + "\n",
                "user"
            );
        } finally {
            this.captureReference = null;

            if (!wasEnabled || this.blocked) {
                this.editor.disable();
            }
        }

        await this.flush();

        if (this.hasPendingChanges) {
            throw new Error(
                "Capture còn chưa được ACK. Không chụp lại; " +
                "hãy đồng bộ cập nhật đang chờ."
            );
        }
    }

    queueUpdate(update) {
        if (!this.pageId &&
            !this.editor.getText().trim() &&
            this.pending.length === 0) {
            this.renderStatus();
            return;
        }

        const bytes = this.pageId
            ? update
            : Y.encodeStateAsUpdate(this.document);

        this.pending.push({
            operationId: operationId(),
            updateBytes: toBase64(bytes),
            initialDeltaJson: JSON.stringify(
                this.editor.getContents()
            ),
            reference: this.captureReference
                ? { ...this.captureReference }
                : null
        });

        if (bytes.length > MAX_UPDATE_BYTES) {
            this.blocked = true;
            this.lastError =
                "Cập nhật vượt 256 KiB. " +
                "Sao chép bản nháp trước khi rời trang.";

            this.editor.disable();
            this.renderStatus();
            return;
        }

        this.renderStatus();
        void this.flush();
    }

    async flush() {
        if (this.flushPromise) {
            return this.flushPromise;
        }

        if (!this.ready ||
            this.disposed ||
            this.blocked ||
            !this.connected) return;

        this.flushPromise = this.flushPending()
            .finally(() => {
                this.flushPromise = null;
            });

        return this.flushPromise;
    }

    async flushPending() {
        this.flushing = true;

        try {
            while (this.pending.length > 0 &&
                   this.connected &&
                   !this.blocked &&
                   !this.disposed) {
                const operation = this.pending[0];
                let ack;

                if (this.pageId) {
                    ack = await this.connection.invoke(
                        "AppendUpdate",
                        {
                            pageId: this.pageId,
                            operationId: operation.operationId,
                            updateBytes: operation.updateBytes,
                            reference: operation.reference
                        },
                        verificationToken()
                    );
                } else {
                    ack = await this.connection.invoke(
                        "AppendPdfPageUpdate",
                        {
                            documentVersionId:
                                this.pdfScope.documentVersionId,
                            groupDocumentShareId:
                                this.pdfScope.shareId,
                            physicalPageNumber:
                                this.pdfScope.physicalPageNumber,
                            operationId:
                                operation.operationId,
                            updateBytes:
                                operation.updateBytes,
                            initialDeltaJson:
                                operation.initialDeltaJson,
                            reference:
                                operation.reference
                        },
                        verificationToken()
                    );
                }

                if (!ack) {
                    throw new Error(
                        "Server chưa xác nhận lưu ghi chú."
                    );
                }

                this.pending.shift();

                if (!this.pageId) {
                    this.pageId = ack.pageId;

                    await this.connection.invoke(
                        "JoinPage",
                        this.pageId
                    );

                    this.onPageCreated(this.pageId);
                }

                // Không tăng cursor bằng ACK.
                this.lastError = "";
                await this.synchronize();
            }
        } catch (error) {
            this.handleError(error);
        } finally {
            this.flushing = false;
            this.renderStatus();
        }
    }

    async synchronize() {
        if (this.disposed || this.blocked) return;

        if (this.replayPromise) {
            this.replayAgain = true;
            return this.replayPromise;
        }

        this.replayPromise = this.replay()
            .then(() => {
                this.lastError = "";
                this.renderStatus();
            })
            .finally(() => {
                this.replayPromise = null;
            });

        return this.replayPromise;
    }

    async replay() {
        do {
            this.replayAgain = false;

            if (!this.pageId && this.pdfScope) {
                const scope = this.pdfScope;

                const note = await api(
                    `pdfs/versions/${scope.documentVersionId}` +
                    `/pages/${scope.physicalPageNumber}/note` +
                    `?shareId=${scope.shareId}`
                );

                if (this.disposed) return;

                if (note.pageId) {
                    this.pageId = note.pageId;

                    if (this.connected) {
                        await this.connection.invoke(
                            "JoinPage",
                            this.pageId
                        );
                    }

                    this.onPageCreated(this.pageId);
                }
            }

            if (!this.pageId) continue;

            let hasMore;

            do {
                const batch = await api(
                    `pages/${this.pageId}/updates?after=${this.cursor}`
                );

                if (this.disposed) return;

                for (const update of batch.updates) {
                    try {
                        Y.applyUpdate(
                            this.document,
                            fromBase64(update.updateBytes),
                            REMOTE
                        );
                    } catch {
                        this.blocked = true;
                        this.editor.disable();

                        throw new Error(
                            "Không đọc được dữ liệu cộng tác. " +
                            "Sao chép bản nháp trước khi tải lại."
                        );
                    }

                    this.cursor = update.updateId;
                }

                hasMore = batch.hasMore;
            } while (hasMore && !this.disposed);
        } while (this.replayAgain && !this.disposed);
    }

    handleError(error) {
        const details = readError(error);
        this.lastError = details.message;

        if ([400, 401, 403, 404, 413].includes(details.status) ||
            this.blocked) {
            this.blocked = true;
            this.editor.disable();
        }

        this.renderStatus();
    }

    renderStatus() {
        if (this.disposed) return;

        if (this.blocked) {
            setStatus(
                this.statusElement,
                this.lastError +
                " Bản nháp hiện tại vẫn nằm trong editor.",
                "error"
            );
        } else if (!this.connected) {
            setStatus(
                this.statusElement,
                this.lastError ||
                "Mất kết nối — chưa đồng bộ.",
                "error"
            );
        } else if (this.pending.length > 0) {
            setStatus(
                this.statusElement,
                this.lastError ||
                `Chưa đồng bộ (${this.pending.length} cập nhật)…`,
                this.lastError ? "error" : "info"
            );
        } else if (this.lastError) {
            setStatus(
                this.statusElement,
                this.lastError,
                "error"
            );
        } else if (this.ready) {
            setStatus(
                this.statusElement,
                "Đã lưu",
                "success"
            );
        }
    }

    async dispose() {
        if (this.hasPendingChanges) {
            throw new Error(
                "Còn cập nhật chưa được xác nhận. " +
                "Đồng bộ hoặc sao chép bản nháp trước khi đổi trang."
            );
        }

        this.disposed = true;
        this.editor.disable();

        window.clearInterval(this.pollTimer);

        window.removeEventListener(
            "beforeunload",
            this.beforeUnload
        );

        if (this.onDocumentUpdate) {
            this.document.off(
                "update",
                this.onDocumentUpdate
            );
        }

        this.binding?.destroy();

        await this.connection.stop();
        this.document.destroy();
    }
}

function readError(error) {
    if (typeof error?.status === "number") {
        return {
            status: error.status,
            message: error.message
        };
    }

    const message =
        error?.message || "Không thể đồng bộ ghi chú.";

    const embedded = message.match(
        /\{"status":\d+,"title":.*\}/
    );

    if (embedded) {
        try {
            const data = JSON.parse(embedded[0]);

            return {
                status: data.status,
                message: data.title
            };
        } catch {
            // Giữ thông báo gốc.
        }
    }

    return {
        status: 0,
        message
    };
}
