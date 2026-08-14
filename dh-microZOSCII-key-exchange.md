# DH-microZOSCII: DH Key to microROM Derivation

**DRAFT v0.1**
**Author:** Julian Cassin
**Date:** 2026-05-08

## SOFTWARE LICENSE v1.1

DH-microZOSCII is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

## 1. OVERVIEW

This document specifies how to derive a microZOSCII ROM directly from Diffie-Hellman shared secrets.

The derived ROM can then be used with any ZOSCII-family protocol (UNSIGNAL, microZOSCII, TEMPURA, etc.)

It is recommended to use this when necessary - depending on use case, if full ZOSCII ROMs are already exchanged, you may not need to do this again.

Note: For completely information-theoretically secure communications, this bootstrap method cannot be used - it is hardened maths. If you require 100% security against eavesdroppers, use out of band key exchange as suggested in the microZOSCII paper.

---

## 2. THE DERIVATION

### 2.1 Input

| Parameter | Size | Description |
|-----------|------|-------------|
| `s1, s2, s3, s4` | 4 x 256 bytes | Up to 4 DH shared secrets `g^(ab) mod p` |

*Note: 1 DH secret is sufficient. 2-4 provide additional entropy and enable microUNSIGNAL features.*

### 2.2 Output

| Output | Size | Format |
|--------|------|--------|
| microROM | 512 x N nibbles (where N = number of DH secrets, 1-4) | Hex characters (0-F), where N = number of DH secrets |

### 2.3 Algorithm

```
microROM = hex(s1) + hex(s2) + hex(s3) + hex(s4)
```

Where `hex()` converts each byte to two hex characters (0-F).

**JavaScript:**
```javascript
const microROM = [s1, s2, s3, s4].map(s =>
    Array.from(s).map(b => b.toString(16).padStart(2, '0')).join('')
).join('');
```

**Python:**
```python
microROM = s1.hex() + s2.hex() + s3.hex() + s4.hex()
```

---

## 3. PROPERTIES

| Number of DH secrets | microROM length | Address space |
|---------------------|----------------|---------------|
| 1 | 512 nibbles | 0-511 |
| 2 | 1024 nibbles | 0-1023 |
| 3 | 1536 nibbles | 0-1535 |
| 4 | 2048 nibbles | 0-2047 |

| Property | Value |
|----------|-------|
| Character set | 0-9, A-F (hex digits) |
| Instances per hex digit (1 secret) | ~32 (on average) |
| Instances per hex digit (4 secrets) | ~128 (on average) |

---

## 4. microUNSIGNAL (DRAFT NOTE)

**Status: NOT FINALISED - Subject to change**

When up to 4 DH keys are exchanged, the combined microROM can be used with a microUNSIGNAL header mechanism similar to full UNSIGNAL Protocol.

**Proposed features:**
- Random prefix/suffix lengths derived from microROM
- Moving window indirection for transmitting the 128KB ROM
- Header addresses within the microROM space

*Specifics are yet to be finalised for microUNSIGNAL.*

---

## 5. USAGE

The microROM (512 x N nibbles) is used directly as the ROM for any ZOSCII-family encoding:

- **microZOSCII:** Encode a 64KB or 128KB session ROM
- **microUNSIGNAL:** Use with header mechanism (see draft note above)
- **UNSIGNAL:** Use as the primary encoding table
- **TEMPURA:** Distribute across 5 servers

For protocol-specific encoding steps, refer to the respective specifications.

---

## 6. SECURITY

| Layer | Security Basis |
|-------|----------------|
| DH key exchange | Computational (discrete log) |
| microROM derivation | Deterministic (no security reduction) |
| ZOSCII encoding | Information-theoretic (I=0) |

**The microROM derivation adds no weakness.** The security of the derived ROM is exactly the security of the DH secret(s) `s1...s4`.

With 4 DH secrets, an attacker must break all 4 exchanges to reconstruct the full microROM.

---

## 7. EXAMPLE

**Input (DH secret s1 - first 8 bytes shown):**
```
A3 F2 8C 1D 4E 5B 7A 9C ...
```

**Output (microROM - first 16 nibbles shown):**
```
a3f28c1d4e5b7a9c ...
```

**Address space positions (1 secret, 0-511):**
- Position 0 -> 'a'
- Position 1 -> '3'
- Position 2 -> 'f'
- Position 3 -> '2'
- ...
- Position 511 -> last hex digit

---

## 8. CONCLUSION

DH shared secret(s) convert directly to a hex string.

That hex string IS the microROM.

No further processing. No restructuring. No windowing.

When 4 DH secrets are exchanged, the combined 2048-nibble microROM enables microUNSIGNAL features (still in draft).

---

## 9. REFERENCES

1. Diffie, W., & Hellman, M. (1976). "New Directions in Cryptography"
2. Cassin, J. (2026). "UNSIGNAL Protocol Specification"
3. Cassin, J. (2026). "microZOSCII: Quantum-Proof Bootstrap Protocol"
4. Cassin, J. (2026). "ZOSCII: Zero Overhead Secure Code Information Interchange"