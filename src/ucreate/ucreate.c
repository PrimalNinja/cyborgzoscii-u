// Cyborg ZOSCII ROM Creator v20260416
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.

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

#define ROM_SIZE 131072L

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

static uint8_t* loadFile(const char* strFilename_a, long* lngSize_a)
{
    uint8_t* ptrData = NULL;
    FILE* ptrFile = NULL;

    *lngSize_a = 0;

    ptrFile = fopen(strFilename_a, "rb");
    if (ptrFile)
    {
        fseek(ptrFile, 0, SEEK_END);
        *lngSize_a = ftell(ptrFile);
        fseek(ptrFile, 0, SEEK_SET);

        if (*lngSize_a > 0)
        {
            ptrData = (uint8_t*)malloc(*lngSize_a);
            if (ptrData)
            {
                fread(ptrData, 1, *lngSize_a, ptrFile);
            }
            else
            {
                *lngSize_a = 0;
            }
        }
        fclose(ptrFile);
    }

    return ptrData;
}

int main(int intArgC_a, char* strArgv_a[])
{
    uint8_t* arrInputData[3] = {NULL};
    long arrInputSize[3] = {0};
    uint8_t arrROM[ROM_SIZE] = {0};
    long arrShare[3] = {0};
    double dblPos[3] = {0.0};
    double dblStep[3] = {0.0};
    long intBytesPerFile = 0;
    int intFileIdx = 0;
    int intI = 0;
    int intInputCount = 0;
    long intOutPos = 0;
    long intRemainder = 0;
    int intResult = 1;
    long intSamplePos = 0;
    FILE* ptrOutput = NULL;
    const char* strOutputPath = NULL;

#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("ZOSCII ROM Creator v20260416\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n\n");

    if (intArgC_a < 3 || intArgC_a > 5)
    {
        fprintf(stderr, "Usage: %s <input1> [input2] [input3] <output>\n", strArgv_a[0]);
        fprintf(stderr, "  Creates a 128KB ROM file from 1-3 input files\n");
        fprintf(stderr, "  Recommended: JPEG or MP3 files (real-world physics captures)\n");
        return intResult;
    }

    // Last parameter is always output
    intInputCount = intArgC_a - 2;
    strOutputPath = strArgv_a[intArgC_a - 1];

    // Check output doesn't already exist
    if (fileExists(strOutputPath))
    {
        fprintf(stderr, "Error: Output file already exists: %s\n", strOutputPath);
        return intResult;
    }

    // Load all input files
    for (intI = 0; intI < intInputCount; intI++)
    {
        arrInputData[intI] = loadFile(strArgv_a[intI + 1], &arrInputSize[intI]);
        if (!arrInputData[intI])
        {
            fprintf(stderr, "Error: Cannot load input file: %s\n", strArgv_a[intI + 1]);
            // Cleanup already loaded
            for (intI = intI - 1; intI >= 0; intI--)
            {
                free(arrInputData[intI]);
            }
            return intResult;
        }
        if (arrInputSize[intI] == 0)
        {
            fprintf(stderr, "Error: Input file is empty: %s\n", strArgv_a[intI + 1]);
            for (intI = intI; intI >= 0; intI--)
            {
                free(arrInputData[intI]);
            }
            return intResult;
        }
    }

    // Calculate bytes each file contributes and step size
    intBytesPerFile = ROM_SIZE / intInputCount;
    intRemainder = ROM_SIZE % intInputCount;

    for (intI = 0; intI < intInputCount; intI++)
    {
        arrShare[intI] = intBytesPerFile;
        if (intI < intRemainder)
        {
            arrShare[intI]++;
        }
        dblStep[intI] = (double)arrInputSize[intI] / (double)arrShare[intI];
        dblPos[intI] = 0.0;
    }

    // Build ROM by alternating through input files
    intOutPos = 0;
    while (intOutPos < ROM_SIZE)
    {
        for (intFileIdx = 0; intFileIdx < intInputCount && intOutPos < ROM_SIZE; intFileIdx++)
        {
            if (arrShare[intFileIdx] > 0)
            {
                intSamplePos = (long)dblPos[intFileIdx];
                if (intSamplePos >= arrInputSize[intFileIdx])
                {
                    intSamplePos = arrInputSize[intFileIdx] - 1;
                }
                arrROM[intOutPos] = arrInputData[intFileIdx][intSamplePos];
                dblPos[intFileIdx] += dblStep[intFileIdx];
                arrShare[intFileIdx]--;
                intOutPos++;
            }
        }
    }

    // Write output
    ptrOutput = fopen(strOutputPath, "wb");
    if (ptrOutput)
    {
        if (fwrite(arrROM, 1, ROM_SIZE, ptrOutput) == ROM_SIZE)
        {
            printf("Created: %s (128KB)\n", strOutputPath);
            printf("  Sources:\n");
            for (intI = 0; intI < intInputCount; intI++)
            {
                printf("    %s (%ld bytes, step %.2f)\n", strArgv_a[intI + 1], arrInputSize[intI], 
                    (double)arrInputSize[intI] / (double)(ROM_SIZE / intInputCount));
            }
            intResult = 0;
        }
        else
        {
            fprintf(stderr, "Error: Failed to write output file\n");
        }
        fclose(ptrOutput);
    }
    else
    {
        fprintf(stderr, "Error: Cannot create output file: %s\n", strOutputPath);
    }

    // Cleanup
    for (intI = 0; intI < intInputCount; intI++)
    {
        if (arrInputData[intI])
        {
            free(arrInputData[intI]);
        }
    }

    return intResult;
}