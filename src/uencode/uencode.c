// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

// Windows & Linux Version

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <time.h>
#ifdef _WIN32
    #include <fcntl.h>
    #include <io.h>
#endif

typedef struct
{
    uint32_t* ptrAddresses;
    uint32_t intCount;
} ByteAddresses;

typedef struct
{
    uint8_t* ptrROMData;
    long lngROMSize;
    ByteAddresses arrLookup[256];
} RomData;

#define UNSIGNAL_OFFSET_LIMIT_PCT 2
#define UNSIGNAL_ROM_LOAD_MAX 131072L
#define UNSIGNAL_HEADER_SIZE 8

static uint16_t findOffset(uint8_t byLow_a, uint8_t byHigh_a, long lngROMSize_a)
{
    uint16_t intResult = 0;
    uint16_t intRaw = (uint16_t)byLow_a | ((uint16_t)byHigh_a << 8);
    
    if (lngROMSize_a >= 131072L)
    {
        intResult = intRaw;
    }
    else
    {
        long lngMax = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
        if (lngMax == 0)
        {
            intResult = 0;
        }
        else
        {
            intResult = (uint16_t)(intRaw % (lngMax + 1));
        }
    }
    
    return intResult;
}

static uint32_t findROMAddress(const ByteAddresses* ptrLookup_a, uint8_t byTarget_a)
{
    uint32_t intResult = 0;
    
    if (ptrLookup_a[byTarget_a].intCount > 0)
    {
        uint32_t intRandomIdx = rand() % ptrLookup_a[byTarget_a].intCount;
        intResult = ptrLookup_a[byTarget_a].ptrAddresses[intRandomIdx];
    }
    
    return intResult;
}

static void buildLookupTable(RomData* ptrRom_a)
{
    uint32_t arrCounts[256] = {0};
    long lngHeaderSize = 0;
    long lngI = 0;
    int intI = 0;
    
    // Initialize lookup array
    for (intI = 0; intI < 256; intI++)
    {
        ptrRom_a->arrLookup[intI].ptrAddresses = NULL;
        ptrRom_a->arrLookup[intI].intCount = 0;
    }
    
    // Header addresses must be within the first 64KB (16-bit addresses)
    lngHeaderSize = (ptrRom_a->lngROMSize > 65536L) ? 65536L : ptrRom_a->lngROMSize;
    
    // Count occurrences
    for (lngI = 0; lngI < lngHeaderSize; lngI++)
    {
        arrCounts[ptrRom_a->ptrROMData[lngI]]++;
    }
    
    // Allocate memory for each byte value
    for (intI = 0; intI < 256; intI++)
    {
        if (arrCounts[intI] > 0)
        {
            ptrRom_a->arrLookup[intI].ptrAddresses = 
                (uint32_t*)malloc(arrCounts[intI] * sizeof(uint32_t));
            ptrRom_a->arrLookup[intI].intCount = 0;
        }
    }
    
    // Fill addresses
    for (lngI = 0; lngI < lngHeaderSize; lngI++)
    {
        uint8_t by = ptrRom_a->ptrROMData[lngI];
        ptrRom_a->arrLookup[by].ptrAddresses[ptrRom_a->arrLookup[by].intCount++] = (uint32_t)lngI;
    }
}

static RomData* loadRom(const char* strFilename_a)
{
    RomData* ptrRom = NULL;
    FILE* ptrROM = NULL;
    
    ptrRom = (RomData*)malloc(sizeof(RomData));
    if (ptrRom)
    {
        // Initialize
        memset(ptrRom, 0, sizeof(RomData));
        
        ptrROM = fopen(strFilename_a, "rb");
        if (ptrROM)
        {
            fseek(ptrROM, 0, SEEK_END);
            ptrRom->lngROMSize = ftell(ptrROM);
            fseek(ptrROM, 0, SEEK_SET);
            
            if (ptrRom->lngROMSize > UNSIGNAL_ROM_LOAD_MAX)
            {
                ptrRom->lngROMSize = UNSIGNAL_ROM_LOAD_MAX;
            }
            
            ptrRom->ptrROMData = (uint8_t*)malloc(ptrRom->lngROMSize);
            if (ptrRom->ptrROMData)
            {
                fread(ptrRom->ptrROMData, 1, ptrRom->lngROMSize, ptrROM);
                
                // Pre-build lookup table for reuse across multiple encodes
                buildLookupTable(ptrRom);
            }
            else
            {
                free(ptrRom);
                ptrRom = NULL;
            }
            
            fclose(ptrROM);
        }
        else
        {
            free(ptrRom);
            ptrRom = NULL;
        }
    }
    
    return ptrRom;
}

static void unloadRom(RomData* ptrRom_a)
{
    int intI = 0;
    
    if (ptrRom_a)
    {
        if (ptrRom_a->ptrROMData)
        {
            free(ptrRom_a->ptrROMData);
        }
        
        for (intI = 0; intI < 256; intI++)
        {
            if (ptrRom_a->arrLookup[intI].ptrAddresses)
            {
                free(ptrRom_a->arrLookup[intI].ptrAddresses);
            }
        }
        
        free(ptrRom_a);
    }
}

static bool encodeFile(const RomData* ptrRom_a, 
                       const char* strInputFile_a, 
                       const char* strOutputFile_a)
{
    bool blnSuccess = false;
    ByteAddresses arrOffsetLookup[256] = {0};
    uint32_t arrOffsetCounts[256] = {0};
    uint8_t byOffsetLow = 0;
    uint8_t byOffsetHigh = 0;
    uint8_t byPrefixLen = 0;
    uint8_t bySuffixLen = 0;
    uint16_t intROMOffset = 0;
    uint16_t intH1 = 0;
    uint16_t intH2 = 0;
    uint16_t intH3 = 0;
    uint16_t intH4 = 0;
    uint8_t* ptrPrefix = NULL;
    uint8_t* ptrSuffix = NULL;
    long lngEffectiveSize = 0;
    long lngI = 0;
    int intI = 0;
    int intCh = 0;
    bool blnLookupValid = true;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    
    // Generate header values
    byOffsetLow = (uint8_t)(rand() % 256);
    byOffsetHigh = (uint8_t)(rand() % 256);
    byPrefixLen = (uint8_t)((rand() % 246) + 10);
    bySuffixLen = (uint8_t)((rand() % 246) + 10);
    
    // Check that ROM contains required byte values using pre-built lookup
    if (ptrRom_a->arrLookup[byOffsetLow].intCount > 0 && 
        ptrRom_a->arrLookup[byOffsetHigh].intCount > 0 &&
        ptrRom_a->arrLookup[byPrefixLen].intCount > 0 && 
        ptrRom_a->arrLookup[bySuffixLen].intCount > 0)
    {
        intROMOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a->lngROMSize);
        
        intH1 = (uint16_t)findROMAddress(ptrRom_a->arrLookup, byOffsetLow);
        intH2 = (uint16_t)findROMAddress(ptrRom_a->arrLookup, byOffsetHigh);
        intH3 = (uint16_t)findROMAddress(ptrRom_a->arrLookup, byPrefixLen);
        intH4 = (uint16_t)findROMAddress(ptrRom_a->arrLookup, bySuffixLen);
        
        // Generate random prefix and suffix
        ptrPrefix = (uint8_t*)malloc(byPrefixLen);
        ptrSuffix = (uint8_t*)malloc(bySuffixLen);
        
        if (ptrPrefix && ptrSuffix)
        {
            for (intI = 0; intI < byPrefixLen; intI++)
            {
                ptrPrefix[intI] = (uint8_t)(rand() % 256);
            }
            for (intI = 0; intI < bySuffixLen; intI++)
            {
                ptrSuffix[intI] = (uint8_t)(rand() % 256);
            }
            
            // Calculate effective encoding window
            lngEffectiveSize = ptrRom_a->lngROMSize - intROMOffset;
            if (lngEffectiveSize > 65536L)
            {
                lngEffectiveSize = 65536L;
            }
            
            // Build offset lookup tables for the encoding window
            for (lngI = 0; lngI < lngEffectiveSize; lngI++)
            {
                arrOffsetCounts[ptrRom_a->ptrROMData[intROMOffset + lngI]]++;
            }
            
            // Initialize offset lookup array
            for (intI = 0; intI < 256; intI++)
            {
                arrOffsetLookup[intI].ptrAddresses = NULL;
                arrOffsetLookup[intI].intCount = 0;
            }
            
            for (intI = 0; intI < 256 && blnLookupValid; intI++)
            {
                if (arrOffsetCounts[intI] > 0)
                {
                    arrOffsetLookup[intI].ptrAddresses = 
                        (uint32_t*)malloc(arrOffsetCounts[intI] * sizeof(uint32_t));
                    if (arrOffsetLookup[intI].ptrAddresses)
                    {
                        arrOffsetLookup[intI].intCount = 0;
                    }
                    else
                    {
                        blnLookupValid = false;
                    }
                }
            }
            
            for (lngI = 0; lngI < lngEffectiveSize && blnLookupValid; lngI++)
            {
                uint8_t by = ptrRom_a->ptrROMData[intROMOffset + lngI];
                arrOffsetLookup[by].ptrAddresses[arrOffsetLookup[by].intCount++] = (uint32_t)lngI;
            }
            
            if (blnLookupValid)
            {
                ptrInput = fopen(strInputFile_a, "rb");
                if (ptrInput)
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput)
                    {
                        // Write header (4 x 16-bit addresses)
                        fwrite(&intH1, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH2, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH3, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH4, sizeof(uint16_t), 1, ptrOutput);
                        
                        // Write prefix
                        fwrite(ptrPrefix, 1, byPrefixLen, ptrOutput);
                        
                        // Stream-encode input
                        intCh = fgetc(ptrInput);
                        while (intCh != EOF)
                        {
                            uint8_t by = (uint8_t)intCh;
                            if (arrOffsetLookup[by].intCount > 0)
                            {
                                uint32_t intRandomIdx = rand() % arrOffsetLookup[by].intCount;
                                uint16_t intAddress = (uint16_t)arrOffsetLookup[by].ptrAddresses[intRandomIdx];
                                fwrite(&intAddress, sizeof(uint16_t), 1, ptrOutput);
                            }
                            intCh = fgetc(ptrInput);
                        }
                        
                        // Write suffix
                        fwrite(ptrSuffix, 1, bySuffixLen, ptrOutput);
                        
                        blnSuccess = true;
                        
                        fclose(ptrOutput);
                    }
                    fclose(ptrInput);
                }
            }
        }
        
        if (ptrPrefix) free(ptrPrefix);
        if (ptrSuffix) free(ptrSuffix);
        
        // Cleanup offset lookup
        for (intI = 0; intI < 256; intI++)
        {
            if (arrOffsetLookup[intI].ptrAddresses)
            {
                free(arrOffsetLookup[intI].ptrAddresses);
            }
        }
    }
    else
    {
        fprintf(stderr, "Error: ROM does not contain required byte values for UNSIGNAL header\n");
    }
    
    return blnSuccess;
}

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomData* ptrRom = NULL;
    bool blnEncodeOk = false;
    
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Protocol Encoder\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a == 4)
    {
        srand((unsigned int)time(NULL));
        
        ptrRom = loadRom(strArgv_a[1]);
        if (ptrRom)
        {
            blnEncodeOk = encodeFile(ptrRom, strArgv_a[2], strArgv_a[3]);
            
            if (blnEncodeOk)
            {
                intResult = 0;
            }
            else
            {
                fprintf(stderr, "Encode failed\n");
            }
            
            unloadRom(ptrRom);
        }
        else
        {
            perror("Failed to load ROM");
        }
    }
    else
    {
        fprintf(stderr, "Usage: %s <romfile> <inputdatafile> <encodedoutput>\n", strArgv_a[0]);
    }
    
    return intResult;
}