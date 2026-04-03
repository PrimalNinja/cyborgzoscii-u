// Cyborg NTS Client - ntsqueue v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Queue and server management for NTS client tools.
//
// Usage:
//   ntsqueue                                       - List all visible queues
//   ntsqueue add <name> <server_url> [guid]        - Add a queue
//   ntsqueue remove <name>                         - Remove a queue
//   ntsqueue addserver <name> <server_url>         - Add server to existing queue
//   ntsqueue removeserver <name> <server_url>      - Remove server from queue
//
// Build:
//   cl /O2 /MT ntsqueue.c /link /SUBSYSTEM:CONSOLE

// Issues:
// 	JSON buffer (2048 bytes) silently truncates long server URLs
// 	strncpy doesn't null-terminate when source fills destination
// 	_stricmp on decoded JSON without validation could overrun
// 	readFileToBuffer allocates arbitrary file size (DoS risk)
// 	decodeBufferSingle allocates arbitrary memory from file header
// 	removeserver deletes queue even though multi-server not actually supported
// 	encodeBufferSingle doesn't verify input bytes exist before encoding (silently skips missing)

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <time.h>
#include <fcntl.h>
#include <io.h>
#include <windows.h>

#include "defines.h"
#include "unsignal.c"
#include "utils.c"

// Try to decode a .json.sig file and extract queue info
// Returns true if decodable with current ROMs
static bool decodeQueueFile(const RomContext* ptrCtx_a, const char* strFilePath_a,
                              char* strName_a, int intNameSize_a,
                              char* strGUID_a, int intGUIDSize_a,
                              char* strServer_a, int intServerSize_a)
{
    uint8_t* ptrEncoded = NULL;
    long lngEncodedSize = 0;
    uint8_t* ptrDecoded = NULL;
    long lngDecodedSize = 0;
    bool blnResult = false;
    char* ptrNameStart = NULL;
    char* ptrNameEnd = NULL;
    char* ptrGUIDStart = NULL;
    char* ptrGUIDEnd = NULL;
    char* ptrServerStart = NULL;
    char* ptrServerEnd = NULL;

    if (!readFileToBuffer(strFilePath_a, &ptrEncoded, &lngEncodedSize))
    {
        return false;
    }

    if (decodeChained(ptrCtx_a, ptrEncoded, lngEncodedSize, &ptrDecoded, &lngDecodedSize))
    {
        if (isValidText(ptrDecoded, lngDecodedSize))
        {
            // Simple JSON parsing - find "name":"...", "guid":"...", "server":"..."
            // The decoded buffer is null-terminated by decodeBufferSingle

            ptrNameStart = strstr((char*)ptrDecoded, "\"name\":\"");
            ptrGUIDStart = strstr((char*)ptrDecoded, "\"guid\":\"");
            ptrServerStart = strstr((char*)ptrDecoded, "\"server\":\"");

            if (ptrNameStart && ptrGUIDStart && ptrServerStart)
            {
                ptrNameStart += 8; // skip "name":"
                ptrNameEnd = strchr(ptrNameStart, '"');

                ptrGUIDStart += 8; // skip "guid":"
                ptrGUIDEnd = strchr(ptrGUIDStart, '"');

                ptrServerStart += 10; // skip "server":"
                ptrServerEnd = strchr(ptrServerStart, '"');

                if (ptrNameEnd && ptrGUIDEnd && ptrServerEnd)
                {
                    int intNameLen = (int)(ptrNameEnd - ptrNameStart);
                    int intGUIDLen = (int)(ptrGUIDEnd - ptrGUIDStart);
                    int intServerLen = (int)(ptrServerEnd - ptrServerStart);

                    if (intNameLen < intNameSize_a && intGUIDLen < intGUIDSize_a && intServerLen < intServerSize_a)
                    {
                        strncpy(strName_a, ptrNameStart, intNameLen);
                        strName_a[intNameLen] = '\0';

                        strncpy(strGUID_a, ptrGUIDStart, intGUIDLen);
                        strGUID_a[intGUIDLen] = '\0';

                        strncpy(strServer_a, ptrServerStart, intServerLen);
                        strServer_a[intServerLen] = '\0';

                        blnResult = true;
                    }
                }
            }
        }

        free(ptrDecoded);
    }

    free(ptrEncoded);
    return blnResult;
}

static bool writeQueueFile(const RomContext* ptrCtx_a,
                             const char* strName_a, const char* strGUID_a, const char* strServer_a)
{
    char strJSON[MAX_JSON] = {0};
    char strFilePath[BUFFER1K] = {0};
    char strGUID[MAX_GUID] = {0};
    uint8_t* ptrEncoded = NULL;
    long lngEncodedSize = 0;
    bool blnResult = false;

    // Sanitize GUID for use in filename
    {
        int intI = 0, intJ = 0;
        while (strGUID_a[intI] && intJ < MAX_GUID - 1)
        {
            char c = strGUID_a[intI++];
            if (c != '\\' && c != '/' && c != ':' && c != '*' &&
                c != '?' && c != '"' && c != '<' && c != '>' && c != '|')
            {
                strGUID[intJ++] = c;
            }
        }
        strGUID[intJ] = '\0';
    }

    snprintf(strJSON, sizeof(strJSON),
        "{\"name\":\"%s\",\"guid\":\"%s\",\"server\":\"%s\"}",
        strName_a, strGUID, strServer_a);

    snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s.json.sig",
        ptrCtx_a->strBaseDir, strGUID);

    if (encodeChained(ptrCtx_a, (uint8_t*)strJSON, (long)strlen(strJSON), &ptrEncoded, &lngEncodedSize))
    {
        blnResult = writeBufferToFile(strFilePath, ptrEncoded, lngEncodedSize);
        free(ptrEncoded);
    }

    return blnResult;
}

// --- Command Handlers ---

static void handleList(const RomContext* ptrCtx_a)
{
    char strSearchPath[BUFFER1K] = {0};
    WIN32_FIND_DATAA objFindData;
    HANDLE hFind = INVALID_HANDLE_VALUE;
    char strFilePath[BUFFER1K] = {0};
    char strName[MAX_QUEUENAME] = {0};
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    int intCount = 0;

    snprintf(strSearchPath, sizeof(strSearchPath), "%squeues\\*.json.sig", ptrCtx_a->strBaseDir);

    hFind = FindFirstFileA(strSearchPath, &objFindData);
    if (hFind == INVALID_HANDLE_VALUE)
    {
        printf("No queues found.\n");
        return;
    }

    printf("%-20s %-38s %s\n", "Queue", "GUID", "Server");
    printf("%-20s %-38s %s\n", "-----", "----", "------");

    do
    {
        snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s", ptrCtx_a->strBaseDir, objFindData.cFileName);

        memset(strName, 0, sizeof(strName));
        memset(strGUID, 0, sizeof(strGUID));
        memset(strServer, 0, sizeof(strServer));

        if (decodeQueueFile(ptrCtx_a, strFilePath, strName, sizeof(strName),
                              strGUID, sizeof(strGUID), strServer, sizeof(strServer)))
        {
            printf("%-20s %-38s %s\n", strName, strGUID, strServer);
            intCount++;
        }
        // Files that can't be decoded are silently skipped
    }
    while (FindNextFileA(hFind, &objFindData));

    FindClose(hFind);

    if (intCount == 0)
    {
        printf("No queues visible with current ROMs.\n");
    }
    else
    {
        printf("\n%d queue(s) visible.\n", intCount);
    }
}

static void handleAdd(const RomContext* ptrCtx_a, const char* strName_a,
                      const char* strServer_a, const char* strGUID_a)
{
    char strGUID[MAX_GUID] = {0};
    char strQueueDir[BUFFER1K] = {0};

    if (strGUID_a && strlen(strGUID_a) > 0)
    {
        strncpy(strGUID, strGUID_a, sizeof(strGUID) - 1);
    }
    else
    {
        getGUID(strGUID, sizeof(strGUID), (RomContext*)ptrCtx_a);
    }

    if (writeQueueFile(ptrCtx_a, strName_a, strGUID, strServer_a))
    {
        // Create the queue data folder
        snprintf(strQueueDir, sizeof(strQueueDir), "%squeues\\%s", ptrCtx_a->strBaseDir, strGUID);
        ensureDirectory(strQueueDir);

        printf("Queue added: %s\n", strName_a);
        printf("GUID: %s\n", strGUID);
        printf("Server: %s\n", strServer_a);
    }
    else
    {
        fprintf(stderr, "Error: Failed to create queue file\n");
    }
}

static void handleRemove(const RomContext* ptrCtx_a, const char* strName_a)
{
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    char strFilePath[BUFFER1K] = {0};

    if (findQueueByName(ptrCtx_a, strName_a, strGUID, sizeof(strGUID),
                          strServer, sizeof(strServer), strFilePath, sizeof(strFilePath)))
    {
        if (DeleteFileA(strFilePath))
        {
            printf("Queue removed: %s (GUID: %s)\n", strName_a, strGUID);
        }
        else
        {
            fprintf(stderr, "Error: Failed to delete queue file\n");
        }
    }
    else
    {
        fprintf(stderr, "Error: Queue '%s' not found with current ROMs\n", strName_a);
    }
}

static void printUsage(const char* strExeName_a)
{
    printf("Usage:\n");
    printf("  %s                                       - List all visible queues\n", strExeName_a);
    printf("  %s add <name> <server_url> [guid]        - Add a queue\n", strExeName_a);
    printf("  %s remove <name>                         - Remove a queue\n", strExeName_a);
}

// --- Entry Point ---

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;
    char strQueuesDir[BUFFER1K] = {0};
    char strStatesDir[BUFFER1K] = {0};

    printf("NTS Queue Manager v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (!initRomContext(&objCtx))
    {
        return intResult;
    }

    printf("Active ROMs: %d\n\n", objCtx.intRomCount);

    // Ensure directories exist
    snprintf(strQueuesDir, sizeof(strQueuesDir), "%squeues", objCtx.strBaseDir);
    ensureDirectory(strQueuesDir);
    snprintf(strStatesDir, sizeof(strStatesDir), "%sstates", objCtx.strBaseDir);
    ensureDirectory(strStatesDir);

    if (intArgC_a == 1)
    {
        // No args: list all queues
        handleList(&objCtx);
        intResult = 0;
    }
    else if (intArgC_a >= 2)
    {
		if (_stricmp(strArgv_a[1], "-h") == 0 || _stricmp(strArgv_a[1], "--help") == 0)
		{
			printUsage(strArgv_a[0]);
			intResult = 0;
		}
        else if (_stricmp(strArgv_a[1], "add") == 0)
        {
            if (intArgC_a >= 4)
            {
                // add <name> <server> [guid]
                const char* strGUID = (intArgC_a >= 5) ? strArgv_a[4] : NULL;
                handleAdd(&objCtx, strArgv_a[2], strArgv_a[3], strGUID);
                intResult = 0;
            }
            else
            {
                fprintf(stderr, "Error: 'add' requires <name> and <server_url>\n");
                printUsage(strArgv_a[0]);
            }
        }
        else if (_stricmp(strArgv_a[1], "remove") == 0)
        {
            if (intArgC_a >= 3)
            {
                handleRemove(&objCtx, strArgv_a[2]);
                intResult = 0;
            }
            else
            {
                fprintf(stderr, "Error: 'remove' requires <name>\n");
                printUsage(strArgv_a[0]);
            }
        }
        else
        {
            fprintf(stderr, "Error: Unknown command '%s'\n", strArgv_a[1]);
            printUsage(strArgv_a[0]);
        }
    }

    freeRomContext(&objCtx);
    return intResult;
}