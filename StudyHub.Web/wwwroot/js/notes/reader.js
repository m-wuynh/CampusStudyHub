const root = document.getElementById('reader');

if (root && root.dataset.readerBound !== 'true') {
    root.dataset.readerBound = 'true';
    await startReader(root);
}

async function startReader(root) {
    const $ = id => root.querySelector(`#${id}`);
    const ink = $('ink');
    const canvas = $('pdf');
    const ctx = ink.getContext('2d');
    const token = root.querySelector(
        '[name="__RequestVerificationToken"]'
    ).value;

    const notesUrl = root.dataset.notes.replace(/\/1\/?$/, '');
    const tools = {
        pen: { color: '#2563eb', width: 3 },
        highlight: { color: '#facc15', width: 20 },
        eraser: { color: '#2563eb', width: 12 }
    };

    let activeTool = 'pen';
    let pdf;
    let page = Number(root.dataset.page) || 1;
    let count = 1;
    let width = 720;
    let height = 1018;
    let strokes = [];
    let undo = [];
    let version = null;
    let current = null;
    let pointer = null;
    let erased = false;

    let busy = true;
    let ready = false;
    let dirty = false;
    let revision = 0;
    let timer = null;
    let saving = null;
    let composing = false;

    const status = text => $('status').textContent = text;

    function scheduleSave() {
        clearTimeout(timer);
        if (!busy && !composing && dirty) {
            timer = setTimeout(() => savePage(true), 1200);
        }
    }

    function changed() {
        revision++;
        dirty = true;
        status('Chưa lưu…');
        scheduleSave();
    }

    function syncToolbar() {
        $('tool').value = activeTool;
        $('color').value = tools[activeTool].color;
        $('width').value = tools[activeTool].width;

        root.querySelectorAll('[data-note-tool]').forEach(button => {
            const active = button.dataset.noteTool === activeTool;
            button.classList.toggle('active', active);
            button.setAttribute('aria-pressed', String(active));
        });

        root.querySelectorAll('[data-note-color]').forEach(button => {
            const active = activeTool !== 'eraser'
                && button.dataset.noteColor === tools[activeTool].color;

            button.classList.toggle('active', active);
            button.setAttribute('aria-pressed', String(active));
            button.style.outline = active
                ? '2px solid #567eac'
                : '1px solid #e4e7eb';
            button.style.outlineOffset = active ? '3px' : '0';
            button.disabled = busy || activeTool === 'eraser';
        });

        $('color').disabled = busy || activeTool === 'eraser';
        ink.style.cursor = activeTool === 'eraser' ? 'cell' : 'crosshair';
    }

    function setBusy(value) {
        busy = value;

        root.querySelectorAll(
            'button,input:not([type="hidden"]),select,textarea'
        ).forEach(element => element.disabled = value);

        ink.style.pointerEvents = value ? 'none' : 'auto';

        if (!value) {
            $('prev').disabled = page <= 1;
            $('next').disabled = page >= count;
        }

        syncToolbar();
    }

    function setTool(value) {
        if (busy || pointer !== null || !Object.hasOwn(tools, value)) return;
        activeTool = value;
        syncToolbar();
    }

    function setColor(value) {
        if (busy || pointer !== null || activeTool === 'eraser') return;
        if (!/^#[a-f0-9]{6}$/i.test(value)) return;
        tools[activeTool].color = value.toLowerCase();
        syncToolbar();
    }

    root.addEventListener('click', event => {
        const tool = event.target.closest('[data-note-tool]');
        if (tool && !tool.disabled) setTool(tool.dataset.noteTool);

        const color = event.target.closest('[data-note-color]');
        if (color && !color.disabled) setColor(color.dataset.noteColor);

        const toggle = event.target.closest('[data-toggle-note]');
        if (toggle && !toggle.disabled) {
            const hidden = root.classList.toggle('note-hidden');
            $('note-panel').hidden = hidden;
            toggle.setAttribute('aria-expanded', String(!hidden));
        }
    });

    $('tool').addEventListener('change', () => setTool($('tool').value));
    $('color').addEventListener('input', () => setColor($('color').value));
    $('width').addEventListener('change', () => {
        if (busy || pointer !== null) return;
        tools[activeTool].width = Math.max(
            1, Math.min(40, Number($('width').value) || 3)
        );
        syncToolbar();
    });

    async function responseError(response) {
        const data = await response.json().catch(() => ({}));
        return new Error(data.title || `Yêu cầu thất bại (${response.status}).`);
    }

    async function request(url, options) {
        const response = await fetch(url, options);
        if (!response.ok) throw await responseError(response);
        return response.json();
    }

    // Một thời điểm chỉ gửi một request lưu.
    async function savePage(automatic = false) {
        clearTimeout(timer);

        if (!ready || pointer !== null || composing) return false;
        if (saving) return saving;
        if (!dirty) return true;

        const snapshotRevision = revision;
        const snapshotPage = page;
        const payload = {
            version,
            text: $('text').value,
            strokes: structuredClone(strokes)
        };

        status(automatic ? 'Đang tự lưu…' : 'Đang lưu…');

        saving = (async () => {
            try {
                const data = await request(`${notesUrl}/${snapshotPage}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(payload)
                });

                version = data.version;
                dirty = revision !== snapshotRevision;

                status(dirty
                    ? 'Còn thay đổi đang chờ lưu…'
                    : 'Đã lưu.');

                return true;
            } catch (error) {
                status(`${error.message} Nội dung đang nhập vẫn được giữ lại.`);
                return false;
            }
        })();

        const result = await saving;
        saving = null;

        if (result && dirty) scheduleSave();
        return result;
    }

    // Dùng trước khi chuyển trang hoặc xuất file.
    async function flush() {
        clearTimeout(timer);

        if (saving && !(await saving)) return false;

        while (dirty) {
            if (!(await savePage(false))) return false;
        }

        return true;
    }

    function remember() {
        undo.push(structuredClone(strokes));
        if (undo.length > 30) undo.shift();
    }

    function drawStroke(stroke) {
        if (!stroke.points?.length) return;

        ctx.save();
        ctx.globalAlpha = stroke.tool === 'highlight' ? 0.28 : 1;
        ctx.strokeStyle = ctx.fillStyle = stroke.color;
        ctx.lineWidth = stroke.width * width / 720;
        ctx.lineCap = ctx.lineJoin = 'round';
        ctx.beginPath();

        if (stroke.points.length === 1) {
            const p = stroke.points[0];
            ctx.arc(p.x * width, p.y * height, ctx.lineWidth / 2, 0, Math.PI * 2);
            ctx.fill();
        } else {
            stroke.points.forEach((p, index) => {
                if (index === 0) ctx.moveTo(p.x * width, p.y * height);
                else ctx.lineTo(p.x * width, p.y * height);
            });
            ctx.stroke();
        }

        ctx.restore();
    }

    function redraw() {
        ctx.clearRect(0, 0, width, height);
        strokes.forEach(drawStroke);
        if (current) drawStroke(current);
    }

    async function render(target) {
        const pdfPage = await pdf.getPage(target);
        const dpr = Math.min(window.devicePixelRatio || 1, 2);
        const base = pdfPage.getViewport({ scale: 1 });

        width = 720 * Number($('zoom').value);
        const viewport = pdfPage.getViewport({ scale: width / base.width });
        height = viewport.height;

        for (const c of [canvas, ink]) {
            c.width = Math.ceil(width * dpr);
            c.height = Math.ceil(height * dpr);
            c.style.width = `${width}px`;
            c.style.height = `${height}px`;
        }

        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        await pdfPage.render({
            canvasContext: canvas.getContext('2d'),
            viewport,
            transform: [dpr, 0, 0, dpr, 0, 0]
        }).promise;
    }

    function activeThumbnail() {
        root.querySelectorAll('[data-thumbnail-page]').forEach(button => {
            const active = Number(button.dataset.thumbnailPage) === page;
            button.classList.toggle('active', active);
            button.setAttribute('aria-current', active ? 'page' : 'false');
        });
    }

    function buildThumbnails() {
        const rail = $('thumbnails');
        if (!rail) return;
        rail.replaceChildren();

        let queue = Promise.resolve();

        async function thumbnail(button) {
            try {
                const p = await pdf.getPage(Number(button.dataset.thumbnailPage));
                const base = p.getViewport({ scale: 1 });
                const viewport = p.getViewport({ scale: 84 / base.width });
                const c = button.querySelector('canvas');

                c.width = Math.ceil(viewport.width * 1.5);
                c.height = Math.ceil(viewport.height * 1.5);
                c.style.width = '100%';
                c.style.height = 'auto';

                await p.render({
                    canvasContext: c.getContext('2d'),
                    viewport,
                    transform: [1.5, 0, 0, 1.5, 0, 0]
                }).promise;
            } catch {
                button.title = 'Không tải được ảnh thu nhỏ; bấm để mở trang.';
            }
        }

        const observer = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (!entry.isIntersecting) continue;
                observer.unobserve(entry.target);
                queue = queue.then(() => thumbnail(entry.target));
            }
        }, { root: rail, rootMargin: '150px' });

        for (let i = 1; i <= count; i++) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'fn-thumbnail';
            button.dataset.thumbnailPage = String(i);
            button.setAttribute('aria-label', `Mở trang ${i}`);

            const c = document.createElement('canvas');
            c.width = 84;
            c.height = 112;

            const label = document.createElement('span');
            label.textContent = `Trang ${i}`;

            button.append(c, label);
            button.addEventListener('click', () => loadPage(i));
            rail.append(button);
            observer.observe(button);
        }

        activeThumbnail();
    }

    async function loadPage(next) {
        if (busy || pointer !== null || composing) return;
        next = Math.max(1, Math.min(count, Math.trunc(next) || 1));
        setBusy(true);

        if (ready && !(await flush())) {
            $('page').value = page;
            setBusy(false);
            return;
        }

        const previous = page;
        const hadPage = ready;

        try {
            status('Đang tải trang…');

            // Đợi cả hai tác vụ kết thúc trước khi xử lý lỗi.
            const results = await Promise.allSettled([
                request(`${notesUrl}/${next}`),
                render(next)
            ]);

            const failure = results.find(x => x.status === 'rejected');
            if (failure) throw failure.reason;

            const data = results[0].value;
            if (!Array.isArray(data.strokes) || typeof data.text !== 'string')
                throw new Error('Dữ liệu ghi chú không hợp lệ.');

            page = next;
            strokes = data.strokes;
            version = data.version;
            $('text').value = data.text;
            undo = [];
            current = null;
            revision = 0;
            dirty = false;
            ready = true;

            redraw();
            $('page').value = page;
            activeThumbnail();

            const url = new URL(location.href);
            url.searchParams.set('page', String(page));
            history.replaceState(null, '', url);

            status('Đã tải trang.');
        } catch (error) {
            // Khôi phục đúng PDF của dữ liệu đang giữ trong bộ nhớ.
            if (hadPage) {
                try {
                    await render(previous);
                    redraw();
                } catch {
                    ready = false;
                }
            }

            $('page').value = page;
            status(`${error.message} Nếu chưa mở được trang, hãy tải lại.`);
        } finally {
            setBusy(!ready);
        }
    }

    function point(event) {
        const box = ink.getBoundingClientRect();
        return {
            x: Math.max(0, Math.min(1, (event.clientX - box.left) / box.width)),
            y: Math.max(0, Math.min(1, (event.clientY - box.top) / box.height))
        };
    }

    function distance(p, a, b) {
        const dx = (b.x - a.x) * width;
        const dy = (b.y - a.y) * height;
        const px = (p.x - a.x) * width;
        const py = (p.y - a.y) * height;
        const t = Math.max(0, Math.min(
            1, (px * dx + py * dy) / (dx * dx + dy * dy || 1)
        ));
        return Math.hypot(px - t * dx, py - t * dy);
    }

    function erase(p) {
        const remaining = strokes.filter(s => !s.points.some((a, i) =>
            distance(p, a, s.points[i + 1] || a)
                <= (tools.eraser.width + s.width / 2) * width / 720));

        if (remaining.length === strokes.length) return;

        if (!erased) {
            remember();
            erased = true;
        }

        strokes = remaining;
        changed();
        redraw();
    }

    ink.addEventListener('pointerdown', event => {
        if (busy || pointer !== null || event.button !== 0 || !event.isPrimary)
            return;

        event.preventDefault();
        clearTimeout(timer);
        pointer = event.pointerId;
        ink.setPointerCapture(pointer);
        erased = false;

        if (activeTool === 'eraser') {
            erase(point(event));
        } else {
            remember();
            current = {
                tool: activeTool,
                color: tools[activeTool].color,
                width: tools[activeTool].width,
                points: [point(event)]
            };
            changed();
            redraw();
        }
    });

    ink.addEventListener('pointermove', event => {
        if (event.pointerId !== pointer) return;
        if (current) {
            if (current.points.length < 10000)
                current.points.push(point(event));
            redraw();
        } else {
            erase(point(event));
        }
    });

    function finish(event) {
        if (event.pointerId !== pointer) return;
        const captured = pointer;
        pointer = null;

        if (current) {
            strokes.push(current);
            current = null;
            changed();
            redraw();
        }

        if (ink.hasPointerCapture(captured))
            ink.releasePointerCapture(captured);

        scheduleSave();
    }

    ink.addEventListener('pointerup', finish);
    ink.addEventListener('pointercancel', finish);
    ink.addEventListener('lostpointercapture', finish);

    $('text').addEventListener('input', changed);
    $('text').addEventListener('compositionstart', () => {
        composing = true;
        clearTimeout(timer);
    });
    $('text').addEventListener('compositionend', () => {
        composing = false;
        scheduleSave();
    });

    $('undo').addEventListener('click', () => {
        if (busy || pointer !== null || !undo.length) return;
        strokes = undo.pop();
        changed();
        redraw();
    });

    $('clear').addEventListener('click', () => {
        if (busy || pointer !== null || !strokes.length) return;
        if (!confirm('Xóa toàn bộ nét vẽ của trang hiện tại?')) return;
        remember();
        strokes = [];
        changed();
        redraw();
    });

    $('prev').addEventListener('click', () => loadPage(page - 1));
    $('next').addEventListener('click', () => loadPage(page + 1));
    $('page').addEventListener('change', () => loadPage(Number($('page').value)));
    $('save').addEventListener('click', () => savePage(false));

    $('zoom').addEventListener('change', async () => {
        if (busy || pointer !== null || composing) return;
        setBusy(true);
        try {
            await render(page);
            redraw();
        } catch (error) {
            status(error.message);
        } finally {
            setBusy(false);
            scheduleSave();
        }
    });

    function download(blob, name) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = name;
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 30000);
    }

    function fileName(suffix) {
        const title = (root.dataset.title || 'tai-lieu')
            .replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_')
            .slice(0, 100);
        return `StudyHub-${title}${suffix}`;
    }

    async function exportText(format) {
        if (busy || pointer !== null || composing) return;
        setBusy(true);

        try {
            if (!(await flush())) return;

            status('Đang xuất ghi chú chữ…');

            const url = new URL(root.dataset.exportNotes, location.href);
            url.searchParams.set('pageCount', String(count));
            url.searchParams.set('format', format);

            const response = await fetch(url);
            if (!response.ok) throw await responseError(response);

            download(
                await response.blob(),
                fileName(`-ghi-chu.${format}`)
            );

            status('Đã tạo file ghi chú riêng.');
        } catch (error) {
            status(error.message);
        } finally {
            setBusy(false);
        }
    }

    async function exportPdf() {
        if (busy || pointer !== null || composing) return;
        setBusy(true);

        try {
            if (!(await flush())) return;

            status('Đang chuẩn bị PDF…');

            const lib = await import(
                'https://cdn.jsdelivr.net/npm/pdf-lib@1.17.1/+esm'
            );

            const response = await fetch(root.dataset.pdf);
            if (!response.ok) throw await responseError(response);

            const output = await lib.PDFDocument.load(
                await response.arrayBuffer()
            );

            const outputPages = output.getPages();

            for (let number = 1; number <= count; number++) {
                status(`Đang xuất PDF: ${number}/${count}`);

                const note = await request(`${notesUrl}/${number}`);
                if (!note.strokes.length) continue;

                const source = await pdf.getPage(number);
                const viewport = source.getViewport({ scale: 1 });
                const target = outputPages[number - 1];

                // Chuyển từ tọa độ hiển thị sang tọa độ PDF.
                // Bao gồm hướng xoay và vùng trang đang hiển thị.
                const convert = p => {
                    const [x, y] = viewport.convertToPdfPoint(
                        p.x * viewport.width,
                        p.y * viewport.height
                    );
                    return { x, y };
                };

                const origin = viewport.convertToPdfPoint(0, 0);
                const unit = viewport.convertToPdfPoint(1, 0);
                const pointScale = Math.hypot(
                    unit[0] - origin[0],
                    unit[1] - origin[1]
                );

                for (const stroke of note.strokes) {
                    if (!stroke.points.length) continue;

                    const hex = stroke.color.slice(1);
                    const color = lib.rgb(
                        parseInt(hex.slice(0, 2), 16) / 255,
                        parseInt(hex.slice(2, 4), 16) / 255,
                        parseInt(hex.slice(4, 6), 16) / 255
                    );

                    const thickness =
                        stroke.width * viewport.width / 720 * pointScale;

                    const opacity = stroke.tool === 'highlight' ? 0.28 : 1;

                    if (stroke.points.length === 1) {
                        target.drawCircle({
                            ...convert(stroke.points[0]),
                            size: thickness / 2,
                            color,
                            opacity
                        });
                    } else {
                        for (let i = 1; i < stroke.points.length; i++) {
                            target.drawLine({
                                start: convert(stroke.points[i - 1]),
                                end: convert(stroke.points[i]),
                                thickness,
                                color,
                                opacity
                            });
                        }
                    }
                }

                // Không đưa note.text vào PDF.
            }

            download(
                new Blob([await output.save()], { type: 'application/pdf' }),
                fileName('-da-danh-dau.pdf')
            );

            status('Đã tạo PDF có nét bút và highlight.');
        } catch (error) {
            status(`Không xuất được PDF: ${error.message}`);
        } finally {
            setBusy(false);
        }
    }

    $('download-annotated').addEventListener('click', exportPdf);
    $('export-txt').addEventListener('click', () => exportText('txt'));
    $('export-md').addEventListener('click', () => exportText('md'));

    // Chỉ giữ Ctrl+S.
    window.addEventListener('keydown', event => {
        if (event.ctrlKey && event.key.toLowerCase() === 's') {
            event.preventDefault();
            if (!busy && !event.isComposing) savePage(false);
        }
    });

    window.addEventListener('beforeunload', event => {
        if (dirty || saving) {
            event.preventDefault();
            event.returnValue = '';
        }
    });

    try {
        setBusy(true);

        const pdfjs = await import(
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/5.4.149/pdf.min.mjs'
        );

        pdfjs.GlobalWorkerOptions.workerSrc =
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/5.4.149/pdf.worker.min.mjs';

        const loading = pdfjs.getDocument({
            url: root.dataset.pdf,
            isEvalSupported: false,
            rangeChunkSize: 262144
        });

        loading.onProgress = progress => {
            if (progress.total) {
                status(`Đang tải PDF… ${Math.round(
                    progress.loaded / progress.total * 100
                )}%`);
            }
        };

        pdf = await loading.promise;
        count = pdf.numPages;
        $('page').max = count;
        $('count').textContent = `/ ${count}`;

        busy = false;
        await loadPage(page);

        if (ready) buildThumbnails();
    } catch (error) {
        setBusy(true);
        status(`Không mở được PDF: ${error.message}`);
    }
}
