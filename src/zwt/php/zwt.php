<?php
// CyborgUnicorn.ZOSCII - Zwt (PHP 5.4+)
// ZOSCII Web Tokens: quantum-proof opaque attestation tokens.
//
// KEYS
//   SHAREDROM              - issuer + relying party
//   ISSUERROM1, ISSUERROM2 - issuer only (issuer block is double-encoded)
//
// CONSTRUCTION
//   issuersignature = sharedsignature
//   sharedstuff     = Unsignal::encode(SHAREDROM, frame[sharedsig, sharedclaims])
//   issuerdata      = Unsignal::encode(ISSUERROM1, Unsignal::encode(ISSUERROM2, frame[sharedsig, privateclaims]))
//   zwt             = lenheader(strlen(sharedstuff)) + sharedstuff [+ issuerdata]
//
//   lenheader is the 32-bit little-endian length of sharedstuff, each byte concealed as a
//   SHAREDROM address (4 slots / 8 wire bytes). The reader dereferences it to find where
//   sharedstuff ends; everything after that is issuerdata (empty = bare token, no attestation).
//   issuerdata is double-encoded and appended, never wrapped a third time. It is optional:
//   omit the issuer ROMs at issue() for a bare same-party token.
//
// FRAME (flat, little-endian, base+offset readable on any platform):
//   [ hash 4 ][ version 1 ][ len 2 LE per field EXCEPT the last ][ blobs in order ]
//   The last field carries no length and runs to the end of the block.
//   issuerFrame : [ issuersignature(=sharedsig) , privateclaims ]
//   sharedFrame : [ sharedsignature , sharedclaims , issuerdata ]
//
// Results are associative arrays: ['success'=>bool, 'error'=>str, ...].
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1

require_once 'Zoscii.php';
require_once 'Unsignal.php';

class Zwt
{
    const HASH_LEN = 4;
    const VERSION_LEN = 1;
    const LEN_FIELD = 2;
    const VERSION_0 = 0;
    const HASH_PASSES = 4;

    // --- Rolling hash (reverse BRAINLESS, 4 passes) - byte-compatible with ZRollingHash ---

    private static function rollingHash($strBytes_a)
    {
        $strHash = '';
        $intPass = 0;
        $intLen = 0;
        $intLast = 0;
        $intI = 0;
        $intState = 0;
        $intFirstVal = 0;
        $blnFirst = true;

        $intLen = strlen($strBytes_a);

        for ($intPass = 0; $intPass < self::HASH_PASSES; $intPass++)
        {
            $intLast = $intLen - 1;

            while ($intLast >= 0 && ($intLast % self::HASH_PASSES) != $intPass)
            {
                $intLast--;
            }

            $intState = 0;
            $intFirstVal = 0;

            if ($intLast >= 0)
            {
                $intI = $intLast;
                $blnFirst = true;

                while ($intI >= 0)
                {
                    if (($intI % self::HASH_PASSES) == $intPass)
                    {
                        if ($blnFirst)
                        {
                            $intState = ord($strBytes_a[$intI]);
                            $intFirstVal = $intState;
                            $blnFirst = false;
                        }
                        else
                        {
                            $intState = $intState ^ ord($strBytes_a[$intI]);
                        }
                    }

                    $intI--;
                }

                $strHash .= chr(($intFirstVal ^ $intState) & 0xFF);
            }
            else
            {
                $strHash .= chr(0);
            }
        }

        return $strHash;
    }

    // --- Frame build / parse ---

    // Build [hash][version][len per field except last][blobs]. Returns bytes, or false on failure.
    private static function buildFrame($arrFields_a)
    {
        $strResult = false;
        $strBody = '';
        $intCount = 0;
        $intI = 0;
        $strField = '';
        $blnOk = true;

        $intCount = count($arrFields_a);

        // Cap every field that carries a length (all but the last) at 65535.
        for ($intI = 0; $intI < $intCount - 1 && $blnOk; $intI++)
        {
            if (strlen($arrFields_a[$intI]) > 0xFFFF)
            {
                $blnOk = false;
            }
        }

        if ($blnOk)
        {
            $strBody = chr(self::VERSION_0);

            for ($intI = 0; $intI < $intCount - 1; $intI++)
            {
                $strBody .= pack('v', strlen($arrFields_a[$intI]));
            }

            for ($intI = 0; $intI < $intCount; $intI++)
            {
                $strBody .= $arrFields_a[$intI];
            }

            $strResult = self::rollingHash($strBody) . $strBody;
        }

        return $strResult;
    }

    // Parse a frame with intFields_a expected fields. Returns array of field strings, or false.
    private static function parseFrame($strFrame_a, $intFields_a)
    {
        $arrResult = false;
        $intMeasured = 0;
        $intHeaderLen = 0;
        $strHash = '';
        $strBody = '';
        $lngBodyLen = 0;
        $arrLens = array();
        $lngMeasured = 0;
        $lngPos = 0;
        $intI = 0;
        $arrData = null;
        $arrFields = array();
        $blnOk = true;

        $intMeasured = $intFields_a - 1;
        $intHeaderLen = self::HASH_LEN + self::VERSION_LEN + ($intMeasured * self::LEN_FIELD);

        if ($strFrame_a !== false && $intFields_a >= 1 && strlen($strFrame_a) >= $intHeaderLen)
        {
            $strHash = substr($strFrame_a, 0, self::HASH_LEN);
            $strBody = substr($strFrame_a, self::HASH_LEN);
            $lngBodyLen = strlen($strBody);

            // Integrity + version.
            if ($strHash === self::rollingHash($strBody) && ord($strBody[0]) == self::VERSION_0)
            {
                $lngPos = self::VERSION_LEN;

                for ($intI = 0; $intI < $intMeasured; $intI++)
                {
                    $arrData = unpack('v', substr($strBody, $lngPos, self::LEN_FIELD));
                    $arrLens[$intI] = $arrData[1];
                    $lngPos += self::LEN_FIELD;
                    $lngMeasured += $arrLens[$intI];
                }

                // The measured blobs must fit; the last field takes the remainder.
                if ($lngPos + $lngMeasured <= $lngBodyLen)
                {
                    $arrLens[$intFields_a - 1] = $lngBodyLen - $lngPos - $lngMeasured;

                    for ($intI = 0; $intI < $intFields_a; $intI++)
                    {
                        $arrFields[$intI] = substr($strBody, $lngPos, $arrLens[$intI]);
                        $lngPos += $arrLens[$intI];
                    }

                    $arrResult = $arrFields;
                }
            }
        }

        return $arrResult;
    }

    // --- Public helpers ---

    // Fresh 16-byte signature.
    public static function newSignature()
    {
        $strResult = '';
        $intI = 0;

        for ($intI = 0; $intI < 16; $intI++)
        {
            $strResult .= chr(rand(0, 255));
        }

        return $strResult;
    }

    // Constant-value byte comparison is not required here (see notes); plain compare is used.
    private static function bytesEqual($strA_a, $strB_a)
    {
        return ($strA_a === $strB_a);
    }

    // --- Length indirection (32-bit LE, concealed as ROM slots) ---
    // The sharedstuff length is written as 4 bytes (b0..b3 little-endian), each byte
    // encoded as a 2-byte SHAREDROM address whose dereferenced ROM value is that byte.
    // 4 slots = 8 wire bytes at the very front of the token. This conceals the split
    // point behind the same indirection as the rest of ZOSCII (no plaintext length).

    const LEN_SLOTS = 4;               // 32-bit length
    const LEN_HEADER_BYTES = 8;        // 4 slots x 2 bytes

    // Encode a 32-bit length as LEN_SLOTS SHAREDROM address slots. Returns 8 bytes, or false.
    private static function encodeLen($objRom_a, $intValue_a)
    {
        $strResult = false;
        $strOut = '';
        $intI = 0;
        $intByte = 0;
        $intAddr = 0;
        $blnOk = true;

        for ($intI = 0; $intI < self::LEN_SLOTS && $blnOk; $intI++)
        {
            $intByte = ($intValue_a >> ($intI * 8)) & 0xFF;
            $intAddr = $objRom_a->findAddress($intByte);

            if ($intAddr < 0)
            {
                $blnOk = false;
            }
            else
            {
                $strOut .= pack('v', $intAddr);
            }
        }

        if ($blnOk)
        {
            $strResult = $strOut;
        }

        return $strResult;
    }

    // Decode LEN_SLOTS address slots at $intOffset_a back to a 32-bit length. Returns int, or -1.
    private static function decodeLen($objRom_a, $strBytes_a, $intOffset_a)
    {
        $intResult = -1;
        $intValue = 0;
        $intI = 0;
        $intAddr = 0;
        $arrData = null;
        $blnOk = true;

        if (strlen($strBytes_a) >= $intOffset_a + self::LEN_HEADER_BYTES)
        {
            for ($intI = 0; $intI < self::LEN_SLOTS && $blnOk; $intI++)
            {
                $arrData = unpack('v', substr($strBytes_a, $intOffset_a + ($intI * 2), 2));
                $intAddr = $arrData[1];

                if ($intAddr >= $objRom_a->lngRomSize)
                {
                    $blnOk = false;
                }
                else
                {
                    $intValue = $intValue | (ord($objRom_a->strRomData[$intAddr]) << ($intI * 8));
                }
            }

            if ($blnOk)
            {
                $intResult = $intValue;
            }
        }

        return $intResult;
    }

    private static function normalise($strValue_a)
    {
        $strResult = $strValue_a;

        if ($strResult === null)
        {
            $strResult = '';
        }

        return $strResult;
    }

    // --- Operations ---

    // Issue a ZWT.
    //   Full token (issuer attestation): pass all three ROMs -> issuerdata is appended.
    //   Bare token (same-party, no attestation): pass null for the issuer ROMs -> no issuerdata.
    // Structure: [len header (sharedstuff size, 32-bit LE via indirection)] + sharedEncoded [+ issuerdata]
    // Returns ['success','error','token','issuerData','sharedSig'].
    public static function issue($strSharedSig_a, $strPrivateClaims_a, $strSharedClaims_a,
                                 $objIssuer1_a, $objIssuer2_a, $objShared_a)
    {
        $arrResult = array('success' => false, 'error' => '', 'token' => '',
                           'issuerData' => '', 'sharedSig' => '');
        $strPrivate = '';
        $strShared = '';
        $strIssuerFrame = false;
        $strIssuerInner = false;
        $strIssuerData = '';
        $strSharedFrame = false;
        $strSharedEncoded = false;
        $strLenHeader = false;
        $blnWantIssuer = false;

        $blnWantIssuer = ($objIssuer1_a !== null || $objIssuer2_a !== null);

        if ($strSharedSig_a === null || strlen($strSharedSig_a) == 0)
        {
            $arrResult['error'] = 'sharedsignature is required';
        }
        else if ($objShared_a === null || !$objShared_a->isLoaded())
        {
            $arrResult['error'] = 'SHAREDROM is not loaded';
        }
        else if ($blnWantIssuer && ($objIssuer1_a === null || !$objIssuer1_a->isLoaded()))
        {
            $arrResult['error'] = 'ISSUERROM1 is not loaded';
        }
        else if ($blnWantIssuer && ($objIssuer2_a === null || !$objIssuer2_a->isLoaded()))
        {
            $arrResult['error'] = 'ISSUERROM2 is not loaded';
        }
        else
        {
            $strPrivate = self::normalise($strPrivateClaims_a);
            $strShared = self::normalise($strSharedClaims_a);

            // Build issuerdata only when issuer ROMs were supplied.
            if ($blnWantIssuer)
            {
                // Issuer frame: [issuersignature(=sharedsig), privateclaims]
                $strIssuerFrame = self::buildFrame(array($strSharedSig_a, $strPrivate));

                if ($strIssuerFrame === false)
                {
                    $arrResult['error'] = 'failed to build issuer frame';
                }
                else
                {
                    // Double-encode: inner ISSUERROM2, outer ISSUERROM1.
                    $strIssuerInner = Unsignal::encode($objIssuer2_a, $strIssuerFrame);
                    if ($strIssuerInner !== false)
                    {
                        $strIssuerData = Unsignal::encode($objIssuer1_a, $strIssuerInner);
                    }
                    else
                    {
                        $strIssuerData = false;
                    }

                    if ($strIssuerData === false)
                    {
                        $arrResult['error'] = 'ISSUERROM encode failed';
                    }
                }
            }

            if ($arrResult['error'] == '')
            {
                // Shared frame: [sharedsignature, sharedclaims]  (issuerdata is NOT inside it)
                $strSharedFrame = self::buildFrame(array($strSharedSig_a, $strShared));

                if ($strSharedFrame === false)
                {
                    $arrResult['error'] = 'failed to build shared frame';
                }
                else
                {
                    $strSharedEncoded = Unsignal::encode($objShared_a, $strSharedFrame);

                    if ($strSharedEncoded === false)
                    {
                        $arrResult['error'] = 'SHAREDROM encode failed';
                    }
                    else
                    {
                        // Front: 32-bit length of the encoded sharedstuff, via ROM indirection.
                        $strLenHeader = self::encodeLen($objShared_a, strlen($strSharedEncoded));

                        if ($strLenHeader === false)
                        {
                            $arrResult['error'] = 'failed to encode length header';
                        }
                        else
                        {
                            // Token = lenheader + sharedEncoded + issuerdata (issuerdata may be '').
                            $arrResult['token'] = $strLenHeader . $strSharedEncoded . $strIssuerData;
                            $arrResult['issuerData'] = $strIssuerData;
                            $arrResult['sharedSig'] = $strSharedSig_a;
                            $arrResult['success'] = true;
                        }
                    }
                }
            }
        }

        return $arrResult;
    }

    // Open a ZWT (relying party, SHAREDROM).
    // Reads the length header by indirection, splits off sharedstuff, decodes it, and takes
    // any remaining bytes as issuerdata (empty = bare token, no issuer attestation).
    // Returns ['success','error','sharedSig','sharedClaims','issuerData'].
    public static function open($strToken_a, $objShared_a)
    {
        $arrResult = array('success' => false, 'error' => '', 'sharedSig' => '',
                           'sharedClaims' => '', 'issuerData' => '');
        $intSharedLen = 0;
        $strSharedEncoded = '';
        $strIssuerData = '';
        $strFrame = false;
        $arrFields = false;
        $lngTokenLen = 0;

        if ($strToken_a === null || strlen($strToken_a) == 0)
        {
            $arrResult['error'] = 'token is empty';
        }
        else if ($objShared_a === null || !$objShared_a->isLoaded())
        {
            $arrResult['error'] = 'SHAREDROM is not loaded';
        }
        else
        {
            $lngTokenLen = strlen($strToken_a);

            // Read the 32-bit sharedstuff length from the front via ROM indirection.
            $intSharedLen = self::decodeLen($objShared_a, $strToken_a, 0);

            if ($intSharedLen < 0)
            {
                $arrResult['error'] = 'failed to read length header';
            }
            else if (self::LEN_HEADER_BYTES + $intSharedLen > $lngTokenLen)
            {
                $arrResult['error'] = 'length header exceeds token size';
            }
            else
            {
                // Split: sharedstuff of exactly $intSharedLen bytes, then the remainder = issuerdata.
                $strSharedEncoded = substr($strToken_a, self::LEN_HEADER_BYTES, $intSharedLen);
                $strIssuerData = substr($strToken_a, self::LEN_HEADER_BYTES + $intSharedLen);

                $strFrame = Unsignal::decode($objShared_a, $strSharedEncoded);

                if ($strFrame === false)
                {
                    $arrResult['error'] = 'SHAREDROM decode failed';
                }
                else
                {
                    $arrFields = self::parseFrame($strFrame, 2);

                    if ($arrFields === false)
                    {
                        $arrResult['error'] = 'malformed or tampered token (integrity check failed)';
                    }
                    else
                    {
                        $arrResult['sharedSig'] = $arrFields[0];
                        $arrResult['sharedClaims'] = $arrFields[1];
                        $arrResult['issuerData'] = $strIssuerData;
                        $arrResult['success'] = true;
                    }
                }
            }
        }

        return $arrResult;
    }

    // Introspect issuerdata (issuer, ISSUERROM1 + ISSUERROM2).
    // Returns ['success','error','sharedSig','privateClaims'].
    public static function introspect($strIssuerData_a, $strPresentedSig_a, $objIssuer1_a, $objIssuer2_a)
    {
        $arrResult = array('success' => false, 'error' => '', 'sharedSig' => '', 'privateClaims' => '');
        $strInner = false;
        $strFrame = false;
        $arrFields = false;
        $strSealedSig = '';

        if ($strIssuerData_a === null || strlen($strIssuerData_a) == 0)
        {
            $arrResult['error'] = 'issuerdata is empty';
        }
        else if ($strPresentedSig_a === null || strlen($strPresentedSig_a) == 0)
        {
            $arrResult['error'] = 'presented sharedsignature is required';
        }
        else if ($objIssuer1_a === null || !$objIssuer1_a->isLoaded())
        {
            $arrResult['error'] = 'ISSUERROM1 is not loaded';
        }
        else if ($objIssuer2_a === null || !$objIssuer2_a->isLoaded())
        {
            $arrResult['error'] = 'ISSUERROM2 is not loaded';
        }
        else
        {
            // Reverse the double encoding: outer ISSUERROM1, then inner ISSUERROM2.
            $strInner = Unsignal::decode($objIssuer1_a, $strIssuerData_a);
            if ($strInner !== false)
            {
                $strFrame = Unsignal::decode($objIssuer2_a, $strInner);
            }

            if ($strFrame === false)
            {
                $arrResult['error'] = 'ISSUERROM decode failed';
            }
            else
            {
                $arrFields = self::parseFrame($strFrame, 2);

                if ($arrFields === false)
                {
                    $arrResult['error'] = 'malformed or tampered issuerdata (integrity check failed)';
                }
                else
                {
                    $strSealedSig = $arrFields[0];

                    if (!self::bytesEqual($strSealedSig, $strPresentedSig_a))
                    {
                        $arrResult['error'] = 'sharedsignature does not match the sealed copy';
                    }
                    else
                    {
                        $arrResult['sharedSig'] = $strSealedSig;
                        $arrResult['privateClaims'] = $arrFields[1];
                        $arrResult['success'] = true;
                    }
                }
            }
        }

        return $arrResult;
    }

    // Convenience verify for a party holding all three ROMs (issuer-local).
    // Returns ['success','error','sharedSig','sharedClaims','privateClaims'].
    public static function verify($strToken_a, $objShared_a, $objIssuer1_a, $objIssuer2_a)
    {
        $arrResult = array('success' => false, 'error' => '', 'sharedSig' => '',
                           'sharedClaims' => '', 'privateClaims' => '');
        $arrOpen = null;
        $arrIntro = null;

        $arrOpen = self::open($strToken_a, $objShared_a);

        if (!$arrOpen['success'])
        {
            $arrResult['error'] = $arrOpen['error'];
        }
        else if (strlen($arrOpen['issuerData']) == 0)
        {
            // Bare token: no issuer attestation present. Shared parts verify by decoding alone.
            $arrResult['sharedSig'] = $arrOpen['sharedSig'];
            $arrResult['sharedClaims'] = $arrOpen['sharedClaims'];
            $arrResult['privateClaims'] = '';
            $arrResult['success'] = true;
        }
        else
        {
            $arrIntro = self::introspect($arrOpen['issuerData'], $arrOpen['sharedSig'], $objIssuer1_a, $objIssuer2_a);

            if (!$arrIntro['success'])
            {
                $arrResult['error'] = $arrIntro['error'];
            }
            else
            {
                $arrResult['sharedSig'] = $arrOpen['sharedSig'];
                $arrResult['sharedClaims'] = $arrOpen['sharedClaims'];
                $arrResult['privateClaims'] = $arrIntro['privateClaims'];
                $arrResult['success'] = true;
            }
        }

        return $arrResult;
    }
}