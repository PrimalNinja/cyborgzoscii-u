# superZOSCII Protocol (The Fourth Protocol)

## SOFTWARE LICENSE v1.1

superZOSCII Protocol (The Fourth Protocol) is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

**Draft Version 1**

## 1. OVERVIEW

superZOSCII Protocol (The Fourth Protocol) is a theoretical extension to the UNSIGNAL Protocol that introduces per-packet window rotation across arbitrarily large ROMs. All packet indirection metadata resides within the currently paged-in 64KB window, maintaining the self-referential security model of ZOSCII.

**Status: THEORETICAL — NOT IMPLEMENTED.** The current UNSIGNAL implementation already achieves near-perfect statistical properties (entropy >7.99, chi-square 28–85%, mean approaching ideal 127.5) and the added complexity of superZOSCII introduces implementation risk for no practical security gain. I(M;A)=0 is already achieved by UNSIGNAL.

## 2. Motivation

UNSIGNAL operates on a 128KB ROM with a single 64KB sliding window per session. The window offset is determined once by the H1/H2 header addresses and remains fixed for the entire message. This means all encoded addresses for a given session are drawn from the same 64KB pool.

superZOSCII extends this by allowing the window to re-position per packet within a much larger ROM (megabytes), with each packet carrying its own indirection metadata within the currently paged-in 64KB window.

## 3. Architecture

The ROM size is no longer limited to 128KB. superZOSCII supports ROMs up to 16MB, addressed as 256 sequential 64KB pages (page 0 through page 255). Page 0 is the initial window established by H1/H2 from the standard UNSIGNAL header.

Each packet consists of:

- A pointer to the packet size (within the current 64KB page) — the value at this address determines how many encoded message bytes follow
- A single-byte page number for the next window (within the current 64KB page)

The standard UNSIGNAL header (H1–H4) still governs the overall encoding. H1/H2 establish the initial page, while H3/H4 define the random prefix and suffix that wrap the entire packet stream — all packets sit between the prefix and suffix noise.

The critical property: all packet indirection addresses (next page number, packet size) are found within the currently active 64KB window. The ROM is effectively paged — only 64KB is visible at any time, and the indirection to the next page lives inside the current page.

### Why This Is Confusing By Design

Each packet has its own entropy pool (the current 64KB page), its own packet size (ROM-derived, variable), and jumps to a randomly selected page for the next packet. From an attacker's perspective:

- They do not know which page is active for any given byte.
- They do not know where one packet ends and the next begins.
- They do not know how many packets exist.
- They do not know the page sequence.
- Each page has its own independent address distribution.
- The page jump is itself encoded within the current page's entropy.

The result is that the encoded output is not merely indistinguishable from random — it is structurally incomprehensible. There is no framing, no fixed packet size, no predictable page order, and no way to determine any of these without the ROM. Every layer of structure is hidden behind another layer of ROM indirection.

## 4. Encoding Steps

1. Generate the initial UNSIGNAL header as normal (H1–H4, prefix, suffix).
2. Establish page 0 (the first 64KB window) from H1/H2.
3. Within the current page, pick a random address where the value is ≥ the minimum packet size. That value becomes the packet length.
4. Encode that many message bytes using addresses from the current page.
5. At the packet boundary, pick a random address in the current page. The byte at that address is the next page number (0–255). Multiply by 65536 to get the absolute ROM offset.
6. Switch to the new page.
7. Continue from step 3 until the message is exhausted.
8. Append the UNSIGNAL suffix as normal.

## 5. Decoding Steps

1. Read and process the UNSIGNAL header as normal.
2. Establish page 0 from H1/H2.
3. Read the packet size address — look up the value in the ROM at that address to determine how many encoded byte pairs follow.
4. Decode that many address pairs from the current page.
5. Read the next-page address — the byte at that address is the page number (0–255). Multiply by 65536 to get the absolute ROM offset.
6. Switch to the new page and repeat from step 3.
7. Stop when the suffix boundary is reached (determined by H4 as in standard UNSIGNAL).

## 6. Combinatorial Analysis

With standard UNSIGNAL on a 128KB ROM, "hello" has approximately 8 billion possible encodings (address combinations across the full ROM, with one window position per session).

With superZOSCII on a 16MB ROM (256 pages):

- 256 independent 64KB pages, each with its own byte distribution and address pool.
- Each packet randomly jumps to any of the 256 pages — the page sequence is ROM-derived and unpredictable.
- Each packet has a ROM-derived variable length — the attacker cannot determine packet boundaries.
- The attacker does not know how many packets exist.
- The attacker does not know which page is active for any given byte.
- Page jumps are themselves encoded within the current page's entropy — the indirection is recursive.

The combinatorial space grows multiplicatively with each packet: the number of possible encodings for a message of N packets is roughly (combinations per packet)^N × (256 page choices)^N, with each packet drawing from an independent address pool. For even a short message split across 3-4 packets, the combinatorial space exceeds anything practically enumerable.

## 7. Pros

- Massively increased combinatorial space for large ROMs.
- Each packet is independently windowed — cross-packet analysis yields nothing.
- Variable packet sizes mean the attacker cannot determine message structure.
- Packet boundaries are ROM-derived, not protocol-derived — no fixed framing to detect.
- Backwards compatible: a single-packet superZOSCII encoding is identical to standard UNSIGNAL.
- Still possible to encode and decode relatively fast on 1980s hardware with appropriate paging mechanism (Commodore 64s and Amstrad CPCs can have megabytes of RAM now and fast mass storage devices)
- Compared to 32-bit ZOSCII the encoded file retains 2x expansion instead of 32-bit's 4x expansion

## 8. Cons

- Increased implementation complexity — every additional mechanism is a potential source of bugs.
- Encoder and decoder must track window state across packets, adding statefulness.
- Larger ROM requirement — the benefit only materialises with ROMs significantly larger than 128KB, up to 16MB for full 256-page utilisation.
- The current UNSIGNAL already achieves near-perfect statistical randomness. The security gain is zero — I(M;A)=0 holds regardless.
- More code paths to test, more edge cases with small ROMs or ROMs with low byte diversity.
- Packet size indirection adds another failure mode if the ROM lacks values above the minimum packet size in the current window.
- The primary historical cause of cryptographic failure is implementation error, not insufficient theoretical security.

## 9. Recommendation

Do not implement while in draft, this document is subject to change.

The current UNSIGNAL Protocol is sufficient for all practical security requirements. superZOSCII should only be revisited if a concrete use case emerges that requires multi-megabyte ROMs with per-packet window rotation — for example, high-bandwidth streaming encryption where session-level window reuse becomes a concern.

## 10. References

- ZOSCII Specification — MIT License
- UNSIGNAL Protocol Specification v1.1 — UNINTELLIGENCE SOFTWARE LICENSE v1.1
- ZOSCII Foundation Test Vectors (entropy, chi-square, serial correlation analysis)

---

ZOSCII Foundation • Cyborg Unicorn Pty Ltd • I(M;A)=0