# DiskAnalyze — Development Log

Living document: project history, current state, known issues, roadmap, design notes and decision log.
Update it together with the code: add a History entry per meaningful change, tick roadmap items,
and record non-obvious choices in the Decision Log.

- **What:** Windows disk-usage analyzer. It scans drives, shows where space goes (pie and bar views
  with drill-down) and finds files duplicated between two folders.
- **Goal:** personal tool. Usability, speed and useful features come before release engineering
  (installer, signing, auto-update). See D-018.
- **Stack:** C# / WPF / MVVM (hand-rolled), currently `net8.0` + `net8.0-windows`.
- **Build / run:**
  ```
  dotnet build DiskAnalyze.sln
  dotnet run --project DiskAnalyze/DiskAnalyze.csproj
  ```

---

## 1. Architecture snapshot

```
DiskAnalyze.Application (net8.0, UI-agnostic)
├─ AnalyzeService/   AnalyzeDiskService → DirectoryTreeScanner (parallel, Channel-based)
├─ DiskEntities/     BaseFileSystemEntity, DirectoryEntityInfo (Children, CalculateSize), FileEntityInfo
└─ Duplicates/       DuplicateFinderService (size+ext prefilter → SHA-256), DuplicateFileGroup

DiskAnalyze (net8.0-windows, WPF)
├─ Core/             ViewModelBase, RelayCommand, FileSizeFormatter
├─ Services/         FileSystemService: domain tree → view-model tree, drive usage %
├─ ViewModels/       MainViewModel (view switching, loading), DiagramViewModelBase
│                    (navigation, side panel, Show in Explorer), Circular/Chart/Duplicates VMs
├─ Views/            MainWindow, CircularDiagramView (pie geometry + hit testing in code-behind),
│                    ChartDiagramView, DuplicatesView, SegmentContentsView (side panel)
└─ Converters/       size, color/brush, visibility, percent→width, bool→GridLength, scale
```

Data flow: `MainViewModel` constructor → `FileSystemService.LoadFileSystemDataAsync()` →
`AnalyzeAllDrivesAsync()` scans every fixed drive → full domain tree → converted to a full VM tree
off the UI thread → the same VM tree is shared by the Circular and Chart views.
The Duplicates view runs its own independent scans of the two selected folders.

Composition root: `MainWindow` constructor (no DI container).

---

## 2. History

| Date | Commit | Summary |
|---|---|---|
| 2026-06-11 | `fac6e25` | Repository created (MIT license only). |
| 2026-06-30 | `6661c76` | Whole application landed in one commit (≈2.5k lines): parallel scanner, pie and bar diagrams with drill-down, "Other" bucket, hover tooltips, segment-contents side panel, drive usage %, two-folder duplicate finder. The commit message describes only the final UI fixes: diagram hidden while loading, side-panel column collapses to zero width when empty, "Show in Explorer" context menu. Earlier iterations are not in git. |
| 2026-11-14 | — | Project review; this document created. Build is clean (0 warnings). No tests exist. |
| 2026-11-14 | — | Planned two features: folder content types (§6.1) and a photo browser with content filters (§6.2, feasibility analysis). Confirmed I-8 on the dev machine. |

---

## 3. Current state — what works

**Scanning**
- [x] Scans all ready fixed drives at startup, in parallel (one worker per CPU core).
- [x] Skips junctions and symlinks (no cycles or double counting).
- [x] Silently skips inaccessible folders (`UnauthorizedAccessException` / `IOException`).
- [x] Directory sizes computed in a single post-pass.
- [x] Drive roots show real "% used" from `DriveInfo`.

**Visualization**
- [x] Pie view: top 5 children plus an "Other" slice, hover magnify with tooltip, click a folder to drill down, "Up" button.
- [x] Bar view: all non-empty children sorted by size, click a folder to drill down.
- [x] Context menu: "Show contents" (side panel listing immediate children, largest first) and "Show in Explorer" (folders open, files are revealed selected; hidden for "Other").
- [x] Loading overlay and status bar.

**Duplicates**
- [x] Compare two folders: prefilter by (size, extension), confirm with SHA-256, side-by-side grid, full paths in a tooltip.
- [x] Guards against the same physical file appearing on both sides (but see issue I-1).

---

## 4. Known issues & tech debt

Found in the 2026-11-14 review. Each item has a concrete location and failure scenario.

### Bugs
- **I-1. Nested folder selection loses real duplicates.** In `DuplicateFinderService.cs:52-54`, a right-side file is dropped if its path appears anywhere on the left. Example: A = `D:\Photos`, B = `D:\Photos\2020`, with identical `D:\Photos\x.jpg` and `D:\Photos\2020\x.jpg`. Left = `[Photos\x.jpg, Photos\2020\x.jpg]`, right = `[Photos\2020\x.jpg]`. The right file is removed because it is also on the left, so the group is discarded and the genuine duplicate is never shown. Fix: remove the overlapping paths from the superset side instead, then require both sides to be non-empty. Cover this case with a test.
- **I-2. Hover brightening never renders.** `Fill="{Binding Converter=FileSystemEntityToBrushConverter}"` binds to the entity object itself (`CircularDiagramView.xaml:67`, `ChartDiagramView.xaml:75`). A change to `IsHovered` doesn't re-run the converter, so the bright-color branch in `FileSystemEntityToBrushConverter.cs:18` is dead in practice; only the scale effect works. Fix: use a `MultiBinding` on (entity, `IsHovered`) or a `DataTrigger`.
- **I-3. Pie slices often share a color.** The color is picked by name hash from 7 colors (`FileSystemEntityToColorConverter.cs:34`). For 5 slices, P(all distinct) = 7·6·5·4·3 / 7⁵ ≈ 15%, so about 85% of directories show at least two same-colored slices. Fix: color pie slices by index; keep hash colors only where stability across views matters.

### Performance / scalability
- **I-4. Bar view is not virtualized.** `ItemsControl` inside a `ScrollViewer` (`ChartDiagramView.xaml:56-57`) creates a visual for every child. A folder with tens of thousands of files (caches, `node_modules`, mail stores) freezes the UI. Fix: use `ListBox`/`VirtualizingStackPanel`, or show the top N plus "Other".
- **I-5. Whole tree materialized twice.** At peak, the domain tree and a full VM tree exist together. Every file VM carries pie geometry fields (arc points, label and tooltip coordinates) although at most 6 entities are drawn. Fix: create VMs lazily per displayed directory and move geometry out of the entity VM.
- **I-6. Allocation-heavy enumeration.** `DirectoryInfo.GetFiles()`/`GetDirectories()` allocate full `FileInfo`/`DirectoryInfo` arrays per directory. `FileSystemEnumerable<T>` with a struct transform is much cheaper.

### Correctness of numbers
- **I-7. Logical sizes, not on-disk sizes.** Hard links (e.g. `C:\Windows\WinSxS`) are counted once per link; compressed and sparse files show full logical length. The scanned total therefore doesn't match "used space".
- **I-8. OneDrive is skipped entirely (confirmed 2026-11-14).** The `%OneDrive%` root and all its subfolders carry the `ReparsePoint` attribute with no link type (cloud-files reparse tag, not a junction or symlink). The blanket reparse-point skip in `DirectoryTreeScanner.cs:74` therefore drops the whole OneDrive tree. Cloud-only files (attributes `RecallOnDataAccess | Offline | Sparse | ReparsePoint`, e.g. `0x401620`) report their full logical size although they use no local space. Hashing them in the duplicate finder would download them. Fix: skip only real links (`LinkTarget != null`); count cloud-only files as 0 bytes on disk; never open them for hashing.
- **I-9. The scanned vs. used gap is invisible.** Space in inaccessible folders (without admin rights) and filesystem metadata isn't shown anywhere.

### UX gaps
- **I-10. No way to choose, cancel or rescan.** The scan starts fire-and-forget from the `MainViewModel` constructor (`MainViewModel.cs:53`) for all drives. No `CancellationToken` reaches the UI; there's no Rescan, folder or drive picker. `AnalyzeDriveAsync` exists but is unused.
- **I-11. No scan progress.** Only an indeterminate bar is shown, and full scans can take minutes.
- **I-12. Duplicates view limits.** It rescans both folders from disk instead of reusing the loaded tree. It has no cancel, progress or result actions (open, reveal, delete). The status counts rows, not groups or reclaimable bytes. It can't find duplicates *within* one folder.
- **I-13. Minor UI.** Labels of small pie slices overlap. Bar width is fixed at 500 px regardless of window size (`PercentToWidthConverter.MaxWidth`). Toolbar buttons don't show the active view. Clicking a file or the "Other" slice does nothing.

### Hygiene
- **I-14. Dead code.** `Resources/Styles.xaml` is never merged into resources. `DefaultButtonStyle` in `App.xaml` is unused. `AnalyzeDriveAsync` is unused. `LastModified` is collected but never displayed.
- **I-15. No tests and no README.** The Application layer is UI-agnostic and easy to test with temp directories.
- **I-16. .NET 8 reached end of support on 2026-11-10.** The machine has the `WindowsDesktop.App 10.0.13` runtime but only a .NET 10 RC SDK. Install the .NET 10 GA SDK (current LTS) before retargeting.
- **I-17. Recursive tree walks.** `CalculateSize`, `ConvertToViewModel` and `Flatten` recurse; this only matters for pathological depth. Low priority.

---

## 5. Roadmap

Priorities follow the "personal tool" goal: fix what misleads, then make it pleasant for daily use,
then add analysis features. Release engineering (installer, signing, auto-update, CI) is out of scope
unless the goal changes.

### Now — correctness & foundation
- [ ] Retarget to `net10.0` / `net10.0-windows` (I-16).
- [ ] Fix nested duplicates (I-1) — **before** adding any delete action.
- [ ] Fix hover brush (I-2); index-based pie palette (I-3).
- [ ] Virtualize or cap the bar view (I-4).
- [ ] Remove dead code (I-14).
- [ ] Add a `DiskAnalyze.Application.Tests` (xUnit) project for size calculation, reparse-point skipping and duplicate grouping, including the nested case. Delete actions planned below make this logic data-critical.

### Next — daily-use comfort
- [ ] Scan target picker (drive list plus "Choose folder…"), **Rescan** and **Cancel** with a `CancellationToken` plumbed end to end (I-10).
- [ ] Live progress: folders and files scanned, bytes, current path, elapsed time (I-11).
- [ ] Clickable breadcrumb path; Back/Forward, including mouse X1/X2 buttons.
- [ ] Context menu: Open, Copy path, **Delete to Recycle Bin** (with confirmation). Update sizes up the parent chain in place without a rescan.
- [ ] Drive level: show "Free" and "Unscanned / inaccessible" (used − scanned) segments (I-9); add a "Restart as administrator" action.
- [ ] Highlight the active view in the toolbar; hide labels for slices under ~4% (I-13).

### Feature A — Folder content types (design: §6.1)
Shows what each folder mostly holds: images, video, audio, documents, archives, code, apps, mixed.
- [ ] **A1. Core.** `FileCategory` enum plus an extension map (~150 extensions). Per-directory bytes by category, filled in the size post-pass. `FolderKind` classifier: dominant category at ≥ 60% of bytes, otherwise Mixed; project markers (`.git`, `*.sln`, `*.csproj`, `package.json`) override to Project. Unit tests.
- [ ] **A2. Color mode.** A "Color: By size rank | By content" toggle, shared by the pie and bar views and kept when switching between them. Add a legend.
- [ ] **A3. Content rendering.** Pie: each slice in the color of its folder kind; Mixed is neutral, with the breakdown in the tooltip. Bars: stacked composition bars (each bar split by category).
- [ ] **A4. Current folder.** Composition strip under the path bar; category icons in the side panel and bars.
- [ ] **A5. Folders by kind.** Flat list of folders of a given kind sorted by size (e.g. all video folders), together with the Largest files list. "Browse photos here" on Images folders leads into Feature B.

### Feature B — Photo browser with content filters (design: §6.2)
A separate window for viewing photos, with filters such as sunsets, people, city, documents and screenshots. Analysis runs locally.
- [ ] **B0. Spike** (console app, about one evening). Run a CLIP ViT-B/32 image encoder on ONNX Runtime with a GPU execution provider over ~500 own photos with ~20 precomputed prompts. Measure throughput and judge precision by eye. Check that Shell thumbnails of cloud-only OneDrive files don't trigger a download.
- [ ] **B1. Photo window, metadata only.** Virtualized thumbnail grid. Filters: folder scope, EXIF date range, camera, type (photo, screenshot, RAW). Preview with EXIF; actions: Open, Show in Explorer, Copy path, Delete to Recycle Bin. Useful even without ML.
- [ ] **B2. Content categories.** Background indexing with progress and cancel. SQLite index cache (key: path + size + mtime) stores a CLIP embedding per photo. Fixed categories come from precomputed prompt embeddings, so no tokenizer is needed; category chips show counts.
- [ ] **B3. Search & similar.** Free-text search ("dog on the beach") using the text encoder and CLIP BPE tokenizer. "Similar photos" groups (bursts, near-duplicates) with reclaimable space, which the SHA-256 duplicate finder can't detect.
- [ ] **B4. Optional.** Face detector (YuNet/SCRFD, a few MB) for reliable people/portrait/group filters; user-defined categories via a prompt editor; threshold tuning per category.

### Later — analysis features
- [ ] **Largest files** flat list across the whole tree, with size/extension/age filters.
- [ ] **Treemap** (squarified) and/or **sunburst** (2–3 levels at once): denser than a one-level pie. Reuses the A2 color modes.
- [ ] **Old files**: filter by `LastModified` (data already collected).
- [ ] **Cleanup suggestions**: `%TEMP%`, browser and package caches (`%LOCALAPPDATA%\NuGet`, npm), dev build output (`bin/`, `obj/`, `.vs/`, `node_modules/`), `Windows.old`, Recycle Bin, old items in Downloads.
- [ ] **Snapshots & diff**: save a compact scan summary, compare with a later scan ("what grew since last week").
- [ ] **Duplicates v2**: within a single folder or drive; reuse the loaded tree; partial-hash prefilter (first and last 64 KB) before the full hash; progress and cancel; reclaimable-space total; "keep one, recycle the rest" (I-12).
- [ ] Export the current view or list to CSV.

### Performance track (pick up when scans feel slow)
- [ ] `FileSystemEnumerable<T>` with `EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }` (I-6).
- [ ] Lazy VM creation; geometry out of the entity VM (I-5).
- [ ] On-disk (allocated) size and hard-link de-duplication by file ID, optionally behind a setting (I-7).
- [ ] Cache the last scan for an instant start and refresh in the background.
- [ ] Optional: read the NTFS MFT directly (admin only), which gives order-of-magnitude faster full-drive scans.

### Maybe
- Dark theme; name search/filter; `DiskAnalyze.exe <path>` command-line argument plus an Explorer
  "Analyze with DiskAnalyze" entry; `FileSystemWatcher` live updates for the open folder.

---

## 6. Design notes

### 6.1 Folder content types — how to display (decided: D-019)

**Categories.** Images, Video, Audio, Documents (pdf, doc[x], xls[x], ppt[x], txt, md, odt…),
Archives (zip, 7z, rar, iso…), Code (cs, js, ts, py, json… plus build output), Apps & system
(exe, dll, msi, sys…), Other. A folder's *kind* is one of these, or Mixed, Project or Empty.

**Classification by bytes, not by file count.** This is a disk analyzer, so the question is what takes
the space. A folder is of kind X when X makes up ≥ 60% of its subtree's bytes; otherwise it is Mixed.
Markers override: a `.git` folder, `*.sln`/`*.csproj` or `package.json` make it Project. Counting by
files would label a folder of 3 movies and 500 thumbnails as "Images", which is misleading here.

**Cost.** One `long[8]` per directory, filled during the existing size post-pass in O(total
nodes). That is about 100 bytes per directory, so ≈ 50 MB for 500k directories, which is acceptable
now and can be compacted later. Computing on demand would traverse the whole tree on every
navigation near the root, so precompute.

**Display options considered**

| Option | Pros | Cons |
|---|---|---|
| A. Separate 4th view "Content" | Simple to build in isolation | Duplicates navigation and the side panel; switching views loses your place; content type is an *attribute* of a folder, not a different structure. |
| **B. Color mode on existing views** | Same navigation and place; works for pie, bars and future treemap/sunburst; color finally carries meaning (complements the I-3 fix) | Pie shows only the dominant kind; Mixed is less informative (mitigated by tooltip and strip). |
| C. Stacked composition bars | Shows the exact mix per row | Only fits the bar view. |
| D. Flat "folders by kind" list | Answers "where are all my videos?" directly | Not a map of the disk. |

**Recommendation: B + C + D, no separate view.** A "Color: By size rank | By content" toggle lives
in `MainViewModel` and is shared by both diagrams. In content mode, the pie colors slices by folder kind
(Mixed is neutral, with the breakdown in the tooltip), and the bar view draws stacked composition bars,
which is C as the bar view's rendering of B. A composition strip under the path bar shows the current
folder's mix. A legend explains the colors. D becomes a list next to "Largest files" and the entry
point to the photo browser.

**Fixed category palette** (distinct from the rank palette): Images orange, Video purple, Audio
teal, Documents blue, Archives brown, Code green, Apps & system slate, Other light gray, Mixed
neutral gray.

### 6.2 Photo content analysis — feasibility (decided: D-020)

**Verdict: feasible, locally and for free, with good quality for scene-level filters** (sunset,
beach, mountains, city/street, night, snow, food, pets, people/selfie/group, documents/receipts,
screenshots). Fine-grained questions such as *which* person or landmark need different models and are
out of scope.

**Approaches compared**

| Approach | Fit for "sunset / people / city" | Custom filters | Offline | Verdict |
|---|---|---|---|---|
| Metadata only: EXIF date, GPS, camera, filename/size heuristics | No content, but cheap and useful filters | — | yes | **Phase B1**; complements ML |
| ImageNet classifier (ResNet/MobileNet via ML.NET or ONNX) | Object labels; poor at scenes and time of day | fixed 1000 | yes | no |
| Places365 scene classifier | Good scenes; no "sunset" or "people" | fixed 365 | yes | no |
| **CLIP / SigLIP zero-shot on ONNX Runtime** | Good at scenes, time of day, people, documents, screenshots | **any text prompt**, free-text search, similarity | yes | **chosen** |
| Windows AI APIs (Windows App SDK image description / object APIs) | Good | limited | yes | Needs a Copilot+ NPU; the dev machine has none |
| Cloud vision APIs (Azure, Google) | Best labels | fixed | **no**: uploads private photos; paid | no |
| Local captioning VLM (e.g. Florence-2) | Rich captions | any | yes | Heavy (~0.3–1 s per image); maybe later for captions |

**How CLIP filters work.** The image encoder turns each photo into a 512-d vector once; it is
cached at ~1 KB as float16. Each filter is one or more text prompts ("a photo of a sunset", "sunset
over the sea"), turned into vectors the same way. Score = cosine similarity; a photo gets a category
when its score clears a per-category threshold or wins a softmax against "other". The same vectors
give free-text search and "similar photos" (cosine > ~0.95: bursts and re-saves), which ties
directly into the app's purpose of reclaiming space. Search over 10k vectors is brute force and takes
under 10 ms.

**Pipeline**
1. **Source.** Image files from the existing scan tree (extensions), or a folder chosen in the photo window.
2. **Input pixels: Windows Shell thumbnails** (`IShellItemImageFactory`, 256 px; CLIP needs 224 px). They are fast thanks to the OS thumbnail cache and cover HEIC/RAW wherever codecs are installed. Most importantly, they work for **cloud-only OneDrive files without downloading the original**; B0 must confirm this. On the dev machine, Pictures is redirected to OneDrive and most images are cloud-only, so decoding originals would mean downloading the whole library. Fallback for local files: WIC decode with `DecodePixelWidth` (JPEG DCT scaling).
3. **Inference.** ONNX Runtime with a GPU execution provider (DirectML or CUDA on the RTX-class GPU; Windows ML packaging is another route — decide in B0), CPU fallback, batches of 32–64.
4. **Text side.** B2 ships precomputed prompt embeddings (JSON generated once), so the app needs no tokenizer. B3 adds the text encoder plus the CLIP BPE tokenizer (`Microsoft.ML.Tokenizers` with vocab and merges files) for free-text search.
5. **Index cache.** SQLite at `%LOCALAPPDATA%\DiskAnalyze\photos.db`, keyed by path + size + mtime. Columns: dimensions, EXIF date, GPS, camera, embedding, face count, category scores. Re-indexing is incremental.
6. **People (B4, optional).** A small face detector gives a face count and makes people/portrait/group filters more reliable than CLIP alone. Face *recognition* (who) is out of scope for privacy and complexity reasons.

**Estimates (to be confirmed in B0)**
- Model: ViT-B/32 image encoder ≈ 350 MB fp32, roughly 90–180 MB quantized or fp16. Downloaded on first use, never committed.
- Throughput: GPU inference is not the bottleneck; thumbnail extraction and decoding are. Expect tens to hundreds of photos per second, so 10k photos index in roughly 1–10 minutes the first time, and incremental runs are fast after that.
- Memory and disk: ~1 KB per photo for embeddings, plus the cache DB.

**Photo window UI (B1+).** A separate window, opened from the toolbar or from "Browse photos here" on
a folder. Left: filter panel with category chips and counts (multi-select), date range, folder scope,
type, and "Similar photos". Center: virtualized thumbnail grid. WPF has no built-in virtualizing wrap
panel, so use a package (e.g. `VirtualizingWrapPanel`) or a custom panel. Right: preview with EXIF and
actions.

**Risks**
- Thresholds need tuning on real photos, so ship per-category sliders.
- Model download size.
- The GPU EP package choice may shift (the DirectML vs. Windows ML direction).
- HEIC needs the HEIF codec installed.
- The index holds content embeddings: keep it local in `%LOCALAPPDATA%` and never sync it.

---

## 7. Decision log

Format: **D-NNN — title** (date, status). Entries D-001…D-017 were reconstructed from the code during
the 2026-11-14 review; their "why" is inferred and may be corrected.

**D-001 — Split into a UI-agnostic core and a WPF shell** (2026-06, accepted)
Scanning, entities and duplicate detection live in `DiskAnalyze.Application` (`net8.0`, no WPF).
Why: testable without UI; another front end stays possible. Trade-off: none significant.

**D-002 — Hand-rolled MVVM, no framework, no DI container** (2026-06, accepted)
`ViewModelBase`/`RelayCommand` are written in-house; services are wired in the `MainWindow` constructor.
Why: tiny app, zero dependencies. Revisit if the number of services grows (CommunityToolkit.Mvvm and
`Microsoft.Extensions.Hosting` are the natural next step).

**D-003 — Scan everything up front into an in-memory tree** (2026-06, accepted, revisit)
All fixed drives are scanned at startup; navigation is then instant. Trade-off: long startup, high
memory, no partial results (I-5, I-10).

**D-004 — Parallel scanner on a `Channel` work queue** (2026-06, accepted)
One worker per core reads directories from an unbounded channel and enqueues subdirectories; an
`Interlocked` outstanding counter completes the channel when it reaches zero. Each directory is owned
by exactly one worker, so `Children` needs no locks.

**D-005 — Do not follow reparse points** (2026-06, accepted, revisit)
Why: legacy junctions such as `C:\Users\All Users` create cycles and double counting. Trade-off: also
skips OneDrive cloud folders (I-8, confirmed); narrow it to real links.

**D-006 — Inaccessible folders are skipped silently** (2026-06, accepted)
Errors go only to `Debug.WriteLine`. Trade-off: the user can't see what was missed (I-9).

**D-007 — Directory sizes in a post-pass** (2026-06, accepted)
`CalculateSize()` runs once after the scan instead of propagating sizes concurrently. Why: no
synchronization on shared parents.

**D-008 — Size means logical file length** (2026-06, accepted, revisit)
Simple and fast; ignores compression, sparseness, cluster slack and hard links (I-7).

**D-009 — Pie shows top 5 plus "Other"; bars show everything** (2026-06, accepted)
Why: the pie stays readable, while the bar view gives the full list. "Other" is a synthetic VM
holding its members, so "Show contents" works on it.

**D-010 — Pie geometry in code-behind; hit test walks up to the owning DataContext** (2026-06, accepted)
Geometry is computed in `CircularDiagramView` and stored on the entity VMs. Hit testing walks up from
any visual to the element whose `DataContext` is an entity, because the tooltip and label overlay the
slice; matching only `Path` caused a hover flicker loop.

**D-011 — Stable name hash for colors** (2026-06, accepted, revisit)
Uses a custom hash because `string.GetHashCode` is randomized per process. Trade-off: frequent
collisions in the pie (I-3).

**D-012 — Drive "% used" comes from `DriveInfo`, not from the scanned total** (2026-06, accepted)
Why: the scanned total omits inaccessible files, so it under-reports.

**D-013 — Duplicate = same size + same extension + same SHA-256** (2026-06, accepted)
The extension is part of the key, so identical content under different extensions isn't reported.
Why: a cheap, very selective prefilter; hashing only runs on candidates present on both sides.

**D-014 — The duplicate finder scans folders independently** (2026-06, accepted, revisit)
It works without the main scan and on any folder. Trade-off: repeated disk I/O (I-12).

**D-015 — Side-panel column collapses to zero width when empty** (2026-06-30, accepted)
A `BooleanToGridLengthConverter` on the column width lets the diagram reclaim the space.

**D-016 — Diagram hidden while the loading overlay is visible** (2026-06-30, accepted)
Why: the empty pie showed through behind the progress bar at startup.

**D-017 — "Show in Explorer": folders open, files are revealed selected** (2026-06-30, accepted)
Uses `explorer.exe "<dir>"` or `explorer.exe /select,"<file>"`; the menu item is hidden for the
synthetic "Other" slice. Failures are ignored (best effort).

**D-018 — Project goal: personal tool; dev log in English** (2026-11-14, accepted, decided by owner)
The roadmap prioritizes correctness, daily-use comfort and analysis features over packaging and
distribution. This single `DEVELOPMENT.md` holds history, roadmap and decisions; it is written in
English to match the code and commits.

**D-019 — Folder content types are a color mode on existing views, classified by bytes** (2026-11-14, proposed)
No separate view. A shared "By size rank | By content" toggle; stacked composition bars in the bar
view; a dominant category at ≥ 60% of bytes, otherwise Mixed; project markers override. Why: content
type is an attribute of a folder, not a different structure; one navigation context; carries over to
treemap/sunburst. Details: §6.1.

**D-020 — Photo content analysis runs locally with a CLIP-family model on ONNX Runtime** (2026-11-14, proposed)
Zero-shot classification against text prompts. Input comes from Shell thumbnails, so cloud-only files
are not downloaded. Embeddings are cached in local SQLite. No cloud APIs, no face recognition. Why:
arbitrary filters plus free-text search and near-duplicate detection from one model, private, free,
and fast on the dev machine's GPU. Windows AI APIs were rejected because they need an NPU. Validate
with spike B0 before committing to the full plan. Details: §6.2.
