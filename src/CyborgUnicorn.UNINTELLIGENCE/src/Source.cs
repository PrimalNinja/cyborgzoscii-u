// CyborgUnicorn.ZOSCII - Source
// Extracts the embedded source.zip to a nominated path.
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

using System;
using System.IO;
using System.Reflection;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// Provides access to the embedded source code archive for this package.
    /// </summary>
    public static class Source
    {
        /// <summary>
        /// Save the embedded source.zip to the nominated file path.
        /// Returns true on success.
        /// </summary>
        public static bool SaveAs(string strPath_a)
        {
            bool blnResult = false;

            try
            {
                using (Stream objStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("source.zip"))
                {
                    if (objStream != null)
                    {
                        string strDir = Path.GetDirectoryName(strPath_a);

                        if (!string.IsNullOrEmpty(strDir) && !Directory.Exists(strDir))
                        {
                            Directory.CreateDirectory(strDir);
                        }

                        using (FileStream objFile = new FileStream(strPath_a, FileMode.Create, FileAccess.Write))
                        {
                            objStream.CopyTo(objFile);
                        }

                        blnResult = true;
                    }
                }
            }
            catch { }

            return blnResult;
        }
    }
}