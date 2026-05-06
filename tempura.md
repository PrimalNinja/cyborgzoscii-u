# TEMPURA Protocol Specification
**Threshold Endpoint Mesh Protocol Using Randomised Addressing**
**Author:** Julian Cassin  
**Date:** 2026-05-06

## SOFTWARE LICENSE v1.1
TEMPURA Protocol is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

---

### 1. OVERVIEW

TEMPURA is a fault-tolerant, information-theoretically secure web application delivery protocol built on the UNSIGNAL Protocol and the PENTAGONE split mechanism. It enables rich web applications to be delivered 100% securely over standard HTTP — no HTTPS required — within an organisational trust model.

TEMPURA achieves security not through encryption or ciphers, but through UNSIGNAL's randomised addressing and the mathematical properties of combinatorial threshold distribution. The payload delivered to any single server is provably meaningless without the client-held ROM. I(M;A)=0.

**Note:** "100% Secure" refers to the encoding layer. Human factors — coercion, dishonesty, physical compromise — are outside the mathematical security model and must be addressed operationally.

---

### 2. ARCHITECTURE

TEMPURA distributes an UNSIGNAL-encoded application payload across **5 servers** using a **3-of-5 threshold** scheme derived from C(5,3)=10 combinatorial patterns.

#### 2.1 Threshold Model

| Property | Value |
|----------|-------|
| Total Servers | 5 |
| Threshold (minimum to serve) | 3 |
| Maximum servers offline | 2 |
| Maximum servers compromised | 2 |
| Combinatorial patterns | 10 (all C(5,3) combinations) |

- If any **3 of 5** servers are online, the application serves correctly.
- If any **3 of 5** servers are uncompromised, the application serves securely.
- A server holding its share has zero knowledge of the payload content.
- No server operator can read their share — the ROM never leaves the client.

#### 2.2 Server Roles

Servers in a TEMPURA mesh are deliberately dumb. They hold bytes. They do not know:
- What protocol produced the bytes
- What the bytes represent
- Who holds the other shares
- How many total servers exist in the mesh

Any server type is valid: web servers, application servers, ZOSCII MQ queues, or static hosts.

#### 2.3 ZOSCII MQ Integration

TEMPURA optionally integrates with **ZOSCII MQ** as a webservice layer. In this mode:

- Responses are fetched from a **different queue** to the request — every time.
- No persistent request/response mapping exists for an adversary to correlate.
- The queue used for each response is non-deterministic from an external observer's perspective.

---

### 3. SPLIT MECHANISM

TEMPURA uses a static 10-row pattern table derived from all C(5,3)=10 combinations of 5 servers taken 3 at a time. This is the PENTAGONE splitting mechanism.

#### 3.1 The 10 Combinatorial Patterns

| Pattern | Servers |
|---------|---------|
| 1 | 1, 2, 3 |
| 2 | 1, 2, 4 |
| 3 | 1, 2, 5 |
| 4 | 1, 3, 4 |
| 5 | 1, 3, 5 |
| 6 | 1, 4, 5 |
| 7 | 2, 3, 4 |
| 8 | 2, 3, 5 |
| 9 | 2, 4, 5 |
| 10 | 3, 4, 5 |

Each byte of the UNSIGNAL-encoded payload is distributed across 3 of the 5 servers according to the pattern for that byte position. Any 3 available servers will satisfy at least one complete pattern, enabling full reconstruction.

#### 3.2 Share Properties

- Each server holds approximately **60% of the total payload bytes** (3/5 of all positions)
- No server holds a complete payload
- No subset of 2 servers holds a complete payload
- Any subset of 3 servers holds at least one complete pattern set

---

### 4. ENCODING PIPELINE

TEMPURA payload delivery follows this pipeline:

```
[Application Source]
        ↓
[UNSIGNAL Encoding] — randomised ROM offsets, prefix/suffix noise
        ↓
[PENTAGONE Split] — C(5,3) combinatorial distribution into 5 shares
        ↓
[Share Distribution] — one share per server
        ↓
[Client Request] — fetches from available servers (minimum 3) - try 3, if fails try another up to 5
        ↓
[Reconstruction] — combines shares, CRC validates, retries with new combination as required
        ↓
[UNSIGNAL Decode] — ROM held by client, never transmitted
        ↓
[Application Rendered]
```

---

### 5. SECURITY MODEL

#### 5.1 Information-Theoretic Security

TEMPURA inherits UNSIGNAL's I(M;A)=0 guarantee. An adversary intercepting traffic from any server or any combination of up to 2 servers gains zero mathematical information about the payload.

#### 5.2 No Transport Security Dependency

TEMPURA does not require HTTPS or TLS. The security model is independent of transport layer — an adversary with full visibility of all HTTP traffic between client and all 5 servers still cannot reconstruct the payload without the client-held ROM.

#### 5.3 Corruption Detection

The client holds a CRC of the expected reconstructed payload. On reconstruction:
1. Client attempts reconstruction from available server combinations
2. CRC is checked against each reconstruction attempt
3. Corrupt combination is identified by process of elimination
4. Clean reconstruction is confirmed without server cooperation

#### 5.4 Threat Model Boundary

| Threat | TEMPURA Response |
|--------|-----------------|
| Network interception (all servers) | I(M;A)=0 — no information without ROM |
| Server compromise (up to 2) | Threshold maintained — no payload exposure |
| Server offline (up to 2) | Threshold maintained — application continues |
| ROM compromise | Outside encoding scope — ROM security is operational |
| Human factors (coercion, dishonesty) | Outside mathematical scope — operational controls required — can torture up to 2 server operators |

---

### 6. DEPLOYMENT MODEL

#### 6.1 Trust Models

TEMPURA is designed for deployment within an **organisational trust model** — parties that have an established relationship and can distribute ROMs via secure out-of-band channels. This is consistent with UNSIGNAL's design scope. A **zero trust model** (trust yourself only) is also possible for applications which don't require server knowledge of payloads.

#### 6.2 Client Requirements

- Holds the ROM (never transmitted, never touches a server)
- Capable of fetching from a minimum of 3 of 5 servers
- Capable of CRC validation of reconstructed payload
- Capable of UNSIGNAL decoding

#### 6.3 Server Requirements

- Serves static byte shares over HTTP
- No cryptographic capability required
- No knowledge of other servers required
- No knowledge of the ROM required

---

### 7. ROLLING UPDATES

Because TEMPURA requires only 3 of 5 servers to serve correctly, application updates can be deployed progressively with minimal or no downtime:

1. Update the payload share on 2 servers — the application continues to serve from the remaining 3 unmodified servers
2. Once the 3rd server receives its updated share, the new version becomes the active payload
3. The remaining 2 servers can then be updated at any time

This means a new application version goes live the moment the 3rd updated server comes online — no coordinated cutover, minimal or no downtime, no rollback complexity. The threshold itself is the deployment gate.

**Note:** For stateless applications with no external dependencies, downtime is zero. Applications with database schema changes, API version dependencies, or shared server-side state may require coordination to manage the transition window where version X and version Y are simultaneously active across the mesh.

---

### 8. CONCLUSION

TEMPURA delivers web applications with greater resilience and stronger security than any current HTTPS-based web architecture:

| Property | HTTPS / TLS | TEMPURA |
|----------|-------------|---------|
| Security model | Computational | Information-Theoretic |
| Quantum resistance | Dependent on algorithm | I(M;A)=0 — immune by definition |
| Server compromise tolerance | None | Up to 2 of 5 servers |
| Server offline tolerance | None | Up to 2 of 5 servers |
| Transport dependency | Requires TLS | Plain HTTP sufficient |
| Server knowledge of payload | Full (decryptable) | Zero |
| "Harvest Now, Decrypt Later" | Vulnerable | Nothing to harvest |

TEMPURA makes the notion of "securing the transport layer" obsolete. When the payload itself is mathematically inert, the transport is irrelevant.

Commercial Licenses available from Cyborg Unicorn / ZOSCII Foundation.