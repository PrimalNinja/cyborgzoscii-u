# zwrserve - ZOSCII Web Radio Serve

Console exe that streams MP3 messages from a ZOSCII MQ queue to ICY (Icecast/SHOUTcast)
listeners. Works with VLC, Winamp, foobar2000 and a browser `<audio>` element.

## Usage

```
zwrserve -i <port> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-s] [-w] [-p <seconds>] [-e <mp3file>]
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

```
zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom
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
- Ctrl+C, or closing the console window, saves every session's pointer and offset.

## Build

`build.bat` builds a self-contained single-file `bin/zwrserve/zwrserve.exe` (win-x64).
The project targets plain `net8.0`, so `-r linux-x64` builds a Linux version from the same
source. Uses `MQClient.cs`, `ZOSCII.cs`, `Unsignal.cs` and `SecureDelete.cs` from `..\src`.
