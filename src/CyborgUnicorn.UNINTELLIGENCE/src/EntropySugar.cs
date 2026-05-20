// CyborgUnicorn.ZOSCII - EntropySugar
// Session entropy collection for ROM generation and security sugar
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CyborgUnicorn.ZOSCII
{
    /// <summary>
    /// Collects and manages session-specific entropy from system and user interaction sources.
    /// Entropy Sugar is additive salt that makes ROM generation session-specific.
    /// Different sessions with the same MP3s produce measurably different ROMs.
    ///
    /// FREE_MEM and CPU_PCT are not captured here as PerformanceCounter is platform-specific.
    /// Inject them from the caller via Add("FREE_MEM", ...) and Add("CPU_PCT", ...) using
    /// PerformanceCounter in your WinForms app where it is natively available.
    /// </summary>
    public class EntropySugar
    {
        // --- Fields ---

        private Dictionary<string, string> m_dictEntropy = new Dictionary<string, string>();
        private DateTime m_dtStartTime = DateTime.Now;

        // --- Add / Inject ---

        /// <summary>
        /// Inject a named entropy value. Use for JS-side values, user interaction counters,
        /// PerformanceCounter readings (FREE_MEM, CPU_PCT), or any external entropy source.
        /// </summary>
        public void Add(string strCode_a, string strValue_a)
        {
            m_dictEntropy[strCode_a] = strValue_a;
        }

        // --- Collection ---

        /// <summary>
        /// Capture fast C#-side entropy sources. Call approximately every 10 seconds.
        /// Populates: EXE_SIZE, ROM_COUNT, SYS_TIME, UPTIME_MS, DECODE_OPS.
        /// FREE_MEM and CPU_PCT must be injected by the caller via Add().
        /// </summary>
        public void CaptureFast(string strRomsFolder_a = "")
        {
            try
            {
                string strExePath = Process.GetCurrentProcess().MainModule.FileName;
                m_dictEntropy["EXE_SIZE"] = new FileInfo(strExePath).Length.ToString();
            }
            catch { }

            try
            {
                if (strRomsFolder_a.Length > 0 && Directory.Exists(strRomsFolder_a))
                {
                    m_dictEntropy["ROM_COUNT"] = Directory.GetFiles(strRomsFolder_a).Length.ToString();
                }
            }
            catch { }

            try
            {
                m_dictEntropy["SYS_TIME"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            }
            catch { }

            try
            {
                m_dictEntropy["UPTIME_MS"] = ((long)(DateTime.Now - m_dtStartTime).TotalMilliseconds).ToString();
            }
            catch { }

            if (!m_dictEntropy.ContainsKey("DECODE_OPS"))
            {
                m_dictEntropy["DECODE_OPS"] = "0";
            }
        }

        /// <summary>
        /// Capture on-demand entropy from data folders (file counts, 1-level deep for MP3 folders).
        /// Populates: FILE_COUNT.
        /// strDataFolder_a    — root data folder for fixed folders (contacts, roms etc)
        /// arrFixedFolders_a  — subfolder names under strDataFolder_a, scanned flat
        /// arrMp3Folders_a    — full paths to mp3 folders, scanned 1 level deep
        /// </summary>
        public void CaptureOnDemand(string strDataFolder_a, string[] arrFixedFolders_a, string[] arrMp3Folders_a)
        {
            long lngFileCount = 0;
            int intI = 0;
            int intJ = 0;

            // Fixed folders — flat scan
            for (intI = 0; intI < arrFixedFolders_a.Length; intI++)
            {
                string strFolder = Path.Combine(strDataFolder_a, arrFixedFolders_a[intI]);
                if (Directory.Exists(strFolder))
                {
                    try { lngFileCount += Directory.GetFiles(strFolder).Length; }
                    catch { }
                }
            }

            // MP3 folders — 1 level deep
            for (intI = 0; intI < arrMp3Folders_a.Length; intI++)
            {
                string strMp3Folder = arrMp3Folders_a[intI];
                if (strMp3Folder.Length > 0 && Directory.Exists(strMp3Folder))
                {
                    try
                    {
                        lngFileCount += Directory.GetFiles(strMp3Folder).Length;
                        string[] arrSubs = Directory.GetDirectories(strMp3Folder);
                        for (intJ = 0; intJ < arrSubs.Length; intJ++)
                        {
                            try { lngFileCount += Directory.GetFiles(arrSubs[intJ]).Length; }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            m_dictEntropy["FILE_COUNT"] = lngFileCount.ToString();
        }

        /// <summary>
        /// Capture slow C#-side entropy sources. Call approximately every 30 seconds.
        /// Populates: FREE_DISK, PROC_HANDLES.
        /// </summary>
        public void CaptureSlow(string strDataFolder_a = "")
        {
            try
            {
                string strRoot = Path.GetPathRoot(
                    strDataFolder_a.Length > 0 ? strDataFolder_a : AppDomain.CurrentDomain.BaseDirectory
                );
                m_dictEntropy["FREE_DISK"] = new DriveInfo(strRoot).AvailableFreeSpace.ToString();
            }
            catch { }

            try
            {
                m_dictEntropy["PROC_HANDLES"] = Process.GetCurrentProcess().HandleCount.ToString();
            }
            catch { }
        }

        // --- Retrieval ---

        /// <summary>
        /// Get all entropy values — use for display in a stats screen.
        /// </summary>
        public Dictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(m_dictEntropy);
        }

        /// <summary>
        /// Get a stored entropy value as a long integer (numeric entropy codes). Returns 0 if missing or unparseable.
        /// </summary>
        public long Get(string strKey_a)
        {
            long lngResult = 0;
            string strVal = "";

            if (m_dictEntropy.TryGetValue(strKey_a, out strVal))
            {
                long.TryParse(strVal, out lngResult);
            }

            return lngResult;
        }

        /// <summary>
        /// Get a stored entropy value as a string. Returns empty string if missing.
        /// </summary>
        public string GetString(string strKey_a)
        {
            string strResult = "";
            m_dictEntropy.TryGetValue(strKey_a, out strResult);
            return strResult ?? "";
        }

        /// <summary>
        /// Export all entropy values as a JSON object string.
        /// </summary>
        public string ToJson()
        {
            string strResult = "{";
            bool blnFirst = true;

            foreach (KeyValuePair<string, string> objPair in m_dictEntropy)
            {
                if (!blnFirst) { strResult += ","; }
                string strKey = objPair.Key.Replace("\"", "_");
                string strVal = (objPair.Value ?? "").Replace("\"", "_");
                strResult += "\"" + strKey + "\":\"" + strVal + "\"";
                blnFirst = false;
            }

            strResult += "}";
            return strResult;
        }
    }
}