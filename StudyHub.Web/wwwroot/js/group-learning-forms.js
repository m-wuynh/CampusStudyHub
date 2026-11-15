(() => {
  'use strict';
  const page = document.getElementById('group-details-page');
  if (page?.dataset.isMember !== 'true') return;
  const base = page.dataset.learningUrl;
  const token = document.querySelector('#group-antiforgery input')?.value;
  const dialog = document.getElementById('gl-dialog');
  const form = document.getElementById('gl-form');
  const saveButton = document.getElementById('gl-dialog-save');
  const errorBox = document.getElementById('gl-form-error');
  const weekInput = document.getElementById('gl-week');
  const stateElement = document.getElementById('group-learning-state');
  const state = stateElement ? JSON.parse(stateElement.textContent) : null;
  let saveForm, busy = false;
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const localInput = value => new Date(new Date(value).getTime() + 7 * 3600000).toISOString().slice(0, 16);
  const utc = value => new Date(`${value}:00+07:00`).toISOString();
  const tomorrow = () => localInput(Date.now() + 86400000);
  const input = (name, label, value = '', type = 'text', max = 200, required = false) => `<label>${label}<input class="sh-input" name="${name}" type="${type}" value="${esc(value)}" maxlength="${max}" ${required ? 'required' : ''}></label>`;
  const area = (name, label, value = '', max = 4000, required = false) => `<label>${label}<textarea class="sh-input" name="${name}" rows="3" maxlength="${max}" ${required ? 'required' : ''}>${esc(value)}</textarea></label>`;
  const check = (name, label, checked = false) => `<label><input type="checkbox" name="${name}" ${checked ? 'checked' : ''}>${label}</label>`;
  const select = (name, label, options) => `<label>${label}<select class="sh-input" name="${name}">${options}</select></label>`;
  const option = (value, label, selected = false) => `<option value="${esc(value)}" ${selected ? 'selected' : ''}>${esc(label)}</option>`;
  function activePanel() {
    return document.querySelector('.group-channel-nav [data-panel].active')?.dataset.panel || 'sessions';
  }
  function reload(panel = activePanel()) {
    const url = new URL(location.href);
    url.searchParams.set('panel', panel);
    if (weekInput.value) url.searchParams.set('week', weekInput.value);
    else url.searchParams.delete('week');
    location.assign(url);
  }
  function submitForm(action, values = {}) {
    if (busy) return;
    const post = document.createElement('form');
    post.method = 'post';
    post.action = base + action;
    post.hidden = true;
    const fields = { ...values, __RequestVerificationToken: token, panel: activePanel(), week: weekInput.value };
    for (const [name, value] of Object.entries(fields)) {
      for (const item of Array.isArray(value) ? value : [value ?? '']) {
        const input = document.createElement('input');
        input.type = 'hidden'; input.name = name; input.value = String(item);
        post.appendChild(input);
      }
    }
    document.body.appendChild(post);
    busy = true;
    saveButton.disabled = true;
    HTMLFormElement.prototype.submit.call(post);
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
  form.addEventListener('submit', e => {
    e.preventDefault();
    if (busy) return;
    errorBox.hidden = true;
    try { saveForm(new FormData(form)); }
    catch (error) { errorBox.textContent = error.message; errorBox.hidden = false; }
  });

  function sessionForm(session) {
    openForm(session ? 'Sửa buổi học' : 'Tạo buổi học nhóm',
      input('title', 'Tên buổi học *', session?.title, 'text', 200, true) + area('objective', 'Mục tiêu đầu ra *', session?.objective, 1000, true) +
      input('start', 'Bắt đầu (giờ Việt Nam) *', session ? localInput(session.startsAtUtc) : tomorrow(), 'datetime-local', 100, true) +
      input('end', 'Kết thúc (giờ Việt Nam) *', session ? localInput(session.endsAtUtc) : localInput(Date.now() + 90000000), 'datetime-local', 100, true) +
      input('location', 'Địa điểm', session?.location, 'text', 300) + input('url', 'Link họp (HTTP/HTTPS)', session?.meetingUrl, 'url', 2000) +
      area('preparation', 'Nội dung và tài liệu cần chuẩn bị', session?.preparation),
      data => submitForm(session ? 'UpdateSession' : 'CreateSession', { id: session?.id || 0,
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
      data => submitForm(assignment ? 'UpdateAssignment' : 'CreateAssignment', { id: assignment?.id || 0,
        title: data.get('title'), description: data.get('description'), dueAtUtc: utc(data.get('due')), sessionId: Number(data.get('session')) || null,
        allowLateSubmission: data.has('late'), assigneeIds: data.getAll('assignee').map(Number), revision: assignment?.revision || 0
      }));
  }
  function shareFlashcardResource() {
    const flashcards = (state.availableResources || [])
      .filter(resource => resource.kind === 'Flashcard');
    if (!flashcards.length) throw new Error('Bạn chưa có flashcard để chia sẻ.');

    return openForm('Chia sẻ flashcard',
      select('resource', 'Flashcard', flashcards.map(resource =>
        option(resource.id, resource.title)).join('')),
      data => submitForm('Share', { kind: 'Flashcard', id: data.get('resource') }),
      'Chia sẻ');
  }

  function sharePdfResource() {
    const pdfs = (state.availableResources || [])
      .filter(resource => resource.kind === 'Pdf');
    if (!pdfs.length) throw new Error('Bạn chưa có PDF phù hợp để chia sẻ vào nhóm này.');

    return openForm('Chia sẻ tài liệu',
      select('resource', 'Tài liệu PDF', pdfs.map(resource =>
        option(resource.id, resource.title)).join('')) +
      '<p class="gl-meta">Nhóm chỉ nhận file PDF gốc. Ghi chú, tô sáng, nét vẽ và annotation cá nhân không được chia sẻ.</p>',
      data => submitForm('Share', { kind: 'Pdf', id: data.get('resource') }),
      'Chia sẻ');
  }

  async function dispatch(b) {
    const action = b.dataset.glAction;
    const id = Number(b.dataset.id);
    if (action === 'refresh') return reload();
    if (!state) throw new Error('Không tải được dữ liệu học tập. Vui lòng tải lại trang.');
    if (action === 'flashcard-share') return shareFlashcardResource();
    if (action === 'pdf-share') return sharePdfResource();
    const session = state.sessions.find(x => x.id === id);
    const assignment = state.assignments.find(x => x.id === id);
    const question = state.questions.find(x => x.id === id);
    switch (action) {
      case 'session-new': return sessionForm();
      case 'session-edit': return sessionForm(session);
      case 'session-register':
        submitForm('Register', { id, registered: b.dataset.registered === 'true' }); return;
      case 'session-cancel': case 'session-complete':
        return openForm(action === 'session-complete' ? 'Kết thúc buổi học' : 'Hủy buổi học',
          area('summary', action === 'session-complete' ? 'Kết quả đã đạt được *' : 'Lý do hủy', '', 4000, action === 'session-complete'),
          data => submitForm('SessionStatus', { id, status: action === 'session-complete' ? 'Completed' : 'Cancelled', summary: data.get('summary'), revision: session.revision }));
      case 'attendance': {
        const people = [...state.members];
        for (const a of session.attendees) if (!people.some(m => m.userId === a.userId)) people.push({ userId: a.userId, name: a.name });
        return openForm('Điểm danh thành viên', select('user', 'Thành viên', people.map(m => option(m.userId, `${m.name}${session.attendees.find(a => a.userId === m.userId)?.attended ? ' — Đã có mặt' : ''}`)).join('')) +
          select('attended', 'Kết quả điểm danh', option('true', 'Có mặt') + option('false', 'Vắng mặt')),
          data => submitForm('Attendance', { id, userId: data.get('user'), attended: data.get('attended') === 'true' }));
      }
      case 'assignment-new': return assignmentForm();
      case 'assignment-edit': return assignmentForm(assignment);
      case 'assignment-close':
        return openForm('Đóng nhận bài', '<p>Nhóm sẽ không nhận bài nộp mới cho nhiệm vụ này. Bài đã nộp vẫn được giữ để nhận xét.</p>',
          () => submitForm('Close', { id, revision: assignment.revision }), 'Đóng nhận bài');
      case 'submit': {
        const latest = assignment.submissions.filter(x => x.userId === state.currentUserId).sort((a, b) => b.version - a.version)[0];
        return openForm('Nộp bài làm', area('content', 'Lời giải', latest?.content, 12000) + input('url', 'Hoặc liên kết bài làm', latest?.resourceUrl, 'url', 2000) + '<p class="gl-meta">Mỗi lần nộp tạo một phiên bản mới và giữ lại lịch sử cũ.</p>',
          data => submitForm('Submit', { id, content: data.get('content'), resourceUrl: data.get('url'), expectedVersion: latest?.version || 0 }), 'Nộp bài');
      }
      case 'review': {
        const submission = assignment.submissions.find(s => s.id === Number(b.dataset.submission));
        return openForm(`Phản hồi cho ${submission.name} · lần ${submission.version}`,
          select('status', 'Kết quả', option('Completed', 'Hoàn thành', submission.status === 'Completed') + option('NeedsRevision', 'Yêu cầu sửa', submission.status === 'NeedsRevision')) +
          area('feedback', 'Nhận xét (bắt buộc khi yêu cầu sửa)', submission.feedback),
          data => submitForm('Review', { id, submissionId: submission.id, status: data.get('status'), feedback: data.get('feedback') }));
      }
      case 'resource-unshare':
        return openForm('Gỡ chia sẻ tài nguyên', '<p>Tài nguyên trở về riêng tư của chủ sở hữu và không còn được nhóm truy cập.</p>',
          () => submitForm('Unshare', { id, kind: b.dataset.kind }), 'Gỡ chia sẻ');
      case 'goal': {
        const p = state.progress;
        return openForm('Mục tiêu tuần', input('title', 'Mục tiêu *', p.goal?.title, 'text', 200, true) +
          input('sessions', 'Số buổi học cần hoàn thành (0–100)', p.goal?.targetSessions || 0, 'number', 3, true) +
          input('assignments', 'Số bài tập cả nhóm cần hoàn thành (0–100)', p.goal?.targetAssignments || 0, 'number', 3, true),
          data => submitForm('Goal', { weekStart: p.weekStart, title: data.get('title'), targetSessions: Number(data.get('sessions')), targetAssignments: Number(data.get('assignments')), revision: p.goal?.revision || 0 }));
      }
      case 'question-new':
        return openForm('Đặt câu hỏi', input('title', 'Tiêu đề *', '', 'text', 200, true) + input('subject', 'Môn học / chủ đề *', '', 'text', 150, true) + area('body', 'Nội dung câu hỏi *', '', 8000, true),
          data => submitForm('Ask', { title: data.get('title'), subject: data.get('subject'), body: data.get('body') }), 'Đăng câu hỏi');
      case 'answer':
        return openForm('Trả lời câu hỏi', `<p>${esc(question.title)}</p>` + area('body', 'Lời giải / gợi ý *', '', 8000, true),
          data => submitForm('Answer', { id, body: data.get('body') }), 'Đăng trả lời');
      case 'answer-accept':
        submitForm('Accept', { id, answerId: Number(b.dataset.answer) || null, revision: question.revision }); return;
    }
  }
  page.addEventListener('click', async e => {
    const b = e.target.closest('[data-gl-action]');
    if (!b || b.disabled || busy) return;
    b.disabled = true;
    try { await dispatch(b); } catch (error) { showError(error); } finally { b.disabled = false; }
  });
  document.getElementById('gl-question-search').addEventListener('input', e => {
    const term = e.target.value.trim().toLocaleLowerCase('vi');
    const cards = [...document.querySelectorAll('[data-question-search]')];
    for (const card of cards) card.hidden = !card.dataset.questionSearch.toLocaleLowerCase('vi').includes(term);
    const empty = document.getElementById('gl-question-empty');
    if (empty) empty.hidden = cards.some(card => !card.hidden);
  });
  weekInput.addEventListener('change', () => reload('progress'));
  const counter = document.getElementById('gl-resource-count');
  if (counter && state) counter.textContent = String(state.resources.filter(r => r.kind === 'Flashcard' || r.kind === 'Pdf').length);
})();
