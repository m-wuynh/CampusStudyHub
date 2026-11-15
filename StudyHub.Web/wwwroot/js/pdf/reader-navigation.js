(() => {
    const shell = document.querySelector("[data-reader-shell]");
    const viewer = document.getElementById("pdf-pdf-viewer");
    if (!shell || !viewer) return;

    const stateUrl = shell.dataset.pageStateUrl;
    const pageCount = Number(shell.dataset.pageCount);
    const isShared = shell.dataset.shared === "true";
    const navigationStatus = shell.querySelector("[data-reader-nav-status]");
    let currentPage = Number(shell.dataset.currentPage);
    let pendingNavigation = null;
    let navigating = false;

    const setStatus = (message, isError = false) => {
        if (!navigationStatus) return;
        navigationStatus.textContent = message;
        navigationStatus.classList.toggle("text-danger", isError);
    };

    const viewerUrlForPage = page => {
        const url = new URL(viewer.src, window.location.origin);
        url.searchParams.set("page", String(page));
        return url.toString();
    };

    const updateOuterUrl = (page, replace = false) => {
        const url = new URL(window.location.href);
        url.searchParams.set("page", String(page));
        const state = { pdfPage: page };
        if (replace) history.replaceState(state, "", url);
        else history.pushState(state, "", url);
    };

    const updatePageUi = state => {
        currentPage = state.pageNumber;
        shell.dataset.currentPage = String(currentPage);
        shell.querySelectorAll("[data-current-page-label], [data-note-page]")
            .forEach(element => { element.textContent = String(currentPage); });

        const noteForm = shell.querySelector("form[data-pdf-autosave]");
        if (noteForm) {
            noteForm.elements.namedItem("pageNumber").value = String(currentPage);
            if (window.pdfAutosave) {
                window.pdfAutosave.replace(state.noteText, state.noteVersionToken);
            } else {
                const textarea = noteForm.querySelector("textarea[name='text']");
                const version = noteForm.elements.namedItem("versionToken");
                if (textarea) textarea.value = state.noteText || "";
                if (version) version.value = state.noteVersionToken || "";
            }
        }

    };

    const loadPageState = async page => {
        const url = new URL(stateUrl, window.location.origin);
        url.searchParams.set("pageNumber", String(page));
        const response = await fetch(url, {
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Không thể tải dữ liệu của trang PDF.");
        return response.json();
    };

    const navigate = async (url, options = {}) => {
        if (navigating) return;
        const target = new URL(url, window.location.origin);
        if (target.origin !== window.location.origin) return;
        const page = Number(target.searchParams.get("page"));
        if (!Number.isInteger(page) || page < 1 || page > pageCount || page === currentPage) return;

        navigating = true;
        shell.classList.add("is-navigating");
        setStatus("Đang chuyển trang…");
        try {
            await window.pdfAutosave?.flush();
            pendingNavigation = {
                page,
                pushHistory: options.pushHistory !== false
            };
            viewer.src = target.toString();
        } catch (error) {
            pendingNavigation = null;
            navigating = false;
            shell.classList.remove("is-navigating");
            setStatus(error instanceof Error ? error.message : "Không thể chuyển trang.", true);
        }
    };

    viewer.addEventListener("load", async () => {
        if (!pendingNavigation) return;
        const navigation = pendingNavigation;
        pendingNavigation = null;
        try {
            const state = await loadPageState(navigation.page);
            updatePageUi(state);
            updateOuterUrl(state.pageNumber, !navigation.pushHistory);
            setStatus(isShared ? `Trang ${state.pageNumber}` : "");
        } catch (error) {
            setStatus(error instanceof Error ? error.message : "Không thể cập nhật trang.", true);
        } finally {
            navigating = false;
            shell.classList.remove("is-navigating");
        }
    });

    window.addEventListener("message", event => {
        if (event.origin !== window.location.origin || event.source !== viewer.contentWindow) return;
        if (event.data?.type !== "pdf:page-request" || !event.data.url) return;
        navigate(event.data.url);
    });

    shell.addEventListener("click", event => {
        const link = event.target.closest("[data-reader-page-request]");
        if (!link) return;
        event.preventDefault();
        navigate(link.href);
    });

    window.addEventListener("popstate", () => {
        const page = Number(new URL(window.location.href).searchParams.get("page")) || 1;
        if (page !== currentPage) navigate(viewerUrlForPage(page), { pushHistory: false });
    });

    updateOuterUrl(currentPage, true);
})();
