// CyborgUnicorn.ZOSCII - UNSIGNAL
// clsUnsignal engine, UEncode, UDecode, UVerify
// UNSIGNAL builds upon ZOSCII — this file depends on ZOSCII.cs.
// Compatible with uencode.c / udecode.c / uverify.c
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    // Cyborg UNSIGNAL Protocol v20260303
    // (c) 2026 Cyborg Unicorn Pty Ltd.
    // This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
    // ZOSCII core logic remains under MIT License.
    //
    // RomData, ByteAddresses, clsZOSCII are defined in ZOSCII.cs.
    // clsUnsignal adds the UNSIGNAL header/offset/prefix/suffix layer on top.

    internal class clsUnsignal
    {
        private const int UNSIGNAL_OFFSET_LIMIT_PCT = 2;
        private const int HEADER_SIZE = 8;

        // --- Helpers ---

        private static Random createRandomSeed(ref RomData ptrRom_a, ushort intOffset_a)
        {
            uint intWindowHash = 0;
            long lngWindowSize = ptrRom_a.lngROMSize - intOffset_a;
            if (lngWindowSize > 65536L) { lngWindowSize = 65536L; }

            for (long lngI = 0; lngI < lngWindowSize; lngI++)
            {
                intWindowHash = (intWindowHash * 33) + ptrRom_a.ptrROMData[intOffset_a + lngI];
            }

            return new Random((int)(intWindowHash ^ (uint)Environment.TickCount));
        }

        private static ushort FindOffset(byte byLow_a, byte byHigh_a, long lngROMSize_a)
        {
            ushort intResult = 0;
            ushort intRaw = (ushort)(byLow_a | (byHigh_a << 8));

            if (lngROMSize_a >= 131072L)
            {
                intResult = intRaw;
            }
            else
            {
                long lngMax = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
                if (lngMax == 0)
                {
                    intResult = 0;
                }
                else
                {
                    intResult = (ushort)(intRaw % (lngMax + 1));
                }
            }

            return intResult;
        }

        private static uint FindROMAddress(ByteAddresses[] ptrLookup_a, byte byTarget_a, Random ptrRand_a)
        {
            uint intResult = 0;

            if (ptrLookup_a[byTarget_a].intCount > 0)
            {
                uint intRandomIdx = (uint)(ptrRand_a.Next((int)ptrLookup_a[byTarget_a].intCount));
                intResult = ptrLookup_a[byTarget_a].ptrAddresses[intRandomIdx];
            }

            return intResult;
        }

        // --- UNSIGNAL encode - file to file ---

        public static bool fuencode(ref RomData ptrRom_a, string strInputFile_a, string strOutputFile_a)
        {
            bool blnSuccess = false;
            ByteAddresses[] arrOffsetLookup = new ByteAddresses[256];
            uint[] arrOffsetCounts = new uint[256];
            byte byOffsetLow = 0;
            byte byOffsetHigh = 0;
            byte byPrefixLen = 0;
            byte bySuffixLen = 0;
            ushort intROMOffset = 0;
            ushort intH1 = 0;
            ushort intH2 = 0;
            ushort intH3 = 0;
            ushort intH4 = 0;
            byte[] ptrPrefix = null;
            byte[] ptrSuffix = null;
            long lngEffectiveSize = 0;
            long lngI = 0;
            int intI = 0;
            bool blnLookupValid = true;
            FileStream ptrInput = null;
            FileStream ptrOutput = null;
            int intCh = 0;

            for (intI = 0; intI < 256; intI++)
            {
                arrOffsetLookup[intI].ptrAddresses = null;
                arrOffsetLookup[intI].intCount = 0;
            }

            Random ptrRandHeader = createRandomSeed(ref ptrRom_a, 0);

            byOffsetLow  = (byte)ptrRandHeader.Next(256);
            byOffsetHigh = (byte)ptrRandHeader.Next(256);
            byPrefixLen  = (byte)(ptrRandHeader.Next(246) + 10);
            bySuffixLen  = (byte)(ptrRandHeader.Next(246) + 10);

            if (ptrRom_a.arrLookup[byOffsetLow].intCount  > 0 &&
                ptrRom_a.arrLookup[byOffsetHigh].intCount > 0 &&
                ptrRom_a.arrLookup[byPrefixLen].intCount  > 0 &&
                ptrRom_a.arrLookup[bySuffixLen].intCount  > 0)
            {
                intROMOffset = FindOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
                Random ptrRandEncode = createRandomSeed(ref ptrRom_a, intROMOffset);

                intH1 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetLow,  ptrRandEncode);
                intH2 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetHigh, ptrRandEncode);
                intH3 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byPrefixLen,  ptrRandEncode);
                intH4 = (ushort)FindROMAddress(ptrRom_a.arrLookup, bySuffixLen,  ptrRandEncode);

                ptrPrefix = new byte[byPrefixLen];
                ptrSuffix = new byte[bySuffixLen];
                ptrRandEncode.NextBytes(ptrPrefix);
                ptrRandEncode.NextBytes(ptrSuffix);

                lngEffectiveSize = ptrRom_a.lngROMSize - intROMOffset;
                if (lngEffectiveSize > 65536L) { lngEffectiveSize = 65536L; }

                for (lngI = 0; lngI < lngEffectiveSize; lngI++)
                {
                    arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset + lngI]]++;
                }

                for (intI = 0; intI < 256; intI++)
                {
                    arrOffsetLookup[intI].ptrAddresses = null;
                    arrOffsetLookup[intI].intCount = 0;
                }

                for (intI = 0; intI < 256 && blnLookupValid; intI++)
                {
                    if (arrOffsetCounts[intI] > 0)
                    {
                        arrOffsetLookup[intI].ptrAddresses = new uint[arrOffsetCounts[intI]];
                        if (arrOffsetLookup[intI].ptrAddresses != null)
                        {
                            arrOffsetLookup[intI].intCount = 0;
                        }
                        else
                        {
                            blnLookupValid = false;
                        }
                    }
                }

                for (lngI = 0; lngI < lngEffectiveSize && blnLookupValid; lngI++)
                {
                    byte by = ptrRom_a.ptrROMData[intROMOffset + lngI];
                    arrOffsetLookup[by].ptrAddresses[arrOffsetLookup[by].intCount++] = (uint)lngI;
                }

                if (blnLookupValid)
                {
                    try
                    {
                        ptrInput = new FileStream(strInputFile_a, FileMode.Open, FileAccess.Read);

                        try
                        {
                            ptrOutput = new FileStream(strOutputFile_a, FileMode.Create, FileAccess.Write);

                            ptrOutput.Write(BitConverter.GetBytes(intH1), 0, 2);
                            ptrOutput.Write(BitConverter.GetBytes(intH2), 0, 2);
                            ptrOutput.Write(BitConverter.GetBytes(intH3), 0, 2);
                            ptrOutput.Write(BitConverter.GetBytes(intH4), 0, 2);
                            ptrOutput.Write(ptrPrefix, 0, byPrefixLen);

                            intCh = ptrInput.ReadByte();
                            while (intCh != -1)
                            {
                                byte by = (byte)intCh;
                                if (arrOffsetLookup[by].intCount > 0)
                                {
                                    uint intRandomIdx = (uint)ptrRandEncode.Next((int)arrOffsetLookup[by].intCount);
                                    ushort intAddress = (ushort)arrOffsetLookup[by].ptrAddresses[intRandomIdx];
                                    ptrOutput.Write(BitConverter.GetBytes(intAddress), 0, 2);
                                }
                                intCh = ptrInput.ReadByte();
                            }

                            ptrOutput.Write(ptrSuffix, 0, bySuffixLen);
                            blnSuccess = true;
                        }
                        finally
                        {
                            if (ptrOutput != null) { ptrOutput.Close(); }
                        }
                    }
                    finally
                    {
                        if (ptrInput != null) { ptrInput.Close(); }
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("Error: ROM does not contain required byte values for UNSIGNAL header");
            }

            return blnSuccess;
        }

        // --- UNSIGNAL encode - byte to byte ---

        public static byte[] fuencodeByteToByte(ref RomData ptrRom_a, byte[] arrInput_a, RomData[] arrExtraRoms_a = null, bool blnTango_a = false)
        {
            byte[] arrResult = null;
            ByteAddresses[] arrOffsetLookup = new ByteAddresses[256];
            uint[] arrOffsetCounts = new uint[256];
            byte byOffsetLow = 0;
            byte byOffsetHigh = 0;
            byte byPrefixLen = 0;
            byte bySuffixLen = 0;
            ushort intROMOffset = 0;
            ushort intH1 = 0;
            ushort intH2 = 0;
            ushort intH3 = 0;
            ushort intH4 = 0;
            byte[] ptrPrefix = null;
            byte[] ptrSuffix = null;
            long lngEffectiveSize = 0;
            long lngI = 0;
            int intI = 0;
            bool blnLookupValid = true;

            for (intI = 0; intI < 256; intI++)
            {
                arrOffsetLookup[intI].ptrAddresses = null;
                arrOffsetLookup[intI].intCount = 0;
            }

            Random ptrRandHeader = createRandomSeed(ref ptrRom_a, 0);

            byOffsetLow  = (byte)ptrRandHeader.Next(256);
            byOffsetHigh = (byte)ptrRandHeader.Next(256);
            byPrefixLen  = (byte)(ptrRandHeader.Next(246) + 10);
            bySuffixLen  = (byte)(ptrRandHeader.Next(246) + 10);

            if (ptrRom_a.arrLookup[byOffsetLow].intCount  > 0 &&
                ptrRom_a.arrLookup[byOffsetHigh].intCount > 0 &&
                ptrRom_a.arrLookup[byPrefixLen].intCount  > 0 &&
                ptrRom_a.arrLookup[bySuffixLen].intCount  > 0)
            {
                intROMOffset = FindOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
                Random ptrRandEncode = createRandomSeed(ref ptrRom_a, intROMOffset);

                intH1 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetLow,  ptrRandEncode);
                intH2 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetHigh, ptrRandEncode);
                intH3 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byPrefixLen,  ptrRandEncode);
                intH4 = (ushort)FindROMAddress(ptrRom_a.arrLookup, bySuffixLen,  ptrRandEncode);

                ptrPrefix = new byte[byPrefixLen];
                ptrSuffix = new byte[bySuffixLen];
                ptrRandEncode.NextBytes(ptrPrefix);
                ptrRandEncode.NextBytes(ptrSuffix);

                lngEffectiveSize = ptrRom_a.lngROMSize - intROMOffset;
                if (lngEffectiveSize > 65536L) { lngEffectiveSize = 65536L; }

                for (lngI = 0; lngI < lngEffectiveSize; lngI++)
                {
                    arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset + lngI]]++;
                }

                for (intI = 0; intI < 256; intI++)
                {
                    arrOffsetLookup[intI].ptrAddresses = null;
                    arrOffsetLookup[intI].intCount = 0;
                }

                for (intI = 0; intI < 256 && blnLookupValid; intI++)
                {
                    if (arrOffsetCounts[intI] > 0)
                    {
                        arrOffsetLookup[intI].ptrAddresses = new uint[arrOffsetCounts[intI]];
                        if (arrOffsetLookup[intI].ptrAddresses != null)
                        {
                            arrOffsetLookup[intI].intCount = 0;
                        }
                        else
                        {
                            blnLookupValid = false;
                        }
                    }
                }

                for (lngI = 0; lngI < lngEffectiveSize && blnLookupValid; lngI++)
                {
                    byte by = ptrRom_a.ptrROMData[intROMOffset + lngI];
                    arrOffsetLookup[by].ptrAddresses[arrOffsetLookup[by].intCount++] = (uint)lngI;
                }

                if (blnLookupValid)
                {
                    int intOutputSize = HEADER_SIZE + byPrefixLen + (arrInput_a.Length * 2) + bySuffixLen;
                    arrResult = new byte[intOutputSize];
                    int intPos = 0;

                    byte[] arrH1 = BitConverter.GetBytes(intH1);
                    byte[] arrH2 = BitConverter.GetBytes(intH2);
                    byte[] arrH3 = BitConverter.GetBytes(intH3);
                    byte[] arrH4 = BitConverter.GetBytes(intH4);
                    arrResult[intPos++] = arrH1[0]; arrResult[intPos++] = arrH1[1];
                    arrResult[intPos++] = arrH2[0]; arrResult[intPos++] = arrH2[1];
                    arrResult[intPos++] = arrH3[0]; arrResult[intPos++] = arrH3[1];
                    arrResult[intPos++] = arrH4[0]; arrResult[intPos++] = arrH4[1];

                    Array.Copy(ptrPrefix, 0, arrResult, intPos, byPrefixLen);
                    intPos += byPrefixLen;

                    bool blnTango = blnTango_a && arrExtraRoms_a != null && arrExtraRoms_a.Length > 1;
                    int intROMCount = blnTango ? arrExtraRoms_a.Length : 1;
                    Random[] arrTangoRands = null;
                    ByteAddresses[][] arrTangoLookups = null;

                    if (blnTango)
                    {
                        arrTangoRands = new Random[intROMCount];
                        arrTangoLookups = new ByteAddresses[intROMCount][];
                        for (intI = 0; intI < intROMCount; intI++)
                        {
                            arrTangoRands[intI] = createRandomSeed(ref arrExtraRoms_a[intI], 0);
                            arrTangoLookups[intI] = arrExtraRoms_a[intI].arrLookup;
                        }
                    }

                    for (intI = 0; intI < arrInput_a.Length; intI++)
                    {
                        byte by = arrInput_a[intI];
                        if (blnTango)
                        {
                            int intROMIdx = intI % intROMCount;
                            ByteAddresses[] objLookup = arrTangoLookups[intROMIdx];
                            Random ptrTangoRand = arrTangoRands[intROMIdx];
                            if (objLookup[by].intCount > 0)
                            {
                                uint intRandomIdx = (uint)ptrTangoRand.Next((int)objLookup[by].intCount);
                                ushort intAddress = (ushort)objLookup[by].ptrAddresses[intRandomIdx];
                                byte[] arrAddr = BitConverter.GetBytes(intAddress);
                                arrResult[intPos++] = arrAddr[0];
                                arrResult[intPos++] = arrAddr[1];
                            }
                        }
                        else
                        {
                            if (arrOffsetLookup[by].intCount > 0)
                            {
                                uint intRandomIdx = (uint)ptrRandEncode.Next((int)arrOffsetLookup[by].intCount);
                                ushort intAddress = (ushort)arrOffsetLookup[by].ptrAddresses[intRandomIdx];
                                byte[] arrAddr = BitConverter.GetBytes(intAddress);
                                arrResult[intPos++] = arrAddr[0];
                                arrResult[intPos++] = arrAddr[1];
                            }
                        }
                    }

                    Array.Copy(ptrSuffix, 0, arrResult, intPos, bySuffixLen);
                }
            }

            return arrResult;
        }

        // --- UNSIGNAL decode - stream to stream ---

        private static bool fudecodeStreamToStream(RomData ptrRom_a, Stream ptrInput_a, Stream ptrOutput_a, RomData[] arrExtraRoms_a = null, bool blnTango_a = false)
        {
            bool blnSuccess = false;
            long lngInputSize = ptrInput_a.Length;
            byte[] arrBuf = new byte[2];
            ushort[] arrAddrs = new ushort[4];
            int intI = 0;

            for (intI = 0; intI < 4; intI++)
            {
                if (ptrInput_a.Read(arrBuf, 0, 2) != 2) { break; }
                arrAddrs[intI] = BitConverter.ToUInt16(arrBuf, 0);
                if (arrAddrs[intI] >= ptrRom_a.lngROMSize) { break; }
            }

            if (intI == 4)
            {
                byte byOffsetLow  = ptrRom_a.ptrROMData[arrAddrs[0]];
                byte byOffsetHigh = ptrRom_a.ptrROMData[arrAddrs[1]];
                byte byPrefixLen  = ptrRom_a.ptrROMData[arrAddrs[2]];
                byte bySuffixLen  = ptrRom_a.ptrROMData[arrAddrs[3]];

                ushort intOffset = FindOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
                long lngDataSize = lngInputSize - HEADER_SIZE - byPrefixLen - bySuffixLen;
                long lngSlots = lngDataSize / 2;

                bool blnTango = blnTango_a && arrExtraRoms_a != null && arrExtraRoms_a.Length > 1;
                int intROMCount = blnTango ? arrExtraRoms_a.Length : 1;

                if (lngSlots >= 0)
                {
                    for (intI = 0; intI < byPrefixLen; intI++)
                    {
                        if (ptrInput_a.ReadByte() == -1) { break; }
                    }

                    if (intI == byPrefixLen)
                    {
                        long lngEffSize = ptrRom_a.lngROMSize - intOffset;
                        if (lngEffSize > 65536) { lngEffSize = 65536; }

                        for (intI = 0; intI < lngSlots; intI++)
                        {
                            if (ptrInput_a.Read(arrBuf, 0, 2) != 2) { break; }

                            ushort intAddr = BitConverter.ToUInt16(arrBuf, 0);

                            if (blnTango)
                            {
                                RomData objRomCurrent = arrExtraRoms_a[intI % intROMCount];
                                if (intAddr < objRomCurrent.lngROMSize)
                                {
                                    ptrOutput_a.WriteByte(objRomCurrent.ptrROMData[intAddr]);
                                }
                            }
                            else
                            {
                                if (intAddr < lngEffSize)
                                {
                                    ptrOutput_a.WriteByte(ptrRom_a.ptrROMData[intOffset + intAddr]);
                                }
                            }
                        }

                        if (intI == lngSlots) { blnSuccess = true; }
                    }
                }
            }

            return blnSuccess;
        }

        // --- UNSIGNAL decode ---

        public static bool fudecodeFileToFile(RomData ptrRom_a, string strInputFile_a, string strOutputFile_a)
        {
            bool blnSuccess = false;

            try
            {
                using (FileStream ptrInput = new FileStream(strInputFile_a, FileMode.Open, FileAccess.Read))
                using (FileStream ptrOutput = new FileStream(strOutputFile_a, FileMode.Create, FileAccess.Write))
                {
                    blnSuccess = fudecodeStreamToStream(ptrRom_a, ptrInput, ptrOutput);
                }
            }
            catch { blnSuccess = false; }

            return blnSuccess;
        }

        public static byte[] fudecodeByteToByte(RomData ptrRom_a, byte[] arrEncodedData_a, RomData[] arrExtraRoms_a = null, bool blnTango_a = false)
        {
            byte[] arrResult = null;

            try
            {
                using (MemoryStream ptrInput = new MemoryStream(arrEncodedData_a))
                using (MemoryStream ptrOutput = new MemoryStream())
                {
                    if (fudecodeStreamToStream(ptrRom_a, ptrInput, ptrOutput, arrExtraRoms_a, blnTango_a))
                    {
                        arrResult = ptrOutput.ToArray();
                    }
                }
            }
            catch { arrResult = null; }

            return arrResult;
        }

        public static byte[] fudecodeFileToByte(RomData ptrRom_a, string strInputFile_a)
        {
            byte[] arrResult = null;

            try
            {
                using (FileStream ptrInput = new FileStream(strInputFile_a, FileMode.Open, FileAccess.Read))
                using (MemoryStream ptrOutput = new MemoryStream())
                {
                    if (fudecodeStreamToStream(ptrRom_a, ptrInput, ptrOutput))
                    {
                        arrResult = ptrOutput.ToArray();
                    }
                }
            }
            catch { arrResult = null; }

            return arrResult;
        }
    }

    // -------------------------------------------------------------------------
    // UEncode
    // -------------------------------------------------------------------------

    /// <summary>
    /// UNSIGNAL protocol encoding operations.
    /// UNSIGNAL extends ZOSCII with a random ROM offset, random prefix/suffix padding,
    /// and a 4-address header — making each encode unique even for identical plaintext.
    /// Compatible with uencode.c.
    /// </summary>
    public static class UEncode
    {
        /// <summary>Encode a byte array using UNSIGNAL protocol. Returns encoded bytes or null on failure.</summary>
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try { RomData objRomData = objRom_a.GetRomData(); arrResult = clsUnsignal.fuencodeByteToByte(ref objRomData, arrInput_a); }
            catch { }
            return arrResult;
        }

        /// <summary>Chain-encode a byte array through multiple ROMs in sequence. Returns encoded bytes or null if any stage fails.</summary>
        /// <summary>Chain-encode through multiple ROMs. If blnTango_a is true, round-robins ROMs at payload byte level — same 2x expansion, up to 3x entropy. With 1 ROM, Tango is identical to standard.</summary>
        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a, bool blnTango_a = false)
        {
            byte[] arrResult = null;
            try
            {
                if (blnTango_a && arrRoms_a.Length > 1)
                {
                    RomData[] arrRomData = new RomData[arrRoms_a.Length];
                    for (int intI = 0; intI < arrRoms_a.Length; intI++) { arrRomData[intI] = arrRoms_a[intI].GetRomData(); }
                    RomData objFirst = arrRomData[0];
                    arrResult = clsUnsignal.fuencodeByteToByte(ref objFirst, arrInput_a, arrRomData, true);
                }
                else
                {
                    arrResult = arrInput_a;
                    int intI = 0;
                    while (intI < arrRoms_a.Length && arrResult != null)
                    {
                        arrResult = Bytes(arrResult, arrRoms_a[intI]);
                        intI++;
                    }
                }
            }
            catch { }
            return arrResult;
        }

        /// <summary>Chain-encode a file through multiple ROMs in sequence using temp files - no full file load into RAM. Returns true on success.</summary>
		public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
		{
			bool blnResult = false;
			try
			{
				if (arrRoms_a == null || arrRoms_a.Length == 0) { return false; }
				string strCurrent = strInputPath_a;
				string[] arrTemps = new string[arrRoms_a.Length - 1];
				int intI = 0;
				for (intI = 0; intI < arrTemps.Length; intI++) { arrTemps[intI] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp"); }
				blnResult = true;
				for (intI = 0; intI < arrRoms_a.Length && blnResult; intI++)
				{
					string strNext = (intI == arrRoms_a.Length - 1) ? strOutputPath_a : arrTemps[intI];
					blnResult = File(strCurrent, strNext, arrRoms_a[intI]);
					strCurrent = strNext;
				}
				for (intI = 0; intI < arrTemps.Length; intI++) 
				{ 
					if (System.IO.File.Exists(arrTemps[intI])) 
					{ 
						SecureDelete.File(arrTemps[intI]); 
					} 
				}
			}
			catch { blnResult = false; }
			return blnResult;
		}

        /// <summary>Encode a file to an output file using UNSIGNAL protocol. Returns true on success.</summary>
        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;
            try { RomData objRomData = objRom_a.GetRomData(); blnResult = clsUnsignal.fuencode(ref objRomData, strInputPath_a, strOutputPath_a); }
            catch { }
            return blnResult;
        }

        /// <summary>Encode a UTF-8 string using UNSIGNAL protocol. Returns encoded bytes or null on failure.</summary>
        public static byte[] String(string strInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try { arrResult = Bytes(Encoding.UTF8.GetBytes(strInput_a), objRom_a); }
            catch { }
            return arrResult;
        }

        /// <summary>Encode a byte array and return as Base64 string. Returns empty string on failure.</summary>
        public static string ToBase64(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";
            try { byte[] arrEncoded = Bytes(arrInput_a, objRom_a); if (arrEncoded != null) { strResult = Convert.ToBase64String(arrEncoded); } }
            catch { }
            return strResult;
        }
    }

    // -------------------------------------------------------------------------
    // UDecode
    // -------------------------------------------------------------------------

    /// <summary>
    /// UNSIGNAL protocol decoding operations.
    /// Reads the 4-address header to determine offset/prefix/suffix, then decodes the payload.
    /// Compatible with udecode.c.
    /// </summary>
    public static class UDecode
    {
        /// <summary>Decode an UNSIGNAL-encoded byte array using a loaded ROM. Returns decoded bytes or null on failure.</summary>
        public static byte[] Bytes(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try { RomData objRomData = objRom_a.GetRomData(); arrResult = clsUnsignal.fudecodeByteToByte(objRomData, arrInput_a); }
            catch { }
            return arrResult;
        }

        /// <summary>Chain-decode a byte array through multiple ROMs in reverse sequence. Returns decoded bytes or null if any stage fails.</summary>
        /// <summary>Chain-decode through multiple ROMs. If blnTango_a is true, round-robins ROMs at payload byte level — must match encode.</summary>
        public static byte[] Chain(byte[] arrInput_a, ZOSCIIRom[] arrRoms_a, bool blnTango_a = false)
        {
            byte[] arrResult = null;
            try
            {
                if (blnTango_a && arrRoms_a.Length > 1)
                {
                    RomData[] arrRomData = new RomData[arrRoms_a.Length];
                    for (int intI = 0; intI < arrRoms_a.Length; intI++) { arrRomData[intI] = arrRoms_a[intI].GetRomData(); }
                    RomData objFirst = arrRomData[0];
                    arrResult = clsUnsignal.fudecodeByteToByte(objFirst, arrInput_a, arrRomData, true);
                }
                else
                {
                    arrResult = arrInput_a;
                    int intI = arrRoms_a.Length - 1;
                    while (intI >= 0 && arrResult != null)
                    {
                        arrResult = Bytes(arrResult, arrRoms_a[intI]);
                        intI--;
                    }
                }
            }
            catch { }
            return arrResult;
        }

        /// <summary>Chain-decode a file through multiple ROMs in reverse sequence using temp files - no full file load into RAM. Returns true on success.</summary>
        public static bool ChainFile(string strInputPath_a, string strOutputPath_a, ZOSCIIRom[] arrRoms_a)
        {
            bool blnResult = false;
            try
            {
                if (arrRoms_a == null || arrRoms_a.Length == 0) { return false; }
                string strCurrent = strInputPath_a;
                string[] arrTemps = new string[arrRoms_a.Length - 1];
                int intI = 0;
                for (intI = 0; intI < arrTemps.Length; intI++) { arrTemps[intI] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp"); }
                blnResult = true;
                for (intI = arrRoms_a.Length - 1; intI >= 0 && blnResult; intI--)
                {
                    string strNext = (intI == 0) ? strOutputPath_a : arrTemps[intI - 1];
                    blnResult = File(strCurrent, strNext, arrRoms_a[intI]);
                    strCurrent = strNext;
                }
                for (intI = 0; intI < arrTemps.Length; intI++) { if (System.IO.File.Exists(arrTemps[intI])) { SecureDelete.File(arrTemps[intI]); } }
            }
            catch { blnResult = false; }
            return blnResult;
        }

        /// <summary>Decode a Base64-encoded UNSIGNAL payload. Returns decoded bytes or null on failure.</summary>
        public static byte[] FromBase64(string strBase64_a, ZOSCIIRom objRom_a)
        {
            byte[] arrResult = null;
            try { arrResult = Bytes(Convert.FromBase64String(strBase64_a), objRom_a); }
            catch { }
            return arrResult;
        }

        /// <summary>Decode an UNSIGNAL-encoded file to an output file using a loaded ROM. Returns true on success.</summary>
        public static bool File(string strInputPath_a, string strOutputPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;
            try { RomData objRomData = objRom_a.GetRomData(); blnResult = clsUnsignal.fudecodeFileToFile(objRomData, strInputPath_a, strOutputPath_a); }
            catch { }
            return blnResult;
        }

        /// <summary>Decode a UNSIGNAL-encoded file and return contents as a UTF-8 string. Returns empty string on failure.</summary>
        public static string FileToString(string strInputPath_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";
            try { RomData objRomData = objRom_a.GetRomData(); byte[] arrDecoded = clsUnsignal.fudecodeFileToByte(objRomData, strInputPath_a); if (arrDecoded != null) { strResult = Encoding.UTF8.GetString(arrDecoded); } }
            catch { }
            return strResult;
        }

        /// <summary>Decode an UNSIGNAL-encoded byte array and return as a UTF-8 string. Returns empty string on failure.</summary>
        public static string ToString(byte[] arrInput_a, ZOSCIIRom objRom_a)
        {
            string strResult = "";
            try { byte[] arrDecoded = Bytes(arrInput_a, objRom_a); if (arrDecoded != null) { strResult = Encoding.UTF8.GetString(arrDecoded); } }
            catch { }
            return strResult;
        }
    }

    // -------------------------------------------------------------------------
    // UVerify
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verify an UNSIGNAL-encoded file against its plaintext source.
    /// Equivalent to the UNSIGNAL compare mode of uverify.c.
    /// </summary>
    public static class UVerify
    {
        private const int UNSIGNAL_OFFSET_LIMIT_PCT = 2;
        private const int UNSIGNAL_HEADER_SIZE = 8;

        /// <summary>Verify an UNSIGNAL-encoded file against its plaintext file. Returns true if match.</summary>
        public static bool File(string strEncodedPath_a, string strPlainPath_a, ZOSCIIRom objRom_a)
        {
            bool blnResult = false;

            try
            {
                RomData objRomData = objRom_a.GetRomData();

                using (FileStream ptrEncoded = new FileStream(strEncodedPath_a, FileMode.Open, FileAccess.Read))
                {
                    long lngInputSize = ptrEncoded.Length;
                    byte[] arrBuf = new byte[2];
                    ushort[] arrAddrs = new ushort[4];
                    int intI = 0;

                    for (intI = 0; intI < 4; intI++)
                    {
                        if (ptrEncoded.Read(arrBuf, 0, 2) != 2) { break; }
                        arrAddrs[intI] = BitConverter.ToUInt16(arrBuf, 0);
                        if (arrAddrs[intI] >= objRomData.lngROMSize) { break; }
                    }

                    if (intI == 4)
                    {
                        byte byOffsetLow  = objRomData.ptrROMData[arrAddrs[0]];
                        byte byOffsetHigh = objRomData.ptrROMData[arrAddrs[1]];
                        byte byPrefixLen  = objRomData.ptrROMData[arrAddrs[2]];
                        byte bySuffixLen  = objRomData.ptrROMData[arrAddrs[3]];

                        ushort intOffset = findOffset(byOffsetLow, byOffsetHigh, objRomData.lngROMSize);
                        long lngSlots = (lngInputSize - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen) / 2;

                        if (lngSlots >= 0)
                        {
                            for (intI = 0; intI < byPrefixLen; intI++)
                            {
                                if (ptrEncoded.ReadByte() == -1) { break; }
                            }

                            if (intI == byPrefixLen)
                            {
                                long lngEffSize = Math.Min(objRomData.lngROMSize - intOffset, 65536L);

                                using (FileStream ptrPlain = new FileStream(strPlainPath_a, FileMode.Open, FileAccess.Read))
                                {
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

                                            if (intPlainByte == -1 || intAddr >= lngEffSize)
                                            {
                                                blnMatch = false;
                                                blnDone = true;
                                            }
                                            else if (objRomData.ptrROMData[intOffset + intAddr] != (byte)intPlainByte)
                                            {
                                                blnMatch = false;
                                                blnDone = true;
                                            }
                                        }
                                    }

                                    if (blnMatch && ptrPlain.ReadByte() != -1) { blnMatch = false; }
                                    blnResult = blnMatch;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return blnResult;
        }

        private static ushort findOffset(byte byLow_a, byte byHigh_a, long lngROMSize_a)
        {
            ushort intRaw = (ushort)(byLow_a | (byHigh_a << 8));
            ushort intResult = 0;

            if (lngROMSize_a >= 131072L)
            {
                intResult = intRaw;
            }
            else
            {
                long lngMax = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
                if (lngMax > 0) { intResult = (ushort)(intRaw % (lngMax + 1)); }
            }

            return intResult;
        }
    }
}