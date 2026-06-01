// Cyborg UNSIGNAL Protocol v20260601
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
} ROMData;

#define UNSIGNAL_OFFSET_LIMIT_PCT 2
#define UNSIGNAL_ROM_LOAD_MAX 131072L
#define UNSIGNAL_HEADER_SIZE 8

static void buildLookupTable(ROMData* ptrROMData_a)
{
    uint32_t arrCounts[256] = {0};
    long intHeaderSize = 0;
    int intI = 0;
    long intJ = 0;

    for (intI = 0; intI < 256; intI++) { ptrROMData_a->arrLookup[intI].ptrAddresses = NULL; ptrROMData_a->arrLookup[intI].intCount = 0; }
    intHeaderSize = (ptrROMData_a->lngROMSize > 65536L) ? 65536L : ptrROMData_a->lngROMSize;
    for (intJ = 0; intJ < intHeaderSize; intJ++) { arrCounts[ptrROMData_a->ptrROMData[intJ]]++; }
    for (intI = 0; intI < 256; intI++)
    {
        if (arrCounts[intI] > 0) { ptrROMData_a->arrLookup[intI].ptrAddresses = (uint32_t*)malloc(arrCounts[intI] * sizeof(uint32_t)); ptrROMData_a->arrLookup[intI].intCount = 0; }
    }
    for (intJ = 0; intJ < intHeaderSize; intJ++) { uint8_t by = ptrROMData_a->ptrROMData[intJ]; ptrROMData_a->arrLookup[by].ptrAddresses[ptrROMData_a->arrLookup[by].intCount++] = (uint32_t)intJ; }
    uint32_t intROMHash = 0;
    for (intJ = 0; intJ < ptrROMData_a->lngROMSize; intJ++) { intROMHash = (intROMHash * 33) + ptrROMData_a->ptrROMData[intJ]; }
    intROMHash ^= (uint32_t)time(NULL);
    srand(intROMHash);
}

static uint16_t findOffset(uint8_t byLow_a, uint8_t byHigh_a, long lngROMSize_a)
{
    uint16_t intRaw = (uint16_t)byLow_a | ((uint16_t)byHigh_a << 8);
    if (lngROMSize_a >= 131072L) { return intRaw; }
    long lngMax = (lngROMSize_a * UNSIGNAL_OFFSET_LIMIT_PCT) / 100;
    if (lngMax == 0) { return 0; }
    return (uint16_t)(intRaw % (lngMax + 1));
}

static uint32_t findROMAddress(const ByteAddresses* ptrLookup_a, uint8_t byTarget_a)
{
    if (ptrLookup_a[byTarget_a].intCount > 0) { return ptrLookup_a[byTarget_a].ptrAddresses[rand() % ptrLookup_a[byTarget_a].intCount]; }
    return 0;
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
            if (ptrROMData->lngROMSize > UNSIGNAL_ROM_LOAD_MAX) { ptrROMData->lngROMSize = UNSIGNAL_ROM_LOAD_MAX; }
            ptrROMData->ptrROMData = (uint8_t*)malloc(ptrROMData->lngROMSize);
            if (ptrROMData->ptrROMData) { fread(ptrROMData->ptrROMData, 1, ptrROMData->lngROMSize, ptrROMFile); buildLookupTable(ptrROMData); }
            else { free(ptrROMData); ptrROMData = NULL; }
            fclose(ptrROMFile);
        }
        else { free(ptrROMData); ptrROMData = NULL; }
    }
    return ptrROMData;
}

static void unloadROM(ROMData* ptrROMData_a)
{
    int intI = 0;
    if (ptrROMData_a)
    {
        if (ptrROMData_a->ptrROMData) { free(ptrROMData_a->ptrROMData); }
        for (intI = 0; intI < 256; intI++) { if (ptrROMData_a->arrLookup[intI].ptrAddresses) { free(ptrROMData_a->arrLookup[intI].ptrAddresses); } }
        free(ptrROMData_a);
    }
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

// Single-ROM UNSIGNAL encode pass
static bool encodeSingle(const ROMData* ptrROMData_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    uint32_t arrOffsetCounts[256] = {0};
    ByteAddresses arrOffsetLookup[256] = {0};
    bool blnLookupValid = true;
    bool blnSuccess = false;
    uint8_t byOffsetHigh = 0, byOffsetLow = 0, byPrefixLen = 0, bySuffixLen = 0;
    int intCh = 0, intI = 0;
    long intEffectiveSize = 0, intJ = 0;
    uint16_t intH1 = 0, intH2 = 0, intH3 = 0, intH4 = 0, intROMOffset = 0;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    uint8_t* ptrPrefix = NULL;
    uint8_t* ptrSuffix = NULL;

    byOffsetLow  = (uint8_t)(rand() % 256);
    byOffsetHigh = (uint8_t)(rand() % 256);
    byPrefixLen  = (uint8_t)((rand() % 246) + 10);
    bySuffixLen  = (uint8_t)((rand() % 246) + 10);

    if (ptrROMData_a->arrLookup[byOffsetLow].intCount > 0 &&
        ptrROMData_a->arrLookup[byOffsetHigh].intCount > 0 &&
        ptrROMData_a->arrLookup[byPrefixLen].intCount > 0 &&
        ptrROMData_a->arrLookup[bySuffixLen].intCount > 0)
    {
        intROMOffset = findOffset(byOffsetLow, byOffsetHigh, ptrROMData_a->lngROMSize);
        intH1 = (uint16_t)findROMAddress(ptrROMData_a->arrLookup, byOffsetLow);
        intH2 = (uint16_t)findROMAddress(ptrROMData_a->arrLookup, byOffsetHigh);
        intH3 = (uint16_t)findROMAddress(ptrROMData_a->arrLookup, byPrefixLen);
        intH4 = (uint16_t)findROMAddress(ptrROMData_a->arrLookup, bySuffixLen);

        ptrPrefix = (uint8_t*)malloc(byPrefixLen);
        ptrSuffix = (uint8_t*)malloc(bySuffixLen);

        if (ptrPrefix && ptrSuffix)
        {
            for (intI = 0; intI < byPrefixLen; intI++) { ptrPrefix[intI] = (uint8_t)(rand() % 256); }
            for (intI = 0; intI < bySuffixLen; intI++) { ptrSuffix[intI] = (uint8_t)(rand() % 256); }

            intEffectiveSize = ptrROMData_a->lngROMSize - intROMOffset;
            if (intEffectiveSize > 65536L) { intEffectiveSize = 65536L; }

            for (intJ = 0; intJ < intEffectiveSize; intJ++) { arrOffsetCounts[ptrROMData_a->ptrROMData[intROMOffset + intJ]]++; }
            for (intI = 0; intI < 256; intI++) { arrOffsetLookup[intI].ptrAddresses = NULL; arrOffsetLookup[intI].intCount = 0; }
            for (intI = 0; intI < 256 && blnLookupValid; intI++)
            {
                if (arrOffsetCounts[intI] > 0)
                {
                    arrOffsetLookup[intI].ptrAddresses = (uint32_t*)malloc(arrOffsetCounts[intI] * sizeof(uint32_t));
                    if (arrOffsetLookup[intI].ptrAddresses) { arrOffsetLookup[intI].intCount = 0; }
                    else { blnLookupValid = false; }
                }
            }
            for (intJ = 0; intJ < intEffectiveSize && blnLookupValid; intJ++)
            {
                uint8_t by = ptrROMData_a->ptrROMData[intROMOffset + intJ];
                arrOffsetLookup[by].ptrAddresses[arrOffsetLookup[by].intCount++] = (uint32_t)intJ;
            }

            if (blnLookupValid)
            {
                ptrInput = fopen(strInputFile_a, "rb");
                if (ptrInput)
                {
                    ptrOutput = fopen(strOutputFile_a, "wb");
                    if (ptrOutput)
                    {
                        fwrite(&intH1, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH2, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH3, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(&intH4, sizeof(uint16_t), 1, ptrOutput);
                        fwrite(ptrPrefix, 1, byPrefixLen, ptrOutput);

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

                        fwrite(ptrSuffix, 1, bySuffixLen, ptrOutput);
                        blnSuccess = true;
                        fclose(ptrOutput);
                    }
                    fclose(ptrInput);
                }
            }
        }

        if (ptrPrefix) { free(ptrPrefix); }
        if (ptrSuffix) { free(ptrSuffix); }
        for (intI = 0; intI < 256; intI++) { if (arrOffsetLookup[intI].ptrAddresses) { free(arrOffsetLookup[intI].ptrAddresses); } }
    }
    else { fprintf(stderr, "Error: ROM does not contain required byte values for UNSIGNAL header\n"); }

    return blnSuccess;
}

// Tango UNSIGNAL encode — header from ROM[0], payload round-robin
static bool encodeTango(ROMData** arrROMs_a, int intROMCount_a, const char* strInputFile_a, const char* strOutputFile_a)
{
    bool blnSuccess = false;
    uint8_t byOffsetHigh = 0, byOffsetLow = 0, byPrefixLen = 0, bySuffixLen = 0;
    int intCh = 0, intI = 0;
    long intByteIndex = 0;
    uint16_t intH1 = 0, intH2 = 0, intH3 = 0, intH4 = 0;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    uint8_t* ptrPrefix = NULL;
    uint8_t* ptrSuffix = NULL;
    ROMData* ptrROM0 = arrROMs_a[0];

    byOffsetLow  = (uint8_t)(rand() % 256);
    byOffsetHigh = (uint8_t)(rand() % 256);
    byPrefixLen  = (uint8_t)((rand() % 246) + 10);
    bySuffixLen  = (uint8_t)((rand() % 246) + 10);

    if (ptrROM0->arrLookup[byOffsetLow].intCount > 0 &&
        ptrROM0->arrLookup[byOffsetHigh].intCount > 0 &&
        ptrROM0->arrLookup[byPrefixLen].intCount > 0 &&
        ptrROM0->arrLookup[bySuffixLen].intCount > 0)
    {
        intH1 = (uint16_t)findROMAddress(ptrROM0->arrLookup, byOffsetLow);
        intH2 = (uint16_t)findROMAddress(ptrROM0->arrLookup, byOffsetHigh);
        intH3 = (uint16_t)findROMAddress(ptrROM0->arrLookup, byPrefixLen);
        intH4 = (uint16_t)findROMAddress(ptrROM0->arrLookup, bySuffixLen);

        ptrPrefix = (uint8_t*)malloc(byPrefixLen);
        ptrSuffix = (uint8_t*)malloc(bySuffixLen);

        if (ptrPrefix && ptrSuffix)
        {
            for (intI = 0; intI < byPrefixLen; intI++) { ptrPrefix[intI] = (uint8_t)(rand() % 256); }
            for (intI = 0; intI < bySuffixLen; intI++) { ptrSuffix[intI] = (uint8_t)(rand() % 256); }

            ptrInput = fopen(strInputFile_a, "rb");
            if (ptrInput)
            {
                ptrOutput = fopen(strOutputFile_a, "wb");
                if (ptrOutput)
                {
                    fwrite(&intH1, sizeof(uint16_t), 1, ptrOutput);
                    fwrite(&intH2, sizeof(uint16_t), 1, ptrOutput);
                    fwrite(&intH3, sizeof(uint16_t), 1, ptrOutput);
                    fwrite(&intH4, sizeof(uint16_t), 1, ptrOutput);
                    fwrite(ptrPrefix, 1, byPrefixLen, ptrOutput);

                    intByteIndex = 0;
                    intCh = fgetc(ptrInput);
                    while (intCh != EOF)
                    {
                        uint8_t by = (uint8_t)intCh;
                        ROMData* ptrROM = arrROMs_a[intByteIndex % intROMCount_a];
                        if (ptrROM->arrLookup[by].intCount > 0)
                        {
                            uint32_t intRandomIdx = rand() % ptrROM->arrLookup[by].intCount;
                            uint16_t intAddress = (uint16_t)ptrROM->arrLookup[by].ptrAddresses[intRandomIdx];
                            fwrite(&intAddress, sizeof(uint16_t), 1, ptrOutput);
                        }
                        intByteIndex++;
                        intCh = fgetc(ptrInput);
                    }

                    fwrite(ptrSuffix, 1, bySuffixLen, ptrOutput);
                    blnSuccess = true;
                    fclose(ptrOutput);
                }
                fclose(ptrInput);
            }
        }

        if (ptrPrefix) { free(ptrPrefix); }
        if (ptrSuffix) { free(ptrSuffix); }
    }
    else { fprintf(stderr, "Error: ROM[0] does not contain required byte values for UNSIGNAL header\n"); }

    return blnSuccess;
}

int main(int intArgC_a, char* strArgv_a[])
{
    bool blnEncodeOk = false;
    bool blnTango = false;
    int intResult = 1;
    int intROMCount = 0;
    int intI = 0;
    ROMData* arrROMs[3] = {NULL, NULL, NULL};
    const char* strInputFile = NULL;
    const char* strOutputFile = NULL;
    char strTempPath1[4096] = {0};
    char strTempPath2[4096] = {0};

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Protocol Encoder v20260601\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a >= 4 && intArgC_a <= 7)
    {
        if (strcmp(strArgv_a[intArgC_a - 1], "-t") == 0) { blnTango = true; intArgC_a--; }

        strOutputFile = strArgv_a[intArgC_a - 1];
        strInputFile  = strArgv_a[intArgC_a - 2];
        intROMCount   = intArgC_a - 3;

        if (intROMCount < 1 || intROMCount > 3) { fprintf(stderr, "Usage: %s <rom1> [rom2] [rom3] <input> <output> [-t]\n", strArgv_a[0]); return 1; }
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

        if (blnTango)
        {
            blnEncodeOk = encodeTango(arrROMs, intROMCount, strInputFile, strOutputFile);
        }
        else if (intROMCount == 1)
        {
            blnEncodeOk = encodeSingle(arrROMs[0], strInputFile, strOutputFile);
        }
        else if (intROMCount == 2)
        {
            snprintf(strTempPath1, sizeof(strTempPath1), "%s.tmp", strOutputFile);
            blnEncodeOk = encodeSingle(arrROMs[0], strInputFile, strTempPath1);
            if (blnEncodeOk) { blnEncodeOk = encodeSingle(arrROMs[1], strTempPath1, strOutputFile); }
            secureDelete(strTempPath1);
        }
        else if (intROMCount == 3)
        {
            snprintf(strTempPath1, sizeof(strTempPath1), "%s.tmp",  strOutputFile);
            snprintf(strTempPath2, sizeof(strTempPath2), "%s.tmp2", strOutputFile);
            blnEncodeOk = encodeSingle(arrROMs[0], strInputFile, strTempPath1);
            if (blnEncodeOk) { blnEncodeOk = encodeSingle(arrROMs[1], strTempPath1, strTempPath2); }
            if (blnEncodeOk) { blnEncodeOk = encodeSingle(arrROMs[2], strTempPath2, strOutputFile); }
            secureDelete(strTempPath1);
            secureDelete(strTempPath2);
        }

        for (intI = 0; intI < intROMCount; intI++) { unloadROM(arrROMs[intI]); }

        if (blnEncodeOk) { intResult = 0; }
        else { fprintf(stderr, "Encode failed\n"); }
    }
    else
    {
        fprintf(stderr, "Usage: %s <rom1> [rom2] [rom3] <input> <output> [-t]\n", strArgv_a[0]);
    }

    return intResult;
}
