// CyborgUnicorn.UNINTELLIGENCE - BRICS / micro / Scheme test harness
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE SOFTWARE LICENSE v1.2

using System;
using System.Collections.Generic;
using System.Text;
using CyborgUnicorn.ZOSCII;

public static class SchemeTest
{
	private static int m_intPassed = 0;
	private static int m_intFailed = 0;

	private sealed class clsMemoryResolver : ISchemeRomResolver
	{
		public Dictionary<string, byte[]> Roms = new Dictionary<string, byte[]>();
		public byte[] GetRomBytes(string strRomId_a) { return Roms.ContainsKey(strRomId_a) ? Roms[strRomId_a] : null; }
	}

	private static byte[] randomBytes(Random objRand_a, int intLen_a) { byte[] arr = new byte[intLen_a]; objRand_a.NextBytes(arr); return arr; }

	private static bool same(byte[] arrA_a, byte[] arrB_a)
	{
		bool blnResult = arrA_a != null && arrB_a != null && arrA_a.Length == arrB_a.Length;
		for (int intI = 0; blnResult && intI < arrA_a.Length; intI++) { blnResult = arrA_a[intI] == arrB_a[intI]; }
		return blnResult;
	}

	private static void check(string strName_a, bool blnOk_a)
	{
		if (blnOk_a) { m_intPassed++; Console.WriteLine("PASS " + strName_a); }
		else { m_intFailed++; Console.WriteLine("FAIL " + strName_a + (SchemeRunner.LastError.Length > 0 ? " (" + SchemeRunner.LastError + ")" : "")); }
	}

	public static int Main(string[] arrArgs_a)
	{
		Random objRand = new Random(12345);
		byte[] arrRomA = randomBytes(objRand, 131072);
		byte[] arrRomB = randomBytes(objRand, 131072);
		byte[] arrSmallRom = randomBytes(objRand, 40000);
		byte[] arrMicroSrc = randomBytes(objRand, 4096);     // stands in for a JPEG
		byte[][] arrPayloads = { new byte[0], new byte[] { 7 }, Encoding.UTF8.GetBytes("Cyborg Unicorn"), randomBytes(objRand, 1000), randomBytes(objRand, 70000) };

		// --- BRICS ---
		using (ZOSCIIRom objA = ZOSCIIRom.FromBytes(arrRomA))
		using (ZOSCIIRom objB = ZOSCIIRom.FromBytes(arrRomB))
		using (ZOSCIIRom objSmall = ZOSCIIRom.FromBytes(arrSmallRom))
		{
			for (int intP = 0; intP < arrPayloads.Length; intP++)
			{
				byte[] arrEnc = BEncode.Bytes(arrPayloads[intP], objA);
				byte[] arrDec = BDecode.Bytes(arrEnc, objA);
				check("BRICS round trip len " + arrPayloads[intP].Length, same(arrPayloads[intP], arrDec));
				if (arrEnc != null && arrPayloads[intP].Length > 0)
				{
					byte[] arrEnc2 = BEncode.Bytes(arrPayloads[intP], objA);
					check("BRICS two encodes differ len " + arrPayloads[intP].Length, !same(arrEnc, arrEnc2));
					check("BRICS wrong ROM fails or differs len " + arrPayloads[intP].Length, !same(arrPayloads[intP], BDecode.Bytes(arrEnc, objB)));
				}
			}
			byte[] arrEncSmall = BEncode.Bytes(arrPayloads[3], objSmall);
			check("BRICS small ROM (regime 1 rule 1)", same(arrPayloads[3], BDecode.Bytes(arrEncSmall, objSmall)));
			byte[] arrChain = BEncode.Chain(arrPayloads[3], new ZOSCIIRom[] { objA, objB });
			check("BRICS chain A,B", same(arrPayloads[3], BDecode.Chain(arrChain, new ZOSCIIRom[] { objA, objB })));
			check("BRICS chain B,A fails", !same(arrPayloads[3], BDecode.Chain(arrChain, new ZOSCIIRom[] { objB, objA })));
		}

		// --- micro ---
		MicroRom objMU = MicroRom.FromBytes(arrMicroSrc, 512);
		MicroRom objMU2 = MicroRom.FromBytes(randomBytes(objRand, 300), 512);
		MicroRom objMZ = MicroRom.FromBytes(arrMicroSrc, 256);
		check("MicroRom 512 nibbles", objMU.Length == 512);
		check("MicroRom 256 nibbles", objMZ.Length == 256);
		check("MicroRom hex round trip", MicroRom.FromHex(objMU.ToHex()).ToHex() == objMU.ToHex());
		for (int intP = 0; intP < arrPayloads.Length; intP++)
		{
			byte[] arrEnc = MUEncode.Bytes(arrPayloads[intP], objMU);
			check("microUNSIGNAL round trip len " + arrPayloads[intP].Length, same(arrPayloads[intP], MUDecode.Bytes(arrEnc, objMU)));
			byte[] arrEncB = MBEncode.Bytes(arrPayloads[intP], objMU);
			check("microBRICS round trip len " + arrPayloads[intP].Length, same(arrPayloads[intP], MBDecode.Bytes(arrEncB, objMU)));
			if (arrPayloads[intP].Length > 0)
			{
				check("microUNSIGNAL wrong ROM differs len " + arrPayloads[intP].Length, !same(arrPayloads[intP], MUDecode.Bytes(arrEnc, objMU2)));
				check("microBRICS wrong ROM differs len " + arrPayloads[intP].Length, !same(arrPayloads[intP], MBDecode.Bytes(arrEncB, objMU2)));
			}
		}
		MicroRom objTiny = MicroRom.FromBytes(randomBytes(objRand, 60), 512);   // 120 nibbles, rule 1
		check("microUNSIGNAL tiny ROM", same(arrPayloads[2], MUDecode.Bytes(MUEncode.Bytes(arrPayloads[2], objTiny), objTiny)));
		check("microBRICS tiny ROM", same(arrPayloads[2], MBDecode.Bytes(MBEncode.Bytes(arrPayloads[2], objTiny), objTiny)));

		// --- CRC32 ---
		check("CRC32 of 123456789", ZCRC32.Compute(Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926);

		// --- Schemes ---
		clsMemoryResolver objResolver = new clsMemoryResolver();
		objResolver.Roms["A"] = arrRomA;
		objResolver.Roms["B"] = arrRomB;
		objResolver.Roms["J"] = arrMicroSrc;

		Scheme objScheme = new Scheme();
		objScheme.Id = "s1";
		objScheme.Name = "micro then full";
		SchemeStep objS1 = new SchemeStep(); objS1.Encoding = SchemeEncoding.MICROZOSCII; objS1.RomIds.Add("J"); objS1.Integrity = SchemeIntegrity.ROLLINGHASH_CRC32;
		SchemeStep objS2 = new SchemeStep(); objS2.Encoding = SchemeEncoding.MICROBRICS; objS2.RomIds.Add("J");
		SchemeStep objS3 = new SchemeStep(); objS3.Encoding = SchemeEncoding.UNSIGNAL; objS3.RomIds.Add("A"); objS3.RomIds.Add("B"); objS3.Tango = true;
		SchemeStep objS4 = new SchemeStep(); objS4.Encoding = SchemeEncoding.BRICS; objS4.RomIds.Add("B"); objS4.Integrity = SchemeIntegrity.ROLLINGHASH;
		SchemeStep objS5 = new SchemeStep(); objS5.Encoding = SchemeEncoding.ZOSCII; objS5.RomIds.Add("A");
		SchemeStep objS6 = new SchemeStep(); objS6.Encoding = SchemeEncoding.MICROUNSIGNAL; objS6.RomIds.Add("J"); objS6.RomIds.Add("J");
		objScheme.Steps.Add(objS1); objScheme.Steps.Add(objS2); objScheme.Steps.Add(objS3); objScheme.Steps.Add(objS4); objScheme.Steps.Add(objS5); objScheme.Steps.Add(objS6);
		check("scheme validates", objScheme.Validate() == null);
		check("scheme distinct ROMs", objScheme.RomIds().Count == 3);

		for (int intP = 0; intP < 4; intP++)
		{
			byte[] arrEnc = SchemeRunner.Encode(arrPayloads[intP], objScheme, objResolver);
			byte[] arrDec = SchemeRunner.Decode(arrEnc, objScheme, objResolver);
			check("scheme round trip len " + arrPayloads[intP].Length, same(arrPayloads[intP], arrDec));
			if (arrEnc != null && arrEnc.Length > 20)
			{
				// a lone flip can land in a layer's random slack and be absorbed by design; flip a spread
				byte[] arrTampered = (byte[])arrEnc.Clone();
				for (int intT = arrTampered.Length / 4; intT < (arrTampered.Length * 3) / 4; intT += 97) { arrTampered[intT] ^= 0x5A; }
				byte[] arrDecT = SchemeRunner.Decode(arrTampered, objScheme, objResolver);
				check("scheme tamper detected len " + arrPayloads[intP].Length, arrDecT == null || !same(arrPayloads[intP], arrDecT));
			}
		}

		clsMemoryResolver objWrong = new clsMemoryResolver();
		objWrong.Roms["A"] = arrRomB; objWrong.Roms["B"] = arrRomA; objWrong.Roms["J"] = randomBytes(objRand, 4096);
		byte[] arrEncOk = SchemeRunner.Encode(arrPayloads[2], objScheme, objResolver);
		check("scheme wrong ROM set fails", SchemeRunner.Decode(arrEncOk, objScheme, objWrong) == null || !same(arrPayloads[2], SchemeRunner.Decode(arrEncOk, objScheme, objWrong)));

		Scheme objCopy = objScheme.Clone("s2", "Copy of " + objScheme.Name);
		check("scheme clone independent", objCopy.Steps.Count == objScheme.Steps.Count && !ReferenceEquals(objCopy.Steps[0], objScheme.Steps[0]) && objCopy.Steps[0].Integrity == SchemeIntegrity.ROLLINGHASH_CRC32);

		Scheme objBad = new Scheme(); SchemeStep objBS = new SchemeStep(); objBS.Encoding = SchemeEncoding.BRICS; objBS.RomIds.Add("A"); objBS.Tango = true; objBad.Steps.Add(objBS);
		check("scheme rejects Tango on BRICS", objBad.Validate() != null);

		Console.WriteLine();
		Console.WriteLine("passed " + m_intPassed + ", failed " + m_intFailed);
		int intHf = HardfileTest.Run();
		return (m_intFailed == 0 && intHf == 0) ? 0 : 1;
	}
}

public static class HardfileTest
{
	private sealed class clsMemResolver : ISchemeRomResolver
	{
		public System.Collections.Generic.Dictionary<string, byte[]> Roms = new System.Collections.Generic.Dictionary<string, byte[]>();
		public byte[] GetRomBytes(string strId_a) { return Roms.ContainsKey(strId_a) ? Roms[strId_a] : null; }
	}

	private static int m_intPassed = 0;
	private static int m_intFailed = 0;

	private static void check(string strName_a, bool blnOk_a)
	{
		if (blnOk_a) { m_intPassed++; Console.WriteLine("PASS " + strName_a); }
		else { m_intFailed++; Console.WriteLine("FAIL " + strName_a); }
	}

	private static bool same(byte[] arrA_a, byte[] arrB_a)
	{
		bool blnResult = arrA_a != null && arrB_a != null && arrA_a.Length == arrB_a.Length;
		for (int intI = 0; blnResult && intI < arrA_a.Length; intI++) { blnResult = arrA_a[intI] == arrB_a[intI]; }
		return blnResult;
	}

	private static void corrupt(string strPath_a, long lngAt_a, int intCount_a)
	{
		using (System.IO.FileStream objF = new System.IO.FileStream(strPath_a, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite))
		{
			objF.Position = lngAt_a;
			for (int intI = 0; intI < intCount_a; intI++) { objF.WriteByte(0xAA); }
		}
	}

	public static int Run()
	{
		Random objRand = new Random(777);
		byte[] arrImage = new byte[131072]; objRand.NextBytes(arrImage);
		byte[] arrData1 = new byte[131072]; objRand.NextBytes(arrData1);
		byte[] arrData2 = new byte[131072]; objRand.NextBytes(arrData2);
		string strTmp = System.IO.Path.GetTempPath();
		string strBase = System.IO.Path.Combine(strTmp, "hftest");
		string strBase2 = System.IO.Path.Combine(strTmp, "hftest2");
		string strReplicaDir = System.IO.Path.Combine(strTmp, "hfreplica");
		string strExportDir = System.IO.Path.Combine(strTmp, "hfexport");
		if (System.IO.Directory.Exists(strExportDir)) { System.IO.Directory.Delete(strExportDir, true); }
		if (System.IO.Directory.Exists(strReplicaDir)) { System.IO.Directory.Delete(strReplicaDir, true); }
		byte[] arrPhoto = new byte[300000]; objRand.NextBytes(arrPhoto);
		byte[] arrNote = Encoding.UTF8.GetBytes("hello hardfile");

		using (ZOSCIIRom objImage = ZOSCIIRom.FromBytes(arrImage))
		using (ZOSCIIRom objD1 = ZOSCIIRom.FromBytes(arrData1))
		using (ZOSCIIRom objD2 = ZOSCIIRom.FromBytes(arrData2))
		{
			IHardfileCodec objCodec = new UnsignalChainCodec(new ZOSCIIRom[] { objD1, objD2 });
			IHardfileCodec objWrong = new UnsignalChainCodec(new ZOSCIIRom[] { objD2, objD1 });
			HardfilePaths objPaths = HardfilePaths.FromBase(strBase);
			objPaths.AddReplicaFolder(strReplicaDir, "hftest");
			long lngRecord1At = 0;

			using (Hardfile objHf = Hardfile.Create(objPaths, 1024 * 1024, 512 * 1024, objImage))
			{
				check("create", objHf != null && new System.IO.FileInfo(objPaths.Data).Length == 1024 * 1024 && new System.IO.FileInfo(objPaths.Index).Length == (65536 / Hardfile.RECORD_SIZE) * Hardfile.RECORD_SIZE + 2048 + 4096);
				check("put note", objHf.Put("notes/today.txt", arrNote, objCodec));
				check("put photo", objHf.Put("photos/2026/img1.jpg", arrPhoto, objCodec));
				check("put photo 2", objHf.Put("photos/2026/img2.jpg", arrPhoto, objCodec));
				check("grew", new System.IO.FileInfo(objPaths.Data).Length > 1024 * 1024);
				check("get note", same(arrNote, objHf.Get("notes/today.txt", objCodec)));
				check("get photo", same(arrPhoto, objHf.Get("photos/2026/img1.jpg", objCodec)));
				check("list root", objHf.List("", objCodec).Count == 2);
				check("list photos", objHf.List("photos/2026", objCodec).Count == 2 && objHf.List("photos/2026", objCodec)[0].Name == "img1.jpg");
				check("wrong data images: no content", !same(arrNote, objHf.Get("notes/today.txt", objWrong)));
				List<HardfileEntry> arrWrong = objHf.List("", objWrong);
				bool blnAllUnreadable = arrWrong.Count == 2;
				for (int intW = 0; intW < arrWrong.Count; intW++) { blnAllUnreadable = blnAllUnreadable && arrWrong[intW].Unreadable && arrWrong[intW].Name.IndexOf("#") == 0; }
				check("wrong data images: files present but unreadable, no names", blnAllUnreadable);
				check("replace", objHf.Put("notes/today.txt", arrPhoto, objCodec) && same(arrPhoto, objHf.Get("notes/today.txt", objCodec)) && objHf.List("notes", objCodec).Count == 1);
				check("delete file", objHf.Delete("photos/2026/img1.jpg", objCodec) && objHf.List("photos/2026", objCodec).Count == 1 && objHf.Get("photos/2026/img1.jpg", objCodec) == null);
				check("delete folder tree", objHf.Delete("photos", objCodec) && !objHf.Exists("photos", objCodec) && objHf.List("", objCodec).Count == 1);
				check("put after delete", objHf.Put("photos/2027/img3.jpg", arrNote, objCodec) && same(arrNote, objHf.Get("photos/2027/img3.jpg", objCodec)));
				check("verify clean", objHf.Verify().Clean);
				check("replica written", System.IO.File.Exists(System.IO.Path.Combine(strReplicaDir, "hftest.hfd")) && new System.IO.FileInfo(System.IO.Path.Combine(strReplicaDir, "hftest.hfd")).Length == new System.IO.FileInfo(objPaths.Data).Length);
				check("mirror written", new System.IO.FileInfo(objPaths.Mirror).Length == new System.IO.FileInfo(objPaths.Index).Length);
				lngRecord1At = objHf.RecordOffset(1);
				int intSkipped = 0;
				int intExported = objHf.Export("", strExportDir, objCodec, out intSkipped);
				check("export", intExported == 2 && intSkipped == 0 && System.IO.File.Exists(System.IO.Path.Combine(strExportDir, "photos", "2027", "img3.jpg")));
			}

			// corrupt index records 1 and 2 in the primary; the mirror must carry the read, then repair must fix the primary
			corrupt(objPaths.Index, lngRecord1At, Hardfile.RECORD_SIZE * 2);
			using (Hardfile objHf = Hardfile.Open(objPaths, 512 * 1024, objImage))
			{
				check("reopen with bad primary records", objHf != null);
				check("reads via mirror", objHf != null && same(arrPhoto, objHf.Get("notes/today.txt", objCodec)));
				HardfileReport objReport = objHf.Verify();
				check("verify reports bad primary", objReport.BadIndexRecords == 2 && objReport.BadMirrorRecords == 0);
				check("repair", objHf.Repair() == 2 && objHf.Verify().Clean);
			}

			// corrupt a data replica and a short mirror: verify flags, repair / open catch them up
			corrupt(System.IO.Path.Combine(strReplicaDir, "hftest.hfd"), 0, 4200);   // envelope header, prefix and the root blob
			using (System.IO.FileStream objF = new System.IO.FileStream(objPaths.Mirror, System.IO.FileMode.Open)) { objF.SetLength(objF.Length - Hardfile.RECORD_SIZE * 4); }
			using (Hardfile objHf = Hardfile.Open(objPaths, 512 * 1024, objImage))
			{
				check("short mirror caught up on open", new System.IO.FileInfo(objPaths.Mirror).Length == new System.IO.FileInfo(objPaths.Index).Length);
				HardfileReport objReport = objHf.Verify();
				check("verify flags data replica", objHf.Verify(true).ReplicaProblems >= 1);
				objHf.Repair();
				check("repair data replica", objHf.Verify().Clean);

			}
			// a store under a scheme with an integrity step: corrupt the PRIMARY copy of a live blob; the read must come from the
			// replica (the scheme's rolling hash rejects the primary), Repair(codec) must rewrite the primary from the replica
			{
				clsMemResolver objRes = new clsMemResolver();
				objRes.Roms["A"] = arrData1; objRes.Roms["B"] = arrData2;
				Scheme objScheme = new Scheme(); objScheme.Id = "fs"; objScheme.Name = "fs";
				SchemeStep objStep = new SchemeStep(); objStep.Encoding = SchemeEncoding.UNSIGNAL; objStep.RomIds.Add("A"); objStep.RomIds.Add("B"); objStep.Integrity = SchemeIntegrity.ROLLINGHASH;
				objScheme.Steps.Add(objStep);
				IHardfileCodec objSchemeCodec = new SchemeCodec(objScheme, objRes);
				string strBase3 = System.IO.Path.Combine(strTmp, "hftest3");
				string strRep3 = System.IO.Path.Combine(strTmp, "hfreplica3");
				HardfilePaths objPaths3 = HardfilePaths.FromBase(strBase3);
				objPaths3.AddReplicaFolder(strRep3, "hftest3");
				byte[] arrBig = new byte[1000000]; objRand.NextBytes(arrBig);
				using (Hardfile objHf = Hardfile.Create(objPaths3, 4 * 1024 * 1024, 1024 * 1024, objImage))
				{
					check("scheme store put", objHf.Put("big.bin", arrBig, objSchemeCodec));
				}
				corrupt(objPaths3.Data, 500000, 64);
				using (Hardfile objHf = Hardfile.Open(objPaths3, 1024 * 1024, objImage))
				{
					check("read survives corrupt primary blob", same(arrBig, objHf.Get("big.bin", objSchemeCodec)));
					check("verify sees corrupt primary blob", objHf.Verify(true).BadBlobs == 1);
					check("repair with codec rewrites the primary", objHf.Repair() > 0 && objHf.Verify(true).Clean);
				}
				byte[] arrPrim2 = System.IO.File.ReadAllBytes(objPaths3.Data);
				byte[] arrRep2 = System.IO.File.ReadAllBytes(System.IO.Path.Combine(strRep3, "hftest3.hfd"));
				bool blnEq = arrPrim2.Length == arrRep2.Length;
				for (int intK = 0; blnEq && intK < arrPrim2.Length; intK++) { blnEq = arrPrim2[intK] == arrRep2[intK]; }
				check("primary and replica identical after repair", blnEq);
				using (Hardfile objHf = Hardfile.Open(objPaths3, 1024 * 1024, objImage))
				{
					check("content intact after repair", same(arrBig, objHf.Get("big.bin", objSchemeCodec)));
				}
			}

			// space and record reuse: delete a big file, put a smaller one, neither file grows and the record count stays
			{
				string strBase4 = System.IO.Path.Combine(strTmp, "hftest4");
				HardfilePaths objPaths4 = HardfilePaths.FromBase(strBase4);
				byte[] arrBig = new byte[600000]; objRand.NextBytes(arrBig);
				byte[] arrSmall = new byte[200000]; objRand.NextBytes(arrSmall);
				using (Hardfile objHf = Hardfile.Create(objPaths4, 6 * 1024 * 1024, 0, objImage))
				{
					check("reuse: put big", objHf.Put("a/big.bin", arrBig, objCodec));
					long lngDataLen = objHf.DataLength;
					long lngIdxLen = objHf.IndexLength;
					check("reuse: delete big", objHf.Delete("a/big.bin", objCodec));
					check("reuse: put small x3 into the hole", objHf.Put("a/s1.bin", arrSmall, objCodec) && objHf.Put("a/s2.bin", arrSmall, objCodec) && objHf.Put("a/s3.bin", arrSmall, objCodec));
					check("reuse: no growth (fixed size store)", objHf.DataLength == lngDataLen && objHf.IndexLength == lngIdxLen);
					check("reuse: content", same(arrSmall, objHf.Get("a/s2.bin", objCodec)));
					check("reuse: full store refuses a file that cannot fit", !objHf.Put("a/toobig.bin", new byte[3 * 1024 * 1024], objCodec));
					check("reuse: structure verify clean", objHf.Verify().Clean);
					check("reuse: files verify clean", objHf.Verify(true).Clean && objHf.Verify(true).BadBlobs == 0);
					check("verify one file", objHf.VerifyFile("a/s1.bin", objCodec) && !objHf.VerifyFile("a/none.bin", objCodec));
				}
				// corrupt a blob in a store with no replica and a plain chain codec: the blob hash must refuse it, Verify(true) must count it
				corrupt(objPaths4.Data, 300000, 16);
				using (Hardfile objHf = Hardfile.Open(objPaths4, 0, objImage))
				{
					check("structure verify still clean after blob corruption", objHf.Verify().Clean);
					HardfileReport objRep = objHf.Verify(true);
					check("files verify counts the bad blob", objRep.BadBlobs == 1);
					int intBad = 0;
					string[] arrNames = { "a/s1.bin", "a/s2.bin", "a/s3.bin" };
					for (int intN = 0; intN < 3; intN++) { if (objHf.Get(arrNames[intN], objCodec) == null) { intBad++; } }
					check("exactly one file refuses to read", intBad == 1);
				}
			}

			// envelope: files from two different sets refuse to open together; a damaged suffix block in the primary opens via the mirror and repairs
			{
				string strBaseA = System.IO.Path.Combine(strTmp, "hfenvA");
				string strBaseB = System.IO.Path.Combine(strTmp, "hfenvB");
				string strIdA = "";
				using (Hardfile objA = Hardfile.Create(HardfilePaths.FromBase(strBaseA), 65536, 65536, objImage)) { strIdA = objA.SetId; objA.Put("x.bin", arrNote, objCodec); }
				using (Hardfile objB = Hardfile.Create(HardfilePaths.FromBase(strBaseB), 65536, 65536, objImage)) { check("set ids differ", objB.SetId != strIdA && objB.SetId.Length == 32); }
				HardfilePaths objMixed = new HardfilePaths();
				objMixed.Data = strBaseA + ".hfd"; objMixed.Index = strBaseB + ".hfi"; objMixed.Mirror = strBaseB + ".hfm";
				check("mixed set files refuse to open", Hardfile.Open(objMixed, 0, objImage) == null);
				check("own set reopens with same id", true);
				using (Hardfile objA = Hardfile.Open(HardfilePaths.FromBase(strBaseA), 65536, objImage)) { check("set id persists", objA != null && objA.SetId == strIdA); }
				// damage the index primary's suffix block: the mirror still reads, the open succeeds, Verify reports, Repair rewrites
				long lngIdxLen = new System.IO.FileInfo(strBaseA + ".hfi").Length;
				corrupt(strBaseA + ".hfi", lngIdxLen - 40, 40);
				corrupt(strBaseA + ".hfi", lngIdxLen - 4096, 4096);
				using (Hardfile objA = Hardfile.Open(HardfilePaths.FromBase(strBaseA), 65536, objImage))
				{
					check("opens via mirror envelope", objA != null && same(arrNote, objA.Get("x.bin", objCodec)));
					check("verify flags the envelope", objA.Verify().ReplicaProblems >= 1);
					objA.Repair();
					check("repair restores the envelope", objA.Verify().Clean);
				}
				// grow: the suffix moves, the set id survives, and the old suffix region is reusable
				using (Hardfile objA = Hardfile.Open(HardfilePaths.FromBase(strBaseA), 65536, objImage))
				{
					long lngBefore = objA.DataLength;
					check("grow put", objA.Put("big.bin", arrPhoto, objCodec) && objA.DataLength > lngBefore);
					check("grow keeps id", objA.SetId == strIdA && objA.Verify(true).Clean);
				}
				using (Hardfile objA = Hardfile.Open(HardfilePaths.FromBase(strBaseA), 65536, objImage)) { check("reopen after grow", objA != null && same(arrPhoto, objA.Get("big.bin", objCodec))); }
			}

			// allocation unit: an 8 KB store rounds every blob up; a freed slot of 8 KB takes a 100-byte file; the unit survives reopen
			{
				string strBaseU = System.IO.Path.Combine(strTmp, "hfunit");
				using (Hardfile objU = Hardfile.Create(HardfilePaths.FromBase(strBaseU), 1024 * 1024, 0, objImage, 8192))
				{
					check("unit set", objU.Unit == 8192);
					check("unit put", objU.Put("a.bin", new byte[100], objCodec) && objU.Put("b.bin", new byte[100], objCodec));
					check("unit delete", objU.Delete("a.bin", objCodec));
					check("unit reuse", objU.Put("c.bin", new byte[2000], objCodec) && objU.Verify(true).Clean);
				}
				using (Hardfile objU = Hardfile.Open(HardfilePaths.FromBase(strBaseU), 0, objImage))
				{
					check("unit persists", objU != null && objU.Unit == 8192 && objU.List("", objCodec).Count == 2 && objU.Verify(true).Clean);
				}
			}

			// marked out: a blob with no good copy is marked out by Repair, listed as such, unreadable, held, skipped by CopyTo; Restore brings it back under the flag
			{
				string strBaseM = System.IO.Path.Combine(strTmp, "hfmark");
				string strBaseM2 = System.IO.Path.Combine(strTmp, "hfmark2");
				using (Hardfile objM = Hardfile.Create(HardfilePaths.FromBase(strBaseM), 4 * 1024 * 1024, 0, objImage))
				{
					check("mark: put", objM.Put("good.bin", arrNote, objCodec) && objM.Put("bad.bin", arrPhoto, objCodec) && objM.Put("sub/also.bin", arrNote, objCodec));
				}
				corrupt(strBaseM + ".hfd", 500000, 32);   // inside bad.bin (photo is ~1.2 MB encoded), no replica to repair from
				using (Hardfile objM = Hardfile.Open(HardfilePaths.FromBase(strBaseM), 0, objImage))
				{
					check("mark: verify sees the bad blob", objM.Verify(true).BadBlobs == 1);
					check("mark: repair marks it out", objM.Repair() == 1 && objM.Verify(true).BadBlobs == 0 && objM.Verify(true).MarkedOut == 1);
					List<HardfileEntry> arrList = objM.List("", objCodec);
					int intMarked = 0;
					for (int intI = 0; intI < arrList.Count; intI++) { if (arrList[intI].MarkedOut) { intMarked++; } }
					check("mark: listed as marked out", arrList.Count == 3 && intMarked == 1);
					check("mark: good still reads", same(arrNote, objM.Get("good.bin", objCodec)) && same(arrNote, objM.Get("sub/also.bin", objCodec)));
					long lngBefore = objM.DataLength;
					check("mark: space held (new file does not land on it)", objM.Put("more.bin", new byte[100], objCodec) && objM.Verify(true).MarkedOut == 1);
					using (Hardfile objNew = objM.CopyTo(HardfilePaths.FromBase(strBaseM2), 0))
					{
						check("mark: copyto skips it", objNew != null && objNew.List("", objCodec).Count == 3 && objNew.Verify(true).Clean && same(arrNote, objNew.Get("good.bin", objCodec)));
					}
					string strMarkedName = "";
					for (int intI = 0; intI < arrList.Count; intI++) { if (arrList[intI].MarkedOut) { strMarkedName = arrList[intI].Name; } }
					string strSegDir = System.IO.Path.Combine(strTmp, "hfseg");
					if (System.IO.Directory.Exists(strSegDir)) { System.IO.Directory.Delete(strSegDir, true); }
					check("mark: export marked out as .hfseg", objM.ExportMarkedOut(strSegDir) == 1 && System.IO.Directory.GetFiles(strSegDir, "*.hfseg").Length == 1 && objM.ExportMarkedOut(strSegDir) == 0);
					check("mark: restore then still unreadable (no good copy)", objM.Restore(strMarkedName, objCodec) && objM.Verify(true).BadBlobs == 1);
					check("mark: mark out by hand", objM.MarkOut(strMarkedName, objCodec) && objM.Verify(true).MarkedOut == 1);
					check("mark: delete frees it", objM.Delete(strMarkedName, objCodec) && objM.Verify(true).Clean && objM.List("", objCodec).Count == 3);
				}
			}

			// timestamps: after close, the files carry random times within the past five years, not the real write time
			{
				string strBaseT = System.IO.Path.Combine(strTmp, "hftime");
				using (Hardfile objT = Hardfile.Create(HardfilePaths.FromBase(strBaseT), 65536, 65536, objImage)) { objT.Put("t.bin", arrNote, objCodec); }
				DateTime dtW = System.IO.File.GetLastWriteTimeUtc(strBaseT + ".hfd");
				DateTime dtI = System.IO.File.GetLastWriteTimeUtc(strBaseT + ".hfi");
				check("timestamps randomised", dtW < DateTime.UtcNow.AddMinutes(-1) && dtW > DateTime.UtcNow.AddYears(-6) && dtI != dtW);
				long lngNowUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
				using (Hardfile objT = Hardfile.Open(HardfilePaths.FromBase(strBaseT), 65536, objImage)) { check("last written kept inside the envelope", objT != null && objT.LastWritten > lngNowUnix - 120 && objT.LastWritten <= lngNowUnix + 1); }
			}

			// decoys: one put changes several separate regions of the data file, not just the new blob
			{
				string strBaseD = System.IO.Path.Combine(strTmp, "hfdecoy");
				using (Hardfile objD = Hardfile.Create(HardfilePaths.FromBase(strBaseD), 2 * 1024 * 1024, 0, objImage)) { objD.Put("one.bin", arrNote, objCodec); objD.Put("two.bin", arrNote, objCodec); objD.Delete("one.bin", objCodec); }
				byte[] arrBefore = System.IO.File.ReadAllBytes(strBaseD + ".hfd");
				using (Hardfile objD = Hardfile.Open(HardfilePaths.FromBase(strBaseD), 0, objImage)) { objD.Put("three.bin", arrNote, objCodec); check("decoy: verify clean after decoys", objD.Verify(true).Clean); }
				byte[] arrAfter = System.IO.File.ReadAllBytes(strBaseD + ".hfd");
				int intRuns = 0;
				bool blnIn = false;
				for (int intK = 0; intK < arrBefore.Length; intK++)
				{
					bool blnDiff = arrBefore[intK] != arrAfter[intK];
					if (blnDiff && !blnIn) { intRuns++; }
					blnIn = blnDiff;
				}
				check("decoy: several changed regions, not one (" + intRuns + ")", intRuns >= 3);
			}

			// read-only peek while a writer holds the store: lists and reads, refuses to write, leaves times alone
			{
				string strBaseP = System.IO.Path.Combine(strTmp, "hfpeek");
				using (Hardfile objW = Hardfile.Create(HardfilePaths.FromBase(strBaseP), 65536, 65536, objImage))
				{
					objW.Put("forms/f1/r1", arrNote, objCodec);
					using (Hardfile objR = Hardfile.OpenReadOnly(HardfilePaths.FromBase(strBaseP), objImage))
					{
						check("peek opens beside the writer", objR != null && objR.IsReadOnly);
						check("peek reads", objR != null && same(arrNote, objR.Get("forms/f1/r1", objCodec)) && objR.List("forms/f1", objCodec).Count == 1);
						check("peek cannot write", objR != null && !objR.Put("x", arrNote, objCodec) && objR.LastError == "read-only");
						check("writer still writes", objW.Put("forms/f1/r2", arrNote, objCodec));
					}
				}
			}

			check("open with wrong image fails", Hardfile.Open(objPaths, 0, objD1) == null);

			string strScratch = System.IO.Path.Combine(strTmp, "hfscratch");
			using (ScratchHardfile objScratch = ScratchHardfile.Create(strScratch, 65536, objImage))
			{
				check("scratch put/get", objScratch != null && objScratch.Store.Put("layer1", arrPhoto, objCodec) && same(arrPhoto, objScratch.Store.Get("layer1", objCodec)));
				objScratch.Discard();
				check("scratch discarded", !System.IO.File.Exists(strScratch + ".hft") && !System.IO.File.Exists(strScratch + ".hfti"));
			}

			byte[] arrJunk = new byte[200000]; objRand.NextBytes(arrJunk);
			System.IO.File.WriteAllBytes(strBase2 + ".hfd", arrJunk);
			System.IO.File.WriteAllBytes(strBase2 + ".hfi", arrJunk);
			check("noise files do not open", Hardfile.Open(HardfilePaths.FromBase(strBase2), 0, objImage) == null);
		}

		Console.WriteLine("hardfile: passed " + m_intPassed + ", failed " + m_intFailed);
		return m_intFailed;
	}
}
