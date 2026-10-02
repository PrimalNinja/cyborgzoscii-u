// Cyborg ZOSCII Web Radio Publish - zwrpublish v20261002
// (c) 2026 Cyborg Unicorn Pty Ltd.
// UNINTELLIGENCE License.
// ZOSCII core logic remains under MIT License.
//
// Publishes a folder of MP3s to a ZOSCII MQ queue. For each MP3 (in filename or file-datetime
// order, see -s below):
//   <name>.jpg or <name>.jpeg   cover image, if present
//   <name>.lyrics.txt           lyrics / text, if present
//   <name>.mp3                  the track
//
// Usage:
//   zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-b] [-d]
//   zwrpublish <folder> <mqurl> -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]
//   zwrpublish -c <guid> <mqurl> <queue> [-r <days>]
//   zwrpublish -x <guid> <mqurl>
//
//   -z  ZOSCII encode each file with <romfile>
//   -u  UNSIGNAL encode each file with <romfile>
//       (neither: files are published as they are)
//   -r  retention in days (default 7)
//   -s<order>  order to publish the MP3s in: -sf = by filename (default), -sd = by each
//              file's filesystem last-write datetime. Either way, for each MP3 the matching
//              jpg/jpeg and .lyrics.txt (if present) are still published immediately before it.
//   -b  batch: upload everything into one MQ transaction, then commit it to <queue>. Nothing
//       appears in the queue until the commit, and then all of it appears at once, in order.
//       If an upload fails the transaction is aborted and nothing is published.
//   -t  upload into transaction <guid> without committing it (new = start a new one). Run it
//       again with the same <guid> to add more; commit with -c.
//   -c  commit transaction <guid> to <queue> (retention -r applies to every file)
//   -x  abort transaction <guid> - everything uploaded into it is deleted
//   -d  dry run: list what would be published, publish nothing
//
// Without -b/-t: the MQ names messages by the second they arrive, so messages published within
// the same second can sort in any order. zwrpublish waits at least a second between publishes
// so the queue keeps jpg -> txt -> mp3 order. With -b/-t there is no wait: the MQ gives the files
// their queue names at commit, in upload order. Each file gets its own nonce and is retried with
// it, so a retry after a lost reply can't publish or upload a duplicate. Stops at the first failure.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using CyborgUnicorn.ZOSCII;

public static class ZWRPublish
{
	internal const string VERSION = "v20261002";
	internal const int MIN_GAP_MS = 1100;     // keeps each message in a later second than the one before
	internal const int ATTEMPTS = 3;
	internal const int RETRY_WAIT_MS = 5000;

	internal const int ENCODE_NONE = 0;
	internal const int ENCODE_ZOSCII = 1;
	internal const int ENCODE_UNSIGNAL = 2;

	internal const int MODE_PUBLISH = 0;      // one publish per file (the original behaviour)
	internal const int MODE_BATCH = 1;        // -b: upload all, then commit
	internal const int MODE_UPLOAD = 2;       // -t: upload only
	internal const int MODE_COMMIT = 3;       // -c
	internal const int MODE_ABORT = 4;        // -x

	private static string g_strFolder = "";
	private static string g_strMQURL = "";
	private static string g_strQueue = "";
	private static string g_strROMFile = "";
	private static int g_intEncodeMode = ENCODE_NONE;
	private static int g_intRetentionDays = 7;
	private static string g_strSortOrder = "sf";  // -sf (filename, default) or -sd (file datetime)
	private static bool g_blnDryRun = false;
	private static int g_intMode = MODE_PUBLISH;
	private static string g_strTransaction = "";
	private static ZOSCIIRom g_objRom = null;
	private static MQClient g_objMQ = null;
	private static Stopwatch g_objLastPublish = null;

	public static int Main(string[] arrArgs_a)
	{
		int intResult = 1;

		Console.WriteLine("ZOSCII Web Radio Publish " + VERSION);
		Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd");
		Console.WriteLine();

		if (parseArgs(arrArgs_a))
		{
			if (initialise())
			{
				if (g_intMode == MODE_COMMIT)
				{
					intResult = commitTransaction() ? 0 : 1;
				}
				else if (g_intMode == MODE_ABORT)
				{
					intResult = abortTransaction() ? 0 : 1;
				}
				else
				{
					intResult = publishFolder() ? 0 : 1;
				}
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
		Console.WriteLine("Usage: zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-s<order>] [-b] [-d]");
		Console.WriteLine("       zwrpublish <folder> <mqurl> -t <guid|new> [-z <romfile> | -u <romfile>] [-s<order>] [-d]");
		Console.WriteLine("       zwrpublish -c <guid> <mqurl> <queue> [-r <days>]");
		Console.WriteLine("       zwrpublish -x <guid> <mqurl>");
		Console.WriteLine();
		Console.WriteLine("  For each MP3 in <folder>, in the chosen order, publishes <name>.jpg (or .jpeg),");
		Console.WriteLine("  then <name>.lyrics.txt, then <name>.mp3. The jpg and txt are optional.");
		Console.WriteLine();
		Console.WriteLine("  -z  ZOSCII encode with <romfile>");
		Console.WriteLine("  -u  UNSIGNAL encode with <romfile>");
		Console.WriteLine("      (neither: publish the files as they are)");
		Console.WriteLine("  -r  retention in days (default 7)");
		Console.WriteLine("  -s<order>  order to publish the MP3s in:");
		Console.WriteLine("             -sf = by filename (default)");
		Console.WriteLine("             -sd = by each file's filesystem last-write datetime");
		Console.WriteLine("  -b  batch: upload everything into one transaction, then commit it to <queue>");
		Console.WriteLine("  -t  upload into transaction <guid> without committing (new = start one)");
		Console.WriteLine("  -c  commit transaction <guid> to <queue>");
		Console.WriteLine("  -x  abort transaction <guid>");
		Console.WriteLine("  -d  dry run - list what would be published, publish nothing");
		Console.WriteLine();
		Console.WriteLine("Example: zwrpublish c:\\music https://example.com/radio/indexmq.php \"Cyborg Unicorn\" -z logo.png");
		Console.WriteLine("         zwrpublish c:\\music https://example.com/radio/indexmq.php \"Cyborg Unicorn\" -z logo.png -b");
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
			else if (strArg == "-b")
			{
				blnResult = setMode(MODE_BATCH, "");
				intI++;
			}
			else if ((strArg == "-t" || strArg == "-c" || strArg == "-x") && intI + 1 < arrArgs_a.Length)
			{
				string strGuid = arrArgs_a[intI + 1].Trim().ToLowerInvariant();
				int intMode = (strArg == "-t") ? MODE_UPLOAD : ((strArg == "-c") ? MODE_COMMIT : MODE_ABORT);

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

		if (blnResult)
		{
			if ((g_intMode == MODE_PUBLISH || g_intMode == MODE_BATCH) && objPositional.Count == 3)
			{
				g_strFolder = objPositional[0];
				g_strMQURL = objPositional[1];
				g_strQueue = objPositional[2];
			}
			else if (g_intMode == MODE_UPLOAD && objPositional.Count == 2)
			{
				g_strFolder = objPositional[0];
				g_strMQURL = objPositional[1];
			}
			else if (g_intMode == MODE_COMMIT && objPositional.Count == 2)
			{
				g_strMQURL = objPositional[0];
				g_strQueue = objPositional[1];
			}
			else if (g_intMode == MODE_ABORT && objPositional.Count == 1)
			{
				g_strMQURL = objPositional[0];
			}
			else
			{
				blnResult = false;
			}
		}

		return blnResult;
	}

	// Only one of -b / -t / -c / -x can be given.
	private static bool setMode(int intMode_a, string strTransaction_a)
	{
		bool blnResult = (g_intMode == MODE_PUBLISH);

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

	// -------------------------------------------------------------------------
	// Publishing
	// -------------------------------------------------------------------------

	private static bool publishFolder()
	{
		bool blnResult = true;
		Dictionary<string, string> objFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		List<string> objMP3s = new List<string>();
		int intPublished = 0;
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

			if (g_strSortOrder == "sd")
			{
				objMP3s.Sort(delegate (string strA, string strB)
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
				objMP3s.Sort(StringComparer.OrdinalIgnoreCase);
			}
		}

		Console.WriteLine("Folder:    " + g_strFolder + "  (" + objMP3s.Count + " MP3s)");
		if (g_intMode == MODE_UPLOAD)
		{
			Console.WriteLine("Upload:    transaction " + g_strTransaction + " @ " + g_strMQURL + " (not committed)");
		}
		else
		{
			Console.WriteLine("Queue:     " + g_strQueue + " @ " + g_strMQURL);
		}
		if (g_intMode == MODE_BATCH)
		{
			Console.WriteLine("Batch:     transaction " + g_strTransaction + ", committed at the end");
		}
		Console.WriteLine("Encode:    " + (g_intEncodeMode == ENCODE_ZOSCII ? "ZOSCII (" + g_strROMFile + ")" : (g_intEncodeMode == ENCODE_UNSIGNAL ? "UNSIGNAL (" + g_strROMFile + ")" : "none")));
		if (g_intMode != MODE_UPLOAD)
		{
			Console.WriteLine("Retention: " + g_intRetentionDays + " days");
		}
		Console.WriteLine("Order:     " + (g_strSortOrder == "sd" ? "-sd (file datetime)" : "-sf (filename, default)"));
		if (g_blnDryRun)
		{
			Console.WriteLine("DRY RUN - nothing will be published");
		}
		Console.WriteLine();

		for (intI = 0; blnResult && intI < objMP3s.Count; intI++)
		{
			string strBase = Path.GetFileNameWithoutExtension(objMP3s[intI]);
			string strJpg = findFile(objFiles, strBase, ".jpg");
			string strTxt = findFile(objFiles, strBase, ".lyrics.txt");

			if (strJpg.Length == 0)
			{
				strJpg = findFile(objFiles, strBase, ".jpeg");
			}

			Console.WriteLine("[" + (intI + 1) + "/" + objMP3s.Count + "] " + strBase);

			if (blnResult && strJpg.Length > 0)
			{
				blnResult = publishFile(strJpg);
				if (blnResult) { intPublished++; }
			}

			if (blnResult && strTxt.Length > 0)
			{
				blnResult = publishFile(strTxt);
				if (blnResult) { intPublished++; }
			}

			if (blnResult)
			{
				blnResult = publishFile(objMP3s[intI]);
				if (blnResult) { intPublished++; }
			}
		}

		Console.WriteLine();

		if (g_intMode == MODE_PUBLISH)
		{
			if (blnResult)
			{
				Console.WriteLine((g_blnDryRun ? "Would publish " : "Published ") + intPublished + " message(s).");
			}
			else
			{
				Console.WriteLine("STOPPED after " + intPublished + " message(s). Everything listed above the failure was published.");
			}
		}
		else if (g_intMode == MODE_UPLOAD)
		{
			if (blnResult)
			{
				Console.WriteLine((g_blnDryRun ? "Would upload " : "Uploaded ") + intPublished + " message(s) into transaction " + g_strTransaction + ".");
				Console.WriteLine("Nothing is in a queue yet. To publish them:  zwrpublish -c " + g_strTransaction + " " + g_strMQURL + " <queue> -r <days>");
			}
			else
			{
				Console.WriteLine("STOPPED after " + intPublished + " upload(s) into transaction " + g_strTransaction + ".");
				Console.WriteLine("Those are still waiting in the transaction. Fix the problem and run again with -t " + g_strTransaction);
				Console.WriteLine("to add the rest (files already uploaded will be uploaded again), or -x " + g_strTransaction + " to throw it away.");
			}
		}
		else if (blnResult)
		{
			Console.WriteLine((g_blnDryRun ? "Would upload " : "Uploaded ") + intPublished + " message(s).");
			blnResult = commitTransaction();
		}
		else
		{
			// -b: an upload failed - nothing has reached the queue, so throw the transaction away
			Console.WriteLine("STOPPED after " + intPublished + " upload(s). Nothing was published.");

			if (!g_blnDryRun)
			{
				abortTransaction();
			}
		}

		return blnResult;
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

	private static bool publishFile(string strPath_a)
	{
		bool blnResult = false;
		string strName = Path.GetFileName(strPath_a);
		string strVerb = (g_intMode == MODE_PUBLISH) ? "publish" : "upload";
		byte[] arrData = null;

		try
		{
			arrData = File.ReadAllBytes(strPath_a);
		}
		catch (Exception objEx)
		{
			Console.WriteLine("    FAILED  " + strName + " - cannot read: " + objEx.Message);
		}

		if (arrData != null)
		{
			byte[] arrPayload = encode(arrData);

			if (arrPayload == null)
			{
				Console.WriteLine("    FAILED  " + strName + " - encoding failed");
			}
			else if (g_blnDryRun)
			{
				Console.WriteLine("    would " + strVerb + "  " + strName + "  (" + arrData.Length + " bytes, " + arrPayload.Length + " on the wire)");
				blnResult = true;
			}
			else
			{
				string strNonce = Guid.NewGuid().ToString();
				string strError = "";
				int intAttempt = 0;

				while (!blnResult && intAttempt < ATTEMPTS)
				{
					intAttempt++;

					if (g_intMode == MODE_PUBLISH)
					{
						MQPublishResult objPub = null;

						waitForNextSecond();

						objPub = g_objMQ.Publish(g_strMQURL, g_strQueue, arrPayload, strNonce, g_intRetentionDays);
						g_objLastPublish = Stopwatch.StartNew();

						if (objPub.Success)
						{
							blnResult = true;
						}
						else
						{
							strError = (objPub.ErrorMessage != null && objPub.ErrorMessage.Length > 0) ? objPub.ErrorMessage : "no response from server";
						}
					}
					else
					{
						// no wait needed - the MQ names the files at commit, in upload order
						MQAnswer objAnswer = MQBatch.Upload(g_strMQURL, g_strTransaction, arrPayload, strNonce);

						if (objAnswer.Success)
						{
							blnResult = true;
						}
						else
						{
							strError = objAnswer.Describe();
						}
					}

					if (!blnResult && intAttempt < ATTEMPTS)
					{
						Console.WriteLine("    retry   " + strName + " - " + strError);
						Thread.Sleep(RETRY_WAIT_MS);
					}
				}

				if (blnResult)
				{
					Console.WriteLine("    " + strVerb + "ed  " + strName + "  (" + arrData.Length + " bytes, " + arrPayload.Length + " on the wire)");
				}
				else
				{
					Console.WriteLine("    FAILED  " + strName + " - " + strError);
				}
			}
		}

		return blnResult;
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
	// Transactions
	// -------------------------------------------------------------------------

	// Commit is safe to send again: a commit that stopped part way carries on, and one that
	// already finished answers with the same names. So it is simply retried.
	private static bool commitTransaction()
	{
		bool blnResult = false;
		string strError = "";
		int intAttempt = 0;

		if (g_blnDryRun)
		{
			Console.WriteLine("Would commit transaction " + g_strTransaction + " to " + g_strQueue + " (" + g_intRetentionDays + " days).");
			blnResult = true;
		}
		else
		{
			Console.WriteLine("Committing transaction " + g_strTransaction + " to " + g_strQueue + " ...");

			while (!blnResult && intAttempt < ATTEMPTS)
			{
				MQAnswer objAnswer = null;

				intAttempt++;
				objAnswer = MQBatch.Commit(g_strMQURL, g_strTransaction, g_strQueue, g_intRetentionDays);

				if (objAnswer.Success)
				{
					blnResult = true;
					Console.WriteLine("Committed: " + objAnswer.Names.Count + " message(s) published to " + g_strQueue + (objAnswer.Message.Length > 0 ? " (" + objAnswer.Message + ")" : "") + ".");
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

			if (!blnResult)
			{
				Console.WriteLine("Commit FAILED - " + strError);
				Console.WriteLine("The uploads are still in the transaction. Send the commit again with:");
				Console.WriteLine("  zwrpublish -c " + g_strTransaction + " " + g_strMQURL + " \"" + g_strQueue + "\" -r " + g_intRetentionDays);
			}
		}

		return blnResult;
	}

	private static bool abortTransaction()
	{
		bool blnResult = false;
		MQAnswer objAnswer = null;

		if (g_blnDryRun)
		{
			Console.WriteLine("Would abort transaction " + g_strTransaction + ".");
			blnResult = true;
		}
		else
		{
			objAnswer = MQBatch.Abort(g_strMQURL, g_strTransaction);

			if (objAnswer.Success)
			{
				Console.WriteLine("Transaction " + g_strTransaction + " aborted - everything uploaded into it was deleted.");
				blnResult = true;
			}
			else
			{
				Console.WriteLine("Abort of transaction " + g_strTransaction + " FAILED - " + objAnswer.Describe());
			}
		}

		return blnResult;
	}
}

// -----------------------------------------------------------------------------
// MQ transaction calls: upload / commit / abort (index.php v20261002 or later).
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
	private const int UPLOAD_TIMEOUT_SECONDS = 120;
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

	public static MQAnswer Upload(string strURL_a, string strTransaction_a, byte[] arrData_a, string strNonce_a)
	{
		MultipartFormDataContent objForm = newForm("upload", strTransaction_a);
		ByteArrayContent objFile = new ByteArrayContent(arrData_a);

		if (strNonce_a.Length > 0)
		{
			objForm.Add(new StringContent(strNonce_a), "n");
		}

		objFile.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		objForm.Add(objFile, "msg", "msg.bin");

		return post(strURL_a, objForm, UPLOAD_TIMEOUT_SECONDS);
	}

	public static MQAnswer Commit(string strURL_a, string strTransaction_a, string strQueue_a, int intRetentionDays_a)
	{
		MultipartFormDataContent objForm = newForm("commit", strTransaction_a);

		objForm.Add(new StringContent(strQueue_a), "q");
		objForm.Add(new StringContent(intRetentionDays_a.ToString("D4")), "r");

		return post(strURL_a, objForm, COMMIT_TIMEOUT_SECONDS);
	}

	public static MQAnswer Abort(string strURL_a, string strTransaction_a)
	{
		return post(strURL_a, newForm("abort", strTransaction_a), UPLOAD_TIMEOUT_SECONDS);
	}

	private static MultipartFormDataContent newForm(string strAction_a, string strTransaction_a)
	{
		MultipartFormDataContent objResult = new MultipartFormDataContent("----MQBoundary" + Guid.NewGuid().ToString("N"));

		objResult.Add(new StringContent(strAction_a), "action");
		objResult.Add(new StringContent(strTransaction_a), "t");

		return objResult;
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

					if (objRoot.TryGetProperty("result", out objValue) && objValue.ValueKind == JsonValueKind.Array)
					{
						foreach (JsonElement objName in objValue.EnumerateArray())
						{
							if (objName.ValueKind == JsonValueKind.String)
							{
								objAnswer_a.Names.Add(objName.GetString());
							}
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
			// an index.php without transactions answers "Unknown action" as JSON; anything else is not an MQ reply
			objAnswer_a.Problem = "unexpected reply: " + (strBody_a.Length > 100 ? strBody_a.Substring(0, 100) : strBody_a);
		}
	}
}