const root = document.querySelector('#notes-library')
const api = root.dataset.api
const notesApi = '/api/notes/notes'
const token = root.querySelector('input[name="__RequestVerificationToken"]').value
const fileInput = document.querySelector('#pdf-upload-file')
const uploadButton = document.querySelector('#pdf-upload-submit')
const rows = document.querySelector('#library-rows')
const status = document.querySelector('#library-status')
const empty = document.querySelector('#library-empty')
const uploadProgress = document.querySelector('#pdf-upload-progress')
const shareModal = document.querySelector('#notes-share-modal')
const shareGroup = document.querySelector('#notes-share-group')
const shareStatus = document.querySelector('#notes-share-status')
const shareConfirm = document.querySelector('#notes-share-confirm')

let documents = []
let notes = []
let activeFilter = 'all'
let shareTarget = null

fileInput.addEventListener('change', () => {
  const file = fileInput.files?.[0]
  uploadButton.disabled = !file
  if (file) status.textContent = `${file.name} · ${formatBytes(file.size)}`
})

document.querySelector('#pdf-upload-form').addEventListener('submit', async (event) => {
  event.preventDefault()
  const file = fileInput.files?.[0]
  if (!file) return
  const data = new FormData()
  data.append('file', file)
  data.append('title', document.querySelector('#pdf-title').value)
  uploadButton.disabled = true
  uploadProgress.value = 0
  uploadProgress.classList.remove('d-none')
  status.textContent = 'Đang kiểm tra và lưu PDF…'
  try {
    await uploadPdf(data)
    fileInput.value = ''
    document.querySelector('#pdf-title').value = ''
    uploadProgress.classList.add('d-none')
    activeFilter = 'all'
    setActiveFilter()
    await loadDocuments('Đã tải PDF lên thư viện.')
  } catch (error) {
    uploadProgress.classList.add('d-none')
    status.textContent = error.message
    uploadButton.disabled = false
  }
})

document.querySelectorAll('[data-filter]').forEach((button) => button.addEventListener('click', () => {
  activeFilter = button.dataset.filter
  setActiveFilter()
  render()
}))
document.querySelector('#note-create').addEventListener('click', createNote)
document.querySelector('#library-search').addEventListener('input', render)
document.querySelector('#library-sort').addEventListener('change', render)

rows.addEventListener('click', async (event) => {
  const button = event.target.closest('[data-action]')
  if (!button) return
  const { action, id, title } = button.dataset
  try {
    if (action === 'share' || action === 'share-note') {
      await openShareDialog(action, id)
      return
    }
    if (action === 'open') {
      location.href = `/Notes/Reader/${id}`
      return
    }
    if (action === 'rename') {
      const nextTitle = window.prompt('Tên tài liệu mới:', title)
      if (nextTitle === null || !nextTitle.trim()) return
      await request(`${api}/${id}/title`, jsonRequest('PUT', { title: nextTitle.trim() }))
    }
    if (action === 'pin') await request(`${api}/${id}/pin`, jsonRequest('POST'))
    if (action === 'archive') await request(`${api}/${id}/archive`, jsonRequest('POST'))
    if (action === 'restore') await request(`${api}/${id}/restore`, jsonRequest('POST'))
    if (action === 'delete') {
      if (!window.confirm(`Xóa “${title}” khỏi thư viện?`)) return
      await request(`${api}/${id}`, jsonRequest('DELETE'))
    }
    if (action === 'open-note') {
      location.href = `/Notes/Note/${id}`
      return
    }
    if (action === 'archive-note') await request(`${notesApi}/${id}/archive`, jsonRequest('POST'))
    if (action === 'restore-note') await request(`${notesApi}/${id}/restore`, jsonRequest('POST'))
    if (action === 'delete-note') {
      if (!window.confirm(`Xóa ghi chú “${title}” khỏi thư viện?`)) return
      await request(`${notesApi}/${id}`, jsonRequest('DELETE'))
    }
    await loadDocuments('Đã cập nhật thư viện.')
  } catch (error) {
    status.textContent = error.message
  }
})

function jsonRequest(method, value) {
  return { method, headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, ...(value === undefined ? {} : { body: JSON.stringify(value) }) }
}

async function openShareDialog(action, id) {
  shareTarget = { action, id }
  shareGroup.replaceChildren()
  shareStatus.textContent = 'Đang tải các nhóm bạn là thành viên…'
  shareConfirm.disabled = true
  bootstrap.Modal.getOrCreateInstance(shareModal).show()
  try {
    const groups = await request('/api/notes/eligible-groups')
    for (const group of groups) {
      const option = document.createElement('option')
      option.value = group.groupId
      option.textContent = group.name
      shareGroup.append(option)
    }
    shareStatus.textContent = groups.length
      ? 'Chỉ nhóm có membership đang hoạt động mới được phép chia sẻ.'
      : 'Bạn chưa là thành viên đang hoạt động của nhóm nào.'
    shareConfirm.disabled = groups.length === 0
  } catch (error) { shareStatus.textContent = error.message }
}

shareConfirm.addEventListener('click', async () => {
  if (!shareTarget || !shareGroup.value) return
  shareConfirm.disabled = true
  shareStatus.textContent = 'Đang tạo quyền chia sẻ…'
  const isDocument = shareTarget.action === 'share'
  const url = isDocument
    ? `/api/notes/documents/${shareTarget.id}/shares`
    : `/api/notes/notes/${shareTarget.id}/shares`
  try {
    await request(url, jsonRequest('POST', { groupId: Number(shareGroup.value) }))
    shareStatus.textContent = isDocument
      ? 'Đã chia sẻ PDF; annotation riêng của nhóm đã được khởi tạo.'
      : 'Đã tạo snapshot độc lập của ghi chú trong nhóm.'
    setTimeout(() => bootstrap.Modal.getOrCreateInstance(shareModal).hide(), 600)
  } catch (error) { shareStatus.textContent = error.message }
  finally { shareConfirm.disabled = shareGroup.options.length === 0 }
})

async function createNote() {
  const title = window.prompt('Tên ghi chú mới:', 'Ghi chú mới')
  if (title === null) return
  try {
    const created = await request(notesApi, jsonRequest('POST', { title }))
    location.href = `/Notes/Note/${created.id}`
  } catch (error) {
    status.textContent = `Không tạo được ghi chú: ${error.message}`
  }
}

async function request(url, options = {}) {
  const response = await fetch(url, { credentials: 'same-origin', ...options })
  if (response.redirected) {
    location.href = '/Login'
    throw new Error('Phiên đăng nhập đã hết hạn.')
  }
  if (response.status === 204) return null
  const contentType = response.headers.get('content-type') || ''
  const body = contentType.includes('application/json') ? await response.json() : await response.text()
  if (!response.ok) throw new Error(body?.message || body?.detail || body || `HTTP ${response.status}`)
  return body
}

function uploadPdf(data) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', api)
    xhr.withCredentials = true
    xhr.setRequestHeader('RequestVerificationToken', token)
    xhr.upload.addEventListener('progress', (event) => {
      if (!event.lengthComputable) return
      uploadProgress.value = Math.round((event.loaded / event.total) * 100)
      status.textContent = `Đang tải PDF lên… ${uploadProgress.value}%`
    })
    xhr.addEventListener('load', () => {
      if (new URL(xhr.responseURL).pathname.toLowerCase().startsWith('/login')) {
        location.href = '/Login'
        reject(new Error('Phiên đăng nhập đã hết hạn.'))
        return
      }
      let body
      try { body = JSON.parse(xhr.responseText) } catch { body = null }
      if (xhr.status >= 200 && xhr.status < 300) resolve(body)
      else reject(new Error(body?.message || body?.detail || `Upload thất bại (HTTP ${xhr.status})`))
    })
    xhr.addEventListener('error', () => reject(new Error('Mất kết nối khi tải PDF lên.')))
    xhr.addEventListener('abort', () => reject(new Error('Đã hủy tải PDF.')))
    xhr.send(data)
  })
}

async function loadDocuments(message = '') {
  status.textContent = 'Đang tải thư viện…'
  try {
    const [loadedDocuments, loadedNotes] = await Promise.all([request(api), request(notesApi)])
    documents = loadedDocuments
    notes = loadedNotes
    status.textContent = message || `${documents.length} PDF · ${notes.length} ghi chú`
    render()
  } catch (error) {
    documents = []
    notes = []
    rows.replaceChildren()
    empty.classList.add('d-none')
    status.textContent = `Không tải được thư viện: ${error.message}`
  }
}

function setActiveFilter() {
  document.querySelectorAll('[data-filter]').forEach((button) => {
    button.classList.toggle('active', button.dataset.filter === activeFilter)
  })
}

function render() {
  const query = document.querySelector('#library-search').value.trim().toLocaleLowerCase('vi')
  const sort = document.querySelector('#library-sort').value
  const libraryItems = [
    ...documents.map((item) => ({ ...item, kind: 'pdf' })),
    ...notes.map((item) => ({ ...item, kind: 'note', fileName: 'Ghi chú', sizeBytes: 0 }))
  ]
  const filtered = libraryItems.filter((item) => {
    const matchesFilter = activeFilter === 'archived'
      ? item.isArchived
      : !item.isArchived && (activeFilter === 'all' ||
        (activeFilter === 'pdf' && item.kind === 'pdf') || (activeFilter === 'notes' && item.kind === 'note'))
    return matchesFilter && `${item.title} ${item.fileName}`.toLocaleLowerCase('vi').includes(query)
  })
  filtered.sort(sort === 'title'
    ? (a, b) => Number(b.isPinned) - Number(a.isPinned) || a.title.localeCompare(b.title, 'vi')
    : (a, b) => Number(b.isPinned) - Number(a.isPinned) || Date.parse(b.updatedAtUtc) - Date.parse(a.updatedAtUtc))

  rows.replaceChildren(...filtered.map(createRow))
  empty.classList.toggle('d-none', filtered.length > 0)
  empty.querySelector('h3').textContent = libraryItems.length === 0 ? 'Thư viện đang trống' : 'Không tìm thấy nội dung'
  empty.querySelector('p').textContent = libraryItems.length === 0
    ? 'Tải PDF lên hoặc tạo ghi chú để bắt đầu.'
    : 'Thử đổi từ khóa hoặc bộ lọc.'
}

function createRow(item) {
  const row = document.createElement('tr')
  const titleCell = document.createElement('td')
  const title = document.createElement('button')
  title.type = 'button'
  title.className = 'notes-document-title'
  title.dataset.action = item.kind === 'note' ? 'open-note' : 'open'
  title.dataset.id = item.id
  title.textContent = item.title
  const detail = document.createElement('div')
  detail.className = 'small text-secondary mt-1'
  detail.textContent = item.isArchived ? 'Đã lưu trữ · chỉ đọc' : item.kind === 'note' ? 'Ghi chú cá nhân' : item.fileName
  titleCell.append(title, detail)

  const sizeCell = document.createElement('td')
  sizeCell.className = 'text-secondary small'
  sizeCell.textContent = item.kind === 'note' ? 'Note' : formatBytes(item.sizeBytes)
  const dateCell = document.createElement('td')
  dateCell.className = 'text-secondary small'
  dateCell.textContent = new Date(item.updatedAtUtc).toLocaleString('vi-VN')
  const actionCell = document.createElement('td')
  actionCell.className = 'text-end text-nowrap'
  const actions = item.kind === 'note'
    ? item.isArchived
      ? [['open-note', 'Mở ghi chú', 'bi-journal-text'], ['restore-note', 'Khôi phục', 'bi-arrow-counterclockwise'], ['delete-note', 'Xóa', 'bi-trash']]
      : [['open-note', 'Mở ghi chú', 'bi-journal-text'], ['share-note', 'Chia sẻ vào nhóm', 'bi-people'], ['archive-note', 'Lưu trữ', 'bi-archive'], ['delete-note', 'Xóa', 'bi-trash']]
    : item.isArchived
      ? [['restore', 'Khôi phục', 'bi-arrow-counterclockwise'], ['delete', 'Xóa', 'bi-trash']]
      : [['pin', item.isPinned ? 'Bỏ ghim' : 'Ghim', item.isPinned ? 'bi-pin-angle-fill' : 'bi-pin-angle'], ['share', 'Chia sẻ vào nhóm', 'bi-people'], ['rename', 'Đổi tên', 'bi-pencil'], ['archive', 'Lưu trữ', 'bi-archive'], ['delete', 'Xóa', 'bi-trash']]
  for (const [action, label, icon] of actions) {
    const button = document.createElement('button')
    button.type = 'button'
    button.className = 'btn-icon-sh notes-row-action'
    button.title = label
    button.setAttribute('aria-label', label)
    Object.assign(button.dataset, { action, id: item.id, title: item.title })
    const glyph = document.createElement('i')
    glyph.className = `bi ${icon}`
    button.append(glyph)
    actionCell.append(button)
  }
  row.append(titleCell, sizeCell, dateCell, actionCell)
  return row
}

function formatBytes(bytes) {
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

loadDocuments()
