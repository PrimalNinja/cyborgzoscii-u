// CyborgUnicorn.ZOSCII - Binary
// BVerify, BSplit, BJoin
// Format-agnostic binary comparison and PENTAGONE 3-of-5 redundancy
// Compatible with uverify.c / usplit.c / ujoin.c
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.IO;

namespace CyborgUnicorn.ZOSCII
{
// -------------------------------------------------------------------------

    /// <summary>
    /// Plain binary file and byte array comparison.
    /// Equivalent to the binary compare mode of uverify.c.
    /// </summary>
    public static class BVerify
    {
        /// <summary>
        /// Compare two files byte-for-byte. Returns true if identical.
        /// </summary>
        public static bool File(string strFile1_a, string strFile2_a)
        {
            bool blnResult = false;

            try
            {
                using (FileStream ptrFile1 = new FileStream(strFile1_a, FileMode.Open, FileAccess.Read))
                using (FileStream ptrFile2 = new FileStream(strFile2_a, FileMode.Open, FileAccess.Read))
                {
                    bool blnMatch = true;
                    bool blnDone = false;

                    while (!blnDone)
                    {
                        int intByte1 = ptrFile1.ReadByte();
                        int intByte2 = ptrFile2.ReadByte();

                        if (intByte1 != intByte2)
                        {
                            blnMatch = false;
                            blnDone = true;
                        }
                        else if (intByte1 == -1)
                        {
                            blnDone = true;
                        }
                    }

                    blnResult = blnMatch;
                }
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Compare two byte arrays. Returns true if identical.
        /// </summary>
        public static bool Bytes(byte[] arrData1_a, byte[] arrData2_a)
        {
            bool blnResult = false;

            try
            {
                if (arrData1_a.Length == arrData2_a.Length)
                {
                    bool blnMatch = true;

                    for (int intI = 0; intI < arrData1_a.Length && blnMatch; intI++)
                    {
                        if (arrData1_a[intI] != arrData2_a[intI])
                        {
                            blnMatch = false;
                        }
                    }

                    blnResult = blnMatch;
                }
            }
            catch { }

            return blnResult;
        }
    }

	// -------------------------------------------------------------------------
    // BSplit
    // -------------------------------------------------------------------------

    /// <summary>
    /// Split any file or byte array into 5 shares using C(5,3)=10 combinatorial pattern.
    /// Any 3 or more shares reconstruct the original.
    /// Returns null on failure.
    /// Compatible with usplit.c.
    /// </summary>
    public static class BSplit
    {
        private const int SHARE_COUNT = 5;
        private const int PATTERN_LEN = 10;

        private static readonly int[,] s_arrPattern = new int[PATTERN_LEN, 3]
        {
            {0, 1, 2},
            {0, 1, 3},
            {0, 1, 4},
            {0, 2, 3},
            {0, 2, 4},
            {0, 3, 4},
            {1, 2, 3},
            {1, 2, 4},
            {1, 3, 4},
            {2, 3, 4}
        };

        /// <summary>
        /// Split a byte array into 5 shares.
        /// Returns array of 5 byte arrays, or null on failure.
        /// </summary>
        public static byte[][] Bytes(byte[] arrInput_a)
        {
            byte[][] arrShares = null;

            try
            {
                int intI = 0;
                int intJ = 0;

                int[] arrSizes = new int[SHARE_COUNT];
                for (long lngPos = 0; lngPos < arrInput_a.Length; lngPos++)
                {
                    int intRow = (int)(lngPos % PATTERN_LEN);
                    for (intJ = 0; intJ < 3; intJ++)
                    {
                        arrSizes[s_arrPattern[intRow, intJ]]++;
                    }
                }

                arrShares = new byte[SHARE_COUNT][];
                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    arrShares[intI] = new byte[arrSizes[intI]];
                }

                int[] arrPos = new int[SHARE_COUNT];
                for (long lngPos = 0; lngPos < arrInput_a.Length; lngPos++)
                {
                    int intRow = (int)(lngPos % PATTERN_LEN);
                    for (intJ = 0; intJ < 3; intJ++)
                    {
                        int intShare = s_arrPattern[intRow, intJ];
                        arrShares[intShare][arrPos[intShare]++] = arrInput_a[lngPos];
                    }
                }
            }
            catch
            {
                arrShares = null;
            }

            return arrShares;
        }

        /// <summary>
        /// Split a file into 5 share files named strOutputBase_a.s1 through .s5.
        /// Returns array of 5 share paths, or null on failure.
        /// </summary>
        public static string[] File(string strInputPath_a, string strOutputBase_a)
        {
            string[] arrSharePaths = null;
            FileStream ptrInput = null;
            FileStream[] arrShares = new FileStream[SHARE_COUNT];
            bool blnError = false;
            int intI = 0;
            int intJ = 0;

            try
            {
                arrSharePaths = new string[SHARE_COUNT];

                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    arrSharePaths[intI] = strOutputBase_a + ".s" + (intI + 1);
                    if (System.IO.File.Exists(arrSharePaths[intI]))
                    {
                        blnError = true;
                    }
                }

                if (!blnError)
                {
                    ptrInput = new FileStream(strInputPath_a, FileMode.Open, FileAccess.Read);

                    for (intI = 0; intI < SHARE_COUNT; intI++)
                    {
                        arrShares[intI] = new FileStream(arrSharePaths[intI], FileMode.Create, FileAccess.Write);
                    }

                    long lngPos = 0;
                    int intByte = ptrInput.ReadByte();

                    while (intByte != -1 && !blnError)
                    {
                        int intRow = (int)(lngPos % PATTERN_LEN);
                        for (intJ = 0; intJ < 3; intJ++)
                        {
                            arrShares[s_arrPattern[intRow, intJ]].WriteByte((byte)intByte);
                        }
                        lngPos++;
                        intByte = ptrInput.ReadByte();
                    }
                }
            }
            catch
            {
                blnError = true;
            }
            finally
            {
                if (ptrInput != null) { ptrInput.Close(); }
                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    if (arrShares[intI] != null) { arrShares[intI].Close(); }
                }
            }

            if (blnError)
            {
                if (arrSharePaths != null)
                {
                    for (intI = 0; intI < SHARE_COUNT; intI++)
                    {
                        if (arrSharePaths[intI] != null && System.IO.File.Exists(arrSharePaths[intI]))
                        {
                            SecureDelete.File(arrSharePaths[intI]);
                        }
                    }
                }
                arrSharePaths = null;
            }

            return arrSharePaths;
        }
    }

    // -------------------------------------------------------------------------
    // BJoin
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reconstruct a file or byte array from 3 or more of its 5 shares.
    /// Returns null on failure.
    /// Compatible with ujoin.c.
    /// </summary>
    public static class BJoin
    {
        private const int SHARE_COUNT = 5;
        private const int PATTERN_LEN = 10;

        private static readonly int[,] s_arrPattern = new int[PATTERN_LEN, 3]
        {
            {0, 1, 2},
            {0, 1, 3},
            {0, 1, 4},
            {0, 2, 3},
            {0, 2, 4},
            {0, 3, 4},
            {1, 2, 3},
            {1, 2, 4},
            {1, 3, 4},
            {2, 3, 4}
        };

        /// <summary>
        /// Reconstruct from byte array shares. Pass null for any missing share.
        /// At least 3 must be non-null. Returns reconstructed bytes or null on failure.
        /// </summary>
        public static byte[] Bytes(byte[][] arrShares_a)
        {
            byte[] arrResult = null;

            try
            {
                bool[] arrPresent = new bool[SHARE_COUNT];
                int[] arrReadFrom = new int[PATTERN_LEN];
                int intPresentCount = 0;
                int intI = 0;

                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    arrPresent[intI] = (arrShares_a[intI] != null);
                    if (arrPresent[intI]) { intPresentCount++; }
                }

                if (intPresentCount >= 3 && buildReadTable(arrPresent, arrReadFrom))
                {
                    List<byte> objOutput = new List<byte>();
                    int[] arrPos = new int[SHARE_COUNT];
                    bool blnDone = false;
                    long lngPos = 0;

                    while (!blnDone)
                    {
                        int intRow = (int)(lngPos % PATTERN_LEN);
                        int intShareIdx = arrReadFrom[intRow];

                        if (arrPos[intShareIdx] >= arrShares_a[intShareIdx].Length)
                        {
                            blnDone = true;
                        }
                        else
                        {
                            byte byVal = arrShares_a[intShareIdx][arrPos[intShareIdx]++];

                            for (int intJ = 0; intJ < 3; intJ++)
                            {
                                int intOther = s_arrPattern[intRow, intJ];
                                if (intOther != intShareIdx && arrPresent[intOther])
                                {
                                    if (arrPos[intOther] < arrShares_a[intOther].Length)
                                    {
                                        arrPos[intOther]++;
                                    }
                                }
                            }

                            objOutput.Add(byVal);
                            lngPos++;
                        }
                    }

                    arrResult = objOutput.ToArray();
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Reconstruct a file from share files. Provide the path to any one share (.s1-.s5) —
        /// sibling shares are auto-discovered.
        /// Returns output path on success, or null on failure.
        /// </summary>
        public static string File(string strAnySharePath_a, string strOutputPath_a)
        {
            string strResult = null;
            bool blnError = false;
            int intI = 0;
            int intJ = 0;

            string strBasePath = buildBasePath(strAnySharePath_a);
            if (strBasePath == null || System.IO.File.Exists(strOutputPath_a))
            {
                return null;
            }

            bool[] arrPresent = new bool[SHARE_COUNT];
            string[] arrSharePaths = new string[SHARE_COUNT];
            int intPresentCount = 0;

            for (intI = 0; intI < SHARE_COUNT; intI++)
            {
                arrSharePaths[intI] = strBasePath + ".s" + (intI + 1);
                arrPresent[intI] = System.IO.File.Exists(arrSharePaths[intI]);
                if (arrPresent[intI]) { intPresentCount++; }
            }

            if (intPresentCount < 3) { return null; }

            int[] arrReadFrom = new int[PATTERN_LEN];
            if (!buildReadTable(arrPresent, arrReadFrom)) { return null; }

            FileStream[] arrShares = new FileStream[SHARE_COUNT];
            long[] arrShareSize = new long[SHARE_COUNT];
            long[] arrSharePos = new long[SHARE_COUNT];
            FileStream ptrOutput = null;

            try
            {
                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    if (arrPresent[intI])
                    {
                        arrShares[intI] = new FileStream(arrSharePaths[intI], FileMode.Open, FileAccess.Read);
                        arrShareSize[intI] = arrShares[intI].Length;
                    }
                }

                ptrOutput = new FileStream(strOutputPath_a, FileMode.Create, FileAccess.Write);

                long lngPos = 0;

                while (!blnError)
                {
                    int intRow = (int)(lngPos % PATTERN_LEN);
                    int intShareIdx = arrReadFrom[intRow];

                    if (arrSharePos[intShareIdx] >= arrShareSize[intShareIdx]) { break; }

                    int intByte = arrShares[intShareIdx].ReadByte();
                    if (intByte == -1) { break; }
                    arrSharePos[intShareIdx]++;

                    for (intJ = 0; intJ < 3; intJ++)
                    {
                        int intOther = s_arrPattern[intRow, intJ];
                        if (intOther != intShareIdx && arrPresent[intOther])
                        {
                            if (arrSharePos[intOther] < arrShareSize[intOther])
                            {
                                arrShares[intOther].ReadByte();
                                arrSharePos[intOther]++;
                            }
                        }
                    }

                    ptrOutput.WriteByte((byte)intByte);
                    lngPos++;
                }

                strResult = strOutputPath_a;
            }
            catch
            {
                blnError = true;
            }
            finally
            {
                for (intI = 0; intI < SHARE_COUNT; intI++)
                {
                    if (arrShares[intI] != null) { arrShares[intI].Close(); }
                }
                if (ptrOutput != null) { ptrOutput.Close(); }
            }

            if (blnError)
            {
                if (System.IO.File.Exists(strOutputPath_a))
                {
                    SecureDelete.File(strOutputPath_a);
                }
                strResult = null;
            }

            return strResult;
        }

        // --- Private helpers ---

        private static string buildBasePath(string strSharePath_a)
        {
            string strResult = null;
            int intLen = strSharePath_a.Length;

            if (intLen >= 3 &&
                strSharePath_a[intLen - 3] == '.' &&
                strSharePath_a[intLen - 2] == 's' &&
                strSharePath_a[intLen - 1] >= '1' &&
                strSharePath_a[intLen - 1] <= '5')
            {
                strResult = strSharePath_a.Substring(0, intLen - 3);
            }

            return strResult;
        }

        private static bool buildReadTable(bool[] arrPresent_a, int[] arrReadFrom_a)
        {
            bool blnResult = true;

            for (int intRow = 0; intRow < PATTERN_LEN && blnResult; intRow++)
            {
                bool blnFound = false;

                for (int intJ = 0; intJ < 3 && !blnFound; intJ++)
                {
                    if (arrPresent_a[s_arrPattern[intRow, intJ]])
                    {
                        arrReadFrom_a[intRow] = s_arrPattern[intRow, intJ];
                        blnFound = true;
                    }
                }

                if (!blnFound) { blnResult = false; }
            }

            return blnResult;
        }
    }
}
