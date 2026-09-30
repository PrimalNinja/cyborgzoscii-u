# zwrserve - ZOSCII Web Radio Serve

Console exe that streams MP3 messages from a ZOSCII MQ queue to ICY (Icecast/SHOUTcast)
listeners. Works with VLC, Winamp, foobar2000 and a browser `<audio>` element.

## Usage

```
zwrserve -i <port> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-s] [-w] [-p <seconds>] [-e <mp3file>] [-a <folder> <x> <y>] [-ip <url> [-c <seconds>]] [-log]
```

| Flag | Meaning |
|---|---|
| `-i <port> <mqurl> <queue>` | ICY server on `<port>`, pulling `<queue>` from `<mqurl>` (index.php) |
| `-z <romfile>` | unZOSCII each message with `<romfile>` |
| `-u <romfile>` | unUNSIGNAL each message with `<romfile>` |
| `-s` | shared pointer: one playhead (session 0), every listener hears the same thing |
| `-w` | wait for new messages at the end of the queue (default: loop to the start) |
| `-p <seconds>` | poll interval while waiting for new messages (default 10) |
| `-e <mp3file>` | local MP3 played from the top, looped, while waiting (default: silence) |
| `-a <folder> <x> <y>` | inserts: after every `<y>` normal tracks, `<x>`% of the time, play a random MP3 from `<folder>` (ads, host talk) |
| `-ip <url>` | post the current public IP to `<url>` whenever it changes (default: disabled) |
| `-c <seconds>` | seconds between IP checks - only used with `-ip` (default 300) |
| `-log` | log every console line and every MQ fetch to `<exename>log.csv` next to the exe |

```
zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom
zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom -ip https://example.com/update-ip -c 120
zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom -s -a c:\\zwr\\ads 50 3
```

## Listener URLs

Default mode (session per listener):

- `http://host:8000/` - new session. Its number is in the `X-ZWR-Session` response header and on the console.
- `http://host:8000/<n>` - resume session `<n>` where it stopped, mid-track. It's created if it doesn't exist.
- A new connection to a session that is already streaming takes it over. The old connection is cut
  off and can no longer save or move the position.
- Browsers open a stream URL twice: a page load, dropped straight away, then the real player.
  zwrserve handles that. A brand new session that lasts under 5 seconds leaves no files. On an
  existing session, a connection doesn't save or move to the next track in its first 5 seconds
  (it sends silence if the track ends in that time). So the throwaway request never changes
  the listener's position.

In shared mode (`-s`), any path joins the one stream.

## Working files - `<exepath>/t/<port>/`

Each port gets its own folder, so one exe can run several instances (one channel per port)
without their session numbers colliding.

| File | Contents |
|---|---|
| `<session>_<yyyyMMddHHmmss>.ptr` | line 1: MQ pointer (last completed message)<br>line 2: message currently in the .bin<br>line 3: byte offset of the next frame in the .bin |
| `<session>.bin` | current payload, decoded |

The `.ptr` is rewritten with a new timestamp every time the pointer moves and when the
listener disconnects, so the timestamp is the last-used time. A cleanup tool deletes
`.ptr` files older than N days along with the matching `.bin`.

## Behaviour

- Non-MP3 messages are skipped (logged). It's MP3 only if two consecutive MPEG Layer III
  frame headers are found near the start.
- ID3v2 and ID3v1 tags are stripped from the stream. `Artist - Title` from the tags is sent
  as `StreamTitle` to clients that ask for ICY metadata (`Icy-MetaData: 1`). Otherwise the
  MQ filename is used.
- While waiting for a new message (end of queue with `-w`, empty queue, or MQ unreachable) the
  elevator MP3 plays from the top, looped, or silent frames matching the last track are sent,
  so players stay connected. The queue is polled every `-p` seconds and the next track cuts in
  at the next frame. Its ID3 title is sent as `StreamTitle` during the wait.
- Frames are sent at real play speed, with a 4 second burst ahead so players start straight away.
- Shared mode: the playhead keeps running with no listeners. A listener that falls 1MB
  behind is dropped.
- Keep a queue (and the elevator MP3) at one sample rate and one channel count. Chrome stops the
  stream with `PIPELINE_ERROR_DECODE: Unsupported midstream configuration change` when either
  changes, and doesn't recover. zwrserve logs a warning when it happens. Silence always matches
  the last track, so it's safe.
- Inserts (`-a <folder> <x> <y>`): every `<y>` normal tracks there's a slot, and `<x>`% of the
  time it's filled with a random MP3 from `<folder>` (plain MP3s, not encoded, not from the MQ).
  `-a ads 50 3` = after every 3 songs, a 50/50 chance of an ad or talk segment; `-a ads 100 1` = one
  after every song. The folder is re-read each time, so files can be added or removed while it
  runs. The same insert isn't picked twice running when there's a choice. An insert whose sample
  rate or channel count differs from the stream is skipped (logged), because Chrome stops at a
  format change. Inserts don't move the queue pointer; skipped non-MP3 messages don't count as
  tracks. In shared mode everyone hears the same inserts; in session mode each listener rolls
  their own.
- Ctrl+C, or closing the console window, saves every session's pointer and offset.
- Optional (`-ip <url>`): every `-c` seconds (default 300 = 5 minutes), zwrserve fetches its
  current public IP from an external "what's my IP" service and, only if it's changed since
  the last successful announce, sends it to `<url>` - either a `POST` with form field `ip`,
  or a `GET` with `{ip}` substituted in the URL if it contains that literal placeholder
  (e.g. `-ip "https://example.com/update?myip={ip}"`), useful for simple dynamic-DNS-style
  webhooks. This is a real network call each time - there's no free local way to learn a
  home router's WAN address - so `-c` below ~60 seconds risks the lookup service
  rate-limiting or blocking the requests; zwrserve logs a warning at startup if it's set
  that low, but still runs. Lookup or announce failures are logged and retried at the next
  interval; they never affect streaming.

## Log file (`-log`)

`-log` writes to `<exename>log.csv` in the exe's folder (`zwrservelog.csv` for `zwrserve.exe`).
It's UTF-8 with a BOM, so Excel shows non-English titles properly. Several instances (one per
port) can share the file: each line carries its port, and they take turns writing. If the file
is open somewhere that locks it, lines wait in memory and are written once it's free.

Columns: `Time,Port,Session,Event,Status,After,Message,HttpStatus,BytesReceived,BytesExpected,FirstByteMs,TotalMs,Reason,Text`

| Event | Meaning |
|---|---|
| `START` | program started - `Text` has the version and command line |
| `LOG` | a console line - `Text` has it, `Session` is filled in from its `[n]` / `[shared]` prefix |
| `FETCH_START` | fetch of the next message after `After` began |
| `FETCH_END` | fetch finished - `Status`, `Message` (filename), HTTP status, bytes, timings, `Reason` |
| `CHECK_START` / `CHECK_END` | the same, for the check made when a session starts waiting |
| `DROPPED` | only if over 200,000 lines had to wait for a locked file - says how many were lost |

`Status` on a `_END` line:

| Status | Meaning |
|---|---|
| `OK` | message received |
| `EMPTY` | the MQ says there's nothing after that pointer - end of queue |
| `TIMEOUT` | no complete response within 60 seconds; `Reason` says whether it never answered or answered but didn't finish, and `BytesReceived` shows how far it got |
| `NETWORK` | couldn't connect, name lookup failed, or the connection dropped - `Reason` has the error |
| `HTTP_ERROR` | the web server returned an error status (e.g. `508 Resource Limit Is Reached`) - `Reason` has the start of its page |
| `SERVER_ERROR` | the MQ answered with an error (e.g. `Queue '...' does not exist.`) |
| `UNKNOWN` | an answer zwrserve didn't recognise - `Reason` has what it could see |

`FirstByteMs` is how long until the server started answering, and `TotalMs` is the whole fetch.
A big `FirstByteMs` means the server was slow to respond; a small `FirstByteMs` with a big
`TotalMs` means the download itself was slow. Failed fetches also get a console line:
`fetch failed <status> after <n>s, <bytes> bytes received - <reason>`.

## Build

`build.bat` builds a self-contained single-file `bin/zwrserve/zwrserve.exe` (win-x64).
The project targets plain `net8.0`, so `-r linux-x64` builds a Linux version from the same
source. Uses `MQClient.cs`, `ZOSCII.cs`, `Unsignal.cs` and `SecureDelete.cs` from `..\src`.