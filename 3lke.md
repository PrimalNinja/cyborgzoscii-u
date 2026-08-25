# 3LKE: Three-Layer Key Exchange

**Inside-Out Pigeonhole - Data Shattering, Not Encryption**

**Author:** Julian Cassin
**Date:** 2026-06-10
**Version:** 1.1 (Draft)
**License:** UNINTELLIGENCE License v1.1 / MIT (core ZOSCII)

---

## Table of Contents

1. [Abstract](#abstract)
2. [The Core Insight: Inside-Out Pigeonhole](#the-core-insight-inside-out-pigeonhole)
3. [Analogy: The Anti-Safe](#analogy-the-anti-safe)
4. [Structural Precedents](#structural-precedents)
5. [The 5 Channels](#the-5-channels)
6. [Protocol Flow](#protocol-flow)
7. [The GUID-SIEVE (Puncturing Matrix)](#the-guid-sieve-puncturing-matrix)
8. [Security Properties](#security-properties)
9. [Attack Scenarios](#attack-scenarios)
10. [Comparison Table](#comparison-table)
11. [Why Not Just Use Encryption?](#why-not-just-use-encryption)
12. [Implementation Notes](#implementation-notes)
13. [License](#license)

---

## Abstract

3LKE is a key exchange protocol built on UNSIGNAL, PENTAGONE, and GUID-based extraction. It distributes a 128KB ROM (the session key) across **5 independent channels** such that **all 5 are required to reconstruct**.

No encryption. No trusted servers. Information-theoretic security.

**I(Original; Remaining) = 0** without the GUID.

---

## The Core Insight: Inside-Out Pigeonhole

> *"3LKE is the Pigeonhole Principle turned inside out."*

| Traditional Pigeonhole (PENTAGONE) | Inside-Out (3LKE) |
| :--- | :--- |
| Cram items into boxes | **Create holes in the item** |
| Ensures fragments incomplete | **Ensures fragment + hole + map required** |
| Any 3 of 5 reconstruct | **All 5 channels required** |

**Traditional Pigeonhole:** More items than containers -> at least one container holds multiple items. Used in PENTAGONE to ensure fragments cannot reconstruct alone.

**3LKE (Inside-Out):** Take a complete 128KB puzzle. Deliberately create **structured vacancies (holes)**. Scatter the mapping instructions for those holes across completely different physical dimensions.

**Result:** The data isn't locked. It's **dismantled**.

---

## Analogy: The Anti-Safe

| Traditional Encryption | 3LKE |
| :--- | :--- |
| Build a massive mathematical safe | **Dismantle the object itself** |
| Wrap data inside cipher blocks | **Puncture the data with GUID-SIEVE** |
| Adversary faces a lock to pick | **Adversary stands in an empty room** |

The data is **physically and structurally not there** until all 5 channels converge.

---

## Structural Precedents

### 1. Punctured Code (Coding Theory)

| Aspect | Traditional Puncturing | 3LKE |
| :--- | :--- | :--- |
| **Mechanism** | Delete bits using a matrix pattern | Delete bytes using GUID-SIEVE pattern |
| **Purpose** | Save bandwidth | **Achieve I(Original; Remaining)=0** |
| **Recovery** | Error-correction math guesses missing bits | **Impossible to guess** (high-entropy holes) |

**The Turn:** Traditional puncturing leaves the data *recoverable*. 3LKE leaves it *permanently broken* without the GUID.

### 2. Chaffing and Winnowing (Rivest, 1998)

| Aspect | Rivest's Chaffing | 3LKE |
| :--- | :--- | :--- |
| **Mechanism** | Split data into packets, mix with fake "chaff" | **Slice out real data chunks** |
| **Transport** | Send all packets publicly | **Scatter across 5 separate channels** |
| **Recovery** | Recipient "winnows" real packets | **Recipient reassembles via GUID map** |

**The Turn:** Chaffing adds *fake* noise. 3LKE removes *real* data and sends it elsewhere.

### 3. One-Time Pad Physical Split (Classic Espionage)

| Aspect | Physical OTP Split | 3LKE |
| :--- | :--- | :--- |
| **Method** | Split pad across courier, radio, dead-drop | **Split ROM across servers, email, SMS** |
| **Risk** | Single interception is insufficient | **Single channel compromise is insufficient** |
| **Recovery** | All pieces required | **All 5 channels required** |

**The Turn:** 3LKE automates this physical strategy into **cloud infrastructure**.

---

## The 5 Channels

| Channel | Content | Delivery | Security |
| :--- | :--- | :--- | :--- |
| **Server 1** | PENTAGONE Share A (round-robin fragment) | One-time fetch queue | Incomplete alone |
| **Server 2** | PENTAGONE Share B (round-robin fragment) | One-time fetch queue | Incomplete alone |
| **Server 3** | PENTAGONE Share C (round-robin fragment) | One-time fetch queue | Incomplete alone |
| **Email** | Extracted delta (bytes removed by GUID) | Email (plain or encrypted) | Useless without GUID |
| **SMS/Post** | GUID token (32 hex chars, 128 bits) | Out-of-band (SMS, postal mail, phone call) | Useless without data |

**Total: 5 parts. All required. No single point of failure.**

---

## Protocol Flow

### Sender Side

1. **Start with full ROM** (128KB high-entropy key material)

2. **Extract delta using GUID**
   - GUID determines extraction pattern (direction + step + alternating modulus 3/5/7)
   - Removes approximately 22% of bytes -> extracted delta

3. **UNSIGNAL encode the remaining data** (optional, adds metadata protection)

4. **Split remaining data into 3 shares** (round-robin, no redundancy)

5. **Distribute**
   - Shares A, B, C -> 3 public one-time fetch queues (servers)
   - Extracted delta -> email
   - GUID -> SMS or postal mail

### Recipient Side

1. **Fetch shares A, B, C** (destructive read - first come, first served)

2. **Receive email** (extracted delta)

3. **Receive SMS/post/phone call** (GUID)

4. **Reconstruct**
   - UNSIGNAL decode (if used)
   - GUID regenerates extraction history
   - Re-insert extracted delta into remaining data

5. **Full ROM recovered**

---

## The GUID-SIEVE (Puncturing Matrix)

| Parameter | Value |
| :--- | :--- |
| **Input** | 32 hex characters (128 bits) |
| **Words** | 16 words (2 hex chars each) |
| **Direction** | Even -> forward, Odd -> backward |
| **Step** | Second hex char (0-15), modulated by 3/5/7 |
| **Removal fraction** | ~22% of ROM bytes (alternating 1/3, 1/5, 1/7) |
| **Without GUID** | **I(Original; Remaining) = 0** |

The GUID is not a decryption key. It is a **shatter pattern** - telling the recipient where the holes are.

### Extraction Algorithm (Pseudocode - NOT ACTUAL ALGORITHM)

```
function extractDelta(ROM, GUID, targetBytes):
    remaining = copy(ROM)
    extracted = []
    position = 0
    passCount = 0

    while length(extracted) < targetBytes:
        word = GUID[passCount % 16]  # 2 hex chars
        direction = even(word[0]) ? 'forward' : 'backward'
        step = word[1] % modulus[passCount % 3]  # 3,5,7 alternates
        length = len(remaining)

        if direction == 'backward':
            position = length - 1 - step
        else:
            position = (position + step) % length

        extracted.append(remaining[position])
        remove(remaining, position)
        passCount++

    return extracted, remaining
```

---

## Security Properties

| Property | How achieved |
| :--- | :--- |
| **No single point of failure** | 5 independent channels |
| **No encryption** | Indirection, extraction, splitting - no ciphers |
| **Information-theoretic security** | I(Original; Remaining) = 0 without GUID |
| **Quantum-proof** | No mathematical structure to attack |
| **Perfect forward secrecy** | Destructive read on servers, ephemeral entropy sugar |
| **Tamper detection** | Recipient knows if attacker fetched first (can't fetch) |
| **Out-of-band security** | SMS/post not on network |

---

## Attack Scenarios

| Attacker has | Can reconstruct? |
| :--- | :--- |
| Any 1 server share | [ ] No |
| Any 2 server shares | [ ] No |
| All 3 server shares | [ ] No (missing email + GUID) |
| Email only | [ ] No (missing servers + GUID) |
| GUID only | [ ] No (missing all data) |
| Servers + email | [ ] No (missing GUID) |
| Servers + GUID | [ ] No (missing email) |
| Email + GUID | [ ] No (missing servers) |
| **All 5 channels** | [x] Yes |

---

## Comparison Table

### 3LKE vs. Precedents

| Property | Puncturing (Coding) | Chaffing (Rivest) | OTP Split (Physical) | **3LKE** |
| :--- | :--- | :--- | :--- | :--- |
| **Mechanism** | Delete bits | Add fake packets | Split physical media | **Delete & scatter** |
| **Security basis** | Error correction | Secret key | Physical possession | **Information theory** |
| **Recovery requires** | Math | Winnowing | All pieces | **All 5 channels** |
| **Quantum-proof** | No | No | Yes (physical) | **Yes** |
| **Automated** | Yes | Yes | No | **Yes** |

### 3LKE vs. Traditional Key Exchange

| Property | Traditional KEX (DH, RSA) | 3LKE |
| :--- | :--- | :--- |
| **Trust required** | Trust in math, trust in implementations | **Trust no one** |
| **Quantum resistance** | No (DH/RSA broken by Shor) | **Yes (information-theoretic)** |
| **Encryption used** | Yes | **No** |
| **Key material** | Shared secret (bits) | **128KB ROM (entropy)** |
| **Channels** | 1 (network) | **5 (servers, email, SMS/post)** |

---

## Why Not Just Use Encryption?

| Encryption | 3LKE |
| :--- | :--- |
| Single channel (usually) | 5 channels |
| Trusted servers required | Untrusted servers fine |
| Key is transmitted (in some form) | Key never exists in one place |
| Breakable (given enough time) | Information-theoretically secure |
| Quantum-vulnerable | Quantum-proof |
| Harvest now, decrypt later | **Nothing to harvest** |

---

## Implementation Notes

- Servers implement **one-time fetch** (destructive read)
- PENTAGONE split uses round-robin (byte 0->A, byte 1->B, byte 2->C, repeat) - **no redundancy**
- UNSIGNAL encoding optional (removes metadata)
- Entropy sugar (MP3s, mouse movements, system state) ensures unique ROMs per session
- The email part (extracted delta) is useless without the GUID
- The GUID is useless without the data
- The server shares are useless without email + GUID

---

## License

Core ZOSCII: MIT
UNSIGNAL, PENTAGONE, PENTAGONE, 3LKE, GUID-SIEVE: UNINTELLIGENCE License v1.1