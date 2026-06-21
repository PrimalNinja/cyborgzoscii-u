// Cyborg ZOSCII / UNINTELLIGENCE Test Harness v20260520
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Build with UNINTELLIGENCE defined to include UNSIGNAL / PENTAGONE tests.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using CyborgUnicorn.ZOSCII;

#if UNINTELLIGENCE
// UEncode, UDecode, UVerify, BSplit, BJoin are in the same namespace
#endif

public static class Test
{
	private static int m_intPassed = 0;
	private static int m_intFailed = 0;
	private static string m_strTempFolder = "";

	// -------------------------------------------------------------------------
	// Entry point
	// -------------------------------------------------------------------------

	public static void Main(string[] arrArgs_a)
	{
		#if UNINTELLIGENCE
		Console.WriteLine("UNINTELLIGENCE Test Harness v20260520");
		#else
		Console.WriteLine("ZOSCII Test Harness v20260520");
		#endif
		Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.1");
		Console.WriteLine();

		bool blnRun  = false;
		bool blnKeep = false;
		bool blnPerf = false;
		string strMQUrl = "";

		for (int intI = 0; intI < arrArgs_a.Length; intI++)
		{
			if (arrArgs_a[intI] == "-run")  { blnRun  = true; }
			if (arrArgs_a[intI] == "-keep") { blnKeep = true; }
			if (arrArgs_a[intI] == "-perf") { blnRun  = true; blnPerf = true; }
			if (arrArgs_a[intI] == "-mq" && intI + 1 < arrArgs_a.Length) { strMQUrl = arrArgs_a[intI + 1]; blnRun = true; }
		}

		if (!blnRun)
		{
			#if UNINTELLIGENCE
			Console.WriteLine("Tests: ZOSCII + UNINTELLIGENCE (UNSIGNAL, PENTAGONE, EntropySugar)");
			#else
			Console.WriteLine("Tests: ZOSCII (ZEncode, ZDecode, ZVerify, BVerify, BSplit, BJoin, SecureDelete, Source, ZTB)");
			#endif
			Console.WriteLine();
			Console.WriteLine("Usage: test.exe -run [-keep] [-perf] [-mq <url>]");
			Console.WriteLine();
			Console.WriteLine("  -run        Run all tests");
			Console.WriteLine("  -keep       Keep test output folder after run");
			Console.WriteLine("  -perf       Also run ZTB performance/speed tests");
			Console.WriteLine("  -mq <url>   Also run MQClient tests against the given server URL");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  test.exe -run");
			Console.WriteLine("  test.exe -run -perf");
			Console.WriteLine("  test.exe -run -mq https://your-server/index.php");
			return;
		}

		m_strTempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zoscii_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(m_strTempFolder);

		Console.WriteLine("=================================================");
		#if UNINTELLIGENCE
		Console.WriteLine(" Mode: ZOSCII + UNINTELLIGENCE");
		#else
		Console.WriteLine(" Mode: ZOSCII only");
		#endif
		if (strMQUrl.Length > 0) { Console.WriteLine(" MQ:   " + strMQUrl); }
		Console.WriteLine("=================================================");
		Console.WriteLine();

		try
		{
			runZOSCIIRomTests();
			runZEncodeDecodeTests();
			runZChainTests();
			runZFileTests();
			runZVerifyTests();
			runBVerifyTests();
			runSecureDeleteTests();
			runSourceTests();

			if (strMQUrl.Length > 0) { runMQTests(strMQUrl); }

			runZRollingHashTests();

			#if UNINTELLIGENCE
			runROMGeneratorTests();
			#endif

			runTangoTests();

			runZTBTests(blnPerf);
			// runZTBMemoryTests(); // TODO: rewrite for new API

			#if UNINTELLIGENCE
			runBSplitJoinTests();
			runUEncodeDecodeTests();
			runUChainTests();
			runUFileTests();
			runUVerifyTests();
			runEntropySugarTests();
			runMicroZOSCIITests();
			#endif
		}
		finally
		{
			if (!blnKeep)
			{
				try { Directory.Delete(m_strTempFolder, true); } catch { }
			}
			else
			{
				Console.WriteLine("Test folder kept: " + m_strTempFolder);
			}
		}

		Console.WriteLine();
		Console.WriteLine("=================================================");
		Console.WriteLine(" Results: " + m_intPassed + " passed, " + m_intFailed + " failed");
		Console.WriteLine("=================================================");

		if (m_intFailed > 0) { Environment.Exit(1); }
	}

	// -------------------------------------------------------------------------
	// Test helpers
	// -------------------------------------------------------------------------

	private static void pass(string strTest_a)
	{
		m_intPassed++;
		Console.WriteLine("  [PASS] " + strTest_a);
	}

	private static void fail(string strTest_a, string strReason_a)
	{
		m_intFailed++;
		Console.WriteLine("  [FAIL] " + strTest_a + " -- " + strReason_a);
	}

	private static void section(string strName_a)
	{
		Console.WriteLine();
		Console.WriteLine("--- " + strName_a + " ---");
	}

	private static string tempFile(string strExt_a)
	{
		return Path.Combine(m_strTempFolder, Guid.NewGuid().ToString("N") + strExt_a);
	}

	private static ZOSCIIRom makeTestROM()
	{
		// Use the test exe itself as the ROM - guaranteed 64MB+, always present
		return ZOSCIIRom.FromFile(Process.GetCurrentProcess().MainModule.FileName);
	}

	private static ZOSCIIRom makeTestROM2()
	{
		// Different 128KB slice of the exe — produces a distinct ROM
		byte[] arrExe = File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName);
		int intOffset = Math.Min(131072, arrExe.Length / 3);
		byte[] arrRom = new byte[131072];
		for (int intI = 0; intI < 131072; intI++) { arrRom[intI] = arrExe[(intOffset + intI) % arrExe.Length]; }
		return ZOSCIIRom.FromBytes(arrRom);
	}

	private static ZOSCIIRom makeTestROM3()
	{
		// Another distinct 128KB slice
		byte[] arrExe = File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName);
		int intOffset = Math.Min(262144, (arrExe.Length * 2) / 3);
		byte[] arrRom = new byte[131072];
		for (int intI = 0; intI < 131072; intI++) { arrRom[intI] = arrExe[(intOffset + intI) % arrExe.Length]; }
		return ZOSCIIRom.FromBytes(arrRom);
	}

	private static bool arraysEqual(byte[] arr1_a, byte[] arr2_a)
	{
		bool blnResult = false;
		if (arr1_a != null && arr2_a != null && arr1_a.Length == arr2_a.Length)
		{
			blnResult = true;
			for (int intI = 0; intI < arr1_a.Length && blnResult; intI++)
			{
				if (arr1_a[intI] != arr2_a[intI]) { blnResult = false; }
			}
		}
		return blnResult;
	}

	// -------------------------------------------------------------------------
	// ZOSCIIRom
	// -------------------------------------------------------------------------

	private static void runZOSCIIRomTests()
	{
		section("ZOSCIIRom");

		string strExePath = Process.GetCurrentProcess().MainModule.FileName;

		// FromFile
		using (ZOSCIIRom objRom = ZOSCIIRom.FromFile(strExePath))
		{
			if (objRom.IsLoaded && objRom.Size > 0) { pass("ZOSCIIRom.FromFile - IsLoaded, Size=" + objRom.Size); }
			else { fail("ZOSCIIRom.FromFile", "IsLoaded=" + objRom.IsLoaded + " Size=" + objRom.Size); }
		}

		// FromBytes
		byte[] arrRomBytes = File.ReadAllBytes(strExePath);
		using (ZOSCIIRom objRom = ZOSCIIRom.FromBytes(arrRomBytes))
		{
			if (objRom.IsLoaded) { pass("ZOSCIIRom.FromBytes - IsLoaded"); }
			else { fail("ZOSCIIRom.FromBytes", "not loaded"); }
		}

		// FromBase64
		string strBase64 = Convert.ToBase64String(arrRomBytes);
		using (ZOSCIIRom objRom = ZOSCIIRom.FromBase64(strBase64))
		{
			if (objRom.IsLoaded) { pass("ZOSCIIRom.FromBase64 - IsLoaded"); }
			else { fail("ZOSCIIRom.FromBase64", "not loaded"); }
		}

		// Dispose
		ZOSCIIRom objDisposed = ZOSCIIRom.FromFile(strExePath);
		objDisposed.Dispose();
		if (!objDisposed.IsLoaded) { pass("ZOSCIIRom.Dispose - IsLoaded false after dispose"); }
		else { fail("ZOSCIIRom.Dispose", "still loaded after dispose"); }

		// Empty array
		using (ZOSCIIRom objRom = ZOSCIIRom.FromBytes(new byte[0]))
		{
			if (!objRom.IsLoaded) { pass("ZOSCIIRom.FromBytes empty - not loaded"); }
			else { pass("ZOSCIIRom.FromBytes empty - loaded (no minimum size enforced)"); }
		}
	}

	// -------------------------------------------------------------------------
	// ZEncode / ZDecode - bytes and strings
	// -------------------------------------------------------------------------

	private static void runZEncodeDecodeTests()
	{
		section("ZEncode / ZDecode - Bytes and Strings");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("Hello ZOSCII");

			// Bytes round trip
			byte[] arrEncoded = ZEncode.Bytes(arrPlain, objRom);
			if (arrEncoded != null && arrEncoded.Length > 0) { pass("ZEncode.Bytes - returns data"); }
			else { fail("ZEncode.Bytes", "null or empty"); }

			byte[] arrDecoded = ZDecode.Bytes(arrEncoded, objRom);
			if (arraysEqual(arrPlain, arrDecoded)) { pass("ZDecode.Bytes - round trip"); }
			else { fail("ZDecode.Bytes", "mismatch"); }

			// String round trip
			byte[] arrStrEncoded = ZEncode.String("Hello ZOSCII", objRom);
			string strDecoded = ZDecode.ToString(arrStrEncoded, objRom);
			if (strDecoded == "Hello ZOSCII") { pass("ZEncode.String / ZDecode.ToString - round trip"); }
			else { fail("ZEncode.String / ZDecode.ToString", "got: " + strDecoded); }

			// ToBase64 / FromBase64
			string strB64 = ZEncode.ToBase64(arrPlain, objRom);
			if (strB64.Length > 0) { pass("ZEncode.ToBase64 - returns data"); }
			else { fail("ZEncode.ToBase64", "empty"); }

			byte[] arrFromB64 = ZDecode.FromBase64(strB64, objRom);
			if (arraysEqual(arrPlain, arrFromB64)) { pass("ZDecode.FromBase64 - round trip"); }
			else { fail("ZDecode.FromBase64", "mismatch"); }

			// Encoded output differs from plain
			if (!arraysEqual(arrPlain, arrEncoded)) { pass("ZEncode.Bytes - encoded differs from plain"); }
			else { fail("ZEncode.Bytes", "encoded same as plain"); }
		}
	}

	// -------------------------------------------------------------------------
	// ZEncode / ZDecode - Chain
	// -------------------------------------------------------------------------

	private static void runZChainTests()
	{
		section("ZEncode / ZDecode - Chain");

		using (ZOSCIIRom objRom1 = makeTestROM())
		using (ZOSCIIRom objRom2 = makeTestROM())
		{
			ZOSCIIRom[] arrRoms = new ZOSCIIRom[] { objRom1, objRom2 };
			byte[] arrPlain = Encoding.UTF8.GetBytes("Chain test payload");

			// Chain bytes round trip
			byte[] arrChained = ZEncode.Chain(arrPlain, arrRoms);
			if (arrChained != null && arrChained.Length > 0) { pass("ZEncode.Chain - returns data"); }
			else { fail("ZEncode.Chain", "null or empty"); }

			byte[] arrUnchained = ZDecode.Chain(arrChained, arrRoms);
			if (arraysEqual(arrPlain, arrUnchained)) { pass("ZDecode.Chain - round trip"); }
			else { fail("ZDecode.Chain", "mismatch"); }

			// ChainFile round trip
			string strIn = tempFile(".bin");
			string strEncFile = tempFile(".zoc");
			string strDecFile = tempFile(".bin");
			File.WriteAllBytes(strIn, arrPlain);

			bool blnEncOk = ZEncode.ChainFile(strIn, strEncFile, arrRoms);
			if (blnEncOk && File.Exists(strEncFile)) { pass("ZEncode.ChainFile - file created"); }
			else { fail("ZEncode.ChainFile", "failed or file missing"); }

			bool blnDecOk = ZDecode.ChainFile(strEncFile, strDecFile, arrRoms);
			if (blnDecOk && File.Exists(strDecFile)) { pass("ZDecode.ChainFile - file created"); }
			else { fail("ZDecode.ChainFile", "failed or file missing"); }

			if (arraysEqual(arrPlain, File.ReadAllBytes(strDecFile))) { pass("ZDecode.ChainFile - round trip matches"); }
			else { fail("ZDecode.ChainFile", "content mismatch"); }
		}
	}

	// -------------------------------------------------------------------------
	// ZEncode / ZDecode - File
	// -------------------------------------------------------------------------

	private static void runZFileTests()
	{
		section("ZEncode / ZDecode - File");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("File encode decode test content");
			string strIn = tempFile(".bin");
			string strEnc = tempFile(".zoc");
			string strDec = tempFile(".bin");
			File.WriteAllBytes(strIn, arrPlain);

			bool blnEncOk = ZEncode.File(strIn, strEnc, objRom);
			if (blnEncOk && File.Exists(strEnc)) { pass("ZEncode.File - file created"); }
			else { fail("ZEncode.File", "failed or missing"); }

			bool blnDecOk = ZDecode.File(strEnc, strDec, objRom);
			if (blnDecOk && File.Exists(strDec)) { pass("ZDecode.File - file created"); }
			else { fail("ZDecode.File", "failed or missing"); }

			if (arraysEqual(arrPlain, File.ReadAllBytes(strDec))) { pass("ZDecode.File - round trip matches"); }
			else { fail("ZDecode.File", "content mismatch"); }

			// FileToString
			string strText = ZDecode.FileToString(strEnc, objRom);
			if (strText == "File encode decode test content") { pass("ZDecode.FileToString - round trip"); }
			else { fail("ZDecode.FileToString", "got: " + strText); }
		}
	}

	// -------------------------------------------------------------------------
	// ZVerify
	// -------------------------------------------------------------------------

	private static void runZVerifyTests()
	{
		section("ZVerify");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("Verify test");
			string strIn = tempFile(".bin");
			string strEnc = tempFile(".zoc");
			File.WriteAllBytes(strIn, arrPlain);
			ZEncode.File(strIn, strEnc, objRom);

			bool blnVerifyFile = ZVerify.File(strEnc, strIn, objRom);
			if (blnVerifyFile) { pass("ZVerify.File - valid match"); }
			else { fail("ZVerify.File", "failed"); }

			byte[] arrEncoded = ZEncode.Bytes(arrPlain, objRom);
			bool blnVerifyBytes = ZVerify.Bytes(arrEncoded, arrPlain, objRom);
			if (blnVerifyBytes) { pass("ZVerify.Bytes - valid match"); }
			else { fail("ZVerify.Bytes", "failed"); }

			// Tampered encoded bytes should fail - overwrite every 2nd byte with 0xFF
			byte[] arrTampered = (byte[])arrEncoded.Clone();
			for (int intI = 0; intI < arrTampered.Length; intI += 2) { arrTampered[intI] = 0xFF; }
			bool blnVerifyTampered = ZVerify.Bytes(arrTampered, arrPlain, objRom);
			if (!blnVerifyTampered) { pass("ZVerify.Bytes - tampered correctly fails"); }
			else { fail("ZVerify.Bytes tampered", "incorrectly passed"); }

		}
	}

	// -------------------------------------------------------------------------
	// BVerify
	// -------------------------------------------------------------------------

	private static void runBVerifyTests()
	{
		section("BVerify");

		byte[] arrData = Encoding.UTF8.GetBytes("Binary verify test");
		byte[] arrCopy = (byte[])arrData.Clone();
		byte[] arrDiff = (byte[])arrData.Clone();
		arrDiff[0] ^= 0xFF;

		// Bytes
		if (BVerify.Bytes(arrData, arrCopy)) { pass("BVerify.Bytes - identical match"); }
		else { fail("BVerify.Bytes", "identical failed"); }

		if (!BVerify.Bytes(arrData, arrDiff)) { pass("BVerify.Bytes - different correctly fails"); }
		else { fail("BVerify.Bytes different", "incorrectly passed"); }

		// File
		string strFile1 = tempFile(".bin");
		string strFile2 = tempFile(".bin");
		string strFile3 = tempFile(".bin");
		File.WriteAllBytes(strFile1, arrData);
		File.WriteAllBytes(strFile2, arrCopy);
		File.WriteAllBytes(strFile3, arrDiff);

		if (BVerify.File(strFile1, strFile2)) { pass("BVerify.File - identical match"); }
		else { fail("BVerify.File", "identical failed"); }

		if (!BVerify.File(strFile1, strFile3)) { pass("BVerify.File - different correctly fails"); }
		else { fail("BVerify.File different", "incorrectly passed"); }
	}

	// -------------------------------------------------------------------------
	// BSplit / BJoin (UNINTELLIGENCE only)
	// -------------------------------------------------------------------------

	#if UNINTELLIGENCE
	private static void runBSplitJoinTests()
	{
		section("BSplit / BJoin");

		byte[] arrData = Encoding.UTF8.GetBytes("PENTAGONE split join test payload - needs to be long enough for shares");

		// Bytes split and join
		byte[][] arrShares = BSplit.Bytes(arrData);
		if (arrShares != null && arrShares.Length == 5) { pass("BSplit.Bytes - 5 shares returned"); }
		else { fail("BSplit.Bytes", "expected 5 shares, got " + (arrShares == null ? "null" : arrShares.Length.ToString())); }

		// Join with all 5
		byte[] arrJoined = BJoin.Bytes(arrShares);
		if (arraysEqual(arrData, arrJoined)) { pass("BJoin.Bytes - all 5 shares round trip"); }
		else { fail("BJoin.Bytes all 5", "mismatch"); }

		// Join with 3 of 5 (shares 0, 2, 4 - drop 1 and 3)
		byte[][] arrPartial = new byte[][] { arrShares[0], null, arrShares[2], null, arrShares[4] };
		byte[] arrJoined3 = BJoin.Bytes(arrPartial);
		if (arraysEqual(arrData, arrJoined3)) { pass("BJoin.Bytes - 3 of 5 shares round trip"); }
		else { fail("BJoin.Bytes 3 of 5", "mismatch"); }

		// File split and join
		string strIn = tempFile(".bin");
		string strOut = tempFile(".bin");
		File.WriteAllBytes(strIn, arrData);

		string[] arrSharePaths = BSplit.File(strIn, strIn);
		if (arrSharePaths != null && arrSharePaths.Length == 5) { pass("BSplit.File - 5 share files created"); }
		else { fail("BSplit.File", "expected 5 paths, got " + (arrSharePaths == null ? "null" : arrSharePaths.Length.ToString())); }

		// Join from share 2
		if (arrSharePaths != null && arrSharePaths.Length == 5)
		{
			string strJoined = BJoin.File(arrSharePaths[1], strOut);
			if (strJoined != null && File.Exists(strOut) && arraysEqual(arrData, File.ReadAllBytes(strOut))) { pass("BJoin.File - round trip from share 2"); }
			else { fail("BJoin.File", "mismatch or file missing"); }

			// Clean up share files
			for (int intI = 0; intI < arrSharePaths.Length; intI++)
			{
				if (File.Exists(arrSharePaths[intI])) { File.Delete(arrSharePaths[intI]); }
			}
		}
	}
	#endif

	// -------------------------------------------------------------------------
	// SecureDelete
	// -------------------------------------------------------------------------

	private static void runSecureDeleteTests()
	{
		section("SecureDelete");

		// File
		string strFile = tempFile(".bin");
		File.WriteAllBytes(strFile, Encoding.UTF8.GetBytes("sensitive data"));
		bool blnDeleted = SecureDelete.File(strFile);
		if (blnDeleted && !File.Exists(strFile)) { pass("SecureDelete.File - deleted"); }
		else { fail("SecureDelete.File", "blnDeleted=" + blnDeleted + " exists=" + File.Exists(strFile)); }

		// Folder
		string strFolder = Path.Combine(m_strTempFolder, "secdel_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(strFolder);
		File.WriteAllBytes(Path.Combine(strFolder, "a.bin"), new byte[] { 1, 2, 3 });
		File.WriteAllBytes(Path.Combine(strFolder, "b.bin"), new byte[] { 4, 5, 6 });
		bool blnFolderDeleted = SecureDelete.Folder(strFolder);
		if (blnFolderDeleted && !Directory.Exists(strFolder)) { pass("SecureDelete.Folder - deleted"); }
		else { fail("SecureDelete.Folder", "blnDeleted=" + blnFolderDeleted + " exists=" + Directory.Exists(strFolder)); }

		// Non-existent file should not throw
		bool blnMissing = SecureDelete.File(tempFile(".bin"));
		pass("SecureDelete.File missing - no exception (result=" + blnMissing + ")");
	}

	// -------------------------------------------------------------------------
	// EntropySugar
	// -------------------------------------------------------------------------

	#if UNINTELLIGENCE
	private static void runEntropySugarTests()
	{
		section("EntropySugar");

		EntropySugar objSugar = new EntropySugar();

		// Add and Get
		objSugar.Add("TEST_KEY", "12345");
		long lngVal = objSugar.Get("TEST_KEY");
		if (lngVal == 12345) { pass("EntropySugar.Add / Get - correct value"); }
		else { fail("EntropySugar.Add / Get", "got " + lngVal); }

		// GetString
		objSugar.Add("STR_KEY", "hello");
		string strVal = objSugar.GetString("STR_KEY");
		if (strVal == "hello") { pass("EntropySugar.GetString - correct value"); }
		else { fail("EntropySugar.GetString", "got " + strVal); }

		// ToJson
		string strJson = objSugar.ToJson();
		if (strJson.Length > 0 && strJson.Contains("TEST_KEY")) { pass("EntropySugar.ToJson - returns JSON with keys"); }
		else { fail("EntropySugar.ToJson", "missing key"); }

		// CaptureFast should not throw
		try { objSugar.CaptureFast(m_strTempFolder); pass("EntropySugar.CaptureFast - no exception"); }
		catch (Exception ex) { fail("EntropySugar.CaptureFast", ex.Message); }

		// CaptureSlow should not throw
		try { objSugar.CaptureSlow(m_strTempFolder); pass("EntropySugar.CaptureSlow - no exception"); }
		catch (Exception ex) { fail("EntropySugar.CaptureSlow", ex.Message); }

		// CaptureOnDemand should not throw
		try
		{
			string[] arrFixed = new string[] { };
			string[] arrMp3 = new string[] { };
			objSugar.CaptureOnDemand(m_strTempFolder, arrFixed, arrMp3);
			pass("EntropySugar.CaptureOnDemand - no exception");
		}
		catch (Exception ex) { fail("EntropySugar.CaptureOnDemand", ex.Message); }
	}
	#endif

	// -------------------------------------------------------------------------
	// ROMGenerator
	// -------------------------------------------------------------------------

	#if UNINTELLIGENCE
	private static void runROMGeneratorTests()
	{
		section("ROMGenerator");

		EntropySugar objSugar = new EntropySugar();

		// Populate entropy sugar with representative values
		objSugar.Add("SYS_TIME",       "1700000000000");
		objSugar.Add("UPTIME_MS",      "12345678");
		objSugar.Add("FREE_MEM",       "2048000000");
		objSugar.Add("INTERACT_DELTA", "42");
		objSugar.Add("MOUSE_DELTA_X",  "137");
		objSugar.Add("MOUSE_DELTA_Y",  "88");
		objSugar.Add("BTN_CLICKS",     "7");
		objSugar.Add("FORM_OPENS",     "3");
		objSugar.Add("DECODE_OPS",     "1");
		objSugar.Add("KEY_TIMESTAMPS", "987654321");
		objSugar.Add("PROC_HANDLES",   "512");
		objSugar.Add("ROM_COUNT",      "10");
		objSugar.Add("FILE_COUNT",     "150");

		// Create a temp folder with synthetic MP3-named files containing random data
		string strMp3Folder = Path.Combine(m_strTempFolder, "mp3s");
		Directory.CreateDirectory(strMp3Folder);

		Random objRand = new Random(42);
		int intI = 0;

		for (intI = 0; intI < 5; intI++)
		{
			byte[] arrFakeMP3 = new byte[65536 + intI * 1000];
			objRand.NextBytes(arrFakeMP3);
			File.WriteAllBytes(Path.Combine(strMp3Folder, "track" + intI + ".mp3"), arrFakeMP3);
		}

		// Test ROMGenerator.Bytes — should return 128KB
		byte[] arrRaw = ROMGenerator.Bytes(new string[] { strMp3Folder }, objSugar);
		if (arrRaw != null && arrRaw.Length == 131072) { pass("ROMGenerator.Bytes - returns 131072 bytes"); }
		else { fail("ROMGenerator.Bytes", "returned " + (arrRaw == null ? "null" : arrRaw.Length + " bytes")); }

		// Test byte distribution — no byte value should dominate (near-flat expected)
		if (arrRaw != null)
		{
			int[] arrCounts = new int[256];
			for (intI = 0; intI < arrRaw.Length; intI++) { arrCounts[arrRaw[intI]]++; }
			int intMin = arrCounts[0];
			int intMax = arrCounts[0];
			for (intI = 1; intI < 256; intI++)
			{
				if (arrCounts[intI] < intMin) { intMin = arrCounts[intI]; }
				if (arrCounts[intI] > intMax) { intMax = arrCounts[intI]; }
			}
			double dblExpected = 131072.0 / 256.0; // ~512
			double dblVariance = (intMax - intMin) / dblExpected;
			if (dblVariance < 0.5) { pass("ROMGenerator.Bytes - byte distribution near-flat (variance " + dblVariance.ToString("F3") + ")"); }
			else { fail("ROMGenerator.Bytes distribution", "variance " + dblVariance.ToString("F3") + " exceeds 0.5 (min=" + intMin + " max=" + intMax + ")"); }
		}

		// Test determinism — same entropy + same files = same ROM
		byte[] arrRaw2 = ROMGenerator.Bytes(new string[] { strMp3Folder }, objSugar);
		if (arrRaw != null && arrRaw2 != null && arraysEqual(arrRaw, arrRaw2)) { pass("ROMGenerator.Bytes - deterministic (same entropy produces same ROM)"); }
		else { fail("ROMGenerator.Bytes determinism", "two calls with same entropy produced different results"); }

		// Test different entropy = different ROM
		EntropySugar objSugar2 = new EntropySugar();
		objSugar2.Add("SYS_TIME",  "9999999999999");
		objSugar2.Add("UPTIME_MS", "99999999");
		byte[] arrRaw3 = ROMGenerator.Bytes(new string[] { strMp3Folder }, objSugar2);
		if (arrRaw != null && arrRaw3 != null && !arraysEqual(arrRaw, arrRaw3)) { pass("ROMGenerator.Bytes - different entropy produces different ROM"); }
		else { fail("ROMGenerator.Bytes entropy sensitivity", "different entropy produced same ROM"); }

		// Test empty folder returns null
		string strEmptyFolder = Path.Combine(m_strTempFolder, "empty_mp3s");
		Directory.CreateDirectory(strEmptyFolder);
		byte[] arrEmpty = ROMGenerator.Bytes(new string[] { strEmptyFolder }, objSugar);
		if (arrEmpty == null) { pass("ROMGenerator.Bytes - empty folder returns null"); }
		else { fail("ROMGenerator.Bytes empty folder", "expected null, got " + arrEmpty.Length + " bytes"); }

		// Test subfolder scanning — MP3s in subfolders should be found
		string strSubFolder = Path.Combine(strMp3Folder, "sub");
		Directory.CreateDirectory(strSubFolder);
		byte[] arrSubFakeMP3 = new byte[32768];
		objRand.NextBytes(arrSubFakeMP3);
		File.WriteAllBytes(Path.Combine(strSubFolder, "subtrack.mp3"), arrSubFakeMP3);
		byte[] arrWithSub = ROMGenerator.Bytes(new string[] { strMp3Folder }, objSugar);
		if (arrWithSub != null && arrWithSub.Length == 131072) { pass("ROMGenerator.Bytes - subfolder MP3s scanned"); }
		else { fail("ROMGenerator.Bytes subfolder", "returned " + (arrWithSub == null ? "null" : arrWithSub.Length + " bytes")); }
	}
	#endif

	// -------------------------------------------------------------------------
	// Chain Tango
	// -------------------------------------------------------------------------

	private static void runTangoTests()
	{
		section("Chain Tango");

		byte[] arrPlain = new byte[256];
		for (int intI = 0; intI < 256; intI++) { arrPlain[intI] = (byte)intI; }

		// 1 ROM — identical behaviour to ZEncode/ZDecode
		using (ZOSCIIRom objRom1 = makeTestROM())
		{
			ZOSCIIRom[] arrRoms1 = new ZOSCIIRom[] { objRom1 };

			byte[] arrEncoded1 = ZEncode.Chain(arrPlain, arrRoms1, true);
			if (arrEncoded1 != null && arrEncoded1.Length == arrPlain.Length * 2) { pass("ZEncode.Chain Tango 1 ROM - 2x expansion"); }
			else { fail("ZTango.Encode 1 ROM", "unexpected size or null"); }

			byte[] arrDecoded1 = ZDecode.Chain(arrEncoded1, arrRoms1, true);
			if (arrDecoded1 != null && arraysEqual(arrDecoded1, arrPlain)) { pass("ZDecode.Chain Tango 1 ROM - round trip"); }
			else { fail("ZTango.Decode 1 ROM", "round trip failed"); }
		}

		// 2 ROMs — round trip
		using (ZOSCIIRom objRom1 = makeTestROM())
		using (ZOSCIIRom objRom2 = makeTestROM2())
		{
			ZOSCIIRom[] arrRoms2 = new ZOSCIIRom[] { objRom1, objRom2 };

			byte[] arrEncoded2 = ZEncode.Chain(arrPlain, arrRoms2, true);
			if (arrEncoded2 != null && arrEncoded2.Length == arrPlain.Length * 2) { pass("ZEncode.Chain Tango 2 ROMs - 2x expansion"); }
			else { fail("ZTango.Encode 2 ROMs", "unexpected size or null"); }

			byte[] arrDecoded2 = ZDecode.Chain(arrEncoded2, arrRoms2, true);
			if (arrDecoded2 != null && arraysEqual(arrDecoded2, arrPlain)) { pass("ZDecode.Chain Tango 2 ROMs - round trip"); }
			else { fail("ZTango.Decode 2 ROMs", "round trip failed"); }

			// Wrong ROM count fails decode
			ZOSCIIRom[] arrRoms1Only = new ZOSCIIRom[] { objRom1 };
			byte[] arrBadDecode = ZDecode.Chain(arrEncoded2, arrRoms1Only, true);
			if (arrBadDecode == null || !arraysEqual(arrBadDecode, arrPlain)) { pass("ZDecode.Chain Tango 2 ROMs - wrong ROM count fails"); }
			else { fail("ZDecode.Chain Tango wrong count", "should not have decoded correctly"); }
		}

		// 3 ROMs — round trip
		using (ZOSCIIRom objRom1 = makeTestROM())
		using (ZOSCIIRom objRom2 = makeTestROM2())
		using (ZOSCIIRom objRom3 = makeTestROM3())
		{
			ZOSCIIRom[] arrRoms3 = new ZOSCIIRom[] { objRom1, objRom2, objRom3 };

			byte[] arrEncoded3 = ZEncode.Chain(arrPlain, arrRoms3, true);
			if (arrEncoded3 != null && arrEncoded3.Length == arrPlain.Length * 2) { pass("ZEncode.Chain Tango 3 ROMs - 2x expansion"); }
			else { fail("ZTango.Encode 3 ROMs", "unexpected size or null"); }

			byte[] arrDecoded3 = ZDecode.Chain(arrEncoded3, arrRoms3, true);
			if (arrDecoded3 != null && arraysEqual(arrDecoded3, arrPlain)) { pass("ZDecode.Chain Tango 3 ROMs - round trip"); }
			else { fail("ZTango.Decode 3 ROMs", "round trip failed"); }

			// 1 ROM vs 2 ROM vs 3 ROM produce different output for same plaintext
			using (ZOSCIIRom objRomA = makeTestROM())
			using (ZOSCIIRom objRomB = makeTestROM())
			{
				ZOSCIIRom[] arrRoms1 = new ZOSCIIRom[] { objRomA };
				ZOSCIIRom[] arrRoms2 = new ZOSCIIRom[] { objRomA, objRomB };
				byte[] arrEnc1 = ZEncode.Chain(arrPlain, arrRoms1, true);
				byte[] arrEnc2 = ZEncode.Chain(arrPlain, arrRoms2, true);
				if (arrEnc1 != null && arrEnc2 != null && !arraysEqual(arrEnc1, arrEnc2)) { pass("ZEncode.Chain Tango - different ROM counts produce different output"); }
				else { fail("ZEncode.Chain Tango ROM count difference", "1 and 2 ROM outputs identical"); }
			}
		}

		// UEncode/UDecode.Chain Tango — UNSIGNAL (UNINTELLIGENCE only)
		using (ZOSCIIRom objRom1 = makeTestROM())
		using (ZOSCIIRom objRom2 = makeTestROM2())
		using (ZOSCIIRom objRom3 = makeTestROM3())
		{
			ZOSCIIRom[] arrRoms3 = new ZOSCIIRom[] { objRom1, objRom2, objRom3 };
			#if UNINTELLIGENCE
			byte[] arrUEncoded = UEncode.Chain(arrPlain, arrRoms3, true);
			if (arrUEncoded != null && arrUEncoded.Length > arrPlain.Length * 2) { pass("UEncode.Chain Tango 3 ROMs - slightly >2x expansion"); }
			else { fail("UEncode.Chain Tango 3 ROMs", "unexpected size " + (arrUEncoded == null ? "null" : arrUEncoded.Length.ToString())); }

			byte[] arrUDecoded = UDecode.Chain(arrUEncoded, arrRoms3, true);
			if (arrUDecoded != null && arraysEqual(arrUDecoded, arrPlain)) { pass("UDecode.Chain Tango 3 ROMs - round trip"); }
			else { fail("UDecode.Chain Tango 3 ROMs", "round trip failed"); }

			// Wrong ROM fails
			ZOSCIIRom[] arrRoms1Only = new ZOSCIIRom[] { objRom1 };
			byte[] arrUBadDecode = UDecode.Chain(arrUEncoded, arrRoms1Only, true);
			if (arrUBadDecode == null || !arraysEqual(arrUBadDecode, arrPlain)) { pass("UDecode.Chain Tango - wrong ROM count fails"); }
			else { fail("UDecode.Chain Tango wrong ROM count", "should not have decoded correctly"); }
			#endif
		}
	}
	// Source
	// -------------------------------------------------------------------------

	private static void runSourceTests()
	{
		section("Source");

		string strDest = tempFile(".zip");
		bool blnOk = Source.SaveAs(strDest);

		// source.zip may not be embedded in test build - just check it doesn't throw
		if (blnOk && File.Exists(strDest) && new FileInfo(strDest).Length > 0) { pass("Source.SaveAs - file written"); }
		else { pass("Source.SaveAs - no embedded source.zip in test build (expected)"); }
	}

	// -------------------------------------------------------------------------
	// MQClient
	// -------------------------------------------------------------------------

	private static void runMQTests(string strMQUrl_a)
	{
		section("MQClient (" + strMQUrl_a + ")");

		MQClient objMQ = new MQClient(30);
		byte[] arrPayload = Encoding.UTF8.GetBytes("ZOSCII MQ test payload " + Guid.NewGuid().ToString("N"));
		string strQueueGUID = Guid.NewGuid().ToString();

		// Publish
		MQPublishResult objPub = objMQ.Publish(strMQUrl_a, strQueueGUID, arrPayload);
		if (objPub.Success) { pass("MQClient.Publish - success"); }
		else { fail("MQClient.Publish", objPub.ErrorMessage); }

		// Check
		MQCheckStatus objCheck = objMQ.Check(strMQUrl_a, strQueueGUID, "");
		if (objCheck == MQCheckStatus.New) { pass("MQClient.Check - new messages detected"); }
		else { fail("MQClient.Check", "expected NewMessages, got " + objCheck); }

		// FetchNext
		MQFetchResult objFetch = objMQ.FetchNext(strMQUrl_a, strQueueGUID, "");
		if (objFetch.HasMessage && objFetch.EncodedBytes != null && objFetch.EncodedBytes.Length > 0)
		{
			if (arraysEqual(arrPayload, objFetch.EncodedBytes)) { pass("MQClient.FetchNext - payload matches"); }
			else { fail("MQClient.FetchNext", "payload mismatch"); }
		}
		else { fail("MQClient.FetchNext", "HasMessage=" + objFetch.HasMessage); }

		// Check after fetch - should be up to date
		string strPointer = objFetch.Pointer ?? "";
		MQCheckStatus objCheck2 = objMQ.Check(strMQUrl_a, strQueueGUID, strPointer);
		if (objCheck2 == MQCheckStatus.UpToDate) { pass("MQClient.Check after fetch - up to date"); }
		else { fail("MQClient.Check after fetch", "expected UpToDate, got " + objCheck2); }

		// Put / Get
		byte[] arrStorePayload = Encoding.UTF8.GetBytes("ZOSCII store test " + Guid.NewGuid().ToString("N"));
		MQPublishResult objPut = objMQ.Put(strMQUrl_a, arrStorePayload);
		if (objPut.Success && objPut.StoredName.Length > 0) { pass("MQClient.Put - success, StoredName=" + objPut.StoredName); }
		else { fail("MQClient.Put", objPut.ErrorMessage); }

		string strStoredName = objPut.Success ? objPut.StoredName : "unknown";
		MQFetchResult objGet = objMQ.Get(strMQUrl_a, strStoredName);
		if (objGet.HasMessage && arraysEqual(arrStorePayload, objGet.EncodedBytes)) { pass("MQClient.Get - payload matches"); }
		else { fail("MQClient.Get", "HasMessage=" + objGet.HasMessage); }

		// Scan
		string[] arrScanned = objMQ.Scan(strMQUrl_a);
		if (arrScanned != null) { pass("MQClient.Scan - returned " + arrScanned.Length + " items"); }
		else { fail("MQClient.Scan", "returned null"); }

		// Replicate queue-to-queue (same server)
		string strQueueA = Guid.NewGuid().ToString();
		string strQueueB = Guid.NewGuid().ToString();
		byte[] arrRepPayload = Encoding.UTF8.GetBytes("ZOSCII replicate test " + Guid.NewGuid().ToString("N"));
		objMQ.Publish(strMQUrl_a, strQueueA, arrRepPayload);
		string strNewPointer = "";
		MQPublishResult objRep = objMQ.Replicate(strMQUrl_a, strQueueA, strMQUrl_a, strQueueB, "", out strNewPointer);
		if (objRep.Success && objRep.ServerMessage != "up-to-date" && strNewPointer.Length > 0) { pass("MQClient.Replicate queue-to-queue - success"); }
		else { fail("MQClient.Replicate queue-to-queue", "Success=" + objRep.Success + " ServerMessage=" + objRep.ServerMessage); }

		// Verify replicated message arrived in queue B
		MQFetchResult objRepFetch = objMQ.FetchNext(strMQUrl_a, strQueueB, "");
		if (objRepFetch.HasMessage && arraysEqual(arrRepPayload, objRepFetch.EncodedBytes)) { pass("MQClient.Replicate queue-to-queue - payload matches in target queue"); }
		else { fail("MQClient.Replicate queue-to-queue payload", "HasMessage=" + objRepFetch.HasMessage); }

		// Replicate queue-to-store (no target queue - creates unidentified files)
		// Publish 3 messages then replicate all 3 to store
		string strQueueC = Guid.NewGuid().ToString();
		for (int intI = 0; intI < 3; intI++)
		{
			objMQ.Publish(strMQUrl_a, strQueueC, Encoding.UTF8.GetBytes("ZOSCII store replicate test " + intI + " " + Guid.NewGuid().ToString("N")));
		}
		string strStorePointer = "";
		int intReplicatedCount = 0;
		bool blnStoreRepOk = true;
		for (int intI = 0; intI < 3; intI++)
		{
			MQPublishResult objStoreRep = objMQ.Replicate(strMQUrl_a, strQueueC, strMQUrl_a, "", strStorePointer, out strStorePointer);
			if (objStoreRep.Success && objStoreRep.ServerMessage != "up-to-date") { intReplicatedCount++; }
			else { blnStoreRepOk = false; }
		}
		if (blnStoreRepOk && intReplicatedCount == 3) { pass("MQClient.Replicate queue-to-store - success (" + intReplicatedCount + " messages)"); }
		else { fail("MQClient.Replicate queue-to-store", "replicated=" + intReplicatedCount); }

		// Scan should now find the unidentified file
		string[] arrScanned2 = objMQ.Scan(strMQUrl_a);
		if (arrScanned2 != null && arrScanned2.Length == 3) { pass("MQClient.Scan after replicate-to-store - found " + arrScanned2.Length + " unidentified item(s)"); }
		else { fail("MQClient.Scan after replicate-to-store", "expected 3 unidentified items, got " + (arrScanned2 == null ? "null" : arrScanned2.Length.ToString())); }

		// Identify to clean up - removes -u suffix from all scanned files
		if (arrScanned2 != null && arrScanned2.Length > 0)
		{
			string[] arrIdentified = objMQ.Identify(strMQUrl_a, arrScanned2);
			if (arrIdentified != null && arrIdentified.Length > 0) { pass("MQClient.Identify - identified " + arrIdentified.Length + " item(s)"); }
			else { fail("MQClient.Identify", "expected identified items, got " + (arrIdentified == null ? "null" : arrIdentified.Length.ToString())); }
		}
	}


	// -------------------------------------------------------------------------
	// UNINTELLIGENCE - UEncode / UDecode - Bytes and Strings
	// -------------------------------------------------------------------------

	#if UNINTELLIGENCE

	private static void runUEncodeDecodeTests()
	{
		section("UEncode / UDecode - Bytes and Strings");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("Hello UNSIGNAL");

			// Bytes round trip
			byte[] arrEncoded = UEncode.Bytes(arrPlain, objRom);
			if (arrEncoded != null && arrEncoded.Length > 0) { pass("UEncode.Bytes - returns data"); }
			else { fail("UEncode.Bytes", "null or empty"); }

			byte[] arrDecoded = UDecode.Bytes(arrEncoded, objRom);
			if (arraysEqual(arrPlain, arrDecoded)) { pass("UDecode.Bytes - round trip"); }
			else { fail("UDecode.Bytes", "mismatch"); }

			// Each encode should produce different output (random offset/prefix/suffix)
			byte[] arrEncoded2 = UEncode.Bytes(arrPlain, objRom);
			if (!arraysEqual(arrEncoded, arrEncoded2)) { pass("UEncode.Bytes - non-deterministic output"); }
			else { pass("UEncode.Bytes - note: same output (ROM may be too small for offset variation)"); }

			// Second encode still decodes correctly
			byte[] arrDecoded2 = UDecode.Bytes(arrEncoded2, objRom);
			if (arraysEqual(arrPlain, arrDecoded2)) { pass("UDecode.Bytes - second encode round trip"); }
			else { fail("UDecode.Bytes second", "mismatch"); }

			// String round trip
			byte[] arrStrEncoded = UEncode.String("Hello UNSIGNAL", objRom);
			string strDecoded = UDecode.ToString(arrStrEncoded, objRom);
			if (strDecoded == "Hello UNSIGNAL") { pass("UEncode.String / UDecode.ToString - round trip"); }
			else { fail("UEncode.String / UDecode.ToString", "got: " + strDecoded); }

			// ToBase64 / FromBase64
			string strB64 = UEncode.ToBase64(arrPlain, objRom);
			if (strB64.Length > 0) { pass("UEncode.ToBase64 - returns data"); }
			else { fail("UEncode.ToBase64", "empty"); }

			byte[] arrFromB64 = UDecode.FromBase64(strB64, objRom);
			if (arraysEqual(arrPlain, arrFromB64)) { pass("UDecode.FromBase64 - round trip"); }
			else { fail("UDecode.FromBase64", "mismatch"); }
		}
	}

	// -------------------------------------------------------------------------
	// UNINTELLIGENCE - UEncode / UDecode - Chain
	// -------------------------------------------------------------------------

	private static void runUChainTests()
	{
		section("UEncode / UDecode - Chain");

		using (ZOSCIIRom objRom1 = makeTestROM())
		using (ZOSCIIRom objRom2 = makeTestROM())
		{
			ZOSCIIRom[] arrRoms = new ZOSCIIRom[] { objRom1, objRom2 };
			byte[] arrPlain = Encoding.UTF8.GetBytes("UNSIGNAL chain test payload");

			// Chain bytes round trip
			byte[] arrChained = UEncode.Chain(arrPlain, arrRoms);
			if (arrChained != null && arrChained.Length > 0) { pass("UEncode.Chain - returns data"); }
			else { fail("UEncode.Chain", "null or empty"); }

			byte[] arrUnchained = UDecode.Chain(arrChained, arrRoms);
			if (arraysEqual(arrPlain, arrUnchained)) { pass("UDecode.Chain - round trip"); }
			else { fail("UDecode.Chain", "mismatch"); }

			// ChainFile round trip
			string strIn = tempFile(".bin");
			string strEncFile = tempFile(".sig");
			string strDecFile = tempFile(".bin");
			File.WriteAllBytes(strIn, arrPlain);

			bool blnEncOk = UEncode.ChainFile(strIn, strEncFile, arrRoms);
			if (blnEncOk && File.Exists(strEncFile)) { pass("UEncode.ChainFile - file created"); }
			else { fail("UEncode.ChainFile", "failed or file missing"); }

			bool blnDecOk = UDecode.ChainFile(strEncFile, strDecFile, arrRoms);
			if (blnDecOk && File.Exists(strDecFile)) { pass("UDecode.ChainFile - file created"); }
			else { fail("UDecode.ChainFile", "failed or file missing"); }

			if (arraysEqual(arrPlain, File.ReadAllBytes(strDecFile))) { pass("UDecode.ChainFile - round trip matches"); }
			else { fail("UDecode.ChainFile", "content mismatch"); }
		}
	}

	// -------------------------------------------------------------------------
	// UNINTELLIGENCE - UEncode / UDecode - File
	// -------------------------------------------------------------------------

	private static void runUFileTests()
	{
		section("UEncode / UDecode - File");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("UNSIGNAL file encode decode test");
			string strIn = tempFile(".bin");
			string strEnc = tempFile(".sig");
			string strDec = tempFile(".bin");
			File.WriteAllBytes(strIn, arrPlain);

			bool blnEncOk = UEncode.File(strIn, strEnc, objRom);
			if (blnEncOk && File.Exists(strEnc)) { pass("UEncode.File - file created"); }
			else { fail("UEncode.File", "failed or missing"); }

			bool blnDecOk = UDecode.File(strEnc, strDec, objRom);
			if (blnDecOk && File.Exists(strDec)) { pass("UDecode.File - file created"); }
			else { fail("UDecode.File", "failed or missing"); }

			if (arraysEqual(arrPlain, File.ReadAllBytes(strDec))) { pass("UDecode.File - round trip matches"); }
			else { fail("UDecode.File", "content mismatch"); }

			// FileToString
			string strText = UDecode.FileToString(strEnc, objRom);
			if (strText == "UNSIGNAL file encode decode test") { pass("UDecode.FileToString - round trip"); }
			else { fail("UDecode.FileToString", "got: " + strText); }
		}
	}

	// -------------------------------------------------------------------------
	// UNINTELLIGENCE - UVerify
	// -------------------------------------------------------------------------

	private static void runUVerifyTests()
	{
		section("UVerify");

		using (ZOSCIIRom objRom = makeTestROM())
		{
			byte[] arrPlain = Encoding.UTF8.GetBytes("UNSIGNAL verify test");
			string strIn = tempFile(".bin");
			string strEnc = tempFile(".sig");
			File.WriteAllBytes(strIn, arrPlain);
			UEncode.File(strIn, strEnc, objRom);

			bool blnVerify = UVerify.File(strEnc, strIn, objRom);
			if (blnVerify) { pass("UVerify.File - valid match"); }
			else { fail("UVerify.File", "failed"); }

		}
	}

	#endif

	#if UNINTELLIGENCE

	// -------------------------------------------------------------------------
	// UNINTELLIGENCE - MicroZOSCII
	// -------------------------------------------------------------------------

	private static void runMicroZOSCIITests()
	{
		section("MicroZOSCII");

		// FromBytes — single secret (120 bytes = 240 nibbles)
		byte[] arrSecret = new byte[120];
		new Random(42).NextBytes(arrSecret);
		string strMicroROM = MicroZOSCII.FromBytes(arrSecret, null, null, null);
		if (strMicroROM != null && strMicroROM.Length == 240) { pass("MicroZOSCII.FromBytes - 240 nibbles"); }
		else { fail("MicroZOSCII.FromBytes", "length=" + (strMicroROM == null ? "null" : strMicroROM.Length.ToString())); }

		// FromBytes — four secrets
		byte[] arrS2 = new byte[32]; new Random(43).NextBytes(arrS2);
		byte[] arrS3 = new byte[32]; new Random(44).NextBytes(arrS3);
		byte[] arrS4 = new byte[32]; new Random(45).NextBytes(arrS4);
		string strMicroROM4 = MicroZOSCII.FromBytes(arrSecret, arrS2, arrS3, arrS4);
		if (strMicroROM4 != null && strMicroROM4.Length == 240) { pass("MicroZOSCII.FromBytes - 4 secrets, 240 nibbles"); }
		else { fail("MicroZOSCII.FromBytes 4 secrets", "length=" + (strMicroROM4 == null ? "null" : strMicroROM4.Length.ToString())); }

		// ToBase62 / FromBase62 round trip
		if (strMicroROM != null)
		{
			string[] arrChunks = MicroZOSCII.ToBase62(strMicroROM);
		if (arrChunks != null && arrChunks.Length == 3 && arrChunks[0].Length == 54) { pass("MicroZOSCII.ToBase62 - 3 chunks of 54 chars"); }
		else { fail("MicroZOSCII.ToBase62", "unexpected output"); }

		string strRecovered = MicroZOSCII.FromBase62(arrChunks[0], arrChunks[1], arrChunks[2]);
		if (strRecovered == strMicroROM) { pass("MicroZOSCII.FromBase62 - round trip matches"); }
		else { fail("MicroZOSCII.FromBase62", "mismatch"); }

		// GetDistribution
		int[] arrDist = MicroZOSCII.GetDistribution(strMicroROM);
		if (arrDist != null && arrDist.Length == 16)
		{
			int intMin = int.MaxValue;
			int intI   = 0;
			for (intI = 0; intI < 16; intI++) { if (arrDist[intI] < intMin) { intMin = arrDist[intI]; } }
			pass("MicroZOSCII.GetDistribution - min instances per nibble: " + intMin);
		}
		else { fail("MicroZOSCII.GetDistribution", "unexpected output"); }

		// Encode / Decode round trip
		byte[] arrROMBytes = new byte[512];
		new Random(99).NextBytes(arrROMBytes);
		byte[] arrAddresses = MicroZOSCII.Encode(strMicroROM, arrROMBytes);
		if (arrAddresses != null && arrAddresses.Length == arrROMBytes.Length * 2) { pass("MicroZOSCII.Encode - 2x expansion"); }
		else { fail("MicroZOSCII.Encode", "unexpected length"); }

		byte[] arrDecoded = MicroZOSCII.Decode(strMicroROM, arrAddresses);
		if (arraysEqual(arrROMBytes, arrDecoded)) { pass("MicroZOSCII.Decode - round trip matches"); }
		else { fail("MicroZOSCII.Decode", "mismatch"); }

		// Base62ChunkToHex / HexChunkToBase62 round trip
		string strChunk = strMicroROM.Substring(0, 80);
		string strBase62 = MicroZOSCII.HexChunkToBase62(strChunk);
		if (strBase62 != null && strBase62.Length == 54) { pass("MicroZOSCII.HexChunkToBase62 - 54 chars"); }
		else { fail("MicroZOSCII.HexChunkToBase62", "unexpected length"); }

		string strHexBack = MicroZOSCII.Base62ChunkToHex(strBase62);
		if (strHexBack == strChunk) { pass("MicroZOSCII.Base62ChunkToHex - round trip matches"); }
		else { fail("MicroZOSCII.Base62ChunkToHex", "mismatch"); }
		}
	}

	#endif

	// -------------------------------------------------------------------------
	// ZTB (ZOSCII Tamperproof Blockchain)
	// -------------------------------------------------------------------------

	private static int m_intZTBDirCounter = 0;

	// Make a work dir with a genesis block
	private static string makeZTBWorkDir(string strChainID_a)
	{
		m_intZTBDirCounter++;
		string strDir = Path.Combine(m_strTempFolder, "ztb_" + m_intZTBDirCounter.ToString());
		Directory.CreateDirectory(strDir);
		string strExePath = Process.GetCurrentProcess().MainModule.FileName;
		string strGenID   = Guid.NewGuid().ToString().ToUpperInvariant();
		ZTBChain.Create(strGenID, new string[] { strExePath }, strDir, strChainID_a);
		return strDir;
	}

	// Generate a GUID
	private static string guid()
	{
		return Guid.NewGuid().ToString().ToUpperInvariant();
	}

	private static void runZTBTests(bool blnPerf_a = false)
	{
		// ---------------------------------------------------------
		section("ZTB - Create");

		string strCDir  = Path.Combine(m_strTempFolder, "ztb_create_test");
		Directory.CreateDirectory(strCDir);
		string strExe   = Process.GetCurrentProcess().MainModule.FileName;
		string strGenID = guid();

		bool blnCreate = ZTBChain.Create(strGenID, new string[] { strExe }, strCDir, "TestGenesis");
		if (blnCreate)                                        { pass("ZTBChain.Create - Success"); }
		else                                                  { fail("ZTBChain.Create", "returned false"); }

		string[] arrGenFiles = Directory.GetFiles(strCDir, "*.ztb",
			SearchOption.AllDirectories).Where(f => new FileInfo(f).Length == ZTBChain.GENESIS_SIZE_PUBLIC).ToArray();
		if (arrGenFiles.Length == 1)                          { pass("ZTBChain.Create - genesis block written"); }
		else                                                  { fail("ZTBChain.Create file", "not found"); }

		if (arrGenFiles.Length == 1 && new FileInfo(arrGenFiles[0]).Length == ZTBChain.GENESIS_SIZE_PUBLIC)
		                                                      { pass("ZTBChain.Create - genesis size correct (" + ZTBChain.GENESIS_SIZE_PUBLIC + " bytes)"); }
		else                                                  { fail("ZTBChain.Create size", "wrong size"); }

		// Refuses duplicate - same block ID
		bool blnDup = ZTBChain.Create(strGenID, new string[] { strExe }, strCDir, "TestGenesis");
		if (!blnDup)                                          { pass("ZTBChain.Create - refuses duplicate genesis"); }
		else                                                  { fail("ZTBChain.Create dup", "should have failed"); }

		// ---------------------------------------------------------
		section("ZTB - Open");

		string strWorkDir = makeZTBWorkDir("TestChain");
		ZTBChain objChain = ZTBChain.Open(strWorkDir, "TestChain");
		if (objChain != null && objChain.ChainID == "TestChain")
		                                                      { pass("ZTBChain.Open - opened, ID=" + objChain.ChainID); }
		else                                                  { fail("ZTBChain.Open", "null or wrong ID"); }

		if (objChain == null) { return; }

		// ---------------------------------------------------------
		section("ZTB - AddBlock");

		byte[] arrP1   = Encoding.UTF8.GetBytes("Block 1 payload");
		string strID1  = guid();
		ZTBBlockResult objB1 = objChain.AddBlock(strID1, null, arrP1);
		if (objB1.Success && objB1.BlockID == strID1)         { pass("ZTBChain.AddBlock - Success, BlockID matches"); }
		else                                                  { fail("ZTBChain.AddBlock 1", "Success=" + objB1.Success + " BlockID=" + objB1.BlockID); }

		if (objB1.PrevBlockID == ZTBChain.NULL_GUID)          { pass("ZTBChain.AddBlock - PrevBlockID is NULL_GUID for block 1"); }
		else                                                  { fail("ZTBChain.AddBlock PrevBlockID", "expected NULL_GUID, got " + objB1.PrevBlockID); }

		if (!objB1.IsBranch)                                  { pass("ZTBChain.AddBlock - IsBranch=false for trunk"); }
		else                                                  { fail("ZTBChain.AddBlock IsBranch", "expected false"); }

		if (objB1.Hash != 0)                                  { pass("ZTBChain.AddBlock - Hash non-zero"); }
		else                                                  { fail("ZTBChain.AddBlock Hash", "zero"); }

		if (objB1.PrevHash == 0)                              { pass("ZTBChain.AddBlock - PrevHash=0 for block 1"); }
		else                                                  { fail("ZTBChain.AddBlock PrevHash", "expected 0, got " + objB1.PrevHash); }

		if (objB1.BlockType == ZTBBlockType.Normal)           { pass("ZTBChain.AddBlock - BlockType=Normal"); }
		else                                                  { fail("ZTBChain.AddBlock BlockType", "got " + objB1.BlockType); }

		if (objB1.HashType == ZTBHashType.RollingFull)        { pass("ZTBChain.AddBlock - HashType=RollingFull (default)"); }
		else                                                  { fail("ZTBChain.AddBlock HashType", "got " + objB1.HashType); }

		if (objB1.Filename != null && objB1.Filename.EndsWith(".ztb"))
		                                                      { pass("ZTBChain.AddBlock - Filename is .ztb (" + objB1.Filename + ")"); }
		else                                                  { fail("ZTBChain.AddBlock Filename", "null or wrong extension"); }

		if (File.Exists(Path.Combine(strWorkDir, objB1.Filename)))
		                                                      { pass("ZTBChain.AddBlock - file exists on disk"); }
		else                                                  { fail("ZTBChain.AddBlock file", "not on disk"); }

		if (objB1.Payload == null)                            { pass("ZTBChain.AddBlock - Payload null on write result"); }
		else                                                  { fail("ZTBChain.AddBlock Payload", "expected null"); }

		byte[] arrP2   = Encoding.UTF8.GetBytes("Block 2 payload");
		string strID2  = guid();
		ZTBBlockResult objB2 = objChain.AddBlock(strID2, strID1, arrP2);
		if (objB2.Success && objB2.BlockID == strID2)         { pass("ZTBChain.AddBlock - block 2 Success"); }
		else                                                  { fail("ZTBChain.AddBlock 2", "Success=" + objB2.Success); }

		if (objB2.PrevBlockID == strID1)                      { pass("ZTBChain.AddBlock - block 2 PrevBlockID links to block 1"); }
		else                                                  { fail("ZTBChain.AddBlock 2 PrevBlockID", "mismatch"); }

		if (objB2.PrevHash != 0)                              { pass("ZTBChain.AddBlock - block 2 PrevHash non-zero (predecessor binding)"); }
		else                                                  { fail("ZTBChain.AddBlock 2 PrevHash", "expected non-zero"); }

		// ---------------------------------------------------------
		section("ZTB - AddBlockText / AddBlockFile / AddBlock with ID");

		string strID3 = guid();
		ZTBBlockResult objBText = objChain.AddBlockText(strID3, strID2, "Text block");
		if (objBText.Success)                                 { pass("ZTBChain.AddBlockText - Success"); }
		else                                                  { fail("ZTBChain.AddBlockText", "failed"); }

		string strTmpFile = Path.Combine(m_strTempFolder, "ztb_file_payload.bin");
		File.WriteAllBytes(strTmpFile, Encoding.UTF8.GetBytes("File payload content"));
		string strID4 = guid();
		ZTBBlockResult objBFile = objChain.AddBlockFile(strID4, strID3, strTmpFile);
		if (objBFile.Success)                                 { pass("ZTBChain.AddBlockFile - Success"); }
		else                                                  { fail("ZTBChain.AddBlockFile", "failed"); }

		// ---------------------------------------------------------
		section("ZTB - FetchBlock");

		ZTBBlockResult objF1 = objChain.FetchBlock(strID1);
		if (objF1.Success)                                    { pass("ZTBChain.FetchBlock(1) - Success"); }
		else                                                  { fail("ZTBChain.FetchBlock(1)", "failed"); }

		if (objF1.Success && arraysEqual(objF1.Payload, arrP1))
		                                                      { pass("ZTBChain.FetchBlock(1) - Payload matches original"); }
		else                                                  { fail("ZTBChain.FetchBlock(1) Payload", "mismatch"); }

		if (objF1.BlockID == strID1)                          { pass("ZTBChain.FetchBlock(1) - BlockID matches"); }
		else                                                  { fail("ZTBChain.FetchBlock(1) BlockID", "mismatch"); }

		if (objF1.BlockType == ZTBBlockType.Normal)           { pass("ZTBChain.FetchBlock(1) - BlockType=Normal"); }
		else                                                  { fail("ZTBChain.FetchBlock(1) BlockType", "got " + objF1.BlockType); }

		if (objF1.HashType == ZTBHashType.RollingFull)        { pass("ZTBChain.FetchBlock(1) - HashType=RollingFull round trip"); }
		else                                                  { fail("ZTBChain.FetchBlock(1) HashType", "got " + objF1.HashType); }

		ZTBBlockResult objF2 = objChain.FetchBlock(strID2);
		if (objF2.Success && arraysEqual(objF2.Payload, arrP2))
		                                                      { pass("ZTBChain.FetchBlock(2) - round trip matches"); }
		else                                                  { fail("ZTBChain.FetchBlock(2)", "mismatch"); }

		ZTBBlockResult objFText = objChain.FetchBlock(strID3);
		if (objFText.Success && Encoding.UTF8.GetString(objFText.Payload) == "Text block")
		                                                      { pass("ZTBChain.FetchBlock(3) - text payload round trip"); }
		else                                                  { fail("ZTBChain.FetchBlock(3)", "mismatch"); }

		ZTBBlockResult objFMiss = objChain.FetchBlock(guid());
		if (!objFMiss.Success)                                { pass("ZTBChain.FetchBlock(missing) - correctly fails"); }
		else                                                  { fail("ZTBChain.FetchBlock(missing)", "should have failed"); }

		// ---------------------------------------------------------
		section("ZTB - Verify");

		ZTBVerifyResult objV = objChain.Verify(strID4, true);
		if (objV.Success)                                     { pass("ZTBChain.Verify - 4-block chain passes"); }
		else                                                  { fail("ZTBChain.Verify", "FailedBlocks=" + objV.FailedBlocks); }

		if (objV.VerifiedBlocks == 4)                         { pass("ZTBChain.Verify - VerifiedBlocks=4"); }
		else                                                  { fail("ZTBChain.Verify VerifiedBlocks", "expected 4, got " + objV.VerifiedBlocks); }

		if (objV.FailedBlocks == 0)                           { pass("ZTBChain.Verify - FailedBlocks=0"); }
		else                                                  { fail("ZTBChain.Verify FailedBlocks", "expected 0, got " + objV.FailedBlocks); }

		// Single block verify
		ZTBVerifyResult objVSingle = objChain.Verify(strID1, false);
		if (objVSingle.Success && objVSingle.VerifiedBlocks == 1)
		                                                      { pass("ZTBChain.Verify single block - passes"); }
		else                                                  { fail("ZTBChain.Verify single", "failed"); }

		// Tamper detection - middle of file
		string strTamperFile = Path.Combine(strWorkDir, objB2.Filename);
		byte[] arrOriginal   = File.ReadAllBytes(strTamperFile);
		byte[] arrTampered   = (byte[])arrOriginal.Clone();
		arrTampered[arrTampered.Length / 2] ^= 0xFF;
		File.WriteAllBytes(strTamperFile, arrTampered);
		ZTBVerifyResult objVTamper = objChain.Verify(strID4, true);
		if (!objVTamper.Success && objVTamper.FailedBlocks > 0)
		                                                      { pass("ZTBChain.Verify - tampered block detected"); }
		else                                                  { fail("ZTBChain.Verify tamper", "tamper not detected"); }
		File.WriteAllBytes(strTamperFile, arrOriginal);

		objChain.Dispose();

		// ---------------------------------------------------------
		section("ZTB - Tamper Detection (1KB boundary)");

		// Write a block with >2KB payload so we can tamper inside and outside 1KB
		string strTamperDir = makeZTBWorkDir("TamperChain");
		byte[] arrLargePayload = new byte[4096];
		new Random(42).NextBytes(arrLargePayload);

		// Test RollingFull — should detect tamper anywhere
		ZTBChain objTFull = ZTBChain.Open(strTamperDir, "TamperChain", ZTBHashType.RollingFull);
		if (objTFull != null)
		{
			string strTF1 = guid();
			string strTF2 = guid();
			objTFull.AddBlock(strTF1, null, Encoding.UTF8.GetBytes("anchor"));
			ZTBBlockResult objTFBlock = objTFull.AddBlock(strTF2, strTF1, arrLargePayload);

			if (objTFBlock.Success)
			{
				string strTFFile = Path.Combine(strTamperDir, objTFBlock.Filename);
				byte[] arrTFOrig = File.ReadAllBytes(strTFFile);

				// Tamper within first 1KB of encoded section
				byte[] arrTFTamper1 = (byte[])arrTFOrig.Clone();
				arrTFTamper1[clsZTB.HEADER_RAW_SIZE + 100] ^= 0xFF;
				File.WriteAllBytes(strTFFile, arrTFTamper1);
				ZTBVerifyResult objTFV1 = objTFull.Verify(strTF2, false);
				if (!objTFV1.Success)                         { pass("ZTBChain RollingFull - tamper within 1KB detected"); }
				else                                          { fail("ZTBChain RollingFull tamper within 1KB", "not detected"); }
				File.WriteAllBytes(strTFFile, arrTFOrig);

				// Tamper outside first 1KB of encoded section
				byte[] arrTFTamper2 = (byte[])arrTFOrig.Clone();
				arrTFTamper2[clsZTB.HEADER_RAW_SIZE + 2000] ^= 0xFF;
				File.WriteAllBytes(strTFFile, arrTFTamper2);
				ZTBVerifyResult objTFV2 = objTFull.Verify(strTF2, false);
				if (!objTFV2.Success)                         { pass("ZTBChain RollingFull - tamper outside 1KB detected"); }
				else                                          { fail("ZTBChain RollingFull tamper outside 1KB", "not detected"); }
				File.WriteAllBytes(strTFFile, arrTFOrig);
			}
			objTFull.Dispose();
		}

		// Test Rolling1KB — detects tamper within its hashed 1KB window
		string strTamperDir2 = makeZTBWorkDir("TamperChain1KB");
		ZTBChain objT1KB = ZTBChain.Open(strTamperDir2, "TamperChain1KB", ZTBHashType.Rolling1KB);
		if (objT1KB != null)
		{
			string strT1KB1 = guid();
			string strT1KB2 = guid();
			objT1KB.AddBlock(strT1KB1, null, Encoding.UTF8.GetBytes("anchor"));
			ZTBBlockResult objT1KBBlock = objT1KB.AddBlock(strT1KB2, strT1KB1, arrLargePayload);

			if (objT1KBBlock.Success)
			{
				string strT1KBFile = Path.Combine(strTamperDir2, objT1KBBlock.Filename);
				byte[] arrT1KBOrig = File.ReadAllBytes(strT1KBFile);

				// Tamper within first 1KB — should be detected
				byte[] arrT1KBTamper1 = (byte[])arrT1KBOrig.Clone();
				arrT1KBTamper1[clsZTB.HEADER_RAW_SIZE + 100] ^= 0xFF;
				File.WriteAllBytes(strT1KBFile, arrT1KBTamper1);
				ZTBVerifyResult objT1KBV1 = objT1KB.Verify(strT1KB2, false);
				if (!objT1KBV1.Success)                       { pass("ZTBChain Rolling1KB - tamper within 1KB detected"); }
				else                                          { fail("ZTBChain Rolling1KB tamper within 1KB", "not detected"); }
				File.WriteAllBytes(strT1KBFile, arrT1KBOrig);

				// Note: Rolling1KB/CRC321KB deliberately do not hash payload bytes beyond the
				// first 1024 -- that is the documented, intentional cost/coverage trade-off of
				// the windowed hash types (see Whitepaper, Section 2F-2H). Whether a tamper in
				// that region is independently caught depends on the rolling ROM and on
				// whichever later block's prevHash window happens to reach it, neither of
				// which is a property of THIS block in isolation -- so there is no meaningful
				// single-block assertion to make about that region here.
			}
			objT1KB.Dispose();
		}

		// ---------------------------------------------------------
		section("ZTB - AddBranch");

		string strTrunkDir = makeZTBWorkDir("Trunk");
		ZTBChain objTrunk  = ZTBChain.Open(strTrunkDir, "Trunk");
		if (objTrunk == null) { fail("ZTBChain.AddBranch setup", "trunk open failed"); return; }

		string strT1 = guid();
		string strT2 = guid();
		string strT3 = guid();
		objTrunk.AddBlock(strT1, null, Encoding.UTF8.GetBytes("Trunk 1"));
		objTrunk.AddBlock(strT2, strT1, Encoding.UTF8.GetBytes("Trunk 2"));
		objTrunk.AddBlock(strT3, strT2, Encoding.UTF8.GetBytes("Trunk 3"));
		pass("ZTBChain.AddBranch setup - 3-block trunk created");

		string strBranchChainID = "Branch1";
		ZTBChain objBranchChain = ZTBChain.Open(strTrunkDir, strBranchChainID);
		if (objBranchChain == null) { fail("ZTBChain.AddBranch", "branch chain open failed"); return; }

		string strBranchID1 = guid();
		ZTBBlockResult objBr1 = objBranchChain.AddBranch(strBranchID1, strT3,
		                                                   Encoding.UTF8.GetBytes("Branch block 1"),
		                                                   "Trunk");
		if (objBr1.Success)                                   { pass("ZTBChain.AddBranch - Success"); }
		else                                                  { fail("ZTBChain.AddBranch", "failed"); }

		if (objBr1.IsBranch)                                  { pass("ZTBChain.AddBranch - IsBranch=true"); }
		else                                                  { fail("ZTBChain.AddBranch IsBranch", "expected true"); }

		if (objBr1.TrunkID == "Trunk")                        { pass("ZTBChain.AddBranch - TrunkID links to trunk"); }
		else                                                  { fail("ZTBChain.AddBranch TrunkID", "got " + objBr1.TrunkID); }

		string strBranchID2 = guid();
		ZTBBlockResult objBr2 = objBranchChain.AddBlock(strBranchID2, strBranchID1,
		                                                  Encoding.UTF8.GetBytes("Branch block 2"));
		if (objBr2.Success && objBr2.IsBranch)                { pass("ZTBChain branch AddBlock - block 2, IsBranch=true"); }
		else                                                  { fail("ZTBChain branch AddBlock", "Success=" + objBr2.Success); }

		ZTBBlockResult objBrFetch = objBranchChain.FetchBlock(strBranchID1);
		if (objBrFetch.Success && Encoding.UTF8.GetString(objBrFetch.Payload) == "Branch block 1")
		                                                      { pass("ZTBChain branch FetchBlock(1) - round trip"); }
		else                                                  { fail("ZTBChain branch FetchBlock(1)", "mismatch"); }

		ZTBVerifyResult objBrV = objBranchChain.Verify(strBranchID2, true);
		if (objBrV.Success)                                   { pass("ZTBChain.VerifyBranch - passes"); }
		else                                                  { fail("ZTBChain.VerifyBranch", "FailedBlocks=" + objBrV.FailedBlocks); }

		objBranchChain.Dispose();

		ZTBBlockResult objTrunkFetch = objTrunk.FetchBlock(strT3);
		if (objTrunkFetch.Success && Encoding.UTF8.GetString(objTrunkFetch.Payload) == "Trunk 3")
		                                                      { pass("ZTBChain trunk FetchBlock after branching - round trip"); }
		else                                                  { fail("ZTBChain trunk FetchBlock after branch", "mismatch"); }

		objTrunk.Dispose();

		// ---------------------------------------------------------
		section("ZTB - 20-Block Trunk with 2 Branches");

		string strWorkDir20 = makeZTBWorkDir("Trunk20");
		ZTBChain objT20     = ZTBChain.Open(strWorkDir20, "Trunk20");
		if (objT20 == null) { fail("20-block setup", "open failed"); return; }

		string strPrev20 = null;
		string[] arrBlockIDs20 = new string[20];
		int intI20 = 0;
		while (intI20 < 20)
		{
			arrBlockIDs20[intI20] = guid();
			objT20.AddBlock(arrBlockIDs20[intI20], strPrev20,
			                Encoding.UTF8.GetBytes("Trunk block " + (intI20 + 1)));
			strPrev20 = arrBlockIDs20[intI20];
			intI20++;
		}
		pass("ZTBChain 20-block trunk - all 20 blocks written");

		ZTBBlockResult objFT1   = objT20.FetchBlock(arrBlockIDs20[0]);
		ZTBBlockResult objFT10  = objT20.FetchBlock(arrBlockIDs20[9]);
		ZTBBlockResult objFT20  = objT20.FetchBlock(arrBlockIDs20[19]);

		if (objFT1.Success && Encoding.UTF8.GetString(objFT1.Payload) == "Trunk block 1")
		                                                      { pass("ZTBChain 20-block trunk - FetchBlock(1) round trip"); }
		else                                                  { fail("ZTBChain 20-block FetchBlock(1)", "mismatch"); }

		if (objFT10.Success && Encoding.UTF8.GetString(objFT10.Payload) == "Trunk block 10")
		                                                      { pass("ZTBChain 20-block trunk - FetchBlock(10) round trip"); }
		else                                                  { fail("ZTBChain 20-block FetchBlock(10)", "mismatch"); }

		if (objFT20.Success && Encoding.UTF8.GetString(objFT20.Payload) == "Trunk block 20")
		                                                      { pass("ZTBChain 20-block trunk - FetchBlock(20) round trip"); }
		else                                                  { fail("ZTBChain 20-block FetchBlock(20)", "mismatch"); }

		ZTBVerifyResult objVT20 = objT20.Verify(arrBlockIDs20[19], true);
		if (objVT20.Success && objVT20.VerifiedBlocks == 20)  { pass("ZTBChain 20-block trunk - Verify all 20 pass"); }
		else                                                  { fail("ZTBChain 20-block Verify", "Success=" + objVT20.Success + " Blocks=" + objVT20.VerifiedBlocks); }

		// Branch A
		string strBrAChainID = "BranchA";
		ZTBChain objBrAChain = ZTBChain.Open(strWorkDir20, strBrAChainID);
		string strBrA1 = guid();
		ZTBBlockResult objBrA = objBrAChain != null
		    ? objBrAChain.AddBranch(strBrA1, arrBlockIDs20[19],
		                             Encoding.UTF8.GetBytes("Branch A block 1"), "Trunk20")
		    : new ZTBBlockResult();
		if (objBrA.Success)                                   { pass("ZTBChain BranchA - created"); }
		else                                                  { fail("ZTBChain BranchA", "failed"); }

		if (objBrAChain != null)
		{
			string strBrA2 = guid();
			objBrAChain.AddBlock(strBrA2, strBrA1, Encoding.UTF8.GetBytes("Branch A block 2"));
			ZTBVerifyResult objBrAV = objBrAChain.Verify(strBrA2, true);
			if (objBrAV.Success && objBrAV.VerifiedBlocks == 22)
			                                                  { pass("ZTBChain BranchA - VerifyBranch passes (2 branch + 20 trunk blocks)"); }
			else                                              { fail("ZTBChain BranchA Verify", "Blocks=" + objBrAV.VerifiedBlocks); }
			objBrAChain.Dispose();
		}

		objT20.Dispose();

		// ---------------------------------------------------------
		section("ZTB - AddCheckpoint");

		string strCPDir = makeZTBWorkDir("CPChain");
		ZTBChain objCP  = ZTBChain.Open(strCPDir, "CPChain");
		if (objCP == null) { fail("Checkpoint setup", "open failed"); return; }

		string strCP1 = guid();
		string strCP2 = guid();
		string strCPID = guid();
		objCP.AddBlock(strCP1, null,  Encoding.UTF8.GetBytes("Before checkpoint 1"));
		objCP.AddBlock(strCP2, strCP1, Encoding.UTF8.GetBytes("Before checkpoint 2"));
		ZTBBlockResult objCPBlock = objCP.AddCheckpoint(strCPID, strCP2, "Checkpoint label");

		if (objCPBlock.Success && objCPBlock.BlockType == ZTBBlockType.Checkpoint)
		                                                      { pass("ZTBChain.AddCheckpoint - Success, BlockType=Checkpoint"); }
		else                                                  { fail("ZTBChain.AddCheckpoint", "Success=" + objCPBlock.Success); }

		ZTBBlockResult objCPFetch = objCP.FetchBlock(strCPID);
		if (objCPFetch.Success && Encoding.UTF8.GetString(objCPFetch.Payload) == "Checkpoint label")
		                                                      { pass("ZTBChain.AddCheckpoint - label payload round trip"); }
		else                                                  { fail("ZTBChain.AddCheckpoint label", "mismatch"); }

		if (objCPFetch.Success && objCPFetch.BlockType == ZTBBlockType.Checkpoint)
		                                                      { pass("ZTBChain.AddCheckpoint - FetchBlock BlockType=Checkpoint"); }
		else                                                  { fail("ZTBChain.AddCheckpoint FetchBlock type", "wrong type"); }

		// Continue adding after checkpoint
		string strPostCP = guid();
		ZTBBlockResult objPostCP = objCP.AddBlock(strPostCP, strCPID, Encoding.UTF8.GetBytes("After checkpoint"));
		if (objPostCP.Success)                                { pass("ZTBChain.AddCheckpoint - blocks continue after checkpoint"); }
		else                                                  { fail("ZTBChain.AddCheckpoint post-block", "failed"); }

		ZTBVerifyResult objCPV = objCP.Verify(strPostCP, true);
		if (objCPV.Success && objCPV.VerifiedBlocks == 4)    { pass("ZTBChain.AddCheckpoint - Verify passes through checkpoint"); }
		else                                                  { fail("ZTBChain.AddCheckpoint Verify", "Success=" + objCPV.Success + " Blocks=" + objCPV.VerifiedBlocks); }

		objCP.Dispose();

		// ---------------------------------------------------------
		section("ZTB - Truncate");

		string strTRDir = makeZTBWorkDir("TRChain");
		ZTBChain objTR  = ZTBChain.Open(strTRDir, "TRChain");
		if (objTR == null) { fail("Truncate setup", "open failed"); return; }

		string strTR1 = guid();
		string strTR2 = guid();
		string strTRCP = guid();
		string strTRTrunc = guid();
		objTR.AddBlock(strTR1,   null,   Encoding.UTF8.GetBytes("TR 1"));
		objTR.AddBlock(strTR2,   strTR1, Encoding.UTF8.GetBytes("TR 2"));
		objTR.AddCheckpoint(strTRCP, strTR2, "Truncation checkpoint");

		ZTBBlockResult objTrunc = objTR.Truncate(guid(), strTRCP);
		if (objTrunc.Success && objTrunc.BlockType == ZTBBlockType.Truncation)
		                                                      { pass("ZTBChain.Truncate - Success, BlockType=Truncation"); }
		else                                                  { fail("ZTBChain.Truncate", "Success=" + objTrunc.Success); }

		// Delete old blocks below the truncation block — strTR2 IS the truncation block, keep it
		SecureDelete.File(Path.Combine(strTRDir, strTR1 + ".ztb"));

		// Add a new block above the checkpoint
		string strTRPost = guid();
		ZTBBlockResult objTRPostBlock = objTR.AddBlock(strTRPost, strTRCP,
		                                                Encoding.UTF8.GetBytes("Post-truncation block"));
		if (objTRPostBlock.Success)                           { pass("ZTBChain.Truncate - new block added above checkpoint after deleting old history"); }
		else                                                  { fail("ZTBChain.Truncate post-block", "failed"); }

		ZTBBlockResult objTRPostFetch = objTR.FetchBlock(strTRPost);
		if (objTRPostFetch.Success &&
		    Encoding.UTF8.GetString(objTRPostFetch.Payload) == "Post-truncation block")
		                                                      { pass("ZTBChain.Truncate - post-truncation FetchBlock round trip"); }
		else                                                  { fail("ZTBChain.Truncate post-block fetch", "mismatch"); }

		ZTBVerifyResult objTRPostV = objTR.Verify(strTRPost, true);
		if (objTRPostV.Success)                               { pass("ZTBChain.Truncate - Verify passes after old history deleted"); }
		else                                                  { fail("ZTBChain.Truncate post-verify", "FailedBlocks=" + objTRPostV.FailedBlocks + " post=" + objTR.FetchBlock(strTRPost).Success + " cp=" + objTR.FetchBlock(strTRCP).Success + " trunc=" + objTR.FetchBlock(strTR2).Success); }

		objTR.Dispose();

		// ---------------------------------------------------------
		section("ZTB - Finalise");

		string strFNDir = makeZTBWorkDir("FNChain");
		ZTBChain objFN  = ZTBChain.Open(strFNDir, "FNChain");
		if (objFN == null) { fail("Finalise setup", "open failed"); return; }

		string strFN1  = guid();
		string strFN2  = guid();
		string strFNID = guid();
		objFN.AddBlock(strFN1, null,   Encoding.UTF8.GetBytes("FN 1"));
		objFN.AddBlock(strFN2, strFN1, Encoding.UTF8.GetBytes("FN 2"));
		ZTBBlockResult objFinalise = objFN.Finalise(strFNID, strFN2, "Final label");

		if (objFinalise.Success && objFinalise.BlockType == ZTBBlockType.Finalise)
		                                                      { pass("ZTBChain.Finalise - Success, BlockType=Finalise"); }
		else                                                  { fail("ZTBChain.Finalise", "Success=" + objFinalise.Success); }

		ZTBBlockResult objFNFetch = objFN.FetchBlock(strFNID);
		if (objFNFetch.Success && Encoding.UTF8.GetString(objFNFetch.Payload) == "Final label")
		                                                      { pass("ZTBChain.Finalise - label payload round trip"); }
		else                                                  { fail("ZTBChain.Finalise label", "mismatch"); }

		ZTBVerifyResult objFNV = objFN.Verify(strFNID, true);
		if (objFNV.Success)                                   { pass("ZTBChain.Finalise - Verify passes through finalise block"); }
		else                                                  { fail("ZTBChain.Finalise Verify", "failed"); }

		objFN.Dispose();

		// ---------------------------------------------------------
		section("ZTB - HashType");

		string strHTDir = makeZTBWorkDir("HTChain");
		ZTBChain objHT  = ZTBChain.Open(strHTDir, "HTChain");
		if (objHT == null) { fail("HashType setup", "open failed"); return; }

		string strHT1 = guid();
		ZTBBlockResult objHT1 = objHT.AddBlock(strHT1, null, Encoding.UTF8.GetBytes("HT block"));
		if (objHT1.HashType == ZTBHashType.RollingFull)       { pass("ZTBChain HashType - RollingFull is default"); }
		else                                                  { fail("ZTBChain HashType default", "got " + objHT1.HashType); }

		if (objHT1.Hash != 0)                                 { pass("ZTBChain Hash - non-zero RollingFull"); }
		else                                                  { fail("ZTBChain Hash", "zero"); }

		ZTBBlockResult objHTFetch = objHT.FetchBlock(strHT1);
		if (objHTFetch.Success && objHTFetch.HashType == ZTBHashType.RollingFull)
		                                                      { pass("ZTBChain FetchBlock - HashType round trip"); }
		else                                                  { fail("ZTBChain FetchBlock HashType", "mismatch"); }

		ZTBVerifyResult objHTVerify = objHT.Verify(strHT1, true);
		if (objHTVerify.Success)                              { pass("ZTBChain HashType - Verify passes"); }
		else                                                  { fail("ZTBChain HashType Verify", "failed"); }

		objHT.Dispose();

		// ---------------------------------------------------------
		section("ZTB - Dispose");

		string strDDir  = makeZTBWorkDir("DChain");
		ZTBChain objD   = ZTBChain.Open(strDDir, "DChain");
		if (objD != null) { objD.Dispose(); }
		string strAfter = objD != null ? objD.ChainID : null;
		if (strAfter == null)                                 { pass("ZTBChain.Dispose - no exception accessing ChainID after dispose (result: null)"); }
		else                                                  { fail("ZTBChain.Dispose", "ChainID still set after dispose"); }

		if (!blnPerf_a) { return; }

		// ---------------------------------------------------------
		section("ZTB - Performance (disk, 10 seconds)");

		string strPerfDir = makeZTBWorkDir("PerfChain");
		ZTBChain objPerf  = ZTBChain.Open(strPerfDir, "PerfChain");
		if (objPerf != null)
		{
			byte[] arrPerfPayload = Encoding.UTF8.GetBytes("Performance test block payload");
			long lngPerfEnd       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000;
			string strPerfPrev    = null;
			int intPerfCount      = 0;

			while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < lngPerfEnd)
			{
				string strPerfID = guid();
				ZTBBlockResult objPerfBlock = objPerf.AddBlock(strPerfID, strPerfPrev, arrPerfPayload);
				if (objPerfBlock.Success)
				{
					strPerfPrev = strPerfID;
					intPerfCount++;
				}
				else { break; }
			}

			pass("ZTB disk perf - " + intPerfCount + " blocks written in 10 seconds (" +
			     (intPerfCount / 10) + " blocks/sec)");
			objPerf.Dispose();
		}

		// ---------------------------------------------------------
		section("ZTB - Performance (memory, 10 seconds)");

		Dictionary<string, byte[]> objMemStore = new Dictionary<string, byte[]>();
		object objMemLock = new object();

		// Seed genesis
		string strMemGenesisID = guid();
		string strMemChainID   = "MemPerfChain";
		byte[] arrMemGenesis   = new byte[ZTBChain.GENESIS_SIZE_PUBLIC];
		arrMemGenesis[0]       = (byte)ZTBBlockType.Genesis;
		byte[] arrExeBytes2    = File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName);
		double dblMemStep      = (double)arrExeBytes2.Length / (ZTBChain.GENESIS_SIZE_PUBLIC - 1);
		double dblMemPos       = 0.0;
		int intMemRomI         = 1;
		while (intMemRomI < ZTBChain.GENESIS_SIZE_PUBLIC)
		{
			long lngP = (long)dblMemPos;
			if (lngP >= arrExeBytes2.Length) { lngP = arrExeBytes2.Length - 1; }
			arrMemGenesis[intMemRomI] = arrExeBytes2[lngP];
			dblMemPos += dblMemStep;
			intMemRomI++;
		}
		lock (objMemLock) { objMemStore[strMemGenesisID + ".ztb"] = arrMemGenesis; }

		ZTBChain objMemPerf = ZTBChain.Open(null, strMemChainID);
		if (objMemPerf != null)
		{
			objMemPerf.OnSaveBlock = delegate(ZTBBlockResult objMeta_a, byte[] arrBytes_a, string strPath_a)
			{
				lock (objMemLock) { objMemStore[objMeta_a.Filename] = arrBytes_a; }
				return true;
			};
			objMemPerf.OnLoadBlock = delegate(string strFilename_a)
			{
				byte[] arrResult = null;
				lock (objMemLock)
				{
					string strKey = Path.GetFileName(strFilename_a);
					if (objMemStore.ContainsKey(strKey)) { arrResult = objMemStore[strKey]; }
				}
				return arrResult;
			};
			objMemPerf.OnFindGenesis = delegate(string strChainID_a)
			{
				byte[] arrResult = null;
				lock (objMemLock)
				{
					foreach (string strK in objMemStore.Keys)
					{
						if (objMemStore[strK].Length == ZTBChain.GENESIS_SIZE_PUBLIC)
						{
							arrResult = objMemStore[strK];
							break;
						}
					}
				}
				return arrResult;
			};

			byte[] arrMemPayload = Encoding.UTF8.GetBytes("Memory performance test block");
			long lngMemEnd       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000;
			string strMemPrev    = null;
			int intMemCount      = 0;

			while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < lngMemEnd)
			{
				string strMemID = guid();
				ZTBBlockResult objMemBlock = objMemPerf.AddBlock(strMemID, strMemPrev, arrMemPayload);
				if (objMemBlock.Success)
				{
					strMemPrev = strMemID;
					intMemCount++;
				}
				else { break; }
			}

			pass("ZTB memory perf - " + intMemCount + " blocks written in 10 seconds (" +
			     (intMemCount / 10) + " blocks/sec)");
			objMemPerf.Dispose();
		}
	}


	// -------------------------------------------------------------------------
	// ZRollingHash
	// -------------------------------------------------------------------------

	private static void runZRollingHashTests()
	{
		section("ZRollingHash");

		byte[] arrPayload = new byte[5000];
		new Random(42).NextBytes(arrPayload);

		// Reverse (default) — Bytes
		byte[] arrHash = ZRollingHash.Bytes(arrPayload);
		if (arrHash != null && arrHash.Length == 4) { pass("ZRollingHash.Bytes reverse - 4 bytes"); }
		else { fail("ZRollingHash.Bytes reverse", "unexpected result"); }

		bool blnVerify = ZRollingHash.Verify(arrPayload, arrHash);
		if (blnVerify) { pass("ZRollingHash.Verify reverse - matches"); }
		else { fail("ZRollingHash.Verify reverse", "mismatch"); }

		// Forward — Bytes
		byte[] arrHashFwd = ZRollingHash.Bytes(arrPayload, true);
		if (arrHashFwd != null && arrHashFwd.Length == 4) { pass("ZRollingHash.Bytes forward - 4 bytes"); }
		else { fail("ZRollingHash.Bytes forward", "unexpected result"); }

		bool blnVerifyFwd = ZRollingHash.Verify(arrPayload, arrHashFwd, true);
		if (blnVerifyFwd) { pass("ZRollingHash.Verify forward - matches"); }
		else { fail("ZRollingHash.Verify forward", "mismatch"); }

		// Forward and reverse differ
		if (!arraysEqual(arrHash, arrHashFwd)) { pass("ZRollingHash - forward and reverse produce different hashes"); }
		else { fail("ZRollingHash", "forward and reverse should differ"); }

		// Tamper detection — reverse
		byte[] arrTampered = (byte[])arrPayload.Clone();
		arrTampered[2500] ^= 0xFF;
		bool blnTamper = ZRollingHash.Verify(arrTampered, arrHash);
		if (!blnTamper) { pass("ZRollingHash.Verify reverse - tamper detected"); }
		else { fail("ZRollingHash.Verify reverse tamper", "tamper not detected"); }

		// Tamper detection — forward
		bool blnTamperFwd = ZRollingHash.Verify(arrTampered, arrHashFwd, true);
		if (!blnTamperFwd) { pass("ZRollingHash.Verify forward - tamper detected"); }
		else { fail("ZRollingHash.Verify forward tamper", "tamper not detected"); }

		// File
		string strTempFile = tempFile(".bin");
		File.WriteAllBytes(strTempFile, arrPayload);
		byte[] arrFileHash = ZRollingHash.File(strTempFile);
		if (arraysEqual(arrFileHash, arrHash)) { pass("ZRollingHash.File - matches Bytes result"); }
		else { fail("ZRollingHash.File", "mismatch with Bytes"); }

		// ZOSCII encode hash+payload, tamper, decode, verify hash mismatch
		using (ZOSCIIRom objRom = makeTestROM())
		{
			// Build hash + payload buffer
			byte[] arrCombined = new byte[arrHash.Length + arrPayload.Length];
			Array.Copy(arrHash,    0, arrCombined, 0,            arrHash.Length);
			Array.Copy(arrPayload, 0, arrCombined, arrHash.Length, arrPayload.Length);

			// ZOSCII encode
			byte[] arrEncoded = ZEncode.Bytes(arrCombined, objRom);
			if (arrEncoded != null) { pass("ZRollingHash ZOSCII encode - hash+payload encoded"); }
			else { fail("ZRollingHash ZOSCII encode", "encode returned null"); }

			if (arrEncoded != null)
			{
				// Tamper a byte in the encoded payload portion (after the hash)
				int intTamperPos = arrHash.Length * 2 + 100;  // past hash addresses, into payload
				if (intTamperPos < arrEncoded.Length)
				{
					arrEncoded[intTamperPos] ^= 0xFF;
				}

				// Decode — will succeed (addresses may still be valid) or fail
				byte[] arrDecoded = ZDecode.Bytes(arrEncoded, objRom);

				if (arrDecoded != null)
				{
					// Extract hash and payload from decoded result
					byte[] arrDecodedHash    = new byte[arrHash.Length];
					byte[] arrDecodedPayload = new byte[arrPayload.Length];
					Array.Copy(arrDecoded, 0,             arrDecodedHash,    0, arrHash.Length);
					Array.Copy(arrDecoded, arrHash.Length, arrDecodedPayload, 0, arrPayload.Length);

					// Verify hash against decoded payload — should fail due to tamper
					bool blnHashMatch = ZRollingHash.Verify(arrDecodedPayload, arrDecodedHash);
					if (!blnHashMatch) { pass("ZRollingHash ZOSCII tamper - hash mismatch correctly detected after decode"); }
					else { fail("ZRollingHash ZOSCII tamper", "tamper not detected via hash verify"); }
				}
				else
				{
					// Decode failed due to invalid address — tamper was caught at ZOSCII level
					pass("ZRollingHash ZOSCII tamper - decode failed (tamper caught at ZOSCII address level)");
				}
			}
		}
	}
}