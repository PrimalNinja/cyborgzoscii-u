# Cyborg Neptune Nuclear Tight Security

This is a closed‑source, free software tool for Windows that demonstrates the power of the UNSIGNAL Protocol and ZOSCII encoding. It provides information‑theoretic security for your files.

## Release Candidate 8
This software is currently RC8 - Release Candidate 8. The core encoding and decoding functionality is complete and the information-theoretic security properties are fully intact, but this is an early release and we are actively taking feedback. If you encounter any bugs or unexpected behaviour please let us know via the WhatsApp support channel: **CyborgUnicorn**.

## Installation
To install Cyborg Neptune Nuclear Tight Security, you will need to install CyborgNeptuneNTS-Setup.exe if you want to use either the data concealment or the secure chat. CyborgNeptuneNTSChat-Setup.exe is required in addition if you wish to use secure chat. 

The reason for separation is that the data concealment portion has no network activity at all - if you see any, you may have been exploited - report it to us.  Chat does require network activity and is secured separately from the core program - read the instructions below for more information how it does this with the chat ROMs.

## Quick Start (5 minutes)
1. Install CyborgNeptuneNTS-Setup.exe
2. Launch the application
3. Drag a JPEG photo onto the login screen (this becomes your ROM)
4. Click "Continue"
5. You're ready to encode files or set up secure chat

For chat:
6. Install CyborgNeptuneNTSChat-Setup.exe (separate installer)
7. In NTS, go to Contacts → Contact ROMs → select 1-3 ROMs
8. Create contacts with their published ROMs
9. Click Publish
10. Launch the Chat program with your chat ROMs

## Key Files
The Nuclear Tight Security (NTS) suite now uses keyfiles as a licensing and integrity check. Embedded within the application is the encoded keyfile.rom which has a compatible signature. This keyfile is created by UNSIGNAL encoding the JPEG with itself. Because it is non-deterministic encoding, in "non-free" versions of NTS, it isn't possible to fake a signature for the customer's keyfile.rom, for which they solely possess. Cyborg Neptune Nuclear Tight Security is the only version distributed with the actual keyfile.rom - it is not used for the security of data at all; it is purely for license and program integrity.

## Languages
Software is localised in English, Bahasa Indonesian, Chinese (Simplified), Czech, Dutch, French, German, Greek, Hausa, Hindi, Igbo, Italian, Japanese, Korean, Polish, Portuguese, Romanian, Russian, Spanish, Swahili, Swedish, Tagalog, Thai, Turkish, Ukrainian, Vietnamese and Yoruba.

## License

This tool is free for **PERMITTED USERS** under the terms of the **UNINTELLIGENCE SOFTWARE LICENSE v1.1**. Please see the **License Agreement** section within the application for full details on permitted users (individuals, private commercial entities, NGOs, etc.) and prohibited users (government intelligence, military, law enforcement, etc.) - Prohibited users (government intelligence agencies, military organizations, defense contractors, law enforcement, and mass surveillance entities) are not authorized to use this free version. Such entities may contact Cyborg Unicorn for commercial licensing options.

## Getting Started

### Login
To login, click the login screen or drag and drop a ROM file onto it. This ROM becomes your **resident ROM** and is held in memory only - it is never written to disk. Without a valid resident ROM, no encoding or decoding can occur.

You may load up to three ROMs (active.rom, active2.rom, active3.rom) for enhanced security. These are applied in sequence during encoding and decoding operations.

### Multi-ROM Security Layers
The UNSIGNAL protocol supports layered security using one, two, or three ROMs. Each additional ROM exponentially increases the search space an attacker must explore:

* **1 ROM (128KB minimum)** - Creates 65,536 "safes" (possible offset positions) each containing unknown data. An attacker would need to try each possible offset to decode a single file correctly.
* **2 ROMs** - First ROM encodes into a temporary file, second ROM encodes again. This creates 65,536 × 65,536 ≈ 4.3 billion possible combinations. File size expands by approximately 4×.
* **3 ROMs** - Triple-layered encoding creates 65,536³ ≈ 281 trillion possible combinations. File size expands by approximately 8×.

**Storage Consideration:** Each additional ROM layer multiplies the output file size (~2× for 1 ROM, ~4× for 2 ROMs, ~8× for 3 ROMs). To reduce storage requirements, compress your data before encoding - compressible data will yield smaller encoded files while maintaining the same security properties.

**Important:** All files encoded with any ROM combination are *valid decodings* to an attacker and to the system. The system cannot and does not verify whether a decoded file is "correct" - it is the user's responsibility to use the correct ROMs in the correct order. The wrong ROM combination will produce garbage output (which could be a plausible alternative decoding).

### ROM Management
Use the **ROMs** tile to manage your ROM collection. ROMs are stored encoded using your resident ROM. Only the correct resident ROM can decode and display them.

* **Activate (ROM 1 / 2 / 3)** - Decodes a stored ROM and saves it as active.rom, active2.rom, or active3.rom for use with the Encode and Decode tools
* **Deactivate All** - Securely deletes all active ROM files from disk
* **Add ROM** - Encodes a new ROM image using your resident ROM and adds it to the collection
* **Delete ROM** - Securely deletes a stored ROM

**ROM Selection Order:** When using multiple ROMs, they are applied in sequence: ROM 1 first, then ROM 2, then ROM 3. For decoding, the reverse order is used (ROM 3, then ROM 2, then ROM 1). Using ROMs in the wrong order will produce garbage output.

### The `active.rom` File - Security Notice
When you activate ROMs from the ROMs explorer, the system decodes them and writes the results to files called active.rom, active2.rom, and active3.rom in your data folder. These files contain the fully decoded ROM images, which are indexed to build the address tables used by the Explorer menu Encode and Decode tools.

**Important: active.rom, active2.rom, and active3.rom are NOT secured while they are active.** By design, they must exist in decoded form on disk so that the Explorer menu Encode and Decode tools can use them. You should be aware that while these files exist on disk they are readable. Use the **Deactivate All** function (or the Deactivate buttons in the Status screen) to securely delete active ROM files when you are finished with them. The resident ROM(s) you log in with are entirely separate - they are held only in memory and are never written to disk.

### Basic Encode / Decode
Use these to encode or decode any file. Basic Encode and Basic Decode use your **resident ROM(s)** - the ROM(s) you logged in with, which are held in memory only and never touch disk. The ROMs are indexed to build the address tables used for encoding and decoding. These operations do not use active.rom, active2.rom, or active3.rom.

### Detailed ROM Analysis
Provides a detailed ROM analysis of its entropy to ensure that the ROM file you chose is up for the job. Colour JPEG files make really good candidates and we recommend you use them for ease of identification.

### Data Protection
Store sensitive information in GUID-based folders organised by type (e.g. Bank Accounts, Passwords). Be sure to backup your %%PRODUCTNAME%% data folder which should be located in your c:\ drive.

### Secure Delete
Overwrites a file with 0xFF then 0x00 before deleting. Note: on SSD/flash or even modern hard drive storage, secure delete is best-effort due to wear leveling.

### Status
View the currently active ROM images. If no ROMs are activated, or an active ROM is not a valid image, "No Active ROM" is shown.

---

## Secure Chat

The Chat system provides quantum-proof, information-theoretically secure messaging between contacts. Each message is encoded using the UNSIGNAL Protocol with the recipient's published ROM, ensuring that only the intended recipient (or recipients) can decode and read the message.

### Preparing for Chat - Selecting Your Chat ROMs
Before you can publish any contacts for chat, you must first select the ROMs that will secure your Chat program. The Chat program requires up to three ROMs - these are the ROMs you will drag onto the Chat login screen to access your secure messages.

To select your Chat ROMs:
1. In the Nuclear Tight Security (NTS) program, open the **Contacts** section
2. Use the **Contact ROMs** operation to choose up to three ROMs that will secure your Chat identity
3. These ROMs are encoded and stored with your contact information - they will be required to log into the Chat program

### Publishing Contacts from Nuclear Tight Security
Once your Chat ROMs are selected, you can publish contacts for use in the Chat program. Each contact has exactly one ROM - the ROM file you select in the contact form as that contact's published ROM. This ROM will be used to encode messages sent to that contact.

To publish contacts for Chat:
1. Open the **Contacts** section in NTS
2. Create or edit a contact entry
3. In the contact form, select the **ROM File** that will serve as that contact's published ROM - this ROM encodes messages to this contact
4. Save the contact
5. Use the **Publish** operation - this publishes ALL publishable contacts (plus their associated ROMs) to a secured format that only the Chat program can access

The published contacts and their ROMs are stored in UNSIGNAL Protocol format in the **published\contacts** and **published\roms** folders. Using different login ROMs for chat access gives you more security than using the same ROMs you did for NTS.

### Logging into the Chat Program
To access your secure messages, launch the Chat program and drag the same up to three ROMs you selected in NTS onto the login screen. The Chat program validates these ROMs against the published contact signatures and loads them as its resident ROMs for the session. Without the correct ROM combination, you cannot access any published contacts or decode any messages.

### How It Works
Each queue is associated with a specific contact. The contact's published ROM (selected in NTS) is used to encode all outgoing messages to that queue. The same ROM is used by the recipient to decode incoming messages. Without the correct contact ROM, messages are indistinguishable from random noise - even with unlimited computing power.

### Queues
Queues represent communication channels with specific contacts. Each queue stores:
* A server URL where messages are hosted
* The associated contact and their published ROM
* A local archive of sent and received messages

### Adding a Queue
To start communicating with a contact, add a queue using the **Add Queue** operation. You will need:
* A queue name (your local identifier for this channel)
* The server URL where messages will be stored
* The contact you wish to communicate with (selected from your published contacts list - these were published from NTS)

### Sending Messages
Once a queue is created, you can send messages of any length. Each message is encoded using the contact's ROM before being transmitted to the server. The recipient's queue will automatically fetch and decode new messages using the same ROM. Only someone with access to both the server and the contact's ROM can decode the message.

### Fetching Messages
Use the **Check** operation to see if new messages are available, or **Fetch** to download all pending messages. Fetched messages are stored locally, encoded with the contact's ROM. They remain secure on disk - only the correct resident ROM (the contact's published ROM) can decode them for viewing.

### Sending Files
Files can be sent through the chat system using the **Send File** button. Files are encoded using the same UNSIGNAL Protocol as text messages and are retrieved by the recipient via the Fetch operation. Exported files are decoded to their original form.

### Important Notes
**If you lose any of your ROMs required to log into the Chat program, you lose access to ALL your contacts and ALL your messages - past, present, and future.** There is no recovery. Back up these ROMs securely, on multiple devices, in multiple locations.

**If you lose a contact's published ROM, you lose the ability to communicate with that contact.** The Chat program cannot recover it - the ROM must be re-shared through a secure out-of-band channel. You can safely send encoded ROMs via chat however as they will be encoded with UNSIGNAL Protocol.

The chat server stores only encoded messages. The server operator cannot read your messages, cannot determine who is communicating with whom beyond the queue structure, and cannot determine the content of any message without the corresponding contact ROM.

### Setting Up Your Own Personal or Corporate Chat Server
The chat server is ZOSCII MQ which is a B2B messaging queue which supports Quantum Proof delivery of messages between parties. More information about ZOSCII MQ is available here: https://zoscii.com/zosciimq/ and the GitHub repository to download ZOSCII MQ is located here: https://github.com/PrimalNinja/cyborgzoscii/tree/master/zosciimq - ZOSCII MQ is released under MIT License.

---

## TrumpetBlower (Whistleblower Submission)

TrumpetBlower is a specialised queue type in the Nuclear Tight Security Chat system designed for secure, anonymous submission of sensitive information. It leverages the information-theoretic properties of the UNSIGNAL Protocol to provide perfect past security and plausible deniability for whistleblowers.

Read more about TrumpetBlower at https://zosciitrumpetblower.com/ before using this feature.

### How TrumpetBlower Works

1. **Public Queue** - TrumpetBlower queues are configured as "TRUMPET" type queues. Anyone can submit messages to these queues, but only those with the correct contact ROM can read them.

2. **ROM as Access Control** - Each TrumpetBlower queue is associated with a contact who holds the published ROM. Without this ROM, the queue contents are indistinguishable from random noise.

3. **Perfect Past Security** - If the ROM is deleted, every message ever submitted becomes permanently unrecoverable - not "can't decrypt right now", but provably gone forever. No future technology can recover them.

4. **Plausible Deniability** - Because encoding is non-deterministic, the same encoded messages could decode to completely different content with a different ROM. An adversary cannot prove what the "correct" decoding is.

### Creating a TrumpetBlower Queue

1. Open the **Queues** section in NTS Chat
2. Click **Add Queue**
3. Set **Queue Type** to "TRUMPET"
4. Select the contact who will receive submissions (this contact's ROM will be used to decode incoming messages)
5. Configure the server URL where messages will be hosted
6. Save the queue

### Submitting to TrumpetBlower

Once a TrumpetBlower queue is created, anyone with access to the queue can submit messages:

1. Select the TrumpetBlower queue
2. Type a message or attach a file
3. Click **Send** - messages are encoded using the contact's ROM before transmission

The recipient can fetch and decode messages using their ROM. No one else - including the server operator - can read them.

### Public Storage - Secure Forever

TrumpetBlower submissions can be hosted on public servers, replicated across multiple sites, and stored indefinitely. Without the ROM, the files contain zero information. When a whistleblower is ready to reveal the truth, they release the ROM. If they change their mind, they delete the ROM - and everyone is left holding useless noise.

### Use Cases

- **Journalism** - News organisations can publish a TrumpetBlower queue URL. Sources submit documents anonymously. Only the news organisation can read them.
- **Corporate Compliance** - Employees can submit concerns without fear of retaliation.
- **Government Oversight** - Citizens can report misconduct to oversight bodies.
- **Academic Research** - Researchers can collect sensitive survey data without compromising participant privacy.

---

## ZOSCII MQ Radio (Music/Media Broadcasting)

ZOSCII MQ Radio is a specialised queue type designed for broadcasting music, images, and text messages to subscribers. 

### How Radio Works

1. **Music Queue** - Radio queues are configured as "MUSIC" type queues
2. **Sequential Playback** - Subscribers fetch messages in order, creating a playlist experience
3. **Automatic Decoding** - Each subscriber's client decodes content using the contact's published ROM
4. **Streaming Playback** - Content is played directly from the server fetch response - not cached.

### Message Types Supported

| Type | Description | Display |
|------|-------------|---------|
| MP3 | Audio files | Plays in the built-in player with oscilloscope visualisation |
| JPG | Images | Displays with automatic slideshow timing |
| Text | Plain text files | Shows with monospace formatting, scrollable |

### Creating a Radio Channel

1. Open the **Queues** section in NTS Chat
2. Click **Add Queue**
3. Set **Queue Type** to "MUSIC"
4. Select the contact who will publish content (this contact's ROM will encode all broadcasts)
5. Configure the server URL where content will be hosted
6. Save the queue

### Currently Known Radio Servers / Channels:

#### Provider: Cyborg Unicorn Web Radio

Server:   https://cyborgunicorn.com.au/radio/indexmq.php
Channels: cyborgunicorn, marketing, zosciitech
ROM File: https://cyborgunicorn.com.au/radio/logo.png

### Listening to a Radio Channel

1. Open the **Radio** section from the main menu
2. Select a channel from the list
3. Click **Play** or double-click the channel
4. The Radio Player will open with:

   - **Behavior selector** - Choose playback behavior (repeat channel, repeat all channels, next channel, wait at end, stop at end, random channel)
   - **Progress bar** - Click anywhere to seek within the current track
   - **Oscilloscope** - Real-time audio visualisation
   - **Now Playing** - Track information (reads ID3 tags from MP3s)
   - **Image display** - Shows JPG files sent to the channel
   - **Text display** - Shows text files sent to the channel

### Playback Behaviors

| Behavior | Description |
|----------|-------------|
| Repeat Channel | Loops the current channel continuously |
| Repeat All Channels | Cycles through all channels, repeating |
| Next Channel | Moves to next channel at end of queue |
| Wait at End | Pauses and waits 30 seconds before checking again |
| Stop at End | Stops playback when queue ends |
| Random Channel | Jumps to random channel at end of queue |

### Auto-Hide Player

The Radio Player auto-hides to the right edge of the screen:
- **Show** - Move mouse to the right edge of the screen
- **Hide** - Move mouse away from the player
- **Delay** - 0.5 seconds before hiding

This allows the player to stay visible while you interact with other parts of the application.

### History Navigation

- **Next** - Skip to the next track in the queue
- **Previous** - Go back to the previous music track (images and text are not added to history)
- The player tracks up to 50 music tracks in history

### Setting Up Your Own Radio Server

Radio channels use the same ZOSCII MQ backend as chat queues:
1. Deploy ZOSCII MQ on your server (MIT License)
2. Create a queue in the MQ using standard queue creation
3. Configure NTS Chat with the server URL and queue GUID
4. Publish your contact ROM to subscribers

Repository: https://github.com/PrimalNinja/cyborgzoscii/tree/master/zosciimq

### Example Use Cases

- **Community Radio** - Local music and announcements
- **Podcast Distribution** - Secure, DRM-free podcast delivery
- **Corporate Communications** - Internal announcements with images
- **Emergency Broadcasts** - Critical information delivery
- **Art Exhibitions** - Curated image slideshows with text commentary
- **Language Learning** - Audio lessons with accompanying text

---

## Technical Notes

### TrumpetBlower Security Properties

- **Information-theoretic security** - Messages contain zero recoverable information without the ROM encoded with 1 to 3 layers of UNSIGNAL Protocol.
- **Perfect past security** - Deleting the ROM permanently destroys all messages
- **Plausible deniability** - Any ROM can produce a valid decoding
- **No metadata leakage** - Server sees only random noise
- **Public hosting safe** - Files can be stored anywhere without risk

### Publishing to a Radio Channel

Use the rudimentary publishing tool currently available on GitHub. For now it is recommended you publish to a local hosted copy of ZOSCII MQ and manually transfer to your server after you have organised your tracks, images and lyrics. The publishing tool and web-based player also in the GitHub repository can be used as reference code if you would like to develop your own tools.

Copyright warning: Follow the laws of the land you live in.

Repository: https://github.com/PrimalNinja/cyborgzoscii/tree/master/zosciimq

### Radio Player Requirements

- **MP3 playback** - Requires browser/OS support for MP3 audio
- **Oscilloscope** - Uses Web Audio API for real-time visualisation
- **Large file handling** - Tracks over 1MB are added to history for previous/next navigation
- **ID3 tags** - Reads ID3v1 tags for track information display
- **No local cache** - All content is streamed directly from server responses

### Performance Considerations

- **Bandwidth** - Large MP3 files will consume bandwidth on each playback
- **History** - Limited to 50 music tracks to prevent memory issues
- **Concurrent fetches** - Request queuing prevents C# bridge crashes on rapid navigation

---

## ⚠️ WARNING

**ZOSCII plus UNSIGNAL Protocol achieves perfect secrecy. If you lose your ROM files you lose your data - this is the dual-edged consequence of perfect secrecy. There is no getting your data back, ever, if you lose your ROM files. Back them up safely - on multiple devices, multiple locations, multiple times. Quantum Proof security has this consequence.**

---

## Information Theoretic Security

Information-theoretic security (ITS) is the gold standard of secrecy. Unlike computational security - which relies on the assumption that an adversary lacks the computing power to break a cipher within a practical timeframe - information-theoretic security is mathematically proven to be unbreakable regardless of how much computing power an attacker has, now or in the future. Even a computer of unlimited power cannot extract the plaintext without the key, because the information simply does not exist in the output.

The concept was formalised by Claude Shannon in his landmark 1949 paper "Communication Theory of Secrecy Systems," in which he proved that the one-time pad (OTP), when used correctly, achieves perfect secrecy. The OTP XORs each bit of the message against a corresponding bit of a truly random key of equal length, producing ciphertext that is statistically independent of the plaintext. Shannon expressed this as I(M;C) = 0 - the mutual information between message and ciphertext is zero. An attacker who intercepts the ciphertext learns nothing at all about the message.

OTP's restrictions follow directly from its construction: the key must be exactly as long as the message, perfectly random, and never reused. Reusing the key against a different message immediately leaks the XOR of the two messages. These constraints make OTP impractical for general use, which is why the field of computational cryptography - AES, RSA, ECC - developed as a pragmatic alternative, accepting weaker (computational rather than mathematical) security guarantees in exchange for practical key sizes.

A second, quite different ITS mechanism is **Shamir's Secret Sharing (SSS)** , introduced by Adi Shamir in 1979. SSS solves a different problem: splitting a secret among n parties such that any k of them can reconstruct it, while k-1 parties learn absolutely nothing. It achieves this through polynomial interpolation - the secret is encoded as the constant term of a random polynomial of degree k-1, and each party receives one point on that polynomial. With k-1 points, every possible secret value is equally consistent with the shares, so I(S ; any k-1 shares) = 0. This is the same Shannon guarantee - zero mutual information - but achieved through completely different mathematics: no XOR, no lookup, just the geometry of polynomials over a finite field. SSS appears in the security taxonomy for this product as a cryptography enabler. SSS is a recommendation for storing your Nuclear Tight Security ROMs in a secure way as an alternative or ideally in addition to your multiple backups.

ZOSCII and UNSIGNAL are a third, independent path to the same destination. Rather than relying on the computational hardness of factoring large numbers or solving elliptic curve problems - assumptions that may be undermined by quantum computing or future mathematical advances - they depend on information simply not being present in the output. The data is random pointers to your random ROM for every instance of every unknown character that an attacker might consider. Your ROM is your access, just as a private key is for most encryption systems.

### ZOSCII - Zero Overhead Secure Code Information Interchange
ZOSCII has its origins in NTC (Native Threaded Code), a compiler technique developed at Cyborg Unicorn to make text output faster on Z80-based systems. The key insight was the same one that underpins ZOSCII: rather than computing or transforming data character by character through a pipeline of instructions, simply look up the result directly in a table using the input as an address. On a Z80 this is a single instruction: `LD A, (HL)` - load the accumulator from the address pointed to by the HL register pair. There is no algorithm. There is no transformation. There is only a lookup.

This is the reason ZOSCII is correctly described as **encoding** and not **encryption**. Encryption implies a reversible algorithmic transformation - a process you apply to plaintext to produce ciphertext, and then reverse with a key to recover the plaintext. ZOSCII does no such thing. It maps each byte of input to a randomly selected address in the ROM that happens to hold that byte value. The ROM is the table. The output is an address, not a transformed value. There is no algorithm, no key schedule, no cipher rounds, no mathematical structure to attack. You cannot "decrypt" ZOSCII output because there is nothing to reverse - without the identical ROM, the output is statistically indistinguishable from random data with no recoverable relationship to the input.

Unlike OTP, ZOSCII does not require the key (ROM) to be as long as the message, does not prohibit reuse, and requires no synchronisation - because the ROM is not XORed against fixed positions. Random selection of which ROM address to use for each byte is what creates the independence. This is expressed as I(M;A) = 0: the mutual information between message and address stream is zero. The same Shannon guarantee, through a completely different and more practical mechanism.

### UNSIGNAL Protocol
UNSIGNAL is an advanced ZOSCII implementation designed to neutralise pattern recognition and heuristic analysis. Its name reflects its intent: a signal that carries no exploitable signal. Even with the same ROM and the same message, UNSIGNAL will never produce the same output twice.

But isn't ZOSCII already perfect secrecy? Yes. But if you have inside information to the fact that some data is encoded you could guess the first character in a file - you cannot prove it ever though. UNSIGNAL goes further to the point that even the first useful byte is totally meaningless - even though we tell you the first 4 bytes below.

It achieves this through a randomised obfuscation layer built on top of ZOSCII. Every UNSIGNAL file begins with a 4-address header. The first two addresses point to random bytes in the ROM whose combined value defines a 16-bit offset that shifts the logical start of the ROM for that session. The third and fourth addresses point to ROM bytes whose values determine the length of a random prefix and suffix appended around the encoded data. The actual message addresses are sandwiched between this noise - the decoder strips the header, calculates the prefix and suffix lengths from the ROM, discards those bytes, and decodes the remainder with standard ZOSCII.

The result is that an observer seeing two UNSIGNAL files encoded from the same message with the same ROM cannot determine they are the same message, cannot identify the file boundaries of the encoded data, and cannot distinguish the file from random bytes. UNSIGNAL is released under the UNINTELLIGENCE SOFTWARE LICENSE v1.1 - see the License for permitted and prohibited uses.

### Perfect Past Security
With conventional encryption, your data sits on disk as ciphertext - scrambled, but theoretically decryptable if a future algorithm or quantum computer breaks the cipher. The information is locked, but it is still there, waiting.

ZOSCII works differently. Decode your files, then delete the ROM. Those files are now gone - permanently, provably, for everyone. Not "we can't decrypt them right now" - the information no longer exists in any recoverable form. No future quantum computer changes this. No mathematical breakthrough changes this. The addresses that remain on disk are pure noise with zero information content. This is something most encryption systems fundamentally cannot offer: not just strong security going forward, but provable destruction of the past.

### Public Storage - Secure Forever
You can take a ZOSCII-encoded file and host it publicly on the internet, forever, and it will remain completely and provably unknown to everyone without the ROM. Not hidden behind access controls - publicly accessible. Anyone can download it, run every algorithm ever invented against it, throw unlimited computing resources at it. Without the ROM, they get nothing, because there is nothing to get. The information does not exist in the file. It only comes into existence when the correct ROM is applied. This means ZOSCII-encoded data can be stored in hostile environments, on public servers, or in untrusted infrastructure with no reduction in security whatsoever. In fact, this is a property along with Perfect Past Security which is used by ZOSCII TrumpetBlower (whistleblowing platform) - let everyone replicate it, when it is time to whistleblow - release the ROM, if you change your mind, delete the ROM - and everyone has useless noise. 

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
The encoding protocol at the heart of this product. ZOSCII originated from NTC (Native Threaded Code) work on Z80 systems, where the goal was to make text output faster by replacing computed transformations with direct table lookups. That same principle - a single `LD A, (HL)` instruction, no algorithm, just an address lookup - became the foundation of an information-theoretically secure encoding system. ZOSCII achieves the same I = 0 Shannon guarantee as the One-Time Pad and Shamir's Secret Sharing, but without their practical restrictions: the ROM is reusable, requires no length matching to the message, and needs no synchronisation.

- ZOSCII website: [zoscii.com](https://zoscii.com)
- ZOSCII mathematical proofs: https://github.com/PrimalNinja/cyborgzoscii/tree/master/proofs
- ZOSCII Foundation: [zoscii.com/foundation.html](https://zoscii.com/foundation.html)
- ZOSCII MQ: [zoscii.com/zosciimq](https://zoscii.com/zosciimq/)
- ZOSCII TrumpetBlower: [zoscii.com/zosciitrumpetblower](https://zoscii.com/zosciitrumpetblower/readme.html)
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