// CyborgUnicorn.ZOSCII - ZTB (ZOSCII Tamperproof Blockchain)
// Quantum-proof by structure: integrity via rolling ROM + hash, no crypto assumptions.
// Payload security is caller's responsibility — ZTB is transparency + provenance only.
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace CyborgUnicorn.ZOSCII
{
    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    internal static class ErrorLog
    {
        private static readonly bool   blnEnabled = true;
        private static readonly string strLogFile = "ztb_debug.log";
        private static readonly object objLock    = new object();

        internal static void Write(string strMsg_a)
        {
            if (blnEnabled)
            {
                try
                {
                    lock (objLock)
                    {
                        File.AppendAllText(strLogFile,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " +
                            strMsg_a + Environment.NewLine);
                    }
                }
                catch { }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    public enum ZTBBlockType
    {
        Genesis    = 0,
        Normal     = 1,
        Checkpoint = 2,
        Truncation = 3,
        Finalise   = 4,
        Bridge     = 5
    }

    public enum ZTBHashType
    {
        CRC32Full   = 0,
        CRC321KB    = 1,
        RollingFull = 2,
        Rolling1KB  = 3
    }

    // -------------------------------------------------------------------------
    // Result types
    // -------------------------------------------------------------------------

    public class ZTBBlockResult
    {
        public bool         Success;
        public string       BlockID;
        public string       PrevBlockID;
        public string       TrunkID;
        public bool         IsBranch;
        public ZTBBlockType BlockType;
        public ZTBHashType  HashType;
        public uint         Hash;
        public uint         PrevHash;
        public long         PayloadLen;
        public long         PaddedLen;
        public string       Filename;
        public byte[]       Payload;
    }

    public class ZTBVerifyResult
    {
        public bool   Success;
        public int    VerifiedBlocks;
        public int    FailedBlocks;
    }

    public class ZTBBlockInfo
    {
        public string BlockID;
        public string Filename;
        public byte[] RawBytes;
    }

    // -------------------------------------------------------------------------
    // clsZTB — internal engine
    // -------------------------------------------------------------------------

    internal static class clsZTB
    {
        // --- Constants ---
        internal const int    ROM_SIZE          = 65536;
        internal const int    MIN_PAYLOAD_SIZE  = 512;
        internal const int    MAX_HISTORY_BLOCKS= 64;
        internal const int    ROM_ENTRY_SIZE    = 1024;
        internal const string NULL_GUID         = "00000000-0000-0000-0000-000000000000";

        // Block format:
        // bytes 0-110:   RAW
        //   byte  0:       block_type
        //   byte  1:       block_version (1)
        //   byte  2:       is_branch
        //   bytes 3-38:    trunk_id       (36 bytes ASCII)
        //   bytes 39-74:   block_id       (36 bytes ASCII)
        //   bytes 75-110:  prev_block_id  (36 bytes ASCII)
        // bytes 111+:    ZOSCII encoded
        //   byte  0:       hash_type
        //   bytes 1-4:     hash
        //   bytes 5-8:     prev_hash
        //   bytes 9-12:    payload_len
        //   bytes 13-16:   padded_len
        //   bytes 17+:     payload

        internal const int HEADER_RAW_SIZE     = 111;
        internal const int RAW_OFF_BLOCK_TYPE  = 0;
        internal const int RAW_OFF_BLOCK_VER   = 1;
        internal const int RAW_OFF_IS_BRANCH   = 2;
        internal const int RAW_OFF_TRUNK_ID    = 3;
        internal const int RAW_OFF_BLOCK_ID    = 39;
        internal const int RAW_OFF_PREV_ID     = 75;

        internal const int ENC_OFF_HASH_TYPE   = 0;
        internal const int ENC_OFF_HASH        = 1;
        internal const int ENC_OFF_PREV_HASH   = 5;
        internal const int ENC_OFF_PAYLOAD_LEN = 9;
        internal const int ENC_OFF_PADDED_LEN  = 13;
        internal const int ENC_OFF_PAYLOAD     = 17;
        internal const int ENC_HEADER_SIZE     = 17;

        internal const int GENESIS_SIZE        = ROM_SIZE;

        // --- CRC32 ---

        internal static uint CalculateCRC32(byte[] arrData_a, int intOffset_a, int intLen_a)
        {
            uint intCrc = 0xFFFFFFFF;
            int intI    = intOffset_a;
            int intEnd  = intOffset_a + intLen_a;

            while (intI < intEnd)
            {
                intCrc ^= arrData_a[intI];
                int intJ = 0;
                while (intJ < 8)
                {
                    if ((intCrc & 1) != 0) { intCrc = (intCrc >> 1) ^ 0xEDB88320; }
                    else                   { intCrc = intCrc >> 1; }
                    intJ++;
                }
                intI++;
            }

            return intCrc ^ 0xFFFFFFFF;
        }

        // --- Hash ---

        internal static uint HashBytes(ZTBHashType objHashType_a, byte[] arrData_a,
                                        int intOffset_a, int intLen_a)
        {
            uint intResult = 0;
            int intHashLen = intLen_a;

            if (objHashType_a == ZTBHashType.CRC321KB || objHashType_a == ZTBHashType.Rolling1KB)
            {
                intHashLen = Math.Min(intLen_a, 1024);
            }

            if (objHashType_a == ZTBHashType.CRC32Full || objHashType_a == ZTBHashType.CRC321KB)
            {
                intResult = CalculateCRC32(arrData_a, intOffset_a, intHashLen);
            }
            else
            {
                byte[] arrSlice = new byte[intHashLen];
                Array.Copy(arrData_a, intOffset_a, arrSlice, 0, intHashLen);
                byte[] arrHash = ZRollingHash.Bytes(arrSlice, false);
                if (arrHash != null && arrHash.Length >= 4)
                {
                    intResult = (uint)(arrHash[0] | (arrHash[1] << 8) |
                                      (arrHash[2] << 16) | (arrHash[3] << 24));
                }
            }

            return intResult;
        }

        // --- XorShift32 ---

        internal static uint XorShift32(uint intState_a)
        {
            uint intX = intState_a;
            intX ^= intX << 13;
            intX ^= intX >> 17;
            intX ^= intX << 5;
            return intX;
        }

        // --- String helpers ---

        internal static string ReadFixedString(byte[] arrData_a, int intOffset_a, int intLen_a)
        {
            string strResult = null;

            try
            {
                int intEnd = intOffset_a;
                while (intEnd < intOffset_a + intLen_a && intEnd < arrData_a.Length &&
                       arrData_a[intEnd] != 0)
                {
                    intEnd++;
                }
                strResult = Encoding.ASCII.GetString(arrData_a, intOffset_a, intEnd - intOffset_a);
            }
            catch { }

            return strResult;
        }

        internal static void WriteFixedString(byte[] arrData_a, int intOffset_a,
                                               int intLen_a, string strValue_a)
        {
            try
            {
                byte[] arrStr = Encoding.ASCII.GetBytes(strValue_a ?? "");
                int intCopy   = Math.Min(arrStr.Length, intLen_a);
                Array.Copy(arrStr, 0, arrData_a, intOffset_a, intCopy);
                int intI = intOffset_a + intCopy;
                while (intI < intOffset_a + intLen_a)
                {
                    arrData_a[intI] = 0;
                    intI++;
                }
            }
            catch { }
        }

        // --- Raw header ---

        internal static byte[] WriteRawHeader(ZTBBlockType objBlockType_a, bool blnIsBranch_a,
                                               string strTrunkID_a, string strBlockID_a,
                                               string strPrevBlockID_a)
        {
            byte[] arrResult = new byte[HEADER_RAW_SIZE];
            arrResult[RAW_OFF_BLOCK_TYPE] = (byte)objBlockType_a;
            arrResult[RAW_OFF_BLOCK_VER]  = 1;
            arrResult[RAW_OFF_IS_BRANCH]  = blnIsBranch_a ? (byte)1 : (byte)0;
            WriteFixedString(arrResult, RAW_OFF_TRUNK_ID, 36, strTrunkID_a ?? NULL_GUID);
            WriteFixedString(arrResult, RAW_OFF_BLOCK_ID, 36, strBlockID_a ?? NULL_GUID);
            WriteFixedString(arrResult, RAW_OFF_PREV_ID,  36, strPrevBlockID_a ?? NULL_GUID);
            return arrResult;
        }

        internal static void ReadRawHeader(byte[] arrData_a, out ZTBBlockType objBlockType_a,
                                            out bool blnIsBranch_a, out string strTrunkID_a,
                                            out string strBlockID_a, out string strPrevBlockID_a)
        {
            objBlockType_a  = (ZTBBlockType)arrData_a[RAW_OFF_BLOCK_TYPE];
            blnIsBranch_a   = (arrData_a[RAW_OFF_IS_BRANCH] != 0);
            strTrunkID_a    = ReadFixedString(arrData_a, RAW_OFF_TRUNK_ID, 36);
            strBlockID_a    = ReadFixedString(arrData_a, RAW_OFF_BLOCK_ID, 36);
            strPrevBlockID_a = ReadFixedString(arrData_a, RAW_OFF_PREV_ID, 36);
        }

        // --- Encoded section ---

        internal static byte[] WriteEncodedSection(ZTBHashType objHashType_a, uint intHash_a,
                                                    uint intPrevHash_a, uint intPayloadLen_a,
                                                    uint intPaddedLen_a, byte[] arrPadded_a)
        {
            byte[] arrResult = new byte[ENC_HEADER_SIZE + arrPadded_a.Length];
            arrResult[ENC_OFF_HASH_TYPE] = (byte)objHashType_a;

            arrResult[ENC_OFF_HASH]     = (byte)(intHash_a & 0xFF);
            arrResult[ENC_OFF_HASH + 1] = (byte)((intHash_a >> 8) & 0xFF);
            arrResult[ENC_OFF_HASH + 2] = (byte)((intHash_a >> 16) & 0xFF);
            arrResult[ENC_OFF_HASH + 3] = (byte)((intHash_a >> 24) & 0xFF);

            arrResult[ENC_OFF_PREV_HASH]     = (byte)(intPrevHash_a & 0xFF);
            arrResult[ENC_OFF_PREV_HASH + 1] = (byte)((intPrevHash_a >> 8) & 0xFF);
            arrResult[ENC_OFF_PREV_HASH + 2] = (byte)((intPrevHash_a >> 16) & 0xFF);
            arrResult[ENC_OFF_PREV_HASH + 3] = (byte)((intPrevHash_a >> 24) & 0xFF);

            arrResult[ENC_OFF_PAYLOAD_LEN]     = (byte)(intPayloadLen_a & 0xFF);
            arrResult[ENC_OFF_PAYLOAD_LEN + 1] = (byte)((intPayloadLen_a >> 8) & 0xFF);
            arrResult[ENC_OFF_PAYLOAD_LEN + 2] = (byte)((intPayloadLen_a >> 16) & 0xFF);
            arrResult[ENC_OFF_PAYLOAD_LEN + 3] = (byte)((intPayloadLen_a >> 24) & 0xFF);

            arrResult[ENC_OFF_PADDED_LEN]     = (byte)(intPaddedLen_a & 0xFF);
            arrResult[ENC_OFF_PADDED_LEN + 1] = (byte)((intPaddedLen_a >> 8) & 0xFF);
            arrResult[ENC_OFF_PADDED_LEN + 2] = (byte)((intPaddedLen_a >> 16) & 0xFF);
            arrResult[ENC_OFF_PADDED_LEN + 3] = (byte)((intPaddedLen_a >> 24) & 0xFF);

            Array.Copy(arrPadded_a, 0, arrResult, ENC_OFF_PAYLOAD, arrPadded_a.Length);
            return arrResult;
        }

        internal static bool ReadEncodedSection(byte[] arrDecoded_a, out ZTBHashType objHashType_a,
                                                  out uint intHash_a, out uint intPrevHash_a,
                                                  out uint intPayloadLen_a, out uint intPaddedLen_a)
        {
            objHashType_a   = ZTBHashType.RollingFull;
            intHash_a       = 0;
            intPrevHash_a   = 0;
            intPayloadLen_a = 0;
            intPaddedLen_a  = 0;
            bool blnResult  = false;

            try
            {
                if (arrDecoded_a != null && arrDecoded_a.Length >= ENC_HEADER_SIZE)
                {
                    objHashType_a   = (ZTBHashType)arrDecoded_a[ENC_OFF_HASH_TYPE];
                    intHash_a       = (uint)(arrDecoded_a[ENC_OFF_HASH] |
                                            (arrDecoded_a[ENC_OFF_HASH + 1] << 8) |
                                            (arrDecoded_a[ENC_OFF_HASH + 2] << 16) |
                                            (arrDecoded_a[ENC_OFF_HASH + 3] << 24));
                    intPrevHash_a   = (uint)(arrDecoded_a[ENC_OFF_PREV_HASH] |
                                            (arrDecoded_a[ENC_OFF_PREV_HASH + 1] << 8) |
                                            (arrDecoded_a[ENC_OFF_PREV_HASH + 2] << 16) |
                                            (arrDecoded_a[ENC_OFF_PREV_HASH + 3] << 24));
                    intPayloadLen_a = (uint)(arrDecoded_a[ENC_OFF_PAYLOAD_LEN] |
                                            (arrDecoded_a[ENC_OFF_PAYLOAD_LEN + 1] << 8) |
                                            (arrDecoded_a[ENC_OFF_PAYLOAD_LEN + 2] << 16) |
                                            (arrDecoded_a[ENC_OFF_PAYLOAD_LEN + 3] << 24));
                    intPaddedLen_a  = (uint)(arrDecoded_a[ENC_OFF_PADDED_LEN] |
                                            (arrDecoded_a[ENC_OFF_PADDED_LEN + 1] << 8) |
                                            (arrDecoded_a[ENC_OFF_PADDED_LEN + 2] << 16) |
                                            (arrDecoded_a[ENC_OFF_PADDED_LEN + 3] << 24));
                    blnResult = true;
                }
            }
            catch { }

            return blnResult;
        }

        // --- ZOSCII encode/decode ---

        internal static byte[] ZOSCIIEncodeBlock(byte[] arrROM_a, byte[] arrData_a)
        {
            byte[] arrResult = null;

            try
            {
                using (ZOSCIIRom objROM = ZOSCIIRom.FromBytes(arrROM_a))
                {
                    arrResult = ZEncode.Bytes(arrData_a, objROM);
                }
            }
            catch { ErrorLog.Write("[ERR] ZOSCIIEncodeBlock"); }

            return arrResult;
        }

        internal static byte[] ZOSCIIDecodeBlock(byte[] arrROM_a, byte[] arrData_a,
                                                  int intOffset_a, int intLen_a)
        {
            byte[] arrResult = null;

            try
            {
                byte[] arrSlice = new byte[intLen_a];
                Array.Copy(arrData_a, intOffset_a, arrSlice, 0, intLen_a);

                using (ZOSCIIRom objROM = ZOSCIIRom.FromBytes(arrROM_a))
                {
                    arrResult = ZDecode.Bytes(arrSlice, objROM);
                }
            }
            catch { ErrorLog.Write("[ERR] ZOSCIIDecodeBlock"); }

            return arrResult;
        }

        // --- ROM entropy check ---

        internal static bool CheckROMEntropy(byte[] arrROM_a)
        {
            bool blnResult = false;

            try
            {
                if (arrROM_a != null && arrROM_a.Length == ROM_SIZE)
                {
                    int[] arrFreq = new int[256];
                    int intI = 0;
                    while (intI < arrROM_a.Length)
                    {
                        arrFreq[arrROM_a[intI]]++;
                        intI++;
                    }
                    int intMax = 0;
                    intI = 0;
                    while (intI < 256)
                    {
                        if (arrFreq[intI] > intMax) { intMax = arrFreq[intI]; }
                        intI++;
                    }
                    blnResult = (intMax < ROM_SIZE / 4);
                }
            }
            catch { }

            return blnResult;
        }

        // --- Block path ---

        internal static string GetBlockPath(string strWorkDir_a, string strBlockID_a)
        {
            return Path.Combine(strWorkDir_a, strBlockID_a + ".ztb");
        }

        // --- Load block bytes ---
        // Handles disk and memory chains uniformly

        internal static byte[] LoadBlock(string strWorkDir_a, string strBlockID_a,
                                          Func<string, byte[]> fnOnLoadBlock_a)
        {
            byte[] arrResult = null;

            try
            {
                string strFilename = strBlockID_a + ".ztb";

                if (fnOnLoadBlock_a != null)
                {
                    arrResult = fnOnLoadBlock_a(strFilename);
                }

                if (arrResult == null && strWorkDir_a != null)
                {
                    string strPath = GetBlockPath(strWorkDir_a, strBlockID_a);
                    if (File.Exists(strPath)) { arrResult = File.ReadAllBytes(strPath); }
                }
            }
            catch { ErrorLog.Write("[ERR] LoadBlock"); }

            return arrResult;
        }

        // --- Walk back via PrevBlockID ---
        // Returns list of block bytes walking back from strBlockID_a (inclusive), up to intMax_a

        internal static List<byte[]> WalkBack(string strWorkDir_a, string strBlockID_a,
                                               int intMax_a, Func<string, byte[]> fnOnLoadBlock_a)
        {
            List<byte[]> arrResult = new List<byte[]>();

            try
            {
                string strCurrentID = strBlockID_a;
                int intCount        = 0;

                while (strCurrentID != null && strCurrentID != NULL_GUID &&
                       strCurrentID.Length > 0 && intCount < intMax_a)
                {
                    byte[] arrBytes = LoadBlock(strWorkDir_a, strCurrentID, fnOnLoadBlock_a);
                    if (arrBytes == null || arrBytes.Length < HEADER_RAW_SIZE) { break; }

                    arrResult.Insert(0, arrBytes);
                    intCount++;

                    // Stop walking at truncation block — its payload contains the ROM
                    if (arrBytes[RAW_OFF_BLOCK_TYPE] == (byte)ZTBBlockType.Truncation) { break; }

                    strCurrentID = ReadFixedString(arrBytes, RAW_OFF_PREV_ID, 36);
                }
            }
            catch { ErrorLog.Write("[ERR] WalkBack"); }

            return arrResult;
        }

        // --- Find genesis (by file size = ROM_SIZE) ---

        internal static byte[] FindGenesis(string strWorkDir_a,
                                            Func<string, byte[]> fnOnLoadBlock_a,
                                            Func<string, byte[]> fnOnFindGenesis_a)
        {
            byte[] arrResult = null;

            try
            {
                if (fnOnFindGenesis_a != null)
                {
                    arrResult = fnOnFindGenesis_a(null);
                }

                if (arrResult == null && strWorkDir_a != null)
                {
                    string[] arrFiles = Directory.GetFiles(strWorkDir_a, "*.ztb",
                                                            SearchOption.AllDirectories);
                    int intI = 0;
                    while (intI < arrFiles.Length && arrResult == null)
                    {
                        if (new FileInfo(arrFiles[intI]).Length == ROM_SIZE)
                        {
                            arrResult = File.ReadAllBytes(arrFiles[intI]);
                        }
                        intI++;
                    }
                }
            }
            catch { ErrorLog.Write("[ERR] FindGenesis"); }

            return arrResult;
        }

        // --- Build rolling ROM ---

        internal static byte[] BuildRollingROM(string strWorkDir_a,
                                                string strPrevBlockID_a,
                                                Func<string, byte[]> fnOnLoadBlock_a,
                                                Func<string, byte[]> fnOnFindGenesis_a)
        {
            byte[] arrResult = null;

            try
            {
                byte[] arrROM      = new byte[ROM_SIZE];
                int intBytesCopied = 0;
                int intSamples     = 0;
                byte[] arrTruncPayload = null;

                if (strPrevBlockID_a != null && strPrevBlockID_a != NULL_GUID)
                {
                    List<byte[]> arrHistory = WalkBack(strWorkDir_a, strPrevBlockID_a,
                                                       MAX_HISTORY_BLOCKS, fnOnLoadBlock_a);

                    // Check if oldest block is a truncation block — its payload IS the raw ROM
                    if (arrHistory.Count > 0 &&
                        arrHistory[0][RAW_OFF_BLOCK_TYPE] == (byte)ZTBBlockType.Truncation)
                    {
                        byte[] arrTrunc = arrHistory[0];
                        if (arrTrunc.Length >= HEADER_RAW_SIZE + ROM_SIZE)
                        {
                            arrTruncPayload = new byte[ROM_SIZE];
                            Array.Copy(arrTrunc, HEADER_RAW_SIZE, arrTruncPayload, 0, ROM_SIZE);
                        }
                        arrHistory.RemoveAt(0);
                    }

                    int intI = 0;
                    while (intI < arrHistory.Count &&
                           intBytesCopied + ROM_ENTRY_SIZE <= ROM_SIZE &&
                           intSamples < MAX_HISTORY_BLOCKS)
                    {
                        Array.Copy(arrHistory[intI], 0, arrROM, intBytesCopied, ROM_ENTRY_SIZE);
                        intBytesCopied += ROM_ENTRY_SIZE;
                        intSamples++;
                        intI++;
                    }
                }

                if (intBytesCopied < ROM_SIZE)
                {
                    // Use truncation payload if available, otherwise genesis
                    byte[] arrFillSource = arrTruncPayload;
                    if (arrFillSource == null)
                    {
                        arrFillSource = FindGenesis(strWorkDir_a, fnOnLoadBlock_a, fnOnFindGenesis_a);
                    }

                    if (arrFillSource != null && arrFillSource.Length == ROM_SIZE)
                    {
                        Array.Copy(arrFillSource, 0, arrROM, intBytesCopied, ROM_SIZE - intBytesCopied);
                        arrResult = arrROM;
                    }
                }
                else
                {
                    arrResult = arrROM;
                }
            }
            catch { ErrorLog.Write("[ERR] BuildRollingROM"); }

            return arrResult;
        }

        // --- Write block ---

        internal static ZTBBlockResult WriteBlock(string strWorkDir_a, string strChainID_a,
                                                   string strNewBlockID_a, string strPrevBlockID_a,
                                                   byte[] arrPayload_a,
                                                   ZTBBlockType objBlockType_a,
                                                   ZTBHashType objHashType_a,
                                                   bool blnIsBranch_a, string strTrunkChainID_a,
                                                   Func<string, byte[]> fnOnLoadBlock_a,
                                                   Func<string, byte[]> fnOnFindGenesis_a,
                                                   Func<ZTBBlockResult, byte[], string, bool> fnOnSaveBlock_a,
                                                   Func<ZTBBlockResult, byte[], string, bool> fnOnBeforeSave_a,
                                                   Func<ZTBBlockResult, string, bool> fnOnAfterSave_a)
        {
            ZTBBlockResult objResult = new ZTBBlockResult();
            objResult.Success = false;

            try
            {
                // Pad payload so the COMPLETE on-disk block reaches at least ROM_ENTRY_SIZE
                // (1024) bytes. The rolling ROM unconditionally copies ROM_ENTRY_SIZE bytes
                // from each historical block's full on-disk file (see BuildRollingROM) — if
                // any block were smaller than that, the sample would run past the end of the
                // block. Total on-disk size = raw header (never encoded) + 2x(encoded-section
                // header + payload), since ZOSCII encoding doubles every byte it touches.
                int intPayloadLen = arrPayload_a != null ? arrPayload_a.Length : 0;
                int intPaddedLen  = intPayloadLen;
                int intTotalSize  = HEADER_RAW_SIZE + 2 * (ENC_HEADER_SIZE + intPaddedLen);

                if (intTotalSize <= ROM_ENTRY_SIZE)
                {
                    // Smallest intPaddedLen such that the total strictly exceeds ROM_ENTRY_SIZE
                    intPaddedLen = (ROM_ENTRY_SIZE - HEADER_RAW_SIZE) / 2 - ENC_HEADER_SIZE + 1;
                    if (intPaddedLen < intPayloadLen) { intPaddedLen = intPayloadLen; }
                }

                byte[] arrPadded  = new byte[intPaddedLen];
                if (arrPayload_a != null) { Array.Copy(arrPayload_a, arrPadded, intPayloadLen); }

                if (intPaddedLen > intPayloadLen)
                {
                    uint intSeed = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFFFFFF);
                    intSeed = XorShift32(intSeed);
                    int intI = intPayloadLen;
                    while (intI < intPaddedLen)
                    {
                        intSeed        = XorShift32(intSeed);
                        arrPadded[intI] = (byte)(intSeed & 0xFF);
                        intI++;
                    }
                }

                // Build raw header
                byte[] arrRawHeader = WriteRawHeader(objBlockType_a, blnIsBranch_a,
                                                     blnIsBranch_a ? strTrunkChainID_a : NULL_GUID,
                                                     strNewBlockID_a,
                                                     strPrevBlockID_a ?? NULL_GUID);

                // Build rolling ROM from prev block
                byte[] arrRollingROM = BuildRollingROM(strWorkDir_a, strPrevBlockID_a,
                                                        fnOnLoadBlock_a, fnOnFindGenesis_a);
                if (arrRollingROM != null)
                {
                    // Prev hash
                    uint intPrevHash = 0;
                    if (strPrevBlockID_a != null && strPrevBlockID_a != NULL_GUID)
                    {
                        byte[] arrPrevBytes = LoadBlock(strWorkDir_a, strPrevBlockID_a,
                                                        fnOnLoadBlock_a);
                        if (arrPrevBytes != null)
                        {
                            intPrevHash = HashBytes(objHashType_a, arrPrevBytes, 0,
                                                    arrPrevBytes.Length);
                        }
                    }

                    // Current block hash covers the payload ONLY (not header/metadata).
                    // This keeps the payload hashable independently of the header, which
                    // matters once payload hashing is streamed — the header is always
                    // small and fully in-memory, so it must never be folded into a hash
                    // that the payload portion needs to compute incrementally.
                    uint intHash = HashBytes(objHashType_a, arrPadded, 0, arrPadded.Length);

                    // Build encoded section with the real hash and prev hash, ZOSCII encode
                    byte[] arrEncRaw = WriteEncodedSection(objHashType_a, intHash, intPrevHash,
                                                           (uint)intPayloadLen, (uint)intPaddedLen,
                                                           arrPadded);
                    byte[] arrEncZOSCII = ZOSCIIEncodeBlock(arrRollingROM, arrEncRaw);

                    if (arrEncZOSCII != null)
                    {
                        byte[] arrFinalOutput = new byte[HEADER_RAW_SIZE + arrEncZOSCII.Length];
                        Array.Copy(arrRawHeader, 0, arrFinalOutput, 0,               HEADER_RAW_SIZE);
                        Array.Copy(arrEncZOSCII, 0, arrFinalOutput, HEADER_RAW_SIZE, arrEncZOSCII.Length);

                        string strFilename = strNewBlockID_a + ".ztb";
                        string strFilepath = strWorkDir_a != null
                                             ? Path.Combine(strWorkDir_a, strFilename)
                                             : strFilename;

                        // objResult.ChainID    = strChainID_a;
                        objResult.BlockID    = strNewBlockID_a;
                        objResult.PrevBlockID = strPrevBlockID_a ?? NULL_GUID;
                        objResult.TrunkID    = blnIsBranch_a ? strTrunkChainID_a : NULL_GUID;
                        objResult.IsBranch   = blnIsBranch_a;
                        objResult.BlockType  = objBlockType_a;
                        objResult.HashType   = objHashType_a;
                        objResult.Hash       = intHash;
                        objResult.PrevHash   = intPrevHash;
                        objResult.PayloadLen = intPayloadLen;
                        objResult.PaddedLen  = intPaddedLen;
                        objResult.Filename   = strFilename;

                        bool blnSaved = false;

                        if (fnOnBeforeSave_a != null)
                        {
                            if (!fnOnBeforeSave_a(objResult, arrFinalOutput, strFilepath))
                            {
                                return objResult;
                            }
                        }

                        if (fnOnSaveBlock_a != null)
                        {
                            blnSaved = fnOnSaveBlock_a(objResult, arrFinalOutput, strFilepath);
                        }

                        if (!blnSaved && strWorkDir_a != null)
                        {
                            string strTmp = strFilepath + ".tmp";
                            File.WriteAllBytes(strTmp, arrFinalOutput);
                            if (File.Exists(strFilepath)) { SecureDelete.File(strFilepath); }
                            File.Move(strTmp, strFilepath);
                            blnSaved = true;
                        }

                        if (blnSaved)
                        {
                            objResult.Success = true;
                            if (fnOnAfterSave_a != null) { fnOnAfterSave_a(objResult, strFilepath); }
                        }
                    }
                }
            }
            catch { ErrorLog.Write("[ERR] WriteBlock"); }

            return objResult;
        }

        // --- Discover branches (reads trunk_id from raw header) ---

        internal static List<string> DiscoverBranches(string strWorkDir_a, string strTrunkID_a)
        {
            List<string> arrResult = new List<string>();

            try
            {
                string[] arrFiles = Directory.GetFiles(strWorkDir_a, "*.ztb",
                                                        SearchOption.AllDirectories);
                System.Collections.Generic.HashSet<string> arrSeen =
                    new System.Collections.Generic.HashSet<string>();

                int intI = 0;
                while (intI < arrFiles.Length)
                {
                    // Skip genesis files
                    if (new FileInfo(arrFiles[intI]).Length != ROM_SIZE)
                    {
                        byte[] arrBytes = File.ReadAllBytes(arrFiles[intI]);
                        if (arrBytes != null && arrBytes.Length >= HEADER_RAW_SIZE)
                        {
                            bool blnIsBranch = (arrBytes[RAW_OFF_IS_BRANCH] != 0);
                            if (blnIsBranch)
                            {
                                string strTrunk = ReadFixedString(arrBytes, RAW_OFF_TRUNK_ID, 36);
                                if (strTrunk == strTrunkID_a)
                                {
                                    // Extract chain ID from filename
                                    string strFN    = Path.GetFileNameWithoutExtension(arrFiles[intI]);
                                    int intUnder    = strFN.IndexOf('_');
                                    if (intUnder > 0)
                                    {
                                        string strChainID = strFN.Substring(0, intUnder);
                                        if (strChainID != strTrunkID_a && !arrSeen.Contains(strChainID))
                                        {
                                            arrSeen.Add(strChainID);
                                            arrResult.Add(strChainID);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    intI++;
                }
            }
            catch { ErrorLog.Write("[ERR] DiscoverBranches"); }

            return arrResult;
        }
    }

    // -------------------------------------------------------------------------
    // ZTBChain — public API
    // -------------------------------------------------------------------------

    public class ZTBChain : IDisposable
    {
        private bool        m_blnDisposed = false;
        private string      m_strWorkDir;
        private string      m_strChainID;
        private ZTBHashType m_objHashType = ZTBHashType.RollingFull;

        // Callbacks
        public Func<ZTBBlockResult, byte[], string, bool>  OnBeforeSaveBlock  = null;
        public Func<ZTBBlockResult, byte[], string, bool>  OnSaveBlock        = null;
        public Func<ZTBBlockResult, string, bool>          OnAfterSaveBlock   = null;
        public Func<string, byte[]>                        OnLoadBlock        = null;
        public Func<string, byte[]>                        OnFindGenesis      = null;

        public const int    ROM_SIZE_PUBLIC     = clsZTB.ROM_SIZE;
        public const int    GENESIS_SIZE_PUBLIC = clsZTB.ROM_SIZE;
        public const int    HEADER_RAW_SIZE     = clsZTB.HEADER_RAW_SIZE;
        public const string NULL_GUID           = clsZTB.NULL_GUID;

        private ZTBChain() { }

        // -------------------------------------------------------------------------
        // Create — write genesis block from entropy sources
        // -------------------------------------------------------------------------

        public static bool Create(string strNewBlockID_a, string[] arrSourcePaths_a,
                                   string strWorkDir_a, string strChainID_a)
        {
            bool blnResult = false;

            try
            {
                string strOutputPath = Path.Combine(strWorkDir_a, strNewBlockID_a + ".ztb");
                if (File.Exists(strOutputPath)) { return false; }

                bool blnValid = (arrSourcePaths_a != null && arrSourcePaths_a.Length > 0 &&
                                 arrSourcePaths_a.Length <= 3);

                if (blnValid)
                {
                    byte[]   arrROM   = new byte[clsZTB.ROM_SIZE];
                    int      intCount = arrSourcePaths_a.Length;
                    double[] arrStep  = new double[intCount];
                    double[] arrPos   = new double[intCount];
                    byte[][] arrSrc   = new byte[intCount][];

                    int intI = 0;
                    while (intI < intCount && blnValid)
                    {
                        arrSrc[intI] = File.ReadAllBytes(arrSourcePaths_a[intI]);
                        if (arrSrc[intI].Length == 0) { blnValid = false; }
                        else { arrStep[intI] = (double)arrSrc[intI].Length / clsZTB.ROM_SIZE; }
                        intI++;
                    }

                    if (blnValid)
                    {
                        intI = 0;
                        while (intI < clsZTB.ROM_SIZE)
                        {
                            byte byVal = 0;
                            int intJ  = 0;
                            while (intJ < intCount)
                            {
                                long lngPos = (long)arrPos[intJ];
                                if (lngPos >= arrSrc[intJ].Length) { lngPos = arrSrc[intJ].Length - 1; }
                                byVal     ^= arrSrc[intJ][lngPos];
                                arrPos[intJ] += arrStep[intJ];
                                intJ++;
                            }
                            arrROM[intI] = byVal;
                            intI++;
                        }

                        byte[] arrGenBlock  = new byte[clsZTB.ROM_SIZE];
                        arrGenBlock[0]      = (byte)ZTBBlockType.Genesis;
                        Array.Copy(arrROM, 0, arrGenBlock, 1, clsZTB.ROM_SIZE - 1);

                        string strTmp = strOutputPath + ".tmp";
                        File.WriteAllBytes(strTmp, arrGenBlock);
                        File.Move(strTmp, strOutputPath);
                        blnResult = true;
                    }
                }
            }
            catch { ErrorLog.Write("[ERR] Create"); }

            return blnResult;
        }

        // -------------------------------------------------------------------------
        // Open
        // -------------------------------------------------------------------------

        public static ZTBChain Open(string strWorkDir_a, string strChainID_a,
                                     ZTBHashType objHashType_a = ZTBHashType.RollingFull)
        {
            ZTBChain objResult = null;

            try
            {
                bool blnValid = (strChainID_a != null && strChainID_a.Length > 0);
                if (blnValid && strWorkDir_a != null) { blnValid = Directory.Exists(strWorkDir_a); }

                if (blnValid)
                {
                    objResult               = new ZTBChain();
                    objResult.m_strWorkDir  = strWorkDir_a;
                    objResult.m_strChainID  = strChainID_a;
                    objResult.m_objHashType = objHashType_a;
                }
            }
            catch { ErrorLog.Write("[ERR] Open"); }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // AddBlock
        // -------------------------------------------------------------------------

        public ZTBBlockResult AddBlock(string strNewBlockID_a, string strPrevBlockID_a,
                                        byte[] arrPayload_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a, arrPayload_a,
                              ZTBBlockType.Normal, false, null);
        }

        public ZTBBlockResult AddBlockText(string strNewBlockID_a, string strPrevBlockID_a,
                                            string strText_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a,
                              Encoding.UTF8.GetBytes(strText_a ?? ""),
                              ZTBBlockType.Normal, false, null);
        }

        public ZTBBlockResult AddBlockFile(string strNewBlockID_a, string strPrevBlockID_a,
                                            string strFilePath_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a, File.ReadAllBytes(strFilePath_a),
                              ZTBBlockType.Normal, false, null);
        }

        // -------------------------------------------------------------------------
        // AddCheckpoint
        // -------------------------------------------------------------------------

        public ZTBBlockResult AddCheckpoint(string strNewBlockID_a, string strPrevBlockID_a,
                                             string strLabel_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a,
                              Encoding.UTF8.GetBytes(strLabel_a ?? ""),
                              ZTBBlockType.Checkpoint, false, null);
        }

        // -------------------------------------------------------------------------
        // AddBranch
        // -------------------------------------------------------------------------

        public ZTBBlockResult AddBranch(string strNewBlockID_a, string strPrevBlockID_a,
                                         byte[] arrPayload_a, string strTrunkChainID_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a, arrPayload_a,
                              ZTBBlockType.Normal, true, strTrunkChainID_a);
        }

        // -------------------------------------------------------------------------
        // FetchBlock
        // -------------------------------------------------------------------------

        public ZTBBlockResult FetchBlock(string strBlockID_a)
        {
            ZTBBlockResult objResult = new ZTBBlockResult();
            objResult.Success = false;

            try
            {
                byte[] arrBlockBytes = clsZTB.LoadBlock(m_strWorkDir, strBlockID_a, OnLoadBlock);
                if (arrBlockBytes != null && arrBlockBytes.Length >= clsZTB.HEADER_RAW_SIZE)
                {
                    ZTBBlockType objBlockType;
                    bool blnIsBranch;
                    string strTrunkID, strBlockID, strPrevBlockID;

                    clsZTB.ReadRawHeader(arrBlockBytes, out objBlockType, out blnIsBranch,
                                          out strTrunkID, out strBlockID, out strPrevBlockID);

                    // Truncation block is not encoded — return success directly
                    if (objBlockType == ZTBBlockType.Truncation)
                    {
                        objResult.Success     = true;
                        // objResult.ChainID     = m_strChainID;
                        objResult.BlockID     = strBlockID;
                        objResult.PrevBlockID = strPrevBlockID;
                        objResult.TrunkID     = strTrunkID;
                        objResult.IsBranch    = blnIsBranch;
                        objResult.BlockType   = ZTBBlockType.Truncation;
                        objResult.Filename    = strBlockID_a + ".ztb";
                        return objResult;
                    }

                    byte[] arrRollingROM = clsZTB.BuildRollingROM(m_strWorkDir, strPrevBlockID,
                                                                    OnLoadBlock, OnFindGenesis);
                    if (arrRollingROM != null)
                    {
                        int intEncLen     = arrBlockBytes.Length - clsZTB.HEADER_RAW_SIZE;
                        byte[] arrDecoded = clsZTB.ZOSCIIDecodeBlock(arrRollingROM, arrBlockBytes,
                                                                       clsZTB.HEADER_RAW_SIZE,
                                                                       intEncLen);
                        if (arrDecoded != null)
                        {
                            ZTBHashType objHashType;
                            uint intStoredHash, intStoredPrevHash, intPayloadLen, intPaddedLen;

                            if (clsZTB.ReadEncodedSection(arrDecoded, out objHashType,
                                                           out intStoredHash, out intStoredPrevHash,
                                                           out intPayloadLen, out intPaddedLen))
                            {
                                // Verify hash — current-block hash covers the payload ONLY,
                                // not the header or encoded-section metadata (see WriteBlock).
                                int intPaddedPayloadLen = arrDecoded.Length - clsZTB.ENC_OFF_PAYLOAD;
                                byte[] arrDecodedPayload = new byte[intPaddedPayloadLen];
                                Array.Copy(arrDecoded, clsZTB.ENC_OFF_PAYLOAD,
                                          arrDecodedPayload, 0, intPaddedPayloadLen);

                                uint intCalcHash  = clsZTB.HashBytes(objHashType, arrDecodedPayload,
                                                                      0, arrDecodedPayload.Length);
                                bool blnHashOK    = (intCalcHash == intStoredHash);

                                bool blnPrevHashOK = true;
                                if (intStoredPrevHash != 0 && strPrevBlockID != null &&
                                    strPrevBlockID != clsZTB.NULL_GUID)
                                {
                                    byte[] arrPrevBytes = clsZTB.LoadBlock(m_strWorkDir,
                                                                            strPrevBlockID, OnLoadBlock);
                                    if (arrPrevBytes != null)
                                    {
                                        // If prev is a truncation block, skip PrevHash check
                                        bool blnPrevIsTrunc = (arrPrevBytes.Length >= clsZTB.HEADER_RAW_SIZE &&
                                                               arrPrevBytes[clsZTB.RAW_OFF_BLOCK_TYPE] ==
                                                               (byte)ZTBBlockType.Truncation);
                                        if (!blnPrevIsTrunc)
                                        {
                                            uint intCalcPrev = clsZTB.HashBytes(objHashType, arrPrevBytes,
                                                                                 0, arrPrevBytes.Length);
                                            blnPrevHashOK = (intCalcPrev == intStoredPrevHash);
                                        }
                                    }
                                }

                                if (blnHashOK && blnPrevHashOK)
                                {
                                    byte[] arrPayload = new byte[intPayloadLen];
                                    if ((int)intPayloadLen <= arrDecoded.Length - clsZTB.ENC_OFF_PAYLOAD)
                                    {
                                        Array.Copy(arrDecoded, clsZTB.ENC_OFF_PAYLOAD,
                                                   arrPayload, 0, (int)intPayloadLen);
                                    }

                                    objResult.Success    = true;
                        // objResult.ChainID    = m_strChainID;
                                    objResult.BlockID    = strBlockID;
                                    objResult.PrevBlockID = strPrevBlockID;
                                    objResult.TrunkID    = strTrunkID;
                                    objResult.IsBranch   = blnIsBranch;
                                    objResult.BlockType  = objBlockType;
                                    objResult.HashType   = objHashType;
                                    objResult.Hash       = intStoredHash;
                                    objResult.PrevHash   = intStoredPrevHash;
                                    objResult.PayloadLen = intPayloadLen;
                                    objResult.PaddedLen  = intPaddedLen;
                                    objResult.Filename   = strBlockID_a + ".ztb";
                                    objResult.Payload    = arrPayload;
                                }
                            }
                        }
                    }
                }
            }
            catch { ErrorLog.Write("[ERR] FetchBlock"); }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Verify
        // -------------------------------------------------------------------------

        public ZTBVerifyResult Verify(string strBlockID_a, bool blnWalk_a)
        {
            ZTBVerifyResult objResult = new ZTBVerifyResult();

            try
            {
                string strCurrentID = strBlockID_a;

                while (strCurrentID != null && strCurrentID != clsZTB.NULL_GUID)
                {
                    ZTBBlockResult objFetch = FetchBlock(strCurrentID);

                    if (objFetch.Success)
                    {
                        objResult.VerifiedBlocks++;
                        strCurrentID = blnWalk_a ? objFetch.PrevBlockID : null;
                        if (objFetch.BlockType == ZTBBlockType.Truncation) { strCurrentID = null; }
                    }
                    else
                    {
                        objResult.FailedBlocks++;
                        strCurrentID = null;
                    }
                }

                objResult.Success = (objResult.FailedBlocks == 0 && objResult.VerifiedBlocks > 0);
            }
            catch { ErrorLog.Write("[ERR] Verify"); }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Truncate — writes truncation block below the nominated checkpoint GUID
        // -------------------------------------------------------------------------

        public ZTBBlockResult Truncate(string strNewBlockID_a, string strCheckpointBlockID_a)
        {
            ZTBBlockResult objResult = new ZTBBlockResult();
            objResult.Success = false;

            try
            {
                // Load the checkpoint to find block10 (checkpoint's prev)
                byte[] arrCP = clsZTB.LoadBlock(m_strWorkDir, strCheckpointBlockID_a, OnLoadBlock);
                if (arrCP != null && arrCP.Length >= clsZTB.HEADER_RAW_SIZE)
                {
                    string strBlock10ID = clsZTB.ReadFixedString(arrCP, clsZTB.RAW_OFF_PREV_ID, 36);

                    // Load block10 to get its PrevBlockID (block9)
                    byte[] arrBlock10 = clsZTB.LoadBlock(m_strWorkDir, strBlock10ID, OnLoadBlock);
                    if (arrBlock10 != null && arrBlock10.Length >= clsZTB.HEADER_RAW_SIZE)
                    {
                        string strBlock9ID = clsZTB.ReadFixedString(arrBlock10,
                                                                      clsZTB.RAW_OFF_PREV_ID, 36);

                        // Build the rolling ROM that block11 (checkpoint) would have used
                        byte[] arrRollingROM = clsZTB.BuildRollingROM(m_strWorkDir, strBlock10ID,
                                                                        OnLoadBlock, OnFindGenesis);
                        if (arrRollingROM != null)
                        {
                            // Write truncation block directly — raw header + raw ROM payload
                            // No ZOSCII encoding since there are no blocks below it
                            byte[] arrRawHeader = clsZTB.WriteRawHeader(ZTBBlockType.Truncation,
                                                                          false, clsZTB.NULL_GUID,
                                                                          strBlock10ID,
                                                                          clsZTB.NULL_GUID);
                            byte[] arrFinalOutput = new byte[clsZTB.HEADER_RAW_SIZE + arrRollingROM.Length];
                            Array.Copy(arrRawHeader,  0, arrFinalOutput, 0,                       clsZTB.HEADER_RAW_SIZE);
                            Array.Copy(arrRollingROM, 0, arrFinalOutput, clsZTB.HEADER_RAW_SIZE,  arrRollingROM.Length);

                            string strFilename = strBlock10ID + ".ztb";
                            string strFilepath = m_strWorkDir != null
                                                 ? Path.Combine(m_strWorkDir, strFilename)
                                                 : strFilename;

                            bool blnSaved = false;

                            if (OnSaveBlock != null)
                            {
                        // objResult.ChainID   = m_strChainID;
                                objResult.BlockID   = strBlock10ID;
                                objResult.BlockType = ZTBBlockType.Truncation;
                                objResult.Filename  = strFilename;
                                blnSaved = OnSaveBlock(objResult, arrFinalOutput, strFilepath);
                            }

                            if (!blnSaved && m_strWorkDir != null)
                            {
                                string strTmp = strFilepath + ".tmp";
                                File.WriteAllBytes(strTmp, arrFinalOutput);
                                if (File.Exists(strFilepath)) { SecureDelete.File(strFilepath); }
                                File.Move(strTmp, strFilepath);
                                blnSaved = true;
                            }

                            if (blnSaved)
                            {
                                objResult.Success    = true;
                        // objResult.ChainID    = m_strChainID;
                                objResult.BlockID    = strBlock10ID;
                                objResult.PrevBlockID = clsZTB.NULL_GUID;
                                objResult.BlockType  = ZTBBlockType.Truncation;
                                objResult.Filename   = strFilename;
                            }
                        }
                    }
                }
            }
            catch { ErrorLog.Write("[ERR] Truncate"); }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Finalise — writes finalise block after the specified prev block
        // -------------------------------------------------------------------------

        public ZTBBlockResult Finalise(string strNewBlockID_a, string strPrevBlockID_a,
                                        string strLabel_a)
        {
            return writeBlock(strNewBlockID_a, strPrevBlockID_a,
                              Encoding.UTF8.GetBytes(strLabel_a ?? ""),
                              ZTBBlockType.Finalise, false, null);
        }

        // -------------------------------------------------------------------------
        // Properties
        // -------------------------------------------------------------------------

        public string ChainID { get { return m_strChainID; } }
        public string WorkDir { get { return m_strWorkDir; } }

        // -------------------------------------------------------------------------
        // IDisposable
        // -------------------------------------------------------------------------

        public void Dispose()
        {
            if (!m_blnDisposed)
            {
                m_strWorkDir  = null;
                m_strChainID  = null;
                m_blnDisposed = true;
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private ZTBBlockResult writeBlock(string strNewBlockID_a, string strPrevBlockID_a,
                                           byte[] arrPayload_a, ZTBBlockType objBlockType_a,
                                           bool blnIsBranch_a, string strTrunkChainID_a)
        {
            bool blnIsBranch     = blnIsBranch_a;
            string strTrunkChain = strTrunkChainID_a;

            // If not explicitly flagged as branch, check prev block's raw header
            if (!blnIsBranch && strPrevBlockID_a != null && strPrevBlockID_a != clsZTB.NULL_GUID)
            {
                byte[] arrPrev = clsZTB.LoadBlock(m_strWorkDir, strPrevBlockID_a, OnLoadBlock);
                if (arrPrev != null && arrPrev.Length >= clsZTB.HEADER_RAW_SIZE)
                {
                    if (arrPrev[clsZTB.RAW_OFF_IS_BRANCH] != 0)
                    {
                        blnIsBranch  = true;
                        strTrunkChain = clsZTB.ReadFixedString(arrPrev, clsZTB.RAW_OFF_TRUNK_ID, 36);
                    }
                }
            }

            return clsZTB.WriteBlock(m_strWorkDir, m_strChainID,
                                      strNewBlockID_a, strPrevBlockID_a, arrPayload_a,
                                      objBlockType_a, m_objHashType,
                                      blnIsBranch, strTrunkChain,
                                      OnLoadBlock, OnFindGenesis,
                                      OnSaveBlock, OnBeforeSaveBlock, OnAfterSaveBlock);
        }
    }
}