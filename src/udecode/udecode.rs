// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Windows & Linux Version

use std::env;
use std::fs::File;
use std::io::{Read, Write, BufReader, BufWriter};
use std::process;

const UNSIGNAL_OFFSET_LIMIT_PCT: usize = 2;
const UNSIGNAL_ROM_LOAD_MAX: usize = 131072;
const HEADER_SIZE: usize = 8;

struct RomData 
{
    ptrROMData: Vec<u8>,
    lngROMSize: usize,
}

fn find_offset(byLow_a: u8, byHigh_a: u8, lngROMSize_a: usize) -> u16 
{
    let intResult: u16;
    let intRaw: u16 = (byLow_a as u16) | ((byHigh_a as u16) << 8);
    
    if lngROMSize_a >= 131072 
    {
        intResult = intRaw;
    } 
    else 
    {
        let lngMax: usize = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
        if lngMax == 0 
        {
            intResult = 0;
        } 
        else 
        {
            intResult = (intRaw as usize % (lngMax + 1)) as u16;
        }
    }
    
    return intResult;
}

fn load_rom(strFilename_a: &str) -> Result<RomData, String> 
{
    let ptrRom: RomData;
    let mut ptrFile: File;
    let mut arrBuf: Vec<u8> = vec![0u8; UNSIGNAL_ROM_LOAD_MAX];
    let lngRead: usize;
    
    match File::open(strFilename_a) 
    {
        Ok(f) => 
        {
            ptrFile = f;
        }
        Err(e) => 
        {
            return Err(format!("Failed to open ROM file: {}", e));
        }
    }
    
    match ptrFile.read(&mut arrBuf) 
    {
        Ok(n) => 
        {
            lngRead = n;
        }
        Err(e) => 
        {
            return Err(format!("Failed to read ROM file: {}", e));
        }
    }
    
    arrBuf.truncate(lngRead);
    ptrRom = RomData 
    {
        ptrROMData: arrBuf,
        lngROMSize: lngRead,
    };
    
    return Ok(ptrRom);
}

fn decode_file(ptrRom_a: &RomData, strInputFile_a: &str, strOutputFile_a: &str) -> bool 
{
    let mut blnSuccess: bool = false;
    let mut ptrInput: BufReader<File>;
    let mut ptrOutput: BufWriter<File>;
    let lngInputSize: usize;
    let mut arrAddrs: [usize; 4] = [0, 0, 0, 0];
    let mut intI: usize = 0;
    let mut arrBuf: [u8; 2] = [0u8; 2];
    
    // Open input file
    match File::open(strInputFile_a) 
    {
        Ok(f) => 
        {
            ptrInput = BufReader::new(f);
        }
        Err(_) => 
        {
            return false;
        }
    }
    
    // Get input file size
    match ptrInput.get_ref().metadata() 
    {
        Ok(meta) => 
        {
            lngInputSize = meta.len() as usize;
        }
        Err(_) => 
        {
            return false;
        }
    }
    
    // Read header
    for intI = 0; intI < 4; intI++ 
    {
        match ptrInput.read_exact(&mut arrBuf) 
        {
            Ok(_) => 
            {
                arrAddrs[intI] = u16::from_le_bytes(arrBuf) as usize;
            }
            Err(_) => 
            {
                break;
            }
        }
        if arrAddrs[intI] >= ptrRom_a.lngROMSize 
        {
            break;
        }
    }
    
    if intI == 4 
    {
        let byOffsetLow: u8 = ptrRom_a.ptrROMData[arrAddrs[0]];
        let byOffsetHigh: u8 = ptrRom_a.ptrROMData[arrAddrs[1]];
        let byPrefixLen: usize = ptrRom_a.ptrROMData[arrAddrs[2]] as usize;
        let bySuffixLen: usize = ptrRom_a.ptrROMData[arrAddrs[3]] as usize;
        
        let intOffset: usize = find_offset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize) as usize;
        
        let lngDataSize: isize = lngInputSize as isize - 
                                  HEADER_SIZE as isize - 
                                  byPrefixLen as isize - 
                                  bySuffixLen as isize;
        let lngSlots: isize = lngDataSize / 2;
        
        if lngSlots >= 0 
        {
            let lngSlots: usize = lngSlots as usize;
            
            // Skip prefix
            for intI = 0; intI < byPrefixLen; intI++ 
            {
                let mut arrSkip: [u8; 1] = [0u8; 1];
                match ptrInput.read_exact(&mut arrSkip) 
                {
                    Ok(_) => {}
                    Err(_) => 
                    {
                        break;
                    }
                }
            }
            
            if intI == byPrefixLen 
            {
                // Open output file
                match File::create(strOutputFile_a) 
                {
                    Ok(f) => 
                    {
                        ptrOutput = BufWriter::new(f);
                        
                        let lngEffSize: usize = (ptrRom_a.lngROMSize - intOffset).min(65536);
                        
                        // Decode
                        for intI = 0; intI < lngSlots; intI++ 
                        {
                            match ptrInput.read_exact(&mut arrBuf) 
                            {
                                Ok(_) => 
                                {
                                    let intAddr: usize = u16::from_le_bytes(arrBuf) as usize;
                                    if intAddr < lngEffSize 
                                    {
                                        match ptrOutput.write_all(&[ptrRom_a.ptrROMData[intOffset + intAddr]]) 
                                        {
                                            Ok(_) => {}
                                            Err(_) => 
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                Err(_) => 
                                {
                                    break;
                                }
                            }
                        }
                        
                        if intI == lngSlots 
                        {
                            blnSuccess = true;
                        }
                    }
                    Err(_) => {}
                }
            }
        }
    }
    
    return blnSuccess;
}

fn main() 
{
    let intResult: i32;
    let ptrRom: RomData;
    let blnDecodeOk: bool;
    
    println!("UNSIGNAL Protocol Decoder v20260301");
    println!("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n");

    let strArgs: Vec<String> = env::args().collect();
    
    if strArgs.len() == 4 
    {
        match load_rom(&strArgs[1]) 
        {
            Ok(rom) => 
            {
                ptrRom = rom;
                blnDecodeOk = decode_file(&ptrRom, &strArgs[2], &strArgs[3]);
                
                if blnDecodeOk 
                {
                    intResult = 0;
                } 
                else 
                {
                    eprintln!("Decode failed");
                    intResult = 1;
                }
            }
            Err(e) => 
            {
                eprintln!("Failed to load ROM: {}", e);
                intResult = 1;
            }
        }
    } 
    else 
    {
        eprintln!("Usage: {} <romfile> <encoded> <output>", strArgs[0]);
        intResult = 1;
    }
    
    process::exit(intResult);
}