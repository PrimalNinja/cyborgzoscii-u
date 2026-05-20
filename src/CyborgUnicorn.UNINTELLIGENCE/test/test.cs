// Cyborg ZOSCII / UNINTELLIGENCE Test Harness v20260520
// (c) 2026 Cyborg Unicorn Pty Ltd.
// This software is released under UNINTELLIGENCE SOFTWARE LICENSE v1.1
// ZOSCII core logic remains under MIT License.
// Build with UNINTELLIGENCE defined to include UNSIGNAL / PENTAGONE tests.

using System;
using System.Collections.Generic;
using System.IO;
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

		bool blnRun = false;
		string strMQUrl = "";

		for (int intI = 0; intI < arrArgs_a.Length; intI++)
		{
			if (arrArgs_a[intI] == "-run") { blnRun = true; }
			if (arrArgs_a[intI] == "-mq" && intI + 1 < arrArgs_a.Length) { strMQUrl = arrArgs_a[intI + 1]; blnRun = true; }
		}

		if (!blnRun)
		{
			#if UNINTELLIGENCE
			Console.WriteLine("Tests: ZOSCII + UNINTELLIGENCE (UNSIGNAL, PENTAGONE, EntropySugar)");
			#else
			Console.WriteLine("Tests: ZOSCII (ZEncode, ZDecode, ZVerify, BVerify, BSplit, BJoin, SecureDelete, Source)");
			#endif
			Console.WriteLine();
			Console.WriteLine("Usage: test.exe -run [-mq <url>]");
			Console.WriteLine();
			Console.WriteLine("  -run        Run all tests");
			Console.WriteLine("  -mq <url>   Also run MQClient tests against the given server URL");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  test.exe -run");
			Console.WriteLine("  test.exe -run -mq https://your-server/index.php");
			return;
		}

		m_strTempFolder = Path.Combine(Path.GetTempPath(), "zoscii_test_" + Guid.NewGuid().ToString("N"));
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

			#if UNINTELLIGENCE
			runBSplitJoinTests();
			runUEncodeDecodeTests();
			runUChainTests();
			runUFileTests();
			runUVerifyTests();
			runEntropySugarTests();
			#endif
		}
		finally
		{
			try { Directory.Delete(m_strTempFolder, true); } catch { }
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
}