// Cyborg NTS Client - ntsfetch v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Fetches new messages from server and stores in local cache.
//
// Usage:
//   ntsfetch <queue>
//
// Build:
//   cl /O2 /MT ntsfetch.c /link /SUBSYSTEM:CONSOLE

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <time.h>
#include <fcntl.h>
#include <io.h>
#include <windows.h>
#include <winhttp.h>

#include "defines.h"
#include "unsignal.c"
#include "utils.c"

#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "advapi32.lib")

// ============================================================
// HTTP fetch - fetches one message at a time
// Returns filename via Content-Disposition, body as binary
// ============================================================

static bool httpFetchNext(const char* strURL_a, const char* strQueueGUID_a,
    const char* strAfterPointer_a,
    char* strFilename_a, int intFilenameSize_a,
    uint8_t** pptrData_a, long* plngDataSize_a)
{
    bool blnResult = false;
    HINTERNET hSession = NULL;
    HINTERNET hConnect = NULL;
    HINTERNET hRequest = NULL;
    URL_COMPONENTSW objUrlComp;
    wchar_t arrWideURL[MAX_URL] = {0};
    wchar_t arrHostName[MAX_HOSTNAME] = {0};
    wchar_t arrUrlPath[BUFFER1K] = {0};
    char strPostBody[MAX_POSTBODY] = {0};
    long lngPostBodySize = 0;

    MultiByteToWideChar(CP_UTF8, 0, strURL_a, -1, arrWideURL, MAX_URL);

    memset(&objUrlComp, 0, sizeof(objUrlComp));
    objUrlComp.dwStructSize = sizeof(objUrlComp);
    objUrlComp.lpszHostName = arrHostName;
    objUrlComp.dwHostNameLength = MAX_HOSTNAME;
    objUrlComp.lpszUrlPath = arrUrlPath;
    objUrlComp.dwUrlPathLength = BUFFER1K;

    if (!WinHttpCrackUrl(arrWideURL, 0, 0, &objUrlComp))
    {
        fprintf(stderr, "Error: Invalid URL\n");
        return false;
    }

    snprintf(strPostBody, sizeof(strPostBody), "action=fetch&q=%s&after=%s", strQueueGUID_a, strAfterPointer_a);
    lngPostBodySize = (long)strlen(strPostBody);

    hSession = WinHttpOpen(L"NTS Client/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);

    if (hSession)
    {
        hConnect = WinHttpConnect(hSession, arrHostName, objUrlComp.nPort, 0);
        if (hConnect)
        {
            DWORD dwFlags = (objUrlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
            hRequest = WinHttpOpenRequest(hConnect, L"POST", arrUrlPath, NULL,
                WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, dwFlags);

            if (hRequest)
            {
                wchar_t* ptrContentType = L"Content-Type: application/x-www-form-urlencoded";

                if (WinHttpSendRequest(hRequest, ptrContentType, -1,
                    strPostBody, lngPostBodySize, lngPostBodySize, 0))
                {
                    if (WinHttpReceiveResponse(hRequest, NULL))
                    {
                        DWORD dwStatusCode = 0;
                        DWORD dwSize = sizeof(dwStatusCode);
                        WinHttpQueryHeaders(hRequest,
                            WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                            WINHTTP_HEADER_NAME_BY_INDEX, &dwStatusCode, &dwSize, WINHTTP_NO_HEADER_INDEX);

                        if (dwStatusCode == 200)
                        {
                            // Check Content-Disposition for filename
                            wchar_t arrDispHeader[CONTENT_DISPOSITION] = {0};
                            DWORD dwDispSize = sizeof(arrDispHeader);
                            bool blnHasFile = false;

                            if (WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CUSTOM,
                                L"Content-Disposition", arrDispHeader, &dwDispSize, WINHTTP_NO_HEADER_INDEX))
                            {
                                // Parse filename from: attachment; filename="XXXXX.bin"
                                wchar_t* ptrFN = wcsstr(arrDispHeader, L"filename=\"");
                                if (ptrFN)
                                {
                                    ptrFN += 10;
                                    wchar_t* ptrEnd = wcschr(ptrFN, L'"');
                                    if (ptrEnd)
                                    {
                                        *ptrEnd = L'\0';
                                        WideCharToMultiByte(CP_UTF8, 0, ptrFN, -1,
                                            strFilename_a, intFilenameSize_a, NULL, NULL);
                                        blnHasFile = true;
                                    }
                                }
                            }

                            if (blnHasFile)
                            {
                                // Read response body
                                uint8_t* ptrBody = NULL;
                                long lngBodySize = 0;
                                long lngBodyCapacity = 65536;
                                DWORD dwBytesRead = 0;

                                ptrBody = (uint8_t*)malloc(lngBodyCapacity);
                                if (ptrBody)
                                {
                                    while (WinHttpReadData(hRequest, ptrBody + lngBodySize,
                                        lngBodyCapacity - lngBodySize, &dwBytesRead))
                                    {
                                        if (dwBytesRead == 0) break;
                                        lngBodySize += dwBytesRead;

                                        if (lngBodySize >= lngBodyCapacity)
                                        {
                                            lngBodyCapacity *= 2;
                                            ptrBody = (uint8_t*)realloc(ptrBody, lngBodyCapacity);
                                            if (!ptrBody) break;
                                        }
                                    }

                                    if (ptrBody && lngBodySize > 0)
                                    {
                                        *pptrData_a = ptrBody;
                                        *plngDataSize_a = lngBodySize;
                                        blnResult = true;
                                    }
                                    else
                                    {
                                        if (ptrBody) free(ptrBody);
                                    }
                                }
                            }
                            // No file = end of queue, not an error
                        }
                        else
                        {
                            fprintf(stderr, "HTTP error: %lu\n", dwStatusCode);
                        }
                    }
                }
                else
                {
                    fprintf(stderr, "Error: Failed to send request (%lu)\n", GetLastError());
                }
                WinHttpCloseHandle(hRequest);
            }
            WinHttpCloseHandle(hConnect);
        }
        WinHttpCloseHandle(hSession);
    }

    return blnResult;
}

// ============================================================
// Entry Point
// ============================================================

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    char strLastPointer[MAX_POINTER] = {0};
    char strFilename[MAX_POINTER] = {0};
    uint8_t* ptrData = NULL;
    long lngDataSize = 0;
    int intFetched = 0;
    char strQueueDir[BUFFER1K] = {0};
    char strFilePath[BUFFER1K] = {0};
    char strStatesDir[BUFFER1K] = {0};

    printf("NTS Fetch v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (intArgC_a < 2)
    {
        fprintf(stderr, "Usage: %s <queue>\n", strArgv_a[0]);
        return intResult;
    }

    if (!initRomContext(&objCtx))
    {
        return intResult;
    }

    // Ensure states directory exists
    snprintf(strStatesDir, sizeof(strStatesDir), "%sstates", objCtx.strBaseDir);
    ensureDirectory(strStatesDir);

    if (!findQueueByName(&objCtx, strArgv_a[1], strGUID, sizeof(strGUID),
        strServer, sizeof(strServer), NULL, 0))
    {
        fprintf(stderr, "Error: Queue '%s' not found\n", strArgv_a[1]);
        freeRomContext(&objCtx);
        return intResult;
    }

    printf("Queue: %s -> %s\n", strArgv_a[1], strGUID);
    printf("Server: %s\n", strServer);

    // Ensure queue directory exists
    snprintf(strQueueDir, sizeof(strQueueDir), "%squeues\\%s", objCtx.strBaseDir, strGUID);
    ensureDirectory(strQueueDir);

    // Load state pointer
    loadState(&objCtx, strGUID, strLastPointer, sizeof(strLastPointer));
    if (strlen(strLastPointer) > 0)
    {
        printf("Resuming from: %s\n", strLastPointer);
    }
    else
    {
        printf("Starting from beginning of queue\n");
    }

    // Fetch loop
    printf("Fetching...\n");
    while (true)
    {
        memset(strFilename, 0, sizeof(strFilename));
        ptrData = NULL;
        lngDataSize = 0;

        if (!httpFetchNext(strServer, strGUID, strLastPointer,
            strFilename, sizeof(strFilename), &ptrData, &lngDataSize))
        {
            // No more messages or error
            break;
        }

        // Save to local cache
        snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s\\%s",
            objCtx.strBaseDir, strGUID, strFilename);

        {
            FILE* ptrFile = fopen(strFilePath, "wb");
            if (ptrFile)
            {
                fwrite(ptrData, 1, lngDataSize, ptrFile);
                fclose(ptrFile);

                printf("  Fetched: %s (%ld bytes)\n", strFilename, lngDataSize);
                intFetched++;

                // Update pointer
                strncpy(strLastPointer, strFilename, sizeof(strLastPointer) - 1);
                saveState(&objCtx, strGUID, strLastPointer);
            }
            else
            {
                fprintf(stderr, "Error: Could not write file %s\n", strFilePath);
            }
        }

        free(ptrData);
    }

    printf("\nFetch complete. %d message(s) fetched.\n", intFetched);
    intResult = 0;

    freeRomContext(&objCtx);
    return intResult;
}