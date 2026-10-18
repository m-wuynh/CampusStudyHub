# Notes & PDF — Milestone 0

M0 replaces the previous Notes screens and services with a technical spike inside the existing `StudyHub.Web` application. It verifies InkLayer Core, Quill 2, and the Study Group adapter boundary. It does not implement persistent PDF or Note workflows yet.

## Run

Requirements: .NET 9 SDK, Node.js 22.13+ (or 24+), npm, and the existing StudyHub SQL Server development database/configuration.

```powershell
cd StudyHub.Web/Notes
npm ci
npm run build
cd ../..
dotnet run --project StudyHub.Web/StudyHub.Web.csproj --launch-profile https
```

Open `https://localhost:7054/Notes`. The M0 fake-boundary check exists only in Development; M1–M4 use the application database, authentication and real Study Group service.

## Check the spike

1. Choose a local PDF. The status shows the document name, page count, and time until the viewer is ready.
2. Select text and apply a highlight, or draw a rectangle.
3. Save annotations, reload them, and verify they stay on the same PDF coordinates after zooming.
4. Export the annotated PDF and open the downloaded file.
5. Enter Vietnamese text in Quill, save the Delta, reload the page, then restore it.
6. Run the three Study Group gateway checks. Each result should show `PASS`.

## M0 limits

- The selected PDF is read from the browser file picker; it is not uploaded to storage.
- Annotation and Quill Delta test data stay in the current browser's `localStorage`.
- M0's fixed `FakeStudyGroupGateway` is instantiated only by its Development smoke endpoint. Normal Notes routes always use `StudyGroupGateway` through `IStudyGroupService`.
- No previous Notes controllers, services, repositories, views, or JavaScript are used by the spike.

## PDF startup performance

The file picker checks only the PDF header, then keeps the browser `File` in place instead of copying the whole file into an `ArrayBuffer`. A local Fetch adapter implements `HEAD` and byte-range (`206 Partial Content`) responses from `File.slice()`, so InkLayer/PDF.js can request only the byte ranges needed to open page 1. It renders page 1 first and requests later page contents when they enter view. Full-file bytes are read only if PDF export is requested. The viewer renders at pixel ratio 1 with zero overscan. `npm run build` reapplies the guarded SDK patch and fails if the pinned SDK internals change unexpectedly.

`pdfjs-dist` is pinned to 6.2.108 to match the worker bundled with InkLayer Core 0.3.0.

## Verification performed

- `npm ci --ignore-scripts` — PASS in the packaging environment.
- `npm ls pdfjs-dist --all` — PASS; `6.2.108` is resolved.
- `npm run build` — PASS; assets emitted to `StudyHub.Web/wwwroot/notes`.
- `node --check client/src/main.js` and `node --check scripts/patch-inklayer-page-flow.mjs` — PASS.
- Local range adapter check — PASS for HEAD metadata, exact 64 KB range responses, and invalid-range rejection.
- PDF.js range-loading check — PASS on a generated 600-page PDF; document metadata and page 1 loaded after requesting 164,712 of 295,784 bytes; later-page content was not fetched.
- `npm ci` normal lifecycle — not completed in this sandbox because esbuild's install script is blocked from spawning its validation binary; `npm ci --ignore-scripts`, the guarded patch, and the Vite build all passed. A normal Windows `npm ci` runs the same patch as `postinstall`.
- .NET build and interactive PDF test — not run in this environment because the .NET SDK and browser are unavailable.

## M3 + M4 — StudyGroup sharing and approval

Apply SQL scripts `06_M1_NotesPdf.sql`, `07_M2_NotesReferences.sql`, then `08_M3_GroupApproval.sql` in that order. Run `09_M3_M4_Preflight.sql` before starting the web app. Detailed Owner/Member smoke tests are in `M3_M4_GroupApproval_TEST_VI.md` at the solution root.

In Development, open `/Notes/DevelopmentRoles` to switch between the Owner and an Active member of an existing group. If the database has only one active account and no real Owner/Member pair, the page can create a clearly named Notes smoke-test group through the existing StudyGroup service and simulate the Member branch with the same account. This creates no user or fake membership. The simulation checks Notes UI/API branches and ChangeRequest behavior; it does not prove authorization between two distinct users. It is unavailable outside Development.

In StudyGroup Details, open **Tài nguyên** and select **Notes & PDF workspace**. The Library also has a **Chia sẻ vào nhóm** action for active PDFs and personal Notes. PDF sharing creates a separate annotation set for each `(StudyGroupId, DocumentVersionId)`. Sharing a personal Note copies its title and Delta into an independent GroupNote snapshot.

Group members can read shared content and submit CREATE/UPDATE/DELETE ChangeRequests. The content owner applies or rejects each request after membership, lifecycle and rowversion checks. Approval, official content mutation and the existing `AuditLogs` entry are committed together. Direct member PUTs to official Note/annotation endpoints return 403. Every group read, PDF stream, and write rechecks membership through the real Study Group service adapter.
