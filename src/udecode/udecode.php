<?php
// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

class RomData 
{
    public $ptrROMData = '';
    public $lngROMSize = 0;
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

function loadRom($strFilename_a) 
{
    $ptrRom = null;
    
    if (file_exists($strFilename_a)) 
    {
        $ptrRom = new RomData();
        
        $ptrFile = fopen($strFilename_a, 'rb');
        if ($ptrFile) 
        {
            $data = fread($ptrFile, UNSIGNAL_ROM_LOAD_MAX);
            fclose($ptrFile);
            
            if ($data !== false) 
            {
                $ptrRom->ptrROMData = $data;
                $ptrRom->lngROMSize = strlen($data);
            } 
            else 
            {
                $ptrRom = null;
            }
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
}

function decodeFile($ptrRom_a, $strInputFile_a, $strOutputFile_a) 
{
    $blnSuccess = false;
    $ptrInput = null;
    $ptrOutput = null;
    $arrBuf = '';
    $arrAddrs = array_fill(0, 4, 0);
    $byOffsetLow = 0;
    $byOffsetHigh = 0;
    $byPrefixLen = 0;
    $bySuffixLen = 0;
    $intOffset = 0;
    $lngInputSize = 0;
    $lngDataSize = 0;
    $lngSlots = 0;
    $lngEffSize = 0;
    $intI = 0;
    
    if (file_exists($strInputFile_a)) 
    {
        $ptrInput = fopen($strInputFile_a, 'rb');
        if ($ptrInput) 
        {
            // Read header
            for ($intI = 0; $intI < 4; $intI++) 
            {
                $arrBuf = fread($ptrInput, 2);
                if (strlen($arrBuf) != 2) 
                {
                    break;
                }
                $arrData = unpack('v', $arrBuf);
                $arrAddrs[$intI] = $arrData[1];
                if ($arrAddrs[$intI] >= $ptrRom_a->lngROMSize) 
                {
                    break;
                }
            }
            
            if ($intI == 4) 
            {
                $byOffsetLow = ord($ptrRom_a->ptrROMData[$arrAddrs[0]]);
                $byOffsetHigh = ord($ptrRom_a->ptrROMData[$arrAddrs[1]]);
                $byPrefixLen = ord($ptrRom_a->ptrROMData[$arrAddrs[2]]);
                $bySuffixLen = ord($ptrRom_a->ptrROMData[$arrAddrs[3]]);
                
                $intOffset = findOffset($byOffsetLow, $byOffsetHigh, $ptrRom_a->lngROMSize);
                
                // Calculate number of slots
                fseek($ptrInput, 0, SEEK_END);
                $lngInputSize = ftell($ptrInput);
                fseek($ptrInput, HEADER_SIZE, SEEK_SET);
                
                $lngDataSize = $lngInputSize - HEADER_SIZE - $byPrefixLen - $bySuffixLen;
                $lngSlots = $lngDataSize / 2;
                
                if ($lngSlots >= 0) 
                {
                    // Skip prefix
                    for ($intI = 0; $intI < $byPrefixLen; $intI++) 
                    {
                        if (fgetc($ptrInput) === false) 
                        {
                            break;
                        }
                    }
                    
                    if ($intI == $byPrefixLen) 
                    {
                        $ptrOutput = fopen($strOutputFile_a, 'wb');
                        if ($ptrOutput) 
                        {
                            $lngEffSize = $ptrRom_a->lngROMSize - $intOffset;
                            if ($lngEffSize > 65536) 
                            {
                                $lngEffSize = 65536;
                            }
                            
                            // Decode
                            for ($intI = 0; $intI < $lngSlots; $intI++) 
                            {
                                $arrBuf = fread($ptrInput, 2);
                                if (strlen($arrBuf) != 2) 
                                {
                                    break;
                                }
                                $arrData = unpack('v', $arrBuf);
                                $intAddr = $arrData[1];
                                if ($intAddr < $lngEffSize) 
                                {
                                    $byte = $ptrRom_a->ptrROMData[$intOffset + $intAddr];
                                    if (fwrite($ptrOutput, $byte) === false) 
                                    {
                                        break;
                                    }
                                }
                            }
                            
                            if ($intI == $lngSlots) 
                            {
                                $blnSuccess = true;
                            }
                            
                            fclose($ptrOutput);
                        }
                    }
                }
            }
            fclose($ptrInput);
        }
    }
    
    return $blnSuccess;
}

// Test harness
function main() 
{
    $intResult = 1;
    $ptrRom = null;
    $blnDecodeOk = false;
    
    echo "UNSIGNAL Protocol Decoder\n";
    echo "(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n";
    
    // Test harness - hardcoded filenames for testing
    $strRomFile = 'rom.bin';
    $strInputFile = 'encoded.enc';
    $strOutputFile = 'decoded.dat';
    
    echo "Test Harness - Using:\n";
    echo "  ROM file: {$strRomFile}\n";
    echo "  Input file: {$strInputFile}\n";
    echo "  Output file: {$strOutputFile}\n\n";
    
    if (file_exists($strRomFile)) 
    {
        $ptrRom = loadRom($strRomFile);
        if ($ptrRom) 
        {
            if (file_exists($strInputFile)) 
            {
                $blnDecodeOk = decodeFile($ptrRom, $strInputFile, $strOutputFile);
                
                if ($blnDecodeOk) 
                {
                    $intResult = 0;
                    echo "Decode successful!\n";
                } 
                else 
                {
                    echo "Decode failed\n";
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