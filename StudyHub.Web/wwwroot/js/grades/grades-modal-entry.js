/**
 * Grades Module - Modal Entry (grades-modal-entry.js)
 * Manages the "Nhập điểm" popup modal for bulk-entering multiple column grades per subject.
 */

let modalCurrentSubjectId = null;
let modalScores = [];

// Nhiều điểm trong một cột phân cách bằng ";". Dấu phẩy và dấu chấm đều là dấu thập phân ("8,5" = "8.5").
const SCORE_SEPARATOR = ';';

function parseScoreToken(token) {
    const normalized = token.trim().replace(',', '.');
    if (!/^\d+(\.\d+)?$/.test(normalized)) return NaN;
    return parseFloat(normalized);
}

function openEnterGradesModal(targetSubjectId) {
    const modalEl = document.getElementById('enterGradesModal');
    if (!modalEl) return;
    const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);

    const selectEl = document.getElementById('modalSubjectSelect');
    if (!selectEl) return;
    selectEl.innerHTML = '';

    const subjectData = (window.GradesState && window.GradesState.subjectDetails) ? window.GradesState.subjectDetails : [];

    if (!subjectData || subjectData.length === 0) {
        selectEl.innerHTML = '<option value="">(Chưa có môn học nào)</option>';
        const cont = document.getElementById('modalColumnsContainer');
        if (cont) cont.innerHTML = '<div class="alert alert-info" style="font-size:13px;"><i class="bi bi-info-circle me-1"></i>Vui lòng thêm môn học vào bảng điểm trước khi nhập điểm.</div>';
        bsModal.show();
        return;
    }

    subjectData.forEach(sub => {
        const subId = sub.userSubjectId || sub.UserSubjectId;
        const subName = sub.subjectName || sub.SubjectName;
        const opt = document.createElement('option');
        opt.value = subId;
        opt.textContent = subName;
        selectEl.appendChild(opt);
    });

    let initialSubjectId = targetSubjectId;
    if (!initialSubjectId && subjectData.length > 0) {
        initialSubjectId = subjectData[0].userSubjectId || subjectData[0].UserSubjectId;
    }

    if (initialSubjectId) {
        selectEl.value = initialSubjectId;
        onModalSubjectChanged(initialSubjectId);
    }

    bsModal.show();
}

function onModalSubjectChanged(subjectId) {
    modalCurrentSubjectId = parseInt(subjectId);
    if (!modalCurrentSubjectId) {
        const cont = document.getElementById('modalColumnsContainer');
        if (cont) cont.innerHTML = '';
        return;
    }

    const subjectData = (window.GradesState && window.GradesState.subjectDetails) ? window.GradesState.subjectDetails : [];
    const subject = subjectData.find(s => (s.userSubjectId || s.UserSubjectId) == modalCurrentSubjectId);
    if (!subject) return;

    modalScores = [];
    const entries = subject.entries || subject.Entries || [];
    entries.forEach(e => {
        modalScores.push({
            id: e.id || e.Id || null,
            gradeColumnId: e.gradeColumnId || e.GradeColumnId,
            score: e.score !== undefined ? e.score : (e.Score !== undefined ? e.Score : 0)
        });
    });

    renderModalColumns();
}

function renderModalColumns() {
    const container = document.getElementById('modalColumnsContainer');
    if (!container) return;
    container.innerHTML = '';

    const columnData = (window.GradesState && window.GradesState.columns) ? window.GradesState.columns : [];

    if (!columnData || columnData.length === 0) {
        container.innerHTML = '<div class="alert alert-warning" style="font-size:13px;"><i class="bi bi-exclamation-triangle me-2"></i>Sổ điểm này chưa có cột đánh giá nào. Vui lòng thêm cột ở mục "Chỉnh sửa bảng".</div>';
        return;
    }

    columnData.forEach(col => {
        const colId = col.gradeColumnId || col.GradeColumnId;
        const colName = col.name || col.Name;
        const colWeight = col.weight || col.Weight;

        const colScores = modalScores.filter(s => s.gradeColumnId == colId);
        const scoresStr = colScores.map(s => s.score).join(SCORE_SEPARATOR + ' ');

        const w = parseFloat(colWeight) || 1;
        let badgeClass = 'text-white';
        let badgeStyle = '';
        if (w <= 1) {
            badgeClass = 'bg-secondary text-white';
        } else if (w <= 2) {
            badgeClass = 'bg-primary text-white';
        } else if (w <= 3) {
            badgeClass = 'text-white';
            badgeStyle = 'background-color: #f97316;';
        } else {
            badgeClass = 'bg-danger text-white';
        }

        const card = document.createElement('div');
        card.className = 'card border rounded-3 p-3 bg-white shadow-sm';
        card.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2 pb-2 border-bottom">
                <span class="fw-bold" style="font-size: 14px; color: var(--sh-slate-800);">${escapeHtml(colName)}</span>
                <span class="badge ${badgeClass}" style="font-size: 11px; ${badgeStyle}">Hệ số ${colWeight}</span>
            </div>
            <input type="text" class="form-control sh-input" 
                   id="col-input-${colId}" 
                   value="${scoresStr}" 
                   style="font-size: 14px;">
        `;
        container.appendChild(card);
    });
}

async function saveModalGrades() {
    if (!modalCurrentSubjectId) {
        alert("Vui lòng chọn môn học.");
        return;
    }

    const btn = document.getElementById('btnSaveModalGrades');
    if (btn) btn.disabled = true;

    const columnData = (window.GradesState && window.GradesState.columns) ? window.GradesState.columns : [];
    const entries = [];

    for (const col of (columnData || [])) {
        const colId = col.gradeColumnId || col.GradeColumnId;
        const colName = col.name || col.Name;
        const inputEl = document.getElementById('col-input-' + colId);
        if (!inputEl) continue;

        const rawVal = inputEl.value.trim();
        if (!rawVal) continue;

        const tokens = rawVal.split(SCORE_SEPARATOR).map(t => t.trim()).filter(t => t.length > 0);
        const existingColEntries = modalScores.filter(s => s.gradeColumnId == colId);

        for (let i = 0; i < tokens.length; i++) {
            const token = tokens[i];
            const scoreNum = parseScoreToken(token);
            if (isNaN(scoreNum) || scoreNum < 0 || scoreNum > 10) {
                alert(`Điểm "${token}" ở nhóm "${colName}" không hợp lệ. Điểm phải là số từ 0 đến 10.`);
                if (btn) btn.disabled = false;
                inputEl.focus();
                return;
            }

            const existingEntry = existingColEntries[i];
            entries.push({
                GradeEntryId: (existingEntry && existingEntry.id) ? existingEntry.id : null,
                GradeColumnId: colId,
                Score: scoreNum
            });
        }
    }

    const payload = {
        UserSubjectId: modalCurrentSubjectId,
        Entries: entries
    };

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/SaveSubjectGrades', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(payload)
        });

        const result = await resp.json();
        if (result.ok) {
            window.location.reload();
        } else {
            alert("Lỗi khi lưu điểm: " + (result.message || "Không xác định"));
            if (btn) btn.disabled = false;
        }
    } catch (e) {
        alert("Lỗi kết nối khi lưu điểm.");
        if (btn) btn.disabled = false;
    }
}


