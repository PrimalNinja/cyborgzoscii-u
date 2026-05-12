# ZOSCII NoEyes Handshake Protocol

**Author:** Julian Cassin  
**Date:** 2026-02-28

## SOFTWARE LICENSE v1.1

UNSIGNAL Protocol is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

## Overview

A trust establishment mechanism that uses existing Certificate Authorities (CAs) as disposable bootstrap facilitators, not ongoing security providers. Once the handshake completes, the CAs are permanently out of the loop.

## Architecture

### Components

- **microZOSCII**: 240-byte dynamically generated microROM
- **3 Independent Middlemen**: Certificate Authorities or equivalent trust brokers
- **2 Endpoints**: The communicating parties, client and server

### Phase 1: Link to Certificate Provider

You get a link from the certificate providers out of band, a generated 240-byte microZOSCII microROM. Each is a fully functional encoding mechanism — not a fragment, not a partial key.

### Phase 2: Bootstrap

Each of the 3 middlemen holds a complete, independently generated 240-byte microZOSCII microROM. Each is a fully functional encoding mechanism — not a fragment, not a partial key. This 240 byte microZOSCII microROM is delivered encoded in your out-of-band microROM for that provider.

### Phase 3: microROM Distribution

Each middleman dynamically generates a fresh 240-byte microZOSCII microROM for this session and provides it to both endpoints.

- Middleman A generates microROM A — knows only its own microROM
- Middleman B generates microROM B — knows only its own microROM
- Middleman C generates microROM C — knows only its own microROM
- Both endpoints receive all 3 microROMs

No middleman sees the other two microROMs. No middleman holds or generates the final 64KB ROM — that is created by the endpoints themselves after the secure channel is established.

Server must prove certificate identity to receive the 3 microROMs. If certificate is validated, client is just sent the 3 microROMs.

### Phase 4: NoEyes Handshake

Given 3 microROMs, the two endpoints now have a secure channel to establish end to end communications and send a full 64KB ROM.  In reality they can communicate securely over the 3 microROMs.  This is done simply by triple encoding using microZOSCII.  The benefits of transmitting the 64kb full ROM however is that communications now becomes more compact and faster. This 64kb ROM can either be nominated by the client, server or system-created.

### Phase 5: Sovereign Communication

Endpoints communicate using the privately exchanged ROM. All three middlemen are now irrelevant.

## Trust Elevation

| Phase | ZOSCII Trust Level | Who Knows What |
|-------|-------------------|----------------|
| Bootstrap | Level 3 (Federated) | Middlemen each hold their own microROM only |
| Triple Encoding | Level 1 (Peer-to-Peer) | Endpoints hold all 3 microROMs, middlemen excluded from each other |
| ROM Exchange | Level 1 → Level 0 | Fresh 64KB ROM exchanged directly, no third parties |
| Operation | Level 0 (Self-Sovereign) | Only endpoints hold the operating ROM |

## Security Properties

### No Single Point of Compromise

- **Middlemen compromised**: Each only knows its own microROM — all 3 must collude to decode the session, and even then they never see the private operating ROM
- **One endpoint compromised**: Attacker gets current ROM but learns nothing about the bootstrap mechanism — cannot replay, intercept, or impersonate
- **Single middleman compromised**: Attacker gains one microROM out of three — useless without the other two

### Zero Knowledge at Every Layer

| Party | Knows Own microROM? | Knows Other microROMs? | Knows Operating ROM? | Knows Data? |
|-------|---------------------|----------------------|---------------------|-------------|
| Middleman A | Yes (its own only) | No | No | No |
| Middleman B | Yes (its own only) | No | No | No |
| Middleman C | Yes (its own only) | No | No | No |
| Endpoint A | Yes (all 3) | Yes (all 3) | Yes | Yes |
| Endpoint B | Yes (all 3) | Yes (all 3) | Yes | Yes |

**Nobody except the endpoints knows everything. That is actual zero trust.**

### Collusion Requirement

Unless all 3 trust authorities collude, the session is information-theoretically secure from the very first message. Even with full collusion, the authorities only compromise the session microROMs — the private 64KB ROM exchanged afterwards is one they never saw.

## What Certificate Authorities Become

CAs retain a legitimate and honest role:

- **Identity verification**: Confirming an IP address or entity is who they claim to be
- **Bootstrap facilitation**: Providing dynamically generated microROMs to establish initial secure channels
- **No ongoing security role**: Communication security is handled entirely by ZOSCII

Certificates prove identity. ZOSCII provides security. These are separate concerns that should never have been conflated.

## Why This Matters

- **No encryption**: Avoids ITAR, EAR and export restrictions entirely
- **No certificates required for security**: Only for identity verification
- **No ongoing third-party dependency**: CAs are used once and discarded
- **Quantum-proof**: I(M;A)=0 — information-theoretic security, not computational assumption
- **Lightweight**: microZOSCII microROM is 240 bytes, deployable anywhere

## Triple Encoding: How It Works

Each of the 3 middlemen dynamically generates a fresh 240-byte microZOSCII ROM per session. These are not static — they are created on the fly for each connection request.

### Process

1. Endpoint A requests connection to Endpoint B
2. Authority A generates a fresh 240-byte microZOSCII ROM for this session
3. Authority B generates a fresh 240-byte microZOSCII ROM for this session
4. Authority C generates a fresh 240-byte microZOSCII ROM for this session
5. All 3 authorities provide their ROM to Endpoint A (the initiator)
6. All 3 authorities provide the same ROMs to Endpoint B (validated via certificate as the correct destination)
7. Both endpoints now hold the same 3 microZOSCII ROMs
8. Communication is triple-encoded through all 3 ROMs
9. Endpoints use the established secure channel to exchange a private full-size ROM
10. Authorities are now permanently out of the loop

### Role of Certificates

Certificates retain exactly one job: **identity verification**.

- Endpoint A initiates — no certificate needed, they're asking
- Endpoint B receives the ROMs because the authorities validated (via certificate) that B is the correct destination for A's request
- Certificates prove "you are who you say you are" — nothing more
- Security is provided entirely by ZOSCII, not by the certificate

### Security Properties

- Each authority generates a complete microZOSCII ROM — not a fragment
- ROMs are dynamic — generated fresh per session, never reused
- Any single authority sees only its own ROM, not the other two
- Compromising 1 or even 2 authorities is insufficient — you need all 3 ROMs to decode
- Each authority operates independently — no coordination between them
- After endpoints exchange a private ROM, all 3 session ROMs become irrelevant
- As with ANY in-band key exchange, the receiving endpoint if all packets are monitored prior to consumption, then future communications can be compromised

---

*ZOSCII Foundation — Quantum Proof Information Theoretic Security est. 2025*

*zoscii.com*