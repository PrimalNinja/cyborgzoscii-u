# UNSIGNAL Protocol: Intelligence Analysis Walkthrough
# ====================================================

1. The Intercept
   - Raw file obtained: no headers, no magic bytes, no structure
   - File size: variable (due to random prefix/suffix)
   - Metadata: timing, source, destination — leads nowhere, cannot be correlated to content or intent

2. Statistical Analysis (ent)
   - Entropy: 7.99+ bits/byte (maximum)
   - Compression: 0% (perfect entropy)
   - Chi square: passes as random
   - Serial correlation: near zero
   - Result: indistinguishable from true random noise

3. Coordinate System Confusion
   - Header addresses: absolute positions within first 64KB of ROM
   - Data addresses: relative to ROM offset derived from H1/H2 indirections themselves
   - Two different coordinate systems in the same file
   - Attacker cannot interpret data addresses without first:
       a) Recognizing H1/H2 as special (they look like normal data)
       b) Decoding H1/H2 to get offset value
       c) Applying offset to reinterpret all following addresses
   - Probability of guessing correctly without ROM: zero
   - Even with ROM, must know which addresses are header vs. data
   - Header/data alignment occurs only 1/65536 of the time by chance

4. Traffic Analysis
   - Random prefix/suffix hides true message boundaries
   - H3/H4 are also indirections themselves
   - Variable ROM start offsets change interpretation per session
   - No fixed patterns in packet sizes or timing
   - Cannot determine if file contains data or is empty

5. Reverse Engineering
   - Encoder obtained: one-line lookup table (public)
   - Algorithm: trivial, security is in ROM (key)
   - Knowing how it works provides no advantage

6. Known-Plaintext Attempts
   - Same plaintext encoded twice → different outputs
   - Multiple address options per character (random selection)
   - No repeatable patterns to exploit

7. Key Recovery
   - Brute force: ITS, impossible by definition
   - Side-channel: simple lookup, no complex math to leak
   - ROM must be obtained via physical/legal means

8. The Verification Problem
   - Any ROM decodes to something
   - Wrong ROM → garbage (but garbage that looks real)
   - No checksums, no MACs, no success indicator
   - Cannot verify which decoding is "correct"

9. Combinatorial Scale (Gone with the Wind example)
   - Single novel encoding: >10^5,500,000 possible representations
   - 5 non-repeated addresses: 1 trillion combinations
   - Tracking until memory exhausted: impossible
   - No collisions, ever

10. Perfect Deniability
    - Every decoding is internally consistent
    - Any output can be dismissed as random coincidence
    - "Correct" decoding undefined without external context

11. Compression Behavior
    - ZOSCII/UNSIGNAL encoded files do not compress (~0% ratio)
    - Output is already close to maximum entropy
    - For size reduction: compress input first, then encode
    - Encoded result remains uncompressible regardless of input

12. Authentication & Tamper Dection Is Internal
    - MAC if required: place it INSIDE the encoded payload
    - Checksums, signatures, verification data: all go IN the message
    - Same encoding rules apply — they become indistinguishable from random noise
    - Attacker cannot distinguish authentication data from message content
    - No external validation markers exist

13. Conclusion
    - Statistical tools return: random noise, nothing here
    - Traffic analysis defeated by boundary hiding
    - Key recovery requires ROM, not math
    - Verification impossible even with ROM candidate
    - Authentication hidden within payload, indistinguishable from message
    - Compression only possible before encoding, not after
    - System achieves epistemic closure: attacker cannot know if they have won
	