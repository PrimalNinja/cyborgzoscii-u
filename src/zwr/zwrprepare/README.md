# zwrprepare - ZOSCII Web Radio Prepare

Console exe that writes a zwr track JSON next to every MP3 in a folder. Each JSON is filled in
with everything that can be worked out locally: ID3 tags, audio format and exact duration, and
the size and hash of the MP3 and its matching image, text and lyrics files. Nothing is sent
anywhere.

## Usage

```
zwrprepare <folder> [-t <template.json>]
```

| Flag | Meaning |
|---|---|
| `<folder>` | every `.mp3` directly in this folder (not subfolders) gets a `<name>.json` next to it |
| `-t <template.json>` | station, channel, artist and stores to put in every track's JSON |

```
zwrprepare c:\music -t c:\zwr\template.json
```

## Matching files

For `<name>.mp3`, matched ignoring case:

| JSON entry | File |
|---|---|
| `mp3` | `<name>.mp3` |
| `image` | `<name>.jpg`, else `<name>.jpeg`, else `<name>.png` |
| `text` | `<name>.txt` |
| `lyrics` | `<name>.lrc`, else `<name>.lyrics.txt` (mp3id's plain lyrics file) |

An entry is left out when its file isn't there.

## Where each field comes from

| Field | Source |
|---|---|
| `class`, `version`, `type` | always `"zwr"`, `0`, `"track"` |
| `id` | the existing JSON's `id`, else the MP3's `UFID` frame with owner `cyborgunicorn.com.au`, else a new GUID |
| `instance` | the existing JSON's value, else `""`. It belongs to a publication, so publishing sets it |
| `title` | ID3 `TIT2` (ID3v1 title as a fallback), else the filename |
| `album`, `year`, `genre`, `copyright` | ID3 `TALB`, `TYER`/`TDRC`, `TCON`, `TCOP` (ID3v1 as a fallback). Left out when empty |
| `station`, `channel` | the template |
| `artist` | `name` from the template, else `TPE1`; `website` from the template, else `WOAR`; the rest from the template |
| `stores` | the template |
| media `store` | the first store in the template's `stores` |
| media `name` | `""`. This is the name in the store, filled in when the file is stored |
| media `file` | the local filename it was made from |
| media `size`, `hash` | byte count, and `ZRollingHash` (forward) of the file as it is on disk, before any encoding, as 8 hex digits |
| `mp3.duration` | seconds, worked out from every MPEG frame, so VBR is exact. A Xing/Info/VBRI header frame isn't counted |
| `mp3.bitrate` | average kbps over the audio frames |
| `mp3.samplerate`, `mp3.channels` | from the first frame |

The order of precedence is the template, then the MP3 and its files, then the existing JSON.
For example, a template `artist.name` overrides `TPE1` for every track; leave it `""` to use
each MP3's own artist.

ID3v2.2, 2.3 and 2.4 are read, in Latin-1, UTF-16 and UTF-8, so Korean, Chinese and other
non-Latin text comes through. The JSON is written as UTF-8 without escaping it.

## Running it again

Running it again on the same folder updates each JSON in place:

- `id`, `instance`, and anything else added to the JSON by hand (extra keys, artist `email`,
  and so on) are kept.
- A media entry keeps its store `name` only while the file's `size` and `hash` still match.
  If the file has changed (re-tagged by mp3id, say), the `name` is cleared, because the stored
  copy is now out of date and has to be stored again.
- An entry whose file has been removed is dropped.
- A JSON whose content wouldn't change isn't rewritten. The console shows `created`,
  `updated` or `unchanged` for each MP3.
- Every JSON, including unchanged ones, is given the same created and modified date/time as
  its MP3, so the two always sort together by date.
- An existing `<name>.json` that isn't valid JSON is left as it is, and that MP3 counts as
  failed.

## Template

`template.json` (comments and trailing commas are allowed):

```json
{
  "station": { "name": "Cyborg Unicorn World", "id": "", "website": "https://cyborgunicorn.com.au", "email": "", "phone": "" },
  "channel": { "name": "English", "id": "" },
  "artist":  { "name": "", "id": "", "website": "", "email": "", "phone": "" },
  "stores":  [ { "name": "main", "type": "mq", "path": "https://cyborgunicorn.com.au/radio/indexmq.php" } ]
}
```

Any other key in the template is copied into every JSON.

## Checks

- An MP3 must contain MPEG Layer III frames that pass the same two-header check zwrserve uses.
  Anything else counts as failed, and no JSON is written for it.
- If the sample rate or channel count changes inside a file, a warning is printed. Chrome stops
  playing at a change like that.
- Exit code: `0` all OK, `2` if any MP3 failed, `1` for bad arguments.

## Build

`build.bat` builds a self-contained single-file `bin/zwrprepare/zwrprepare.exe` (win-x64).
Unzip the `zwrprepare` folder next to `src\` in the nuget source, the same as zwrserve. Uses
`ZRollingHash.cs` from `..\src`. JSON uses the built-in `System.Text.Json`, so there are no
packages to fetch.