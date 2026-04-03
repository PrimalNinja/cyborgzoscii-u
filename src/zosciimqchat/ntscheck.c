// Cyborg NTS Client - ntscheck v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Checks server for new messages without downloading or updating state.
//
// Usage:
//   ntscheck [queue]     - Check one or all queues
//
// Build:
//   cl /O2 /MT ntscheck.c /link /SUBSYSTEM:CONSOLE

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

// --- Queue scanning ---

typedef struct
{
    char strName[256];
    char strGUID[MAX_GUID];
    char strServer[MAX_SERVER];
} QueueInfo;

static int scanAllQueues(const RomContext* ptrCtx_a, QueueInfo* arrQueues_a, int intMaxQueues_a)
{
    char strSearchPath[BUFFER1K];
    WIN32_FIND_DATAA fd;
    HANDLE hFind;
    char strFilePath[BUFFER1K];
    int intCount = 0;

    snprintf(strSearchPath, sizeof(strSearchPath), "%squeues\\*.json.sig", ptrCtx_a->strBaseDir);
    hFind = FindFirstFileA(strSearchPath, &fd);
    if (hFind == INVALID_HANDLE_VALUE) { return 0; }

    do
    {
        uint8_t* enc = NULL; long esz = 0;
        uint8_t* dec = NULL; long dsz = 0;

        snprintf(strFilePath, sizeof(strFilePath), "%squeues\\%s", ptrCtx_a->strBaseDir, fd.cFileName);
        if (readFileToBuffer(strFilePath, &enc, &esz))
        {
            if (decodeChained(ptrCtx_a, enc, esz, &dec, &dsz))
            {
                if (isValidText(dec, dsz) && intCount < intMaxQueues_a)
                {
                    char* pN = strstr((char*)dec, "\"name\":\"");
                    char* pG = strstr((char*)dec, "\"guid\":\"");
                    char* pS = strstr((char*)dec, "\"server\":\"");

                    if (pN && pG && pS)
                    {
                        pN += 8; char* pNE = strchr(pN, '"');
                        pG += 8; char* pGE = strchr(pG, '"');
                        pS += 10; char* pSE = strchr(pS, '"');

                        if (pNE && pGE && pSE)
                        {
                            int nl = (int)(pNE - pN); if (nl > 255) nl = 255;
                            int gl = (int)(pGE - pG); if (gl > 63) gl = 63;
                            int sl = (int)(pSE - pS); if (sl > 511) sl = 511;

                            memset(&arrQueues_a[intCount], 0, sizeof(QueueInfo));
                            strncpy(arrQueues_a[intCount].strName, pN, nl);
                            strncpy(arrQueues_a[intCount].strGUID, pG, gl);
                            strncpy(arrQueues_a[intCount].strServer, pS, sl);
                            intCount++;
                        }
                    }
                }
                free(dec);
            }
            free(enc);
        }
    }
    while (FindNextFileA(hFind, &fd));

    FindClose(hFind);
    return intCount;
}

// --- HTTP check - single fetch to see if anything new exists ---
// Returns: 1 = new messages, 0 = up to date, -1 = error

static int httpCheckNew(const char* strURL_a, const char* strQueueGUID_a, const char* strAfterPointer_a)
{
    int intResult = -1;
    HINTERNET hSession = NULL;
    HINTERNET hConnect = NULL;
    HINTERNET hRequest = NULL;
    URL_COMPONENTSW objUrlComp;
    wchar_t arrWideURL[MAX_URL] = {0};
    wchar_t arrHostName[MAX_HOSTNAME] = {0};
    wchar_t arrUrlPath[BUFFER1K] = {0};
    char strPostBody[MAX_POSTBODY] = {0};

    MultiByteToWideChar(CP_UTF8, 0, strURL_a, -1, arrWideURL, MAX_URL);
    memset(&objUrlComp, 0, sizeof(objUrlComp));
    objUrlComp.dwStructSize = sizeof(objUrlComp);
    objUrlComp.lpszHostName = arrHostName; objUrlComp.dwHostNameLength = MAX_HOSTNAME;
    objUrlComp.lpszUrlPath = arrUrlPath; objUrlComp.dwUrlPathLength = BUFFER1K;

    if (!WinHttpCrackUrl(arrWideURL, 0, 0, &objUrlComp)) { return -1; }

    hSession = WinHttpOpen(L"NTS Client/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!hSession) { return -1; }

    hConnect = WinHttpConnect(hSession, arrHostName, objUrlComp.nPort, 0);
    if (!hConnect) { WinHttpCloseHandle(hSession); return -1; }

    {
        DWORD dwFlags = (objUrlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;

        snprintf(strPostBody, sizeof(strPostBody), "action=fetch&q=%s&after=%s",
            strQueueGUID_a, strAfterPointer_a);

        hRequest = WinHttpOpenRequest(hConnect, L"POST", arrUrlPath, NULL,
            WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, dwFlags);

        if (hRequest)
        {
            wchar_t* ptrCT = L"Content-Type: application/x-www-form-urlencoded";
            long lngBodyLen = (long)strlen(strPostBody);

            if (WinHttpSendRequest(hRequest, ptrCT, -1, strPostBody, lngBodyLen, lngBodyLen, 0))
            {
                if (WinHttpReceiveResponse(hRequest, NULL))
                {
                    DWORD dwStatusCode = 0; DWORD dwSize = sizeof(dwStatusCode);
                    WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                        WINHTTP_HEADER_NAME_BY_INDEX, &dwStatusCode, &dwSize, WINHTTP_NO_HEADER_INDEX);

                    if (dwStatusCode == 200)
                    {
                        // Check for Content-Disposition header — its presence means a file was returned
                        wchar_t arrDisp[CONTENT_DISPOSITION] = {0};
                        DWORD dwDispSize = sizeof(arrDisp);

                        if (WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CUSTOM, L"Content-Disposition",
                            arrDisp, &dwDispSize, WINHTTP_NO_HEADER_INDEX))
                        {
                            intResult = 1; // New messages available
                        }
                        else
                        {
                            intResult = 0; // No new messages
                        }
                    }

                    // Drain the response body so connection closes cleanly
                    {
                        char arrDrain[BUFFER4K];
                        DWORD dwRead = 0;
                        while (WinHttpReadData(hRequest, arrDrain, sizeof(arrDrain), &dwRead) && dwRead > 0) {}
                    }
                }
            }

            WinHttpCloseHandle(hRequest);
        }
    }

    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);
    return intResult;
}

// --- Entry Point ---

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;

    printf("NTS Check v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (!initRomContext(&objCtx))
    {
        return intResult;
    }

    if (intArgC_a >= 2)
    {
        // Check specific queue
        char strGUID[MAX_GUID] = {0};
        char strServer[MAX_SERVER] = {0};
        char strLastPointer[MAX_POINTER] = {0};
        QueueInfo arrQueues[1];
        int intFound = 0;

        // Use scan to find the queue
        QueueInfo arrAll[500];
        int intTotal = scanAllQueues(&objCtx, arrAll, 500);
        int intI;

        for (intI = 0; intI < intTotal; intI++)
        {
            if (_stricmp(arrAll[intI].strName, strArgv_a[1]) == 0)
            {
                loadState(&objCtx, arrAll[intI].strGUID, strLastPointer, sizeof(strLastPointer));
                printf("Checking %s...\n", arrAll[intI].strName);
                int intNew = httpCheckNew(arrAll[intI].strServer, arrAll[intI].strGUID, strLastPointer);

                if (intNew < 0)
                {
                    printf("%-20s %-38s ERROR\n", arrAll[intI].strName, arrAll[intI].strGUID);
                }
                else if (intNew == 0)
                {
                    printf("%-20s %-38s up to date\n", arrAll[intI].strName, arrAll[intI].strGUID);
                }
                else
                {
                    printf("%-20s %-38s new messages\n", arrAll[intI].strName, arrAll[intI].strGUID);
                }
                intFound = 1;
                break;
            }
        }

        if (!intFound)
        {
            fprintf(stderr, "Error: Queue '%s' not found\n", strArgv_a[1]);
            freeRomContext(&objCtx);
            return intResult;
        }
    }
    else
    {
        // Check all queues
        QueueInfo arrQueues[500];
        int intTotal = scanAllQueues(&objCtx, arrQueues, 500);
        int intI;

        if (intTotal == 0)
        {
            printf("No queues visible with current ROMs.\n");
        }
        else
        {
            printf("Checking %d queue(s)...\n\n", intTotal);
            printf("%-20s %-38s %s\n", "Queue", "GUID", "Status");
            printf("%-20s %-38s %s\n", "-------", "----", "------");

            for (intI = 0; intI < intTotal; intI++)
            {
                char strLastPointer[MAX_POINTER] = {0};
                loadState(&objCtx, arrQueues[intI].strGUID, strLastPointer, sizeof(strLastPointer));

                int intNew = httpCheckNew(arrQueues[intI].strServer, arrQueues[intI].strGUID, strLastPointer);

                if (intNew < 0)
                {
                    printf("%-20s %-38s ERROR\n", arrQueues[intI].strName, arrQueues[intI].strGUID);
                }
                else if (intNew == 0)
                {
                    printf("%-20s %-38s up to date\n", arrQueues[intI].strName, arrQueues[intI].strGUID);
                }
                else
                {
                    printf("%-20s %-38s new messages\n", arrQueues[intI].strName, arrQueues[intI].strGUID);
                }
            }
        }
    }

    intResult = 0;
    freeRomContext(&objCtx);
    return intResult;
}