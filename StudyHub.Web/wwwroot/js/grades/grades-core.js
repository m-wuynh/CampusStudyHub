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
    document.querySelectorAll('.nav-link').forEach(b => {
        b.style.cssText = 'font-size:12px;color:var(--sh-slate-500);border:0;background:transparent;padding:8px 16px;font-weight:600;';
        b.classList.remove('active');
    });
    if (btn) {
        btn.style.cssText = 'font-size:12px;font-weight:700;color:var(--sh-indigo-600);border-bottom:2px solid var(--sh-indigo-600);border-top:0;border-left:0;border-right:0;background:transparent;padding:8px 16px;';
        btn.classList.add('active');
    }
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
