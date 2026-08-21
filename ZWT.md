# ZWT -- ZOSCII Web Tokens

**Version 0.3 (DRAFT)** (document version; the wire `version` byte is still 0)
**Author:** Julian Cassin

A quantum-proof, opaque session/attestation token. The JWT analogue for ZOSCII: an issuer attests a user to a relying party, but unlike JWT the token is information-theoretically opaque and its verification structure is concealed. No asymmetric primitive -- nothing for Shor's algorithm to attack.

---

## Keys

| Key | Held by | Role |
|-----|---------|------|
| **SHAREDROM** | Issuer + Relying Party | Shared per-relationship key |
| **ISSUERROM1** and **ISSUERROM2** | Issuer only | Issuer's private ROM -- never shared. One pair for all relying parties, or one pair per relying party |

---

## Construction

sharedsignature = a GUID or similar
issuersignature = sharedsignature

The issuer block body contains: version, length of issuersignature, issuersignature, and privateclaims (the last field has no length).

The issuerblock is the rollinghash of the issuerbody, prepended to the issuerbody. The hash covers version, lengths, and fields.

issuerdata = encode(ISSUERROM1, encode(ISSUERROM2, issuerblock)) -- the issuer seals the shared signature with its private ROM, double-encoded. issuerdata is OPTIONAL (see below).

The shared block body contains: version, length of sharedsignature, sharedsignature, and sharedclaims (the last field has no length). Note: issuerdata is NOT a field of the shared block.

sharedblock = rollinghash(sharedbody) + sharedbody
sharedstuff = encode(SHAREDROM, sharedblock)

The token is a concatenation:

lenheader = the 32-bit little-endian byte length of sharedstuff, each byte concealed as a SHAREDROM address (4 slots / 8 wire bytes, read by indirection)
zwt = lenheader + sharedstuff [ + issuerdata ]

`sharedsignature` and `issuersignature` are identical values.

`encode(ROM, ...)` is a reversible UNSIGNAL encoding. `decode(ROM, ...)` is its inverse. To open, the relying party reads the lenheader by ROM indirection, splits sharedstuff from the remainder, decodes sharedstuff with SHAREDROM, and reads the shared fields. Anything after sharedstuff is issuerdata, opened separately by the issuer with ISSUERROM1 and ISSUERROM2. The rolling hash is verified on decode, binding every field.

**issuerdata is optional.** It exists only so a *separate* issuer can verify a token in front of a relying party who is not the issuer. When the party minting and the party checking are the same entity, issuerdata is omitted entirely (and the issuer ROMs are not needed): the token is just `lenheader + sharedstuff`, a bare same-party token. A reader detects this by the remainder being empty after sharedstuff.

---

## Wire Format

The token is a flat, versioned structure -- readable on any target (Z80, 6502, C, C#, Python) with base-plus-offset arithmetic. All multi-byte integers are **little-endian**.

A ZWT is a concatenation of three parts:

```
[ length header ][ sharedstuff ][ issuerdata (optional) ]
```

- **length header** -- 4 slots (8 bytes): the 32-bit LE byte length of `sharedstuff`, each length byte concealed as a 2-byte SHAREDROM address whose dereferenced ROM value is that byte. Read by indirection, not decode.
- **sharedstuff** -- `encode(SHAREDROM, sharedblock)`, exactly the number of bytes given by the length header.
- **issuerdata** -- `encode(ISSUERROM1, encode(ISSUERROM2, issuerblock))`, the remaining bytes. Optional: absent for a bare same-party token (nothing follows sharedstuff).

To read: dereference the 4 header slots through SHAREDROM to get `sharedstuff` length; slice `sharedstuff` (bytes 8 .. 8+len) and `issuerdata` (the remainder); decode `sharedstuff` with SHAREDROM; hand `issuerdata` (if any) to the issuer.

### Length Header (indirection, read with SHAREDROM)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 2 | SHAREDROM address -> byte 0 of sharedstuff length (LE) |
| 2 | 2 | SHAREDROM address -> byte 1 |
| 4 | 2 | SHAREDROM address -> byte 2 |
| 6 | 2 | SHAREDROM address -> byte 3 (most significant) |

32-bit length gives a 4 GB ceiling on the encoded sharedstuff. issuerdata has no length field -- it is simply everything after sharedstuff, so it is unbounded and optional.

### Shared Block (inside sharedstuff, UNSIGNALed with SHAREDROM)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | rolling hash (CRC) |
| 4 | 1 | version (= 0) |
| 5 | 2 | length of sharedsignature (LE) |
| 7 | .. | sharedsignature |
| .. | .. | sharedclaims (last field, no length) |

### Issuer Block (inside issuerdata, double UNSIGNALed with ISSUERROM1 and ISSUERROM2)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | rolling hash (CRC) |
| 4 | 1 | version (= 0) |
| 5 | 2 | length of issuersignature (LE) |
| 7 | .. | issuersignature |
| .. | .. | privateclaims (last field, no length) |

`sharedclaims` has no length field -- it is the last field of the shared block. `privateclaims` has no length field -- it is the last field of the issuer block.

The issuer block is built and double-UNSIGNAL-encoded first, then concatenated as `issuerdata` after `sharedstuff`. The relying party reads only the length header and sharedstuff; issuerdata stays opaque to it and is opened separately by the issuer. Each block's rolling hash covers everything after its own 4-byte hash -- version, length table, and fields.

### Design Choices

- **No pointers, only lengths -- and the last field needs neither.** Fields are in fixed order. Each blob starts where the previous ended. The last field in a block extends to the end of the block.
- **issuerdata is concatenated, not nested.** It is appended after the encoded sharedstuff rather than sealed inside the shared frame. This avoids a third UNSIGNAL pass over it (it is already double-encoded and maximally opaque), makes it **optional** by simple presence/absence, and lets one format serve both full attestation tokens and bare same-party tokens.
- **The split point is concealed.** The sharedstuff length lives only in the indirected header -- an observer without SHAREDROM sees 8 bytes of ROM addresses that mean nothing, so where sharedstuff ends and issuerdata begins is not visible on the wire.
- **No field-count byte, no type tags, no field IDs.** The segment schema is fixed per version and known out of band.
- **Version byte for forward compatibility**, inside the hash coverage.
- **Hash covers everything except itself.**
- **Double UNSIGNAL encoded issuer data.** `issuerdata` is double-encoded because the relying party has the plaintext `sharedsignature`; double encoding removes any known-plaintext foothold on `privateclaims`.

`privateclaims` passes through UNSIGNAL twice (the issuer block's double encoding). `sharedclaims` passes through once. issuerdata is no longer triple-processed -- the concatenation removed the third pass, making tokens roughly a third smaller.

### Integrity note

The shared block's rolling hash makes tampering of sharedsignature/sharedclaims detectable on open. issuerdata's integrity is the issuer's concern: its meaningful content (the sealed sharedsignature, privateclaims, and their hash) is protected by the issuer block's hash and the double encoding, so an attacker cannot alter the sealed values without introspection failing. Flips landing in UNSIGNAL slack (a layer's random suffix) are absorbed harmlessly and cannot change any sealed value.

A typical token -- a 16-byte GUID `sharedsignature` with short claims -- is approximately **1.8-3.2 KB** (measured: empty-claims baseline ~1.8 KB, short claims ~3.2 KB). The size is dominated by the three UNSIGNAL layers' random padding rather than the claims, so putting GUID-sized values in the claims barely changes it.

---

## Verification

| Verifier | Checks | With |
|----------|--------|------|
| **Relying Party** | opens the ZWT and reads `sharedsignature` + shared claims | SHAREDROM |
| **Issuer** | `sharedsignature` matches the copy sealed in `issuersignature` | ISSUERROM1 and ISSUERROM2 |

---

## Updating Shared Claims

The relying party can update `sharedclaims` without re-issuing the entire token. This is useful for:
- Session state management
- Nonce rotation (ping-pong)
- Client-side claim updates that don't affect issuer validation

Updating shared claims creates a **new token**: the shared block is rebuilt with the new claims (same `sharedsignature`), re-encoded, and the original `issuerdata` tail is re-appended verbatim, so issuer attestation is untouched. The old token remains valid with old claims -- the RP chooses which token to use.

### Security Properties

| What | Can Change | Why |
|------|------------|-----|
| `sharedclaims` | RP | RP's own view -- affects only its own door |
| `privateclaims` | Issuer only | Sealed with issuer ROMs -- never exposed to RP |
| `sharedsignature` | Issuer only | Binding to issuerdata -- changing it breaks introspection |
| `issuerdata` | Issuer only | Sealed with issuer ROMs -- RP can't read or modify |

---

## Ping-Pong (Challenge-Response)

The RP initiates a challenge-response protocol by updating the nonce in `sharedclaims`. The issuer validates the nonce and acts upon it once.

**Nonces can be used two ways, depending on what you need to prove:**

| Mode | Rule | Proves | Use case |
|------|------|--------|----------|
| **Fresh (random)** | issuer accepts any nonce not seen before, and records it as used | the message was **not replayed** | session tokens, general replay prevention |
| **Incremental (+step)** | issuer issues nonce R and accepts only R + expected step in return | the responder **read this specific token live** (not an old capture) | challenge-response, the FOB-CAR flow below |

Fresh proves *not-replayed*; incremental proves *live-response-to-my-challenge*. The FOB-CAR workflow below uses the **incremental** mode (CAR issues R, FOB returns R + 1). The generic session-token flow uses the **fresh** mode. They are two deliberate strategies, not variants of one.

### Nonce and Challenge ID in Issuer Claims

The `privateclaims` contain both the nonce and a challenge identifier.

| Field | Purpose |
|-------|---------|
| **nonce** | One-time freshness marker -- prevents replay |
| **challenge** | Identifies which challenge/request this token belongs to |
| **user** (or other claims) | Actual identity/authorization data |

### Protocol Flow

1. Issuer -> RP: token (nonce: N1, challenge: C1 in both claims)
2. RP -> Issuer: token + updated sharedclaims (nonce: N2)
3. Issuer validates: N2 is fresh (not used before)
4. Issuer -> RP: new token (nonce: N2, challenge: C1 in both claims)
5. RP -> Issuer: token + updated sharedclaims (nonce: N3)
6. Issuer rejects: N3 already used

### State Tracking

The issuer tracks:
- **Used nonces** -- to prevent replay attacks
- **Challenge state** -- to track which challenges are pending or completed
- **Session state** -- to track the current nonce per session

### Workflow: FOB-CAR Double Ping-Pong

**NOTE** If both parties are trusted (the FOB and the CAR, then it isn't mandatory to even have an internal claim, that is an option)

**Step 0: FOB Initiates**
FOB sends request to CAR. Timer starts.

**Step 1a: CAR Sends Ping 1**
CAR issues token with nonce R1 in BOTH private and shared claims.
CAR sends token to FOB.

**Step 1b: FOB Sends Pong 1**
FOB updates shared claims -- nonce changes from R1 to R1 + 1.
Private claims still have R1. Shared claims now have R1 + 1.
FOB sends updated token back to CAR.

**Step 1c: CAR Validates Pong 1**
CAR introspects the token.
CAR reads private claims nonce = R1.
CAR reads shared claims nonce = R1 + 1.
CAR KNOWS shared = private + 1. Validation passes. FOB is authenticated.

**Step 2a: CAR Sends Ping 2 (MANDATORY)**
CAR issues a NEW token with nonce R2 in BOTH private and shared claims.
CAR sends token to FOB.

**Step 2b: FOB Sends Pong 2**
FOB updates shared claims -- nonce changes from R2 to R2 + 1.
Private claims still have R2. Shared claims now have R2 + 1.
FOB sends updated token back to CAR.

**Step 2c: CAR Validates Pong 2**
CAR introspects the token.
CAR reads private claims nonce = R2.
CAR reads shared claims nonce = R2 + 1.
CAR KNOWS shared = private + 1. Validation passes.

**Step 3: CAR Opens/Unlocks and Sends New Token to FOB**
CAR UNLOCKS THE DOOR.
CAR issues a NEW token with nonce R3 in BOTH private and shared claims.
CAR sends new token to FOB so the FOB can chirp again if needed.

**Timer stops. If Step 2c is not completed within the timeframe, CAR rejects the session.**


### Summary Table

| Step | Who | Action | Private Nonce | Shared Nonce | Validation |
|------|-----|--------|---------------|--------------|------------|
| 0 | FOB | Initiate (Timer starts) | -- | -- | -- |
| 1a | CAR | Send Ping 1 | R1 | R1 | -- |
| 1b | FOB | Send Pong 1 | R1 | R1 + 1 | -- |
| 1c | CAR | Validate Pong 1 | R1 | R1 + 1 | shared = private + 1 PASS |
| 2a | CAR | Send Ping 2 | R2 | R2 | -- |
| 2b | FOB | Send Pong 2 | R2 | R2 + 1 | -- |
| 2c | CAR | Validate Pong 2 | R2 | R2 + 1 | shared = private + 1 PASS |
| 3 | CAR | Open/Unlock + Send new token | R3 | R3 | -- |

### Rule

The CAR always issues a new random nonce.
The FOB always returns nonce + 1.
The CAR always validates that shared = private + expected increment.

Two complete rounds are mandatory:
- Round 1: FOB chirps, CAR responds (authenticates FOB)
- Round 2: CAR chirps, FOB responds (verifies proximity, stops relay)

The token is never modified. It is replaced with a new token after each exchange.

### Why This Works

- **RP pong:** the RP updates `sharedclaims.nonce` only (it is in the shared envelope); `issuerdata` is unchanged, so this is *not* a re-issue -- the same sealed private nonce stays in place.
- **Issuer validation:** the issuer reads the private nonce from the (unchanged) `issuerdata` and checks the RP's shared nonce against it -- fresh (unused) in fresh mode, or exactly R + step in incremental mode.
- **Issuer ping:** when the issuer sends the next challenge it issues a **new token** (new `issuerdata`) carrying the next private nonce. Updating the private nonce is always a full re-issue, never an in-place edit.
- Challenge ID tracks which request this belongs to.
- The binding between `sharedsignature` and `issuerdata` is preserved across the exchange.
- Replay is prevented -- used nonces are rejected.

---

## Car Relay and Replay Attack Prevention

The double ping-pong challenge-response is **mandatory** to stop relay attacks. Replay is prevented by nonce progression. Relay is prevented by requiring two rapid alternating exchanges within a tight time window.

### Attack Vectors

| Attack | Description | Prevention |
|--------|-------------|------------|
| **Replay** | Attacker captures and resends an old valid message | Nonce progression -- each nonce used once |
| **Relay** | Attacker forwards messages between FOB and CAR in real-time | Double ping-pong + timing/proximity bound |
| **MITM** | Attacker intercepts and modifies messages | ROM binding prevents forgery |

### Why Single Ping-Pong Is Not Enough

Single ping-pong prevents replay because a captured nonce cannot be reused.

However, relay still works. An attacker forwards messages between FOB and CAR in real-time. The attacker passes the challenge from CAR to FOB and the response from FOB to CAR. The CAR accepts because the response is valid.

### How a Relay Attack Actually Succeeds

It is worth being precise about the one attack the timing bound is guarding against, because a relay does not break any crypto -- it defeats *distance*.

**Scope: this applies to passive proximity unlock (PKES) only.** The relay attack works only when the car is continuously interrogating for a nearby FOB and the FOB **auto-responds with no human action** -- that auto-response is what a relay shuttles back and forth while the owner does nothing. It does **not** apply to active button-push (RKE), where the FOB transmits only when the owner physically presses the button: there is no challenge being auto-answered to relay, the FOB is silent until pressed, and a press means the owner is standing at the car -- the button press is itself the proof of presence. (Button-push RKE has its own separate weaknesses, e.g. jam-and-replay / RollJam, but relay is not one of them.) Consequently the mandatory double ping-pong, the timing window, and the clock-drift discriminator below are all **PKES requirements**. For button-push RKE a single authenticated exchange with nonce progression is sufficient -- there is no relay threat to bound.

Two attackers with two linked relay devices:

- **Device A** sits next to the CAR (in the car park).
- **Device B** sits next to the FOB (outside the owner's house, 50 m away).
- A and B are joined by the attackers' own fast channel (their own radio link, or the internet).

The exchange:

1. CAR emits **Ping 1** (nonce R1). The real FOB is out of range, so normally nothing happens.
2. **Device A** hears Ping 1 and forwards it over the attackers' link to **Device B**.
3. **Device B** re-emits Ping 1 to the real FOB.
4. The FOB does exactly what it should: reads the token, sets shared nonce to R1 + 1, sends **Pong 1**.
5. **Device B** captures Pong 1, relays it to **Device A**, which re-emits it to the CAR.
6. CAR validates shared = private + 1. **Passes.** Round 2 relays identically.

What just happened:

- The FOB **genuinely participated, live**, with the real nonces -- nonce progression is fully satisfied.
- Nothing was replayed (the nonces are fresh) and nothing was forged (the FOB signed with real keys).
- The attackers never held SHAREDROM or the ISSUERROMs, never read `privateclaims`, never touched the crypto.
- They simply **carried the messages between two legitimate endpoints that were too far apart to talk directly.**

The only observable difference between this and the FOB standing at the car is **latency**: the relay adds the propagation time of the attackers' link (car park -> house -> back). If that added delay pushes the response outside the CAR's timing window, the CAR rejects. If the attackers' link is fast enough -- and modern relay hardware adds microseconds -- the exchange completes inside the legitimate window and the CAR cannot distinguish it from the real FOB.

This is exactly why the residual is *physical, not protocol*. The protocol did everything correctly; the messages were real live answers to real live challenges. The attacker only moved them through space faster than the timing check could notice.

**Round-trip timing is the only relay defence available from the messages themselves -- nothing above the ping-pong adds anything at the message layer.** Every message-layer technique that appears to help (more rounds, timing statistics, jitter analysis) is just a way of *measuring the round-trip more sharply*. A relay is caught by the message-layer check if and only if it exceeds the acceptable time window; if it fits inside that window, no number of rounds and no message-timing analysis can detect it, because every signal carried *by the messages* reduces to round-trip time.

There is one way to add a genuinely independent axis, and it does not come from the messages: an endpoint's own internal state that a relay cannot forward. A shared, synced clock with a learned drift model is exactly such a state -- see *Clock Drift as an Independent Relay Discriminator* below.

### Double Ping-Pong (Relay Prevention)

Round 1 (Authentication):
- FOB -> CAR: NONCE=1 (initiate)
- CAR -> FOB: NONCE=2 (ping 1)
- FOB -> CAR: NONCE=3 (pong 1)

Round 2 (Proximity Verification -- MANDATORY):
- CAR -> FOB: NONCE=4 (ping 2)
- FOB -> CAR: NONCE=5 (pong 2)

### Why Double Ping-Pong Raises the Bar Against Relay

Round 1 authenticates the FOB. The CAR accepts.

Round 2 requires a fast exchange. A relay adds latency; if the second response arrives after the expected window, the CAR rejects the session.

**What this can and cannot do.** Against a message-level relay -- an attacker forwarding the protocol messages between two legitimate endpoints -- the round-trip timing bound is the *maximal* defence available at the protocol layer. Nonce progression already forces the response to be a live answer to this specific challenge (no pre-recorded reply works), so the only remaining signal a relay can be caught by is latency, and round-trip time is the only measurement the endpoints have. A second protocol layer cannot measure closeness any better than the first -- it is the same clock and the same endpoints -- so tightening the ping-pong window is the ceiling of what any token protocol can do. There is no additional protocol step that would prevent a relay 'more'.

The residual risk is therefore purely physical: if relay hardware can forward within the accepted window, no token protocol can tell. Reducing the window to nanosecond scale is a hardware move to a faster physical layer (e.g. UWB ranging), not a better protocol -- a separate subsystem outside ZWT's scope, not a missing step in it. ZWT already performs the complete protocol-layer relay defence.

### Configurable Rounds and Timing Analysis

The round count and the use of inter-round timing are implementation choices, not fixed by the protocol. Note up front: none of these are *additional* protections -- they are all ways of measuring the one and only relay signal, the **timeframe**, more precisely. The timeframe is the sole defence; everything below sharpens that single measurement.

- **More rounds.** Two rounds is the mandatory minimum (Round 1 authenticates, Round 2 forces a second live exchange). Nothing stops an implementation running N rounds. Each additional round is another live nonce exchange the attacker must answer inside the window, and another timing sample.
- **Sample the times, don't just threshold them.** Rather than a single pass/fail on one round-trip, an implementation can record the timing of every exchange and analyse the series -- mean, variance, jitter, drift between rounds. This is strictly more information than one threshold check.
- **Why it helps (within the ceiling).** All of these help in exactly one way: they measure the timeframe more sharply. A relay typically adds a roughly constant latency offset and can perturb the jitter profile; across several samples that offset and perturbation are easier to see than in a single round-trip -- a legitimate paired exchange has a tight, stable timing signature, a relayed one sits consistently higher or noisier. A relay fast enough to stay inside the window on every sample still passes -- the ceiling is unchanged -- but a sharper timing measurement shrinks the margin the relay has to fit inside. It never adds a second, non-timing way to catch a relay, because there isn't one.
- **Tunable trade-off.** Tighter windows and more rounds cut the relay margin but cost latency and can reject legitimate exchanges under adverse RF conditions. The right number of rounds and the window width are per-deployment tuning, not protocol constants.

So the protocol-layer defence is not a single fixed timer: it is as rich a timing model as the implementer wants to build on top of the mandatory two-round minimum. It still cannot beat a relay operating inside the physical window -- that ceiling is physical -- but how close you push to that ceiling is an implementation decision.

### Clock Drift as an Independent Relay Discriminator

A relay forwards *messages*; it cannot forward an endpoint's *internal state*. If the FOB carries its own clock and syncs it with the CAR while in range, that gives the CAR a second observable that is not message round-trip time at all -- and therefore not something a fast relay can defeat by being fast.

**How it works:**

- While the FOB is in range, it periodically syncs its clock with the CAR, so the two agree on time and the CAR records the sync moment.
- Once the FOB leaves range, its local oscillator drifts from the CAR's at a characteristic rate -- a given FOB's crystal drifts in a particular direction and magnitude, accumulating predictably with elapsed time since last sync.
- On challenge, the CAR requires the FOB to include its current local clock reading in the response, sealed inside `privateclaims` and bound to the current nonce so a relay cannot lift it from an earlier exchange or rewrite it.
- The CAR checks whether the FOB's reported clock matches what *this* FOB's clock should read, given its learned drift rate and the time since last sync.

**Why a relay cannot beat it:** the relay does not control the FOB's oscillator and cannot recompute a correctly-drifted timestamp without knowing that specific FOB's drift profile -- which was only ever established through in-range syncs the attacker never observed. A relay that is fast on message latency still cannot make the FOB's clock read the expected value. This is an axis independent of round-trip time.

**Honest caveats:**

- It is a **fingerprint / statistical** discriminator, not an absolute proof of proximity. Oscillator drift shifts with temperature and ageing, and a patient attacker who can observe the FOB over time could model its drift too. It raises the bar substantially but is probabilistic, unlike physical distance-bounding.
- The clock reading **must be sealed and nonce-bound** (e.g. in `privateclaims`), or a relay could replay or patch it.
- It requires the FOB to have a clock and an in-range sync opportunity; it does nothing for a FOB that has never synced.

**Operational and physical measures.** No message-layer or clock-layer technique fully removes relay; complete elimination requires operational or physical measures. Examples:

- The ability to **turn a FOB off** (disabling its auto-response so there is nothing for a relay to shuttle).
- The ability to **shield a FOB** (e.g. a Faraday pouch), which physically prevents the FOB from hearing an interrogation.
- The ability for a FOB to **alert the user** when it is being interrogated or is auto-responding -- kept separate from its ability to enable or disable the unlock mechanism, so the user is warned even if the unlock path itself is compromised.

So while round-trip timing remains the only relay defence obtainable from the messages, a synced-clock drift model is a genuinely separate axis -- endpoint internal state a relay cannot carry -- and can be layered on top to further mitigate relay attacks.

### Required Timing Window

| Parameter | Requirement | Why |
|-----------|-------------|-----|
| **Proximity Window** | Tight (e.g., < 50ms) | Physical proximity requirement |
| **Round 1 -> Round 2** | Rapid exchange | Relay introduces measurable delay |
| **Both Rounds Required** | 2 rounds | Single round does not detect relay |

### Security Properties

| Attack | Prevention | Mechanism |
|--------|------------|-----------|
| **Replay** | Yes | Nonce progression -- each nonce used once |
| **Relay** | Maximal at protocol layer | Nonce binding + tight round-trip window is the most any token protocol can do; residual is a physical-layer/hardware matter |
| **MITM** | Yes | ROM binding prevents forgery |
| **Outsider** | Yes | Cannot forge without SHAREDROM + ISSUERROM |
| **Compromised RP** | Yes | Can only affect its own door |

### Summary

- Single Ping-Pong -> Prevents REPLAY (nonce used once)
- Double Ping-Pong -> Maximal protocol-layer RELAY defence (nonce binding + tight timing window); residual relay risk is physical-layer, not a protocol gap
- ROM Binding -> Prevents FORGERY (needs both ROMs)
- Paired CAR-FOB -> Prevents SPOOFING (only paired works)

**Double ping-pong is mandatory and is the complete protocol-layer relay defence -- nonce binding plus a tight round-trip window is the most any token protocol can achieve. Any further relay reduction is a physical-layer/hardware question, not a change to ZWT.**

---

## Multi-Server Issuer Claims

Since `privateclaims` are sealed inside the issuer block with ISSUERROM1 and ISSUERROM2, **only the issuer's servers** can read them. This enables a trusted multi-server architecture.

### Architecture

All issuer servers share ISSUERROM1 and ISSUERROM2. All servers share SHAREDROM. The relying party has only SHAREDROM.

### Claim Types

| Claim Type | Location | Readable By | Contains |
|------------|----------|-------------|----------|
| `sharedclaims` | Shared block | Issuer + RP | Public claims, nonces, state |
| `privateclaims` | Issuer block | Issuer only | User ID, roles, permissions, secrets, nonce, challenge |

### Multi-Server Validation

Any issuer server with the ROMs can introspect a token. `privateclaims` remain consistent because all servers share the same ROMs.

### Shared State

The issuer servers must share state to prevent replay attacks:
- Used nonces (prevent replay across servers)
- Challenge state (shared across servers)
- Session state (shared across servers)

### Server Failover

If one issuer server fails, another can take over. The `privateclaims` remain consistent because all servers share the same ROMs. The shared state must be replicated or stored in a shared database.

---

## Comparison: Shared vs Private Claims

| Aspect | Shared Claims | Private Claims |
|--------|---------------|----------------|
| **Readable by RP** | Yes | No |
| **Readable by Issuer** | Yes | Yes |
| **Modifiable by RP** | Yes (UpdateSharedClaims) | No |
| **Modifiable by Issuer** | Yes (Issue new token) | Yes (Issue new token) |
| **Sealed with** | SHAREDROM | ISSUERROM1 + ISSUERROM2 |
| **Typical contents** | Scope, state, nonce | User ID, roles, permissions, nonce, challenge |
| **Use case** | RP-local state | Identity verification, challenge tracking |

---

## Summary: Trust Model

- **SHAREDROM** -- Trusted by RP + Issuer
  - RP can decode shared block
  - RP can update shared claims
  - RP cannot read or modify private claims

- **ISSUERROM1 + ISSUERROM2** -- Trusted by Issuer only
  - Only issuer servers have these
  - Seals private claims
  - Prevents RP forgery

- **Shared Signature (GUID)** -- Binding between layers
  - Must match in both shared and issuer blocks
  - Cannot be forged without issuer ROMs

- **Nonce** -- One-time freshness marker
  - Stored in both shared and private claims
  - RP can update shared copy
  - Issuer validates and updates private copy
  - Used nonces are rejected

- **Challenge ID** -- Request/state tracker
  - Stored in private claims only
  - Links token to specific challenge
  - Issuer tracks challenge state
  - Prevents re-use of completed challenges

---

## Why Nobody but the Issuer Can Forge a Valid Token

- A forger without SHAREDROM can't open or produce a ZWT -- outsiders locked out.
- A relying party holds SHAREDROM, so it can produce a `sharedsignature`, but it cannot produce a matching `issuersignature` -- that requires ISSUERROM1 and ISSUERROM2 (issuer-only).
- A `sharedsignature` is only valid when it matches the copy sealed inside `issuersignature`.
- Therefore only the issuer can produce a valid token. A forged shared-sig has no matching sealed copy and fails.

**Even a fully compromised relying party cannot mint a token the issuer will accept.**

---

## Properties

| Property | How |
|----------|-----|
| **Opaque** | Whole ZWT is UNSIGNAL-encoded; payload, signatures, and structure are indistinguishable from noise (I(M;A)=0) |
| **Concealed verification structure** | An observer can't tell how many signatures exist, which keys govern them, or where they are |
| **Cross-site inert** | A ZWT is unvalidatable by any party without the relationship key -- no `aud` check needed; misuse is structural, not just forbidden |
| **Quantum-proof** | No asymmetric primitive; nothing for Shor's algorithm to attack |
| **Unforgeable** | Valid tokens require the issuer's private ROM pair (via the sealed-shared-sig binding) |

---

## Not Solved by ZWT Alone

| Gap | Note |
|-----|------|
| **Replay to the legitimate relying party** | A stolen ZWT can be replayed to its intended recipient. Bind a server-issued nonce inside the token; avoid clock-based expiry (clocks are attacker-influenceable). |
| **Revocation** | Stateless local verification can't revoke mid-life. If needed, verify `issuersignature` via issuer introspection instead -- gains revocation, costs a round-trip. |
| **Relay (proximity attacks)** | ZWT already applies the maximal protocol-layer defence: nonce binding plus a tight round-trip timing window. A relay fast enough to answer inside that window cannot be distinguished by *any* token protocol -- the residual is a physical-layer/hardware matter (e.g. UWB ranging), not a gap ZWT could close with more protocol. |

---

## Notes

- `sharedclaims` are readable by the relying party (opened with SHAREDROM). `privateclaims` are sealed inside `issuerdata` and readable only by the issuer.
- SHAREDROM is per-relationship, so a relying party forging a shared signature could only ever affect its own door -- a non-event -- and the binding to `issuersignature` prevents even that from producing an issuer-valid token.