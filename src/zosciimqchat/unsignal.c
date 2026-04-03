// Cyborg NTS Unsignal Utils
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// --- Structures ---

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

typedef struct
{
    RomData* ptrRom1;
    RomData* ptrRom2;
    RomData* ptrRom3;
    int intRomCount;
    char strBaseDir[BUFFER1K];
} RomContext;

// --- UNSIGNAL Core Functions ---

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

    for (intI = 0; intI < 256; intI++)
    {
        ptrRom_a->arrLookup[intI].ptrAddresses = NULL;
        ptrRom_a->arrLookup[intI].intCount = 0;
    }

    lngHeaderSize = (ptrRom_a->lngROMSize > 65536L) ? 65536L : ptrRom_a->lngROMSize;

    for (lngI = 0; lngI < lngHeaderSize; lngI++)
    {
        arrCounts[ptrRom_a->ptrROMData[lngI]]++;
    }

    for (intI = 0; intI < 256; intI++)
    {
        if (arrCounts[intI] > 0)
        {
            ptrRom_a->arrLookup[intI].ptrAddresses =
                (uint32_t*)malloc(arrCounts[intI] * sizeof(uint32_t));
            ptrRom_a->arrLookup[intI].intCount = 0;
        }
    }

    for (lngI = 0; lngI < lngHeaderSize; lngI++)
    {
        uint8_t by = ptrRom_a->ptrROMData[lngI];
        ptrRom_a->arrLookup[by].ptrAddresses[ptrRom_a->arrLookup[by].intCount++] = (uint32_t)lngI;
    }

    {
        uint32_t intRomHash = 0;
        for (lngI = 0; lngI < ptrRom_a->lngROMSize; lngI++)
        {
            intRomHash = (intRomHash * 33) + ptrRom_a->ptrROMData[lngI];
        }
        intRomHash ^= (uint32_t)time(NULL);
#ifdef _WIN32
        {
            LARGE_INTEGER liCounter;
            QueryPerformanceCounter(&liCounter);
            intRomHash ^= (uint32_t)(liCounter.LowPart);
        }
#endif
        srand(intRomHash);
    }
}

static RomData* loadRom(const char* strFilename_a)
{
    RomData* ptrRom = NULL;
    FILE* ptrROM = NULL;

    ptrRom = (RomData*)malloc(sizeof(RomData));
    if (ptrRom)
    {
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

// --- UNSIGNAL Encode to Memory Buffer ---

static bool encodeBufferSingle(const RomData* ptrRom_a,
                               const uint8_t* ptrInput_a, long lngInputSize_a,
                               uint8_t** pptrOutput_a, long* plngOutputSize_a)
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
    bool blnLookupValid = true;
    uint8_t* ptrOutput = NULL;
    long lngOutputSize = 0;
    long lngOutputPos = 0;

    byOffsetLow = (uint8_t)(rand() % 256);
    byOffsetHigh = (uint8_t)(rand() % 256);
    byPrefixLen = (uint8_t)((rand() % 246) + 10);
    bySuffixLen = (uint8_t)((rand() % 246) + 10);

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

            lngEffectiveSize = ptrRom_a->lngROMSize - intROMOffset;
            if (lngEffectiveSize > 65536L)
            {
                lngEffectiveSize = 65536L;
            }

            for (lngI = 0; lngI < lngEffectiveSize; lngI++)
            {
                arrOffsetCounts[ptrRom_a->ptrROMData[intROMOffset + lngI]]++;
            }

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
                // Calculate output size: header(8) + prefix + data(input*2) + suffix
                lngOutputSize = UNSIGNAL_HEADER_SIZE + byPrefixLen + (lngInputSize_a * 2) + bySuffixLen;
                ptrOutput = (uint8_t*)malloc(lngOutputSize);

                if (ptrOutput)
                {
                    // Write header
                    memcpy(ptrOutput + 0, &intH1, 2);
                    memcpy(ptrOutput + 2, &intH2, 2);
                    memcpy(ptrOutput + 4, &intH3, 2);
                    memcpy(ptrOutput + 6, &intH4, 2);
                    lngOutputPos = UNSIGNAL_HEADER_SIZE;

                    // Write prefix
                    memcpy(ptrOutput + lngOutputPos, ptrPrefix, byPrefixLen);
                    lngOutputPos += byPrefixLen;

                    // Encode data
                    for (lngI = 0; lngI < lngInputSize_a; lngI++)
                    {
                        uint8_t by = ptrInput_a[lngI];
                        if (arrOffsetLookup[by].intCount > 0)
                        {
                            uint32_t intRandomIdx = rand() % arrOffsetLookup[by].intCount;
                            uint16_t intAddress = (uint16_t)arrOffsetLookup[by].ptrAddresses[intRandomIdx];
                            memcpy(ptrOutput + lngOutputPos, &intAddress, 2);
                            lngOutputPos += 2;
                        }
                    }

                    // Write suffix
                    memcpy(ptrOutput + lngOutputPos, ptrSuffix, bySuffixLen);
                    lngOutputPos += bySuffixLen;

                    *pptrOutput_a = ptrOutput;
                    *plngOutputSize_a = lngOutputPos;
                    blnSuccess = true;
                }
            }
        }

        if (ptrPrefix) free(ptrPrefix);
        if (ptrSuffix) free(ptrSuffix);

        for (intI = 0; intI < 256; intI++)
        {
            if (arrOffsetLookup[intI].ptrAddresses)
            {
                free(arrOffsetLookup[intI].ptrAddresses);
            }
        }
    }

    return blnSuccess;
}

// --- UNSIGNAL Decode from Memory Buffer ---

static bool decodeBufferSingle(const RomData* ptrRom_a,
                               const uint8_t* ptrInput_a, long lngInputSize_a,
                               uint8_t** pptrOutput_a, long* plngOutputSize_a)
{
    bool blnSuccess = false;
    uint16_t arrAddrs[4] = {0};
    uint8_t byOffsetLow = 0;
    uint8_t byOffsetHigh = 0;
    uint8_t byPrefixLen = 0;
    uint8_t bySuffixLen = 0;
    uint16_t intOffset = 0;
    long lngDataSize = 0;
    long lngSlots = 0;
    long lngEffSize = 0;
    long lngI = 0;
    long lngPos = 0;
    int intI = 0;
    uint8_t* ptrOutput = NULL;

    if (lngInputSize_a < UNSIGNAL_HEADER_SIZE)
    {
        return false;
    }

    // Read header
    for (intI = 0; intI < 4; intI++)
    {
        lngPos = intI * 2;
        arrAddrs[intI] = (uint16_t)ptrInput_a[lngPos] | ((uint16_t)ptrInput_a[lngPos + 1] << 8);
        if (arrAddrs[intI] >= ptrRom_a->lngROMSize)
        {
            return false;
        }
    }

    byOffsetLow = ptrRom_a->ptrROMData[arrAddrs[0]];
    byOffsetHigh = ptrRom_a->ptrROMData[arrAddrs[1]];
    byPrefixLen = ptrRom_a->ptrROMData[arrAddrs[2]];
    bySuffixLen = ptrRom_a->ptrROMData[arrAddrs[3]];

    intOffset = findOffset(byOffsetLow, byOffsetHigh, ptrRom_a->lngROMSize);

    lngDataSize = lngInputSize_a - UNSIGNAL_HEADER_SIZE - byPrefixLen - bySuffixLen;
    if (lngDataSize < 0)
    {
        return false;
    }

    lngSlots = lngDataSize / 2;

    lngEffSize = ptrRom_a->lngROMSize - intOffset;
    if (lngEffSize > 65536)
    {
        lngEffSize = 65536;
    }

    ptrOutput = (uint8_t*)malloc(lngSlots + 1);
    if (ptrOutput)
    {
        lngPos = UNSIGNAL_HEADER_SIZE + byPrefixLen;

        for (lngI = 0; lngI < lngSlots; lngI++)
        {
            uint16_t intAddr = (uint16_t)ptrInput_a[lngPos] | ((uint16_t)ptrInput_a[lngPos + 1] << 8);
            lngPos += 2;

            if (intAddr < lngEffSize)
            {
                ptrOutput[lngI] = ptrRom_a->ptrROMData[intOffset + intAddr];
            }
            else
            {
                free(ptrOutput);
                return false;
            }
        }

        ptrOutput[lngSlots] = '\0';
        *pptrOutput_a = ptrOutput;
        *plngOutputSize_a = lngSlots;
        blnSuccess = true;
    }

    return blnSuccess;
}

// --- Chained Encode/Decode (1-3 ROMs, in memory) ---

static bool encodeChained(const RomContext* ptrCtx_a,
                          const uint8_t* ptrInput_a, long lngInputSize_a,
                          uint8_t** pptrOutput_a, long* plngOutputSize_a)
{
    uint8_t* ptrBuf1 = NULL;
    uint8_t* ptrBuf2 = NULL;
    long lngBuf1Size = 0;
    long lngBuf2Size = 0;
    bool blnOk = false;

    if (ptrCtx_a->intRomCount == 1)
    {
        blnOk = encodeBufferSingle(ptrCtx_a->ptrRom1, ptrInput_a, lngInputSize_a, pptrOutput_a, plngOutputSize_a);
    }
    else if (ptrCtx_a->intRomCount == 2)
    {
        blnOk = encodeBufferSingle(ptrCtx_a->ptrRom1, ptrInput_a, lngInputSize_a, &ptrBuf1, &lngBuf1Size);
        if (blnOk)
        {
            blnOk = encodeBufferSingle(ptrCtx_a->ptrRom2, ptrBuf1, lngBuf1Size, pptrOutput_a, plngOutputSize_a);
            free(ptrBuf1);
        }
    }
    else if (ptrCtx_a->intRomCount == 3)
    {
        blnOk = encodeBufferSingle(ptrCtx_a->ptrRom1, ptrInput_a, lngInputSize_a, &ptrBuf1, &lngBuf1Size);
        if (blnOk)
        {
            blnOk = encodeBufferSingle(ptrCtx_a->ptrRom2, ptrBuf1, lngBuf1Size, &ptrBuf2, &lngBuf2Size);
            free(ptrBuf1);
        }
        if (blnOk)
        {
            blnOk = encodeBufferSingle(ptrCtx_a->ptrRom3, ptrBuf2, lngBuf2Size, pptrOutput_a, plngOutputSize_a);
            free(ptrBuf2);
        }
    }

    return blnOk;
}

static bool decodeChained(const RomContext* ptrCtx_a,
                          const uint8_t* ptrInput_a, long lngInputSize_a,
                          uint8_t** pptrOutput_a, long* plngOutputSize_a)
{
    uint8_t* ptrBuf1 = NULL;
    uint8_t* ptrBuf2 = NULL;
    long lngBuf1Size = 0;
    long lngBuf2Size = 0;
    bool blnOk = false;

    if (ptrCtx_a->intRomCount == 1)
    {
        blnOk = decodeBufferSingle(ptrCtx_a->ptrRom1, ptrInput_a, lngInputSize_a, pptrOutput_a, plngOutputSize_a);
    }
    else if (ptrCtx_a->intRomCount == 2)
    {
        // Reverse order: decode ROM2 first, then ROM1
        blnOk = decodeBufferSingle(ptrCtx_a->ptrRom2, ptrInput_a, lngInputSize_a, &ptrBuf1, &lngBuf1Size);
        if (blnOk)
        {
            blnOk = decodeBufferSingle(ptrCtx_a->ptrRom1, ptrBuf1, lngBuf1Size, pptrOutput_a, plngOutputSize_a);
            free(ptrBuf1);
        }
    }
    else if (ptrCtx_a->intRomCount == 3)
    {
        // Reverse order: decode ROM3, then ROM2, then ROM1
        blnOk = decodeBufferSingle(ptrCtx_a->ptrRom3, ptrInput_a, lngInputSize_a, &ptrBuf1, &lngBuf1Size);
        if (blnOk)
        {
            blnOk = decodeBufferSingle(ptrCtx_a->ptrRom2, ptrBuf1, lngBuf1Size, &ptrBuf2, &lngBuf2Size);
            free(ptrBuf1);
        }
        if (blnOk)
        {
            blnOk = decodeBufferSingle(ptrCtx_a->ptrRom1, ptrBuf2, lngBuf2Size, pptrOutput_a, plngOutputSize_a);
            free(ptrBuf2);
        }
    }

    return blnOk;
}