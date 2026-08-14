# CyborgUnicorn.UNINTELLIGENCE

ZOSCII (Zero Overhead Secure Code Information Interchange) SDK for .NET.

Information-theoretically secure encoding achieving **I(M;A)=0** — the encoded output
is statistically independent of the input without the ROM key material.

## Naming conventions

| Prefix | Purpose |
|---|---|
| `Z` | ZOSCII encode / decode / verify |
| `U` | UNSIGNAL encode / decode / verify |
| `B` | Binary operations: compare, split, join, verify |
| `MQ` | Message Queue operations (queue, store, replication, monitoring) |
| `ZWT` | ZOSCII Web Tokens: issue, open, introspect, verify |

---

## UEncode / UDecode

All string overloads assume UTF-8. Both ends must agree on encoding.

```csharp
byte[] encoded   = UEncode.Bytes(data, rom);
byte[] decoded   = UDecode.Bytes(encoded, rom);
byte[] chain     = UEncode.Chain(data, new[] { rom1, rom2, rom3 });
byte[] unchained = UDecode.Chain(chain, new[] { rom1, rom2, rom3 });
byte[] tango     = UEncode.Chain(data, new[] { rom1, rom2, rom3 }, true);   // Tango: round-robin ROMs per byte, 2x expansion, up to 3x entropy
byte[] untango   = UDecode.Chain(tango, new[] { rom1, rom2, rom3 }, true);  // must match encode
bool   ok        = UEncode.ChainFile("input.bin", "output.sig", new[] { rom1, rom2, rom3 });
bool   ok        = UDecode.ChainFile("output.sig", "recovered.bin", new[] { rom1, rom2, rom3 });
UEncode.File("input.bin", "output.sig", rom);
UDecode.File("output.sig", "recovered.bin", rom);
string b64       = UEncode.ToBase64(data, rom);
byte[] back      = UDecode.FromBase64(b64, rom);
byte[] encoded   = UEncode.String("hello", rom);
string text      = UDecode.ToString(encoded, rom);
string text      = UDecode.FileToString("output.sig", rom);
```

---

## UVerify

```csharp
bool blnMatch3 = UVerify.File("encoded.sig", "original.bin", rom);
```

---

## ZEncode / ZDecode

All string overloads assume UTF-8. Both ends must agree on encoding.

```csharp
byte[] encoded   = ZEncode.Bytes(data, rom);
byte[] decoded   = ZDecode.Bytes(encoded, rom);
byte[] chain     = ZEncode.Chain(data, new[] { rom1, rom2, rom3 });
byte[] unchained = ZDecode.Chain(chain, new[] { rom1, rom2, rom3 });
byte[] tango     = ZEncode.Chain(data, new[] { rom1, rom2, rom3 }, true);   // Tango: round-robin ROMs per byte, 2x expansion, up to 3x entropy
byte[] untango   = ZDecode.Chain(tango, new[] { rom1, rom2, rom3 }, true);  // must match encode
bool   ok        = ZEncode.ChainFile("input.bin", "output.zoc", new[] { rom1, rom2, rom3 });
bool   ok        = ZDecode.ChainFile("output.zoc", "recovered.bin", new[] { rom1, rom2, rom3 });
ZEncode.File("input.bin", "output.zoc", rom);
ZDecode.File("output.zoc", "recovered.bin", rom);
string b64       = ZEncode.ToBase64(data, rom);
byte[] back      = ZDecode.FromBase64(b64, rom);
byte[] encoded   = ZEncode.String("hello", rom);
string text      = ZDecode.ToString(encoded, rom);
string text      = ZDecode.FileToString("output.zoc", rom);
```

---

## ZVerify / BVerify

```csharp
bool blnMatch1 = ZVerify.File("encoded.zoc", "original.bin", rom);
bool blnMatch2 = ZVerify.Bytes(encodedBytes, plainBytes, rom);
bool blnMatch4 = BVerify.File("file1.bin", "file2.bin");
bool blnMatch5 = BVerify.Bytes(arr1, arr2);
```

---

## ZOSCIIRom

```csharp
using (ZOSCIIRom rom = ZOSCIIRom.FromFile("mykey.jpg")) { }
using (ZOSCIIRom rom = ZOSCIIRom.FromBytes(rawBytes)) { }
using (ZOSCIIRom rom = ZOSCIIRom.FromBase64(base64String)) { }
```

---

## BSplit / BJoin

PENTAGONE 3-of-5 redundancy — any 3 or more shares reconstruct the original.

BSplit.File returns string[5] (share paths .s1-.s5) or null on failure.
BJoin.File takes any one share path — sibling shares are auto-discovered.
BJoin.Bytes takes byte[][5] — pass null for any missing share (minimum 3 non-null).

```csharp
string[] arrShares = BSplit.File("data.sig", "data.sig");
string strOut      = BJoin.File("data.sig.s2", "recovered.sig");

byte[][] arrShareBytes   = BSplit.Bytes(data);
byte[]   arrRecovered    = BJoin.Bytes(new[] { arrShareBytes[0], null, arrShareBytes[2], arrShareBytes[3], null });
```

---

## SecureDelete

```csharp
SecureDelete.File("sensitive.bin");
SecureDelete.Folder("sensitive_dir");
```

---

## MQClient

MQPublishResult: Success, ErrorMessage, ServerMessage, StoredName (Put only)
MQFetchResult:   HasMessage, EncodedBytes, Filename, Pointer
Encoding/decoding is the caller's responsibility — MQClient sends and receives raw bytes.
Scan returns string[] (unidentified names) or null on failure
Identify returns string[] (identified names) or null on failure

```csharp
MQClient(int intTimeoutSeconds_a = 60)
void SetUserAgentRandom()
void SetUserAgentNone()
void SetUserAgent(string strUserAgent_a)
MQCheckStatus Check(string strServerURL_a, string strQueueGUID_a, string strAfterPointer_a)
MQFetchResult FetchNext(string strServerURL_a, string strQueueGUID_a, string strAfterPointer_a)
MQPublishResult Publish(string strServerURL_a, string strQueueGUID_a, byte[] arrData_a, string strNonce_a = "", int intRetentionDays_a = 7)
MQFetchResult Get(string strServerURL_a, string strStoredName_a)
string[] Identify(string strServerURL_a, string[] arrNames_a)
MQPublishResult Put(string strServerURL_a, byte[] arrData_a, string strNonce_a = "", int intRetentionDays_a = 7)
string[] Scan(string strServerURL_a)
MQPublishResult Replicate(string strRemoteServerURL_a, string strRemoteQueueGUID_a, string strLocalServerURL_a, string strTargetQueueGUID_a, string strLastPointer_a, out string strNewPointer_a, int intRetentionDays_a = 7)
```

```csharp
var mq = new MQClient();              // default 60s timeout
var mq = new MQClient(120);           // custom timeout

// User-Agent control (default: random GUID per request)
mq.SetUserAgentRandom();              // fresh GUID on every request (default)
mq.SetUserAgentNone();                // omit User-Agent header entirely
mq.SetUserAgent("MyApp/1.0");        // fixed string until changed

string strQueueServer = "https://your-server/index.php";

// Queue
MQPublishResult pub    = mq.Publish(strQueueServer, "myqueue", data);
MQCheckStatus   status = mq.Check(strQueueServer, "myqueue", lastPointer);
MQFetchResult   result = mq.FetchNext(strQueueServer, "myqueue", lastPointer);
if (result.HasMessage) { lastPointer = result.Pointer; }

// Store
MQPublishResult  upload   = mq.Put(strQueueServer, data);
MQFetchResult    retrieve = mq.Get(strQueueServer, upload.StoredName);
string[] arrUnidentified  = mq.Scan(strQueueServer);
string[] arrIdentified    = mq.Identify(strQueueServer, arrUnidentified);

// Replication - one message per call, caller loops and persists pointer
// Replicate without localQueue stores as unidentified (-u suffix), discoverable via Scan
string strNewPointer;
MQPublishResult rep = mq.Replicate(remoteURL, remoteQueue, strQueueServer, localQueue,
    lastPointer, out strNewPointer, intRetentionDays_a: 7);
if (rep.Success && rep.ServerMessage != "up-to-date") { lastPointer = strNewPointer; }

MQPublishResult rep = mq.Replicate(remoteURL, remoteQueue, strQueueServer, "",
    lastPointer, out strNewPointer, intRetentionDays_a: 7);  // empty localQueue = store as unidentified
```

---

## MicroZOSCII

Bootstrap encoding for transmitting a full ROM over a direct connection.
Derives a 240-nibble microROM from raw bytes or from three 54-character base-62 strings,
then encodes/decodes a full ROM as a stream of 1-byte address lookups into the microROM.
Each nibble of the ROM hex maps to a randomly selected position — information-theoretically secure transmission.
A 128KB ROM produces 262,144 addresses (262,144 bytes on the wire — 2x expansion).

```csharp
// Derive microROM from byte array(s) — clamped to 240 nibbles, pass null to omit bytes2/3/4
string strMicroROM = MicroZOSCII.FromBytes(arrBytes1, null, null, null);
string strMicroROM = MicroZOSCII.FromBytes(arrBytes1, arrBytes2, arrBytes3, arrBytes4);

// Derive microROM from 3 x 54 base-62 strings (human entry, barcode, contact list)
string strMicroROM = MicroZOSCII.FromBase62(strChunk1, strChunk2, strChunk3);

// Encode a 240-nibble microROM as 3 x 54 base-62 strings for storage/display
string[] arrChunks = MicroZOSCII.ToBase62(strMicroROM);  // returns string[3]

// Individual chunk conversion utilities
string strHex    = MicroZOSCII.Base62ChunkToHex(strChunk);   // 54 base-62 chars → 80 hex nibbles
string strBase62 = MicroZOSCII.HexChunkToBase62(strHex);     // 80 hex nibbles → 54 base-62 chars

// Check nibble distribution — int[16], index 0=count of '0' ... 15=count of 'F'
// Average is 15 per nibble (240/16). Recommended minimum: 5 instances per nibble.
// Discard and regenerate if any value falls below 5.
int[] arrDist = MicroZOSCII.GetDistribution(strMicroROM);

// Encode full ROM bytes → address array (1 byte per address, 2x expansion)
byte[] arrAddresses = MicroZOSCII.Encode(strMicroROM, arrROMBytes);

// Decode address array → full ROM bytes
byte[] arrROM = MicroZOSCII.Decode(strMicroROM, arrAddresses);
```

---

## ROMExchange

Peer-to-peer ROM exchange over TCP.
Establishes a direct connection, performs a Diffie-Hellman key exchange using the caller's
ZOSCII ROM as the private key source, then transmits a full ROM via MicroZOSCII encoding.
DH exchange and ROM transmission are separate calls — either step can be bypassed
(e.g. seeds typed in, scanned from a 2D barcode, or loaded from a contact list).
Connection is maintained with a random GUID ping/pong keepalive until terminated.
No identifying information is sent at any point.

ROMExchangeResult: Success, ROMBytes (ReceiveROM only), ErrorMessage
Events: OnConnection(handle, peerIP), OnTerminated(handle), OnError(handle, message)

```csharp
var objExchange = new ROMExchange();              // default 60s timeout, 15s ping interval
var objExchange = new ROMExchange(120, 30000);    // custom timeout and ping interval

// Events
objExchange.OnConnection  += (strHandle, strPeerIP) => { };
objExchange.OnTerminated  += (strHandle) => { };
objExchange.OnError       += (strHandle, strMessage) => { };

// Bootstrap method registry — present to user before connecting
string[,] arrMethods = ROMExchange.GetBootstrapMethods();
// returns { { "DH", "Diffie-Hellman Key Exchange" }, ... }

// Listener side
string strHandle = objExchange.Listen(9000, false);          // false = manual accept, 60s timeout
string strHandle = objExchange.Listen(9000, false, 120);     // false = manual accept, 120s timeout
// OnConnection fires when peer connects
objExchange.Authorise(strHandle);                     // manual accept mode
objExchange.Reject(strHandle);                        // manual reject mode

// Initiator side
string strHandle = objExchange.Connect("192.168.1.5", 9000);
// OnConnection fires on success

// DH key exchange — both sides call independently, uses caller's ROM as private key source
byte[] arrSecret = objExchange.DHExchange(strHandle, objROM);

// Derive microROM from shared secret
string strMicroROM = MicroZOSCII.FromBytes(arrSecret, null, null, null);

// Send ROM (listener side)
ROMExchangeResult objResult = objExchange.SendROM(strHandle, strMicroROM, arrROMBytes);

// Receive ROM (initiator side)
ROMExchangeResult objResult = objExchange.ReceiveROM(strHandle, strMicroROM);
if (objResult.Success) { byte[] arrROM = objResult.ROMBytes; }

// Start keepalive — call when all exchanges are complete
objExchange.StartKeepalive(strHandle);

// Status and termination
ROMExchangeStatus objStatus = objExchange.GetStatus(strHandle);
objExchange.Terminate(strHandle);
```

---

## ZRollingHash

BRAINLESS rolling hash — 4-pass XOR chain, 32-bit (4-byte) output.
Two modes: reverse (default, requires complete payload) and forward (streamable).
Forward and reverse produce different hashes for the same input.
No ROM required. Works on bytes or files.

```csharp
// Hash bytes — reverse (default, requires full payload)
byte[] arrHash = ZRollingHash.Bytes(arrData);

// Hash bytes — forward (streamable)
byte[] arrHash = ZRollingHash.Bytes(arrData, true);

// Hash a file
byte[] arrHash = ZRollingHash.File("data.bin");
byte[] arrHash = ZRollingHash.File("data.bin", true);  // forward

// Verify
bool blnOk = ZRollingHash.Verify(arrData, arrHash);
bool blnOk = ZRollingHash.Verify(arrData, arrHash, true);  // forward
bool blnOk = ZRollingHash.VerifyFile("data.bin", arrHash);
bool blnOk = ZRollingHash.VerifyFile("data.bin", arrHash, true);  // forward
```

---

## EntropySugar

FREE_MEM and CPU_PCT are not captured automatically as PerformanceCounter is platform-specific.
Inject them from the caller via Add() using PerformanceCounter where available.

```csharp
var sugar = new EntropySugar();
sugar.CaptureFast(romsFolder);
sugar.CaptureSlow(dataFolder);
sugar.CaptureOnDemand(dataFolder, fixedFolders, mp3Folders);
sugar.Add("FREE_MEM", freeMem.ToString());
sugar.Add("CPU_PCT", cpuPct.ToString());
sugar.Add("BTN_CLICKS", "42");
long sysTime                       = sugar.Get("SYS_TIME");
Dictionary<string, string> all     = sugar.GetAll();
string json                        = sugar.ToJson();
```

---

## ROMGenerator

Generates 128KB ROMs from MP3 source files using EntropySugar to derive generation parameters.
Same entropy + same MP3s = same ROM. Different sessions produce different ROMs from the same source files.
Use `UEncode.Chain` with the resident ROMs to encode the raw bytes before saving to disk.

```csharp
byte[] rawRom = ROMGenerator.Bytes(new[] { @"C:\data\mp3s" }, sugar);
```

---

## Source

Extracts the embedded source code archive for this package.

```csharp
bool blnOk = Source.SaveAs(@"C:\MyFolder\CyborgUnicorn.UNINTELLIGENCE.source.zip");
```

To embed source.zip when building the nuget, add to the .csproj:
```xml
<ItemGroup>
  <EmbeddedResource Include="source.zip" />
</ItemGroup>
```

---

## ZTBChain

ZOSCII Tamperproof Blockchain — quantum-proof by structure. Integrity via rolling ROM + hash,
no cryptographic assumptions. Transparent ledger: chain structure is public, payload security
is the caller's responsibility.

Block files on disk: `<BlockID>.ztb`
Genesis block: `<BlockID>.ztb` (65536 bytes: byte[0]=block_type, bytes[1-65535]=ROM)
Truncation block: `<BlockID>.ztb` (111 + 65536 bytes: raw header + raw ROM, not ZOSCII-encoded)

**Enums**

`ZTBBlockType`: Genesis=0, Normal=1, Checkpoint=2, Truncation=3, Finalise=4, Bridge=5
`ZTBHashType`:  CRC32Full=0, CRC321KB=1, RollingFull=2 (default), Rolling1KB=3

**Result types**

`ZTBBlockResult`:  Success, BlockID, PrevBlockID, TrunkID, IsBranch, BlockType, HashType, Hash, PrevHash, PayloadLen, PaddedLen, Filename, Payload
`ZTBVerifyResult`: Success, VerifiedBlocks, FailedBlocks

**Constants**

`ZTBChain.NULL_GUID`           — `"00000000-0000-0000-0000-000000000000"`
`ZTBChain.GENESIS_SIZE_PUBLIC` — 65536
`ZTBChain.HEADER_RAW_SIZE`     — 111

```csharp
public static bool Create(string strNewBlockID_a, string[] arrSourcePaths_a, string strWorkDir_a, string strChainID_a)
public static ZTBChain Open(string strWorkDir_a, string strChainID_a, ZTBHashType objHashType_a = ZTBHashType.RollingFull)
public ZTBBlockResult AddBlock(string strNewBlockID_a, string strPrevBlockID_a, byte[] arrPayload_a)
public ZTBBlockResult AddBlockText(string strNewBlockID_a, string strPrevBlockID_a, string strText_a)
public ZTBBlockResult AddBlockFile(string strNewBlockID_a, string strPrevBlockID_a, string strFilePath_a)
public ZTBBlockResult AddCheckpoint(string strNewBlockID_a, string strPrevBlockID_a, string strLabel_a)
public ZTBBlockResult AddBranch(string strNewBlockID_a, string strPrevBlockID_a, byte[] arrPayload_a, string strTrunkChainID_a)
public ZTBBlockResult FetchBlock(string strBlockID_a)
public ZTBVerifyResult Verify(string strBlockID_a, bool blnWalk_a)
public ZTBBlockResult Truncate(string strNewBlockID_a, string strCheckpointBlockID_a)
public ZTBBlockResult Finalise(string strNewBlockID_a, string strPrevBlockID_a, string strLabel_a)
public void Dispose()
```

```csharp
// Create genesis block from 1-3 entropy source files (JPEG, MP3, etc.)
// Caller supplies the GUID for the genesis block
bool ok = ZTBChain.Create(strGenesisBlockID, new[] { "photo.jpg", "music.mp3" },
                           @"C:\MyChain", "MainTrunk");

// Open a chain (file-based or memory chain with null workDir)
ZTBChain chain = ZTBChain.Open(@"C:\MyChain", "MainTrunk");
ZTBChain chain = ZTBChain.Open(@"C:\MyChain", "MainTrunk", ZTBHashType.CRC32Full);
ZTBChain chain = ZTBChain.Open(null, "MainTrunk");   // memory chain — wire callbacks before use

// Add blocks — caller supplies both GUIDs; strPrevBlockID = null for first block
ZTBBlockResult r = chain.AddBlock(strNewBlockID, strPrevBlockID, data);
ZTBBlockResult r = chain.AddBlockText(strNewBlockID, strPrevBlockID, "hello");
ZTBBlockResult r = chain.AddBlockFile(strNewBlockID, strPrevBlockID, "doc.bin");

// Checkpoint — labeled marker block (BlockType=Checkpoint)
ZTBBlockResult r = chain.AddCheckpoint(strNewBlockID, strPrevBlockID, "New financial year 2026");

// Truncate — rewrites the checkpoint's prev block as a Truncation block in-place,
// storing the full rolling ROM as its raw payload. Everything below can be SecureDeleted.
// The chain above the checkpoint remains fully verifiable.
ZTBBlockResult r = chain.Truncate(strNewBlockID, strCheckpointBlockID);

// Finalise — permanently seal the chain after the specified block
ZTBBlockResult r = chain.Finalise(strNewBlockID, strPrevBlockID, "Optional label");

// Branch — called on the BRANCH chain; strPrevBlockID is the trunk tip; strTrunkChainID is trunk's ChainID
ZTBChain branch  = ZTBChain.Open(@"C:\MyChain", "Sales");
ZTBBlockResult r = branch.AddBranch(strNewBlockID, strTrunkTipBlockID, data, "MainTrunk");

// Add subsequent branch blocks — open the branch chain and call AddBlock normally
ZTBBlockResult r = branch.AddBlock(strNewBlockID, strPrevBlockID, data);

// Fetch — direct access by BlockID
ZTBBlockResult r = chain.FetchBlock(strBlockID);
byte[] payload   = r.Payload;

// Verify — single block (blnWalk=false) or walk back to root (blnWalk=true)
// Stops cleanly at Genesis or Truncation block
ZTBVerifyResult v = chain.Verify(strBlockID, true);    // walk full chain
ZTBVerifyResult v = chain.Verify(strBlockID, false);   // single block only

// Callbacks — hook into block I/O (all null by default)
chain.OnBeforeSaveBlock = (result, bytes, path) => true;   // return false to cancel
chain.OnSaveBlock       = (result, bytes, path) => false;  // return true to skip disk write
chain.OnAfterSaveBlock  = (result, path)        => true;   // return false = treat as failed
chain.OnLoadBlock       = (filename)            => null;   // return bytes to override disk read
chain.OnFindGenesis     = (chainID)             => null;   // return genesis bytes for memory chains

// Properties
string id      = chain.ChainID;
string workDir = chain.WorkDir;
```

---

**Block format**

```
bytes 0-110:   RAW (not encoded)
  byte  0:     block_type
  byte  1:     block_version (1)
  byte  2:     is_branch
  bytes 3-38:  trunk_id       (36 bytes ASCII)
  bytes 39-74: block_id       (36 bytes ASCII)
  bytes 75-110:prev_block_id  (36 bytes ASCII)
bytes 111+:    ZOSCII encoded (Normal, Checkpoint, Finalise)
  byte  0:     hash_type
  bytes 1-4:   hash (of full unencoded block, hash field zeroed)
  bytes 5-8:   prev_hash (hash of entire previous block)
  bytes 9-12:  payload_len
  bytes 13-16: padded_len
  bytes 17+:   payload (xorshift32 padded to 512 bytes minimum)
```

Genesis block: bytes 0-65535, byte[0]=block_type=0, bytes[1-65535]=ROM. Not ZOSCII-encoded.
Truncation block: bytes 0-110 raw header + bytes 111-65646 raw ROM. Not ZOSCII-encoded.

**Tamper detection**

| HashType     | Detects tamper within 1KB | Detects tamper beyond 1KB |
|--------------|--------------------------|--------------------------|
| RollingFull  | Yes                      | Yes                      |
| Rolling1KB   | Yes                      | No (by design)           |
| CRC32Full    | Yes                      | Yes                      |
| CRC321KB     | Yes                      | No (by design)           |

**Truncation workflow**

```
(before truncation)               (after truncation + SecureDelete of old blocks)
block 13 (Normal)                 block 13 (Normal)
  └── block 12 (Normal)             └── block 12 (Normal)
        └── block 11 (Checkpoint)         └── block 11 (Checkpoint)
              └── block 10 (Normal)             └── block 10 (Truncation, payload=ROM)
                    └── block 9
                          └── ...
                                └── genesis
```

`Truncate(newGUID, checkpointBlockID)` overwrites the checkpoint's prev block in-place with a
Truncation block (same GUID, same PrevBlockID, BlockType=Truncation, raw ROM payload). All blocks
below the Truncation block can be `SecureDelete`d. The chain above the checkpoint verifies normally.

---

## ZWT

ZOSCII Web Tokens — a quantum-proof, opaque session/attestation token. The JWT analogue for ZOSCII:
an issuer attests a user to a relying party, but unlike JWT the whole token is UNSIGNAL-encoded
(**I(M;A)=0**) — payload, signatures and verification structure are indistinguishable from noise.
No asymmetric primitive, so there is nothing for Shor's algorithm to attack.

**Keys**

`SHAREDROM` — held by issuer + relying party (per-relationship key)
`ISSUERROM1` and `ISSUERROM2` — held by issuer only, never shared (one pair for all relying parties, or one pair per relying party). The issuer block is double-encoded with both.

**Claims visibility**

`sharedclaims`  — readable by BOTH parties (both hold SHAREDROM). Live in the SHAREDROM envelope.
`privateclaims` — readable only by the issuer. Sealed inside the issuer block (double-encoded with ISSUERROM1 and ISSUERROM2).

**Result type**

`ZWTResult`: Success, Error, Token, IssuerSignature, SharedSignature, SharedClaims, PrivateClaims

```csharp
public static byte[] NewSignature()
public static byte[] ClaimsFromString(string strClaims_a)
public static string ClaimsToString(byte[] arrClaims_a)
public static ZWTResult Issue(byte[] arrSharedSignature_a, byte[] arrPrivateClaims_a, byte[] arrSharedClaims_a, ZOSCIIRom objIssuerRom1_a, ZOSCIIRom objIssuerRom2_a, ZOSCIIRom objSharedRom_a)
public static ZWTResult Open(byte[] arrToken_a, ZOSCIIRom objSharedRom_a)
public static ZWTResult Introspect(byte[] arrIssuerData_a, byte[] arrPresentedSharedSignature_a, ZOSCIIRom objIssuerRom1_a, ZOSCIIRom objIssuerRom2_a)
public static ZWTResult Verify(byte[] arrToken_a, ZOSCIIRom objSharedRom_a, ZOSCIIRom objIssuerRom1_a, ZOSCIIRom objIssuerRom2_a)
public static ZWTResult UpdateSharedClaims(byte[] arrToken_a, byte[] arrNewSharedClaims_a, ZOSCIIRom objSharedRom_a)
```

```csharp
using (ZOSCIIRom sharedRom = ZOSCIIRom.FromFile("shared.rom"))
using (ZOSCIIRom issuerRom1 = ZOSCIIRom.FromFile("issuer1.rom"))
using (ZOSCIIRom issuerRom2 = ZOSCIIRom.FromFile("issuer2.rom"))
{
    // --- Issuer side: mint a token (holds all three ROMs) ---
    byte[] arrSharedSig     = ZWT.NewSignature();                       // GUID-based, 16 bytes
    byte[] arrPrivateClaims = ZWT.ClaimsFromString("{\"uid\":\"42\"}"); // issuer-only
    byte[] arrSharedClaims  = ZWT.ClaimsFromString("{\"scope\":\"read\"}");

    ZWTResult objIssue = ZWT.Issue(arrSharedSig, arrPrivateClaims, arrSharedClaims, issuerRom1, issuerRom2, sharedRom);
    byte[] arrToken = objIssue.Token;   // the opaque ZWT — send to relying party

    // --- Relying party side: open the token (holds SHAREDROM only) ---
    ZWTResult objOpen = ZWT.Open(arrToken, sharedRom);
    byte[] arrSig     = objOpen.SharedSignature;               // read shared signature
    string strScope   = ZWT.ClaimsToString(objOpen.SharedClaims);
    byte[] arrIssuerData = objOpen.IssuerSignature;            // opaque to RP — for introspection

    // --- Issuer side: introspect (holds ISSUERROM1 + ISSUERROM2) ---
    // Double-decodes the issuer block, confirms the presented sharedsig matches the
    // sealed copy, and recovers the issuer-only private claims.
    ZWTResult objIntro = ZWT.Introspect(arrIssuerData, arrSig, issuerRom1, issuerRom2);
    string strUid = ZWT.ClaimsToString(objIntro.PrivateClaims);

    // --- Issuer-local convenience: open + introspect in one call (needs all three ROMs) ---
    ZWTResult objVerify = ZWT.Verify(arrToken, sharedRom, issuerRom1, issuerRom2);
}

// Updating Relyer claims
// 1. RP has token:
byte[] token = ...;

// 2. RP updates shared claims:
byte[] newSharedClaims = ClaimsFromString("{ \"scope\": \"premium\", \"email\": \"new@a.com\" }");

ZWTResult updated = ZWT.UpdateSharedClaims(
    token,
    newSharedClaims,
    sharedRom
);

byte[] newToken = updated.Token;  // New token with updated shared claims

// 3. RP uses new token:
ZWTResult open = ZWT.Open(newToken, sharedRom);
string scope = open.SharedClaims;  // "premium" ✅
```

**Why only the issuer can forge a valid token**

- Without SHAREDROM a forger can neither open nor produce a ZWT — outsiders are locked out.
- A relying party holds SHAREDROM, so it can produce *a* `sharedsignature`, but not a matching
  `issuersignature` — that requires ISSUERROM1 and ISSUERROM2 (issuer-only).
- A `sharedsignature` is valid only when it matches the copy sealed inside `issuersignature`.
- Therefore only the issuer can mint a token the issuer will accept. A forged shared-sig has no
  matching sealed copy and fails introspection.

**What is the purpose of Issuer Claims?**

- Since the internal incoding containing issuer claims is also UNSIGNAL Protocol secured with
  the issuers own ROM, they can place private information in there knowing it will be secure.
- Issuer Claim information can be freely passed around and back to multiple issuers servers with
  full validation and secrecy.

**Not solved by ZWT alone**

- **Replay to the legitimate relying party** — a stolen ZWT can be replayed to its intended
  recipient. Bind a server-issued nonce inside the token; avoid clock-based expiry.
- **Revocation** — stateless local verification can't revoke mid-life. Route `issuersignature`
  through issuer introspection instead — gains revocation, costs a round-trip.

Each envelope binds its full field preimage with a `ZRollingHash` (4-byte) integrity field, so
`Open` and `Introspect` reject any malformed or tampered token. `sharedclaims` are deliberately
not sealed inside `issuersignature` — sealing shared-readable data under the issuer's private ROM
would serve no purpose, and an RP editing its own copy of shared data under its own half of a
per-relationship key affects nothing but its own door.

---

## Encryption

Plugin-based encryption registry. Drop DLLs implementing `IEncryptionProvider` into a folder,
probe by filename, and the encryption appears in the dropdown automatically.

Note: No encryption plugins or encryption code is supplied in this nuget. The encryption plugins 
can be obtained from the encryption GitHub repo.

**Interface** (implemented by each plugin DLL):

```csharp
public interface IEncryptionProvider
{
    EncryptionType GetEncryptionType();
    byte[]? Encrypt(byte[]? plaintext, byte[]? key, byte[]? iv = null);
    byte[]? Decrypt(byte[]? ciphertext, byte[]? key, byte[]? iv = null);
    byte[]? GenerateKey();
    byte[]? GenerateIV();
}

public struct EncryptionType
{
    public string Code { get; set; }   // "AES256GCM"
    public string Name { get; set; }   // "AES-256-GCM"
}
```

**Host application usage:**

```csharp
using Encrypt;

var registry = new Encryption();

// Probe specific DLLs — caller controls trust
string[] arrPlugins = new[] { "encryption1.dll", "encryption2.dll", "encryption3.dll" };
registry.Probe(@"Plugins\", arrPlugins);

// Populate dropdown — GetMethods() returns string[,] matching GetBootstrapMethods() pattern
string[,] arrMethods = registry.GetMethods();
// arrMethods[0,0] = "AES256GCM"   → dropdown value
// arrMethods[0,1] = "AES-256-GCM" → dropdown label

// User picks "AES256GCM" from dropdown
string strCode = "AES256GCM";
byte[] arrKey  = registry.GenerateKey(strCode);
byte[] arrIV   = registry.GenerateIV(strCode);   // null if algorithm doesn't use IV

// UNSIGNAL first, then encrypt
byte[] arrUnsignalled = UEncode.Bytes(arrPlaintext, rom);
byte[] arrCiphertext  = registry.Encrypt(strCode, arrUnsignalled, arrKey, arrIV);
byte[] arrRecovered   = registry.Decrypt(strCode, arrCiphertext, arrKey, arrIV);
```

**Available encryption providers** (compile each to its own DLL):

| DLL | Code | Name | IV Required |
|-----|------|------|-------------|
| XORProvider.dll | `XOR` | XOR | ❌ |
| DESProvider.dll | `DES` | DES | ✅ (8 bytes) |
| TripleDESProvider.dll | `3DES` | 3DES | ✅ (8 bytes) |
| RC2Provider.dll | `RC2` | RC2 | ✅ (8 bytes) |
| AESCBCProvider.dll | `AES256CBC` | AES-256-CBC | ✅ (16 bytes) |
| AESCCMProvider.dll | `AES256CCM` | AES-256-CCM | ✅ (13 bytes) |
| AESGCMProvider.dll | `AES256GCM` | AES-256-GCM | ✅ (12 bytes) |
| ChaCha20Provider.dll | `CHACHA20` | ChaCha20-Poly1305 | ✅ (12 bytes) |
| RSAProvider.dll | `RSA4096` | RSA-4096 | ❌ |
| BRAINLESSProvider.dll | `BRAINLESS` | BRAINLESS Ouroboros | ✅ (1 byte: mode selector) |

**Output formats** (ciphertext includes IV + tag where applicable):

- CBC/3DES/DES/RC2: `IV (8/16 bytes) + Ciphertext`
- GCM/CCM/ChaCha20: `IV (12-13 bytes) + Tag (16 bytes) + Ciphertext`
- XOR: `Ciphertext only`
- RSA: `Ciphertext only` (key blob determines public/private)

**Adding a new encryption provider** (Amiga-style probing):

```csharp
// Drop encryption4.dll in the plugins folder, add to filename list
registry.Probe("plugins", new[] { "encryption1.dll", "encryption2.dll", "encryption3.dll", "encryption4.dll" });
// encryption4.dll appears in dropdown automatically — no code changes
```

**Building a custom provider:**

1. Reference `Encrypt` namespace (or copy the interface into your project)
2. Implement `IEncryptionProvider`
3. Compile to DLL
4. Drop in plugins folder

```csharp
public class MyProvider : IEncryptionProvider
{
    public EncryptionType GetEncryptionType()
    {
        return new EncryptionType { Code = "MYCIPHER", Name = "My Cipher" };
    }

    public byte[]? Encrypt(byte[]? plaintext, byte[]? key, byte[]? iv = null)
    {
        // Your encryption logic here
    }

    public byte[]? Decrypt(byte[]? ciphertext, byte[]? key, byte[]? iv = null)
    {
        // Your decryption logic here
    }

    public byte[]? GenerateKey() => new byte[32];
    public byte[]? GenerateIV() => null;
}
```

**Why this works with POWERUP:**

UNSIGNAL pre-encoding removes the attack surface before encryption sees the data.
Broken algorithms (DES, RC2, XOR) become viable because the pre-encoding eliminates
the patterns their vulnerabilities depend on. The encryption algorithm becomes
almost irrelevant — the security is in the UNSIGNAL layer.

---

## License

UNSIGNAL, PENTAGONE - UNINTELLIGENCE SOFTWARE LICENSE v1.1
ZOSCII, ZOSCII MQ - MIT LICENSE - if unclear, use the ZOSCII Nuget
Commercial Licenses Available

(c) 2026 Cyborg Unicorn Pty Ltd - https://cyborgunicorn.com.au