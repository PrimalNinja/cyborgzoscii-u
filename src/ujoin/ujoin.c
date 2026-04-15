// Cyborg UNSIGNAL Protocol v20260416
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// 3-of-5 combinatorial share joining

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

// C(5,3) = 10 combinations — must match usplit.c exactly
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

// Lookup: for each row, which share index to read from
static int arrReadFrom[PATTERN_LEN] = {0};

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

static bool buildBasePath(const char* strInput_a, char* strBase_a, size_t lngBaseSize_a)
{
    bool blnResult = false;
    size_t lngLen = 0;

    strncpy(strBase_a, strInput_a, lngBaseSize_a - 1);
    lngLen = strlen(strBase_a);

    // Strip .s1 through .s5 suffix
    if (lngLen >= 3 &&
        strBase_a[lngLen - 3] == '.' &&
        strBase_a[lngLen - 2] == 's' &&
        strBase_a[lngLen - 1] >= '1' &&
        strBase_a[lngLen - 1] <= '5')
    {
        strBase_a[lngLen - 3] = '\0';
        blnResult = true;
    }

    return blnResult;
}

static bool buildReadTable(const bool* arrPresent_a)
{
    bool blnResult = true;
    int intRow = 0;
    int intJ = 0;
    bool blnFound = false;

    for (intRow = 0; intRow < PATTERN_LEN; intRow++)
    {
        blnFound = false;
        for (intJ = 0; intJ < 3; intJ++)
        {
            if (arrPresent_a[arrPattern[intRow][intJ]])
            {
                arrReadFrom[intRow] = arrPattern[intRow][intJ];
                blnFound = true;
                break;
            }
        }
        if (!blnFound)
        {
            blnResult = false;
            break;
        }
    }

    return blnResult;
}

int main(int intArgC_a, char* strArgv_a[])
{
    int intResult = 1;
    char strBasePath[1024] = {0};
    char strSharePath[SHARE_COUNT][1024] = {{0}};
    bool arrPresent[SHARE_COUNT] = {false};
    int intPresentCount = 0;
    FILE* ptrShares[SHARE_COUNT] = {NULL};
    FILE* ptrOutput = NULL;
    long arrShareSize[SHARE_COUNT] = {0};
    long arrSharePos[SHARE_COUNT] = {0};
    long lngPos = 0;
    int intRow = 0;
    int intShareIdx = 0;
    int intByte = 0;
    int intI = 0;
    bool blnError = false;

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("UNSIGNAL Join v20260416\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a != 3)
    {
        fprintf(stderr, "Usage: %s <filename.s1|.s2|.s3|.s4|.s5> <outputfile>\n", strArgv_a[0]);
        return intResult;
    }

    // Strip .sN suffix from input to find sibling shares
    if (!buildBasePath(strArgv_a[1], strBasePath, sizeof(strBasePath)))
    {
        fprintf(stderr, "Error: Input file must have .s1 to .s5 extension\n");
        return intResult;
    }

    // Check output doesn't already exist
    if (fileExists(strArgv_a[2]))
    {
        fprintf(stderr, "Error: Destination file already exists: %s\n", strArgv_a[2]);
        return intResult;
    }

    // Discover which shares are present
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        snprintf(strSharePath[intI], sizeof(strSharePath[intI]), "%s.s%d", strBasePath, intI + 1);
        arrPresent[intI] = fileExists(strSharePath[intI]);
        if (arrPresent[intI])
        {
            intPresentCount++;
        }
    }

    if (intPresentCount < 3)
    {
        fprintf(stderr, "Error: Need at least 3 share files to reconstruct (found %d)\n", intPresentCount);
        return intResult;
    }

    // Build the read lookup table
    if (!buildReadTable(arrPresent))
    {
        fprintf(stderr, "Error: Available shares cannot cover all positions\n");
        return intResult;
    }

    // Open present share files and get sizes
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        if (arrPresent[intI])
        {
            ptrShares[intI] = fopen(strSharePath[intI], "rb");
            if (!ptrShares[intI])
            {
                fprintf(stderr, "Error: Cannot open share file: %s\n", strSharePath[intI]);
                blnError = true;
                break;
            }
            fseek(ptrShares[intI], 0, SEEK_END);
            arrShareSize[intI] = ftell(ptrShares[intI]);
            fseek(ptrShares[intI], 0, SEEK_SET);
            arrSharePos[intI] = 0;
        }
    }

    // Open output
    if (!blnError)
    {
        ptrOutput = fopen(strArgv_a[2], "wb");
        if (!ptrOutput)
        {
            fprintf(stderr, "Error: Cannot create output file: %s\n", strArgv_a[2]);
            blnError = true;
        }
    }

    // Join
    if (!blnError)
    {
        lngPos = 0;
        while (!blnError)
        {
            intRow = (int)(lngPos % PATTERN_LEN);
            intShareIdx = arrReadFrom[intRow];

            // Check if this share still has bytes
            if (arrSharePos[intShareIdx] >= arrShareSize[intShareIdx])
            {
                break;
            }

            intByte = fgetc(ptrShares[intShareIdx]);
            if (intByte == EOF)
            {
                break;
            }
            arrSharePos[intShareIdx]++;

            // Advance all other present shares that also have this position
            for (intI = 0; intI < 3; intI++)
            {
                if (arrPattern[intRow][intI] != intShareIdx && arrPresent[arrPattern[intRow][intI]])
                {
                    if (arrSharePos[arrPattern[intRow][intI]] < arrShareSize[arrPattern[intRow][intI]])
                    {
                        fgetc(ptrShares[arrPattern[intRow][intI]]);
                        arrSharePos[arrPattern[intRow][intI]]++;
                    }
                }
            }

            if (fputc(intByte, ptrOutput) == EOF)
            {
                blnError = true;
                break;
            }
            lngPos++;
        }
    }

    // Close all files
    for (intI = 0; intI < SHARE_COUNT; intI++)
    {
        if (ptrShares[intI])
        {
            fclose(ptrShares[intI]);
        }
    }
    if (ptrOutput)
    {
        fclose(ptrOutput);
    }

    // Clean up on error
    if (blnError)
    {
        fprintf(stderr, "Error: Join failed\n");
        if (fileExists(strArgv_a[2]))
        {
            remove(strArgv_a[2]);
        }
    }
    else
    {
        printf("Joined: %s\n", strArgv_a[2]);
        printf("  Shares used: ");
        for (intI = 0; intI < SHARE_COUNT; intI++)
        {
            if (arrPresent[intI])
            {
                printf("s%d ", intI + 1);
            }
        }
        printf("\n");
        intResult = 0;
    }

    return intResult;
}