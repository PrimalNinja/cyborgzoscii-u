// CyborgUnicorn.ZOSCII - ZOSCII
// ZOSCIIRom, clsZOSCII engine, ZEncode, ZDecode, ZVerify
// Compatible with zencode.c / zdecode.c / uverify.c -z
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

using System;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    // -------------------------------------------------------------------------
    // Core ROM structures — defined here so ZOSCII.cs is fully self-contained.
    // Unsignal.cs depends on these; they do NOT depend on Unsignal.cs.
    // -------------------------------------------------------------------------

    internal struct ByteAddresses
    {
        public uint[] ptrAddresses;
        public uint intCount;
    }

    internal struct RomData
    {
        public byte[] ptrROMData;
        public long lngROMSize;
        public ByteAddresses[] arrLookup;
    }

    // -------------------------------------------------------------------------
    // clsZOSCII — core ROM engine
    // Handles ROM loading, lookup table construction, and pure ZOSCII
    // encode/decode. No UNSIGNAL header/offset/prefix/suffix logic here.
    // -------------------------------------------------------------------------

    internal static class clsZOSCII
    {
        internal const int ROM_LOAD_MAX = 131072;

        // --- ROM management ---

        internal static RomData LoadRomFromBytes(byte[] arrData_a)
        {
            RomData objRom = new RomData();
            objRom.ptrROMData = null;
            objRom.lngROMSize = 0;
            objRom.arrLookup = null;

            try
            {
                long lngLoad = Math.Min(arrData_a.Length, ROM_LOAD_MAX);
                objRom.lngROMSize = lngLoad;
                objRom.ptrROMData = new byte[lngLoad];
                Array.Copy(arrData_a, objRom.ptrROMData, (int)lngLoad);
                BuildLookupTable(ref objRom);
            }
            catch
            {
                objRom.ptrROMData = null;
                objRom.lngROMSize = 0;
                objRom.arrLookup = null;
            }

            return objRom;
        }

        internal static void UnloadRom(ref RomData objRom_a)
        {
            objRom_a.ptrROMData = null;
            objRom_a.arrLookup = null;
            objRom_a.lngROMSize = 0;
        }

        internal static void BuildLookupTable(ref RomData objRom_a)
        {
            uint[] arrCounts = new uint[256];
            int intI = 0;

            objRom_a.arrLookup = new ByteAddresses[256];
            for (intI = 0; intI < 256; intI++)
            {
                objRom_a.arrLookup[intI].ptrAddresses = null;
                objRom_a.arrLookup[intI].intCount = 0;
            }

            // Only the first 64KB is addressable via 16-bit addresses
            long lngHeaderSize = (objRom_a.lngROMSize > 65536L) ? 65536L : objRom_a.lngROMSize;

            for (long lngI = 0; lngI < lngHeaderSize; lngI++)
            {
                arrCounts[objRom_a.ptrROMData[lngI]]++;
            }

            for (intI = 0; intI < 256; intI++)
            {
                if (arrCounts[intI] > 0)
                {
                    objRom_a.arrLookup[intI].ptrAddresses = new uint[arrCounts[intI]];
                    objRom_a.arrLookup[intI].intCount = 0;
                }
            }

            for (long lngI = 0; lngI < lngHeaderSize; lngI++)
            {
                byte by = objRom_a.ptrROMData[lngI];
                objRom_a.arrLookup[by].ptrAddresses[objRom_a.arrLookup[by].intCount++] = (uint)lngI;
            }
        }

        // --- Random seed ---
        // ROM window hash XOR TickCount — mirrors UNSIGNAL's createRandomSeed approach.

        internal static Random createRandomSeed(ref RomData objRom_a)
        {
            uint intWindowHash = 0;
            long lngWindowSize = objRom_a.lngROMSize;
            if (lngWindowSize > 65536L) { lngWindowSize = 65536L; }

            for (long lngI = 0; lngI < lngWindowSize; lngI++)
            {
                intWindowHash = (intWindowHash * 33) + objRom_a.ptrROMData[lngI];
            }

            return new Random((int)(intWindowHash ^ (uint)Environment.TickCount));
        }

        // --- Pure ZOSCII encode ---
        // Each plaintext byte -> randomly chosen 16-bit ROM address of a matching byte.
        // Output is exactly input.Length * 2 bytes. No header, no padding.

        internal static byte[] zencodeByteToByte(ref RomData objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            int intI = 0;

            try
            {
                arrResult = new byte[arrInput_a.Length * 2];
                int intPos = 0;
                Random ptrRand = createRandomSeed(ref objRom_a);

                for (intI = 0; intI < arrInput_a.Length; intI++)
                {
                    byte by = arrInput_a[intI];
                    uint intCount = objRom_a.arrLookup[by].intCount;

                    if (intCount > 0)
                    {
                        uint intIdx = (uint)ptrRand.Next((int)intCount);
                        ushort intAddr = (ushort)objRom_a.arrLookup[by].ptrAddresses[intIdx];
                        byte[] arrAddr = BitConverter.GetBytes(intAddr);
                        arrResult[intPos++] = arrAddr[0];
                        arrResult[intPos++] = arrAddr[1];
                    }
                    else
                    {
                        // ROM missing this byte value — encode fails
                        arrResult = null;
                        break;
                    }
                }
            }
            catch
            {
                arrResult = null;
            }

            return arrResult;
        }

        internal static bool zencodeFileToFile(ref RomData objRom_a, string strInputPath_a, string strOutputPath_a)
        {
            bool blnResult = false;

            try
            {
                Random ptrRand = createRandomSeed(ref objRom_a);

                using (FileStream ptrInput = new FileStream(strInputPath_a, FileMode.Open, FileAccess.Read))
                using (FileStream ptrOutput = new FileStream(strOutputPath_a, FileMode.Create, FileAccess.Write))
                {
                    int intCh = ptrInput.ReadByte();
                    bool blnOk = true;

                    while (intCh != -1 && blnOk)
                    {
                        byte by = (byte)intCh;
                        uint intCount = objRom_a.arrLookup[by].intCount;

                        if (intCount > 0)
                        {
                            uint intIdx = (uint)ptrRand.Next((int)intCount);
                            ushort intAddr = (ushort)objRom_a.arrLookup[by].ptrAddresses[intIdx];
                            ptrOutput.Write(BitConverter.GetBytes(intAddr), 0, 2);
                        }
                        else
                        {
                            blnOk = false;
                        }

                        intCh = ptrInput.ReadByte();
                    }

                    blnResult = blnOk;
                }
            }
            catch { }

            return blnResult;
        }

        // --- Pure ZOSCII decode ---
        // Each 16-bit ROM address -> the byte at that ROM address.

        internal static byte[] zdecodeByteToByte(RomData objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;

            try
            {
                long lngSlots = arrInput_a.Length / 2;
                arrResult = new byte[lngSlots];

                for (long lngI = 0; lngI < lngSlots; lngI++)
                {
                    ushort intAddr = BitConverter.ToUInt16(arrInput_a, (int)(lngI * 2));

                    if (intAddr < objRom_a.lngROMSize)
                    {
                        arrResult[lngI] = objRom_a.ptrROMData[intAddr];
                    }
                    else
                    {
                        arrResult = null;
                        break;
                    }
                }
            }
            catch
            {
                arrResult = null;
            }

            return arrResult;
        }

        internal static bool zdecodeFileToFile(RomData objRom_a, string strInputPath_a, string strOutputPath_a)
        {
            bool blnResult = false;

            try
            {
                byte[] arrBuf = new byte[2];

                using (FileStream ptrInput = new FileStream(strInputPath_a, FileMode.Open, FileAccess.Read))
                using (FileStream ptrOutput = new FileStream(strOutputPath_a, FileMode.Create, FileAccess.Write))
                {
                    bool blnOk = true;

                    while (blnOk)
                    {
                        int intRead = ptrInput.Read(arrBuf, 0, 2);
                        if (intRead == 0) { break; }
                        if (intRead != 2) { blnOk = false; break; }

                        ushort intAddr = BitConverter.ToUInt16(arrBuf, 0);

                        if (intAddr < objRom_a.lngROMSize)
                        {
                            ptrOutput.WriteByte(objRom_a.ptrROMData[intAddr]);
                        }
                        else
                        {
                            blnOk = false;
                        }
                    }

                    blnResult = blnOk;
                }
            }
            catch { }

            return blnResult;
        }

        internal static byte[] zdecodeFileToByte(RomData objRom_a, string strInputPath_a)
        {
            byte[] arrResult = null;

            try
            {
                using (FileStream ptrInput = new FileStream(strInputPath_a, FileMode.Open, FileAccess.Read))
                using (MemoryStream ptrOutput = new MemoryStream())
                {
                    byte[] arrBuf = new byte[2];
                    bool blnOk = true;

                    while (blnOk)
                    {
                        int intRead = ptrInput.Read(arrBuf, 0, 2);
                        if (intRead == 0) { break; }
                        if (intRead != 2) { blnOk = false; break; }

                        ushort intAddr = BitConverter.ToUInt16(arrBuf, 0);

                        if (intAddr < objRom_a.lngROMSize)
                        {
                            ptrOutput.WriteByte(objRom_a.ptrROMData[intAddr]);
                        }
                        else
                        {
                            blnOk = false;
                        }
                    }

                    if (blnOk) { arrResult = ptrOutput.ToArray(); }
                }
            }
            catch { }

            return arrResult;
        }
    }

    // -------------------------------------------------------------------------
    // ZOSCIIRom
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents a loaded ZOSCII ROM — the key material for ZOSCII and UNSIGNAL
    /// encoding operations. A ROM is 128KB of high-entropy binary data.
    /// ROMs are loaded once and reused; dispose when done.
    /// </summary>
    public class ZOSCIIRom : IDisposable
    {
        // --- Fields ---

        private bool m_blnDisposed = false;
        private bool m_blnLoaded = false;
        private RomData m_objRomData;

        // --- Constructor ---

        private ZOSCIIRom() { }

        // --- Factories ---

        /// <summary>
        /// Load a ROM from a Base64-encoded string of raw ROM bytes.
        /// </summary>
        public static ZOSCIIRom FromBase64(string strBase64_a)
        {
            ZOSCIIRom objRom = new ZOSCIIRom();

            try
            {
                byte[] arrData = Convert.FromBase64String(strBase64_a);
                objRom.m_objRomData = clsZOSCII.LoadRomFromBytes(arrData);
                objRom.m_blnLoaded = true;
            }
            catch { }

            return objRom;
        }

        /// <summary>
        /// Load a ROM from a raw byte array.
        /// </summary>
        public static ZOSCIIRom FromBytes(byte[] arrData_a)
        {
            ZOSCIIRom objRom = new ZOSCIIRom();

            try
            {
                objRom.m_objRomData = clsZOSCII.LoadRomFromBytes(arrData_a);
                objRom.m_blnLoaded = true;
            }
            catch { }

            return objRom;
        }

        /// <summary>
        /// Load a ROM from a raw .rom file (unencoded 128KB bytes).
        /// For .rom.sig files (UNSIGNAL-protected), decode with UDecode.Bytes first,
        /// then pass the raw bytes to FromBytes.
        /// </summary>
        public static ZOSCIIRom FromFile(string strPath_a)
        {
            ZOSCIIRom objRom = new ZOSCIIRom();

            try
            {
                byte[] arrData = System.IO.File.ReadAllBytes(strPath_a);
                objRom.m_objRomData = clsZOSCII.LoadRomFromBytes(arrData);
                objRom.m_blnLoaded = true;
            }
            catch { }

            return objRom;
        }

        // --- Properties ---

        /// <summary>True if the ROM loaded successfully and is ready for use.</summary>
        public bool IsLoaded
        {
            get { return m_blnLoaded; }
        }

        /// <summary>ROM size in bytes. 0 if not loaded.</summary>
        public long Size
        {
            get
            {
                long lngResult = 0;
                if (m_blnLoaded) { lngResult = m_objRomData.lngROMSize; }
                return lngResult;
            }
        }

        // --- Internal ---

        internal RomData GetRomData()
        {
            return m_objRomData;
        }

        // --- IDisposable ---

        public void Dispose()
        {
            if (!m_blnDisposed)
            {
                if (m_blnLoaded)
                {
                    clsZOSCII.UnloadRom(ref m_objRomData);
                    m_blnLoaded = false;
                }
                m_blnDisposed = true;
            }
        }
    }

    // -------------------------------------------------------------------------
    // ZEncode
    // -------------------------------------------------------------------------

    /// <summary>
    /// ZOSCII encoding operations.
    /// Each plaintext byte is replaced by the 16-bit ROM address of a matching byte,
    /// chosen randomly from all matching addresses. Achieves I(M;A)=0.
    /// Output is exactly input.Length * 2 bytes — no header, no padding.
    /// Compatible with zencode.c.
    /// </summary>
    public static class ZEncode
    {
        /// <summary>
        /// Encode a byte array using a loaded ROM. Returns encoded bytes or null on failure.
        /// </summary>
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                arrResult = clsZOSCII.zencodeByteToByte(ref objRomData, arrInput_a);
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Chain-encode a byte array through multiple ROMs in sequence.
        /// Returns encoded bytes or null if any stage fails.
        /// </summary>
        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a)
        {
            byte[] arrResult = null;

            try
            {
                arrResult = arrInput_a;
                int intI = 0;

                while (intI < arrRoms_a.Length && arrResult != null)
                {
                    arrResult = Bytes(arrResult, arrRoms_a[intI]);
                    intI++;
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Encode a file to an output file using a loaded ROM. Returns true on success.
        /// </summary>
        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                blnResult = clsZOSCII.zencodeFileToFile(ref objRomData, strInputPath_a, strOutputPath_a);
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Encode a UTF-8 string using a loaded ROM. Returns encoded bytes or null on failure.
        /// </summary>
        public static byte[] String(string strInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;

            try
            {
                byte[] arrInput = Encoding.UTF8.GetBytes(strInput_a);
                arrResult = Bytes(arrInput, objRom_a);
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Encode a byte array and return as Base64 string. Returns empty string on failure.
        /// </summary>
        public static string ToBase64(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";

            try
            {
                byte[] arrEncoded = Bytes(arrInput_a, objRom_a);
                if (arrEncoded != null)
                {
                    strResult = Convert.ToBase64String(arrEncoded);
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Chain-encode a file through multiple ROMs in sequence using temp files — no full file load into RAM.
        /// Returns true on success. Temp files are securely deleted on completion.
        /// </summary>
        public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
        {
            bool blnResult = false;

            try
            {
                if (arrRoms_a == null || arrRoms_a.Length == 0) { return false; }

                string strCurrent = strInputPath_a;
                string[] arrTemps = new string[arrRoms_a.Length - 1];
                int intI = 0;

                for (intI = 0; intI < arrTemps.Length; intI++)
                {
                    arrTemps[intI] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                }

                blnResult = true;

                for (intI = 0; intI < arrRoms_a.Length && blnResult; intI++)
                {
                    string strNext = (intI == arrRoms_a.Length - 1) ? strOutputPath_a : arrTemps[intI];
                    blnResult = File(strCurrent, strNext, arrRoms_a[intI]);
                    strCurrent = strNext;
                }

                for (intI = 0; intI < arrTemps.Length; intI++)
                {
                    if (System.IO.File.Exists(arrTemps[intI])) { SecureDelete.File(arrTemps[intI]); }
                }
            }
            catch { blnResult = false; }

            return blnResult;
        }
    }

    // -------------------------------------------------------------------------
    // ZDecode
    // -------------------------------------------------------------------------

    /// <summary>
    /// ZOSCII decoding operations.
    /// Each 16-bit address in the encoded data is looked up in the ROM to recover the plaintext byte.
    /// Compatible with zdecode.c.
    /// </summary>
    public static class ZDecode
    {
        /// <summary>
        /// Decode a ZOSCII-encoded byte array using a loaded ROM. Returns decoded bytes or null on failure.
        /// </summary>
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                arrResult = clsZOSCII.zdecodeByteToByte(objRomData, arrInput_a);
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Chain-decode a byte array through multiple ROMs in reverse sequence.
        /// Returns decoded bytes or null if any stage fails.
        /// </summary>
        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a)
        {
            byte[] arrResult = null;

            try
            {
                arrResult = arrInput_a;
                int intI = arrRoms_a.Length - 1;

                while (intI >= 0 && arrResult != null)
                {
                    arrResult = Bytes(arrResult, arrRoms_a[intI]);
                    intI--;
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Decode a Base64-encoded ZOSCII payload. Returns decoded bytes or null on failure.
        /// </summary>
        public static byte[] FromBase64(string strBase64_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;

            try
            {
                byte[] arrEncoded = Convert.FromBase64String(strBase64_a);
                arrResult = Bytes(arrEncoded, objRom_a);
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Decode a ZOSCII-encoded file to an output file using a loaded ROM. Returns true on success.
        /// </summary>
        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                blnResult = clsZOSCII.zdecodeFileToFile(objRomData, strInputPath_a, strOutputPath_a);
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Decode a ZOSCII-encoded file and return contents as a UTF-8 string.
        /// Returns empty string on failure.
        /// </summary>
        public static string FileToString(string strInputPath_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                byte[] arrDecoded = clsZOSCII.zdecodeFileToByte(objRomData, strInputPath_a);
                if (arrDecoded != null)
                {
                    strResult = Encoding.UTF8.GetString(arrDecoded);
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Decode a ZOSCII-encoded byte array and return as a UTF-8 string.
        /// Returns empty string on failure.
        /// </summary>
        public static string ToString(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";

            try
            {
                byte[] arrDecoded = Bytes(arrInput_a, objRom_a);
                if (arrDecoded != null)
                {
                    strResult = Encoding.UTF8.GetString(arrDecoded);
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Chain-decode a file through multiple ROMs in reverse sequence using temp files — no full file load into RAM.
        /// Returns true on success. Temp files are securely deleted on completion.
        /// </summary>
        public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
        {
            bool blnResult = false;

            try
            {
                if (arrRoms_a == null || arrRoms_a.Length == 0) { return false; }

                string strCurrent = strInputPath_a;
                string[] arrTemps = new string[arrRoms_a.Length - 1];
                int intI = 0;

                for (intI = 0; intI < arrTemps.Length; intI++)
                {
                    arrTemps[intI] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                }

                blnResult = true;

                for (intI = arrRoms_a.Length - 1; intI >= 0 && blnResult; intI--)
                {
                    string strNext = (intI == 0) ? strOutputPath_a : arrTemps[intI - 1];
                    blnResult = File(strCurrent, strNext, arrRoms_a[intI]);
                    strCurrent = strNext;
                }

                for (intI = 0; intI < arrTemps.Length; intI++)
                {
                    if (System.IO.File.Exists(arrTemps[intI])) { SecureDelete.File(arrTemps[intI]); }
                }
            }
            catch { blnResult = false; }

            return blnResult;
        }
    }

    // -------------------------------------------------------------------------
    // ZVerify
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verify a ZOSCII-encoded file or byte array against its plaintext source.
    /// Equivalent to the -z mode of uverify.c.
    /// </summary>
    public static class ZVerify
    {
        /// <summary>
        /// Verify a ZOSCII-encoded file against its plaintext file. Returns true if match.
        /// </summary>
        public static bool File(string strEncodedPath_a, string strPlainPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;

            try
            {
                RomData objRomData = objRom_a.GetRomData();

                using (FileStream ptrEncoded = new FileStream(strEncodedPath_a, FileMode.Open, FileAccess.Read))
                using (FileStream ptrPlain = new FileStream(strPlainPath_a, FileMode.Open, FileAccess.Read))
                {
                    long lngSlots = ptrEncoded.Length / 2;
                    byte[] arrBuf = new byte[2];
                    bool blnMatch = true;
                    bool blnDone = false;

                    for (long lngI = 0; lngI < lngSlots && !blnDone; lngI++)
                    {
                        if (ptrEncoded.Read(arrBuf, 0, 2) != 2)
                        {
                            blnMatch = false;
                            blnDone = true;
                        }
                        else
                        {
                            ushort intAddr = BitConverter.ToUInt16(arrBuf, 0);
                            int intPlainByte = ptrPlain.ReadByte();

                            if (intPlainByte == -1 || intAddr >= objRomData.lngROMSize)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                            else if (objRomData.ptrROMData[intAddr] != (byte)intPlainByte)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                        }
                    }

                    if (blnMatch && ptrPlain.ReadByte() != -1)
                    {
                        blnMatch = false;
                    }

                    blnResult = blnMatch;
                }
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Verify ZOSCII-encoded bytes against plaintext bytes. Returns true if match.
        /// </summary>
        public static bool Bytes(byte[] arrEncoded_a, byte[] arrPlain_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;

            try
            {
                RomData objRomData = objRom_a.GetRomData();
                long lngSlots = arrEncoded_a.Length / 2;
                bool blnMatch = (arrPlain_a.Length == lngSlots);
                bool blnDone = !blnMatch;

                for (long lngI = 0; lngI < lngSlots && !blnDone; lngI++)
                {
                    ushort intAddr = BitConverter.ToUInt16(arrEncoded_a, (int)(lngI * 2));

                    if (intAddr >= objRomData.lngROMSize || objRomData.ptrROMData[intAddr] != arrPlain_a[lngI])
                    {
                        blnMatch = false;
                        blnDone = true;
                    }
                }

                blnResult = blnMatch;
            }
            catch { }

            return blnResult;
        }
    }
}