# Hardfile Specification

**Author:** Cyborg Unicorn Pty Ltd
**Version:** 1.6 (nuget 1.0.8)
**Status:** implemented in `src/Hardfile.cs`, tested by `schemetest/`

## SOFTWARE LICENSE v1.2

Hardfile is released under UNINTELLIGENCE SOFTWARE LICENSE v1.2

---

## 1. PURPOSE

A hardfile is a store with no visible files. Everything a user puts in it (photos, messages, documents, an
application's working data) lives inside a small set of files that are noise from the first byte to the last:
no names, no sizes, no dates, no count, no structure, not even whether the store has been used. Reading
anything requires two things: the **hardfile image** (which makes the structure readable) and the
**data images** (which make the contents readable). Holding one without the other gives nothing.

Every structure and every stored file carries a ZOSCII rolling hash, so tampering or corruption is detected
without any key, at three levels: the structure alone, the structure and every file, or one file. Deleting
overwrites with noise and the space is reused like ordinary memory. Nothing is compacted in place. Copies of
the index and of the data can be kept on other devices and are kept in step by copying bytes, never by
repeating work.

---

## 2. THE SET

One store is a set of files sharing a base name:

| File | Name | Role |
|------|------|------|
| Data | `<base>.hfd` | noise plus encoded blobs, in append order; carries no structure |
| Index | `<base>.hfi` | fixed-size records, one per node, indirected through the hardfile image |
| Mirror | `<base>.hfm` | a byte-for-byte copy of the index, written after it |
| Scratch | `<base>.hft`, `<base>.hfti` | a growth-only set for intermediates; secure-deleted on close |

**Replicas.** Any number of additional data and index paths may be attached (typically a folder on another
device holding the same base name). They are written in lockstep with the primaries. The scratch set is never
replicated.

**Backup unit.** `.hfd` + `.hfi` (+ `.hfm`). Any one of index or mirror suffices to open the store if the other
is damaged; the data file is authoritative for content. Every file carries the set's GUID in its envelope
(section 4a), so files from different sets cannot be combined by mistake.

---

## 3. IMAGES

Two independent kinds of key material:

| Image | Protects | Supplied by |
|-------|----------|-------------|
| Hardfile image | the index (links, offsets, lengths) and the root's meta | the application; in Cyborg NTS, published per contact as an encoded file |
| Data images / scheme | every node's meta and content | the contact's scheme (or a ROM chain), as for any other encoded data |

The hardfile image is a ZOSCII ROM. Only its first 64 KB is addressed (16-bit addresses). It must contain
every byte value 0–255 at least once, which any random or media-derived ROM does.

The data images are whatever the `IHardfileCodec` wraps: an UNSIGNAL chain over 1..3 ROMs
(`UnsignalChainCodec`), a scheme run by `SchemeRunner` (`SchemeCodec`), or nothing (`PassthroughCodec`) for
content that was already encoded elsewhere and need not be encoded again.

**Capacity.** Every ZOSCII-family layer writes two bytes per byte, so a one-layer codec gives half the data
file as usable capacity, two layers a quarter, and so on; the micro encodings are the same ratio, BRICS a
little more with its block headers. A pass-through store holds its contents 1:1, less the envelope and the
unit padding. Encoding inside the store is recommended; pass-through is for files that are already encoded.

---

## 4. INDIRECTION

Every number in the index is written as addresses, never as a value. For each byte of the number, a random
position in the hardfile image whose content is that byte is chosen and its 16-bit address is written,
low byte first:

```
value byte b  ->  address a such that image[a] == b, a < 65536   (2 bytes on disk)
```

Reading is `image[a]`. A number of n bytes occupies 2n bytes. The same value is written differently every
time, and without the image a record is indistinguishable from the noise around it.

---

## 4a. ENVELOPE

Every file of the set (data, index, mirror, replicas) is wrapped the same way:

```
[PREFIX LEN 4][SUFFIX LEN 4]  prefix noise  ...content...  noise  [SUFFIX: block + noise]
```

| Part | Size | Meaning |
|------|------|---------|
| PREFIX LEN | 4 bytes (2 indirected) | length of the random prefix, 8..2048 |
| SUFFIX LEN | 4 bytes (2 indirected) | length of the random suffix region, 48..4096 |
| prefix | PREFIX LEN | noise; content starts after it |
| block | 60 bytes (30 indirected) at `length - SUFFIX LEN` | the set GUID (16 bytes), the allocation unit (2 bytes, in multiples of 256), the last-written time (8 bytes, unix seconds) and a rolling hash over all three (4 bytes) |

The block is the openability check: under the right image it decodes to a GUID whose hash matches; under
any other image it does not, and the file is not touched. The last-written time is the true one and lives only where the image can read it; the file-system times
are randomised (section 10). `LastWritten` exposes it.

**When the block is written.** At create, at growth, at repair, and once at close. Not on every change,
deliberately: a block rewritten at a fixed position on every write is a beacon to anyone diffing snapshots of
the file, giving away the suffix length (as per-write rewrites of record slots would give away the prefix
and the record size); indirection re-randomises values, never positions. Planned: decoy writes (random
free-space extents re-noised and random dead records re-randomised alongside every real write) so that the
real change hides among many; the block can then be stamped per change, and moved to a new suffix length
each time. Every file of a set carries the same GUID, so
`Open` also refuses a data file and an index from different sets. The GUID is minted at `Create`; `CopyTo`
mints a new one, since a copy is a new set.

On growth the file is extended with noise, the old suffix region becomes noise (and, in the data file,
free space), a new random suffix length is drawn and a fresh block is written at the new end, and the
header's SUFFIX LEN is rewritten. Prefix and suffix lengths are per file and random, so the content never
sits at a predictable offset.

---

## 5. INDEX RECORD

Fixed 84 bytes. Record number r is at byte offset `PREFIX LEN + r * 84` in the index file; the usable record
count is `(length - PREFIX LEN - SUFFIX LEN) / 84`.

| Field | Bytes on disk | Value width | Meaning |
|-------|---------------|-------------|---------|
| NEXT | 8 | 32-bit | record number of the next sibling, 0 = none |
| CHILD | 8 | 32-bit | record number of the first child, 0 = none (a node with a child is a folder) |
| DATA | 16 | 64-bit | byte offset of this node's blob in the data file |
| META | 8 | 32-bit | length in bytes of the encoded meta block |
| SIZE | 16 | 64-bit | length in bytes of the encoded content block |
| BLOB | 8 | 32-bit | rolling hash over the blob as stored: the META + SIZE bytes at DATA |
| CHAIN | 8 | 32-bit | rolling hash over this node's children's record numbers, in chain order, 4 bytes each; 0 with no children |
| FLAG | 4 | 16-bit | 1 = live, 0 = dead, 2 = marked out |
| HASH | 8 | 32-bit | rolling hash over the plain bytes of the eight fields above (4+4+8+4+8+4+4+2 = 38 bytes) |

All hashes are the ZOSCII rolling hash (4-pass XOR chain, no table, no key). No other checksum is used
anywhere in the store.

Record 0 is the root, so a link value of 0 always means "none". The root is a folder with an empty name.

**Record states.**

| FLAG | State | Listed | Readable | Blob replicated | Blob repaired | Space |
|------|-------|--------|----------|-----------------|---------------|-------|
| 1 | live | yes | yes, when its blob hash passes | yes | yes | held |
| 0 | dead | no | no | record only | record only | free, reused |
| 2 | marked out | yes, as such | no | never | never | held, never reused |

**Dead record.** FLAG = 0; every other field holds a fresh random value and HASH is valid over them, so a
dead record is a well-formed record that reveals nothing about the node it was (not its size, not where its
blob sat, not its links) and is never zero. Left in place when a node is deleted and reused by the next node
created; skipped by every walk. Range checks on NEXT / CHILD / DATA apply only to live records.

**Marked-out record.** FLAG = 2, every other field as it was. The model is a bad-sector map: the blob failed
its hash in every copy, so it is routed around, its space is held so nothing writes over the remains, it is
never copied to a replica, `Repair` leaves it alone, `CopyTo` leaves it behind, and it stays until the user
deletes it or restores it. `Repair` marks a record out when no copy of its blob passes; `MarkOut` does it by
hand; `Restore` returns it to live, after which it reads again only if a passing copy exists (a replica that
was offline, say). `ExportRaw` writes the held bytes out exactly as stored for salvage elsewhere, and `ExportMarkedOut` does it for
all of them at once. The file type is `.hfseg`, a **hardfile segment**: one node's encoded meta followed by
its encoded content, nothing else, so it can be tried against a replica, another decoder or a partial decode.

**Unreadable entries.** A record whose meta cannot be decoded (marked out, bad with no good copy, or the wrong
data images) is still listed, by record number as `#<record>`, flagged unreadable, and is addressable by that
name for `MarkOut`, `Restore`, `ExportRaw` and `Delete`. With the wrong data images every file lists this way:
present, unreadable, the behaviour of encoded files in a folder.

**Capacity.** 32-bit record numbers: up to 4,294,967,295 nodes. 64-bit offsets and sizes: data file and
single blobs up to 2^63 bytes.

**Validity on read.** A record is accepted only if HASH matches and NEXT / CHILD are below the record count
implied by the index length, and DATA + META + SIZE does not exceed the data file length. A record failing in
the primary is read from the mirror, then from each index replica, in order.

**End of index.** Records are appended in order and never removed, so the index in use is the prefix of
records that read validly (dead ones included); the first record that does not is the end. Beyond it is noise.

---

## 6. DATA LAYOUT

The data file has no headers. A node's blob is:

```
[DATA]                 encoded meta block, META bytes
[DATA + META]          encoded content block, SIZE bytes (0 for folders)
```

Everything between the prefix and the suffix not covered by a live record is free space and holds noise:
the gaps left by deleted nodes and the tail. The root's meta block is the first blob, at `PREFIX LEN`.

**Reading a blob.** The META + SIZE bytes at DATA are read and their rolling hash compared with BLOB; a copy
whose hash does not match is never decoded. The first copy (primary, then replicas) that matches is used.

**Meta block (plaintext, before encoding):** UTF-8 text, five fields separated by LF:

```
kind        "D" folder or "F" file
created     unix seconds
modified    unix seconds
length      plaintext content length as recorded by the writer
name        the node's name (may contain anything except LF)
```

**Content block:** the file's bytes, encoded by the codec. Nothing is added or wrapped.

The root's meta is encoded with the **hardfile image itself** (UNSIGNAL), so that `Open` can verify the root
with the hardfile image alone. Every other meta and every content block is encoded with the codec.

---

## 6a. FREE SPACE

Space is managed like ordinary memory, in **units**. The unit is a per-store power of two from 256 bytes to
64 KB, fixed at create (default 1 KB) and carried in the envelope. Every blob occupies whole units: the
record keeps the exact lengths, the allocation is the length rounded up. This bounds the free list to
`file size / unit` extents at worst, keeps every hole usable by something, and makes coalescing cheap.
Pick the unit for the contents: 1 KB for messages and documents, 8 KB for a photo or video vault.

At open, the live blobs are read from the index, each rounded up to the unit, and the free list is their
complement over the content region: every gap and the tail, sorted by offset, adjacent extents joined.

- **Allocate**: a new blob takes the first free extent it fits (first fit); the extent shrinks or vanishes.
  When nothing fits and growth is allowed, the data file is extended by multiples of y and the extension
  becomes free. When nothing fits and growth is 0, the write fails with "hardfile full".
- **Free**: a deleted blob's bytes are overwritten with noise and its extent returns to the list, joined
  with any adjacent free extent on either side.
- **Records**: a deleted node's record is flagged dead with random values in every other field, and its
  number kept on a free-record list; the next node created takes it. The index grows only when no dead
  record is available.

The free list is never written to disk; it is rebuilt from the index on every open (one sequential read of
the index, 80 bytes per node), so there is no allocation structure to corrupt or to leak and nothing to go
stale after a crash. The index is the used list. If open time ever matters for very large stores, a bitmap
(one bit per unit) can be cached in the index suffix region, hashed, and distrusted whenever it disagrees
with the index; that is an addition, not a format change.

---

## 7. OPERATIONS

| Operation | Effect |
|-----------|--------|
| Create(paths, x, y, image[, unit]) | writes x bytes of noise to the data file and an index of max(64 KB, 1% of x) rounded to whole records; writes the root (meta under the image, record 0); growth y |
| Open(paths, y, image) | opens all files, catches up any shorter mirror / replica from the primary, reads the envelopes (the first data copy whose block decodes gives the set GUID; the index must carry the same), verifies the root's record, blob hash and meta; null if the envelope or the root does not read under this image; rebuilds the free lists |
| List(folder, codec) | the folder's children: each child's blob hash checked, meta decoded, contents untouched |
| MakeFolder(path, codec) | creates missing folders along the path |
| Put(path, bytes, codec) | encodes meta and content, allocates from free space, writes the blob, writes the record with BLOB and CHAIN, links it as the parent's last child and updates the parent's CHAIN; replaces an existing file of the same name (old one deleted first) |
| Get(path, codec) | the content block decoded, from the first copy whose blob hash matches |
| Delete(path, codec) | unlinks the node (and its subtree), updates the parent's CHAIN, overwrites their blobs with noise, rewrites their records as dead (flag 0, random fields), returns their space and records to the free lists |
| CopyTo(newPaths, y) | a fresh set, sized to the live bytes, holding every live node with blobs copied verbatim after their hashes are checked; marked-out nodes are left behind; needs only the hardfile image; the source is never written |
| Export(folder, outFolder, codec) | decodes the subtree to plain files; existing output files are skipped; a blob failing its hash is skipped and counted |
| Import(inFolder, folder, codec) | puts a folder tree into the store; existing store files are skipped |
| Verify() | **structure**: every file's envelope (header, GUID, hash), every record's HASH in every copy, every folder's CHAIN against its children, replica lengths, orphans; marked-out records counted |
| Verify(true) | **structure + files**: the above, plus every live blob's BLOB hash in every copy that holds it (marked-out blobs are not checked) |
| VerifyFile(path, codec) | **one file**: its record and its blob hash pass in at least one copy |
| Repair() | every record failing HASH rewritten from a copy that passes (any direction); every CHAIN recomputed; every live blob failing BLOB rewritten from a copy that passes, in any direction, or marked out when no copy passes; replicas made byte-identical to the primary in the noise regions, held regions excepted; every file's envelope rewritten |
| MarkOut / Restore(path, codec) | set a file's flag to marked out / live |
| ExportRaw(path, codec) | the held bytes of a file as stored, for salvage |
| ExportMarkedOut(folder) | every marked-out node's held bytes as `<record>.hfseg` in the folder; existing files skipped |

---

## 8. GROWTH

Two parameters at create time:

- **x** initial data size (raised to hold the envelope and the root if smaller). Written as noise immediately.
- **y** growth. When an append does not fit, the data file is extended by multiples of y, all noise, and the
  index by multiples of max(64 KB, 1% of y). With y = 0 the set is fixed and `Put` fails with "hardfile full"
  (data) or "index full" (index).

Because growth happens in large noise-filled steps and never shrinks, the file sizes carry no information
about the amount stored. A fixed-size set (a USB stick filled to capacity with y = 0) is indistinguishable
before and after use.

---

## 9. REPLICAS, MIRROR AND REPAIR

The index group is `[primary, mirror, replicas…]`, the data group `[primary, replicas…]`.

**Writes.** A write goes to the primary first. Only when the primary write succeeded are the identical bytes
copied to the mirror and to every replica. The operation (encoding, hashing, allocation) happens once; the
copies receive bytes. A copy that cannot be written at the time is simply behind, which append-only turns
into "shorter": `Open` appends the primary's bytes from the copy's length to the primary's length. A copy
that is longer is truncated.

**Which copy is right** is decided by the rolling hashes, never by position and never by vote:

| Copies of | Decided by |
|-----------|------------|
| an index record | HASH: the first copy whose record hash matches |
| a blob | BLOB: the first copy whose blob hash matches the record |
| a sibling chain | CHAIN: recomputed from the children and compared with the parent's record |

A matching hash is correct to 1 in 4 billion whatever the other copies say, so there is nothing for a
consensus to add; a copy with no matching hash is not used.

`Repair` is symmetric: for each record and each blob, the first copy that passes is the source for every copy
that does not, so a corrupt primary is repaired from a replica as readily as the reverse. Chain hashes are
recomputed from the children. Afterwards the noise regions of every replica are made byte-identical to the
primary, so a repaired set verifies clean.

An unreachable replica path (device not present) is skipped at open and does not fail the open.

---

## 10. SECURITY PROPERTIES

- Without the hardfile image: the envelope is noise, so nothing can even say whether a file is a hardfile; the index is noise; no record can be located; the data file is noise.
- With the wrong image: the envelope's hash fails and nothing opens, so nothing is written over a store that belongs to another image.
- With the hardfile image only: the tree shape is visible (record count, links, blob sizes); every name and
  every content block is still noise.
- With the data images only: nothing can be located.
- Every header value is re-randomised on each write, so two writes of the same value never produce the same
  bytes, and a record can be copied between stores by value but not matched by bytes.
- File sizes, growth steps and the noise fill reveal neither content volume nor use.
- Every record, every blob and every sibling chain is covered by a rolling hash, so any change to any byte of
  structure or content is detected on read and by `Verify`, at the level chosen: structure, structure and
  files, or one file. The hash needs no key, so verification never needs the data images.
- When a set is closed, every file of it gets a random created / modified / accessed time within the past
  five years, each file its own, so the file times say nothing about when the store was used
  (`RandomiseTimestamps` can also be called while mounted). Limit: NTFS keeps an MFT change time that user
  mode cannot set; it shows that attributes were touched, not when content was written. exFAT and FAT (USB
  sticks) have no such field.
- Delete is overwrite; there is no journal and no on-disk free list. Reused space held only noise. A dead
  record is random apart from its flag, so even the holder of the image learns only that a node was deleted,
  never what it was.
- The scratch set keeps intermediates of multi-layer work off the host filesystem and is secure-deleted.
- Corruption is contained to a record or a blob; the mirror and replicas restore records; content integrity is
  the codec's (a scheme's rolling hash / CRC32 inside the layers).

---

## 11. USE IN CYBORG NTS

- **Saturn** owns a filestore (`C:\<Tier>Data\filestore\`) with an image from its own ROM folder, a Filestore
  tile for browse / add / export / import / verify / repair / copy, and replica folders including removable
  media sized to capacity with y = 0.
- **Chat** and **TimeRunner** each open their own filestore after the normal ROM login, using an image Saturn
  published to the contact (`filestore-chat.img.sig`, `filestore-timerunner.img.sig`, encoded under the publish
  ROMs) and the contact's scheme as codec. Their existing folder layouts (`contacts/`, `queues/<guid>/`,
  `weeks/<week>/`) map onto `Put / Get / List` paths unchanged.
- The per-file encode / decode feature and the Explorer integration are separate and unchanged.

---

## 12. LIMITS AND NON-GOALS

- Free space is reused first-fit; a store can fragment, but with fixed-size noise filling fragmentation costs
  nothing but the holes a large blob cannot use. `CopyTo` repacks.
- One root and one GUID per set. A second ROM set gets its own set (its own image and scheme via publishing),
  not a second root in the same files. Opening with the wrong image is refused; there is no override.
- No concurrency: one opener at a time (`FileShare.None`).
- No transactions across nodes; a torn write damages at most the node being written, which the mirror and
  `Repair` cover for the index and the codec's integrity check reveals for content.
- Names are matched exactly (case-sensitive, UTF-8); a folder is walked linearly to find a name.

---

**Hardfile: no files, no structure, no sizes, no dates. Two keys or nothing, and a rolling hash on everything.**
