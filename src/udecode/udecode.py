#!/usr/bin/env python3
# Cyborg UNSIGNAL Protocol v20260301
# (c) 2026 Cyborg Unicorn Pty Ltd.
# This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
# ZOSCII core logic remains under MIT License.

import sys
import struct
import os

class RomData:
    def __init__(self):
        self.ptrROMData = b''
        self.lngROMSize = 0

UNSIGNAL_OFFSET_LIMIT_PCT = 2
UNSIGNAL_ROM_LOAD_MAX = 131072
HEADER_SIZE = 8

def findOffset(byLow_a, byHigh_a, lngROMSize_a):
    intResult = 0
    intRaw = (byLow_a) | (byHigh_a << 8)
    
    if lngROMSize_a >= 131072:
        intResult = intRaw
    else:
        lngMax = int((lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100)
        if lngMax == 0:
            intResult = 0
        else:
            intResult = intRaw % (lngMax + 1)
    
    return intResult

def loadRom(strFilename_a):
    ptrRom = None
    
    if os.path.exists(strFilename_a):
        try:
            with open(strFilename_a, 'rb') as ptrFile:
                arrBuf = ptrFile.read(UNSIGNAL_ROM_LOAD_MAX)
                if arrBuf:
                    ptrRom = RomData()
                    ptrRom.ptrROMData = arrBuf
                    ptrRom.lngROMSize = len(arrBuf)
        except IOError:
            ptrRom = None
    
    return ptrRom

def unloadRom(ptrRom_a):
    # In Python, garbage collector handles this, but method kept for symmetry
    ptrRom_a.ptrROMData = b''
    ptrRom_a.lngROMSize = 0

def decodeFile(ptrRom_a, strInputFile_a, strOutputFile_a):
    blnSuccess = False
    ptrInput = None
    ptrOutput = None
    arrBuf = b''
    arrAddrs = [0, 0, 0, 0]
    byOffsetLow = 0
    byOffsetHigh = 0
    byPrefixLen = 0
    bySuffixLen = 0
    intOffset = 0
    lngInputSize = 0
    lngDataSize = 0
    lngSlots = 0
    lngEffSize = 0
    intI = 0
    
    if os.path.exists(strInputFile_a):
        try:
            ptrInput = open(strInputFile_a, 'rb')
            
            # Read header
            for intI in range(4):
                arrBuf = ptrInput.read(2)
                if len(arrBuf) != 2:
                    break
                arrAddrs[intI] = struct.unpack('<H', arrBuf)[0]
                if arrAddrs[intI] >= ptrRom_a.lngROMSize:
                    break
            
            if intI == 3 and len(arrBuf) == 2:  # All 4 headers read successfully
                byOffsetLow = ptrRom_a.ptrROMData[arrAddrs[0]]
                byOffsetHigh = ptrRom_a.ptrROMData[arrAddrs[1]]
                byPrefixLen = ptrRom_a.ptrROMData[arrAddrs[2]]
                bySuffixLen = ptrRom_a.ptrROMData[arrAddrs[3]]
                
                intOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize)
                
                # Calculate number of slots
                ptrInput.seek(0, 2)
                lngInputSize = ptrInput.tell()
                ptrInput.seek(HEADER_SIZE, 0)
                
                lngDataSize = lngInputSize - HEADER_SIZE - byPrefixLen - bySuffixLen
                lngSlots = lngDataSize // 2
                
                if lngSlots >= 0:
                    # Skip prefix
                    for intI in range(byPrefixLen):
                        if not ptrInput.read(1):
                            break
                    
                    if intI == byPrefixLen - 1:
                        ptrOutput = open(strOutputFile_a, 'wb')
                        
                        lngEffSize = ptrRom_a.lngROMSize - intOffset
                        if lngEffSize > 65536:
                            lngEffSize = 65536
                        
                        # Decode
                        for intI in range(lngSlots):
                            arrBuf = ptrInput.read(2)
                            if len(arrBuf) != 2:
                                break
                            intAddr = struct.unpack('<H', arrBuf)[0]
                            if intAddr < lngEffSize:
                                ptrOutput.write(bytes([ptrRom_a.ptrROMData[intOffset + intAddr]]))
                        
                        if intI == lngSlots - 1:
                            blnSuccess = True
                        
                        ptrOutput.close()
            ptrInput.close()
        except IOError:
            if ptrOutput:
                ptrOutput.close()
            if ptrInput:
                ptrInput.close()
    
    return blnSuccess

def main():
    intResult = 1
    ptrRom = None
    blnDecodeOk = False
    
    print("UNSIGNAL Protocol Decoder")
    print("(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n")
    
    if len(sys.argv) == 4:
        ptrRom = loadRom(sys.argv[1])
        if ptrRom:
            blnDecodeOk = decodeFile(ptrRom, sys.argv[2], sys.argv[3])
            
            if blnDecodeOk:
                intResult = 0
                print("Decode successful!")
            else:
                print("Decode failed", file=sys.stderr)
            
            unloadRom(ptrRom)
        else:
            print("Failed to load ROM", file=sys.stderr)
    else:
        print(f"Usage: {sys.argv[0]} <romfile> <encoded> <output>", file=sys.stderr)
    
    sys.exit(intResult)

if __name__ == "__main__":
    main()