// CyborgUnicorn.ZOSCII - SecureDelete
// Two-pass secure file deletion (0xFF then 0x00 overwrite before delete)
// Compatible with secure delete logic in usplit.c and ujoin.c
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

using System;
using System.IO;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// Secure file and folder deletion.
    /// Overwrites file content with 0xFF then 0x00 before deletion to prevent recovery.
    /// Used internally by BSplit and BJoin on error cleanup.
    /// </summary>
    public static class SecureDelete
    {
        // --- Constants ---

        private const int CHUNK_SIZE = 4096;

        // --- Public ---

        /// <summary>
        /// Securely delete a file by overwriting with 0xFF, then 0x00, then deleting.
        /// Returns true on success.
        /// </summary>
        public static bool File(string strPath_a)
        {
            bool blnResult = false;
            int intI = 0;

            try
            {
                if (!System.IO.File.Exists(strPath_a))
                {
                    blnResult = false;
                }
                else
                {
                    long lngLength = new FileInfo(strPath_a).Length;

                    if (lngLength > 0)
                    {
                        byte[] arrFF = new byte[CHUNK_SIZE];
                        byte[] arr00 = new byte[CHUNK_SIZE];

                        for (intI = 0; intI < CHUNK_SIZE; intI++)
                        {
                            arrFF[intI] = 0xFF;
                            arr00[intI] = 0x00;
                        }

                        using (FileStream ptrFile = new FileStream(strPath_a, FileMode.Open, FileAccess.Write))
                        {
                            // Pass 1: overwrite with 0xFF
                            long lngWritten = 0;
                            while (lngWritten < lngLength)
                            {
                                int intToWrite = (int)Math.Min(CHUNK_SIZE, lngLength - lngWritten);
                                ptrFile.Write(arrFF, 0, intToWrite);
                                lngWritten += intToWrite;
                            }
                            ptrFile.Flush();

                            // Pass 2: overwrite with 0x00
                            ptrFile.Seek(0, SeekOrigin.Begin);
                            lngWritten = 0;
                            while (lngWritten < lngLength)
                            {
                                int intToWrite = (int)Math.Min(CHUNK_SIZE, lngLength - lngWritten);
                                ptrFile.Write(arr00, 0, intToWrite);
                                lngWritten += intToWrite;
                            }
                            ptrFile.Flush();
                        }
                    }

                    System.IO.File.Delete(strPath_a);
                    blnResult = true;
                }
            }
            catch { }

            return blnResult;
        }

        /// <summary>
        /// Securely delete all files in a folder (recursive), then remove the folder.
        /// Returns true if all files and the folder were deleted successfully.
        /// </summary>
        public static bool Folder(string strPath_a)
        {
            bool blnResult = true;
            int intI = 0;

            try
            {
                string[] arrFiles = Directory.GetFiles(strPath_a, "*", SearchOption.AllDirectories);

                for (intI = 0; intI < arrFiles.Length; intI++)
                {
                    if (!File(arrFiles[intI]))
                    {
                        blnResult = false;
                    }
                }

                if (blnResult)
                {
                    Directory.Delete(strPath_a, true);
                }
            }
            catch { }

            return blnResult;
        }
    }
}