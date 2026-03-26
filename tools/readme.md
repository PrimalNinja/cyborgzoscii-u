# Cyborg Neptune Nuclear Tight Security

This is a closed‑source, free software tool for Windows that demonstrates the power of the UNSIGNAL Protocol and ZOSCII encoding. It provides information‑theoretic security for your files.

## Release Candidate 6
This software is currently RC6 - Release Candidate 6. The core encoding and decoding functionality is complete and the information-theoretic security properties are fully intact, but this is an early release and we are actively taking feedback. If you encounter any bugs or unexpected behaviour please let us know via the WhatsApp support channel: **CyborgUnicorn**.

## Languages
Software is localised in English, Bahasa Indonesian, Chinese (Simplified), Czech, Dutch, French, German, Greek, Hausa, Hindi, Igbo, Italian, Japanese, Korean, Polish, Portuguese, Romanian, Russian, Spanish, Swahili, Swedish, Tagalog, Thai, Turkish, Ukrainian, Vietnamese and Yoruba.

## License

This tool is free for **PERMITTED USERS** under the terms of the **UNINTELLIGENCE SOFTWARE LICENSE v1.1**. Please see the **License Agreement** section within the application for full details on permitted users (individuals, private commercial entities, NGOs, etc.) and prohibited users (government intelligence, military, law enforcement, etc.) - Prohibited users (government intelligence agencies, military organizations, defense contractors, law enforcement, and mass surveillance entities) are not authorized to use this free version. Such entities may contact Cyborg Unicorn for commercial licensing options.

## Getting Started

### Login
To login, click the login screen or drag and drop a ROM file onto it. This ROM becomes your **resident ROM** and is held in memory only - it is never written to disk. Without a valid resident ROM, no encoding or decoding can occur.

### Multi-ROM Security Layers
The UNSIGNAL protocol supports layered security using one, two, or three ROMs. Each additional ROM exponentially increases the search space an attacker must explore:

* 1 ROM (128KB minimum) - Creates 65,536 "safes" (possible offset positions) each containing unknown data. An attacker would need to try each possible offset to decode a single file correctly.
* 2 ROMs - First ROM encodes into a temporary file, second ROM encodes again. This creates 65,536 × 65,536 ≈ 4.3 billion possible combinations. File size expands by approximately 4×.
* 3 ROMs - Triple-layered encoding creates 65,536³ ≈ 281 trillion possible combinations. File size expands by approximately 8×.

Storage Consideration: Each additional ROM layer multiplies the output file size (~2× for 1 ROM, ~4× for 2 ROMs, ~8× for 3 ROMs). To reduce storage requirements, compress your data before encoding - compressible data will yield smaller encoded files while maintaining the same security properties.

Important: All files encoded with any ROM combination are valid decodings to an attacker and to the system. The system cannot and does not verify whether a decoded file is "correct" - it is the user's responsibility to use the correct ROMs in the correct order. The wrong ROM combination will produce garbage output (which could be a plausible alternative decoding).

### ROM Management
Use the **ROMs** tile to manage your ROM collection. ROMs are stored encoded using your resident ROM. Only the correct resident ROM can decode and display them.

*   **Activate (ROM 1 / 2 / 3) ** - Decodes a stored ROM and saves it as active.rom, active2.rom, or active3.rom for use with the Encode and Decode tools.
*   **Deactivate All** - Securely deletes all active ROM files from disk.
*   **Add ROM** - Encodes a new ROM image using your resident ROM and adds it to the collection.
*   **Delete ROM** - Securely deletes a stored ROM.

ROM Selection Order: When using multiple ROMs, they are applied in sequence: ROM 1 first, then ROM 2, then ROM 3. For decoding, the reverse order is used (ROM 3, then ROM 2, then ROM 1). Using ROMs in the wrong order will produce garbage output.

### The `active.rom` File - Security Notice
When you activate ROMs from the ROMs explorer, the system decodes them and writes the results to files called active.rom, active2.rom, and active3.rom in your data folder. These files contain the fully decoded ROM images, which are indexed to build the address tables used by the Explorer menu Encode and Decode tools.

**Important: active.rom, active2.rom, and active3.rom are NOT secured while they are active. ** By design, they must exist in decoded form on disk so that the Explorer menu Encode and Decode tools can use them. You should be aware that while these files exist on disk they are readable. Use the **Deactivate All** function (or the Deactivate buttons in the Status screen) to securely delete active ROM files when you are finished with them. The resident ROM(s) you log in with are entirely separate - they are held only in memory and are never written to disk.

### Basic Encode / Decode
Use these to encode or decode any file. Basic Encode and Basic Decode use your **resident ROM(s)** - the ROM(s) you logged in with, which are held in memory only and never touch disk. The ROMs are indexed to build the address tables used for encoding and decoding. These operations do not use active.rom, active2.rom, or active3.rom.

### Detailed ROM Analysis
Provides a detailed ROM analysis of its entropy to ensure that the ROM file you chose is up for the job. Colour JPEG files make really good candidates and we recommend you use them for ease of identification.

### Data Protection
Store sensitive information in GUID-based folders organised by type (e.g. Bank Accounts, Passwords).

### Secure Delete
Overwrites a file with `0xFF` then `0x00` before deleting. Note: on SSD/flash or even modern hard drive storage, secure delete is best-effort due to wear levelling.

### Status
View the currently active ROM image. If no ROM is activated, or the active ROM is not a valid image, "No Active ROM" is shown.

---

## ⚠️ WARNING

**ZOSCII plus UNSIGNAL Protocol achieves perfect secrecy. If you lose your ROM files you lose your data - this is the dual-edged consequence of perfect secrecy. There is no getting your data back, ever, if you lose your ROM files. Back them up safely - on multiple devices, multiple locations, multiple times. Quantum Proof security has this consequence.**

---

## Information Theoretic Security

Information-theoretic security (ITS) is the gold standard of secrecy. Unlike computational security - which relies on the assumption that an adversary lacks the computing power to break a cipher within a practical timeframe - information-theoretic security is mathematically proven to be unbreakable regardless of how much computing power an attacker has, now or in the future. Even a computer of unlimited power cannot extract the plaintext without the key, because the information simply does not exist in the output.

The concept was formalised by Claude Shannon in his landmark 1949 paper "Communication Theory of Secrecy Systems," in which he proved that the one-time pad (OTP), when used correctly, achieves perfect secrecy. The OTP XORs each bit of the message against a corresponding bit of a truly random key of equal length, producing ciphertext that is statistically independent of the plaintext. Shannon expressed this as `I(M;C) = 0` - the mutual information between message and ciphertext is zero. An attacker who intercepts the ciphertext learns nothing at all about the message.

OTP's restrictions follow directly from its construction: the key must be exactly as long as the message, perfectly random, and never reused. Reusing the key against a different message immediately leaks the XOR of the two messages. These constraints make OTP impractical for general use, which is why the field of computational cryptography - AES, RSA, ECC - developed as a pragmatic alternative, accepting weaker (computational rather than mathematical) security guarantees in exchange for practical key sizes.

A second, quite different ITS mechanism is **Shamir's Secret Sharing (SSS)** , introduced by Adi Shamir in 1979. SSS solves a different problem: splitting a secret among n parties such that any k of them can reconstruct it, while k-1 parties learn absolutely nothing. It achieves this through polynomial interpolation - the secret is encoded as the constant term of a random polynomial of degree k-1, and each party receives one point on that polynomial. With k-1 points, every possible secret value is equally consistent with the shares, so `I(S ; any k-1 shares) = 0`. This is the same Shannon guarantee - zero mutual information - but achieved through completely different mathematics: no XOR, no lookup, just the geometry of polynomials over a finite field. SSS appears in the security taxonomy for this product as a cryptography enabler. SSS is a recommendation for storing your Nuclear Tight Security ROMs in a secure way as an alternative or ideally in addition to your multiple backups.

ZOSCII and UNSIGNAL are a third, independent path to the same destination. Rather than relying on the computational hardness of factoring large numbers or solving elliptic curve problems - assumptions that may be undermined by quantum computing or future mathematical advances - they depend on information simply not being present in the output. The data is random pointers to your random ROM for every instance of every unknown character that an attacker might consider. Your ROM is your access, just as a private key is for most encryption systems.

### ZOSCII - Zero Overhead Secure Code Information Interchange
ZOSCII has its origins in NTC (Native Threaded Code), a compiler technique developed at Cyborg Unicorn to make text output faster on Z80-based systems. The key insight was the same one that underpins ZOSCII: rather than computing or transforming data character by character through a pipeline of instructions, simply look up the result directly in a table using the input as an address. On a Z80 this is a single instruction: `LD A, (HL)` - load the accumulator from the address pointed to by the HL register pair. There is no algorithm. There is no transformation. There is only a lookup.

This is the reason ZOSCII is correctly described as **encoding** and not **encryption**. Encryption implies a reversible algorithmic transformation - a process you apply to plaintext to produce ciphertext, and then reverse with a key to recover the plaintext. ZOSCII does no such thing. It maps each byte of input to a randomly selected address in the ROM that happens to hold that byte value. The ROM is the table. The output is an address, not a transformed value. There is no algorithm, no key schedule, no cipher rounds, no mathematical structure to attack. You cannot "decrypt" ZOSCII output because there is nothing to reverse - without the identical ROM, the output is statistically indistinguishable from random data with no recoverable relationship to the input.

Unlike OTP, ZOSCII does not require the key (ROM) to be as long as the message, does not prohibit reuse, and requires no synchronisation - because the ROM is not XORed against fixed positions. Random selection of which ROM address to use for each byte is what creates the independence. This is expressed as `I(M;A) = 0`: the mutual information between message and address stream is zero. The same Shannon guarantee, through a completely different and more practical mechanism.

### UNSIGNAL Protocol
UNSIGNAL is an advanced ZOSCII implementation designed to neutralise pattern recognition and heuristic analysis. Its name reflects its intent: a signal that carries no exploitable signal. Even with the same ROM and the same message, UNSIGNAL will never produce the same output twice.

But isn't ZOSCII already perfect secrecy? Yes. But if you have inside information to the fact that some data is encoded you could guess the first character in a file - you cannot prove it ever though. UNSIGNAL goes further to the point that even the first useful byte is totally meaningless - even though we tell you the first 4 bytes below.

It achieves this through a randomised obfuscation layer built on top of ZOSCII. Every UNSIGNAL file begins with a 4-address header. The first two addresses point to random bytes in the ROM whose combined value defines a 16-bit offset that shifts the logical start of the ROM for that session. The third and fourth addresses point to ROM bytes whose values determine the length of a random prefix and suffix appended around the encoded data. The actual message addresses are sandwiched between this noise - the decoder strips the header, calculates the prefix and suffix lengths from the ROM, discards those bytes, and decodes the remainder with standard ZOSCII.

The result is that an observer seeing two UNSIGNAL files encoded from the same message with the same ROM cannot determine they are the same message, cannot identify the file boundaries of the encoded data, and cannot distinguish the file from random bytes. UNSIGNAL is released under the UNINTELLIGENCE SOFTWARE LICENSE v1.1 - see the License for permitted and prohibited uses.

### Perfect Past Security
With conventional encryption, your data sits on disk as ciphertext - scrambled, but theoretically decryptable if a future algorithm or quantum computer breaks the cipher. The information is locked, but it is still there, waiting.

ZOSCII works differently. Decode your files, then delete the ROM. Those files are now gone - permanently, provably, for everyone. Not "we can't decrypt them right now" - the information no longer exists in any recoverable form. No future quantum computer changes this. No mathematical breakthrough changes this. The addresses that remain on disk are pure noise with zero information content. This is something most encryption systems fundamentally cannot offer: not just strong security going forward, but provable destruction of the past.

### Public Storage - Secure Forever
You can take a ZOSCII-encoded file and host it publicly on the internet, forever, and it will remain completely and provably unknown to everyone without the ROM. Not hidden behind access controls - publicly accessible. Anyone can download it, run every algorithm ever invented against it, throw unlimited computing resources at it. Without the ROM, they get nothing, because there is nothing to get. The information does not exist in the file. It only comes into existence when the correct ROM is applied. This means ZOSCII-encoded data can be stored in hostile environments, on public servers, or in untrusted infrastructure with no reduction in security whatsoever. In fact, this is a property along with Perfect Past Security which is used by ZOSCII Trumpet Blower (whistleblowing platform) - let everyone replicate it, when it is time to whistleblow - release the ROM, if you change your mind, delete the ROM - and everyone has useless noise. ZOSCII Trumpet Blower integration is planned for some editions of the Nuclear Tight Security suite.

### Weaponized Ambiguity
Before an adversary can attempt to break ZOSCII, they first need to know they are looking at ZOSCII. They cannot. ZOSCII-encoded data has no signature, no header, no identifying markers, no statistical patterns. To any observer - even one with unlimited computing power - it is indistinguishable from random noise, encrypted data, a compressed archive, a corrupted file, or an unknown binary format. There is no algorithmic fingerprint to detect because there is no algorithm. This weaponized ambiguity is not a feature bolted on - it is inherent to the construction. An adversary cannot even confirm that a message exists, let alone attempt to read it. But beware, if you want your phone number secret, don't call your file "My Phone Number is 12345678.txt". If you don't care that people know what is inside, then simply call the file "My Phone Number.txt" (it is secure!). They still won't know it is ZOSCII or UNSIGNAL Protocol unless you advertise that fact. Addresses 1,2,3,3,4 can mean anything - in fact 4 Billion possibilities - perhaps 11111, 22222, 33333, 44444, Hello, Yabby, Yadda, aaaaa... etc.

### Plausible Deniability
With encryption, when brute force finds a key that produces valid plaintext, the adversary knows they have found the answer - ciphertext decrypts deterministically to one specific result. ZOSCII has no such property. Because encoding is non-deterministic, different ROMs will decode the same address sequence into different, equally plausible messages. There is no correct answer to verify against, no checksum, no validation, no way to distinguish the real plaintext from any other valid interpretation. This is not a clever trick - it is the mathematical proof of information-theoretic security. If multiple messages are equally likely given everything an adversary can observe, the adversary has gained zero information. The same address sequence can legitimately decode to completely different content depending on which ROM is applied, and no ROM can be proven more "correct" than another without independent knowledge of which ROM was used.

---

## Trust
This software contains no backdoors, we do not phone home and we do not capture your ROMs. We cannot guarantee the OS you are using however has the same guarantees.

- [User Guidelines](https://github.com/PrimalNinja/cyborgzoscii/blob/master/docs/user-guidelines.md)
- [Securing Your PC Running Windows 10+](https://github.com/PrimalNinja/cyborgzoscii/blob/master/docs/securing-pc.md)

---

## About Cyborg Unicorn Pty Ltd
Cyborg Unicorn Pty Ltd is an Australian software company focused on low-level systems engineering, information-theoretic security, and high-performance computing. We build tools that respect the intelligence of the people who use them - tools that are grounded in real computer science rather than marketing.

Website: [cyborgunicorn.com.au](https://cyborgunicorn.com.au)
WhatsApp support channel: CyborgUnicorn

### Nuclear Tight Security Suite
The product you are currently using. A file encoding suite built on ZOSCII and the UNSIGNAL Protocol, providing information-theoretic security for individuals, companies, and organisations. Available in Neptune (free), Jupiter (consumer), Saturn (corporate), and Uranus (government/defence) editions.

### ZOSCII - Zero Overhead Secure Code Information Interchange
The encoding protocol at the heart of this product. ZOSCII originated from NTC (Native Threaded Code) work on Z80 systems, where the goal was to make text output faster by replacing computed transformations with direct table lookups. That same principle - a single `LD A, (HL)` instruction, no algorithm, just an address lookup - became the foundation of an information-theoretically secure encoding system. ZOSCII achieves the same `I = 0` Shannon guarantee as the One-Time Pad and Shamir's Secret Sharing, but without their practical restrictions: the ROM is reusable, requires no length matching to the message, and needs no synchronisation.

- ZOSCII website: [zoscii.com](https://zoscii.com)
- ZOSCII Foundation: [zoscii.com/foundation.html](https://zoscii.com/foundation.html)
- ZOSCII MQ: [zoscii.com/zosciimq](https://zoscii.com/zosciimq/)
- ZOSCII Trumpet Blower: [zoscii.com/zosciitrumpetblower](https://zoscii.com/zosciitrumpetblower/readme.html)
- ZOSCII MIT repository: [github.com/PrimalNinja/cyborgzoscii](https://github.com/PrimalNinja/cyborgzoscii)
- ZOSCII UNINTELLIGENCE License repository: [github.com/PrimalNinja/cyborgzoscii-u](https://github.com/PrimalNinja/cyborgzoscii-u)

### NTC - Native Threaded Code
The origin of ZOSCII. NTC is a compiler technique for Z80 systems that uses the hardware stack pointer as an instruction pointer, eliminating the CALL/RET overhead of conventional high-level language implementations and achieving 46–64% reduction in execution overhead. The same table-lookup insight that made NTC fast for text output became the basis for ZOSCII encoding.

NTC repository: [github.com/PrimalNinja/ntc](https://github.com/PrimalNinja/ntc)

### Cyborg Designer
A visual design tool built by Cyborg Unicorn, MIT licensed.

Repository: [github.com/PrimalNinja/cyborgdesigner](https://github.com/PrimalNinja/cyborgdesigner)

### Cuboids - Tri-Sword Framework
A high-performance GPU computing framework implementing the Tri-Sword architecture: Memory Sovereignty, Logic & Arithmetic Primitives, and Pipeline Orchestration. Achieves breakthrough performance on constrained hardware by treating the GPU as a sovereign Turing-complete platform rather than a peripheral device. MIT licensed.

Repository: [github.com/PrimalNinja/cuboids](https://github.com/PrimalNinja/cuboids)

---

*Cyborg Unicorn Pty Ltd © 2026. All rights reserved.*