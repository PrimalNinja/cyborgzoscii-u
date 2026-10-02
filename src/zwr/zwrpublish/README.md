# zwrpublish - ZOSCII Web Radio Publish

Console exe that sends a folder to a ZOSCII MQ: to a queue (publish) or to the store (store),
one message at a time or as one transaction.

## Usage

```
zwrpublish <folder> <mqurl> <queue>|-store [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-j] [-b] [-d]
zwrpublish <folder> <mqurl> <queue>|-store -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]
zwrpublish -c <guid> <mqurl> <queue>|-store [-r <days>]
zwrpublish -x <guid> <mqurl> [-store]
```

| Flag | Meaning |
|---|---|
| `<queue>` | publish to this queue |
| `-store` | store instead of publishing |
| `-z <romfile>` | ZOSCII encode each file with `<romfile>` (any file: a jpg, png, anything) |
| `-u <romfile>` | UNSIGNAL encode each file with `<romfile>` |
| `-r <days>` | retention in days (default 7). In a transaction it's applied at the commit |
| `-s<order>` | `-sf` = by filename, `-sd` = by each file's last-write datetime. Default `-sf` in files mode, `-sd` in JSON mode |
| `-j` | JSON mode (below) |
| `-b` | batch: everything as one MQ transaction, committed at the end |
| `-t <guid>` | stage into transaction `<guid>` without committing it. `-t new` starts a new one and prints its GUID |
| `-c <guid>` | commit transaction `<guid>` to `<queue>` or to the store |
| `-x <guid>` | roll back transaction `<guid>`: everything staged in it is deleted |
| `-d` | dry run: list what would be sent, send nothing |

With neither `-z` nor `-u`, files are sent as they are. Give either a `<queue>` or `-store`.
Only one of `-b`, `-t`, `-c` and `-x` can be used at a time.

## Files mode (default)

For each `.mp3` in the folder (not subfolders), in `-s` order:

1. `<name>.jpg` (or `.jpeg`), the cover image, if present
2. `<name>.lyrics.txt`, the lyrics or text, if present
3. `<name>.mp3`

Extensions are matched ignoring case. With `<queue>` they're published, exactly as before.
With `-store` they're stored, and each file's store name is added to
`<folder>\zwrpublish-stored.csv` (time, file, size, store name).

## JSON mode (`-j`)

Works on the `<name>.json` files made by zwrprepare, in `-s` order (file datetime by default,
which zwrprepare sets to the mp3's datetime). JSONs that aren't `"class": "zwr"`,
`"type": "track"` are skipped.

1. **Store the files.** Every `mp3` / `image` / `text` / `lyrics` entry that has a `file` and
   no store `name` yet is stored, and the store name is written back into the JSON. The JSON
   keeps its created and modified datetime, so its place in the order doesn't change. Before
   anything is sent, every such file is checked against the `size` and `hash` zwrprepare
   recorded; a file that has changed stops the run ("run zwrprepare again").
2. **Publish the JSONs** to `<queue>`, encoded the same way as the files. A JSON with any
   entry still missing its store name is refused.

`-store -j` does step 1 only. Running `-j` again stores only what isn't stored yet, so after a
failure just run it again - but step 2 publishes every JSON again each time.

Your pipeline:

```
mp3id      c:\music "Copyright (c) 2026 Cyborg Unicorn" -a "Cyborg Unicorn / Primal Ninja"
zwrprepare c:\music -t c:\zwr\template.json
zwrpublish c:\music https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -r 3650 -j -b
```

## Transactions (`-b`, `-t`, `-c`, `-x`)

- **`-b`**: files mode stages every file in one transaction and commits it at the end. JSON
  mode uses two: one for the files, committed to the store, then one for the JSONs, committed
  to the queue. Nothing appears until a commit, then all of it at once, in order, with no
  one-second wait between files. If a file fails, its transaction is rolled back and nothing
  from it is sent. If a commit fails, everything stays staged and the exact `-c` command to
  send it again is printed. A commit can be sent again safely: one that stopped part way
  carries on, and one that already finished just answers again.
- **`-t <guid>`**: stage a big catalogue over several runs while nothing live changes, then
  `-c <guid> <mqurl> <queue>` or `-c <guid> <mqurl> -store` sends the lot, or
  `-x <guid> <mqurl>` throws it away. If a `-t` run stops part way, what it staged stays
  staged; running the same folder again stages those files a second time, so run it on just
  the files that are left (or roll back and start again).
- The MQ deletes a transaction with no activity for a day (`TRANSACTION_EXPIRY`).

## Order and retries

- Single publishes: the MQ names each message by the second it arrives, so zwrpublish waits a
  little over a second between publishes to keep jpg -> txt -> mp3 (or JSON) order.
- Transactions: the MQ names everything at the commit, in upload order, so there's no wait.
- Each file gets up to 3 attempts. Publishes and staged files carry a nonce, so a retry after
  a lost reply can't send a duplicate. Single stores don't, because a retried store has to get
  its store name back: if a reply is lost after the file was stored, the retry stores a second
  copy and the first is left unused until its retention runs out. A batch store (`-b`) can't
  do that - its names come back from the commit.
- It stops at the first file that still fails.

Store and transactions need index.php v20261003 or later, with `ALLOW_STORE` set to `TRUE` for
anything going to the store.

## Build

`build.bat` builds a self-contained single-file `bin/zwrpublish/zwrpublish.exe` (win-x64).
Put the `zwrpublish` folder next to `src\` in the nuget source, the same as zwrserve. Uses
`MQClient.cs`, `ZOSCII.cs`, `Unsignal.cs`, `SecureDelete.cs` and `ZRollingHash.cs` from
`..\src`. The store and transaction calls are in `zwrpublish.cs` itself, so the nuget source
is unchanged.