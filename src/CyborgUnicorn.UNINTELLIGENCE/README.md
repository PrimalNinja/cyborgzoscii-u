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

## License

UNSIGNAL, PENTAGONE - UNINTELLIGENCE SOFTWARE LICENSE v1.1
ZOSCII, ZOSCII MQ - MIT LICENSE - if unclear, use the ZOSCII Nuget
Commercial Licenses Available

(c) 2026 Cyborg Unicorn Pty Ltd - https://cyborgunicorn.com.au