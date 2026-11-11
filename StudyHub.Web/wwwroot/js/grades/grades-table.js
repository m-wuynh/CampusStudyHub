/**
 * Điểm số & Mục tiêu – Tab "Bảng điểm" (grades-table.js)
 * Gộp từ 4 file cũ, giữ nguyên thứ tự nạp: core → wizard → table → modal-entry.
 * Mục lục:
 *   1. CORE – trạng thái, chuyển tab, chuyển chế độ (xem / nhập / cấu trúc), đổi tên sổ
 *   2. WIZARD – tạo sổ điểm 2 bước
 *   3. TABLE – sửa/xóa điểm tại ô, sắp xếp cột và môn, mở modal sửa cột/môn
 *   4. MODAL NHẬP ĐIỂM – tách ";", kiểm tra 0–10, gọi SaveSubjectGrades
 */


// ══════════════════════════════════════════════════════════════════════
// 1. CORE – trạng thái, chuyển tab, chuyển chế độ (xem / nhập / cấu trúc), đổi tên sổ
// (trước đây: grades-core.js)
// ══════════════════════════════════════════════════════════════════════

/**
 * Grades Module - Core (grades-core.js)
 * Manages State, Tabs switching, UI Modes, and GradeBook renaming.
 */

window.GradesState = (function () {
    const rawData = document.getElementById('grades-server-data')?.textContent;
    const parsed = rawData ? JSON.parse(rawData) : {};
    
    return {
        selectedGradeBookId: parsed.selectedGradeBookId || null,
        existingGradeBooks: parsed.existingGradeBooks || [],
        allSubjects: parsed.allSubjects || [],
        goalLadders: parsed.goalLadders || [],
        columns: parsed.columns || [],
        subjectDetails: parsed.subjectDetails || [],
        getCsrfToken: function () {
            return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        }
    };
})();

// Anti Forgery Token for AJAX
const csrfToken = window.GradesState.getCsrfToken();

// ── Tab switching ──────────────────────────────────────────
function gradeTab(btn, tabId) {
    // Giao diện tab do CSS (.sv-tabs trong violet-theme.css) đảm nhận; JS chỉ bật/tắt class active.
    document.querySelectorAll('#grades-tabs .nav-link').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    const gradesTabEl = document.getElementById('grades-tab');
    const goalsTabEl = document.getElementById('goals-tab');
    if (gradesTabEl) gradesTabEl.style.display = tabId === 'grades-tab' ? 'block' : 'none';
    if (goalsTabEl) goalsTabEl.style.display = tabId === 'goals-tab' ? 'block' : 'none';

    try {
        localStorage.setItem('activeGradeTab', tabId);
        if (tabId === 'goals-tab') {
            if (!window.location.hash.includes('goals')) {
                history.replaceState(null, '', window.location.pathname + window.location.search + '#goals');
            }
        } else {
            if (window.location.hash.includes('goals')) {
                history.replaceState(null, '', window.location.pathname + window.location.search);
            }
        }
    } catch(e) {}
}

// ── Mode Switching ─────────────────────────────────────────
function setUiMode(mode) {
    // mode can be 'view', 'score', 'structure'
    const modes = ['view', 'score', 'structure'];
    
    // Update Headers
    modes.forEach(m => {
        const header = document.getElementById(`${m}-mode-header`);
        if(header) {
            if (m === mode) {
                header.style.display = (m === 'view' ? 'block' : 'flex');
            } else {
                header.style.display = 'none';
            }
        }
    });

    // Update Actions (only in view mode)
    const actions = document.getElementById('view-mode-actions');
    if (actions) {
        if (mode === 'view') {
            actions.style.setProperty('display', 'flex', 'important');
        } else {
            actions.style.setProperty('display', 'none', 'important');
        }
    }

    // Update Tables
    modes.forEach(m => {
        const table = document.getElementById(`table-${m}-mode`);
        if(table) table.style.display = (m === mode) ? 'block' : 'none';
    });

    // Reset URL if going back to view mode to avoid editMode lingering
    if (mode === 'view' && window.location.search.includes('editMode')) {
        const gbId = window.GradesState.selectedGradeBookId || '';
        window.location.href = window.location.pathname + (gbId ? "?gradeBookId=" + gbId : "");
    }
}

async function updateGradeBookName(gradeBookId, name) {
    if(!name.trim()) return;
    try {
        const formData = new FormData();
        formData.append('GradeBookId', gradeBookId);
        formData.append('Name', name);
        formData.append('currentGradeBookId', window.GradesState.selectedGradeBookId || '');
        
        const token = window.GradesState.getCsrfToken() || csrfToken;
        const resp = await fetch('/Grades/UpdateGradeBook', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });
        
        const result = await resp.json();
        if (result.ok) {
            const status = document.getElementById('gradebook-name-status');
            if (status) {
                status.style.display = 'inline-block';
                setTimeout(() => { status.style.display = 'none'; }, 2000);
            }
            const viewHeader = document.querySelector('#view-mode-header h3');
            if (viewHeader) viewHeader.textContent = name;
            
            const scoreHeader = document.querySelector('#score-mode-header h3');
            if (scoreHeader) {
                // Tên sổ là dữ liệu người dùng: gán bằng textContent, không ghép vào HTML.
                const badge = document.createElement('span');
                badge.className = 'badge bg-primary';
                badge.style.fontSize = '10px';
                badge.style.marginLeft = '4px';
                badge.textContent = 'Nhập điểm';
                scoreHeader.textContent = name + ' ';
                scoreHeader.appendChild(badge);
            }
        } else {
            alert("Lỗi: " + (result.message || "Không thể cập nhật tên"));
        }
    } catch (e) {
        alert("Lỗi kết nối");
    }
}

/** Mã hóa chuỗi trước khi ghép vào HTML (dùng chung cho các file grades-*.js). */
function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

document.addEventListener('DOMContentLoaded', () => {
    // Check and trigger TempData alerts without inline script
    document.querySelectorAll('.js-tempdata-alert').forEach(el => {
        const type = el.getAttribute('data-type');
        const msg = el.getAttribute('data-message');
        // showToast (site.js) ghép message vào innerHTML; thông báo có thể chứa tên do người dùng nhập.
        if (msg && window.showToast) {
            window.showToast(escapeHtml(msg), type);
        }
    });

    const urlParams = new URLSearchParams(window.location.search);
    const editMode = urlParams.get('editMode');
    if (editMode === 'structure') {
        setUiMode('structure');
    } else if (editMode === 'score' || editMode === 'true') {
        setUiMode('score');
    }

    if (!editMode && (window.location.hash === '#goals' || localStorage.getItem('activeGradeTab') === 'goals-tab')) {
        const goalsBtn = document.getElementById('tab-btn-goals');
        if (goalsBtn) gradeTab(goalsBtn, 'goals-tab');
    }

    const wizardModal = document.getElementById('wizardGradeBookModal');
    if (wizardModal) {
        wizardModal.addEventListener('show.bs.modal', () => {
            if (typeof wizardGoToStep === 'function') {
                wizardGoToStep(1);
            }
        });
    }
});

// ══════════════════════════════════════════════════════════════════════
// 2. WIZARD – tạo sổ điểm 2 bước
// (trước đây: grades-wizard.js)
// ══════════════════════════════════════════════════════════════════════

/**
 * Grades Module - Wizard (grades-wizard.js)
 * Manages the step-by-step gradebook creation wizard and date validations.
 */

function wizardGoToStep(step) {
    if (step === 2) {
        // Validate step 1
        const nameInput = document.querySelector('#wizard-step-1 input[name="Name"]');
        if (!nameInput.value.trim()) {
            nameInput.reportValidity();
            return;
        }

        const fromYear = parseInt(document.getElementById('wizardFromYear').value) || 0;
        const toYear = parseInt(document.getElementById('wizardToYear').value) || 0;
        const existingGradeBooks = (window.GradesState && window.GradesState.existingGradeBooks) ? window.GradesState.existingGradeBooks : [];
        const exists = existingGradeBooks && existingGradeBooks.some(gb => (gb.fromYear || gb.FromYear) === fromYear && (gb.toYear || gb.ToYear) === toYear);
        if (exists) {
            if (window.showToast) {
                window.showToast('Sổ điểm này đã tồn tại', 'error');
            } else {
                alert('Sổ điểm này đã tồn tại');
            }
            return;
        }
        
        const numCols = parseInt(document.getElementById('wizard-column-count').value) || 0;
        const numSubjs = parseInt(document.getElementById('wizard-subject-count').value) || 0;
        
        let colsHtml = '';
        for (let i = 0; i < numCols; i++) {
            colsHtml += `
                <div class="row g-2 mb-2">
                    <div class="col-8">
                        <input type="text" name="Columns[${i}].Name" class="sh-input" placeholder="Tên cột ${i+1}" required maxlength="100">
                    </div>
                    <div class="col-4">
                        <input type="number" name="Columns[${i}].Weight" class="sh-input" placeholder="Hệ số" value="1" step="0.1" min="0.1" required>
                    </div>
                </div>
            `;
        }
        document.getElementById('wizard-columns-container').innerHTML = colsHtml;
        
        let subjsHtml = '';
        for (let i = 0; i < numSubjs; i++) {
            subjsHtml += `
                <div class="mb-2">
                    <input type="text" name="SubjectNames[${i}]" class="sh-input" placeholder="Tên môn học ${i+1}" required maxlength="150">
                </div>
            `;
        }
        document.getElementById('wizard-subjects-container').innerHTML = subjsHtml;

        document.getElementById('wizard-step-1').style.display = 'none';
        document.getElementById('btn-wizard-next').style.display = 'none';
        
        document.getElementById('wizard-step-2').style.display = 'block';
        document.getElementById('btn-wizard-back').style.display = 'block';
        document.getElementById('btn-wizard-submit').style.display = 'block';
    } else {
        document.getElementById('wizard-step-2').style.display = 'none';
        document.getElementById('btn-wizard-back').style.display = 'none';
        document.getElementById('btn-wizard-submit').style.display = 'none';
        
        document.getElementById('wizard-step-1').style.display = 'block';
        document.getElementById('btn-wizard-next').style.display = 'block';
    }
}

// ══════════════════════════════════════════════════════════════════════
// 3. TABLE – sửa/xóa điểm tại ô, sắp xếp cột và môn, mở modal sửa cột/môn
// (trước đây: grades-table.js)
// ══════════════════════════════════════════════════════════════════════

/**
 * Grades Module - Table Operations (grades-table.js)
 * Manages inline grade editing, deleting, column and subject reordering, and modal triggers.
 */

let currentlyEditing = null;

function openInlineEdit(spanEl, entryId, subjectId, columnId, currentScore) {
    if (currentlyEditing) return; // one at a time
    currentlyEditing = spanEl;

    const originalHtml = spanEl.innerHTML;
    const isNew = entryId === null;
    
    const input = document.createElement('input');
    // text + inputmode=decimal để chấp nhận cả "8,5" lẫn "8.5" trên mọi ngôn ngữ trình duyệt
    input.type = 'text';
    input.inputMode = 'decimal';
    input.className = 'sh-input form-control-sm border border-primary';
    input.style.cssText = 'width: 50px; padding: 2px 4px; font-size: 12px; height: auto; display: inline-block; text-align: center;';
    if (!isNew) {
        input.value = currentScore;
    }
    
    spanEl.innerHTML = '';
    spanEl.appendChild(input);
    input.focus();

    const saveFn = async () => {
        const val = input.value.trim();
        if (val === '') {
            // Revert
            spanEl.innerHTML = originalHtml;
            currentlyEditing = null;
            return;
        }
        
        const normalized = val.replace(',', '.');
        const num = /^\d+(\.\d+)?$/.test(normalized) ? parseFloat(normalized) : NaN;
        if (isNaN(num) || num < 0 || num > 10) {
            alert('Điểm phải từ 0 đến 10.');
            input.focus();
            return;
        }

        input.disabled = true;
        try {
            let url, payload;
            if (isNew) {
                url = '/Grades/AddGrade';
                payload = { UserSubjectId: subjectId, GradeColumnId: columnId, Score: num };
            } else {
                url = '/Grades/UpdateGrade';
                payload = { Id: entryId, Score: num };
            }

            const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
            const resp = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(payload)
            });
            
            const result = await resp.json();
            if (result.ok) {
                const gbId = window.GradesState ? window.GradesState.selectedGradeBookId : '';
                window.location.href = window.location.pathname + (gbId ? "?gradeBookId=" + gbId + "&editMode=score" : "?editMode=score");
            } else {
                alert("Lỗi: " + result.error);
                spanEl.innerHTML = originalHtml;
                currentlyEditing = null;
            }
        } catch(e) {
            alert("Lỗi kết nối");
            spanEl.innerHTML = originalHtml;
            currentlyEditing = null;
        }
    };

    input.addEventListener('blur', saveFn);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            input.blur();
        } else if (e.key === 'Escape') {
            input.removeEventListener('blur', saveFn);
            spanEl.innerHTML = originalHtml;
            currentlyEditing = null;
        }
    });
}

async function deleteGradeInline(entryId, btnEl) {
    if(!confirm("Bạn có chắc chắn muốn xóa điểm này?")) return;
    btnEl.disabled = true;
    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/DeleteGrade', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ Id: entryId })
        });
        const result = await resp.json();
        if (result.ok) {
            const gbId = window.GradesState ? window.GradesState.selectedGradeBookId : '';
            window.location.href = window.location.pathname + (gbId ? "?gradeBookId=" + gbId + "&editMode=score" : "?editMode=score");
        } else {
            alert("Lỗi: " + result.error);
            btnEl.disabled = false;
        }
    } catch(e) {
        alert("Lỗi kết nối");
        btnEl.disabled = false;
    }
}

// ── Reorder Logic ─────────────────────────────────────────────
async function moveColumn(colId, dir) {
    const row = document.getElementById('structure-mode-header-row');
    if (!row) return;
    const ths = Array.from(row.querySelectorAll('th[data-col-id]'));
    const idx = ths.findIndex(th => th.getAttribute('data-col-id') == colId);
    if (idx < 0) return;
    if (dir === -1 && idx === 0) return;
    if (dir === 1 && idx === ths.length - 1) return;

    // Swap array elements to compute new display orders
    const newOrders = ths.map((th, i) => ({
        GradeColumnId: parseInt(th.getAttribute('data-col-id')),
        NewDisplayOrder: i + 1
    }));
    
    const targetIdx = idx + dir;
    const temp = newOrders[idx].NewDisplayOrder;
    newOrders[idx].NewDisplayOrder = newOrders[targetIdx].NewDisplayOrder;
    newOrders[targetIdx].NewDisplayOrder = temp;

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const gbId = window.GradesState ? window.GradesState.selectedGradeBookId : 0;
        const resp = await fetch('/Grades/ReorderColumns', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ GradeBookId: gbId, Columns: newOrders })
        });
        const result = await resp.json();
        if(result.ok) {
            window.location.href = window.location.pathname + (gbId ? "?gradeBookId=" + gbId + "&editMode=structure" : "?editMode=structure");
        } else {
            alert(result.error);
        }
    } catch(e) { alert("Lỗi kết nối"); }
}

async function moveSubject(subId, dir) {
    const tbody = document.querySelector('#structure-table-grid tbody');
    if (!tbody) return;
    const trs = Array.from(tbody.querySelectorAll('tr[data-subject-id]'));
    const idx = trs.findIndex(tr => tr.getAttribute('data-subject-id') == subId);
    if (idx < 0) return;
    if (dir === -1 && idx === 0) return;
    if (dir === 1 && idx === trs.length - 1) return;

    const newOrders = trs.map((tr, i) => ({
        UserSubjectId: parseInt(tr.getAttribute('data-subject-id')),
        NewDisplayOrder: i + 1
    }));
    
    const targetIdx = idx + dir;
    const temp = newOrders[idx].NewDisplayOrder;
    newOrders[idx].NewDisplayOrder = newOrders[targetIdx].NewDisplayOrder;
    newOrders[targetIdx].NewDisplayOrder = temp;

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const gbId = window.GradesState ? window.GradesState.selectedGradeBookId : '';
        const resp = await fetch('/Grades/ReorderSubjects', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ Subjects: newOrders })
        });
        const result = await resp.json();
        if(result.ok) {
            window.location.href = window.location.pathname + (gbId ? "?gradeBookId=" + gbId + "&editMode=structure" : "?editMode=structure");
        } else {
            alert(result.error);
        }
    } catch(e) { alert("Lỗi kết nối"); }
}

// ── Column & Subject Edit Modals ─────────────────────────────────
function editColumn(id, name, weight, order) {
    document.getElementById('editColumnId').value = id;
    document.getElementById('editColumnName').value = name;
    document.getElementById('editColumnWeight').value = weight;
    document.getElementById('editColumnOrder').value = order;
    new bootstrap.Modal(document.getElementById('editColumnModal')).show();
}

function editSubject(id, name, weight) {
    document.getElementById('editUserSubjectId').value = id;
    document.getElementById('editSubjectName').value = name;
    document.getElementById('editCreditWeight').value = weight;
    new bootstrap.Modal(document.getElementById('editUserSubjectModal')).show();
}

// ══════════════════════════════════════════════════════════════════════
// 4. MODAL NHẬP ĐIỂM – tách ";", kiểm tra 0–10, gọi SaveSubjectGrades
// (trước đây: grades-modal-entry.js)
// ══════════════════════════════════════════════════════════════════════

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
