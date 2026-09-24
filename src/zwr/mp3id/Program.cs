using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mp3Id
{
    /// <summary>
    /// MP3 Title Tag Updater - STANDALONE WINDOWS CONSOLE VERSION
    /// Ported from the original web (mp3id.php) tool.
    ///
    /// Usage:
    ///   mp3id.exe <folderpath> "<copyright text>"
    ///
    /// Behaviour:
    ///   - Processes every *.mp3 file directly inside <folderpath> (not subfolders),
    ///     with no skipping/filtering other than the basic MP3 validity check.
    ///   - For each file: strips ALL existing ID3 tags and writes a fresh minimal
    ///     ID3v2.3 tag containing:
    ///       TIT2 = title, the filename verbatim (minus .mp3)
    ///       TPE1 = artist, from ArtistName below
    ///       TYER / TDRC = year, taken from the file's last-modified date
    ///       TCOP = copyright, from the second command-line argument
    ///       COMM = contents of "<filename>.txt" next to the mp3, if present
    ///       USLT = contents of "<filename>.lyrics.txt" next to the mp3, if present (plain lyrics, no timing)
    ///       APIC = contents of "<filename>.jpg" next to the mp3, if present (front cover)
    ///   - The file's original last-write time is restored after saving, exactly
    ///     like the PHP tool (touch()).
    ///   - Writes <folderpath>/logs/yyyyMMdd.log (successes) and
    ///     <folderpath>/logs/yyyyMMdderror.log (failures).
    /// </summary>
    internal static class Program
    {
        // ==================== CONFIGURATION ====================
        // Artist is still fixed here; copyright is now supplied on the command line.
        private const string ArtistName = "Cyborg Unicorn / Primal Ninja";

        private static string _logPath;
        private static string _errorLogPath;
        private static readonly object LogLock = new object();

        private static int Main(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.WriteLine("MP3 Tag Cleaner & Updater");
                Console.WriteLine("Usage: mp3id.exe <folderpath> \"<copyright text>\"");
                Console.WriteLine();
                Console.WriteLine("Example: mp3id.exe C:\\Radio\\Uploads \"Copyright (c) 2026 Cyborg Unicorn. All rights reserved.\"");
                return 1;
            }

            string copyright = args[1];
            string folder;

            try
            {
                folder = Path.GetFullPath(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: invalid folder path - " + ex.Message);
                return 1;
            }

            if (!Directory.Exists(folder))
            {
                Console.WriteLine("Error: folder does not exist: " + folder);
                return 1;
            }

            string logsFolder = Path.Combine(folder, "logs");

            try
            {
                Directory.CreateDirectory(logsFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: cannot create logs folder " + logsFolder + " - " + ex.Message);
                return 1;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd");
            _logPath = Path.Combine(logsFolder, stamp + ".log");
            _errorLogPath = Path.Combine(logsFolder, stamp + "error.log");

            Console.WriteLine("MP3 Tag Cleaner & Updater");
            Console.WriteLine("Folder:    " + folder);
            Console.WriteLine("Artist:    " + ArtistName);
            Console.WriteLine("Copyright: " + copyright);
            Console.WriteLine("Year:      auto-set from each file's modification date");
            Console.WriteLine("Log:       " + _logPath);
            Console.WriteLine("Errors:    " + _errorLogPath);
            Console.WriteLine();

            LogSuccess("==== Run started. Folder: " + folder + " | Copyright: " + copyright + " ====");

            string[] mp3Files;

            try
            {
                // Top-level only, same scope as the PHP tool's glob("*.mp3") in its working directory.
                mp3Files = Directory.GetFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: cannot list folder - " + ex.Message);
                LogError("Cannot list folder " + folder + " - " + ex.Message);
                return 1;
            }

            if (mp3Files.Length == 0)
            {
                Console.WriteLine("No MP3 files found in: " + folder);
                LogSuccess("No MP3 files found in: " + folder);
                return 0;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (string filePath in mp3Files)
            {
                string fileName = Path.GetFileName(filePath);

                try
                {
                    string detail = Mp3TagWriter.ProcessFile(filePath, ArtistName, copyright);
                    successCount++;
                    LogSuccess(fileName + " - " + detail);
                    Console.WriteLine("[OK]   " + fileName);
                }
                catch (Exception ex)
                {
                    failCount++;
                    LogError(fileName + " - " + ex.Message);
                    Console.WriteLine("[FAIL] " + fileName + " - " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Complete: " + successCount + " updated, " + failCount + " failed");
            LogSuccess("==== Run complete: " + successCount + " updated, " + failCount + " failed ====");

            return failCount > 0 ? 2 : 0;
        }

        // -------------------------------------------------------------------
        // Logging: <logs>/yyyyMMdd.log (success) and <logs>/yyyyMMddderror.log (failures)
        // -------------------------------------------------------------------

        private static void LogSuccess(string message)
        {
            WriteLog(_logPath, message);
        }

        private static void LogError(string message)
        {
            WriteLog(_errorLogPath, message);
        }

        private static void WriteLog(string path, string message)
        {
            lock (LogLock)
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;

                try
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                catch
                {
                    // Logging must never crash the run; fall back to console only.
                    Console.WriteLine("(log write failed) " + message);
                }
            }
        }
    }
}