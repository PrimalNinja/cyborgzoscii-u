// CyborgUnicorn.ZOSCII - ROMExchange Server Test Harness
// Manual step-through test for the listener (server) side of ROMExchange.
// Run alongside ctest.exe on the same or a remote machine.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Diagnostics;
using System.Text;
using CyborgUnicorn.ZOSCII;

public static class STest
{
    private static string m_strHandle = "";
    private static string m_strPeerIP = "";
    private static byte[] m_arrSharedSecret = null;
    private static string m_strMicroROM = "";
    private static bool m_blnConnected = false;
    private static bool m_blnTerminated = false;

    public static void Main(string[] arrArgs_a)
    {
        Console.WriteLine("ROMExchange Server Test Harness");
        Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE LICENSE");
        Console.WriteLine();

        int intPort = 0;

        if (arrArgs_a.Length < 1 || !int.TryParse(arrArgs_a[0], out intPort))
        {
            Console.WriteLine("Usage:   stest.exe <port>");
            Console.WriteLine();
            Console.WriteLine("Example: stest.exe 9000");
            Console.WriteLine("         ctest.exe 127.0.0.1 9000   (same machine)");
            Console.WriteLine("         ctest.exe 192.168.1.5 9000  (remote machine)");
            return;
        }

        ROMExchange objExchange = new ROMExchange();

        objExchange.OnConnection += delegate(string strHandle_a, string strPeerIP_a)
        {
            m_strHandle = strHandle_a;
            m_strPeerIP = strPeerIP_a;
            m_blnConnected = true;
            Console.WriteLine();
            Console.WriteLine("[EVENT] Peer connected from " + strPeerIP_a);
            Console.WriteLine("        Handle: " + strHandle_a);
        };

        objExchange.OnTerminated += delegate(string strHandle_a)
        {
            m_blnTerminated = true;
            Console.WriteLine();
            Console.WriteLine("[EVENT] Session terminated: " + strHandle_a);
        };

        objExchange.OnError += delegate(string strHandle_a, string strMessage_a)
        {
            Console.WriteLine();
            Console.WriteLine("[ERROR] " + strHandle_a + " - " + strMessage_a);
        };

        // -------------------------------------------------------------------------
        // Step 1 - Listen
        // -------------------------------------------------------------------------

        Console.WriteLine("Step 1 - Listen");
        Console.WriteLine("  Press ENTER to start listening on port " + intPort + "...");
        Console.ReadLine();

        string strListenHandle = objExchange.Listen(intPort, false);

        if (strListenHandle == null)
        {
            Console.WriteLine("[FAIL] Could not start listener.");
            return;
        }

        Console.WriteLine("  Listening on port " + intPort + " (manual accept mode)");
        Console.WriteLine("  Waiting for peer to connect...");

        while (!m_blnConnected && !m_blnTerminated)
        {
            System.Threading.Thread.Sleep(100);
        }

        if (m_blnTerminated)
        {
            Console.WriteLine("[FAIL] Session terminated before connection.");
            return;
        }

        // -------------------------------------------------------------------------
        // Step 2 - Authorise
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 2 - Authorise");
        Console.WriteLine("  Peer IP: " + m_strPeerIP);
        Console.WriteLine("  Press ENTER to authorise, or type REJECT + ENTER to reject...");

        string strInput = Console.ReadLine();

        if (strInput != null && strInput.ToUpper() == "REJECT")
        {
            objExchange.Reject(m_strHandle);
            Console.WriteLine("  Connection rejected.");
            return;
        }

        objExchange.Authorise(m_strHandle);
        Console.WriteLine("  Authorised.");

        // -------------------------------------------------------------------------
        // Step 3 - DH Exchange
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 3 - DH Key Exchange");
        Console.WriteLine("  Press ENTER to run DH exchange...");
        Console.ReadLine();

        string strExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

        using (ZOSCIIRom objROM = ZOSCIIRom.FromFile(strExePath))
        {
            if (!objROM.IsLoaded)
            {
                Console.WriteLine("[FAIL] Could not load ROM from exe.");
                return;
            }

            m_arrSharedSecret = objExchange.DHExchange(m_strHandle, objROM);
        }

        if (m_arrSharedSecret == null)
        {
            Console.WriteLine("[FAIL] DH exchange failed.");
            return;
        }

        Console.WriteLine("  Shared secret (" + m_arrSharedSecret.Length + " bytes)");
        Console.WriteLine("  First 16 bytes: " + bytesToHex(m_arrSharedSecret, 16));
        Console.WriteLine("  Last  16 bytes: " + bytesToHexFromEnd(m_arrSharedSecret, 16));

        m_strMicroROM = MicroZOSCII.FromBytes(m_arrSharedSecret, null, null, null);

        if (m_strMicroROM == null)
        {
            Console.WriteLine("[FAIL] MicroROM derivation failed.");
            return;
        }

        Console.WriteLine("  MicroROM length: " + m_strMicroROM.Length + " nibbles");

        string[] arrChunks = MicroZOSCII.ToBase62(m_strMicroROM);

        if (arrChunks != null)
        {
            int intI = 0;

            for (intI = 0; intI < arrChunks.Length; intI++)
            {
                string strExpanded = m_strMicroROM.Substring(intI * 80, 80);
                Console.WriteLine("  Chunk " + (intI + 1) + " base62: " + arrChunks[intI]);
                Console.WriteLine("  Chunk " + (intI + 1) + " hex:    " + strExpanded);
            }
        }

        int[] arrDist = MicroZOSCII.GetDistribution(m_strMicroROM);

        if (arrDist != null)
        {
            string strDist = "";
            int intI = 0;

            for (intI = 0; intI < 16; intI++)
            {
                strDist += "0123456789ABCDEF"[intI] + ":" + arrDist[intI] + " ";
            }

            Console.WriteLine("  Distribution:  " + strDist.Trim());
        }

        // -------------------------------------------------------------------------
        // Step 4 - Send ROM
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 4 - Send ROM");
        Console.WriteLine("  Generating 128KB test ROM...");

        byte[] arrTestROM = generateTestROM(131072);
        uint intChecksum = crc32(arrTestROM);

        Console.WriteLine("  Test ROM: " + arrTestROM.Length + " bytes");
        Console.WriteLine("  CRC32:    " + intChecksum.ToString("X8"));
        Console.WriteLine("  Press ENTER to encode and send ROM...");
        Console.ReadLine();

        Console.WriteLine("  Test ROM: " + arrTestROM.Length + " bytes");
        Console.WriteLine("  CRC32:    " + intChecksum.ToString("X8"));
        Console.WriteLine("  Press ENTER to encode and send ROM...");
        Console.ReadLine();

        ROMExchangeResult objResult = objExchange.SendROM(m_strHandle, m_strMicroROM, arrTestROM);

        if (!objResult.Success)
        {
            Console.WriteLine("[FAIL] SendROM failed: " + objResult.ErrorMessage);
            return;
        }

        Console.WriteLine("  ROM sent successfully.");
        Console.WriteLine("  Tell the client to compare CRC32: " + intChecksum.ToString("X8"));

        // -------------------------------------------------------------------------
        // Step 5 - Ping/pong (automatic, runs in background)
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 5 - Ping/Pong");
        Console.WriteLine("  Starting keepalive...");
        objExchange.StartKeepalive(m_strHandle);
        Console.WriteLine("  Keepalive is running in the background.");
        Console.WriteLine("  Press ENTER to terminate...");
        Console.ReadLine();

        // -------------------------------------------------------------------------
        // Step 6 - Terminate
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 6 - Terminate");
        objExchange.Terminate(m_strHandle);
        Console.WriteLine("  Terminated.");
        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static byte[] generateTestROM(int intSize_a)
    {
        byte[] arrROM = new byte[intSize_a];
        Random objRandom = new Random(42);
        int intI = 0;

        for (intI = 0; intI < arrROM.Length; intI++)
        {
            arrROM[intI] = (byte)objRandom.Next(256);
        }

        return arrROM;
    }

    private static string bytesToHex(byte[] arrBytes_a, int intCount_a)
    {
        StringBuilder objSB = new StringBuilder();
        int intLimit = Math.Min(intCount_a, arrBytes_a.Length);
        int intI = 0;

        for (intI = 0; intI < intLimit; intI++)
        {
            objSB.Append(arrBytes_a[intI].ToString("X2"));
        }

        return objSB.ToString();
    }

    private static string bytesToHexFromEnd(byte[] arrBytes_a, int intCount_a)
    {
        StringBuilder objSB = new StringBuilder();
        int intStart = Math.Max(0, arrBytes_a.Length - intCount_a);
        int intI = 0;

        for (intI = intStart; intI < arrBytes_a.Length; intI++)
        {
            objSB.Append(arrBytes_a[intI].ToString("X2"));
        }

        return objSB.ToString();
    }

    private static uint crc32(byte[] arrBytes_a)
    {
        uint intCRC = 0xFFFFFFFF;
        int intI = 0;

        for (intI = 0; intI < arrBytes_a.Length; intI++)
        {
            intCRC ^= arrBytes_a[intI];
            int intJ = 0;

            for (intJ = 0; intJ < 8; intJ++)
            {
                if ((intCRC & 1) != 0)
                {
                    intCRC = (intCRC >> 1) ^ 0xEDB88320;
                }
                else
                {
                    intCRC >>= 1;
                }
            }
        }

        return intCRC ^ 0xFFFFFFFF;
    }
}