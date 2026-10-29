(() => {
  'use strict';
  const page = document.getElementById('group-details-page');
  if (page?.dataset.isMember !== 'true') return;
  const base = `/api/groups/${encodeURIComponent(page.dataset.groupId)}/learning`;
  const token = document.querySelector('#group-antiforgery input')?.value;
  const dialog = document.getElementById('gl-dialog');
  const form = document.getElementById('gl-form');
  const saveButton = document.getElementById('gl-dialog-save');
  const errorBox = document.getElementById('gl-form-error');
  const weekInput = document.getElementById('gl-week');
  let state, saveForm, busy = false, loadSequence = 0;
  //c
  const notesHost = document.querySelector(
  '[data-notesv3-group-resources]'
);

let notesResources = {
  pdfs: [],
  notebooks: []
};

let notesResourcesError = '';
let notesApiModule;

function notesUrl(path) {
  const prefix = notesHost?.dataset.appRoot || '/';
  return prefix + path.replace(/^\/+/, '');
}

async function notesApi(path, options) {
  if (!notesHost) {
    throw new Error('Không tìm thấy vùng tài nguyên NotesV3.');
  }

  notesApiModule ??= import(
    notesUrl('js/notesv3/notes-ui.js')
  );

  const module = await notesApiModule;
  return module.api(path, options);
}

async function refreshNotesResources() {
  if (!notesHost) return;

  try {
    notesResources = await notesApi(
      `groups/${encodeURIComponent(page.dataset.groupId)}/resources`
    );

    notesResourcesError = '';
  } catch (error) {
    notesResources = {
      pdfs: [],
      notebooks: []
    };

    notesResourcesError = error.message;
  }

  renderResources();
}
  //end
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const date = value => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium', timeStyle: 'short' });
  const localInput = value => new Date(new Date(value).getTime() + 7 * 3600000).toISOString().slice(0, 16);
  const utc = value => new Date(`${value}:00+07:00`).toISOString();
  const tomorrow = () => localInput(Date.now() + 86400000);
  const button = (action, label, id = '', extra = '', primary = false) => `<button type="button" class="${primary ? 'btn-primary-sh' : 'btn-secondary-sh'}" data-gl-action="${action}" data-id="${esc(id)}" ${extra}>${label}</button>`;
  const empty = text => `<div class="gl-empty">${esc(text)}</div>`;
  const badge = (text, style = '') => `<span class="gl-badge ${style}">${esc(text)}</span>`;
  const input = (name, label, value = '', type = 'text', max = 200, required = false) => `<label>${label}<input class="sh-input" name="${name}" type="${type}" value="${esc(value)}" maxlength="${max}" ${required ? 'required' : ''}></label>`;
  const area = (name, label, value = '', max = 4000, required = false) => `<label>${label}<textarea class="sh-input" name="${name}" rows="3" maxlength="${max}" ${required ? 'required' : ''}>${esc(value)}</textarea></label>`;
  const check = (name, label, checked = false) => `<label><input type="checkbox" name="${name}" ${checked ? 'checked' : ''}>${label}</label>`;
  const select = (name, label, options) => `<label>${label}<select class="sh-input" name="${name}">${options}</select></label>`;
  const option = (value, label, selected = false) => `<option value="${esc(value)}" ${selected ? 'selected' : ''}>${esc(label)}</option>`;
  const webLink = (url, label) => {
    try { if (!['http:', 'https:'].includes(new URL(url).protocol)) return ''; } catch { return ''; }
    return `<a href="${esc(url)}" target="_blank" rel="noopener noreferrer">${esc(label)}</a>`;
  };
  const submissionStatus = value => ({ Submitted: 'Đã nộp', Completed: 'Hoàn thành', NeedsRevision: 'Cần sửa' }[value] || value);

  async function api(path = '', method = 'GET', body) {
    const controller = method === 'GET' ? new AbortController() : null;
    const timeout = controller ? setTimeout(() => controller.abort(), 15000) : null;
    try {
      const response = await fetch(base + path, {
        method, credentials: 'same-origin', signal: controller?.signal,
        headers: { Accept: 'application/json', ...(method !== 'GET' ? { 'Content-Type': 'application/json', RequestVerificationToken: token } : {}) },
        ...(body === undefined ? {} : { body: JSON.stringify(body) })
      });
      const result = await response.json().catch(error => {
        if (controller?.signal.aborted) throw error;
        return null;
      });
      if (!response.ok || response.redirected || result === null) {
        const validation = result?.errors && Object.values(result.errors).flat().join(' ');
        throw new Error(result?.message || validation || 'Không tải được dữ liệu. Hãy kiểm tra đăng nhập và thử lại.');
      }
      return result;
    } catch (error) {
      if (controller?.signal.aborted) throw new Error('Tải dữ liệu quá lâu. Vui lòng thử lại.');
      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }

  const panelRenderers = {
    sessions: renderSessions, assignments: renderAssignments, questions: renderQuestions,
    resources: renderResources, progress: renderProgress
  };
  // function renderLoadError(panel, message) {
  //   panel.innerHTML = empty(message) + button('refresh', 'Thử lại');
  // }

  //c
  function renderLoadError(panel, message) {
  if (panel.id === 'gl-resources' && notesHost) {
    renderResources();
    return;
  }

  panel.innerHTML =
    empty(message) +
    button('refresh', 'Thử lại');
}
  //end
  async function load() {
    const sequence = ++loadSequence;
    try {
      const data = await api(weekInput.value ? `?week=${encodeURIComponent(weekInput.value)}` : '');
      if (sequence !== loadSequence) return;
      state = data;
      // Each tab must finish loading even if another tab cannot render.
      for (const [id, render] of Object.entries(panelRenderers)) {
        const panel = document.getElementById(`gl-${id}`);
        if (!panel) {
          console.error(`Study Group: missing gl-${id}`);
          continue;
        }
        try { render(); }
        catch (error) {
          console.error(`Study Group: cannot render ${id}`, error);
          renderLoadError(panel, 'Không hiển thị được dữ liệu. Vui lòng thử lại.');
        }
      }
    } catch (error) {
      if (sequence !== loadSequence) return;
      for (const id of Object.keys(panelRenderers)) {
        const panel = document.getElementById(`gl-${id}`);
        if (panel) renderLoadError(panel, error.message);
      }
      throw error;
    }
  }
  function showError(error) { window.showToast?.(error.message, 'error'); }
  function openForm(title, fields, onSave, label = 'Lưu') {
    document.getElementById('gl-dialog-title').textContent = title;
    document.getElementById('gl-form-fields').innerHTML = fields;
    errorBox.hidden = true;
    saveButton.textContent = label;
    saveForm = onSave;
    dialog.showModal();
  }
  document.getElementById('gl-dialog-close').addEventListener('click', () => { if (!busy) dialog.close(); });
  document.getElementById('gl-dialog-cancel').addEventListener('click', () => { if (!busy) dialog.close(); });
  dialog.addEventListener('cancel', e => { if (busy) e.preventDefault(); });
  form.addEventListener('submit', async e => {
    e.preventDefault();
    if (busy) return;
    busy = true; saveButton.disabled = true; errorBox.hidden = true;
    try {
      await saveForm(new FormData(form));
      dialog.close();
      window.showToast?.('Đã lưu thay đổi.', 'success');
      try { await load(); } catch (error) { showError(new Error(`Đã lưu, nhưng chưa tải lại được dữ liệu: ${error.message}`)); }
    } catch (error) { errorBox.textContent = error.message; errorBox.hidden = false; }
    finally { busy = false; saveButton.disabled = false; }
  });

  function renderSessions() {
    const now = Date.now();
    const sessions = [...state.sessions].sort((a, b) => {
      const futureA = a.status === 'Scheduled' && new Date(a.endsAtUtc) > now;
      const futureB = b.status === 'Scheduled' && new Date(b.endsAtUtc) > now;
      return Number(futureB) - Number(futureA) || (futureA ? new Date(a.startsAtUtc) - new Date(b.startsAtUtc) : new Date(b.startsAtUtc) - new Date(a.startsAtUtc));
    });
    document.getElementById('gl-sessions').innerHTML = (state.canManage ? button('session-new', '+ Tạo buổi học', '', '', true) : '') +
      (sessions.length ? `<div class="gl-list">${sessions.map(s => {
        const scheduled = s.status === 'Scheduled';
        const started = new Date(s.startsAtUtc).getTime() <= now;
        const registered = s.attendees.some(a => a.userId === state.currentUserId);
        return `<article class="gl-card">
          <h3>${esc(s.title)}</h3>${badge({ Scheduled: 'Đã lên lịch', Completed: 'Đã kết thúc', Cancelled: 'Đã hủy' }[s.status], s.status === 'Completed' ? 'good' : '')}
          <div class="gl-meta">${date(s.startsAtUtc)} → ${date(s.endsAtUtc)}</div>
          <p><strong>Mục tiêu:</strong> ${esc(s.objective)}</p>
          ${s.preparation ? `<p><strong>Chuẩn bị:</strong> ${esc(s.preparation)}</p>` : ''}
          ${s.location ? `<p>📍 ${esc(s.location)}</p>` : ''}${s.meetingUrl ? webLink(s.meetingUrl, 'Mở liên kết buổi học ↗') : ''}
          <div class="gl-meta">${s.attendees.length} đăng ký · ${s.attendees.filter(a => a.attended).length} có mặt</div>
          ${s.attendees.length ? `<details><summary>Người tham gia</summary><p class="mt-2">${s.attendees.map(a => `${esc(a.name)}${a.attended ? ' ✓' : ''}`).join(' · ')}</p></details>` : ''}
          ${s.summary ? `<p class="mt-3"><strong>${s.status === 'Cancelled' ? 'Lý do hủy' : 'Tổng kết'}:</strong> ${esc(s.summary)}</p>` : ''}
          <div class="gl-actions">
          ${scheduled && !started ? button('session-register', registered ? 'Hủy đăng ký' : 'Đăng ký tham gia', s.id, `data-registered="${!registered}"`, !registered) : ''}
          ${state.canManage && scheduled && !started ? button('session-edit', 'Sửa lịch', s.id) : ''}
          ${state.canManage && s.status !== 'Cancelled' && started ? button('attendance', 'Điểm danh', s.id) : ''}
          ${state.canManage && scheduled && started ? button('session-complete', 'Kết thúc & tổng kết', s.id) : ''}
          ${state.canManage && scheduled ? button('session-cancel', 'Hủy buổi học', s.id) : ''}</div></article>`;
      }).join('')}</div>` : empty('Chưa có buổi học. Người quản lý có thể tạo lịch học đầu tiên.'));
  }

  function renderAssignments() {
    document.getElementById('gl-assignments').innerHTML = (state.canManage ? button('assignment-new', '+ Giao bài tập', '', '', true) : '') +
      (state.assignments.length ? `<div class="gl-list">${state.assignments.map(a => {
        const mine = a.submissions.filter(s => s.userId === state.currentUserId).sort((x, y) => y.version - x.version)[0];
        const canSubmit = a.status === 'Open' && a.assignees.some(x => x.userId === state.currentUserId) && mine?.status !== 'Completed' && (a.allowLateSubmission || new Date(a.dueAtUtc) >= Date.now());
        const latestIds = new Set(a.assignees.map(m => a.submissions.filter(s => s.userId === m.userId).sort((x, y) => y.version - x.version)[0]?.id));
        const session = state.sessions.find(s => s.id === a.sessionId);
        return `<article class="gl-card"><h3>${esc(a.title)}</h3>${badge(a.status === 'Open' ? 'Đang mở' : 'Đã đóng', a.status === 'Open' ? '' : 'muted')}
          ${mine ? badge(submissionStatus(mine.status), mine.status === 'Completed' ? 'good' : '') : ''}
          <div class="gl-meta">Hạn nộp: ${date(a.dueAtUtc)} · ${a.allowLateSubmission ? 'Cho phép nộp muộn' : 'Không nhận bài muộn'}</div>
          ${session ? `<div class="gl-meta">Buổi học: ${esc(session.title)}</div>` : ''}<p>${esc(a.description)}</p>
          <details><summary>Giao cho ${a.assignees.length} người</summary><p class="mt-2">${a.assignees.map(m => esc(m.name)).join(' · ')}</p></details>
          <div class="gl-actions">${canSubmit ? button('submit', mine ? 'Nộp phiên bản mới' : 'Nộp bài', a.id, '', true) : ''}
          ${state.canManage && a.status === 'Open' ? button('assignment-edit', 'Sửa bài tập', a.id) + button('assignment-close', 'Đóng nhận bài', a.id) : ''}</div>
          <details class="mt-3"><summary>${state.canManage ? 'Bài nộp và lịch sử phản hồi' : 'Lịch sử bài nộp của bạn'} (${a.submissions.length})</summary>
          ${a.submissions.map(s => `<div class="gl-answer"><strong>${esc(s.name)} · Lần ${s.version}</strong> ${badge(submissionStatus(s.status), s.status === 'Completed' ? 'good' : '')}
            <div class="gl-meta">${date(s.submittedAtUtc)}${s.isLate ? ' · Nộp muộn' : ''}</div><p>${esc(s.content)}</p>
            ${s.resourceUrl ? webLink(s.resourceUrl, 'Xem bài làm ↗') : ''}${s.feedback ? `<p><strong>Phản hồi:</strong> ${esc(s.feedback)}</p>` : ''}
            ${state.canManage && latestIds.has(s.id) ? `<div class="gl-actions">${button('review', 'Nhận xét / đánh giá', a.id, `data-submission="${s.id}"`)}</div>` : ''}</div>`).join('') || empty('Chưa có bài nộp.')}</details></article>`;
      }).join('')}</div>` : empty('Chưa có bài tập được giao.'));
  }

  function renderResources() {
  const container = document.getElementById('gl-resources');
  if (!container) return;

  // Giữ tài nguyên flashcard từ module Group hiện có.
  // Notes/PDF cũ không còn được render tại đây.
  const flashcards = (state?.resources || [])
    .filter(resource => resource.kind === 'Flashcard');

  const pdfs = notesResources.pdfs || [];
  const notebooks = notesResources.notebooks || [];

  const total = flashcards.length + pdfs.length + notebooks.length;

  const counter = document.getElementById('gl-resource-count');
  if (counter) counter.textContent = String(total);

  const toolbar = `
    <div class="gl-actions">
      ${button('resource-share', '+ Thêm tài nguyên', '', '', true)}
      ${button('notes-resources-refresh', 'Làm mới')}
    </div>
  `;

  const pdfCards = pdfs.map(pdf => `
    <article class="gl-card">
      <h3>${esc(pdf.title)}</h3>

      <div class="gl-meta">
        PDF · ${esc(pdf.fileName)}
        ${pdf.pageCount ? ` · ${esc(pdf.pageCount)} trang` : ''}
      </div>

      <div class="gl-actions">
        <a class="btn-primary-sh"
           href="${esc(notesUrl(
             `NotesV3/Pdf/${pdf.documentId}?shareId=${pdf.shareId}`
           ))}">
          Mở PDF
        </a>

        <a class="btn-secondary-sh"
           href="${esc(notesUrl(
             `NotesV3/Book/${pdf.documentId}?shareId=${pdf.shareId}`
           ))}">
          Notes Book
        </a>

        ${pdf.canUnshare
          ? button(
              'notes-pdf-unshare',
              'Gỡ chia sẻ',
              pdf.shareId
            )
          : ''}
      </div>
    </article>
  `).join('');

  const notebookCards = notebooks.map(notebook => `
    <article class="gl-card">
      <h3>${esc(notebook.title)}</h3>

      <div class="gl-meta">
        Notebook nhóm · Thành viên cùng chỉnh sửa
      </div>

      <div class="gl-actions">
        <a class="btn-primary-sh"
           href="${esc(notesUrl(
             `NotesV3/Notebook/${notebook.id}`
           ))}">
          Mở notebook
        </a>

        ${button(
          'notes-notebook-delete',
          'Xóa notebook nhóm',
          notebook.id
        )}
      </div>
    </article>
  `).join('');

  const flashcardCards = flashcards.map(resource => `
    <article class="gl-card">
      <h3>${esc(resource.title)}</h3>

      <div class="gl-meta">
        Flashcard · ${esc(resource.ownerName)}
      </div>

      <div class="gl-actions">
        <a class="btn-primary-sh"
           href="${esc(notesUrl(
             `Groups/${page.dataset.groupId}` +
             `/Resources/Flashcard/${resource.id}`
           ))}">
          Mở flashcard
        </a>

        ${state?.canManage ||
          resource.ownerUserId === state?.currentUserId
          ? button(
              'resource-unshare',
              'Gỡ chia sẻ',
              resource.id,
              'data-kind="Flashcard"'
            )
          : ''}
      </div>
    </article>
  `).join('');

  const warning = notesResourcesError
    ? `<p class="text-danger" role="alert">
         ${esc(notesResourcesError)}
       </p>`
    : '';

  const content = total > 0
    ? `<div class="gl-list">
         ${pdfCards}
         ${notebookCards}
         ${flashcardCards}
       </div>`
    : empty(
        'Chưa có tài nguyên. Chọn “Thêm tài nguyên” để bắt đầu.'
      );

  container.innerHTML = toolbar + warning + content;
}

  function renderQuestions() {
    const term = document.getElementById('gl-question-search').value.trim().toLocaleLowerCase('vi');
    const questions = state.questions.filter(q => `${q.title} ${q.subject} ${q.body}`.toLocaleLowerCase('vi').includes(term));
    document.getElementById('gl-questions').innerHTML = button('question-new', '+ Đặt câu hỏi', '', '', true) +
      (questions.length ? `<div class="gl-list">${questions.map(q => {
        const canAccept = state.canManage || q.authorUserId === state.currentUserId;
        return `<article class="gl-card"><h3>${esc(q.title)}</h3>${badge(q.subject)}${badge(q.acceptedAnswerId ? 'Đã giải quyết' : 'Đang trao đổi', q.acceptedAnswerId ? 'good' : '')}
          <div class="gl-meta">${esc(q.authorName)} · ${date(q.createdAtUtc)}</div><p>${esc(q.body)}</p>
          <details><summary>${q.answers.length} câu trả lời</summary>${q.answers.map(a => `<div class="gl-answer ${a.id === q.acceptedAnswerId ? 'accepted' : ''}">
            <strong>${esc(a.authorName)}</strong>${a.id === q.acceptedAnswerId ? badge('Câu trả lời được chọn', 'good') : ''}
            <div class="gl-meta">${date(a.createdAtUtc)}</div><p>${esc(a.body)}</p>
            ${canAccept ? button('answer-accept', a.id === q.acceptedAnswerId ? 'Bỏ chọn' : 'Chọn câu trả lời', q.id, `data-answer="${a.id === q.acceptedAnswerId ? '' : a.id}"`) : ''}</div>`).join('')}</details>
          <div class="gl-actions">${button('answer', 'Viết câu trả lời', q.id)}</div></article>`;
      }).join('')}</div>` : empty(term ? 'Không tìm thấy câu hỏi phù hợp.' : 'Chưa có câu hỏi. Hãy chia sẻ điều bạn đang thắc mắc.'));
  }

  function renderProgress() {
    const p = state.progress, goal = p.goal;
    weekInput.value = p.weekStart;
    const rate = p.assigned ? Math.round(p.completed * 100 / p.assigned) : 0;
    const metric = (value, label) => `<div class="gl-metric"><strong>${value}</strong><span>${label}</span></div>`;
    document.getElementById('gl-progress').innerHTML = `${state.canManage ? button('goal', goal ? 'Sửa mục tiêu tuần' : '+ Đặt mục tiêu tuần', '', '', true) : ''}
      <div class="gl-metrics">${metric(p.completedSessions, 'Buổi học đã kết thúc')}${metric(`${p.submitted}/${p.assigned}`, 'Lượt giao bài đã được nộp')}${metric(`${p.completed}/${p.assigned}`, 'Lượt giao bài đã hoàn thành')}${metric(`${rate}%`, 'Tỷ lệ hoàn thành')}</div>
      ${goal ? `<article class="gl-card"><h3>${esc(goal.title)}</h3>
        ${goal.targetSessions ? `<p>Buổi học: ${p.completedSessions}/${goal.targetSessions}</p><progress aria-label="Mục tiêu buổi học" value="${Math.min(p.completedSessions, goal.targetSessions)}" max="${goal.targetSessions}"></progress>` : ''}
        ${goal.targetAssignments ? `<p>Bài tập mà mọi người được giao đã hoàn thành: ${p.fullyCompletedAssignments}/${goal.targetAssignments}</p><progress aria-label="Mục tiêu bài tập" value="${Math.min(p.fullyCompletedAssignments, goal.targetAssignments)}" max="${goal.targetAssignments}"></progress>` : ''}</article>` : empty('Tuần này chưa đặt mục tiêu.')}
      <div class="gl-card mt-3"><h3>${state.canManage ? 'Tiến độ thành viên hiện tại' : 'Tiến độ của bạn'}</h3><div class="gl-table-scroll"><table class="gl-table"><thead><tr><th>Thành viên</th><th>Có mặt / đăng ký*</th><th>Đã nộp / được giao</th><th>Hoàn thành</th></tr></thead>
      <tbody>${p.members.map(m => `<tr><td>${esc(m.name)}</td><td>${m.attendedSessions}/${m.registeredSessions}</td><td>${m.submitted}/${m.assigned}</td><td>${m.completed}</td></tr>`).join('')}</tbody></table></div>
      <p class="gl-meta">* Chỉ tính buổi đã kết thúc trong tuần. Bài tập tính theo hạn nộp; mỗi người dùng phiên bản nộp mới nhất. Tổng nhóm giữ cả lịch sử thành viên đã rời nhóm.</p></div>`;
  }

  function sessionForm(session) {
    openForm(session ? 'Sửa buổi học' : 'Tạo buổi học nhóm',
      input('title', 'Tên buổi học *', session?.title, 'text', 200, true) + area('objective', 'Mục tiêu đầu ra *', session?.objective, 1000, true) +
      input('start', 'Bắt đầu (giờ Việt Nam) *', session ? localInput(session.startsAtUtc) : tomorrow(), 'datetime-local', 100, true) +
      input('end', 'Kết thúc (giờ Việt Nam) *', session ? localInput(session.endsAtUtc) : localInput(Date.now() + 90000000), 'datetime-local', 100, true) +
      input('location', 'Địa điểm', session?.location, 'text', 300) + input('url', 'Link họp (HTTP/HTTPS)', session?.meetingUrl, 'url', 2000) +
      area('preparation', 'Nội dung và tài liệu cần chuẩn bị', session?.preparation),
      data => api(`/sessions${session ? `/${session.id}` : ''}`, session ? 'PUT' : 'POST', {
        title: data.get('title'), objective: data.get('objective'), startsAtUtc: utc(data.get('start')), endsAtUtc: utc(data.get('end')),
        location: data.get('location'), meetingUrl: data.get('url'), preparation: data.get('preparation'), revision: session?.revision || 0
      }));
  }
  function assignmentForm(assignment) {
    openForm(assignment ? 'Sửa bài tập' : 'Giao bài tập',
      input('title', 'Tiêu đề *', assignment?.title, 'text', 200, true) + area('description', 'Đề bài và yêu cầu *', assignment?.description, 8000, true) +
      input('due', 'Hạn nộp (giờ Việt Nam) *', assignment ? localInput(assignment.dueAtUtc) : tomorrow(), 'datetime-local', 100, true) +
      select('session', 'Gắn với buổi học', option('', 'Không gắn buổi học') + state.sessions.filter(s => s.status !== 'Cancelled').map(s => option(s.id, s.title, s.id === assignment?.sessionId)).join('')) +
      check('late', 'Cho phép nộp muộn', assignment?.allowLateSubmission) +
      (assignment ? '<p class="gl-meta">Danh sách người nhận được giữ nguyên từ lúc giao bài.</p>' : `<fieldset><legend>Người nhận</legend><small>Không chọn ai để giao cho tất cả thành viên hiện tại. Người vào sau không tự động nhận bài.</small>${state.members.map(m => `<label><input type="checkbox" name="assignee" value="${m.userId}">${esc(m.name)}</label>`).join('')}</fieldset>`),
      data => api(`/assignments${assignment ? `/${assignment.id}` : ''}`, assignment ? 'PUT' : 'POST', {
        title: data.get('title'), description: data.get('description'), dueAtUtc: utc(data.get('due')), sessionId: Number(data.get('session')) || null,
        allowLateSubmission: data.has('late'), assigneeIds: data.getAll('assignee').map(Number), revision: assignment?.revision || 0
      }));
  }
  //c
  async function openNotesResourceForm() {
  const [pdfs, notebooks] = await Promise.all([
    notesApi('pdfs'),
    notesApi('notebooks')
  ]);

  const flashcards = (state?.availableResources || [])
    .filter(resource => resource.kind === 'Flashcard');

  const resourceOptions = [
    option('', 'Chọn loại hoặc tài nguyên'),
    option('new-notebook', 'Tạo notebook nhóm mới'),
    option('upload-pdf', 'Tải PDF mới và chia sẻ vào nhóm'),

    ...pdfs.map(pdf => option(
      `pdf/${pdf.documentId}`,
      `PDF: ${pdf.title}`
    )),

    ...notebooks.map(notebook => option(
      `notebook/${notebook.id}`,
      `Chép notebook cá nhân: ${notebook.title}`
    )),

    ...flashcards.map(deck => option(
      `flashcard/${deck.id}`,
      `Flashcard: ${deck.title}`
    ))
  ].join('');

  let uploadedDocumentId = null;

  const fields =
    select('resource', 'Tài nguyên', resourceOptions) +

    `<div data-notes-title-field hidden>
       ${input(
         'notesTitle',
         'Tên hiển thị',
         '',
         'text',
         200
       )}
     </div>` +

    `<div data-notes-file-field hidden>
       <label>
         PDF tối đa 25 MiB
         <input class="sh-input"
                name="notesPdf"
                type="file"
                accept=".pdf,application/pdf">
       </label>
     </div>` +

    `<p class="gl-meta" data-notes-resource-help>
       PDF được chia sẻ bằng quyền truy cập.
       Notebook cá nhân được chép thành bản riêng của nhóm.
     </p>`;

  openForm(
    'Thêm tài nguyên vào nhóm',
    fields,
    async data => {
      const selected = String(data.get('resource') || '');

      if (!selected) {
        throw new Error('Hãy chọn tài nguyên.');
      }

      const groupId = page.dataset.groupId;

      if (selected === 'new-notebook') {
        const title = String(
          data.get('notesTitle') || ''
        ).trim();

        if (!title) {
          throw new Error('Hãy nhập tên notebook nhóm.');
        }

        await notesApi('notebooks', {
          method: 'POST',
          body: {
            title,
            groupId: Number(groupId)
          }
        });
      } else if (selected === 'upload-pdf') {
        if (!uploadedDocumentId) {
          const file = data.get('notesPdf');

          if (!(file instanceof File) || file.size === 0) {
            throw new Error('Hãy chọn một file PDF.');
          }

          if (file.size > 25 * 1024 * 1024) {
            throw new Error('PDF tối đa 25 MiB.');
          }

          const upload = new FormData();
          upload.append('File', file);

          const title = String(
            data.get('notesTitle') || ''
          ).trim();

          if (title) {
            upload.append('Title', title);
          }

          const result = await notesApi('pdfs', {
            method: 'POST',
            body: upload
          });

          uploadedDocumentId = result.documentId;
        }

        try {
          await notesApi(
            `groups/${groupId}/pdf-shares`,
            {
              method: 'POST',
              body: {
                documentId: uploadedDocumentId
              }
            }
          );
        } catch (error) {
          throw new Error(
            'PDF đã được tải vào thư viện cá nhân, ' +
            'nhưng chưa chia sẻ được vào nhóm. ' +
            'Bạn có thể thử lưu lại. ' +
            error.message
          );
        }
      } else {
        const [kind, rawId] = selected.split('/');
        const id = Number(rawId);

        if (!Number.isSafeInteger(id) || id <= 0) {
          throw new Error('Tài nguyên không hợp lệ.');
        }

        if (kind === 'pdf') {
          await notesApi(
            `groups/${groupId}/pdf-shares`,
            {
              method: 'POST',
              body: { documentId: id }
            }
          );
        } else if (kind === 'notebook') {
          await copyNotebookResource(id, groupId);
        } else if (kind === 'flashcard') {
          // Giữ API flashcard hiện có.
          await api(
            `/resources/Flashcard/${id}`,
            'PUT'
          );
        } else {
          throw new Error('Loại tài nguyên không hợp lệ.');
        }
      }

      await refreshNotesResources();
    },
    'Thêm tài nguyên'
  );

  const resourcePicker = form.elements.namedItem('resource');
  const titleField = form.querySelector('[data-notes-title-field]');
  const fileField = form.querySelector('[data-notes-file-field]');
  const titleInput = form.elements.namedItem('notesTitle');
  const fileInput = form.elements.namedItem('notesPdf');
  const help = form.querySelector('[data-notes-resource-help]');

  function updateFields() {
    const selected = resourcePicker.value;

    titleField.hidden = ![
      'new-notebook',
      'upload-pdf'
    ].includes(selected);

    fileField.hidden = selected !== 'upload-pdf';

    titleInput.required = selected === 'new-notebook';

    fileInput.required =
      selected === 'upload-pdf' &&
      !uploadedDocumentId;

    if (selected.startsWith('notebook/')) {
      help.textContent =
        'Tạo một bản notebook riêng cho nhóm. ' +
        'Các chỉnh sửa sau này không ảnh hưởng bản cá nhân.';
    } else if (
      selected === 'upload-pdf' ||
      selected.startsWith('pdf/')
    ) {
      help.textContent =
        'Nhóm dùng chung file PDF gốc. ' +
        'Ghi chú nhóm và ghi chú cá nhân được tách riêng.';
    } else if (selected.startsWith('flashcard/')) {
      help.textContent =
        'Flashcard sử dụng cơ chế chia sẻ hiện có của nhóm.';
    } else {
      help.textContent =
        'Notebook nhóm cho phép các thành viên đang hoạt động ' +
        'cùng chỉnh sửa.';
    }
  }

  resourcePicker.addEventListener('change', updateFields);

  fileInput.addEventListener('change', () => {
    uploadedDocumentId = null;
    updateFields();
  });

  updateFields();
}

async function copyNotebookResource(
  sourceNotebookId,
  groupId
) {
  const source = await notesApi(
    `notebooks/${sourceNotebookId}`
  );

  const { createNotebookSeed } = await import(
    notesUrl('js/notesv3/collab-yjs.js')
  );

  const seeds = [];

  for (const item of source.pages) {
    const sourcePage = await notesApi(
      `pages/${item.id}`
    );

    seeds.push({
      sourcePageId: sourcePage.id,
      sourceVersionToken: sourcePage.versionToken,
      initialUpdate: createNotebookSeed(
        sourcePage.deltaJson
      )
    });
  }

  await notesApi(
    `groups/${groupId}/notebook-copies`,
    {
      method: 'POST',
      body: {
        sourceNotebookId,
        seeds
      }
    }
  );
}

function confirmUnshareNotesPdf(shareId) {
  const pdf = notesResources.pdfs.find(
    item => item.shareId === shareId
  );

  if (!pdf) {
    throw new Error('Hãy làm mới danh sách tài nguyên.');
  }

  return openForm(
    'Gỡ chia sẻ PDF',
    '<p>Nhóm sẽ không còn truy cập PDF và ghi chú ' +
    'thuộc lượt chia sẻ này. PDF cá nhân vẫn được giữ.</p>',
    async () => {
      await notesApi(
        `pdf-shares/${shareId}`,
        {
          method: 'DELETE',
          body: {
            versionToken: pdf.shareVersionToken
          }
        }
      );

      await refreshNotesResources();
    },
    'Gỡ chia sẻ'
  );
}

function confirmDeleteGroupNotebook(notebookId) {
  return openForm(
    'Xóa notebook nhóm',
    '<p>Notebook và các trang bên trong sẽ bị ẩn khỏi nhóm. ' +
    'Bản notebook cá nhân ban đầu không bị ảnh hưởng.</p>',
    async () => {
      await notesApi(
        `notebooks/${notebookId}`,
        { method: 'DELETE' }
      );

      await refreshNotesResources();
    },
    'Xóa notebook'
  );
}
  //end
  async function dispatch(b) {
    const action = b.dataset.glAction;
const id = Number(b.dataset.id);

if (action === 'notes-resources-refresh') {
  await refreshNotesResources();

  // Flashcard vẫn được tải qua cơ chế Group hiện có.
  try {
    await load();
  } catch (error) {
    showError(error);
  }

  return;
}

if (action === 'resource-share') {
  return openNotesResourceForm();
}

if (action === 'notes-pdf-unshare') {
  return confirmUnshareNotesPdf(id);
}

if (action === 'notes-notebook-delete') {
  return confirmDeleteGroupNotebook(id);
}

if (action === 'refresh') {
  return load();
}

if (!state) {
  throw new Error('Hãy chờ dữ liệu tải xong.');
}
    const session = state.sessions.find(x => x.id === id);
    const assignment = state.assignments.find(x => x.id === id);
    const question = state.questions.find(x => x.id === id);
    switch (action) {
      case 'session-new': return sessionForm();
      case 'session-edit': return sessionForm(session);
      case 'session-register':
        await api(`/sessions/${id}/registration`, 'PUT', { registered: b.dataset.registered === 'true' }); return load();
      case 'session-cancel': case 'session-complete':
        return openForm(action === 'session-complete' ? 'Kết thúc buổi học' : 'Hủy buổi học',
          area('summary', action === 'session-complete' ? 'Kết quả đã đạt được *' : 'Lý do hủy', '', 4000, action === 'session-complete'),
          data => api(`/sessions/${id}/status`, 'POST', { status: action === 'session-complete' ? 'Completed' : 'Cancelled', summary: data.get('summary'), revision: session.revision }));
      case 'attendance': {
        const people = [...state.members];
        for (const a of session.attendees) if (!people.some(m => m.userId === a.userId)) people.push({ userId: a.userId, name: a.name });
        return openForm('Điểm danh thành viên', select('user', 'Thành viên', people.map(m => option(m.userId, `${m.name}${session.attendees.find(a => a.userId === m.userId)?.attended ? ' — Đã có mặt' : ''}`)).join('')) +
          select('attended', 'Kết quả điểm danh', option('true', 'Có mặt') + option('false', 'Vắng mặt')),
          data => api(`/sessions/${id}/attendance/${data.get('user')}`, 'PUT', { attended: data.get('attended') === 'true' }));
      }
      case 'assignment-new': return assignmentForm();
      case 'assignment-edit': return assignmentForm(assignment);
      case 'assignment-close':
        return openForm('Đóng nhận bài', '<p>Nhóm sẽ không nhận bài nộp mới cho nhiệm vụ này. Bài đã nộp vẫn được giữ để nhận xét.</p>',
          () => api(`/assignments/${id}/close`, 'POST', { revision: assignment.revision }), 'Đóng nhận bài');
      case 'submit': {
        const latest = assignment.submissions.filter(x => x.userId === state.currentUserId).sort((a, b) => b.version - a.version)[0];
        return openForm('Nộp bài làm', area('content', 'Lời giải', latest?.content, 12000) + input('url', 'Hoặc liên kết bài làm', latest?.resourceUrl, 'url', 2000) + '<p class="gl-meta">Mỗi lần nộp tạo một phiên bản mới và giữ lại lịch sử cũ.</p>',
          data => api(`/assignments/${id}/submissions`, 'POST', { content: data.get('content'), resourceUrl: data.get('url'), expectedVersion: latest?.version || 0 }), 'Nộp bài');
      }
      case 'review': {
        const submission = assignment.submissions.find(s => s.id === Number(b.dataset.submission));
        return openForm(`Phản hồi cho ${submission.name} · lần ${submission.version}`,
          select('status', 'Kết quả', option('Completed', 'Hoàn thành', submission.status === 'Completed') + option('NeedsRevision', 'Yêu cầu sửa', submission.status === 'NeedsRevision')) +
          area('feedback', 'Nhận xét (bắt buộc khi yêu cầu sửa)', submission.feedback),
          data => api(`/assignments/${id}/submissions/${submission.id}/review`, 'PUT', { status: data.get('status'), feedback: data.get('feedback') }));
      }
      // case 'resource-share':
      //   if (!state.availableResources.length) throw new Error('Bạn chưa có tài nguyên chưa chia sẻ. Hãy tạo ghi chú, tải PDF hoặc tạo bộ flashcard trước.');
      //   return openForm('Chia sẻ tài nguyên của bạn', select('resource', 'Chọn tài nguyên', state.availableResources.map(r => option(`${r.kind}/${r.id}`, `${r.title} (${r.kind})`)).join('')) +
      //     '<p class="gl-meta">Thành viên nhóm được đọc tài nguyên. Bạn vẫn giữ quyền chỉnh sửa; gỡ chia sẻ hoặc rời nhóm sẽ đưa tài nguyên về riêng tư.</p>',
      //     data => api(`/resources/${data.get('resource')}`, 'PUT'), 'Chia sẻ');
      case 'resource-unshare':
        return openForm('Gỡ chia sẻ tài nguyên', '<p>Tài nguyên trở về riêng tư của chủ sở hữu và không còn được nhóm truy cập.</p>',
          () => api(`/resources/${b.dataset.kind}/${id}`, 'DELETE'), 'Gỡ chia sẻ');
      case 'goal': {
        const p = state.progress;
        return openForm('Mục tiêu tuần', input('title', 'Mục tiêu *', p.goal?.title, 'text', 200, true) +
          input('sessions', 'Số buổi học cần hoàn thành (0–100)', p.goal?.targetSessions || 0, 'number', 3, true) +
          input('assignments', 'Số bài tập cả nhóm cần hoàn thành (0–100)', p.goal?.targetAssignments || 0, 'number', 3, true),
          data => api('/goal', 'PUT', { weekStart: p.weekStart, title: data.get('title'), targetSessions: Number(data.get('sessions')), targetAssignments: Number(data.get('assignments')), revision: p.goal?.revision || 0 }));
      }
      case 'question-new':
        return openForm('Đặt câu hỏi', input('title', 'Tiêu đề *', '', 'text', 200, true) + input('subject', 'Môn học / chủ đề *', '', 'text', 150, true) + area('body', 'Nội dung câu hỏi *', '', 8000, true),
          data => api('/questions', 'POST', { title: data.get('title'), subject: data.get('subject'), body: data.get('body') }), 'Đăng câu hỏi');
      case 'answer':
        return openForm('Trả lời câu hỏi', `<p>${esc(question.title)}</p>` + area('body', 'Lời giải / gợi ý *', '', 8000, true),
          data => api(`/questions/${id}/answers`, 'POST', { body: data.get('body') }), 'Đăng trả lời');
      case 'answer-accept':
        await api(`/questions/${id}/accepted-answer`, 'PUT', { answerId: Number(b.dataset.answer) || null, revision: question.revision }); return load();
    }
  }
  page.addEventListener('click', async e => {
    const b = e.target.closest('[data-gl-action]');
    if (!b || b.disabled) return;
    b.disabled = true;
    try { await dispatch(b); } catch (error) { showError(error); } finally { b.disabled = false; }
  });
  document.getElementById('gl-question-search').addEventListener('input', () => { if (state) renderQuestions(); });
  weekInput.addEventListener('change', () => load().catch(showError));
  load().catch(showError);
  //c
  refreshNotesResources().catch(showError);
  //end
})();
