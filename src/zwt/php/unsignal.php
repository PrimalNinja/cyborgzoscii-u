<?php
// CyborgUnicorn.ZOSCII - Unsignal (PHP 5.4+)
// In-memory UNSIGNAL encode/decode: raw ZOSCII plus an 8-byte header (4 x 16-bit
// ROM addresses that dereference to offsetLow/offsetHigh/prefixLen/suffixLen),
// a random prefix, the 2-byte address slots, and a random suffix. Byte-compatible
// with the reference file-based encodeFile/decodeFile.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

require_once 'Zoscii.php';

class Unsignal
{
    const HEADER_SIZE = 8;

    // Regime trigger: 64KB + 2%-of-64KB = 66846 (fixed, not 2% of actual ROM).
    const REGIME_T1 = 66846;

    // 2% of ROM size, integer division (no float).
    private static function pct2($lngRomSize_a)
    {
        return (int)(($lngRomSize_a * 2) / 100);
    }

    // Effective ROM size (the span addresses index into), by regime:
    //   x <  66846            -> x - 2%
    //   66846 <= x < 131072   -> 65536
    //   x >= 131072           -> 65536
    private static function effectiveSize($lngRomSize_a)
    {
        $intResult = 0;

        if ($lngRomSize_a < self::REGIME_T1)
        {
            $intResult = $lngRomSize_a - self::pct2($lngRomSize_a);
        }
        else
        {
            $intResult = 65536;
        }

        return $intResult;
    }

    // Sliding window (offset range), by regime:
    //   x <  66846            -> 2%
    //   66846 <= x < 131072   -> 131072 - x - 2%
    //   x >= 131072           -> 65536
    private static function slidingWindow($lngRomSize_a)
    {
        $intResult = 0;

        if ($lngRomSize_a < self::REGIME_T1)
        {
            $intResult = self::pct2($lngRomSize_a);
        }
        else if ($lngRomSize_a < 131072)
        {
            $intResult = 131072 - $lngRomSize_a - self::pct2($lngRomSize_a);
        }
        else
        {
            $intResult = 65536;
        }

        return $intResult;
    }

    // Resolve the logical-start offset from the two header bytes: fold the 16-bit
    // pointer into the sliding-window range for this ROM size.
    private static function findOffset($byLow_a, $byHigh_a, $lngRomSize_a)
    {
        $intResult = 0;
        $intRaw = 0;
        $intWindow = 0;

        $intRaw = ($byLow_a) | ($byHigh_a << 8);
        $intWindow = self::slidingWindow($lngRomSize_a);

        if ($intWindow <= 0)
        {
            $intResult = 0;
        }
        else
        {
            $intResult = $intRaw % ($intWindow + 1);
        }

        return $intResult;
    }

    // Encode a byte string with UNSIGNAL. Returns encoded bytes, or false on failure.
    public static function encode($objZoscii_a, $strBytes_a)
    {
        $strResult = false;
        $arrOffsetLookup = array();
        $arrOffsetCounts = array_fill(0, 256, 0);
        $byOffsetLow = 0;
        $byOffsetHigh = 0;
        $byPrefixLen = 0;
        $bySuffixLen = 0;
        $intRomOffset = 0;
        $intH1 = 0;
        $intH2 = 0;
        $intH3 = 0;
        $intH4 = 0;
        $strPrefix = '';
        $strSuffix = '';
        $strOut = '';
        $lngEffSize = 0;
        $lngLen = 0;
        $lngI = 0;
        $intI = 0;
        $intByte = 0;
        $intAddr = 0;
        $intRandomIdx = 0;
        $blnOk = true;

        if ($objZoscii_a !== null && $objZoscii_a->isLoaded() && $strBytes_a !== null)
        {
            for ($intI = 0; $intI < 256; $intI++)
            {
                $arrOffsetLookup[$intI] = new ZosciiByteAddresses();
                $arrOffsetLookup[$intI]->arrAddresses = array();
                $arrOffsetLookup[$intI]->intCount = 0;
            }

            // Header control bytes.
            $byOffsetLow = rand(0, 255);
            $byOffsetHigh = rand(0, 255);
            $byPrefixLen = rand(10, 255);
            $bySuffixLen = rand(10, 255);

            // The ROM must contain each control byte so the header can be encoded as addresses.
            if ($objZoscii_a->arrLookup[$byOffsetLow]->intCount > 0 &&
                $objZoscii_a->arrLookup[$byOffsetHigh]->intCount > 0 &&
                $objZoscii_a->arrLookup[$byPrefixLen]->intCount > 0 &&
                $objZoscii_a->arrLookup[$bySuffixLen]->intCount > 0)
            {
                $intRomOffset = self::findOffset($byOffsetLow, $byOffsetHigh, $objZoscii_a->lngRomSize);

                $intH1 = $objZoscii_a->findAddress($byOffsetLow);
                $intH2 = $objZoscii_a->findAddress($byOffsetHigh);
                $intH3 = $objZoscii_a->findAddress($byPrefixLen);
                $intH4 = $objZoscii_a->findAddress($bySuffixLen);

                for ($intI = 0; $intI < $byPrefixLen; $intI++)
                {
                    $strPrefix .= chr(rand(0, 255));
                }

                for ($intI = 0; $intI < $bySuffixLen; $intI++)
                {
                    $strSuffix .= chr(rand(0, 255));
                }

                // Effective ROM span for this size regime.
                $lngEffSize = self::effectiveSize($objZoscii_a->lngRomSize);

                for ($lngI = 0; $lngI < $lngEffSize; $lngI++)
                {
                    $intByte = ord($objZoscii_a->strRomData[$intRomOffset + $lngI]);
                    $arrOffsetLookup[$intByte]->arrAddresses[] = $lngI;
                    $arrOffsetLookup[$intByte]->intCount++;
                    $arrOffsetCounts[$intByte]++;
                }

                // Header: 4 x 16-bit addresses, then prefix.
                $strOut .= pack('v', $intH1);
                $strOut .= pack('v', $intH2);
                $strOut .= pack('v', $intH3);
                $strOut .= pack('v', $intH4);
                $strOut .= $strPrefix;

                // Body: one 2-byte address slot per input byte.
                $lngLen = strlen($strBytes_a);
                for ($lngI = 0; $lngI < $lngLen && $blnOk; $lngI++)
                {
                    $intByte = ord($strBytes_a[$lngI]);

                    if ($arrOffsetLookup[$intByte]->intCount > 0)
                    {
                        $intRandomIdx = rand(0, $arrOffsetLookup[$intByte]->intCount - 1);
                        $intAddr = $arrOffsetLookup[$intByte]->arrAddresses[$intRandomIdx];
                        $strOut .= pack('v', $intAddr);
                    }
                    else
                    {
                        $blnOk = false;
                    }
                }

                if ($blnOk)
                {
                    $strOut .= $strSuffix;
                    $strResult = $strOut;
                }
            }
        }

        return $strResult;
    }

    // Decode UNSIGNAL bytes back to the original. Returns bytes, or false on failure.
    public static function decode($objZoscii_a, $strEncoded_a)
    {
        $strResult = false;
        $arrAddrs = array_fill(0, 4, 0);
        $byOffsetLow = 0;
        $byOffsetHigh = 0;
        $byPrefixLen = 0;
        $bySuffixLen = 0;
        $intRomOffset = 0;
        $lngInputSize = 0;
        $lngDataSize = 0;
        $lngSlots = 0;
        $lngEffSize = 0;
        $lngPos = 0;
        $intI = 0;
        $intAddr = 0;
        $arrData = null;
        $strOut = '';
        $blnOk = true;

        if ($objZoscii_a !== null && $objZoscii_a->isLoaded() && $strEncoded_a !== null)
        {
            $lngInputSize = strlen($strEncoded_a);

            if ($lngInputSize >= self::HEADER_SIZE)
            {
                // Read the 4 header addresses; each must be inside the ROM.
                for ($intI = 0; $intI < 4 && $blnOk; $intI++)
                {
                    $arrData = unpack('v', substr($strEncoded_a, $intI * 2, 2));
                    $arrAddrs[$intI] = $arrData[1];

                    if ($arrAddrs[$intI] >= $objZoscii_a->lngRomSize)
                    {
                        $blnOk = false;
                    }
                }

                if ($blnOk)
                {
                    $byOffsetLow = ord($objZoscii_a->strRomData[$arrAddrs[0]]);
                    $byOffsetHigh = ord($objZoscii_a->strRomData[$arrAddrs[1]]);
                    $byPrefixLen = ord($objZoscii_a->strRomData[$arrAddrs[2]]);
                    $bySuffixLen = ord($objZoscii_a->strRomData[$arrAddrs[3]]);

                    $intRomOffset = self::findOffset($byOffsetLow, $byOffsetHigh, $objZoscii_a->lngRomSize);

                    $lngDataSize = $lngInputSize - self::HEADER_SIZE - $byPrefixLen - $bySuffixLen;

                    if ($lngDataSize >= 0 && ($lngDataSize % 2) == 0)
                    {
                        $lngSlots = $lngDataSize / 2;

                        $lngEffSize = self::effectiveSize($objZoscii_a->lngRomSize);

                        // Body starts after the 8-byte header and the prefix.
                        $lngPos = self::HEADER_SIZE + $byPrefixLen;

                        for ($intI = 0; $intI < $lngSlots; $intI++)
                        {
                            $arrData = unpack('v', substr($strEncoded_a, $lngPos, 2));
                            $intAddr = $arrData[1] % $lngEffSize;
                            $lngPos += 2;

                            $strOut .= $objZoscii_a->strRomData[$intRomOffset + $intAddr];
                        }

                        $strResult = $strOut;
                    }
                }
            }
        }

        return $strResult;
    }
}