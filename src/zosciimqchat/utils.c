// Cyborg NTS Utils
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// --- Utility Functions ---

static bool fileExists(const char* strPath_a)
{
    FILE* ptrFile = fopen(strPath_a, "rb");
    if (ptrFile)
    {
        fclose(ptrFile);
        return true;
    }
    return false;
}

static int getROMCount(const char* strBasePath_a)
{
    int intCount = 0;
    char strRomPath[BUFFER1K] = {0};

    snprintf(strRomPath, sizeof(strRomPath), "%sactive.rom", strBasePath_a);
    if (fileExists(strRomPath))
    {
        intCount = 1;

        snprintf(strRomPath, sizeof(strRomPath), "%sactive2.rom", strBasePath_a);
        if (fileExists(strRomPath))
        {
            intCount = 2;

            snprintf(strRomPath, sizeof(strRomPath), "%sactive3.rom", strBasePath_a);
            if (fileExists(strRomPath))
            {
                intCount = 3;
            }
        }
    }

    return intCount;
}

static void getGUID(char* strGUID_a, int intSize_a, RomContext* ptrCtx_a)
{
    uint8_t arrData[16];
    int intI = 0;
    uint32_t intRomHash = 0;
    long lngI = 0;
    RomData* ptrRom = ptrCtx_a->ptrRom1;
    
    // Hash ROM content (same as buildLookupTable)
    for (lngI = 0; lngI < ptrRom->lngROMSize; lngI++)
    {
        intRomHash = (intRomHash * 33) + ptrRom->ptrROMData[lngI];
    }
    intRomHash ^= (uint32_t)time(NULL);
    
    // Seed rand with ROM hash
    srand(intRomHash);
    
    // Generate random bytes
    for (intI = 0; intI < 16; intI++)
    {
        arrData[intI] = (uint8_t)(rand() % 256);
    }
    
    // Set version (4) and variant (RFC 4122)
    arrData[6] = (arrData[6] & 0x0F) | 0x40;
    arrData[8] = (arrData[8] & 0x3F) | 0x80;
    
    snprintf(strGUID_a, intSize_a,
        "%02X%02X%02X%02X-%02X%02X-%02X%02X-%02X%02X-%02X%02X%02X%02X%02X%02X",
        arrData[0], arrData[1], arrData[2], arrData[3],
        arrData[4], arrData[5],
        arrData[6], arrData[7],
        arrData[8], arrData[9],
        arrData[10], arrData[11], arrData[12], arrData[13], arrData[14], arrData[15]);
}

static bool initRomContext(RomContext* ptrCtx_a)
{
    char strRomPath[BUFFER1K] = {0};
    char strExePath[BUFFER1K] = {0};
    char* ptrLastSlash = NULL;

    memset(ptrCtx_a, 0, sizeof(RomContext));

    // Get exe directory
    GetModuleFileNameA(NULL, strExePath, sizeof(strExePath) - 1);
    ptrLastSlash = strrchr(strExePath, '\\');
    if (!ptrLastSlash)
    {
        ptrLastSlash = strrchr(strExePath, '/');
    }
    if (ptrLastSlash)
    {
        *(ptrLastSlash + 1) = '\0';
        strncpy(ptrCtx_a->strBaseDir, strExePath, sizeof(ptrCtx_a->strBaseDir) - 1);
    }

    ptrCtx_a->intRomCount = getROMCount(ptrCtx_a->strBaseDir);
    if (ptrCtx_a->intRomCount == 0)
    {
        fprintf(stderr, "Error: No active ROMs found\n");
        return false;
    }

    snprintf(strRomPath, sizeof(strRomPath), "%sactive.rom", ptrCtx_a->strBaseDir);
    ptrCtx_a->ptrRom1 = loadRom(strRomPath);
    if (!ptrCtx_a->ptrRom1)
    {
        fprintf(stderr, "Error: Failed to load active.rom\n");
        return false;
    }

    if (ptrCtx_a->intRomCount >= 2)
    {
        snprintf(strRomPath, sizeof(strRomPath), "%sactive2.rom", ptrCtx_a->strBaseDir);
        ptrCtx_a->ptrRom2 = loadRom(strRomPath);
        if (!ptrCtx_a->ptrRom2)
        {
            fprintf(stderr, "Error: Failed to load active2.rom\n");
            unloadRom(ptrCtx_a->ptrRom1);
            return false;
        }
    }

    if (ptrCtx_a->intRomCount >= 3)
    {
        snprintf(strRomPath, sizeof(strRomPath), "%sactive3.rom", ptrCtx_a->strBaseDir);
        ptrCtx_a->ptrRom3 = loadRom(strRomPath);
        if (!ptrCtx_a->ptrRom3)
        {
            fprintf(stderr, "Error: Failed to load active3.rom\n");
            unloadRom(ptrCtx_a->ptrRom1);
            if (ptrCtx_a->ptrRom2) unloadRom(ptrCtx_a->ptrRom2);
            return false;
        }
    }

    return true;
}

static void freeRomContext(RomContext* ptrCtx_a)
{
    if (ptrCtx_a->ptrRom1) unloadRom(ptrCtx_a->ptrRom1);
    if (ptrCtx_a->ptrRom2) unloadRom(ptrCtx_a->ptrRom2);
    if (ptrCtx_a->ptrRom3) unloadRom(ptrCtx_a->ptrRom3);
}

static void ensureDirectory(const char* strPath_a)
{
    if (!CreateDirectoryA(strPath_a, NULL))
    {
        // ERROR_ALREADY_EXISTS is fine
        if (GetLastError() != ERROR_ALREADY_EXISTS)
        {
            fprintf(stderr, "Warning: Could not create directory: %s\n", strPath_a);
        }
    }
}

// --- Queue File I/O ---
// Each queue is stored as queues/<GUID>.json.sig
// The .sig file is UNSIGNAL-encoded JSON containing: name, guid, server

static bool readFileToBuffer(const char* strPath_a, uint8_t** pptrData_a, long* plngSize_a)
{
    FILE* ptrFile = NULL;
    long lngSize = 0;
    uint8_t* ptrData = NULL;

    ptrFile = fopen(strPath_a, "rb");
    if (!ptrFile)
    {
        return false;
    }

    fseek(ptrFile, 0, SEEK_END);
    lngSize = ftell(ptrFile);
    fseek(ptrFile, 0, SEEK_SET);

    ptrData = (uint8_t*)malloc(lngSize);
    if (!ptrData)
    {
        fclose(ptrFile);
        return false;
    }

    fread(ptrData, 1, lngSize, ptrFile);
    fclose(ptrFile);

    *pptrData_a = ptrData;
    *plngSize_a = lngSize;
    return true;
}

static bool writeBufferToFile(const char* strPath_a, const uint8_t* ptrData_a, long lngSize_a)
{
    FILE* ptrFile = NULL;

    ptrFile = fopen(strPath_a, "wb");
    if (!ptrFile)
    {
        return false;
    }

    fwrite(ptrData_a, 1, lngSize_a, ptrFile);
    fclose(ptrFile);
    return true;
}

// Returns true if the decoded content looks like valid text (printable ASCII + common control chars)
static bool isValidText(const uint8_t* ptrData_a, long lngSize_a)
{
    long lngI = 0;

    if (lngSize_a < 2)
    {
        return false;
    }

    for (lngI = 0; lngI < lngSize_a; lngI++)
    {
        uint8_t by = ptrData_a[lngI];
        // Allow printable ASCII (32-126), tab(9), newline(10), carriage return(13)
        if (by >= 32 && by <= 126)
        {
            continue;
        }
        if (by == 9 || by == 10 || by == 13)
        {
            continue;
        }
        return false;
    }

    return true;
}

// --- Queue Lookup ---

static bool findQueueByName(const RomContext* ptrCtx_a, const char* strName_a,
                              char* strFoundGUID_a, int intGUIDSize_a,
                              char* strFoundServer_a, int intServerSize_a,
                              char* strFoundFilePath_a, int intFilePathSize_a)
{
    char strSearchPath[BUFFER1K] = {0};
    WIN32_FIND_DATAA objFindData;
    HANDLE hFind = INVALID_HANDLE_VALUE;
    char strFilePath[BUFFER1K] = {0};
    bool blnFound = false;

    snprintf(strSearchPath, sizeof(strSearchPath), "%squeues\\*.json.sig", ptrCtx_a->strBaseDir);
    hFind = FindFirstFileA(strSearchPath, &objFindData);
    if (hFind == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    do
    {
        uint8_t* ptrEncoded = NULL;
        long lngEncodedSize = 0;
        uint8_t* ptrDecoded = NULL;
        long lngDecodedSize = 0;

        snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s", ptrCtx_a->strBaseDir, objFindData.cFileName);

        if (readFileToBuffer(strFilePath, &ptrEncoded, &lngEncodedSize))
        {
            if (decodeChained(ptrCtx_a, ptrEncoded, lngEncodedSize, &ptrDecoded, &lngDecodedSize))
            {
                if (isValidText(ptrDecoded, lngDecodedSize))
                {
                    char* ptrNameStart = strstr((char*)ptrDecoded, "\"name\":\"");
                    if (ptrNameStart)
                    {
                        ptrNameStart += 8;
                        char* ptrNameEnd = strchr(ptrNameStart, '"');
                        if (ptrNameEnd)
                        {
                            int intLen = (int)(ptrNameEnd - ptrNameStart);
                            char strName[MAX_QUEUENAME] = {0};
                            strncpy(strName, ptrNameStart, intLen < sizeof(strName) - 1 ? intLen : sizeof(strName) - 1);

                            if (_stricmp(strName, strName_a) == 0)
                            {
                                char* ptrGUID = strstr((char*)ptrDecoded, "\"guid\":\"");
                                if (ptrGUID)
                                {
                                    ptrGUID += 8;
                                    char* ptrGUIDEnd = strchr(ptrGUID, '"');
                                    if (ptrGUIDEnd)
                                    {
                                        int intGLen = (int)(ptrGUIDEnd - ptrGUID);
                                        strncpy(strFoundGUID_a, ptrGUID, intGLen < intGUIDSize_a - 1 ? intGLen : intGUIDSize_a - 1);
                                        strFoundGUID_a[intGUIDSize_a - 1] = '\0';
                                    }
                                }

                                char* ptrServer = strstr((char*)ptrDecoded, "\"server\":\"");
                                if (ptrServer)
                                {
                                    ptrServer += 10;
                                    char* ptrServerEnd = strchr(ptrServer, '"');
                                    if (ptrServerEnd)
                                    {
                                        int intSLen = (int)(ptrServerEnd - ptrServer);
                                        strncpy(strFoundServer_a, ptrServer, intSLen < intServerSize_a - 1 ? intSLen : intServerSize_a - 1);
                                        strFoundServer_a[intServerSize_a - 1] = '\0';
                                    }
                                }

                                if (strFoundFilePath_a)
                                {
                                    strncpy(strFoundFilePath_a, strFilePath, intFilePathSize_a - 1);
                                    strFoundFilePath_a[intFilePathSize_a - 1] = '\0';
                                }

                                blnFound = true;
                            }
                        }
                    }
                }
                free(ptrDecoded);
            }
            free(ptrEncoded);
        }
    }
    while (!blnFound && FindNextFileA(hFind, &objFindData));

    FindClose(hFind);
    return blnFound;
}

// --- State File Management ---

static void loadState(const RomContext* ptrCtx_a, const char* strGUID_a, char* strLastPointer_a, int intSize_a)
{
    char strStatePath[BUFFER1K] = {0};
    FILE* ptrFile = NULL;

    strLastPointer_a[0] = '\0';
    snprintf(strStatePath, sizeof(strStatePath), "%sstates\\%s.txt", ptrCtx_a->strBaseDir, strGUID_a);

    ptrFile = fopen(strStatePath, "r");
    if (ptrFile)
    {
        fgets(strLastPointer_a, intSize_a, ptrFile);
        // Strip newline
        {
            char* ptrNewline = strchr(strLastPointer_a, '\n');
            if (ptrNewline) *ptrNewline = '\0';
            ptrNewline = strchr(strLastPointer_a, '\r');
            if (ptrNewline) *ptrNewline = '\0';
        }
        fclose(ptrFile);
    }
}

static void saveState(const RomContext* ptrCtx_a, const char* strGUID_a, const char* strPointer_a)
{
    char strStatePath[BUFFER1K] = {0};
    FILE* ptrFile = NULL;

    snprintf(strStatePath, sizeof(strStatePath), "%sstates\\%s.txt", ptrCtx_a->strBaseDir, strGUID_a);
    ptrFile = fopen(strStatePath, "w");
    if (ptrFile)
    {
        fprintf(ptrFile, "%s", strPointer_a);
        fclose(ptrFile);
    }
}

