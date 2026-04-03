// Cyborg NTS Client - ntssend v20260403
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE License 1.1.
// ZOSCII core logic remains under MIT License.

// Windows Version
// Publishes a message to a queue.
//
// Usage:
//   ntssend <queue> <message>
//   ntssend <queue> -f <filepath>
//
// Build:
//   cl /O2 /MT ntssend.c /link /SUBSYSTEM:CONSOLE

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

// --- WinHTTP POST ---
// Posts form data to the MQ server: // --- Multipart Form POST (matches web publisher) ---

static bool httpPost(const char* strURL_a, const char* strQueueName_a,
                     const uint8_t* ptrData_a, long lngDataSize_a)
{
    bool blnResult = false;
    HINTERNET hSession = NULL;
    HINTERNET hConnect = NULL;
    HINTERNET hRequest = NULL;
    URL_COMPONENTSW objUrlComp;
    wchar_t arrWideURL[MAX_URL] = {0};
    wchar_t arrHostName[MAX_HOSTNAME] = {0};
    wchar_t arrUrlPath[BUFFER1K] = {0};
    DWORD dwStatusCode = 0;
    DWORD dwSize = sizeof(dwStatusCode);
    char* ptrPostBody = NULL;
    long lngPostBodySize = 0;
    char* ptrBoundary = NULL;
    char strBoundary[CONTENT_BOUNDARY] = {0};
    int intI = 0;

    // Generate a random boundary string
    snprintf(strBoundary, sizeof(strBoundary), "---------------------------%08X%08X",
             (unsigned int)(time(NULL) & 0xFFFFFFFF),
             (unsigned int)(GetCurrentProcessId() & 0xFFFFFFFF));

    // Convert URL to wide string
    MultiByteToWideChar(CP_UTF8, 0, strURL_a, -1, arrWideURL, MAX_URL);

    // Parse URL
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

    // Build multipart form body
    // --boundary
    // Content-Disposition: form-data; name="action"
    // 
    // publish
    // --boundary
    // Content-Disposition: form-data; name="q"
    // 
    // {queue_name}
    // --boundary
    // Content-Disposition: form-data; name="r"
    // 
    // {retention}
    // --boundary
    // Content-Disposition: form-data; name="msg"; filename="msg.bin"
    // Content-Type: application/octet-stream
    // 
    // {raw binary data}
    // --boundary--

    {
        // Over-allocate: binary data + generous fixed overhead for multipart headers
        // 4 boundary sections * ~200 bytes each + queue name + binary payload
        long lngOverhead = 2048 + (long)strlen(strQueueName_a);

        ptrPostBody = (char*)malloc(lngOverhead + lngDataSize_a);
        if (!ptrPostBody)
        {
            fprintf(stderr, "Error: Out of memory\n");
            return false;
        }

        char* ptrPos = ptrPostBody;

        // action field
        ptrPos += sprintf(ptrPos, "--%s\r\n", strBoundary);
        ptrPos += sprintf(ptrPos, "Content-Disposition: form-data; name=\"action\"\r\n\r\n");
        ptrPos += sprintf(ptrPos, "publish\r\n");

        // q field (queue name)
        ptrPos += sprintf(ptrPos, "--%s\r\n", strBoundary);
        ptrPos += sprintf(ptrPos, "Content-Disposition: form-data; name=\"q\"\r\n\r\n");
        ptrPos += sprintf(ptrPos, "%s\r\n", strQueueName_a);

        // r field (retention)
        ptrPos += sprintf(ptrPos, "--%s\r\n", strBoundary);
        ptrPos += sprintf(ptrPos, "Content-Disposition: form-data; name=\"r\"\r\n\r\n");
        ptrPos += sprintf(ptrPos, "7\r\n");

        // msg field (binary file)
        ptrPos += sprintf(ptrPos, "--%s\r\n", strBoundary);
        ptrPos += sprintf(ptrPos, "Content-Disposition: form-data; name=\"msg\"; filename=\"msg.bin\"\r\n");
        ptrPos += sprintf(ptrPos, "Content-Type: application/octet-stream\r\n\r\n");

        // Copy raw binary data
        memcpy(ptrPos, ptrData_a, lngDataSize_a);
        ptrPos += lngDataSize_a;

        // Final boundary
        ptrPos += sprintf(ptrPos, "\r\n--%s--\r\n", strBoundary);

        lngPostBodySize = (long)(ptrPos - ptrPostBody);
    }

    // WinHTTP session
    hSession = WinHttpOpen(L"NTS Client/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME,
        WINHTTP_NO_PROXY_BYPASS, 0);

    if (hSession)
    {
        hConnect = WinHttpConnect(hSession, arrHostName, objUrlComp.nPort, 0);

        if (hConnect)
        {
            DWORD dwFlags = (objUrlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
            hRequest = WinHttpOpenRequest(hConnect, L"POST", arrUrlPath,
                NULL, WINHTTP_NO_REFERER,
                WINHTTP_DEFAULT_ACCEPT_TYPES, dwFlags);

            if (hRequest)
            {
                // Build Content-Type header with boundary
                char strContentType[CONTENT_TYPE] = {0};
                snprintf(strContentType, sizeof(strContentType),
                    "Content-Type: multipart/form-data; boundary=%s", strBoundary);

                wchar_t arrWideContentType[CONTENT_TYPE] = {0};
                MultiByteToWideChar(CP_UTF8, 0, strContentType, -1, arrWideContentType, CONTENT_TYPE);

                if (WinHttpSendRequest(hRequest, arrWideContentType, -1,
                    (LPVOID)ptrPostBody, lngPostBodySize, lngPostBodySize, 0))
                {
                    if (WinHttpReceiveResponse(hRequest, NULL))
                    {
                        WinHttpQueryHeaders(hRequest,
                            WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                            WINHTTP_HEADER_NAME_BY_INDEX,
                            &dwStatusCode, &dwSize, WINHTTP_NO_HEADER_INDEX);

                        if (dwStatusCode == 200)
                        {
                            // Read response to check for errors
                            char arrResponseBuf[BUFFER1K] = {0};
                            DWORD dwBytesRead = 0;

                            WinHttpReadData(hRequest, arrResponseBuf, sizeof(arrResponseBuf) - 1, &dwBytesRead);
                            arrResponseBuf[dwBytesRead] = '\0';

                            // Check for error in JSON response
                            if (strstr(arrResponseBuf, "\"error\":\"\"") ||
                                strstr(arrResponseBuf, "\"error\": \"\""))
                            {
                                blnResult = true;
                            }
                            else
                            {
                                fprintf(stderr, "Server error: %s\n", arrResponseBuf);
                            }
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

    free(ptrPostBody);
    return blnResult;
}

// --- Entry Point ---

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    RomContext objCtx;
    char strGUID[MAX_GUID] = {0};
    char strServer[MAX_SERVER] = {0};
    uint8_t* ptrMessage = NULL;
    long lngMessageSize = 0;
    uint8_t* ptrEncoded = NULL;
    long lngEncodedSize = 0;

    printf("NTS Send v20260403\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License 1.1\n\n");

    if (intArgC_a < 3)
    {
        fprintf(stderr, "Usage: %s <queue> <message>\n", strArgv_a[0]);
        fprintf(stderr, "       %s <queue> -f <filepath>\n", strArgv_a[0]);
        return intResult;
    }

    if (!initRomContext(&objCtx))
    {
        return intResult;
    }

    // Resolve queue name to GUID and server
    if (!findQueueByName(&objCtx, strArgv_a[1], strGUID, sizeof(strGUID),
                           strServer, sizeof(strServer), NULL, 0))
    {
        fprintf(stderr, "Error: Queue '%s' not found\n", strArgv_a[1]);
        freeRomContext(&objCtx);
        return intResult;
    }

    printf("Queue: %s -> %s\n", strArgv_a[1], strGUID);
    printf("Server: %s\n", strServer);

    // Get message data
    if (intArgC_a >= 4 && _stricmp(strArgv_a[2], "-f") == 0)
    {
        // Read from file
        if (!readFileToBuffer(strArgv_a[3], &ptrMessage, &lngMessageSize))
        {
            fprintf(stderr, "Error: Could not read file '%s'\n", strArgv_a[3]);
            freeRomContext(&objCtx);
            return intResult;
        }
        printf("Read %ld bytes from file\n", lngMessageSize);
    }
    else
    {
        // Message from command line
        lngMessageSize = (long)strlen(strArgv_a[2]);
        ptrMessage = (uint8_t*)malloc(lngMessageSize);
        if (ptrMessage)
        {
            memcpy(ptrMessage, strArgv_a[2], lngMessageSize);
        }
    }

    if (!ptrMessage)
    {
        fprintf(stderr, "Error: No message data\n");
        freeRomContext(&objCtx);
        return intResult;
    }

    // UNSIGNAL encode the message
    printf("Encoding %ld bytes...\n", lngMessageSize);
    if (encodeChained(&objCtx, ptrMessage, lngMessageSize, &ptrEncoded, &lngEncodedSize))
    {
        printf("Encoded to %ld bytes\n", lngEncodedSize);

        // POST to server
        printf("Sending to server...\n");
        if (httpPost(strServer, strGUID, ptrEncoded, lngEncodedSize))
        {
            printf("Message sent successfully.\n");
            intResult = 0;
        }
        else
        {
            fprintf(stderr, "Error: Failed to send message\n");
        }

        free(ptrEncoded);
    }
    else
    {
        fprintf(stderr, "Error: Encode failed\n");
    }

    free(ptrMessage);
    freeRomContext(&objCtx);
    return intResult;
}