// Cyborg UNSIGNAL Protocol v20260416
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// 3-of-5 combinatorial share splitting

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

#define SHARE_COUNT 5
#define PATTERN_LEN 10

// C(5,3) = 10 combinations, each byte written to 3 of 5 files
// Any 3 files cover all 10 rows by pigeonhole principle
static const uint8_t arrPattern[PATTERN_LEN][3] = 
{
    {0, 1, 2},  // Row 0: s1 s2 s3
    {0, 1, 3},  // Row 1: s1 s2 s4
    {0, 1, 4},  // Row 2: s1 s2 s5
    {0, 2, 3},  // Row 3: s1 s3 s4
    {0, 2, 4},  // Row 4: s1 s3 s5
    {0, 3, 4},  // Row 5: s1 s4 s5
    {1, 2, 3},  // Row 6: s2 s3 s4
    {1, 2, 4},  // Row 7: s2 s3 s5
    {1, 3, 4},  // Row 8: s2 s4 s5
    {2, 3, 4}   // Row 9: s3 s4 s5
};

static bool fileExists(const char* strPath_a)
{
    bool blnResult = false;
    FILE* ptrFile = NULL;

    ptrFile = fopen(strPath_a, "rb");
    if (ptrFile)
    {
        fclose(ptrFile);
        blnResult = true;
    }

    return blnResult;
}

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    char strSharePath[SHARE_COUNT][1024] = {{0}};
    FILE* ptrInput = NULL;
    FILE* ptrShares[SHARE_COUNT] = {NULL};
    int intI = 0;
    int intJ = 0;
    long lngPos = 0;
    int intByte = 0;
    int intRow = 0;
    bool blnError = false;

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Split v20260416\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a != 3)
    {
        fprintf(stderr, "Usage: %s <inputfile> <outputfile>\n", strArgv_a[0]);
        return intResult;
    }

    // Check input file exists
    if (!fileExists(strArgv_a[1]))
    {
        fprintf(stderr, "Error: Input file not found\n");
        return intResult;
    }

    // Build share paths and check none already exist
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        snprintf(strSharePath[intI], sizeof(strSharePath[intI]), "%s.s%d", strArgv_a[2], intI + 1);
        if (fileExists(strSharePath[intI]))
        {
            fprintf(stderr, "Error: Share file already exists: %s\n", strSharePath[intI]);
            return intResult;
        }
    }

    // Open input
    ptrInput = fopen(strArgv_a[1], "rb");
    if (!ptrInput)
    {
        fprintf(stderr, "Error: Cannot open input file\n");
        return intResult;
    }

    // Open all share files
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        ptrShares[intI] = fopen(strSharePath[intI], "wb");
        if (!ptrShares[intI])
        {
            fprintf(stderr, "Error: Cannot create share file: %s\n", strSharePath[intI]);
            blnError = true;
            break;
        }
    }

    // Split
    if (!blnError)
    {
        lngPos = 0;
        while ((intByte = fgetc(ptrInput)) != EOF)
        {
            intRow = (int)(lngPos % PATTERN_LEN);
            for (intJ = 0; intJ < 3; intJ++)
            {
                if (fputc(intByte, ptrShares[arrPattern[intRow][intJ]]) == EOF)
                {
                    blnError = true;
                    break;
                }
            }
            if (blnError)
            {
                break;
            }
            lngPos++;
        }
    }

    // Close all files
    if (ptrInput)
    {
        fclose(ptrInput);
    }
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        if (ptrShares[intI])
        {
            fclose(ptrShares[intI]);
        }
    }

    // Clean up on error
    if (blnError)
    {
        fprintf(stderr, "Error: Split failed\n");
        for (intI = 0; intI < SHARE_COUNT; intI++)
        {
            if (fileExists(strSharePath[intI]))
            {
                remove(strSharePath[intI]);
            }
        }
    }
    else
    {
        printf("Split: %s\n", strArgv_a[1]);
        for (intI = 0; intI < SHARE_COUNT; intI++)
        {
            printf("  -> %s\n", strSharePath[intI]);
        }
        intResult = 0;
    }

    return intResult;
}