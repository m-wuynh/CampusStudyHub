const root = document.querySelector("#group-notes-workspace");
const groupId = Number(root.dataset.groupId);
const userId = Number(root.dataset.userId);
const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
const status = document.querySelector("#group-notes-status");
const api = `/api/notes/groups/${groupId}`;
let workspace;

document.querySelector("#group-note-create").addEventListener("submit", async event => {
  event.preventDefault();
  const title = document.querySelector("#new-group-note-title").value.trim();
  const text = document.querySelector("#new-group-note-body").value.trim();
  try {
    const result = await send(`${api}/notes`, "POST", {
      title,
      contentDeltaJson: delta(text),
      baseVersionToken: null
    });
    document.querySelector("#new-group-note-title").value = "";
    document.querySelector("#new-group-note-body").value = "";
    status.textContent = result.applied ? "Đã tạo Group Note." : `Đã gửi đề xuất #${result.requestId}.`;
    await load();
  } catch (error) { status.textContent = error.message; }
});

document.querySelector("#group-note-list").addEventListener("click", async event => {
  const button = event.target.closest("[data-action]");
  if (!button) return;
  const note = workspace.notes.find(item => item.groupNoteId === Number(button.dataset.id));
  if (!note) return;
  try {
    if (button.dataset.action === "edit") {
      const title = prompt("Tiêu đề:", note.title);
      if (title === null) return;
      const body = prompt("Nội dung:", plainText(note.contentDeltaJson));
      if (body === null) return;
      const result = note.ownerUserId === userId
        ? await send(`${api}/notes/${note.groupNoteId}?operation=UPDATE`, "PUT", {
          title, contentDeltaJson: delta(body), baseVersionToken: note.versionToken
        })
        : await send(`${api}/requests`, "POST", {
          targetType: "GroupNote", operation: "UPDATE", targetId: note.groupNoteId,
          documentVersionId: null, proposedTitle: title,
          proposedPayloadJson: delta(body), baseVersionToken: note.versionToken
        });
      status.textContent = result.applied ? "Đã cập nhật Group Note." : `Đã gửi đề xuất #${result.requestId}.`;
    } else if (button.dataset.action === "delete") {
      if (!confirm(`Xóa hoặc gửi đề xuất xóa “${note.title}”?`)) return;
      const result = note.ownerUserId === userId
        ? await send(`${api}/notes/${note.groupNoteId}?operation=DELETE`, "PUT", {
          title: note.title, contentDeltaJson: note.contentDeltaJson, baseVersionToken: note.versionToken
        })
        : await send(`${api}/requests`, "POST", {
          targetType: "GroupNote", operation: "DELETE", targetId: note.groupNoteId,
          documentVersionId: null, proposedTitle: note.title,
          proposedPayloadJson: null, baseVersionToken: note.versionToken
        });
      status.textContent = result.applied ? "Đã xóa Group Note." : `Đã gửi đề xuất #${result.requestId}.`;
    } else if (button.dataset.action === "archive" || button.dataset.action === "restore") {
      await send(`${api}/notes/${note.groupNoteId}/${button.dataset.action}`, "POST");
      status.textContent = button.dataset.action === "archive" ? "Đã lưu trữ ghi chú." : "Đã khôi phục ghi chú.";
    }
    await load();
  } catch (error) { status.textContent = error.message; }
});

document.querySelector("#group-request-list").addEventListener("click", async event => {
  const button = event.target.closest("[data-decision]");
  if (!button) return;
  try {
    const approve = button.dataset.decision === "approve";
    const result = await send(`${api}/requests/${button.dataset.id}/decision`, "POST", {
      approve, resolutionNote: null
    });
    status.textContent = result.status === "Conflict" ? "Đề xuất cũ đã thành Conflict; nội dung chính thức không bị ghi đè." : approve ? "Đã duyệt và ghi audit." : "Đã từ chối; nội dung chính thức giữ nguyên.";
    await load();
  } catch (error) { status.textContent = error.message; await load(); }
});

async function load() {
  try {
    workspace = await send(`${api}/workspace`);
    document.querySelector("#group-notes-title").textContent = `Tài nguyên · ${workspace.groupName}`;
    renderDocuments(workspace.documents);
    renderNotes(workspace.notes);
    renderRequests(workspace.pendingRequests);
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
      unshare.addEventListener("click", async () => { try { await send(`/api/notes/documents/${item.documentId}/shares/${groupId}`, "DELETE"); await load(); } catch (error) { status.textContent = error.message; } });
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
    const meta = document.createElement("span"); meta.textContent = `${note.isArchived ? "Đã lưu trữ · chỉ đọc" : note.ownerUserId === userId ? "Bạn là chủ sở hữu" : "Snapshot của nhóm"} · ${new Date(note.updatedAtUtc).toLocaleString("vi-VN")}`;
    card.append(title, body, meta);
    if (!note.isArchived) {
      card.append(actionButton("edit", note.groupNoteId, note.ownerUserId === userId ? "Sửa" : "Đề xuất sửa"));
      card.append(actionButton("delete", note.groupNoteId, note.ownerUserId === userId ? "Xóa" : "Đề xuất xóa"));
    }
    if (note.ownerUserId === userId)
      card.append(actionButton(note.isArchived ? "restore" : "archive", note.groupNoteId, note.isArchived ? "Khôi phục" : "Lưu trữ"));
    return card;
  }));
  list.querySelectorAll("button[data-action]").forEach(button => button.classList.add("btn", "btn-sm", "btn-outline-secondary", "me-1", "mt-2"));
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
