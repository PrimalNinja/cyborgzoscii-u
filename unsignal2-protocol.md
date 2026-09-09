# UNSIGNAL2 Protocol Specification

**Author:** Julian Cassin
**Date:** 2026-09-09
**Version:** 2.0 (DRAFT)

## SOFTWARE LICENSE v1.1

UNSIGNAL2 Protocol is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

### 1. OVERVIEW

UNSIGNAL2 is an evolution of the UNSIGNAL Protocol that introduces **per-segment offsets and sizes**, enabling the same address to decode to different values in different segments of the same file.

---

### 2. FILE HEADER (FH) - 6 Addresses × 2 Bytes = 12 Bytes

| Header | Size | What you do |
|--------|------|-------------|
| **FH1** | 2 bytes | `ROM[FH1]` = low byte of Segment 1 offset |
| **FH2** | 2 bytes | `ROM[FH2]` = high byte of Segment 1 offset |
| **FH3** | 2 bytes | `ROM[FH3]` = low byte of Segment 1 size |
| **FH4** | 2 bytes | `ROM[FH4]` = high byte of Segment 1 size |
| **FH5** | 2 bytes | `ROM[FH5]` = prefix length |
| **FH6** | 2 bytes | `ROM[FH6]` = suffix length |

**Total file header size:** 6 × 2 = 12 bytes

---

### 3. FILE STRUCTURE

```
[FH][Prefix][Segment 1][Segment 2][Segment 3]...[Suffix]

FH:       [FH1][FH2][FH3][FH4][FH5][FH6]  (12 bytes)
Prefix:   random bytes                     (length from ROM[FH5])
Payload:  [Segment 1][Segment 2]...        (variable)
Suffix:   random bytes                     (length from ROM[FH6])
```

---

### 4. SEGMENT HEADER (SH) - 4 Addresses × 2 Bytes = 8 Bytes

| Header | Size | What you do |
|--------|------|-------------|
| **SH1** | 2 bytes | `ROM[SH1]` = low byte of NEXT segment offset |
| **SH2** | 2 bytes | `ROM[SH2]` = high byte of NEXT segment offset |
| **SH3** | 2 bytes | `ROM[SH3]` = low byte of NEXT segment size |
| **SH4** | 2 bytes | `ROM[SH4]` = high byte of NEXT segment size |

**Total segment header size:** 4 × 2 = 8 bytes

---

### 5. SEGMENT STRUCTURE

```
Segment N:
  [SH1][SH2][SH3][SH4][Data bytes...]

  SH1 = 2-byte ADDRESS → ROM[SH1]   = low byte of NEXT segment offset
  SH2 = 2-byte ADDRESS → ROM[SH2]   = high byte of NEXT segment offset
  SH3 = 2-byte ADDRESS → ROM[SH3]   = low byte of NEXT segment size
  SH4 = 2-byte ADDRESS → ROM[SH4]   = high byte of NEXT segment size

  next_offset = (ROM[SH2] << 8) | ROM[SH1]
  next_size   = (ROM[SH4] << 8) | ROM[SH3]

  Data = bytes encoded using CURRENT segment's offset
       → One dereference: ROM[offset + address]
```

---

### 6. DECODING

```
segment1_offset = (ROM[FH2] << 8) | ROM[FH1]
segment1_size   = (ROM[FH4] << 8) | ROM[FH3]
prefix_len      = ROM[FH5]
suffix_len      = ROM[FH6]

Skip prefix_len bytes

// Segment 1
Read SH1, SH2, SH3, SH4
next_offset = (ROM[SH2] << 8) | ROM[SH1]
next_size   = (ROM[SH4] << 8) | ROM[SH3]
Decode segment1_size bytes using ROM[segment1_offset + address]

// Segment 2
Read SH1, SH2, SH3, SH4
next_offset = (ROM[SH2] << 8) | ROM[SH1]
next_size   = (ROM[SH4] << 8) | ROM[SH3]
Decode previous_next_size bytes using ROM[previous_next_offset + address]

// Continue until end of payload
Skip suffix_len bytes
```

---

### 7. SUMMARY

| Element | In file | Dereference | Result |
|---------|---------|-------------|--------|
| **FH1** | 2-byte addr | `ROM[FH1]` | low byte of Segment 1 offset |
| **FH2** | 2-byte addr | `ROM[FH2]` | high byte of Segment 1 offset |
| **FH3** | 2-byte addr | `ROM[FH3]` | low byte of Segment 1 size |
| **FH4** | 2-byte addr | `ROM[FH4]` | high byte of Segment 1 size |
| **FH5** | 2-byte addr | `ROM[FH5]` | prefix length |
| **FH6** | 2-byte addr | `ROM[FH6]` | suffix length |
| **SH1** | 2-byte addr | `ROM[SH1]` | low byte of NEXT segment offset |
| **SH2** | 2-byte addr | `ROM[SH2]` | high byte of NEXT segment offset |
| **SH3** | 2-byte addr | `ROM[SH3]` | low byte of NEXT segment size |
| **SH4** | 2-byte addr | `ROM[SH4]` | high byte of NEXT segment size |
| **Data** | 2-byte addr | `ROM[offset + address]` | actual byte |

---

### 8. Worked Example — 100-element message in 3 segments

A 100-element message split into three segments (40 + 35 + 25 elements). This example uses the 16-bit-address / byte configuration, so each element on the wire is a 2-byte address, each SH is 4 addresses (8 bytes), and the FH is 6 addresses (12 bytes). **Sizes are in address space (element counts) and are data-only — they exclude the segment's own 8-byte SH.**

Chosen for this example: `prefix_len = 13`, `suffix_len = 21`, segment sizes `40, 35, 25`.

**Byte layout:**

```
Region     Bytes                         Byte offset
--------   ---------------------------   -----------
FH         6 addr × 2         = 12        0
Prefix     random             = 13        12
Segment 1  SH(8) + 40×2       = 88        25
Segment 2  SH(8) + 35×2       = 78        113
Segment 3  SH(8) + 25×2       = 58        191
Suffix     random             = 21        249
--------                                  -----------
FILE TOTAL                    = 270        (EOF 270)
```

**What each header carries (each value is `ROM[addr]`):**

```
FH → segment 1 params + pad lengths:
  segment1_offset = (ROM[FH2]<<8) | ROM[FH1]
  segment1_size   = (ROM[FH4]<<8) | ROM[FH3]   = 40
  prefix_len      = ROM[FH5]                    = 13
  suffix_len      = ROM[FH6]                    = 21

Segment 1's SH → segment 2 params:
  segment2_offset = (ROM[SH2]<<8) | ROM[SH1]
  segment2_size   = (ROM[SH4]<<8) | ROM[SH3]   = 35

Segment 2's SH → segment 3 params:
  segment3_offset = (ROM[SH2]<<8) | ROM[SH1]
  segment3_size   = (ROM[SH4]<<8) | ROM[SH3]   = 25

Segment 3's SH → next params point past the payload region;
  the decoder stops because the payload region (bounded by
  suffix_len from the file end) is exhausted, not because the
  chain self-terminates. Segment 3's SH values are unused noise.
```

**Decode walk:**

```
payload_region = file_len(270) − FH(12) − prefix_len(13) − suffix_len(21)
               = 224 bytes           (byte offsets 25 .. 248)

Read FH → seg1 offset/size(40), prefix_len(13), suffix_len(21)
Skip 13 prefix bytes                              → at offset 25

Segment 1 @ offset 25:
  read SH (8 bytes)          → seg2 offset/size(35)
  decode 40 elements (80 bytes) using ROM[seg1_offset + addr]
                                                   → at offset 113

Segment 2 @ offset 113:
  read SH (8 bytes)          → seg3 offset/size(25)
  decode 35 elements (70 bytes) using ROM[seg2_offset + addr]
                                                   → at offset 191

Segment 3 @ offset 191:
  read SH (8 bytes)          → (values unused)
  decode 25 elements (50 bytes) using ROM[seg3_offset + addr]
                                                   → at offset 249

offset 249 == payload end (270 − 21 suffix). Payload region
exhausted → stop. Remaining 21 bytes are the random suffix.
```

**Note on termination:** the decoder does not look for an end-of-chain marker. The payload region is bounded up front by the known `suffix_len` (offsets 25–248 here); the decoder walks segments only within that region and stops when it is consumed. The final segment's SH is decoded but its "next segment" values are never used — they are indistinguishable from the surrounding noise, which is intended.

---

### 9. SUGGESTED IMPLEMENTATION: SEGMENT SIZE WHEN ENCODING

*This section is encoder-only guidance, not a protocol requirement. The wire format does not care how segment sizes were chosen — the decoder simply reads each size from the segment headers and follows it. Only the encoder applies this; the decoder needs no knowledge of the scheme. Implementations are free to choose their own range.*

**Recommendation:** when encoding, choose each segment's size as a random number of elements — a reasonable default is **between 128 and 512** — drawn **independently per segment**. The range is not fixed by the protocol; pick your own.

**Why segment at all (the base benefit):** per-segment offsets mean the same address decodes to a different value in different segments. This holds regardless of how sizes are chosen — even fixed-size segments gain it. Segmenting is the primary strengthening.

**Why randomise the size (the multiplier):** a documented fixed segment size (e.g. always 256) makes the segment boundaries fall at predictable strides. This does not leak the message — the segment headers are still `ROM[addr]` values unreadable without the ROM — but it presents a single known structural template. Randomising the size **per segment** replaces that one template with a per-segment combinatorial constraint: a candidate ROM must produce a length chain in which *every* segment independently lands in the chosen range *and* the chain fits the payload. This maximises the unpredictability of address reuse within a single message and makes structural candidate-elimination combinatorially harder, at a cost of only a few bytes of overhead variance. (There is nothing to "brute-force" in the usual sense — there is no recognisable correct decode to search toward, since every ROM decodes to plausible output. The only cheap thing an attacker can do is *discard* made-up ROMs whose decoded segment-length chain does not fit the file; random per-segment sizing forces each discarded-or-not decision to satisfy an independent length constraint at every segment, so fewer come cheap.) Segmenting is the benefit; randomising the size multiplies it.

**Encoding steps:**

1. For segments 1..N−1, draw an independent random size in the chosen range (e.g. 128–512 elements).
2. The final segment takes the remainder of the payload, so the segment sizes sum exactly to the payload region.
3. Below a small floor (e.g. payloads under 256 elements), a single segment is acceptable — there is nothing to gain from segmenting a payload smaller than one segment.

**No CSPRNG is required.** The randomness here selects segment sizes only; it does not carry the security. From an attacker's point of view decoding is still blind — they lack the ROM, so they cannot read any segment size, any offset, or any content regardless of how predictably or unpredictably the encoder chose the sizes. A weak or fully predictable size generator does not help an attacker who cannot read the segment headers in the first place. The size randomness buys *structural unpredictability against candidate-elimination*, not confidentiality of the message — the message confidentiality comes from the ROM being secret, exactly as everywhere else. As with the rest of ZOSCII, the security is in ROM secrecy, never in the quality of a random number generator.

---

**UNSIGNAL2: Segment-level ambiguity. File-level security. Indirection everywhere.**