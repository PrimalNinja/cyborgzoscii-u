#!/usr/bin/env python3
# Cyborg UNSIGNAL Protocol v20260301
# (c) 2026 Cyborg Unicorn Pty Ltd.
# This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
# ZOSCII core logic remains under MIT License.

import sys
import random
import struct
import os
import time

class ByteAddresses:
    def __init__(self):
        self.ptrAddresses = []
        self.intCount = 0

class RomData:
    def __init__(self):
        self.ptrROMData = b''
        self.lngROMSize = 0
        self.arrLookup = [ByteAddresses() for _ in range(256)]

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

def findROMAddress(ptrLookup_a, byTarget_a):
    intResult = 0
    
    if ptrLookup_a[byTarget_a].intCount > 0:
        intRandomIdx = random.randint(0, ptrLookup_a[byTarget_a].intCount - 1)
        intResult = ptrLookup_a[byTarget_a].ptrAddresses[intRandomIdx]
    
    return intResult

def buildLookupTable(ptrRom_a):
    arrCounts = [0] * 256
    lngHeaderSize = 0
    
    # Initialize lookup array
    ptrRom_a.arrLookup = [ByteAddresses() for _ in range(256)]
    for intI in range(256):
        ptrRom_a.arrLookup[intI].ptrAddresses = []
        ptrRom_a.arrLookup[intI].intCount = 0
    
    # Header addresses must be within the first 64KB (16-bit addresses)
    lngHeaderSize = ptrRom_a.lngROMSize
    if lngHeaderSize > 65536:
        lngHeaderSize = 65536
    
    # Count occurrences
    for lngI in range(lngHeaderSize):
        arrCounts[ptrRom_a.ptrROMData[lngI]] += 1
    
    # Allocate memory for each byte value
    for intI in range(256):
        if arrCounts[intI] > 0:
            ptrRom_a.arrLookup[intI].ptrAddresses = [0] * arrCounts[intI]
            ptrRom_a.arrLookup[intI].intCount = 0
    
    # Fill addresses
    for lngI in range(lngHeaderSize):
        by = ptrRom_a.ptrROMData[lngI]
        ptrRom_a.arrLookup[by].ptrAddresses[ptrRom_a.arrLookup[by].intCount] = lngI
        ptrRom_a.arrLookup[by].intCount += 1

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
                    
                    # Pre-build lookup table for reuse across multiple encodes
                    buildLookupTable(ptrRom)
        except IOError:
            ptrRom = None
    
    return ptrRom

def unloadRom(ptrRom_a):
    # In Python, garbage collector handles this, but method kept for symmetry
    ptrRom_a.ptrROMData = b''
    ptrRom_a.lngROMSize = 0
    ptrRom_a.arrLookup = [ByteAddresses() for _ in range(256)]

def encodeFile(ptrRom_a, strInputFile_a, strOutputFile_a):
    blnSuccess = False
    ptrInput = None
    ptrOutput = None
    arrOffsetLookup = [ByteAddresses() for _ in range(256)]
    arrOffsetCounts = [0] * 256
    byOffsetLow = 0
    byOffsetHigh = 0
    byPrefixLen = 0
    bySuffixLen = 0
    intROMOffset = 0
    intH1 = 0
    intH2 = 0
    intH3 = 0
    intH4 = 0
    ptrPrefix = b''
    ptrSuffix = b''
    lngEffectiveSize = 0
    blnLookupValid = True
    
    # Initialize offset lookup array
    for intI in range(256):
        arrOffsetLookup[intI].ptrAddresses = []
        arrOffsetLookup[intI].intCount = 0
    
    # Generate header values
    byOffsetLow = random.randint(0, 255)
    byOffsetHigh = random.randint(0, 255)
    byPrefixLen = random.randint(10, 255)
    bySuffixLen = random.randint(10, 255)
    
    # Check that ROM contains required byte values using pre-built lookup
    if (ptrRom_a.arrLookup[byOffsetLow].intCount > 0 and 
        ptrRom_a.arrLookup[byOffsetHigh].intCount > 0 and
        ptrRom_a.arrLookup[byPrefixLen].intCount > 0 and 
        ptrRom_a.arrLookup[bySuffixLen].intCount > 0):
        
        intROMOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a.lngROMSize)
        
        intH1 = findROMAddress(ptrRom_a.arrLookup, byOffsetLow)
        intH2 = findROMAddress(ptrRom_a.arrLookup, byOffsetHigh)
        intH3 = findROMAddress(ptrRom_a.arrLookup, byPrefixLen)
        intH4 = findROMAddress(ptrRom_a.arrLookup, bySuffixLen)
        
        # Generate random prefix and suffix
        ptrPrefix = bytes([random.randint(0, 255) for _ in range(byPrefixLen)])
        ptrSuffix = bytes([random.randint(0, 255) for _ in range(bySuffixLen)])
        
        # Calculate effective encoding window
        lngEffectiveSize = ptrRom_a.lngROMSize - intROMOffset
        if lngEffectiveSize > 65536:
            lngEffectiveSize = 65536
        
        # Build offset lookup tables for the encoding window
        for lngI in range(lngEffectiveSize):
            arrOffsetCounts[ptrRom_a.ptrROMData[intROMOffset + lngI]] += 1
        
        for intI in range(256):
            if arrOffsetCounts[intI] > 0:
                arrOffsetLookup[intI].ptrAddresses = [0] * arrOffsetCounts[intI]
                arrOffsetLookup[intI].intCount = 0
        
        for lngI in range(lngEffectiveSize):
            by = ptrRom_a.ptrROMData[intROMOffset + lngI]
            arrOffsetLookup[by].ptrAddresses[arrOffsetLookup[by].intCount] = lngI
            arrOffsetLookup[by].intCount += 1
        
        if os.path.exists(strInputFile_a):
            try:
                ptrInput = open(strInputFile_a, 'rb')
                ptrOutput = open(strOutputFile_a, 'wb')
                
                # Write header (4 x 16-bit addresses)
                ptrOutput.write(struct.pack('<H', intH1))
                ptrOutput.write(struct.pack('<H', intH2))
                ptrOutput.write(struct.pack('<H', intH3))
                ptrOutput.write(struct.pack('<H', intH4))
                
                # Write prefix
                ptrOutput.write(ptrPrefix)
                
                # Stream-encode input
                while True:
                    intCh = ptrInput.read(1)
                    if not intCh:
                        break
                    by = intCh[0]
                    if arrOffsetLookup[by].intCount > 0:
                        intRandomIdx = random.randint(0, arrOffsetLookup[by].intCount - 1)
                        intAddress = arrOffsetLookup[by].ptrAddresses[intRandomIdx]
                        ptrOutput.write(struct.pack('<H', intAddress))
                
                # Write suffix
                ptrOutput.write(ptrSuffix)
                
                blnSuccess = True
                ptrOutput.close()
                ptrInput.close()
            except IOError:
                if ptrOutput:
                    ptrOutput.close()
                if ptrInput:
                    ptrInput.close()
    else:
        print("Error: ROM does not contain required byte values for UNSIGNAL header", file=sys.stderr)
    
    return blnSuccess

def main():
    intResult = 1
    ptrRom = None
    blnEncodeOk = False
    
    print("UNSIGNAL Protocol Encoder")
    print("(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n")
    
    if len(sys.argv) == 4:
        random.seed(int(time.time()))
        
        ptrRom = loadRom(sys.argv[1])
        if ptrRom:
            blnEncodeOk = encodeFile(ptrRom, sys.argv[2], sys.argv[3])
            
            if blnEncodeOk:
                intResult = 0
                print("Encode successful!")
            else:
                print("Encode failed", file=sys.stderr)
            
            unloadRom(ptrRom)
        else:
            print("Failed to load ROM", file=sys.stderr)
    else:
        print(f"Usage: {sys.argv[0]} <romfile> <inputdatafile> <encodedoutput>", file=sys.stderr)
    
    sys.exit(intResult)

if __name__ == "__main__":
    main()