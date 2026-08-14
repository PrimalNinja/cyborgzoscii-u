# Finding Unknown Causation Knowledge You Observe Unconditionally

**Author:** Julian Cassin
**Date:** 2026-04-03

---

Every attack on an encoding or encryption system depends on establishing a causal link - between input and output, between patterns and meaning, between observation and knowledge. This documents the specific causal assumptions that standard attack vectors rely on, and explains why ZOSCII's ROM indirection eliminates each one.

This is not a theoretical exercise. These are the attack classes taught to signals intelligence analysts, penetration testers, and cryptanalysts worldwide. Every one of them fails against ZOSCII - not because the implementation is clever, but because the mathematics of I(M;A)=0 removes the causal foundations they require.

---

## 1. Statistical Analysis

**Causal assumption:** Encoded output retains statistical fingerprints of the input - frequency distributions, digraph patterns, entropy signatures - that can be measured and exploited.

**Why it fails:** ZOSCII output achieves 7.99+ bits/byte entropy, passes chi-square randomness tests, shows zero serial correlation, and does not compress. There is no statistical causation between input characteristics and output characteristics. The encoded stream is indistinguishable from true random noise.

## 2. Frequency Analysis

**Causal assumption:** Characters in the input language appear at known frequencies (e.g. 'E' at ~12.7% in English), and these frequencies survive encoding.

**Why it fails:** Each input character maps to multiple possible ROM addresses, selected randomly at encode time. The same character encoded twice produces different output bytes. No frequency distribution from the input survives into the output.

## 3. Known-Plaintext Attack

**Causal assumption:** If the attacker knows some portion of the original data and the corresponding encoded output, they can deduce the transformation and apply it to the unknown portions.

**Why it fails:** The same plaintext encoded with the same ROM produces different output every time, because the encoder randomly selects among all valid addresses for each byte. A known plaintext-ciphertext pair reveals one of many possible address choices, not the ROM itself. No causal chain can be constructed from pairs to key.

## 4. Chosen-Plaintext Attack

**Causal assumption:** If the attacker can cause specific data to be encoded and observe the result, they can systematically reconstruct the transformation.

**Why it fails:** For the same reason as known-plaintext: the random address selection means that encoding the same input repeatedly produces different outputs each time. The attacker cannot build a deterministic mapping. The ROM contains up to 65,536 entries; each input byte may have hundreds of valid addresses. The combinatorial space defeats systematic reconstruction.

## 5. Coordinate System Confusion

**Causal assumption:** Data within the encoded file can be interpreted using a single, consistent addressing scheme.

**Why it fails:** UNSIGNAL Protocol headers use absolute positions within the first 64KB of the ROM. Data addresses are relative to a ROM offset derived from the headers themselves via indirection. These are two different coordinate systems coexisting in the same file. An attacker cannot interpret data addresses without first recognising and decoding the headers - which are indistinguishable from data. The probability of correctly guessing the header/data alignment without the ROM is zero.

## 6. Traffic Analysis

**Causal assumption:** Message boundaries, packet sizes, and timing patterns reveal information about the content or communication structure.

**Why it fails:** Random prefix and suffix padding hides true message boundaries. Variable ROM start offsets change interpretation per session. H3/H4 headers are themselves indirections. No fixed patterns exist in packet sizes or timing. The attacker cannot even determine whether a file contains data or is empty.

## 7. Reverse Engineering

**Causal assumption:** Understanding how the encoder works provides leverage for breaking the encoding.

**Why it fails:** The encoder is a one-line lookup. The algorithm is public, trivial, and provides no advantage. Security resides entirely in the ROM, not in the process. Knowing the mechanism gives the attacker nothing they did not already have.

## 8. Key Recovery (Brute Force)

**Causal assumption:** The key space can be searched exhaustively or reduced through mathematical shortcuts.

**Why it fails:** ZOSCII is information-theoretically secure. Brute force is not merely computationally infeasible - it is impossible by definition. There is no mathematical relationship between key and output to exploit, no shortcut to discover, and no quantum algorithm (including Grover's) that can invert a function that does not exist as a computable transformation.

## 9. Side-Channel Attack

**Causal assumption:** Timing variations, power consumption, electromagnetic emissions, or cache behaviour during encoding leak information about the key or data.

**Why it fails:** The encode operation is a random number allocation and two memory lookups - a couple of instructions, constant time, constant power. There is no complex mathematical operation whose execution characteristics vary with the data. There is no causal link between observable side-channel emissions and the content being encoded.

## 10. The Verification Problem

**Causal assumption:** When a candidate key is tested, the attacker can verify whether the decoding is correct.

**Why it fails:** Any ROM decodes any encoded file to *something*. The wrong ROM produces output that may appear plausible - there are no checksums, no MACs, no success indicators in the encoded stream. The attacker cannot distinguish a correct decoding from an incorrect one. Every candidate ROM produces an internally consistent result. Verification requires external context that the attacker does not possess.

## 11. Combinatorial Exhaustion

**Causal assumption:** The space of possible encodings for a given input is tractable.

**Why it fails:** A single novel encoded with ZOSCII has more than 10^5,500,000 possible representations. Using just 5 non-repeated addresses per character yields over 1 trillion combinations. The combinatorial space cannot be tracked, stored, or searched. There are no collisions.

## 12. Authentication Extraction

**Causal assumption:** Authentication data (MACs, checksums, signatures) exists outside the encoded payload and can be identified, stripped, or analysed separately.

**Why it fails:** In ZOSCII/UNSIGNAL, all authentication data is placed *inside* the encoded payload before encoding. Once encoded, authentication bytes are indistinguishable from message bytes, which are indistinguishable from random noise. The attacker cannot locate, isolate, or analyse authentication data without first decoding the entire payload - which requires the ROM.

## 13. Quantum Attack (Grover's / Shor's)

**Causal assumption:** Quantum algorithms can search unstructured spaces quadratically faster (Grover's) or factor large numbers / compute discrete logarithms efficiently (Shor's).

**Why it fails:** Grover's algorithm requires a function to invert. ZOSCII's encoding is not a computable function in the cryptographic sense - it is a non-deterministic lookup with no inverse mapping. There is no function for Grover's to accelerate. Shor's algorithm targets mathematical structures (prime factorisation, discrete logarithms) that do not exist in ZOSCII. Quantum computing provides zero advantage against information-theoretic security.

---

## Conclusion: Epistemic Closure

ZOSCII achieves what cryptographers call *epistemic closure* against the attacker: not merely that the attacker cannot break the system in practice, but that the attacker cannot know whether they have broken it even in principle. Every observation is unconditional - the attacker sees everything, and knows nothing. Every causal chain that every known attack vector depends on has been severed at the mathematical foundation.

That is what Finding Unknown Causation Knowledge You Observe Unconditionally means.