# RealMouse (Windows)

Native Windows automation utility designed for AI agents (WorkBuddy, Claude, Cursor, Windsurf, etc.).

## Key Features
- High-speed desktop text search using Windows WinRT OCR.
- Zero-disk memory buffer operations without temporary files.
- Relative semantic positioning (`--toright`, `--toleft`, `--below`).
- Anti-deadlock mouse wheeling and smooth trajectory movement.
- Single-process batch scripting via `.rmc` scripts.

## Directory Structure
- `bin/`: Contains precompiled `rm.exe` and `RealMouse.dll`.
- `src/`: C# source files (`Program.cs`, `RealMouse.cs`, `OcrService.cs`).
- `lib/`: PowerShell integration modules.
- `examples/`: Sample automation scripts.
- `SKILL.md`: Agent skill instructions.

## Usage
```powershell
# Search and click
bin\rm.exe ocrw "Submit" --click

# Type into adjacent input field
bin\rm.exe ocrw "Username" --toright 50 --type "admin"

# Scroll down
bin\rm.exe wheel -4 500 500
```
