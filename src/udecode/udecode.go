// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Windows & Linux Version

package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"os"
)

const (
	UNSIGNAL_OFFSET_LIMIT_PCT = 2
	UNSIGNAL_ROM_LOAD_MAX     = 131072
	HEADER_SIZE               = 8
)

type RomData struct {
	ptrROMData []byte
	lngROMSize int64
}

func findOffset(byLow_a byte, byHigh_a byte, lngROMSize_a int64) uint16 
{
	var intResult uint16 = 0
	intRaw := uint16(byLow_a) | (uint16(byHigh_a) << 8)
	
	if lngROMSize_a >= 131072 
	{
		intResult = intRaw
	} 
	else 
	{
		lngMax := (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100
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

func loadRom(strFilename_a string) (*RomData, error) 
{
	var ptrRom *RomData = nil
	var ptrFile *os.File = nil
	var err error = nil
	
	ptrFile, err = os.Open(strFilename_a)
	if err == nil 
	{
		defer ptrFile.Close()
		
		ptrRom = &RomData{}
		arrBuf := make([]byte, UNSIGNAL_ROM_LOAD_MAX)
		var n int = 0
		
		n, err = io.ReadFull(ptrFile, arrBuf)
		if err == nil || err == io.ErrUnexpectedEOF 
		{
			ptrRom.ptrROMData = arrBuf[:n]
			ptrRom.lngROMSize = int64(len(ptrRom.ptrROMData))
		} 
		else 
		{
			ptrRom = nil
		}
	}
	
	return ptrRom, err
}

func decodeFile(ptrRom_a *RomData, strInputFile_a string, strOutputFile_a string) bool 
{
	var blnSuccess bool = false
	var ptrInput *os.File = nil
	var ptrOutput *os.File = nil
	var err error = nil
	
	ptrInput, err = os.Open(strInputFile_a)
	if err == nil 
	{
		defer ptrInput.Close()
		
		var ptrInputInfo os.FileInfo = nil
		ptrInputInfo, err = ptrInput.Stat()
		if err == nil 
		{
			lngInputSize := ptrInputInfo.Size()
			var arrAddrs [4]uint16
			var intI int = 0
			
			// Read header
			for intI = 0; intI < 4; intI++ 
			{
				err = binary.Read(ptrInput, binary.LittleEndian, &arrAddrs[intI])
				if err != nil 
				{
					break
				}
				if int64(arrAddrs[intI]) >= ptrRom_a.lngROMSize 
				{
					err = fmt.Errorf("header address out of bounds")
					break
				}
			}
			
			if err == nil 
			{
				byOffsetLow := ptrRom_a.ptrROMData[arrAddrs[0]]
				byOffsetHigh := ptrRom_a.ptrROMData[arrAddrs[1]]
				byPrefixLen := ptrRom_a.ptrROMData[arrAddrs[2]]
				bySuffixLen := ptrRom_a.ptrROMData[arrAddrs[3]]
				
				intOffset := findOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize)
				
				lngDataSize := lngInputSize - HEADER_SIZE - int64(byPrefixLen) - int64(bySuffixLen)
				lngSlots := lngDataSize / 2
				
				if lngSlots >= 0 
				{
					// Skip prefix
					if byPrefixLen > 0 
					{
						arrSkip := make([]byte, byPrefixLen)
						_, err = io.ReadFull(ptrInput, arrSkip)
					}
					
					if err == nil 
					{
						ptrOutput, err = os.Create(strOutputFile_a)
						if err == nil 
						{
							defer ptrOutput.Close()
							
							lngEffSize := ptrRom_a.lngROMSize - int64(intOffset)
							if lngEffSize > 65536 
							{
								lngEffSize = 65536
							}
							
							arrAddrBuf := make([]byte, 2)
							
							// Decode
							for intI = 0; intI < int(lngSlots); intI++ 
							{
								_, err = io.ReadFull(ptrInput, arrAddrBuf)
								if err != nil 
								{
									break
								}
								
								intAddr := int64(binary.LittleEndian.Uint16(arrAddrBuf))
								if intAddr < lngEffSize 
								{
									_, err = ptrOutput.Write([]byte{ptrRom_a.ptrROMData[int64(intOffset)+intAddr]})
									if err != nil 
									{
										break
									}
								}
							}
							
							if err == nil 
							{
								blnSuccess = true
							}
						}
					}
				}
			}
		}
	}
	
	return blnSuccess
}

func main() 
{
	var intResult int = 1
	var ptrRom *RomData = nil
	var err error = nil
	var blnDecodeOk bool = false
	
	fmt.Println("UNSIGNAL Protocol Decoder v20260301")
	fmt.Println("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n")

	strArgs := os.Args
	if len(strArgs) == 4 
	{
		ptrRom, err = loadRom(strArgs[1])
		if err == nil && ptrRom != nil 
		{
			blnDecodeOk = decodeFile(ptrRom, strArgs[2], strArgs[3])
			
			if blnDecodeOk 
			{
				intResult = 0
			} 
			else 
			{
				fmt.Fprintf(os.Stderr, "Decode failed\n")
			}
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
		fmt.Fprintf(os.Stderr, "Usage: %s <romfile> <encoded> <output>\n", strArgs[0])
	}
	
	os.Exit(intResult)
}