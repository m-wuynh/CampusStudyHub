import { createInkLayer } from '@inklayer-dev/core/capabilities'
import '@inklayer-dev/core/style'

const root = document.querySelector("#group-pdf-reader");
const groupId = Number(root.dataset.groupId);
const documentId = Number(root.dataset.documentId);
const userId = root.dataset.userId || "current-user";
const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
const pages = document.querySelector("#group-pdf-pages");
const status = document.querySelector("#group-reader-status");
const api = `/api/notes/groups/${groupId}/documents/${documentId}`;
let core;
let versionId;
let versionToken;
let readOnly = true;
let dirty = false;
let saving = false;
let initialAnnotations = [];

for (const button of document.querySelectorAll("[data-tool]"))
  button.addEventListener("click", () => core?.annotations.setTool(button.dataset.tool));
document.querySelector("#group-highlight-color").addEventListener("input", event => core?.annotations.setToolAppearance("free-highlight", { stroke: { color: event.target.value, width: 16, opacity: 0.35, lineCap: "round", lineJoin: "round" } }));
document.querySelector("#group-pen-color").addEventListener("input", event => core?.annotations.setToolAppearance("freehand", { stroke: { color: event.target.value, width: 3, opacity: 1, lineCap: "round", lineJoin: "round" } }));
document.querySelector("#group-reader-zoom-in").addEventListener("click", () => core?.getPageFlow()?.zoomIn());
document.querySelector("#group-reader-zoom-out").addEventListener("click", () => core?.getPageFlow()?.zoomOut());
document.querySelector("#group-reader-fit").addEventListener("click", () => core?.getPageFlow()?.setScale("page-width"));
document.querySelector("#group-reader-save").addEventListener("click", save);
window.addEventListener("keydown", event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s" && dirty) { event.preventDefault(); save(); }
});
window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });

await open();

async function open() {
  try {
    const versionQuery = new URLSearchParams(location.search).get("versionId");
    const suffix = versionQuery ? `?versionId=${encodeURIComponent(versionQuery)}` : "";
    const details = await request(`${api}${suffix}`);
    versionId = details.versionId;
    readOnly = details.isArchived;
    root.dataset.canManage = String(details.canManage);
    document.querySelector("#group-reader-title").textContent = details.title;
    status.textContent = `${details.fileName} · ${formatBytes(details.sizeBytes)}${readOnly ? " · PDF lưu trữ, chỉ đọc" : details.canManage ? " · annotation chính thức" : " · chỉnh sửa sẽ gửi đề xuất"}`;
    core = await createInkLayer({
      root: document.querySelector("#group-pdf-workspace"),
      annotation: {
        currentUser: { id: userId, name: root.dataset.userName || "Bạn" },
        creationModes: { "free-highlight": "continuous" },
        defaultAppearances: {
          "free-highlight": { stroke: { color: "#facc15", width: 16, opacity: 0.35, lineCap: "round", lineJoin: "round" } },
          freehand: { stroke: { color: "#ef4444", width: 3, opacity: 1, lineCap: "round", lineJoin: "round" } }
        }
      },
      pageFlow: {
        container: pages,
        scale: "page-width",
        pixelRatio: 1,
        overscan: "0px",
        onError: error => { status.textContent = `Không render được PDF: ${error?.message || "Lỗi không xác định."}`; }
      }
    });
    core.viewer.subscribe(event => {
      if (event.type === "loadProgress" && event.progress.percentage !== null)
        status.textContent = `Đang tải PDF… ${event.progress.percentage}%`;
    });
    await core.load({ url: `${api}/file${suffix}`, range: "auto", rangeChunkSize: 64 * 1024 });
    if (!await waitForFirstPage()) throw new Error("Trang đầu chưa render sau 15 giây. Tải lại PDF rồi thử lại.");
    const saved = await request(`${api}/annotations?versionId=${versionId}`);
    versionToken = saved.versionToken;
    initialAnnotations = JSON.parse(saved.payloadJson);
    core.annotations.repository.replaceAll(initialAnnotations);
    readOnly ||= saved.isArchived;
    core.annotations.repository.subscribe(event => {
      if (readOnly || ["selection", "destroy"].includes(event.type)) return;
      dirty = true;
      status.textContent = "Có thay đổi chưa lưu. Ctrl+S để lưu hoặc gửi đề xuất.";
    });
    const count = core.viewer.getSnapshot().document.numPages;
    status.textContent = `${details.fileName} · ${count} trang${readOnly ? " · Chỉ đọc" : details.canManage ? " · chỉnh sửa chính thức" : " · lưu thành đề xuất"}`;
    for (const selector of ["#group-reader-zoom-in", "#group-reader-zoom-out", "#group-reader-fit"])
      document.querySelector(selector).disabled = false;
    if (!readOnly) {
      for (const control of document.querySelectorAll("[data-tool], #group-highlight-color, #group-pen-color")) control.disabled = false;
      document.querySelector("#group-reader-save").disabled = false;
    }
  } catch (error) { status.textContent = `Không mở được tài liệu: ${error.message}`; }
}

async function save() {
  if (!core || readOnly || !dirty || saving) return;
  saving = true;
  const button = document.querySelector("#group-reader-save");
  button.disabled = true;
  const payload = core.annotations.repository.getAll();
  const serialized = JSON.stringify(payload);
  const operation = initialAnnotations.length === 0 && payload.length > 0 ? "CREATE" :
    payload.length === 0 && initialAnnotations.length > 0 ? "DELETE" : "UPDATE";
  status.textContent = "Đang lưu annotation nhóm…";
  try {
    if (root.dataset.canManage === "true") {
      const result = await request(`${api}/annotations?versionId=${versionId}`, "PUT", { payload, versionToken });
      versionToken = result.versionToken;
      initialAnnotations = payload;
      status.textContent = `Đã lưu annotation chính thức · ${payload.length} mục.`;
    } else {
      const result = await request(`/api/notes/groups/${groupId}/requests`, "POST", {
        targetType: "GroupAnnotation", operation, targetId: null,
        documentVersionId: versionId, proposedTitle: null,
        proposedPayloadJson: serialized, baseVersionToken: versionToken
      });
      status.textContent = `Đã gửi đề xuất #${result.id}; annotation chính thức chưa đổi.`;
      initialAnnotations = payload;
    }
    dirty = false;
  } catch (error) { status.textContent = `Lưu thất bại: ${error.message} · thay đổi vẫn còn trên màn hình.`; }
  finally { saving = false; button.disabled = readOnly; }
}

async function request(url, method = "GET", body) {
  const response = await fetch(url, { method, credentials: "same-origin", headers: { ...(body === undefined ? {} : { "Content-Type": "application/json" }), ...(method === "GET" ? {} : { RequestVerificationToken: token }) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
  if (response.redirected) { location.href = "/Login"; throw new Error("Phiên đăng nhập đã hết hạn."); }
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || `HTTP ${response.status}`);
  return data;
}

function waitForFirstPage() {
  const isMounted = () => pages.querySelector('[data-inklayer-flow-page="0"][data-inklayer-flow-mounted="true"]');
  if (isMounted()) return Promise.resolve(true);
  return new Promise(resolve => {
    const observer = new MutationObserver(() => {
      if (!isMounted()) return;
      clearTimeout(timeout);
      observer.disconnect();
      resolve(true);
    });
    const timeout = setTimeout(() => { observer.disconnect(); resolve(false); }, 15000);
    observer.observe(pages, { subtree: true, attributes: true, attributeFilter: ["data-inklayer-flow-mounted"] });
  });
}
function formatBytes(bytes) { return bytes < 1048576 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1048576).toFixed(1)} MB`; }
