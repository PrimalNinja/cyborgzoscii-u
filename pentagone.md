# PENTAGONE Protocol Specification
**Pentagonal Encoding Network Through A Guaranteed Override of N Exposures**
**Author:** Julian Cassin  
**Date:** 2026-05-06

## SOFTWARE LICENSE v1.1
PENTAGONE Protocol is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

### 1. OVERVIEW

PENTAGONE is the combinatorial threshold splitting mechanism underlying the TEMPURA Protocol. It distributes an encoded payload across **5 shares** such that any **3 shares** are sufficient to reconstruct the original — with no polynomial arithmetic, no cryptographic primitives, and no mathematical complexity beyond a static lookup table.

PENTAGONE is derived from the combinatorial properties of C(5,3)=10 — all possible combinations of 5 servers taken 3 at a time. Every byte of the payload is written to exactly 3 of 5 shares according to a rotating pattern. By the pigeonhole principle, any 3 available shares will satisfy at least one complete pattern row, enabling full reconstruction.

PENTAGONE does not provide security on its own. It is designed to operate on UNSIGNAL-encoded payloads, where the security guarantee I(M;A)=0 is provided by the encoding layer. PENTAGONE provides **fault tolerance, redundancy and secret sharing of already secured payloads** — the encoded payload remains available and reconstructable even when up to 2 of 5 shares are lost, offline, or corrupted.

---

### 2. RELATIONSHIP TO OTHER PROTOCOLS

| Protocol | Role |
|----------|------|
| ZOSCII | Core encoding — ROM-based address indirection |
| UNSIGNAL | Obfuscation layer — randomised offsets, prefix/suffix noise, I(M;A)=0 |
| PENTAGONE | Distribution layer — 3-of-5 threshold split and reconstruction |
| TEMPURA | Delivery layer — fault-tolerant web application delivery using UNSIGNAL + PENTAGONE |
| SSS (Shamir's Secret Sharing) | Analogous information-theoretic threshold scheme using polynomial interpolation — PENTAGONE achieves equivalent resilience properties through combinatorial means |

PENTAGONE sits between UNSIGNAL encoding and delivery. It receives an UNSIGNAL-encoded byte stream and distributes it. It does not inspect, modify, or have knowledge of the content.

---

### 3. THE COMBINATORIAL PATTERN TABLE

PENTAGONE uses a static 10-row pattern table. Each row defines which 3 of the 5 shares receive the byte at that position. The table cycles — position 0 uses row 0, position 10 uses row 0 again, and so on.

| Row | Share 1 | Share 2 | Share 3 | Share 4 | Share 5 |
|-----|---------|---------|---------|---------|---------|
| 0 | ✓ | ✓ | ✓ | | |
| 1 | ✓ | ✓ | | ✓ | |
| 2 | ✓ | ✓ | | | ✓ |
| 3 | ✓ | | ✓ | ✓ | |
| 4 | ✓ | | ✓ | | ✓ |
| 5 | ✓ | | | ✓ | ✓ |
| 6 | | ✓ | ✓ | ✓ | |
| 7 | | ✓ | ✓ | | ✓ |
| 8 | | ✓ | | ✓ | ✓ |
| 9 | | | ✓ | ✓ | ✓ |

Each share appears in exactly 6 of the 10 rows, receiving approximately **60% of all payload bytes**.

---

### 4. PIGEONHOLE RECONSTRUCTION GUARANTEE

The guarantee that any 3 shares reconstruct the full payload follows directly from the pigeonhole principle applied to C(5,3):

- There are C(5,3) = 10 possible combinations of 3 shares from 5
- The pattern table contains exactly these 10 combinations
- Every byte position maps to exactly one row
- Every possible combination of 3 shares covers all 10 rows across the full payload
- Therefore any 3 shares contain every byte of the payload at least once

No polynomial arithmetic. No Lagrange interpolation. No finite field mathematics. The reconstruction is a sequential read with a static lookup table — identical in complexity to the split.

---

### 5. SPLIT ALGORITHM

#### 5.1 Inputs
- `payload` — UNSIGNAL-encoded byte stream
- Output files: `payload.s1`, `payload.s2`, `payload.s3`, `payload.s4`, `payload.s5`

#### 5.2 Steps

```
for each byte at position P in payload:
    row = P mod 10
    for each share index S in pattern[row]:
        write byte to share[S]
        write position P to share[S] pointer log
```

Each share file contains:
- The subset of payload bytes assigned to that share
- A position index enabling correct byte-order reconstruction

#### 5.3 Share File Format

Each share file (`.s1` through `.s5`) contains only the payload bytes assigned to that share, written sequentially in the order they were encountered. No position metadata is stored — the join algorithm reconstructs byte order by cycling through the same static pattern table used during the split.

---

### 6. JOIN ALGORITHM

#### 6.1 Inputs
- Any 3 or more of: `payload.s1`, `payload.s2`, `payload.s3`, `payload.s4`, `payload.s5`

#### 6.2 Steps

The join first builds a read lookup table (`arrReadFrom`) mapping each of the 10 pattern rows to the index of the first available share that covers that row. This is done once before reconstruction begins.

```
for each row in pattern table:
    arrReadFrom[row] = first available share index that appears in that row

for each byte position P in output:
    row = P mod 10
    read next byte from ptrShares[arrReadFrom[row]]
    for each other present share that also holds position P:
        advance that share's file pointer (discard duplicate byte)
    write byte to output
```

Because any 3 shares cover all 10 rows by the pigeonhole guarantee, `arrReadFrom` will always resolve successfully. Duplicate copies of each byte held by other present shares are discarded by advancing their file pointers, keeping all share reads in sync.



#### 6.3 Error Handling

On join failure, any partially written output file is securely deleted before the tool exits.

---

### 7. SECURE DELETE

Both `usplit` and `ujoin` implement a two-pass secure overwrite on failure before deleting any partial output files:

- **Pass 1:** Overwrite entire file with `0xFF`
- **Pass 2:** Overwrite entire file with `0x00`
- **Delete:** File is then removed from the filesystem

This ensures no partial share or partial reconstruction remains recoverable on disk if an operation fails mid-way. Overwrite is performed in 4096-byte chunks. On split failure, all partially written share files (`.s1` through `.s5`) are securely deleted. On join failure, the partial output file is securely deleted.

---

### 8. SHARE PROPERTIES

| Property | Value |
|----------|-------|
| Bytes per share | ~60% of payload (6 of every 10 bytes) |
| Minimum shares to reconstruct | 3 |
| Maximum shares offline/lost | 2 |
| Maximum shares corrupted (detectable) | Outside PENTAGONE scope — handled at delivery layer |
| Share knowledge of payload | Zero — share is a subset of UNSIGNAL noise |
| Share knowledge of other shares | Zero |
| Share knowledge of total share count | Zero |

---

### 9. COMPARISON WITH SHAMIR'S SECRET SHARING

PENTAGONE shares fundamental information-theoretic properties with Shamir's Secret Sharing (SSS) while achieving them through combinatorial rather than polynomial means.

| Property | Shamir's Secret Sharing | PENTAGONE |
|----------|------------------------|-----------|
| Threshold scheme | K of N | 3 of 5 |
| Mathematical basis | Polynomial interpolation (Lagrange) | Combinatorial pattern table (pigeonhole) |
| Arithmetic required | Finite field operations | None — sequential read/write only |
| Share size | Equal to secret | ~60% of payload |
| Information-theoretic | Yes (perfect secrecy of shares) | Yes (shares are UNSIGNAL noise) |
| Implementation complexity | Non-trivial | Minimal — static table lookup |
| Auditability | Requires mathematical verification | Visually auditable — table is human-readable |
| 8-bit hardware compatible | No | Yes |

The key distinction: SSS achieves information-theoretic security through polynomial mathematics applied to the secret itself. PENTAGONE achieves fault tolerance through combinatorial distribution applied to an already information-theoretically secure UNSIGNAL payload. The security guarantee originates in UNSIGNAL — PENTAGONE adds resilience without adding mathematical complexity.

---

### 10. TOOLING

| Tool | Platform | Function |
|------|----------|----------|
| `usplit` | Windows / Linux (console) | Split encoded payload into 5 shares |
| `ujoin` | Windows / Linux (console) | Reconstruct payload from 3+ shares |
| `uverify` | Windows / Linux (console) | Verify reconstruction via comparison against the original, UNSIGNAL decode, or ZOSCII decode (`-z` flag) |

`usplit` and `ujoin` take two parameters — input filename and output filename — with `.s1` through `.s5` appended automatically by `usplit`. No ROM is required by PENTAGONE tooling — ROM handling is the responsibility of the UNSIGNAL layer.

---

### 11. CORE LOGIC (MINIMAL)

The complete split pattern in C:

```c
// C(5,3) = 10 combinations — static pattern table
static const uint8_t arrPattern[10][3] =
{
    {0, 1, 2},  // Row 0: s1 s2 s3
    {0, 1, 3},  // Row 1: s1 s2 s4
    {0, 1, 4},  // Row 2: s1 s2 s5
    {0, 2, 3},  // Row 3: s1 s3 s4
    {0, 2, 4},  // Row 4: s1 s3 s5
    {0, 3, 4},  // Row 5: s1 s4 s5
    {1, 2, 3},  // Row 6: s2 s3 s4
    {1, 2, 4},  // Row 7: s2 s3 s5
    {1, 3, 4},  // Row 8: s2 s4 s5
    {2, 3, 4}   // Row 9: s3 s4 s5
};

// Split: for each byte at position P, write to 3 shares per pattern row
intRow = (int)(lngPos % PATTERN_LEN);
for (intJ = 0; intJ < 3; intJ++)
{
    // write byte to ptrShares[arrPattern[intRow][intJ]]
}
```

**Note:** ZOSCII core logic remains under MIT license. PENTAGONE protocol implementation is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1.

---

### 12. CONCLUSION

PENTAGONE is the simplest possible threshold distribution mechanism that satisfies the 3-of-5 resilience requirement — no mathematics beyond a lookup table, no cryptographic dependencies, no configuration. Combined with UNSIGNAL encoding, it delivers fault-tolerant, information-theoretically secure payload distribution suitable for deployment on any hardware from enterprise servers to 8-bit embedded systems.

| Property | Traditional Redundancy (RAID/Replication) | PENTAGONE |
|----------|------------------------------------------|-----------|
| Fault tolerance | Yes | Yes |
| Security of individual shares | None — share is readable | Zero — share is UNSIGNAL noise |
| Mathematical complexity | None | None |
| Minimum shares to reconstruct | Varies | 3 of 5 (fixed) |
| Share server requires crypto capability | N/A | No |
| Auditability | Simple | Simple — static table |

#### 12.1 Why 3 of 5?

The 3-of-5 threshold is a practical sweet spot:

- **Odd total** — no tie scenarios in majority logic
- **Threshold above 50%** — 3/5 = 60%, the reconstructing group always represents a genuine majority of shares
- **Meaningful fault tolerance** — tolerating loss of 2 of 5 servers (40%) covers realistic failure and compromise scenarios
- **Operationally manageable** — 5 servers stays within the span of control of a single organisation without becoming a coordination problem

The next valid candidate, 4 of 7, works mathematically (C(7,4)=35 pattern rows) and tolerates one additional failure. However, 7 parties means 7 ROM distribution relationships, 7 trust boundaries to maintain, and a pattern table 3.5x larger to audit — operational overhead that grows faster than the marginal security gain for most deployments.

---

**Implementation Note:** Any threshold variant — C(7,4), C(9,5), or any other C(x,y) — can be achieved simply by replacing the pattern table with the corresponding combinatorial combinations and updating the share filename checks accordingly. The core split and join logic remains unchanged.