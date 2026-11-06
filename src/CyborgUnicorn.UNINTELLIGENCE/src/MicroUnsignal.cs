// CyborgUnicorn.ZOSCII - microUNSIGNAL and microBRICS, 8-bit / nibble configurations
// MUEncode, MUDecode (single flat stream), MBEncode, MBDecode (multi-block)
// The ROM is a nibble ROM: up to 512 nibbles (hex characters), as MicroZOSCII uses, but with the
// micro regime (t1 = 261, effective size 256, sliding window per rule) and UNSIGNAL / BRICS headers.
// Each plaintext byte becomes two 1-byte addresses: low nibble then high nibble (as MicroZOSCII).
// Header and block values are composed as (ROM[high] << 4) | ROM[low].
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.2

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>A nibble ROM for the micro encodings: 1..512 nibble values (0-15).</summary>
    public sealed class MicroRom
    {
        public const int MAX_NIBBLES = 512;

        private byte[] m_arrNibbles = null;
        private List<int>[] m_arrPositions = null;

        public bool IsLoaded { get { return m_arrNibbles != null && m_arrNibbles.Length > 0; } }
        public int Length { get { return m_arrNibbles == null ? 0 : m_arrNibbles.Length; } }
        internal byte[] Nibbles { get { return m_arrNibbles; } }
        internal List<int>[] Positions { get { return m_arrPositions; } }

        /// <summary>From a hex string (0-9, A-F / a-f), up to 512 characters; longer input is truncated.</summary>
        public static MicroRom FromHex(string strHex_a)
        {
            MicroRom objResult = new MicroRom();
            try
            {
                if (strHex_a != null && strHex_a.Length > 0)
                {
                    int intLen = Math.Min(strHex_a.Length, MAX_NIBBLES);
                    byte[] arrNibbles = new byte[intLen];
                    bool blnOk = true;
                    for (int intI = 0; intI < intLen && blnOk; intI++)
                    {
                        int intV = hexValue(strHex_a[intI]);
                        if (intV < 0) { blnOk = false; } else { arrNibbles[intI] = (byte)intV; }
                    }
                    if (blnOk) { objResult.load(arrNibbles); }
                }
            }
            catch { }
            return objResult;
        }

        /// <summary>From raw bytes (any file, e.g. a JPEG): each byte supplies two nibbles, high then low. Truncated to intMaxNibbles_a (default 512).</summary>
        public static MicroRom FromBytes(byte[] arrBytes_a, int intMaxNibbles_a = MAX_NIBBLES)
        {
            MicroRom objResult = new MicroRom();
            try
            {
                if (arrBytes_a != null && arrBytes_a.Length > 0)
                {
                    int intMax = Math.Min(Math.Max(1, intMaxNibbles_a), MAX_NIBBLES);
                    int intLen = Math.Min(arrBytes_a.Length * 2, intMax);
                    byte[] arrNibbles = new byte[intLen];
                    for (int intI = 0; intI < intLen; intI++)
                    {
                        byte by = arrBytes_a[intI / 2];
                        arrNibbles[intI] = (intI % 2 == 0) ? (byte)((by >> 4) & 0x0F) : (byte)(by & 0x0F);
                    }
                    objResult.load(arrNibbles);
                }
            }
            catch { }
            return objResult;
        }

        /// <summary>From 1..6 hand-typed base-62 chunks of 54 characters (80 nibbles each), in order.</summary>
        public static MicroRom FromBase62(string[] arrChunks_a)
        {
            MicroRom objResult = new MicroRom();
            try
            {
                StringBuilder objSB = new StringBuilder();
                bool blnOk = arrChunks_a != null && arrChunks_a.Length > 0;
                for (int intI = 0; blnOk && intI < arrChunks_a.Length; intI++)
                {
                    string strHex = MicroZOSCII.Base62ChunkToHex(arrChunks_a[intI]);
                    if (strHex == null) { blnOk = false; } else { objSB.Append(strHex); }
                }
                if (blnOk) { objResult = FromHex(objSB.ToString()); }
            }
            catch { }
            return objResult;
        }

        public string ToHex()
        {
            StringBuilder objSB = new StringBuilder();
            if (m_arrNibbles != null)
            {
                for (int intI = 0; intI < m_arrNibbles.Length; intI++) { objSB.Append("0123456789ABCDEF"[m_arrNibbles[intI]]); }
            }
            return objSB.ToString();
        }

        private void load(byte[] arrNibbles_a)
        {
            m_arrNibbles = arrNibbles_a;
            m_arrPositions = new List<int>[16];
            for (int intI = 0; intI < 16; intI++) { m_arrPositions[intI] = new List<int>(); }
            for (int intI = 0; intI < arrNibbles_a.Length; intI++) { m_arrPositions[arrNibbles_a[intI]].Add(intI); }
        }

        private static int hexValue(char chC_a)
        {
            int intResult = -1;
            if (chC_a >= '0' && chC_a <= '9') { intResult = chC_a - '0'; }
            else if (chC_a >= 'A' && chC_a <= 'F') { intResult = chC_a - 'A' + 10; }
            else if (chC_a >= 'a' && chC_a <= 'f') { intResult = chC_a - 'a' + 10; }
            return intResult;
        }
    }

    internal static class clsMicro
    {
        private const int T1 = 261;          // 256 + 2% of 256
        private const int BLOCK_MIN = 64;    // 8-bit block size: up to 255 elements
        private const int BLOCK_MAX = 255;

        private static int pct2(int intX_a) { return (intX_a * 2) / 100; }

        public static int effectiveSize(int intX_a)
        {
            int intResult = 0;
            if (intX_a < T1) { intResult = intX_a - pct2(intX_a); } else { intResult = 256; }
            if (intResult < 1) { intResult = intX_a; }
            return intResult;
        }

        public static int slidingWindow(int intX_a)
        {
            int intResult = 0;
            if (intX_a < T1) { intResult = pct2(intX_a); }
            else if (intX_a < 512) { intResult = 512 - intX_a - pct2(intX_a); }
            else { intResult = 256; }
            if (intResult < 0) { intResult = 0; }
            return intResult;
        }

        public static int foldOffset(int intRaw_a, int intX_a)
        {
            int intResult = 0;
            int intMax = slidingWindow(intX_a);
            if (intMax > 0) { intResult = intRaw_a % (intMax + 1); }
            return intResult;
        }

        public static Random createRandom(MicroRom objRom_a)
        {
            uint intHash = 0;
            byte[] arrN = objRom_a.Nibbles;
            for (int intI = 0; intI < arrN.Length; intI++) { intHash = (intHash * 33) + arrN[intI]; }
            return new Random((int)(intHash ^ (uint)Environment.TickCount ^ (uint)Guid.NewGuid().GetHashCode()));
        }

        // global address (header): any position holding the nibble, in 0..255
        public static bool globalAddress(MicroRom objRom_a, int intNibble_a, Random ptrRand_a, out byte byAddr_a)
        {
            bool blnResult = false;
            byAddr_a = 0;
            List<int> arrPos = objRom_a.Positions[intNibble_a & 0x0F];
            List<int> arrUsable = new List<int>();
            for (int intI = 0; intI < arrPos.Count; intI++) { if (arrPos[intI] < 256) { arrUsable.Add(arrPos[intI]); } }
            if (arrUsable.Count > 0) { byAddr_a = (byte)arrUsable[ptrRand_a.Next(arrUsable.Count)]; blnResult = true; }
            return blnResult;
        }

        // window address: a in [0, effective) with ROM[offset + a] == nibble
        public static bool windowAddress(MicroRom objRom_a, int intOffset_a, int intEffective_a, int intNibble_a, Random ptrRand_a, out byte byAddr_a)
        {
            bool blnResult = false;
            byAddr_a = 0;
            List<int> arrPos = objRom_a.Positions[intNibble_a & 0x0F];
            List<int> arrUsable = new List<int>();
            for (int intI = 0; intI < arrPos.Count; intI++)
            {
                int intRel = arrPos[intI] - intOffset_a;
                if (intRel >= 0 && intRel < intEffective_a && intRel < 256) { arrUsable.Add(intRel); }
            }
            if (arrUsable.Count > 0) { byAddr_a = (byte)arrUsable[ptrRand_a.Next(arrUsable.Count)]; blnResult = true; }
            return blnResult;
        }

        public static int deref(MicroRom objRom_a, byte byAddr_a)
        {
            int intResult = -1;
            if (byAddr_a < objRom_a.Length) { intResult = objRom_a.Nibbles[byAddr_a]; }
            return intResult;
        }

        public static int derefWindow(MicroRom objRom_a, int intOffset_a, int intEffective_a, byte byAddr_a)
        {
            int intResult = -1;
            int intPos = intOffset_a + (byAddr_a % intEffective_a);
            if (intPos < objRom_a.Length) { intResult = objRom_a.Nibbles[intPos]; }
            return intResult;
        }

        // ---- microUNSIGNAL: [H1][H2][H3][H4][prefix][2 addresses per byte][suffix] ----

        public static byte[] muEncode(MicroRom objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                int intX = objRom_a.Length;
                int intEff = effectiveSize(intX);
                Random ptrRand = createRandom(objRom_a);
                int intRaw = ptrRand.Next(256);
                int intOffset = foldOffset(intRaw, intX);
                int intPrefixLen = 10 + ptrRand.Next(6);   // nibble values 10..15
                int intSuffixLen = 10 + ptrRand.Next(6);
                byte[] arrH = new byte[4];
                bool blnOk = true;
                blnOk = blnOk && globalAddress(objRom_a, intRaw & 0x0F, ptrRand, out arrH[0]);
                blnOk = blnOk && globalAddress(objRom_a, (intRaw >> 4) & 0x0F, ptrRand, out arrH[1]);
                blnOk = blnOk && globalAddress(objRom_a, intPrefixLen, ptrRand, out arrH[2]);
                blnOk = blnOk && globalAddress(objRom_a, intSuffixLen, ptrRand, out arrH[3]);
                if (blnOk)
                {
                    using (MemoryStream objOut = new MemoryStream())
                    {
                        objOut.Write(arrH, 0, 4);
                        byte[] arrPad = new byte[intPrefixLen];
                        ptrRand.NextBytes(arrPad);
                        objOut.Write(arrPad, 0, arrPad.Length);
                        for (int intI = 0; intI < arrInput_a.Length && blnOk; intI++)
                        {
                            byte byLo = 0;
                            byte byHi = 0;
                            blnOk = windowAddress(objRom_a, intOffset, intEff, arrInput_a[intI] & 0x0F, ptrRand, out byLo)
                                 && windowAddress(objRom_a, intOffset, intEff, (arrInput_a[intI] >> 4) & 0x0F, ptrRand, out byHi);
                            if (blnOk) { objOut.WriteByte(byLo); objOut.WriteByte(byHi); }
                        }
                        if (blnOk)
                        {
                            arrPad = new byte[intSuffixLen];
                            ptrRand.NextBytes(arrPad);
                            objOut.Write(arrPad, 0, arrPad.Length);
                            arrResult = objOut.ToArray();
                        }
                    }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static byte[] muDecode(MicroRom objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                if (arrInput_a.Length >= 4)
                {
                    int intX = objRom_a.Length;
                    int intEff = effectiveSize(intX);
                    int intLo = deref(objRom_a, arrInput_a[0]);
                    int intHi = deref(objRom_a, arrInput_a[1]);
                    int intPrefixLen = deref(objRom_a, arrInput_a[2]);
                    int intSuffixLen = deref(objRom_a, arrInput_a[3]);
                    if (intLo >= 0 && intHi >= 0 && intPrefixLen >= 0 && intSuffixLen >= 0)
                    {
                        int intOffset = foldOffset((intHi << 4) | intLo, intX);
                        int intData = arrInput_a.Length - 4 - intPrefixLen - intSuffixLen;
                        if (intData >= 0 && intData % 2 == 0)
                        {
                            byte[] arrOut = new byte[intData / 2];
                            bool blnOk = true;
                            int intPos = 4 + intPrefixLen;
                            for (int intI = 0; intI < arrOut.Length && blnOk; intI++)
                            {
                                int intL = derefWindow(objRom_a, intOffset, intEff, arrInput_a[intPos]);
                                int intH = derefWindow(objRom_a, intOffset, intEff, arrInput_a[intPos + 1]);
                                if (intL < 0 || intH < 0) { blnOk = false; } else { arrOut[intI] = (byte)((intH << 4) | intL); }
                                intPos += 2;
                            }
                            if (blnOk) { arrResult = arrOut; }
                        }
                    }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        // ---- microBRICS: [FH1..FH6][prefix][ [BH1..BH4][block data] ... ][suffix] ----

        public static byte[] mbEncode(MicroRom objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                int intX = objRom_a.Length;
                int intEff = effectiveSize(intX);
                Random ptrRand = createRandom(objRom_a);

                List<int> arrSizeList = new List<int>();
                int intPos = 0;
                while (intPos < arrInput_a.Length)
                {
                    int intSize = BLOCK_MIN + ptrRand.Next(BLOCK_MAX - BLOCK_MIN + 1);
                    if (intPos + intSize > arrInput_a.Length) { intSize = arrInput_a.Length - intPos; }
                    arrSizeList.Add(intSize);
                    intPos += intSize;
                }
                if (arrSizeList.Count == 0) { arrSizeList.Add(0); }
                int intBlockCount = arrSizeList.Count;
                arrSizeList.Add(BLOCK_MIN + ptrRand.Next(BLOCK_MAX - BLOCK_MIN + 1));
                int[] arrSize = arrSizeList.ToArray();
                int[] arrRawOffset = new int[intBlockCount + 1];
                int[] arrOffset = new int[intBlockCount + 1];
                for (int intB = 0; intB <= intBlockCount; intB++)
                {
                    arrRawOffset[intB] = ptrRand.Next(256);
                    arrOffset[intB] = foldOffset(arrRawOffset[intB], intX);
                }
                int intPrefixLen = 10 + ptrRand.Next(6);
                int intSuffixLen = 10 + ptrRand.Next(6);

                byte[] arrFH = new byte[6];
                bool blnOk = true;
                blnOk = blnOk && globalAddress(objRom_a, arrRawOffset[0] & 0x0F, ptrRand, out arrFH[0]);
                blnOk = blnOk && globalAddress(objRom_a, (arrRawOffset[0] >> 4) & 0x0F, ptrRand, out arrFH[1]);
                blnOk = blnOk && globalAddress(objRom_a, arrSize[0] & 0x0F, ptrRand, out arrFH[2]);
                blnOk = blnOk && globalAddress(objRom_a, (arrSize[0] >> 4) & 0x0F, ptrRand, out arrFH[3]);
                blnOk = blnOk && globalAddress(objRom_a, intPrefixLen, ptrRand, out arrFH[4]);
                blnOk = blnOk && globalAddress(objRom_a, intSuffixLen, ptrRand, out arrFH[5]);

                if (blnOk)
                {
                    using (MemoryStream objOut = new MemoryStream())
                    {
                        objOut.Write(arrFH, 0, 6);
                        byte[] arrPad = new byte[intPrefixLen];
                        ptrRand.NextBytes(arrPad);
                        objOut.Write(arrPad, 0, arrPad.Length);
                        intPos = 0;
                        for (int intB = 0; intB < intBlockCount && blnOk; intB++)
                        {
                            byte[] arrBH = new byte[4];
                            blnOk = blnOk && globalAddress(objRom_a, arrRawOffset[intB + 1] & 0x0F, ptrRand, out arrBH[0]);
                            blnOk = blnOk && globalAddress(objRom_a, (arrRawOffset[intB + 1] >> 4) & 0x0F, ptrRand, out arrBH[1]);
                            blnOk = blnOk && globalAddress(objRom_a, arrSize[intB + 1] & 0x0F, ptrRand, out arrBH[2]);
                            blnOk = blnOk && globalAddress(objRom_a, (arrSize[intB + 1] >> 4) & 0x0F, ptrRand, out arrBH[3]);
                            if (blnOk)
                            {
                                objOut.Write(arrBH, 0, 4);
                                for (int intI = 0; intI < arrSize[intB] && blnOk; intI++)
                                {
                                    byte byLo = 0;
                                    byte byHi = 0;
                                    byte by = arrInput_a[intPos + intI];
                                    blnOk = windowAddress(objRom_a, arrOffset[intB], intEff, by & 0x0F, ptrRand, out byLo)
                                         && windowAddress(objRom_a, arrOffset[intB], intEff, (by >> 4) & 0x0F, ptrRand, out byHi);
                                    if (blnOk) { objOut.WriteByte(byLo); objOut.WriteByte(byHi); }
                                }
                                intPos += arrSize[intB];
                            }
                        }
                        if (blnOk)
                        {
                            arrPad = new byte[intSuffixLen];
                            ptrRand.NextBytes(arrPad);
                            objOut.Write(arrPad, 0, arrPad.Length);
                            arrResult = objOut.ToArray();
                        }
                    }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static byte[] mbDecode(MicroRom objRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                if (arrInput_a.Length >= 6)
                {
                    int intX = objRom_a.Length;
                    int intEff = effectiveSize(intX);
                    int[] arrV = new int[6];
                    bool blnOk = true;
                    for (int intI = 0; intI < 6 && blnOk; intI++) { arrV[intI] = deref(objRom_a, arrInput_a[intI]); blnOk = arrV[intI] >= 0; }
                    if (blnOk)
                    {
                        int intOffset = foldOffset((arrV[1] << 4) | arrV[0], intX);
                        int intSize = (arrV[3] << 4) | arrV[2];
                        int intPrefixLen = arrV[4];
                        int intSuffixLen = arrV[5];
                        int intRemaining = arrInput_a.Length - 6 - intPrefixLen - intSuffixLen;
                        int intPos = 6 + intPrefixLen;
                        using (MemoryStream objOut = new MemoryStream())
                        {
                            while (intRemaining > 0 && blnOk)
                            {
                                int intNeed = 4 + intSize * 2;
                                if (intNeed > intRemaining) { blnOk = false; }
                                else
                                {
                                    int[] arrB = new int[4];
                                    for (int intI = 0; intI < 4 && blnOk; intI++) { arrB[intI] = deref(objRom_a, arrInput_a[intPos + intI]); blnOk = arrB[intI] >= 0; }
                                    intPos += 4;
                                    for (int intI = 0; intI < intSize && blnOk; intI++)
                                    {
                                        int intL = derefWindow(objRom_a, intOffset, intEff, arrInput_a[intPos]);
                                        int intH = derefWindow(objRom_a, intOffset, intEff, arrInput_a[intPos + 1]);
                                        if (intL < 0 || intH < 0) { blnOk = false; } else { objOut.WriteByte((byte)((intH << 4) | intL)); }
                                        intPos += 2;
                                    }
                                    if (blnOk)
                                    {
                                        intRemaining -= intNeed;
                                        intOffset = foldOffset((arrB[1] << 4) | arrB[0], intX);
                                        intSize = (arrB[3] << 4) | arrB[2];
                                    }
                                }
                            }
                            if (blnOk && intRemaining == 0) { arrResult = objOut.ToArray(); }
                        }
                    }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }
    }

    /// <summary>microUNSIGNAL encoding (8-bit / nibble, single flat stream).</summary>
    public static class MUEncode
    {
        public static byte[] Bytes(byte[] arrInput_a, MicroRom objRom_a)
        {
            byte[] arrResult = null;
            if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded) { arrResult = clsMicro.muEncode(objRom_a, arrInput_a); }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, MicroRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            int intI = 0;
            while (arrResult != null && intI < arrRoms_a.Length) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI++; }
            return arrResult;
        }

        public static byte[] String(string strInput_a, MicroRom objRom_a) { return Bytes(Encoding.UTF8.GetBytes(strInput_a), objRom_a); }
    }

    /// <summary>microUNSIGNAL decoding.</summary>
    public static class MUDecode
    {
        public static byte[] Bytes(byte[] arrInput_a, MicroRom objRom_a)
        {
            byte[] arrResult = null;
            if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded) { arrResult = clsMicro.muDecode(objRom_a, arrInput_a); }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, MicroRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            int intI = arrRoms_a.Length - 1;
            while (arrResult != null && intI >= 0) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI--; }
            return arrResult;
        }

        public static string ToString(byte[] arrInput_a, MicroRom objRom_a)
        {
            string strResult = null;
            byte[] arrOut = Bytes(arrInput_a, objRom_a);
            if (arrOut != null) { strResult = Encoding.UTF8.GetString(arrOut); }
            return strResult;
        }
    }

    /// <summary>microBRICS encoding (8-bit / nibble, multi-block).</summary>
    public static class MBEncode
    {
        public static byte[] Bytes(byte[] arrInput_a, MicroRom objRom_a)
        {
            byte[] arrResult = null;
            if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded) { arrResult = clsMicro.mbEncode(objRom_a, arrInput_a); }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, MicroRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            int intI = 0;
            while (arrResult != null && intI < arrRoms_a.Length) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI++; }
            return arrResult;
        }

        public static byte[] String(string strInput_a, MicroRom objRom_a) { return Bytes(Encoding.UTF8.GetBytes(strInput_a), objRom_a); }
    }

    /// <summary>microBRICS decoding.</summary>
    public static class MBDecode
    {
        public static byte[] Bytes(byte[] arrInput_a, MicroRom objRom_a)
        {
            byte[] arrResult = null;
            if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded) { arrResult = clsMicro.mbDecode(objRom_a, arrInput_a); }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, MicroRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            int intI = arrRoms_a.Length - 1;
            while (arrResult != null && intI >= 0) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI--; }
            return arrResult;
        }

        public static string ToString(byte[] arrInput_a, MicroRom objRom_a)
        {
            string strResult = null;
            byte[] arrOut = Bytes(arrInput_a, objRom_a);
            if (arrOut != null) { strResult = Encoding.UTF8.GetString(arrOut); }
            return strResult;
        }
    }
}
