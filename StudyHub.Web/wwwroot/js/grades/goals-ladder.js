/**
 * Grades Module - Goals & Goal Ladder (goals-ladder.js)
 * Manages Goal Ladder interactions, creating rungs, editing goals, reordering, and activity sync.
 */

function onAddGoalGradeBookChanged(gradeBookId) {
    const gbId = parseInt(gradeBookId);
    const subSelect = document.getElementById('addGoalSubjectId');
    if (!subSelect) return;
    subSelect.innerHTML = '';

    const hiddenGb = document.getElementById('addGoalCurrentGradeBookId');
    if (hiddenGb) hiddenGb.value = gbId || '';

    const allSubjectsData = (window.GradesState && window.GradesState.allSubjects) ? window.GradesState.allSubjects : [];
    const filtered = (allSubjectsData || []).filter(s => (s.gradeBookId || s.GradeBookId) === gbId);
    if (!filtered || filtered.length === 0) {
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = '(Bảng điểm này chưa có môn học nào)';
        subSelect.appendChild(opt);
    } else {
        // Tên môn là duy nhất trong mỗi sổ nên liệt kê trực tiếp.
        filtered.forEach(s => {
            const opt = document.createElement('option');
            opt.value = s.userSubjectId || s.UserSubjectId;
            opt.textContent = s.subjectName || s.SubjectName;
            subSelect.appendChild(opt);
        });
    }
    onAddGoalSubjectChanged();
}

function resetAddGoalModal() {
    const titleEl = document.getElementById('addGoalModalTitle');
    if (titleEl) titleEl.innerText = 'Đặt mục tiêu học tập';

    const currentGbId = (window.GradesState && window.GradesState.selectedGradeBookId) ? window.GradesState.selectedGradeBookId : 0;
    const gbSelect = document.getElementById('addGoalGradeBookSelect');
    if (gbSelect) {
        if (currentGbId > 0) gbSelect.value = currentGbId;
        onAddGoalGradeBookChanged(gbSelect.value);
    }

    const titleInput = document.getElementById('goal-title');
    if (titleInput) titleInput.value = '';
    const targetInput = document.getElementById('goal-target');
    if (targetInput) targetInput.value = '';
    const deadlineInput = document.getElementById('goal-deadline');
    if (deadlineInput) deadlineInput.value = '';
}

function openAddGoalForSubject(id) {
    const allSubjectsData = (window.GradesState && window.GradesState.allSubjects) ? window.GradesState.allSubjects : [];
    const sub = (allSubjectsData || []).find(s => (s.userSubjectId || s.UserSubjectId) == id);
    const gbId = sub ? (sub.gradeBookId || sub.GradeBookId) : null;
    // Môn không thuộc sổ (dữ liệu cũ) không được tạo mục tiêu mới (Q03).
    if (!gbId) {
        alert('Môn học này không thuộc sổ điểm nào nên không thể thêm mục tiêu mới.');
        return;
    }

    const gbSelect = document.getElementById('addGoalGradeBookSelect');
    if (gbSelect) {
        gbSelect.value = gbId;
        onAddGoalGradeBookChanged(gbId);
    }

    const sel = document.getElementById('addGoalSubjectId');
    if (sel) sel.value = id;

    const titleInput = document.getElementById('goal-title');
    if (titleInput) titleInput.value = '';
    const targetInput = document.getElementById('goal-target');
    if (targetInput) targetInput.value = '';
    const deadlineInput = document.getElementById('goal-deadline');
    if (deadlineInput) deadlineInput.value = '';

    onAddGoalSubjectChanged();
    new bootstrap.Modal(document.getElementById('addGoalModal')).show();
}

function onAddGoalSubjectChanged() {
    const subId = parseInt(document.getElementById('addGoalSubjectId')?.value);
    const allSubjectsData = (window.GradesState && window.GradesState.allSubjects) ? window.GradesState.allSubjects : [];
    const selectedSub = (allSubjectsData || []).find(s => (s.userSubjectId || s.UserSubjectId) == subId);

    const goalLaddersData = (window.GradesState && window.GradesState.goalLadders) ? window.GradesState.goalLadders : [];
    // Ladder gắn đúng một môn (UserSubjectId), không gộp theo tên.
    const ladder = (goalLaddersData || []).find(l => (l.userSubjectId || l.UserSubjectId) == subId);
    
    const targetHint = document.getElementById('goal-target-hint');
    const modalTitle = document.getElementById('addGoalModalTitle');
    const displayName = selectedSub ? (selectedSub.subjectName || selectedSub.SubjectName) : '';

    if (ladder && (ladder.hasBaseGoal || ladder.HasBaseGoal) && (ladder.baseGoal || ladder.BaseGoal)) {
        const bg = ladder.baseGoal || ladder.BaseGoal;
        const baseTarget = bg.targetValue || bg.TargetValue;
        if (modalTitle && displayName) {
            modalTitle.innerText = `Thêm nấc mục tiêu cho môn ${displayName}`;
        }
        if (targetHint && baseTarget) {
            targetHint.innerText = `Phải lớn hơn mục tiêu gốc (${Number(baseTarget).toFixed(1)}) và ≤ 10.0`;
            targetHint.className = 'text-primary fw-semibold';
        }
    } else {
        if (modalTitle && displayName) {
            modalTitle.innerText = `Đặt mục tiêu cho môn ${displayName}`;
        }
        if (targetHint) {
            targetHint.innerText = 'Mục tiêu đầu tiên sẽ tự động làm Mục tiêu gốc (0.1–10.0)';
            targetHint.className = 'text-muted';
        }
    }
}

function editGoal(id, title, targetVal, isBase, isDone, deadline) {
    document.getElementById('editGoalId').value = id;
    document.getElementById('edit-goal-title').value = title;
    const targetInput = document.getElementById('edit-goal-target');
    const targetHint = document.getElementById('edit-goal-target-hint');
    const noticeEl = document.getElementById('editGoalNotice');

    targetInput.value = targetVal;
    
    if (isBase) {
        targetInput.disabled = true;
        targetHint.innerText = 'Mục tiêu gốc là cố định, không thể sửa điểm';
        noticeEl.innerHTML = `
            <div class="alert alert-primary py-2 px-3 mb-3" style="font-size:12px;">
                <i class="bi bi-shield-lock me-1"></i><strong>Mục tiêu gốc (Cố định):</strong> Bạn có thể sửa tên hoặc hạn hoàn thành nhưng không thể sửa điểm mục tiêu.
            </div>`;
    } else {
        targetInput.disabled = false;
        targetHint.innerText = 'Điểm mục tiêu (phải lớn hơn mục tiêu gốc)';
        if (isDone) {
            noticeEl.innerHTML = `
                <div class="alert alert-warning py-2 px-3 mb-3" style="font-size:12px;">
                    <i class="bi bi-exclamation-triangle-fill me-1"></i><strong>Nấc đã hoàn thành:</strong> Khi bạn sửa điểm mục tiêu mới, thành tích cũ sẽ được tự động lưu vào Lịch sử thành tích và nấc mục tiêu sẽ chuyển về trạng thái Đang thực hiện.
                </div>`;
        } else {
            noticeEl.innerHTML = `
                <div class="alert alert-info py-2 px-3 mb-3" style="font-size:12px;">
                    <i class="bi bi-info-circle me-1"></i>Điểm của nấc bổ sung phải lớn hơn điểm mục tiêu gốc và nằm trong khoảng (0, 10].
                </div>`;
        }
    }

    document.getElementById('edit-goal-deadline').value = deadline || '';
    new bootstrap.Modal(document.getElementById('editGoalModal')).show();
}

async function confirmDeleteAdditionalGoal(goalId, targetVal) {
    if (!confirm(`Xác nhận xóa nấc mục tiêu ${targetVal}?\n\nLưu ý: Các hoạt động đã hoàn thành sẽ được giữ lại trên Lịch học cá nhân để bảo tồn lịch sử học tập.`)) return;
    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/DeleteGoal', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ GoalId: goalId })
        });
        const res = await resp.json();
        if (res.ok) window.location.reload();
        else alert("Lỗi khi xóa: " + res.error);
    } catch (e) {
        alert("Lỗi kết nối khi xóa nấc mục tiêu.");
    }
}

async function moveGoalOrder(userSubjectId, goalId, dir) {
    const listEl = document.getElementById('goal-list-' + userSubjectId);
    if (!listEl) return;
    const goalLaddersData = (window.GradesState && window.GradesState.goalLadders) ? window.GradesState.goalLadders : [];
    const ladder = (goalLaddersData || []).find(l => (l.userSubjectId || l.UserSubjectId) == userSubjectId);
    if (!ladder) return;
    const addGoals = ladder.additionalGoals || ladder.AdditionalGoals;
    if (!addGoals) return;

    const goals = [...addGoals];
    const idx = goals.findIndex(g => (g.id || g.Id) == goalId);
    if (idx < 0) return;
    if (dir === -1 && idx === 0) return;
    if (dir === 1 && idx === goals.length - 1) return;

    const targetIdx = idx + dir;
    const temp = goals[idx];
    goals[idx] = goals[targetIdx];
    goals[targetIdx] = temp;

    const items = goals.map((g, i) => ({
        GoalId: g.id || g.Id,
        NewDisplayOrder: i + 1
    }));

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/ReorderGoals', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(items)
        });
        const res = await resp.json();
        if (res.ok) window.location.reload();
        else alert(res.error);
    } catch (e) {
        alert("Lỗi kết nối");
    }
}

// ── Goal Activity AJAX ───────────────────────────────────────
function toLocalInputValue(date) {
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function openAddActivityModal(goalId) {
    document.getElementById('addActivityGoalId').value = goalId;
    document.getElementById('add-activity-title').value = '';
    // Bắt đầu ở đầu giờ kế tiếp, kéo dài 1 giờ; Date tự chuyển sang ngày hôm sau khi qua 23:00.
    const start = new Date();
    start.setMinutes(0, 0, 0);
    start.setHours(start.getHours() + 1);
    const end = new Date(start.getTime() + 60 * 60 * 1000);
    document.getElementById('add-activity-start').value = toLocalInputValue(start);
    document.getElementById('add-activity-end').value = toLocalInputValue(end);
    document.getElementById('add-activity-desc').value = '';
    new bootstrap.Modal(document.getElementById('addActivityModal')).show();
}

async function submitAddActivity(e) {
    e.preventDefault();
    const goalId = parseInt(document.getElementById('addActivityGoalId').value);
    const title = document.getElementById('add-activity-title').value.trim();
    const start = document.getElementById('add-activity-start').value;
    const end = document.getElementById('add-activity-end').value;
    const desc = document.getElementById('add-activity-desc').value.trim();

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/AddGoalActivity', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ GoalId: goalId, Title: title, StartAtLocal: start, EndAtLocal: end, Description: desc })
        });
        const res = await resp.json();
        if (res.ok) window.location.reload();
        else alert("Lỗi: " + res.error);
    } catch (err) {
        alert("Lỗi kết nối khi tạo hoạt động.");
    }
}

function openEditActivityModal(id, title, start, end, desc) {
    document.getElementById('editActivityId').value = id;
    document.getElementById('edit-activity-title').value = title;
    document.getElementById('edit-activity-start').value = start;
    document.getElementById('edit-activity-end').value = end;
    document.getElementById('edit-activity-desc').value = desc || '';
    new bootstrap.Modal(document.getElementById('editActivityModal')).show();
}

async function submitEditActivity(e) {
    e.preventDefault();
    const id = parseInt(document.getElementById('editActivityId').value);
    const title = document.getElementById('edit-activity-title').value.trim();
    const start = document.getElementById('edit-activity-start').value;
    const end = document.getElementById('edit-activity-end').value;
    const desc = document.getElementById('edit-activity-desc').value.trim();

    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/UpdateGoalActivity', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ ActivityId: id, Title: title, StartAtLocal: start, EndAtLocal: end, Description: desc })
        });
        const res = await resp.json();
        if (res.ok) window.location.reload();
        else alert("Lỗi: " + res.error);
    } catch (err) {
        alert("Lỗi kết nối khi sửa hoạt động.");
    }
}

async function deleteActivity(id) {
    if (!confirm("Bạn có chắc chắn muốn xóa hoạt động này? Hoạt động cũng sẽ được gỡ khỏi Lịch học cá nhân.")) return;
    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/DeleteGoalActivity', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ ActivityId: id })
        });
        const res = await resp.json();
        if (res.ok) window.location.reload();
        else alert("Lỗi: " + res.error);
    } catch (err) {
        alert("Lỗi kết nối khi xóa hoạt động.");
    }
}

async function toggleActivity(id, goalId, iconEl) {
    try {
        const token = window.GradesState ? window.GradesState.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
        const resp = await fetch('/Grades/ToggleGoalActivity', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({ ActivityId: id })
        });
        const res = await resp.json();
        if (res.ok) {
            const row = iconEl ? iconEl.closest('.activity-row') : document.getElementById('activity-item-' + id);
            if (row) {
                const titleSpan = row.querySelector('.activity-title');
                const badgeSpan = row.querySelector('.activity-badge');
                if (res.isCompleted) {
                    if (iconEl) {
                        iconEl.className = 'bi bi-check-circle-fill text-success fs-5 cursor-pointer flex-shrink-0';
                        iconEl.title = 'Nhấn để đổi trạng thái';
                    }
                    if (titleSpan) {
                        titleSpan.className = 'text-truncate text-decoration-line-through text-muted activity-title';
                        titleSpan.style.color = '';
                    }
                    if (badgeSpan) {
                        badgeSpan.className = 'badge activity-badge activity-badge-done';
                        badgeSpan.textContent = 'Đã xong';
                    }
                } else {
                    if (iconEl) {
                        iconEl.className = 'bi bi-circle text-muted fs-5 cursor-pointer flex-shrink-0';
                        iconEl.title = 'Nhấn để đánh dấu hoàn thành';
                    }
                    if (titleSpan) {
                        titleSpan.className = 'text-truncate fw-medium activity-title';
                        titleSpan.style.color = 'var(--sh-slate-800)';
                    }
                    if (badgeSpan) {
                        badgeSpan.className = 'badge activity-badge activity-badge-todo';
                        badgeSpan.textContent = 'To do';
                    }
                }
            }
            // Cập nhật bộ đếm tiến độ hoàn thành dạng 1/3 hoàn thành
            if (goalId) {
                const list = document.getElementById('activities-list-' + goalId);
                const progressEl = document.getElementById('activity-progress-' + goalId);
                if (list && progressEl) {
                    const allRows = list.querySelectorAll('.activity-row');
                    const doneRows = list.querySelectorAll('.activity-row .bi-check-circle-fill');
                    progressEl.textContent = `${doneRows.length}/${allRows.length} hoàn thành`;
                }
            }
        } else {
            alert("Lỗi: " + (res.error || res.message || "Không thể cập nhật"));
        }
    } catch (err) {
        alert("Lỗi kết nối khi cập nhật hoạt động.");
    }
}

