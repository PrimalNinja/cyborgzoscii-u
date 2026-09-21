# Block Reinforced Index Confidentiality Scheme Specification — 16-bit / byte

**Author:** Julian Cassin
**Version:** 2.0 (16-bit / byte configuration)

## SOFTWARE LICENSE v1.2

BRICS is released under UNINTELLIGENCE SOFTWARE LICENSE v1.2

---

### 1. OVERVIEW

BRICS is a ZOSCII implementation that neutralizes pattern recognition and heuristic analysis. Randomized offsets and variable padding ensure that identical inputs never produce identical outputs, even on the same ROM.

This document is the **16-bit / byte** configuration. It is one of four parallel UNSIGNAL/BRICS specifications that differ only by address size and block policy; the 8-bit / nibble configuration (the micro variant) is identical in structure with the size tokens swapped.

---

### 1a. ADDRESS SIZE

This configuration uses an **16-bit address**. An **address** is one emitted reference: **2 bytes** in the encoded output, indexing one **byte** in the ROM.

A composed value — the ROM start pointer, a block offset, a block size — is built from **two addresses** (a low part and a high part) whose dereferenced values combine into **one** value the width of the address: `ROM[low]` is the low byte, `ROM[high]` the high byte, together one 16-bit value. The composition shift is **8** (the byte width in bits):

```
value = (ROM[high] << 8) | ROM[low]
```

The pointer is the same width as the address size — two bytes compose one address-sized value, not a wider one.

Keep two units distinct: the **addressable unit** (a byte — what one address indexes in the ROM) and the **address** (the emitted reference — 2 bytes in the encoded output). Header sizes and the data payload are counted in bytes of encoded output.

**Why this address size.** 16-bit addressing spans a byte-scale ROM up to 64KB — the standard full-ROM configuration. 32-bit addressing is not recommended: for most files the top bytes of every address are zero the overwhelming majority of the time (≈90% high-order zero bytes — storage waste and a conspicuous regular structure in the output), and it loses the odd/even one-byte misalignment benefit, which depends on the granularity being fine enough that a one-byte shift is meaningful.

---

### 2. FILE HEADER (FH) — 6 Addresses (2 bytes each = 12 bytes)

| Header | Size | Dereference |
|--------|------|-------------|
| **FH1** | 2 bytes | `ROM[FH1]` = low byte of Block 1 offset |
| **FH2** | 2 bytes | `ROM[FH2]` = high byte of Block 1 offset |
| **FH3** | 2 bytes | `ROM[FH3]` = low byte of Block 1 size |
| **FH4** | 2 bytes | `ROM[FH4]` = high byte of Block 1 size |
| **FH5** | 2 bytes | `ROM[FH5]` = prefix length |
| **FH6** | 2 bytes | `ROM[FH6]` = suffix length |

**Total file header size:** 6 × 2 = 12 bytes

FH5 and FH6 each point to a ROM position whose value is 10 or greater; that value is the length of a random prefix / suffix byte array. FH1/FH2 and FH3/FH4 can point to odd or even positions, shifting all addresses by one byte from the start or end without an attacker knowing which. The prefix hides where the message starts; the suffix hides where it ends (defeating a backwards walk).

---

### 3. FILE STRUCTURE

```
[FH][Prefix][Block 1][Block 2]...[Suffix]

FH:       [FH1][FH2][FH3][FH4][FH5][FH6]  (12 bytes)
Prefix:   random bytes                     (length from ROM[FH5])
Payload:  [Block 1][Block 2]...            (variable)
Suffix:   random bytes                     (length from ROM[FH6])
```

**BRICS uses multiple blocks (N ≥ 1), each with its own offset and size.** The same address decodes to a different value in different blocks, because each block re-offsets the ROM. The file header points at block 1; each block's header points at the next. The chain terminates when the payload region (bounded by the known suffix length) is consumed — the final block's "NEXT block" values are unused noise. A single-block BRICS file (N = 1) is exactly an UNSIGNAL file.

---

### 4. BLOCK HEADER (BH) — 4 Addresses (2 bytes each = 8 bytes)

| Header | Size | Dereference |
|--------|------|-------------|
| **BH1** | 2 bytes | `ROM[BH1]` = low byte of NEXT block offset |
| **BH2** | 2 bytes | `ROM[BH2]` = high byte of NEXT block offset |
| **BH3** | 2 bytes | `ROM[BH3]` = low byte of NEXT block size |
| **BH4** | 2 bytes | `ROM[BH4]` = high byte of NEXT block size |

**Total block header size:** 4 × 2 = 8 bytes

---

### 5. BLOCK STRUCTURE

```
Block N:
  [BH1][BH2][BH3][BH4][Data bytes...]

  BH1 = 2 bytes ADDRESS → ROM[BH1] = low byte of NEXT block offset
  BH2 = 2 bytes ADDRESS → ROM[BH2] = high byte of NEXT block offset
  BH3 = 2 bytes ADDRESS → ROM[BH3] = low byte of NEXT block size
  BH4 = 2 bytes ADDRESS → ROM[BH4] = high byte of NEXT block size

  next_offset = (ROM[BH2] << 8) | ROM[BH1]
  next_size   = (ROM[BH4] << 8) | ROM[BH3]

  Data = bytes encoded using CURRENT block's offset
       → One dereference: ROM[offset + address]
```

---

### 6. DECODING

```
block1_offset = (ROM[FH2] << 8) | ROM[FH1]
block1_size   = (ROM[FH4] << 8) | ROM[FH3]
prefix_len    = ROM[FH5]
suffix_len    = ROM[FH6]

Skip prefix_len bytes

// Block 1
Read BH1, BH2, BH3, BH4
next_offset = (ROM[BH2] << 8) | ROM[BH1]
next_size   = (ROM[BH4] << 8) | ROM[BH3]
Decode block1_size bytes using ROM[block1_offset + address]

// Subsequent blocks (BRICS N>1; UNSIGNAL has none)
Read BH1, BH2, BH3, BH4
next_offset = (ROM[BH2] << 8) | ROM[BH1]
next_size   = (ROM[BH4] << 8) | ROM[BH3]
Decode previous_next_size bytes using ROM[previous_next_offset + address]

// Continue until the payload region is consumed
Skip suffix_len bytes
```

The decoder does not look for an end-of-chain marker. The payload region is bounded up front by the known `suffix_len`; the decoder walks blocks only within that region and stops when it is consumed. The final block's BH is decoded but its "NEXT block" values are never used — they are indistinguishable from the surrounding noise, which is intended.

---

### 7. CORE LOGIC (MINIMAL)

The block/offset/padding machinery above wraps plain ZOSCII. The underlying encode/decode is address-size-agnostic and is the same operation in every configuration:

```javascript
// Minimal ZOSCII (Internal)
encode = (r,m) => [...m].map(c => [...r].map((b,i)=>b==c?i:[]).flat().sort(()=>Math.random()-.5)[0]);
decode = (r,a) => a.map(a => r[a]).join('');
```

For each message value, collect every ROM index holding that value and pick one at random; decode is `ROM[address]`. ZOSCII itself remains under the MIT license; the BRICS layer is under UNINTELLIGENCE SOFTWARE LICENSE v1.2.

---

### 8. SUGGESTED IMPLEMENTATION: BLOCK SIZE WHEN ENCODING

*Encoder-only guidance, not a wire-format requirement. The decoder reads each size from the headers and follows it; only the encoder applies this. Implementations are free to choose their own range.*

**Recommendation:** choose each block's size as a random element count — a reasonable default is **between 128 and 512** — drawn **independently per block**. The range is not fixed by the scheme.

Per-block offsets mean the same address decodes to a different value in different blocks; this is the primary strengthening and holds even for fixed-size blocks. Randomising the size per block replaces a single known structural template with a per-block combinatorial constraint: a candidate ROM must produce a length chain in which every block independently lands in the chosen range and the chain fits the payload. There is nothing to brute-force in the usual sense — every ROM decodes to plausible output — so the only cheap attack is *discarding* made-up ROMs whose decoded block-length chain does not fit the file; random per-block sizing forces each such decision to satisfy an independent length constraint at every block.

**No CSPRNG is required.** The size randomness selects block sizes only; it carries no security. An attacker without the ROM cannot read any block size, offset, or content regardless of how predictably the sizes were chosen. The confidentiality is in ROM secrecy, exactly as everywhere else in ZOSCII — never in a random number generator.

---

### 9. SUGGESTED IMPLEMENTATION: REGIME 1 — EFFECTIVE ROM SIZE AND SLIDING WINDOW

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

The offset derived from the FH pointer, and the ZOSCII addressing, operate against the effective size and sliding window — not the raw ROM size. Encoder and decoder must apply the same scheme or files will not round-trip.

**Rule 1 is a bonus:** because the sliding window shifts where addressing effectively starts each session, a given address does not resolve to the same underlying ROM byte from one encode to the next. Completely different messages can produce the exact same address stream on the same ROM — an observer seeing overlapping address sequences learns nothing about whether the underlying messages are related. This holds even on ROMs far below 64KB. The cost is proportional: `effective_size` and `sliding_window` are drawn from the same pool (`y + w = x`), so a ROM cannot have both a full 64KB of address space and the maximum window count at once.

---

### 10. SUGGESTED IMPLEMENTATION: REGIME 2 — ADDRESS-SPACE WRAP-AROUND

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

### 11. SUMMARY

| Element | In file | Dereference | Result |
|---------|---------|-------------|--------|
| **FH1** | 2 bytes addr | `ROM[FH1]` | low byte of Block 1 offset |
| **FH2** | 2 bytes addr | `ROM[FH2]` | high byte of Block 1 offset |
| **FH3** | 2 bytes addr | `ROM[FH3]` | low byte of Block 1 size |
| **FH4** | 2 bytes addr | `ROM[FH4]` | high byte of Block 1 size |
| **FH5** | 2 bytes addr | `ROM[FH5]` | prefix length |
| **FH6** | 2 bytes addr | `ROM[FH6]` | suffix length |
| **BH1** | 2 bytes addr | `ROM[BH1]` | low byte of NEXT block offset |
| **BH2** | 2 bytes addr | `ROM[BH2]` | high byte of NEXT block offset |
| **BH3** | 2 bytes addr | `ROM[BH3]` | low byte of NEXT block size |
| **BH4** | 2 bytes addr | `ROM[BH4]` | high byte of NEXT block size |
| **Data** | 2 bytes addr | `ROM[offset + address]` | actual byte |

---

**Block Reinforced Index Confidentiality Scheme: Block-level ambiguity. File-level security. Indirection everywhere.**