# ZOSCII / UNSIGNAL Attacker

A browser test bench that builds a ZOSCII or UNSIGNAL file and then attacks it using only the file bytes. Anyone can add their own attack as a `.js` file (see Custom Attack API below).

## What the attacker does

### The threat model

The attacker gets the file bytes and nothing else. It does not get:

- the ROM (private key)
- the window offset, prefix length or suffix length (all read from the ROM)
- the message length or where the message starts
- whether BRAINLESS was applied
- the plaintext

Holding the ROM is access, not an attack. The ROM baseline exists only to show that.

### How it analyses an UNSIGNAL file

UNSIGNAL files have an 8-byte header, a random prefix, the message addresses, and a random suffix. The prefix and suffix lengths come from the ROM, so the attacker cannot find where the message starts or ends.

The attacker handles this by sweeping every possible reading of the file:

1. **BRAINLESS on or off.** BRAINLESS is a keyless XOR over the whole file. It can always be reversed without a key, but nothing in the file says whether it was applied. The attacker keeps both the RAW bytes and the PEELED (un-XORed) bytes.
2. **Byte alignment.** The message starts at byte 8 + prefixLen. The 256 possible prefix lengths come down to 2 ways of pairing bytes into 16-bit addresses: EVEN (from byte 8) and ODD (from byte 9). Every prefix length is just a starting point inside one of those two.

That gives 4 readings for UNSIGNAL (RAW/EVEN, RAW/ODD, PEELED/EVEN, PEELED/ODD) and 2 for ZOSCII (RAW, PEELED; ZOSCII has no header or padding). Every attack runs on all of them.

Every run also prints the possible message length. The attacker only knows the total: file size = 8 header + prefix + 2 x message length + suffix, with prefix and suffix each 10 to 255. So the message could be anywhere from 0 characters up to the maximum that fits.

### What can leak

An address is a position in the ROM. The same address always reads the same ROM byte, so two equal addresses in the message mean two equal plaintext bytes. That is the only structure available to an attacker. It never gives a byte's value, only which positions hold the same byte.

On a large random ROM with a short message, equal addresses are rare and look like chance. With a long message in one window, common letters (space, e, t) get reused often enough that equal addresses pile up well above chance. The tool reports this honestly when it happens.

### Zero clues

- The encoder log (prefix length, suffix length, where the message starts) goes to its own box in the encoder panel, never to the attack console.
- The demo answer (real message length, real reading, real offset) only prints when **Show demo answer** is ticked. It is off by default.
- If an attack does not use an input you filled in (crib, attack file, positions), it prints a NOTE saying those inputs were NOT tested.

## The built-in attacks

### Repeat-structure (ciphertext only)

Counts equal addresses in each of the readings and compares each count with chance. For n random 16-bit values, about n x (n - 1) / 2 / 65536 equal pairs are expected.

- **No reading leaks:** every reading is at chance level. The attacker cannot pick the real reading, alignment or offset.
- **One reading stands out:** its pair count is far above chance, so it picks itself out as the real reading (revealing BRAINLESS state and alignment). Byte values are still unknown.
- **More than one stands out:** the leak does not pick a single reading.

### Crib drag (known or guessed plaintext)

Takes the crib box text as exact bytes (case and line breaks kept, trailing line breaks dropped) and slides it along every reading, one position at a time. At each position:

- equal addresses under **different** crib bytes cannot happen, so that position is thrown out
- equal addresses under the **same** crib byte count as support (k)

Verdicts:

- **DEFINITELY ISN'T:** every position in every reading was thrown out. The text is not in the file.
- **MIGHT BE - strong / weak / no support:** at least one position survives. Support compares the best k with chance, scaled by the number of positions tested, so a lucky match somewhere among thousands of positions is not counted as support.
- **MIGHT BE - DEGENERATE crib:** a crib of one repeated symbol has nothing that can rule a position out, so no test was performed.

It never says "IS". Without the ROM the file can prove a guess wrong, never right.

If a position survives, the tool maps the crib's addresses to its letters and shows every other place in the file where those addresses appear. It reports only recoveries outside the crib, so the crib's own letters are not counted.

### Dictionary

Runs the same pattern test for every word in the loaded attack file (one word per line). Each word is tested as written, lowercase, Capitalised and UPPER, because the test uses exact bytes and each case form is a separate test. It lists words with support and counts words that only passed by chance.

### Known-plaintext (KPA)

A demo, not an attack. It grants the attacker everything it could not work out on its own: BRAINLESS state, prefix length, message length, the crib's real position, and a correct crib. It then shows how far one correct crib spreads through reused addresses. If the crib is not in the message, it says so and stops. Bytes the encoder dropped (not present in a small ROM) are accounted for.

With **Attacker HAS the ROM** ticked, it also checks the recovered bytes against the ROM.

### BRAINLESS peel

Shows the PEELED reading. The peel always works: on a file that was never XORed it just produces different noise, so nothing says which reading is real. As a cipher layer BRAINLESS adds nothing, but because it cannot be detected it doubles the number of readings the attacker has to consider.

### ROM baseline

Needs **Attacker HAS the ROM** ticked. Decodes the message by direct ROM lookup. This is key compromise, not an attack on the scheme. If some bytes were never encoded (not in a small ROM), it says so.

## Custom Attack API

A custom attack is a `.js` file that defines one function:

```js
function attack(api) {
  // your attack here
  return 'optional result';
}
```

Load it with **CUSTOM ATTACK**, set ATTACK TYPE to **Custom attack**, then click **RUN ATTACK**.

### The `api` object

| Field | What it is |
|---|---|
| `api.mode` | `'unsignal'` or `'zoscii'` |
| `api.wire` | Full file bytes as a number array (a copy) |
| `api.structures` | Array of candidate readings: 4 in UNSIGNAL mode, 2 in ZOSCII mode. Each is `{name, reading, parity, addrs}` |
| `api.readAddrs(bytes, start)` | Pairs bytes into little-endian 16-bit addresses from byte `start` |
| `api.peel(bytes)` | Keyless BRAINLESS inverse |
| `api.crib` | Crib box text as typed, line breaks kept |
| `api.cribBytes` | Crib as UTF-8 bytes, encoded the same way the encoder encodes the message |
| `api.dictionary` | Loaded attack file text, or `''` if none |
| `api.allow` | Allowed positions as a number array (empty = all) |
| `api.forbid` | Forbidden positions as a number array |
| `api.log(text)` | Prints a line to the attack console |

### Fields of each `api.structures` entry

| Field | Values |
|---|---|
| `name` | `'RAW/EVEN'`, `'RAW/ODD'`, `'PEELED/EVEN'`, `'PEELED/ODD'` (ZOSCII: `'RAW/start0'`, `'PEELED/start0'`) |
| `reading` | `'RAW'` or `'PEELED'` |
| `parity` | `0` = EVEN, `1` = ODD |
| `addrs` | Address array for that reading (a copy) |

### What is NOT passed

Nothing that an attacker does not hold:

- the ROM (private key)
- prefix or suffix length
- message length or position
- BRAINLESS on/off
- the plaintext

If your attack needs any of these, it is using access, not attacking.

### Return value

Whatever `attack` returns is printed as `result: ...`. Strings print as-is, and anything else is printed as JSON. It may also return a Promise.

Errors are caught and printed as `[custom] error: ...`.

### Honesty note

The attack runs inside the same page as the encoder, so it could read the encoder panel from the page directly. An honest attack uses `api` only.

### Example

```js
function attack(api) {
  for (const s of api.structures) api.log(s.name + ': ' + s.addrs.length + ' addresses');
  return 'example finished';
}
```