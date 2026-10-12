import { createInkLayer } from '@inklayer-dev/core/capabilities'
import '@inklayer-dev/core/style'
import Quill from 'quill'
import 'quill/dist/quill.snow.css'

const root = document.querySelector('#notes-reader')
const api = root.dataset.api
const documentId = root.dataset.documentId
const token = root.querySelector('input[name="__RequestVerificationToken"]').value
const $ = (selector) => document.querySelector(selector)

let core
let dirty = false
let saving = false
let readOnly = false
let usedCompatibilityLoad = false
let activeCommentAnnotationId = null
let activeReaderTool = 'select'
let eraserGesture = null
let searchMatches = []
let activeSearchIndex = -1
let searchQuery = ''
let documentVersionId = null
let noteEditor = null
let activeNoteId = null
let activeNoteVersionToken = null
let activeNoteArchived = false
let noteDirty = false
let noteSaving = false
let captureMode = null
let areaGesture = null
let captureBusy = false

$('#toggle-comments').addEventListener('click', () => setCommentsPanel($('#reader-comments').hidden))
$('#close-comments').addEventListener('click', () => setCommentsPanel(false))
$('#comment-form').addEventListener('submit', postComment)
$('#reader-pages').addEventListener('dblclick', createCommentAtPointer, true)
$('#reader-pages').addEventListener('pointerdown', beginReaderGesture, true)
$('#reader-pages').addEventListener('pointermove', continueReaderGesture, true)
$('#reader-pages').addEventListener('pointerup', finishReaderGesture, true)
$('#reader-pages').addEventListener('pointercancel', finishReaderGesture, true)

$('#save-annotations').addEventListener('click', saveAnnotations)
$('#toggle-note-workspace').addEventListener('click', toggleNoteWorkspace)
$('#note-create-in-reader').addEventListener('click', createNoteInReader)
$('#reader-note-select').addEventListener('change', () => loadNote($('#reader-note-select').value))
$('#reader-note-title').addEventListener('input', markNoteDirty)
$('#save-note').addEventListener('click', saveActiveNote)
$('#capture-note-text').addEventListener('click', toggleTextCapture)
$('#capture-note-area').addEventListener('click', toggleAreaCapture)
$('#reader-zoom-in').addEventListener('click', () => core?.getPageFlow()?.zoomIn())
$('#reader-zoom-out').addEventListener('click', () => core?.getPageFlow()?.zoomOut())
$('#reader-fit-width').addEventListener('click', () => core?.getPageFlow()?.setScale('page-width'))
$('#reader-prev-page').addEventListener('click', () => goToPage(Number($('#reader-page-number').value) - 2))
$('#reader-next-page').addEventListener('click', () => goToPage(Number($('#reader-page-number').value)))
$('#reader-page-number').addEventListener('change', () => goToPage(Number($('#reader-page-number').value) - 1))
$('#reader-search-submit').addEventListener('click', searchDocument)
$('#reader-search-next').addEventListener('click', () => moveSearchResult(1))
$('#reader-search-previous').addEventListener('click', () => moveSearchResult(-1))
$('#reader-search').addEventListener('keydown', (event) => {
  if (event.key === 'Enter') {
    event.preventDefault()
    if (searchMatches.length && $('#reader-search').value.trim() === searchQuery) moveSearchResult(event.shiftKey ? -1 : 1)
    else searchDocument()
  }
})
$('#reader-search').addEventListener('input', clearSearchResults)

$('#pen-color').addEventListener('input', updatePenAppearance)
$('#pen-width').addEventListener('input', updatePenAppearance)
$('#pen-opacity').addEventListener('input', updatePenAppearance)
$('#highlight-color').addEventListener('input', updateHighlightAppearance)
$('#highlight-width').addEventListener('input', updateHighlightAppearance)
$('#highlight-opacity').addEventListener('input', updateHighlightAppearance)
$('#eraser-size').addEventListener('input', () => {
  $('#eraser-size-value').value = $('#eraser-size').value
  $('#eraser-size-value').textContent = $('#eraser-size').value
})
document.querySelectorAll('[data-tool]').forEach((button) => button.addEventListener('click', () => {
  if (!core || button.disabled) return
  if (captureMode) cancelCaptureMode()
  setReaderTool(button.dataset.tool)
}))
$('#tool-eraser').addEventListener('click', () => {
  if (!core || $('#tool-eraser').disabled) return
  setReaderTool(activeReaderTool === 'eraser' ? 'select' : 'eraser')
})
document.querySelectorAll('.reader-tool-settings input').forEach((input) => {
  input.addEventListener('change', closeToolSettings)
  input.addEventListener('pointerup', () => {
    if (input.type === 'range') closeToolSettings()
  })
  input.addEventListener('keyup', (event) => {
    if (input.type === 'range' && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'].includes(event.key)) closeToolSettings()
  })
})
document.addEventListener('pointerdown', (event) => {
  if (root.classList.contains('reader-settings-open') && !event.target.closest('.reader-tool-rail')) closeToolSettings()
})
$('#delete-annotation').addEventListener('click', deleteSelection)
window.addEventListener('keydown', (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's' && core) {
    event.preventDefault()
    if (dirty) saveAnnotations()
    if (noteDirty) saveActiveNote()
  }
})
window.addEventListener('beforeunload', (event) => {
  if (!dirty && !noteDirty) return
  event.preventDefault()
  event.returnValue = ''
})

initializeNoteEditor()
await openDocument()

async function openDocument() {
  try {
    const requestedVersionId = new URLSearchParams(location.search).get('versionId')
    const versionQuery = requestedVersionId ? `?versionId=${encodeURIComponent(requestedVersionId)}` : ''
    const details = await request(`${api}/${documentId}${versionQuery}`)
    documentVersionId = details.versionId
    readOnly = details.isArchived
    $('#reader-title').textContent = details.title
    $('#reader-status').textContent = `${details.fileName} · ${formatBytes(details.sizeBytes)}${readOnly ? ' · Chỉ đọc' : ''}`
    core = await createInkLayer({
      root: $('#pdf-reader-workspace'),
      annotation: {
        currentUser: {
          id: root.dataset.userId || 'current-user',
          name: root.dataset.userName || 'Bạn'
        },
        creationModes: { 'free-highlight': 'continuous' },
        defaultAppearances: {
          'free-highlight': {
            stroke: { color: '#facc15', width: 16, opacity: 0.35, lineCap: 'round', lineJoin: 'round' }
          },
          freehand: { stroke: { color: '#ef4444', width: 3, opacity: 1, lineCap: 'round', lineJoin: 'round' } }
        }
      },
      pageFlow: {
        container: $('#reader-pages'),
        scale: 'page-width',
        pixelRatio: 1,
        overscan: '0px',
        onCurrentPageChanged: (pageIndex) => updatePageNavigation(pageIndex),
        onError: (error) => {
          console.error('[M1] PDF page render failed', error)
          $('#reader-status').textContent = `Không render được PDF: ${error?.message || 'Lỗi không xác định.'}`
        }
      }
    })
    core.viewer.subscribe((event) => {
      if (event.type === 'loadProgress') {
        const progress = event.progress.percentage
        $('#reader-status').textContent = progress === null
          ? 'Đang tải và phân tích PDF…'
          : `Đang tải PDF… ${progress}%`
      }
      if (captureMode === 'text' && event.type === 'textSelected') void insertTextCapture(event.selection)
      if (captureMode === 'text' && event.type === 'documentTextSelected')
        setNoteStatus('Hãy chọn chữ trong cùng một trang PDF để tạo tham chiếu chính xác.', 'failed')
    })
    const pdfUrl = `${api}/${documentId}/file${versionQuery}`
    try {
      // Stream by GET; the Range adapter's HEAD probe is not reliable on every host.
      await core.load({ url: pdfUrl, range: false })
    } catch (streamError) {
      console.warn('[M1] Streaming PDF load failed; retrying with downloaded bytes.', streamError)
      $('#reader-status').textContent = 'Đang thử cách tải PDF tương thích…'
      try {
        const response = await fetchPdfResponse(pdfUrl)
        const bytes = await readPdfBytes(response)
        await core.load({ data: bytes })
        usedCompatibilityLoad = true
      } catch (compatibilityError) {
        throw new Error(
          `Tải dạng stream: ${describeError(streamError)} · Tải tương thích: ${describeError(compatibilityError)}`,
          { cause: compatibilityError }
        )
      }
    }
    const saved = await request(`${api}/${documentId}/annotations${versionQuery}`)
    core.annotations.repository.replaceAll(saved)
    core.annotations.subscribe((event) => {
      if (event.type === 'toolChanged') updateActiveTool(event.tool)
      if (event.type === 'selectionChanged') {
        syncCommentTarget(event.selection.ids)
        const selected = event.selection.primaryId && core.annotations.repository.getById(event.selection.primaryId)
        if (event.isClick && selected?.type === 'note') openCommentForAnnotation(selected.id)
      }
    })
    core.annotations.repository.subscribe((event) => {
      renderComments()
      renderOpenCommentPopover()
      if (readOnly || ['selection', 'destroy'].includes(event.type)) return
      dirty = true
      setSaveStatus('Có thay đổi chưa lưu', 'dirty')
    })

    const rendered = await waitForFirstPage()
    const pageCount = core.viewer.getSnapshot().document.numPages
    $('#reader-page-count').textContent = `/ ${pageCount}`
    $('#reader-page-number').max = String(pageCount)
    $('#reader-status').textContent = `${details.fileName} · ${pageCount} trang${readOnly ? ' · Chỉ đọc' : ''}${usedCompatibilityLoad ? ' · chế độ tương thích' : ''}`
    ;['#reader-zoom-in', '#reader-zoom-out', '#reader-fit-width', '#reader-prev-page', '#reader-next-page', '#reader-page-number', '#reader-search', '#reader-search-submit', '#toggle-comments'].forEach((selector) => { $(selector).disabled = false })
    if (!readOnly) {
      document.querySelectorAll('[data-tool], #pen-color, #pen-width, #pen-opacity, #highlight-color, #highlight-width, #highlight-opacity, #eraser-size, #delete-annotation').forEach((button) => { button.disabled = false })
      $('#tool-eraser').disabled = false
      $('#save-annotations').disabled = false
    }
    updateSearchNavigation()
    dirty = false
    setSaveStatus(`Đã lưu${saved.length ? ` · ${saved.length} annotation` : ''}`)
    if (!rendered) throw new Error('Không xác nhận được trang đầu đã render.')
    if (!readOnly) await updatePageCount(core.viewer.getSnapshot().document.numPages)
    $('#toggle-note-workspace').disabled = false
    await loadNoteOptions(new URLSearchParams(location.search).get('noteId'))
    await navigateToReferenceFromUrl()
  } catch (error) {
    console.error('[M1] Could not open document', error, { cause: error?.cause })
    $('#reader-status').textContent = `Không mở được tài liệu: ${describeError(error)}`
  }
}

async function fetchPdfResponse(url) {
  const response = await fetch(url, { credentials: 'same-origin' })
  if (response.redirected) {
    location.href = '/Login'
    throw new Error('Phiên đăng nhập hết hạn; hãy đăng nhập lại.')
  }
  if (!response.ok) {
    const responseText = await response.text()
    let message = responseText
    try { message = JSON.parse(responseText)?.message || responseText } catch { /* Keep the response text for diagnostics. */ }
    if (message.length > 180) message = `${message.slice(0, 180)}…`
    throw new Error(`API PDF trả HTTP ${response.status}${message ? `: ${message}` : '.'}`)
  }
  const contentType = response.headers.get('content-type') || ''
  if (!contentType.toLowerCase().includes('application/pdf'))
    throw new Error(`API trả Content-Type "${contentType || 'không có'}", cần application/pdf.`)
  return response
}

async function readPdfBytes(response) {
  if (!response.body) throw new Error('Trình duyệt không cung cấp luồng tải file.')
  const reader = response.body.getReader()
  const chunks = []
  const total = Number(response.headers.get('content-length')) || 0
  let loaded = 0
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      chunks.push(value)
      loaded += value.byteLength
      $('#reader-status').textContent = total
        ? `Đang tải PDF tương thích… ${Math.floor(loaded / total * 100)}%`
        : `Đang tải PDF tương thích… ${formatBytes(loaded)}`
    }
  } finally {
    reader.releaseLock()
  }
  const bytes = new Uint8Array(loaded)
  let offset = 0
  for (const chunk of chunks) {
    bytes.set(chunk, offset)
    offset += chunk.byteLength
  }
  const header = new TextDecoder('ascii').decode(bytes.subarray(0, Math.min(bytes.length, 1024)))
  if (!header.includes('%PDF-')) throw new Error('API trả dữ liệu không có chữ ký PDF hợp lệ.')
  return bytes
}

function describeError(error) {
  const messages = []
  let current = error
  while (current && messages.length < 4) {
    const message = current.message || String(current)
    if (!messages.includes(message)) messages.push(message)
    current = current.cause
  }
  return messages.join(' → ')
}

function waitForFirstPage() {
  const pages = $('#reader-pages')
  const isMounted = () => pages.querySelector('[data-inklayer-flow-page="0"][data-inklayer-flow-mounted="true"]')
  if (isMounted()) return Promise.resolve(true)
  return new Promise((resolve) => {
    const observer = new MutationObserver(() => {
      if (!isMounted()) return
      clearTimeout(timeout)
      observer.disconnect()
      resolve(true)
    })
    const timeout = setTimeout(() => {
      observer.disconnect()
      resolve(false)
    }, 15000)
    observer.observe(pages, { subtree: true, attributes: true, attributeFilter: ['data-inklayer-flow-mounted'] })
  })
}

function initializeNoteEditor() {
  noteEditor = new Quill('#reader-note-editor', {
    theme: 'snow',
    modules: { toolbar: '#reader-note-toolbar' },
    placeholder: 'Viết ghi chú, hoặc chụp nội dung từ PDF…',
    formats: ['header', 'bold', 'italic', 'underline', 'list', 'blockquote', 'link', 'image']
  })
  noteEditor.on('text-change', (_delta, _old, source) => {
    if (source !== 'user' || !activeNoteId || activeNoteArchived) return
    markNoteDirty()
  })
}

async function toggleNoteWorkspace() {
  const panel = $('#note-workspace')
  const opening = panel.hidden
  panel.hidden = !opening
  $('#reader-layout').classList.toggle('note-workspace-open', opening)
  $('#toggle-note-workspace').setAttribute('aria-expanded', String(opening))
  $('#toggle-note-workspace').classList.toggle('active', opening)
  if (opening && !activeNoteId) $('#reader-note-select').focus()
  if (!opening) cancelCaptureMode()
}

async function loadNoteOptions(preferredId = null, discardCurrentEdits = false) {
  try {
    const notes = await request('/api/notes/notes')
    const select = $('#reader-note-select')
    select.replaceChildren(new Option('Chọn ghi chú…', ''))
    for (const note of notes) {
      const option = new Option(`${note.title}${note.isArchived ? ' · lưu trữ' : ''}`, String(note.id))
      option.disabled = note.isArchived
      select.add(option)
    }
    if (preferredId) {
      const option = [...select.options].find((item) => item.value === String(preferredId))
      if (option && !option.disabled) {
        $('#note-workspace').hidden = false
        $('#reader-layout').classList.add('note-workspace-open')
        $('#toggle-note-workspace').setAttribute('aria-expanded', 'true')
        $('#toggle-note-workspace').classList.add('active')
        select.value = option.value
        await loadNote(option.value, discardCurrentEdits)
      }
    }
  } catch (error) {
    setNoteStatus(`Không tải được danh sách ghi chú: ${error.message}`, 'failed')
  }
}

async function createNoteInReader() {
  if (noteDirty && !window.confirm('Bỏ thay đổi chưa lưu để tạo ghi chú mới?')) return
  try {
    const created = await request('/api/notes/notes', jsonRequest('POST', { title: 'Ghi chú mới' }))
    await loadNoteOptions(String(created.id), true)
  } catch (error) {
    setNoteStatus(`Không tạo được ghi chú: ${error.message}`, 'failed')
  }
}

async function loadNote(noteId, discardCurrentEdits = false) {
  if (noteDirty && !discardCurrentEdits && !window.confirm('Bỏ thay đổi chưa lưu của ghi chú hiện tại?')) {
    $('#reader-note-select').value = activeNoteId ? String(activeNoteId) : ''
    return
  }
  if (!noteId) {
    activeNoteId = null
    activeNoteVersionToken = null
    noteEditor?.setContents([{ insert: '\n' }], 'silent')
    $('#reader-note-title').value = ''
    $('#reader-note-title').disabled = true
    noteEditor?.enable(false)
    noteDirty = false
    updateNoteButtons()
    setNoteStatus('Chọn ghi chú để bắt đầu.')
    renderNoteReferences([])
    return
  }
  try {
    setNoteStatus('Đang tải ghi chú…', 'saving')
    const note = await request(`/api/notes/notes/${noteId}`)
    activeNoteId = note.id
    activeNoteVersionToken = note.versionToken
    activeNoteArchived = note.isArchived
    $('#reader-note-title').value = note.title
    $('#reader-note-title').disabled = activeNoteArchived
    noteEditor.setContents(JSON.parse(note.contentDeltaJson), 'silent')
    noteEditor.enable(!activeNoteArchived)
    noteDirty = false
    updateNoteButtons()
    renderNoteReferences(note.references)
    setNoteStatus(activeNoteArchived ? 'Ghi chú đã lưu trữ · chỉ đọc' : `Đã lưu · ${note.references.length} tham chiếu`)
  } catch (error) {
    setNoteStatus(`Không mở được ghi chú: ${error.message}`, 'failed')
  }
}

function markNoteDirty() {
  noteDirty = true
  setNoteStatus('Có thay đổi chưa lưu', 'dirty')
  updateNoteButtons()
}

async function saveActiveNote() {
  if (!activeNoteId || activeNoteArchived || !noteDirty || noteSaving) return
  noteSaving = true
  updateNoteButtons()
  setNoteStatus('Đang lưu ghi chú…', 'saving')
  try {
    const result = await request(`/api/notes/notes/${activeNoteId}`, jsonRequest('PUT', {
      title: $('#reader-note-title').value,
      contentDelta: noteEditor.getContents(),
      versionToken: activeNoteVersionToken
    }))
    activeNoteVersionToken = result.versionToken
    noteDirty = false
    setNoteStatus(`Đã lưu · ${new Date(result.updatedAtUtc).toLocaleTimeString('vi-VN')}`)
    await refreshNoteOptionsTitle()
  } catch (error) {
    setNoteStatus(`Lưu thất bại: ${error.message} · nội dung vẫn còn trên màn hình`, 'failed')
  } finally {
    noteSaving = false
    updateNoteButtons()
  }
}

async function refreshNoteOptionsTitle() {
  const option = [...$('#reader-note-select').options].find((item) => item.value === String(activeNoteId))
  if (option) option.textContent = $('#reader-note-title').value.trim()
}

function updateNoteButtons() {
  const hasEditableNote = Boolean(activeNoteId) && !activeNoteArchived
  $('#save-note').disabled = !hasEditableNote || !noteDirty || noteSaving
  $('#capture-note-text').disabled = !hasEditableNote || readOnly || !documentVersionId || captureBusy
  $('#capture-note-area').disabled = !hasEditableNote || readOnly || !documentVersionId || captureBusy
}

function setNoteStatus(message, state = 'saved') {
  const status = $('#note-save-status')
  status.textContent = message
  status.dataset.state = state
}

function jsonRequest(method, value) {
  return { method, headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, body: JSON.stringify(value) }
}

function toggleTextCapture() {
  if (captureMode === 'text') return cancelCaptureMode()
  if (!canCapture()) return
  cancelCaptureMode()
  captureMode = 'text'
  root.classList.add('reader-text-capture')
  setReaderTool('text-select')
  $('#capture-note-text').classList.add('active')
  setNoteStatus('Kéo để chọn chữ trên PDF; nội dung sẽ được chèn vào ghi chú.')
}

function toggleAreaCapture() {
  if (captureMode === 'area') return cancelCaptureMode()
  if (!canCapture()) return
  cancelCaptureMode()
  captureMode = 'area'
  root.classList.add('reader-capture-mode')
  core.annotations.setTool('select')
  updateActiveTool('select')
  $('#capture-note-area').classList.add('active')
  setNoteStatus('Kéo chuột trên PDF để chọn vùng cần chụp.')
}

function canCapture() {
  if (activeNoteId && !activeNoteArchived && !readOnly && documentVersionId) return true
  setNoteStatus('Chọn một ghi chú có thể sửa và PDF chưa lưu trữ trước.', 'failed')
  return false
}

function cancelCaptureMode() {
  const wasTextCapture = captureMode === 'text'
  captureMode = null
  areaGesture?.overlay?.remove()
  areaGesture = null
  root.classList.remove('reader-text-capture', 'reader-capture-mode')
  $('#capture-note-text').classList.remove('active')
  $('#capture-note-area').classList.remove('active')
  if (core) {
    if (wasTextCapture) core.viewer.clearTextSelection()
    core.annotations.setTool('select')
  }
  activeReaderTool = 'select'
  updateActiveTool('select')
}

async function insertTextCapture(selection) {
  if (captureBusy || captureMode !== 'text' || !activeNoteId || !selection?.text?.trim()) return
  captureBusy = true
  updateNoteButtons()
  const pageIndex = selection.pageIndex
  const rectangles = selection.rects || []
  const anchor = unionBounds(rectangles)
  const previousContents = noteEditor.getContents()
  const previousDirty = noteDirty
  try {
    if (!anchor) throw new Error('Không đọc được tọa độ vùng chữ đã chọn.')
    const insertAt = Math.max(0, noteEditor.getLength() - 1)
    noteEditor.insertText(insertAt, `${selection.text.trim()}\n\n`, 'user')
    await persistCapture('Text', pageIndex, anchor, selection.text.trim(), previousContents, previousDirty)
  } catch (error) {
    setNoteStatus(`Không chụp được chữ: ${error.message}`, 'failed')
  } finally {
    captureBusy = false
    cancelCaptureMode()
    updateNoteButtons()
  }
}

function unionBounds(rectangles) {
  if (!rectangles?.length) return null
  const x = Math.min(...rectangles.map((rect) => rect.x))
  const y = Math.min(...rectangles.map((rect) => rect.y))
  const right = Math.max(...rectangles.map((rect) => rect.x + rect.width))
  const bottom = Math.max(...rectangles.map((rect) => rect.y + rect.height))
  return { x, y, width: right - x, height: bottom - y }
}

async function persistCapture(captureType, pageIndex, anchor, capturedText = null, previousContents, previousDirty) {
  try {
    const result = await request(`/api/notes/notes/${activeNoteId}/captures`, jsonRequest('POST', {
      versionToken: activeNoteVersionToken,
      documentVersionId,
      pageIndex,
      captureType,
      anchor,
      capturedText,
      contentDelta: noteEditor.getContents()
    }))
    activeNoteVersionToken = result.versionToken
    noteDirty = false
    setNoteStatus('Đã chụp và lưu nội dung cùng tham chiếu PDF.')
    await loadNote(activeNoteId)
  } catch (error) {
    noteEditor.setContents(previousContents, 'silent')
    noteDirty = previousDirty
    throw error
  }
}

function renderNoteReferences(references = []) {
  const list = $('#note-reference-list')
  list.replaceChildren()
  $('#note-reference-count').textContent = String(references.length)
  if (!references.length) {
    const empty = document.createElement('li')
    empty.className = 'small text-secondary'
    empty.textContent = 'Chưa có tham chiếu.'
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
  try { anchor = JSON.parse(reference.anchorJson) } catch { /* The server validates the stored JSON. */ }
  const query = new URLSearchParams({
    versionId: String(reference.documentVersionId),
    pageIndex: String(reference.pageIndex),
    x: String(anchor.x ?? 0), y: String(anchor.y ?? 0),
    width: String(anchor.width ?? 1), height: String(anchor.height ?? 1)
  })
  location.href = `/Notes/Reader/${reference.documentId}?${query}`
}

async function navigateToReferenceFromUrl() {
  const query = new URLSearchParams(location.search)
  const pageValue = query.get('pageIndex')
  if (pageValue === null) return
  const pageIndex = Number(pageValue)
  if (!Number.isInteger(pageIndex) || pageIndex < 0) return
  goToPage(pageIndex)
  const page = await waitForPageElement(pageIndex)
  if (!page) return
  const scale = core.getPageFlow()?.getScale().scale || 1
  const x = Number(query.get('x')) || 0
  const y = Number(query.get('y')) || 0
  const width = Number(query.get('width')) || 1
  const height = Number(query.get('height')) || 1
  const marker = document.createElement('div')
  marker.className = 'reader-reference-marker'
  marker.style.left = `${x * scale}px`
  marker.style.top = `${y * scale}px`
  marker.style.width = `${Math.max(10, width * scale)}px`
  marker.style.height = `${Math.max(10, height * scale)}px`
  page.append(marker)
  marker.scrollIntoView({ block: 'center', inline: 'center', behavior: 'smooth' })
  setTimeout(() => marker.remove(), 2600)
}

function waitForPageElement(pageIndex) {
  const find = () => root.querySelector(`[data-inklayer-flow-page="${pageIndex}"][data-inklayer-flow-mounted="true"]`)
  if (find()) return Promise.resolve(find())
  return new Promise((resolve) => {
    const observer = new MutationObserver(() => {
      const page = find()
      if (!page) return
      clearTimeout(timeout)
      observer.disconnect()
      resolve(page)
    })
    const timeout = setTimeout(() => { observer.disconnect(); resolve(null) }, 8000)
    $('#reader-pages').addEventListener('scroll', () => {
      const page = find()
      if (page) { clearTimeout(timeout); observer.disconnect(); resolve(page) }
    }, { once: true, passive: true })
    observer.observe($('#reader-pages'), { subtree: true, childList: true, attributes: true, attributeFilter: ['data-inklayer-flow-mounted'] })
  })
}

async function saveAnnotations() {
  if (!core || readOnly || !dirty || saving) return
  saving = true
  $('#save-annotations').disabled = true
  setSaveStatus('Đang lưu…', 'saving')
  try {
    const annotations = core.annotations.repository.getAll()
    const result = await request(`${api}/${documentId}/annotations${versionQueryString()}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
      body: JSON.stringify(annotations)
    })
    dirty = false
    setSaveStatus(`Đã lưu ${result.count} annotation lúc ${new Date(result.savedAtUtc).toLocaleTimeString('vi-VN')}`)
  } catch (error) {
    setSaveStatus(`Lưu thất bại: ${error.message} · thay đổi vẫn còn trên màn hình`, 'failed')
  } finally {
    saving = false
    $('#save-annotations').disabled = readOnly
  }
}

function updateActiveTool(tool) {
  document.querySelectorAll('[data-tool]').forEach((button) => {
    button.classList.toggle('active', button.dataset.tool === tool)
  })
  $('#tool-eraser').classList.toggle('active', tool === 'eraser')
}

function setReaderTool(tool) {
  const wasActive = activeReaderTool === tool
  activeReaderTool = tool
  core.annotations.setTool(tool === 'eraser' ? 'select' : tool)
  updateActiveTool(tool)
  const hasSettings = ['free-highlight', 'freehand', 'eraser'].includes(tool)
  const shouldOpen = hasSettings && (!wasActive || !root.classList.contains('reader-settings-open'))
  $('#highlight-settings').hidden = !(shouldOpen && tool === 'free-highlight')
  $('#pen-settings').hidden = !(shouldOpen && tool === 'freehand')
  $('#eraser-settings').hidden = !(shouldOpen && tool === 'eraser')
  root.classList.toggle('reader-settings-open', shouldOpen)
  $('#tool-highlight').setAttribute('aria-expanded', String(shouldOpen && tool === 'free-highlight'))
  $('#tool-pen').setAttribute('aria-expanded', String(shouldOpen && tool === 'freehand'))
  $('#tool-eraser').setAttribute('aria-expanded', String(shouldOpen && tool === 'eraser'))
  if (tool === 'eraser') $('#reader-pages').classList.add('reader-erasing')
  else $('#reader-pages').classList.remove('reader-erasing')
}

function closeToolSettings() {
  $('#highlight-settings').hidden = true
  $('#pen-settings').hidden = true
  $('#eraser-settings').hidden = true
  root.classList.remove('reader-settings-open')
  $('#tool-highlight').setAttribute('aria-expanded', 'false')
  $('#tool-pen').setAttribute('aria-expanded', 'false')
  $('#tool-eraser').setAttribute('aria-expanded', 'false')
}

function deleteSelection() {
  const selected = core?.annotations.repository.getSelection().ids ?? []
  if (selected.length === 0) return setSaveStatus('Chọn annotation cần xóa trước.', 'failed')
  selected.forEach((id) => core.annotations.deleteAnnotation(id))
}

function setCommentsPanel(open) {
  const panel = $('#reader-comments')
  if (panel.parentElement !== root) {
    panel.classList.remove('reader-comments-popover')
    panel.removeAttribute('style')
    root.append(panel)
  }
  panel.hidden = !open
  $('#toggle-comments').setAttribute('aria-expanded', String(open))
  if (open) renderComments()
}

function createCommentAtPointer(event) {
  if (captureMode) return
  if (activeReaderTool === 'eraser') return
  if (event.target.closest('#reader-comments, input, textarea, button, a')) return
  const page = event.target.closest('.inklayer-page-flow-page')
  if (!page || !core || readOnly) return
  event.preventDefault()
  event.stopPropagation()
  const pageIndex = Number(page.dataset.inklayerFlowPage)
  const scale = core.getPageFlow()?.getScale().scale || 1
  const rect = page.getBoundingClientRect()
  const width = rect.width / scale
  const height = rect.height / scale
  const noteSize = Math.min(32, width, height)
  const pointerX = (event.clientX - rect.left) / scale
  const pointerY = (event.clientY - rect.top) / scale
  const existingNote = core.annotations.repository.getByPage(pageIndex).find((item) =>
    item.type === 'note' && pointerX >= item.bounds.x && pointerX <= item.bounds.x + item.bounds.width &&
    pointerY >= item.bounds.y && pointerY <= item.bounds.y + item.bounds.height)
  if (existingNote) {
    openCommentForAnnotation(existingNote.id, event.clientX - rect.left, event.clientY - rect.top)
    return
  }
  const x = Math.max(0, Math.min(width - noteSize, pointerX))
  const y = Math.max(0, Math.min(height - noteSize, pointerY))
  try {
    const annotation = core.annotations.createAnnotation({
      type: 'note',
      pageIndex,
      bounds: { x, y, width: noteSize, height: noteSize },
      content: { text: '' }
    })
    activeCommentAnnotationId = annotation.id
    core.annotations.setSelection({ ids: [annotation.id], primaryId: annotation.id }, 'programmatic')
    openCommentForAnnotation(annotation.id, (event.clientX - rect.left), (event.clientY - rect.top))
  } catch (error) {
    setSaveStatus(`Không tạo được bình luận: ${error.message}`, 'failed')
  }
}

function openCommentForAnnotation(annotationId, x, y) {
  const annotation = core?.annotations.repository.getById(annotationId)
  if (!annotation) return
  const page = root.querySelector(`[data-inklayer-flow-page="${annotation.pageIndex}"]`)
  if (!page) return
  const scale = core.getPageFlow()?.getScale().scale || 1
  const left = x ?? annotation.bounds.x * scale
  const top = y ?? annotation.bounds.y * scale
  const panel = $('#reader-comments')
  activeCommentAnnotationId = annotationId
  panel.hidden = false
  panel.classList.add('reader-comments-popover')
  if (panel.parentElement !== page) page.append(panel)
  const panelWidth = Math.min(360, Math.max(260, page.clientWidth - 16))
  panel.style.width = `${panelWidth}px`
  panel.style.left = `${Math.max(8, Math.min(page.clientWidth - panelWidth - 8, left + 18))}px`
  panel.style.top = `${Math.max(8, Math.min(page.clientHeight - 260, top + 18))}px`
  $('#toggle-comments').setAttribute('aria-expanded', 'true')
  renderComments()
  $('#comment-input').focus()
}

function renderOpenCommentPopover() {
  const panel = $('#reader-comments')
  if (!panel.classList.contains('reader-comments-popover') || panel.hidden) return
  renderComments()
}

function syncCommentTarget(selectedIds) {
  activeCommentAnnotationId = selectedIds?.[0] ?? null
  renderComments()
}

function renderComments() {
  if (!core) return
  const annotations = core.annotations.repository.getAll()
  const total = annotations.reduce((count, item) => count + (item.comments?.length ?? 0), 0)
  $('#comment-count').textContent = String(total)
  const selected = annotations.find((item) => item.id === activeCommentAnnotationId)
  const thread = $('#comment-thread')
  const form = $('#comment-form')
  const hint = $('#comment-target-hint')
  thread.replaceChildren()
  form.hidden = readOnly || !selected

  if (!selected) {
    hint.textContent = 'Chọn một ghi chú, vùng tô sáng hoặc annotation trên trang để xem và bình luận.'
    thread.textContent = total ? `Tài liệu có ${total} bình luận. Chọn annotation để mở luồng tương ứng.` : 'Chưa có bình luận. Nhấp đúp vào vị trí trên PDF để đặt ghim và mở hội thoại.'
    return
  }

  hint.textContent = `Trang ${selected.pageIndex + 1} · ${annotationLabel(selected.type)} · ${selected.comments?.length ?? 0} bình luận`
  $('#comment-input').disabled = readOnly
  form.querySelector('button[type="submit"]').disabled = readOnly
  const comments = selected.comments ?? []
  if (!comments.length) {
    thread.textContent = 'Chưa có bình luận trong luồng này. Viết bình luận đầu tiên bên dưới.'
    return
  }

  comments.forEach((comment) => {
    const card = document.createElement('article')
    card.className = 'reader-comment'
    const heading = document.createElement('div')
    heading.className = 'reader-comment-heading'
    const author = document.createElement('strong')
    author.textContent = comment.author?.name || comment.title || 'Người dùng'
    const date = document.createElement('time')
    date.textContent = comment.date ? new Date(comment.date).toLocaleString('vi-VN') : ''
    heading.append(author, date)
    const body = document.createElement('p')
    body.textContent = comment.content
    card.append(heading, body)
    if (!readOnly && comment.author?.id === (root.dataset.userId || 'current-user')) {
      const actions = document.createElement('div')
      actions.className = 'reader-comment-actions'
      actions.append(makeCommentAction('Sửa', () => editComment(selected.id, comment.id, comment.content)))
      actions.append(makeCommentAction('Xóa', () => removeComment(selected.id, comment.id)))
      card.append(actions)
    }
    thread.append(card)
  })
}

async function postComment(event) {
  event.preventDefault()
  const annotation = core?.annotations.repository.getById(activeCommentAnnotationId)
  const content = $('#comment-input').value.trim()
  if (!annotation || !content) return
  try {
    core.annotations.addComment(annotation.id, {
      id: crypto.randomUUID(),
      title: root.dataset.userName || 'Bạn',
      content,
      author: { id: root.dataset.userId || 'current-user', name: root.dataset.userName || 'Bạn' },
      date: new Date().toISOString(),
      status: 'None'
    })
    $('#comment-input').value = ''
    renderComments()
  } catch (error) {
    setSaveStatus(`Không gửi được bình luận: ${error.message}`, 'failed')
  }
}

function editComment(annotationId, commentId, previousContent) {
  const content = window.prompt('Chỉnh sửa bình luận', previousContent)?.trim()
  if (!content || content === previousContent) return
  try {
    core.annotations.updateComment(annotationId, commentId, (comment) => ({ ...comment, content, date: new Date().toISOString() }))
    renderComments()
  } catch (error) {
    setSaveStatus(`Không sửa được bình luận: ${error.message}`, 'failed')
  }
}

function removeComment(annotationId, commentId) {
  if (!window.confirm('Xóa bình luận này?')) return
  try {
    core.annotations.deleteComment(annotationId, commentId)
    renderComments()
  } catch (error) {
    setSaveStatus(`Không xóa được bình luận: ${error.message}`, 'failed')
  }
}

function makeCommentAction(label, onClick) {
  const button = document.createElement('button')
  button.type = 'button'
  button.className = 'btn btn-sm btn-link p-0'
  button.textContent = label
  button.addEventListener('click', onClick)
  return button
}

function annotationLabel(type) {
  return ({ note: 'Ghi chú', 'free-highlight': 'Tô sáng', highlight: 'Tô sáng chữ', freehand: 'Bút', rectangle: 'Khung' })[type] || 'Annotation'
}

function goToPage(pageIndex) {
  const snapshot = core?.viewer.getSnapshot()
  const pageCount = snapshot?.document?.numPages ?? 0
  if (!pageCount) return
  const target = Math.max(0, Math.min(pageCount - 1, pageIndex))
  core.getPageFlow()?.scrollToPage(target)
  $('#reader-page-number').value = String(target + 1)
}

function updatePageNavigation(pageIndex) {
  $('#reader-page-number').value = String(pageIndex + 1)
}

async function searchDocument() {
  const query = $('#reader-search').value.trim()
  if (!query || !core) return clearSearchResults()
  $('#reader-search-status').textContent = 'Đang tìm…'
  try {
    const result = await core.viewer.search(query, { maxResults: 1000 })
    searchQuery = query
    searchMatches = [...result.matches]
    activeSearchIndex = searchMatches.length ? 0 : -1
    if (activeSearchIndex >= 0) showSearchResult()
    $('#reader-search-status').textContent = searchMatches.length
      ? `${activeSearchIndex + 1}/${searchMatches.length}${result.truncated ? '+' : ''}`
      : 'Không có kết quả'
    updateSearchNavigation()
  } catch (error) {
    $('#reader-search-status').textContent = `Tìm kiếm lỗi: ${error.message}`
  }
}

function moveSearchResult(direction) {
  if (!searchMatches.length) return
  activeSearchIndex = (activeSearchIndex + direction + searchMatches.length) % searchMatches.length
  showSearchResult()
  $('#reader-search-status').textContent = `${activeSearchIndex + 1}/${searchMatches.length}`
}

function showSearchResult() {
  const match = searchMatches[activeSearchIndex]
  if (!match) return
  core.viewer.setSearchHighlights(searchMatches, activeSearchIndex)
  core.getPageFlow()?.scrollToPage(match.pageIndex)
}

function updateSearchNavigation() {
  const disabled = searchMatches.length === 0
  $('#reader-search-next').disabled = disabled
  $('#reader-search-previous').disabled = disabled
}

function clearSearchResults() {
  searchMatches = []
  activeSearchIndex = -1
  searchQuery = ''
  core?.viewer.clearSearchHighlights()
  $('#reader-search-status').textContent = ''
  updateSearchNavigation()
}

function updatePenAppearance() {
  const width = Number($('#pen-width').value)
  const opacity = Number($('#pen-opacity').value) / 100
  $('#pen-width-value').value = String(width)
  $('#pen-width-value').textContent = String(width)
  $('#pen-opacity-value').value = `${Math.round(opacity * 100)}%`
  $('#pen-opacity-value').textContent = `${Math.round(opacity * 100)}%`
  core?.annotations.setToolAppearance('freehand', {
    stroke: { color: $('#pen-color').value, width, opacity, lineCap: 'round', lineJoin: 'round' }
  })
}

function updateHighlightAppearance() {
  const width = Number($('#highlight-width').value)
  const opacity = Number($('#highlight-opacity').value) / 100
  $('#highlight-width-value').value = String(width)
  $('#highlight-width-value').textContent = String(width)
  $('#highlight-opacity-value').value = `${Math.round(opacity * 100)}%`
  $('#highlight-opacity-value').textContent = `${Math.round(opacity * 100)}%`
  core?.annotations.setToolAppearance('free-highlight', {
    stroke: { color: $('#highlight-color').value, width, opacity, lineCap: 'round', lineJoin: 'round' }
  })
}

function beginReaderGesture(event) {
  if (captureMode === 'area') return beginAreaCaptureGesture(event)
  beginEraserGesture(event)
}

function continueReaderGesture(event) {
  if (areaGesture) return continueAreaCaptureGesture(event)
  continueEraserGesture(event)
}

function finishReaderGesture(event) {
  if (areaGesture) return void finishAreaCaptureGesture(event)
  finishEraserGesture(event)
}

function beginAreaCaptureGesture(event) {
  if (event.button !== 0 || !activeNoteId || activeNoteArchived || readOnly) return
  const page = event.target.closest('.inklayer-page-flow-page')
  if (!page) return
  event.preventDefault()
  event.stopPropagation()
  const start = pagePoint(event, page)
  const overlay = document.createElement('div')
  overlay.className = 'reader-capture-selection'
  page.append(overlay)
  areaGesture = { pointerId: event.pointerId, page, pageIndex: Number(page.dataset.inklayerFlowPage), start, current: start, overlay }
  $('#reader-pages').setPointerCapture?.(event.pointerId)
  updateAreaOverlay()
}

function continueAreaCaptureGesture(event) {
  if (!areaGesture || event.pointerId !== areaGesture.pointerId) return
  event.preventDefault()
  event.stopPropagation()
  areaGesture.current = pagePoint(event, areaGesture.page)
  updateAreaOverlay()
}

function updateAreaOverlay() {
  if (!areaGesture) return
  const scale = core.getPageFlow()?.getScale().scale || 1
  const x = Math.min(areaGesture.start.x, areaGesture.current.x)
  const y = Math.min(areaGesture.start.y, areaGesture.current.y)
  const width = Math.abs(areaGesture.current.x - areaGesture.start.x)
  const height = Math.abs(areaGesture.current.y - areaGesture.start.y)
  Object.assign(areaGesture.overlay.style, {
    left: `${x * scale}px`, top: `${y * scale}px`, width: `${width * scale}px`, height: `${height * scale}px`
  })
}

async function finishAreaCaptureGesture(event) {
  if (!areaGesture || event.pointerId !== areaGesture.pointerId) return
  event.preventDefault()
  event.stopPropagation()
  const gesture = areaGesture
  areaGesture = null
  gesture.overlay.remove()
  if ($('#reader-pages').hasPointerCapture?.(event.pointerId)) $('#reader-pages').releasePointerCapture(event.pointerId)
  if (event.type !== 'pointerup') return
  const rawEnd = pagePoint(event, gesture.page)
  const scale = core.getPageFlow()?.getScale().scale || 1
  const pageRect = gesture.page.getBoundingClientRect()
  const end = {
    x: Math.max(0, Math.min(pageRect.width / scale, rawEnd.x)),
    y: Math.max(0, Math.min(pageRect.height / scale, rawEnd.y))
  }
  const anchor = {
    x: Math.min(gesture.start.x, end.x),
    y: Math.min(gesture.start.y, end.y),
    width: Math.abs(end.x - gesture.start.x),
    height: Math.abs(end.y - gesture.start.y)
  }
  if (anchor.width < 8 || anchor.height < 8) {
    setNoteStatus('Vùng chụp quá nhỏ. Kéo chọn vùng lớn hơn.', 'failed')
    return
  }
  captureBusy = true
  updateNoteButtons()
  const previousContents = noteEditor.getContents()
  const previousDirty = noteDirty
  try {
    const image = cropPdfArea(gesture.page, anchor)
    const index = Math.max(0, noteEditor.getLength() - 1)
    noteEditor.insertEmbed(index, 'image', image, 'user')
    noteEditor.insertText(index + 1, '\n', 'user')
    await persistCapture('Area', gesture.pageIndex, anchor, null, previousContents, previousDirty)
  } catch (error) {
    noteEditor.setContents(previousContents, 'silent')
    noteDirty = previousDirty
    setNoteStatus(`Không chụp được vùng PDF: ${error.message}`, 'failed')
  } finally {
    captureBusy = false
    cancelCaptureMode()
    updateNoteButtons()
  }
}

function cropPdfArea(page, anchor) {
  const source = page.querySelector('.inklayer-page-flow-canvas') || page.querySelector('canvas')
  if (!(source instanceof HTMLCanvasElement) || !source.width || !source.height)
    throw new Error('Không tìm thấy canvas của trang PDF đã render.')
  const scale = core.getPageFlow()?.getScale().scale || 1
  const pageRect = page.getBoundingClientRect()
  const canvasRect = source.getBoundingClientRect()
  const cssScaleX = source.width / canvasRect.width
  const cssScaleY = source.height / canvasRect.height
  const left = Math.max(0, Math.floor((anchor.x * scale - (canvasRect.left - pageRect.left)) * cssScaleX))
  const top = Math.max(0, Math.floor((anchor.y * scale - (canvasRect.top - pageRect.top)) * cssScaleY))
  const right = Math.min(source.width, Math.ceil((anchor.x + anchor.width) * scale * cssScaleX - (canvasRect.left - pageRect.left) * cssScaleX))
  const bottom = Math.min(source.height, Math.ceil((anchor.y + anchor.height) * scale * cssScaleY - (canvasRect.top - pageRect.top) * cssScaleY))
  const cropWidth = right - left
  const cropHeight = bottom - top
  if (cropWidth < 2 || cropHeight < 2) throw new Error('Vùng chọn nằm ngoài phần hiển thị của trang PDF.')
  const downscale = Math.min(1, 1400 / Math.max(cropWidth, cropHeight))
  const output = document.createElement('canvas')
  output.width = Math.max(1, Math.round(cropWidth * downscale))
  output.height = Math.max(1, Math.round(cropHeight * downscale))
  const context = output.getContext('2d')
  if (!context) throw new Error('Trình duyệt không tạo được ảnh vùng chụp.')
  context.drawImage(source, left, top, cropWidth, cropHeight, 0, 0, output.width, output.height)
  return output.toDataURL('image/jpeg', 0.84)
}

function beginEraserGesture(event) {
  if (captureMode) return
  if (activeReaderTool !== 'eraser' || event.button !== 0) return
  const page = event.target.closest('.inklayer-page-flow-page')
  if (!page) return
  event.preventDefault()
  event.stopPropagation()
  const point = pagePoint(event, page)
  eraserGesture = { pointerId: event.pointerId, page, pageIndex: Number(page.dataset.inklayerFlowPage), points: [point] }
  $('#reader-pages').setPointerCapture?.(event.pointerId)
}

function continueEraserGesture(event) {
  if (!eraserGesture || event.pointerId !== eraserGesture.pointerId) return
  event.preventDefault()
  event.stopPropagation()
  const point = pagePoint(event, eraserGesture.page)
  const previous = eraserGesture.points.at(-1)
  if (Math.hypot(point.x - previous.x, point.y - previous.y) >= 2) eraserGesture.points.push(point)
}

function finishEraserGesture(event) {
  if (!eraserGesture || event.pointerId !== eraserGesture.pointerId) return
  event.preventDefault()
  event.stopPropagation()
  const gesture = eraserGesture
  eraserGesture = null
  if (event.type === 'pointerup') {
    const point = pagePoint(event, gesture.page)
    const previous = gesture.points.at(-1)
    if (Math.hypot(point.x - previous.x, point.y - previous.y) > 0) gesture.points.push(point)
    eraseStrokeSegments(gesture)
  }
  if ($('#reader-pages').hasPointerCapture?.(event.pointerId)) $('#reader-pages').releasePointerCapture(event.pointerId)
}

function pagePoint(event, page) {
  const scale = core.getPageFlow()?.getScale().scale || 1
  const rect = page.getBoundingClientRect()
  return { x: (event.clientX - rect.left) / scale, y: (event.clientY - rect.top) / scale }
}

function eraseStrokeSegments(gesture) {
  const scale = core.getPageFlow()?.getScale().scale || 1
  const radius = Number($('#eraser-size').value) / scale / 2
  const path = gesture.points
  const pageAnnotations = core.annotations.repository.getByPage(gesture.pageIndex)
  for (const annotation of pageAnnotations) {
    if (!['freehand', 'free-highlight'].includes(annotation.type)) continue
    const lines = readAnnotationLines(annotation)
    if (!lines.length) continue
    let changed = false
    const fragments = []
    for (const line of lines) {
      const split = splitLineAroundEraser(line, path, radius + (annotation.appearance.stroke?.width ?? 1) / 2)
      changed ||= split.changed
      fragments.push(...split.fragments)
    }
    if (!changed) continue

    core.annotations.deleteAnnotation(annotation.id)
    const replacements = fragments
      .filter((points) => points.length >= 4)
      .map((points) => {
        const bounds = pointBounds(points)
        return core.annotations.createAnnotation({
          type: annotation.type,
          pageIndex: annotation.pageIndex,
          bounds,
          points,
          appearance: annotation.appearance,
          content: annotation.content,
          extensions: annotation.extensions
        })
      })
    if (replacements[0]) {
      for (const comment of annotation.comments ?? []) core.annotations.addComment(replacements[0].id, comment)
    }
  }
}

function readAnnotationLines(annotation) {
  try {
    const rootNode = JSON.parse(annotation.rendererState.serialized)
    const lines = []
    const visit = (node) => {
      if (node?.className === 'Line' && Array.isArray(node.attrs?.points)) lines.push(node.attrs.points)
      node?.children?.forEach(visit)
    }
    visit(rootNode)
    return lines
  } catch {
    return []
  }
}

function splitLineAroundEraser(points, eraserPath, radius) {
  const dense = []
  const spacing = Math.max(1, Math.min(4, radius * 0.4))
  for (let index = 0; index < points.length - 2; index += 2) {
    const x1 = points[index]
    const y1 = points[index + 1]
    const x2 = points[index + 2]
    const y2 = points[index + 3]
    const distance = Math.hypot(x2 - x1, y2 - y1)
    const steps = Math.max(1, Math.ceil(distance / spacing))
    for (let step = index === 0 ? 0 : 1; step <= steps; step++) {
      const ratio = step / steps
      dense.push({ x: x1 + (x2 - x1) * ratio, y: y1 + (y2 - y1) * ratio })
    }
  }
  if (points.length >= 2 && dense.length === 0) dense.push({ x: points[0], y: points[1] })

  let changed = false
  let current = []
  const fragments = []
  for (const point of dense) {
    if (distanceToPath(point, eraserPath) <= radius) {
      changed = true
      if (current.length >= 4) fragments.push(current)
      current = []
      continue
    }
    current.push(point.x, point.y)
  }
  if (current.length >= 4) fragments.push(current)
  return { changed, fragments: changed ? fragments : [points] }
}

function distanceToPath(point, path) {
  if (path.length === 1) return Math.hypot(point.x - path[0].x, point.y - path[0].y)
  let closest = Infinity
  for (let index = 0; index < path.length - 1; index++) {
    closest = Math.min(closest, pointToSegmentDistance(point, path[index], path[index + 1]))
  }
  return closest
}

function pointToSegmentDistance(point, start, end) {
  const dx = end.x - start.x
  const dy = end.y - start.y
  const lengthSquared = dx * dx + dy * dy
  const ratio = lengthSquared ? Math.max(0, Math.min(1, ((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared)) : 0
  return Math.hypot(point.x - (start.x + ratio * dx), point.y - (start.y + ratio * dy))
}

function pointBounds(points) {
  let left = Infinity
  let top = Infinity
  let right = -Infinity
  let bottom = -Infinity
  for (let index = 0; index < points.length; index += 2) {
    left = Math.min(left, points[index])
    right = Math.max(right, points[index])
    top = Math.min(top, points[index + 1])
    bottom = Math.max(bottom, points[index + 1])
  }
  return { x: left, y: top, width: Math.max(1, right - left), height: Math.max(1, bottom - top) }
}

async function updatePageCount(pageCount) {
  try {
    await request(`${api}/${documentId}/page-count${versionQueryString()}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
      body: JSON.stringify({ pageCount })
    })
  } catch (error) {
    console.warn('[M1] Page count metadata was not updated', error)
  }
}

function versionQueryString() {
  return documentVersionId ? `?versionId=${encodeURIComponent(documentVersionId)}` : ''
}

async function request(url, options = {}) {
  const response = await fetch(url, { credentials: 'same-origin', ...options })
  if (response.redirected) {
    location.href = '/Login'
    throw new Error('Phiên đăng nhập đã hết hạn.')
  }
  const body = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) {
    if (response.status === 401 || response.status === 403) location.href = '/Login'
    throw new Error(body?.message || body?.detail || `HTTP ${response.status}`)
  }
  return body
}

function setSaveStatus(message, state = 'saved') {
  const status = $('#annotation-save-status')
  status.textContent = message
  status.dataset.state = state
}

function formatBytes(bytes) {
  return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
