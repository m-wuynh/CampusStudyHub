export function createLocalPdfRangeFetch(file, fileUrl) {
  return async (input, init = {}) => {
    if (String(input) !== fileUrl) return globalThis.fetch(input, init)
    if (init.signal?.aborted) throw new DOMException('PDF range request was aborted.', 'AbortError')

    const method = (init.method ?? 'GET').toUpperCase()
    const headers = new Headers({
      'Accept-Ranges': 'bytes',
      'Content-Type': 'application/pdf',
      'Content-Length': String(file.size)
    })

    if (method === 'HEAD') return new Response(null, { status: 200, headers })
    if (method !== 'GET') return new Response(null, { status: 405, headers: { Allow: 'GET, HEAD' } })

    const range = new Headers(init.headers).get('Range')
    if (!range) return new Response(file, { status: 200, headers })

    const match = /^bytes=(\d+)-(\d+)$/.exec(range)
    if (!match) return new Response(null, { status: 416, headers: { 'Content-Range': `bytes */${file.size}` } })

    const start = Number(match[1])
    const end = Number(match[2])
    if (start > end || end >= file.size) {
      return new Response(null, { status: 416, headers: { 'Content-Range': `bytes */${file.size}` } })
    }

    headers.set('Content-Length', String(end - start + 1))
    headers.set('Content-Range', `bytes ${start}-${end}/${file.size}`)
    return new Response(file.slice(start, end + 1), { status: 206, headers })
  }
}
