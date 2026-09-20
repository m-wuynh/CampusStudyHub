/* =====================================================
   ANNOTATION LAYER
===================================================== */

/*
 * Tìm annotation layer của một page.
 */
export function getAnnotationLayer(pageNumber) {

    return document.querySelector(
        `.annotation-layer[data-page="${pageNumber}"]`
    );
}


/*
 * Tìm page container.
 */
export function getPageContainer(pageNumber) {

    return document.querySelector(
        `.pdf-page[data-page="${pageNumber}"]`
    );
}


/* =====================================================
   TOOL STATE
===================================================== */

let penEnabled = false;

let eraserEnabled = false;

let highlightEnabled = false;

let isDrawing = false;

let currentStroke = [];


/* =====================================================
   CANVAS STATE
===================================================== */

/*
 * Mỗi canvas có danh sách stroke riêng.
 */
const strokesByCanvas =
    new WeakMap();


/* =====================================================
   ANNOTATION CACHE
===================================================== */

/*
 * Annotation được load từ database một lần
 * và cache theo page.
 *
 * Map:
 *
 * pageNumber → [annotation, annotation, ...]
 */
const annotationsByPage =
    new Map();

let annotationsLoaded = false;


/* =====================================================
   HISTORY
===================================================== */

const undoStack = [];

const redoStack = [];


/* =====================================================
   SETTINGS
===================================================== */

const penSettings = {

    color: "#EF4444",

    width: 3,

    opacity: 1

};


const eraserSettings = {

    radius: 12

};


const highlightSettings = {

    color: "#FACC15",

    width: 16,

    opacity: 0.35

};


/* =====================================================
   HISTORY BUTTONS
===================================================== */

function updateHistoryButtons() {

    const undoButton =
        document.getElementById(
            "undoTool"
        );

    const redoButton =
        document.getElementById(
            "redoTool"
        );


    if (undoButton) {

        undoButton.disabled =
            undoStack.length === 0;
    }


    if (redoButton) {

        redoButton.disabled =
            redoStack.length === 0;
    }
}


/* =====================================================
   CREATE CANVAS
===================================================== */

export function createAnnotationCanvas(
    annotationLayer
) {

    let canvas =
        annotationLayer.querySelector(
            ".annotation-canvas"
        );


    if (canvas) {

        if (
            !strokesByCanvas.has(canvas)
        ) {

            strokesByCanvas.set(
                canvas,
                []
            );
        }


        return canvas;
    }


    canvas =
        document.createElement(
            "canvas"
        );


    canvas.className =
        "annotation-canvas";


    const width =
        annotationLayer.clientWidth;


    const height =
        annotationLayer.clientHeight;


    canvas.width =
        Math.max(
            1,
            Math.floor(width)
        );


    canvas.height =
        Math.max(
            1,
            Math.floor(height)
        );


    annotationLayer.appendChild(
        canvas
    );


    strokesByCanvas.set(
        canvas,
        []
    );


    return canvas;
}


/* =====================================================
   POINTER EVENTS
===================================================== */

function attachCanvasEvents(canvas) {

    if (
        canvas.dataset.initialized ===
        "true"
    ) {

        return;
    }


    canvas.addEventListener(
        "pointerdown",
        handlePointerDown
    );


    canvas.addEventListener(
        "pointermove",
        handlePointerMove
    );


    canvas.addEventListener(
        "pointerup",
        handlePointerUp
    );


    canvas.addEventListener(
        "pointercancel",
        handlePointerUp
    );


    canvas.dataset.initialized =
        "true";
}


/* =====================================================
   INITIALIZE ONE ANNOTATION LAYER
===================================================== */

export function initializeAnnotationLayer(
    annotationLayer
) {

    const canvas =
        createAnnotationCanvas(
            annotationLayer
        );


    attachCanvasEvents(
        canvas
    );


    applyCurrentToolState(
        annotationLayer,
        canvas
    );


    return canvas;
}


/* =====================================================
   APPLY CURRENT TOOL STATE
===================================================== */

function applyCurrentToolState(
    layer,
    canvas
) {

    const drawingEnabled =
        penEnabled ||
        highlightEnabled ||
        eraserEnabled;


    if (drawingEnabled) {

        layer.classList.add(
            "drawing-enabled"
        );

        canvas.style.pointerEvents =
            "auto";


        if (eraserEnabled) {

            layer.classList.add(
                "eraser-enabled"
            );

        }
        else {

            layer.classList.remove(
                "eraser-enabled"
            );
        }

    }
    else {

        layer.classList.remove(
            "drawing-enabled"
        );

        layer.classList.remove(
            "eraser-enabled"
        );

        canvas.style.pointerEvents =
            "none";

        canvas.style.cursor =
            "default";
    }
}


/* =====================================================
   INITIALIZE ALL EXISTING LAYERS
===================================================== */

/*
 * Chỉ dùng khi thực sự cần initialize
 * các layer đã tồn tại.
 *
 * Không gọi hàm này trong mỗi renderPage().
 */
export function initializePenCanvas() {

    const layers =
        document.querySelectorAll(
            ".annotation-layer"
        );


    layers.forEach(
        layer => {

            initializeAnnotationLayer(
                layer
            );
        }
    );
}


/* =====================================================
   ENABLE PEN
===================================================== */

export function enablePen() {

    penEnabled = true;
    eraserEnabled = false;
    highlightEnabled = false;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            const canvas =
                initializeAnnotationLayer(
                    layer
                );

            layer.classList.add(
                "drawing-enabled"
            );

            layer.classList.remove(
                "eraser-enabled"
            );

            canvas.style.pointerEvents =
                "auto";

            canvas.style.cursor =
                "crosshair";
        });
}


/* =====================================================
   DISABLE PEN
===================================================== */

export function disablePen() {

    penEnabled = false;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            const canvas =
                layer.querySelector(
                    ".annotation-canvas"
                );

            if (!canvas) {
                return;
            }


            if (
                !eraserEnabled &&
                !highlightEnabled
            ) {

                canvas.style.pointerEvents =
                    "none";

                canvas.style.cursor =
                    "default";

                layer.classList.remove(
                    "drawing-enabled"
                );
            }
        });
}


/* =====================================================
   ENABLE ERASER
===================================================== */

export function enableEraser() {

    penEnabled = false;
    eraserEnabled = true;
    highlightEnabled = false;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            const canvas =
                initializeAnnotationLayer(
                    layer
                );

            layer.classList.add(
                "drawing-enabled"
            );

            layer.classList.add(
                "eraser-enabled"
            );

            canvas.style.pointerEvents =
                "auto";

            canvas.style.cursor =
                "crosshair";
        });
}


/* =====================================================
   DISABLE ERASER
===================================================== */

export function disableEraser() {

    eraserEnabled = false;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            layer.classList.remove(
                "eraser-enabled"
            );


            const canvas =
                layer.querySelector(
                    ".annotation-canvas"
                );

            if (!canvas) {
                return;
            }


            if (
                !penEnabled &&
                !highlightEnabled
            ) {

                canvas.style.pointerEvents =
                    "none";

                canvas.style.cursor =
                    "default";

                layer.classList.remove(
                    "drawing-enabled"
                );
            }
        });
}


/* =====================================================
   ENABLE HIGHLIGHT
===================================================== */

export function enableHighlight() {

    penEnabled = false;
    eraserEnabled = false;
    highlightEnabled = true;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            const canvas =
                initializeAnnotationLayer(
                    layer
                );

            layer.classList.add(
                "drawing-enabled"
            );

            layer.classList.remove(
                "eraser-enabled"
            );

            canvas.style.pointerEvents =
                "auto";

            canvas.style.cursor =
                "crosshair";
        });
}


/* =====================================================
   DISABLE HIGHLIGHT
===================================================== */

export function disableHighlight() {

    highlightEnabled = false;
    isDrawing = false;


    document
        .querySelectorAll(
            ".annotation-layer"
        )
        .forEach(layer => {

            const canvas =
                layer.querySelector(
                    ".annotation-canvas"
                );

            if (!canvas) {
                return;
            }


            if (
                !penEnabled &&
                !eraserEnabled
            ) {

                canvas.style.pointerEvents =
                    "none";

                canvas.style.cursor =
                    "default";

                layer.classList.remove(
                    "drawing-enabled"
                );
            }
        });
}


/* =====================================================
   NORMALIZED COORDINATE
===================================================== */

function getNormalizedPoint(
    event,
    canvas
) {

    const rect =
        canvas.getBoundingClientRect();


    if (
        rect.width <= 0 ||
        rect.height <= 0
    ) {

        return {
            x: 0,
            y: 0
        };
    }


    const x =
        (
            event.clientX -
            rect.left
        ) /
        rect.width;


    const y =
        (
            event.clientY -
            rect.top
        ) /
        rect.height;


    return {

        x: Math.max(
            0,
            Math.min(1, x)
        ),

        y: Math.max(
            0,
            Math.min(1, y)
        )

    };
}


/* =====================================================
   DRAW STROKE
===================================================== */

function drawStroke(
    canvas,
    points,
    settings
) {

    if (
        !points ||
        points.length < 2
    ) {

        return;
    }


    const context =
        canvas.getContext("2d");


    context.strokeStyle =
        settings?.color ??
        "#EF4444";


    context.lineWidth =
        settings?.width ??
        3;


    context.globalAlpha =
        settings?.opacity ??
        1;


    context.lineCap =
        "round";


    context.lineJoin =
        "round";


    context.beginPath();


    points.forEach(
        (point, index) => {

            const x =
                (
                    point.x ??
                    point.X
                ) *
                canvas.width;


            const y =
                (
                    point.y ??
                    point.Y
                ) *
                canvas.height;


            if (
                !Number.isFinite(x) ||
                !Number.isFinite(y)
            ) {

                return;
            }


            if (index === 0) {

                context.moveTo(
                    x,
                    y
                );

            }
            else {

                context.lineTo(
                    x,
                    y
                );
            }
        }
    );


    context.stroke();


    context.globalAlpha =
        1;
}


/* =====================================================
   REDRAW CANVAS
===================================================== */

function redrawCanvas(canvas) {

    const context =
        canvas.getContext("2d");


    context.clearRect(
        0,
        0,
        canvas.width,
        canvas.height
    );


    const strokes =
        strokesByCanvas.get(canvas);


    if (!strokes) {
        return;
    }


    strokes.forEach(
        stroke => {

            const settings = {

                color:
                    stroke.color ??
                    (
                        stroke.type ===
                            "highlight"

                            ? highlightSettings.color
                            : penSettings.color
                    ),

                opacity:
                    stroke.opacity ??
                    (
                        stroke.type ===
                            "highlight"

                            ? highlightSettings.opacity
                            : penSettings.opacity
                    ),

                width:
                    stroke.width ??
                    (
                        stroke.type ===
                            "highlight"

                            ? highlightSettings.width
                            : penSettings.width
                    )
            };


            drawStroke(
                canvas,
                stroke.points,
                settings
            );
        }
    );
}


/* =====================================================
   LOAD ANNOTATIONS FROM DATABASE
===================================================== */

export async function loadAnnotations(
    documentId
) {

    if (!documentId) {

        console.error(
            "Document ID is missing."
        );

        return false;
    }


    try {

        const response =
            await fetch(
                `/Flexcil/Reader/AnnotationApi?handler=Load&documentId=${documentId}`
            );


        if (!response.ok) {

            console.error(
                "Load annotation failed:",
                response.status
            );

            return false;
        }


        const annotations =
            await response.json();


        annotationsByPage.clear();


        annotations.forEach(
            annotation => {

                const pageNumber =
                    Number(
                        annotation.pageNumber
                    );


                if (
                    !annotationsByPage.has(
                        pageNumber
                    )
                ) {

                    annotationsByPage.set(
                        pageNumber,
                        []
                    );
                }


                annotationsByPage
                    .get(pageNumber)
                    .push(annotation);
            }
        );


        annotationsLoaded = true;


        return true;

    }
    catch (error) {

        console.error(
            "Error loading annotations:",
            error
        );

        return false;
    }
}


/* =====================================================
   RENDER ANNOTATIONS FOR ONE PAGE
===================================================== */

export function renderAnnotationsForPage(
    pageNumber
) {

    if (!annotationsLoaded) {
        return;
    }


    const layer =
        getAnnotationLayer(
            pageNumber
        );


    if (!layer) {
        return;
    }


    const canvas =
        initializeAnnotationLayer(
            layer
        );


    /*
     * Prevent duplicate rendering
     * on the same canvas.
     */
    if (
        canvas.dataset.annotationsRendered ===
        "true"
    ) {

        return;
    }


    const annotations =
        annotationsByPage.get(
            Number(pageNumber)
        ) ?? [];


    const strokes =
        strokesByCanvas.get(canvas);


    if (!strokes) {
        return;
    }


    annotations.forEach(
        annotation => {

            let rawPoints;


            try {

                rawPoints =
                    typeof annotation.data ===
                        "string"

                        ? JSON.parse(
                            annotation.data
                        )

                        : annotation.data;

            }
            catch (error) {

                console.error(
                    "Cannot parse annotation data:",
                    annotation.data,
                    error
                );

                return;
            }


            if (
                !Array.isArray(rawPoints)
            ) {

                return;
            }


            const points =
                rawPoints.map(
                    point => ({

                        x:
                            Number(
                                point.x ??
                                point.X
                            ),

                        y:
                            Number(
                                point.y ??
                                point.Y
                            )

                    })
                );


            const stroke = {

                points:

                    points,

                type:

                    Number(
                        annotation.type
                    ) === 1

                        ? "highlight"
                        : "pen",

                annotationId:

                    annotation.id,

                color:

                    annotation.color,

                opacity:

                    annotation.opacity,

                width:

                    annotation.strokeWidth
            };


            strokes.push(
                stroke
            );
        }
    );


    canvas.dataset.annotationsRendered =
        "true";


    redrawCanvas(
        canvas
    );
}


/* =====================================================
   DISTANCE POINT → SEGMENT
===================================================== */

function distancePointToSegment(
    point,
    start,
    end
) {

    const dx =
        end.x - start.x;


    const dy =
        end.y - start.y;


    if (
        dx === 0 &&
        dy === 0
    ) {

        return Math.hypot(
            point.x - start.x,
            point.y - start.y
        );
    }


    const t =
        Math.max(
            0,
            Math.min(
                1,
                (
                    (
                        point.x -
                        start.x
                    ) * dx +

                    (
                        point.y -
                        start.y
                    ) * dy
                ) /
                (
                    dx * dx +
                    dy * dy
                )
            )
        );


    const projection = {

        x:
            start.x +
            t * dx,

        y:
            start.y +
            t * dy
    };


    return Math.hypot(
        point.x -
        projection.x,

        point.y -
        projection.y
    );
}


/* =====================================================
   CHECK POINT NEAR STROKE
===================================================== */

function isPointNearStroke(
    point,
    stroke,
    canvas
) {

    const points =
        stroke.points;


    if (
        !points ||
        points.length < 2
    ) {

        return false;
    }


    const threshold =
        eraserSettings.radius /
        Math.max(
            canvas.width,
            canvas.height
        );


    for (
        let i = 0;
        i < points.length - 1;
        i++
    ) {

        const distance =
            distancePointToSegment(
                point,
                points[i],
                points[i + 1]
            );


        if (
            distance <= threshold
        ) {

            return true;
        }
    }


    return false;
}


/* =====================================================
   SAVE STROKE
===================================================== */

async function saveStroke(
    canvas,
    stroke,
    addToHistory = true
) {

    const pageContainer =
        canvas.closest(
            ".pdf-page"
        );


    if (!pageContainer) {
        return null;
    }


    const pageNumber =
        Number(
            pageContainer.dataset.page
        );


    const documentId =
        Number(
            document
                .querySelector(
                    "meta[name='document-id']"
                )
                ?.content
        );


    if (!documentId) {

        console.error(
            "Document ID is missing."
        );

        return null;
    }


    const token =
        document.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value;


    const response =
        await fetch(
            "/Flexcil/Reader/AnnotationApi?handler=Save",
            {

                method:
                    "POST",

                headers: {

                    "Content-Type":
                        "application/json",

                    "RequestVerificationToken":
                        token
                },

                body:
                    JSON.stringify({

                        documentId:
                            documentId,

                        pageNumber:
                            pageNumber,

                        type:
                            stroke.type ===
                                "highlight"

                                ? 1
                                : 2,

                        points:
                            stroke.points,

                        color:
                            stroke.color,

                        opacity:
                            stroke.opacity,

                        strokeWidth:
                            stroke.width
                    })
            }
        );


    if (!response.ok) {

        console.error(
            "Failed to save annotation:",
            response.status
        );

        return null;
    }


    const result =
        await response.json();


    if (!result.success) {

        return null;
    }


    stroke.annotationId =
        result.id;


    if (addToHistory) {

        undoStack.push({

            action:
                "create",

            stroke:
                stroke,

            canvas:
                canvas
        });


        redoStack.length =
            0;


        updateHistoryButtons();
    }


    return result.id;
}


/* =====================================================
   DELETE ANNOTATION
===================================================== */

async function deleteAnnotation(
    annotationId
) {

    if (!annotationId) {
        return false;
    }


    const token =
        document.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value;


    try {

        const response =
            await fetch(
                "/Flexcil/Reader/AnnotationApi?handler=Delete",
                {

                    method:
                        "POST",

                    headers: {

                        "Content-Type":
                            "application/json",

                        "RequestVerificationToken":
                            token
                    },

                    body:
                        JSON.stringify({

                            id:
                                annotationId
                        })
                }
            );


        if (!response.ok) {

            console.error(
                "Failed to delete annotation:",
                response.status
            );

            return false;
        }


        const result =
            await response.json();


        return result.success === true;

    }
    catch (error) {

        console.error(
            "Error deleting annotation:",
            error
        );

        return false;
    }
}


/* =====================================================
   REMOVE STROKE FROM CANVAS
===================================================== */

function removeStrokeFromCanvas(
    canvas,
    stroke
) {

    const strokes =
        strokesByCanvas.get(
            canvas
        );


    if (!strokes) {
        return;
    }


    const index =
        strokes.indexOf(
            stroke
        );


    if (index === -1) {
        return;
    }


    strokes.splice(
        index,
        1
    );


    redrawCanvas(
        canvas
    );
}


/* =====================================================
   ADD STROKE TO CANVAS
===================================================== */

function addStrokeToCanvas(
    canvas,
    stroke
) {

    const strokes =
        strokesByCanvas.get(
            canvas
        );


    if (!strokes) {
        return;
    }


    if (
        strokes.includes(
            stroke
        )
    ) {

        return;
    }


    strokes.push(
        stroke
    );


    redrawCanvas(
        canvas
    );
}


/* =====================================================
   ERASE AT POINT
===================================================== */

async function eraseAtPoint(
    canvas,
    point
) {

    const strokes =
        strokesByCanvas.get(
            canvas
        );


    if (!strokes) {
        return;
    }


    const removedStrokes =
        [];


    const remainingStrokes =
        strokes.filter(
            stroke => {

                const shouldRemove =
                    isPointNearStroke(
                        point,
                        stroke,
                        canvas
                    );


                if (
                    shouldRemove
                ) {

                    removedStrokes.push(
                        stroke
                    );
                }


                return !shouldRemove;
            }
        );


    if (
        removedStrokes.length === 0
    ) {

        return;
    }


    /*
     * UI xóa ngay lập tức.
     */
    strokesByCanvas.set(
        canvas,
        remainingStrokes
    );


    redrawCanvas(
        canvas
    );


    /*
     * Database xử lý sau.
     */
    for (
        const stroke
        of removedStrokes
    ) {

        if (
            !stroke.annotationId
        ) {

            continue;
        }


        const deleted =
            await deleteAnnotation(
                stroke.annotationId
            );


        if (deleted) {

            undoStack.push({

                action:
                    "delete",

                stroke:
                    stroke,

                canvas:
                    canvas
            });


            redoStack.length =
                0;


            updateHistoryButtons();

        }
        else {

            /*
             * Nếu DB delete thất bại,
             * restore stroke vào UI.
             */
            addStrokeToCanvas(
                canvas,
                stroke
            );
        }
    }
}


/* =====================================================
   POINTER DOWN
===================================================== */

function handlePointerDown(
    event
) {

    if (
        !penEnabled &&
        !eraserEnabled &&
        !highlightEnabled
    ) {

        return;
    }


    const canvas =
        event.currentTarget;


    event.preventDefault();


    canvas.setPointerCapture(
        event.pointerId
    );


    const point =
        getNormalizedPoint(
            event,
            canvas
        );


    if (
        eraserEnabled
    ) {

        void eraseAtPoint(
            canvas,
            point
        );

        return;
    }


    isDrawing =
        true;


    currentStroke =
        [point];
}


/* =====================================================
   POINTER MOVE
===================================================== */

function handlePointerMove(
    event
) {

    const canvas =
        event.currentTarget;


    if (
        eraserEnabled
    ) {

        const point =
            getNormalizedPoint(
                event,
                canvas
            );


        void eraseAtPoint(
            canvas,
            point
        );


        return;
    }


    if (
        !isDrawing ||
        (
            !penEnabled &&
            !highlightEnabled
        )
    ) {

        return;
    }


    const point =
        getNormalizedPoint(
            event,
            canvas
        );


    currentStroke.push(
        point
    );


    redrawCanvas(
        canvas
    );


    const settings =
        highlightEnabled
            ? highlightSettings
            : penSettings;


    drawStroke(
        canvas,
        currentStroke,
        settings
    );
}


/* =====================================================
   POINTER UP
===================================================== */

function handlePointerUp(
    event
) {

    if (!isDrawing) {
        return;
    }


    const canvas =
        event.currentTarget;


    isDrawing =
        false;


    try {

        canvas.releasePointerCapture(
            event.pointerId
        );

    }
    catch {
        /*
         * Pointer capture may already
         * have been released.
         */
    }


    if (
        currentStroke.length < 2
    ) {

        currentStroke = [];

        return;
    }


    const isHighlight =
        highlightEnabled;


    const settings =
        isHighlight
            ? highlightSettings
            : penSettings;


    const stroke = {

        points:
            [...currentStroke],

        type:
            isHighlight
                ? "highlight"
                : "pen",

        annotationId:
            null,

        color:
            settings.color,

        opacity:
            settings.opacity,

        width:
            settings.width
    };


    const strokes =
        strokesByCanvas.get(
            canvas
        );


    if (strokes) {

        strokes.push(
            stroke
        );


        redrawCanvas(
            canvas
        );


        void saveStroke(
            canvas,
            stroke
        );
    }


    currentStroke = [];
}


/* =====================================================
   UNDO
===================================================== */

export async function undoAnnotation() {

    if (
        undoStack.length === 0
    ) {

        return;
    }


    const action =
        undoStack.pop();


    if (
        action.action ===
        "create"
    ) {

        const deleted =
            await deleteAnnotation(
                action.stroke.annotationId
            );


        if (!deleted) {

            undoStack.push(
                action
            );

            updateHistoryButtons();

            return;
        }


        removeStrokeFromCanvas(
            action.canvas,
            action.stroke
        );


        redoStack.push(
            action
        );
    }


    else if (
        action.action ===
        "delete"
    ) {

        const annotationId =
            await saveStroke(
                action.canvas,
                action.stroke,
                false
            );


        if (!annotationId) {

            undoStack.push(
                action
            );

            updateHistoryButtons();

            return;
        }


        action.stroke.annotationId =
            annotationId;


        addStrokeToCanvas(
            action.canvas,
            action.stroke
        );


        redoStack.push(
            action
        );
    }


    updateHistoryButtons();
}


/* =====================================================
   REDO
===================================================== */

export async function redoAnnotation() {

    if (
        redoStack.length === 0
    ) {

        return;
    }


    const action =
        redoStack.pop();


    if (
        action.action ===
        "create"
    ) {

        const annotationId =
            await saveStroke(
                action.canvas,
                action.stroke,
                false
            );


        if (!annotationId) {

            redoStack.push(
                action
            );

            updateHistoryButtons();

            return;
        }


        action.stroke.annotationId =
            annotationId;


        addStrokeToCanvas(
            action.canvas,
            action.stroke
        );


        undoStack.push(
            action
        );
    }


    else if (
        action.action ===
        "delete"
    ) {

        const deleted =
            await deleteAnnotation(
                action.stroke.annotationId
            );


        if (!deleted) {

            redoStack.push(
                action
            );

            updateHistoryButtons();

            return;
        }


        removeStrokeFromCanvas(
            action.canvas,
            action.stroke
        );


        undoStack.push(
            action
        );
    }


    updateHistoryButtons();
}
