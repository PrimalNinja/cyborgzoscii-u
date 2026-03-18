// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <stdbool.h>
#ifdef _WIN32
    #include <fcntl.h>
    #include <io.h>
#endif

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
    if (ptrROM) 
    {
        ptrRom = malloc(sizeof(RomData));
        if (ptrRom) 
        {
            fseek(ptrROM, 0, SEEK_END);
            ptrRom->lngROMSize = ftell(ptrROM);
            fseek(ptrROM, 0, SEEK_SET);
            
            if (ptrRom->lngROMSize > UNSIGNAL_ROM_LOAD_MAX) 
            {
                ptrRom->lngROMSize = UNSIGNAL_ROM_LOAD_MAX;
            }
            
            ptrRom->ptrROMData = malloc(ptrRom->lngROMSize);
            if (ptrRom->ptrROMData) 
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
    if (ptrRom_a) 
    {
        if (ptrRom_a->ptrROMData) 
        {
            free(ptrRom_a->ptrROMData);
        }
        free(ptrRom_a);
    }
}

static bool decodeFile(const RomData* ptrRom_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    bool blnSuccess = false;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    uint8_t arrBuf[2];
    uint16_t arrAddrs[4] = {0};
    uint8_t byOffsetLow = 0, byOffsetHigh = 0, byPrefixLen = 0, bySuffixLen = 0;
    uint16_t intOffset = 0;
    long lngInputSize = 0, lngDataSize = 0, lngSlots = 0, lngEffSize = 0;
    int intI = 0;
    
    ptrInput = fopen(strInputFile_a, "rb");
    if (ptrInput) 
    {
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
            
            // Calculate number of slots
            fseek(ptrInput, 0, SEEK_END);
            lngInputSize = ftell(ptrInput);
            fseek(ptrInput, HEADER_SIZE, SEEK_SET);
            
            lngDataSize = lngInputSize - HEADER_SIZE - byPrefixLen - bySuffixLen;
            lngSlots = lngDataSize / 2;
            
            if (lngSlots >= 0) 
            {
                // Skip prefix
                for (intI = 0; intI < byPrefixLen; intI++) 
                {
                    if (fgetc(ptrInput) == EOF) 
                    {
                        break;
                    }
                }
                
                if (intI == byPrefixLen) 
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput) 
                    {
                        lngEffSize = ptrRom_a->lngROMSize - intOffset;
                        if (lngEffSize > 65536) 
                        {
                            lngEffSize = 65536;
                        }
                        
                        // Decode
                        for (intI = 0; intI < lngSlots; intI++) 
                        {
                            if (fread(arrBuf, 2, 1, ptrInput) != 1) 
                            {
                                break;
                            }
                            
                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            if (intAddr < lngEffSize) 
                            {
                                if (fputc(ptrRom_a->ptrROMData[intOffset + intAddr], ptrOutput) == EOF) 
                                {
                                    break;
                                }
                            }
                        }
                        
                        if (intI == lngSlots) 
                        {
                            blnSuccess = true;
                        }
                    }
                }
            }
        }
    }
    
    if (ptrInput) 
    {
        fclose(ptrInput);
    }
    if (ptrOutput) 
    {
        fclose(ptrOutput);
    }
    return blnSuccess;
}

int main(int argc_a, char* strArgv_a[])
{
    int intResult = 1;
    RomData* ptrRom = NULL;
    bool blnDecodeOk = false;
    
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Protocol Decoder v20260301\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (argc_a == 4) 
    {
        ptrRom = loadRom(strArgv_a[1]);
        if (ptrRom) 
        {
            blnDecodeOk = decodeFile(ptrRom, strArgv_a[2], strArgv_a[3]);
            freeRom(ptrRom);
            
            if (blnDecodeOk) 
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