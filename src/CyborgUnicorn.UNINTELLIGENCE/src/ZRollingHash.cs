// CyborgUnicorn.ZOSCII - ZRollingHash
// BRAINLESS rolling hash — 4-pass XOR chain, 32-bit output.
// Forward (blnStreamable_a=true):  seed from first byte of each stride, walk forward.
// Reverse (blnStreamable_a=false): seed from last byte of each stride, walk backward.
//                                  Entire payload required before processing begins.
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

using System;
using System.IO;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// BRAINLESS rolling hash — 4-pass, 32-bit output.
    /// blnStreamable_a = true  → forward BRAINLESS.
    /// blnStreamable_a = false → reverse BRAINLESS, requires complete payload.
    /// </summary>
    public static class ZRollingHash
    {
        private const int PASSES = 4;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Generate a 4-byte (32-bit) hash from a byte array.
        /// blnStreamable_a - true = forward, false = reverse (default).
        /// Returns null on failure.
        /// </summary>
        public static byte[] Bytes(byte[] arrInput_a, bool blnStreamable_a = false)
        {
            byte[] arrResult = null;

            try
            {
                if (arrInput_a != null && arrInput_a.Length > 0)
                {
                    arrResult = blnStreamable_a
                        ? computeForward(arrInput_a)
                        : computeReverse(arrInput_a);
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Generate a 4-byte (32-bit) hash from a file.
        /// blnStreamable_a - true = forward, false = reverse (default).
        /// Returns null on failure.
        /// </summary>
        public static byte[] File(string strPath_a, bool blnStreamable_a = false)
        {
            byte[] arrResult = null;

            try
            {
                if (strPath_a != null && System.IO.File.Exists(strPath_a))
                {
                    byte[] arrInput = System.IO.File.ReadAllBytes(strPath_a);
                    arrResult = Bytes(arrInput, blnStreamable_a);
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Verify a byte array against a previously generated hash.
        /// blnStreamable_a must match the value used during Bytes().
        /// Returns true if hash matches.
        /// </summary>
        public static bool Verify(byte[] arrInput_a, byte[] arrHash_a, bool blnStreamable_a = false)
        {
            bool blnResult = false;

            try
            {
                if (arrInput_a != null && arrHash_a != null && arrHash_a.Length == PASSES)
                {
                    byte[] arrGenerated = Bytes(arrInput_a, blnStreamable_a);

                    if (arrGenerated != null)
                    {
                        int intDiff = 0;
                        int intI    = 0;

                        for (intI = 0; intI < PASSES; intI++)
                        {
                            intDiff |= arrGenerated[intI] ^ arrHash_a[intI];
                        }

                        blnResult = (intDiff == 0);
                    }
                }
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Verify a file against a previously generated hash.
        /// blnStreamable_a must match the value used during File().
        /// Returns true if hash matches.
        /// </summary>
        public static bool VerifyFile(string strPath_a, byte[] arrHash_a, bool blnStreamable_a = false)
        {
            bool blnResult = false;

            try
            {
                if (strPath_a != null && arrHash_a != null && System.IO.File.Exists(strPath_a))
                {
                    byte[] arrInput = System.IO.File.ReadAllBytes(strPath_a);
                    blnResult = Verify(arrInput, arrHash_a, blnStreamable_a);
                }
            }
            catch { }

            return blnResult;
        }

        // -------------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------------

        private static byte[] computeForward(byte[] arrInput_a)
        {
            byte[] arrHash = new byte[PASSES];
            int    intPass = 0;

            for (intPass = 0; intPass < PASSES; intPass++)
            {
                int  intI     = intPass;
                byte byState  = 0;
                byte byFirst  = 0;
                bool blnFirst = true;

                while (intI < arrInput_a.Length)
                {
                    if (blnFirst)
                    {
                        byState  = arrInput_a[intI];
                        byFirst  = byState;
                        blnFirst = false;
                    }
                    else
                    {
                        byState = (byte)(byState ^ arrInput_a[intI]);
                    }

                    intI += PASSES;
                }

                // Circular: last result XORs back into first
                arrHash[intPass] = (byte)(byFirst ^ byState);
            }

            return arrHash;
        }

        private static byte[] computeReverse(byte[] arrInput_a)
        {
            byte[] arrHash = new byte[PASSES];
            int    intPass = 0;

            for (intPass = 0; intPass < PASSES; intPass++)
            {
                // Find last index of this stride
                int intLast = arrInput_a.Length - 1;

                while (intLast >= 0 && (intLast % PASSES) != intPass)
                {
                    intLast--;
                }

                if (intLast >= 0)
                {
                    int  intI     = intLast;
                    byte byState  = 0;
                    byte byLast   = 0;
                    bool blnFirst = true;

                    while (intI >= 0)
                    {
                        if ((intI % PASSES) == intPass)
                        {
                            if (blnFirst)
                            {
                                byState  = arrInput_a[intI];
                                byLast   = byState;
                                blnFirst = false;
                            }
                            else
                            {
                                byState = (byte)(byState ^ arrInput_a[intI]);
                            }
                        }

                        intI--;
                    }

                    // Circular: last result XORs back into last
                    arrHash[intPass] = (byte)(byLast ^ byState);
                }
            }

            return arrHash;
        }
    }
}