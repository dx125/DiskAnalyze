# DiskAnalyze — Development Log

Living document: project history, current state, known issues, roadmap, design notes and decision log.
Update it together with the code: add a History entry per meaningful change, tick roadmap items,
and record non-obvious choices in the Decision Log.

- **What:** Windows disk-usage analyzer. It scans drives or folders, shows where space goes (pie and
  bar views with drill-down) and finds files duplicated between two folders.
- **Goal:** personal tool. Usability, speed and useful features come before release engineering
  (installer, signing, auto-update). See D-018.
- **Stack:** C# / WPF / MVVM (hand-rolled), `net10.0` + `net10.0-windows`; xUnit tests.
- **Machine-specific notes:** `machine.local.json` at the repo root, git-ignored. Hardware, drives,
  local folder stats and benchmarks go there, never into this public file (D-027).
- **Build / run / test:**
  ```
  dotnet build DiskAnalyze.sln
  dotnet run --project DiskAnalyze/DiskAnalyze.csproj
  dotnet test DiskAnalyze.sln
  ```

---

## 1. Architecture snapshot

```
DiskAnalyze.Application (net10.0, UI-agnostic)
├─ AnalyzeService/   AnalyzeDiskService → DirectoryTreeScanner (parallel, Channel-based,
│                    one FileSystemEnumerable pass per directory), ScanProgress,
│                    CloudFiles (online-only placeholder detection)
├─ DiskEntities/     BaseFileSystemEntity, DirectoryEntityInfo (Children, iterative CalculateSize),
│                    FileEntityInfo (IsCloudOnly)
├─ Duplicates/       DuplicateFinderService (size+ext prefilter → SHA-256, nesting-aware),
│                    DuplicateFileGroup (ReclaimableBytes), DuplicateSearchProgress
└─ Settings/         AppSettings (immutable record, Normalize), SettingsStore (JSON in
                     %LOCALAPPDATA%\DiskAnalyze\settings.json; lenient read, atomic write)

DiskAnalyze (net10.0-windows, WPF)
├─ Core/             ViewModelBase, RelayCommand, FileSizeFormatter
├─ Themes/           Colors.Light.xaml, Colors.Dark.xaml (same token keys), Controls.xaml
│                    (square templates for every control in use, icons)
├─ Services/         FileSystemService (scan targets; domain tree → VM tree; drive usage and
│                    "not scanned"), ScanTarget, ShellActions (Show in Explorer),
│                    ThemeManager (swaps the colour dictionary, follows Windows, DWM title bar),
│                    AppSettingsService (current settings, apply + save on change)
├─ ViewModels/       MainViewModel (view switching incl. Settings, scan / rescan / cancel, progress,
│                    startup target), DiagramViewModelBase (navigation, rank colors, side panel,
│                    Show in Explorer), Circular (configurable slice count)/Chart/Duplicates/Settings VMs
├─ Views/            MainWindow, CircularDiagramView (pie geometry + hit testing in code-behind;
│                    labels in a non-hit-testable layer above the slices), ChartDiagramView
│                    (virtualized), DuplicatesView, SegmentContentsView (side panel), SettingsView
└─ Converters/       shared in App.xaml: entity brush (theme tokens, rank, hover, label colour), enum
                     equals (radio groups), size, visibility, percent→star width, percent threshold,
                     bool→GridLength, scale

DiskAnalyze.Application.Tests (net10.0, xUnit)
                     scanner on temp dirs (incl. junctions), sizes, cloud attributes, duplicates,
                     settings store
```

Startup: `App.OnStartup` loads the settings, applies the theme (so the window never flashes in the
wrong colours) and creates `MainWindow`.

Data flow: `MainWindow.Loaded` → `MainViewModel.StartInitialScan()` (skipped when "Scan on startup" is off) →
`FileSystemService.LoadFileSystemDataAsync(target paths, progress, ct)` → `AnalyzePathsAsync` scans
in parallel and reports progress every 200 ms → domain tree → VM tree built off the UI thread → the
same root is shared by the pie and bar views. A single drive or folder target becomes the root itself;
"All fixed drives" adds a synthetic "This PC" parent. Cancel keeps the previous results.
The Duplicates view runs its own scans of the two selected folders.

Composition root: `App.OnStartup` (settings, theme) and the `MainWindow` constructor (services and
view models); no DI container.

---

## 2. History

| Date | Commit | Summary |
|---|---|---|
| 2026-06-11 | `fac6e25` | Repository created (MIT license only). |
| 2026-06-30 | `6661c76` | Whole application landed in one commit (≈2.5k lines): parallel scanner, pie and bar diagrams with drill-down, "Other" bucket, hover tooltips, segment-contents side panel, drive usage %, two-folder duplicate finder. The commit message describes only the final UI fixes: diagram hidden while loading, side-panel column collapses to zero width when empty, "Show in Explorer" context menu. Earlier iterations are not in git. |
| 2026-11-14 | `8b91848` | Project review; this document created. Planned two features: folder content types (§6.1) and a photo browser with content filters (§6.2, feasibility analysis). |
| 2026-11-14 | `01928cf` | Review fixes (developed on branch `review-fixes`, fast-forwarded into `main`). Retargeted to .NET 10. Fixed I-1–I-4, I-6, I-8, I-10, I-11, I-13–I-17; partly fixed I-9 and I-12 (§4). New: scan target picker, Rescan, Cancel, live progress, OneDrive support, 27 xUnit tests, README. Smoke-tested in the running app (benchmarks in `machine.local.json`). |
| 2026-11-14 | `598d65f` | Machine-specific details moved to the git-ignored `machine.local.json`. Designed the capability gate for photo analysis (§6.3, D-027); Feature B steps renumbered (the gate is B2). Added a scan-error summary to the roadmap. |
| 2026-11-15 | — | Visual style (§6.4, D-028–D-031). Light and dark themes, palette "Graphite · Cobalt", picked by the owner from three options. Hot switching; System mode follows Windows. Square templates for every control; the standard title bar is recoloured through DWM, and window corners are square. New Settings view (gear in the toolbar): theme, scan on startup and what to scan, pie slice count (3–8), and the photo-analysis switches (inactive until Feature B). Settings are stored in `%LOCALAPPDATA%\DiskAnalyze\settings.json`. Toolbar tabs renamed Pie / Bars / Duplicates. Bars now draw the label above a thin bar. Fixed I-18. 12 new tests (39 total). |

---

## 3. Current state — what works

**Scanning**
- [x] Scan target picker: all fixed drives, one drive, or any folder ("Folder…"); **Scan** rescans; **Cancel** keeps the previous results.
- [x] Live progress: files, folders, bytes, elapsed time, current path.
- [x] Parallel scan (one worker per core), one enumeration pass per directory, hidden and system files included.
- [x] Skips only real links (junctions, symlinks); other reparse points such as OneDrive cloud folders are scanned.
- [x] Cloud-only (online-only) files count as 0 bytes and are marked "cloud only" in the side panel.
- [x] Inaccessible folders are skipped. Drive rows show "% used" and, when the gap is above 1% of capacity, "N GB not scanned".

**Visualization**
- [x] Pie view: top N plus "Other" (N = 3–8, default 5, set in Settings), rank-based colors (never repeated among the slices). Hover shifts the slice colour, magnifies it and shows a tooltip (size, % used, modified date). Labels sit above every slice and are hidden on slices under 4%. Click a folder to drill down; click "Other" to list its members.
- [x] Bar view: all non-empty children, virtualized; a label row above a thin bar that scales with the window width; the whole row is clickable; folders get a chevron.
- [x] Context menu: "Show contents" and "Show in Explorer". The side panel has a close button and tooltips with path and modified date.
- [x] Toolbar tabs (Pie / Bars / Duplicates) mark the active view with an accent bar.

**Appearance & settings**
- [x] Light and dark themes, switched live; "System" follows the Windows app mode while the app runs (§6.4).
- [x] Square, flat controls: buttons, inputs, combo boxes, check boxes, scroll bars, menus, tooltips, data grid. The standard title bar takes the theme colours; window corners are square on Windows 11.
- [x] Settings view behind the gear button (Back or Esc returns): theme, scan on startup and its target, pie slices, photo-analysis switches (shown, inactive). Changes apply at once and are saved immediately.
- [x] When scan on startup is off, the diagrams show a "Nothing scanned yet" hint instead of an empty pie.

**Duplicates**
- [x] Compare two folders. Nested selections compare "rest of outer" vs. "inner". Empty and cloud-only files are ignored.
- [x] Per-phase progress and Cancel. Summary: groups, files and reclaimable bytes. Rows of the same group share a background; the context menu reveals file A or B in Explorer.

**Quality**
- [x] 39 xUnit tests for the Application layer; README.

---

## 4. Known issues & tech debt

Found in the 2026-11-14 review. Fixed items are summarized; the original failure scenarios are in
this file's git history.

### Fixed (2026-11-14, branch `review-fixes`)
- **I-1. Nested folder selection lost real duplicates.** Example: A = `Photos`, B = `Photos\2020`, identical `Photos\x.jpg` and `Photos\2020\x.jpg`. Filtering is now nesting-aware (D-021). Regression tests cover both directions and "a file is never its own duplicate".
- **I-2. Hover brightening never rendered.** `EntityBrushConverter` is now a `MultiBinding` over (`ColorIndex`, `IsOtherCategory`, `IsHovered`).
- **I-3. Pie slices often shared a color.** Colors now follow size rank (D-023).
- **I-4. Bar view was not virtualized.** It now uses a `VirtualizingStackPanel` with recycling and pixel scrolling.
- **I-6. Allocation-heavy enumeration.** Each directory is read in one `FileSystemEnumerable<T>` pass with no `FileInfo` objects; progress counters are published once per directory.
- **I-8. OneDrive was skipped entirely.** Only real links (`LinkTarget != null`) are skipped now. Cloud-only files count as 0 bytes and are never hashed (D-022). Verified against a real OneDrive folder: its subfolders are now scanned, and online-only files are marked cloud-only instead of being counted at their logical size.
- **I-10. No way to choose, cancel or rescan.** Added a scan target picker, Scan, Folder… and Cancel; the initial scan starts on window `Loaded` (D-024).
- **I-11. No scan progress.** Live counters update every 200 ms.
- **I-13. Minor UI.** Labels are hidden under 4%; bars are star-sized; the view switcher is a set of toggles; clicking "Other" lists its members; the whole bar row is the click target. Directory rows are no longer magnified by 2% (that overflowed full-width rows); a chevron column marks folders instead.
- **I-14. Dead code.** Removed `Styles.xaml`, unused App styles, `AnalyzeDriveAsync`, and the old color and width converters. `LastModified` is now shown in tooltips. Shared converters moved to `App.xaml`.
- **I-15. No tests and no README.** Added `DiskAnalyze.Application.Tests` (27 tests) and `README.md`.
- **I-16. .NET 8 end of support.** Retargeted to `net10.0` / `net10.0-windows` (D-026).
- **I-17. Recursive tree walks.** `CalculateSize`, VM conversion and duplicate collection are now iterative; a test covers a 200,000-level tree.
- **Found during smoke testing:**
  - Commands stayed disabled after a scan finished until the next mouse or keyboard input, because WPF's `CommandManager` doesn't requery by itself. Fixed with `InvalidateRequerySuggested()` on loading-state changes.
  - "Not scanned" reported NTFS metadata on an empty 4 TB drive. The threshold is now relative to capacity (D-025).

### Fixed (2026-11-15, visual style)
- **I-18. Pie labels could be covered by the next slice.** Each slice drew its label and hover box in its own layer, so every later slice painted over them; long names were cut off ("FlutterSdk" showed as "FlutterS"). Labels and the hover box now live in one layer above all slices. That layer isn't hit-testable, so hover and clicks still reach the slices. The label box also widened from 60 to 76 px, so short names no longer wrap mid-word.

### Partly fixed
- **I-9. Scanned vs. used gap.** Drive rows and tooltips show "N GB not scanned". Still open: dedicated "Free" and "Not scanned" segments in the diagrams; a "Restart as administrator" action. Besides access rights, the gap can come from I/O errors: a flaky external drive timing out on reads (`ERROR_SEM_TIMEOUT`) looks the same today, which the scan-error summary (roadmap Next) will tell apart.
- **I-12. Duplicates view limits.** Done: cancel, progress, summary (groups, files, reclaimable bytes), reveal A or B in Explorer. Still open: reuse the loaded tree instead of rescanning, duplicates *within* one folder, delete actions (Duplicates v2).

### Open
- **I-5. Whole tree materialized twice.** At peak, the domain tree and a full VM tree exist together. Every file VM carries pie geometry fields although at most 6 entities are drawn. Fix: create VMs lazily per displayed directory and move geometry out of the entity VM.
- **I-7. Logical sizes, not on-disk sizes.** Hard links (e.g. `C:\Windows\WinSxS`) are counted once per link; compression, sparseness and cluster slack are ignored.

---

## 5. Roadmap

Priorities follow the "personal tool" goal: fix what misleads, then make it pleasant for daily use,
then add analysis features. Release engineering (installer, signing, auto-update, CI) is out of scope
unless the goal changes.

### Now — correctness & foundation
- [x] Retarget to `net10.0` / `net10.0-windows` (I-16).
- [x] Fix nested duplicates (I-1) — **before** adding any delete action.
- [x] Fix hover brush (I-2); index-based pie palette (I-3).
- [x] Virtualize the bar view (I-4).
- [x] Remove dead code (I-14).
- [x] Add a `DiskAnalyze.Application.Tests` (xUnit) project for size calculation, junction skipping, cloud attributes and duplicate grouping, including the nested case.

### Next — daily-use comfort
- [x] Scan target picker (drive list plus "Folder…"), **Scan** (rescan) and **Cancel** with a `CancellationToken` plumbed end to end (I-10).
- [x] Live progress: folders and files scanned, bytes, current path, elapsed time (I-11).
- [x] Light and dark themes with hot switching; Settings view; settings file (§6.4, D-028–D-031).
- [ ] Clickable breadcrumb path; Back/Forward, including mouse X1/X2 buttons.
- [ ] Context menu: Open, Copy path, **Delete to Recycle Bin** (with confirmation). Update sizes up the parent chain in place without a rescan.
- [ ] Drive level: show "Free" and "Not scanned" segments in the diagrams (the "N GB not scanned" text is done, I-9); add a "Restart as administrator" action.
- [x] Highlight the active view in the toolbar; hide labels for slices under 4% (I-13).
- [ ] **Scan-error summary.** Count folders that couldn't be read, grouped by cause: access denied vs. I/O errors such as `ERROR_SEM_TIMEOUT` on a failing USB drive. Show it in the status bar and the drive tooltip, with a list of affected paths. Today errors only reach Debug output (D-006).

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
- [ ] **B2. Capability gate** (§6.3). At startup, in the background:
  - Detect GPUs (via DXGI), ONNX Runtime execution providers and whether the model is present; probe-run one inference.
  - Decide whether CLIP on ONNX Runtime is allowed and available, and on which execution provider; decide whether photo content analysis is on.
  - Settings `ClipOnnx` (Auto/Allow/Deny), `ExecutionProvider` and `PhotoContentAnalysis` (Auto/Enabled/Disabled); developer override from `machine.local.json`.
  - The photo window shows the active mode and, when off, the reason.
  - The decision logic is a pure function with table-driven unit tests.
- [ ] **B3. Content categories.** Background indexing with progress and cancel. SQLite index cache (key: path + size + mtime) stores a CLIP embedding per photo. Fixed categories come from precomputed prompt embeddings, so no tokenizer is needed; category chips show counts.
- [ ] **B4. Search & similar.** Free-text search ("dog on the beach") using the text encoder and CLIP BPE tokenizer. "Similar photos" groups (bursts, near-duplicates) with reclaimable space, which the SHA-256 duplicate finder can't detect.
- [ ] **B5. Optional.** Face detector (YuNet/SCRFD, a few MB) for reliable people/portrait/group filters; user-defined categories via a prompt editor; threshold tuning per category.

### Later — analysis features
- [ ] **Largest files** flat list across the whole tree, with size/extension/age filters.
- [ ] **Treemap** (squarified) and/or **sunburst** (2–3 levels at once): denser than a one-level pie. Reuses the A2 color modes.
- [ ] **Old files**: filter by `LastModified` (data already collected and shown in tooltips).
- [ ] **Cleanup suggestions**: `%TEMP%`, browser and package caches (`%LOCALAPPDATA%\NuGet`, npm), dev build output (`bin/`, `obj/`, `.vs/`, `node_modules/`), `Windows.old`, Recycle Bin, old items in Downloads.
- [ ] **Snapshots & diff**: save a compact scan summary, compare with a later scan ("what grew since last week").
- [ ] **Duplicates v2**: within a single folder or drive; reuse the loaded tree; partial-hash prefilter (first and last 64 KB) before the full hash; "keep one, recycle the rest" (rest of I-12).
- [ ] Export the current view or list to CSV.

### Performance track (pick up when scans feel slow)
- [x] `FileSystemEnumerable<T>` with `EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }` (I-6).
- [ ] Lazy VM creation; geometry out of the entity VM (I-5).
- [ ] On-disk (allocated) size and hard-link de-duplication by file ID, optionally behind a setting (I-7).
- [ ] Cache the last scan for an instant start and refresh in the background.
- [ ] Optional: read the NTFS MFT directly (admin only), which gives order-of-magnitude faster full-drive scans.

### Maybe
- Name search/filter; `DiskAnalyze.exe <path>` command-line argument plus an Explorer
  "Analyze with DiskAnalyze" entry; `FileSystemWatcher` live updates for the open folder.
- Remember window size and position, and the last view, in the settings file.

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
neutral gray. Since the themes (§6.4) these become `Color.Category.*` tokens in both colour
dictionaries, with light and dark steps validated like the series palette.

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
| Windows AI APIs (Windows App SDK image description / object APIs) | Good | limited | yes | Needs a Copilot+ PC with an NPU; not available on typical desktops |
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
2. **Input pixels: Windows Shell thumbnails** (`IShellItemImageFactory`, 256 px; CLIP needs 224 px). They are fast thanks to the OS thumbnail cache and cover HEIC/RAW wherever codecs are installed. Most importantly, they work for **cloud-only OneDrive files without downloading the original**; B0 must confirm this. When Pictures is redirected to OneDrive (a common default), most images may be cloud-only, so decoding originals would mean downloading the whole library. Fallback for local files: WIC decode with `DecodePixelWidth` (JPEG DCT scaling).
3. **Inference.** ONNX Runtime with a GPU execution provider (DirectML, or CUDA on NVIDIA GPUs; Windows ML packaging is another route — decide in B0), CPU fallback, batches of 32–64.
4. **Text side.** B3 ships precomputed prompt embeddings (JSON generated once), so the app needs no tokenizer. B4 adds the text encoder plus the CLIP BPE tokenizer (`Microsoft.ML.Tokenizers` with vocab and merges files) for free-text search.
5. **Index cache.** SQLite at `%LOCALAPPDATA%\DiskAnalyze\photos.db`, keyed by path + size + mtime. Columns: dimensions, EXIF date, GPS, camera, embedding, face count, category scores. Re-indexing is incremental.
6. **People (B5, optional).** A small face detector gives a face count and makes people/portrait/group filters more reliable than CLIP alone. Face *recognition* (who) is out of scope for privacy and complexity reasons.

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

### 6.3 Machine capabilities & feature gating (decided: D-027)

**Two kinds of machine information, kept apart.**
1. **Developer reference facts**: hardware, drives, local folder stats, benchmarks. They live in `machine.local.json` at the repo root, git-ignored via `*.local.json`. Never in this file, because the repo is public.
2. **What the app needs at runtime**: detected by the app itself. The repo file isn't next to the exe or on another machine; GPUs and drivers change; and "will the GPU execution provider actually load and run" can only be answered by trying.

**Two switches; the second depends on the first.**
- **`ClipOnnx`** (Auto / Allow / Deny): may the app load ONNX Runtime and the CLIP model at all. **`ExecutionProvider`** (Auto / DirectML / CUDA / CPU) picks the backend.
- **`PhotoContentAnalysis`** (Auto / Enabled / Disabled): the content-filter feature itself. It needs `ClipOnnx` to be allowed and available. When it is off, the photo window keeps working with metadata filters (B1), and the content chips show the reason.

**Detection at startup.** Runs in the background after the main window is shown and never delays scanning.

| Check | How | Why this way |
|---|---|---|
| GPU adapters | DXGI `EnumAdapters1` → description, vendor, `DedicatedVideoMemory` | Not WMI: `Win32_VideoController.AdapterRAM` is 32-bit and caps at 4 GB, so a 12 GB card reports 4 GB. |
| ONNX Runtime | Load the native library; list available execution providers | Missing or failing to load → `ClipOnnx` unavailable. |
| Model | File in `%LOCALAPPDATA%\DiskAnalyze\models`, size and hash check | Missing → offer the download. |
| Probe | Create a session on the chosen provider, run one dummy batch, time it | Catches driver and provider failures that the availability list misses; gives a real throughput estimate for the UI. |
| RAM, cores | `GC.GetGCMemoryInfo()`, `Environment.ProcessorCount` | Batch size and parallelism. |

The result is cached in `%LOCALAPPDATA%\DiskAnalyze\machine-profile.json`. It is invalidated when the
adapter list, driver version or app version changes, so the probe doesn't run on every start.

**Decision in Auto mode**

| Detected | ClipOnnx | PhotoContentAnalysis |
|---|---|---|
| Denied in settings | off | off: "disabled in settings" |
| ONNX Runtime or model missing | unavailable | off; offers the model download |
| GPU provider probe OK, ≥ 4 GB dedicated VRAM | on, GPU | on |
| CPU only, probe OK | on, CPU | off by default; the user can enable it, with an estimated indexing time for the library |
| Probe failed | unavailable | off, with the error as the reason |

Allow/Enabled in settings forces the feature on wherever possible (e.g. CPU fallback).

**Override precedence** (later wins):
1. Runtime detection.
2. User settings in `%LOCALAPPDATA%\DiskAnalyze\settings.json`, changed in the UI.
3. Developer override: the `features` section of `machine.local.json`. The build copies the file next to the exe only when it exists. It lets you simulate "no GPU" or "CLIP denied" on the dev machine without touching hardware.

**Code placement.**
- `MachineProfile` (detected facts) and `FeatureGate`, a pure function from profile + settings + overrides to decisions, live in the Application layer and are unit-tested table-first.
- DXGI and ONNX Runtime probing is Windows/native code. It goes in the WPF project, or in a future `DiskAnalyze.Photos` project.
- The user-settings layer already exists: `AppSettings.PhotoAnalysis` (`photoContentAnalysis`, `clipOnnx`, `executionProvider` in `settings.json`, the same names as the `features` section of `machine.local.json`). The Settings view shows it disabled until B2.

### 6.4 Visual style & theming (decided: D-028–D-031)

**Look.** Strict and flat: no rounded corners anywhere (controls, bars, tooltips, menus, window),
1 px lines, no shadows. Depth comes only from surface colour and a line. The accent marks state as a
2 px bar (active tab) or a 1 px border (focus, Settings toggle, open combo box). The primary button
(Scan, Find Duplicates) is the only solid-accent control on a screen. The font is the system Segoe UI
at 12 px.

**Palette "Graphite · Cobalt"** (chosen from three options; the comparison page with the rejected
Slate · Teal and Carbon · Amber lives outside the repo). Cool neutral greys and one cobalt accent.

| Token | Light | Dark | Use |
|---|---|---|---|
| Window | `#F3F4F6` | `#111316` | window and diagram background |
| Chrome | `#FFFFFF` | `#181B1F` | title bar (via DWM), toolbar, status bar |
| Surface | `#FFFFFF` | `#1C1F24` | side panel, tooltips, inputs, menus |
| Band / Hover / Selected | `#F0F2F5` / `#E7EAEF` / `#DFE7FB` | `#212429` / `#2A2F36` / `#1D2B4D` | row states |
| Line / LineStrong | `#D7DBE1` / `#A9B0BA` | `#2C3138` / `#4A515B` | separators / control borders |
| Text / Secondary / Muted | `#14171C` / `#4A515C` / `#6B7380` | `#E7E9EC` / `#A9AFB8` / `#7D8590` | text levels (all ≥ 4.3:1 on Window) |
| Accent / OnAccent | `#2453D4` / `#FFFFFF` | `#5B8CFF` / `#0B1020` | accent ≥ 5.8:1 on Window in both themes |

**Diagram palette.** Eight series colours in a fixed order, the same hues stepped separately for each
theme (not an automatic inversion), plus a grey "Other". Validated with a colour-vision check: the
worst adjacent pair under protanopia/deuteranopia simulation is ΔE 9.1 (light) / 8.4 (dark), target
≥ 8. Under normal vision it is ΔE 19.6 / 19.3, floor 15. On dark, every fill is ≥ 3:1 against the
background. On light, four fills (orange, green, yellow, pink) are below 3:1, which is acceptable
because every slice carries a label and the side panel lists sizes. Text on a fill is white, except
dark on yellow in the light theme. The bar view puts the label above an 8 px bar, so text never sits
on a fill. Warning `#FAB219` and Critical `#D03B3B` are reserved for status and always come with text.

**How switching works.** `App.xaml` merges `Themes/Colors.Light.xaml` first and `Themes/Controls.xaml`
second. `ThemeManager` replaces merged dictionary 0 with the other colour file. Everything styled
through `{DynamicResource Brush.*}` repaints in place. Brushes computed in code (`EntityBrushConverter`
for slices, bars and slice labels) add `ThemeManager.ActualTheme` as an extra binding, so they
re-evaluate on a switch. In System mode, `SystemEvents.UserPreferenceChanged` (General) re-reads
`HKCU\…\Themes\Personalize\AppsUseLightTheme`. The title bar follows through `DwmSetWindowAttribute`:
immersive dark mode, caption colour = Chrome, text = Text, border = LineStrong, corners = do not round.

**Rules for new UI.**
- Never use a literal colour in a view. Add a token to *both* colour files, or reuse one.
- Use DynamicResource for brushes, never StaticResource, or the element won't follow a theme switch.
- Popups (menus, tooltips, combo drop-downs) don't inherit the window's colours, so their styles set Foreground and Background themselves.
- A new kind of control needs a square template in `Controls.xaml`: the stock Aero2 templates hard-code light-theme hover colours.

**Settings file** (`%LOCALAPPDATA%\DiskAnalyze\settings.json`, written on every change):
```json
{
  "theme": "system",              // system | light | dark
  "scanOnStartup": true,
  "startupScanPath": null,        // null = all fixed drives; else a drive or folder
  "pieSlices": 5,                 // 3–8
  "photoAnalysis": { "photoContentAnalysis": "auto", "clipOnnx": "auto", "executionProvider": "auto" }
}
```
Reading is lenient: comments, trailing commas and any key casing are accepted, and out-of-range
values are clamped. A malformed file falls back to the defaults and never blocks startup.

---

## 7. Decision log

Format: **D-NNN — title** (date, status). Entries D-001…D-017 were reconstructed from the code during
the 2026-11-14 review; their "why" is inferred and may be corrected.

**D-001 — Split into a UI-agnostic core and a WPF shell** (2026-06, accepted)
Scanning, entities and duplicate detection live in `DiskAnalyze.Application` (no WPF).
Why: testable without UI (now covered by `DiskAnalyze.Application.Tests`); another front end stays
possible. Trade-off: none significant.

**D-002 — Hand-rolled MVVM, no framework, no DI container** (2026-06, accepted)
`ViewModelBase`/`RelayCommand` are written in-house; services are wired in the `MainWindow` constructor.
Why: tiny app, zero dependencies. Revisit if the number of services grows (CommunityToolkit.Mvvm and
`Microsoft.Extensions.Hosting` are the natural next step).

**D-003 — Scan everything up front into an in-memory tree** (2026-06, accepted, revisit)
The whole target is scanned before display; navigation is then instant. Trade-off: high memory and no
partial results (I-5). Startup cost is mitigated by target choice, cancel and progress (D-024).

**D-004 — Parallel scanner on a `Channel` work queue** (2026-06, accepted)
One worker per core reads directories from an unbounded channel and enqueues subdirectories; an
`Interlocked` outstanding counter completes the channel when it reaches zero. Each directory is owned
by exactly one worker, so `Children` needs no locks.

**D-005 — Do not follow reparse points** (2026-06, superseded by D-022)
Why: legacy junctions such as `C:\Users\All Users` create cycles and double counting. It also skipped
OneDrive cloud folders (I-8).

**D-006 — Inaccessible folders are skipped silently** (2026-06, accepted)
Errors go only to `Debug.WriteLine`. The resulting gap is now surfaced per drive as "not scanned"
(D-025). Revisit: the planned scan-error summary distinguishes access denied from I/O errors.

**D-007 — Directory sizes in a post-pass** (2026-06, accepted)
`CalculateSize()` runs once after the scan instead of propagating sizes concurrently. Why: no
synchronization on shared parents. Iterative since 2026-11-14 (I-17).

**D-008 — Size means logical file length** (2026-06, accepted, revisit; amended by D-022)
Simple and fast; ignores compression, sparseness, cluster slack and hard links (I-7). Exception:
cloud-only files count as 0.

**D-009 — Pie shows top 5 plus "Other"; bars show everything** (2026-06, accepted; amended by D-031: the slice count is a setting, 3–8)
Why: the pie stays readable, while the bar view gives the full list. "Other" is a synthetic VM
holding its members, so "Show contents" works on it, and clicking it opens that list.

**D-010 — Pie geometry in code-behind; hit test walks up to the owning DataContext** (2026-06, accepted)
Geometry is computed in `CircularDiagramView` and stored on the entity VMs. Hit testing walks up from
any visual to the element whose `DataContext` is an entity, because the tooltip and label overlay the
slice; matching only `Path` caused a hover flicker loop.

**D-011 — Stable name hash for colors** (2026-06, superseded by D-023)
Used a custom hash because `string.GetHashCode` is randomized per process; collided often in the pie (I-3).

**D-012 — Drive "% used" comes from `DriveInfo`, not from the scanned total** (2026-06, accepted; amended by D-025)
Why: the scanned total omits inaccessible files, so it under-reports.

**D-013 — Duplicate = same size + same extension + same SHA-256** (2026-06, accepted)
The extension is part of the key, so identical content under different extensions isn't reported.
Why: a cheap, very selective prefilter; hashing only runs on candidates present on both sides.
Empty and cloud-only files are excluded (D-022).

**D-014 — The duplicate finder scans folders independently** (2026-06, accepted, revisit)
It works without the main scan and on any folder. Trade-off: repeated disk I/O (I-12).

**D-015 — Side-panel column collapses to zero width when empty** (2026-06-30, accepted)
A `BooleanToGridLengthConverter` on the column width lets the diagram reclaim the space.

**D-016 — Diagram hidden while the loading overlay is visible** (2026-06-30, accepted)
Why: the empty pie showed through behind the progress bar at startup.

**D-017 — "Show in Explorer": folders open, files are revealed selected** (2026-06-30, accepted)
Uses `explorer.exe "<dir>"` or `explorer.exe /select,"<file>"`; the menu item is hidden for the
synthetic "Other" slice. Best effort. Shared via `ShellActions` since 2026-11-14.

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
and fast on a discrete GPU. Windows AI APIs were rejected because they need an NPU. Validate
with spike B0 before committing to the full plan. Details: §6.2.

**D-021 — Nested folders in duplicate search: "rest of outer" vs "inner"** (2026-11-14, accepted)
When one folder contains the other, files under the inner folder are scanned on both sides; they are
attributed to the inner side only. Pairs are reported between the inner folder and the rest of the
outer folder, and duplicates purely within the inner folder are not reported (that is the
within-folder search of Duplicates v2). Why: this matches what comparing `Photos` with `Photos\2020`
means, and a physical file can never be reported as its own duplicate.

**D-022 — Follow cloud reparse points; cloud-only files are 0 bytes and never opened** (2026-11-14, accepted)
Directories are skipped only when `DirectoryInfo.LinkTarget != null` (symlinks, junctions, mount
points). Reading it opens the reparse point itself, so it never triggers a download. Files with
`RecallOnDataAccess`, `RecallOnOpen` or `Offline` count as 0 bytes because they use no local space,
and they are excluded from hashing because reading them would download them. Empty files are also
excluded from duplicates, since they free no space. Supersedes D-005; amends D-008.

**D-023 — Segment colors by size rank** (2026-11-14, accepted; palette replaced by D-029)
`ColorIndex` is the rank among displayed siblings (largest first) into an 8-color palette; "Other"
is gray. (The original palette was light enough for black labels; D-029 swapped in theme-aware,
validated colours with white labels.) Every view sorts the same way, so an entity keeps its color
across pie and bars, and the pie's top 5 never collide. Supersedes D-011. Feature A adds a
"By content" mode (D-019).

**D-024 — Scan targets and scan lifecycle** (2026-11-14, accepted)
Targets are all fixed drives (under a synthetic "This PC" root), one drive, or a folder; a single
target is the root itself, so "Up" stops there. The initial scan starts on the window's `Loaded`
event, not in the VM constructor. Cancel keeps the previously shown results. Progress is polled from
atomic counters every 200 ms; workers publish counters once per directory to avoid cross-core
contention.

**D-025 — "Not scanned" is shown only above 1% of drive capacity** (2026-11-14, accepted)
Used minus scanned includes NTFS metadata, which grows with volume size: an empty 4 TB drive reports
~25 GB used. Only a gap above 1% of capacity points at unreadable folders. Amends D-012.

**D-026 — Target .NET 10 LTS** (2026-11-14, accepted)
.NET 8 left support on 2026-11-10. There is no `global.json`, so any installed SDK ≥ 10 builds the
solution; newer SDKs, including .NET 11 previews, fetch the .NET 10 reference packs on their own.
Installing the .NET 10 GA SDK is still recommended. Tests use xUnit 2.9.

**D-027 — Machine facts in a git-ignored file; feature gates from runtime detection** (2026-11-14, accepted for the file, proposed for the gate)
Machine-specific details (hardware, drives, local folder stats, benchmarks) live in
`machine.local.json` at the repo root, ignored via `*.local.json`/`*.local.md`. This document is
public and stays machine-neutral. The app never relies on that file for facts. At startup it detects
GPUs (DXGI, not WMI), ONNX Runtime execution providers and the model, probe-runs one inference, and
derives `ClipOnnx` and `PhotoContentAnalysis`. User settings, then the file's `features` section
(developer override), take precedence over detection. Why: the file is absent next to the exe and on
other machines, hardware and drivers change, and only a real probe proves the GPU path works.
Details: §6.3.

**D-028 — Own themes from resource dictionaries, not WPF's Fluent theme** (2026-11-15, accepted)
Two colour dictionaries with identical token keys plus one shared `Controls.xaml` with square
templates; `ThemeManager` swaps the colour dictionary and everything bound with DynamicResource
repaints live. Why: the owner asked for strict lines and no rounded corners. The .NET 9+ Fluent
theme (`Application.ThemeMode`) gives light/dark for free but rounds every control and brings the
Windows 11 look, and overriding its templates costs as much as writing our own, which also gives
full control of the palette. Trade-off: every control kind in use needs a template, and a new kind
needs one too (§6.4 rules).

**D-029 — Palette "Graphite · Cobalt"; one validated diagram palette for both themes** (2026-11-15, accepted, chosen by owner)
Chosen from three options with light/dark mock-ups: Graphite · Cobalt, Slate · Teal and Carbon ·
Amber. Amber was the most characterful, but its accent collided with the warning colour; teal
clashed with nothing. The owner picked the calmest option, for long sessions. The cobalt accent sits
close to diagram series 1 (blue); the two never appear in the same role. Diagram colours are
eight fixed-order hues with separate light and dark steps, validated for colour-blind separation
(numbers in §6.4); labels go above bars rather than on them. Supersedes the palette of D-023.

**D-030 — Keep the standard title bar, recoloured through DWM; square window corners** (2026-11-15, accepted, decided by owner)
`DwmSetWindowAttribute` sets immersive dark mode and the caption, text and border colours from the
theme tokens, and turns off Windows 11 corner rounding. The caption matches the toolbar, so the two
read as one band. Why: native caption buttons, Snap Layouts and accessibility keep working with
little code. A custom title bar merged with the toolbar was rejected: it would mean reimplementing
the caption buttons and the Snap flyout. On Windows 10 the colour and corner attributes are ignored
and the frame stays standard.

**D-031 — Settings: a view in the main window, applied immediately, stored as JSON** (2026-11-15, accepted)
A gear toggle in the toolbar opens the Settings view in place of the current one; Back, Esc or any
tab returns. There is no Save button: each change is applied through `AppSettingsService.Changed`
and written at once (atomic temp-file replace) to `%LOCALAPPDATA%\DiskAnalyze\settings.json`. If
the write fails, the change still holds for the session and the view shows the error. The model
(`AppSettings`, `SettingsStore`) lives in the Application layer so it is unit-tested, and so the
photo-analysis gate (D-027) can read the same file later. Contents: theme (System by default),
scan on startup and its target (default: all fixed drives), pie slices (3–8; the maximum equals the
palette size, so slices never share a colour), and the photo-analysis switches, which stay
disabled until Feature B. Size units (1024 vs 1000) were considered and left out.
