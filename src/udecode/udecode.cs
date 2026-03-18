// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

using System;
using System.IO;

class Program
{
    private const int UNSIGNAL_OFFSET_LIMIT_PCT = 2;
    private const int UNSIGNAL_ROM_LOAD_MAX = 131072;
    private const int HEADER_SIZE = 8;

    private class RomData
    {
        public byte[] ptrROMData;
        public long lngROMSize;
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

    private static RomData LoadRom(string strFilename_a)
    {
        RomData ptrRom = null;
        
        try
        {
            using (FileStream ptrStream = new FileStream(strFilename_a, FileMode.Open, FileAccess.Read))
            {
                ptrRom = new RomData();
                long lngLoad = Math.Min(ptrStream.Length, UNSIGNAL_ROM_LOAD_MAX);
                ptrRom.lngROMSize = lngLoad;
                ptrRom.ptrROMData = new byte[lngLoad];
                ptrStream.Read(ptrRom.ptrROMData, 0, (int)lngLoad);
            }
        }
        catch
        {
            ptrRom = null;
        }
        
        return ptrRom;
    }

    private static void FreeRom(RomData ptrRom_a)
    {
        // In C#, garbage collector handles this, but method kept for consistency
    }

    private static bool DecodeFile(RomData ptrRom_a, string strInputFile_a, string strOutputFile_a)
    {
        bool blnSuccess = false;
        
        try
        {
            using (FileStream ptrInput = new FileStream(strInputFile_a, FileMode.Open, FileAccess.Read))
            using (FileStream ptrOutput = new FileStream(strOutputFile_a, FileMode.Create, FileAccess.Write))
            {
                long lngInputSize = ptrInput.Length;
                byte[] arrBuf = new byte[2];
                ushort[] arrAddrs = new ushort[4];
                int intI = 0;
                
                // Read header
                for (intI = 0; intI < 4; intI++)
                {
                    if (ptrInput.Read(arrBuf, 0, 2) != 2)
                    {
                        break;
                    }
                    arrAddrs[intI] = BitConverter.ToUInt16(arrBuf, 0);
                    if (arrAddrs[intI] >= ptrRom_a.lngROMSize)
                    {
                        break;
                    }
                }
                
                if (intI == 4)
                {
                    byte byOffsetLow = ptrRom_a.ptrROMData[arrAddrs[0]];
                    byte byOffsetHigh = ptrRom_a.ptrROMData[arrAddrs[1]];
                    byte byPrefixLen = ptrRom_a.ptrROMData[arrAddrs[2]];
                    byte bySuffixLen = ptrRom_a.ptrROMData[arrAddrs[3]];
                    
                    ushort intOffset = FindOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
                    
                    long lngDataSize = lngInputSize - HEADER_SIZE - byPrefixLen - bySuffixLen;
                    long lngSlots = lngDataSize / 2;
                    
                    if (lngSlots >= 0)
                    {
                        // Skip prefix
                        for (intI = 0; intI < byPrefixLen; intI++)
                        {
                            if (ptrInput.ReadByte() == -1)
                            {
                                break;
                            }
                        }
                        
                        if (intI == byPrefixLen)
                        {
                            long lngEffSize = ptrRom_a.lngROMSize - intOffset;
                            if (lngEffSize > 65536)
                            {
                                lngEffSize = 65536;
                            }
                            
                            // Decode
                            for (intI = 0; intI < lngSlots; intI++)
                            {
                                if (ptrInput.Read(arrBuf, 0, 2) != 2)
                                {
                                    break;
                                }
                                
                                ushort intAddr = BitConverter.ToUInt16(arrBuf, 0);
                                if (intAddr < lngEffSize)
                                {
                                    ptrOutput.WriteByte(ptrRom_a.ptrROMData[intOffset + intAddr]);
                                }
                            }
                            
                            if (intI == lngSlots)
                            {
                                blnSuccess = true;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            blnSuccess = false;
        }
        
        return blnSuccess;
    }

    static void Main(string[] strArgs_a)
    {
        int intResult = 1;
        RomData ptrRom = null;
        bool blnDecodeOk = false;
        
        Console.WriteLine("UNSIGNAL Protocol Decoder v20260301");
        Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1");
        Console.WriteLine();

        if (strArgs_a.Length == 3)
        {
            ptrRom = LoadRom(strArgs_a[0]);
            if (ptrRom != null)
            {
                blnDecodeOk = DecodeFile(ptrRom, strArgs_a[1], strArgs_a[2]);
                FreeRom(ptrRom);
                
                if (blnDecodeOk)
                {
                    intResult = 0;
                }
                else
                {
                    Console.Error.WriteLine("Decode failed");
                }
            }
            else
            {
                Console.Error.WriteLine("Failed to load ROM");
            }
        }
        else
        {
            Console.Error.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <romfile> <encoded> <output>");
        }
        
        Environment.Exit(intResult);
    }
}