// Cyborg UNSIGNAL/ZOSCII Verify v20260416
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

// Windows & Linux Version

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
#define ROM_LOAD_MAX 131072L
#define UNSIGNAL_HEADER_SIZE 8

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
            
            if (ptrROMData->lngROMSize > ROM_LOAD_MAX)
            {
                ptrROMData->lngROMSize = ROM_LOAD_MAX;
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

static bool compareBinary(const char* strFile1_a, const char* strFile2_a)
{
    bool blnDone = false;
    bool blnMatch = false;
    int intByte1 = 0;
    int intByte2 = 0;
    FILE* ptrFile1 = NULL;
    FILE* ptrFile2 = NULL;

    ptrFile1 = fopen(strFile1_a, "rb");
    if (ptrFile1)
    {
        ptrFile2 = fopen(strFile2_a, "rb");
        if (ptrFile2)
        {
            blnMatch = true;
            while (!blnDone)
            {
                intByte1 = fgetc(ptrFile1);
                intByte2 = fgetc(ptrFile2);

                if (intByte1 != intByte2)
                {
                    blnMatch = false;
                    blnDone = true;
                }
                else if (intByte1 == EOF)
                {
                    blnDone = true;
                }
            }
            fclose(ptrFile2);
        }
        else
        {
            fprintf(stderr, "Error: Cannot open file: %s\n", strFile2_a);
        }
        fclose(ptrFile1);
    }
    else
    {
        fprintf(stderr, "Error: Cannot open file: %s\n", strFile1_a);
    }

    return blnMatch;
}

static bool compareUnsignal(const ROMData* ptrROMData_a, const char* strSigFile_a, const char* strPlainFile_a)
{
    uint16_t arrAddrs[4] = {0};
    uint8_t arrBuf[2];
    bool blnDone = false;
    bool blnMatch = false;
	uint8_t byOffsetHigh = 0;
    uint8_t byOffsetLow = 0;
	uint8_t byPrefixLen = 0;
	uint8_t bySuffixLen = 0;
	long intDataSize = 0;
	long intEffectiveSize = 0;
    int intI = 0;
    long intInputSize = 0;
    uint16_t intOffset = 0;
    int intPlainByte = 0;
	long intSlots = 0;
    FILE* ptrPlain = NULL;
    FILE* ptrSig = NULL;

    ptrSig = fopen(strSigFile_a, "rb");
    if (ptrSig)
    {
        // Read header
        for (intI = 0; intI < 4; intI++)
        {
            if (fread(arrBuf, 2, 1, ptrSig) != 1)
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
            fseek(ptrSig, 0, SEEK_END);
            intInputSize = ftell(ptrSig);
            fseek(ptrSig, UNSIGNAL_HEADER_SIZE, SEEK_SET);

            intDataSize = intInputSize - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen;
            intSlots = intDataSize / 2;

            if (intSlots >= 0)
            {
                // Skip prefix
                for (intI = 0; intI < byPrefixLen; intI++)
                {
                    if (fgetc(ptrSig) == EOF)
                    {
                        break;
                    }
                }

                if (intI == byPrefixLen)
                {
                    ptrPlain = fopen(strPlainFile_a, "rb");
                    if (ptrPlain)
                    {
                        intEffectiveSize = ptrROMData_a->lngROMSize - intOffset;
                        if (intEffectiveSize > 65536)
                        {
                            intEffectiveSize = 65536;
                        }

                        blnMatch = true;
                        for (intI = 0; intI < intSlots && !blnDone; intI++)
                        {
                            if (fread(arrBuf, 2, 1, ptrSig) != 1)
                            {
                                blnMatch = false;
                                blnDone = true;
                                break;
                            }

                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            intPlainByte = fgetc(ptrPlain);

                            if (intPlainByte == EOF)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                            else if (intAddr >= intEffectiveSize)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                            else if (ptrROMData_a->ptrROMData[intOffset + intAddr] != (uint8_t)intPlainByte)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                        }

                        // Check plain file has no more bytes
                        if (blnMatch)
                        {
                            if (fgetc(ptrPlain) != EOF)
                            {
                                blnMatch = false;
                            }
                        }

                        fclose(ptrPlain);
                    }
                    else
                    {
                        fprintf(stderr, "Error: Cannot open file: %s\n", strPlainFile_a);
                    }
                }
            }
        }
        fclose(ptrSig);
    }
    else
    {
        fprintf(stderr, "Error: Cannot open file: %s\n", strSigFile_a);
    }

    return blnMatch;
}

static bool compareZOSCII(const ROMData* ptrROMData_a, const char* strZosFile_a, const char* strPlainFile_a)
{
    uint8_t arrBuf[2];
    bool blnDone = false;
    bool blnMatch = false;
    long intI = 0;
    long intInputSize = 0;
    int intPlainByte = 0;
    long intSlots = 0;
    FILE* ptrPlain = NULL;
    FILE* ptrZOSCII = NULL;

    ptrZOSCII = fopen(strZosFile_a, "rb");
    if (ptrZOSCII)
    {
        fseek(ptrZOSCII, 0, SEEK_END);
        intInputSize = ftell(ptrZOSCII);
        fseek(ptrZOSCII, 0, SEEK_SET);

        intSlots = intInputSize / 2;

        ptrPlain = fopen(strPlainFile_a, "rb");
        if (ptrPlain)
        {
            blnMatch = true;
            for (intI = 0; intI < intSlots && !blnDone; intI++)
            {
                if (fread(arrBuf, 2, 1, ptrZOSCII) != 1)
                {
                    blnMatch = false;
                    blnDone = true;
                    break;
                }

                uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                intPlainByte = fgetc(ptrPlain);

                if (intPlainByte == EOF)
                {
                    blnMatch = false;
                    blnDone = true;
                }
                else if (intAddr >= ptrROMData_a->lngROMSize)
                {
                    blnMatch = false;
                    blnDone = true;
                }
                else if (ptrROMData_a->ptrROMData[intAddr] != (uint8_t)intPlainByte)
                {
                    blnMatch = false;
                    blnDone = true;
                }
            }

            // Check plain file has no more bytes
            if (blnMatch)
            {
                if (fgetc(ptrPlain) != EOF)
                {
                    blnMatch = false;
                }
            }

            fclose(ptrPlain);
        }
        else
        {
            fprintf(stderr, "Error: Cannot open file: %s\n", strPlainFile_a);
        }
        fclose(ptrZOSCII);
    }
    else
    {
        fprintf(stderr, "Error: Cannot open file: %s\n", strZosFile_a);
    }

    return blnMatch;
}

int main(int intArgC_a, char* strArgv_a[])
{
    bool blnMatch = false;
    bool blnZoscii = false;
    int intResult = 1;
    ROMData* ptrROMData = NULL;

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL/ZOSCII Verify v20260416\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a == 3)
    {
        // Binary compare
        printf("Binary compare: %s vs %s\n", strArgv_a[1], strArgv_a[2]);
        blnMatch = compareBinary(strArgv_a[1], strArgv_a[2]);
        if (blnMatch)
        {
            printf("MATCH\n");
            intResult = 0;
        }
        else
        {
            printf("MISMATCH\n");
        }
    }
    else if (intArgC_a == 4 || intArgC_a == 5)
    {
        // Check for -z flag
        if (intArgC_a == 5)
        {
            if (strcmp(strArgv_a[4], "-z") == 0)
            {
                blnZoscii = true;
            }
            else
            {
                fprintf(stderr, "Error: Unknown flag: %s\n", strArgv_a[4]);
                return intResult;
            }
        }

        ptrROMData = loadROM(strArgv_a[1]);
        if (ptrROMData)
        {
            if (blnZoscii)
            {
                printf("ZOSCII compare: %s + %s vs %s\n", strArgv_a[1], strArgv_a[2], strArgv_a[3]);
                blnMatch = compareZOSCII(ptrROMData, strArgv_a[2], strArgv_a[3]);
            }
            else
            {
                printf("UNSIGNAL compare: %s + %s vs %s\n", strArgv_a[1], strArgv_a[2], strArgv_a[3]);
                blnMatch = compareUnsignal(ptrROMData, strArgv_a[2], strArgv_a[3]);
            }

            if (blnMatch)
            {
                printf("MATCH\n");
                intResult = 0;
            }
            else
            {
                printf("MISMATCH\n");
            }

            unloadROM(ptrROMData);
        }
        else
        {
            fprintf(stderr, "Error: Failed to load ROM: %s\n", strArgv_a[1]);
        }
    }
    else
    {
        fprintf(stderr, "Usage:\n");
        fprintf(stderr, "  %s <file1> <file2>                    Binary compare\n", strArgv_a[0]);
        fprintf(stderr, "  %s <rom> <sigfile> <plainfile>        UNSIGNAL compare\n", strArgv_a[0]);
        fprintf(stderr, "  %s <rom> <zosfile> <plainfile> -z     ZOSCII compare\n", strArgv_a[0]);
    }

    return intResult;
}