import { defineConfig } from 'vite'
import { resolve } from 'node:path'

const moduleRoot = import.meta.dirname

export default defineConfig({
  root: resolve(moduleRoot, 'client'),
  base: '/notes/',
  build: {
    outDir: resolve(moduleRoot, '../wwwroot/notes'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        spike: resolve(moduleRoot, 'client/index.html'),
        notesLibrary: resolve(moduleRoot, 'client/src/library.js'),
        notesReaderM2: resolve(moduleRoot, 'client/src/reader.js'),
        noteEditorM2: resolve(moduleRoot, 'client/src/note-editor.js'),
        groupReader: resolve(moduleRoot, 'client/src/group-reader.js'),
        groupWorkspace: resolve(moduleRoot, 'client/src/group-workspace.js')
      },
      output: {
        entryFileNames: 'assets/[name].js',
        chunkFileNames: 'assets/chunks/[name]-[hash].js',
        assetFileNames: (assetInfo) => assetInfo.name === 'inklayer-core.css'
          ? 'assets/inklayer-core.css'
          : assetInfo.name?.startsWith('quill') && assetInfo.name.endsWith('.css')
            ? 'assets/notes-quill.css'
            : 'assets/[name]-[hash][extname]'
      }
    }
  },
  server: {
    proxy: { '/phase0': { target: 'https://localhost:7054', secure: false } }
  }
})
