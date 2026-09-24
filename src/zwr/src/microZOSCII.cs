// CyborgUnicorn.ZOSCII - MicroZOSCII
// microZOSCII bootstrap encoding: derives a microROM from raw bytes or from
// three 54-character base-62 strings, then encodes/decodes a full ROM as a
// stream of 1-byte address lookups into the microROM nibble table.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.Numerics;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// microZOSCII bootstrap encoding.
    ///
    /// The microROM is a string of hex nibbles (0-F), up to 256 characters.
    /// Each nibble of the full ROM hex is encoded as a 1-byte address: the position
    /// inside the microROM where that nibble value appears (chosen randomly from all
    /// matching positions). Decoding is a simple lookup: microROM[address].
    ///
    /// microROM sizes:
    ///   FromBytes   - exactly 240 nibbles (clamped), derived from any byte array(s)
    ///   FromBase62  - exactly 240 nibbles, from 3 x 54 base-62 strings
    ///
    /// Transmission of a 128KB ROM produces 262,144 1-byte addresses (2x expansion).
    /// </summary>
    public static class MicroZOSCII
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        /// <summary>Maximum microROM size in nibbles (fits in 1-byte address 0-255).</summary>
        public const int MAX_MICRO_ROM_NIBBLES = 256;

        /// <summary>microROM nibbles per base-62 chunk.</summary>
        public const int NIBBLES_PER_CHUNK = 80;

        /// <summary>Base-62 characters per chunk.</summary>
        public const int BASE62_CHARS_PER_CHUNK = 54;

        /// <summary>Number of base-62 chunks for a full human-entry microROM.</summary>
        public const int CHUNK_COUNT = 3;

        private const string BASE62_CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        // -------------------------------------------------------------------------
        // microROM derivation
        // -------------------------------------------------------------------------

        /// <summary>
        /// Derive a 240-nibble microROM from 1 to 4 byte arrays.
        /// Each array contributes hex(bytes) nibbles. Result is clamped to 240 nibbles (3 x 80).
        /// arrBytes1_a is required. arrBytes2_a, arrBytes3_a, arrBytes4_a are optional (pass null to omit).
        /// Returns a 240-character uppercase hex string, or null on error.
        /// </summary>
        public static string FromBytes(byte[] arrBytes1_a, byte[] arrBytes2_a, byte[] arrBytes3_a, byte[] arrBytes4_a)
        {
            string strResult = null;

            try
            {
                if (arrBytes1_a != null && arrBytes1_a.Length > 0)
                {
                    string strMicroROM = bytesToHex(arrBytes1_a);

                    if (arrBytes2_a != null && arrBytes2_a.Length > 0)
                    {
                        strMicroROM += bytesToHex(arrBytes2_a);
                    }

                    if (arrBytes3_a != null && arrBytes3_a.Length > 0)
                    {
                        strMicroROM += bytesToHex(arrBytes3_a);
                    }

                    if (arrBytes4_a != null && arrBytes4_a.Length > 0)
                    {
                        strMicroROM += bytesToHex(arrBytes4_a);
                    }

                    if (strMicroROM.Length > CHUNK_COUNT * NIBBLES_PER_CHUNK)
                    {
                        strMicroROM = strMicroROM.Substring(0, CHUNK_COUNT * NIBBLES_PER_CHUNK);
                    }

                    strResult = strMicroROM;
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Derive a 240-nibble microROM from three 54-character base-62 strings.
        /// Each string is one third of the microROM, base-62 encoded.
        /// The three chunks are decoded and concatenated to produce the 240-nibble microROM.
        /// strChunk1_a, strChunk2_a, strChunk3_a - 54 base-62 characters each.
        /// Returns a 240-character uppercase hex string, or null on error.
        /// </summary>
        public static string FromBase62(string strChunk1_a, string strChunk2_a, string strChunk3_a)
        {
            string strResult = null;

            try
            {
                string[] arrChunks = new string[] { strChunk1_a, strChunk2_a, strChunk3_a };
                bool blnValid = true;
                int intI = 0;

                for (intI = 0; intI < arrChunks.Length; intI++)
                {
                    if (arrChunks[intI] == null || arrChunks[intI].Length != BASE62_CHARS_PER_CHUNK)
                    {
                        blnValid = false;
                    }
                }

                if (blnValid)
                {
                    string strMicroROM = "";

                    for (intI = 0; intI < arrChunks.Length; intI++)
                    {
                        string strChunkHex = Base62ChunkToHex(arrChunks[intI]);

                        if (strChunkHex == null || strChunkHex.Length != NIBBLES_PER_CHUNK)
                        {
                            blnValid = false;
                        }
                        else
                        {
                            strMicroROM += strChunkHex;
                        }
                    }

                    if (blnValid)
                    {
                        strResult = strMicroROM;
                    }
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Encode a 240-nibble microROM as three 54-character base-62 strings.
        /// The microROM is split into three 80-nibble chunks, each base-62 encoded.
        /// strMicroROM_a - 240-character hex microROM string from FromBase62 or equivalent.
        /// Returns string[3] of 54-character base-62 strings, or null on error.
        /// </summary>
        public static string[] ToBase62(string strMicroROM_a)
        {
            string[] arrResult = null;

            try
            {
                if (strMicroROM_a != null && strMicroROM_a.Length == CHUNK_COUNT * NIBBLES_PER_CHUNK)
                {
                    arrResult = new string[CHUNK_COUNT];
                    bool blnValid = true;
                    int intI = 0;

                    for (intI = 0; intI < CHUNK_COUNT; intI++)
                    {
                        string strChunk = strMicroROM_a.Substring(intI * NIBBLES_PER_CHUNK, NIBBLES_PER_CHUNK);
                        string strBase62 = HexChunkToBase62(strChunk);

                        if (strBase62 == null || strBase62.Length != BASE62_CHARS_PER_CHUNK)
                        {
                            blnValid = false;
                        }
                        else
                        {
                            arrResult[intI] = strBase62;
                        }
                    }

                    if (!blnValid)
                    {
                        arrResult = null;
                    }
                }
            }
            catch { }

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // Distribution analysis
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns the instance count of each nibble value 0-F in the microROM.
        /// Useful for validating ROM quality before use — any value with 0 occurrences
        /// cannot be encoded and will cause Encode to fail for ROM bytes containing that nibble.
        /// strMicroROM_a - microROM string from FromBytes or FromBase62.
        /// Returns int[16] where index 0 = count of '0', 1 = count of '1', ... 15 = count of 'F'.
        /// Returns null on error.
        /// </summary>
        public static int[] GetDistribution(string strMicroROM_a)
        {
            int[] arrResult = null;

            try
            {
                if (strMicroROM_a != null && strMicroROM_a.Length > 0)
                {
                    arrResult = new int[16];
                    int intI = 0;

                    for (intI = 0; intI < strMicroROM_a.Length; intI++)
                    {
                        int intVal = hexNibbleValue(strMicroROM_a[intI]);

                        if (intVal >= 0)
                        {
                            arrResult[intVal]++;
                        }
                    }
                }
            }
            catch { }

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // Encode / Decode
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encode a ROM byte array using the microROM.
        /// Each byte of arrROM_a is hex-expanded to 2 nibbles; each nibble is encoded
        /// as a randomly selected 1-byte address into the microROM where that nibble appears.
        /// strMicroROM_a - microROM string from FromBytes or FromBase62.
        /// arrROM_a      - the full ROM bytes to encode.
        /// Returns a byte[] of addresses (2x the ROM size), or null on error.
        /// </summary>
        public static byte[] Encode(string strMicroROM_a, byte[] arrROM_a)
        {
            byte[] arrResult = null;

            try
            {
                if (strMicroROM_a != null && strMicroROM_a.Length > 0
                    && arrROM_a != null && arrROM_a.Length > 0)
                {
                    List<byte>[] arrPositions = buildPositionTable(strMicroROM_a);
                    Random objRandom = createRandomSeed(strMicroROM_a);
                    byte[] arrAddresses = new byte[arrROM_a.Length * 2];
                    int intOut = 0;
                    int intI = 0;

                    for (intI = 0; intI < arrROM_a.Length; intI++)
                    {
                        int intHigh = (arrROM_a[intI] >> 4) & 0x0F;
                        int intLow  =  arrROM_a[intI]       & 0x0F;

                        List<byte> arrHighPos = arrPositions[intHigh];
                        List<byte> arrLowPos  = arrPositions[intLow];

                        if (arrHighPos.Count > 0 && arrLowPos.Count > 0)
                        {
                            arrAddresses[intOut]     = arrHighPos[objRandom.Next(arrHighPos.Count)];
                            arrAddresses[intOut + 1] = arrLowPos [objRandom.Next(arrLowPos.Count)];
                            intOut += 2;
                        }
                    }

                    if (intOut == arrAddresses.Length)
                    {
                        arrResult = arrAddresses;
                    }
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Decode an address array back to ROM bytes using the microROM.
        /// Each address is a 1-byte index into strMicroROM_a; pairs of nibbles reassemble bytes.
        /// strMicroROM_a  - microROM string from FromBytes or FromBase62.
        /// arrAddresses_a - address array from Encode or received from peer.
        /// Returns the decoded ROM byte array, or null on error.
        /// </summary>
        public static byte[] Decode(string strMicroROM_a, byte[] arrAddresses_a)
        {
            byte[] arrResult = null;

            try
            {
                if (strMicroROM_a != null && strMicroROM_a.Length > 0
                    && arrAddresses_a != null && arrAddresses_a.Length >= 2
                    && arrAddresses_a.Length % 2 == 0)
                {
                    bool blnValid = true;
                    int intI = 0;

                    for (intI = 0; intI < arrAddresses_a.Length; intI++)
                    {
                        if (arrAddresses_a[intI] >= strMicroROM_a.Length)
                        {
                            blnValid = false;
                        }
                    }

                    if (blnValid)
                    {
                        byte[] arrBytes = new byte[arrAddresses_a.Length / 2];

                        for (intI = 0; intI < arrBytes.Length; intI++)
                        {
                            int intHigh = hexNibbleValue(strMicroROM_a[arrAddresses_a[intI * 2]]);
                            int intLow  = hexNibbleValue(strMicroROM_a[arrAddresses_a[intI * 2 + 1]]);

                            if (intHigh >= 0 && intLow >= 0)
                            {
                                arrBytes[intI] = (byte)((intHigh << 4) | intLow);
                            }
                            else
                            {
                                blnValid = false;
                            }
                        }

                        if (blnValid)
                        {
                            arrResult = arrBytes;
                        }
                    }
                }
            }
            catch { }

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Create a seeded Random from the microROM content XOR'd with TickCount.
        /// Mirrors UNSIGNAL's createRandomSeed approach.
        /// </summary>
        private static Random createRandomSeed(string strMicroROM_a)
        {
            uint intHash = 0;
            int intI = 0;

            for (intI = 0; intI < strMicroROM_a.Length; intI++)
            {
                intHash = (intHash * 33) + strMicroROM_a[intI];
            }

            return new Random((int)(intHash ^ (uint)Environment.TickCount));
        }

        /// <summary>
        /// Convert a byte array to an uppercase hex string.
        /// </summary>
        private static string bytesToHex(byte[] arrBytes_a)
        {
            string strResult = "";
            int intI = 0;

            for (intI = 0; intI < arrBytes_a.Length; intI++)
            {
                strResult += arrBytes_a[intI].ToString("X2");
            }

            return strResult;
        }

        /// <summary>
        /// Build a position lookup table: for each nibble value 0-F, the list of
        /// positions in strMicroROM_a where that nibble appears.
        /// </summary>
        private static List<byte>[] buildPositionTable(string strMicroROM_a)
        {
            List<byte>[] arrPositions = new List<byte>[16];
            int intI = 0;

            for (intI = 0; intI < 16; intI++)
            {
                arrPositions[intI] = new List<byte>();
            }

            for (intI = 0; intI < strMicroROM_a.Length && intI < MAX_MICRO_ROM_NIBBLES; intI++)
            {
                int intVal = hexNibbleValue(strMicroROM_a[intI]);

                if (intVal >= 0)
                {
                    arrPositions[intVal].Add((byte)intI);
                }
            }

            return arrPositions;
        }

        /// <summary>
        /// Return the integer value 0-15 for a hex character, or -1 if invalid.
        /// </summary>
        private static int hexNibbleValue(char chrNibble_a)
        {
            int intResult = -1;

            if (chrNibble_a >= '0' && chrNibble_a <= '9')
            {
                intResult = chrNibble_a - '0';
            }
            else if (chrNibble_a >= 'A' && chrNibble_a <= 'F')
            {
                intResult = chrNibble_a - 'A' + 10;
            }
            else if (chrNibble_a >= 'a' && chrNibble_a <= 'f')
            {
                intResult = chrNibble_a - 'a' + 10;
            }

            return intResult;
        }

        /// <summary>
        /// Convert a 54-character base-62 string to an 80-nibble uppercase hex string.
        /// Treats the base-62 string as a base-62 number, converts to base-16, pads to 80 chars.
        /// </summary>
        public static string Base62ChunkToHex(string strBase62_a)
        {
            string strResult = null;

            try
            {
                BigInteger objValue = BigInteger.Zero;
                int intI = 0;

                for (intI = 0; intI < strBase62_a.Length; intI++)
                {
                    int intDigit = BASE62_CHARS.IndexOf(strBase62_a[intI]);

                    if (intDigit < 0)
                    {
                        return null;
                    }

                    objValue = objValue * 62 + intDigit;
                }

                string strHex = objValue.ToString("X");

                // Pad to exactly 80 nibbles
                while (strHex.Length < NIBBLES_PER_CHUNK)
                {
                    strHex = "0" + strHex;
                }

                // BigInteger.ToString("X") can produce a leading zero making it 81 chars — trim
                if (strHex.Length > NIBBLES_PER_CHUNK)
                {
                    strHex = strHex.Substring(strHex.Length - NIBBLES_PER_CHUNK);
                }

                if (strHex.Length == NIBBLES_PER_CHUNK)
                {
                    strResult = strHex;
                }
            }
            catch { }

            return strResult;
        }

        /// <summary>
        /// Convert an 80-nibble uppercase hex string to a 54-character base-62 string.
        /// Treats the hex string as a base-16 number, converts to base-62, pads to 54 chars.
        /// </summary>
        public static string HexChunkToBase62(string strHex_a)
        {
            string strResult = null;

            try
            {
                BigInteger objValue = BigInteger.Zero;
                int intI = 0;

                for (intI = 0; intI < strHex_a.Length; intI++)
                {
                    int intNibble = hexNibbleValue(strHex_a[intI]);

                    if (intNibble < 0)
                    {
                        return null;
                    }

                    objValue = objValue * 16 + intNibble;
                }

                string strBase62 = "";

                while (objValue > 0)
                {
                    int intRemainder = (int)(objValue % 62);
                    strBase62 = BASE62_CHARS[intRemainder] + strBase62;
                    objValue /= 62;
                }

                // Pad to exactly 54 characters
                while (strBase62.Length < BASE62_CHARS_PER_CHUNK)
                {
                    strBase62 = BASE62_CHARS[0] + strBase62;
                }

                if (strBase62.Length == BASE62_CHARS_PER_CHUNK)
                {
                    strResult = strBase62;
                }
            }
            catch { }

            return strResult;
        }
    }
}