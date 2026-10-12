import { createInkLayer } from '@inklayer-dev/core/capabilities'
import '@inklayer-dev/core/style'
import Quill from 'quill'
import 'quill/dist/quill.snow.css'
import { createLocalPdfRangeFetch } from './pdf-local-range.js'
import '../styles/app.css'

const $ = (selector) => document.querySelector(selector)
const annotationsKey = 'flexcilclone.m0.annotations'
const noteKey = 'flexcilclone.m0.quill-delta'
const buttons = ['#select-text', '#highlight', '#rectangle', '#save-annotations', '#restore-annotations', '#zoom-out', '#zoom-in', '#export-pdf']
let core = null
let sourceBytes = null
let sourceFile = null
let sourceUrl = null
let documentHandle = null
let stopViewerEvents = null
let rejectFirstPageRender = null

const quill = new Quill('#note-editor', {
  theme: 'snow',
  modules: { toolbar: [['bold', 'italic', 'underline'], [{ list: 'ordered' }, { list: 'bullet' }], ['blockquote', 'link'], ['clean']] },
  placeholder: 'Gõ thử tiếng Việt: Chào bạn, đây là ghi chú kiểm tra Unicode.'
})

$('#pdf-file').addEventListener('change', async (event) => {
  const file = event.target.files?.[0]
  if (!file) return
  const startedAt = performance.now()
  setPdfStatus(`Đang đọc ${file.name} (${formatBytes(file.size)})…`)
  await new Promise(requestAnimationFrame)
  try {
    if (core) {
      stopViewerEvents?.()
      await core.destroy()
      core = null
    }
    stopViewerEvents = null
    sourceBytes = null
    sourceFile = null
    documentHandle = null
    if (sourceUrl) URL.revokeObjectURL(sourceUrl)
    sourceUrl = null
    buttons.forEach((selector) => { $(selector).disabled = true })
    const header = new TextDecoder().decode(await file.slice(0, 1024).arrayBuffer())
    if (!header.includes('%PDF-')) {
      setPdfStatus('File này không có PDF header hợp lệ. Hãy chọn file PDF khác.')
      return
    }
    sourceFile = file
    sourceUrl = URL.createObjectURL(file)
    setPdfStatus('Đang mở trang đầu bằng tải từng phần…')
    core = await createInkLayer({
      root: $('#pdf-workspace'),
      viewer: { fetch: createLocalPdfRangeFetch(file, sourceUrl) },
      pageFlow: {
        container: $('#pages'),
        scale: 'page-width',
        pixelRatio: 1,
        overscan: '0px',
        onError: (error) => {
          console.error('[M0] PDF page render failed', error)
          rejectFirstPageRender?.(new Error(error?.message || 'InkLayer không render được trang PDF.'))
        }
      }
    })
    stopViewerEvents = core.viewer.subscribe((message) => {
      if (message.type === 'loadProgress') {
        const phase = message.progress.phase === 'parsing' ? 'Đang phân tích PDF' : 'Đang đọc dữ liệu PDF'
        const percent = message.progress.percentage
        setPdfStatus(`${phase}${percent == null ? '…' : ` · ${percent}%`}`)
      }
      if (message.type === 'error') {
        console.error('[M0] InkLayer viewer error', message.error)
        setPdfStatus(formatError(message.error))
      }
    })
    documentHandle = await core.load({ url: sourceUrl, range: 'auto', rangeChunkSize: 64 * 1024 })
    await waitForFirstPageRender()
    const elapsedSeconds = ((performance.now() - startedAt) / 1000).toFixed(1)
    setPdfStatus(`${file.name} · ${documentHandle.numPages} trang · sẵn sàng sau ${elapsedSeconds}s`)
    buttons.forEach((selector) => { $(selector).disabled = false })
  } catch (error) {
    console.error('[M0] Could not initialize/load PDF', error)
    stopViewerEvents?.()
    stopViewerEvents = null
    await core?.destroy()
    core = null
    if (sourceUrl) URL.revokeObjectURL(sourceUrl)
    sourceUrl = null
    sourceFile = null
    sourceBytes = null
    setPdfStatus(formatError(error))
  }
})

$('#select-text').addEventListener('click', () => core?.annotations.setTool('text-select'))
$('#highlight').addEventListener('click', () => {
  const active = core?.viewer.getTextSelection()
  if (!active) return setAnnotationStatus('Hãy chọn một đoạn chữ trước.')
  const selections = active.kind === 'page' ? [active.selection] : active.selection.fragments
  for (const selection of selections) core.annotations.createTextMarkup('highlight', selection)
  core.viewer.clearTextSelection()
  setAnnotationStatus(`${core.annotations.repository.getAll().length} annotation đang có.`)
})
$('#rectangle').addEventListener('click', () => core?.annotations.setTool('rectangle'))
$('#save-annotations').addEventListener('click', () => {
  localStorage.setItem(annotationsKey, JSON.stringify(core.annotations.repository.getAll()))
  setAnnotationStatus('Đã lưu annotation vào localStorage.')
})
$('#restore-annotations').addEventListener('click', () => {
  const saved = JSON.parse(localStorage.getItem(annotationsKey) || '[]')
  core.annotations.repository.replaceAll(saved)
  setAnnotationStatus(`Đã nạp ${saved.length} annotation.`)
})
$('#zoom-in').addEventListener('click', () => core?.getPageFlow()?.zoomIn())
$('#zoom-out').addEventListener('click', () => core?.getPageFlow()?.zoomOut())
$('#export-pdf').addEventListener('click', async () => {
  if (!sourceFile || !core) return
  try {
    sourceBytes ??= new Uint8Array(await sourceFile.arrayBuffer())
    const { buildAnnotatedPdf } = await import('@inklayer-dev/core/export/pdf')
    const output = await buildAnnotatedPdf(sourceBytes, core.annotations.repository.getAll(), { annotationTypes: core.annotationTypes })
    download(output, 'annotated-spike.pdf', 'application/pdf')
  } catch (error) {
    setAnnotationStatus(`Không export được PDF: ${error.message}`)
  }
})
$('#save-note').addEventListener('click', () => {
  localStorage.setItem(noteKey, JSON.stringify(quill.getContents()))
  $('#note-status').textContent = 'Đã lưu Delta'
})
$('#restore-note').addEventListener('click', () => {
  const saved = localStorage.getItem(noteKey)
  if (!saved) return $('#note-status').textContent = 'Chưa có Delta đã lưu'
  quill.setContents(JSON.parse(saved))
  $('#note-status').textContent = 'Đã nạp Delta'
})
$('#run-gateway').addEventListener('click', async () => {
  const list = $('#gateway-results')
  list.replaceChildren(document.createElement('li'))
  list.firstElementChild.textContent = 'Đang chạy…'
  try {
    const response = await fetch('/phase0/group-gateway-checks')
    if (!response.ok) throw new Error(`HTTP ${response.status}; hãy chạy ASP.NET ở Development.`)
    const result = await response.json()
    list.replaceChildren(...result.cases.map((test) => {
      const item = document.createElement('li')
      item.className = test.passed ? 'pass' : 'fail'
      item.textContent = `${test.passed ? 'PASS' : 'FAIL'} · ${test.name} · ${test.actualAllowed ? 'allowed' : 'denied'}`
      return item
    }))
  } catch (error) {
    list.replaceChildren()
    const item = document.createElement('li')
    item.className = 'fail'
    item.textContent = `Không gọi được gateway: ${error.message}`
    list.append(item)
  }
})

function setPdfStatus(message) { $('#pdf-status').textContent = message }
function setAnnotationStatus(message) { $('#annotation-status').textContent = message }
function formatBytes(bytes) {
  return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function waitForFirstPageRender() {
  const pages = $('#pages')
  const firstPage = () => pages.querySelector('[data-inklayer-flow-page="0"][data-inklayer-flow-mounted="true"]')
  if (firstPage()) return Promise.resolve()

  return new Promise((resolve, reject) => {
    let settled = false
    const finish = (callback, value) => {
      if (settled) return
      settled = true
      clearTimeout(timeout)
      observer.disconnect()
      rejectFirstPageRender = null
      callback(value)
    }
    const observer = new MutationObserver(() => {
      if (firstPage()) finish(resolve)
    })
    const timeout = setTimeout(() => finish(reject, new Error('Đã đọc PDF nhưng trang đầu không render sau 15 giây.')), 15000)
    rejectFirstPageRender = (error) => finish(reject, error)
    observer.observe(pages, { subtree: true, attributes: true, attributeFilter: ['data-inklayer-flow-mounted'] })
    if (firstPage()) finish(resolve)
  })
}
function formatError(error) {
  const details = [error?.code, error?.message, error?.cause?.message].filter(Boolean)
  return `Không mở được PDF: ${details.join(' · ') || 'Lỗi không xác định. Mở DevTools Console để xem chi tiết.'}`
}
function download(bytes, filename, type) {
  const url = URL.createObjectURL(new Blob([bytes], { type }))
  const link = Object.assign(document.createElement('a'), { href: url, download: filename })
  link.click()
  URL.revokeObjectURL(url)
}
