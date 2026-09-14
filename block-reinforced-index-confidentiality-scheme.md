# Block Reinforced Index Confidentiality Scheme Specification

**Author:** Julian Cassin
**Date:** 2026-09-09
**Version:** 2.0 (DRAFT)

## SOFTWARE LICENSE v1.1

Block Reinforced Index Confidentiality Scheme is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

### 1. OVERVIEW

Block Reinforced Index Confidentiality is an evolution of the UNSIGNAL Protocol that introduces **per-block offsets and sizes**, enabling the same address to decode to different values in different blocks of the same file.

---

### 2. FILE HEADER (FH) - 6 Addresses × 2 Bytes = 12 Bytes

| Header | Size | What you do |
|--------|------|-------------|
| **FH1** | 2 bytes | `ROM[FH1]` = low byte of Block 1 offset |
| **FH2** | 2 bytes | `ROM[FH2]` = high byte of Block 1 offset |
| **FH3** | 2 bytes | `ROM[FH3]` = low byte of Block 1 size |
| **FH4** | 2 bytes | `ROM[FH4]` = high byte of Block 1 size |
| **FH5** | 2 bytes | `ROM[FH5]` = prefix length |
| **FH6** | 2 bytes | `ROM[FH6]` = suffix length |

**Total file header size:** 6 × 2 = 12 bytes

---

### 3. FILE STRUCTURE

```
[FH][Prefix][Block 1][Block 2][Block 3]...[Suffix]

FH:       [FH1][FH2][FH3][FH4][FH5][FH6]  (12 bytes)
Prefix:   random bytes                     (length from ROM[FH5])
Payload:  [Block 1][Block 2]...        (variable)
Suffix:   random bytes                     (length from ROM[FH6])
```

---

### 4. BLOCK HEADER (BH) - 4 Addresses × 2 Bytes = 8 Bytes

| Header | Size | What you do |
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

  BH1 = 2-byte ADDRESS → ROM[BH1]   = low byte of NEXT block offset
  BH2 = 2-byte ADDRESS → ROM[BH2]   = high byte of NEXT block offset
  BH3 = 2-byte ADDRESS → ROM[BH3]   = low byte of NEXT block size
  BH4 = 2-byte ADDRESS → ROM[BH4]   = high byte of NEXT block size

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
prefix_len      = ROM[FH5]
suffix_len      = ROM[FH6]

Skip prefix_len bytes

// Block 1
Read BH1, BH2, BH3, BH4
next_offset = (ROM[BH2] << 8) | ROM[BH1]
next_size   = (ROM[BH4] << 8) | ROM[BH3]
Decode block1_size bytes using ROM[block1_offset + address]

// Block 2
Read BH1, BH2, BH3, BH4
next_offset = (ROM[BH2] << 8) | ROM[BH1]
next_size   = (ROM[BH4] << 8) | ROM[BH3]
Decode previous_next_size bytes using ROM[previous_next_offset + address]

// Continue until end of payload
Skip suffix_len bytes
```

---

### 7. SUMMARY

| Element | In file | Dereference | Result |
|---------|---------|-------------|--------|
| **FH1** | 2-byte addr | `ROM[FH1]` | low byte of Block 1 offset |
| **FH2** | 2-byte addr | `ROM[FH2]` | high byte of Block 1 offset |
| **FH3** | 2-byte addr | `ROM[FH3]` | low byte of Block 1 size |
| **FH4** | 2-byte addr | `ROM[FH4]` | high byte of Block 1 size |
| **FH5** | 2-byte addr | `ROM[FH5]` | prefix length |
| **FH6** | 2-byte addr | `ROM[FH6]` | suffix length |
| **BH1** | 2-byte addr | `ROM[BH1]` | low byte of NEXT block offset |
| **BH2** | 2-byte addr | `ROM[BH2]` | high byte of NEXT block offset |
| **BH3** | 2-byte addr | `ROM[BH3]` | low byte of NEXT block size |
| **BH4** | 2-byte addr | `ROM[BH4]` | high byte of NEXT block size |
| **Data** | 2-byte addr | `ROM[offset + address]` | actual byte |

---

### 8. Worked Example — 100-element message in 3 blocks

A 100-element message split into three blocks (40 + 35 + 25 elements). This example uses the 16-bit-address / byte configuration, so each element on the wire is a 2-byte address, each BH is 4 addresses (8 bytes), and the FH is 6 addresses (12 bytes). **Sizes are in address space (element counts) and are data-only — they exclude the block's own 8-byte BH.**

Chosen for this example: `prefix_len = 13`, `suffix_len = 21`, block sizes `40, 35, 25`.

**Byte layout:**

```
Region     Bytes                         Byte offset
--------   ---------------------------   -----------
FH         6 addr × 2         = 12        0
Prefix     random             = 13        12
Block 1  BH(8) + 40×2       = 88        25
Block 2  BH(8) + 35×2       = 78        113
Block 3  BH(8) + 25×2       = 58        191
Suffix     random             = 21        249
--------                                  -----------
FILE TOTAL                    = 270        (EOF 270)
```

**What each header carries (each value is `ROM[addr]`):**

```
FH → block 1 params + pad lengths:
  block1_offset = (ROM[FH2]<<8) | ROM[FH1]
  block1_size   = (ROM[FH4]<<8) | ROM[FH3]   = 40
  prefix_len      = ROM[FH5]                    = 13
  suffix_len      = ROM[FH6]                    = 21

Block 1's BH → block 2 params:
  block2_offset = (ROM[BH2]<<8) | ROM[BH1]
  block2_size   = (ROM[BH4]<<8) | ROM[BH3]   = 35

Block 2's BH → block 3 params:
  block3_offset = (ROM[BH2]<<8) | ROM[BH1]
  block3_size   = (ROM[BH4]<<8) | ROM[BH3]   = 25

Block 3's BH → next params point past the payload region;
  the decoder stops because the payload region (bounded by
  suffix_len from the file end) is exhausted, not because the
  chain self-terminates. Block 3's BH values are unused noise.
```

**Decode walk:**

```
payload_region = file_len(270) − FH(12) − prefix_len(13) − suffix_len(21)
               = 224 bytes           (byte offsets 25 .. 248)

Read FH → blk1 offset/size(40), prefix_len(13), suffix_len(21)
Skip 13 prefix bytes                              → at offset 25

Block 1 @ offset 25:
  read BH (8 bytes)          → blk2 offset/size(35)
  decode 40 elements (80 bytes) using ROM[blk1_offset + addr]
                                                   → at offset 113

Block 2 @ offset 113:
  read BH (8 bytes)          → blk3 offset/size(25)
  decode 35 elements (70 bytes) using ROM[blk2_offset + addr]
                                                   → at offset 191

Block 3 @ offset 191:
  read BH (8 bytes)          → (values unused)
  decode 25 elements (50 bytes) using ROM[blk3_offset + addr]
                                                   → at offset 249

offset 249 == payload end (270 − 21 suffix). Payload region
exhausted → stop. Remaining 21 bytes are the random suffix.
```

**Note on termination:** the decoder does not look for an end-of-chain marker. The payload region is bounded up front by the known `suffix_len` (offsets 25–248 here); the decoder walks blocks only within that region and stops when it is consumed. The final block's BH is decoded but its "next block" values are never used — they are indistinguishable from the surrounding noise, which is intended.

---

### 9. SUGGESTED IMPLEMENTATION: BLOCK SIZE WHEN ENCODING

*This section is encoder-only guidance, not a scheme requirement. The wire format does not care how block sizes were chosen — the decoder simply reads each size from the block headers and follows it. Only the encoder applies this; the decoder needs no knowledge of the scheme. Implementations are free to choose their own range.*

**Recommendation:** when encoding, choose each block's size as a random number of elements — a reasonable default is **between 128 and 512** — drawn **independently per block**. The range is not fixed by the scheme; pick your own.

**Why block at all (the base benefit):** per-block offsets mean the same address decodes to a different value in different blocks. This holds regardless of how sizes are chosen — even fixed-size blocks gain it. Blocking is the primary strengthening.

**Why randomise the size (the multiplier):** a documented fixed block size (e.g. always 256) makes the block boundaries fall at predictable strides. This does not leak the message — the block headers are still `ROM[addr]` values unreadable without the ROM — but it presents a single known structural template. Randomising the size **per block** replaces that one template with a per-block combinatorial constraint: a candidate ROM must produce a length chain in which *every* block independently lands in the chosen range *and* the chain fits the payload. This maximises the unpredictability of address reuse within a single message and makes structural candidate-elimination combinatorially harder, at a cost of only a few bytes of overhead variance. (There is nothing to "brute-force" in the usual sense — there is no recognisable correct decode to search toward, since every ROM decodes to plausible output. The only cheap thing an attacker can do is *discard* made-up ROMs whose decoded block-length chain does not fit the file; random per-block sizing forces each discarded-or-not decision to satisfy an independent length constraint at every block, so fewer come cheap.) Blocking is the benefit; randomising the size multiplies it.

**Encoding steps:**

1. For blocks 1..N−1, draw an independent random size in the chosen range (e.g. 128–512 elements).
2. The final block takes the remainder of the payload, so the block sizes sum exactly to the payload region.
3. Below a small floor (e.g. payloads under 256 elements), a single block is acceptable — there is nothing to gain from blocking a payload smaller than one block.

**No CSPRNG is required.** The randomness here selects block sizes only; it does not carry the security. From an attacker's point of view decoding is still blind — they lack the ROM, so they cannot read any block size, any offset, or any content regardless of how predictably or unpredictably the encoder chose the sizes. A weak or fully predictable size generator does not help an attacker who cannot read the block headers in the first place. The size randomness buys *structural unpredictability against candidate-elimination*, not confidentiality of the message — the message confidentiality comes from the ROM being secret, exactly as everywhere else. As with the rest of ZOSCII, the security is in ROM secrecy, never in the quality of a random number generator.

---

**Block Reinforced Index Confidentiality Scheme: Block-level ambiguity. File-level security. Indirection everywhere.**