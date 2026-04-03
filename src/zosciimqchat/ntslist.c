// Cyborg NTS Client - ntslist v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Lists messages in local cached queue (offline only).
//
// Usage:
//   ntslist <queue> [from] [to]
//
// Build:
//   cl /O2 /MT ntslist.c /link /SUBSYSTEM:CONSOLE

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

// --- List Command ---

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    char strQueueDir[BUFFER1K] = {0};
    char strSearchPath[BUFFER1K] = {0};
    WIN32_FIND_DATAA objFindData;
    HANDLE hFind;
    const char* strFrom = NULL;
    const char* strTo = NULL;
    int intCount = 0;
    int intShown = 0;
    int intDefaultLimit = 5;

    printf("NTS List v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (intArgC_a < 2)
    {
        fprintf(stderr, "Usage: %s <queue> [from] [to]\n", strArgv_a[0]);
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

    printf("Queue: %s\n", strArgv_a[1]);
    printf("GUID:    %s\n", strGUID);
    printf("Server:  %s\n\n", strServer);

    if (intArgC_a >= 3) { strFrom = strArgv_a[2]; }
    if (intArgC_a >= 4) { strTo = strArgv_a[3]; }

    // Scan local queue directory
    snprintf(strQueueDir, sizeof(strQueueDir), "%squeues\\%s", objCtx.strBaseDir, strGUID);
    snprintf(strSearchPath, sizeof(strSearchPath), "%s\\*.bin", strQueueDir);

    hFind = FindFirstFileA(strSearchPath, &objFindData);
    if (hFind == INVALID_HANDLE_VALUE)
    {
        printf("No cached messages.\n");
        freeRomContext(&objCtx);
        return 0;
    }

    // Collect filenames for sorting
    {
        static char arrFiles[MAX_FILES][MAX_PATH];
        int intTotal = 0;
        int intI = 0;
        int intJ = 0;

        do
        {
            if (intTotal < MAX_FILES)
            {
                strncpy(arrFiles[intTotal], objFindData.cFileName, MAX_PATHM1);
                intTotal++;
            }
        }
        while (FindNextFileA(hFind, &objFindData));
        FindClose(hFind);

        // Simple bubble sort (filenames are chronological)
        for (intI = 0; intI < intTotal - 1; intI++)
        {
            for (intJ = 0; intJ < intTotal - intI - 1; intJ++)
            {
                if (strcmp(arrFiles[intJ], arrFiles[intJ + 1]) > 0)
                {
                    char strTemp[MAX_PATH];
                    strncpy(strTemp, arrFiles[intJ], MAX_PATHM1);
                    strncpy(arrFiles[intJ], arrFiles[intJ + 1], MAX_PATHM1);
                    strncpy(arrFiles[intJ + 1], strTemp, MAX_PATHM1);
                }
            }
        }

        printf("Messages: %d cached\n\n", intTotal);

        // Build filtered index array (in reverse chronological order)
        {
            int intPageSize = 10;
            int intFilteredCount = 0;
            int intPageStart = 0;
            int intPageEnd = 0;
            int intK = 0;
            int intPreviewMax = 80;

            // Count matching files first (for range queries)
            if (strFrom == NULL && strTo == NULL)
            {
                intFilteredCount = intTotal;
            }
            else
            {
                for (intI = 0; intI < intTotal; intI++)
                {
                    bool blnMatch = true;
                    if (strFrom && strcmp(arrFiles[intI], strFrom) < 0) { blnMatch = false; }
                    if (strTo && strcmp(arrFiles[intI], strTo) > 0) { blnMatch = false; }
                    if (blnMatch) { intFilteredCount++; }
                }
            }

            if (intFilteredCount == 0)
            {
                printf("No messages in range.\n");
            }
            else
            {
                // Display pages in reverse order (newest first)
                // Walk the sorted array backwards, skipping non-matching entries
                intK = intTotal - 1;  // Start from newest
                intShown = 0;

                while (intK >= 0 && intShown < intFilteredCount)
                {
                    // Check if this file is in range
                    bool blnMatch = true;
                    if (strFrom && strcmp(arrFiles[intK], strFrom) < 0) { blnMatch = false; }
                    if (strTo && strcmp(arrFiles[intK], strTo) > 0) { blnMatch = false; }

                    if (blnMatch)
                    {
                        char strFullPath[BUFFER1K];
                        snprintf(strFullPath, sizeof(strFullPath), "%s\\%s", strQueueDir, arrFiles[intK]);

                        WIN32_FIND_DATAA fd2;
                        HANDLE h2 = FindFirstFileA(strFullPath, &fd2);
                        DWORD dwSize = 0;
                        if (h2 != INVALID_HANDLE_VALUE)
                        {
                            dwSize = fd2.nFileSizeLow;
                            FindClose(h2);
                        }

                        printf("  %s  %lu bytes\n", arrFiles[intK], (unsigned long)dwSize);

                        // Decode and show preview
                        {
                            uint8_t* ptrEncoded = NULL;
                            long lngEncodedSize = 0;
                            uint8_t* ptrDecoded = NULL;
                            long lngDecodedSize = 0;

                            if (readFileToBuffer(strFullPath, &ptrEncoded, &lngEncodedSize))
                            {
                                if (decodeChained(&objCtx, ptrEncoded, lngEncodedSize, &ptrDecoded, &lngDecodedSize))
                                {
                                    if (isValidText(ptrDecoded, lngDecodedSize))
                                    {
                                        // Show text preview, truncated to intPreviewMax chars
                                        int intShowLen = (lngDecodedSize < intPreviewMax) ? (int)lngDecodedSize : intPreviewMax;
                                        printf("    \"");
                                        // Print character by character, replacing control chars
                                        {
                                            int intC;
                                            for (intC = 0; intC < intShowLen; intC++)
                                            {
                                                if (ptrDecoded[intC] == '\r' || ptrDecoded[intC] == '\n' || ptrDecoded[intC] == '\t')
                                                {
                                                    printf(" ");
                                                }
                                                else
                                                {
                                                    printf("%c", ptrDecoded[intC]);
                                                }
                                            }
                                        }
                                        if (lngDecodedSize > intPreviewMax)
                                        {
                                            printf("...");
                                        }
                                        printf("\"\n");
                                    }
                                    else
                                    {
                                        // Binary content - no preview
                                    }
                                    free(ptrDecoded);
                                }
                                else
                                {
                                    // Could not decode - no preview
                                }
                                free(ptrEncoded);
                            }
                        }

                        intShown++;

                        // Pause every pageSize entries
                        if (intShown % intPageSize == 0 && intShown < intFilteredCount)
                        {
                            // Check if there are more matching entries below
                            bool blnMoreBelow = false;
                            {
                                int intPeek = intK - 1;
                                while (intPeek >= 0)
                                {
                                    bool blnPeekMatch = true;
                                    if (strFrom && strcmp(arrFiles[intPeek], strFrom) < 0) { blnPeekMatch = false; }
                                    if (strTo && strcmp(arrFiles[intPeek], strTo) > 0) { blnPeekMatch = false; }
                                    if (blnPeekMatch) { blnMoreBelow = true; break; }
                                    intPeek--;
                                }
                            }

                            if (blnMoreBelow)
                            {
                                printf("\n-- %d of %d shown. Press Enter for more, Ctrl+C to stop --", intShown, intFilteredCount);
                                fflush(stdout);
                                {
                                    int intCh = getchar();
                                    if (intCh == EOF) { break; }
                                }
                                printf("\n");
                            }
                        }
                    }

                    intK--;
                }
            }
        }

        printf("\n%d message(s) shown.\n", intShown);
    }

    intResult = 0;
    freeRomContext(&objCtx);
    return intResult;
}