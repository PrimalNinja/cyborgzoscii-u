<?php
// CyborgUnicorn.ZOSCII - Zoscii (PHP 5.4+)
// ROM holder plus raw ZOSCII encode/decode (address-lookup, no header/noise).
// Bytes are handled as PHP binary strings throughout (strings ARE byte arrays).
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

class ZosciiByteAddresses
{
    public $arrAddresses = array();
    public $intCount = 0;
}

class Zoscii
{
    // --- Fields ---

    public $strRomData = '';       // ROM bytes (binary string)
    public $lngRomSize = 0;
    public $arrLookup = array();   // [byte => ZosciiByteAddresses] over first 64KB

    const ROM_LOAD_MAX = 131072;
    const HEADER_WINDOW = 65536;   // header addresses must fit 16-bit

    // --- Construction ---

    // Build a Zoscii from a binary string. Returns a Zoscii, or null on empty input.
    public static function fromString($strBytes_a)
    {
        $objResult = null;

        if ($strBytes_a !== null && strlen($strBytes_a) > 0)
        {
            $objRom = new Zoscii();
            $objRom->strRomData = $strBytes_a;
            $objRom->lngRomSize = strlen($strBytes_a);
            $objRom->buildLookup();
            $objResult = $objRom;
        }

        return $objResult;
    }

    // Build a Zoscii from a file (reads up to ROM_LOAD_MAX bytes). Returns null on failure.
    public static function fromFile($strPath_a)
    {
        $objResult = null;
        $strData = '';
        $ptrFile = null;

        if (file_exists($strPath_a))
        {
            $ptrFile = fopen($strPath_a, 'rb');
            if ($ptrFile)
            {
                $strData = fread($ptrFile, self::ROM_LOAD_MAX);
                fclose($ptrFile);

                if ($strData !== false && strlen($strData) > 0)
                {
                    $objResult = self::fromString($strData);
                }
            }
        }

        return $objResult;
    }

    public function isLoaded()
    {
        return ($this->lngRomSize > 0);
    }

    // --- Lookup + rand seed ---

    // Build the byte->addresses table over the first 64KB, then seed rand() from ROM content.
    public function buildLookup()
    {
        $arrCounts = array_fill(0, 256, 0);
        $lngWindow = 0;
        $intI = 0;
        $lngI = 0;
        $intByte = 0;
        $intRomHash = 0;

        for ($intI = 0; $intI < 256; $intI++)
        {
            $this->arrLookup[$intI] = new ZosciiByteAddresses();
            $this->arrLookup[$intI]->arrAddresses = array();
            $this->arrLookup[$intI]->intCount = 0;
        }

        $lngWindow = $this->lngRomSize;
        if ($lngWindow > self::HEADER_WINDOW)
        {
            $lngWindow = self::HEADER_WINDOW;
        }

        for ($lngI = 0; $lngI < $lngWindow; $lngI++)
        {
            $intByte = ord($this->strRomData[$lngI]);
            $this->arrLookup[$intByte]->arrAddresses[] = $lngI;
            $this->arrLookup[$intByte]->intCount++;
        }

        // Seed rand from ROM content plus time (matches reference UNSIGNAL seeding).
        $intRomHash = 0;
        for ($lngI = 0; $lngI < $this->lngRomSize; $lngI++)
        {
            $intRomHash = ($intRomHash * 33) + ord($this->strRomData[$lngI]);
        }

        $intRomHash ^= (int)(microtime(true) * 1000000);

        srand($intRomHash);
    }

    // Pick a random ROM address (within the 64KB header window) whose byte == target.
    // Returns -1 if the byte does not occur.
    public function findAddress($intTarget_a)
    {
        $intResult = -1;
        $intRandomIdx = 0;

        if ($this->arrLookup[$intTarget_a]->intCount > 0)
        {
            $intRandomIdx = rand(0, $this->arrLookup[$intTarget_a]->intCount - 1);
            $intResult = $this->arrLookup[$intTarget_a]->arrAddresses[$intRandomIdx];
        }

        return $intResult;
    }

    // --- Raw ZOSCII encode/decode (full 64KB window, no header/noise) ---

    // Encode a byte string to a raw ZOSCII address stream (2 bytes LE per input byte).
    // Returns encoded bytes, or false if any input byte is absent from the ROM window.
    public function encode($strBytes_a)
    {
        $strResult = false;
        $strOut = '';
        $lngLen = 0;
        $lngI = 0;
        $intByte = 0;
        $intAddr = 0;
        $blnOk = true;

        if ($strBytes_a !== null)
        {
            $lngLen = strlen($strBytes_a);

            for ($lngI = 0; $lngI < $lngLen && $blnOk; $lngI++)
            {
                $intByte = ord($strBytes_a[$lngI]);
                $intAddr = $this->findAddress($intByte);

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
        }

        return $strResult;
    }

    // Decode a raw ZOSCII address stream back to bytes. Out-of-bounds addresses fold
    // (addr % romSize) rather than failing, matching the native read model. Returns bytes.
    public function decode($strEncoded_a)
    {
        $strResult = false;
        $strOut = '';
        $lngLen = 0;
        $lngSlots = 0;
        $lngI = 0;
        $intAddr = 0;
        $arrData = null;

        if ($strEncoded_a !== null)
        {
            $lngLen = strlen($strEncoded_a);

            if (($lngLen % 2) == 0)
            {
                $lngSlots = $lngLen / 2;

                for ($lngI = 0; $lngI < $lngSlots; $lngI++)
                {
                    $arrData = unpack('v', substr($strEncoded_a, $lngI * 2, 2));
                    $intAddr = $arrData[1] % $this->lngRomSize;
                    $strOut .= $this->strRomData[$intAddr];
                }

                $strResult = $strOut;
            }
        }

        return $strResult;
    }
}