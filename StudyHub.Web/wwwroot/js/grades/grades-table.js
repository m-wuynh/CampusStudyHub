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

