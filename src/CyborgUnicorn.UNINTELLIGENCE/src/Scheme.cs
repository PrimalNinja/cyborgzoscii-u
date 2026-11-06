// CyborgUnicorn.ZOSCII - Schemes
// A scheme is an ordered list of steps, inner to outer. Each step names an encoding, 1..3 ROM ids,
// optional Tango (where the encoding supports it) and an optional integrity check applied to that
// step's input before it encodes and verified after it decodes.
// The nuget runs schemes; it never resolves ROM ids itself. The caller supplies an ISchemeRomResolver
// that returns the raw bytes of a ROM by id (a published ROM file, a JPEG, anything). Byte encodings
// load those bytes as a ZOSCIIRom; micro encodings take the leading nibbles (256 for microZOSCII,
// 512 for microUNSIGNAL / microBRICS).
// No serialisation here: applications store schemes in their own format.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.2

using System;
using System.Collections.Generic;

namespace CyborgUnicorn.ZOSCII
{
    public static class SchemeEncoding
    {
        public const string ZOSCII = "ZOSCII";
        public const string UNSIGNAL = "UNSIGNAL";
        public const string BRICS = "BRICS";
        public const string MICROZOSCII = "MICROZOSCII";
        public const string MICROUNSIGNAL = "MICROUNSIGNAL";
        public const string MICROBRICS = "MICROBRICS";

        public static readonly string[] All = { ZOSCII, UNSIGNAL, BRICS, MICROZOSCII, MICROUNSIGNAL, MICROBRICS };

        public static bool IsMicro(string strEncoding_a)
        {
            return strEncoding_a == MICROZOSCII || strEncoding_a == MICROUNSIGNAL || strEncoding_a == MICROBRICS;
        }

        public static bool SupportsTango(string strEncoding_a)
        {
            return strEncoding_a == ZOSCII || strEncoding_a == UNSIGNAL;
        }

        public static int MicroNibbles(string strEncoding_a)
        {
            int intResult = 0;
            if (strEncoding_a == MICROZOSCII) { intResult = 256; }
            else if (strEncoding_a == MICROUNSIGNAL || strEncoding_a == MICROBRICS) { intResult = 512; }
            return intResult;
        }
    }

    public static class SchemeIntegrity
    {
        public const string NONE = "NONE";
        public const string ROLLINGHASH = "ROLLINGHASH";
        public const string ROLLINGHASH_CRC32 = "ROLLINGHASH+CRC32";
    }

    public sealed class SchemeStep
    {
        public string Encoding = SchemeEncoding.UNSIGNAL;
        public List<string> RomIds = new List<string>();
        public bool Tango = false;
        public string Integrity = SchemeIntegrity.NONE;

        public SchemeStep Clone()
        {
            SchemeStep objStep = new SchemeStep();
            objStep.Encoding = Encoding;
            objStep.RomIds = new List<string>(RomIds);
            objStep.Tango = Tango;
            objStep.Integrity = Integrity;
            return objStep;
        }
    }

    public sealed class Scheme
    {
        public string Id = "";
        public string Name = "";
        public List<SchemeStep> Steps = new List<SchemeStep>();

        /// <summary>Every distinct ROM id referenced by any step, in first-use order.</summary>
        public List<string> RomIds()
        {
            List<string> arrResult = new List<string>();
            for (int intS = 0; intS < Steps.Count; intS++)
            {
                for (int intR = 0; intR < Steps[intS].RomIds.Count; intR++)
                {
                    if (!arrResult.Contains(Steps[intS].RomIds[intR])) { arrResult.Add(Steps[intS].RomIds[intR]); }
                }
            }
            return arrResult;
        }

        public Scheme Clone(string strNewId_a, string strNewName_a)
        {
            Scheme objScheme = new Scheme();
            objScheme.Id = strNewId_a;
            objScheme.Name = strNewName_a;
            for (int intS = 0; intS < Steps.Count; intS++) { objScheme.Steps.Add(Steps[intS].Clone()); }
            return objScheme;
        }

        /// <summary>Null when the scheme is runnable, else the first problem found.</summary>
        public string Validate()
        {
            string strResult = null;
            if (Steps.Count == 0) { strResult = "a scheme needs at least one step"; }
            for (int intS = 0; strResult == null && intS < Steps.Count; intS++)
            {
                SchemeStep objStep = Steps[intS];
                if (Array.IndexOf(SchemeEncoding.All, objStep.Encoding) < 0) { strResult = "step " + (intS + 1) + ": unknown encoding " + objStep.Encoding; }
                else if (objStep.RomIds.Count < 1 || objStep.RomIds.Count > 3) { strResult = "step " + (intS + 1) + ": 1 to 3 ROMs"; }
                else if (objStep.Tango && (!SchemeEncoding.SupportsTango(objStep.Encoding) || objStep.RomIds.Count < 2)) { strResult = "step " + (intS + 1) + ": Tango needs ZOSCII or UNSIGNAL with 2 or 3 ROMs"; }
                else if (objStep.Integrity != SchemeIntegrity.NONE && objStep.Integrity != SchemeIntegrity.ROLLINGHASH && objStep.Integrity != SchemeIntegrity.ROLLINGHASH_CRC32) { strResult = "step " + (intS + 1) + ": unknown integrity " + objStep.Integrity; }
            }
            return strResult;
        }
    }

    public interface ISchemeRomResolver
    {
        /// <summary>Raw bytes of the ROM with this id, or null when unknown.</summary>
        byte[] GetRomBytes(string strRomId_a);
    }

    /// <summary>Runs a scheme. Encode applies steps first to last (inner to outer); Decode reverses them.</summary>
    public static class SchemeRunner
    {
        public static string LastError = "";

        public static byte[] Encode(byte[] arrInput_a, Scheme objScheme_a, ISchemeRomResolver objResolver_a)
        {
            byte[] arrResult = null;
            LastError = "";
            string strProblem = objScheme_a == null ? "no scheme" : objScheme_a.Validate();
            if (strProblem != null) { LastError = strProblem; }
            else if (arrInput_a == null || objResolver_a == null) { LastError = "no input or resolver"; }
            else
            {
                arrResult = arrInput_a;
                for (int intS = 0; arrResult != null && intS < objScheme_a.Steps.Count; intS++)
                {
                    arrResult = encodeStep(arrResult, objScheme_a.Steps[intS], objResolver_a, intS + 1);
                }
            }
            return arrResult;
        }

        public static byte[] Decode(byte[] arrInput_a, Scheme objScheme_a, ISchemeRomResolver objResolver_a)
        {
            byte[] arrResult = null;
            LastError = "";
            string strProblem = objScheme_a == null ? "no scheme" : objScheme_a.Validate();
            if (strProblem != null) { LastError = strProblem; }
            else if (arrInput_a == null || objResolver_a == null) { LastError = "no input or resolver"; }
            else
            {
                arrResult = arrInput_a;
                for (int intS = objScheme_a.Steps.Count - 1; arrResult != null && intS >= 0; intS--)
                {
                    arrResult = decodeStep(arrResult, objScheme_a.Steps[intS], objResolver_a, intS + 1);
                }
            }
            return arrResult;
        }

        // --- integrity framing: [rollinghash 4][crc32 4]? + payload ---

        private static byte[] wrapIntegrity(byte[] arrPayload_a, string strIntegrity_a)
        {
            byte[] arrResult = arrPayload_a;
            if (strIntegrity_a == SchemeIntegrity.ROLLINGHASH || strIntegrity_a == SchemeIntegrity.ROLLINGHASH_CRC32)
            {
                byte[] arrHash = arrPayload_a.Length > 0 ? ZRollingHash.Bytes(arrPayload_a) : new byte[4];
                byte[] arrCrc = strIntegrity_a == SchemeIntegrity.ROLLINGHASH_CRC32 ? ZCRC32.Bytes(arrPayload_a) : new byte[0];
                arrResult = new byte[arrHash.Length + arrCrc.Length + arrPayload_a.Length];
                Array.Copy(arrHash, 0, arrResult, 0, arrHash.Length);
                Array.Copy(arrCrc, 0, arrResult, arrHash.Length, arrCrc.Length);
                Array.Copy(arrPayload_a, 0, arrResult, arrHash.Length + arrCrc.Length, arrPayload_a.Length);
            }
            return arrResult;
        }

        private static byte[] unwrapIntegrity(byte[] arrBlock_a, string strIntegrity_a, int intStep_a)
        {
            byte[] arrResult = arrBlock_a;
            if (strIntegrity_a == SchemeIntegrity.ROLLINGHASH || strIntegrity_a == SchemeIntegrity.ROLLINGHASH_CRC32)
            {
                int intHead = strIntegrity_a == SchemeIntegrity.ROLLINGHASH_CRC32 ? 8 : 4;
                arrResult = null;
                if (arrBlock_a.Length >= intHead)
                {
                    byte[] arrHash = new byte[4];
                    byte[] arrPayload = new byte[arrBlock_a.Length - intHead];
                    Array.Copy(arrBlock_a, 0, arrHash, 0, 4);
                    Array.Copy(arrBlock_a, intHead, arrPayload, 0, arrPayload.Length);
                    bool blnOk = arrPayload.Length == 0 ? isZero(arrHash) : ZRollingHash.Verify(arrPayload, arrHash);
                    if (blnOk && intHead == 8)
                    {
                        byte[] arrCrc = new byte[4];
                        Array.Copy(arrBlock_a, 4, arrCrc, 0, 4);
                        blnOk = ZCRC32.Verify(arrPayload, arrCrc);
                    }
                    if (blnOk) { arrResult = arrPayload; } else { LastError = "step " + intStep_a + ": integrity check failed"; }
                }
                else { LastError = "step " + intStep_a + ": too short for integrity header"; }
            }
            return arrResult;
        }

        private static bool isZero(byte[] arr_a)
        {
            bool blnResult = true;
            for (int intI = 0; intI < arr_a.Length; intI++) { if (arr_a[intI] != 0) { blnResult = false; } }
            return blnResult;
        }

        // --- steps ---

        private static byte[] encodeStep(byte[] arrInput_a, SchemeStep objStep_a, ISchemeRomResolver objResolver_a, int intStep_a)
        {
            byte[] arrResult = null;
            byte[] arrBlock = wrapIntegrity(arrInput_a, objStep_a.Integrity);
            if (SchemeEncoding.IsMicro(objStep_a.Encoding))
            {
                MicroRom[] arrRoms = loadMicroRoms(objStep_a, objResolver_a, intStep_a);
                if (arrRoms != null)
                {
                    if (objStep_a.Encoding == SchemeEncoding.MICROZOSCII)
                    {
                        arrResult = arrBlock;
                        for (int intR = 0; arrResult != null && intR < arrRoms.Length; intR++) { arrResult = MicroZOSCII.Encode(arrRoms[intR].ToHex(), arrResult); }
                    }
                    else if (objStep_a.Encoding == SchemeEncoding.MICROUNSIGNAL) { arrResult = MUEncode.Chain(arrBlock, arrRoms); }
                    else { arrResult = MBEncode.Chain(arrBlock, arrRoms); }
                }
            }
            else
            {
                ZOSCIIRom[] arrRoms = loadByteRoms(objStep_a, objResolver_a, intStep_a);
                if (arrRoms != null)
                {
                    try
                    {
                        if (objStep_a.Encoding == SchemeEncoding.ZOSCII) { arrResult = ZEncode.Chain(arrBlock, arrRoms, objStep_a.Tango); }
                        else if (objStep_a.Encoding == SchemeEncoding.UNSIGNAL) { arrResult = UEncode.Chain(arrBlock, arrRoms, objStep_a.Tango); }
                        else { arrResult = BEncode.Chain(arrBlock, arrRoms); }
                    }
                    finally { disposeRoms(arrRoms); }
                }
            }
            if (arrResult == null && LastError.Length == 0) { LastError = "step " + intStep_a + ": encode failed (ROM lacks a required value?)"; }
            return arrResult;
        }

        private static byte[] decodeStep(byte[] arrInput_a, SchemeStep objStep_a, ISchemeRomResolver objResolver_a, int intStep_a)
        {
            byte[] arrBlock = null;
            if (SchemeEncoding.IsMicro(objStep_a.Encoding))
            {
                MicroRom[] arrRoms = loadMicroRoms(objStep_a, objResolver_a, intStep_a);
                if (arrRoms != null)
                {
                    if (objStep_a.Encoding == SchemeEncoding.MICROZOSCII)
                    {
                        arrBlock = arrInput_a;
                        for (int intR = arrRoms.Length - 1; arrBlock != null && intR >= 0; intR--) { arrBlock = MicroZOSCII.Decode(arrRoms[intR].ToHex(), arrBlock); }
                    }
                    else if (objStep_a.Encoding == SchemeEncoding.MICROUNSIGNAL) { arrBlock = MUDecode.Chain(arrInput_a, arrRoms); }
                    else { arrBlock = MBDecode.Chain(arrInput_a, arrRoms); }
                }
            }
            else
            {
                ZOSCIIRom[] arrRoms = loadByteRoms(objStep_a, objResolver_a, intStep_a);
                if (arrRoms != null)
                {
                    try
                    {
                        if (objStep_a.Encoding == SchemeEncoding.ZOSCII) { arrBlock = ZDecode.Chain(arrInput_a, arrRoms, objStep_a.Tango); }
                        else if (objStep_a.Encoding == SchemeEncoding.UNSIGNAL) { arrBlock = UDecode.Chain(arrInput_a, arrRoms, objStep_a.Tango); }
                        else { arrBlock = BDecode.Chain(arrInput_a, arrRoms); }
                    }
                    finally { disposeRoms(arrRoms); }
                }
            }
            byte[] arrResult = null;
            if (arrBlock == null) { if (LastError.Length == 0) { LastError = "step " + intStep_a + ": decode failed"; } }
            else { arrResult = unwrapIntegrity(arrBlock, objStep_a.Integrity, intStep_a); }
            return arrResult;
        }

        private static ZOSCIIRom[] loadByteRoms(SchemeStep objStep_a, ISchemeRomResolver objResolver_a, int intStep_a)
        {
            ZOSCIIRom[] arrResult = new ZOSCIIRom[objStep_a.RomIds.Count];
            bool blnOk = true;
            for (int intR = 0; blnOk && intR < arrResult.Length; intR++)
            {
                byte[] arrBytes = objResolver_a.GetRomBytes(objStep_a.RomIds[intR]);
                if (arrBytes == null) { blnOk = false; LastError = "step " + intStep_a + ": ROM " + objStep_a.RomIds[intR] + " not available"; }
                else
                {
                    arrResult[intR] = ZOSCIIRom.FromBytes(arrBytes);
                    if (!arrResult[intR].IsLoaded) { blnOk = false; LastError = "step " + intStep_a + ": ROM " + objStep_a.RomIds[intR] + " could not be loaded"; }
                }
            }
            if (!blnOk) { disposeRoms(arrResult); arrResult = null; }
            return arrResult;
        }

        private static MicroRom[] loadMicroRoms(SchemeStep objStep_a, ISchemeRomResolver objResolver_a, int intStep_a)
        {
            MicroRom[] arrResult = new MicroRom[objStep_a.RomIds.Count];
            bool blnOk = true;
            int intNibbles = SchemeEncoding.MicroNibbles(objStep_a.Encoding);
            for (int intR = 0; blnOk && intR < arrResult.Length; intR++)
            {
                byte[] arrBytes = objResolver_a.GetRomBytes(objStep_a.RomIds[intR]);
                if (arrBytes == null) { blnOk = false; LastError = "step " + intStep_a + ": ROM " + objStep_a.RomIds[intR] + " not available"; }
                else
                {
                    arrResult[intR] = MicroRom.FromBytes(arrBytes, intNibbles);
                    if (!arrResult[intR].IsLoaded) { blnOk = false; LastError = "step " + intStep_a + ": ROM " + objStep_a.RomIds[intR] + " gives no nibbles"; }
                }
            }
            if (!blnOk) { arrResult = null; }
            return arrResult;
        }

        private static void disposeRoms(ZOSCIIRom[] arrRoms_a)
        {
            if (arrRoms_a != null)
            {
                for (int intR = 0; intR < arrRoms_a.Length; intR++) { if (arrRoms_a[intR] != null) { arrRoms_a[intR].Dispose(); } }
            }
        }
    }

    /// <summary>Standard CRC-32 (IEEE 802.3, reflected, poly 0xEDB88320), computed bitwise: no lookup table.</summary>
    public static class ZCRC32
    {
        public static uint Compute(byte[] arrInput_a)
        {
            uint intCrc = 0xFFFFFFFF;
            if (arrInput_a != null)
            {
                for (int intI = 0; intI < arrInput_a.Length; intI++)
                {
                    intCrc ^= arrInput_a[intI];
                    for (int intB = 0; intB < 8; intB++)
                    {
                        uint intMask = (uint)-(int)(intCrc & 1);
                        intCrc = (intCrc >> 1) ^ (0xEDB88320 & intMask);
                    }
                }
            }
            return ~intCrc;
        }

        /// <summary>4 bytes, little-endian.</summary>
        public static byte[] Bytes(byte[] arrInput_a)
        {
            uint intCrc = Compute(arrInput_a);
            return new byte[] { (byte)(intCrc & 0xFF), (byte)((intCrc >> 8) & 0xFF), (byte)((intCrc >> 16) & 0xFF), (byte)((intCrc >> 24) & 0xFF) };
        }

        public static bool Verify(byte[] arrInput_a, byte[] arrCrc_a)
        {
            bool blnResult = false;
            if (arrCrc_a != null && arrCrc_a.Length == 4)
            {
                byte[] arrWanted = Bytes(arrInput_a);
                blnResult = arrWanted[0] == arrCrc_a[0] && arrWanted[1] == arrCrc_a[1] && arrWanted[2] == arrCrc_a[2] && arrWanted[3] == arrCrc_a[3];
            }
            return blnResult;
        }
    }
}
