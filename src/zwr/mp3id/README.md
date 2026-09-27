# mp3id - MP3 Title Tag Updater

Console exe that scans a folder and rewrites the ID3 tags on every MP3 in it: title (from
the filename), artist (passed in on the command line, or left as whatever's already tagged),
year (from the file's date), and copyright (passed in on the command line) - then adds a
matching `.jpg` as album art and a matching `.txt` as a comment, if either is sitting next to
the mp3.

## Usage

```
mp3id.exe <folderpath> "<copyright text>" [-a "<artist>"] [-l <logofile>] [-s<order>]
```

Processes every `*.mp3` file directly inside `<folderpath>` (not subfolders), with no
skipping or filtering other than the basic MP3 validity check below. The copyright text is
required and should be quoted since it normally contains spaces.

```
mp3id.exe C:\Radio\Uploads "Copyright (c) 2026 Cyborg Unicorn. All rights reserved." -a "Cyborg Unicorn"
mp3id.exe C:\Radio\Uploads "Copyright (c) 2026 Cyborg Unicorn." -l C:\Radio\logo.jpg -sd
```

`-a "<artist>"` is optional and sets `TPE1`. When it's omitted, each file's *existing* artist
tag (if it has one) is read before the old tags are stripped, and that value is carried over
unchanged into the fresh tag - so the artist is simply left as it is. If a file has no
existing artist tag either, no `TPE1` frame is written at all. There is no built-in default
artist name.

`-l <logofile>` is optional. When given, that one `.jpg` is used as the album art for
*every* mp3 in the folder - the per-file `<filename>.jpg` lookup described below is skipped
entirely, so any per-track `.jpg` files sitting next to the mp3s are ignored. Without `-l`,
behaviour is unchanged: each file gets its own matching `.jpg` if one exists, or no art at
all if it doesn't.

`-s<order>` is optional and sets the order files are processed (and logged) in. All the
filenames in the folder (and, for `-sd`, their filesystem last-write datetimes) are read up
front, sorted, and then processed in that order. This only changes processing order - the
per-file `.jpg`/`.txt`/`.lyrics.txt` matching rules are unchanged either way.

| Flag  | Order |
|---|---|
| `-sf` | By filename (default) |
| `-sd` | By each file's filesystem last-modified datetime |

## What gets written

| Frame | Source |
|---|---|
| `TIT2` | Title - the filename verbatim, minus the `.mp3` extension. Nothing is changed; rename the file to whatever the title should read. |
| `TPE1` | Artist - from `-a "<artist>"` if given; otherwise the file's existing artist tag, preserved unchanged, if it has one; otherwise omitted |
| `TYER` / `TDRC` | Year - taken from the file's last-modified date |
| `TCOP` | Copyright - the second command-line argument |
| `COMM` | Comment - contents of `<filename>.txt` next to the mp3, if present |
| `USLT` | Lyrics (plain, no timing) - contents of `<filename>.lyrics.txt` next to the mp3, if present |
| `APIC` | Album art (front cover) - the `-l <logofile>` image if given, otherwise the contents of `<filename>.jpg` next to the mp3, if present |

Every leading ID3v2 tag is stripped first (not just one), and only the fresh minimal tag
above is written back, so everything else - album, genre, existing art, comments, ID3v1 -
is removed, *except* the artist, which is read out and preserved first when `-a` isn't given.
The file's original last-write time is restored after saving.

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
- Text, comment and lyrics frames (`TIT2`/`TPE1`/`TYER`/`TCOP`/`COMM`/`USLT`) are written UTF-8
  encoded (encoding byte `0x03`), so Chinese, Korean, and any other non-Latin-1 text round-trips
  correctly. `APIC`'s own encoding byte is the one exception - it's always `0x00` (Latin-1),
  since its description field is left empty and `0x00` is the one value every ID3v2.3 reader
  is guaranteed to accept there.
- Every file in the folder is attempted; a failure on one file doesn't stop the rest.
- When `-a` is omitted, the existing artist is read from whatever ID3v2 tag is already on the
  file - v2.2 (`TP1`), v2.3 or v2.4 (`TPE1`), in ISO-8859-1, UTF-16 or UTF-8 - before that tag
  is stripped. This is a best-effort read: on anything unexpected it simply finds no artist
  rather than risking a wrong one, in which case no `TPE1` frame is written.
- `-l <logofile>` is checked once up front (must exist and start with the `FF D8` JPEG
  signature) before any mp3 is touched, so a bad path fails fast with a clear message
  instead of partway through the run.

## Build

`build.bat` builds a self-contained single-file `bin/mp3id/mp3id.exe` (win-x64). The project
targets plain `net8.0`, so `-r linux-x64` builds a Linux version from the same source.
No external dependencies - just the .NET SDK.