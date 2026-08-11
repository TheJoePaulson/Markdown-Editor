# Markdown Editor — User Guide

**Version 2.0.0** — Portable WinUI 3 Markdown Editor for Windows 11 and Server 2025

---

## Table of Contents

- Overview
- Getting Started
- Features
- Editing Shortcuts
- Find and Replace
- Templates
- PDF Export
- Multi-Tab Editing
- Theming
- Portable Deployment
- Autosave and Backups
- Storage Locations
- Release History
- Markdown Syntax Reference

---

## Overview

Markdown Editor is a self-contained, portable, WinUI 3 desktop application for authoring and previewing Markdown documents. It is designed for IT professionals writing standard operating procedures, runbooks, meeting notes, README files, and Copilot agent instructions.

The application requires no installation. Extract the folder anywhere — USB drive, network share, or local disk — and launch the executable. Your settings, autosaves, backups, and logs are stored alongside the executable in a per-user profile folder, so your work travels with the application folder.

Key characteristics:

- Runs on Windows 11 and Windows Server 2025 with Desktop Experience
- No installation required, no registry modifications, no admin rights needed
- All data is stored in the application folder for portability
- Falls back gracefully to `%LOCALAPPDATA%` when running from a read-only location

---

## Getting Started

### First Launch

Double-click `MarkdownEditor.exe`. On first launch, the application creates a `Data` folder alongside the executable to hold your profile.

You will see:

- A toolbar at the top with buttons for New, Open, Save, Save As, Export PDF, formatting shortcuts, theme selection, and find/replace
- A tab strip below the toolbar with one open tab
- An editor pane on the left and a live preview pane on the right, split into two equal columns
- A status bar at the bottom showing the current message and portable-mode information

### Typing and Preview

Start typing in the editor pane. The preview updates automatically. Everything you type is autosaved every few seconds — you can close the application at any time without losing work.

### Saving

Save with **Ctrl+S** or the Save toolbar button. If the document has never been saved, a Save As dialog appears. Once saved, the tab title changes from the placeholder name (e.g., `Untitled 1.md`) to the file name.

---

## Features

### Editing

- Full Unicode support with UTF-8 encoding
- Native undo and redo (Ctrl+Z, Ctrl+Y)
- Consolas 15pt font for the editor
- Selection is highlighted even when focus is elsewhere (useful during Find/Replace)

### Live Preview

- Renders Markdown to HTML using the Markdig library
- Uses a WebView2 control (Chromium-based)
- Preview updates as you type
- Theme-aware — background and text colors change with theme selection
- Supports advanced Markdown extensions (tables, fenced code blocks, task lists, footnotes)

### Toolbar

- **New** — Create a new Untitled document in a new tab
- **New from Template** — Choose from a dropdown of installed templates
- **Open** — Open a Markdown file. The dropdown arrow shows Recent Files.
- **Save** — Save the current document
- **Save As** — Save the current document to a new location
- **Export PDF** — Render the current document as a PDF
- **Bold, Italic, Heading, Bullet, Link, Code** — Insert Markdown syntax
- **Find and Replace** — Search within the current document
- **Theme** — Switch between Light, Dark, and System themes

---

## Editing Shortcuts

Keyboard shortcuts speed up common formatting tasks and mimic conventions used in Notion, Obsidian, and VS Code.

| Shortcut | Action |
|---|---|
| Ctrl+B | Wrap selection in **bold** |
| Ctrl+I | Wrap selection in *italic* |
| Ctrl+1 | Toggle H1 heading on current line |
| Ctrl+2 | Toggle H2 heading on current line |
| Ctrl+3 | Toggle H3 heading on current line |
| Ctrl+4 | Toggle H4 heading on current line |
| Ctrl+0 | Remove heading from current line |
| Enter (on a `- ` line) | Continue the bulleted list |
| Enter (on a `1. ` line) | Continue and auto-increment the numbered list |
| Enter (on an empty list marker) | Exit the list |
| Tab (on a list line) | Indent the list item by 2 spaces |
| Shift+Tab (on a list line) | Outdent the list item |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |

### Heading Toggle Behavior

Heading shortcuts are toggles. Pressing Ctrl+2 on a line that is already H2 removes the heading. Pressing a different level changes the heading to that level.

Ctrl+0 always removes the heading regardless of current level.

### List Continuation

When you press Enter on a line that starts with a list marker, the next line automatically starts with the same marker. For numbered lists, the number increments.

Pressing Enter on an empty list marker exits the list — useful when you're done adding items.

---

## Find and Replace

Press **Ctrl+F** to open the Find bar. Press **Ctrl+H** to open Find and Replace.

- **Enter** in the Find box — Find next
- **Shift+Enter** in the Find box — Find previous
- **F3** — Find next
- **Shift+F3** — Find previous
- **Esc** — Close the Find bar
- **Aa** button — Match case
- **ab** button — Whole word only
- **Replace** button — Replace the current match
- **Replace All** button — Replace every match at once

The match counter shows "Match N of M" so you always know your position.

All replacements preserve undo history. Press Ctrl+Z after Replace All to revert every change with a single undo.

Find and Replace operates only within the currently active tab.

---

## Templates

Templates are Markdown files stored in the `Templates` folder alongside the executable. Click the **New from Template** toolbar button (folder icon) to see a dropdown of available templates.

Selecting a template opens it as a new tab with the template content pre-loaded. The tab is marked as unsaved, so your first Save prompts for a location.

### Included Templates

- **Standard SOP Document** — Structured operating procedure with sections for Purpose, Scope, Roles, Steps, Verification, and Revision History
- **GitHub ReadMe Document** — Project README with sections for Overview, Features, Installation, Usage, and Contribution
- **Copilot Agent Instruction** — Full-agent authoring template covering Persona, Scope, Response Style, Guardrails, and Governance
- **Copilot Agent Skill** — Per-skill template covering Triggers, Inputs, Outputs, Behavior, Error Handling, and Telemetry

### Adding Your Own Templates

Drop any `.md` file into the `Templates` folder. The dropdown updates automatically the next time you open it. The filename (without extension) becomes the template name.

### Refreshing the List

Click **Refresh templates** in the dropdown to re-scan the folder — useful if you just added a new template.

### Opening the Folder

Click **Open templates folder** to launch File Explorer at the `Templates` directory.

---

## PDF Export

Click the **Export PDF** toolbar button to render the current document as a PDF.

- The PDF preserves your current theme (Light mode produces a white PDF, Dark mode produces a dark PDF)
- The PDF opens automatically in your default PDF viewer after export
- Margins are 0.75 inches on all sides
- Backgrounds and code block styling are preserved
- The suggested filename matches the current document name

PDF export uses WebView2's native rendering engine — the same engine that produces the on-screen preview — so what you see is what you get.

---

## Multi-Tab Editing

Open multiple documents simultaneously. Each tab has its own independent editor, preview, and undo history.

### Tab Actions

- **Plus (+)** button on the tab strip — Open a new Untitled tab
- **X** button on a tab — Close that tab
- **Drag tabs** — Reorder tabs by dragging their headers
- **Hover** — See the full file path in a tooltip
- **Asterisk (*)** in the tab title — Indicates unsaved changes

### Automatic Switch-to-Existing

If you open a file that's already open in another tab, the application switches to that tab instead of creating a duplicate.

### Closing Behavior

- Closing the last tab automatically creates a new Untitled tab (never leaves you with zero tabs)
- No prompt appears when closing an unsaved tab — autosave handles crash recovery

### Session Restore

When you launch the application, every tab that was open on the previous session is restored automatically. Each tab reopens with its content and file path preserved.

---

## Theming

The application supports three themes:

- **System** — Follows Windows theme (default)
- **Light** — Force light mode
- **Dark** — Force dark mode

Click the **Theme** toolbar button and pick from the dropdown. The choice persists across launches.

The theme applies to:

- Editor chrome (title bar, toolbar, tab strip, status bar)
- Preview pane (background, text, headings, code blocks, tables, blockquotes)
- All open tabs simultaneously

---

## Portable Deployment

The application is fully portable. Move or copy the entire folder to any Windows machine and it just runs.

### USB Deployment

Extract the ZIP into a folder on a USB drive:



E:\MarkdownEditor\


Eject and plug into another machine. Launch `MarkdownEditor.exe`. All your settings, tabs, autosaves, and backups travel with the USB drive.

### Network Share Deployment

Copy the folder to a network share:



\fileserver\Tools\MarkdownEditor\


Users can launch it directly from the share. If the share is read-only, the application detects this and falls back to `%LOCALAPPDATA%\MarkdownEditor\Profiles\<username>\` for user data. This lets IT deploy the application read-only for security while still allowing users to save their own work.

### Multi-User Support

Every user gets a separate profile subfolder inside `Data\Profiles\<username>\`. This works even when multiple users share the same portable folder — each has independent settings, tabs, autosaves, and backups.

---

## Autosave and Backups

### Autosave

Every open tab is autosaved to disk a few seconds after the last edit. Autosave is per-tab — each tab has its own draft file. Drafts are stored as JSON snapshots in the autosave folder.

Autosave means:

- You can close the application at any time without losing work
- Every open tab restores automatically on next launch
- If the machine crashes or power is lost, drafts persist

### Backups

Every time you Save, a timestamped copy of the previous version is placed in the `Backups` folder. Filenames follow the pattern `<name>-<yyyyMMdd-HHmmss>.md.bak` so recent backups sort to the top.

The application retains the 25 most recent backups by default (configurable in `Settings.json`). Older backups are pruned automatically.

Backups protect you from accidental overwrites — even if you save over a document you didn't mean to, the previous version is one folder away.

### Atomic Writes

All file writes (autosave, save, backup, settings, session) use a temp-file-plus-rename pattern. If the write is interrupted, the original file remains intact. You will never have a partial or corrupt file.

---

## Storage Locations

By default, everything lives inside the application folder:



MarkdownEditor.exe Templates
 Standard SOP Document.md ... Data
 Profiles
 
 Autosave
 Draft-.json Backups
 -.md.bak Logs
 Application.log Settings
 Settings.json


If the application folder is read-only (running from a locked-down network share, for example), user data falls back to:



%LOCALAPPDATA%\MarkdownEditor\Profiles<username>\


The status bar shows "Portable" when writing locally or "Fallback" when using `%LOCALAPPDATA%`.

---

## Release History

### v2.0.0 (Major)

- Multi-tab editing with independent per-tab undo, editor, and preview
- Session restore across launches (all tabs reopen automatically)
- Autosave keyed per document ID
- Switch-to-existing tab when reopening a file
- New from Template opens as a new tab

### v1.4.0 (Minor)

- Ctrl+B and Ctrl+I keyboard shortcuts for bold and italic
- List auto-continuation for bulleted and numbered lists
- Auto-increment for numbered lists
- Empty list-line exit behavior
- Tab and Shift+Tab for list indent and outdent
- Ctrl+0 through Ctrl+4 for heading toggling

### v1.3.1 (Patch)

- Fixed: Find and Replace matches now visibly highlighted while Find box has focus
- Editor auto-scrolls to matches when navigating with F3

### v1.3.0 (Minor)

- Export to PDF using WebView2's native rendering
- Theme-aware PDF output (light or dark)
- Auto-opens PDF in default viewer after export

### v1.2.0 (Minor)

- Find and Replace panel with Ctrl+F and Ctrl+H shortcuts
- Match case and match whole word options
- Match counter showing "Match N of M"
- Replace one and Replace All actions
- F3 and Shift+F3 to navigate matches

### v1.1.0 (Minor)

- Templates folder with four starter templates
- New from Template toolbar dropdown
- Custom templates supported (drop any .md file into Templates folder)
- Refresh templates and Open templates folder actions

### v1.0.0 (Major)

- Initial release
- Live Markdown preview via Markdig and WebView2
- Open, Save, Save As with atomic writes and automatic backups
- Recent Files dropdown
- Light, Dark, and System theme switcher
- Window size and position persistence
- Crash-safe autosave with restore on launch
- Per-user profile isolation
- Structured rotating logs
- Portable folder layout with %LOCALAPPDATA% fallback

---

## Markdown Syntax Reference

Markdown is a plain-text formatting syntax that converts to HTML. The application supports the full CommonMark specification plus Markdig's advanced extensions.

### Headings

Prefix a line with one to six hash marks:

    # Heading 1
    ## Heading 2
    ### Heading 3
    #### Heading 4
    ##### Heading 5
    ###### Heading 6

Renders as decreasing-size headings.

### Emphasis

Wrap text in asterisks or underscores:

    *italic* or _italic_
    **bold** or __bold__
    ***bold italic*** or ___bold italic___
    ~~strikethrough~~

Renders as *italic*, **bold**, ***bold italic***, ~~strikethrough~~.

### Paragraphs and Line Breaks

Separate paragraphs with a blank line. To force a line break without a new paragraph, end a line with two spaces or a backslash.

### Bulleted Lists

Any of these three markers work:

    - Item
    * Item
    + Item

Nest by indenting two spaces per level:

    - Outer
      - Inner
        - Deepest

### Numbered Lists

Prefix with a number and period:

    1. First
    2. Second
    3. Third

The renderer ignores the actual numbers you type — only the position matters. You can write `1. `, `1. `, `1. ` and it renders as 1, 2, 3. The first number is honored as the starting offset.

### Task Lists (GFM Extension)

    - [ ] Unchecked task
    - [x] Completed task

Renders as clickable-looking checkboxes.

### Links

Inline links:

    [link text](https://example.com)
    [with title](https://example.com)

Reference links:

    [link text][label]

    [label]: https://example.com

Autolinks:

    <https://example.com>

### Images

Same syntax as links, prefixed with an exclamation mark:

    path/to/image.png
    image.png "Optional title"

### Blockquotes

Prefix lines with a right angle bracket:

    > This is a blockquote.
    > It can span multiple lines.
    >
    > > Nested blockquotes work too.

### Code

Inline code uses backticks:

    Use the `Get-Item` cmdlet.

Fenced code blocks use three backticks. You can specify a language for syntax highlighting:

    ``` powershell
    Get-Process | Where-Object CPU -gt 100
    ```

    ``` csharp
    public void Hello()
    {
        Console.WriteLine("Hi");
    }
    ```

Indented code blocks use four spaces of indent instead of fences.

### Horizontal Rules

Three or more hyphens, asterisks, or underscores on their own line:

    ---
    ***
    ___

### Tables

Tables are pipe-separated columns with a header row and an alignment row.

Basic table:

    | Column 1 | Column 2 | Column 3 |
    |----------|----------|----------|
    | Value A  | Value B  | Value C  |
    | Value D  | Value E  | Value F  |

Alignment is controlled by the colons in the alignment row:

    | Left-aligned | Centered | Right-aligned |
    |:-------------|:--------:|--------------:|
    | text         | text     | text          |
    | more         | more     | more          |

- `:---` — left align (default)
- `:---:` — center align
- `---:` — right align

Column widths auto-fit content. You do not need to pad columns for the table to render, but padding makes the source easier to read.

Tables can contain inline formatting:

    | Feature | Ctrl+F | Description |
    |---------|--------|-------------|
    | **Find** | Yes | Search current tab |
    | *Match case* | Aa | Toggle case sensitivity |

### Footnotes (Markdig Extension)

    Here is some text with a footnote.[^1]

    [^1]: This is the footnote content.

### HTML

Raw HTML is passed through:

    <div style="color: red">
      Custom HTML in Markdown
    </div>

Use sparingly — HTML embedded in Markdown reduces portability.

### Escaping

Prefix a special character with a backslash to render it literally:

    \*not italic\*
    \# not a heading
    \` not code

The characters most commonly escaped are `\ ` `` ` `` `*` `_` `{ }` `[ ]` `( )` `#` `+` `-` `.` `!`.

### Comments

Markdown has no native comment syntax. Use HTML comments if needed:

    <!-- This is a comment and won't render. -->

---

## Appendix — Where to Get Help

- The application logs to `Data\Profiles\<username>\Logs\Application.log` — check this file if something misbehaves
- Settings live in `Data\Profiles\<username>\Settings\Settings.json` and can be edited directly if needed
- All source files are under `Docs\` in the application folder
- The application is self-contained — there is no external dependency to install

---

*Markdown Editor — Portable WinUI 3 for Windows 11 and Server 2025*