// Cyborg UNSIGNAL Protocol v20260303
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

using System;
using System.IO;

struct ByteAddresses
{
    public uint[] ptrAddresses;
    public uint intCount;
}

struct RomData
{
    public byte[] ptrROMData;
    public long lngROMSize;
    public ByteAddresses[] arrLookup;
}

class Program
{
    private const int UNSIGNAL_OFFSET_LIMIT_PCT = 2;
    private const int UNSIGNAL_ROM_LOAD_MAX = 131072;
    private const int HEADER_SIZE = 8;

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

    private static uint FindROMAddress(ByteAddresses[] ptrLookup_a, byte byTarget_a)
    {
        uint intResult = 0;
        
        if (ptrLookup_a[byTarget_a].intCount > 0)
        {
            uint intRandomIdx = (uint)(ptrRand.Next((int)ptrLookup_a[byTarget_a].intCount));
            intResult = ptrLookup_a[byTarget_a].ptrAddresses[intRandomIdx];
        }
        
        return intResult;
    }

	private static Random ptrRand;
	
    private static void BuildLookupTable(ref RomData ptrRom_a)
    {
        uint[] arrCounts = new uint[256];
        long lngHeaderSize = 0;
        
        // Initialize lookup array
        ptrRom_a.arrLookup = new ByteAddresses[256];
        for (int intI = 0; intI < 256; intI++)
        {
            ptrRom_a.arrLookup[intI].ptrAddresses = null;
            ptrRom_a.arrLookup[intI].intCount = 0;
        }
        
        // Header addresses must be within the first 64KB (16-bit addresses)
        lngHeaderSize = (ptrRom_a.lngROMSize > 65536L) ? 65536L : ptrRom_a.lngROMSize;
        
        // Count occurrences
        for (long lngI = 0; lngI < lngHeaderSize; lngI++)
        {
            arrCounts[ptrRom_a.ptrROMData[lngI]]++;
        }
        
        // Allocate memory for each byte value
        for (int intI = 0; intI < 256; intI++)
        {
            if (arrCounts[intI] > 0)
            {
                ptrRom_a.arrLookup[intI].ptrAddresses = new uint[arrCounts[intI]];
                ptrRom_a.arrLookup[intI].intCount = 0;
            }
        }
        
        // Fill addresses
        for (long lngI = 0; lngI < lngHeaderSize; lngI++)
        {
            byte by = ptrRom_a.ptrROMData[lngI];
            ptrRom_a.arrLookup[by].ptrAddresses[ptrRom_a.arrLookup[by].intCount++] = (uint)lngI;
        }

		// Seed Random based on ROM content
		uint romHash = 0;
		for (long lngI = 0; lngI < ptrRom_a.lngROMSize; lngI++)
		{
			romHash = (romHash * 33) + ptrRom_a.ptrROMData[lngI];
		}
		
		/* XOR with current time for per-run uniqueness */
		romHash ^= (uint)Environment.TickCount;
		
		ptrRand = new Random((int)romHash);
    }

    private static RomData LoadRom(string strFilename_a)
    {
        RomData ptrRom = new RomData();
        ptrRom.ptrROMData = null;
        ptrRom.lngROMSize = 0;
        ptrRom.arrLookup = null;
        
        try
        {
            using (FileStream ptrStream = new FileStream(strFilename_a, FileMode.Open, FileAccess.Read))
            {
                long lngLoad = Math.Min(ptrStream.Length, UNSIGNAL_ROM_LOAD_MAX);
                ptrRom.lngROMSize = lngLoad;
                ptrRom.ptrROMData = new byte[lngLoad];
                ptrStream.Read(ptrRom.ptrROMData, 0, (int)lngLoad);
                
                // Pre-build lookup table for reuse across multiple encodes
                BuildLookupTable(ref ptrRom);
            }
        }
        catch
        {
            ptrRom.ptrROMData = null;
            ptrRom.lngROMSize = 0;
            ptrRom.arrLookup = null;
        }
        
        return ptrRom;
    }

    private static void UnloadRom(ref RomData ptrRom_a)
    {
        // In C#, garbage collector handles this, but method kept for symmetry
        ptrRom_a.ptrROMData = null;
        ptrRom_a.arrLookup = null;
        ptrRom_a.lngROMSize = 0;
    }

    private static bool EncodeFile(ref RomData ptrRom_a, string strInputFile_a, string strOutputFile_a)
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
        
        // Initialize offset lookup array
        for (intI = 0; intI < 256; intI++)
        {
            arrOffsetLookup[intI].ptrAddresses = null;
            arrOffsetLookup[intI].intCount = 0;
        }
        
        // Generate header values
        byOffsetLow = (byte)ptrRand.Next(256);
        byOffsetHigh = (byte)ptrRand.Next(256);
        byPrefixLen = (byte)(ptrRand.Next(246) + 10);
        bySuffixLen = (byte)(ptrRand.Next(246) + 10);
        
        // Check that ROM contains required byte values using pre-built lookup
        if (ptrRom_a.arrLookup[byOffsetLow].intCount > 0 && 
            ptrRom_a.arrLookup[byOffsetHigh].intCount > 0 &&
            ptrRom_a.arrLookup[byPrefixLen].intCount > 0 && 
            ptrRom_a.arrLookup[bySuffixLen].intCount > 0)
        {
            intROMOffset = FindOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
            
            intH1 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetLow);
            intH2 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byOffsetHigh);
            intH3 = (ushort)FindROMAddress(ptrRom_a.arrLookup, byPrefixLen);
            intH4 = (ushort)FindROMAddress(ptrRom_a.arrLookup, bySuffixLen);
            
            // Generate random prefix and suffix
            ptrPrefix = new byte[byPrefixLen];
            ptrSuffix = new byte[bySuffixLen];
            ptrRand.NextBytes(ptrPrefix);
            ptrRand.NextBytes(ptrSuffix);
            
            // Calculate effective encoding window
            lngEffectiveSize = ptrRom_a.lngROMSize - intROMOffset;
            if (lngEffectiveSize > 65536L)
            {
                lngEffectiveSize = 65536L;
            }
            
            // Build offset lookup tables for the encoding window
            for (lngI = 0; lngI < lngEffectiveSize; lngI++)
            {
                arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset + lngI]]++;
            }
            
            // Initialize offset lookup array
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
                        
                        // Write header (4 x 16-bit addresses)
                        ptrOutput.Write(BitConverter.GetBytes(intH1), 0, 2);
                        ptrOutput.Write(BitConverter.GetBytes(intH2), 0, 2);
                        ptrOutput.Write(BitConverter.GetBytes(intH3), 0, 2);
                        ptrOutput.Write(BitConverter.GetBytes(intH4), 0, 2);
                        
                        // Write prefix
                        ptrOutput.Write(ptrPrefix, 0, byPrefixLen);
                        
                        // Stream-encode input
                        intCh = ptrInput.ReadByte();
                        while (intCh != -1)
                        {
                            byte by = (byte)intCh;
                            if (arrOffsetLookup[by].intCount > 0)
                            {
                                uint intRandomIdx = (uint)ptrRand.Next((int)arrOffsetLookup[by].intCount);
                                ushort intAddress = (ushort)arrOffsetLookup[by].ptrAddresses[intRandomIdx];
                                ptrOutput.Write(BitConverter.GetBytes(intAddress), 0, 2);
                            }
                            intCh = ptrInput.ReadByte();
                        }
                        
                        // Write suffix
                        ptrOutput.Write(ptrSuffix, 0, bySuffixLen);
                        
                        blnSuccess = true;
                    }
                    finally
                    {
                        if (ptrOutput != null)
                        {
                            ptrOutput.Close();
                        }
                    }
                }
                finally
                {
                    if (ptrInput != null)
                    {
                        ptrInput.Close();
                    }
                }
            }
        }
        else
        {
            Console.Error.WriteLine("Error: ROM does not contain required byte values for UNSIGNAL header");
        }
        
        return blnSuccess;
    }

    static void Main(string[] strArgs_a)
    {
        int intResult = 1;
        RomData ptrRom = new RomData();
        bool blnEncodeOk = false;
        
        Console.WriteLine("UNSIGNAL Protocol Encoder v20260303");
        Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1");
        Console.WriteLine();

        if (strArgs_a.Length == 3)
        {
            ptrRom = LoadRom(strArgs_a[0]);
            if (ptrRom.ptrROMData != null)
            {
                blnEncodeOk = EncodeFile(ref ptrRom, strArgs_a[1], strArgs_a[2]);
                
                if (blnEncodeOk)
                {
                    intResult = 0;
                }
                else
                {
                    Console.Error.WriteLine("Encode failed");
                }
                
                UnloadRom(ref ptrRom);
            }
            else
            {
                Console.Error.WriteLine("Failed to load ROM");
            }
        }
        else
        {
            Console.Error.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <romfile> <inputdatafile> <encodedoutput>");
        }
        
        Environment.Exit(intResult);
    }
}