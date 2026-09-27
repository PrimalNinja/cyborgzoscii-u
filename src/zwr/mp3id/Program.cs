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
    ///   mp3id.exe <folderpath> "<copyright text>" [-a "<artist>"] [-l <logofile>] [-s<order>]
    ///
    /// Behaviour:
    ///   - Processes every *.mp3 file directly inside <folderpath> (not subfolders),
    ///     with no skipping/filtering other than the basic MP3 validity check.
    ///   - For each file: strips ALL existing ID3 tags and writes a fresh minimal
    ///     ID3v2.3 tag containing:
    ///       TIT2 = title, the filename verbatim (minus .mp3)
    ///       TPE1 = artist, from -a "<artist>" if given; otherwise the existing TPE1 value
    ///              already in the file (if any) is read first and carried over unchanged,
    ///              so the artist is left as it is; if there's no -a and no existing artist
    ///              tag, no TPE1 frame is written at all
    ///       TYER / TDRC = year, taken from the file's last-modified date
    ///       TCOP = copyright, from the second command-line argument
    ///       COMM = contents of "<filename>.txt" next to the mp3, if present
    ///       USLT = contents of "<filename>.lyrics.txt" next to the mp3, if present (plain lyrics, no timing)
    ///       APIC = station logo, if -l <logofile> was given (same image for every file,
    ///              <filename>.jpg is never even checked in that case); otherwise the
    ///              contents of "<filename>.jpg" next to the mp3, if present (front cover)
    ///   - Files are processed in the order set by -s<order> (default -sf):
    ///       -sf = sort by filename (default)
    ///       -sd = sort by each file's filesystem last-write datetime
    ///     Sorting only changes the order files are processed/logged in - the per-file
    ///     .jpg/.txt/.lyrics.txt matching rules are unchanged either way.
    ///   - The file's original last-write time is restored after saving, exactly
    ///     like the PHP tool (touch()).
    ///   - Writes <folderpath>/logs/yyyyMMdd.log (successes) and
    ///     <folderpath>/logs/yyyyMMdderror.log (failures).
    /// </summary>
    internal static class Program
    {
        private static string _logPath;
        private static string _errorLogPath;
        private static readonly object LogLock = new object();

        private static int Main(string[] args)
        {
            string artistName = null;
            string sortOrder = "sf"; // default: sort by filename

            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                PrintUsage();
                return 1;
            }

            string copyright = args[1];
            string folder;
            string logoPath = null;

            // Anything after the two required positional args: -a <artist> (optional), -l <logofile>, -s<order>.
            for (int i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-a", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        Console.WriteLine("Error: -a requires an artist name, e.g. -a \"DJ Someone\"");
                        return 1;
                    }

                    artistName = args[i + 1];
                    i++;
                }
                else if (string.Equals(args[i], "-l", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        Console.WriteLine("Error: -l requires a filename, e.g. -l C:\\Radio\\logo.jpg");
                        return 1;
                    }

                    logoPath = args[i + 1];
                    i++;
                }
                else if (args[i].Length > 2 && args[i].StartsWith("-s", StringComparison.OrdinalIgnoreCase))
                {
                    string order = args[i].Substring(2).ToLowerInvariant();

                    if (order != "f" && order != "d")
                    {
                        Console.WriteLine("Error: unrecognised sort order '" + args[i] + "' - use -sf (filename) or -sd (file datetime)");
                        return 1;
                    }

                    sortOrder = "s" + order;
                }
                else
                {
                    Console.WriteLine("Error: unrecognised argument: " + args[i]);
                    return 1;
                }
            }

            byte[] logoBytes = null;
            string logoName = null;

            if (logoPath != null)
            {
                try
                {
                    logoPath = Path.GetFullPath(logoPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: invalid -l path - " + ex.Message);
                    return 1;
                }

                if (!File.Exists(logoPath))
                {
                    Console.WriteLine("Error: -l file does not exist: " + logoPath);
                    return 1;
                }

                try
                {
                    logoBytes = File.ReadAllBytes(logoPath);
                    logoName = Path.GetFileName(logoPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: cannot read -l file " + logoPath + " - " + ex.Message);
                    return 1;
                }

                // Not a strict validation, just a sanity check: JPEG files start with FF D8.
                if (logoBytes.Length < 2 || logoBytes[0] != 0xFF || logoBytes[1] != 0xD8)
                {
                    Console.WriteLine("Error: -l file does not look like a JPEG (no FF D8 signature): " + logoPath);
                    return 1;
                }
            }

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
            Console.WriteLine("Artist:    " + (artistName ?? "(not given - existing artist tag, if any, will be preserved unchanged)"));
            Console.WriteLine("Copyright: " + copyright);
            Console.WriteLine("Year:      auto-set from each file's modification date");
            Console.WriteLine("Logo:      " + (logoPath != null ? logoPath + " (used for every file, " + logoBytes.Length + " bytes)" : "none - per-file <filename>.jpg is used if present"));
            Console.WriteLine("Order:     " + (sortOrder == "sd" ? "-sd (file datetime)" : "-sf (filename, default)"));
            Console.WriteLine("Log:       " + _logPath);
            Console.WriteLine("Errors:    " + _errorLogPath);
            Console.WriteLine();

            LogSuccess("==== Run started. Folder: " + folder + " | Artist: " + (artistName ?? "(not given - existing tag preserved if present)") + " | Copyright: " + copyright + (logoPath != null ? " | Logo: " + logoPath : "") + " | Order: " + sortOrder + " ====");

            string[] mp3Files;

            try
            {
                // Top-level only, same scope as the PHP tool's glob("*.mp3") in its working directory.
                // Read every filename (and, for -sd, its filesystem last-write datetime) up front,
                // then sort the whole batch before processing starts - the sort order only changes
                // the order files are handled/logged in; the per-file .jpg/.txt/.lyrics.txt rules
                // in Mp3TagWriter are unaffected either way.
                string[] rawFiles = Directory.GetFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly);

                if (sortOrder == "sd")
                {
                    mp3Files = rawFiles
                        .Select(f => new { Path = f, WriteTime = File.GetLastWriteTime(f) })
                        .OrderBy(f => f.WriteTime)
                        .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(f => f.Path)
                        .ToArray();
                }
                else
                {
                    mp3Files = rawFiles
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
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
                    string detail = Mp3TagWriter.ProcessFile(filePath, artistName, copyright, logoBytes, logoName);
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
        // Usage
        // -------------------------------------------------------------------

        private static void PrintUsage()
        {
            Console.WriteLine("MP3 Tag Cleaner & Updater");
            Console.WriteLine("Usage: mp3id.exe <folderpath> \"<copyright text>\" [-a \"<artist>\"] [-l <logofile>] [-s<order>]");
            Console.WriteLine();
            Console.WriteLine("Example: mp3id.exe C:\\Radio\\Uploads \"Copyright (c) 2026 Cyborg Unicorn. All rights reserved.\" -a \"Cyborg Unicorn\"");
            Console.WriteLine("Example: mp3id.exe C:\\Radio\\Uploads \"Copyright (c) 2026 Cyborg Unicorn.\" -l C:\\Radio\\logo.jpg -sd");
            Console.WriteLine();
            Console.WriteLine("  -a <artist>    Artist name to write to TPE1. Optional: if omitted, any artist");
            Console.WriteLine("                 already tagged on the file is read first and left unchanged;");
            Console.WriteLine("                 if the file has no existing artist tag either, none is written.");
            Console.WriteLine("  -l <logofile>  Use this .jpg as the album art for every mp3 in the folder,");
            Console.WriteLine("                 instead of looking for a matching <filename>.jpg per file.");
            Console.WriteLine("  -s<order>      Order files are processed in. -sf = by filename (default),");
            Console.WriteLine("                 -sd = by each file's filesystem last-modified datetime.");
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