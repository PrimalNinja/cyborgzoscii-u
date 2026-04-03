// Cyborg NTS Client - ntsread v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Reads and decodes a specific message from local cache.
//
// Usage:
//   ntsread <queue> <messageid>
//
// Build:
//   cl /O2 /MT ntsread.c /link /SUBSYSTEM:CONSOLE

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

#pragma comment(lib, "advapi32.lib")

// --- Entry Point ---

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    char strFilePath[BUFFER1K] = {0};
    uint8_t* ptrEncoded = NULL;
    long lngEncodedSize = 0;
    uint8_t* ptrDecoded = NULL;
    long lngDecodedSize = 0;

    printf("NTS Read v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (intArgC_a < 3)
    {
        fprintf(stderr, "Usage: %s <queue> <messageid>\n", strArgv_a[0]);
        return intResult;
    }

    if (!initRomContext(&objCtx))
    {
        return intResult;
    }

    if (!findQueueByName(&objCtx, strArgv_a[1], strGUID, sizeof(strGUID),
        strServer, sizeof(strServer), NULL, 0))
    {
        fprintf(stderr, "Error: Queue '%s' not found\n", strArgv_a[1]);
        freeRomContext(&objCtx);
        return intResult;
    }

    // Build path to cached message file
    // Auto-append .bin if not provided
    {
        char strMessageId[MAX_POINTER] = {0};
        size_t lngLen = strlen(strArgv_a[2]);

        strncpy(strMessageId, strArgv_a[2], sizeof(strMessageId) - 5);
        if (lngLen < 4 || _stricmp(strArgv_a[2] + lngLen - 4, ".bin") != 0)
        {
            strcat(strMessageId, ".bin");
        }

        snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s\\%s",
            objCtx.strBaseDir, strGUID, strMessageId);
    }

    if (!readFileToBuffer(strFilePath, &ptrEncoded, &lngEncodedSize))
    {
        fprintf(stderr, "Error: Message not found in local cache: %s\n", strArgv_a[2]);
        freeRomContext(&objCtx);
        return intResult;
    }

    printf("Queue: %s\n", strArgv_a[1]);
    printf("Message: %s\n", strArgv_a[2]);
    printf("Size:    %ld bytes (encoded)\n\n", lngEncodedSize);

    // Decode
    if (decodeChained(&objCtx, ptrEncoded, lngEncodedSize, &ptrDecoded, &lngDecodedSize))
    {
        printf("Decoded: %ld bytes\n\n", lngDecodedSize);

        if (isValidText(ptrDecoded, lngDecodedSize))
        {
            // Print as text
            printf("--- Content ---\n");
            fwrite(ptrDecoded, 1, lngDecodedSize, stdout);
            printf("\n--- End ---\n");
        }
        else
        {
            // Binary content - show hex summary
            printf("Binary content (%ld bytes). First 64 bytes:\n", lngDecodedSize);
            {
                long lngI;
                long lngMax = lngDecodedSize < CONTENT_BOUNDARY ? lngDecodedSize : CONTENT_BOUNDARY;
                for (lngI = 0; lngI < lngMax; lngI++)
                {
                    printf("%02X ", ptrDecoded[lngI]);
                    if ((lngI + 1) % 16 == 0) printf("\n");
                }
                if (lngMax % 16 != 0) printf("\n");
            }
        }

        free(ptrDecoded);
        intResult = 0;
    }
    else
    {
        fprintf(stderr, "Error: Decode failed (wrong ROMs?)\n");
    }

    free(ptrEncoded);
    freeRomContext(&objCtx);
    return intResult;
}