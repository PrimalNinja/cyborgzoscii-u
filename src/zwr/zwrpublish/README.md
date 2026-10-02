# zwrpublish - ZOSCII Web Radio Publish

Console exe that publishes a folder of MP3s to a ZOSCII MQ queue, in the layout the web radio
player and zwrserve expect.

## Usage

```
zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-b] [-d]
zwrpublish <folder> <mqurl> -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]
zwrpublish -c <guid> <mqurl> <queue> [-r <days>]
zwrpublish -x <guid> <mqurl>
```

| Flag | Meaning |
|---|---|
| `<folder> <mqurl> <queue>` | publish the MP3s in `<folder>` to `<queue>` at `<mqurl>` (index.php) |
| `-z <romfile>` | ZOSCII encode each file with `<romfile>` (any file: a jpg, png, anything) |
| `-u <romfile>` | UNSIGNAL encode each file with `<romfile>` |
| `-r <days>` | retention in days (default 7). With transactions it's applied at the commit |
| `-s<order>` | order to publish the MP3s in: `-sf` = by filename (default), `-sd` = by each file's filesystem last-write datetime |
| `-b` | batch: upload everything into one MQ transaction, then commit it to `<queue>` |
| `-t <guid>` | upload into transaction `<guid>` without committing it. `-t new` starts a new one and prints its GUID |
| `-c <guid>` | commit transaction `<guid>` to `<queue>` |
| `-x <guid>` | abort transaction `<guid>`: everything uploaded into it is deleted |
| `-d` | dry run: list what would be published, publish nothing |

With neither `-z` nor `-u`, the files are published as they are. Only one of `-b`, `-t`, `-c`
and `-x` can be used at a time.

```
zwrpublish c:\music https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -r 3650 -sd
zwrpublish c:\music https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -r 3650 -sd -b
```

## What it publishes

For each `.mp3` in the folder (not subfolders), in the order set by `-s<order>` (default
`-sf`, filename order; `-sd` sorts by each file's filesystem last-write datetime instead):

1. `<name>.jpg` (or `.jpeg`), the cover image, if present
2. `<name>.lyrics.txt`, the lyrics or text, if present
3. `<name>.mp3`

Extensions are matched ignoring case. Files with no matching MP3 are ignored. Whichever order
the MP3s are published in, the jpg and txt for a given track are always published immediately
before that track's mp3.

## Modes

**Publish (default)** - one publish per file, the same as before. Each file is live in the
queue as soon as it's published.

**Batch (`-b`)** - every file is uploaded into one MQ transaction, then the transaction is
committed to `<queue>`. Nothing appears in the queue until the commit; then everything appears
at once, in upload order. There's no one-second wait between files.

- If an upload fails, the transaction is aborted and nothing is published.
- If the commit fails, the uploads stay in the transaction and the exact `-c` command to send
  the commit again is printed. A commit can be sent again safely: one that stopped part way
  carries on, and one that already finished just answers again.

**Upload only (`-t`)** - uploads into a transaction and leaves it uncommitted, so a big
catalogue can go up over several runs (for example one folder at a time, each with
`-t <same guid>`) while the live queue is untouched. Then `-c <guid> <mqurl> <queue>` publishes
the lot, and `-x <guid> <mqurl>` throws it away instead. If a `-t` run stops part way, what it
uploaded stays in the transaction. Running the same folder again uploads those files a second
time, so either abort and start again, or run it on just the files that are left.

The MQ deletes a transaction that has had no upload, commit or abort for a day (its
`TRANSACTION_EXPIRY`), so a multi-run upload needs a run at least once a day until it's committed.

## Order and retries

- Without transactions, the MQ names each message by the second it's published, and messages
  published within the same second are numbered in the order they finished. zwrpublish still
  waits a little over a second between publishes, so jpg -> txt -> mp3 order never depends on
  that, at about one second per file.
- With transactions, the MQ names every file at the commit, numbering them in upload order
  within the same second (and carrying into the next second past 9,999), so no wait is needed.
- Each file gets its own nonce and up to 3 attempts. If a reply is lost after the file was
  actually published or uploaded, the retry is answered "Nonce already used." and counts as
  done, so nothing goes in twice.
- It stops at the first file that still fails.

Transactions need index.php v20261002 or later with `ALLOW_TRANSACTIONS` set to `TRUE`.

## Build

`build.bat` builds a self-contained single-file `bin/zwrpublish/zwrpublish.exe` (win-x64).
Unzip the `zwrpublish` folder next to `src\` in the nuget source, the same as zwrserve. Uses
`MQClient.cs`, `ZOSCII.cs`, `Unsignal.cs` and `SecureDelete.cs` from `..\src`. The
transaction calls (upload, commit, abort) are in `zwrpublish.cs` itself, so the nuget source
is unchanged.