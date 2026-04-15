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
} RomData;

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

            if (ptrRom->lngROMSize > ROM_LOAD_MAX) 
            {
                ptrRom->lngROMSize = ROM_LOAD_MAX;
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

static bool compareBinary(const char* strFile1_a, const char* strFile2_a)
{
    bool blnMatch = false;
    FILE* ptrFile1 = NULL;
    FILE* ptrFile2 = NULL;
    int intByte1 = 0;
    int intByte2 = 0;
    bool blnDone = false;

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

static bool compareUnsignal(const RomData* ptrRom_a, const char* strSigFile_a, const char* strPlainFile_a)
{
    bool blnMatch = false;
    FILE* ptrSig = NULL;
    FILE* ptrPlain = NULL;
    uint8_t arrBuf[2];
    uint16_t arrAddrs[4] = {0};
    uint8_t byOffsetLow = 0, byOffsetHigh = 0, byPrefixLen = 0, bySuffixLen = 0;
    uint16_t intOffset = 0;
    long lngInputSize = 0, lngDataSize = 0, lngSlots = 0, lngEffSize = 0;
    int intI = 0;
    int intPlainByte = 0;
    bool blnDone = false;

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
            fseek(ptrSig, 0, SEEK_END);
            lngInputSize = ftell(ptrSig);
            fseek(ptrSig, UNSIGNAL_HEADER_SIZE, SEEK_SET);

            lngDataSize = lngInputSize - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen;
            lngSlots = lngDataSize / 2;

            if (lngSlots >= 0)
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
                        lngEffSize = ptrRom_a->lngROMSize - intOffset;
                        if (lngEffSize > 65536)
                        {
                            lngEffSize = 65536;
                        }

                        blnMatch = true;
                        for (intI = 0; intI < lngSlots && !blnDone; intI++)
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
                            else if (intAddr >= lngEffSize)
                            {
                                blnMatch = false;
                                blnDone = true;
                            }
                            else if (ptrRom_a->ptrROMData[intOffset + intAddr] != (uint8_t)intPlainByte)
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

static bool compareZoscii(const RomData* ptrRom_a, const char* strZosFile_a, const char* strPlainFile_a)
{
    bool blnMatch = false;
    FILE* ptrZos = NULL;
    FILE* ptrPlain = NULL;
    uint8_t arrBuf[2];
    long lngInputSize = 0;
    long lngSlots = 0;
    long lngI = 0;
    int intPlainByte = 0;
    bool blnDone = false;

    ptrZos = fopen(strZosFile_a, "rb");
    if (ptrZos)
    {
        fseek(ptrZos, 0, SEEK_END);
        lngInputSize = ftell(ptrZos);
        fseek(ptrZos, 0, SEEK_SET);

        lngSlots = lngInputSize / 2;

        ptrPlain = fopen(strPlainFile_a, "rb");
        if (ptrPlain)
        {
            blnMatch = true;
            for (lngI = 0; lngI < lngSlots && !blnDone; lngI++)
            {
                if (fread(arrBuf, 2, 1, ptrZos) != 1)
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
                else if (intAddr >= ptrRom_a->lngROMSize)
                {
                    blnMatch = false;
                    blnDone = true;
                }
                else if (ptrRom_a->ptrROMData[intAddr] != (uint8_t)intPlainByte)
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
        fclose(ptrZos);
    }
    else
    {
        fprintf(stderr, "Error: Cannot open file: %s\n", strZosFile_a);
    }

    return blnMatch;
}

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomData* ptrRom = NULL;
    bool blnMatch = false;
    bool blnZoscii = false;

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

        ptrRom = loadRom(strArgv_a[1]);
        if (ptrRom)
        {
            if (blnZoscii)
            {
                printf("ZOSCII compare: %s + %s vs %s\n", strArgv_a[1], strArgv_a[2], strArgv_a[3]);
                blnMatch = compareZoscii(ptrRom, strArgv_a[2], strArgv_a[3]);
            }
            else
            {
                printf("UNSIGNAL compare: %s + %s vs %s\n", strArgv_a[1], strArgv_a[2], strArgv_a[3]);
                blnMatch = compareUnsignal(ptrRom, strArgv_a[2], strArgv_a[3]);
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

            freeRom(ptrRom);
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