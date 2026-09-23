# UNSIGNAL Protocol Specification — 16-bit / byte

**Author:** Julian Cassin
**Version:** 1.2 (16-bit / byte configuration)

## SOFTWARE LICENSE v1.2

UNSIGNAL is released under UNINTELLIGENCE SOFTWARE LICENSE v1.2

---

### 1. OVERVIEW

UNSIGNAL is a ZOSCII implementation that neutralizes pattern recognition and heuristic analysis. A randomized ROM offset and variable prefix/suffix padding ensure that identical inputs never produce identical outputs, even on the same ROM.

This document is the **16-bit / byte** configuration. Its 8-bit / nibble counterpart (the micro variant) is identical in structure with the size tokens swapped.

---

### 1a. ADDRESS SIZE

This configuration uses an **16-bit address**. An **address** is one emitted reference: **2 bytes** in the encoded output, indexing one **byte** in the ROM.

A composed value — the ROM start pointer — is built from **two addresses** (a low part and a high part) whose dereferenced values combine into **one** value the width of the address: `ROM[low]` is the low byte, `ROM[high]` the high byte, together one 16-bit value. The composition shift is **8** (the byte width in bits):

```
value = (ROM[high] << 8) | ROM[low]
```

The pointer is the same width as the address size — two bytes compose one address-sized value, not a wider one.

Keep two units distinct: the **addressable unit** (a byte — what one address indexes in the ROM) and the **address** (the emitted reference — 2 bytes in the encoded output). Header sizes and the data payload are counted in bytes of encoded output.

**Why this address size.** 16-bit addressing spans a byte-scale ROM up to 64KB — the standard full-ROM configuration. 32-bit addressing is not recommended: for most files the top bytes of every address are zero the overwhelming majority of the time (≈90% high-order zero bytes — storage waste and a conspicuous regular structure in the output), and it loses the odd/even one-byte misalignment benefit, which depends on the granularity being fine enough that a one-byte shift is meaningful.

---
### 2. HEADER — 4 Addresses (2 bytes each = 8 bytes)

Every UNSIGNAL file begins with a 4-address header. Addresses are stored low part first (little-endian), consistent at every width.

| Header | Size | Dereference |
|--------|------|-------------|
| **H1** | 2 bytes | `ROM[H1]` = low byte of ROM start pointer |
| **H2** | 2 bytes | `ROM[H2]` = high byte of ROM start pointer |
| **H3** | 2 bytes | `ROM[H3]` = prefix length |
| **H4** | 2 bytes | `ROM[H4]` = suffix length |

**Total header size:** 4 × 2 = 8 bytes

H1/H2 compose the ROM start pointer — the logical "start" of the ROM for this session — as `(ROM[H2] << 8) | ROM[H1]`. H3 and H4 each point to a ROM position whose value is 10 or greater; that value is the length of a random prefix / suffix byte array. H1/H2 can point to an odd or even position, shifting all addresses by one byte from the start without an attacker knowing which. The prefix hides where the message starts; the suffix hides where it ends (defeating a backwards walk).

---

### 3. FILE STRUCTURE

```
[H1][H2][H3][H4][Prefix][Encoded stream][Suffix]

Header:   [H1][H2][H3][H4]                (8 bytes)
Prefix:   random bytes                     (length from ROM[H3])
Payload:  encoded address stream           (variable)
Suffix:   random bytes                     (length from ROM[H4])
```

UNSIGNAL is a single flat stream — one ROM offset for the whole message, no blocks and no per-block re-offsetting. (Per-block offsets are what BRICS adds on top of this structure.)

---

### 4. ENCODING STEPS

1. **Select randoms:** generate random values for the ROM start pointer (low/high) and the prefix/suffix lengths.
2. **Map header:** use ZOSCII logic to find addresses H1–H4 whose dereferenced ROM values equal those randoms.
3. **Generate noise:** build a prefix and a suffix of random bytes of the chosen lengths.
4. **Encode data:** ZOSCII-encode the message, offsetting the search space by the ROM pointer from H1/H2.
5. **Assemble:** `[H1][H2][H3][H4] + [Prefix] + [Encoded stream] + [Suffix]`.

---

### 5. DECODING STEPS

```
rom_pointer = (ROM[H2] << 8) | ROM[H1]
prefix_len  = ROM[H3]
suffix_len  = ROM[H4]

Skip prefix_len bytes after the header
Terminate suffix_len bytes before end of file
Decode the remaining address stream: ROM[rom_pointer + address]
```

The prefix and suffix are stripped by their known lengths; everything between is the encoded stream, decoded against the ROM at the session pointer.

---

### 6. CORE LOGIC (MINIMAL)

The offset/padding machinery above wraps plain ZOSCII. The underlying encode/decode is address-size-agnostic and is the same operation in every configuration:

```javascript
// Minimal ZOSCII (Internal)
encode = (r,m) => [...m].map(c => [...r].map((b,i)=>b==c?i:[]).flat().sort(()=>Math.random()-.5)[0]);
decode = (r,a) => a.map(a => r[a]).join('');
```

For each message value, collect every ROM index holding that value and pick one at random; decode is `ROM[address]`. ZOSCII itself remains under the MIT license; the UNSIGNAL layer is under UNINTELLIGENCE SOFTWARE LICENSE v1.2.

---

### 7. SUGGESTED IMPLEMENTATION: REGIME 1 — EFFECTIVE ROM SIZE AND SLIDING WINDOW

*Default offset regime (`-r1`). One reasonable way to derive an effective ROM size and sliding window from the actual ROM size, so the 16-bit pointer behaves sensibly across small and large ROMs. Suggested, not normative — encoder and decoder must agree. All percentage math uses integer division, truncated — never floating point with rounding.*

**Variables:**

```
x  = actual ROM size in bytes
xp = (x * 2) / 100                 // 2% of x, integer division
t1 = 65536 + (65536 * 2) / 100     // = 66846 — fixed Rule-1 trigger (2% of 64KB, not of x)
```

**Rule 1 — small ROM (`x < t1`):**

```
effective_size (y) = x - xp
sliding_window (w) = xp
```

**Rule 2 — medium ROM (`t1 <= x < 131072`):**

```
effective_size (y) = 65536
sliding_window (w) = 131072 - x - xp
```

**Rule 3 — large ROM (`x >= 131072`):**

```
effective_size (y) = 65536
sliding_window (w) = 65536
```

The offset derived from the header pointer, and the ZOSCII addressing, operate against the effective size and sliding window — not the raw ROM size. Encoder and decoder must apply the same scheme or files will not round-trip.

**Rule 1 is a bonus:** because the sliding window shifts where addressing effectively starts each session, a given address does not resolve to the same underlying ROM byte from one encode to the next. Completely different messages can produce the exact same address stream on the same ROM — an observer seeing overlapping address sequences learns nothing about whether the underlying messages are related. This holds even on ROMs far below 64KB. The cost is proportional: `effective_size` and `sliding_window` are drawn from the same pool (`y + w = x`), so a ROM cannot have both a full 64KB of address space and the maximum window count at once.

---

### 8. SUGGESTED IMPLEMENTATION: REGIME 2 — ADDRESS-SPACE WRAP-AROUND

*Second offset regime (`-r2`), backward compatible with Regime 1: `hl mod window_size` is a no-op on any address a Regime 1 encoder produced. Suggested, not normative; encoder and decoder must agree.*

**When to use Regime 2.** Where the cyclic repetition it creates does not leak. The clearest case is BRICS applied per block: the full address space is used within each block, and the blocks of a message have no correlation to each other, so per-block wrapping exposes no cross-block pattern. Choose Regime 2 where the wrapped address space is confined to a unit (a block, a segment, a session) with no exploitable relationship to any other unit. Where one flat address space spans correlated content, prefer Regime 1.

**What it does.** Regime 1 leaves the address space beyond the window unused when the window is smaller than the full address space (64KB). Regime 2 wraps the address space around the window — addresses use the **full** address space, and on read the decoder folds each back in:

```
hl = hl mod window_size
ld a, (hl)
```

So a smaller window still gives full addressing — addresses past the window wrap back and re-use it from the start. At the limit, a **1-byte ROM backs the full address space**: that single value has 65536 instances (every address mods to 0). Any ROM size, however small, can back the full address space this way — provided it contains the values the message needs.

**Encoder side.** The encoder wraps too, expressed as cycling: it walks the window recording positions for each value, and at the window end cycles back to the start until the instance list spans the full address space. Encoder cycling and decoder `hl mod window_size` are the same wrap from both ends.

**The repeating pattern is still secret.** A small ROM wrapped to fill the address space is the small ROM repeated cyclically. This is not a leak: the pattern *is* the ROM, and the ROM is secret. Without the ROM the repetition is unreadable; with it, an observer would simply decode.

**Floor.** The ROM must still contain every value the message uses — wrapping supplies many instances of the values present, it cannot conjure an absent value. A 1-byte ROM is the degenerate case: it can only encode a message of that single byte value.

---

**UNSIGNAL Protocol: Randomized offset. Variable padding. Identical inputs, never identical outputs.**