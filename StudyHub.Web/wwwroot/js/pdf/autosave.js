(() => {
    const delayMilliseconds = 900;

    for (const form of document.querySelectorAll("form[data-pdf-autosave]")) {
        const textarea = form.querySelector("textarea[name='text']");
        const status = form.querySelector("[data-autosave-status]");
        const retry = form.querySelector("[data-autosave-retry]");
        const url = form.dataset.autosaveUrl;
        if (!textarea || !status || !url || textarea.readOnly) continue;

        let timer;
        let saving = false;
        let dirty = false;
        let savePromise = null;

        const show = (message, isError = false) => {
            status.textContent = message;
            status.classList.toggle("text-danger", isError);
            retry?.classList.toggle("d-none", !isError);
        };

        const schedule = (delay = delayMilliseconds) => {
            clearTimeout(timer);
            timer = window.setTimeout(save, delay);
        };

        const save = () => {
            if (savePromise) return savePromise;
            if (!dirty) return Promise.resolve();

            savePromise = (async () => {
                saving = true;
                dirty = false;
                const savedText = textarea.value;
                show("Đang lưu…");

                try {
                    const body = new FormData(form);
                    const response = await fetch(url, {
                        method: "POST",
                        body,
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    if (!response.ok) {
                        const problem = await response.json().catch(() => null);
                        throw new Error(problem?.title || "Không thể tự động lưu.");
                    }

                    const result = await response.json();
                    const nextVersion = result.versionToken || "";
                    for (const input of form.querySelectorAll("input[name='versionToken']")) {
                        input.value = nextVersion;
                    }

                    const savedAt = new Intl.DateTimeFormat("vi-VN", {
                        hour: "2-digit",
                        minute: "2-digit"
                    }).format(new Date());
                    show(`Đã lưu lúc ${savedAt}`);
                    if (textarea.value !== savedText) {
                        dirty = true;
                        schedule(100);
                    }
                } catch (error) {
                    dirty = true;
                    show(error instanceof Error ? error.message : "Không thể tự động lưu.", true);
                } finally {
                    saving = false;
                    savePromise = null;
                }
            })();

            return savePromise;
        };

        textarea.addEventListener("input", () => {
            dirty = true;
            show("Chưa lưu");
            schedule();
        });

        retry?.addEventListener("click", () => schedule(0));

        window.pdfAutosave = {
            flush: async () => {
                clearTimeout(timer);
                if (savePromise) await savePromise;
                if (dirty) await save();
                if (dirty) throw new Error("Không thể lưu ghi chú trước khi chuyển trang.");
            },
            replace: (text, versionToken) => {
                clearTimeout(timer);
                dirty = false;
                textarea.value = text || "";
                const versionInput = form.querySelector("input[name='versionToken']");
                if (versionInput) versionInput.value = versionToken || "";
                show("Mọi thay đổi sẽ tự động lưu.");
            }
        };

        window.addEventListener("beforeunload", event => {
            if (!dirty && !saving) return;
            event.preventDefault();
            event.returnValue = "";
        });
    }
})();
