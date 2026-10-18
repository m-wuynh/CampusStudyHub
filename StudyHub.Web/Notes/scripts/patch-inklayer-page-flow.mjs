import { readdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const moduleRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const distPath = resolve(moduleRoot, 'node_modules/@inklayer-dev/core/dist')
const chunk = (await readdir(distPath)).find((name) => /^composition-root-.*\.js$/.test(name))

if (!chunk) throw new Error('InkLayer Core composition chunk was not found.')

const chunkPath = resolve(distPath, chunk)
const source = await readFile(chunkPath, 'utf8')
const methodStart = source.indexOf('async initialize(e = !0) {')
const methodEnd = source.indexOf('\n\tgetCurrentPage() {', methodStart)

if (methodStart < 0 || methodEnd < 0) throw new Error('InkLayer page-flow initialization method was not found.')

const currentMethod = source.slice(methodStart, methodEnd)
if (currentMethod.includes('/* M0 fast first page */')) {
  console.log('InkLayer fast first-page rendering is already enabled.')
  process.exit(0)
}
if (!currentMethod.includes('for (let e = 0; e < t.numPages; e += 1)')) {
  throw new Error('InkLayer page-flow internals changed; refusing to apply an unverified patch.')
}

const fastMethod = `async initialize(e = !0) {
\t\t/* M0 fast first page */
\t\tlet t = this.options.viewer.getSnapshot().document;
\t\tif (t === null) throw g("The PDF document was replaced before page flow initialized.");
\t\tlet n = this.options.container;
\t\te && (this.scale = await this.resolveScale(this.scaleValue));
\t\tn.replaceChildren(), n.classList.add("inklayer-page-flow"), n.style.display = "grid", n.style.gap = (this.options.pageGap ?? 16) + "px", n.style.alignItems = "start";
\t\tlet pageOne = await t.document.getPage(1);
\t\tthis.assertActive();
\t\tlet i = pageOne.getViewport({ scale: this.scale });
\t\tfor (let e = 0; e < t.numPages; e += 1) {
\t\t\tlet r = u(n.ownerDocument, e, i.width, i.height);
\t\t\tthis.shells.set(e, r), n.append(r.root);
\t\t}
\t\tthis.observer = new IntersectionObserver((e) => this.handleIntersections(e), {
\t\t\troot: n,
\t\t\trootMargin: this.options.overscan ?? "100% 0px",
\t\t\tthreshold: [0, .01, .25, .5, .75, 1]
\t\t});
\t\tfor (let e of this.shells.values()) this.observer.observe(e.root);
\t\tthis.scrollListener = () => this.updateCurrentPageFromViewport(), n.addEventListener("scroll", this.scrollListener, { passive: !0 });
\t\tthis.resizeObserver === null && typeof ResizeObserver == "function" && (this.resizeObserver = new ResizeObserver(() => {
\t\t\t!m(this.scaleValue) || this.destroyed || this.setScale(this.scaleValue).catch((e) => this.options.onError?.(_(e, this.currentPage)));
\t\t}), this.resizeObserver.observe(n));
\t\tawait this.mountPage(0);
\t\tthis.zoomGesture === null && this.options.enablePinchZoom !== !1 && (this.gestureScale = this.scale, this.zoomGesture = r({
\t\t\tcontainer: n,
\t\t\tgetScale: () => this.gestureScale,
\t\t\tsetScale: (e, t) => { this.gestureScale = e, this.queueGestureScale(e, t); },
\t\t\tcenterAtAnchor: !1,
\t\t\tminScale: this.minScale,
\t\t\tmaxScale: this.maxScale
\t\t}));
\t}`

let patchedSource = source.slice(0, methodStart) + fastMethod + source.slice(methodEnd)
const mountStart = patchedSource.indexOf('\tasync mountPage(e) {')
const mountEnd = patchedSource.indexOf('\n\tunmountPage(e) {', mountStart)
if (mountStart < 0 || mountEnd < 0) throw new Error('InkLayer page mount method was not found.')
const mountMethod = patchedSource.slice(mountStart, mountEnd)
const canvasSizing = '\t\t\t\tt.canvas.width = Math.ceil(r.width * r.pixelRatio), t.canvas.height = Math.ceil(r.height * r.pixelRatio), t.canvas.style.width = `${r.width}px`, t.canvas.style.height = `${r.height}px`, t.canvas.getContext("2d")?.drawImage(i, 0, 0, t.canvas.width, t.canvas.height);'
if (!mountMethod.includes(canvasSizing)) throw new Error('InkLayer page canvas sizing changed; refusing an unverified patch.')
const pageSizing = '\t\t\t\tt.root.style.width = `${r.width}px`, t.root.style.height = `${r.height}px`,\n' + canvasSizing
patchedSource = patchedSource.slice(0, mountStart) + mountMethod.replace(canvasSizing, pageSizing) + patchedSource.slice(mountEnd)

await writeFile(chunkPath, patchedSource)
console.log('InkLayer renders page 1 first and sizes later pages only when they enter view.')
