import Quill from 'quill'
import 'quill/dist/quill.snow.css'

const root = document.querySelector('#note-editor-page')
const noteId = root.dataset.noteId
const notesApi = root.dataset.notesApi
const documentsApi = root.dataset.documentsApi
const token = root.querySelector('input[name="__RequestVerificationToken"]').value
const $ = (selector) => document.querySelector(selector)

const editor = new Quill('#note-page-editor', {
  theme: 'snow',
  modules: { toolbar: '#note-page-toolbar' },
  placeholder: 'Viết ghi chú…',
  formats: ['header', 'bold', 'italic', 'underline', 'strike', 'list', 'blockquote', 'link', 'image']
})
let versionToken = null
let archived = false
let dirty = false
let saving = false
let editRevision = 0

editor.on('text-change', (_delta, _old, source) => {
  if (source === 'user') setDirty()
})
$('#note-page-title').addEventListener('input', setDirty)
$('#note-page-save').addEventListener('click', save)
$('#note-page-document').addEventListener('change', () => {
  $('#note-page-open-split').disabled = !$('#note-page-document').value
})
$('#note-page-open-split').addEventListener('click', () => {
  const documentId = $('#note-page-document').value
  if (!documentId) return
  location.href = `/Notes/Reader/${documentId}`
})
window.addEventListener('keydown', (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
    event.preventDefault()
    void save()
  }
})
window.addEventListener('beforeunload', (event) => {
  if (!dirty) return
  event.preventDefault()
  event.returnValue = ''
})

await load()

async function load() {
  try {
    const [note, documents] = await Promise.all([
      request(`${notesApi}/${noteId}`),
      request(documentsApi)
    ])
    versionToken = note.versionToken
    archived = note.isArchived
    $('#note-page-title').value = note.title
    editor.setContents(JSON.parse(note.contentDeltaJson), 'silent')
    editor.enable(!archived)
    $('#note-page-title').disabled = archived
    $('#note-page-save').disabled = archived
    setStatus(archived ? 'Ghi chú đã lưu trữ · chỉ đọc' : 'Đã lưu')
    if (new URLSearchParams(location.search).get('new') === '1' && !archived) {
      $('#note-page-title').focus()
      $('#note-page-title').select()
      setStatus('Đặt tên ghi chú rồi viết nội dung. Bấm Ctrl+S để lưu.')
    }
    fillDocuments(documents.filter((item) => !item.isArchived))
    renderReferences(note.references)
  } catch (error) {
    setStatus(`Không tải được ghi chú: ${error.message}`, 'failed')
  }
}

function fillDocuments(documents) {
  const select = $('#note-page-document')
  select.replaceChildren(new Option(documents.length ? 'Chọn PDF…' : 'Chưa có PDF trong thư viện', ''))
  for (const item of documents) select.add(new Option(item.title, String(item.id)))
  $('#note-page-open-split').disabled = true
}

async function save() {
  if (!dirty || archived || saving) return
  const savedRevision = editRevision
  saving = true
  $('#note-page-save').disabled = true
  setStatus('Đang lưu…', 'saving')
  try {
    const result = await request(`${notesApi}/${noteId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
      body: JSON.stringify({
        title: $('#note-page-title').value,
        contentDelta: editor.getContents(),
        versionToken
      })
    })
    versionToken = result.versionToken
    if (editRevision === savedRevision) {
      dirty = false
      setStatus(`Đã lưu · ${new Date(result.updatedAtUtc).toLocaleTimeString('vi-VN')}`)
    } else {
      setStatus('Có thay đổi chưa lưu', 'dirty')
    }
  } catch (error) {
    setStatus(`Lưu thất bại: ${error.message} · nội dung vẫn còn trên màn hình`, 'failed')
  } finally {
    saving = false
    $('#note-page-save').disabled = archived || !dirty
  }
}

function setDirty() {
  if (archived) return
  editRevision += 1
  dirty = true
  $('#note-page-save').disabled = false
  setStatus('Có thay đổi chưa lưu', 'dirty')
}

function setStatus(message, state = 'saved') {
  const status = $('#note-page-status')
  status.textContent = message
  status.dataset.state = state
}

function renderReferences(references) {
  const list = $('#note-page-reference-list')
  list.replaceChildren()
  if (!references.length) {
    const empty = document.createElement('li')
    empty.className = 'small text-secondary'
    empty.textContent = 'Chưa có tham chiếu PDF.'
    list.append(empty)
    return
  }
  for (const reference of references) {
    const item = document.createElement('li')
    item.className = 'note-reference-item'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = `Trang ${reference.pageIndex + 1} · ${reference.documentTitle} · ${reference.captureType === 'Text' ? 'chữ' : 'vùng'}`
    button.title = reference.capturedText || reference.documentTitle
    button.addEventListener('click', () => openReference(reference))
    item.append(button)
    list.append(item)
  }
}

function openReference(reference) {
  let anchor = {}
  try { anchor = JSON.parse(reference.anchorJson) } catch { /* The API validates persisted anchors. */ }
  const query = new URLSearchParams({
    versionId: String(reference.documentVersionId),
    pageIndex: String(reference.pageIndex),
    x: String(anchor.x ?? 0), y: String(anchor.y ?? 0),
    width: String(anchor.width ?? 1), height: String(anchor.height ?? 1)
  })
  location.href = `/Notes/Reader/${reference.documentId}?${query}`
}

async function request(url, options = {}) {
  const response = await fetch(url, { credentials: 'same-origin', ...options })
  if (response.redirected) {
    location.href = '/Login'
    throw new Error('Phiên đăng nhập đã hết hạn.')
  }
  const contentType = response.headers.get('content-type') || ''
  const body = contentType.includes('application/json') ? await response.json() : await response.text()
  if (!response.ok) throw new Error(body?.message || body?.detail || body || `HTTP ${response.status}`)
  return body
}
