/* =====================================================
   PDF.JS
===================================================== */

import * as pdfjsLib
    from "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/5.4.149/pdf.min.mjs";


pdfjsLib.GlobalWorkerOptions.workerSrc =
    "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/5.4.149/pdf.worker.min.mjs";


/* =====================================================
   ANNOTATION
===================================================== */

import {
    initializeAnnotationLayer,
    enablePen,
    disablePen,
    enableEraser,
    disableEraser,
    enableHighlight,
    disableHighlight,
    loadAnnotations,
    renderAnnotationsForPage,
    undoAnnotation,
    redoAnnotation
} from "../annotation/annotation-layer.js";


/* =====================================================
   ELEMENTS
===================================================== */

const pdfContainer =
    document.getElementById(
        "pdfContainer"
    );


const readerContent =
    document.querySelector(
        ".reader-content"
    );


const previousPageButton =
    document.getElementById(
        "previousPage"
    );


const nextPageButton =
    document.getElementById(
        "nextPage"
    );


const pageNumberInput =
    document.getElementById(
        "pageNumber"
    );


const pageCountElement =
    document.getElementById(
        "pageCount"
    );


const zoomOutButton =
    document.getElementById(
        "zoomOut"
    );


const zoomInButton =
    document.getElementById(
        "zoomIn"
    );


const fitWidthButton =
    document.getElementById(
        "fitWidth"
    );


const zoomLevelElement =
    document.getElementById(
        "zoomLevel"
    );


const loadingElement =
    document.getElementById(
        "pdfLoading"
    );


const errorElement =
    document.getElementById(
        "pdfError"
    );


const errorMessageElement =
    document.getElementById(
        "pdfErrorMessage"
    );


const penTool =
    document.getElementById(
        "penTool"
    );


const eraserTool =
    document.getElementById(
        "eraserTool"
    );


const highlightTool =
    document.getElementById(
        "highlightTool"
    );


const undoTool =
    document.getElementById(
        "undoTool"
    );


const redoTool =
    document.getElementById(
        "redoTool"
    );


/* =====================================================
   TOOL STATE
===================================================== */

let penActive =
    false;


let eraserActive =
    false;


let highlightActive =
    false;


/* =====================================================
   PDF STATE
===================================================== */

const pdfUrl =
    document
        .querySelector(
            "meta[name='pdf-url']"
        )
        ?.content;


const documentId =
    Number(
        document
            .querySelector(
                "meta[name='document-id']"
            )
            ?.content
    );


let pdfDocument =
    null;


let currentPage =
    1;


let scale =
    1.0;


let rendering =
    false;


let scrollTicking =
    false;


/* =====================================================
   TOOL BUTTONS
===================================================== */

/* =====================================================
   PEN TOOL
===================================================== */

penTool.addEventListener(
    "click",
    () => {

        if (penActive) {

            penActive = false;

            disablePen();

            penTool.classList.remove(
                "active"
            );

            return;
        }


        penActive = true;

        eraserActive = false;
        highlightActive = false;


        enablePen();


        penTool.classList.add(
            "active"
        );

        eraserTool.classList.remove(
            "active"
        );

        highlightTool.classList.remove(
            "active"
        );
    }
);


/* =====================================================
   ERASER TOOL
===================================================== */

eraserTool.addEventListener(
    "click",
    () => {

        if (eraserActive) {

            eraserActive = false;

            disableEraser();

            eraserTool.classList.remove(
                "active"
            );

            return;
        }


        penActive = false;
        eraserActive = true;
        highlightActive = false;


        enableEraser();


        eraserTool.classList.add(
            "active"
        );

        penTool.classList.remove(
            "active"
        );

        highlightTool.classList.remove(
            "active"
        );
    }
);


/* =====================================================
   HIGHLIGHT TOOL
===================================================== */

highlightTool.addEventListener(
    "click",
    () => {

        if (highlightActive) {

            highlightActive = false;

            disableHighlight();

            highlightTool.classList.remove(
                "active"
            );

            return;
        }


        penActive = false;
        eraserActive = false;
        highlightActive = true;


        enableHighlight();


        highlightTool.classList.add(
            "active"
        );

        penTool.classList.remove(
            "active"
        );

        eraserTool.classList.remove(
            "active"
        );
    }
);

/* =====================================================
   UNDO / REDO
===================================================== */

undoTool.addEventListener(
    "click",
    async () => {

        undoTool.disabled =
            true;

        await undoAnnotation();

    }
);


redoTool.addEventListener(
    "click",
    async () => {

        redoTool.disabled =
            true;

        await redoAnnotation();

    }
);


/* =====================================================
   LOAD PDF
===================================================== */

async function loadPdf() {

    if (!pdfUrl) {

        showError(
            "Không tìm thấy đường dẫn PDF."
        );

        return;
    }


    try {

        showLoading();


        console.log(
            "Loading PDF:",
            pdfUrl
        );


        pdfDocument =
            await pdfjsLib
                .getDocument(
                    pdfUrl
                )
                .promise;


        console.log(
            "PDF loaded:",
            pdfDocument.numPages,
            "pages"
        );


        pageCountElement.textContent =
            pdfDocument.numPages;


        /*
         * Bắt đầu load annotation song song
         * với việc render page đầu tiên.
         */
        const annotationsPromise =
            loadAnnotations(
                documentId
            );


        /*
         * Chỉ render page 1 trước.
         */
        await renderPage(1);


        /*
         * Chờ annotation DB load xong.
         */
        await annotationsPromise;


        /*
         * Render annotation của page 1.
         */
        renderAnnotationsForPage(1);


        /*
         * User có thể bắt đầu sử dụng
         * document ngay.
         */
        hideLoading();

        updateControls();


        /*
         * Render các page còn lại
         * ở background.
         *
         * Không await.
         */
        void renderRemainingPages();

    }
    catch (error) {

        console.error(
            "PDF loading error:",
            error
        );


        showError(
            "Không thể tải nội dung PDF."
        );
    }
}


/* =====================================================
   RENDER ONE PAGE
===================================================== */

async function renderPage(
    pageNumber
) {

    if (!pdfDocument) {
        return null;
    }


    const page =
        await pdfDocument.getPage(
            pageNumber
        );


    const viewport =
        page.getViewport({
            scale: scale
        });


    /*
     * Nếu page đã tồn tại,
     * không render lại.
     */
    const existingPage =
        pdfContainer.querySelector(
            `.pdf-page[data-page="${pageNumber}"]`
        );


    if (existingPage) {

        return existingPage;
    }


    /* =================================================
       PAGE CONTAINER
    ================================================= */

    const pageContainer =
        document.createElement(
            "div"
        );


    pageContainer.className =
        "pdf-page";


    pageContainer.dataset.page =
        pageNumber;


    pageContainer.style.width =
        `${viewport.width}px`;


    pageContainer.style.height =
        `${viewport.height}px`;


    /* =================================================
       PDF CANVAS
    ================================================= */

    const canvas =
        document.createElement(
            "canvas"
        );


    const context =
        canvas.getContext(
            "2d"
        );


    canvas.width =
        Math.floor(
            viewport.width
        );


    canvas.height =
        Math.floor(
            viewport.height
        );


    canvas.style.width =
        `${viewport.width}px`;


    canvas.style.height =
        `${viewport.height}px`;


    pageContainer.appendChild(
        canvas
    );


    /* =================================================
       ANNOTATION LAYER
    ================================================= */

    const annotationLayer =
        document.createElement(
            "div"
        );


    annotationLayer.className =
        "annotation-layer";


    annotationLayer.dataset.page =
        pageNumber;


    pageContainer.appendChild(
        annotationLayer
    );


    /* =================================================
       ADD PAGE TO DOM
    ================================================= */

    pdfContainer.appendChild(
        pageContainer
    );


    /*
     * Chỉ initialize annotation
     * cho page này.
     *
     * Không query toàn bộ document.
     */
    initializeAnnotationLayer(
        annotationLayer
    );


    /* =================================================
       RENDER PDF
    ================================================= */

    await page.render({

        canvasContext:
            context,

        viewport:
            viewport

    }).promise;


    /*
     * Nếu annotation đã được load,
     * render annotation của page này.
     */
    renderAnnotationsForPage(
        pageNumber
    );


    return pageContainer;
}


/* =====================================================
   RENDER REMAINING PAGES
===================================================== */

async function renderRemainingPages() {

    if (!pdfDocument) {
        return;
    }


    for (
        let pageNumber = 2;

        pageNumber <=
        pdfDocument.numPages;

        pageNumber++
    ) {

        const existingPage =
            pdfContainer.querySelector(
                `.pdf-page[data-page="${pageNumber}"]`
            );


        if (existingPage) {
            continue;
        }


        try {

            await renderPage(
                pageNumber
            );

        }
        catch (error) {

            console.error(
                `Failed to render page ${pageNumber}:`,
                error
            );
        }
    }
}


/* =====================================================
   UPDATE CONTROLS
===================================================== */

function updateControls() {

    previousPageButton.disabled =
        currentPage <= 1;


    nextPageButton.disabled =
        !pdfDocument ||
        currentPage >=
        pdfDocument.numPages;


    pageNumberInput.value =
        currentPage;


    zoomLevelElement.textContent =
        `${Math.round(scale * 100)}%`;
}


/* =====================================================
   GO TO PAGE
===================================================== */

function goToPage(
    pageNumber
) {

    if (!pdfDocument) {
        return;
    }


    if (
        pageNumber < 1 ||
        pageNumber >
        pdfDocument.numPages
    ) {

        return;
    }


    const pageElement =
        pdfContainer.querySelector(
            `.pdf-page[data-page="${pageNumber}"]`
        );


    /*
     * Background rendering có thể chưa
     * tới page này.
     */
    if (!pageElement) {
        return;
    }


    pageElement.scrollIntoView({

        behavior:
            "smooth",

        block:
            "start"
    });
}


/* =====================================================
   PREVIOUS PAGE
===================================================== */

previousPageButton.addEventListener(
    "click",
    () => {

        goToPage(
            currentPage - 1
        );
    }
);


/* =====================================================
   NEXT PAGE
===================================================== */

nextPageButton.addEventListener(
    "click",
    () => {

        goToPage(
            currentPage + 1
        );
    }
);


/* =====================================================
   PAGE INPUT
===================================================== */

pageNumberInput.addEventListener(
    "change",
    () => {

        const page =
            Number(
                pageNumberInput.value
            );


        if (
            !pdfDocument ||
            page < 1 ||
            page >
            pdfDocument.numPages
        ) {

            pageNumberInput.value =
                currentPage;

            return;
        }


        goToPage(page);
    }
);


/* =====================================================
   ZOOM IN
===================================================== */

zoomInButton.addEventListener(
    "click",
    async () => {

        if (rendering) {
            return;
        }


        rendering =
            true;


        try {

            scale =
                Math.min(
                    scale + 0.25,
                    3.0
                );


            await rerenderDocument();

        }
        finally {

            rendering =
                false;
        }
    }
);


/* =====================================================
   ZOOM OUT
===================================================== */

zoomOutButton.addEventListener(
    "click",
    async () => {

        if (rendering) {
            return;
        }


        if (scale <= 0.5) {
            return;
        }


        rendering =
            true;


        try {

            scale =
                Math.max(
                    scale - 0.25,
                    0.5
                );


            await rerenderDocument();

        }
        finally {

            rendering =
                false;
        }
    }
);


/* =====================================================
   FIT WIDTH
===================================================== */

fitWidthButton.addEventListener(
    "click",
    async () => {

        if (rendering) {
            return;
        }


        rendering =
            true;


        try {

            scale =
                await calculateFitWidthScale();


            await rerenderDocument();

        }
        finally {

            rendering =
                false;
        }
    }
);


/* =====================================================
   CALCULATE FIT WIDTH
===================================================== */

async function calculateFitWidthScale() {

    if (!pdfDocument) {
        return 1;
    }


    const firstPage =
        await pdfDocument.getPage(
            1
        );


    const viewport =
        firstPage.getViewport({
            scale: 1
        });


    const availableWidth =
        readerContent.clientWidth -
        64;


    return Math.max(
        0.5,
        availableWidth /
        viewport.width
    );
}


/* =====================================================
   RERENDER DOCUMENT
===================================================== */

async function rerenderDocument() {

    const currentPageBeforeRender =
        currentPage;


    showLoading();


    /*
     * Xóa DOM cũ.
     *
     * Annotation cache vẫn nằm trong
     * annotation-layer.js nên không mất.
     */
    pdfContainer.innerHTML =
        "";


    /*
     * Render page hiện tại trước.
     */
    await renderPage(
        currentPageBeforeRender
    );


    /*
     * Render annotation của page hiện tại
     * đã được render trong renderPage().
     */
    currentPage =
        currentPageBeforeRender;


    updateControls();


    hideLoading();


    /*
     * Các page còn lại render background.
     */
    void renderRemainingPages();
}


/* =====================================================
   CURRENT PAGE FROM SCROLL
===================================================== */

function updateCurrentPageFromScroll() {

    if (!pdfDocument) {
        return;
    }


    const pages =
        pdfContainer.querySelectorAll(
            ".pdf-page"
        );


    if (
        pages.length === 0
    ) {

        return;
    }


    const containerRect =
        readerContent.getBoundingClientRect();


    const viewportTop =
        containerRect.top;


    const viewportBottom =
        containerRect.bottom;


    let bestPage =
        null;


    let bestVisibleHeight =
        0;


    pages.forEach(
        page => {

            const rect =
                page.getBoundingClientRect();


            const visibleTop =
                Math.max(
                    rect.top,
                    viewportTop
                );


            const visibleBottom =
                Math.min(
                    rect.bottom,
                    viewportBottom
                );


            const visibleHeight =
                Math.max(
                    0,
                    visibleBottom -
                    visibleTop
                );


            if (
                visibleHeight >
                bestVisibleHeight
            ) {

                bestVisibleHeight =
                    visibleHeight;

                bestPage =
                    page;
            }
        }
    );


    if (!bestPage) {
        return;
    }


    const pageNumber =
        Number(
            bestPage.dataset.page
        );


    if (
        pageNumber !==
        currentPage
    ) {

        currentPage =
            pageNumber;


        updateControls();
    }
}


/* =====================================================
   SCROLL EVENT
===================================================== */

readerContent.addEventListener(
    "scroll",
    () => {

        if (scrollTicking) {
            return;
        }


        scrollTicking =
            true;


        requestAnimationFrame(
            () => {

                updateCurrentPageFromScroll();


                scrollTicking =
                    false;
            }
        );
    },
    {
        passive:
            true
    }
);


/* =====================================================
   LOADING
===================================================== */

function showLoading() {

    loadingElement.hidden =
        false;


    errorElement.hidden =
        true;
}


function hideLoading() {

    loadingElement.hidden =
        true;
}


/* =====================================================
   ERROR
===================================================== */

function showError(
    message
) {

    loadingElement.hidden =
        true;


    errorElement.hidden =
        false;


    errorMessageElement.textContent =
        message;
}


/* =====================================================
   START
===================================================== */

loadPdf();
