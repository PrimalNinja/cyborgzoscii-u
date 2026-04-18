// Cyborg UNSIGNAL Protocol v20260301
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
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
} ROMData;

static uint16_t findOffset(uint8_t byLow_a, uint8_t byHigh_a, long lngROMSize_a)
{
    uint16_t intRaw = (uint16_t)byLow_a | ((uint16_t)byHigh_a << 8);
    uint16_t intResult = 0;
    
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

static ROMData* loadROM(const char* strFilename_a)
{
    ROMData* ptrROMData = NULL;
    FILE* ptrROMFile = NULL;
    
    ptrROMData = (ROMData*)malloc(sizeof(ROMData));
    if (ptrROMData)
    {
        // Initialize
        memset(ptrROMData, 0, sizeof(ROMData));
        
        ptrROMFile = fopen(strFilename_a, "rb");
        if (ptrROMFile)
        {
            fseek(ptrROMFile, 0, SEEK_END);
            ptrROMData->lngROMSize = ftell(ptrROMFile);
            fseek(ptrROMFile, 0, SEEK_SET);
            
            if (ptrROMData->lngROMSize > UNSIGNAL_ROM_LOAD_MAX)
            {
                ptrROMData->lngROMSize = UNSIGNAL_ROM_LOAD_MAX;
            }
            
            ptrROMData->ptrROMData = (uint8_t*)malloc(ptrROMData->lngROMSize);
            if (ptrROMData->ptrROMData)
            {
                fread(ptrROMData->ptrROMData, 1, ptrROMData->lngROMSize, ptrROMFile);
            }
            else
            {
                free(ptrROMData);
                ptrROMData = NULL;
            }
            
            fclose(ptrROMFile);
        }
        else
        {
            free(ptrROMData);
            ptrROMData = NULL;
        }
    }
    
    return ptrROMData;
}


static void unloadROM(ROMData* ptrROMData_a)
{
    if (ptrROMData_a)
    {
        if (ptrROMData_a->ptrROMData)
        {
            free(ptrROMData_a->ptrROMData);
        }
        
        free(ptrROMData_a);
    }
}

static bool decodeFile(const ROMData* ptrROMData_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint16_t arrAddrs[4] = {0};
    uint8_t arrBuf[2];
    bool blnSuccess = false;
	uint8_t byOffsetHigh = 0;
    uint8_t byOffsetLow = 0;
	uint8_t byPrefixLen = 0;
	uint8_t bySuffixLen = 0;
	long intDataSize = 0;
	long intEffectiveSize = 0;
    int intI = 0;
    long intInputSize = 0;
    uint16_t intOffset = 0;
	long intSlots = 0;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    
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
            if (arrAddrs[intI] >= ptrROMData_a->lngROMSize) 
            {
                break;
            }
        }
        
        if (intI == 4) 
        {
            byOffsetLow = ptrROMData_a->ptrROMData[arrAddrs[0]];
            byOffsetHigh = ptrROMData_a->ptrROMData[arrAddrs[1]];
            byPrefixLen = ptrROMData_a->ptrROMData[arrAddrs[2]];
            bySuffixLen = ptrROMData_a->ptrROMData[arrAddrs[3]];
            
            intOffset = findOffset(byOffsetLow, byOffsetHigh, ptrROMData_a->lngROMSize);
            
            // Calculate number of slots
            fseek(ptrInput, 0, SEEK_END);
            intInputSize = ftell(ptrInput);
            fseek(ptrInput, HEADER_SIZE, SEEK_SET);
            
            intDataSize = intInputSize - HEADER_SIZE - byPrefixLen - bySuffixLen;
            intSlots = intDataSize / 2;
            
            if (intSlots >= 0) 
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
                        intEffectiveSize = ptrROMData_a->lngROMSize - intOffset;
                        if (intEffectiveSize > 65536) 
                        {
                            intEffectiveSize = 65536;
                        }
                        
                        // Decode
                        for (intI = 0; intI < intSlots; intI++) 
                        {
                            if (fread(arrBuf, 2, 1, ptrInput) != 1) 
                            {
                                break;
                            }
                            
                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            if (intAddr < intEffectiveSize) 
                            {
                                if (fputc(ptrROMData_a->ptrROMData[intOffset + intAddr], ptrOutput) == EOF) 
                                {
                                    break;
                                }
                            }
                        }
                        
                        if (intI == intSlots) 
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
    bool blnDecodeOk = false;
    int intResult = 1;
    ROMData* ptrROMData = NULL;
    
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Protocol Decoder v20260301\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (argc_a == 4) 
    {
        ptrROMData = loadROM(strArgv_a[1]);
        if (ptrROMData) 
        {
            blnDecodeOk = decodeFile(ptrROMData, strArgv_a[2], strArgv_a[3]);
            unloadROM(ptrROMData);
            
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