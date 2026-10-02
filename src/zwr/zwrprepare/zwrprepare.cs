// Cyborg ZOSCII Web Radio Prepare - zwrprepare v20261002
// (c) 2026 Cyborg Unicorn Pty Ltd.
// ZOSCII core logic remains under MIT License.
//
// For every MP3 in a folder, writes <name>.json next to it: a zwr track descriptor filled in
// with everything that can be worked out locally - ID3 tags, audio format and duration, and
// the size and ZRollingHash of the mp3 and its matching image / text / lyrics files.
// Station, channel, artist and store details come from an optional template JSON.
//
// Usage:
//   zwrprepare <folder> [-t <template.json>]
//
// Matching files next to <name>.mp3:
//   <name>.jpg / .jpeg / .png    -> image
//   <name>.txt                   -> text
//   <name>.lrc, else <name>.lyrics.txt -> lyrics
//
// Store names are left empty: they're filled in when the files are stored. Re-running keeps the
// existing id, instance and store names, except a store name is cleared when that file's size
// or hash has changed (the stored copy is out of date).
//
// Each JSON gets the same created and modified date/time as its mp3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CyborgUnicorn.ZOSCII;

public static class ZWRPrepare
{
	internal const string VERSION = "v20261002";
	internal const string UFID_OWNER = "cyborgunicorn.com.au";

	private static string g_strFolder = "";
	private static string g_strTemplateFile = "";
	private static JsonObject g_objTemplate = null;
	private static string g_strDefaultStore = "";

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
		Console.WriteLine("ZOSCII Web Radio Prepare " + VERSION);
		Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd");
		Console.WriteLine();

		if (parseArgs(arrArgs_a))
		{
			if (initialise())
			{
				intResult = prepareFolder();
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
		Console.WriteLine("Usage: zwrprepare <folder> [-t <template.json>]");
		Console.WriteLine();
		Console.WriteLine("  Writes <name>.json next to every <name>.mp3 in <folder>, filled in from the");
		Console.WriteLine("  mp3's ID3 tags and audio, plus the size and hash of the mp3 and its matching");
		Console.WriteLine("  <name>.jpg/.jpeg/.png (image), <name>.txt (text), <name>.lrc or");
		Console.WriteLine("  <name>.lyrics.txt (lyrics).");
		Console.WriteLine();
		Console.WriteLine("  -t  template JSON with station / channel / artist / stores for every track");
		Console.WriteLine();
		Console.WriteLine("Example: zwrprepare c:\\music -t c:\\zwr\\station.json");
	}

	private static bool parseArgs(string[] arrArgs_a)
	{
		bool blnResult = true;
		List<string> objPositional = new List<string>();
		int intI = 0;

		while (blnResult && intI < arrArgs_a.Length)
		{
			string strArg = arrArgs_a[intI].ToLowerInvariant();

			if (strArg == "-t" && intI + 1 < arrArgs_a.Length)
			{
				g_strTemplateFile = arrArgs_a[intI + 1];
				intI += 2;
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

		if (blnResult && objPositional.Count == 1)
		{
			g_strFolder = objPositional[0];
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

		if (blnResult && g_strTemplateFile.Length > 0)
		{
			try
			{
				g_objTemplate = JsonNode.Parse(File.ReadAllText(g_strTemplateFile), null, g_objReadOptions) as JsonObject;

				if (g_objTemplate == null)
				{
					Console.WriteLine("Error: template is not a JSON object: " + g_strTemplateFile);
					blnResult = false;
				}
			}
			catch (Exception objEx)
			{
				Console.WriteLine("Error: cannot read template " + g_strTemplateFile + " - " + objEx.Message);
				blnResult = false;
			}
		}

		if (blnResult && g_objTemplate != null)
		{
			JsonArray objStores = g_objTemplate["stores"] as JsonArray;

			if (objStores != null && objStores.Count > 0)
			{
				g_strDefaultStore = getString(objStores[0] as JsonObject, "name");
			}
		}

		return blnResult;
	}

	// -------------------------------------------------------------------------
	// Folder
	// -------------------------------------------------------------------------

	private static int prepareFolder()
	{
		Dictionary<string, string> objFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		List<string> objMP3s = new List<string>();
		int intCreated = 0;
		int intUpdated = 0;
		int intUnchanged = 0;
		int intFailed = 0;
		int intI = 0;

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
		Console.WriteLine("Template:  " + (g_objTemplate != null ? g_strTemplateFile : "none"));
		Console.WriteLine("Store:     " + (g_strDefaultStore.Length > 0 ? g_strDefaultStore : "none (store names left empty)"));
		Console.WriteLine();

		for (intI = 0; intI < objMP3s.Count; intI++)
		{
			string strOutcome = prepareTrack(objMP3s[intI], objFiles);

			if (strOutcome == "created") { intCreated++; }
			else if (strOutcome == "updated") { intUpdated++; }
			else if (strOutcome == "unchanged") { intUnchanged++; }
			else { intFailed++; }
		}

		Console.WriteLine();
		Console.WriteLine("Complete: " + intCreated + " created, " + intUpdated + " updated, " + intUnchanged + " unchanged, " + intFailed + " failed");

		return intFailed > 0 ? 2 : 0;
	}

	// -------------------------------------------------------------------------
	// One track - returns "created", "updated", "unchanged" or "failed"
	// -------------------------------------------------------------------------

	private static string prepareTrack(string strMP3Path_a, Dictionary<string, string> objFiles_a)
	{
		string strResult = "failed";
		string strMP3Name = Path.GetFileName(strMP3Path_a);
		string strBase = Path.GetFileNameWithoutExtension(strMP3Path_a);
		string strJsonPath = Path.Combine(Path.GetDirectoryName(strMP3Path_a) ?? "", strBase + ".json");
		string strError = "";
		string strOldText = "";
		byte[] arrData = null;
		JsonObject objExisting = null;
		MP3Info objInfo = null;
		ID3Info objTags = null;

		try
		{
			arrData = File.ReadAllBytes(strMP3Path_a);
		}
		catch (Exception objEx)
		{
			strError = "cannot read: " + objEx.Message;
		}

		if (strError.Length == 0)
		{
			objInfo = MP3Info.Analyse(arrData);

			if (!objInfo.IsMP3)
			{
				strError = "not an MP3 - no MPEG Layer III frames found";
			}
		}

		// An existing JSON that can't be read is left alone rather than overwritten.
		if (strError.Length == 0 && File.Exists(strJsonPath))
		{
			try
			{
				strOldText = File.ReadAllText(strJsonPath);
				objExisting = JsonNode.Parse(strOldText, null, g_objReadOptions) as JsonObject;

				if (objExisting == null)
				{
					strError = Path.GetFileName(strJsonPath) + " exists but isn't a JSON object - left as it is";
				}
			}
			catch (Exception objEx)
			{
				strError = Path.GetFileName(strJsonPath) + " exists but can't be read - left as it is: " + objEx.Message;
			}
		}

		if (strError.Length == 0)
		{
			JsonObject objOut = null;
			List<string> objExtras = new List<string>();

			objTags = ID3Info.Read(arrData);

			try
			{
				objOut = buildTrack(strBase, strMP3Path_a, arrData, objInfo, objTags, objExisting, objFiles_a, objExtras);
			}
			catch (Exception objEx)
			{
				strError = objEx.Message;
			}

			if (strError.Length == 0)
			{
				string strNewText = objOut.ToJsonString(g_objWriteOptions) + Environment.NewLine;

				if (objExisting != null && strNewText == strOldText)
				{
					strResult = "unchanged";
				}
				else
				{
					try
					{
						File.WriteAllText(strJsonPath, strNewText, new UTF8Encoding(false));
						strResult = (objExisting != null) ? "updated" : "created";
					}
					catch (Exception objEx)
					{
						strError = "cannot write " + Path.GetFileName(strJsonPath) + ": " + objEx.Message;
					}
				}
			}

			if (strError.Length == 0)
			{
				copyFileTimes(strMP3Path_a, strJsonPath);
			}

			if (strError.Length == 0)
			{
				Console.WriteLine("[" + padRight(strResult, 9) + "] " + strMP3Name + "  " + describe(objInfo) + (objExtras.Count > 0 ? ", " + string.Join(", ", objExtras) : ""));
			}
		}

		if (strError.Length > 0)
		{
			strResult = "failed";
			Console.WriteLine("[failed   ] " + strMP3Name + " - " + strError);
		}

		return strResult;
	}

	private static JsonObject buildTrack(string strBase_a, string strMP3Path_a, byte[] arrData_a, MP3Info objInfo_a, ID3Info objTags_a,
		JsonObject objExisting_a, Dictionary<string, string> objFiles_a, List<string> objExtras_a)
	{
		JsonObject objOut = new JsonObject();
		JsonObject objTemplate = g_objTemplate;
		string strFile = "";

		objOut["class"] = "zwr";
		objOut["version"] = 0;
		objOut["type"] = "track";

		objOut["id"] = firstOf(getString(objExisting_a, "id"), objTags_a.TrackGUID, Guid.NewGuid().ToString());
		objOut["instance"] = getString(objExisting_a, "instance");
		objOut["title"] = firstOf(objTags_a.Title, strBase_a, "");

		addIfSet(objOut, "album", firstOf(objTags_a.Album, getString(objExisting_a, "album"), ""));
		addIfSet(objOut, "year", firstOf(objTags_a.Year, getString(objExisting_a, "year"), ""));
		addIfSet(objOut, "genre", firstOf(objTags_a.Genre, getString(objExisting_a, "genre"), ""));
		addIfSet(objOut, "copyright", firstOf(objTags_a.Copyright, getString(objExisting_a, "copyright"), ""));

		// Template wins, then what the mp3 says, then whatever the existing JSON had.
		objOut["station"] = mergeSection("station", new string[] { "name", "id", "website", "email", "phone" }, objTemplate, null, objExisting_a);
		objOut["channel"] = mergeSection("channel", new string[] { "name", "id" }, objTemplate, null, objExisting_a);

		{
			Dictionary<string, string> objFromTags = new Dictionary<string, string>();
			objFromTags["name"] = objTags_a.Artist;
			objFromTags["website"] = objTags_a.ArtistURL;
			objOut["artist"] = mergeSection("artist", new string[] { "name", "id", "website", "email", "phone" }, objTemplate, objFromTags, objExisting_a);
		}

		{
			JsonArray objStores = null;

			if (objTemplate != null && objTemplate["stores"] is JsonArray)
			{
				objStores = (JsonArray)objTemplate["stores"].DeepClone();
			}
			else if (objExisting_a != null && objExisting_a["stores"] is JsonArray)
			{
				objStores = (JsonArray)objExisting_a["stores"].DeepClone();
			}
			else
			{
				objStores = new JsonArray();
			}

			objOut["stores"] = objStores;
		}

		// mp3
		{
			JsonObject objMP3 = mediaEntry(getObject(objExisting_a, "mp3"), Path.GetFileName(strMP3Path_a), arrData_a);
			objMP3["duration"] = Math.Round(objInfo_a.Seconds, 3);
			objMP3["bitrate"] = objInfo_a.AverageKbps();
			objMP3["samplerate"] = objInfo_a.SampleRate;
			objMP3["channels"] = objInfo_a.Channels;
			objOut["mp3"] = objMP3;
		}

		strFile = findFirst(objFiles_a, strBase_a, new string[] { ".jpg", ".jpeg", ".png" });
		if (strFile.Length > 0)
		{
			objOut["image"] = mediaEntry(getObject(objExisting_a, "image"), Path.GetFileName(strFile), File.ReadAllBytes(strFile));
			objExtras_a.Add("image");
		}

		strFile = findFirst(objFiles_a, strBase_a, new string[] { ".txt" });
		if (strFile.Length > 0)
		{
			objOut["text"] = mediaEntry(getObject(objExisting_a, "text"), Path.GetFileName(strFile), File.ReadAllBytes(strFile));
			objExtras_a.Add("text");
		}

		strFile = findFirst(objFiles_a, strBase_a, new string[] { ".lrc", ".lyrics.txt" });
		if (strFile.Length > 0)
		{
			objOut["lyrics"] = mediaEntry(getObject(objExisting_a, "lyrics"), Path.GetFileName(strFile), File.ReadAllBytes(strFile));
			objExtras_a.Add("lyrics");
		}

		if (objInfo_a.MixedFormat)
		{
			objExtras_a.Add("WARNING: sample rate or channels change inside the file");
		}

		// Anything else in the template or hand-added to the existing JSON is carried over.
		// A media entry whose file has gone is dropped, not carried over.
		copyUnknownKeys(objTemplate, objOut, null);
		copyUnknownKeys(objExisting_a, objOut, new string[] { "mp3", "image", "text", "lyrics" });

		return objOut;
	}

	// Size + hash of a file, keeping the store and store name from the existing entry only
	// if the file is still the same one that was stored.
	private static JsonObject mediaEntry(JsonObject objExisting_a, string strFileName_a, byte[] arrData_a)
	{
		JsonObject objResult = new JsonObject();
		string strHash = toHex(ZRollingHash.Bytes(arrData_a, true));
		string strStore = g_strDefaultStore;
		string strName = "";

		if (objExisting_a != null && getLong(objExisting_a, "size") == arrData_a.Length && getString(objExisting_a, "hash") == strHash)
		{
			strStore = firstOf(getString(objExisting_a, "store"), g_strDefaultStore, "");
			strName = getString(objExisting_a, "name");
		}

		objResult["store"] = strStore;
		objResult["name"] = strName;
		objResult["file"] = strFileName_a;
		objResult["size"] = arrData_a.Length;
		objResult["hash"] = strHash;

		return objResult;
	}

	private static JsonObject mergeSection(string strKey_a, string[] arrFields_a, JsonObject objTemplate_a, Dictionary<string, string> objFromTags_a, JsonObject objExisting_a)
	{
		JsonObject objResult = new JsonObject();
		JsonObject objTemplate = getObject(objTemplate_a, strKey_a);
		JsonObject objExisting = getObject(objExisting_a, strKey_a);
		int intI = 0;

		for (intI = 0; intI < arrFields_a.Length; intI++)
		{
			string strTag = "";

			if (objFromTags_a != null && objFromTags_a.ContainsKey(arrFields_a[intI]) && objFromTags_a[arrFields_a[intI]] != null)
			{
				strTag = objFromTags_a[arrFields_a[intI]];
			}

			objResult[arrFields_a[intI]] = firstOf(getString(objTemplate, arrFields_a[intI]), strTag, getString(objExisting, arrFields_a[intI]));
		}

		copyUnknownKeys(objTemplate, objResult, null);
		copyUnknownKeys(objExisting, objResult, null);

		return objResult;
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private static void copyUnknownKeys(JsonObject objFrom_a, JsonObject objTo_a, string[] arrSkip_a)
	{
		if (objFrom_a != null)
		{
			foreach (KeyValuePair<string, JsonNode> objPair in objFrom_a)
			{
				if (!objTo_a.ContainsKey(objPair.Key) && (arrSkip_a == null || Array.IndexOf(arrSkip_a, objPair.Key) < 0))
				{
					objTo_a[objPair.Key] = (objPair.Value != null) ? objPair.Value.DeepClone() : null;
				}
			}
		}
	}

	private static JsonObject getObject(JsonObject objFrom_a, string strKey_a)
	{
		JsonObject objResult = null;

		if (objFrom_a != null && objFrom_a.ContainsKey(strKey_a))
		{
			objResult = objFrom_a[strKey_a] as JsonObject;
		}

		return objResult;
	}

	private static string getString(JsonObject objFrom_a, string strKey_a)
	{
		string strResult = "";

		if (objFrom_a != null && objFrom_a.ContainsKey(strKey_a))
		{
			JsonValue objValue = objFrom_a[strKey_a] as JsonValue;
			string strValue = null;

			if (objValue != null && objValue.TryGetValue<string>(out strValue) && strValue != null)
			{
				strResult = strValue;
			}
		}

		return strResult;
	}

	private static long getLong(JsonObject objFrom_a, string strKey_a)
	{
		long lngResult = -1;

		if (objFrom_a != null && objFrom_a.ContainsKey(strKey_a))
		{
			JsonValue objValue = objFrom_a[strKey_a] as JsonValue;
			long lngValue = 0;

			if (objValue != null && objValue.TryGetValue<long>(out lngValue))
			{
				lngResult = lngValue;
			}
		}

		return lngResult;
	}

	private static void addIfSet(JsonObject objTo_a, string strKey_a, string strValue_a)
	{
		if (strValue_a != null && strValue_a.Length > 0)
		{
			objTo_a[strKey_a] = strValue_a;
		}
	}

	private static string firstOf(string strA_a, string strB_a, string strC_a)
	{
		string strResult = "";

		if (strA_a != null && strA_a.Length > 0)
		{
			strResult = strA_a;
		}
		else if (strB_a != null && strB_a.Length > 0)
		{
			strResult = strB_a;
		}
		else if (strC_a != null)
		{
			strResult = strC_a;
		}

		return strResult;
	}

	private static string findFirst(Dictionary<string, string> objFiles_a, string strBase_a, string[] arrExts_a)
	{
		string strResult = "";
		string strPath = "";
		int intI = 0;

		for (intI = 0; strResult.Length == 0 && intI < arrExts_a.Length; intI++)
		{
			if (objFiles_a.TryGetValue(strBase_a + arrExts_a[intI], out strPath))
			{
				strResult = strPath;
			}
		}

		return strResult;
	}

	// The JSON gets the mp3's created and modified date/time. Not being able to set them
	// doesn't fail the track - the JSON itself is written.
	private static void copyFileTimes(string strFrom_a, string strTo_a)
	{
		try
		{
			File.SetCreationTime(strTo_a, File.GetCreationTime(strFrom_a));
			File.SetLastWriteTime(strTo_a, File.GetLastWriteTime(strFrom_a));
		}
		catch (Exception objEx)
		{
			Console.WriteLine("    warning: cannot set the date/time on " + Path.GetFileName(strTo_a) + " - " + objEx.Message);
		}
	}

	private static string toHex(byte[] arrData_a)
	{
		string strResult = "";

		if (arrData_a != null)
		{
			strResult = Convert.ToHexString(arrData_a).ToLowerInvariant();
		}

		return strResult;
	}

	private static string describe(MP3Info objInfo_a)
	{
		int intSeconds = (int)Math.Round(objInfo_a.Seconds);

		return (intSeconds / 60) + ":" + (intSeconds % 60).ToString("00") + ", " + objInfo_a.AverageKbps() + "kbps, " +
			objInfo_a.SampleRate + "Hz " + (objInfo_a.Channels == 1 ? "mono" : "stereo");
	}

	private static string padRight(string strText_a, int intWidth_a)
	{
		return strText_a.PadRight(intWidth_a);
	}
}

// -----------------------------------------------------------------------------
// MP3 audio: walks every MPEG Layer III frame to get format and exact duration
// (works for VBR too). A frame only counts when the next frame header is also
// valid (or it ends exactly at the end of the audio) - the same two-header check
// zwrserve uses.
// -----------------------------------------------------------------------------

internal class MP3Info
{
	public bool IsMP3 = false;
	public int AudioStart = 0;
	public int AudioEnd = 0;
	public long Frames = 0;
	public long AudioBytes = 0;
	public double Seconds = 0;
	public int SampleRate = 0;
	public int Channels = 0;
	public bool MixedFormat = false;

	private static readonly int[] g_arrBitrateV1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
	private static readonly int[] g_arrBitrateV2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

	// Indexed by the 2-bit version field: 0 = MPEG 2.5, 1 = reserved, 2 = MPEG 2, 3 = MPEG 1
	private static readonly int[,] g_arrSampleRates =
	{
		{ 11025, 12000, 8000 },
		{ 0, 0, 0 },
		{ 22050, 24000, 16000 },
		{ 44100, 48000, 32000 },
	};

	public int AverageKbps()
	{
		int intResult = 0;

		if (Seconds > 0)
		{
			intResult = (int)Math.Round(AudioBytes * 8.0 / Seconds / 1000.0);
		}

		return intResult;
	}

	public static MP3Info Analyse(byte[] arrData_a)
	{
		MP3Info objResult = new MP3Info();
		int intPos = 0;
		bool blnFirst = true;

		objResult.AudioStart = skipID3v2(arrData_a);
		objResult.AudioEnd = findAudioEnd(arrData_a, objResult.AudioStart);
		intPos = objResult.AudioStart;

		while (intPos + 4 <= objResult.AudioEnd)
		{
			int intLength = 0;
			int intSampleRate = 0;
			int intChannels = 0;
			int intSamples = 0;
			int intVersion = 0;

			if (parseHeader(arrData_a, intPos, out intLength, out intSampleRate, out intChannels, out intSamples, out intVersion)
				&& intPos + intLength <= objResult.AudioEnd
				&& confirmed(arrData_a, intPos + intLength, objResult.AudioEnd))
			{
				bool blnInfoFrame = false;

				if (blnFirst)
				{
					objResult.SampleRate = intSampleRate;
					objResult.Channels = intChannels;
					blnInfoFrame = isInfoFrame(arrData_a, intPos, intLength, intVersion, intChannels);
					blnFirst = false;
				}
				else if (intSampleRate != objResult.SampleRate || intChannels != objResult.Channels)
				{
					objResult.MixedFormat = true;
				}

				// A Xing / Info / VBRI frame at the start is a silent header frame, not audio.
				if (!blnInfoFrame)
				{
					objResult.Frames++;
					objResult.AudioBytes += intLength;
					objResult.Seconds += (double)intSamples / intSampleRate;
				}

				intPos += intLength;
			}
			else
			{
				intPos++;
			}
		}

		objResult.IsMP3 = objResult.Frames > 0;

		return objResult;
	}

	private static bool confirmed(byte[] arrData_a, int intNext_a, int intEnd_a)
	{
		bool blnResult = false;
		int intLength = 0;
		int intSampleRate = 0;
		int intChannels = 0;
		int intSamples = 0;
		int intVersion = 0;

		if (intNext_a + 4 > intEnd_a)
		{
			blnResult = true;
		}
		else
		{
			blnResult = parseHeader(arrData_a, intNext_a, out intLength, out intSampleRate, out intChannels, out intSamples, out intVersion);
		}

		return blnResult;
	}

	private static bool parseHeader(byte[] arrData_a, int intPos_a, out int intLength_a, out int intSampleRate_a, out int intChannels_a, out int intSamples_a, out int intVersion_a)
	{
		bool blnResult = false;

		intLength_a = 0;
		intSampleRate_a = 0;
		intChannels_a = 0;
		intSamples_a = 0;
		intVersion_a = 0;

		if (intPos_a + 4 <= arrData_a.Length && arrData_a[intPos_a] == 0xFF && (arrData_a[intPos_a + 1] & 0xE0) == 0xE0)
		{
			int intB1 = arrData_a[intPos_a + 1];
			int intB2 = arrData_a[intPos_a + 2];
			int intB3 = arrData_a[intPos_a + 3];
			int intVersion = (intB1 >> 3) & 3;
			int intLayer = (intB1 >> 1) & 3;
			int intBitrateIdx = (intB2 >> 4) & 15;
			int intRateIdx = (intB2 >> 2) & 3;
			int intPadding = (intB2 >> 1) & 1;
			int intEmphasis = intB3 & 3;

			if (intVersion != 1 && intLayer == 1 && intBitrateIdx != 0 && intBitrateIdx != 15 && intRateIdx != 3 && intEmphasis != 2)
			{
				int intBitrate = ((intVersion == 3) ? g_arrBitrateV1[intBitrateIdx] : g_arrBitrateV2[intBitrateIdx]) * 1000;
				int intSampleRate = g_arrSampleRates[intVersion, intRateIdx];

				intLength_a = ((intVersion == 3) ? 144 : 72) * intBitrate / intSampleRate + intPadding;
				intSampleRate_a = intSampleRate;
				intChannels_a = (((intB3 >> 6) & 3) == 3) ? 1 : 2;
				intSamples_a = (intVersion == 3) ? 1152 : 576;
				intVersion_a = intVersion;
				blnResult = intLength_a >= 4;
			}
		}

		return blnResult;
	}

	private static bool isInfoFrame(byte[] arrData_a, int intPos_a, int intLength_a, int intVersion_a, int intChannels_a)
	{
		bool blnResult = false;
		int intSideInfo = (intVersion_a == 3) ? (intChannels_a == 1 ? 17 : 32) : (intChannels_a == 1 ? 9 : 17);
		int intTag = intPos_a + 4 + intSideInfo;

		if (intTag + 4 <= intPos_a + intLength_a)
		{
			string strTag = Encoding.ASCII.GetString(arrData_a, intTag, 4);
			blnResult = (strTag == "Xing" || strTag == "Info");
		}

		if (!blnResult && intPos_a + 36 + 4 <= intPos_a + intLength_a)
		{
			blnResult = Encoding.ASCII.GetString(arrData_a, intPos_a + 36, 4) == "VBRI";
		}

		return blnResult;
	}

	internal static int skipID3v2(byte[] arrData_a)
	{
		int intPos = 0;

		while (intPos + 10 <= arrData_a.Length && arrData_a[intPos] == (byte)'I' && arrData_a[intPos + 1] == (byte)'D' && arrData_a[intPos + 2] == (byte)'3')
		{
			int intSize = (arrData_a[intPos + 6] << 21) | (arrData_a[intPos + 7] << 14) | (arrData_a[intPos + 8] << 7) | arrData_a[intPos + 9];
			bool blnFooter = (arrData_a[intPos + 3] == 4) && ((arrData_a[intPos + 5] & 0x10) != 0);

			intPos += 10 + intSize + (blnFooter ? 10 : 0);
		}

		return Math.Min(intPos, arrData_a.Length);
	}

	// End of audio: before an ID3v1 tag and/or an APEv2 tag at the end of the file.
	private static int findAudioEnd(byte[] arrData_a, int intStart_a)
	{
		int intEnd = arrData_a.Length;

		if (intEnd - 128 >= intStart_a && arrData_a[intEnd - 128] == (byte)'T' && arrData_a[intEnd - 127] == (byte)'A' && arrData_a[intEnd - 126] == (byte)'G')
		{
			intEnd -= 128;
		}

		if (intEnd - 32 >= intStart_a && Encoding.ASCII.GetString(arrData_a, intEnd - 32, 8) == "APETAGEX")
		{
			int intTagSize = BitConverter.ToInt32(arrData_a, intEnd - 32 + 12);
			uint uintFlags = BitConverter.ToUInt32(arrData_a, intEnd - 32 + 20);
			int intTotal = intTagSize + (((uintFlags & 0x80000000) != 0) ? 32 : 0);

			if (intTotal > 0 && intEnd - intTotal >= intStart_a)
			{
				intEnd -= intTotal;
			}
		}

		return intEnd;
	}
}

// -----------------------------------------------------------------------------
// ID3 tags: ID3v2.2 / 2.3 / 2.4, with ID3v1 as a fallback for anything missing.
// Best effort - anything it can't make sense of is just left empty.
// -----------------------------------------------------------------------------

internal class ID3Info
{
	public string Title = "";
	public string Artist = "";
	public string Album = "";
	public string Year = "";
	public string Genre = "";
	public string Copyright = "";
	public string ArtistURL = "";
	public string TrackGUID = "";

	private static readonly string[] g_arrGenres =
	{
		"Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz", "Metal",
		"New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock", "Techno", "Industrial",
		"Alternative", "Ska", "Death Metal", "Pranks", "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk",
		"Fusion", "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise",
		"Alternative Rock", "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic",
		"Darkwave", "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy", "Cult", "Gangsta",
		"Top 40", "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave", "Psychedelic", "Rave", "Showtunes",
		"Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro", "Musical", "Rock & Roll", "Hard Rock"
	};

	public static ID3Info Read(byte[] arrData_a)
	{
		ID3Info objResult = new ID3Info();
		Dictionary<string, byte[]> objFrames = new Dictionary<string, byte[]>();

		try
		{
			readV2(arrData_a, objFrames);
		}
		catch
		{
			// keep whatever frames were read before the problem
		}

		objResult.Title = textFrame(objFrames, "TIT2");
		objResult.Artist = textFrame(objFrames, "TPE1");
		objResult.Album = textFrame(objFrames, "TALB");
		objResult.Year = yearOf(firstOf(textFrame(objFrames, "TYER"), textFrame(objFrames, "TDRC")));
		objResult.Genre = genreOf(textFrame(objFrames, "TCON"));
		objResult.Copyright = textFrame(objFrames, "TCOP");
		objResult.ArtistURL = urlFrame(objFrames, "WOAR");
		objResult.TrackGUID = ufidGUID(objFrames);

		readV1(arrData_a, objResult);

		return objResult;
	}

	private static void readV2(byte[] arrData_a, Dictionary<string, byte[]> objFrames_a)
	{
		int intPos = 0;

		while (intPos + 10 <= arrData_a.Length && arrData_a[intPos] == (byte)'I' && arrData_a[intPos + 1] == (byte)'D' && arrData_a[intPos + 2] == (byte)'3')
		{
			int intVersion = arrData_a[intPos + 3];
			int intFlags = arrData_a[intPos + 5];
			int intSize = synchsafe(arrData_a, intPos + 6);
			int intBodyLength = Math.Max(0, Math.Min(intSize, arrData_a.Length - intPos - 10));
			byte[] arrBody = new byte[intBodyLength];
			int intStart = 0;

			Array.Copy(arrData_a, intPos + 10, arrBody, 0, intBodyLength);

			// Whole-tag unsynchronisation: every FF 00 was written for FF.
			if ((intFlags & 0x80) != 0 && intVersion <= 3)
			{
				arrBody = undoUnsync(arrBody);
			}

			if ((intFlags & 0x40) != 0 && arrBody.Length >= 4 && intVersion >= 3)
			{
				intStart = (intVersion == 4) ? synchsafe(arrBody, 0) : 4 + bigEndian(arrBody, 0, 4);
			}

			if (intVersion >= 2 && intVersion <= 4)
			{
				readFrames(arrBody, intStart, intVersion, (intFlags & 0x80) != 0, objFrames_a);
			}

			intPos += 10 + intSize + ((intVersion == 4 && (intFlags & 0x10) != 0) ? 10 : 0);
		}
	}

	private static void readFrames(byte[] arrBody_a, int intStart_a, int intVersion_a, bool blnTagUnsync_a, Dictionary<string, byte[]> objFrames_a)
	{
		int intHeader = (intVersion_a == 2) ? 6 : 10;
		int intIdLength = (intVersion_a == 2) ? 3 : 4;
		int intPos = intStart_a;
		bool blnMore = true;

		while (blnMore && intPos >= 0 && intPos + intHeader <= arrBody_a.Length)
		{
			if (arrBody_a[intPos] == 0)
			{
				blnMore = false; // padding
			}
			else
			{
				string strId = Encoding.ASCII.GetString(arrBody_a, intPos, intIdLength);
				int intSize = (intVersion_a == 2) ? bigEndian(arrBody_a, intPos + 3, 3)
					: (intVersion_a == 4) ? synchsafe(arrBody_a, intPos + 4) : bigEndian(arrBody_a, intPos + 4, 4);

				if (intSize <= 0 || intPos + intHeader + intSize > arrBody_a.Length)
				{
					blnMore = false;
				}
				else
				{
					int intDataStart = intPos + intHeader;
					int intDataLength = intSize;
					bool blnSkip = false;
					bool blnUnsync = false;

					if (intVersion_a == 3)
					{
						blnSkip = (arrBody_a[intPos + 9] & 0xC0) != 0;   // compressed or encrypted
					}
					else if (intVersion_a == 4)
					{
						int intFlags2 = arrBody_a[intPos + 9];
						blnSkip = (intFlags2 & 0x0C) != 0;                 // compressed or encrypted
						blnUnsync = blnTagUnsync_a || (intFlags2 & 0x02) != 0;

						if ((intFlags2 & 0x01) != 0)                       // data length indicator
						{
							intDataStart += 4;
							intDataLength -= 4;
						}
					}
					else
					{
						strId = mapV22(strId);
					}

					if (!blnSkip && intDataLength > 0)
					{
						byte[] arrFrame = new byte[intDataLength];
						Array.Copy(arrBody_a, intDataStart, arrFrame, 0, intDataLength);

						if (blnUnsync)
						{
							arrFrame = undoUnsync(arrFrame);
						}

						if (strId == "UFID")
						{
							strId = "UFID:" + ufidOwner(arrFrame).ToLowerInvariant();
						}

						if (!objFrames_a.ContainsKey(strId))
						{
							objFrames_a[strId] = arrFrame;
						}
					}

					intPos += intHeader + intSize;
				}
			}
		}
	}

	private static string mapV22(string strId_a)
	{
		string strResult = strId_a;

		if (strId_a == "TT2") { strResult = "TIT2"; }
		else if (strId_a == "TP1") { strResult = "TPE1"; }
		else if (strId_a == "TAL") { strResult = "TALB"; }
		else if (strId_a == "TYE") { strResult = "TYER"; }
		else if (strId_a == "TCO") { strResult = "TCON"; }
		else if (strId_a == "TCR") { strResult = "TCOP"; }
		else if (strId_a == "WAR") { strResult = "WOAR"; }
		else if (strId_a == "UFI") { strResult = "UFID"; }

		return strResult;
	}

	private static void readV1(byte[] arrData_a, ID3Info objInfo_a)
	{
		int intPos = arrData_a.Length - 128;

		if (intPos >= 0 && arrData_a[intPos] == (byte)'T' && arrData_a[intPos + 1] == (byte)'A' && arrData_a[intPos + 2] == (byte)'G')
		{
			int intGenre = arrData_a[intPos + 127];

			objInfo_a.Title = firstOf(objInfo_a.Title, latin1(arrData_a, intPos + 3, 30));
			objInfo_a.Artist = firstOf(objInfo_a.Artist, latin1(arrData_a, intPos + 33, 30));
			objInfo_a.Album = firstOf(objInfo_a.Album, latin1(arrData_a, intPos + 63, 30));
			objInfo_a.Year = firstOf(objInfo_a.Year, yearOf(latin1(arrData_a, intPos + 93, 4)));

			if (objInfo_a.Genre.Length == 0 && intGenre < g_arrGenres.Length)
			{
				objInfo_a.Genre = g_arrGenres[intGenre];
			}
		}
	}

	private static string textFrame(Dictionary<string, byte[]> objFrames_a, string strId_a)
	{
		string strResult = "";
		byte[] arrFrame = null;

		if (objFrames_a.TryGetValue(strId_a, out arrFrame) && arrFrame.Length >= 1)
		{
			strResult = decodeText(arrFrame[0], arrFrame, 1, arrFrame.Length - 1);
		}

		return strResult;
	}

	private static string urlFrame(Dictionary<string, byte[]> objFrames_a, string strId_a)
	{
		string strResult = "";
		byte[] arrFrame = null;

		if (objFrames_a.TryGetValue(strId_a, out arrFrame))
		{
			strResult = latin1(arrFrame, 0, arrFrame.Length);
		}

		return strResult;
	}

	// UFID: owner (Latin-1, zero-terminated) then up to 64 bytes of identifier.
	private static string ufidOwner(byte[] arrFrame_a)
	{
		int intEnd = Array.IndexOf(arrFrame_a, (byte)0);

		return (intEnd >= 0) ? Encoding.Latin1.GetString(arrFrame_a, 0, intEnd) : "";
	}

	private static string ufidGUID(Dictionary<string, byte[]> objFrames_a)
	{
		string strResult = "";
		byte[] arrFrame = null;

		if (objFrames_a.TryGetValue("UFID:" + ZWRPrepare.UFID_OWNER, out arrFrame))
		{
			int intEnd = Array.IndexOf(arrFrame, (byte)0);
			Guid objGuid = Guid.Empty;

			if (intEnd >= 0 && Guid.TryParse(Encoding.ASCII.GetString(arrFrame, intEnd + 1, arrFrame.Length - intEnd - 1).Trim('\0', ' '), out objGuid))
			{
				strResult = objGuid.ToString();
			}
		}

		return strResult;
	}

	private static string decodeText(byte bytEncoding_a, byte[] arrData_a, int intOffset_a, int intLength_a)
	{
		string strResult = "";

		if (intLength_a > 0)
		{
			if (bytEncoding_a == 0x00)
			{
				strResult = Encoding.Latin1.GetString(arrData_a, intOffset_a, intLength_a);
			}
			else if (bytEncoding_a == 0x01)
			{
				if (intLength_a >= 2 && arrData_a[intOffset_a] == 0xFE && arrData_a[intOffset_a + 1] == 0xFF)
				{
					strResult = Encoding.BigEndianUnicode.GetString(arrData_a, intOffset_a + 2, intLength_a - 2);
				}
				else if (intLength_a >= 2 && arrData_a[intOffset_a] == 0xFF && arrData_a[intOffset_a + 1] == 0xFE)
				{
					strResult = Encoding.Unicode.GetString(arrData_a, intOffset_a + 2, intLength_a - 2);
				}
				else
				{
					strResult = Encoding.Unicode.GetString(arrData_a, intOffset_a, intLength_a);
				}
			}
			else if (bytEncoding_a == 0x02)
			{
				strResult = Encoding.BigEndianUnicode.GetString(arrData_a, intOffset_a, intLength_a);
			}
			else if (bytEncoding_a == 0x03)
			{
				strResult = Encoding.UTF8.GetString(arrData_a, intOffset_a, intLength_a);
			}
		}

		// v2.4 separates multiple values with a zero; later UTF-16 values carry their own BOM.
		strResult = strResult.TrimEnd('\0').Replace("\uFEFF", "").Replace("\uFFFE", "").Replace("\0", " / ").Trim();

		return strResult;
	}

	private static string latin1(byte[] arrData_a, int intOffset_a, int intLength_a)
	{
		return Encoding.Latin1.GetString(arrData_a, intOffset_a, intLength_a).Replace("\0", " ").Trim();
	}

	private static string yearOf(string strText_a)
	{
		string strResult = "";
		int intYear = 0;

		if (strText_a.Length >= 4 && int.TryParse(strText_a.Substring(0, 4), out intYear) && intYear > 0)
		{
			strResult = strText_a.Substring(0, 4);
		}

		return strResult;
	}

	// "(17)Rock" -> "Rock", "(17)" or "17" -> "Rock"
	private static string genreOf(string strText_a)
	{
		string strResult = strText_a;
		int intNumber = 0;

		if (strText_a.StartsWith("(") && strText_a.IndexOf(')') > 1)
		{
			int intClose = strText_a.IndexOf(')');
			string strInner = strText_a.Substring(1, intClose - 1);
			string strRest = strText_a.Substring(intClose + 1).Trim();

			if (strRest.Length > 0)
			{
				strResult = strRest;
			}
			else if (int.TryParse(strInner, out intNumber) && intNumber >= 0 && intNumber < g_arrGenres.Length)
			{
				strResult = g_arrGenres[intNumber];
			}
		}
		else if (int.TryParse(strText_a, out intNumber) && intNumber >= 0 && intNumber < g_arrGenres.Length)
		{
			strResult = g_arrGenres[intNumber];
		}

		return strResult;
	}

	private static byte[] undoUnsync(byte[] arrData_a)
	{
		List<byte> objResult = new List<byte>(arrData_a.Length);
		int intI = 0;

		for (intI = 0; intI < arrData_a.Length; intI++)
		{
			if (!(arrData_a[intI] == 0x00 && intI > 0 && arrData_a[intI - 1] == 0xFF))
			{
				objResult.Add(arrData_a[intI]);
			}
		}

		return objResult.ToArray();
	}

	private static int synchsafe(byte[] arrData_a, int intOffset_a)
	{
		return ((arrData_a[intOffset_a] & 0x7F) << 21) | ((arrData_a[intOffset_a + 1] & 0x7F) << 14) | ((arrData_a[intOffset_a + 2] & 0x7F) << 7) | (arrData_a[intOffset_a + 3] & 0x7F);
	}

	private static int bigEndian(byte[] arrData_a, int intOffset_a, int intBytes_a)
	{
		int intResult = 0;
		int intI = 0;

		for (intI = 0; intI < intBytes_a; intI++)
		{
			intResult = (intResult << 8) | arrData_a[intOffset_a + intI];
		}

		return intResult;
	}

	private static string firstOf(string strA_a, string strB_a)
	{
		return (strA_a != null && strA_a.Length > 0) ? strA_a : (strB_a ?? "");
	}
}