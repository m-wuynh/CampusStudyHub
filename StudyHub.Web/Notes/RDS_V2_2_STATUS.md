# Notes & PDF — RDS v2.2 implementation status

Checked 2026-10-18 against `StudyHub_Notes_PDF_RDS_v2_2_Closed_Business_Realtime_3Layer.docx` and the current working tree. This is evidence and a gap list, not an acceptance certificate.

## Implemented in the working tree

- Existing MVC Document Library, local PDF upload/storage/reader, personal annotations, Quill personal Notes, Ctrl+S, capture and PDF references remain in use. The personal reader now sends the `Document.RowVersion` ETag with `If-Match`; concurrent cross-tab whole-set annotation saves return conflict rather than overwrite. Save responses preserve edits made during a pending request.
- Active StudyGroup members can create, edit and soft-delete GroupNotes directly. GroupNote creation and editing now use the existing Quill package; Ctrl+S saves the open GroupNote. Membership and SQL Server RowVersion are checked on writes. The old ChangeRequest API returns 410 and its approval UI is hidden; existing request rows remain for audit.
- Active members can save Group PDF annotations directly with whole-set RowVersion conflict detection. PDF Share/Unshare management remains restricted to the source owner.
- The Notes SignalR room checks membership when a client joins. Notifications now target only currently Active group members. An **unapplied** Notes SQL outbox migration and hosted publisher stage GroupNote, Group PDF annotation, Share/Unshare and source archive/delete events in the same SQL write/transaction. The worker claims events with a lease and retries failed sends. Until the outbox table is deployed, these paths still attempt direct delivery after a successful write.
- Archive/delete of a source PDF revokes active shares. Group PDF reads check the active share, membership, source state, active source owner and active owner membership.
- A 30-day soft-deleted GroupNote list and restore-as-copy action are present. An Active member can copy an active GroupNote into a private Note. These copies accept text-only Quill deltas and reject embedded content to avoid copying group-only assets without provenance checks. Relevant actions write AuditLogs.
- PDF upload, personal annotation save, rename, archive/restore and soft-delete now add AuditLogs in their existing SQL writes. GroupNote archive/restore also writes an audit record and queues or sends a change notification.
- A reviewed, **unapplied** SQL migration `StudyHub.DAL/Migrations/Notes/20261016_AddPdfShareAnnotationSessions.sql` defines annotation layers keyed by `(GroupDocumentShareId, DocumentVersionId)`. After that table is present, new shares create clean layers and re-share is allowed; old session rows remain for history. Before the table is present, legacy annotation storage still works and re-share of the same PDF/group remains blocked to prevent old annotation exposure.
- `StudyHub.DAL/Migrations/Notes/20261016_AddNotesOutbox.sql` is staged for review only. No outbox delivery has been tested against an installed table.
- `StudyHub.DAL/Migrations/Notes/20261018_AddGroupNoteRevisions.sql` is staged for review only. When installed, GroupNote create/import/edit/delete/archive and restore-as-copy writes an immutable snapshot with the content change. Active members can list up to 50 revisions from the past 30 days and restore a text-only revision as a new GroupNote. Existing notes receive only a migration-time baseline; earlier edits cannot be reconstructed. GroupNote archive/restore now also requires a RowVersion token.

## Verification

### Group PDF sharing smoke test (2026-10-18)

- Removed the Notes library's Development-only "Kiểm thử Owner/Member" button. The Notes StudyGroup gateway now reads only the membership/group fields needed for access checks; this avoids an API 500 against LocalDB missing `GroupMembers.BannedByUserId` and `UnbanRequestedAtUtc`. StudyGroup source and schema were not changed.
- Development account owner shared existing PDF `DocumentId=28` into the existing one-member test group `StudyGroupId=3` using `POST /api/notes/documents/28/shares`: PASS. The share is **left active** (`GroupDocumentShareId=5`) for manual review. Share listing, library `shareCount=1`, group workspace, group reader details and PDF file `HEAD` (`application/pdf`) all passed. Duplicate share returned HTTP 409.
- Local Notes library returned HTTP 200 without the Owner/Member button. Four existing personal Notes listed and one opened successfully. The page notebook API returns a clear HTTP 503 until its separate mapping table is installed. Authenticated API checks passed; a second-account member view and browser visual check were not run.

### PDF page notebook (2026-10-18)

- Personal PDF Reader now shows only notes belonging to the open PDF, grouped by page. Creating a note on the current page reopens its existing note. The API checks PDF ownership and rejects capture from a different page or PDF. Standalone notes remain available in the library.
- `StudyHub.DAL/Migrations/Notes/20261018_AddPdfPageNotebook.sql` is staged, **not applied**. It creates a separate `PdfPageNotes` mapping table with one note per PDF page, leaving the existing `Notes` schema intact. It assigns only legacy notes whose references identify one unambiguous PDF page. Other legacy notes remain standalone for manual review.
- Web build, npm build and JavaScript syntax checks: PASS. Authenticated personal Notes GET passed on the current database; PDF page notebook writes and SQL migration tests: **NOT RUN**. The current database lacks the mapping table, so page notebooks are not yet runnable against it.

### Flexcil-inspired reader layout (2026-10-18)

- The personal PDF reader now uses a PDF-first split view with a draggable, keyboard-accessible divider for the Note pane. The Note pane width is remembered locally; tablet and phone widths stack PDF above the Note pane. The editor uses a warm paper surface while existing Quill, capture and reference flows remain unchanged.
- The personal library now presents PDF and Note rows as a responsive file-card grid with document icons, metadata, share status and a separate action row; filtering, sorting and API contracts are unchanged.
- Web build, staged npm build, `node --check` and targeted `git diff --check`: PASS. The changed `notesReaderM2.js` entry bundle was copied and hash checked. Authenticated browser visual testing remains unverified because the current database lacks Grades columns reached by the normal Dashboard redirect after Development login.

### Unified Notes layout pass (2026-10-18)

- Library now has a left file-manager navigation rail and document grid; Group workspace uses a matching PDF/Notes/recovery rail. On narrow screens the rails become horizontal navigation.
- Standalone personal Note now shows a centered paper-like Quill page with the PDF/reference panel on the right, stacked below on narrow screens. Existing note save, references and split-reader navigation remain wired to their existing IDs.
- Web and npm builds PASS; the library entry bundle was copied and hash checked. Local `GET /Notes` returned 302 to Login without a session. An authenticated visual inspection was not performed; starting the app also logged missing Calendar schema columns from an unrelated background worker. The local app process was stopped. No schema or data change was made.

### Notes UI clarity pass (2026-10-18)

- Library upload and new-note actions are separated into labeled cards; filters, search and row actions have larger targets. The share dialog keeps its title, impact summary, group selector and revoke/open actions in reading order.
- The personal PDF toolbar is now in normal page flow with visible action names, including on narrow screens; it no longer overlays the PDF. The personal Note editor, group workspace cards, GroupNote Quill forms and group PDF toolbar have clearer spacing and focus targets.
- `dotnet build StudyHub.Web/StudyHub.Web.csproj`, staged npm build, `node --check` and targeted `git diff --check`: PASS. The changed `groupWorkspace.js` entry bundle was copied and hash checked. Authenticated visual/browser testing was not run because the current database/schema mismatch breaks the normal dashboard redirect after Development login; this pass made no database write.

### Document note and group sharing usability pass (2026-10-18)

- Library PDF rows now show the active share count. The share dialog reads owner-only active shares, identifies groups that already have access, prevents a duplicate share click, links to the PDF in the group, and lets the owner revoke access with confirmation. The dialog explains that the private source PDF remains in the owner's library and the group annotation layer is separate. Revocation from the group workspace also requires confirmation.
- Creating a personal Note goes directly to its editor with the title selected. Creating one beside a PDF selects its title and gives a short capture instruction. The reader Note panel scrolls when its controls and references exceed the available height; controls have larger targets.
- Web build and staged npm build: PASS. `node --check` for the edited Notes sources: PASS. Four changed entry bundles were copied and SHA-256 matched against staged output. No database write or authenticated browser run was performed for this usability pass.
- The existing `git diff --check` reports trailing whitespace in previously modified generated `wwwroot/notes/index.html` and line-ending warnings; this pass did not change that file.

- `dotnet build StudyHub.Web/StudyHub.Web.csproj`: PASS on 2026-10-18, 0 errors, with pre-existing warnings in BLL EmailService and Web `_ViewImports.cshtml`. An initial sandboxed build could not read AppData NuGet.Config; the same command succeeded outside that sandbox.
- `npm.cmd run build` into a separate Notes staging directory: PASS, 993 modules. Changed entry bundles were copied into `wwwroot/notes/assets`; existing shared chunks and other local assets were preserved. Vite reports large PDF-related chunks as a size warning.
- `node --check` on changed Notes frontend sources: PASS.
- Read-only SQL preflight against the configured SQL Server: PASS for existing Notes tables, reference column and GroupNote/GroupAnnotationSet RowVersion columns. The database has two active PDF shares and no active share with an earlier share history. There is one Active group and one Active member, so a two-account collaboration scenario is not verified.
- SQL Server migration history shows five pending migrations after `20260929060911_RemoveGroupMeetingLinkAndMakeCapacityOptional`, including StudyGroup changes. `msdb` reports a full backup completed 2026-10-14 13:15:35 server time; restoreability and the intended target database are unconfirmed.
- Authenticated browser/API writes, revoke race tests, two-account collaboration tests, and migration execution: **NOT RUN**. No database write or schema change was made in this pass.

## RDS v2.2 gaps and limitations

- GroupNote still saves a whole Quill delta. RowVersion rejects stale writes; there is no CRDT adapter, operation log, state epoch, safe offline replay or per-user undo. The staged revision snapshots are not a CRDT operation log.
- Group PDF annotations still save one JSON set per version/share session. There is no per-annotation operation/version/tombstone/restore model or merge of concurrent edits to different annotations.
- The session table is not deployed. Runtime detection falls back to the legacy table. The manual SQL migration was used because generating an EF migration from the current snapshot also attempted to recreate existing Notes tables and alter unrelated StudyGroup schema; the EF model snapshot was left untouched. Future EF migration work must reconcile this baseline before generation.
- The SQL outbox is staged but not deployed or tested. It uses at-least-once delivery, so clients may see duplicate reload notifications. It still lacks a per-group event sequence, gap resynchronization and a published-row retention policy; direct-delivery fallback can lose a notification after commit.
- No verified StudyGroup membership-change event, forced room removal, authorization lease, reason-coded share hold/pause/resume, moderation hold/report integration or dissolved-group cleanup. Recipient filtering limits subsequent Notes broadcasts to currently Active members, but an already downloaded PDF cannot be removed from a browser.
- The 30-day soft-delete list is an access window. The staged revision table offers immutable snapshots but is not deployed, has no verified purge/retention job and cannot recover pre-migration history. Text-only restore drops rich formatting. Attachment provenance, quarantine and content-safe export remain unimplemented.
- Server PDF checks cover extension, size and `%PDF-` header/version only. Parseability, encryption/active-content policy and malware scanning are not established. Storage is local rather than Cloudinary.

## Database safety gate

Do not apply any of the four Notes migrations to the current SQL Server until the target database, restorable backup and five pending migrations have been reviewed with the team. All four migration files are staged for review only.
