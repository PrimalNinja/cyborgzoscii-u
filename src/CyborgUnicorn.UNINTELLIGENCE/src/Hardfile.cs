// CyborgUnicorn.ZOSCII - Hardfile
//
// A store with no visible files. One set per store:
//
//   <name>.hfd  data   noise + encoded meta / content blobs, no structure at all
//   <name>.hfi  index  fixed 84-byte records, one per node, indirected through the hardfile image
//   <name>.hfm  mirror an identical copy of the index, written after it
//   <name>.hft  temp   a growth-only set for intermediates, secure-deleted on close (ScratchHardfile)
//
// Envelope (every file of the set): bytes 0..7 are two indirected 16-bit numbers, a random prefix length
// (8..2048) and a random suffix length (48..4096). Content starts after the prefix. The suffix region at the
// end holds an indirected block [set GUID 16][unit 2][last written 8][rolling hash 4] (60 bytes on disk);
// the same GUID, unit and last-written time are in every file of the set, and the block is rewritten on
// every change, so the true last-written time lives only where the image can read it. Open decodes every file's envelope: a hash that fails is the wrong image, GUIDs that
// differ are files from different sets, and in either case nothing opens and nothing is written. Growth
// appends noise, frees the old suffix block and writes a fresh random-length suffix at the new end.
//
// Index record (all numbers indirected: each byte -> a random 16-bit address in the image holding that byte):
//
//   [NEXT 8][CHILD 8][DATA 16][META 8][SIZE 16][BLOB 8][CHAIN 8][FLAG 4][HASH 8]   = 84 bytes
//
// NEXT / CHILD are record numbers (0 = none; record 0 is the root, so 0 never links). DATA is the blob's
// offset in the data file, META / SIZE the encoded meta and content lengths. Three ZOSCII rolling hashes:
// BLOB over the encoded blob as stored (meta + content), CHAIN over this node's children's record numbers
// in order (0 when it has none), HASH over everything before it. FLAG is 1 for a live record, 0 for a dead
// one (random values in every other field, so it carries nothing about what it was) and 2 for a record that
// is marked out: its blob failed its hash in every copy, the space is held, the blob is never replicated or
// repaired, and the user deletes it or restores it when a good copy appears. Meta (name, kind, dates) and content are encoded separately by the caller's IHardfileCodec
// (data images or a scheme), so listing a folder decodes metas and never contents. A node with children
// is a folder.
//
// Space: the data file and the index are noise-filled and grown in chunks, so sizes say nothing. Blobs are
// allocated in whole units (a per-store size fixed at create, default 1 KB), so holes stay usable and the
// free list stays bounded. The free list (every unit not covered by a live record, coalesced) is rebuilt at
// open and kept in memory: a new blob takes the first hole it fits, dead records are reused, growth happens
// only when nothing fits.
// Delete unlinks the node and its subtree, overwrites their blobs with noise and zeroes their records.
// CopyTo writes a fresh set from the live nodes; nothing is compacted in place.
//
// Replicas: any number of extra data / index paths (other devices). A write goes to the primary first and,
// only if that succeeded, the identical bytes go to the mirror and replicas: one operation, copied, never
// repeated. Reads take the first copy whose hashes pass; a shorter copy is caught up on open; Verify checks
// hashes at the level asked (structure, structure + files, one file); Repair copies passing over failing.
//
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.2

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    public interface IHardfileCodec
    {
        byte[] Encode(byte[] arrPlain_a);
        byte[] Decode(byte[] arrEncoded_a);
    }

    public sealed class HardfileEntry
    {
        public string Name = "";
        public bool IsFolder = false;
        public long Created = 0;
        public long Modified = 0;
        public long ContentLength = 0;
        public long Record = 0;
        public bool MarkedOut = false;
        public bool Unreadable = false;   // meta did not decode: marked out, or a bad blob with no good copy

        internal byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes((IsFolder ? "D" : "F") + "\n" + Created + "\n" + Modified + "\n" + ContentLength + "\n" + Name);
        }

        internal static HardfileEntry FromBytes(byte[] arrMeta_a)
        {
            HardfileEntry objResult = null;
            try
            {
                string[] arrParts = Encoding.UTF8.GetString(arrMeta_a).Split(new char[] { '\n' }, 5);
                if (arrParts.Length == 5 && (arrParts[0] == "D" || arrParts[0] == "F"))
                {
                    objResult = new HardfileEntry();
                    objResult.IsFolder = arrParts[0] == "D";
                    objResult.Created = long.Parse(arrParts[1]);
                    objResult.Modified = long.Parse(arrParts[2]);
                    objResult.ContentLength = long.Parse(arrParts[3]);
                    objResult.Name = arrParts[4];
                }
            }
            catch { objResult = null; }
            return objResult;
        }
    }

    public sealed class HardfilePaths
    {
        public string Data = "";
        public string Index = "";
        public string Mirror = "";
        public List<string> DataReplicas = new List<string>();
        public List<string> IndexReplicas = new List<string>();

        public static HardfilePaths FromBase(string strBase_a)
        {
            HardfilePaths objPaths = new HardfilePaths();
            objPaths.Data = strBase_a + ".hfd";
            objPaths.Index = strBase_a + ".hfi";
            objPaths.Mirror = strBase_a + ".hfm";
            return objPaths;
        }

        public void AddReplicaFolder(string strFolder_a, string strBaseName_a)
        {
            DataReplicas.Add(System.IO.Path.Combine(strFolder_a, strBaseName_a + ".hfd"));
            IndexReplicas.Add(System.IO.Path.Combine(strFolder_a, strBaseName_a + ".hfi"));
        }
    }

    public sealed class HardfileReport
    {
        public int BadIndexRecords = 0;
        public int BadMirrorRecords = 0;
        public int ReplicaProblems = 0;
        public int Orphans = 0;
        public int BadChains = 0;
        public int BadBlobs = 0;
        public int MarkedOut = 0;
        public List<string> Notes = new List<string>();
        public bool Clean { get { return BadIndexRecords == 0 && BadMirrorRecords == 0 && ReplicaProblems == 0 && Orphans == 0 && BadChains == 0 && BadBlobs == 0; } }
    }

    public sealed class Hardfile : IDisposable
    {
        public const int RECORD_SIZE = 84;
        private const int FLAG_BYTES = 2;
        private const long FLAG_LIVE = 1;
        private const long FLAG_DEAD = 0;
        private const long FLAG_MARKEDOUT = 2;
        private const int PTR_BYTES = 4;
        private const int LEN_BYTES = 8;
        private const int HASH_BYTES = 4;
        private const long INDEX_MIN = 65536;
        private const int INDEX_PERCENT = 1;

        private sealed class clsFileGroup : IDisposable
        {
            public List<FileStream> Streams = new List<FileStream>();
            public List<string> Paths = new List<string>();
            public FileStream Primary { get { return Streams[0]; } }
            public long Length { get { return Streams[0].Length; } }

            // the primary is written first; only when that succeeded are the identical bytes copied to the others
            public void Write(long lngAt_a, byte[] arrBytes_a, int intCount_a)
            {
                Streams[0].Position = lngAt_a;
                Streams[0].Write(arrBytes_a, 0, intCount_a);
                for (int intI = 1; intI < Streams.Count; intI++)
                {
                    try { Streams[intI].Position = lngAt_a; Streams[intI].Write(arrBytes_a, 0, intCount_a); }
                    catch { /* a copy that cannot be written is behind; Open catches it up, Verify reports it */ }
                }
            }

            public void Flush() { for (int intI = 0; intI < Streams.Count; intI++) { Streams[intI].Flush(); } }

            public void Dispose()
            {
                for (int intI = 0; intI < Streams.Count; intI++) { try { Streams[intI].Flush(); Streams[intI].Dispose(); } catch { } }
                Streams.Clear();
            }
        }

        private struct Record
        {
            public long lngNext;
            public long lngChild;
            public long lngData;
            public long lngMeta;
            public long lngSize;
            public long lngBlobHash;
            public long lngChainHash;
            public long lngFlag;
        }

        private struct Extent
        {
            public long lngAt;
            public long lngLen;
        }

        private clsFileGroup m_objData = null;
        private clsFileGroup m_objIndex = null;
        private ZOSCIIRom m_objImage = null;
        private RomData m_objRom;
        private long m_lngGrowth = 0;
        private Random m_objRand = new Random();
        private HardfilePaths m_objPaths = null;
        private string m_strLastError = "";
        private long m_lngRecordCount = -1;
        private List<Extent> m_arrFree = null;          // free data regions, sorted by offset, coalesced
        private List<long> m_arrFreeRecords = null;      // dead record numbers available for reuse
        private byte[] m_arrGuid = null;                 // set identity, inside every file's envelope
        private long m_lngDataPrefix = 0;
        private long m_lngDataSuffix = 0;
        private long m_lngIndexPrefix = 0;
        private long m_lngIndexSuffix = 0;

        private const int ENVELOPE_HEADER = 8;           // two indirected 16-bit numbers
        private const int ENVELOPE_BLOCK = 60;           // 30 plain bytes (guid + unit + last written + hash) indirected
        private long m_lngLastWritten = 0;               // unix seconds, inside the envelope
        private long m_lngLastWrittenRead = 0;           // scratch: the value the last readEnvelope saw
        private bool m_blnReadOnly = false;              // opened by a reader (the core peeking a store it minted): never writes, never catches up
        public const int UNIT_DEFAULT = 1024;
        private long m_lngUnit = UNIT_DEFAULT;           // allocation unit in bytes, 256..65536
        private const int PREFIX_MIN = 8;
        private const int PREFIX_MAX = 2048;
        private const int SUFFIX_MIN = 48;
        private const int SUFFIX_MAX = 4096;

        public string LastError { get { return m_strLastError; } }
        public HardfilePaths Paths { get { return m_objPaths; } }
        public long DataLength { get { return m_objData == null ? 0 : m_objData.Length; } }
        public long IndexLength { get { return m_objIndex == null ? 0 : m_objIndex.Length; } }
        public string SetId { get { return m_arrGuid == null ? "" : new Guid(m_arrGuid).ToString("N"); } }
        public long Unit { get { return m_lngUnit; } }
        /// <summary>When the set was last changed, unix seconds, read from inside the envelope (file times are randomised).</summary>
        public long LastWritten { get { return m_lngLastWritten; } }
        /// <summary>Byte offset of an index record in the index file (tooling / tests).</summary>
        public long RecordOffset(long lngRecord_a) { return m_lngIndexPrefix + lngRecord_a * RECORD_SIZE; }

        // ---------------------------------------------------------------
        // create / open / close
        // ---------------------------------------------------------------

        public static Hardfile Create(HardfilePaths objPaths_a, long lngSize_a, long lngGrowth_a, ZOSCIIRom objImage_a) { return Create(objPaths_a, lngSize_a, lngGrowth_a, objImage_a, UNIT_DEFAULT); }

        /// <summary>lngUnit_a: allocation unit in bytes (256..65536, rounded to a power of two); every blob takes whole units.</summary>
        public static Hardfile Create(HardfilePaths objPaths_a, long lngSize_a, long lngGrowth_a, ZOSCIIRom objImage_a, long lngUnit_a)
        {
            Hardfile objResult = null;
            try
            {
                if (objImage_a != null && objImage_a.IsLoaded && objPaths_a != null)
                {
                    Hardfile objHf = new Hardfile();
                    objHf.m_objPaths = objPaths_a;
                    objHf.m_objImage = objImage_a;
                    objHf.m_objRom = objImage_a.GetRomData();
                    objHf.m_lngGrowth = lngGrowth_a;
                    objHf.m_objData = openGroup(objPaths_a.Data, objPaths_a.DataReplicas, null, FileMode.Create, false);
                    objHf.m_objIndex = openGroup(objPaths_a.Index, objPaths_a.IndexReplicas, objPaths_a.Mirror, FileMode.Create, false);
                    objHf.m_arrGuid = Guid.NewGuid().ToByteArray();
                    objHf.m_lngUnit = normaliseUnit(lngUnit_a);
                    objHf.m_lngLastWritten = now();
                    HardfileEntry objRoot = new HardfileEntry();
                    objRoot.IsFolder = true;
                    objRoot.Created = now();
                    objRoot.Modified = objRoot.Created;
                    byte[] arrMeta = UEncode.Bytes(objRoot.ToBytes(), objImage_a);
                    long lngDataSize = Math.Max(lngSize_a, PREFIX_MAX + SUFFIX_MAX + arrMeta.Length + 1);
                    objHf.fillNoise(objHf.m_objData, 0, lngDataSize);
                    objHf.fillNoise(objHf.m_objIndex, 0, objHf.indexSizeFor(lngDataSize) + PREFIX_MAX + SUFFIX_MAX);
                    objHf.m_lngDataPrefix = PREFIX_MIN + objHf.m_objRand.Next(PREFIX_MAX - PREFIX_MIN + 1);
                    objHf.m_lngDataSuffix = SUFFIX_MIN + objHf.m_objRand.Next(SUFFIX_MAX - SUFFIX_MIN + 1);
                    objHf.m_lngIndexPrefix = PREFIX_MIN + objHf.m_objRand.Next(PREFIX_MAX - PREFIX_MIN + 1);
                    objHf.m_lngIndexSuffix = SUFFIX_MIN + objHf.m_objRand.Next(SUFFIX_MAX - SUFFIX_MIN + 1);
                    objHf.writeEnvelope(objHf.m_objData, objHf.m_lngDataPrefix, objHf.m_lngDataSuffix);
                    objHf.writeEnvelope(objHf.m_objIndex, objHf.m_lngIndexPrefix, objHf.m_lngIndexSuffix);
                    Record objR = new Record();
                    objR.lngFlag = FLAG_LIVE;
                    objR.lngData = objHf.m_lngDataPrefix;
                    objR.lngMeta = arrMeta.Length;
                    objR.lngBlobHash = hashBytes(arrMeta);
                    objHf.m_objData.Write(objHf.m_lngDataPrefix, arrMeta, arrMeta.Length);
                    objHf.writeRecord(0, objR);
                    objHf.m_lngRecordCount = 1;
                    objHf.m_arrFreeRecords = new List<long>();
                    objHf.m_arrFree = new List<Extent>();
                    long lngRootUnits = objHf.roundUp(arrMeta.Length);
                    objHf.freeAdd(objHf.m_lngDataPrefix + lngRootUnits, objHf.m_objData.Length - objHf.m_lngDataSuffix - objHf.m_lngDataPrefix - lngRootUnits);
                    objHf.m_objData.Flush();
                    objHf.m_objIndex.Flush();
                    objResult = objHf;
                }
            }
            catch { objResult = null; }
            return objResult;
        }

        /// <summary>Open for reading only, sharing with a running writer: no catch-up, no stamps, no timestamp randomisation. For the core peeking a store it minted.</summary>
        public static Hardfile OpenReadOnly(HardfilePaths objPaths_a, ZOSCIIRom objImage_a) { return open(objPaths_a, 0, objImage_a, true); }

        public static Hardfile Open(HardfilePaths objPaths_a, long lngGrowth_a, ZOSCIIRom objImage_a) { return open(objPaths_a, lngGrowth_a, objImage_a, false); }

        private static Hardfile open(HardfilePaths objPaths_a, long lngGrowth_a, ZOSCIIRom objImage_a, bool blnReadOnly_a)
        {
            Hardfile objResult = null;
            try
            {
                if (objImage_a != null && objImage_a.IsLoaded && objPaths_a != null && File.Exists(objPaths_a.Data) && File.Exists(objPaths_a.Index))
                {
                    Hardfile objHf = new Hardfile();
                    objHf.m_objPaths = objPaths_a;
                    objHf.m_objImage = objImage_a;
                    objHf.m_objRom = objImage_a.GetRomData();
                    objHf.m_lngGrowth = lngGrowth_a;
                    objHf.m_blnReadOnly = blnReadOnly_a;
                    objHf.m_objData = openGroup(objPaths_a.Data, objPaths_a.DataReplicas, null, blnReadOnly_a ? FileMode.Open : FileMode.OpenOrCreate, blnReadOnly_a);
                    objHf.m_objIndex = openGroup(objPaths_a.Index, objPaths_a.IndexReplicas, objPaths_a.Mirror, blnReadOnly_a ? FileMode.Open : FileMode.OpenOrCreate, blnReadOnly_a);
                    if (!blnReadOnly_a)
                    {
                        objHf.catchUpGroup(objHf.m_objData);
                        objHf.catchUpGroup(objHf.m_objIndex);
                    }
                    bool blnOk = objHf.openEnvelopes();
                    Record objR = new Record();
                    blnOk = blnOk && objHf.readRecord(0, out objR);
                    if (blnOk)
                    {
                        byte[] arrMeta = objHf.readBlob(objR);
                        byte[] arrPlain = arrMeta == null ? null : UDecode.Bytes(arrMeta, objImage_a);
                        HardfileEntry objRoot = arrPlain == null ? null : HardfileEntry.FromBytes(arrPlain);
                        blnOk = objRoot != null && objRoot.IsFolder && objRoot.Name.Length == 0;
                    }
                    if (blnOk) { objHf.rebuildFreeLists(); objResult = objHf; }
                    else { objHf.Dispose(); }
                }
            }
            catch { objResult = null; }
            return objResult;
        }

        public void Dispose()
        {
            List<string> arrPaths = new List<string>();
            try { if (!m_blnReadOnly && m_lngLastWritten > 0) { stampEnvelopes(); } } catch { }
            if (m_objData != null) { arrPaths.AddRange(m_objData.Paths); m_objData.Dispose(); m_objData = null; }
            if (m_objIndex != null) { arrPaths.AddRange(m_objIndex.Paths); m_objIndex.Dispose(); m_objIndex = null; }
            if (!m_blnReadOnly) { randomiseTimestamps(arrPaths); }
        }

        /// <summary>Set every file of the set to a random created / modified / accessed time within the past five years. Done automatically at Dispose; callable while mounted for long sessions.</summary>
        public void RandomiseTimestamps()
        {
            List<string> arrPaths = new List<string>();
            if (m_objData != null) { arrPaths.AddRange(m_objData.Paths); }
            if (m_objIndex != null) { arrPaths.AddRange(m_objIndex.Paths); }
            randomiseTimestamps(arrPaths);
        }

        // a different random moment per file, so the files of a set do not even share a time; NTFS's MFT change
        // time cannot be set from user mode and is not covered (exFAT / FAT have no such field)
        private void randomiseTimestamps(List<string> arrPaths_a)
        {
            for (int intI = 0; intI < arrPaths_a.Count; intI++)
            {
                try
                {
                    if (File.Exists(arrPaths_a[intI]))
                    {
                        DateTime dtWhen = DateTime.UtcNow.AddSeconds(-(double)m_objRand.Next(1, 5 * 365 * 24 * 3600));
                        DateTime dtCreated = dtWhen.AddSeconds(-(double)m_objRand.Next(0, 365 * 24 * 3600));
                        File.SetCreationTimeUtc(arrPaths_a[intI], dtCreated);
                        File.SetLastWriteTimeUtc(arrPaths_a[intI], dtWhen);
                        File.SetLastAccessTimeUtc(arrPaths_a[intI], dtWhen.AddSeconds(m_objRand.Next(0, 30 * 24 * 3600)));
                    }
                }
                catch { /* a replica on read-only or foreign media keeps its times */ }
            }
        }

        // a writer opens read/write allowing readers; a reader opens read-only allowing the writer
        private static clsFileGroup openGroup(string strPrimary_a, List<string> arrReplicas_a, string strMirror_a, FileMode objMode_a, bool blnReadOnly_a)
        {
            FileAccess objAccess = blnReadOnly_a ? FileAccess.Read : FileAccess.ReadWrite;
            FileShare objShare = blnReadOnly_a ? FileShare.ReadWrite : FileShare.Read;
            clsFileGroup objGroup = new clsFileGroup();
            objGroup.Paths.Add(strPrimary_a);
            objGroup.Streams.Add(new FileStream(strPrimary_a, objMode_a, objAccess, objShare));
            if (strMirror_a != null && strMirror_a.Length > 0)
            {
                objGroup.Paths.Add(strMirror_a);
                objGroup.Streams.Add(new FileStream(strMirror_a, objMode_a, objAccess, objShare));
            }
            for (int intI = 0; arrReplicas_a != null && intI < arrReplicas_a.Count; intI++)
            {
                try
                {
                    string strDir = System.IO.Path.GetDirectoryName(arrReplicas_a[intI]);
                    if (strDir != null && strDir.Length > 0 && !Directory.Exists(strDir)) { Directory.CreateDirectory(strDir); }
                    objGroup.Paths.Add(arrReplicas_a[intI]);
                    objGroup.Streams.Add(new FileStream(arrReplicas_a[intI], objMode_a, objAccess, objShare));
                }
                catch { }
            }
            return objGroup;
        }

        private void catchUpGroup(clsFileGroup objGroup_a)
        {
            long lngLen = objGroup_a.Primary.Length;
            byte[] arrBuf = new byte[1 << 20];
            for (int intI = 1; intI < objGroup_a.Streams.Count; intI++)
            {
                FileStream objRep = objGroup_a.Streams[intI];
                if (objRep.Length != lngLen)
                {
                    long lngFrom = Math.Min(objRep.Length, lngLen);
                    if (objRep.Length > lngLen) { objRep.SetLength(lngLen); }
                    long lngPos = lngFrom;
                    while (lngPos < lngLen)
                    {
                        int intChunk = (int)Math.Min(arrBuf.Length, lngLen - lngPos);
                        objGroup_a.Primary.Position = lngPos;
                        int intGot = objGroup_a.Primary.Read(arrBuf, 0, intChunk);
                        if (intGot <= 0) { lngPos = lngLen; }
                        else { objRep.Position = lngPos; objRep.Write(arrBuf, 0, intGot); lngPos += intGot; }
                    }
                    objRep.Flush();
                }
            }
        }

        // ---------------------------------------------------------------
        // public tree operations
        // ---------------------------------------------------------------

        public List<HardfileEntry> List(string strFolder_a, IHardfileCodec objCodec_a)
        {
            List<HardfileEntry> arrResult = new List<HardfileEntry>();
            long lngFolder = findNode(strFolder_a, objCodec_a);
            Record objF;
            if (lngFolder >= 0 && readRecord(lngFolder, out objF))
            {
                long lngChild = objF.lngChild;
                while (lngChild > 0)
                {
                    Record objC;
                    if (!readRecord(lngChild, out objC)) { lngChild = 0; }
                    else
                    {
                        HardfileEntry objEntry = readMeta(lngChild, objC, objCodec_a);
                        if (objEntry == null)
                        {
                            // meta unreadable: marked out, or a bad blob not yet repaired; shown by record number so it can be acted on
                            objEntry = new HardfileEntry();
                            objEntry.Name = "#" + lngChild;
                            objEntry.IsFolder = objC.lngChild > 0;
                            objEntry.ContentLength = objC.lngSize;
                            objEntry.Unreadable = true;
                        }
                        if (objEntry != null) { objEntry.Record = lngChild; objEntry.MarkedOut = isMarkedOut(objC); arrResult.Add(objEntry); }
                        lngChild = objC.lngNext;
                    }
                }
            }
            return arrResult;
        }

        /// <summary>Flag a file as marked out: space held, blob never replicated or repaired, not readable until restored.</summary>
        public bool MarkOut(string strPath_a, IHardfileCodec objCodec_a) { return setFlag(strPath_a, objCodec_a, FLAG_MARKEDOUT); }

        /// <summary>Flag a marked-out file live again (its blob must then pass its hash to be readable).</summary>
        public bool Restore(string strPath_a, IHardfileCodec objCodec_a) { return setFlag(strPath_a, objCodec_a, FLAG_LIVE); }

        /// <summary>The stored bytes of a file (encoded meta + content) exactly as held, hash or no hash: for salvage of a marked-out or bad blob. Null when the record is dead or unknown.</summary>
        public byte[] ExportRaw(string strPath_a, IHardfileCodec objCodec_a)
        {
            byte[] arrResult = null;
            long lngNode = findNode(strPath_a, objCodec_a);
            Record objR;
            if (lngNode > 0 && readRecord(lngNode, out objR) && !isDead(objR))
            {
                for (int intS = 0; arrResult == null && intS < m_objData.Streams.Count; intS++) { arrResult = readDataFrom(m_objData.Streams[intS], objR.lngData, objR.lngMeta + objR.lngSize); }
            }
            return arrResult;
        }

        /// <summary>Every marked-out node's held bytes written to strOutFolder_a as <record>.hfseg (a hardfile segment: encoded meta + content as stored). Existing files are skipped. Returns files written.</summary>
        public int ExportMarkedOut(string strOutFolder_a)
        {
            int intWritten = 0;
            try
            {
                if (!Directory.Exists(strOutFolder_a)) { Directory.CreateDirectory(strOutFolder_a); }
                long lngCount = recordCount();
                for (long lngI = 1; lngI < lngCount; lngI++)
                {
                    Record objR;
                    if (readRecord(lngI, out objR) && isMarkedOut(objR))
                    {
                        string strOut = System.IO.Path.Combine(strOutFolder_a, lngI + ".hfseg");
                        if (!File.Exists(strOut))
                        {
                            byte[] arrRaw = null;
                            for (int intS = 0; arrRaw == null && intS < m_objData.Streams.Count; intS++) { arrRaw = readDataFrom(m_objData.Streams[intS], objR.lngData, objR.lngMeta + objR.lngSize); }
                            if (arrRaw != null) { File.WriteAllBytes(strOut, arrRaw); intWritten++; }
                        }
                    }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; }
            return intWritten;
        }

        private bool setFlag(string strPath_a, IHardfileCodec objCodec_a, long lngFlag_a)
        {
            bool blnResult = false;
            if (m_blnReadOnly) { m_strLastError = "read-only"; }
            else
            {
            long lngNode = findNode(strPath_a, objCodec_a);
            Record objR;
            if (lngNode > 0 && readRecord(lngNode, out objR) && !isDead(objR))
            {
                objR.lngFlag = lngFlag_a;
                writeRecord(lngNode, objR);
                touch();
                m_objIndex.Flush();
                m_objData.Flush();
                blnResult = true;
            }
            }
            return blnResult;
        }

        public bool Exists(string strPath_a, IHardfileCodec objCodec_a) { return findNode(strPath_a, objCodec_a) >= 0; }

        public bool IsReadOnly { get { return m_blnReadOnly; } }

        public bool MakeFolder(string strPath_a, IHardfileCodec objCodec_a)
        {
            bool blnResult = false;
            if (m_blnReadOnly) { m_strLastError = "read-only"; }
            else
            try
            {
                string[] arrParts = splitPath(strPath_a);
                long lngParent = 0;
                blnResult = true;
                for (int intI = 0; blnResult && intI < arrParts.Length; intI++)
                {
                    long lngFound = findChild(lngParent, arrParts[intI], objCodec_a);
                    if (lngFound < 0)
                    {
                        HardfileEntry objEntry = new HardfileEntry();
                        objEntry.Name = arrParts[intI];
                        objEntry.IsFolder = true;
                        objEntry.Created = now();
                        objEntry.Modified = objEntry.Created;
                        lngFound = appendNode(lngParent, objEntry, null, objCodec_a);
                        if (lngFound < 0) { blnResult = false; }
                    }
                    lngParent = lngFound;
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; blnResult = false; }
            return blnResult;
        }

        public bool Put(string strPath_a, byte[] arrContent_a, IHardfileCodec objCodec_a)
        {
            bool blnResult = false;
            if (m_blnReadOnly) { m_strLastError = "read-only"; }
            else
            try
            {
                string[] arrParts = splitPath(strPath_a);
                if (arrParts.Length > 0 && arrContent_a != null)
                {
                    string strFolder = string.Join("/", arrParts, 0, arrParts.Length - 1);
                    if (MakeFolder(strFolder, objCodec_a))
                    {
                        long lngParent = findNode(strFolder, objCodec_a);
                        long lngOld = findChild(lngParent, arrParts[arrParts.Length - 1], objCodec_a);
                        if (lngOld >= 0) { unlinkAndWipe(lngParent, lngOld); }
                        HardfileEntry objEntry = new HardfileEntry();
                        objEntry.Name = arrParts[arrParts.Length - 1];
                        objEntry.Created = now();
                        objEntry.Modified = objEntry.Created;
                        objEntry.ContentLength = arrContent_a.Length;
                        blnResult = appendNode(lngParent, objEntry, arrContent_a, objCodec_a) >= 0;
                    }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; blnResult = false; }
            return blnResult;
        }

        public byte[] Get(string strPath_a, IHardfileCodec objCodec_a)
        {
            byte[] arrResult = null;
            try
            {
                long lngNode = findNode(strPath_a, objCodec_a);
                Record objR;
                if (lngNode > 0 && readRecord(lngNode, out objR) && objR.lngSize > 0 && !isMarkedOut(objR))
                {
                    byte[] arrBlob = readBlob(objR);
                    if (arrBlob != null)
                    {
                        byte[] arrEncoded = new byte[objR.lngSize];
                        Array.Copy(arrBlob, objR.lngMeta, arrEncoded, 0, arrEncoded.Length);
                        arrResult = objCodec_a.Decode(arrEncoded);
                    }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; arrResult = null; }
            return arrResult;
        }

        public bool Delete(string strPath_a, IHardfileCodec objCodec_a)
        {
            bool blnResult = false;
            if (m_blnReadOnly) { m_strLastError = "read-only"; }
            else
            try
            {
                string[] arrParts = splitPath(strPath_a);
                if (arrParts.Length > 0)
                {
                    long lngParent = findNode(string.Join("/", arrParts, 0, arrParts.Length - 1), objCodec_a);
                    long lngNode = lngParent < 0 ? -1 : findChild(lngParent, arrParts[arrParts.Length - 1], objCodec_a);
                    if (lngNode > 0) { unlinkAndWipe(lngParent, lngNode); blnResult = true; }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; blnResult = false; }
            return blnResult;
        }

        // ---------------------------------------------------------------
        // bulk
        // ---------------------------------------------------------------

        /// <summary>A fresh set holding every live node, blobs copied verbatim. The source is never written. Needs only the image.</summary>
        public Hardfile CopyTo(HardfilePaths objNewPaths_a, long lngGrowth_a)
        {
            Hardfile objNew = null;
            try
            {
                objNew = Create(objNewPaths_a, Math.Max(liveBytes(), 1) + PREFIX_MAX + SUFFIX_MAX, lngGrowth_a > 0 ? lngGrowth_a : 65536, m_objImage);
                bool blnOk = objNew != null && copyChildren(0, objNew, 0);
                if (objNew != null && blnOk) { objNew.m_lngGrowth = lngGrowth_a; objNew.m_objData.Flush(); objNew.m_objIndex.Flush(); }
                else if (objNew != null) { objNew.Dispose(); objNew = null; }
            }
            catch (Exception ex) { m_strLastError = ex.Message; if (objNew != null) { objNew.Dispose(); } objNew = null; }
            return objNew;
        }

        private bool copyChildren(long lngFolder_a, Hardfile objNew_a, long lngNewFolder_a)
        {
            bool blnOk = true;
            Record objF;
            if (readRecord(lngFolder_a, out objF))
            {
                long lngChild = objF.lngChild;
                while (blnOk && lngChild > 0)
                {
                    Record objC;
                    if (!readRecord(lngChild, out objC)) { blnOk = false; }
                    else
                    {
                        byte[] arrBlob = isMarkedOut(objC) ? null : readBlob(objC);
                        if (arrBlob == null) { blnOk = isMarkedOut(objC); }   // a marked-out node is left behind; anything else unreadable fails the copy
                        else
                        {
                            byte[] arrMeta = new byte[objC.lngMeta];
                            byte[] arrContent = new byte[objC.lngSize];
                            Array.Copy(arrBlob, 0, arrMeta, 0, arrMeta.Length);
                            Array.Copy(arrBlob, objC.lngMeta, arrContent, 0, arrContent.Length);
                            long lngNewNode = objNew_a.appendEncoded(lngNewFolder_a, arrMeta, arrContent);
                            if (lngNewNode < 0) { blnOk = false; }
                            else if (objC.lngChild > 0) { blnOk = copyChildren(lngChild, objNew_a, lngNewNode); }
                        }
                        lngChild = objC.lngNext;
                    }
                }
            }
            return blnOk;
        }

        /// <summary>Decode every file under strFolder_a into strOutFolder_a; existing files are left alone. Returns files written; unreadable nodes counted in intSkipped_a.</summary>
        public int Export(string strFolder_a, string strOutFolder_a, IHardfileCodec objCodec_a, out int intSkipped_a)
        {
            int intWritten = 0;
            intSkipped_a = 0;
            try
            {
                if (!Directory.Exists(strOutFolder_a)) { Directory.CreateDirectory(strOutFolder_a); }
                List<HardfileEntry> arrEntries = List(strFolder_a, objCodec_a);
                for (int intI = 0; intI < arrEntries.Count; intI++)
                {
                    string strChild = strFolder_a.Length == 0 ? arrEntries[intI].Name : strFolder_a + "/" + arrEntries[intI].Name;
                    string strOut = System.IO.Path.Combine(strOutFolder_a, arrEntries[intI].Name);
                    if (arrEntries[intI].IsFolder)
                    {
                        int intSub = 0;
                        intWritten += Export(strChild, strOut, objCodec_a, out intSub);
                        intSkipped_a += intSub;
                    }
                    else if (!File.Exists(strOut))
                    {
                        byte[] arrContent = Get(strChild, objCodec_a);
                        if (arrContent == null) { intSkipped_a++; }
                        else { File.WriteAllBytes(strOut, arrContent); intWritten++; }
                    }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; }
            return intWritten;
        }

        /// <summary>Put every file under strInFolder_a at strFolder_a; files already in the store are left alone. Returns files imported.</summary>
        public int Import(string strInFolder_a, string strFolder_a, IHardfileCodec objCodec_a)
        {
            int intImported = 0;
            try
            {
                if (Directory.Exists(strInFolder_a))
                {
                    string[] arrFiles = Directory.GetFiles(strInFolder_a);
                    for (int intI = 0; intI < arrFiles.Length; intI++)
                    {
                        string strName = System.IO.Path.GetFileName(arrFiles[intI]);
                        string strPath = strFolder_a.Length == 0 ? strName : strFolder_a + "/" + strName;
                        if (!Exists(strPath, objCodec_a) && Put(strPath, File.ReadAllBytes(arrFiles[intI]), objCodec_a)) { intImported++; }
                    }
                    string[] arrDirs = Directory.GetDirectories(strInFolder_a);
                    for (int intI = 0; intI < arrDirs.Length; intI++)
                    {
                        string strName = System.IO.Path.GetFileName(arrDirs[intI]);
                        intImported += Import(arrDirs[intI], strFolder_a.Length == 0 ? strName : strFolder_a + "/" + strName, objCodec_a);
                    }
                }
            }
            catch (Exception ex) { m_strLastError = ex.Message; }
            return intImported;
        }

        // ---------------------------------------------------------------
        // verify / repair
        // ---------------------------------------------------------------

        /// <summary>Structure only: every index record's hash in every copy, every folder's chain hash, orphans, replica lengths.</summary>
        public HardfileReport Verify() { return Verify(false); }

        /// <summary>Structure, and with blnFiles_a every live blob's rolling hash in every copy that holds it.</summary>
        public HardfileReport Verify(bool blnFiles_a)
        {
            HardfileReport objReport = new HardfileReport();
            try
            {
                long lngCount = recordCount();
                byte[] arrRec = new byte[RECORD_SIZE];
                for (long lngI = 0; lngI < lngCount; lngI++)
                {
                    for (int intS = 0; intS < m_objIndex.Streams.Count; intS++)
                    {
                        bool blnOk = readRecordBytes(m_objIndex.Streams[intS], lngI, arrRec) && recordHashOk(arrRec);
                        if (!blnOk)
                        {
                            if (intS == 0) { objReport.BadIndexRecords++; }
                            else if (intS == 1 && m_objPaths.Mirror.Length > 0) { objReport.BadMirrorRecords++; }
                            else { objReport.ReplicaProblems++; }
                            objReport.Notes.Add("record " + lngI + " bad in " + m_objIndex.Paths[intS]);
                        }
                    }
                    Record objR;
                    if (readRecord(lngI, out objR) && !isDead(objR))
                    {
                        if (objR.lngChainHash != chainHashOf(objR.lngChild)) { objReport.BadChains++; objReport.Notes.Add("record " + lngI + ": sibling chain hash mismatch"); }
                        if (isMarkedOut(objR)) { objReport.MarkedOut++; }
                        else if (blnFiles_a)
                        {
                            for (int intS = 0; intS < m_objData.Streams.Count; intS++)
                            {
                                byte[] arrBlob = readDataFrom(m_objData.Streams[intS], objR.lngData, objR.lngMeta + objR.lngSize);
                                if (arrBlob == null || hashBytes(arrBlob) != objR.lngBlobHash)
                                {
                                    if (intS == 0) { objReport.BadBlobs++; } else { objReport.ReplicaProblems++; }
                                    objReport.Notes.Add("record " + lngI + ": blob hash mismatch in " + m_objData.Paths[intS]);
                                }
                            }
                        }
                    }
                }
                for (int intS = 1; intS < m_objData.Streams.Count; intS++)
                {
                    if (m_objData.Streams[intS].Length != m_objData.Primary.Length) { objReport.ReplicaProblems++; objReport.Notes.Add("data replica length differs: " + m_objData.Paths[intS]); }
                }
                for (int intS = 0; intS < m_objData.Streams.Count; intS++) { long lngP; long lngS; if (!sameGuid(readEnvelope(m_objData.Streams[intS], out lngP, out lngS), m_arrGuid)) { objReport.ReplicaProblems++; objReport.Notes.Add("envelope bad in " + m_objData.Paths[intS]); } }
                for (int intS = 0; intS < m_objIndex.Streams.Count; intS++) { long lngP; long lngS; if (!sameGuid(readEnvelope(m_objIndex.Streams[intS], out lngP, out lngS), m_arrGuid)) { objReport.ReplicaProblems++; objReport.Notes.Add("envelope bad in " + m_objIndex.Paths[intS]); } }
                objReport.Orphans = countOrphans();
            }
            catch (Exception ex) { m_strLastError = ex.Message; objReport.Notes.Add(ex.Message); }
            return objReport;
        }

        /// <summary>One file: true when its record and its blob hash pass in at least one copy.</summary>
        public bool VerifyFile(string strPath_a, IHardfileCodec objCodec_a)
        {
            bool blnResult = false;
            long lngNode = findNode(strPath_a, objCodec_a);
            Record objR;
            if (lngNode > 0 && readRecord(lngNode, out objR)) { blnResult = readBlob(objR) != null; }
            return blnResult;
        }

        /// <summary>Index records and blobs: every copy that fails its rolling hash is rewritten from a copy that passes; chain hashes are recomputed; replicas are then made byte-identical to the primary in the noise regions.</summary>
        public int Repair()
        {
            int intRepaired = 0;
            if (m_blnReadOnly) { m_strLastError = "read-only"; }
            else
            try
            {
                long lngCount = recordCount();
                byte[] arrRec = new byte[RECORD_SIZE];
                byte[] arrGood = new byte[RECORD_SIZE];
                for (long lngI = 0; lngI < lngCount; lngI++)
                {
                    bool blnHaveGood = false;
                    for (int intS = 0; !blnHaveGood && intS < m_objIndex.Streams.Count; intS++)
                    {
                        if (readRecordBytes(m_objIndex.Streams[intS], lngI, arrRec) && recordHashOk(arrRec)) { Array.Copy(arrRec, arrGood, RECORD_SIZE); blnHaveGood = true; }
                    }
                    if (blnHaveGood)
                    {
                        for (int intS = 0; intS < m_objIndex.Streams.Count; intS++)
                        {
                            bool blnOk = readRecordBytes(m_objIndex.Streams[intS], lngI, arrRec) && recordHashOk(arrRec);
                            if (!blnOk) { m_objIndex.Streams[intS].Position = m_lngIndexPrefix + lngI * RECORD_SIZE; m_objIndex.Streams[intS].Write(arrGood, 0, RECORD_SIZE); intRepaired++; }
                        }
                    }
                }
                m_objIndex.Flush();
                for (long lngI = 0; lngI < lngCount; lngI++)
                {
                    Record objR;
                    if (readRecord(lngI, out objR) && !isDead(objR))
                    {
                        long lngChain = chainHashOf(objR.lngChild);
                        if (objR.lngChainHash != lngChain) { objR.lngChainHash = lngChain; writeRecord(lngI, objR); intRepaired++; }
                        long lngLen = objR.lngMeta + objR.lngSize;
                        byte[] arrGoodBlob = null;
                        for (int intS = 0; !isMarkedOut(objR) && arrGoodBlob == null && intS < m_objData.Streams.Count; intS++)
                        {
                            byte[] arrTry = readDataFrom(m_objData.Streams[intS], objR.lngData, lngLen);
                            if (arrTry != null && hashBytes(arrTry) == objR.lngBlobHash) { arrGoodBlob = arrTry; }
                        }
                        if (!isMarkedOut(objR) && arrGoodBlob == null && lngI > 0)
                        {
                            // no copy passes: hold the space, stop replicating it, leave it to the user
                            objR.lngFlag = FLAG_MARKEDOUT;
                            writeRecord(lngI, objR);
                            intRepaired++;
                        }
                        else if (arrGoodBlob != null)
                        {
                            for (int intS = 0; intS < m_objData.Streams.Count; intS++)
                            {
                                byte[] arrHave = readDataFrom(m_objData.Streams[intS], objR.lngData, lngLen);
                                if (arrHave == null || hashBytes(arrHave) != objR.lngBlobHash)
                                {
                                    try { m_objData.Streams[intS].Position = objR.lngData; m_objData.Streams[intS].Write(arrGoodBlob, 0, arrGoodBlob.Length); intRepaired++; } catch { }
                                }
                            }
                        }
                    }
                }
                m_objData.Flush();
                if (m_objData.Streams.Count > 1)
                {
                    // regions held by marked-out records are never copied in either direction
                    List<Extent> arrHeld = new List<Extent>();
                    for (long lngI = 0; lngI < lngCount; lngI++)
                    {
                        Record objR;
                        if (readRecord(lngI, out objR) && isMarkedOut(objR)) { Extent objE = new Extent(); objE.lngAt = objR.lngData; objE.lngLen = roundUp(objR.lngMeta + objR.lngSize); arrHeld.Add(objE); }
                    }
                    byte[] arrA = new byte[1 << 20];
                    byte[] arrB = new byte[1 << 20];
                    long lngPrimLen = m_objData.Primary.Length;
                    for (int intS = 1; intS < m_objData.Streams.Count; intS++)
                    {
                        FileStream objRep = m_objData.Streams[intS];
                        if (objRep.Length > lngPrimLen) { objRep.SetLength(lngPrimLen); }
                        long lngPos = 0;
                        while (lngPos < lngPrimLen)
                        {
                            int intChunk = (int)Math.Min(arrA.Length, lngPrimLen - lngPos);
                            m_objData.Primary.Position = lngPos;
                            readFully(m_objData.Primary, arrA, intChunk);
                            bool blnSame = objRep.Length >= lngPos + intChunk;
                            if (blnSame)
                            {
                                objRep.Position = lngPos;
                                readFully(objRep, arrB, intChunk);
                                for (int intK = 0; blnSame && intK < intChunk; intK++) { blnSame = arrA[intK] == arrB[intK]; }
                            }
                            if (!blnSame)
                            {
                                // write the chunk back except the held regions inside it
                                long lngFrom = lngPos;
                                for (int intH = 0; intH < arrHeld.Count; intH++)
                                {
                                    long lngHStart = Math.Max(arrHeld[intH].lngAt, lngPos);
                                    long lngHEnd = Math.Min(arrHeld[intH].lngAt + arrHeld[intH].lngLen, lngPos + intChunk);
                                    if (lngHEnd > lngHStart)
                                    {
                                        if (lngHStart > lngFrom) { objRep.Position = lngFrom; objRep.Write(arrA, (int)(lngFrom - lngPos), (int)(lngHStart - lngFrom)); }
                                        lngFrom = Math.Max(lngFrom, lngHEnd);
                                    }
                                }
                                if (lngPos + intChunk > lngFrom) { objRep.Position = lngFrom; objRep.Write(arrA, (int)(lngFrom - lngPos), (int)(lngPos + intChunk - lngFrom)); }
                            }
                            lngPos += intChunk;
                        }
                        objRep.Flush();
                    }
                    m_objData.Flush();
                }
                // envelopes: rewrite from the in-memory set values to every copy (a repair is an explicit, user-driven write)
                touch();
                stampEnvelopes();
                m_objData.Flush();
                m_objIndex.Flush();
            }
            catch (Exception ex) { m_strLastError = ex.Message; }
            return intRepaired;
        }

        private int countOrphans()
        {
            long lngCount = recordCount();
            bool[] arrSeen = new bool[lngCount];
            markReachable(0, arrSeen);
            int intOrphans = 0;
            for (long lngI = 1; lngI < lngCount; lngI++)
            {
                Record objR;
                if (!arrSeen[lngI] && readRecord(lngI, out objR) && !isDead(objR)) { intOrphans++; }
            }
            return intOrphans;
        }

        private void markReachable(long lngNode_a, bool[] arrSeen_a)
        {
            Record objR;
            if (lngNode_a >= 0 && lngNode_a < arrSeen_a.Length && !arrSeen_a[lngNode_a] && readRecord(lngNode_a, out objR))
            {
                arrSeen_a[lngNode_a] = true;
                long lngChild = objR.lngChild;
                while (lngChild > 0 && lngChild < arrSeen_a.Length && !arrSeen_a[lngChild])
                {
                    markReachable(lngChild, arrSeen_a);
                    Record objC;
                    lngChild = readRecord(lngChild, out objC) ? objC.lngNext : 0;
                }
            }
        }

        // ---------------------------------------------------------------
        // node plumbing
        // ---------------------------------------------------------------

        private static long now() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }

        private static string[] splitPath(string strPath_a)
        {
            List<string> arrParts = new List<string>();
            if (strPath_a != null)
            {
                string[] arrRaw = strPath_a.Replace('\\', '/').Split('/');
                for (int intI = 0; intI < arrRaw.Length; intI++) { if (arrRaw[intI].Length > 0) { arrParts.Add(arrRaw[intI]); } }
            }
            return arrParts.ToArray();
        }

        private static bool isDead(Record objR_a) { return objR_a.lngFlag == FLAG_DEAD; }
        private static bool isMarkedOut(Record objR_a) { return objR_a.lngFlag == FLAG_MARKEDOUT; }

        // a dead record is flagged dead and otherwise random, so it reveals nothing about the node it was
        private Record deadRecord()
        {
            Record objR = new Record();
            objR.lngFlag = FLAG_DEAD;
            objR.lngNext = (long)(uint)m_objRand.Next();
            objR.lngChild = (long)(uint)m_objRand.Next();
            objR.lngData = ((long)(uint)m_objRand.Next() << 32) | (long)(uint)m_objRand.Next();
            objR.lngMeta = (long)(uint)m_objRand.Next();
            objR.lngSize = ((long)(uint)m_objRand.Next() << 32) | (long)(uint)m_objRand.Next();
            objR.lngBlobHash = (long)(uint)m_objRand.Next();
            objR.lngChainHash = (long)(uint)m_objRand.Next();
            return objR;
        }

        private long findNode(string strPath_a, IHardfileCodec objCodec_a)
        {
            long lngNode = 0;
            string[] arrParts = splitPath(strPath_a);
            for (int intI = 0; lngNode >= 0 && intI < arrParts.Length; intI++) { lngNode = findChild(lngNode, arrParts[intI], objCodec_a); }
            return lngNode;
        }

        private long findChild(long lngParent_a, string strName_a, IHardfileCodec objCodec_a)
        {
            long lngResult = -1;
            Record objP;
            if (lngParent_a >= 0 && readRecord(lngParent_a, out objP))
            {
                long lngChild = objP.lngChild;
                while (lngResult < 0 && lngChild > 0)
                {
                    Record objC;
                    if (!readRecord(lngChild, out objC)) { lngChild = 0; }
                    else
                    {
                        HardfileEntry objEntry = readMeta(lngChild, objC, objCodec_a);
                        if (objEntry != null && objEntry.Name == strName_a) { lngResult = lngChild; }
                        else if (objEntry == null && strName_a == "#" + lngChild) { lngResult = lngChild; }   // unreadable (marked out, or bad with no good copy yet): addressable by record number
                        lngChild = objC.lngNext;
                    }
                }
            }
            return lngResult;
        }

        private HardfileEntry readMeta(long lngNode_a, Record objR_a, IHardfileCodec objCodec_a)
        {
            HardfileEntry objResult = null;
            byte[] arrBlob = readBlob(objR_a);
            if (arrBlob != null)
            {
                byte[] arrEncoded = new byte[objR_a.lngMeta];
                Array.Copy(arrBlob, 0, arrEncoded, 0, arrEncoded.Length);
                byte[] arrPlain = lngNode_a == 0 ? UDecode.Bytes(arrEncoded, m_objImage) : objCodec_a.Decode(arrEncoded);
                if (arrPlain != null) { objResult = HardfileEntry.FromBytes(arrPlain); }
            }
            return objResult;
        }

        private long appendNode(long lngParent_a, HardfileEntry objEntry_a, byte[] arrContent_a, IHardfileCodec objCodec_a)
        {
            long lngResult = -1;
            byte[] arrMeta = objCodec_a.Encode(objEntry_a.ToBytes());
            byte[] arrData = arrContent_a == null ? new byte[0] : objCodec_a.Encode(arrContent_a);
            if (arrMeta == null || arrData == null) { m_strLastError = "codec failed"; }
            else { lngResult = appendEncoded(lngParent_a, arrMeta, arrData); }
            return lngResult;
        }

        private long appendEncoded(long lngParent_a, byte[] arrMeta_a, byte[] arrData_a)
        {
            long lngResult = -1;
            long lngNeed = arrMeta_a.Length + arrData_a.Length;
            long lngAt = freeTake(roundUp(lngNeed));
            if (lngAt < 0) { m_strLastError = "hardfile full"; }
            else
            {
                long lngRecord = recordTake();
                if (lngRecord < 0) { m_strLastError = "index full"; freeAdd(lngAt, roundUp(lngNeed)); }
                else
                {
                    byte[] arrBlob = new byte[lngNeed];
                    Array.Copy(arrMeta_a, 0, arrBlob, 0, arrMeta_a.Length);
                    Array.Copy(arrData_a, 0, arrBlob, arrMeta_a.Length, arrData_a.Length);
                    m_objData.Write(lngAt, arrBlob, arrBlob.Length);
                    Record objR = new Record();
                    objR.lngFlag = FLAG_LIVE;
                    objR.lngData = lngAt;
                    objR.lngMeta = arrMeta_a.Length;
                    objR.lngSize = arrData_a.Length;
                    objR.lngBlobHash = hashBytes(arrBlob);
                    objR.lngChainHash = chainHashOf(0);
                    writeRecord(lngRecord, objR);
                    linkAsLastChild(lngParent_a, lngRecord);
                    touch();
                    m_objData.Flush();
                    m_objIndex.Flush();
                    lngResult = lngRecord;
                }
            }
            return lngResult;
        }

        // --- envelope: [prefix len][suffix len] at 0, [guid][hash] indirected in the suffix region ---

        private void writeEnvelope(clsFileGroup objGroup_a, long lngPrefix_a, long lngSuffix_a)
        {
            byte[] arrHead = new byte[ENVELOPE_HEADER];
            if (!writeNumber(arrHead, 0, lngPrefix_a, 2) || !writeNumber(arrHead, 4, lngSuffix_a, 2)) { throw new Exception("hardfile image lacks a byte value needed for the envelope"); }
            objGroup_a.Write(0, arrHead, arrHead.Length);
            byte[] arrPlain = new byte[30];
            Array.Copy(m_arrGuid, 0, arrPlain, 0, 16);
            long lngUnitCode = m_lngUnit / 256;
            arrPlain[16] = (byte)(lngUnitCode & 0xFF);
            arrPlain[17] = (byte)((lngUnitCode >> 8) & 0xFF);
            for (int intB = 0; intB < 8; intB++) { arrPlain[18 + intB] = (byte)((m_lngLastWritten >> (8 * intB)) & 0xFF); }
            byte[] arrHashed = new byte[26];
            Array.Copy(arrPlain, 0, arrHashed, 0, 26);
            long lngHash = hashBytes(arrHashed);
            for (int intB = 0; intB < 4; intB++) { arrPlain[26 + intB] = (byte)((lngHash >> (8 * intB)) & 0xFF); }
            byte[] arrBlock = new byte[ENVELOPE_BLOCK];
            for (int intB = 0; intB < 30; intB++)
            {
                if (!writeNumber(arrBlock, intB * 2, arrPlain[intB], 1)) { throw new Exception("hardfile image lacks a byte value needed for the envelope"); }
            }
            objGroup_a.Write(objGroup_a.Length - lngSuffix_a, arrBlock, arrBlock.Length);
        }

        private byte[] readEnvelope(FileStream objStream_a, out long lngPrefix_a, out long lngSuffix_a)
        {
            long lngUnit = 0;
            return readEnvelope(objStream_a, out lngPrefix_a, out lngSuffix_a, out lngUnit);
        }

        // reads one stream's envelope; returns the guid, or null when the header or the block does not read under this image
        private byte[] readEnvelope(FileStream objStream_a, out long lngPrefix_a, out long lngSuffix_a, out long lngUnit_a)
        {
            byte[] arrResult = null;
            lngPrefix_a = 0;
            lngSuffix_a = 0;
            lngUnit_a = 0;
            byte[] arrHead = readDataFrom(objStream_a, 0, ENVELOPE_HEADER);
            if (arrHead != null)
            {
                long lngPrefix = readNumber(arrHead, 0, 2);
                long lngSuffix = readNumber(arrHead, 4, 2);
                if (lngPrefix >= PREFIX_MIN && lngPrefix <= PREFIX_MAX && lngSuffix >= SUFFIX_MIN && lngSuffix <= SUFFIX_MAX && lngPrefix + lngSuffix < objStream_a.Length)
                {
                    byte[] arrBlock = readDataFrom(objStream_a, objStream_a.Length - lngSuffix, ENVELOPE_BLOCK);
                    if (arrBlock != null)
                    {
                        byte[] arrHashed = new byte[26];
                        for (int intB = 0; intB < 26; intB++) { arrHashed[intB] = (byte)readNumber(arrBlock, intB * 2, 1); }
                        long lngStored = readNumber(arrBlock, 52, 4);
                        if (lngStored == hashBytes(arrHashed))
                        {
                            byte[] arrGuid = new byte[16];
                            Array.Copy(arrHashed, 0, arrGuid, 0, 16);
                            arrResult = arrGuid;
                            lngPrefix_a = lngPrefix;
                            lngSuffix_a = lngSuffix;
                            lngUnit_a = (long)(arrHashed[16] | (arrHashed[17] << 8)) * 256;
                            long lngWritten = 0;
                            for (int intB = 0; intB < 8; intB++) { lngWritten |= ((long)arrHashed[18 + intB]) << (8 * intB); }
                            m_lngLastWrittenRead = lngWritten;
                        }
                    }
                }
            }
            return arrResult;
        }

        // Every change to the set: the last-written time is taken, the envelope block is stamped on a random third
        // of changes (never on every one, so no region changes every time), and decoy writes go out with it:
        // a few random free-space extents get fresh noise and a few random dead records get fresh random fields,
        // in every copy. Free space is noise by definition and dead records random by definition, so nothing is
        // lost and every hash still passes, but a monitor diffing snapshots sees many regions change per write and
        // cannot tell the real record, the real blob or the envelope block from the decoys. The block is also
        // stamped once at Dispose so LastWritten is always current on disk.
        private const int DECOY_MIN = 3;
        private const int DECOY_MAX = 7;
        private const int DECOY_SPAN = 4096;

        private void touch()
        {
            m_lngLastWritten = now();
            if (m_objRand.Next(3) == 0) { stampEnvelopes(); }
            decoyWrites();
        }

        private void decoyWrites()
        {
            int intCount = DECOY_MIN + m_objRand.Next(DECOY_MAX - DECOY_MIN + 1);
            for (int intI = 0; intI < intCount; intI++)
            {
                if (m_arrFree != null && m_arrFree.Count > 0)
                {
                    Extent objE = m_arrFree[m_objRand.Next(m_arrFree.Count)];
                    long lngLen = Math.Min(objE.lngLen, (long)(1 + m_objRand.Next(DECOY_SPAN)));
                    long lngAt = objE.lngAt + (objE.lngLen > lngLen ? (long)(m_objRand.NextDouble() * (objE.lngLen - lngLen)) : 0);
                    fillNoise(m_objData, lngAt, lngLen);
                }
                if (m_arrFreeRecords != null && m_arrFreeRecords.Count > 0)
                {
                    writeRecord(m_arrFreeRecords[m_objRand.Next(m_arrFreeRecords.Count)], deadRecord());
                }
            }
        }

        private void stampEnvelopes()
        {
            if (m_objData != null && m_objIndex != null)
            {
                writeEnvelope(m_objData, m_lngDataPrefix, m_lngDataSuffix);
                writeEnvelope(m_objIndex, m_lngIndexPrefix, m_lngIndexSuffix);
            }
        }

        private static bool sameGuid(byte[] arrA_a, byte[] arrB_a)
        {
            bool blnResult = arrA_a != null && arrB_a != null && arrA_a.Length == arrB_a.Length;
            for (int intI = 0; blnResult && intI < arrA_a.Length; intI++) { blnResult = arrA_a[intI] == arrB_a[intI]; }
            return blnResult;
        }

        // the set's guid comes from the first data copy whose envelope reads, the index must carry the same one;
        // further copies (mirror, replicas) with a bad envelope do not block the open: Verify reports them, Repair rewrites them
        private bool openEnvelopes()
        {
            bool blnOk = false;
            m_arrGuid = null;
            for (int intS = 0; m_arrGuid == null && intS < m_objData.Streams.Count; intS++) { m_arrGuid = readEnvelope(m_objData.Streams[intS], out m_lngDataPrefix, out m_lngDataSuffix, out m_lngUnit); }
            if (m_arrGuid != null) { m_lngUnit = normaliseUnit(m_lngUnit); m_lngLastWritten = m_lngLastWrittenRead; }
            if (m_arrGuid != null)
            {
                for (int intS = 0; !blnOk && intS < m_objIndex.Streams.Count; intS++)
                {
                    long lngP = 0;
                    long lngS = 0;
                    byte[] arrGuid = readEnvelope(m_objIndex.Streams[intS], out lngP, out lngS);
                    if (sameGuid(arrGuid, m_arrGuid)) { blnOk = true; m_lngIndexPrefix = lngP; m_lngIndexSuffix = lngS; }
                }
            }
            return blnOk;
        }

        // grow a file by whole steps of noise and move its suffix block to a fresh random-length suffix at the new end
        private bool growFile(clsFileGroup objGroup_a, long lngNeeded_a, bool blnIndex_a)
        {
            bool blnResult = false;
            if (m_lngGrowth > 0)
            {
                long lngStep = blnIndex_a ? indexSizeFor(m_lngGrowth) : m_lngGrowth;
                long lngOldLen = objGroup_a.Length;
                long lngOldSuffix = blnIndex_a ? m_lngIndexSuffix : m_lngDataSuffix;
                long lngGrowTo = lngOldLen;
                while (lngGrowTo < lngNeeded_a + SUFFIX_MAX) { lngGrowTo += lngStep; }
                fillNoise(objGroup_a, lngOldLen - lngOldSuffix, lngGrowTo - (lngOldLen - lngOldSuffix));   // old suffix region becomes noise too
                long lngNewSuffix = SUFFIX_MIN + m_objRand.Next(SUFFIX_MAX - SUFFIX_MIN + 1);
                if (blnIndex_a) { m_lngIndexSuffix = lngNewSuffix; writeEnvelope(objGroup_a, m_lngIndexPrefix, lngNewSuffix); }
                else
                {
                    m_lngDataSuffix = lngNewSuffix;
                    writeEnvelope(objGroup_a, m_lngDataPrefix, lngNewSuffix);
                    freeAdd(lngOldLen - lngOldSuffix, (lngGrowTo - lngNewSuffix) - (lngOldLen - lngOldSuffix));
                }
                blnResult = true;
            }
            return blnResult;
        }

        private static long normaliseUnit(long lngUnit_a)
        {
            long lngUnit = 256;
            while (lngUnit < lngUnit_a && lngUnit < 65536) { lngUnit *= 2; }
            return lngUnit;
        }

        private long roundUp(long lngLen_a)
        {
            return ((lngLen_a + m_lngUnit - 1) / m_lngUnit) * m_lngUnit;
        }

        // --- free space (data) and free records (index) ---

        private void freeAdd(long lngAt_a, long lngLen_a)
        {
            if (lngLen_a > 0)
            {
                Extent objE = new Extent();
                objE.lngAt = lngAt_a;
                objE.lngLen = lngLen_a;
                int intPos = 0;
                while (intPos < m_arrFree.Count && m_arrFree[intPos].lngAt < lngAt_a) { intPos++; }
                m_arrFree.Insert(intPos, objE);
                if (intPos + 1 < m_arrFree.Count && m_arrFree[intPos].lngAt + m_arrFree[intPos].lngLen == m_arrFree[intPos + 1].lngAt)
                {
                    Extent objM = m_arrFree[intPos]; objM.lngLen += m_arrFree[intPos + 1].lngLen; m_arrFree[intPos] = objM; m_arrFree.RemoveAt(intPos + 1);
                }
                if (intPos > 0 && m_arrFree[intPos - 1].lngAt + m_arrFree[intPos - 1].lngLen == m_arrFree[intPos].lngAt)
                {
                    Extent objM = m_arrFree[intPos - 1]; objM.lngLen += m_arrFree[intPos].lngLen; m_arrFree[intPos - 1] = objM; m_arrFree.RemoveAt(intPos);
                }
            }
        }

        // first fit; grows the data file when nothing fits (growth > 0); -1 when full
        private long freeTake(long lngLen_a)
        {
            long lngResult = -1;
            for (int intI = 0; lngResult < 0 && intI < m_arrFree.Count; intI++)
            {
                if (m_arrFree[intI].lngLen >= lngLen_a)
                {
                    Extent objE = m_arrFree[intI];
                    lngResult = objE.lngAt;
                    objE.lngAt += lngLen_a;
                    objE.lngLen -= lngLen_a;
                    if (objE.lngLen == 0) { m_arrFree.RemoveAt(intI); } else { m_arrFree[intI] = objE; }
                }
            }
            if (lngResult < 0 && m_lngGrowth > 0)
            {
                if (growFile(m_objData, m_objData.Length + lngLen_a, false)) { lngResult = freeTake(lngLen_a); }
            }
            return lngResult;
        }

        private long recordTake()
        {
            long lngResult = -1;
            if (m_arrFreeRecords.Count > 0)
            {
                lngResult = m_arrFreeRecords[m_arrFreeRecords.Count - 1];
                m_arrFreeRecords.RemoveAt(m_arrFreeRecords.Count - 1);
            }
            else
            {
                long lngRecord = recordCount();
                bool blnFits = m_lngIndexPrefix + (lngRecord + 1) * RECORD_SIZE <= m_objIndex.Length - m_lngIndexSuffix;
                if (!blnFits) { blnFits = growFile(m_objIndex, m_lngIndexPrefix + (lngRecord + 1) * RECORD_SIZE, true); }
                if (blnFits) { lngResult = lngRecord; m_lngRecordCount = lngRecord + 1; }
            }
            return lngResult;
        }

        private long liveBytes()
        {
            long lngFree = 0;
            for (int intI = 0; intI < m_arrFree.Count; intI++) { lngFree += m_arrFree[intI].lngLen; }
            return m_objData.Length - lngFree;
        }

        private void linkAsLastChild(long lngParent_a, long lngNode_a)
        {
            Record objP;
            readRecord(lngParent_a, out objP);
            if (objP.lngChild == 0) { objP.lngChild = lngNode_a; }
            else
            {
                long lngLast = objP.lngChild;
                Record objL;
                readRecord(lngLast, out objL);
                while (objL.lngNext > 0) { lngLast = objL.lngNext; readRecord(lngLast, out objL); }
                objL.lngNext = lngNode_a;
                writeRecord(lngLast, objL);
            }
            objP.lngChainHash = chainHashOf(objP.lngChild);
            writeRecord(lngParent_a, objP);
        }

        private void unlinkAndWipe(long lngParent_a, long lngNode_a)
        {
            Record objN;
            readRecord(lngNode_a, out objN);
            Record objP;
            readRecord(lngParent_a, out objP);
            if (objP.lngChild == lngNode_a) { objP.lngChild = objN.lngNext; }
            else
            {
                long lngPrev = objP.lngChild;
                Record objPrev;
                readRecord(lngPrev, out objPrev);
                while (objPrev.lngNext != lngNode_a && objPrev.lngNext > 0) { lngPrev = objPrev.lngNext; readRecord(lngPrev, out objPrev); }
                if (objPrev.lngNext == lngNode_a) { objPrev.lngNext = objN.lngNext; writeRecord(lngPrev, objPrev); }
            }
            wipeSubtree(lngNode_a);
            objP.lngChainHash = chainHashOf(objP.lngChild);
            writeRecord(lngParent_a, objP);
            touch();
            m_objData.Flush();
            m_objIndex.Flush();
        }

        private void wipeSubtree(long lngNode_a)
        {
            Record objN;
            if (readRecord(lngNode_a, out objN))
            {
                long lngChild = objN.lngChild;
                while (lngChild > 0)
                {
                    Record objC;
                    long lngNext = readRecord(lngChild, out objC) ? objC.lngNext : 0;
                    wipeSubtree(lngChild);
                    lngChild = lngNext;
                }
                fillNoise(m_objData, objN.lngData, objN.lngMeta + objN.lngSize);
                writeRecord(lngNode_a, deadRecord());
                freeAdd(objN.lngData, roundUp(objN.lngMeta + objN.lngSize));
                m_arrFreeRecords.Add(lngNode_a);
            }
        }

        private long recordCount()
        {
            if (m_lngRecordCount < 0) { rebuildFreeLists(); }
            return m_lngRecordCount;
        }

        // records are never removed (dead ones stay as zero records that still hash), so the index in use is the
        // prefix of records that read validly; the free space is everything not covered by a live record
        private void rebuildFreeLists()
        {
            long lngMaxRecords = (m_objIndex.Length - m_lngIndexPrefix - m_lngIndexSuffix) / RECORD_SIZE;
            long lngCount = 0;
            List<Extent> arrLive = new List<Extent>();
            m_arrFreeRecords = new List<long>();
            bool blnMore = true;
            for (long lngI = 0; blnMore && lngI < lngMaxRecords; lngI++)
            {
                Record objR;
                if (readRecord(lngI, out objR))
                {
                    if (isDead(objR)) { if (lngI > 0) { m_arrFreeRecords.Add(lngI); } }
                    else { Extent objE = new Extent(); objE.lngAt = objR.lngData; objE.lngLen = roundUp(objR.lngMeta + objR.lngSize); arrLive.Add(objE); }
                    lngCount = lngI + 1;
                }
                else { blnMore = false; }
            }
            m_lngRecordCount = Math.Max(lngCount, 1);
            arrLive.Sort(delegate (Extent objA_a, Extent objB_a) { return objA_a.lngAt.CompareTo(objB_a.lngAt); });
            m_arrFree = new List<Extent>();
            long lngPos = m_lngDataPrefix;
            long lngEnd = m_objData.Length - m_lngDataSuffix;
            for (int intI = 0; intI < arrLive.Count; intI++)
            {
                if (arrLive[intI].lngAt > lngPos) { freeAdd(lngPos, arrLive[intI].lngAt - lngPos); }
                if (arrLive[intI].lngAt + arrLive[intI].lngLen > lngPos) { lngPos = arrLive[intI].lngAt + arrLive[intI].lngLen; }
            }
            if (lngEnd > lngPos) { freeAdd(lngPos, lngEnd - lngPos); }
        }

        private long indexSizeFor(long lngDataSize_a)
        {
            long lngSize = (lngDataSize_a * INDEX_PERCENT) / 100;
            if (lngSize < INDEX_MIN) { lngSize = INDEX_MIN; }
            return (lngSize / RECORD_SIZE) * RECORD_SIZE;
        }

        private bool ensureSpace(clsFileGroup objGroup_a, long lngNeeded_a, bool blnIndex_a)
        {
            bool blnResult = objGroup_a.Length >= lngNeeded_a;
            if (!blnResult && m_lngGrowth > 0)
            {
                long lngStep = blnIndex_a ? indexSizeFor(m_lngGrowth) : m_lngGrowth;
                long lngGrowTo = objGroup_a.Length;
                while (lngGrowTo < lngNeeded_a) { lngGrowTo += lngStep; }
                fillNoise(objGroup_a, objGroup_a.Length, lngGrowTo - objGroup_a.Length);
                blnResult = true;
            }
            return blnResult;
        }

        private void fillNoise(clsFileGroup objGroup_a, long lngFrom_a, long lngCount_a)
        {
            if (lngCount_a > 0)
            {
                byte[] arrNoise = new byte[65536];
                long lngLeft = lngCount_a;
                long lngPos = lngFrom_a;
                while (lngLeft > 0)
                {
                    int intChunk = (int)Math.Min(arrNoise.Length, lngLeft);
                    m_objRand.NextBytes(arrNoise);
                    objGroup_a.Write(lngPos, arrNoise, intChunk);
                    lngLeft -= intChunk;
                    lngPos += intChunk;
                }
            }
        }

        private static bool readFully(FileStream objStream_a, byte[] arrBuf_a, int intCount_a)
        {
            int intRead = 0;
            bool blnOk = true;
            while (blnOk && intRead < intCount_a)
            {
                int intGot = objStream_a.Read(arrBuf_a, intRead, intCount_a - intRead);
                if (intGot <= 0) { blnOk = false; } else { intRead += intGot; }
            }
            return blnOk;
        }

        // a node's blob (meta + content as stored) from the first copy whose rolling hash matches the record
        private byte[] readBlob(Record objR_a)
        {
            byte[] arrResult = null;
            long lngLen = objR_a.lngMeta + objR_a.lngSize;
            if (lngLen >= 0 && objR_a.lngData >= 0 && lngLen < int.MaxValue)
            {
                for (int intS = 0; arrResult == null && intS < m_objData.Streams.Count; intS++)
                {
                    byte[] arrTry = readDataFrom(m_objData.Streams[intS], objR_a.lngData, lngLen);
                    if (arrTry != null && hashBytes(arrTry) == objR_a.lngBlobHash) { arrResult = arrTry; }
                }
            }
            return arrResult;
        }

        private static byte[] readDataFrom(FileStream objStream_a, long lngAt_a, long lngCount_a)
        {
            byte[] arrResult = null;
            try
            {
                if (lngAt_a >= 0 && lngCount_a >= 0 && lngAt_a + lngCount_a <= objStream_a.Length)
                {
                    byte[] arrBuf = new byte[lngCount_a];
                    objStream_a.Position = lngAt_a;
                    if (readFully(objStream_a, arrBuf, arrBuf.Length)) { arrResult = arrBuf; }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        // ---------------------------------------------------------------
        // hashes
        // ---------------------------------------------------------------

        private static long hashBytes(byte[] arrBytes_a)
        {
            long lngResult = 0;
            if (arrBytes_a != null && arrBytes_a.Length > 0)
            {
                byte[] arrHash = ZRollingHash.Bytes(arrBytes_a);
                lngResult = (long)(arrHash[0] | (arrHash[1] << 8) | (arrHash[2] << 16) | ((long)arrHash[3] << 24));
            }
            return lngResult;
        }

        // rolling hash over the children's record numbers in chain order (4 bytes each); 0 for no children
        private long chainHashOf(long lngFirstChild_a)
        {
            List<byte> arrBytes = new List<byte>();
            long lngChild = lngFirstChild_a;
            int intGuard = 0;
            while (lngChild > 0 && intGuard < 10000000)
            {
                for (int intB = 0; intB < 4; intB++) { arrBytes.Add((byte)((lngChild >> (8 * intB)) & 0xFF)); }
                Record objC;
                lngChild = readRecord(lngChild, out objC) ? objC.lngNext : 0;
                intGuard++;
            }
            return hashBytes(arrBytes.ToArray());
        }

        // ---------------------------------------------------------------
        // index records
        // ---------------------------------------------------------------

        private bool writeNumber(byte[] arrOut_a, int intAt_a, long lngValue_a, int intBytes_a)
        {
            bool blnResult = true;
            for (int intI = 0; blnResult && intI < intBytes_a; intI++)
            {
                byte by = (byte)((lngValue_a >> (8 * intI)) & 0xFF);
                ByteAddresses objList = m_objRom.arrLookup[by];
                if (objList.intCount == 0) { blnResult = false; }
                else
                {
                    uint intPos = objList.ptrAddresses[m_objRand.Next((int)objList.intCount)];
                    arrOut_a[intAt_a + intI * 2] = (byte)(intPos & 0xFF);
                    arrOut_a[intAt_a + intI * 2 + 1] = (byte)((intPos >> 8) & 0xFF);
                }
            }
            return blnResult;
        }

        private long readNumber(byte[] arrIn_a, int intAt_a, int intBytes_a)
        {
            long lngValue = 0;
            for (int intI = 0; intI < intBytes_a; intI++)
            {
                int intAddr = arrIn_a[intAt_a + intI * 2] | (arrIn_a[intAt_a + intI * 2 + 1] << 8);
                lngValue |= ((long)m_objRom.ptrROMData[intAddr]) << (8 * intI);
            }
            return lngValue;
        }

        private static byte[] plainOf(Record objR_a)
        {
            byte[] arrPlain = new byte[PTR_BYTES * 2 + LEN_BYTES * 2 + PTR_BYTES + HASH_BYTES * 2 + FLAG_BYTES];
            long[] arrVals = { objR_a.lngNext, objR_a.lngChild, objR_a.lngData, objR_a.lngMeta, objR_a.lngSize, objR_a.lngBlobHash, objR_a.lngChainHash, objR_a.lngFlag };
            int[] arrLens = { PTR_BYTES, PTR_BYTES, LEN_BYTES, PTR_BYTES, LEN_BYTES, HASH_BYTES, HASH_BYTES, FLAG_BYTES };
            int intAt = 0;
            for (int intV = 0; intV < arrVals.Length; intV++)
            {
                for (int intB = 0; intB < arrLens[intV]; intB++) { arrPlain[intAt++] = (byte)((arrVals[intV] >> (8 * intB)) & 0xFF); }
            }
            return arrPlain;
        }

        private static long hashOf(Record objR_a)
        {
            byte[] arrHash = ZRollingHash.Bytes(plainOf(objR_a));
            return (long)(arrHash[0] | (arrHash[1] << 8) | (arrHash[2] << 16) | ((long)arrHash[3] << 24));
        }

        private byte[] encodeRecord(Record objR_a)
        {
            byte[] arrOut = new byte[RECORD_SIZE];
            bool blnOk = writeNumber(arrOut, 0, objR_a.lngNext, PTR_BYTES)
                      && writeNumber(arrOut, 8, objR_a.lngChild, PTR_BYTES)
                      && writeNumber(arrOut, 16, objR_a.lngData, LEN_BYTES)
                      && writeNumber(arrOut, 32, objR_a.lngMeta, PTR_BYTES)
                      && writeNumber(arrOut, 40, objR_a.lngSize, LEN_BYTES)
                      && writeNumber(arrOut, 56, objR_a.lngBlobHash, HASH_BYTES)
                      && writeNumber(arrOut, 64, objR_a.lngChainHash, HASH_BYTES)
                      && writeNumber(arrOut, 72, objR_a.lngFlag, FLAG_BYTES)
                      && writeNumber(arrOut, 76, hashOf(objR_a), HASH_BYTES);
            if (!blnOk) { throw new Exception("hardfile image lacks a byte value needed for a record"); }
            return arrOut;
        }

        private Record decodeRecord(byte[] arrIn_a)
        {
            Record objR = new Record();
            objR.lngNext = readNumber(arrIn_a, 0, PTR_BYTES);
            objR.lngChild = readNumber(arrIn_a, 8, PTR_BYTES);
            objR.lngData = readNumber(arrIn_a, 16, LEN_BYTES);
            objR.lngMeta = readNumber(arrIn_a, 32, PTR_BYTES);
            objR.lngSize = readNumber(arrIn_a, 40, LEN_BYTES);
            objR.lngBlobHash = readNumber(arrIn_a, 56, HASH_BYTES);
            objR.lngChainHash = readNumber(arrIn_a, 64, HASH_BYTES);
            objR.lngFlag = readNumber(arrIn_a, 72, FLAG_BYTES);
            return objR;
        }

        private bool recordHashOk(byte[] arrIn_a)
        {
            return readNumber(arrIn_a, 76, HASH_BYTES) == hashOf(decodeRecord(arrIn_a));
        }

        private void writeRecord(long lngRecord_a, Record objR_a)
        {
            m_objIndex.Write(m_lngIndexPrefix + lngRecord_a * RECORD_SIZE, encodeRecord(objR_a), RECORD_SIZE);
        }

        private bool readRecordBytes(FileStream objStream_a, long lngRecord_a, byte[] arrBuf_a)
        {
            bool blnResult = false;
            long lngAt = m_lngIndexPrefix + lngRecord_a * RECORD_SIZE;
            if (lngAt + RECORD_SIZE <= objStream_a.Length - m_lngIndexSuffix)
            {
                objStream_a.Position = lngAt;
                blnResult = readFully(objStream_a, arrBuf_a, RECORD_SIZE);
            }
            return blnResult;
        }

        private bool readRecord(long lngRecord_a, out Record objR_a)
        {
            bool blnResult = false;
            objR_a = new Record();
            byte[] arrBuf = new byte[RECORD_SIZE];
            for (int intS = 0; !blnResult && intS < m_objIndex.Streams.Count; intS++)
            {
                if (readRecordBytes(m_objIndex.Streams[intS], lngRecord_a, arrBuf) && recordHashOk(arrBuf))
                {
                    objR_a = decodeRecord(arrBuf);
                    long lngRecords = (m_objIndex.Length - m_lngIndexPrefix - m_lngIndexSuffix) / RECORD_SIZE;
                    blnResult = isDead(objR_a) || objR_a.lngNext >= 0 && objR_a.lngNext < lngRecords && objR_a.lngChild >= 0 && objR_a.lngChild < lngRecords
                             && objR_a.lngData >= 0 && objR_a.lngMeta >= 0 && objR_a.lngSize >= 0 && objR_a.lngData + objR_a.lngMeta + objR_a.lngSize <= m_objData.Length;
                }
            }
            return blnResult;
        }
    }

    /// <summary>Stores blobs exactly as given: for content already encoded elsewhere. The index, envelope and blob hashes still apply; capacity is 1:1.</summary>
    public sealed class PassthroughCodec : IHardfileCodec
    {
        public byte[] Encode(byte[] arrPlain_a) { return arrPlain_a; }
        public byte[] Decode(byte[] arrEncoded_a) { return arrEncoded_a; }
    }

    public sealed class UnsignalChainCodec : IHardfileCodec
    {
        private ZOSCIIRom[] m_arrRoms;
        public UnsignalChainCodec(ZOSCIIRom[] arrRoms_a) { m_arrRoms = arrRoms_a; }
        public byte[] Encode(byte[] arrPlain_a) { return UEncode.Chain(arrPlain_a, m_arrRoms); }
        public byte[] Decode(byte[] arrEncoded_a) { return UDecode.Chain(arrEncoded_a, m_arrRoms); }
    }

    public sealed class SchemeCodec : IHardfileCodec
    {
        private Scheme m_objScheme;
        private ISchemeRomResolver m_objResolver;
        public SchemeCodec(Scheme objScheme_a, ISchemeRomResolver objResolver_a) { m_objScheme = objScheme_a; m_objResolver = objResolver_a; }
        public byte[] Encode(byte[] arrPlain_a) { return SchemeRunner.Encode(arrPlain_a, m_objScheme, m_objResolver); }
        public byte[] Decode(byte[] arrEncoded_a) { return SchemeRunner.Decode(arrEncoded_a, m_objScheme, m_objResolver); }
    }

    public sealed class ScratchHardfile : IDisposable
    {
        private Hardfile m_objHf = null;
        private HardfilePaths m_objPaths = null;

        public Hardfile Store { get { return m_objHf; } }

        public static ScratchHardfile Create(string strBase_a, long lngGrowth_a, ZOSCIIRom objImage_a)
        {
            ScratchHardfile objResult = null;
            HardfilePaths objPaths = new HardfilePaths();
            objPaths.Data = strBase_a + ".hft";
            objPaths.Index = strBase_a + ".hfti";
            objPaths.Mirror = "";
            long lngGrowth = Math.Max(lngGrowth_a, 65536);
            Hardfile objHf = Hardfile.Create(objPaths, lngGrowth, lngGrowth, objImage_a);
            if (objHf != null) { objResult = new ScratchHardfile(); objResult.m_objHf = objHf; objResult.m_objPaths = objPaths; }
            return objResult;
        }

        public void Discard()
        {
            if (m_objHf != null) { m_objHf.Dispose(); m_objHf = null; }
            if (m_objPaths != null)
            {
                if (File.Exists(m_objPaths.Data)) { SecureDelete.File(m_objPaths.Data); }
                if (File.Exists(m_objPaths.Index)) { SecureDelete.File(m_objPaths.Index); }
                m_objPaths = null;
            }
        }

        public void Dispose() { Discard(); }
    }
}
