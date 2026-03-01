// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Windows & Linux Version

use std::env;
use std::fs::File;
use std::io::{Read, Write, BufReader, BufWriter};
use std::process;
use rand::Rng;

struct ByteAddresses 
{
    ptrAddresses: Vec<u16>,
    intCount: u32,
}

const UNSIGNAL_OFFSET_LIMIT_PCT: usize = 2;
const UNSIGNAL_ROM_LOAD_MAX: usize = 131072;
const HEADER_SIZE: usize = 8;

struct RomData 
{
    ptrROMData: Vec<u8>,
    lngROMSize: usize,
    arrLookup: [ByteAddresses; 256],
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

fn find_rom_address(ptrLookup_a: &[ByteAddresses; 256], byTarget_a: u8) -> u16 
{
    let mut intResult: u16 = 0;
    
    if ptrLookup_a[byTarget_a as usize].intCount > 0 
    {
        let intRandomIdx: usize = rand::thread_rng().gen_range(0..ptrLookup_a[byTarget_a as usize].intCount) as usize;
        intResult = ptrLookup_a[byTarget_a as usize].ptrAddresses[intRandomIdx];
    }
    
    return intResult;
}

fn build_lookup_table(ptrRom_a: &mut RomData) 
{
    let mut arrCounts: [u32; 256] = [0; 256];
    let lngHeaderSize: usize;
    let mut lngI: usize = 0;
    let mut intI: usize = 0;
    
    // Initialize lookup array
    for intI = 0; intI < 256; intI++ 
    {
        ptrRom_a.arrLookup[intI].ptrAddresses = Vec::new();
        ptrRom_a.arrLookup[intI].intCount = 0;
    }
    
    // Header addresses must be within the first 64KB (16-bit addresses)
    lngHeaderSize = ptrRom_a.lngROMSize;
    if lngHeaderSize > 65536 
    {
        lngHeaderSize = 65536;
    }
    
    // Count occurrences
    for lngI = 0; lngI < lngHeaderSize; lngI++ 
    {
        arrCounts[ptrRom_a.ptrROMData[lngI] as usize] += 1;
    }
    
    // Allocate memory for each byte value
    for intI = 0; intI < 256; intI++ 
    {
        if arrCounts[intI] > 0 
        {
            ptrRom_a.arrLookup[intI].ptrAddresses = Vec::with_capacity(arrCounts[intI] as usize);
            ptrRom_a.arrLookup[intI].intCount = 0;
        }
    }
    
    // Fill addresses
    for lngI = 0; lngI < lngHeaderSize; lngI++ 
    {
        let by: u8 = ptrRom_a.ptrROMData[lngI];
        ptrRom_a.arrLookup[by as usize].ptrAddresses.push(lngI as u16);
        ptrRom_a.arrLookup[by as usize].intCount += 1;
    }
}

fn load_rom(strFilename_a: &str) -> Result<RomData, String> 
{
    let mut ptrRom: RomData;
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
        arrLookup: std::array::from_fn(|_| ByteAddresses 
        { 
            ptrAddresses: Vec::new(), 
            intCount: 0 
        }),
    };
    
    // Pre-build lookup table for reuse across multiple encodes
    build_lookup_table(&mut ptrRom);
    
    return Ok(ptrRom);
}

fn unload_rom(ptrRom_a: &mut RomData) 
{
    // In Rust, memory is freed when variables go out of scope,
    // but method kept for symmetry
    ptrRom_a.ptrROMData.clear();
    ptrRom_a.lngROMSize = 0;
    for intI in 0..256 
    {
        ptrRom_a.arrLookup[intI].ptrAddresses.clear();
        ptrRom_a.arrLookup[intI].intCount = 0;
    }
}

fn encode_file(ptrRom_a: &RomData, strInputFile_a: &str, strOutputFile_a: &str) -> bool 
{
    let mut blnSuccess: bool = false;
    let mut arrOffsetLookup: [ByteAddresses; 256] = std::array::from_fn(|_| ByteAddresses 
    { 
        ptrAddresses: Vec::new(), 
        intCount: 0 
    });
    let mut arrOffsetCounts: [u32; 256] = [0; 256];
    let byOffsetLow: u8;
    let byOffsetHigh: u8;
    let byPrefixLen: u8;
    let bySuffixLen: u8;
    let intROMOffset: u16;
    let intH1: u16;
    let intH2: u16;
    let intH3: u16;
    let intH4: u16;
    let mut ptrPrefix: Vec<u8> = Vec::new();
    let mut ptrSuffix: Vec<u8> = Vec::new();
    let lngEffectiveSize: usize;
    let mut ptrRand: rand::rngs::ThreadRng;
    let mut lngI: usize = 0;
    let mut intI: usize = 0;
    let mut blnLookupValid: bool = true;
    let mut ptrInput: BufReader<File>;
    let mut ptrOutput: BufWriter<File>;
    let mut arrBuf: [u8; 1] = [0u8; 1];
    
    ptrRand = rand::thread_rng();
    
    // Initialize offset lookup array
    for intI = 0; intI < 256; intI++ 
    {
        arrOffsetLookup[intI].ptrAddresses = Vec::new();
        arrOffsetLookup[intI].intCount = 0;
    }
    
    // Generate header values
    byOffsetLow = ptrRand.gen();
    byOffsetHigh = ptrRand.gen();
    byPrefixLen = ptrRand.gen_range(10..=255);
    bySuffixLen = ptrRand.gen_range(10..=255);
    
    // Check that ROM contains required byte values using pre-built lookup
    if ptrRom_a.arrLookup[byOffsetLow as usize].intCount > 0 && 
       ptrRom_a.arrLookup[byOffsetHigh as usize].intCount > 0 &&
       ptrRom_a.arrLookup[byPrefixLen as usize].intCount > 0 && 
       ptrRom_a.arrLookup[bySuffixLen as usize].intCount > 0 
    {
        intROMOffset = find_offset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize);
        
        intH1 = find_rom_address(&ptrRom_a.arrLookup, byOffsetLow);
        intH2 = find_rom_address(&ptrRom_a.arrLookup, byOffsetHigh);
        intH3 = find_rom_address(&ptrRom_a.arrLookup, byPrefixLen);
        intH4 = find_rom_address(&ptrRom_a.arrLookup, bySuffixLen);
        
        // Generate random prefix and suffix
        ptrPrefix = Vec::with_capacity(byPrefixLen as usize);
        for intI = 0; intI < byPrefixLen as usize; intI++ 
        {
            ptrPrefix.push(ptrRand.gen());
        }
        
        ptrSuffix = Vec::with_capacity(bySuffixLen as usize);
        for intI = 0; intI < bySuffixLen as usize; intI++ 
        {
            ptrSuffix.push(ptrRand.gen());
        }
        
        // Calculate effective encoding window
        lngEffectiveSize = (ptrRom_a.lngROMSize - intROMOffset as usize).min(65536);
        
        // Build offset lookup tables for the encoding window
        for lngI = 0; lngI < lngEffectiveSize; lngI++ 
        {
            arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset as usize + lngI] as usize] += 1;
        }
        
        // Initialize offset lookup array (already done above)
        
        for intI = 0; intI < 256 && blnLookupValid; intI++ 
        {
            if arrOffsetCounts[intI] > 0 
            {
                arrOffsetLookup[intI].ptrAddresses = Vec::with_capacity(arrOffsetCounts[intI] as usize);
                if !arrOffsetLookup[intI].ptrAddresses.is_empty() || arrOffsetCounts[intI] == 0 
                {
                    arrOffsetLookup[intI].intCount = 0;
                } 
                else 
                {
                    blnLookupValid = false;
                }
            }
        }
        
        for lngI = 0; lngI < lngEffectiveSize && blnLookupValid; lngI++ 
        {
            let by: u8 = ptrRom_a.ptrROMData[intROMOffset as usize + lngI];
            arrOffsetLookup[by as usize].ptrAddresses.push(lngI as u16);
            arrOffsetLookup[by as usize].intCount += 1;
        }
        
        if blnLookupValid 
        {
            match File::open(strInputFile_a) 
            {
                Ok(f) => 
                {
                    ptrInput = BufReader::new(f);
                    
                    match File::create(strOutputFile_a) 
                    {
                        Ok(f) => 
                        {
                            ptrOutput = BufWriter::new(f);
                            
                            // Write header (4 x 16-bit addresses)
                            if ptrOutput.write_all(&intH1.to_le_bytes()).is_ok() &&
                               ptrOutput.write_all(&intH2.to_le_bytes()).is_ok() &&
                               ptrOutput.write_all(&intH3.to_le_bytes()).is_ok() &&
                               ptrOutput.write_all(&intH4.to_le_bytes()).is_ok() &&
                               ptrOutput.write_all(&ptrPrefix).is_ok() 
                            {
                                // Stream-encode input
                                loop 
                                {
                                    match ptrInput.read_exact(&mut arrBuf) 
                                    {
                                        Ok(_) => 
                                        {
                                            let by: u8 = arrBuf[0];
                                            if arrOffsetLookup[by as usize].intCount > 0 
                                            {
                                                let intIdx: usize = ptrRand.gen_range(
                                                    0..arrOffsetLookup[by as usize].intCount
                                                ) as usize;
                                                let intAddress: u16 = arrOffsetLookup[by as usize].ptrAddresses[intIdx];
                                                
                                                if ptrOutput.write_all(&intAddress.to_le_bytes()).is_err() 
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                        Err(ref e) if e.kind() == std::io::ErrorKind::UnexpectedEof => 
                                        {
                                            if ptrOutput.write_all(&ptrSuffix).is_ok() 
                                            {
                                                blnSuccess = true;
                                            }
                                            break;
                                        }
                                        Err(_) => 
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        Err(_) => {}
                    }
                }
                Err(_) => {}
            }
        }
    }
    else 
    {
        eprintln!("Error: ROM does not contain required byte values for UNSIGNAL header");
    }
    
    return blnSuccess;
}

fn main() 
{
    let mut intResult: i32 = 1;
    let mut ptrRom: RomData;
    let strArgs: Vec<String> = env::args().collect();
    let blnEncodeOk: bool;
    
    println!("UNSIGNAL Protocol Encoder");
    println!("(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n");

    if strArgs.len() == 4 
    {
        match load_rom(&strArgs[1]) 
        {
            Ok(rom) => 
            {
                ptrRom = rom;
                blnEncodeOk = encode_file(&ptrRom, &strArgs[2], &strArgs[3]);
                
                if blnEncodeOk 
                {
                    intResult = 0;
                } 
                else 
                {
                    eprintln!("Encode failed");
                }
                
                unload_rom(&mut ptrRom);
            }
            Err(e) => 
            {
                eprintln!("Failed to load ROM: {}", e);
            }
        }
    } 
    else 
    {
        eprintln!("Usage: {} <romfile> <inputdatafile> <encodedoutput>", strArgs[0]);
    }
    
    process::exit(intResult);
}