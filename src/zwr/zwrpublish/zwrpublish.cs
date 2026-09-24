// Cyborg ZOSCII Web Radio Publish - zwrpublish v20260924
// (c) 2026 Cyborg Unicorn Pty Ltd.
// UNINTELLIGENCE License.
// ZOSCII core logic remains under MIT License.
//
// Publishes a folder of MP3s to a ZOSCII MQ queue. For each MP3 (in name order):
//   <name>.jpg or <name>.jpeg   cover image, if present
//   <name>.lyrics.txt           lyrics / text, if present
//   <name>.mp3                  the track
//
// Usage:
//   zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-d]
//
//   -z  ZOSCII encode each file with <romfile>
//   -u  UNSIGNAL encode each file with <romfile>
//       (neither: files are published as they are)
//   -r  retention in days (default 7)
//   -d  dry run: list what would be published, publish nothing
//
// The MQ names messages by the second they arrive, so messages published within the same
// second can sort in any order. zwrpublish waits at least a second between publishes so the
// queue keeps jpg -> txt -> mp3 order. Each file gets its own nonce and is retried with it,
// so a retry after a lost reply can't publish a duplicate. Stops at the first failure.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CyborgUnicorn.ZOSCII;

public static class ZWRPublish
{
	internal const string VERSION = "v20260924";
	internal const int MIN_GAP_MS = 1100;     // keeps each message in a later second than the one before
	internal const int ATTEMPTS = 3;
	internal const int RETRY_WAIT_MS = 5000;

	internal const int ENCODE_NONE = 0;
	internal const int ENCODE_ZOSCII = 1;
	internal const int ENCODE_UNSIGNAL = 2;

	private static string g_strFolder = "";
	private static string g_strMQURL = "";
	private static string g_strQueue = "";
	private static string g_strROMFile = "";
	private static int g_intEncodeMode = ENCODE_NONE;
	private static int g_intRetentionDays = 7;
	private static bool g_blnDryRun = false;
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
				intResult = publishFolder() ? 0 : 1;
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
		Console.WriteLine("Usage: zwrpublish <folder> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-r <days>] [-d]");
		Console.WriteLine();
		Console.WriteLine("  For each MP3 in <folder>, in name order, publishes <name>.jpg (or .jpeg),");
		Console.WriteLine("  then <name>.lyrics.txt, then <name>.mp3. The jpg and txt are optional.");
		Console.WriteLine();
		Console.WriteLine("  -z  ZOSCII encode with <romfile>");
		Console.WriteLine("  -u  UNSIGNAL encode with <romfile>");
		Console.WriteLine("      (neither: publish the files as they are)");
		Console.WriteLine("  -r  retention in days (default 7)");
		Console.WriteLine("  -d  dry run - list what would be published, publish nothing");
		Console.WriteLine();
		Console.WriteLine("Example: zwrpublish c:\\music https://example.com/radio/indexmq.php \"Cyborg Unicorn\" -z logo.png");
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

		if (blnResult && objPositional.Count == 3)
		{
			g_strFolder = objPositional[0];
			g_strMQURL = objPositional[1];
			g_strQueue = objPositional[2];
		}
		else
		{
			blnResult = false;
		}

		return blnResult;
	}

	private static bool initialise()
	{
		bool blnResult = true;

		if (!Directory.Exists(g_strFolder))
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

			objMP3s.Sort(StringComparer.OrdinalIgnoreCase);
		}

		Console.WriteLine("Folder:    " + g_strFolder + "  (" + objMP3s.Count + " MP3s)");
		Console.WriteLine("Queue:     " + g_strQueue + " @ " + g_strMQURL);
		Console.WriteLine("Encode:    " + (g_intEncodeMode == ENCODE_ZOSCII ? "ZOSCII (" + g_strROMFile + ")" : (g_intEncodeMode == ENCODE_UNSIGNAL ? "UNSIGNAL (" + g_strROMFile + ")" : "none")));
		Console.WriteLine("Retention: " + g_intRetentionDays + " days");
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

		if (blnResult)
		{
			Console.WriteLine((g_blnDryRun ? "Would publish " : "Published ") + intPublished + " message(s).");
		}
		else
		{
			Console.WriteLine("STOPPED after " + intPublished + " message(s). Everything listed above the failure was published.");
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
				Console.WriteLine("    would publish  " + strName + "  (" + arrData.Length + " bytes, " + arrPayload.Length + " on the wire)");
				blnResult = true;
			}
			else
			{
				string strNonce = Guid.NewGuid().ToString();
				string strError = "";
				int intAttempt = 0;

				while (!blnResult && intAttempt < ATTEMPTS)
				{
					MQPublishResult objPub = null;

					intAttempt++;
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

						if (intAttempt < ATTEMPTS)
						{
							Console.WriteLine("    retry   " + strName + " - " + strError);
							Thread.Sleep(RETRY_WAIT_MS);
						}
					}
				}

				if (blnResult)
				{
					Console.WriteLine("    published  " + strName + "  (" + arrData.Length + " bytes, " + arrPayload.Length + " on the wire)");
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
}
