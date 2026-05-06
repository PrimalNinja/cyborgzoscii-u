# UNSIGNAL Protocol: Intelligence Neutralisation Analysis

*UNSIGNAL Protocol is part of the ZOSCII ecosystem*
*License: UNINTELLIGENCE SOFTWARE LICENSE v1.1*
*Copyright (c) 2026 Cyborg Unicorn Pty Ltd*

---

## Part I: Intelligence Surveillance Methods

### Bulk Collection ("Upstream" Surveillance)

Agencies like the NSA and GCHQ use **upstream collection** to tap directly into internet physical infrastructure (undersea fibre-optic cables).

| Programme | Agency | Method |
|-----------|--------|--------|
| TEMPORA | GCHQ | Taps undersea cables, buffers data for later analysis |
| UPSTREAM | NSA | Monitors massive amounts of traffic globally |
| XKeyscore | NSA | Holds "full take" of unfiltered internet content for 3-5 days, metadata for up to 30 days |

**Key Concepts:**
- **"Haystack" Approach:** Collect the "whole haystack" to ensure no valuable information is missed
- **Temporary Buffers:** Allow analysts to retrospectively search for data once a new target is identified

### Triggers and "Selectors"

To manage data volume, agencies use **selectors** (triggers) to decide what to permanently store.

| Selector Type | Description |
|---------------|-------------|
| **Specific Selectors** | Email addresses, phone numbers, IP addresses — if a communication contains a "tasked selector," it's harvested |
| **"About" Collection** | Messages not to/from a target, but mentioning a target's selector |
| **Activity Triggers** | Searching for privacy-enhancing tools (Tor) or reading technical journals can trigger tracking |

### Targeted ("Downstream") Harvesting

Once a target is established, agencies use **downstream collection** (formerly PRISM).

| Method | Description |
|--------|-------------|
| **Direct Access** | Agencies compel companies (Google, Microsoft, Yahoo) to provide all communications to/from a specific selector |
| **Mandatory Retention** | Countries like Australia require ISPs to store all citizens' metadata for two years |

### Surveillance Methods Summary

| Method | Audience | Mechanism | Data Kept |
|--------|----------|-----------|-----------|
| Upstream | Entire populations | Tapping cables/backbones | Temporary buffer, then triggers |
| Downstream | Specific targets | Compelling tech companies | Full history of that target |
| Metadata Retention | All citizens | Legal mandates on ISPs | Who/When/Where (no content) |

---

## Part II: UNSIGNAL Neutralisation of Surveillance Programmes

### TEMPORA & UPSTREAM (Bulk Cable Interception)

| Programme Tactic | UNSIGNAL Countermeasure |
|------------------|------------------------|
| Tap fibre-optic cables to buffer and search massive traffic flows | **Noise-Inertia:** Data is mathematically indistinguishable from random noise |
| Automated filters find "interesting" data (encrypted packets, keywords) | **Pattern Immunity:** Lacks digital "signal" or headers that trigger collection |
| Buffers data to be cracked later if target identified | **Retrospective Immunity:** No mathematical path to "crack" — even with quantum computers |
| Metadata mapping (who talks to whom) | **Metadata Elimination:** Removes identifiable cryptographic "handshakes" |

### PRISM (Server-Side Collection)

| Programme Tactic | UNSIGNAL Countermeasure |
|------------------|------------------------|
| NSA requests/direct access to data from Google, Microsoft, Apple | **End-to-End Neutralisation:** Servers only host neutralised noise |
| Legal compulsion to hand over data | **Zero-Knowledge:** Company literally does not possess a readable version of the message |

---

## Part III: Neutralisation of Other Intelligence Systems

### XKeyscore (Real-Time Search & Retrieval)

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| Searches and analyses global internet traffic in real-time using "soft selectors" (keywords, file types) | Removes all "magic bytes," headers, and identifiable structures |
| Automated filters flag traffic for keyword matches | Filters see data as "unstructured noise" — cannot trigger keyword or file-type detection |

### ECHELON (Global SIGINT Network)

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| Five Eyes global network intercepting satellite and radio communications | **Weaponized Ambiguity:** Variable prefix/suffix noise prevents metadata correlation |
| Monitors electromagnetic spectrum for specific transmission patterns | Timing and packet size cannot be correlated to specific user or intent |
| Builds social graphs and pattern-of-life analysis | Cannot build meaningful social graphs |

### Cognitive Electronic Warfare (AI Jammers & Sensors)

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| AI-driven electronic warfare instantly detects, identifies, and jams signals based on "electronic signature" | **Signal-Free Encoding:** Prevents AI from identifying distinct transmission "signature" |
| Autonomous jammers disrupt "hostile" or "interesting" traffic | Without recognizable signal to lock onto, autonomous jammers cannot effectively categorize or disrupt traffic |

### Deep Packet Inspection (DPI) Firewalls

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| National firewalls identify and block encrypted traffic (VPNs, Tor) based on handshake "signatures" | Mimics true random entropy — lacks standard "handshake" signature used by firewalls |
| Uses "entropy-based" blocking rules to target encrypted data | Bypasses entropy-based blocking rules |

### Biometric & Behavioral Analytics

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| Intelligence platforms track metadata over time to identify users without names | **PrivacyKey™:** Generates unique, ephemeral keys for every single event or transaction |
| Tracks biometric events and transaction patterns | No persistent "attack surface" exists for these analytics systems to track a user over time |

### SORM / SORM-3 — Russia (FSB Direct Tap)

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| FSB-controlled hardware installed directly at ISPs gives real-time tap access to all traffic — no warrant, no company intermediary | **Noise-Inertia:** Tapped traffic is mathematically indistinguishable from random noise — nothing to analyse in real time |
| SORM-3 (Yarovaya Law) mandates full content retention for 6 months, metadata for 3 years | **Retrospective Immunity:** Retained data contains no mathematical path to the original message, even with unlimited future compute |
| No requirement to notify the target or the ISP of what is being collected | **Pattern Immunity:** Absence of headers, handshakes, or selectors means automated collection triggers cannot fire |

### Golden Shield — China (Great Firewall DPI)

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| National DPI system actively blocks traffic by entropy signature, handshake pattern, and protocol fingerprint at scale | Mimics true random entropy — no protocol fingerprint or handshake signature present to match against blocking rules |
| Maintains whitelist/blacklist of known encrypted traffic signatures (VPN, Tor, TLS variants) | UNSIGNAL output has no known signature — cannot be classified as encrypted traffic or added to a blacklist |
| Deep inspection of packet payloads for keyword and content triggers | Signal-Free encoding removes all identifiable payload structure — keyword matching returns no results |

### GhostNet / MSS SIGINT Network — China

| System Tactic | UNSIGNAL Countermeasure |
|---------------|------------------------|
| Operates dozens of ground-based SIGINT stations monitoring satellite and international communications traffic across Asia-Pacific | **Noise-Inertia:** Intercepted traffic is indistinguishable from random noise — automated analysis yields nothing |
| Monitors international communications satellites from dedicated intercept facilities | **Pattern Immunity:** No frequency, timing, or structural patterns present for correlation across intercept points |
| Builds target social graphs by correlating metadata across multiple intercept points | **Metadata Elimination:** Absence of PKE handshakes removes the identifiable markers used to map communication relationships |

---

## Part IV: Intelligence Neutralisation Summary

| Intelligence System | Primary Surveillance Tactic | UNSIGNAL Neutralisation Strategy | Relevant Property |
|--------------------|----------------------------|--------------------------------|-------------------|
| **TEMPORA / UPSTREAM** | Bulk interception of fibre-optic cables; buffering traffic for future decryption | Renders data mathematically inert; no "cipher" to crack, even with infinite compute | Information-Theoretic Security (I(M;A)=0) |
| **PRISM** | Legal/direct access to stored data on big-tech servers | Ensures servers only host "neutralised" noise; provider holds no readable keys | End-to-End Neutralisation |
| **XKeyscore** | Real-time filtering via "soft selectors" (keywords, file headers, metadata) | Removes all "magic bytes" and predictable headers that trigger automated filters | Signal-Free Encoding |
| **ECHELON** | Global SIGINT monitoring for pattern-of-life analysis | Uses variable noise to mask transmission timing and packet sizes | Weaponized Ambiguity |
| **Deep Packet Inspection** | Blocking traffic based on "signatures" of encryption handshakes | Mimics true random entropy; lacks standard "handshake" signature | NoEyes Handshake |
| **Cognitive Electronic Warfare** | AI-driven sensors that identify and jam signals based on electronic signatures | Prevents AI models from identifying distinct signal "fingerprint" | Direct Memory Addressing (randomized ROM offsets) |
| **Behavioral Analytics** | Tracking metadata over time to build identity profiles | Generates unique, ephemeral keys for every single event or transaction | PrivacyKey™ |
| **SORM / SORM-3 (Russia)** | FSB direct tap at ISP level; full content retained 6 months, metadata 3 years | Retained traffic is mathematically inert — no path to original message regardless of retention period | Information-Theoretic Security (I(M;A)=0) |
| **Golden Shield / Great Firewall (China)** | National DPI blocking by entropy signature, protocol fingerprint, and keyword payload inspection | No fingerprint, no handshake, no keywords — cannot be classified, blocked, or matched | Signal-Free Encoding; NoEyes Handshake |
| **GhostNet / MSS SIGINT Network (China)** | Distributed ground stations intercept satellite and international traffic; correlates metadata to build social graphs | Intercepted traffic is noise; no PKE handshakes means no metadata markers to correlate | Noise-Inertia; Metadata Elimination |