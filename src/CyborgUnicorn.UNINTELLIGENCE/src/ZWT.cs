// CyborgUnicorn.ZOSCII - ZWT (ZOSCII Web Tokens)
// Quantum-proof, opaque session/attestation token — the JWT analogue for ZOSCII.
// An issuer attests a user to a relying party. Unlike JWT the whole token is
// UNSIGNAL-encoded (I(M;A)=0): payload, signatures and verification structure
// are indistinguishable from noise. No asymmetric primitive — nothing for Shor.
//
// KEYS
//   SHAREDROM  - held by issuer + relying party (per-relationship key)
//   ISSUERROM  - held by issuer only (never shared)
//
// CONSTRUCTION (spec v0.2)
//   issuersignature = sharedsignature
//   sharedstuff     = UEncode( SHAREDROM, sharedFrame )
//   issuerdata      = UEncode( ISSUERROM1, UEncode( ISSUERROM2, issuerFrame ) )   // double-encoded, optional
//   zwt             = lenheader( sharedstuff.Length ) + sharedstuff [ + issuerdata ]
//
// The lenheader is the 32-bit little-endian length of the encoded sharedstuff, each byte
// concealed as a SHAREDROM address (4 slots / 8 wire bytes). The reader dereferences it to
// find where sharedstuff ends; everything after is issuerdata (empty = bare token). issuerdata
// is double-encoded and APPENDED (never wrapped a third time) and is OPTIONAL: omit the issuer
// ROMs at Issue() for a bare same-party token needing only SHAREDROM.
//
// FRAME (flat, little-endian; readable on Z80 / 6502 / C / C# / Python with base+offset):
//   [ hash 4 ][ version 1 ][ len 2 LE per field EXCEPT the last ][ blobs in fixed order ]
//   The last field carries NO length — it runs from the previous field's end to the end of
//   the block. The 4-byte rolling hash (CRC) leads and covers everything after it (version +
//   length table + blobs).
//   issuerFrame  : [ issuersignature(=sharedsig) , privateclaims ]  (privateclaims = last field)
//   sharedFrame  : [ sharedsignature , sharedclaims ]               (sharedclaims  = last field)
//   issuerdata (the double-UNSIGNAL-encoded issuerFrame) is NOT a frame field — it is
//   concatenated after the encoded sharedstuff. Double encoding removes any known-plaintext
//   foothold on privateclaims, since the relying party already holds the plaintext sharedsignature.
//
// VERIFICATION
//   Relying party : Open(zwt, SHAREDROM) -> reads sharedsig + sharedclaims
//   Issuer        : Introspect(issuersignature, ISSUERROM) -> reads sharedsig + privateclaims,
//                   then confirms the presented sharedsig equals the sealed copy.
//
// CLAIMS VISIBILITY
//   sharedclaims  - readable by BOTH parties (both hold SHAREDROM). The issuer
//                   authors them; the issuer already knows them. They live in the
//                   SHAREDROM envelope only and are deliberately NOT sealed inside
//                   issuersignature — sealing shared-readable data under the
//                   issuer's private ROM would serve no purpose. An RP editing its
//                   own copy of shared data under its own half of a per-relationship
//                   key affects nothing but its own door (a non-event), exactly like
//                   an RP-forged shared-sig in the spec's Notes.
//   privateclaims - readable only by the issuer; sealed inside issuersignature
//                   (ISSUERROM envelope), never exposed to the RP.
//
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    // -------------------------------------------------------------------------
    // ZWTResult
    // -------------------------------------------------------------------------

    /// <summary>
    /// Result of a ZWT operation. Success flag plus whichever fields the
    /// operation produced. On failure, Success is false and Error carries
    /// the reason; all byte fields are left at their defaults.
    /// </summary>
    public class ZWTResult
    {
        // --- Fields ---

        private bool m_blnSuccess = false;
        private string m_strError = "";
        private byte[] m_arrToken = null;
        private byte[] m_arrIssuerSignature = null;
        private byte[] m_arrSharedSignature = null;
        private byte[] m_arrSharedClaims = null;
        private byte[] m_arrPrivateClaims = null;

        // --- Properties ---

        public bool Success
        {
            get { return m_blnSuccess; }
            set { m_blnSuccess = value; }
        }

        public string Error
        {
            get { return m_strError; }
            set { m_strError = (value == null) ? "" : value; }
        }

        /// <summary>The complete opaque ZWT (SHAREDROM envelope). Set by Issue().</summary>
        public byte[] Token
        {
            get { return m_arrToken; }
            set { m_arrToken = value; }
        }

        /// <summary>The issuer-sealed signature (ISSUERROM envelope). Set by Issue().</summary>
        public byte[] IssuerSignature
        {
            get { return m_arrIssuerSignature; }
            set { m_arrIssuerSignature = value; }
        }

        /// <summary>The shared signature (GUID-like nonce). Set by Issue()/Open()/Introspect().</summary>
        public byte[] SharedSignature
        {
            get { return m_arrSharedSignature; }
            set { m_arrSharedSignature = value; }
        }

        /// <summary>Relying-party-readable claims. Set by Open().</summary>
        public byte[] SharedClaims
        {
            get { return m_arrSharedClaims; }
            set { m_arrSharedClaims = value; }
        }

        /// <summary>Issuer-only claims. Set by Introspect().</summary>
        public byte[] PrivateClaims
        {
            get { return m_arrPrivateClaims; }
            set { m_arrPrivateClaims = value; }
        }
    }

    // -------------------------------------------------------------------------
    // ZWT
    // -------------------------------------------------------------------------

    /// <summary>
    /// ZOSCII Web Tokens. Static operations for issuing, opening and introspecting
    /// opaque attestation tokens.
    ///
    ///   Issue      - issuer side, needs ISSUERROM + SHAREDROM
    ///   Open       - relying-party side, needs SHAREDROM
    ///   Introspect - issuer side, needs ISSUERROM
    ///   Verify     - convenience for a party holding BOTH ROMs (issuer-local)
    /// </summary>
    public static class ZWT
    {
        // --- Constants ---

        // Flat cross-platform frame (Z80 / 6502 / C / C# / Python — all little-endian, base+offset):
        //
        //   offset 0        : rolling hash (CRC)  4 bytes  — ZRollingHash over bytes [4 .. end]
        //   offset 4        : version             1 byte   — 0
        //   offset 5        : len field[0]        2 bytes LE
        //   offset 7        : len field[1]        2 bytes LE
        //   offset ...      : len field[i]        2 bytes LE   (one per field, fixed count per version)
        //   then            : field blobs back-to-back, in fixed known order, each up to 65535 bytes
        //
        // No pointers, no field-count byte, no type tags: the segment schema is known by both
        // parties per version. The version byte lets future versions append segments without
        // breaking v0 readers. The hash covers version + length table + all blobs (everything
        // after the 4-byte hash), so version and lengths are integrity-protected.
        private const int HASH_LEN = 4;     // rolling hash / CRC width
        private const int VERSION_LEN = 1;  // version byte
        private const int LEN_FIELD = 2;    // per-field length, 16-bit LE (full 64KB per segment)
        private const byte VERSION_0 = 0;   // current frame version

        // Length indirection: the encoded-sharedstuff length is written as 4 bytes (32-bit LE),
        // each byte concealed as a 2-byte SHAREDROM address whose dereferenced ROM value is that
        // byte. 4 slots / 8 wire bytes at the front of the token. The reader dereferences it to
        // find where sharedstuff ends; everything after is issuerdata (empty = bare token).
        private const int LEN_SLOTS = 4;         // 32-bit length
        private const int LEN_HEADER_BYTES = 8;  // 4 slots x 2 bytes

        // --- Issue ---

        /// <summary>
        /// Issue a ZWT. Issuer holds all three ROMs.
        /// arrSharedSignature_a - GUID-like nonce (use NewSignature() for one).
        /// arrPrivateClaims_a   - issuer-only claims (may be null/empty).
        /// arrSharedClaims_a    - relying-party-readable claims (may be null/empty).
        /// objIssuerRom1_a      - ISSUERROM1 (issuer-only, outer of the double-encode).
        /// objIssuerRom2_a      - ISSUERROM2 (issuer-only, inner of the double-encode).
        /// objSharedRom_a       - SHAREDROM (per-relationship).
        /// Returns a ZWTResult with Token, IssuerSignature (= issuerdata) and SharedSignature on success.
        /// </summary>
        public static ZWTResult Issue(
            byte[] arrSharedSignature_a,
            byte[] arrPrivateClaims_a,
            byte[] arrSharedClaims_a,
            ZOSCIIRom objIssuerRom1_a,
            ZOSCIIRom objIssuerRom2_a,
            ZOSCIIRom objSharedRom_a)
        {
            ZWTResult objResult = new ZWTResult();
            bool blnWantIssuer = false;

            blnWantIssuer = (objIssuerRom1_a != null || objIssuerRom2_a != null);

            if (arrSharedSignature_a == null || arrSharedSignature_a.Length == 0)
            {
                objResult.Error = "sharedsignature is required";
            }
            else if (objSharedRom_a == null || !objSharedRom_a.IsLoaded)
            {
                objResult.Error = "SHAREDROM is not loaded";
            }
            else if (blnWantIssuer && (objIssuerRom1_a == null || !objIssuerRom1_a.IsLoaded))
            {
                objResult.Error = "ISSUERROM1 is not loaded";
            }
            else if (blnWantIssuer && (objIssuerRom2_a == null || !objIssuerRom2_a.IsLoaded))
            {
                objResult.Error = "ISSUERROM2 is not loaded";
            }
            else
            {
                byte[] arrPrivate = normalise(arrPrivateClaims_a);
                byte[] arrShared = normalise(arrSharedClaims_a);
                byte[] arrIssuerData = new byte[0];
                bool blnOk = true;

                // Build issuerdata only when issuer ROMs were supplied.
                if (blnWantIssuer)
                {
                    // Issuer block: [hash][version][len issuersig][issuersig(=sharedsig)][privateclaims]
                    byte[][] arrIssuerFields = new byte[2][];
                    arrIssuerFields[0] = arrSharedSignature_a;
                    arrIssuerFields[1] = arrPrivate;

                    byte[] arrIssuerFrame = buildFrame(arrIssuerFields);

                    if (arrIssuerFrame == null)
                    {
                        objResult.Error = "failed to build issuer frame";
                        blnOk = false;
                    }
                    else
                    {
                        // issuerdata = UEncode( ISSUERROM1, UEncode( ISSUERROM2, issuerframe ) )
                        byte[] arrIssuerInner = UEncode.Bytes(arrIssuerFrame, objIssuerRom2_a);
                        byte[] arrDouble = (arrIssuerInner == null) ? null : UEncode.Bytes(arrIssuerInner, objIssuerRom1_a);

                        if (arrDouble == null)
                        {
                            objResult.Error = "ISSUERROM encode failed";
                            blnOk = false;
                        }
                        else
                        {
                            arrIssuerData = arrDouble;
                        }
                    }
                }

                if (blnOk)
                {
                    // Shared block: [hash][version][len sharedsig][sharedsig][sharedclaims]
                    // (issuerdata is NOT a field here - it is concatenated after the encoded blob).
                    byte[][] arrSharedFields = new byte[2][];
                    arrSharedFields[0] = arrSharedSignature_a;
                    arrSharedFields[1] = arrShared;

                    byte[] arrSharedFrame = buildFrame(arrSharedFields);

                    if (arrSharedFrame == null)
                    {
                        objResult.Error = "failed to build shared frame";
                    }
                    else
                    {
                        byte[] arrSharedEncoded = UEncode.Bytes(arrSharedFrame, objSharedRom_a);

                        if (arrSharedEncoded == null)
                        {
                            objResult.Error = "SHAREDROM encode failed";
                        }
                        else
                        {
                            byte[] arrLenHeader = encodeLen(objSharedRom_a, arrSharedEncoded.Length);

                            if (arrLenHeader == null)
                            {
                                objResult.Error = "failed to encode length header";
                            }
                            else
                            {
                                // Token = lenheader + sharedEncoded + issuerdata (issuerdata may be empty).
                                byte[] arrToken = new byte[arrLenHeader.Length + arrSharedEncoded.Length + arrIssuerData.Length];
                                Array.Copy(arrLenHeader, 0, arrToken, 0, arrLenHeader.Length);
                                Array.Copy(arrSharedEncoded, 0, arrToken, arrLenHeader.Length, arrSharedEncoded.Length);
                                Array.Copy(arrIssuerData, 0, arrToken, arrLenHeader.Length + arrSharedEncoded.Length, arrIssuerData.Length);

                                objResult.Token = arrToken;
                                objResult.IssuerSignature = arrIssuerData;
                                objResult.SharedSignature = arrSharedSignature_a;
                                objResult.Success = true;
                            }
                        }
                    }
                }
            }

            return objResult;
        }

        // --- Open (relying party) ---

        /// <summary>
        /// Open a ZWT with SHAREDROM. Relying-party side.
        /// Recovers sharedsignature, issuersignature and sharedclaims, and verifies
        /// the outer integrity binding. Does NOT prove issuer authenticity — that
        /// requires Introspect() with ISSUERROM (issuer-local) or issuer introspection.
        /// Returns a ZWTResult with SharedSignature, IssuerSignature and SharedClaims on success.
        /// </summary>
        public static ZWTResult Open(byte[] arrToken_a, ZOSCIIRom objSharedRom_a)
        {
            ZWTResult objResult = new ZWTResult();
            int intSharedLen = 0;

            if (arrToken_a == null || arrToken_a.Length == 0)
            {
                objResult.Error = "token is empty";
            }
            else if (objSharedRom_a == null || !objSharedRom_a.IsLoaded)
            {
                objResult.Error = "SHAREDROM is not loaded";
            }
            else
            {
                // Read the 32-bit sharedstuff length from the front via ROM indirection.
                intSharedLen = decodeLen(objSharedRom_a, arrToken_a, 0);

                if (intSharedLen < 0)
                {
                    objResult.Error = "failed to read length header";
                }
                else if (LEN_HEADER_BYTES + intSharedLen > arrToken_a.Length)
                {
                    objResult.Error = "length header exceeds token size";
                }
                else
                {
                    // Split: sharedstuff of exactly intSharedLen bytes, then the remainder = issuerdata.
                    byte[] arrSharedEncoded = new byte[intSharedLen];
                    byte[] arrIssuerData = new byte[arrToken_a.Length - LEN_HEADER_BYTES - intSharedLen];
                    Array.Copy(arrToken_a, LEN_HEADER_BYTES, arrSharedEncoded, 0, intSharedLen);
                    Array.Copy(arrToken_a, LEN_HEADER_BYTES + intSharedLen, arrIssuerData, 0, arrIssuerData.Length);

                    byte[] arrFrame = UDecode.Bytes(arrSharedEncoded, objSharedRom_a);

                    if (arrFrame == null)
                    {
                        objResult.Error = "SHAREDROM decode failed";
                    }
                    else
                    {
                        byte[][] arrFields = parseFrame(arrFrame, 2);

                        if (arrFields == null)
                        {
                            objResult.Error = "malformed or tampered token (integrity check failed)";
                        }
                        else
                        {
                            objResult.SharedSignature = arrFields[0];
                            objResult.SharedClaims = arrFields[1];
                            objResult.IssuerSignature = arrIssuerData;   // issuerdata — opaque to RP, for introspection (empty = bare token)
                            objResult.Success = true;
                        }
                    }
                }
            }

            return objResult;
        }

		/// <summary>
		/// Update shared claims in an existing ZWT.
		/// Anyone with SHAREDROM (issuer or RP) can do this. The issuerdata tail is preserved
		/// verbatim (re-appended unchanged), so issuer attestation is untouched.
		/// </summary>
		public static ZWTResult UpdateSharedClaims(
			byte[] arrToken_a,
			byte[] arrNewSharedClaims_a,
			ZOSCIIRom objSharedRom_a)
		{
			ZWTResult objResult = new ZWTResult();

			ZWTResult objOpen = Open(arrToken_a, objSharedRom_a);

			if (!objOpen.Success)
			{
				objResult.Error = objOpen.Error;
			}
			else
			{
				// Rebuild the shared frame with the new claims (same sharedsignature).
				byte[][] arrSharedFields = new byte[2][];
				arrSharedFields[0] = objOpen.SharedSignature;
				arrSharedFields[1] = normalise(arrNewSharedClaims_a);

				byte[] arrSharedFrame = buildFrame(arrSharedFields);

				if (arrSharedFrame == null)
				{
					objResult.Error = "failed to rebuild shared frame";
				}
				else
				{
					byte[] arrSharedEncoded = UEncode.Bytes(arrSharedFrame, objSharedRom_a);

					if (arrSharedEncoded == null)
					{
						objResult.Error = "SHAREDROM encode failed";
					}
					else
					{
						byte[] arrLenHeader = encodeLen(objSharedRom_a, arrSharedEncoded.Length);

						if (arrLenHeader == null)
						{
							objResult.Error = "failed to encode length header";
						}
						else
						{
							// Preserve the original issuerdata tail verbatim.
							byte[] arrIssuerData = objOpen.IssuerSignature;
							if (arrIssuerData == null)
							{
								arrIssuerData = new byte[0];
							}

							byte[] arrNewToken = new byte[arrLenHeader.Length + arrSharedEncoded.Length + arrIssuerData.Length];
							Array.Copy(arrLenHeader, 0, arrNewToken, 0, arrLenHeader.Length);
							Array.Copy(arrSharedEncoded, 0, arrNewToken, arrLenHeader.Length, arrSharedEncoded.Length);
							Array.Copy(arrIssuerData, 0, arrNewToken, arrLenHeader.Length + arrSharedEncoded.Length, arrIssuerData.Length);

							objResult.Success = true;
							objResult.Token = arrNewToken;
							objResult.SharedSignature = objOpen.SharedSignature;
							objResult.SharedClaims = arrSharedFields[1];
							objResult.IssuerSignature = arrIssuerData;
						}
					}
				}
			}

			return objResult;
		}

        // --- Introspect (issuer) ---

        /// <summary>
        /// Introspect issuerdata with ISSUERROM1 and ISSUERROM2. Issuer side.
        /// Reverses the double UNSIGNAL encoding (UDecode ISSUERROM1, then UDecode ISSUERROM2),
        /// parses the issuer block, verifies its integrity binding, and confirms the sealed
        /// issuersignature (== sharedsignature) matches the one presented (typically the
        /// sharedsignature the relying party read via Open()).
        /// Returns a ZWTResult with SharedSignature and PrivateClaims on success.
        /// </summary>
        public static ZWTResult Introspect(
            byte[] arrIssuerData_a,
            byte[] arrPresentedSharedSignature_a,
            ZOSCIIRom objIssuerRom1_a,
            ZOSCIIRom objIssuerRom2_a)
        {
            ZWTResult objResult = new ZWTResult();

            if (arrIssuerData_a == null || arrIssuerData_a.Length == 0)
            {
                objResult.Error = "issuerdata is empty";
            }
            else if (arrPresentedSharedSignature_a == null || arrPresentedSharedSignature_a.Length == 0)
            {
                objResult.Error = "presented sharedsignature is required";
            }
            else if (objIssuerRom1_a == null || !objIssuerRom1_a.IsLoaded)
            {
                objResult.Error = "ISSUERROM1 is not loaded";
            }
            else if (objIssuerRom2_a == null || !objIssuerRom2_a.IsLoaded)
            {
                objResult.Error = "ISSUERROM2 is not loaded";
            }
            else
            {
                // Reverse the double encoding: outer layer was ISSUERROM1, inner was ISSUERROM2.
                byte[] arrInner = UDecode.Bytes(arrIssuerData_a, objIssuerRom1_a);
                byte[] arrFrameBytes = (arrInner == null) ? null : UDecode.Bytes(arrInner, objIssuerRom2_a);

                if (arrFrameBytes == null)
                {
                    objResult.Error = "ISSUERROM decode failed";
                }
                else
                {
                    byte[][] arrFields = parseFrame(arrFrameBytes, 2);

                    if (arrFields == null)
                    {
                        objResult.Error = "malformed or tampered issuerdata (integrity check failed)";
                    }
                    else
                    {
                        byte[] arrSealedSharedSig = arrFields[0];

                        if (!constantTimeEquals(arrSealedSharedSig, arrPresentedSharedSignature_a))
                        {
                            objResult.Error = "sharedsignature does not match the sealed copy";
                        }
                        else
                        {
                            objResult.SharedSignature = arrSealedSharedSig;
                            objResult.PrivateClaims = arrFields[1];
                            objResult.Success = true;
                        }
                    }
                }
            }

            return objResult;
        }

        // --- Verify (issuer-local convenience) ---

        /// <summary>
        /// Convenience verifier for a party holding ALL THREE ROMs (issuer-local, e.g.
        /// the issuer validating a token it minted, or a test harness). Opens the token with
        /// SHAREDROM, then introspects the recovered issuerdata with ISSUERROM1 and ISSUERROM2
        /// and confirms the sealed sharedsignature matches. A relying party with only SHAREDROM
        /// cannot call this — it lacks the issuer ROMs, which is the whole point (unforgeable
        /// without the issuer's private ROM pair).
        /// Returns a ZWTResult with SharedSignature, SharedClaims and PrivateClaims on success.
        /// </summary>
        public static ZWTResult Verify(
            byte[] arrToken_a,
            ZOSCIIRom objSharedRom_a,
            ZOSCIIRom objIssuerRom1_a,
            ZOSCIIRom objIssuerRom2_a)
        {
            ZWTResult objResult = new ZWTResult();

            ZWTResult objOpen = Open(arrToken_a, objSharedRom_a);

            if (!objOpen.Success)
            {
                objResult.Error = objOpen.Error;
            }
            else if (objOpen.IssuerSignature == null || objOpen.IssuerSignature.Length == 0)
            {
                // Bare token: no issuer attestation present. Shared parts verify by decoding alone.
                objResult.SharedSignature = objOpen.SharedSignature;
                objResult.SharedClaims = objOpen.SharedClaims;
                objResult.PrivateClaims = new byte[0];
                objResult.Success = true;
            }
            else
            {
                ZWTResult objIntro = Introspect(objOpen.IssuerSignature, objOpen.SharedSignature, objIssuerRom1_a, objIssuerRom2_a);

                if (!objIntro.Success)
                {
                    objResult.Error = objIntro.Error;
                }
                else
                {
                    objResult.SharedSignature = objOpen.SharedSignature;
                    objResult.SharedClaims = objOpen.SharedClaims;
                    objResult.PrivateClaims = objIntro.PrivateClaims;
                    objResult.Success = true;
                }
            }

            return objResult;
        }

        // --- Helpers ---

        /// <summary>
        /// Generate a fresh GUID-based sharedsignature (16 bytes).
        /// </summary>
        public static byte[] NewSignature()
        {
            byte[] arrResult = null;

            try
            {
                arrResult = Guid.NewGuid().ToByteArray();
            }
            catch { }

            return arrResult;
        }

        /// <summary>UTF-8 encode a claims string. Returns empty array on null.</summary>
        public static byte[] ClaimsFromString(string strClaims_a)
        {
            byte[] arrResult = new byte[0];

            try
            {
                if (strClaims_a != null && strClaims_a.Length > 0)
                {
                    arrResult = Encoding.UTF8.GetBytes(strClaims_a);
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>UTF-8 decode a claims byte array. Returns empty string on null.</summary>
        public static string ClaimsToString(byte[] arrClaims_a)
        {
            string strResult = "";

            try
            {
                if (arrClaims_a != null && arrClaims_a.Length > 0)
                {
                    strResult = Encoding.UTF8.GetString(arrClaims_a);
                }
            }
            catch { }

            return strResult;
        }

        // -------------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------------

        // --- Length indirection (32-bit LE, concealed as ROM slots) ---

        // Encode a 32-bit length as LEN_SLOTS ROM address slots against objRom_a.
        // Each length byte -> a 2-byte address whose ROM value is that byte. Returns 8 bytes, or null.
        private static byte[] encodeLen(ZOSCIIRom objRom_a, int intValue_a)
        {
            byte[] arrResult = null;
            byte[] arrRom = null;
            byte[] arrOut = null;
            int intI = 0;
            int intByte = 0;
            int intAddr = 0;
            bool blnOk = true;

            try
            {
                arrRom = objRom_a.GetRomData().ptrROMData;
                arrOut = new byte[LEN_HEADER_BYTES];

                for (intI = 0; intI < LEN_SLOTS && blnOk; intI++)
                {
                    intByte = (intValue_a >> (intI * 8)) & 0xFF;
                    intAddr = findRomByte(arrRom, (byte)intByte);

                    if (intAddr < 0)
                    {
                        blnOk = false;
                    }
                    else
                    {
                        arrOut[intI * 2] = (byte)(intAddr & 0xFF);
                        arrOut[(intI * 2) + 1] = (byte)((intAddr >> 8) & 0xFF);
                    }
                }

                if (blnOk)
                {
                    arrResult = arrOut;
                }
            }
            catch
            {
                arrResult = null;
            }

            return arrResult;
        }

        // Decode LEN_SLOTS address slots at intOffset_a back to a 32-bit length. Returns -1 on failure.
        private static int decodeLen(ZOSCIIRom objRom_a, byte[] arrBytes_a, int intOffset_a)
        {
            int intResult = -1;
            byte[] arrRom = null;
            long lngRomSize = 0;
            int intValue = 0;
            int intI = 0;
            int intAddr = 0;
            bool blnOk = true;

            try
            {
                arrRom = objRom_a.GetRomData().ptrROMData;
                lngRomSize = arrRom.Length;

                if (arrBytes_a != null && arrBytes_a.Length >= intOffset_a + LEN_HEADER_BYTES)
                {
                    for (intI = 0; intI < LEN_SLOTS && blnOk; intI++)
                    {
                        intAddr = (arrBytes_a[intOffset_a + (intI * 2)] & 0xFF)
                                | ((arrBytes_a[intOffset_a + (intI * 2) + 1] & 0xFF) << 8);

                        if (intAddr >= lngRomSize)
                        {
                            blnOk = false;
                        }
                        else
                        {
                            intValue = intValue | ((arrRom[intAddr] & 0xFF) << (intI * 8));
                        }
                    }

                    if (blnOk)
                    {
                        intResult = intValue;
                    }
                }
            }
            catch
            {
                intResult = -1;
            }

            return intResult;
        }

        // Scan the first 64KB of the ROM for an address whose byte equals byTarget_a.
        // Returns a 16-bit address, or -1 if the byte does not occur in that window.
        private static int findRomByte(byte[] arrRom_a, byte byTarget_a)
        {
            int intResult = -1;
            int intWindow = 0;
            int intI = 0;

            intWindow = arrRom_a.Length;
            if (intWindow > 65536)
            {
                intWindow = 65536;
            }

            for (intI = 0; intI < intWindow && intResult < 0; intI++)
            {
                if (arrRom_a[intI] == byTarget_a)
                {
                    intResult = intI;
                }
            }

            return intResult;
        }

        private static byte[] normalise(byte[] arrInput_a)
        {
            byte[] arrResult = arrInput_a;

            if (arrResult == null)
            {
                arrResult = new byte[0];
            }

            return arrResult;
        }

        /// <summary>
        /// Build a version-0 flat frame:
        ///   [hash 4][version 1][len0 2 LE]...[len(N-2) 2 LE][blob0]...[blobN-1]
        /// A 2-byte LE length is written for every field EXCEPT the last: the last field runs
        /// from where the previous field ends to the end of the block, so it needs no length.
        /// The hash is the 4-byte reverse BRAINLESS rolling hash over everything after it
        /// (version + length table + all blobs). Every measured field is capped at 65535 bytes
        /// so its length fits the 16-bit LE slot — a full 64KB per segment. Returns null on failure.
        /// </summary>
        private static byte[] buildFrame(byte[][] arrFields_a)
        {
            byte[] arrResult = null;

            try
            {
                int intFieldCount = arrFields_a.Length;
                byte[][] arrNorm = new byte[intFieldCount][];
                int intI = 0;
                bool blnOk = true;

                for (intI = 0; intI < intFieldCount; intI++)
                {
                    arrNorm[intI] = normalise(arrFields_a[intI]);

                    // Only fields that carry a length entry are capped (all but the last).
                    if (intI < intFieldCount - 1 && arrNorm[intI].Length > 0xFFFF)
                    {
                        blnOk = false;
                    }
                }

                if (blnOk)
                {
                    // Body = everything the hash covers: version + length table + blobs.
                    using (MemoryStream objBody = new MemoryStream())
                    {
                        objBody.WriteByte(VERSION_0);

                        // Length table: one entry per field EXCEPT the last.
                        for (intI = 0; intI < intFieldCount - 1; intI++)
                        {
                            byte[] arrLen = lenToBytes(arrNorm[intI].Length);
                            objBody.Write(arrLen, 0, arrLen.Length);
                        }

                        // Blobs in fixed order (the last runs to end of block).
                        for (intI = 0; intI < intFieldCount; intI++)
                        {
                            objBody.Write(arrNorm[intI], 0, arrNorm[intI].Length);
                        }

                        byte[] arrBody = objBody.ToArray();
                        byte[] arrHash = ZRollingHash.Bytes(arrBody, false);

                        if (arrHash != null && arrHash.Length == HASH_LEN)
                        {
                            using (MemoryStream objFrame = new MemoryStream())
                            {
                                objFrame.Write(arrHash, 0, arrHash.Length);
                                objFrame.Write(arrBody, 0, arrBody.Length);
                                arrResult = objFrame.ToArray();
                            }
                        }
                    }
                }
            }
            catch
            {
                arrResult = null;
            }

            return arrResult;
        }

        /// <summary>
        /// Parse a version-0 flat frame produced by buildFrame. Verifies the leading hash over
        /// everything after it, checks the version, reads (intExpectedFields_a - 1) 16-bit LE
        /// lengths, and slices that many measured blobs in order; the final field is whatever
        /// remains of the body after them (it carries no length). Returns the fields, or null
        /// on any integrity / version / format failure.
        /// </summary>
        private static byte[][] parseFrame(byte[] arrFrame_a, int intExpectedFields_a)
        {
            byte[][] arrResult = null;

            try
            {
                int intMeasured = intExpectedFields_a - 1;   // all fields but the last carry a length
                int intHeaderLen = HASH_LEN + VERSION_LEN + (intMeasured * LEN_FIELD);

                if (arrFrame_a != null && intExpectedFields_a >= 1 && arrFrame_a.Length >= intHeaderLen)
                {
                    int intBodyLen = arrFrame_a.Length - HASH_LEN;

                    byte[] arrHash = new byte[HASH_LEN];
                    byte[] arrBody = new byte[intBodyLen];

                    Array.Copy(arrFrame_a, 0, arrHash, 0, HASH_LEN);
                    Array.Copy(arrFrame_a, HASH_LEN, arrBody, 0, intBodyLen);

                    if (ZRollingHash.Verify(arrBody, arrHash, false) && arrBody[0] == VERSION_0)
                    {
                        // Read the (N-1) measured lengths.
                        int[] arrLens = new int[intExpectedFields_a];
                        long lngMeasured = 0;
                        int intPos = VERSION_LEN;
                        int intI = 0;

                        for (intI = 0; intI < intMeasured; intI++)
                        {
                            arrLens[intI] = bytesToLen(arrBody, intPos);
                            intPos += LEN_FIELD;
                            lngMeasured += arrLens[intI];
                        }

                        // The measured blobs must fit within the body; the last field takes the rest.
                        if (intPos + lngMeasured <= arrBody.Length)
                        {
                            arrLens[intExpectedFields_a - 1] = (int)(arrBody.Length - intPos - lngMeasured);

                            byte[][] arrFields = new byte[intExpectedFields_a][];

                            for (intI = 0; intI < intExpectedFields_a; intI++)
                            {
                                arrFields[intI] = new byte[arrLens[intI]];
                                Array.Copy(arrBody, intPos, arrFields[intI], 0, arrLens[intI]);
                                intPos += arrLens[intI];
                            }

                            arrResult = arrFields;
                        }
                    }
                }
            }
            catch
            {
                arrResult = null;
            }

            return arrResult;
        }

        /// <summary>Write a length (0..65535) as 2 bytes, little-endian.</summary>
        private static byte[] lenToBytes(int intValue_a)
        {
            byte[] arrResult = new byte[LEN_FIELD];

            arrResult[0] = (byte)(intValue_a & 0xFF);
            arrResult[1] = (byte)((intValue_a >> 8) & 0xFF);

            return arrResult;
        }

        /// <summary>Read a 2-byte little-endian length.</summary>
        private static int bytesToLen(byte[] arrBytes_a, int intOffset_a)
        {
            int intResult = 0;

            intResult = (arrBytes_a[intOffset_a] & 0xFF)
                      | ((arrBytes_a[intOffset_a + 1] & 0xFF) << 8);

            return intResult;
        }

        /// <summary>
        /// Length-independent-branch byte comparison. Compares full contents without
        /// early-exiting on the first mismatch, so timing does not leak match position.
        /// </summary>
        private static bool constantTimeEquals(byte[] arrA_a, byte[] arrB_a)
        {
            bool blnResult = false;

            if (arrA_a != null && arrB_a != null && arrA_a.Length == arrB_a.Length)
            {
                int intDiff = 0;
                int intI = 0;

                for (intI = 0; intI < arrA_a.Length; intI++)
                {
                    intDiff |= arrA_a[intI] ^ arrB_a[intI];
                }

                blnResult = (intDiff == 0);
            }

            return blnResult;
        }
    }
}