// CyborgUnicorn.ZOSCII - BRICS (Block Reinforced Index Confidentiality Scheme), 16-bit / byte
// BEncode, BDecode
// Multi-block UNSIGNAL: a file header points at block 1, every block header points at the next,
// each block re-offsets the ROM so the same address decodes to a different value in each block.
// A single-block BRICS file is an UNSIGNAL file in structure; this encoder always emits N >= 1 blocks
// of random size (128..512 elements) and random offset.
// Shares the regime 1 window rules with Unsignal.cs (effective size / sliding window / fold on read).
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.2

using System;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    internal class clsBrics
    {
        private const int FILE_HEADER_ADDRS = 6;
        private const int BLOCK_HEADER_ADDRS = 4;
        private const int ADDR_SIZE = 2;
        private const int BLOCK_MIN = 128;
        private const int BLOCK_MAX = 512;

        private const long REGIME_T1 = 66846L;

        private static long Pct2(long lngRomSize_a) { return (lngRomSize_a * 2) / 100; }

        private static long EffectiveSize(long lngRomSize_a)
        {
            long lngResult = 0;
            if (lngRomSize_a < REGIME_T1) { lngResult = lngRomSize_a - Pct2(lngRomSize_a); }
            else { lngResult = 65536L; }
            return lngResult;
        }

        private static long SlidingWindow(long lngRomSize_a)
        {
            long lngResult = 0;
            if (lngRomSize_a < REGIME_T1) { lngResult = Pct2(lngRomSize_a); }
            else if (lngRomSize_a < 131072L) { lngResult = 131072L - lngRomSize_a - Pct2(lngRomSize_a); }
            else { lngResult = 65536L; }
            return lngResult;
        }

        private static ushort FoldOffset(ushort intRaw_a, long lngRomSize_a)
        {
            ushort intResult = 0;
            long lngMax = SlidingWindow(lngRomSize_a);
            if (lngMax > 0) { intResult = (ushort)(intRaw_a % (lngMax + 1)); }
            return intResult;
        }

        private static Random createRandom(ref RomData ptrRom_a)
        {
            uint intHash = 0;
            long lngSpan = Math.Min(ptrRom_a.lngROMSize, 65536L);
            for (long lngI = 0; lngI < lngSpan; lngI++) { intHash = (intHash * 33) + ptrRom_a.ptrROMData[lngI]; }
            return new Random((int)(intHash ^ (uint)Environment.TickCount ^ (uint)Guid.NewGuid().GetHashCode()));
        }

        // random global address whose ROM value is byValue_a (header addresses, as UNSIGNAL does)
        private static bool globalAddress(ref RomData ptrRom_a, byte byValue_a, Random ptrRand_a, out ushort intAddress_a)
        {
            bool blnResult = false;
            intAddress_a = 0;
            if (ptrRom_a.arrLookup[byValue_a].intCount > 0)
            {
                uint intIdx = (uint)ptrRand_a.Next((int)ptrRom_a.arrLookup[byValue_a].intCount);
                uint intPos = ptrRom_a.arrLookup[byValue_a].ptrAddresses[intIdx];
                if (intPos < 65536) { intAddress_a = (ushort)intPos; blnResult = true; }
            }
            return blnResult;
        }

        // positions of every value over the whole loaded ROM (the ROM's own lookup covers only the first 64KB,
        // which is enough for header addresses but not for block windows that start inside the sliding range)
        private static ByteAddresses[] buildFullLookup(ref RomData ptrRom_a)
        {
            ByteAddresses[] arrLookup = new ByteAddresses[256];
            uint[] arrCounts = new uint[256];
            for (long lngI = 0; lngI < ptrRom_a.lngROMSize; lngI++) { arrCounts[ptrRom_a.ptrROMData[lngI]]++; }
            for (int intI = 0; intI < 256; intI++)
            {
                arrLookup[intI].ptrAddresses = arrCounts[intI] > 0 ? new uint[arrCounts[intI]] : null;
                arrLookup[intI].intCount = 0;
            }
            for (long lngI = 0; lngI < ptrRom_a.lngROMSize; lngI++)
            {
                byte by = ptrRom_a.ptrROMData[lngI];
                arrLookup[by].ptrAddresses[arrLookup[by].intCount++] = (uint)lngI;
            }
            return arrLookup;
        }

        // random address a in [0, effective) with ROM[offset + a] == byValue_a.
        // Positions are ascending, so the window is a contiguous slice found by binary search.
        private static bool windowAddress(ByteAddresses[] arrFull_a, ushort intOffset_a, long lngEffective_a, byte byValue_a, Random ptrRand_a, out ushort intAddress_a)
        {
            bool blnResult = false;
            intAddress_a = 0;
            ByteAddresses objList = arrFull_a[byValue_a];
            if (objList.intCount > 0)
            {
                long lngLow = intOffset_a;
                long lngHigh = intOffset_a + lngEffective_a; // exclusive
                int intFirst = lowerBound(objList.ptrAddresses, (int)objList.intCount, lngLow);
                int intLast = lowerBound(objList.ptrAddresses, (int)objList.intCount, lngHigh);
                if (intLast > intFirst)
                {
                    int intPick = intFirst + ptrRand_a.Next(intLast - intFirst);
                    intAddress_a = (ushort)(objList.ptrAddresses[intPick] - intOffset_a);
                    blnResult = true;
                }
            }
            return blnResult;
        }

        private static int lowerBound(uint[] arrSorted_a, int intCount_a, long lngValue_a)
        {
            int intLo = 0;
            int intHi = intCount_a;
            while (intLo < intHi)
            {
                int intMid = (intLo + intHi) / 2;
                if (arrSorted_a[intMid] < lngValue_a) { intLo = intMid + 1; } else { intHi = intMid; }
            }
            return intLo;
        }

        private static void writeAddr(Stream ptrOut_a, ushort intAddr_a)
        {
            ptrOut_a.WriteByte((byte)(intAddr_a & 0xFF));
            ptrOut_a.WriteByte((byte)(intAddr_a >> 8));
        }

        private static bool readAddr(Stream ptrIn_a, out ushort intAddr_a)
        {
            bool blnResult = false;
            intAddr_a = 0;
            int intLo = ptrIn_a.ReadByte();
            int intHi = ptrIn_a.ReadByte();
            if (intLo >= 0 && intHi >= 0) { intAddr_a = (ushort)(intLo | (intHi << 8)); blnResult = true; }
            return blnResult;
        }

        // --- encode ---

        public static bool fbencodeStreamToStream(ref RomData ptrRom_a, Stream ptrInput_a, Stream ptrOutput_a)
        {
            bool blnSuccess = false;
            long lngRomSize = ptrRom_a.lngROMSize;
            long lngEffective = EffectiveSize(lngRomSize);
            Random ptrRand = createRandom(ref ptrRom_a);
            ByteAddresses[] arrFull = buildFullLookup(ref ptrRom_a);

            byte[] arrPlain = null;
            using (MemoryStream objMem = new MemoryStream())
            {
                ptrInput_a.CopyTo(objMem);
                arrPlain = objMem.ToArray();
            }

            // block plan: sizes 128..512 drawn once until the input is covered (last block takes the remainder),
            // plus one extra size/offset pair for the final block's "next" noise; random raw offsets folded into the window
            System.Collections.Generic.List<ushort> arrSizeList = new System.Collections.Generic.List<ushort>();
            long lngPos = 0;
            while (lngPos < arrPlain.Length)
            {
                int intSize = BLOCK_MIN + ptrRand.Next(BLOCK_MAX - BLOCK_MIN + 1);
                if (lngPos + intSize > arrPlain.Length) { intSize = (int)(arrPlain.Length - lngPos); }
                arrSizeList.Add((ushort)intSize);
                lngPos += intSize;
            }
            if (arrSizeList.Count == 0) { arrSizeList.Add(0); }
            int intBlockCount = arrSizeList.Count;
            arrSizeList.Add((ushort)(BLOCK_MIN + ptrRand.Next(BLOCK_MAX - BLOCK_MIN + 1)));
            ushort[] arrSize = arrSizeList.ToArray();
            ushort[] arrOffset = new ushort[intBlockCount + 1];
            for (int intB = 0; intB <= intBlockCount; intB++) { arrOffset[intB] = FoldOffset((ushort)ptrRand.Next(65536), lngRomSize); }

            byte byPrefixLen = (byte)(ptrRand.Next(246) + 10);
            byte bySuffixLen = (byte)(ptrRand.Next(246) + 10);

            ushort[] arrFH = new ushort[FILE_HEADER_ADDRS];
            bool blnOk = true;
            blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrOffset[0] & 0xFF), ptrRand, out arrFH[0]);
            blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrOffset[0] >> 8), ptrRand, out arrFH[1]);
            blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrSize[0] & 0xFF), ptrRand, out arrFH[2]);
            blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrSize[0] >> 8), ptrRand, out arrFH[3]);
            blnOk = blnOk && globalAddress(ref ptrRom_a, byPrefixLen, ptrRand, out arrFH[4]);
            blnOk = blnOk && globalAddress(ref ptrRom_a, bySuffixLen, ptrRand, out arrFH[5]);

            if (blnOk)
            {
                for (int intI = 0; intI < FILE_HEADER_ADDRS; intI++) { writeAddr(ptrOutput_a, arrFH[intI]); }
                byte[] arrPad = new byte[byPrefixLen];
                ptrRand.NextBytes(arrPad);
                ptrOutput_a.Write(arrPad, 0, arrPad.Length);

                lngPos = 0;
                for (int intB = 0; intB < intBlockCount && blnOk; intB++)
                {
                    ushort[] arrBH = new ushort[BLOCK_HEADER_ADDRS];
                    blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrOffset[intB + 1] & 0xFF), ptrRand, out arrBH[0]);
                    blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrOffset[intB + 1] >> 8), ptrRand, out arrBH[1]);
                    blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrSize[intB + 1] & 0xFF), ptrRand, out arrBH[2]);
                    blnOk = blnOk && globalAddress(ref ptrRom_a, (byte)(arrSize[intB + 1] >> 8), ptrRand, out arrBH[3]);
                    if (blnOk)
                    {
                        for (int intI = 0; intI < BLOCK_HEADER_ADDRS; intI++) { writeAddr(ptrOutput_a, arrBH[intI]); }
                        for (int intI = 0; intI < arrSize[intB] && blnOk; intI++)
                        {
                            ushort intAddr = 0;
                            blnOk = windowAddress(arrFull, arrOffset[intB], lngEffective, arrPlain[lngPos + intI], ptrRand, out intAddr);
                            if (blnOk) { writeAddr(ptrOutput_a, intAddr); }
                        }
                        lngPos += arrSize[intB];
                    }
                }

                if (blnOk)
                {
                    arrPad = new byte[bySuffixLen];
                    ptrRand.NextBytes(arrPad);
                    ptrOutput_a.Write(arrPad, 0, arrPad.Length);
                    blnSuccess = true;
                }
            }

            return blnSuccess;
        }

        // --- decode ---

        public static bool fbdecodeStreamToStream(RomData ptrRom_a, Stream ptrInput_a, Stream ptrOutput_a)
        {
            bool blnSuccess = false;
            long lngRomSize = ptrRom_a.lngROMSize;
            long lngEffective = EffectiveSize(lngRomSize);
            long lngTotal = ptrInput_a.Length;
            ushort[] arrFH = new ushort[FILE_HEADER_ADDRS];
            bool blnOk = true;

            for (int intI = 0; intI < FILE_HEADER_ADDRS && blnOk; intI++)
            {
                blnOk = readAddr(ptrInput_a, out arrFH[intI]) && arrFH[intI] < lngRomSize;
            }

            if (blnOk)
            {
                ushort intOffset = FoldOffset((ushort)(ptrRom_a.ptrROMData[arrFH[0]] | (ptrRom_a.ptrROMData[arrFH[1]] << 8)), lngRomSize);
                ushort intSize = (ushort)(ptrRom_a.ptrROMData[arrFH[2]] | (ptrRom_a.ptrROMData[arrFH[3]] << 8));
                byte byPrefixLen = ptrRom_a.ptrROMData[arrFH[4]];
                byte bySuffixLen = ptrRom_a.ptrROMData[arrFH[5]];
                long lngRemaining = lngTotal - (FILE_HEADER_ADDRS * ADDR_SIZE) - byPrefixLen - bySuffixLen;

                for (int intI = 0; intI < byPrefixLen && blnOk; intI++) { blnOk = ptrInput_a.ReadByte() >= 0; }

                if (blnOk && lngRemaining >= 0)
                {
                    while (lngRemaining > 0 && blnOk)
                    {
                        long lngNeed = (BLOCK_HEADER_ADDRS + intSize) * ADDR_SIZE;
                        if (lngNeed > lngRemaining)
                        {
                            blnOk = false;
                        }
                        else
                        {
                            ushort[] arrBH = new ushort[BLOCK_HEADER_ADDRS];
                            for (int intI = 0; intI < BLOCK_HEADER_ADDRS && blnOk; intI++)
                            {
                                blnOk = readAddr(ptrInput_a, out arrBH[intI]) && arrBH[intI] < lngRomSize;
                            }
                            if (blnOk)
                            {
                                ushort intNextOffset = FoldOffset((ushort)(ptrRom_a.ptrROMData[arrBH[0]] | (ptrRom_a.ptrROMData[arrBH[1]] << 8)), lngRomSize);
                                ushort intNextSize = (ushort)(ptrRom_a.ptrROMData[arrBH[2]] | (ptrRom_a.ptrROMData[arrBH[3]] << 8));
                                for (int intI = 0; intI < intSize && blnOk; intI++)
                                {
                                    ushort intAddr = 0;
                                    blnOk = readAddr(ptrInput_a, out intAddr);
                                    if (blnOk)
                                    {
                                        long lngFolded = intAddr % lngEffective;
                                        long lngPos = intOffset + lngFolded;
                                        if (lngPos < lngRomSize) { ptrOutput_a.WriteByte(ptrRom_a.ptrROMData[lngPos]); } else { blnOk = false; }
                                    }
                                }
                                lngRemaining -= lngNeed;
                                intOffset = intNextOffset;
                                intSize = intNextSize;
                            }
                        }
                    }
                    blnSuccess = blnOk && lngRemaining == 0;
                }
            }

            return blnSuccess;
        }

        public static byte[] fbencodeByteToByte(ref RomData ptrRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                using (MemoryStream ptrIn = new MemoryStream(arrInput_a))
                using (MemoryStream ptrOut = new MemoryStream())
                {
                    if (fbencodeStreamToStream(ref ptrRom_a, ptrIn, ptrOut)) { arrResult = ptrOut.ToArray(); }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static byte[] fbdecodeByteToByte(RomData ptrRom_a, byte[] arrInput_a)
        {
            byte[] arrResult = null;
            try
            {
                using (MemoryStream ptrIn = new MemoryStream(arrInput_a))
                using (MemoryStream ptrOut = new MemoryStream())
                {
                    if (fbdecodeStreamToStream(ptrRom_a, ptrIn, ptrOut)) { arrResult = ptrOut.ToArray(); }
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }
    }

    // -------------------------------------------------------------------------
    // BEncode / BDecode
    // -------------------------------------------------------------------------

    /// <summary>BRICS encoding operations (16-bit / byte). Same call shapes as UEncode.</summary>
    public static class BEncode
    {
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try
            {
                if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded)
                {
                    RomData objRom = objRom_a.GetRomData();
                    arrResult = clsBrics.fbencodeByteToByte(ref objRom, arrInput_a);
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            try
            {
                int intI = 0;
                while (intI < arrRoms_a.Length && arrResult != null) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI++; }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;
            try
            {
                byte[] arrOut = Bytes(System.IO.File.ReadAllBytes(strInputPath_a), objRom_a);
                if (arrOut != null) { System.IO.File.WriteAllBytes(strOutputPath_a, arrOut); blnResult = true; }
            }
            catch { blnResult = false; }
            return blnResult;
        }

        public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
        {
            bool blnResult = false;
            try
            {
                byte[] arrOut = Chain(System.IO.File.ReadAllBytes(strInputPath_a), arrRoms_a);
                if (arrOut != null) { System.IO.File.WriteAllBytes(strOutputPath_a, arrOut); blnResult = true; }
            }
            catch { blnResult = false; }
            return blnResult;
        }

        public static byte[] String(string strInput_a, ZOSCIIRom objRom_a) { return Bytes(Encoding.UTF8.GetBytes(strInput_a), objRom_a); }

        public static string ToBase64(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = null;
            byte[] arrOut = Bytes(arrInput_a, objRom_a);
            if (arrOut != null) { strResult = Convert.ToBase64String(arrOut); }
            return strResult;
        }
    }

    /// <summary>BRICS decoding operations (16-bit / byte). Same call shapes as UDecode.</summary>
    public static class BDecode
    {
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try
            {
                if (arrInput_a != null && objRom_a != null && objRom_a.IsLoaded)
                {
                    arrResult = clsBrics.fbdecodeByteToByte(objRom_a.GetRomData(), arrInput_a);
                }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a)
        {
            byte[] arrResult = arrInput_a;
            try
            {
                int intI = arrRoms_a.Length - 1;
                while (intI >= 0 && arrResult != null) { arrResult = Bytes(arrResult, arrRoms_a[intI]); intI--; }
            }
            catch { arrResult = null; }
            return arrResult;
        }

        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;
            try
            {
                byte[] arrOut = Bytes(System.IO.File.ReadAllBytes(strInputPath_a), objRom_a);
                if (arrOut != null) { System.IO.File.WriteAllBytes(strOutputPath_a, arrOut); blnResult = true; }
            }
            catch { blnResult = false; }
            return blnResult;
        }

        public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
        {
            bool blnResult = false;
            try
            {
                byte[] arrOut = Chain(System.IO.File.ReadAllBytes(strInputPath_a), arrRoms_a);
                if (arrOut != null) { System.IO.File.WriteAllBytes(strOutputPath_a, arrOut); blnResult = true; }
            }
            catch { blnResult = false; }
            return blnResult;
        }

        public static string ToString(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = null;
            byte[] arrOut = Bytes(arrInput_a, objRom_a);
            if (arrOut != null) { strResult = Encoding.UTF8.GetString(arrOut); }
            return strResult;
        }

        public static byte[] FromBase64(string strInput_a, ZOSCIIRom objRom_a) { return Bytes(Convert.FromBase64String(strInput_a), objRom_a); }
    }
}
