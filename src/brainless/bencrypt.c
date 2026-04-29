// Cyborg BRAINLESS Encryption Protocol v20260430
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1

// Pure XOR Ouroboros Chain - No ROM, No ZOSCII, Just Brainless

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#ifdef _WIN32
    #include <fcntl.h>
    #include <io.h>
#endif

// Brainless Encrypt: XOR chain (reverse then tail^head)
void bencrypt(uint8_t* ptrData_a, size_t intLen_a)
{
    if (intLen_a < 2) return;
    
    // Reverse XOR Chain
    for (size_t intI = intLen_a - 2; intI > 0; intI--) {
        ptrData_a[intI] = ptrData_a[intI] ^ ptrData_a[intI + 1];
    }
    if (intLen_a >= 2) {
        ptrData_a[0] = ptrData_a[0] ^ ptrData_a[1];
    }
    
    // Ouroboros Step: Tail XOR'd against Head
    ptrData_a[intLen_a - 1] = ptrData_a[intLen_a - 1] ^ ptrData_a[0];
}

int main(int intArgC_a, char* strArgv_a[])
{
    bool blnEncryptOk = false;
    int intResult = 1;
    FILE* ptrInput = NULL;
    FILE* ptrOutput = NULL;
    uint8_t* ptrData = NULL;
    long intLen = 0;
    
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
    _setmode(_fileno(stdout), _O_BINARY);
#endif

    printf("BRAINLESS Encryption Protocol v20260430\n");
    printf("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1\n");
    printf("Pure XOR Ouroboros Chain - No ROM, No ZOSCII\n\n");

    if (intArgC_a == 3) {
        ptrInput = fopen(strArgv_a[1], "rb");
        if (ptrInput) {
            fseek(ptrInput, 0, SEEK_END);
            intLen = ftell(ptrInput);
            fseek(ptrInput, 0, SEEK_SET);
            
            ptrData = (uint8_t*)malloc(intLen);
            if (ptrData) {
                fread(ptrData, 1, intLen, ptrInput);
                fclose(ptrInput);
                
                bencrypt(ptrData, intLen);
                
                ptrOutput = fopen(strArgv_a[2], "wb");
                if (ptrOutput) {
                    fwrite(ptrData, 1, intLen, ptrOutput);
                    fclose(ptrOutput);
                    blnEncryptOk = true;
                    printf("Encrypt successful: %s -> %s (%ld bytes)\n", strArgv_a[1], strArgv_a[2], intLen);
                } else {
                    perror("Failed to open output");
                }
                free(ptrData);
            } else {
                fclose(ptrInput);
                fprintf(stderr, "Memory allocation failed\n");
            }
        } else {
            perror("Failed to open input");
        }
        
        if (blnEncryptOk) {
            intResult = 0;
        } else {
            fprintf(stderr, "Encryption failed\n");
        }
    } else {
        fprintf(stderr, "Usage: %s <inputfile> <outputfile>\n", strArgv_a[0]);
    }
    
    return intResult;
}