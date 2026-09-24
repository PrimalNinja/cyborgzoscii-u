# mp3id - MP3 Title Tag Updater

Console exe that scans a folder and rewrites the ID3 tags on every MP3 in it: title (from
the filename), artist (fixed), year (from the file's date), and copyright (passed in on the
command line) - then adds a matching `.jpg` as album art and a matching `.txt` as a comment,
if either is sitting next to the mp3.

## Usage

```
mp3id.exe <folderpath> "<copyright text>"
```

Processes every `*.mp3` file directly inside `<folderpath>` (not subfolders), with no
skipping or filtering other than the basic MP3 validity check below. The copyright text is
required and should be quoted since it normally contains spaces.

```
mp3id.exe C:\Radio\Uploads "Copyright (c) 2026 Cyborg Unicorn. All rights reserved."
```

## What gets written

| Frame | Source |
|---|---|
| `TIT2` | Title - the filename verbatim, minus the `.mp3` extension. Nothing is changed; rename the file to whatever the title should read. |
| `TPE1` | Artist - fixed, set in `Program.cs` |
| `TYER` / `TDRC` | Year - taken from the file's last-modified date |
| `TCOP` | Copyright - the second command-line argument |
| `COMM` | Comment - contents of `<filename>.txt` next to the mp3, if present |
| `USLT` | Lyrics (plain, no timing) - contents of `<filename>.lyrics.txt` next to the mp3, if present |
| `APIC` | Album art (front cover) - contents of `<filename>.jpg` next to the mp3, if present |

Every leading ID3v2 tag is stripped first (not just one), and only the fresh minimal tag
above is written back, so everything else - album, genre, existing art, comments, ID3v1 -
is removed. The file's original last-write time is restored after saving.

## Logs - `<folderpath>/logs/`

| File | Contents |
|---|---|
| `yyyyMMdd.log` | one line per successfully updated file, with what was written |
| `yyyyMMdderror.log` | one line per failed file (e.g. didn't pass the basic MP3 check), with the reason |

## Behaviour

- Basic MP3 check: same test `zwrserve.cs` uses - a file passes only if a valid MPEG-1/2
  Layer III frame header is found, confirmed by a second valid header immediately after it
  (or landing exactly on end of file). Anything that fails this is skipped and logged as an
  error; nothing stops the run.
- Frame sizes follow the ID3v2.3 spec: the tag header size is synchsafe (7 bits/byte), but
  each frame's own size field is plain big-endian - not synchsafe - matching the original
  PHP tool this was ported from.
- Text and comment frames are written UTF-8 encoded (encoding byte `0x03`).
- Every file in the folder is attempted; a failure on one file doesn't stop the rest.

## Build

`build.bat` builds a self-contained single-file `bin/mp3id/mp3id.exe` (win-x64). The project
targets plain `net8.0`, so `-r linux-x64` builds a Linux version from the same source.
No external dependencies - just the .NET SDK.