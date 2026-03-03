// Cyborg UNSIGNAL Protocol v20260303
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Windows & Linux Version

package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"math/rand"
	"os"
	"time"
)

type ByteAddresses struct 
{
	ptrAddresses []uint16
	intCount     uint32
}

const (
	UNSIGNAL_OFFSET_LIMIT_PCT = 2
	UNSIGNAL_ROM_LOAD_MAX     = 131072
	HEADER_SIZE               = 8
)

type RomData struct 
{
	ptrROMData []byte
	lngROMSize int64
	arrLookup  [256]ByteAddresses
}

var globalRand *rand.Rand

func findOffset(byLow_a byte, byHigh_a byte, lngROMSize_a int64) uint16 
{
	var intResult uint16 = 0
	var intRaw uint16 = uint16(byLow_a) | (uint16(byHigh_a) << 8)
	
	if lngROMSize_a >= 131072 
	{
		intResult = intRaw
	} 
	else 
	{
		var lngMax int64 = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100
		if lngMax == 0 
		{
			intResult = 0
		} 
		else 
		{
			intResult = uint16(int64(intRaw) % (lngMax + 1))
		}
	}
	
	return intResult
}

func findROMAddress(ptrLookup_a *[256]ByteAddresses, byTarget_a byte) uint16 
{
	var intResult uint16 = 0
	
	if ptrLookup_a[byTarget_a].intCount > 0 
	{
		var intRandomIdx int = globalRand.Intn(int(ptrLookup_a[byTarget_a].intCount))
		intResult = ptrLookup_a[byTarget_a].ptrAddresses[intRandomIdx]
	}
	
	return intResult
}

func buildLookupTable(ptrRom_a *RomData) 
{
	var arrCounts [256]uint32
	var lngHeaderSize int64 = 0
	var lngI int64 = 0
	var intI int = 0
	
	// Initialize lookup array
	for intI = 0; intI < 256; intI++ 
	{
		ptrRom_a.arrLookup[intI].ptrAddresses = nil
		ptrRom_a.arrLookup[intI].intCount = 0
	}
	
	// Header addresses must be within the first 64KB (16-bit addresses)
	lngHeaderSize = ptrRom_a.lngROMSize
	if lngHeaderSize > 65536 
	{
		lngHeaderSize = 65536
	}
	
	// Count occurrences
	for lngI = 0; lngI < lngHeaderSize; lngI++ 
	{
		arrCounts[ptrRom_a.ptrROMData[lngI]]++
	}
	
	// Allocate memory for each byte value
	for intI = 0; intI < 256; intI++ 
	{
		if arrCounts[intI] > 0 
		{
			ptrRom_a.arrLookup[intI].ptrAddresses = make([]uint16, 0, arrCounts[intI])
			ptrRom_a.arrLookup[intI].intCount = 0
		}
	}
	
	// Fill addresses
	for lngI = 0; lngI < lngHeaderSize; lngI++ 
	{
		var by byte = ptrRom_a.ptrROMData[lngI]
		ptrRom_a.arrLookup[by].ptrAddresses = append(ptrRom_a.arrLookup[by].ptrAddresses, uint16(lngI))
		ptrRom_a.arrLookup[by].intCount++
	}
	
	// Seed rand based on ROM content
	var intRomHash uint64 = 0
	for lngI = 0; lngI < ptrRom_a.lngROMSize; lngI++ 
	{
		intRomHash = intRomHash*33 + uint64(ptrRom_a.ptrROMData[lngI])
	}

	intRomHash ^= uint64(time.Now().UnixNano())

	globalRand = rand.New(rand.NewSource(int64(intRomHash)))
}

func loadRom(strFilename_a string) (*RomData, error) 
{
	var ptrRom *RomData = nil
	var ptrFile *os.File = nil
	var info os.FileInfo = nil
	var err error = nil
	var lngSize int64 = 0
	
	ptrFile, err = os.Open(strFilename_a)
	if err == nil 
	{
		defer ptrFile.Close()
		
		info, err = ptrFile.Stat()
		if err == nil 
		{
			lngSize = info.Size()
			if lngSize > UNSIGNAL_ROM_LOAD_MAX 
			{
				lngSize = UNSIGNAL_ROM_LOAD_MAX
			}
			
			var arrBuf []byte = make([]byte, lngSize)
			_, err = io.ReadFull(ptrFile, arrBuf)
			if err == nil 
			{
				ptrRom = &RomData{}
				ptrRom.ptrROMData = arrBuf
				ptrRom.lngROMSize = lngSize
				
				// Pre-build lookup table for reuse across multiple encodes
				buildLookupTable(ptrRom)
			}
		}
	}
	
	return ptrRom, err
}

func unloadRom(ptrRom_a *RomData) 
{
	// In Go, garbage collector handles this, but method kept for symmetry
	ptrRom_a.ptrROMData = nil
	ptrRom_a.lngROMSize = 0
	for intI := 0; intI < 256; intI++ 
	{
		ptrRom_a.arrLookup[intI].ptrAddresses = nil
		ptrRom_a.arrLookup[intI].intCount = 0
	}
}

func encodeFile(ptrRom_a *RomData, strInputFile_a string, strOutputFile_a string) bool 
{
	var blnSuccess bool = false
	var arrOffsetLookup [256]ByteAddresses
	var arrOffsetCounts [256]uint32
	var byOffsetLow byte = 0
	var byOffsetHigh byte = 0
	var byPrefixLen byte = 0
	var bySuffixLen byte = 0
	var intROMOffset uint16 = 0
	var intH1 uint16 = 0
	var intH2 uint16 = 0
	var intH3 uint16 = 0
	var intH4 uint16 = 0
	var ptrPrefix []byte = nil
	var ptrSuffix []byte = nil
	var lngEffectiveSize int64 = 0
	var lngI int64 = 0
	var intI int = 0
	var blnLookupValid bool = true
	var ptrInput *os.File = nil
	var ptrOutput *os.File = nil
	var err error = nil
	var arrBuf []byte = make([]byte, 1)
	
	// Initialize offset lookup array
	for intI = 0; intI < 256; intI++ 
	{
		arrOffsetLookup[intI].ptrAddresses = nil
		arrOffsetLookup[intI].intCount = 0
	}
	
	// Generate header values
	byOffsetLow = byte(globalRand.Intn(256))
	byOffsetHigh = byte(globalRand.Intn(256))
	byPrefixLen = byte(globalRand.Intn(246) + 10)
	bySuffixLen = byte(globalRand.Intn(246) + 10)
	
	// Check that ROM contains required byte values using pre-built lookup
	if ptrRom_a.arrLookup[byOffsetLow].intCount > 0 && 
	   ptrRom_a.arrLookup[byOffsetHigh].intCount > 0 &&
	   ptrRom_a.arrLookup[byPrefixLen].intCount > 0 && 
	   ptrRom_a.arrLookup[bySuffixLen].intCount > 0 
	{
		intROMOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize)
		
		intH1 = findROMAddress(&ptrRom_a.arrLookup, byOffsetLow)
		intH2 = findROMAddress(&ptrRom_a.arrLookup, byOffsetHigh)
		intH3 = findROMAddress(&ptrRom_a.arrLookup, byPrefixLen)
		intH4 = findROMAddress(&ptrRom_a.arrLookup, bySuffixLen)
		
		// Generate random prefix and suffix
		ptrPrefix = make([]byte, byPrefixLen)
		ptrSuffix = make([]byte, bySuffixLen)
		for intI = 0; intI < int(byPrefixLen); intI++ 
		{
			ptrPrefix[intI] = byte(globalRand.Intn(256))
		}
		for intI = 0; intI < int(bySuffixLen); intI++ 
		{
			ptrSuffix[intI] = byte(globalRand.Intn(256))
		}
		
		// Calculate effective encoding window
		lngEffectiveSize = ptrRom_a.lngROMSize - int64(intROMOffset)
		if lngEffectiveSize > 65536 
		{
			lngEffectiveSize = 65536
		}
		
		// Build offset lookup tables for the encoding window
		for lngI = 0; lngI < lngEffectiveSize; lngI++ 
		{
			arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset + uint16(lngI)]]++
		}
		
		// Initialize offset lookup array
		for intI = 0; intI < 256; intI++ 
		{
			arrOffsetLookup[intI].ptrAddresses = nil
			arrOffsetLookup[intI].intCount = 0
		}
		
		for intI = 0; intI < 256 && blnLookupValid; intI++ 
		{
			if arrOffsetCounts[intI] > 0 
			{
				arrOffsetLookup[intI].ptrAddresses = make([]uint16, 0, arrOffsetCounts[intI])
				if arrOffsetLookup[intI].ptrAddresses != nil 
				{
					arrOffsetLookup[intI].intCount = 0
				} 
				else 
				{
					blnLookupValid = false
				}
			}
		}
		
		for lngI = 0; lngI < lngEffectiveSize && blnLookupValid; lngI++ 
		{
			var by byte = ptrRom_a.ptrROMData[intROMOffset + uint16(lngI)]
			arrOffsetLookup[by].ptrAddresses = append(arrOffsetLookup[by].ptrAddresses, uint16(lngI))
			arrOffsetLookup[by].intCount++
		}
		
		if blnLookupValid 
		{
			ptrInput, err = os.Open(strInputFile_a)
			if err == nil 
			{
				defer ptrInput.Close()
				
				ptrOutput, err = os.Create(strOutputFile_a)
				if err == nil 
				{
					defer ptrOutput.Close()
					
					// Write header (4 x 16-bit addresses)
					binary.Write(ptrOutput, binary.LittleEndian, intH1)
					binary.Write(ptrOutput, binary.LittleEndian, intH2)
					binary.Write(ptrOutput, binary.LittleEndian, intH3)
					binary.Write(ptrOutput, binary.LittleEndian, intH4)
					
					// Write prefix
					ptrOutput.Write(ptrPrefix)
					
					// Stream-encode input
					for 
					{
						_, err = ptrInput.Read(arrBuf)
						if err != nil 
						{
							break
						}
						
						var by byte = arrBuf[0]
						if arrOffsetLookup[by].intCount > 0 
						{
							var intRandomIdx int = globalRand.Intn(int(arrOffsetLookup[by].intCount))
							var intAddress uint16 = arrOffsetLookup[by].ptrAddresses[intRandomIdx]
							binary.Write(ptrOutput, binary.LittleEndian, intAddress)
						}
					}
					
					// Write suffix
					ptrOutput.Write(ptrSuffix)
					
					blnSuccess = true
				}
			}
		}
	}
	else 
	{
		fmt.Fprintf(os.Stderr, "Error: ROM does not contain required byte values for UNSIGNAL header\n")
	}
	
	return blnSuccess
}

func main() 
{
	var intResult int = 1
	var ptrRom *RomData = nil
	var err error = nil
	var blnEncodeOk bool = false
	
	fmt.Println("UNSIGNAL Protocol Encoder v20260303")
	fmt.Println("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n")

	strArgs := os.Args
	if len(strArgs) == 4 
	{
		ptrRom, err = loadRom(strArgs[1])
		if err == nil && ptrRom != nil 
		{
			blnEncodeOk = encodeFile(ptrRom, strArgs[2], strArgs[3])
			
			if blnEncodeOk 
			{
				intResult = 0
			} 
			else 
			{
				fmt.Fprintf(os.Stderr, "Encode failed\n")
			}
			
			unloadRom(ptrRom)
		} 
		else 
		{
			if err != nil 
			{
				fmt.Fprintf(os.Stderr, "Failed to load ROM: %v\n", err)
			} 
			else 
			{
				fmt.Fprintf(os.Stderr, "Failed to load ROM\n")
			}
		}
	} 
	else 
	{
		fmt.Fprintf(os.Stderr, "Usage: %s <romfile> <inputdatafile> <encodedoutput>\n", strArgs[0])
	}
	
	os.Exit(intResult)
}