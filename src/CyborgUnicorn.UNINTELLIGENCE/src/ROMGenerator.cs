// CyborgUnicorn.ZOSCII - ROMGenerator
// Entropy-sugar-driven ROM generation from MP3 source files
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.IO;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// Generates ZOSCII ROMs from MP3 source files using Entropy Sugar to derive
    /// generation parameters. Same entropy + same MP3s = same ROM. Different sessions
    /// produce different ROMs from the same source files.
    ///
    /// Algorithm: 5 independent MP3 readers with entropy-derived file index, starting
    /// offset, and direction (forward/backward). Output byte = XOR of all 5 readers.
    /// Produces near-flat byte distribution.
    /// </summary>
    public static class ROMGenerator
    {
        // --- Constants ---

        private const int ROM_SIZE = 131072; // 128KB

        // --- Public ---

        /// <summary>
        /// Generate a 128KB ROM from MP3 files in the provided folders (1 level deep each)
        /// using entropy sugar to derive parameters. Returns raw 128KB byte array or null on failure.
        /// </summary>
        public static byte[] Bytes(string[] arrMp3Folders_a, EntropySugar objSugar_a)
        {
            byte[] arrResult = null;

            try
            {
                string[] arrMp3Files = collectMp3Files(arrMp3Folders_a);
                if (arrMp3Files.Length > 0)
                {
                    arrResult = generateFromFiles(arrMp3Files, objSugar_a);
                }
            }
            catch { }

            return arrResult;
        }

        // --- Private ---

        private static string[] collectMp3Files(string[] arrFolders_a)
        {
            List<string> objList = new List<string>();
            int intI = 0;
            int intJ = 0;

            for (intI = 0; intI < arrFolders_a.Length; intI++)
            {
                string strFolder = arrFolders_a[intI];
                if (strFolder.Length > 0 && Directory.Exists(strFolder))
                {
                    try
                    {
                        string[] arrFiles = Directory.GetFiles(strFolder, "*.mp3");
                        for (intJ = 0; intJ < arrFiles.Length; intJ++) { objList.Add(arrFiles[intJ]); }

                        string[] arrSubs = Directory.GetDirectories(strFolder);
                        for (intJ = 0; intJ < arrSubs.Length; intJ++)
                        {
                            try
                            {
                                string[] arrSubFiles = Directory.GetFiles(arrSubs[intJ], "*.mp3");
                                for (int intK = 0; intK < arrSubFiles.Length; intK++) { objList.Add(arrSubFiles[intK]); }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            return objList.ToArray();
        }

        private static byte[] generateFromFiles(string[] arrMp3Files_a, EntropySugar objSugar_a)
        {
            byte[] arrOutput = null;

            try
            {
                int intFileCount = arrMp3Files_a.Length;

                // Derive seeds from entropy sugar
                long lngSysTime   = objSugar_a.Get("SYS_TIME");
                long lngUptime    = objSugar_a.Get("UPTIME_MS");
                long lngFreeMem   = objSugar_a.Get("FREE_MEM");
                long lngInteract  = objSugar_a.Get("INTERACT_DELTA");
                long lngMouseX    = objSugar_a.Get("MOUSE_DELTA_X");
                long lngMouseY    = objSugar_a.Get("MOUSE_DELTA_Y");
                long lngBtnClicks = objSugar_a.Get("BTN_CLICKS");
                long lngFormOpens = objSugar_a.Get("FORM_OPENS");
                long lngDecodeOps = objSugar_a.Get("DECODE_OPS");
                long lngKeyTime   = objSugar_a.Get("KEY_TIMESTAMPS");
                long lngHandles   = objSugar_a.Get("PROC_HANDLES");
                long lngROMCount  = objSugar_a.Get("ROM_COUNT");
                long lngFileCount = objSugar_a.Get("FILE_COUNT");

                // 5 independent file selection seeds
                long[] arrFileSeeds = {
                    Math.Abs(lngSysTime   + lngBtnClicks),
                    Math.Abs(lngUptime    + lngDecodeOps),
                    Math.Abs(lngFreeMem   + lngKeyTime),
                    Math.Abs(lngInteract  + lngHandles),
                    Math.Abs(lngMouseX    + lngMouseY + lngFormOpens)
                };

                // 5 independent offset seeds
                long[] arrOffsetSeeds = {
                    Math.Abs(lngFormOpens + lngSysTime   + lngROMCount),
                    Math.Abs(lngDecodeOps + lngUptime    + lngMouseX),
                    Math.Abs(lngKeyTime   + lngFreeMem   + lngBtnClicks),
                    Math.Abs(lngHandles   + lngInteract  + lngMouseY),
                    Math.Abs(lngBtnClicks + lngMouseY    + lngFileCount)
                };

                // 5 independent direction seeds
                long[] arrDirSeeds = {
                    lngSysTime   + lngDecodeOps,
                    lngUptime    + lngMouseX,
                    lngFreeMem   + lngHandles,
                    lngInteract  + lngKeyTime,
                    lngBtnClicks + lngFormOpens
                };

                // Load 5 tracks
                byte[][] arrTracks = new byte[5][];
                int intI = 0;

                for (intI = 0; intI < 5; intI++)
                {
                    int intIdx = (int)(Math.Abs(arrFileSeeds[intI]) % intFileCount);
                    arrTracks[intI] = System.IO.File.ReadAllBytes(arrMp3Files_a[intIdx]);
                }

                // Derive start position and direction per track
                int[] arrPos = new int[5];
                bool[] arrForward = new bool[5];

                for (intI = 0; intI < 5; intI++)
                {
                    int intLen = Math.Max(1, arrTracks[intI].Length);
                    arrForward[intI] = (arrDirSeeds[intI] % 2 == 0);

                    if (arrForward[intI])
                    {
                        arrPos[intI] = (int)(Math.Abs(arrOffsetSeeds[intI]) % intLen);
                    }
                    else
                    {
                        arrPos[intI] = intLen - 1 - (int)(Math.Abs(arrOffsetSeeds[intI]) % intLen);
                    }
                }

                // Generate 128KB — XOR all 5 tracks per output byte
                arrOutput = new byte[ROM_SIZE];
                int intOut = 0;

                while (intOut < ROM_SIZE)
                {
                    byte byResult = 0;

                    for (intI = 0; intI < 5; intI++)
                    {
                        int intLen = arrTracks[intI].Length;
                        if (intLen > 0)
                        {
                            byResult ^= arrTracks[intI][arrPos[intI]];

                            if (arrForward[intI])
                            {
                                arrPos[intI]++;
                                if (arrPos[intI] >= intLen) { arrPos[intI] = 0; }
                            }
                            else
                            {
                                arrPos[intI]--;
                                if (arrPos[intI] < 0) { arrPos[intI] = intLen - 1; }
                            }
                        }
                    }

                    arrOutput[intOut] = byResult;
                    intOut++;
                }
            }
            catch { }

            return arrOutput;
        }
    }
}
