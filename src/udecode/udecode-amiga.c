// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

// Amiga Version

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#define UNSIGNAL_OFFSET_LIMIT_PCT 2
#define UNSIGNAL_ROM_LOAD_MAX 131072L
#define HEADER_SIZE 8

typedef struct 
{
    uint8_t* ptrROMData;
    long lngROMSize;
} RomData;

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

static RomData* loadRom(const char* strFilename_a)
{
    RomData* ptrRom = NULL;
    FILE* ptrROM = NULL;
    
    ptrROM = fopen(strFilename_a, "rb");
    if (ptrROM != NULL) 
    {
        ptrRom = (RomData*)malloc(sizeof(RomData));
        if (ptrRom != NULL) 
        {
            fseek(ptrROM, 0, SEEK_END);
            ptrRom->lngROMSize = ftell(ptrROM);
            fseek(ptrROM, 0, SEEK_SET);
            
            if (ptrRom->lngROMSize > UNSIGNAL_ROM_LOAD_MAX) 
            {
                ptrRom->lngROMSize = UNSIGNAL_ROM_LOAD_MAX;
            }
            
            ptrRom->ptrROMData = (uint8_t*)malloc(ptrRom->lngROMSize);
            if (ptrRom->ptrROMData != NULL) 
            {
                fread(ptrRom->ptrROMData, 1, ptrRom->lngROMSize, ptrROM);
            } 
            else 
            {
                free(ptrRom);
                ptrRom = NULL;
            }
        }
        fclose(ptrROM);
    }
    
    return ptrRom;
}

static void freeRom(RomData* ptrRom_a)
{
    if (ptrRom_a != NULL) 
    {
        if (ptrRom_a->ptrROMData != NULL) 
        {
            free(ptrRom_a->ptrROMData);
        }
        free(ptrRom_a);
    }
}

static int decodeFile(const RomData* ptrRom_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    int intResult = 0;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    uint8_t arrBuf[2];
    uint16_t arrAddrs[4] = {0};
    uint8_t byOffsetLow = 0;
    uint8_t byOffsetHigh = 0;
    uint8_t byPrefixLen = 0;
    uint8_t bySuffixLen = 0;
    uint16_t intOffset = 0;
    long lngInputSize = 0;
    long lngDataSize = 0;
    long lngSlots = 0;
    long lngEffSize = 0;
    int intI = 0;
    
    ptrInput = fopen(strInputFile_a, "rb");
    if (ptrInput != NULL) 
    {
        fseek(ptrInput, 0, SEEK_END);
        lngInputSize = ftell(ptrInput);
        fseek(ptrInput, 0, SEEK_SET);
        
        // Read header
        for (intI = 0; intI < 4; intI++) 
        {
            if (fread(arrBuf, 2, 1, ptrInput) != 1) 
            {
                break;
            }
            arrAddrs[intI] = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
            if (arrAddrs[intI] >= ptrRom_a->lngROMSize) 
            {
                break;
            }
        }
        
        if (intI == 4) 
        {
            byOffsetLow = ptrRom_a->ptrROMData[arrAddrs[0]];
            byOffsetHigh = ptrRom_a->ptrROMData[arrAddrs[1]];
            byPrefixLen = ptrRom_a->ptrROMData[arrAddrs[2]];
            bySuffixLen = ptrRom_a->ptrROMData[arrAddrs[3]];
            
            intOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a->lngROMSize);
            
            lngDataSize = lngInputSize - HEADER_SIZE - (long)byPrefixLen - (long)bySuffixLen;
            lngSlots = lngDataSize / 2;
            
            if (lngSlots >= 0) 
            {
                // Skip prefix
                for (intI = 0; intI < (int)byPrefixLen; intI++) 
                {
                    if (fgetc(ptrInput) == EOF) 
                    {
                        break;
                    }
                }
                
                if (intI == (int)byPrefixLen) 
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput != NULL) 
                    {
                        lngEffSize = ptrRom_a->lngROMSize - (long)intOffset;
                        if (lngEffSize > 65536L) 
                        {
                            lngEffSize = 65536L;
                        }
                        
                        // Decode
                        for (intI = 0; intI < (int)lngSlots; intI++) 
                        {
                            if (fread(arrBuf, 2, 1, ptrInput) != 1) 
                            {
                                break;
                            }
                            
                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            if ((long)intAddr < lngEffSize) 
                            {
                                if (fputc(ptrRom_a->ptrROMData[(long)intOffset + (long)intAddr], ptrOutput) == EOF) 
                                {
                                    break;
                                }
                            }
                        }
                        
                        if (intI == (int)lngSlots) 
                        {
                            intResult = 1;
                        }
                        
                        fclose(ptrOutput);
                    }
                }
            }
        }
        fclose(ptrInput);
    }
    
    return intResult;
}

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomData* ptrRom = NULL;
    int intDecodeOk = 0;
    
    printf("UNSIGNAL Protocol Decoder\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a == 4) 
    {
        ptrRom = loadRom(strArgv_a[1]);
        if (ptrRom != NULL) 
        {
            intDecodeOk = decodeFile(ptrRom, strArgv_a[2], strArgv_a[3]);
            freeRom(ptrRom);
            
            if (intDecodeOk != 0) 
            {
                intResult = 0;
            } 
            else 
            {
                fprintf(stderr, "Decode failed\n");
            }
        } 
        else 
        {
            perror("Failed to load ROM");
        }
    } 
    else 
    {
        fprintf(stderr, "Usage: %s <romfile> <encoded> <output>\n", strArgv_a[0]);
    }
    
    return intResult;
}