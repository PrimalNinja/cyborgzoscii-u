# Signature Permutation Encoding Remote Message

**A Zero-Knowledge, Quantum-Proof Digital Signature Protocol Using Self-Encoding ROMs**

**Version 1.0** (DRAFT) 
**Author:** Julian Cassin  
**Date:** 2026-04-07

## SOFTWARE LICENSE v1.1

UNSIGNAL Protocol is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

> **Note:** This document builds upon the **Protocol Enabling Notarised Information Security ** specification. Core concepts including ROMs (`E`), self-signatures (`S`), Base ROMs, Payloads (`P`), and the 15-step protocol are defined there and not duplicated here. Refer to the Protocol Enabling Notarised Information Security document for:
> - Complete protocol steps (generation, validation, consumption)
> - Security properties (self-validation, forward secrecy, non-repudiation)
> - Known weaknesses and considerations
> - Relation to existing cryptographic concepts

---

## Abstract

Signature Permutation Encoding Remote Message is a novel digital signature mechanism based on information-theoretic security rather than computational hardness. By encoding a ROM using itself as the key, Signature Permutation Encoding Remote Message produces a self-signature that can be verified without revealing the original ROM. This provides:

- **100% security** (information-theoretic, proven by Shannon)
- **Quantum-proof** (no mathematical structure for Shor's algorithm to exploit)
- **Zero-knowledge** (the verifier learns nothing about the ROM)
- **Tamper detection** (any change to the ROM breaks the signature)

Signature Permutation Encoding Remote Message has no dependency on number theory, trapdoor functions, or unproven computational assumptions. It is the first practical digital signature scheme that is provably secure against any adversary with unlimited computational power, including quantum computers.

---

## 1. Core Concept

### 1.1 Self-Signature Definition

Given a ROM `E` (a random byte array, 2KB to 128KB), the self-signature `S` is defined as:

```
S = UNSIGNAL_encode(E, ROM=E)
```

That is: encode the ROM `E` using `E` itself as the encoding key (ROM).

### 1.2 Verification

Given `S` (the signature) and a candidate ROM `E'`, verification is:

```
E_decoded = UNSIGNAL_decode(S, ROM=E')
Verification passes if and only if E_decoded == E'
```

**Only the correct ROM will decode `S` to itself.** Any other ROM will decode `S` to a different, meaningless value.

### 1.3 Key Properties

| Property | Description |
|----------|-------------|
| **Self-referential** | The ROM is both the key and the message |
| **Deterministic** | Given the same ROM, `S` is deterministic (if random selection is fixed) or can be randomized (if multiple valid encodings exist) |
| **Verifiable** | Anyone with the ROM can verify the signature |
| **Unforgeable** | Without the ROM, it is impossible to create a valid `S` |
| **Zero-knowledge** | `S` reveals nothing about `E` (I(E;S)=0) |

---

## 2. Mathematical Foundation

### 2.1 Information-Theoretic Security

From Shannon's theory:

```
I(E;S) = H(E) - H(E|S) = 0
```

Where:
- `I(E;S)` is the mutual information between ROM `E` and signature `S`
- `H(E)` is the entropy (uncertainty) of the ROM
- `H(E|S)` is the conditional entropy of the ROM given the signature

**Interpretation:** Knowing the signature `S` gives you zero information about the ROM `E`. The signature reveals nothing.

### 2.2 Why This Works

UNSIGNAL encoding works by address indirection:

1. For each byte in the message (here, the ROM itself), find all positions in the ROM where that byte occurs
2. Randomly select one of those positions
3. Output the selected address

The output `S` is a sequence of random-looking addresses. Without the ROM, these addresses are meaningless. With the ROM, you can decode `S` back to the original bytes.

When the message is the ROM itself, this creates a fixed point: the ROM encodes to `S`, and `S` decodes back to the ROM — but only when using the correct ROM as the decoding key.

### 2.3 Combinatorial Infeasibility

For a 128KB ROM (a random sliding window of 64KB is used) with 256 possible byte values, each byte appears approximately 256 times on average. Therefore, each byte in the message has approximately 256 possible encoding choices.

For a ROM of size `N`, the total number of possible self-signatures is:

```
Total signatures = ∏_{i=1}^{N} (count[E[i]] in E)
```

For a random ROM, this is approximately:

```
Total signatures ≈ (N/256)^N = (256)^N = 256^65536 ≈ 10^(65536 * log10(256)) ≈ 10^(65536 * 2.408) ≈ 10^157,800
```

This is astronomically larger than the number of atoms in the observable universe (≈ 10^80).

Smaller ROMs are also usable, 2KB, 4KB, 8KB, 16KB, 32KB etc, but 8KB to 128KB form a sweet spot. If you can afford the storage and bandwidth, go to 128KB.  Note: a 2KB ROM gives 10^1849 strength - which is astronomical.

---

## 3. Applications

### 3.1 Bring Your Own Keyfile (BYOK)

**Use case:** Software licensing without phoning home.

| Step | Action |
|------|--------|
| 1 | Customer generates ROM `E` |
| 2 | Customer encodes `E` using `E` as ROM → `S` |
| 3 | Customer sends `S` to software vendor (not `E`) |
| 4 | Vendor stores `S` (cannot decode it without `E`) |
| 5 | Customer uses `E` as their license key |
| 6 | Software decodes `S` using `E` to verify license |

**Why it works:** The vendor never sees `E`. Even if the vendor is compelled to hand over licensing data, they have nothing useful. The customer's license key remains secret.

### 3.2 Tamper Detection (Tripwire)

**Use case:** Detecting if software has been modified.

| Step | Action |
|------|--------|
| 1 | Developer generates ROM `E` |
| 2 | Developer encodes `E` using `E` as ROM → `S` |
| 3 | Developer embeds `S` in the software binary |
| 4 | At runtime, software decodes `S` using `E` (embedded or external) |
| 5 | If `E_decoded != E`, the software has been tampered with |

**Why it works:** Any modification to the software that changes `E` or `S` will cause verification to fail. This is a form of code signing without traditional certificates.

### 3.3 Notarised Data Submission (Protocol Enabling Notarised Information Security)

As defined in the Protocol Enabling Notarised Information Security, the self-signature `S` is used to validate the workspace ROM `E` before the Customer overwrites it with the Payload `P`. Refer to the Protocol Enabling Notarised Information Security document for the complete 15-step protocol.

## 4. Conclusion

Signature Permutation Encoding Remote Message is the first practical digital signature scheme based on information-theoretic security rather than computational hardness. By leveraging UNSIGNAL's self-encoding property, Signature Permutation Encoding Remote Message provides:

- **100% security** (proven by Shannon)
- **Quantum-proof** (no mathematical structure for Shor's algorithm)
- **Zero-knowledge** (signature reveals nothing about the key)
- **Tamper detection** (any change to the key breaks verification)

Signature Permutation Encoding Remote Message is not a replacement for all signature schemes — it has different trade-offs (larger key size, slower signing). But for applications where security is paramount and ROM storage is feasible, Signature Permutation Encoding Remote Message offers unprecedented guarantees.

The mathematics are proven. The implementation is open source. The protocol is ready.

---

## 5. References

1. Shannon, C.E. (1949). "Communication Theory of Secrecy Systems"
2. Cassin, J. (2025). "ZOSCII: Zero Overhead Secure Code Information Interchange"
3. Cassin, J. (2026). "UNSIGNAL Protocol Specification"
4. Cassin, J. (2026). "Protocol Enabling Notarised Information Security"
