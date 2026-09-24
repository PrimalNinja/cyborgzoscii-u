// Cyborg ZOSCII Web Radio Serve - zwrserve v20260924
// (c) 2026 Cyborg Unicorn Pty Ltd.
// UNINTELLIGENCE License.
// ZOSCII core logic remains under MIT License.
//
// Streams MP3 messages from a ZOSCII MQ queue to any number of ICY
// (Icecast / SHOUTcast) listeners over HTTP. Console output shows what is
// playing and who is connected.
//
// Usage:
//   zwrserve -i <port> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-s] [-w] [-p <seconds>] [-e <mp3file>]
//
//   -i  ICY protocol: listen on <port>, pull from <queue> at <mqurl>
//   -z  unZOSCII each message with <romfile>
//   -u  unUNSIGNAL each message with <romfile>
//   -s  shared pointer: one playhead, every listener hears the same thing
//       (default: each listener gets their own session and playhead)
//   -w  wait for new messages when the end of the queue is reached
//       (default: loop to the start of the queue)
//   -p  seconds between polls when waiting for new messages (default 10)
//   -e  local MP3 played from the top, looped, while waiting for new messages
//       (default: silence)
//
// Working files in <exepath>/t/<port>/  (one folder per port, so several instances can share the exe)
//   <session#>_<yyyyMMddHHmmss>.ptr   line 1 = MQ pointer (last completed message)
//                                     line 2 = message currently in the .bin
//                                     line 3 = byte offset of the next frame in the .bin
//   <session#>.bin                    current payload being streamed
//
// Listener URLs (default mode):
//   http://host:port/        new session
//   http://host:port/<n>     resume session <n> (created if it does not exist)
// Shared mode (-s) uses session 0 and ignores the path.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CyborgUnicorn.ZOSCII;

public static class ZWRServe
{
	// -------------------------------------------------------------------------
	// Constants
	// -------------------------------------------------------------------------

	internal const string VERSION = "v20260924";
	internal const int META_INTERVAL = 16000;          // bytes of audio between ICY metadata blocks
	internal const double LEAD_SECONDS = 4.0;          // burst sent ahead of real time so players start quickly
	internal const int SHARED_QUEUE_LIMIT = 1048576;   // shared mode: drop a listener that falls this many bytes behind
	internal const int SEND_TIMEOUT_MS = 30000;
	internal const int REQUEST_TIMEOUT_MS = 5000;
	internal const int PROBE_SECONDS = 5;               // a brand new session that lasts less than this leaves no files behind
	internal const int TAKEOVER_WAIT_MS = 3000;         // how long a takeover waits for the old connection's thread to let go

	internal const int DECODE_NONE = 0;
	internal const int DECODE_ZOSCII = 1;
	internal const int DECODE_UNSIGNAL = 2;

	// -------------------------------------------------------------------------
	// Globals
	// -------------------------------------------------------------------------

	internal static int g_intPort = 0;
	internal static string g_strMQURL = "";
	internal static string g_strQueue = "";
	internal static string g_strROMFile = "";
	internal static int g_intDecodeMode = DECODE_NONE;
	internal static ZOSCIIRom g_objRom = null;
	internal static bool g_blnShared = false;
	internal static bool g_blnLoop = true;
	internal static int g_intPollSeconds = 10;
	internal static string g_strTempFolder = "";
	internal static string g_strElevatorFile = "";
	internal static List<MP3Frame> g_objElevator = null;
	internal static string g_strElevatorTitle = "";

	internal static volatile bool g_blnStopping = false;
	internal static int g_intListenerCount = 0;
	internal static int g_intNextSession = 1;
	internal static SharedStation g_objStation = null;

	private static readonly object g_objConsoleLock = new object();
	private static readonly object g_objSessionLock = new object();
	private static readonly Dictionary<int, SessionClaim> g_objActiveSessions = new Dictionary<int, SessionClaim>();

	// -------------------------------------------------------------------------
	// Entry point
	// -------------------------------------------------------------------------

	public static int Main(string[] arrArgs_a)
	{
		int intResult = 1;

		Console.WriteLine("ZOSCII Web Radio Serve " + VERSION);
		Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd");
		Console.WriteLine();

		if (parseArgs(arrArgs_a))
		{
			if (initialise())
			{
				runServer();
				intResult = 0;
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
		Console.WriteLine("Usage: zwrserve -i <port> <mqurl> <queue> [-z <romfile> | -u <romfile>] [-s] [-w] [-p <seconds>] [-e <mp3file>]");
		Console.WriteLine();
		Console.WriteLine("  -i  ICY (Icecast/SHOUTcast) server on <port>, streaming <queue> from <mqurl>");
		Console.WriteLine("  -z  unZOSCII messages with <romfile>");
		Console.WriteLine("  -u  unUNSIGNAL messages with <romfile>");
		Console.WriteLine("  -s  shared pointer (radio) - default is one session per listener");
		Console.WriteLine("  -w  wait for new messages at the end of the queue - default is loop to the start");
		Console.WriteLine("  -p  poll interval in seconds while waiting (default 10)");
		Console.WriteLine("  -e  MP3 looped from the top while waiting (default silence)");
		Console.WriteLine();
		Console.WriteLine("Example: zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom");
		Console.WriteLine("Listen:  http://localhost:8000/        new session");
		Console.WriteLine("         http://localhost:8000/<n>     resume session <n>");
	}

	private static bool parseArgs(string[] arrArgs_a)
	{
		bool blnResult = true;
		bool blnHaveProtocol = false;
		int intI = 0;

		while (blnResult && intI < arrArgs_a.Length)
		{
			string strArg = arrArgs_a[intI].ToLowerInvariant();

			if (strArg == "-i" && intI + 3 < arrArgs_a.Length)
			{
				blnResult = int.TryParse(arrArgs_a[intI + 1], out g_intPort) && g_intPort > 0 && g_intPort < 65536;
				g_strMQURL = arrArgs_a[intI + 2];
				g_strQueue = arrArgs_a[intI + 3];
				blnHaveProtocol = true;
				intI += 4;
			}
			else if ((strArg == "-z" || strArg == "-u") && intI + 1 < arrArgs_a.Length)
			{
				g_intDecodeMode = (strArg == "-z") ? DECODE_ZOSCII : DECODE_UNSIGNAL;
				g_strROMFile = arrArgs_a[intI + 1];
				intI += 2;
			}
			else if (strArg == "-p" && intI + 1 < arrArgs_a.Length)
			{
				blnResult = int.TryParse(arrArgs_a[intI + 1], out g_intPollSeconds) && g_intPollSeconds > 0;
				intI += 2;
			}
			else if (strArg == "-e" && intI + 1 < arrArgs_a.Length)
			{
				g_strElevatorFile = arrArgs_a[intI + 1];
				intI += 2;
			}
			else if (strArg == "-s")
			{
				g_blnShared = true;
				intI++;
			}
			else if (strArg == "-w")
			{
				g_blnLoop = false;
				intI++;
			}
			else
			{
				Console.WriteLine("Unknown or incomplete argument: " + arrArgs_a[intI]);
				blnResult = false;
			}
		}

		if (blnResult && !blnHaveProtocol)
		{
			blnResult = false;
		}

		return blnResult;
	}

	private static bool initialise()
	{
		bool blnResult = true;

		try
		{
			g_strTempFolder = Path.Combine(AppContext.BaseDirectory, "t", g_intPort.ToString());
			Directory.CreateDirectory(g_strTempFolder);
		}
		catch (Exception objEx)
		{
			Console.WriteLine("Error: cannot create working folder " + g_strTempFolder + " - " + objEx.Message);
			blnResult = false;
		}

		if (blnResult && g_intDecodeMode != DECODE_NONE)
		{
			g_objRom = ZOSCIIRom.FromFile(g_strROMFile);

			if (g_objRom.Size == 0)
			{
				Console.WriteLine("Error: cannot load ROM " + g_strROMFile);
				blnResult = false;
			}
		}

		if (blnResult && g_strElevatorFile.Length > 0)
		{
			blnResult = loadElevator();
		}

		if (blnResult)
		{
			g_intNextSession = findHighestSession() + 1;
		}

		return blnResult;
	}

	// Reads every frame of the elevator MP3 into memory once; all sessions share it.
	private static bool loadElevator()
	{
		bool blnResult = false;

		try
		{
			using (MP3Reader objReader = new MP3Reader(g_strElevatorFile, 0))
			{
				if (objReader.IsMP3)
				{
					List<MP3Frame> objFrames = new List<MP3Frame>();
					MP3Frame objFrame = objReader.ReadFrame();

					while (objFrame != null)
					{
						objFrames.Add(objFrame);
						objFrame = objReader.ReadFrame();
					}

					if (objFrames.Count > 0)
					{
						g_objElevator = objFrames;
						g_strElevatorTitle = objReader.Title;
						blnResult = true;
					}
				}
			}
		}
		catch { }

		if (!blnResult)
		{
			Console.WriteLine("Error: cannot load elevator MP3 " + g_strElevatorFile);
		}

		return blnResult;
	}

	// Highest session number already present in t/ so new sessions never collide with old ones.
	private static int findHighestSession()
	{
		int intResult = 0;

		try
		{
			string[] arrFiles = Directory.GetFiles(g_strTempFolder);
			int intI = 0;

			for (intI = 0; intI < arrFiles.Length; intI++)
			{
				string strName = Path.GetFileName(arrFiles[intI]);
				int intEnd = 0;

				while (intEnd < strName.Length && char.IsDigit(strName[intEnd]))
				{
					intEnd++;
				}

				if (intEnd > 0 && intEnd <= 9)
				{
					int intSession = int.Parse(strName.Substring(0, intEnd));
					if (intSession > intResult)
					{
						intResult = intSession;
					}
				}
			}
		}
		catch { }

		return intResult;
	}

	// -------------------------------------------------------------------------
	// Server
	// -------------------------------------------------------------------------

	private static void runServer()
	{
		TcpListener objListener = null;
		Thread objStationThread = null;

		try
		{
			objListener = new TcpListener(IPAddress.Any, g_intPort);
			objListener.Start();
		}
		catch (Exception objEx)
		{
			Log("Error: cannot listen on port " + g_intPort + " - " + objEx.Message);
			objListener = null;
		}

		if (objListener != null)
		{
			Console.CancelKeyPress += (objSender_a, objArgs_a) =>
			{
				objArgs_a.Cancel = true;
				if (!g_blnStopping)
				{
					Log("Stopping...");
					g_blnStopping = true;
					try { objListener.Stop(); } catch { }
				}
			};

			// Console window closed / terminated: stop and give sessions a moment to save their pointers.
			AppDomain.CurrentDomain.ProcessExit += (objSender_a, objArgs_a) =>
			{
				if (!g_blnStopping)
				{
					Stopwatch objWait = Stopwatch.StartNew();

					g_blnStopping = true;
					try { objListener.Stop(); } catch { }

					while (Volatile.Read(ref g_intListenerCount) > 0 && objWait.ElapsedMilliseconds < 3000)
					{
						Thread.Sleep(50);
					}

					Thread.Sleep(250);
				}
			};

			Log("Queue:     " + g_strQueue + " @ " + g_strMQURL);
			Log("Decode:    " + (g_intDecodeMode == DECODE_ZOSCII ? "ZOSCII (" + g_strROMFile + ")" : (g_intDecodeMode == DECODE_UNSIGNAL ? "UNSIGNAL (" + g_strROMFile + ")" : "none")));
			Log("Mode:      " + (g_blnShared ? "shared pointer (session 0)" : "session per listener") + (g_blnLoop ? ", loop" : ", wait at end"));
			Log("Waiting:   " + ((g_objElevator != null) ? "elevator " + g_strElevatorFile + " (" + g_objElevator[0].SampleRate + "Hz/" + g_objElevator[0].Channels + "ch, " + g_objElevator.Count + " frames)" : "silence"));
			Log("Work dir:  " + g_strTempFolder);
			Log("Listening: http://0.0.0.0:" + g_intPort + "/  (Ctrl+C to stop)");

			if (g_blnShared)
			{
				g_objStation = new SharedStation();
				objStationThread = new Thread(g_objStation.Run);
				objStationThread.IsBackground = true;
				objStationThread.Start();
			}

			while (!g_blnStopping)
			{
				try
				{
					TcpClient objClient = objListener.AcceptTcpClient();
					Thread objThread = new Thread(handleClient);
					objThread.IsBackground = true;
					objThread.Start(objClient);
				}
				catch (Exception objEx)
				{
					if (!g_blnStopping)
					{
						Log("Accept error: " + objEx.Message);
					}
				}
			}

			// Give listener threads and the station a moment to save their pointers.
			{
				Stopwatch objWait = Stopwatch.StartNew();

				while ((Volatile.Read(ref g_intListenerCount) > 0 || (objStationThread != null && objStationThread.IsAlive)) && objWait.ElapsedMilliseconds < 5000)
				{
					Thread.Sleep(100);
				}
			}

			Log("Stopped.");
		}
	}

	private static void handleClient(object objState_a)
	{
		TcpClient objClient = (TcpClient)objState_a;
		Socket objSocket = objClient.Client;
		string strIP = "?";

		try
		{
			strIP = ((IPEndPoint)objSocket.RemoteEndPoint).Address.ToString();
			objSocket.NoDelay = true;
			objSocket.SendTimeout = SEND_TIMEOUT_MS;
			objSocket.ReceiveTimeout = REQUEST_TIMEOUT_MS;

			string strRequest = readRequest(objSocket);

			if (strRequest != null)
			{
				string strMethod = "";
				string strPath = "";
				bool blnMeta = false;

				parseRequest(strRequest, out strMethod, out strPath, out blnMeta);

				if (strMethod != "GET" && strMethod != "HEAD")
				{
					sendSimple(objSocket, "405 Method Not Allowed");
				}
				else if (strPath == "favicon.ico" || strPath == "robots.txt")
				{
					sendSimple(objSocket, "404 Not Found");
				}
				else if (g_blnShared)
				{
					serveShared(objSocket, strIP, strMethod == "HEAD", blnMeta);
				}
				else
				{
					serveSession(objSocket, strIP, strPath, strMethod == "HEAD", blnMeta);
				}
			}
		}
		catch (Exception objEx)
		{
			Log("[!] " + strIP + " " + objEx.Message);
		}

		try { objSocket.Shutdown(SocketShutdown.Both); } catch { }
		try { objClient.Close(); } catch { }
	}

	// Default mode: one session per listener.
	private static void serveSession(Socket objSocket_a, string strIP_a, string strPath_a, bool blnHeadOnly_a, bool blnMeta_a)
	{
		int intSession = 0;
		bool blnValidPath = true;

		if (strPath_a.Length > 0)
		{
			blnValidPath = int.TryParse(strPath_a, out intSession) && intSession > 0;
		}

		if (!blnValidPath)
		{
			sendSimple(objSocket_a, "404 Not Found");
		}
		else if (blnHeadOnly_a)
		{
			// HEAD doesn't use up a session number.
			sendText(objSocket_a, buildHeaders(intSession, blnMeta_a));
		}
		else
		{
			SessionClaim objClaim = claimSession(ref intSession, objSocket_a, strIP_a);

			try
			{
				int intCount = Interlocked.Increment(ref g_intListenerCount);

				sendText(objSocket_a, buildHeaders(intSession, blnMeta_a));
				Log("[+] " + strIP_a + " session " + intSession + (blnMeta_a ? " (icy-meta)" : "") + "  listeners: " + intCount);

				try
				{
					streamSession(objSocket_a, intSession, objClaim, blnMeta_a, strIP_a);
				}
				finally
				{
					intCount = Interlocked.Decrement(ref g_intListenerCount);
					Log("[-] " + strIP_a + " session " + intSession + "  listeners: " + intCount);
				}
			}
			finally
			{
				releaseSession(intSession, objClaim);
			}
		}
	}

	private static void streamSession(Socket objSocket_a, int intSession_a, SessionClaim objClaim_a, bool blnMeta_a, string strIP_a)
	{
		ListenerOutput objOutput = new ListenerOutput(objSocket_a, blnMeta_a);
		string strPrefix = "[" + intSession_a + "] ";
		Func<bool> fnIsCurrent = () => IsCurrentClaim(intSession_a, objClaim_a);
		Stopwatch objClock = Stopwatch.StartNew();
		Func<bool> fnSettled = () => objClock.Elapsed.TotalSeconds >= PROBE_SECONDS;

		using (TrackSource objSource = new TrackSource(intSession_a, strPrefix, fnIsCurrent, fnSettled))
		{
			double dblSent = 0.0;
			bool blnOk = true;

			while (blnOk && !g_blnStopping)
			{
				MP3Frame objFrame = objSource.NextFrame(() => g_blnStopping || IsClosed(objSocket_a) || !fnIsCurrent());

				if (objFrame == null)
				{
					blnOk = false;
				}
				else
				{
					// A fetch/decode gap between tracks is caught up afterwards (sent faster than real time)
					// so the listener's buffer refills, but by no more than LEAD_SECONDS.
					if (dblSent < objClock.Elapsed.TotalSeconds - ZWRServe.LEAD_SECONDS)
					{
						dblSent = objClock.Elapsed.TotalSeconds - ZWRServe.LEAD_SECONDS;
					}

					waitUntil(objClock, dblSent - LEAD_SECONDS);

					objOutput.SetTitle(objSource.Title);
					blnOk = objOutput.Write(objFrame.Data, 0, objFrame.Data.Length);
					dblSent += objFrame.Duration;
				}
			}

			// Browsers open a stream twice (page load, then the player). A connection that only lived a
			// moment was the throwaway one: a brand new session leaves no files behind, and an existing
			// session was never saved or moved on (see TrackSource settled), so its position is untouched.
			if (!fnSettled() && !g_blnStopping && fnIsCurrent())
			{
				if (objSource.IsNewSession)
				{
					objSource.Discard();
					Log("[~] " + strIP_a + " session " + intSession_a + " closed within " + PROBE_SECONDS + "s, files removed");
				}
				else
				{
					Log("[~] " + strIP_a + " session " + intSession_a + " closed within " + PROBE_SECONDS + "s, position kept");
				}
			}
		}
	}

	// Shared mode: all listeners attach to the station.
	private static void serveShared(Socket objSocket_a, string strIP_a, bool blnHeadOnly_a, bool blnMeta_a)
	{
		sendText(objSocket_a, buildHeaders(0, blnMeta_a));

		if (!blnHeadOnly_a)
		{
			SharedListener objListener = new SharedListener();
			ListenerOutput objOutput = new ListenerOutput(objSocket_a, blnMeta_a);
			int intCount = Interlocked.Increment(ref g_intListenerCount);
			bool blnOk = true;

			Log("[+] " + strIP_a + " shared" + (blnMeta_a ? " (icy-meta)" : "") + "  listeners: " + intCount);
			g_objStation.AddListener(objListener);

			try
			{
				while (blnOk && !g_blnStopping)
				{
					List<BroadcastItem> objItems = objListener.Take(250);
					int intI = 0;

					if (objItems.Count == 0)
					{
						blnOk = !IsClosed(objSocket_a);
					}

					for (intI = 0; blnOk && intI < objItems.Count; intI++)
					{
						objOutput.SetTitle(objItems[intI].Title);
						blnOk = objOutput.Write(objItems[intI].Data, 0, objItems[intI].Data.Length);
					}

					if (blnOk && objListener.Dropped)
					{
						Log("[!] " + strIP_a + " too slow, dropped");
						blnOk = false;
					}
				}
			}
			finally
			{
				g_objStation.RemoveListener(objListener);
				intCount = Interlocked.Decrement(ref g_intListenerCount);
				Log("[-] " + strIP_a + " shared  listeners: " + intCount);
			}
		}
	}

	// -------------------------------------------------------------------------
	// Session bookkeeping
	// -------------------------------------------------------------------------

	// intSession_a == 0 means allocate a new one. If the session is already streaming, the new
	// connection takes it over: the old connection is cut off and its thread can no longer save
	// the pointer or write the .bin (see IsCurrentClaim).
	private static SessionClaim claimSession(ref int intSession_a, Socket objSocket_a, string strIP_a)
	{
		SessionClaim objNew = new SessionClaim();
		SessionClaim objOld = null;

		objNew.Socket = objSocket_a;

		lock (g_objSessionLock)
		{
			if (intSession_a == 0)
			{
				intSession_a = g_intNextSession;
				g_intNextSession++;
			}
			else if (intSession_a >= g_intNextSession)
			{
				g_intNextSession = intSession_a + 1;
			}

			g_objActiveSessions.TryGetValue(intSession_a, out objOld);
			g_objActiveSessions[intSession_a] = objNew;
		}

		if (objOld != null)
		{
			Log("[>] " + strIP_a + " session " + intSession_a + " taken over by a new connection");
			try { objOld.Socket.Shutdown(SocketShutdown.Both); } catch { }
			try { objOld.Socket.Close(); } catch { }

			// Let the old thread close its .bin before this one might overwrite it.
			objOld.Released.WaitOne(TAKEOVER_WAIT_MS);
		}

		return objNew;
	}

	private static void releaseSession(int intSession_a, SessionClaim objClaim_a)
	{
		lock (g_objSessionLock)
		{
			SessionClaim objCurrent = null;

			if (g_objActiveSessions.TryGetValue(intSession_a, out objCurrent) && objCurrent == objClaim_a)
			{
				g_objActiveSessions.Remove(intSession_a);
			}
		}

		objClaim_a.Released.Set();
	}

	// False once another connection has taken the session over.
	internal static bool IsCurrentClaim(int intSession_a, SessionClaim objClaim_a)
	{
		bool blnResult = false;

		lock (g_objSessionLock)
		{
			SessionClaim objCurrent = null;
			blnResult = g_objActiveSessions.TryGetValue(intSession_a, out objCurrent) && objCurrent == objClaim_a;
		}

		return blnResult;
	}

	// -------------------------------------------------------------------------
	// HTTP helpers
	// -------------------------------------------------------------------------

	private static string readRequest(Socket objSocket_a)
	{
		string strResult = null;
		byte[] arrBuffer = new byte[8192];
		int intTotal = 0;
		bool blnDone = false;

		try
		{
			while (!blnDone && intTotal < arrBuffer.Length)
			{
				int intRead = objSocket_a.Receive(arrBuffer, intTotal, arrBuffer.Length - intTotal, SocketFlags.None);

				if (intRead <= 0)
				{
					blnDone = true;
				}
				else
				{
					intTotal += intRead;
					string strText = Encoding.ASCII.GetString(arrBuffer, 0, intTotal);

					if (strText.Contains("\r\n\r\n") || strText.Contains("\n\n"))
					{
						strResult = strText;
						blnDone = true;
					}
				}
			}
		}
		catch { }

		return strResult;
	}

	private static void parseRequest(string strRequest_a, out string strMethod_a, out string strPath_a, out bool blnMeta_a)
	{
		string[] arrLines = strRequest_a.Replace("\r", "").Split('\n');
		string[] arrParts = arrLines[0].Split(' ');
		int intI = 0;

		strMethod_a = arrParts[0].ToUpperInvariant();
		strPath_a = (arrParts.Length > 1) ? arrParts[1] : "/";
		blnMeta_a = false;

		// Path: drop query string, slashes and a trailing .mp3 so /12, /12/ and /12.mp3 all mean session 12.
		{
			int intQuery = strPath_a.IndexOf('?');
			if (intQuery >= 0)
			{
				strPath_a = strPath_a.Substring(0, intQuery);
			}

			strPath_a = strPath_a.Trim('/');

			if (strPath_a.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
			{
				strPath_a = strPath_a.Substring(0, strPath_a.Length - 4);
			}
		}

		for (intI = 1; intI < arrLines.Length; intI++)
		{
			int intColon = arrLines[intI].IndexOf(':');

			if (intColon > 0)
			{
				string strName = arrLines[intI].Substring(0, intColon).Trim().ToLowerInvariant();
				string strValue = arrLines[intI].Substring(intColon + 1).Trim();

				if (strName == "icy-metadata" && strValue == "1")
				{
					blnMeta_a = true;
				}
			}
		}
	}

	// HTTP/1.0 status line rather than "ICY 200 OK" so browser <audio> elements accept the stream.
	private static string buildHeaders(int intSession_a, bool blnMeta_a)
	{
		StringBuilder objSB = new StringBuilder();

		objSB.Append("HTTP/1.0 200 OK\r\n");
		objSB.Append("Content-Type: audio/mpeg\r\n");
		objSB.Append("Cache-Control: no-cache, no-store\r\n");
		objSB.Append("Pragma: no-cache\r\n");
		objSB.Append("Connection: close\r\n");
		objSB.Append("Access-Control-Allow-Origin: *\r\n");
		objSB.Append("Access-Control-Expose-Headers: X-ZWR-Session\r\n");
		objSB.Append("icy-name: ZOSCII Web Radio - " + g_strQueue + "\r\n");
		objSB.Append("icy-pub: 0\r\n");
		objSB.Append("X-ZWR-Session: " + intSession_a + "\r\n");

		if (blnMeta_a)
		{
			objSB.Append("icy-metaint: " + META_INTERVAL + "\r\n");
		}

		objSB.Append("\r\n");

		return objSB.ToString();
	}

	private static void sendSimple(Socket objSocket_a, string strStatus_a)
	{
		string strBody = strStatus_a + "\r\n";
		sendText(objSocket_a, "HTTP/1.0 " + strStatus_a + "\r\nContent-Type: text/plain\r\nContent-Length: " + strBody.Length + "\r\nConnection: close\r\n\r\n" + strBody);
	}

	private static void sendText(Socket objSocket_a, string strText_a)
	{
		byte[] arrData = Encoding.ASCII.GetBytes(strText_a);
		SendAll(objSocket_a, arrData, 0, arrData.Length);
	}

	internal static bool SendAll(Socket objSocket_a, byte[] arrData_a, int intOffset_a, int intCount_a)
	{
		bool blnResult = true;
		int intSent = 0;

		try
		{
			while (blnResult && intSent < intCount_a)
			{
				int intN = objSocket_a.Send(arrData_a, intOffset_a + intSent, intCount_a - intSent, SocketFlags.None);

				if (intN <= 0)
				{
					blnResult = false;
				}
				else
				{
					intSent += intN;
				}
			}
		}
		catch
		{
			blnResult = false;
		}

		return blnResult;
	}

	// True once the remote end has closed the connection.
	internal static bool IsClosed(Socket objSocket_a)
	{
		bool blnResult = false;

		try
		{
			if (objSocket_a.Poll(0, SelectMode.SelectRead))
			{
				byte[] arrPeek = new byte[1];
				blnResult = (objSocket_a.Available == 0) || (objSocket_a.Receive(arrPeek, SocketFlags.Peek) == 0);
			}
		}
		catch
		{
			blnResult = true;
		}

		return blnResult;
	}

	// Sleep until the clock reaches dblTarget_a seconds (or we are stopping).
	internal static void waitUntil(Stopwatch objClock_a, double dblTarget_a)
	{
		double dblAhead = dblTarget_a - objClock_a.Elapsed.TotalSeconds;

		while (dblAhead > 0 && !g_blnStopping)
		{
			Thread.Sleep((int)Math.Min(50.0, dblAhead * 1000.0) + 1);
			dblAhead = dblTarget_a - objClock_a.Elapsed.TotalSeconds;
		}
	}

	// -------------------------------------------------------------------------
	// Console
	// -------------------------------------------------------------------------

	internal static void Log(string strText_a)
	{
		lock (g_objConsoleLock)
		{
			Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + strText_a);
		}
	}
}

// -----------------------------------------------------------------------------
// SessionClaim - which connection currently owns a session
// -----------------------------------------------------------------------------

internal class SessionClaim
{
	public Socket Socket;
	public readonly ManualResetEvent Released = new ManualResetEvent(false);
}

// -----------------------------------------------------------------------------
// ListenerOutput - writes audio to one listener, inserting ICY metadata if asked
// -----------------------------------------------------------------------------

internal class ListenerOutput
{
	private Socket m_objSocket;
	private bool m_blnMeta;
	private int m_intBytesToMeta;
	private string m_strTitle;
	private string m_strSentTitle;

	public ListenerOutput(Socket objSocket_a, bool blnMeta_a)
	{
		m_objSocket = objSocket_a;
		m_blnMeta = blnMeta_a;
		m_intBytesToMeta = ZWRServe.META_INTERVAL;
		m_strTitle = "";
		m_strSentTitle = null;
	}

	public void SetTitle(string strTitle_a)
	{
		m_strTitle = strTitle_a ?? "";
	}

	public bool Write(byte[] arrData_a, int intOffset_a, int intCount_a)
	{
		bool blnResult = true;

		if (!m_blnMeta)
		{
			blnResult = ZWRServe.SendAll(m_objSocket, arrData_a, intOffset_a, intCount_a);
		}
		else
		{
			int intPos = intOffset_a;
			int intRemaining = intCount_a;

			while (blnResult && intRemaining > 0)
			{
				int intChunk = Math.Min(intRemaining, m_intBytesToMeta);

				blnResult = ZWRServe.SendAll(m_objSocket, arrData_a, intPos, intChunk);
				intPos += intChunk;
				intRemaining -= intChunk;
				m_intBytesToMeta -= intChunk;

				if (blnResult && m_intBytesToMeta == 0)
				{
					byte[] arrMeta = buildMetaBlock();
					blnResult = ZWRServe.SendAll(m_objSocket, arrMeta, 0, arrMeta.Length);
					m_intBytesToMeta = ZWRServe.META_INTERVAL;
				}
			}
		}

		return blnResult;
	}

	// Title is only sent when it changes; otherwise a single zero length byte.
	private byte[] buildMetaBlock()
	{
		byte[] arrResult = new byte[] { 0 };

		if (m_strTitle != m_strSentTitle)
		{
			string strTitle = m_strTitle.Replace("'", "’");
			byte[] arrText = Encoding.UTF8.GetBytes("StreamTitle='" + strTitle + "';");

			// Max block is 255 * 16 bytes. Trim the title (by characters) until it fits.
			while (arrText.Length > 255 * 16 && strTitle.Length > 0)
			{
				strTitle = strTitle.Substring(0, strTitle.Length - 1);
				arrText = Encoding.UTF8.GetBytes("StreamTitle='" + strTitle + "';");
			}

			{
				int intBlocks = (arrText.Length + 15) / 16;
				arrResult = new byte[1 + intBlocks * 16];
				arrResult[0] = (byte)intBlocks;
				Array.Copy(arrText, 0, arrResult, 1, arrText.Length);
			}

			m_strSentTitle = m_strTitle;
		}

		return arrResult;
	}
}

// -----------------------------------------------------------------------------
// TrackSource - one session's walk through the queue, frame by frame
// -----------------------------------------------------------------------------

internal class TrackSource : IDisposable
{
	private int m_intSession;
	private string m_strPrefix;
	private string m_strBinPath;
	private string m_strPointer;        // last fully played message
	private string m_strCurrent;        // message currently in the .bin ("" if none)
	private long m_lngResumeOffset;     // byte offset to resume at in the .bin
	private MP3Reader m_objReader;
	private MQClient m_objMQ;
	private string m_strTitle;
	private bool m_blnWaiting;
	private bool m_blnPlayedSinceLoop;
	private int m_intLastSampleRate;
	private int m_intLastChannels;
	private byte[] m_arrLastHeader;     // header of the last real frame, used to shape silence
	private Stopwatch m_objPollClock;
	private long m_lngNextPollMs;
	private int m_intElevatorIndex;
	private Func<bool> m_fnIsCurrent;   // false once another connection has taken this session over
	private bool m_blnNewSession;       // no .ptr existed when the session started
	private Func<bool> m_fnSettled;     // false for the first few seconds of a connection
	private bool m_blnDiscarded;
	private bool m_blnDisposed;

	public TrackSource(int intSession_a, string strPrefix_a, Func<bool> fnIsCurrent_a, Func<bool> fnSettled_a)
	{
		m_intSession = intSession_a;
		m_strPrefix = strPrefix_a;
		m_strBinPath = Path.Combine(ZWRServe.g_strTempFolder, intSession_a + ".bin");
		m_strPointer = "";
		m_strCurrent = "";
		m_lngResumeOffset = 0;
		m_objReader = null;
		m_objMQ = new MQClient(60);
		m_strTitle = "";
		m_blnWaiting = false;
		m_blnPlayedSinceLoop = false;
		m_intLastSampleRate = 0;
		m_intLastChannels = 0;
		m_arrLastHeader = null;
		m_objPollClock = Stopwatch.StartNew();
		m_lngNextPollMs = 0;
		m_intElevatorIndex = 0;
		m_fnIsCurrent = fnIsCurrent_a;
		m_fnSettled = fnSettled_a;
		m_blnNewSession = true;
		m_blnDiscarded = false;
		m_blnDisposed = false;

		loadPointer();
	}

	public string Title
	{
		get { return m_strTitle; }
	}

	public bool IsNewSession
	{
		get { return m_blnNewSession; }
	}

	// Remove this session's .ptr and .bin, and don't save on Dispose.
	public void Discard()
	{
		if (m_objReader != null)
		{
			m_objReader.Dispose();
			m_objReader = null;
		}

		deletePointers("");

		try { File.Delete(m_strBinPath); } catch { }

		m_blnDiscarded = true;
	}

	// Next frame to send, moving through the queue as tracks finish.
	// While waiting for new messages it returns silent frames, so listeners stay connected.
	// Returns null only when fnAbort_a says to stop.
	public MP3Frame NextFrame(Func<bool> fnAbort_a)
	{
		MP3Frame objResult = null;
		bool blnDone = false;

		while (!blnDone)
		{
			if (m_objReader != null)
			{
				objResult = m_objReader.ReadFrame();

				if (objResult != null)
				{
					checkFormatChange(objResult);
					blnDone = true;
				}
				else if (!mayMoveOn())
				{
					// Track ended during a new connection's first seconds: hold here with silence
					// so a throwaway connection can't move a resumed session on.
					objResult = buildSilentFrame();
					blnDone = true;
				}
				else
				{
					finishTrack();
				}
			}
			else if (fnAbort_a())
			{
				blnDone = true;
			}
			else if (m_blnWaiting && m_objPollClock.ElapsedMilliseconds < m_lngNextPollMs)
			{
				objResult = waitingFrame();
				blnDone = true;
			}
			else
			{
				openNextTrack(fnAbort_a);
			}
		}

		return objResult;
	}

	private void finishTrack()
	{
		m_objReader.Dispose();
		m_objReader = null;
		m_strPointer = m_strCurrent;
		m_strCurrent = "";
		m_lngResumeOffset = 0;
		savePointer();
	}

	private void openNextTrack(Func<bool> fnAbort_a)
	{
		// Resume the track that was in the .bin when this session last stopped.
		if (m_strCurrent.Length > 0)
		{
			if (File.Exists(m_strBinPath))
			{
				m_objReader = openReader(m_strCurrent, m_lngResumeOffset);
			}

			if (m_objReader != null)
			{
				ZWRServe.Log(m_strPrefix + "resume  " + m_strTitle + "  @" + m_lngResumeOffset);
			}
			else
			{
				m_strCurrent = "";
				m_lngResumeOffset = 0;
			}
		}
		else
		{
			MQFetchResult objFetch = m_objMQ.FetchNext(ZWRServe.g_strMQURL, ZWRServe.g_strQueue, m_strPointer);

			if (objFetch.HasMessage)
			{
				m_blnWaiting = false;
				acceptMessage(objFetch);
			}
			else
			{
				endOfQueue();
			}
		}
	}

	private void acceptMessage(MQFetchResult objFetch_a)
	{
		byte[] arrPayload = objFetch_a.EncodedBytes;

		if (ZWRServe.g_intDecodeMode == ZWRServe.DECODE_ZOSCII)
		{
			arrPayload = ZDecode.Bytes(arrPayload, ZWRServe.g_objRom);
		}
		else if (ZWRServe.g_intDecodeMode == ZWRServe.DECODE_UNSIGNAL)
		{
			arrPayload = UDecode.Bytes(arrPayload, ZWRServe.g_objRom);
		}

		if (!m_fnIsCurrent())
		{
			// Taken over while fetching - the new connection owns the .bin and .ptr now.
		}
		else if (arrPayload == null)
		{
			ZWRServe.Log(m_strPrefix + "skip    " + objFetch_a.Filename + "  (decode failed)");
			skipMessage(objFetch_a.Pointer);
		}
		else
		{
			bool blnWritten = false;

			try
			{
				File.WriteAllBytes(m_strBinPath, arrPayload);
				blnWritten = true;
			}
			catch (Exception objEx)
			{
				ZWRServe.Log(m_strPrefix + "error   cannot write " + m_strBinPath + " - " + objEx.Message);
			}

			if (blnWritten)
			{
				m_objReader = openReader(objFetch_a.Filename, 0);

				if (m_objReader == null)
				{
					ZWRServe.Log(m_strPrefix + "skip    " + objFetch_a.Filename + "  (not MP3, " + arrPayload.Length + " bytes)");
					skipMessage(objFetch_a.Pointer);
				}
				else
				{
					m_strCurrent = objFetch_a.Pointer;
					m_lngResumeOffset = 0;
					m_blnPlayedSinceLoop = true;
					savePointer();
					ZWRServe.Log(m_strPrefix + "playing " + m_strTitle + "  (" + objFetch_a.Filename + ", " + arrPayload.Length + " bytes)");
				}
			}
			else
			{
				// Disk problem - don't spin on it.
				Thread.Sleep(ZWRServe.g_intPollSeconds * 1000);
			}
		}
	}

	private void skipMessage(string strPointer_a)
	{
		m_strPointer = strPointer_a;
		m_strCurrent = "";
		m_lngResumeOffset = 0;
		savePointer();
	}

	private void endOfQueue()
	{
		if (ZWRServe.g_blnLoop && m_strPointer.Length > 0 && m_blnPlayedSinceLoop)
		{
			ZWRServe.Log(m_strPrefix + "end of queue, looping to start");
			m_strPointer = "";
			m_blnPlayedSinceLoop = false;
			savePointer();
		}
		else
		{
			if (!m_blnWaiting)
			{
				MQCheckStatus objStatus = m_objMQ.Check(ZWRServe.g_strMQURL, ZWRServe.g_strQueue, m_strPointer);
				string strFiller = (ZWRServe.g_objElevator != null) ? "elevator" : "silence";

				if (objStatus == MQCheckStatus.Error)
				{
					ZWRServe.Log(m_strPrefix + "MQ error or unreachable, " + strFiller + ", retrying every " + ZWRServe.g_intPollSeconds + "s");
				}
				else
				{
					ZWRServe.Log(m_strPrefix + "end of queue, " + strFiller + " until a new message arrives");
				}

				m_blnWaiting = true;
				m_strTitle = ZWRServe.g_strElevatorTitle;
				m_intElevatorIndex = 0;

				if (ZWRServe.g_objElevator != null && m_intLastSampleRate != 0 &&
					(ZWRServe.g_objElevator[0].SampleRate != m_intLastSampleRate || ZWRServe.g_objElevator[0].Channels != m_intLastChannels))
				{
					ZWRServe.Log(m_strPrefix + "warning elevator is " + ZWRServe.g_objElevator[0].SampleRate + "Hz/" + ZWRServe.g_objElevator[0].Channels + "ch, queue is " + m_intLastSampleRate + "Hz/" + m_intLastChannels + "ch, some players may glitch");
				}
			}

			// Silence is sent until the next poll.
			m_lngNextPollMs = m_objPollClock.ElapsedMilliseconds + ZWRServe.g_intPollSeconds * 1000L;

			// Looping with nothing playable in the whole queue: next pass starts again from the top.
			if (ZWRServe.g_blnLoop && !m_blnPlayedSinceLoop && m_strPointer.Length > 0)
			{
				m_strPointer = "";
				savePointer();
			}
		}
	}

	// While waiting: the elevator MP3 from the top, looped, or silence if there isn't one.
	private MP3Frame waitingFrame()
	{
		MP3Frame objResult = null;

		if (ZWRServe.g_objElevator != null)
		{
			objResult = ZWRServe.g_objElevator[m_intElevatorIndex];
			m_intElevatorIndex = (m_intElevatorIndex + 1) % ZWRServe.g_objElevator.Count;
		}
		else
		{
			objResult = buildSilentFrame();
		}

		return objResult;
	}

	// Lowest-bitrate frame in the same MPEG version, sample rate and channel mode as the last real
	// frame, with zeroed side info and data - decodes as silence. Defaults to 44.1kHz stereo.
	private MP3Frame buildSilentFrame()
	{
		MP3Frame objResult = new MP3Frame();
		byte[] arrHeader = new byte[4];
		int intSampleRate = 0;
		int intSamples = 0;
		int intChannels = 0;
		int intLength = 0;

		arrHeader[0] = 0xFF;

		if (m_arrLastHeader != null)
		{
			arrHeader[1] = (byte)(m_arrLastHeader[1] | 0x01);          // no CRC
			arrHeader[2] = (byte)(0x10 | (m_arrLastHeader[2] & 0x0C));  // bitrate index 1, same sample rate, no padding
			arrHeader[3] = (byte)(m_arrLastHeader[3] & 0xC0);          // same channel mode
		}
		else
		{
			arrHeader[1] = 0xFB;    // MPEG 1 Layer III, no CRC
			arrHeader[2] = 0x10;    // 32kbps, 44.1kHz
			arrHeader[3] = 0x00;    // stereo
		}

		intLength = MP3Reader.parseHeader(arrHeader, 0, out intSampleRate, out intSamples, out intChannels);

		objResult.Data = new byte[intLength];
		Array.Copy(arrHeader, objResult.Data, 4);
		objResult.Duration = (double)intSamples / intSampleRate;
		objResult.SampleRate = intSampleRate;
		objResult.Channels = intChannels;

		return objResult;
	}

	private MP3Reader openReader(string strMessage_a, long lngOffset_a)
	{
		MP3Reader objResult = null;

		try
		{
			MP3Reader objReader = new MP3Reader(m_strBinPath, lngOffset_a);

			if (objReader.IsMP3)
			{
				objResult = objReader;
				m_strTitle = (objReader.Title.Length > 0) ? objReader.Title : strMessage_a;
			}
			else
			{
				objReader.Dispose();
			}
		}
		catch (Exception objEx)
		{
			ZWRServe.Log(m_strPrefix + "error   cannot read " + m_strBinPath + " - " + objEx.Message);
		}

		return objResult;
	}

	private void checkFormatChange(MP3Frame objFrame_a)
	{
		if (m_intLastSampleRate != 0 && (objFrame_a.SampleRate != m_intLastSampleRate || objFrame_a.Channels != m_intLastChannels))
		{
			ZWRServe.Log(m_strPrefix + "warning format change " + m_intLastSampleRate + "Hz/" + m_intLastChannels + "ch -> " + objFrame_a.SampleRate + "Hz/" + objFrame_a.Channels + "ch, some players may glitch");
		}

		m_intLastSampleRate = objFrame_a.SampleRate;
		m_intLastChannels = objFrame_a.Channels;

		if (m_arrLastHeader == null)
		{
			m_arrLastHeader = new byte[4];
		}

		Array.Copy(objFrame_a.Data, m_arrLastHeader, 4);
	}

	// ---------------------------------------------------------------------
	// Pointer file:  <session>_<yyyyMMddHHmmss>.ptr
	// ---------------------------------------------------------------------

	private void loadPointer()
	{
		try
		{
			string[] arrFiles = Directory.GetFiles(ZWRServe.g_strTempFolder, m_intSession + "_*.ptr");

			if (arrFiles.Length > 0)
			{
				string strNewest = arrFiles[0];

				m_blnNewSession = false;
				int intI = 0;

				for (intI = 1; intI < arrFiles.Length; intI++)
				{
					if (string.CompareOrdinal(Path.GetFileName(arrFiles[intI]), Path.GetFileName(strNewest)) > 0)
					{
						strNewest = arrFiles[intI];
					}
				}

				{
					string[] arrLines = File.ReadAllLines(strNewest);

					if (arrLines.Length > 0) { m_strPointer = arrLines[0].Trim(); }
					if (arrLines.Length > 1) { m_strCurrent = arrLines[1].Trim(); }
					if (arrLines.Length > 2) { long.TryParse(arrLines[2].Trim(), out m_lngResumeOffset); }
				}

				// Leftovers from a crash between write and delete.
				for (intI = 0; intI < arrFiles.Length; intI++)
				{
					if (arrFiles[intI] != strNewest)
					{
						try { File.Delete(arrFiles[intI]); } catch { }
					}
				}
			}
		}
		catch (Exception objEx)
		{
			ZWRServe.Log(m_strPrefix + "error   cannot read pointer - " + objEx.Message);
		}
	}

	// An existing session is only saved or moved to the next message once its connection has settled.
	// A brand new session has nothing to protect, so it always may.
	private bool mayMoveOn()
	{
		return m_blnNewSession || m_fnSettled();
	}

	// Writes a fresh <session>_<now>.ptr then removes older ones, so the filename always carries last-used time.
	// Does nothing once the session has been taken over by another connection, or discarded.
	private void savePointer()
	{
		if (m_fnIsCurrent() && !m_blnDiscarded && mayMoveOn())
		{
			try
			{
				string strName = m_intSession + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".ptr";
				string strPath = Path.Combine(ZWRServe.g_strTempFolder, strName);
				long lngOffset = (m_objReader != null) ? m_objReader.Position : m_lngResumeOffset;

				File.WriteAllText(strPath, m_strPointer + "\n" + m_strCurrent + "\n" + lngOffset + "\n");
				deletePointers(strName);
			}
			catch (Exception objEx)
			{
				ZWRServe.Log(m_strPrefix + "error   cannot write pointer - " + objEx.Message);
			}
		}
	}

	// Deletes this session's .ptr files except strKeep_a ("" = delete all).
	private void deletePointers(string strKeep_a)
	{
		try
		{
			string[] arrOld = Directory.GetFiles(ZWRServe.g_strTempFolder, m_intSession + "_*.ptr");
			int intI = 0;

			for (intI = 0; intI < arrOld.Length; intI++)
			{
				if (Path.GetFileName(arrOld[intI]) != strKeep_a)
				{
					try { File.Delete(arrOld[intI]); } catch { }
				}
			}
		}
		catch { }
	}

	public void Dispose()
	{
		if (!m_blnDisposed)
		{
			savePointer();

			if (m_objReader != null)
			{
				m_objReader.Dispose();
				m_objReader = null;
			}

			m_blnDisposed = true;
		}
	}
}

// -----------------------------------------------------------------------------
// Shared mode: one station, many listeners
// -----------------------------------------------------------------------------

internal class BroadcastItem
{
	public byte[] Data;
	public string Title;
	public double Duration;
}

internal class SharedListener
{
	private readonly object m_objLock = new object();
	private readonly Queue<BroadcastItem> m_objQueue = new Queue<BroadcastItem>();
	private readonly AutoResetEvent m_objSignal = new AutoResetEvent(false);
	private int m_intQueuedBytes = 0;

	public volatile bool Dropped = false;

	// Returns false (and marks the listener dropped) if it is too far behind.
	public bool Offer(BroadcastItem objItem_a)
	{
		bool blnResult = true;

		lock (m_objLock)
		{
			if (m_intQueuedBytes + objItem_a.Data.Length > ZWRServe.SHARED_QUEUE_LIMIT)
			{
				Dropped = true;
				blnResult = false;
			}
			else
			{
				m_objQueue.Enqueue(objItem_a);
				m_intQueuedBytes += objItem_a.Data.Length;
			}
		}

		m_objSignal.Set();
		return blnResult;
	}

	public List<BroadcastItem> Take(int intWaitMs_a)
	{
		List<BroadcastItem> objResult = new List<BroadcastItem>();

		m_objSignal.WaitOne(intWaitMs_a);

		lock (m_objLock)
		{
			while (m_objQueue.Count > 0)
			{
				objResult.Add(m_objQueue.Dequeue());
			}

			m_intQueuedBytes = 0;
		}

		return objResult;
	}
}

internal class SharedStation
{
	private readonly object m_objLock = new object();
	private readonly List<SharedListener> m_objListeners = new List<SharedListener>();
	private readonly Queue<BroadcastItem> m_objBurst = new Queue<BroadcastItem>();
	private double m_dblBurstSeconds = 0.0;

	public void AddListener(SharedListener objListener_a)
	{
		lock (m_objLock)
		{
			foreach (BroadcastItem objItem in m_objBurst)
			{
				objListener_a.Offer(objItem);
			}

			m_objListeners.Add(objListener_a);
		}
	}

	public void RemoveListener(SharedListener objListener_a)
	{
		lock (m_objLock)
		{
			m_objListeners.Remove(objListener_a);
		}
	}

	// The playhead runs whether or not anyone is listening - it's radio.
	public void Run()
	{
		using (TrackSource objSource = new TrackSource(0, "[shared] ", () => true, () => true))
		{
			Stopwatch objClock = Stopwatch.StartNew();
			double dblSent = 0.0;
			bool blnRunning = true;

			while (blnRunning && !ZWRServe.g_blnStopping)
			{
				MP3Frame objFrame = objSource.NextFrame(() => ZWRServe.g_blnStopping);

				if (objFrame == null)
				{
					blnRunning = false;
				}
				else
				{
					BroadcastItem objItem = new BroadcastItem();
					objItem.Data = objFrame.Data;
					objItem.Title = objSource.Title;
					objItem.Duration = objFrame.Duration;

					// A fetch/decode gap between tracks is caught up afterwards (sent faster than real time)
					// so the listener's buffer refills, but by no more than LEAD_SECONDS.
					if (dblSent < objClock.Elapsed.TotalSeconds - ZWRServe.LEAD_SECONDS)
					{
						dblSent = objClock.Elapsed.TotalSeconds - ZWRServe.LEAD_SECONDS;
					}

					ZWRServe.waitUntil(objClock, dblSent);
					dblSent += objFrame.Duration;

					lock (m_objLock)
					{
						int intI = 0;

						m_objBurst.Enqueue(objItem);
						m_dblBurstSeconds += objItem.Duration;

						while (m_dblBurstSeconds > ZWRServe.LEAD_SECONDS && m_objBurst.Count > 1)
						{
							m_dblBurstSeconds -= m_objBurst.Dequeue().Duration;
						}

						for (intI = 0; intI < m_objListeners.Count; intI++)
						{
							m_objListeners[intI].Offer(objItem);
						}
					}
				}
			}
		}
	}
}

// -----------------------------------------------------------------------------
// MP3 frame reading
// -----------------------------------------------------------------------------

internal class MP3Frame
{
	public byte[] Data;
	public double Duration;
	public int SampleRate;
	public int Channels;
}

internal class MP3Reader : IDisposable
{
	private static readonly int[] s_arrBitrateV1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
	private static readonly int[] s_arrBitrateV2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

	// Indexed by the 2 version bits: 0 = MPEG 2.5, 1 = reserved, 2 = MPEG 2, 3 = MPEG 1
	private static readonly int[,] s_arrSampleRate =
	{
		{ 11025, 12000, 8000 },
		{ 0, 0, 0 },
		{ 22050, 24000, 16000 },
		{ 44100, 48000, 32000 }
	};

	private const int SYNC_SCAN = 65536;

	private FileStream m_objStream;
	private long m_lngAudioEnd;
	private long m_lngPosition;
	private byte[] m_arrHeader = new byte[4];

	public bool IsMP3 = false;
	public string Title = "";

	// lngStartOffset_a > 0 resumes from that byte (resynced to the next frame).
	public MP3Reader(string strPath_a, long lngStartOffset_a)
	{
		long lngAudioStart = 0;
		string strV1Title = "";
		string strV1Artist = "";
		string strV2Title = "";
		string strV2Artist = "";

		m_objStream = new FileStream(strPath_a, FileMode.Open, FileAccess.Read, FileShare.Read);
		m_lngAudioEnd = m_objStream.Length;

		// ID3v1 tag: last 128 bytes starting "TAG"
		if (m_lngAudioEnd >= 128)
		{
			byte[] arrTag = readAt(m_lngAudioEnd - 128, 128);

			if (arrTag.Length == 128 && arrTag[0] == 'T' && arrTag[1] == 'A' && arrTag[2] == 'G')
			{
				m_lngAudioEnd -= 128;
				strV1Title = latin1(arrTag, 3, 30);
				strV1Artist = latin1(arrTag, 33, 30);
			}
		}

		// ID3v2 tag at the start
		{
			byte[] arrHead = readAt(0, 10);

			if (arrHead.Length == 10 && arrHead[0] == 'I' && arrHead[1] == 'D' && arrHead[2] == '3')
			{
				int intTagSize = syncsafe(arrHead, 6);
				bool blnFooter = (arrHead[5] & 0x10) != 0;

				lngAudioStart = 10 + intTagSize + (blnFooter ? 10 : 0);
				readID3v2(arrHead[3], arrHead[5], intTagSize, out strV2Title, out strV2Artist);
			}
		}

		// Title: "Artist - Title" from ID3v2, falling back to ID3v1.
		{
			string strTitle = (strV2Title.Length > 0) ? strV2Title : strV1Title;
			string strArtist = (strV2Artist.Length > 0) ? strV2Artist : strV1Artist;

			if (strArtist.Length > 0 && strTitle.Length > 0)
			{
				Title = strArtist + " - " + strTitle;
			}
			else
			{
				Title = strTitle + strArtist;
			}
		}

		// It's an MP3 only if two consecutive valid Layer III frame headers are found near the start.
		{
			long lngFirst = findSync(lngAudioStart);

			IsMP3 = (lngFirst >= 0);
			m_lngPosition = lngFirst;

			if (IsMP3 && lngStartOffset_a > lngFirst)
			{
				long lngResume = findSync(lngStartOffset_a);

				if (lngResume >= 0)
				{
					m_lngPosition = lngResume;
				}
			}
		}
	}

	// Byte offset of the next frame to be read.
	public long Position
	{
		get { return m_lngPosition; }
	}

	public MP3Frame ReadFrame()
	{
		MP3Frame objResult = null;
		bool blnSearching = IsMP3;

		while (blnSearching && m_lngPosition + 4 <= m_lngAudioEnd)
		{
			int intSampleRate = 0;
			int intSamples = 0;
			int intChannels = 0;
			int intLength = 0;

			m_objStream.Seek(m_lngPosition, SeekOrigin.Begin);
			readFully(m_arrHeader, 4);
			intLength = parseHeader(m_arrHeader, 0, out intSampleRate, out intSamples, out intChannels);

			if (intLength > 0 && m_lngPosition + intLength <= m_lngAudioEnd)
			{
				objResult = new MP3Frame();
				objResult.Data = new byte[intLength];
				Array.Copy(m_arrHeader, objResult.Data, 4);
				readFully(objResult.Data, 4, intLength - 4);
				objResult.Duration = (double)intSamples / intSampleRate;
				objResult.SampleRate = intSampleRate;
				objResult.Channels = intChannels;
				m_lngPosition += intLength;
				blnSearching = false;
			}
			else
			{
				// Junk between frames (or a truncated last frame): find the next real frame.
				long lngNext = findSync(m_lngPosition + 1);

				if (lngNext < 0)
				{
					m_lngPosition = m_lngAudioEnd;
				}
				else
				{
					m_lngPosition = lngNext;
				}
			}
		}

		if (blnSearching)
		{
			m_lngPosition = m_lngAudioEnd;
		}

		return objResult;
	}

	// Frame length for a valid MPEG Layer III header at arrData_a[intOffset_a], else 0.
	internal static int parseHeader(byte[] arrData_a, int intOffset_a, out int intSampleRate_a, out int intSamples_a, out int intChannels_a)
	{
		int intResult = 0;
		int intB0 = arrData_a[intOffset_a];
		int intB1 = arrData_a[intOffset_a + 1];
		int intB2 = arrData_a[intOffset_a + 2];
		int intB3 = arrData_a[intOffset_a + 3];

		intSampleRate_a = 0;
		intSamples_a = 0;
		intChannels_a = 0;

		if (intB0 == 0xFF && (intB1 & 0xE0) == 0xE0)
		{
			int intVersion = (intB1 >> 3) & 3;
			int intLayer = (intB1 >> 1) & 3;
			int intBitrateIdx = (intB2 >> 4) & 15;
			int intRateIdx = (intB2 >> 2) & 3;
			int intPadding = (intB2 >> 1) & 1;
			int intChannelMode = (intB3 >> 6) & 3;
			int intEmphasis = intB3 & 3;

			// Layer III (bits 01), not reserved version, not free/bad bitrate, not reserved rate or emphasis.
			if (intVersion != 1 && intLayer == 1 && intBitrateIdx != 0 && intBitrateIdx != 15 && intRateIdx != 3 && intEmphasis != 2)
			{
				int intBitrate = ((intVersion == 3) ? s_arrBitrateV1[intBitrateIdx] : s_arrBitrateV2[intBitrateIdx]) * 1000;
				int intRate = s_arrSampleRate[intVersion, intRateIdx];

				intResult = ((intVersion == 3) ? 144 : 72) * intBitrate / intRate + intPadding;
				intSampleRate_a = intRate;
				intSamples_a = (intVersion == 3) ? 1152 : 576;
				intChannels_a = (intChannelMode == 3) ? 1 : 2;
			}
		}

		return intResult;
	}

	// First offset >= lngFrom_a (within SYNC_SCAN) holding a valid header whose frame is followed by
	// another valid header, or ends exactly at the end of the audio. -1 if none.
	private long findSync(long lngFrom_a)
	{
		long lngResult = -1;

		if (lngFrom_a >= 0 && lngFrom_a < m_lngAudioEnd)
		{
			int intWindow = (int)Math.Min(m_lngAudioEnd - lngFrom_a, SYNC_SCAN + 4096);
			byte[] arrBuf = readAt(lngFrom_a, intWindow);
			int intLimit = Math.Min(arrBuf.Length - 4, SYNC_SCAN);
			int intI = 0;

			while (lngResult < 0 && intI <= intLimit)
			{
				int intSampleRate = 0;
				int intSamples = 0;
				int intChannels = 0;
				int intLength = parseHeader(arrBuf, intI, out intSampleRate, out intSamples, out intChannels);

				if (intLength > 0)
				{
					long lngNext = lngFrom_a + intI + intLength;

					if (lngNext == m_lngAudioEnd)
					{
						lngResult = lngFrom_a + intI;
					}
					else if (intI + intLength + 4 <= arrBuf.Length)
					{
						if (parseHeader(arrBuf, intI + intLength, out intSampleRate, out intSamples, out intChannels) > 0)
						{
							lngResult = lngFrom_a + intI;
						}
					}
				}

				intI++;
			}
		}

		return lngResult;
	}

	// Reads TIT2/TPE1 (v2.3/v2.4) or TT2/TP1 (v2.2).
	private void readID3v2(int intMajor_a, int intFlags_a, int intTagSize_a, out string strTitle_a, out string strArtist_a)
	{
		long lngPos = 10;
		long lngEnd = 10 + (long)intTagSize_a;
		bool blnDone = false;

		strTitle_a = "";
		strArtist_a = "";

		try
		{
			// Extended header
			if ((intFlags_a & 0x40) != 0 && intMajor_a >= 3)
			{
				byte[] arrExt = readAt(lngPos, 4);
				if (arrExt.Length == 4)
				{
					lngPos += (intMajor_a == 4) ? syncsafe(arrExt, 0) : (4 + bigEndian(arrExt, 0, 4));
				}
			}

			while (!blnDone)
			{
				int intHeaderSize = (intMajor_a == 2) ? 6 : 10;
				byte[] arrFrame = (lngPos + intHeaderSize <= lngEnd) ? readAt(lngPos, intHeaderSize) : new byte[0];

				if (arrFrame.Length < intHeaderSize || arrFrame[0] == 0)
				{
					blnDone = true;
				}
				else
				{
					string strID = Encoding.ASCII.GetString(arrFrame, 0, (intMajor_a == 2) ? 3 : 4);
					int intSize = 0;

					if (intMajor_a == 2)
					{
						intSize = bigEndian(arrFrame, 3, 3);
					}
					else if (intMajor_a == 4)
					{
						intSize = syncsafe(arrFrame, 4);
					}
					else
					{
						intSize = bigEndian(arrFrame, 4, 4);
					}

					if (intSize <= 0 || lngPos + intHeaderSize + intSize > lngEnd)
					{
						blnDone = true;
					}
					else
					{
						bool blnTitle = (strID == "TIT2" || strID == "TT2");
						bool blnArtist = (strID == "TPE1" || strID == "TP1");

						if (blnTitle || blnArtist)
						{
							byte[] arrText = readAt(lngPos + intHeaderSize, Math.Min(intSize, 1024));
							string strText = decodeID3Text(arrText);

							if (blnTitle) { strTitle_a = strText; }
							if (blnArtist) { strArtist_a = strText; }
						}

						lngPos += intHeaderSize + intSize;
						blnDone = (strTitle_a.Length > 0 && strArtist_a.Length > 0);
					}
				}
			}
		}
		catch { }
	}

	private static string decodeID3Text(byte[] arrText_a)
	{
		string strResult = "";

		if (arrText_a.Length > 1)
		{
			int intEncoding = arrText_a[0];
			int intStart = 1;
			int intCount = arrText_a.Length - 1;
			Encoding objEnc = Encoding.GetEncoding(28591);

			if (intEncoding == 1)
			{
				objEnc = Encoding.Unicode;

				if (intCount >= 2 && arrText_a[1] == 0xFE && arrText_a[2] == 0xFF)
				{
					objEnc = Encoding.BigEndianUnicode;
				}

				if (intCount >= 2 && ((arrText_a[1] == 0xFF && arrText_a[2] == 0xFE) || (arrText_a[1] == 0xFE && arrText_a[2] == 0xFF)))
				{
					intStart += 2;
					intCount -= 2;
				}
			}
			else if (intEncoding == 2)
			{
				objEnc = Encoding.BigEndianUnicode;
			}
			else if (intEncoding == 3)
			{
				objEnc = Encoding.UTF8;
			}

			strResult = objEnc.GetString(arrText_a, intStart, intCount);

			// v2.4 allows several null separated values - keep the first.
			{
				int intNull = strResult.IndexOf('\0');
				if (intNull >= 0)
				{
					strResult = strResult.Substring(0, intNull);
				}
			}

			strResult = strResult.Trim();
		}

		return strResult;
	}

	private static string latin1(byte[] arrData_a, int intOffset_a, int intCount_a)
	{
		string strResult = Encoding.GetEncoding(28591).GetString(arrData_a, intOffset_a, intCount_a);
		int intNull = strResult.IndexOf('\0');

		if (intNull >= 0)
		{
			strResult = strResult.Substring(0, intNull);
		}

		return strResult.Trim();
	}

	private static int syncsafe(byte[] arrData_a, int intOffset_a)
	{
		return ((arrData_a[intOffset_a] & 0x7F) << 21) | ((arrData_a[intOffset_a + 1] & 0x7F) << 14) | ((arrData_a[intOffset_a + 2] & 0x7F) << 7) | (arrData_a[intOffset_a + 3] & 0x7F);
	}

	private static int bigEndian(byte[] arrData_a, int intOffset_a, int intCount_a)
	{
		int intResult = 0;
		int intI = 0;

		for (intI = 0; intI < intCount_a; intI++)
		{
			intResult = (intResult << 8) | arrData_a[intOffset_a + intI];
		}

		return intResult;
	}

	private byte[] readAt(long lngOffset_a, int intCount_a)
	{
		byte[] arrResult = new byte[0];

		if (lngOffset_a >= 0 && intCount_a > 0 && lngOffset_a < m_objStream.Length)
		{
			int intAvailable = (int)Math.Min(intCount_a, m_objStream.Length - lngOffset_a);
			byte[] arrBuf = new byte[intAvailable];

			m_objStream.Seek(lngOffset_a, SeekOrigin.Begin);
			readFully(arrBuf, intAvailable);
			arrResult = arrBuf;
		}

		return arrResult;
	}

	private void readFully(byte[] arrBuf_a, int intCount_a)
	{
		readFully(arrBuf_a, 0, intCount_a);
	}

	private void readFully(byte[] arrBuf_a, int intOffset_a, int intCount_a)
	{
		int intDone = 0;
		bool blnEOF = false;

		while (intDone < intCount_a && !blnEOF)
		{
			int intRead = m_objStream.Read(arrBuf_a, intOffset_a + intDone, intCount_a - intDone);

			if (intRead <= 0)
			{
				blnEOF = true;
			}
			else
			{
				intDone += intRead;
			}
		}
	}

	public void Dispose()
	{
		if (m_objStream != null)
		{
			m_objStream.Dispose();
			m_objStream = null;
		}
	}
}
