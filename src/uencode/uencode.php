<?php
// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

class ByteAddresses 
{
    public $ptrAddresses = array();
    public $intCount = 0;
}

class RomData 
{
    public $ptrROMData = '';
    public $lngROMSize = 0;
    public $arrLookup = array();
}

define('UNSIGNAL_OFFSET_LIMIT_PCT', 2);
define('UNSIGNAL_ROM_LOAD_MAX', 131072);
define('HEADER_SIZE', 8);

function findOffset($byLow_a, $byHigh_a, $lngROMSize_a) 
{
    $intResult = 0;
    $intRaw = ($byLow_a) | ($byHigh_a << 8);
    
    if ($lngROMSize_a >= 131072) 
    {
        $intResult = $intRaw;
    } 
    else 
    {
        $lngMax = (int)(($lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100);
        if ($lngMax == 0) 
        {
            $intResult = 0;
        } 
        else 
        {
            $intResult = $intRaw % ($lngMax + 1);
        }
    }
    
    return $intResult;
}

function findROMAddress($ptrLookup_a, $byTarget_a) 
{
    $intResult = 0;
    
    if ($ptrLookup_a[$byTarget_a]->intCount > 0) 
    {
        $intRandomIdx = rand(0, $ptrLookup_a[$byTarget_a]->intCount - 1);
        $intResult = $ptrLookup_a[$byTarget_a]->ptrAddresses[$intRandomIdx];
    }
    
    return $intResult;
}

function buildLookupTable($ptrRom_a) 
{
    $arrCounts = array_fill(0, 256, 0);
    $lngHeaderSize = 0;
    
    // Initialize lookup array
    $ptrRom_a->arrLookup = array();
    for ($intI = 0; $intI < 256; $intI++) 
    {
        $ptrRom_a->arrLookup[$intI] = new ByteAddresses();
        $ptrRom_a->arrLookup[$intI]->ptrAddresses = array();
        $ptrRom_a->arrLookup[$intI]->intCount = 0;
    }
    
    // Header addresses must be within the first 64KB (16-bit addresses)
    $lngHeaderSize = $ptrRom_a->lngROMSize;
    if ($lngHeaderSize > 65536) 
    {
        $lngHeaderSize = 65536;
    }
    
    // Count occurrences
    for ($lngI = 0; $lngI < $lngHeaderSize; $lngI++) 
    {
        $byte = ord($ptrRom_a->ptrROMData[$lngI]);
        $arrCounts[$byte]++;
    }
    
    // Allocate memory for each byte value
    for ($intI = 0; $intI < 256; $intI++) 
    {
        if ($arrCounts[$intI] > 0) 
        {
            $ptrRom_a->arrLookup[$intI]->ptrAddresses = array();
            $ptrRom_a->arrLookup[$intI]->intCount = 0;
        }
    }
    
    // Fill addresses
    for ($lngI = 0; $lngI < $lngHeaderSize; $lngI++) 
    {
        $by = ord($ptrRom_a->ptrROMData[$lngI]);
        $ptrRom_a->arrLookup[$by]->ptrAddresses[] = $lngI;
        $ptrRom_a->arrLookup[$by]->intCount++;
    }
}

function loadRom($strFilename_a) 
{
    $ptrRom = null;
    
    if (file_exists($strFilename_a)) 
    {
        $ptrRom = new RomData();
        $ptrRom->ptrROMData = file_get_contents($strFilename_a);
        if ($ptrRom->ptrROMData !== false) 
        {
            $ptrRom->lngROMSize = strlen($ptrRom->ptrROMData);
            if ($ptrRom->lngROMSize > UNSIGNAL_ROM_LOAD_MAX) 
            {
                $ptrRom->ptrROMData = substr($ptrRom->ptrROMData, 0, UNSIGNAL_ROM_LOAD_MAX);
                $ptrRom->lngROMSize = UNSIGNAL_ROM_LOAD_MAX;
            }
            
            // Pre-build lookup table for reuse across multiple encodes
            buildLookupTable($ptrRom);
        } 
        else 
        {
            $ptrRom = null;
        }
    }
    
    return $ptrRom;
}

function unloadRom($ptrRom_a) 
{
    // In PHP, garbage collector handles this, but method kept for symmetry
    $ptrRom_a->ptrROMData = '';
    $ptrRom_a->lngROMSize = 0;
    $ptrRom_a->arrLookup = array();
}

function encodeFile($ptrRom_a, $strInputFile_a, $strOutputFile_a) 
{
    $blnSuccess = false;
    $arrOffsetLookup = array();
    $arrOffsetCounts = array_fill(0, 256, 0);
    $byOffsetLow = 0;
    $byOffsetHigh = 0;
    $byPrefixLen = 0;
    $bySuffixLen = 0;
    $intROMOffset = 0;
    $intH1 = 0;
    $intH2 = 0;
    $intH3 = 0;
    $intH4 = 0;
    $ptrPrefix = '';
    $ptrSuffix = '';
    $lngEffectiveSize = 0;
    $lngI = 0;
    $intI = 0;
    $blnLookupValid = true;
    $ptrInput = null;
    $ptrOutput = null;
    
    // Initialize offset lookup array
    for ($intI = 0; $intI < 256; $intI++) 
    {
        $arrOffsetLookup[$intI] = new ByteAddresses();
        $arrOffsetLookup[$intI]->ptrAddresses = array();
        $arrOffsetLookup[$intI]->intCount = 0;
    }
    
    // Generate header values
    $byOffsetLow = rand(0, 255);
    $byOffsetHigh = rand(0, 255);
    $byPrefixLen = rand(10, 255);
    $bySuffixLen = rand(10, 255);
    
    // Check that ROM contains required byte values using pre-built lookup
    if ($ptrRom_a->arrLookup[$byOffsetLow]->intCount > 0 && 
        $ptrRom_a->arrLookup[$byOffsetHigh]->intCount > 0 &&
        $ptrRom_a->arrLookup[$byPrefixLen]->intCount > 0 && 
        $ptrRom_a->arrLookup[$bySuffixLen]->intCount > 0) 
    {
        $intROMOffset = findOffset($byOffsetLow, $byOffsetHigh, $ptrRom_a->lngROMSize);
        
        $intH1 = findROMAddress($ptrRom_a->arrLookup, $byOffsetLow);
        $intH2 = findROMAddress($ptrRom_a->arrLookup, $byOffsetHigh);
        $intH3 = findROMAddress($ptrRom_a->arrLookup, $byPrefixLen);
        $intH4 = findROMAddress($ptrRom_a->arrLookup, $bySuffixLen);
        
        // Generate random prefix and suffix
        $ptrPrefix = '';
        for ($intI = 0; $intI < $byPrefixLen; $intI++) 
        {
            $ptrPrefix .= chr(rand(0, 255));
        }
        
        $ptrSuffix = '';
        for ($intI = 0; $intI < $bySuffixLen; $intI++) 
        {
            $ptrSuffix .= chr(rand(0, 255));
        }
        
        // Calculate effective encoding window
        $lngEffectiveSize = $ptrRom_a->lngROMSize - $intROMOffset;
        if ($lngEffectiveSize > 65536) 
        {
            $lngEffectiveSize = 65536;
        }
        
        // Build offset lookup tables for the encoding window
        for ($lngI = 0; $lngI < $lngEffectiveSize; $lngI++) 
        {
            $byte = ord($ptrRom_a->ptrROMData[$intROMOffset + $lngI]);
            $arrOffsetCounts[$byte]++;
        }
        
        for ($intI = 0; $intI < 256 && $blnLookupValid; $intI++) 
        {
            if ($arrOffsetCounts[$intI] > 0) 
            {
                $arrOffsetLookup[$intI]->ptrAddresses = array();
                if (is_array($arrOffsetLookup[$intI]->ptrAddresses)) 
                {
                    $arrOffsetLookup[$intI]->intCount = 0;
                } 
                else 
                {
                    $blnLookupValid = false;
                }
            }
        }
        
        for ($lngI = 0; $lngI < $lngEffectiveSize && $blnLookupValid; $lngI++) 
        {
            $by = ord($ptrRom_a->ptrROMData[$intROMOffset + $lngI]);
            $arrOffsetLookup[$by]->ptrAddresses[] = $lngI;
            $arrOffsetLookup[$by]->intCount++;
        }
        
        if ($blnLookupValid) 
        {
            if (file_exists($strInputFile_a)) 
            {
                $ptrInput = fopen($strInputFile_a, 'rb');
                if ($ptrInput) 
                {
                    $ptrOutput = fopen($strOutputFile_a, 'wb');
                    if ($ptrOutput) 
                    {
                        // Write header (4 x 16-bit addresses)
                        fwrite($ptrOutput, pack('v', $intH1));
                        fwrite($ptrOutput, pack('v', $intH2));
                        fwrite($ptrOutput, pack('v', $intH3));
                        fwrite($ptrOutput, pack('v', $intH4));
                        
                        // Write prefix
                        fwrite($ptrOutput, $ptrPrefix);
                        
                        // Stream-encode input
                        while (!feof($ptrInput)) 
                        {
                            $intCh = fgetc($ptrInput);
                            if ($intCh === false) 
                            {
                                break;
                            }
                            $by = ord($intCh);
                            if ($arrOffsetLookup[$by]->intCount > 0) 
                            {
                                $intRandomIdx = rand(0, $arrOffsetLookup[$by]->intCount - 1);
                                $intAddress = $arrOffsetLookup[$by]->ptrAddresses[$intRandomIdx];
                                fwrite($ptrOutput, pack('v', $intAddress));
                            }
                        }
                        
                        // Write suffix
                        fwrite($ptrOutput, $ptrSuffix);
                        
                        $blnSuccess = true;
                        
                        fclose($ptrOutput);
                    }
                    fclose($ptrInput);
                }
            }
        }
    } 
    else 
    {
        fprintf(STDERR, "Error: ROM does not contain required byte values for UNSIGNAL header\n");
    }
    
    return $blnSuccess;
}

// Test harness
function main() 
{
    $intResult = 1;
    $ptrRom = null;
    $blnEncodeOk = false;
    
    echo "UNSIGNAL Protocol Encoder\n";
    echo "(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n";
    
    // Test harness - hardcoded filenames for testing
    $strRomFile = 'rom.bin';
    $strInputFile = 'input.dat';
    $strOutputFile = 'output.enc';
    
    echo "Test Harness - Using:\n";
    echo "  ROM file: {$strRomFile}\n";
    echo "  Input file: {$strInputFile}\n";
    echo "  Output file: {$strOutputFile}\n\n";
    
    srand(time());
    
    if (file_exists($strRomFile)) 
    {
        $ptrRom = loadRom($strRomFile);
        if ($ptrRom) 
        {
            if (file_exists($strInputFile)) 
            {
                $blnEncodeOk = encodeFile($ptrRom, $strInputFile, $strOutputFile);
                
                if ($blnEncodeOk) 
                {
                    $intResult = 0;
                    echo "Encode successful!\n";
                } 
                else 
                {
                    echo "Encode failed\n";
                }
            } 
            else 
            {
                echo "Input file not found: {$strInputFile}\n";
            }
            
            unloadRom($ptrRom);
        } 
        else 
        {
            echo "Failed to load ROM: {$strRomFile}\n";
        }
    } 
    else 
    {
        echo "ROM file not found: {$strRomFile}\n";
    }
    
    exit($intResult);
}

// Run the test harness
main();
?>