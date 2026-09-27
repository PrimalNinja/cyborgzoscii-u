# zwrpublish - ZOSCII Web Radio Publish

Console exe that publishes a folder of MP3s to a ZOSCII MQ queue, in the layout the web radio
player and zwrserve expect.

## Usage

```
zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-d]
```

| Flag | Meaning |
|---|---|
| `<folder> <mqurl> <queue>` | publish the MP3s in `<folder>` to `<queue>` at `<mqurl>` (index.php) |
| `-z <romfile>` | ZOSCII encode each file with `<romfile>` (any file: a jpg, png, anything) |
| `-u <romfile>` | UNSIGNAL encode each file with `<romfile>` |
| `-r <days>` | retention in days (default 7) |
| `-s<order>` | order to publish the MP3s in: `-sf` = by filename (default), `-sd` = by each file's filesystem last-write datetime |
| `-d` | dry run: list what would be published, publish nothing |

With neither `-z` nor `-u`, the files are published as they are.

```
zwrpublish c:\music https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -r 3650 -sd
```

## What it publishes

For each `.mp3` in the folder (not subfolders), in the order set by `-s<order>` (default
`-sf`, filename order; `-sd` sorts by each file's filesystem last-write datetime instead):

1. `<name>.jpg` (or `.jpeg`), the cover image, if present
2. `<name>.txt`, the lyrics or text, if present
3. `<name>.mp3`

Extensions are matched ignoring case. Files with no matching MP3 are ignored. Whichever order
the MP3s are published in, the jpg and txt for a given track are always published immediately
before that track's mp3.

## Order and retries

- The MQ names each message by the second it arrives. Messages published within the same second
  can sort in any order, so zwrpublish waits a little over a second between publishes. That keeps
  jpg -> txt -> mp3 in order in the queue, at about one second per file.
- Each file gets its own nonce and up to 3 attempts. If a reply is lost after the file was
  actually stored, the retry is answered "Nonce already used." and counts as done, so nothing is
  published twice.
- It stops at the first file that still fails. Everything listed above the failure was published.

## Build

`build.bat` builds a self-contained single-file `bin/zwrpublish/zwrpublish.exe` (win-x64).
Unzip the `zwrpublish` folder next to `src\` in the nuget source, the same as zwrserve. Uses
`MQClient.cs`, `ZOSCII.cs`, `Unsignal.cs` and `SecureDelete.cs` from `..\src`.