# ZOSCII MQ Chat

**Author:** Julian Cassin  
**Date:** 2026-04-03
**Version 1.0 RC 6**

**Warning:** This is a proof of concept and may still contain buffer overflow vulnerabilities. Do not use in production without further hardening.

Proof of concept chat tools for secure messaging via ZOSCII MQ.

Messages are UNSIGNAL-encoded using active ROMs before transmission. The server only ever sees opaque binary blobs. No metadata, no sender identity, no message structure is imposed — the content is whatever you put in.

## SOFTWARE LICENSE v1.1

ZOSCII MQ Chat is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

## Setup

Copy all six executables into your `NeptuneData` folder alongside your active ROMs (`active.rom`, `active2.rom`, `active3.rom`). The tools will automatically detect and use whichever ROMs are present (1, 2, or 3).

The tools create two subdirectories on first use:

- `queues/` — queue configuration files and locally cached messages
- `states/` — fetch position tracking per queue

## Tools

### ntsqueue — Queue Management

```
ntsqueue                              List all queues visible with current ROMs
ntsqueue add <name> <server> [guid]   Add a queue
ntsqueue remove <name>                Remove a queue
ntsqueue -h                           Show help
```

Adding a queue registers a human-readable name, a server URL, and a GUID (auto-generated if omitted). The queue configuration is stored as an UNSIGNAL-encoded file — only visible when the same ROMs are active.

To share a queue with someone, give them the GUID and server URL. They add it under whatever name they choose:

```
ntsqueue add myname http://server.example/index.php AB12CD34-EF56-7890-ABCD-EF1234567890
```

### ntssend — Send a Message

```
ntssend <queue> <message>        Send a text message
ntssend <queue> -f <filepath>    Send a file
```

The message is UNSIGNAL-encoded through the active ROMs and published to the server. The server receives only the encoded binary.

### ntsfetch — Fetch Messages

```
ntsfetch <queue>
```

Fetches all new messages from the server into the local cache. Tracks position per queue so subsequent runs only download new messages. If interrupted, resumes from the last successfully saved message.

### ntslist — List Messages

```
ntslist <queue>                  List newest messages (10 per page)
ntslist <queue> <from>           List from a point onwards
ntslist <queue> <from> <to>      List a range
```

Lists locally cached messages, newest first. Each message shows the filename, size, and a decoded text preview (up to 80 characters) if the content is readable with the current ROMs.

The `from` and `to` parameters are filename prefixes. Since filenames begin with `YYYYMMDDHHNNSS`, any date/time prefix works as a filter:

```
ntslist myqueue 20260403              Everything from April 3rd
ntslist myqueue 20260403 20260404     Just April 3rd to 4th
```

Press Enter to page through results. Ctrl+C to stop.

### ntsread — Read a Message

```
ntsread <queue> <messageid>
```

Reads and decodes a specific message from the local cache. The `.bin` extension is optional. Text content is displayed directly. Binary content shows a hex dump of the first 64 bytes.

### ntscheck — Check for New Messages

```
ntscheck                         Check all queues
ntscheck <queue>                 Check a specific queue
```

Queries the server to see if new messages are available without downloading them or updating local state. Reports "new messages", "up to date", or "ERROR" per queue.

## Build

Requires Visual Studio 2022 (or compatible MSVC toolchain).

```
"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
cl /O2 /MT ntsqueue.c /link /SUBSYSTEM:CONSOLE
cl /O2 /MT ntssend.c /link /SUBSYSTEM:CONSOLE
cl /O2 /MT ntsfetch.c /link /SUBSYSTEM:CONSOLE
cl /O2 /MT ntslist.c /link /SUBSYSTEM:CONSOLE
cl /O2 /MT ntsread.c /link /SUBSYSTEM:CONSOLE
cl /O2 /MT ntscheck.c /link /SUBSYSTEM:CONSOLE
```

Source files required: `defines.h`, `unsignal.c`, `utils.c`, plus the six tool `.c` files.

## Notes

- Messages have no imposed structure. No subject, no sender, no timestamp embedded. The server-assigned filename provides chronological ordering.
- Swapping active ROMs changes which queues and messages are visible. Queue configurations encoded with different ROMs are silently hidden.
- The server never sees plaintext. All encoding and decoding happens locally.
- These tools are a proof of concept. The production implementation will be in the NTS C# applications.
