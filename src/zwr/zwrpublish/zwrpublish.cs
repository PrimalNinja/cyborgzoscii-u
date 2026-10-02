// Cyborg ZOSCII Web Radio Publish - zwrpublish v20261003
// (c) 2026 Cyborg Unicorn Pty Ltd.
// UNINTELLIGENCE License.
// ZOSCII core logic remains under MIT License.
//
// Sends a folder to a ZOSCII MQ - to a queue (publish) or to the store (store), one message
// at a time or as one transaction.
//
// Files mode (default): for each MP3 (in filename or file-datetime order, see -s below):
//   <name>.jpg or <name>.jpeg   cover image, if present
//   <name>.lyrics.txt           lyrics / text, if present
//   <name>.mp3                  the track
//
// JSON mode (-j): for each <name>.json made by zwrprepare (in file-datetime order):
//   1. stores every file it lists (mp3, image, text, lyrics) that has no store name yet,
//      and writes the store names back into the JSON (keeping the JSON's date/time)
//   2. publishes the JSON itself to <queue> (not with -store)
//
// Usage:
//   zwrpublish <folder> <mqurl> <queue>|-store [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-j] [-b] [-d]
//   zwrpublish <folder> <mqurl> <queue>|-store -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]
//   zwrpublish -c <guid> <mqurl> <queue>|-store [-r <days>]
//   zwrpublish -x <guid> <mqurl> [-store]
//
//   <queue>  publish to this queue
//   -store   store instead of publishing (files mode: the store names are added to
//            <folder>/zwrpublish-stored.csv)
//   -z  ZOSCII encode each file with <romfile>
//   -u  UNSIGNAL encode each file with <romfile>
//       (neither: files are sent as they are)
//   -r  retention in days (default 7)
//   -s<order>  order: -sf = by filename, -sd = by each file's last-write datetime
//              (files mode default -sf, JSON mode default -sd)
//   -j  JSON mode (see above)
//   -b  batch: send everything as one MQ transaction and commit it at the end. Nothing
//       appears until the commit, then all of it appears at once, in order. If a file fails
//       the transaction is rolled back and nothing is sent.
//   -t  stage into transaction <guid> without committing (new = start one); commit with -c
//   -c  commit transaction <guid> to <queue> or to the store
//   -x  roll back transaction <guid> - everything staged in it is deleted
//   -d  dry run: list what would be sent, send nothing
//
// Without a transaction, the MQ names queue messages by the second they arrive, so
// zwrpublish waits at least a second between publishes to keep their order. In a
// transaction the MQ names them at the commit, in upload order, so no wait is needed.
// Each file is retried up to 3 times. Stops at the first failure.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using CyborgUnicorn.ZOSCII;

public static class ZWRPublish
{
	internal const string VERSION = "v20261003";
	internal const int MIN_GAP_MS = 1100;     // keeps each message in a later second than the one before
	internal const int ATTEMPTS = 3;
	internal const int RETRY_WAIT_MS = 5000;

	internal const int ENCODE_NONE = 0;
	internal const int ENCODE_ZOSCII = 1;
	internal const int ENCODE_UNSIGNAL = 2;

	internal const int MODE_SEND = 0;         // one message per request (the original behaviour)
	internal const int MODE_BATCH = 1;        // -b: stage all, then commit
	internal const int MODE_STAGE = 2;        // -t: stage only
	internal const int MODE_COMMIT = 3;       // -c
	internal const int MODE_ROLLBACK = 4;     // -x

	internal static readonly string[] MEDIA_ROLES = { "mp3", "image", "text", "lyrics" };

	private static string g_strFolder = "";
	private static string g_strMQURL = "";
	private static string g_strQueue = "";
	private static bool g_blnStore = false;
	private static bool g_blnJson = false;
	private static string g_strROMFile = "";
	private static int g_intEncodeMode = ENCODE_NONE;
	private static int g_intRetentionDays = 7;
	private static string g_strSortOrder = "";   // "" = mode default, "sf" or "sd"
	private static bool g_blnDryRun = false;
	private static int g_intMode = MODE_SEND;
	private static string g_strTransaction = "";
	private static ZOSCIIRom g_objRom = null;
	private static MQClient g_objMQ = null;
	private static Stopwatch g_objLastPublish = null;

	private static readonly JsonDocumentOptions g_objReadOptions = new JsonDocumentOptions
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static readonly JsonSerializerOptions g_objWriteOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static int Main(string[] arrArgs_a)
	{
		int intResult = 1;

		Console.OutputEncoding = Encoding.UTF8;
		Console.WriteLine("ZOSCII Web Radio Publish " + VERSION);
		Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd");
		Console.WriteLine();

		if (parseArgs(arrArgs_a))
		{
			if (initialise())
			{
				bool blnOK = false;

				if (g_intMode == MODE_COMMIT)
				{
					blnOK = (commitTransaction() != null);
				}
				else if (g_intMode == MODE_ROLLBACK)
				{
					blnOK = rollbackTransaction();
				}
				else if (g_blnJson)
				{
					blnOK = sendJsonFolder();
				}
				else
				{
					blnOK = sendFolder();
				}

				intResult = blnOK ? 0 : 1;
			}
		}
		else
		{
			printUsage();
		}

		return intResult;
	}

	private static void printUsage()
	{
		Console.WriteLine("Usage: zwrpublish <folder> <mqurl> <queue>|-store [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-j] [-b] [-d]");
		Console.WriteLine("       zwrpublish <folder> <mqurl> <queue>|-store -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]");
		Console.WriteLine("       zwrpublish -c <guid> <mqurl> <queue>|-store [-r <days>]");
		Console.WriteLine("       zwrpublish -x <guid> <mqurl> [-store]");
		Console.WriteLine();
		Console.WriteLine("  Files mode: for each MP3 in <folder>, in the chosen order, sends <name>.jpg (or .jpeg),");
		Console.WriteLine("  then <name>.lyrics.txt, then <name>.mp3. The jpg and txt are optional.");
		Console.WriteLine("  JSON mode (-j): for each <name>.json from zwrprepare, stores the files it lists that");
		Console.WriteLine("  aren't stored yet, writes the store names into it, then publishes it to <queue>.");
		Console.WriteLine();
		Console.WriteLine("  <queue>  publish to this queue");
		Console.WriteLine("  -store   store instead (files mode: names go to <folder>\\zwrpublish-stored.csv)");
		Console.WriteLine("  -z  ZOSCII encode with <romfile>");
		Console.WriteLine("  -u  UNSIGNAL encode with <romfile>");
		Console.WriteLine("      (neither: send the files as they are)");
		Console.WriteLine("  -r  retention in days (default 7)");
		Console.WriteLine("  -s<order>  -sf = by filename, -sd = by file last-write datetime");
		Console.WriteLine("             (default: -sf in files mode, -sd in JSON mode)");
		Console.WriteLine("  -j  JSON mode");
		Console.WriteLine("  -b  batch: one transaction, committed at the end");
		Console.WriteLine("  -t  stage into transaction <guid> without committing (new = start one)");
		Console.WriteLine("  -c  commit transaction <guid> to <queue> or to the store");
		Console.WriteLine("  -x  roll back transaction <guid>");
		Console.WriteLine("  -d  dry run - list what would be sent, send nothing");
		Console.WriteLine();
		Console.WriteLine("Example: zwrpublish c:\\music https://example.com/radio/indexmq.php \"Cyborg Unicorn\" -z logo.png");
		Console.WriteLine("         zwrpublish c:\\music https://example.com/radio/indexmq.php \"Cyborg Unicorn\" -z logo.png -j -b");
	}

	private static bool parseArgs(string[] arrArgs_a)
	{
		bool blnResult = true;
		List<string> objPositional = new List<string>();
		int intI = 0;

		while (blnResult && intI < arrArgs_a.Length)
		{
			string strArg = arrArgs_a[intI].ToLowerInvariant();

			if ((strArg == "-z" || strArg == "-u") && intI + 1 < arrArgs_a.Length)
			{
				g_intEncodeMode = (strArg == "-z") ? ENCODE_ZOSCII : ENCODE_UNSIGNAL;
				g_strROMFile = arrArgs_a[intI + 1];
				intI += 2;
			}
			else if (strArg == "-r" && intI + 1 < arrArgs_a.Length)
			{
				blnResult = int.TryParse(arrArgs_a[intI + 1], out g_intRetentionDays) && g_intRetentionDays >= 0 && g_intRetentionDays <= 9999;
				intI += 2;
			}
			else if (strArg == "-d")
			{
				g_blnDryRun = true;
				intI++;
			}
			else if (strArg == "-store")
			{
				g_blnStore = true;
				intI++;
			}
			else if (strArg == "-j")
			{
				g_blnJson = true;
				intI++;
			}
			else if (strArg == "-b")
			{
				blnResult = setMode(MODE_BATCH, "");
				intI++;
			}
			else if ((strArg == "-t" || strArg == "-c" || strArg == "-x") && intI + 1 < arrArgs_a.Length)
			{
				string strGuid = arrArgs_a[intI + 1].Trim().ToLowerInvariant();
				int intMode = (strArg == "-t") ? MODE_STAGE : ((strArg == "-c") ? MODE_COMMIT : MODE_ROLLBACK);

				if (strArg == "-t" && strGuid == "new")
				{
					strGuid = Guid.NewGuid().ToString();
				}

				if (MQBatch.IsGUID(strGuid))
				{
					blnResult = setMode(intMode, strGuid);
				}
				else
				{
					Console.WriteLine("Not a transaction GUID: " + arrArgs_a[intI + 1]);
					blnResult = false;
				}

				intI += 2;
			}
			else if (strArg.Length > 2 && strArg.StartsWith("-s"))
			{
				string strOrder = strArg.Substring(2);

				if (strOrder == "f" || strOrder == "d")
				{
					g_strSortOrder = "s" + strOrder;
					intI++;
				}
				else
				{
					Console.WriteLine("Unknown sort order: " + arrArgs_a[intI] + " - use -sf (filename) or -sd (file datetime)");
					blnResult = false;
				}
			}
			else if (strArg.StartsWith("-"))
			{
				Console.WriteLine("Unknown or incomplete argument: " + arrArgs_a[intI]);
				blnResult = false;
			}
			else
			{
				objPositional.Add(arrArgs_a[intI]);
				intI++;
			}
		}

		if (blnResult && g_blnJson && (g_intMode == MODE_STAGE || g_intMode == MODE_COMMIT || g_intMode == MODE_ROLLBACK))
		{
			Console.WriteLine("-j works with or without -b, not with -t, -c or -x.");
			blnResult = false;
		}

		if (blnResult)
		{
			// <queue> and -store are the two destinations - exactly one, except -x needs neither
			int intDestinations = (g_blnStore ? 1 : 0);
			int intWanted = 0;

			if (g_intMode == MODE_COMMIT)
			{
				intWanted = g_blnStore ? 1 : 2;     // <mqurl> [<queue>]
			}
			else if (g_intMode == MODE_ROLLBACK)
			{
				intWanted = 1;                      // <mqurl>
			}
			else
			{
				intWanted = g_blnStore ? 2 : 3;     // <folder> <mqurl> [<queue>]
			}

			if (objPositional.Count != intWanted)
			{
				blnResult = false;
			}
			else if (g_intMode == MODE_COMMIT)
			{
				g_strMQURL = objPositional[0];
				if (!g_blnStore) { g_strQueue = objPositional[1]; intDestinations++; }
			}
			else if (g_intMode == MODE_ROLLBACK)
			{
				g_strMQURL = objPositional[0];
				intDestinations = 1;
			}
			else
			{
				g_strFolder = objPositional[0];
				g_strMQURL = objPositional[1];
				if (!g_blnStore) { g_strQueue = objPositional[2]; intDestinations++; }
			}

			if (blnResult && intDestinations != 1)
			{
				Console.WriteLine("Give either a <queue> or -store, not both.");
				blnResult = false;
			}
		}

		if (blnResult && g_strSortOrder.Length == 0)
		{
			g_strSortOrder = g_blnJson ? "sd" : "sf";
		}

		return blnResult;
	}

	// Only one of -b / -t / -c / -x can be given.
	private static bool setMode(int intMode_a, string strTransaction_a)
	{
		bool blnResult = (g_intMode == MODE_SEND);

		if (blnResult)
		{
			g_intMode = intMode_a;
			g_strTransaction = strTransaction_a;
		}
		else
		{
			Console.WriteLine("Only one of -b, -t, -c and -x can be used at a time.");
		}

		return blnResult;
	}

	private static bool initialise()
	{
		bool blnResult = true;

		if (g_strFolder.Length > 0 && !Directory.Exists(g_strFolder))
		{
			Console.WriteLine("Error: folder not found " + g_strFolder);
			blnResult = false;
		}

		if (blnResult && g_intEncodeMode != ENCODE_NONE)
		{
			g_objRom = ZOSCIIRom.FromFile(g_strROMFile);

			if (g_objRom.Size == 0)
			{
				Console.WriteLine("Error: cannot load ROM " + g_strROMFile);
				blnResult = false;
			}
		}

		g_objMQ = new MQClient(120);

		return blnResult;
	}

	private static string verb()
	{
		return g_blnStore ? "store" : "publish";
	}

	private static string destination()
	{
		return g_blnStore ? "the store" : g_strQueue;
	}

	private static void printHeader(int intCount_a, string strWhat_a)
	{
		Console.WriteLine("Folder:    " + g_strFolder + "  (" + intCount_a + " " + strWhat_a + ")");
		Console.WriteLine("MQ:        " + g_strMQURL);
		Console.WriteLine("To:        " + (g_blnJson && !g_blnStore ? "files to the store, JSONs to " + g_strQueue : destination()));
		if (g_intMode == MODE_BATCH)
		{
			Console.WriteLine("Batch:     one transaction, committed at the end");
		}
		else if (g_intMode == MODE_STAGE)
		{
			Console.WriteLine("Stage:     transaction " + g_strTransaction + " (not committed)");
		}
		Console.WriteLine("Encode:    " + (g_intEncodeMode == ENCODE_ZOSCII ? "ZOSCII (" + g_strROMFile + ")" : (g_intEncodeMode == ENCODE_UNSIGNAL ? "UNSIGNAL (" + g_strROMFile + ")" : "none")));
		if (g_intMode != MODE_STAGE)
		{
			Console.WriteLine("Retention: " + g_intRetentionDays + " days");
		}
		Console.WriteLine("Order:     " + (g_strSortOrder == "sd" ? "-sd (file datetime)" : "-sf (filename)"));
		if (g_blnDryRun)
		{
			Console.WriteLine("DRY RUN - nothing will be sent");
		}
		Console.WriteLine();
	}

	private static void sortFiles(List<string> objFiles_a)
	{
		if (g_strSortOrder == "sd")
		{
			objFiles_a.Sort(delegate (string strA, string strB)
			{
				int intCompare = File.GetLastWriteTime(strA).CompareTo(File.GetLastWriteTime(strB));

				if (intCompare == 0)
				{
					intCompare = string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase);
				}

				return intCompare;
			});
		}
		else
		{
			objFiles_a.Sort(StringComparer.OrdinalIgnoreCase);
		}
	}

	// -------------------------------------------------------------------------
	// Files mode
	// -------------------------------------------------------------------------

	private static bool sendFolder()
	{
		bool blnResult = true;
		Dictionary<string, string> objFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		List<string> objMP3s = new List<string>();
		List<string> objSent = new List<string>();      // files sent, in order (batch: matched to commit names)
		List<string> objNames = new List<string>();     // store names, single store
		int intI = 0;

		if (g_intMode == MODE_BATCH)
		{
			g_strTransaction = Guid.NewGuid().ToString();
		}

		// Index the folder by lower-case name so .JPG / .Lyrics.Txt etc. are found on any file system.
		{
			string[] arrAll = Directory.GetFiles(g_strFolder);

			for (intI = 0; intI < arrAll.Length; intI++)
			{
				string strName = Path.GetFileName(arrAll[intI]);
				objFiles[strName] = arrAll[intI];

				if (strName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
				{
					objMP3s.Add(arrAll[intI]);
				}
			}

			sortFiles(objMP3s);
		}

		printHeader(objMP3s.Count, "MP3s");

		for (intI = 0; blnResult && intI < objMP3s.Count; intI++)
		{
			string strBase = Path.GetFileNameWithoutExtension(objMP3s[intI]);
			string strJpg = findFile(objFiles, strBase, ".jpg");
			string strTxt = findFile(objFiles, strBase, ".lyrics.txt");
			List<string> objTrack = new List<string>();
			int intJ = 0;

			if (strJpg.Length == 0)
			{
				strJpg = findFile(objFiles, strBase, ".jpeg");
			}

			if (strJpg.Length > 0) { objTrack.Add(strJpg); }
			if (strTxt.Length > 0) { objTrack.Add(strTxt); }
			objTrack.Add(objMP3s[intI]);

			Console.WriteLine("[" + (intI + 1) + "/" + objMP3s.Count + "] " + strBase);

			for (intJ = 0; blnResult && intJ < objTrack.Count; intJ++)
			{
				byte[] arrPayload = readEncoded(objTrack[intJ]);
				string strName = null;

				blnResult = (arrPayload != null);

				if (blnResult)
				{
					strName = sendPayload(Path.GetFileName(objTrack[intJ]), arrPayload);
					blnResult = (strName != null);
				}

				if (blnResult)
				{
					objSent.Add(objTrack[intJ]);
					objNames.Add(strName);
				}
			}
		}

		Console.WriteLine();
		blnResult = finishRun(blnResult, objSent.Count, objSent, objNames);

		return blnResult;
	}

	// After all files were sent (or one failed): commit / roll back a batch, record store names.
	private static bool finishRun(bool blnSent_a, int intCount_a, List<string> objSent_a, List<string> objNames_a)
	{
		bool blnResult = blnSent_a;

		if (g_intMode == MODE_SEND)
		{
			if (blnSent_a)
			{
				Console.WriteLine((g_blnDryRun ? "Would send " : "Sent ") + intCount_a + " message(s) to " + destination() + ".");
			}
			else
			{
				Console.WriteLine("STOPPED after " + intCount_a + " message(s). Everything listed above the failure was sent.");
			}

			if (g_blnStore && !g_blnDryRun)
			{
				recordStored(objSent_a, objNames_a);
			}
		}
		else if (g_intMode == MODE_STAGE)
		{
			if (blnSent_a)
			{
				Console.WriteLine((g_blnDryRun ? "Would stage " : "Staged ") + intCount_a + " message(s) in transaction " + g_strTransaction + ".");
				Console.WriteLine("Nothing is sent yet. To commit:  zwrpublish -c " + g_strTransaction + " " + g_strMQURL + " " + (g_blnStore ? "-store" : "<queue>") + " -r <days>");
			}
			else
			{
				Console.WriteLine("STOPPED after " + intCount_a + " staged message(s) in transaction " + g_strTransaction + ".");
				Console.WriteLine("They are still staged. Run again with -t " + g_strTransaction + " on the files that are left,");
				Console.WriteLine("or -x " + g_strTransaction + " to throw it away.");
			}
		}
		else if (blnSent_a)
		{
			List<string> objNames = null;

			Console.WriteLine((g_blnDryRun ? "Would stage " : "Staged ") + intCount_a + " message(s).");
			objNames = commitTransaction();
			blnResult = (objNames != null);

			if (blnResult && g_blnStore && !g_blnDryRun)
			{
				recordStored(objSent_a, objNames);
			}
		}
		else
		{
			// -b: a file failed - nothing has been sent, so throw the transaction away
			Console.WriteLine("STOPPED after " + intCount_a + " staged message(s). Nothing was sent.");

			if (!g_blnDryRun)
			{
				rollbackTransaction();
			}
		}

		return blnResult;
	}

	// Files mode with -store: file -> store name, appended to <folder>/zwrpublish-stored.csv
	private static void recordStored(List<string> objFiles_a, List<string> objNames_a)
	{
		string strCsv = Path.Combine(g_strFolder, "zwrpublish-stored.csv");
		StringBuilder objSB = new StringBuilder();
		int intI = 0;

		try
		{
			if (!File.Exists(strCsv))
			{
				objSB.Append("Time,File,Size,StoreName\r\n");
			}

			for (intI = 0; intI < objFiles_a.Count && intI < objNames_a.Count; intI++)
			{
				objSB.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ",\"" + Path.GetFileName(objFiles_a[intI]).Replace("\"", "\"\"") + "\"," +
					new FileInfo(objFiles_a[intI]).Length + "," + objNames_a[intI] + "\r\n");
			}

			File.AppendAllText(strCsv, objSB.ToString(), new UTF8Encoding(true));
			Console.WriteLine("Store names written to " + strCsv);
		}
		catch (Exception objEx)
		{
			Console.WriteLine("WARNING: cannot write " + strCsv + " - " + objEx.Message);
		}
	}

	private static string findFile(Dictionary<string, string> objFiles_a, string strBase_a, string strExt_a)
	{
		string strResult = "";
		string strPath = "";

		if (objFiles_a.TryGetValue(strBase_a + strExt_a, out strPath))
		{
			strResult = strPath;
		}

		return strResult;
	}

	private static byte[] readEncoded(string strPath_a)
	{
		byte[] arrResult = null;

		try
		{
			arrResult = encode(File.ReadAllBytes(strPath_a));

			if (arrResult == null)
			{
				Console.WriteLine("    FAILED  " + Path.GetFileName(strPath_a) + " - encoding failed");
			}
		}
		catch (Exception objEx)
		{
			Console.WriteLine("    FAILED  " + Path.GetFileName(strPath_a) + " - cannot read: " + objEx.Message);
		}

		return arrResult;
	}

	// Sends one payload the way the mode says: publish, store, or stage in the transaction.
	// Returns the store name (store), "" (publish / stage / dry run), or null on failure.
	private static string sendPayload(string strLabel_a, byte[] arrPayload_a)
	{
		return sendPayloadAs(strLabel_a, arrPayload_a, g_blnStore, g_strQueue, g_strTransaction, g_intMode);
	}

	private static string sendPayloadAs(string strLabel_a, byte[] arrPayload_a, bool blnStore_a, string strQueue_a, string strTransaction_a, int intMode_a)
	{
		string strResult = null;
		string strVerb = blnStore_a ? "store" : "publish";
		string strDone = (intMode_a == MODE_SEND) ? (blnStore_a ? "stored" : "published") : "staged";
		string strError = "";
		string strNonce = Guid.NewGuid().ToString();
		int intAttempt = 0;

		if (g_blnDryRun)
		{
			Console.WriteLine("    would " + ((intMode_a == MODE_SEND) ? strVerb : "stage") + "  " + strLabel_a + "  (" + arrPayload_a.Length + " bytes on the wire)");
			strResult = "";
		}
		else
		{
			while (strResult == null && intAttempt < ATTEMPTS)
			{
				intAttempt++;

				if (intMode_a != MODE_SEND)
				{
					// staged - no wait needed, the MQ names everything at the commit, in upload order
					MQAnswer objAnswer = MQBatch.Stage(g_strMQURL, strVerb, strTransaction_a, arrPayload_a, strNonce);
					if (objAnswer.Success) { strResult = ""; } else { strError = objAnswer.Describe(); }
				}
				else if (blnStore_a)
				{
					// no nonce: a retried store whose first reply was lost must still get its name back
					MQAnswer objAnswer = MQBatch.Store(g_strMQURL, arrPayload_a, g_intRetentionDays);
					if (objAnswer.Success && objAnswer.Names.Count == 1) { strResult = objAnswer.Names[0]; } else { strError = objAnswer.Describe(); }
				}
				else
				{
					MQPublishResult objPub = null;

					waitForNextSecond();
					objPub = g_objMQ.Publish(g_strMQURL, strQueue_a, arrPayload_a, strNonce, g_intRetentionDays);
					g_objLastPublish = Stopwatch.StartNew();

					if (objPub.Success) { strResult = ""; } else { strError = (objPub.ErrorMessage != null && objPub.ErrorMessage.Length > 0) ? objPub.ErrorMessage : "no response from server"; }
				}

				if (strResult == null && intAttempt < ATTEMPTS)
				{
					Console.WriteLine("    retry   " + strLabel_a + " - " + strError);
					Thread.Sleep(RETRY_WAIT_MS);
				}
			}

			if (strResult != null)
			{
				Console.WriteLine("    " + strDone + "  " + strLabel_a + "  (" + arrPayload_a.Length + " bytes on the wire)" + (strResult.Length > 0 ? "  -> " + strResult : ""));
			}
			else
			{
				Console.WriteLine("    FAILED  " + strLabel_a + " - " + strError);
			}
		}

		return strResult;
	}

	private static byte[] encode(byte[] arrData_a)
	{
		byte[] arrResult = arrData_a;

		if (g_intEncodeMode == ENCODE_ZOSCII)
		{
			arrResult = ZEncode.Bytes(arrData_a, g_objRom);
		}
		else if (g_intEncodeMode == ENCODE_UNSIGNAL)
		{
			arrResult = UEncode.Bytes(arrData_a, g_objRom);
		}

		return arrResult;
	}

	// The MQ timestamps messages to the second; a gap of over a second after the previous
	// publish finished guarantees this one lands in a later second, so queue order = publish order.
	private static void waitForNextSecond()
	{
		if (g_objLastPublish != null)
		{
			long lngWait = MIN_GAP_MS - g_objLastPublish.ElapsedMilliseconds;

			if (lngWait > 0)
			{
				Thread.Sleep((int)lngWait);
			}
		}
	}

	// -------------------------------------------------------------------------
	// JSON mode
	// -------------------------------------------------------------------------

	private class TrackJson
	{
		public string Path = "";
		public JsonObject Root = null;
		public DateTime Created;
		public DateTime Modified;
	}

	private class Asset
	{
		public TrackJson Track = null;
		public string Role = "";
		public string FilePath = "";
		public byte[] Payload = null;
	}

	private static bool sendJsonFolder()
	{
		bool blnResult = true;
		List<string> objPaths = new List<string>(Directory.GetFiles(g_strFolder, "*.json"));
		List<TrackJson> objTracks = new List<TrackJson>();
		List<Asset> objAssets = new List<Asset>();
		int intI = 0;

		sortFiles(objPaths);
		printHeader(objPaths.Count, "JSONs");

		// Load every JSON first, and check every file still to be stored, before sending anything.
		for (intI = 0; blnResult && intI < objPaths.Count; intI++)
		{
			TrackJson objTrack = loadTrack(objPaths[intI]);

			if (objTrack != null)
			{
				objTracks.Add(objTrack);
				blnResult = findAssets(objTrack, objAssets);
			}
		}

		if (blnResult)
		{
			Console.WriteLine(objTracks.Count + " track JSON(s), " + objAssets.Count + " file(s) to store.");
			Console.WriteLine();
		}

		if (blnResult && objAssets.Count > 0)
		{
			blnResult = storeAssets(objAssets);
		}

		if (blnResult && !g_blnStore)
		{
			blnResult = publishTracks(objTracks);
		}

		return blnResult;
	}

	// null = not a zwr track JSON (skipped, not an error)
	private static TrackJson loadTrack(string strPath_a)
	{
		TrackJson objResult = null;
		string strName = Path.GetFileName(strPath_a);

		try
		{
			JsonObject objRoot = JsonNode.Parse(File.ReadAllText(strPath_a), null, g_objReadOptions) as JsonObject;

			if (objRoot != null && getString(objRoot, "class") == "zwr" && getString(objRoot, "type") == "track")
			{
				objResult = new TrackJson();
				objResult.Path = strPath_a;
				objResult.Root = objRoot;
				objResult.Created = File.GetCreationTime(strPath_a);
				objResult.Modified = File.GetLastWriteTime(strPath_a);
			}
			else
			{
				Console.WriteLine("    skipped  " + strName + " - not a zwr track JSON");
			}
		}
		catch (Exception objEx)
		{
			Console.WriteLine("    skipped  " + strName + " - not valid JSON: " + objEx.Message);
		}

		return objResult;
	}

	// Every media entry with a file and no store name yet. The file must still match the size
	// and hash zwrprepare recorded, or the JSON describes a different file.
	private static bool findAssets(TrackJson objTrack_a, List<Asset> objAssets_a)
	{
		bool blnResult = true;
		int intI = 0;

		for (intI = 0; blnResult && intI < MEDIA_ROLES.Length; intI++)
		{
			JsonObject objEntry = objTrack_a.Root[MEDIA_ROLES[intI]] as JsonObject;

			if (objEntry != null && getString(objEntry, "name").Length == 0 && getString(objEntry, "file").Length > 0)
			{
				string strFile = Path.Combine(Path.GetDirectoryName(objTrack_a.Path) ?? "", Path.GetFileName(getString(objEntry, "file")));
				string strProblem = "";
				byte[] arrData = null;

				try
				{
					arrData = File.ReadAllBytes(strFile);
				}
				catch (Exception objEx)
				{
					strProblem = "cannot read " + Path.GetFileName(strFile) + ": " + objEx.Message;
				}

				if (strProblem.Length == 0 && getLong(objEntry, "size") >= 0 && getLong(objEntry, "size") != arrData.Length)
				{
					strProblem = Path.GetFileName(strFile) + " has changed size since zwrprepare - run zwrprepare again";
				}

				if (strProblem.Length == 0 && getString(objEntry, "hash").Length > 0 && getString(objEntry, "hash") != toHex(ZRollingHash.Bytes(arrData, true)))
				{
					strProblem = Path.GetFileName(strFile) + " has changed since zwrprepare - run zwrprepare again";
				}

				if (strProblem.Length > 0)
				{
					Console.WriteLine("    FAILED  " + Path.GetFileName(objTrack_a.Path) + " - " + strProblem);
					blnResult = false;
				}
				else
				{
					Asset objAsset = new Asset();
					objAsset.Track = objTrack_a;
					objAsset.Role = MEDIA_ROLES[intI];
					objAsset.FilePath = strFile;
					objAsset.Payload = encode(arrData);
					objAssets_a.Add(objAsset);
				}
			}
		}

		return blnResult;
	}

	// Step 1: the files go to the store - singly, each name saved into its JSON straight away,
	// or (-b) as one transaction whose commit returns all the names in upload order.
	private static bool storeAssets(List<Asset> objAssets_a)
	{
		bool blnResult = true;
		string strTransaction = (g_intMode == MODE_BATCH) ? Guid.NewGuid().ToString() : "";
		int intI = 0;

		Console.WriteLine("Storing files" + (strTransaction.Length > 0 ? " (transaction " + strTransaction + ")" : "") + ":");

		for (intI = 0; blnResult && intI < objAssets_a.Count; intI++)
		{
			Asset objAsset = objAssets_a[intI];
			string strName = sendPayloadAs(Path.GetFileName(objAsset.FilePath), objAsset.Payload, true, "", strTransaction, g_intMode);

			blnResult = (strName != null);

			if (blnResult && g_intMode == MODE_SEND && !g_blnDryRun)
			{
				blnResult = setStoreName(objAsset, strName);
			}
		}

		if (strTransaction.Length > 0 && !g_blnDryRun)
		{
			if (blnResult)
			{
				List<string> objNames = commitAs(strTransaction, true, "");

				blnResult = (objNames != null && objNames.Count == objAssets_a.Count);

				if (objNames != null && !blnResult)
				{
					Console.WriteLine("    FAILED  the commit returned " + objNames.Count + " name(s) for " + objAssets_a.Count + " file(s)");
				}

				for (intI = 0; blnResult && intI < objAssets_a.Count; intI++)
				{
					blnResult = setStoreName(objAssets_a[intI], objNames[intI]);
				}
			}
			else
			{
				Console.WriteLine("A file failed - nothing was stored.");
				rollbackAs(strTransaction, true);
			}
		}

		Console.WriteLine();

		return blnResult;
	}

	private static bool setStoreName(Asset objAsset_a, string strName_a)
	{
		JsonObject objEntry = objAsset_a.Track.Root[objAsset_a.Role] as JsonObject;

		objEntry["name"] = strName_a;

		return saveTrack(objAsset_a.Track);
	}

	// Rewritten in zwrprepare's layout, keeping the JSON's own created / modified date-time,
	// which sets its place in the publish order.
	private static bool saveTrack(TrackJson objTrack_a)
	{
		bool blnResult = false;

		try
		{
			File.WriteAllText(objTrack_a.Path, objTrack_a.Root.ToJsonString(g_objWriteOptions) + Environment.NewLine, new UTF8Encoding(false));
			File.SetCreationTime(objTrack_a.Path, objTrack_a.Created);
			File.SetLastWriteTime(objTrack_a.Path, objTrack_a.Modified);
			blnResult = true;
		}
		catch (Exception objEx)
		{
			Console.WriteLine("    FAILED  cannot save " + Path.GetFileName(objTrack_a.Path) + " - " + objEx.Message);
		}

		return blnResult;
	}

	// Step 2: the JSONs go to the queue, singly or (-b) as one transaction.
	private static bool publishTracks(List<TrackJson> objTracks_a)
	{
		bool blnResult = true;
		string strTransaction = (g_intMode == MODE_BATCH) ? Guid.NewGuid().ToString() : "";
		int intI = 0;

		Console.WriteLine("Publishing JSONs to " + g_strQueue + (strTransaction.Length > 0 ? " (transaction " + strTransaction + ")" : "") + ":");

		for (intI = 0; blnResult && intI < objTracks_a.Count; intI++)
		{
			string strMissing = missingStoreNames(objTracks_a[intI]);

			if (strMissing.Length > 0 && !g_blnDryRun)
			{
				Console.WriteLine("    FAILED  " + Path.GetFileName(objTracks_a[intI].Path) + " - not stored yet: " + strMissing);
				blnResult = false;
			}
			else
			{
				byte[] arrPayload = readEncoded(objTracks_a[intI].Path);

				blnResult = (arrPayload != null);

				if (blnResult)
				{
					blnResult = (sendPayloadAs(Path.GetFileName(objTracks_a[intI].Path), arrPayload, false, g_strQueue, strTransaction, g_intMode) != null);
				}
			}
		}

		if (strTransaction.Length > 0 && !g_blnDryRun)
		{
			if (blnResult)
			{
				blnResult = (commitAs(strTransaction, false, g_strQueue) != null);
			}
			else
			{
				Console.WriteLine("A JSON failed - none were published (the files already stored stay stored).");
				rollbackAs(strTransaction, false);
			}
		}

		Console.WriteLine();
		Console.WriteLine(blnResult ? (g_blnDryRun ? "Dry run done." : "Done.") : "STOPPED.");

		return blnResult;
	}

	private static string missingStoreNames(TrackJson objTrack_a)
	{
		List<string> objMissing = new List<string>();
		int intI = 0;

		for (intI = 0; intI < MEDIA_ROLES.Length; intI++)
		{
			JsonObject objEntry = objTrack_a.Root[MEDIA_ROLES[intI]] as JsonObject;

			if (objEntry != null && getString(objEntry, "name").Length == 0)
			{
				objMissing.Add(MEDIA_ROLES[intI]);
			}
		}

		if (objTrack_a.Root["mp3"] as JsonObject == null)
		{
			objMissing.Add("mp3 (no entry)");
		}

		return string.Join(", ", objMissing);
	}

	private static string getString(JsonObject objFrom_a, string strKey_a)
	{
		string strResult = "";
		JsonValue objValue = (objFrom_a != null && objFrom_a.ContainsKey(strKey_a)) ? objFrom_a[strKey_a] as JsonValue : null;
		string strValue = null;

		if (objValue != null && objValue.TryGetValue<string>(out strValue) && strValue != null)
		{
			strResult = strValue;
		}

		return strResult;
	}

	private static long getLong(JsonObject objFrom_a, string strKey_a)
	{
		long lngResult = -1;
		JsonValue objValue = (objFrom_a != null && objFrom_a.ContainsKey(strKey_a)) ? objFrom_a[strKey_a] as JsonValue : null;
		long lngValue = 0;

		if (objValue != null && objValue.TryGetValue<long>(out lngValue))
		{
			lngResult = lngValue;
		}

		return lngResult;
	}

	private static string toHex(byte[] arrData_a)
	{
		return (arrData_a != null) ? Convert.ToHexString(arrData_a).ToLowerInvariant() : "";
	}

	// -------------------------------------------------------------------------
	// Transactions
	// -------------------------------------------------------------------------

	private static List<string> commitTransaction()
	{
		return commitAs(g_strTransaction, g_blnStore, g_strQueue);
	}

	private static bool rollbackTransaction()
	{
		return rollbackAs(g_strTransaction, g_blnStore);
	}

	// Commit is safe to send again: a commit that stopped part way carries on, and one that
	// already finished answers with the same names. So it is simply retried.
	// Returns the names in upload order, or null if it failed.
	private static List<string> commitAs(string strTransaction_a, bool blnStore_a, string strQueue_a)
	{
		List<string> objResult = null;
		string strTo = blnStore_a ? "the store" : strQueue_a;
		string strError = "";
		int intAttempt = 0;

		if (g_blnDryRun)
		{
			Console.WriteLine("Would commit transaction " + strTransaction_a + " to " + strTo + " (" + g_intRetentionDays + " days).");
			objResult = new List<string>();
		}
		else
		{
			Console.WriteLine("Committing transaction " + strTransaction_a + " to " + strTo + " ...");

			while (objResult == null && intAttempt < ATTEMPTS)
			{
				MQAnswer objAnswer = null;

				intAttempt++;
				objAnswer = MQBatch.Commit(g_strMQURL, blnStore_a ? "store" : "publish", strTransaction_a, strQueue_a, g_intRetentionDays);

				if (objAnswer.Success)
				{
					objResult = objAnswer.Names;
					Console.WriteLine("Committed: " + objResult.Count + " message(s) to " + strTo + (objAnswer.Message.Length > 0 ? " (" + objAnswer.Message + ")" : "") + ".");
				}
				else
				{
					strError = objAnswer.Describe();

					if (intAttempt < ATTEMPTS)
					{
						Console.WriteLine("    retry   commit - " + strError);
						Thread.Sleep(RETRY_WAIT_MS);
					}
				}
			}

			if (objResult == null)
			{
				Console.WriteLine("Commit FAILED - " + strError);
				Console.WriteLine("Everything is still staged. Send the commit again with:");
				Console.WriteLine("  zwrpublish -c " + strTransaction_a + " " + g_strMQURL + " " + (blnStore_a ? "-store" : "\"" + strQueue_a + "\"") + " -r " + g_intRetentionDays);
			}
		}

		return objResult;
	}

	private static bool rollbackAs(string strTransaction_a, bool blnStore_a)
	{
		bool blnResult = false;
		MQAnswer objAnswer = null;

		if (g_blnDryRun)
		{
			Console.WriteLine("Would roll back transaction " + strTransaction_a + ".");
			blnResult = true;
		}
		else
		{
			objAnswer = MQBatch.Rollback(g_strMQURL, blnStore_a ? "store" : "publish", strTransaction_a);

			if (objAnswer.Success)
			{
				Console.WriteLine("Transaction " + strTransaction_a + " rolled back - everything staged in it was deleted.");
				blnResult = true;
			}
			else
			{
				Console.WriteLine("Roll back of transaction " + strTransaction_a + " FAILED - " + objAnswer.Describe());
			}
		}

		return blnResult;
	}
}

// -----------------------------------------------------------------------------
// MQ calls for store and transactions (index.php v20261003 or later):
//   action=store&msg=...                                single store, returns its name
//   action=publish|store&t=<guid>&msg=...               stage
//   action=publish&t=<guid>&q=...&r=...&commit=true     commit to a queue
//   action=store&t=<guid>&r=...&commit=true             commit to the store
//   action=publish|store&t=<guid>&commit=false          roll back
// Requests are multipart POSTs, the same as MQClient.Publish.
// -----------------------------------------------------------------------------

internal class MQAnswer
{
	public bool Answered = false;     // a ZOSCII MQ JSON reply was received
	public string Error = "";
	public string Message = "";
	public string Problem = "";       // no reply / not an MQ reply - why
	public List<string> Names = new List<string>();

	public bool Success
	{
		get { return Answered && Error.Length == 0; }
	}

	public string Describe()
	{
		string strResult = Error;

		if (strResult.Length == 0)
		{
			strResult = (Problem.Length > 0) ? Problem : "no response from server";
		}

		return strResult;
	}
}

internal static class MQBatch
{
	private const int SEND_TIMEOUT_SECONDS = 120;
	private const int COMMIT_TIMEOUT_SECONDS = 600;

	private static readonly HttpClient g_objClient = createClient();

	private static HttpClient createClient()
	{
		HttpClient objResult = new HttpClient();
		objResult.Timeout = Timeout.InfiniteTimeSpan;   // per request instead, see post()
		return objResult;
	}

	public static bool IsGUID(string strText_a)
	{
		return Regex.IsMatch(strText_a, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
	}

	public static MQAnswer Store(string strURL_a, byte[] arrData_a, int intRetentionDays_a)
	{
		MultipartFormDataContent objForm = newForm("store", "");

		objForm.Add(new StringContent(intRetentionDays_a.ToString("D4")), "r");
		addMessage(objForm, arrData_a);

		return post(strURL_a, objForm, SEND_TIMEOUT_SECONDS);
	}

	public static MQAnswer Stage(string strURL_a, string strVerb_a, string strTransaction_a, byte[] arrData_a, string strNonce_a)
	{
		MultipartFormDataContent objForm = newForm(strVerb_a, strTransaction_a);

		if (strNonce_a.Length > 0)
		{
			objForm.Add(new StringContent(strNonce_a), "n");
		}

		addMessage(objForm, arrData_a);

		return post(strURL_a, objForm, SEND_TIMEOUT_SECONDS);
	}

	public static MQAnswer Commit(string strURL_a, string strVerb_a, string strTransaction_a, string strQueue_a, int intRetentionDays_a)
	{
		MultipartFormDataContent objForm = newForm(strVerb_a, strTransaction_a);

		if (strVerb_a == "publish")
		{
			objForm.Add(new StringContent(strQueue_a), "q");
		}

		objForm.Add(new StringContent(intRetentionDays_a.ToString("D4")), "r");
		objForm.Add(new StringContent("true"), "commit");

		return post(strURL_a, objForm, COMMIT_TIMEOUT_SECONDS);
	}

	public static MQAnswer Rollback(string strURL_a, string strVerb_a, string strTransaction_a)
	{
		MultipartFormDataContent objForm = newForm(strVerb_a, strTransaction_a);

		objForm.Add(new StringContent("false"), "commit");

		return post(strURL_a, objForm, SEND_TIMEOUT_SECONDS);
	}

	private static MultipartFormDataContent newForm(string strAction_a, string strTransaction_a)
	{
		MultipartFormDataContent objResult = new MultipartFormDataContent("----MQBoundary" + Guid.NewGuid().ToString("N"));

		objResult.Add(new StringContent(strAction_a), "action");

		if (strTransaction_a.Length > 0)
		{
			objResult.Add(new StringContent(strTransaction_a), "t");
		}

		return objResult;
	}

	private static void addMessage(MultipartFormDataContent objForm_a, byte[] arrData_a)
	{
		ByteArrayContent objFile = new ByteArrayContent(arrData_a);

		objFile.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		objForm_a.Add(objFile, "msg", "msg.bin");
	}

	private static MQAnswer post(string strURL_a, MultipartFormDataContent objForm_a, int intTimeoutSeconds_a)
	{
		MQAnswer objResult = new MQAnswer();

		try
		{
			using (CancellationTokenSource objCancel = new CancellationTokenSource(TimeSpan.FromSeconds(intTimeoutSeconds_a)))
			using (HttpRequestMessage objRequest = new HttpRequestMessage(HttpMethod.Post, strURL_a))
			{
				objRequest.Headers.TryAddWithoutValidation("User-Agent", Guid.NewGuid().ToString());
				objRequest.Content = objForm_a;

				using (HttpResponseMessage objResponse = g_objClient.SendAsync(objRequest, objCancel.Token).GetAwaiter().GetResult())
				{
					string strBody = objResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

					if (!objResponse.IsSuccessStatusCode)
					{
						objResult.Problem = "HTTP " + (int)objResponse.StatusCode + " " + objResponse.ReasonPhrase;
					}
					else
					{
						parse(strBody, objResult);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			objResult.Problem = "no complete reply within " + intTimeoutSeconds_a + " seconds";
		}
		catch (Exception objEx)
		{
			objResult.Problem = objEx.Message;
		}
		finally
		{
			objForm_a.Dispose();
		}

		return objResult;
	}

	private static void parse(string strBody_a, MQAnswer objAnswer_a)
	{
		try
		{
			using (JsonDocument objDoc = JsonDocument.Parse(strBody_a))
			{
				JsonElement objRoot = objDoc.RootElement;
				JsonElement objValue;

				if (objRoot.ValueKind == JsonValueKind.Object && objRoot.TryGetProperty("system", out objValue) && objValue.GetString() == "ZOSCII MQ")
				{
					objAnswer_a.Answered = true;

					if (objRoot.TryGetProperty("error", out objValue) && objValue.ValueKind == JsonValueKind.String)
					{
						objAnswer_a.Error = objValue.GetString();
					}

					if (objRoot.TryGetProperty("message", out objValue) && objValue.ValueKind == JsonValueKind.String)
					{
						objAnswer_a.Message = objValue.GetString();
					}

					if (objRoot.TryGetProperty("result", out objValue))
					{
						if (objValue.ValueKind == JsonValueKind.Array)
						{
							foreach (JsonElement objName in objValue.EnumerateArray())
							{
								if (objName.ValueKind == JsonValueKind.String)
								{
									objAnswer_a.Names.Add(objName.GetString());
								}
							}
						}
						else if (objValue.ValueKind == JsonValueKind.String)
						{
							objAnswer_a.Names.Add(objValue.GetString());
						}
					}
				}
				else
				{
					objAnswer_a.Problem = "reply is not from ZOSCII MQ";
				}
			}
		}
		catch
		{
			// a switched-off action answers nothing; anything else that isn't JSON is not an MQ reply
			objAnswer_a.Problem = (strBody_a.Length == 0) ? "empty reply - is that action allowed on the server?" : "unexpected reply: " + (strBody_a.Length > 100 ? strBody_a.Substring(0, 100) : strBody_a);
		}
	}
}