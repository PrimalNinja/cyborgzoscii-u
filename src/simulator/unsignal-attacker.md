# ZOSCII / UNSIGNAL Attacker

A browser test bench that builds a ZOSCII, UNSIGNAL or BRICS file (or one of the 8-bit micro variants) and then attacks it using only the file bytes. Anyone can add their own attack as a `.js` file (see Custom Attack API below).

## Layout

Two panels: **1 · ROM & ENCODE** (builds the target file) and **2 · ATTACK** (attacks it). On screens 1600px wide or more they sit side by side; otherwise they stack.

## Protocols

| Option | Addresses | Window | Framing |
|---|---|---|---|
| ZOSCII | 16-bit per byte | fixed, first 64KB of the ROM | none (no header, no padding) |
| UNSIGNAL | 16-bit per byte | sliding (Regime 1) | 8-byte header, prefix, message, suffix |
| BRICS | 16-bit per byte | per-block offsets | 12-byte header, prefix, blocks, suffix |
| microZOSCII | 8-bit per nibble | fixed | none |
| microUNSIGNAL | 8-bit per nibble | sliding | header, prefix/suffix 10..15 |
| microBRICS | 8-bit per nibble | per-block offsets, blocks 128..255 bytes (256..510 nibbles) | header, prefix/suffix 10..15 |

UNSIGNAL is the default.

Every layer, outer and inner, is built with the general encoder (`gen_encode`), the same code the creator generates, so the attacker's files always match the creator's. The attacker has no encoder of its own.

### ROM

- **GENERATE RANDOM ROM** fills both ROM slots (ROM 1 and ROM 2) with 128KB of random bytes. This runs once on load.
- Dropping a file on a ROM canvas loads it as that ROM (first 131072 bytes used). Smaller ROM = smaller pool = more leakage.
- The canvas shows the ROM as green intensity. A dashed box marks the window: green for ZOSCII (first 64KB), orange for the others (the sliding window at its offset).
- Stats show ROM size, unique byte values (of 256), window offset (or block count for BRICS, protocol name for micro) and message address count.

### UNSIGNAL (16-bit)

- **Regime 1 sliding window.** From ROM size x (integer maths): if x < 66846, effective size y = x - 2% and window range w = 2%; if x < 131072, y = 65536 and w = x - 65536; otherwise y = 65536 and w = 65536. The window offset is the ROM-derived start pointer mod (w + 1), the same as the creator.
- **Header (8 bytes):** 4 addresses into the first window. Their ROM bytes give offset low, offset high, prefix length and suffix length. Prefix and suffix lengths are 10..255.
- Then the random prefix, the message addresses (relative to the window offset), and the random suffix.

### BRICS (16-bit)

- The message is split into blocks of 128..512 bytes. Each block is encoded at its own window offset.
- **File header (12 bytes):** block 1 offset pointer (2 addresses), block 1 size (2 addresses), prefix length, suffix length.
- Then the random prefix (10..255). Each block is an 8-byte block header (next block's pointer and size; noise on the last block) followed by that block's addresses. Then the random suffix (10..255).
- Block count, block sizes and offsets are all ROM-derived. No block boundary is visible in the file.

### Micro variants (8-bit)

- Each byte is split into two nibbles (low first), and each nibble is one 8-bit address.
- The ROM is read as nibbles, 2 per byte, high nibble first (the same as `MicroZOSCII.FromBytes`). microZOSCII uses the first 256 nibbles; microUNSIGNAL and microBRICS slide a 256-nibble window.
- BRICS block sizes count message bytes, so a microBRICS block of 128..255 bytes is 256..510 nibble addresses.
- If any nibble value is missing from the window the whole encode fails and nothing is encoded.

### Dropped bytes

If any byte value in the message is not in the ROM window, nothing is encoded and the encoder log says how many bytes were missing, the same as the creator. On a full random ROM this does not happen.

### Re-encode

Changing the protocol, BRAINLESS, COMPRESS, DOUBLE-ENCODE or the inner protocol re-encodes the message automatically if one was already encoded. If the protocol is changed without re-encoding, the attacker refuses to run until you re-encode.

## What the attacker does

### The threat model

The attacker gets the file bytes and nothing else. It does not get:

- the ROM (private key)
- the window offset, prefix length or suffix length (all read from the ROM)
- BRICS block count, sizes or offsets
- the message length or where the message starts
- whether BRAINLESS was applied
- whether the plaintext was compressed or double-encoded
- the plaintext

Holding the ROM is access, not an attack. The ROM baseline exists only to show that.

### How it analyses a file

The attacker sweeps every possible reading of the file:

1. **BRAINLESS on or off.** BRAINLESS is a keyless XOR over the whole file. It can always be reversed without a key, but nothing in the file says whether it was applied. The attacker keeps both the RAW bytes and the PEELED (un-XORed) bytes.
2. **Byte alignment.** In UNSIGNAL the message starts at byte 8 + prefixLen. The 246 possible prefix lengths come down to 2 ways of pairing bytes into 16-bit addresses: EVEN (from byte 8) and ODD (from byte 9). Within each there are up to 123 candidate message-start offsets. BRICS has the same random prefix (10..255) after a 12-byte header, so its data starts at byte 12 + prefixLen and reduces to the same two alignments. Reading from byte 8 or 9 gives the same pairing as from 12 or 13, with two extra header addresses at the front.

That gives 4 readings for UNSIGNAL and BRICS (RAW/EVEN, RAW/ODD, PEELED/EVEN, PEELED/ODD) and 2 for ZOSCII (RAW/start0, PEELED/start0). Every 16-bit attack runs on all of them.

Every run also prints what is known about the message length:

- **UNSIGNAL:** unknown. File size = 8 header + prefix + 2 x message length + suffix, with prefix and suffix each 10 to 255, so the message could be anywhere from 0 up to the maximum that fits.
- **BRICS:** unknown. File = 12 header + prefix + N x 8 block headers + 2 x message length + suffix, with N and every block size and offset ROM-derived.
- **ZOSCII:** file size / 2 addresses (no padding).

### 8-bit (micro) outer layer

The attack suite is built around 16-bit little-endian addresses. When the outer protocol is a micro variant, only **ROM baseline** is implemented. Any other attack type prints that it is not implemented for 8-bit layers and stops.

### Compress plaintext (encoder option)

Ticking **COMPRESS plaintext** runs the message through raw deflate (`CompressionStream('deflate-raw')`) before it is encoded. The encoder then encodes the compressed bytes, not the text.

- Raw deflate has no fixed header, unlike gzip (`1F 8B 08`) or zlib (`78 xx`), so it hands the attacker no guaranteed crib.
- The attacker is not told compression was used. Nothing in the file says so.
- Text cribs and dictionary words are tested as text, so they are tested against deflate output and will not match. That is the point of the option.
- The encoder log shows the plaintext and compressed sizes (encoder side only).
- **Show demo answer** adds the compression note to the ground-truth line. The **ROM baseline** decompresses what it recovers and prints the original text (only if recovery was exact).
- Needs a browser with `CompressionStream` / `DecompressionStream` and `'deflate-raw'`. If it is missing, the encoder logs an error and does not encode.

### Double-encode (encoder option)

Ticking **DOUBLE-ENCODE** encodes twice. Layer 1 (inner) encodes the message with **ROM 1**. The whole layer-1 file (header, padding and addresses) then becomes the plaintext of layer 2 (outer), encoded with **ROM 2** using the protocol chosen in PROTOCOL. BRAINLESS, if ticked, is applied once, to the outer file only.

- **INNER LAYER PROTOCOL** (enabled only when DOUBLE-ENCODE is ticked) picks the inner protocol: `same as outer method`, or any of the six protocols. Micro inner layers read ROM 1 as nibbles.
- If a non-"same" inner layer finds a value missing from the ROM 1 window, nothing is encoded and the log says so.
- A second ROM canvas (ROM 2) appears when it is ticked, with its own stats. Each ROM takes its own dropped key file.
- The attacker still gets only the outer file bytes and sweeps the same readings. Text cribs and dictionary words are tested against the inner file's bytes, so they will not match, as with COMPRESS.
- **Show demo answer** notes the double encoding and the inner protocol. The **ROM baseline** needs both ROMs: ROM 2 decodes the outer file to the inner file, then ROM 1 decodes that to the message.
- ROM 2 must contain every byte value, or nothing is encoded (the encoder log says so).
- `api.cribBytes` is never compressed or double-encoded.

### What can leak

An address is a position in the ROM. The same address always reads the same ROM byte, so two equal addresses in the message mean two equal plaintext bytes. That is the only structure available to an attacker. It never gives a byte's value, only which positions hold the same byte.

On a large random ROM with a short message, equal addresses are rare and look like chance. With a long message in one window, common letters (space, e, t) get reused often enough that equal addresses pile up well above chance. The tool reports this honestly when it happens.

In BRICS an equal-address pair only means "same byte" when both positions are in the same block. Cross-block matches are chance, and block boundaries are hidden, so real and chance matches cannot be told apart.

### Zero clues

- The encoder log (prefix length, suffix length, offsets, block sizes, compression sizes) goes to its own box in the encoder panel, never to the attack console.
- The demo answer only prints when **Show demo answer** is ticked. It is off by default. For UNSIGNAL/ZOSCII it gives the real message length, real reading and offset; for BRICS the block count, real reading and block offsets.
- If an attack does not use an input you filled in (crib, attack file, positions), it prints a NOTE saying those inputs were NOT tested.

## The built-in attacks

### Repeat-structure (ciphertext only)

Counts equal addresses in each reading, prints a skeleton (letters marking positions that share an address) and compares each count with chance. For n random 16-bit values, about n x (n - 1) / 2 / 65536 equal pairs are expected. A reading counts as standing out when its pair count is more than 4 x expected and the Poisson tail probability is under 0.01.

- **No reading leaks:** every reading is at chance level. The attacker cannot pick the real reading, alignment or offset.
- **One reading stands out:** its pair count is far above chance, so it picks itself out as the real reading (revealing BRAINLESS state and alignment). Byte values are still unknown.
- **More than one stands out:** the leak does not pick a single reading.

On BRICS it adds that any above-chance pairs mix real within-block and chance cross-block matches, so the flat repeat leak UNSIGNAL can show on a long message does not carry over.

### Crib drag (known or guessed plaintext)

Takes the crib box text as exact bytes (case and line breaks kept, trailing line breaks dropped) and slides it along every reading, one position at a time, honouring ALLOWED and FORBIDDEN POSITIONS. At each position:

- equal addresses under **different** crib bytes cannot happen, so that position is thrown out
- equal addresses under the **same** crib byte count as support (k)

It prints the crib's constraint profile (may-share pairs, forbidden pairs, adjacent forbidden pairs), how many placements were scanned and eliminated, and the expected chance pairs for a crib of that length.

Verdicts:

- **DEFINITELY ISN'T:** every position in every reading was thrown out, or the crib is longer than the address stream. The text is not in the file.
- **MIGHT BE - strong / weak / no support:** at least one position survives. Support takes the Poisson tail for the best k and multiplies it by the number of placements scanned: under 0.001 is strong, under 0.05 is weak, otherwise no support. So a lucky match somewhere among thousands of positions is not counted as support. It also says whether the field is too crowded to localise (survivors over 1% of placements).
- **MIGHT BE - DEGENERATE crib:** a crib of one repeated symbol has nothing that can rule a position out, so no test was performed.

It never says "IS". Without the ROM the file can prove a guess wrong, never right.

If a position survives, the tool maps the best survivor's addresses to the crib's letters and shows every other place in the file where those addresses appear. It counts only recoveries outside the crib and compares that count with chance: far above chance means likely real message bytes; at chance means likely padding or noise matching a learned address.

On BRICS it warns that the flat crib assumes one offset for the whole crib. A crib crossing a block boundary gets wrongly eliminated, so DEFINITELY ISN'T is only reliable for a crib that sits inside one block.

### Dictionary

Runs the same pattern test for every word of 2 or more characters in the loaded attack file (one word per line), honouring ALLOWED and FORBIDDEN POSITIONS. Each word is tested as written, lowercase, Capitalised and UPPER, because the test uses exact bytes and each case form is a separate test. It counts words with support (k of 1 or more), lists the top 20, and counts words that only passed by chance (k = 0).

### Known plaintext (KPA)

The known plaintext is what you type in the crib box, taken as true and somewhere in the message. Nothing else is given: no ROM, no position, no reading, no framing.

It tries every reading and position, honouring ALLOWED and FORBIDDEN POSITIONS, and keeps the placements where the plaintext fits (no equal address under two different bytes).

- **Fits nowhere:** the plaintext is not in the file as typed.
- **Exactly one placement fits:** the plaintext is located. Its learned ROM cells are applied to the rest of that reading and the bytes they reveal are shown. On BRICS this only holds inside the same block offset.
- **More than one fits:** not located. It reports how much each placement would reveal if it were the real one, but picking one is a guess, so nothing else is revealed for certain.

### BRAINLESS peel

Shows the PEELED reading. The peel always works: on a file that was never XORed it just produces different noise, so nothing says which reading is real. As a cipher layer BRAINLESS adds nothing, but because it cannot be detected it doubles the number of readings the attacker has to consider.

### ROM baseline

Needs **Attacker HAS the ROM** ticked. Decodes the file with `gen_decode` and the ROM, exactly as the intended recipient would; a double-encoded file needs both ROMs. This is key compromise, not an attack on the scheme. It works for all six protocols, including the micro variants.

## Custom Attack API

A custom attack is a `.js` file that defines one function:

```js
function attack(api) {
  // your attack here
  return 'optional result';
}
```

Load it with **CUSTOM ATTACK**, set ATTACK TYPE to **Custom attack**, then click **RUN ATTACK**. It runs in strict mode. Custom attacks are not available when the outer protocol is a micro variant.

### The `api` object

| Field | What it is |
|---|---|
| `api.mode` | `'zoscii'`, `'unsignal'` or `'brics'` |
| `api.wire` | Full file bytes as a number array (a copy) |
| `api.structures` | Array of candidate readings: 4 in UNSIGNAL and BRICS mode, 2 in ZOSCII mode. Each is `{name, reading, parity, addrs}` |
| `api.readAddrs(bytes, start)` | Pairs bytes into little-endian 16-bit addresses from byte `start` |
| `api.peel(bytes)` | Keyless BRAINLESS inverse |
| `api.crib` | Crib box text as typed, line breaks kept |
| `api.cribBytes` | Crib as UTF-8 bytes, encoded the same way the encoder encodes the message (never compressed or double-encoded) |
| `api.dictionary` | Loaded attack file text, or `''` if none |
| `api.allow` | Allowed positions as a number array (empty = all) |
| `api.forbid` | Forbidden positions as a number array |
| `api.log(text)` | Prints a line to the attack console |

### Fields of each `api.structures` entry

| Field | Values |
|---|---|
| `name` | `'RAW/EVEN'`, `'RAW/ODD'`, `'PEELED/EVEN'`, `'PEELED/ODD'` (ZOSCII: `'RAW/start0'`, `'PEELED/start0'`) |
| `reading` | `'RAW'` or `'PEELED'` |
| `parity` | `0` = EVEN (from byte 8, or byte 0 in ZOSCII), `1` = ODD (from byte 9) |
| `addrs` | Address array for that reading (a copy) |

### What is NOT passed

Nothing that an attacker does not hold:

- the ROM (private key)
- prefix or suffix length
- BRICS block count, sizes or offsets
- message length or position
- BRAINLESS on/off
- whether the plaintext was compressed or double-encoded
- the plaintext

If your attack needs any of these, it is using access, not attacking.

### Return value

Whatever `attack` returns is printed as `result: ...`. Strings print as-is, and anything else is printed as JSON. It may also return a Promise. `[custom] done.` prints when it finishes.

Compile errors print as `[custom] compile error: ...`, and runtime errors (including a file with no `attack` function) as `[custom] error: ...`.

### Honesty note

The attack runs inside the same page as the encoder, so it could read the encoder panel from the page directly. An honest attack uses `api` only.

### Example

```js
function attack(api) {
  for (const s of api.structures) api.log(s.name + ': ' + s.addrs.length + ' addresses');
  return 'example finished';
}
```