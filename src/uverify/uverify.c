// Cyborg UNSIGNAL/ZOSCII Verify v20260601
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
    if (lngROMSize_a >= 131072L) { return intRaw; }
    long lngMax = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
    if (lngMax == 0) { return 0; }
    return (uint16_t)(intRaw % (lngMax + 1));
}

static ROMData* loadROM(const char* strFilename_a)
{
    ROMData* ptrROMData = (ROMData*)malloc(sizeof(ROMData));
    if (ptrROMData)
    {
        memset(ptrROMData, 0, sizeof(ROMData));
        FILE* ptrROMFile = fopen(strFilename_a, "rb");
        if (ptrROMFile)
        {
            fseek(ptrROMFile, 0, SEEK_END); ptrROMData->lngROMSize = ftell(ptrROMFile); fseek(ptrROMFile, 0, SEEK_SET);
            if (ptrROMData->lngROMSize > ROM_LOAD_MAX) { ptrROMData->lngROMSize = ROM_LOAD_MAX; }
            ptrROMData->ptrROMData = (uint8_t*)malloc(ptrROMData->lngROMSize);
            if (ptrROMData->ptrROMData) { fread(ptrROMData->ptrROMData, 1, ptrROMData->lngROMSize, ptrROMFile); }
            else { free(ptrROMData); ptrROMData = NULL; }
            fclose(ptrROMFile);
        }
        else { free(ptrROMData); ptrROMData = NULL; }
    }
    return ptrROMData;
}

static void unloadROM(ROMData* ptrROMData_a)
{
    if (ptrROMData_a) { if (ptrROMData_a->ptrROMData) { free(ptrROMData_a->ptrROMData); } free(ptrROMData_a); }
}

static void secureDelete(const char* strPath_a)
{
    FILE* ptrFile = fopen(strPath_a, "r+b");
    long lngSize = 0; uint8_t byVal = 0; long intI = 0;
    if (ptrFile)
    {
        fseek(ptrFile, 0, SEEK_END); lngSize = ftell(ptrFile); fseek(ptrFile, 0, SEEK_SET);
        byVal = 0xFF; for (intI = 0; intI < lngSize; intI++) { fwrite(&byVal, 1, 1, ptrFile); }
        fseek(ptrFile, 0, SEEK_SET);
        byVal = 0x00; for (intI = 0; intI < lngSize; intI++) { fwrite(&byVal, 1, 1, ptrFile); }
        fclose(ptrFile);
    }
    remove(strPath_a);
}

static bool compareBinary(const char* strFile1_a, const char* strFile2_a)
{
    bool blnDone = false;
    bool blnMatch = false;
    int intByte1 = 0, intByte2 = 0;
    FILE* ptrFile1 = fopen(strFile1_a, "rb");
    if (ptrFile1)
    {
        FILE* ptrFile2 = fopen(strFile2_a, "rb");
        if (ptrFile2)
        {
            blnMatch = true;
            while (!blnDone)
            {
                intByte1 = fgetc(ptrFile1); intByte2 = fgetc(ptrFile2);
                if (intByte1 != intByte2) { blnMatch = false; blnDone = true; }
                else if (intByte1 == EOF) { blnDone = true; }
            }
            fclose(ptrFile2);
        }
        else { fprintf(stderr, "Error: Cannot open file: %s\n", strFile2_a); }
        fclose(ptrFile1);
    }
    else { fprintf(stderr, "Error: Cannot open file: %s\n", strFile1_a); }
    return blnMatch;
}

// Single UNSIGNAL decode pass to temp file
static bool decodeUnsignalSingle(const ROMData* ptrROMData_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint16_t arrAddrs[4] = {0};
    uint8_t arrBuf[2];
    bool blnSuccess = false;
    uint8_t byOffsetHigh = 0, byOffsetLow = 0, byPrefixLen = 0, bySuffixLen = 0;
    long intDataSize = 0, intEffSize = 0;
    int intI = 0;
    long intInputSize = 0;
    uint16_t intOffset = 0;
    long intSlots = 0;
    FILE* ptrInput = fopen(strInputFile_a, "rb");
    FILE* ptrOutput = NULL;

    if (ptrInput)
    {
        for (intI = 0; intI < 4; intI++)
        {
            if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
            arrAddrs[intI] = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
            if (arrAddrs[intI] >= ptrROMData_a->lngROMSize) { break; }
        }
        if (intI == 4)
        {
            byOffsetLow  = ptrROMData_a->ptrROMData[arrAddrs[0]];
            byOffsetHigh = ptrROMData_a->ptrROMData[arrAddrs[1]];
            byPrefixLen  = ptrROMData_a->ptrROMData[arrAddrs[2]];
            bySuffixLen  = ptrROMData_a->ptrROMData[arrAddrs[3]];
            intOffset = findOffset(byOffsetLow, byOffsetHigh, ptrROMData_a->lngROMSize);
            fseek(ptrInput, 0, SEEK_END); intInputSize = ftell(ptrInput); fseek(ptrInput, UNSIGNAL_HEADER_SIZE, SEEK_SET);
            intDataSize = intInputSize - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen;
            intSlots = intDataSize / 2;
            if (intSlots >= 0)
            {
                for (intI = 0; intI < byPrefixLen; intI++) { if (fgetc(ptrInput) == EOF) { break; } }
                if (intI == byPrefixLen)
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput)
                    {
                        intEffSize = ptrROMData_a->lngROMSize - intOffset;
                        if (intEffSize > 65536) { intEffSize = 65536; }
                        for (intI = 0; intI < intSlots; intI++)
                        {
                            if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            if (intAddr < intEffSize) { if (fputc(ptrROMData_a->ptrROMData[intOffset + intAddr], ptrOutput) == EOF) { break; } }
                        }
                        if (intI == intSlots) { blnSuccess = true; }
                        fclose(ptrOutput);
                    }
                }
            }
        }
        fclose(ptrInput);
    }
    return blnSuccess;
}

// Single ZOSCII decode pass to temp file
static bool decodeZOSCIISingle(const ROMData* ptrROMData_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint8_t arrBuf[2];
    bool blnSuccess = false;
    long intI = 0, intInputSize = 0, intSlots = 0;
    FILE* ptrInput = fopen(strInputFile_a, "rb");
    FILE* ptrOutput = NULL;

    if (ptrInput)
    {
        fseek(ptrInput, 0, SEEK_END); intInputSize = ftell(ptrInput); fseek(ptrInput, 0, SEEK_SET);
        intSlots = intInputSize / 2;
        ptrOutput = fopen(strOutputFile_a, "wb");
        if (ptrOutput)
        {
            for (intI = 0; intI < intSlots; intI++)
            {
                if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
                uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                if (intAddr < ptrROMData_a->lngROMSize) { if (fputc(ptrROMData_a->ptrROMData[intAddr], ptrOutput) == EOF) { break; } }
            }
            if (intI == intSlots) { blnSuccess = true; }
            fclose(ptrOutput);
        }
        fclose(ptrInput);
    }
    return blnSuccess;
}

// Tango UNSIGNAL decode — header from ROM[0], payload round-robin
static bool decodeUnsignalTango(ROMData** arrROMs_a, int intROMCount_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint16_t arrAddrs[4] = {0};
    uint8_t arrBuf[2];
    bool blnSuccess = false;
    uint8_t byOffsetHigh = 0, byOffsetLow = 0, byPrefixLen = 0, bySuffixLen = 0;
    long intDataSize = 0;
    int intI = 0;
    long intInputSize = 0, intSlots = 0;
    FILE* ptrInput = fopen(strInputFile_a, "rb");
    FILE* ptrOutput = NULL;
    ROMData* ptrROM0 = arrROMs_a[0];

    if (ptrInput)
    {
        for (intI = 0; intI < 4; intI++)
        {
            if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
            arrAddrs[intI] = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
            if (arrAddrs[intI] >= ptrROM0->lngROMSize) { break; }
        }
        if (intI == 4)
        {
            byOffsetLow  = ptrROM0->ptrROMData[arrAddrs[0]];
            byOffsetHigh = ptrROM0->ptrROMData[arrAddrs[1]];
            byPrefixLen  = ptrROM0->ptrROMData[arrAddrs[2]];
            bySuffixLen  = ptrROM0->ptrROMData[arrAddrs[3]];
            fseek(ptrInput, 0, SEEK_END); intInputSize = ftell(ptrInput); fseek(ptrInput, UNSIGNAL_HEADER_SIZE, SEEK_SET);
            intDataSize = intInputSize - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen;
            intSlots = intDataSize / 2;
            if (intSlots >= 0)
            {
                for (intI = 0; intI < byPrefixLen; intI++) { if (fgetc(ptrInput) == EOF) { break; } }
                if (intI == byPrefixLen)
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput)
                    {
                        for (intI = 0; intI < intSlots; intI++)
                        {
                            if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
                            ROMData* ptrROM = arrROMs_a[intI % intROMCount_a];
                            uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                            if (intAddr < ptrROM->lngROMSize) { if (fputc(ptrROM->ptrROMData[intAddr], ptrOutput) == EOF) { break; } }
                        }
                        if (intI == intSlots) { blnSuccess = true; }
                        fclose(ptrOutput);
                    }
                }
            }
        }
        fclose(ptrInput);
    }
    return blnSuccess;
}

// Tango ZOSCII decode — round-robin
static bool decodeZOSCIITango(ROMData** arrROMs_a, int intROMCount_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint8_t arrBuf[2];
    bool blnSuccess = false;
    long intI = 0, intInputSize = 0, intSlots = 0;
    FILE* ptrInput = fopen(strInputFile_a, "rb");
    FILE* ptrOutput = NULL;

    if (ptrInput)
    {
        fseek(ptrInput, 0, SEEK_END); intInputSize = ftell(ptrInput); fseek(ptrInput, 0, SEEK_SET);
        intSlots = intInputSize / 2;
        ptrOutput = fopen(strOutputFile_a, "wb");
        if (ptrOutput)
        {
            for (intI = 0; intI < intSlots; intI++)
            {
                if (fread(arrBuf, 2, 1, ptrInput) != 1) { break; }
                ROMData* ptrROM = arrROMs_a[intI % intROMCount_a];
                uint16_t intAddr = (uint16_t)arrBuf[0] | ((uint16_t)arrBuf[1] << 8);
                if (intAddr < ptrROM->lngROMSize) { if (fputc(ptrROM->ptrROMData[intAddr], ptrOutput) == EOF) { break; } }
            }
            if (intI == intSlots) { blnSuccess = true; }
            fclose(ptrOutput);
        }
        fclose(ptrInput);
    }
    return blnSuccess;
}

int main(int intArgC_a, char* strArgv_a[])
{
    bool blnMatch = false;
    bool blnTango = false;
    bool blnZoscii = false;
    bool blnDecodeOk = false;
    int intResult = 1;
    int intROMCount = 0;
    int intI = 0;
    ROMData* arrROMs[3] = {NULL, NULL, NULL};
    char strTempPath1[4096] = {0};
    char strTempPath2[4096] = {0};

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL/ZOSCII Verify v20260601\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    // Strip flags from end
    while (intArgC_a > 1)
    {
        if      (strcmp(strArgv_a[intArgC_a - 1], "-t") == 0) { blnTango  = true; intArgC_a--; }
        else if (strcmp(strArgv_a[intArgC_a - 1], "-z") == 0) { blnZoscii = true; intArgC_a--; }
        else { break; }
    }

    if (intArgC_a == 3)
    {
        // Binary compare
        printf("Binary compare: %s vs %s\n", strArgv_a[1], strArgv_a[2]);
        blnMatch = compareBinary(strArgv_a[1], strArgv_a[2]);
        printf(blnMatch ? "MATCH\n" : "MISMATCH\n");
        if (blnMatch) { intResult = 0; }
    }
    else if (intArgC_a >= 4 && intArgC_a <= 6)
    {
        const char* strSigFile   = strArgv_a[intArgC_a - 2];
        const char* strPlainFile = strArgv_a[intArgC_a - 1];
        intROMCount = intArgC_a - 3;

        if (intROMCount < 1 || intROMCount > 3) { fprintf(stderr, "Usage: %s <rom1> [rom2] [rom3] <sigfile> <plainfile> [-t] [-z]\n", strArgv_a[0]); return 1; }
        if (blnTango && intROMCount < 2) { fprintf(stderr, "Error: Tango mode requires at least 2 ROMs\n"); return 1; }

        for (intI = 0; intI < intROMCount; intI++)
        {
            arrROMs[intI] = loadROM(strArgv_a[1 + intI]);
            if (!arrROMs[intI])
            {
                fprintf(stderr, "Failed to load ROM: %s\n", strArgv_a[1 + intI]);
                for (intI = 0; intI < intROMCount; intI++) { if (arrROMs[intI]) { unloadROM(arrROMs[intI]); } }
                return 1;
            }
        }

        printf("%s compare%s: %s vs %s\n", blnZoscii ? "ZOSCII" : "UNSIGNAL", blnTango ? " (Tango)" : "", strSigFile, strPlainFile);

        // Decode to temp then binary compare
        snprintf(strTempPath1, sizeof(strTempPath1), "%s.verify.tmp", strSigFile);
        snprintf(strTempPath2, sizeof(strTempPath2), "%s.verify.tmp2", strSigFile);

        if (blnTango)
        {
            if (blnZoscii) { blnDecodeOk = decodeZOSCIITango(arrROMs, intROMCount, strSigFile, strTempPath1); }
            else           { blnDecodeOk = decodeUnsignalTango(arrROMs, intROMCount, strSigFile, strTempPath1); }
            if (blnDecodeOk) { blnMatch = compareBinary(strTempPath1, strPlainFile); }
            secureDelete(strTempPath1);
        }
        else if (intROMCount == 1)
        {
            if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[0], strSigFile, strTempPath1); }
            else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[0], strSigFile, strTempPath1); }
            if (blnDecodeOk) { blnMatch = compareBinary(strTempPath1, strPlainFile); }
            secureDelete(strTempPath1);
        }
        else if (intROMCount == 2)
        {
            // Reverse order decode
            if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[1], strSigFile, strTempPath1); }
            else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[1], strSigFile, strTempPath1); }
            if (blnDecodeOk)
            {
                if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[0], strTempPath1, strTempPath2); }
                else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[0], strTempPath1, strTempPath2); }
            }
            if (blnDecodeOk) { blnMatch = compareBinary(strTempPath2, strPlainFile); }
            secureDelete(strTempPath1);
            secureDelete(strTempPath2);
        }
        else if (intROMCount == 3)
        {
            if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[2], strSigFile, strTempPath1); }
            else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[2], strSigFile, strTempPath1); }
            if (blnDecodeOk)
            {
                if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[1], strTempPath1, strTempPath2); }
                else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[1], strTempPath1, strTempPath2); }
            }
            if (blnDecodeOk)
            {
                char strTempPath3[4096] = {0};
                snprintf(strTempPath3, sizeof(strTempPath3), "%s.verify.tmp3", strSigFile);
                if (blnZoscii) { blnDecodeOk = decodeZOSCIISingle(arrROMs[0], strTempPath2, strTempPath3); }
                else           { blnDecodeOk = decodeUnsignalSingle(arrROMs[0], strTempPath2, strTempPath3); }
                if (blnDecodeOk) { blnMatch = compareBinary(strTempPath3, strPlainFile); }
                secureDelete(strTempPath3);
            }
            secureDelete(strTempPath1);
            secureDelete(strTempPath2);
        }

        for (intI = 0; intI < intROMCount; intI++) { unloadROM(arrROMs[intI]); }

        printf(blnMatch ? "MATCH\n" : "MISMATCH\n");
        if (blnMatch) { intResult = 0; }
    }
    else
    {
        fprintf(stderr, "Usage:\n");
        fprintf(stderr, "  %s <file1> <file2>                                    Binary compare\n", strArgv_a[0]);
        fprintf(stderr, "  %s <rom1> [rom2] [rom3] <sigfile> <plainfile> [-t] [-z]\n", strArgv_a[0]);
    }

    return intResult;
}
