import Quill from 'quill';
import 'quill/dist/quill.snow.css';

const root = document.querySelector("#group-notes-workspace");
const groupId = Number(root.dataset.groupId);
const userId = Number(root.dataset.userId);
const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
const status = document.querySelector("#group-notes-status");
const api = `/api/notes/groups/${groupId}`;
let workspace;
const createTextarea = document.querySelector("#new-group-note-body");
const createHost = document.createElement("div");
createHost.setAttribute("aria-labelledby", "new-group-note-body-label");
createTextarea.hidden = true;
createTextarea.style.display = 'none';
createTextarea.required = false;
createTextarea.after(createHost);
const editorOptions = { theme: 'snow', modules: { toolbar: [['bold', 'italic', 'underline'], [{ header: [1, 2, false] }], [{ list: 'ordered' }, { list: 'bullet' }], ['link', 'clean']] } };
const createEditor = new Quill(createHost, editorOptions);
const editForm = document.createElement('form');
editForm.hidden = true;
editForm.className = 'notes-group-create mb-4';
editForm.innerHTML = '<label class="form-label small fw-semibold" for="edit-group-note-title">Sửa Group Note</label><input id="edit-group-note-title" class="form-control mb-2" maxlength="200" required><div id="edit-group-note-body" aria-label="Nội dung Group Note đang sửa"></div><div class="d-flex justify-content-end gap-2 mt-2"><button id="group-note-edit-cancel" type="button" class="btn btn-outline-secondary">Hủy</button><button type="submit" class="btn-primary-sh">Lưu thay đổi</button></div>';
document.querySelector('#group-note-list').before(editForm);
const editEditor = new Quill(editForm.querySelector('#edit-group-note-body'), editorOptions);
let editingNote = null;
const historyPanel = document.querySelector('#group-note-history');
document.querySelector('#group-note-history-close').addEventListener('click', () => { historyPanel.hidden = true; });

async function showHistory(note) {
  const revisions = await send(`${api}/notes/${note.groupNoteId}/revisions`);
  const list = document.querySelector('#group-note-history-list');
  document.querySelector('#group-note-history-title').textContent = `Lịch sử: ${note.title}`;
  list.replaceChildren(...revisions.map(revision => {
    const card = document.createElement('article');
    card.className = 'notes-group-card';
    const heading = document.createElement('strong');
    heading.textContent = `${revision.title} · ${revision.operation}`;
    const meta = document.createElement('span');
    meta.textContent = `${new Date(revision.createdAtUtc).toLocaleString('vi-VN')}${revision.actorUserId ? ` · thành viên #${revision.actorUserId}` : ' · bản gốc khi kích hoạt lịch sử'}`;
    const restore = document.createElement('button');
    restore.type = 'button';
    restore.className = 'btn btn-sm btn-outline-secondary mt-2';
    restore.textContent = 'Khôi phục thành bản sao chữ';
    restore.addEventListener('click', async () => {
      if (!confirm(`Tạo GroupNote mới từ phiên bản #${revision.revisionId}?`)) return;
      restore.disabled = true;
      try {
        await send(`${api}/notes/${note.groupNoteId}/revisions/${revision.revisionId}/restore-copy`, 'POST');
        historyPanel.hidden = true;
        await load();
      } catch (error) { status.textContent = error.message; }
      finally { restore.disabled = false; }
    });
    card.append(heading, meta, restore);
    return card;
  }));
  if (!revisions.length) {
    const empty = document.createElement('p');
    empty.textContent = 'Không có phiên bản trong 30 ngày.';
    list.append(empty);
  }
  historyPanel.hidden = false;
  historyPanel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

editForm.querySelector('#group-note-edit-cancel').addEventListener('click', () => { editForm.hidden = true; editingNote = null; });
editForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!editingNote) return;
  const title = editForm.querySelector('#edit-group-note-title').value.trim();
  if (!editEditor.getText().trim()) { status.textContent = 'Nhập nội dung trước khi lưu.'; return; }
  const submit = editForm.querySelector('[type="submit"]');
  submit.disabled = true;
  try {
    await send(`${api}/notes/${editingNote.groupNoteId}?operation=UPDATE`, 'PUT', {
      title, contentDeltaJson: JSON.stringify(editEditor.getContents()), baseVersionToken: editingNote.versionToken
    });
    editForm.hidden = true;
    editingNote = null;
    await load();
  } catch (error) { status.textContent = `${error.message} Nội dung đang sửa vẫn ở đây.`; }
  finally { submit.disabled = false; }
});
document.addEventListener('keydown', event => {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 's' || editForm.hidden) return;
  event.preventDefault();
  editForm.requestSubmit();
});

document.querySelector("#group-note-create").addEventListener("submit", async event => {
  event.preventDefault();
  const title = document.querySelector("#new-group-note-title").value.trim();
  if (!createEditor.getText().trim()) { status.textContent = 'Nhập nội dung trước khi tạo.'; return; }
  try {
    const result = await send(`${api}/notes`, "POST", {
      title,
      contentDeltaJson: JSON.stringify(createEditor.getContents()),
      baseVersionToken: null
    });
    document.querySelector("#new-group-note-title").value = "";
    createEditor.setText('');
    status.textContent = "Đã tạo Group Note.";
    await load();
  } catch (error) { status.textContent = error.message; }
});

document.querySelector("#group-note-list").addEventListener("click", async event => {
  const button = event.target.closest("[data-action]");
  if (!button) return;
  const note = workspace.notes.find(item => item.groupNoteId === Number(button.dataset.id));
  if (!note) return;
  if (button.dataset.action === 'history') {
    try { await showHistory(note); }
    catch (error) { status.textContent = error.message; }
    return;
  }
  if (button.dataset.action === "edit") {
    editingNote = note;
    editForm.querySelector('#edit-group-note-title').value = note.title;
    try { editEditor.setContents(JSON.parse(note.contentDeltaJson)); }
    catch { status.textContent = 'Nội dung Group Note không hợp lệ.'; return; }
    editForm.hidden = false;
    editForm.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    return;
  }
  try {
    if (button.dataset.action === "delete") {
      if (!confirm(`Xóa Group Note “${note.title}” cho mọi thành viên?`)) return;
      await send(`${api}/notes/${note.groupNoteId}?operation=DELETE`, "PUT", {
        title: note.title, contentDeltaJson: note.contentDeltaJson, baseVersionToken: note.versionToken
      });
      status.textContent = "Đã xóa Group Note.";
    } else if (button.dataset.action === "archive" || button.dataset.action === "restore") {
      await send(`${api}/notes/${note.groupNoteId}/${button.dataset.action}`, "POST",
        { versionToken: note.versionToken });
      status.textContent = button.dataset.action === "archive" ? "Đã lưu trữ ghi chú." : "Đã khôi phục ghi chú.";
    } else if (button.dataset.action === "copy-personal") {
      const copy = await send(`${api}/notes/${note.groupNoteId}/copy-personal`, "POST");
      status.textContent = `Đã tạo Note cá nhân #${copy.id} từ nội dung chữ của nhóm.`;
    }
    await load();
  } catch (error) { status.textContent = error.message; }
});

document.querySelector("#group-deleted-note-list").addEventListener("click", async event => {
  const historyButton = event.target.closest('[data-deleted-history-id]');
  if (historyButton) {
    const deletedNote = workspace.deletedNotes.find(item => item.groupNoteId === Number(historyButton.dataset.deletedHistoryId));
    if (!deletedNote) return;
    try { await showHistory(deletedNote); }
    catch (error) { status.textContent = error.message; }
    return;
  }
  const button = event.target.closest("[data-restore-id]");
  if (!button) return;
  const deleted = workspace.deletedNotes.find(item => item.groupNoteId === Number(button.dataset.restoreId));
  if (!deleted || !confirm(`Khôi phục “${deleted.title}” thành GroupNote mới?`)) return;
  try {
    await send(`${api}/notes/${deleted.groupNoteId}/restore-copy`, "POST", { versionToken: deleted.versionToken });
    await load();
  } catch (error) { status.textContent = error.message; }
});

async function load() {
  try {
    workspace = await send(`${api}/workspace`);
    document.querySelector("#group-notes-title").textContent = `Tài nguyên · ${workspace.groupName}`;
    renderDocuments(workspace.documents);
    renderNotes(workspace.notes);
    renderDeletedNotes(workspace.deletedNotes);
    if (editingNote) {
      const latest = workspace.notes.find(note => note.groupNoteId === editingNote.groupNoteId);
      if (!latest || latest.versionToken !== editingNote.versionToken) {
        status.textContent = 'Group Note đã thay đổi. Nội dung đang sửa vẫn ở đây; sao chép lại trước khi tải trang.';
        return;
      }
    }
    status.textContent = "Quyền nhóm được kiểm tra lại ở mỗi request.";
  } catch (error) { status.textContent = error.message; }
}

function renderDocuments(items) {
  const list = document.querySelector("#group-pdf-list");
  document.querySelector("#group-pdf-count").textContent = items.length;
  document.querySelector("#group-pdf-empty").hidden = items.length > 0;
  list.replaceChildren(...items.map(item => {
    const card = document.createElement("article"); card.className = "notes-group-card";
    const link = document.createElement("a"); link.className = "notes-group-card-link";
    link.href = `/Notes/Groups/${groupId}/Documents/${item.documentId}?versionId=${item.versionId}`;
    const icon = document.createElement("i"); icon.className = "bi bi-file-earmark-pdf";
    const title = document.createElement("strong"); title.textContent = item.title;
    const meta = document.createElement("span"); meta.textContent = `${item.fileName} · ${formatBytes(item.sizeBytes)}${item.isArchived ? " · Đã lưu trữ, chỉ đọc" : ""}`;
    const open = document.createElement("span"); open.className = "notes-group-open"; open.textContent = "Mở PDF →";
    link.append(icon, title, meta, open); card.append(link);
    if (item.canManage) {
      const unshare = document.createElement("button"); unshare.type = "button"; unshare.className = "btn btn-sm btn-outline-secondary mt-2"; unshare.textContent = "Thu hồi chia sẻ";
      unshare.addEventListener("click", async () => {
        if (!confirm(`Thu hồi chia sẻ “${item.title}”? Thành viên nhóm sẽ không thể mở PDF và annotation của phiên chia sẻ này.`)) return;
        unshare.disabled = true;
        try { await send(`/api/notes/documents/${item.documentId}/shares/${groupId}`, "DELETE"); await load(); }
        catch (error) { status.textContent = error.message; unshare.disabled = false; }
      });
      card.append(unshare);
    }
    return card;
  }));
}

function renderNotes(items) {
  const list = document.querySelector("#group-note-list");
  document.querySelector("#group-note-count").textContent = items.length;
  document.querySelector("#group-note-empty").hidden = items.length > 0;
  list.replaceChildren(...items.map(note => {
    const card = document.createElement("article"); card.className = "notes-group-card";
    const title = document.createElement("strong"); title.textContent = note.title;
    const body = document.createElement("p"); body.textContent = plainText(note.contentDeltaJson); body.className = "notes-group-preview";
    const meta = document.createElement("span"); meta.textContent = `${note.isArchived ? "Đã lưu trữ · chỉ đọc" : note.ownerUserId === userId ? "Bạn tạo ghi chú này" : "Ghi chú nhóm"} · ${new Date(note.updatedAtUtc).toLocaleString("vi-VN")}`;
    card.append(title, body, meta);
    const actions = document.createElement('div'); actions.className = 'notes-group-actions';
    if (!note.isArchived) {
      actions.append(actionButton("edit", note.groupNoteId, "Sửa"));
      actions.append(actionButton("delete", note.groupNoteId, "Xóa"));
    }
    if (note.ownerUserId === userId)
      actions.append(actionButton(note.isArchived ? "restore" : "archive", note.groupNoteId, note.isArchived ? "Khôi phục" : "Lưu trữ"));
    actions.append(actionButton("copy-personal", note.groupNoteId, "Sao chép về cá nhân"));
    if (workspace.recoveryAvailable)
      actions.append(actionButton('history', note.groupNoteId, 'Lịch sử'));
    card.append(actions);
    return card;
  }));
  list.querySelectorAll("button[data-action]").forEach(button => button.classList.add("btn", "btn-sm", "btn-outline-secondary"));
}

function renderDeletedNotes(items) {
  const list = document.querySelector("#group-deleted-note-list");
  document.querySelector("#group-deleted-note-count").textContent = items.length;
  document.querySelector("#group-deleted-note-empty").hidden = items.length > 0;
  list.replaceChildren(...items.map(note => {
    const card = document.createElement("article"); card.className = "notes-group-card";
    const title = document.createElement("strong"); title.textContent = note.title;
    const meta = document.createElement("span"); meta.textContent = `Đã xóa · ${new Date(note.deletedAtUtc).toLocaleString("vi-VN")}`;
    const restore = document.createElement("button"); restore.type = "button";
    restore.className = "btn btn-sm btn-outline-secondary mt-2";
    restore.dataset.restoreId = note.groupNoteId;
    restore.textContent = "Khôi phục thành bản sao";
    card.append(title, meta, restore);
    if (workspace.recoveryAvailable) {
      const history = document.createElement('button');
      history.type = 'button';
      history.className = 'btn btn-sm btn-outline-secondary mt-2 ms-1';
      history.dataset.deletedHistoryId = note.groupNoteId;
      history.textContent = 'Lịch sử';
      card.append(history);
    }
    return card;
  }));
}

function renderRequests(items) {
  const list = document.querySelector("#group-request-list");
  document.querySelector("#group-request-count").textContent = items.length;
  document.querySelector("#group-request-empty").hidden = items.length > 0;
  list.replaceChildren(...items.map(request => {
    const card = document.createElement("article"); card.className = "notes-group-card";
    const title = document.createElement("strong"); title.textContent = `${request.operation} · ${request.targetType}`;
    const meta = document.createElement("span"); meta.textContent = `Đề xuất #${request.id} · thành viên ${request.requesterUserId} · ${new Date(request.createdAtUtc).toLocaleString("vi-VN")}`;
    card.append(title, meta);
    if (request.proposedTitle) { const text = document.createElement("p"); text.textContent = request.proposedTitle; card.append(text); }
    if (request.proposedPreview) { const preview = document.createElement("p"); preview.className = "notes-group-preview"; preview.textContent = request.proposedPreview; card.append(preview); }
    if (request.reviewOwnerUserId === userId) {
      card.append(decisionButton(request.id, "approve", "Duyệt"), decisionButton(request.id, "reject", "Từ chối"));
    } else {
      const pending = document.createElement("span"); pending.className = "badge text-bg-warning mt-2"; pending.textContent = "Chờ chủ nội dung xử lý"; card.append(pending);
    }
    return card;
  }));
}

function actionButton(action, id, label) { const button = document.createElement("button"); button.type = "button"; button.dataset.action = action; button.dataset.id = id; button.textContent = label; return button; }
function decisionButton(id, decision, label) { const button = actionButton("", id, label); button.dataset.decision = decision; button.className = decision === "approve" ? "btn btn-sm btn-primary me-1 mt-2" : "btn btn-sm btn-outline-danger mt-2"; return button; }
function delta(text) { return JSON.stringify({ ops: [{ insert: `${text}\n` }] }); }
function plainText(json) { try { return JSON.parse(json).ops.map(op => typeof op.insert === "string" ? op.insert : "").join("").trim(); } catch { return ""; } }
function formatBytes(bytes) { return bytes < 1048576 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1048576).toFixed(1)} MB`; }
async function send(url, method = "GET", body) {
  const response = await fetch(url, { method, credentials: "same-origin", headers: { ...(body === undefined ? {} : { "Content-Type": "application/json" }), ...(method === "GET" ? {} : { RequestVerificationToken: token }) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
  if (response.redirected) { location.href = "/Login"; throw new Error("Phiên đăng nhập đã hết hạn."); }
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || `HTTP ${response.status}`);
  return data;
}

load();
if (window.signalR) {
  const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/notes').withAutomaticReconnect().build();
  connection.on('GroupNotesChanged', () => { void load(); });
  connection.on('GroupAnnotationsChanged', () => { void load(); });
  connection.on('PdfShareChanged', () => { void load(); });
  connection.onreconnected(() => { void connection.invoke('EnterGroup', groupId).then(load).catch(() => { status.textContent = 'Không còn quyền truy cập nhóm.'; }); });
  void connection.start().then(() => connection.invoke('EnterGroup', groupId)).catch(() => {
    status.textContent = 'Không kết nối được đồng bộ nhóm. Tải lại trang để thử lại.';
  });
}
