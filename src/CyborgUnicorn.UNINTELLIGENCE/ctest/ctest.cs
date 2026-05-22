// CyborgUnicorn.ZOSCII - ROMExchange Client Test Harness
// Manual step-through test for the initiator (client) side of ROMExchange.
// Run alongside stest.exe on the same or a remote machine.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Text;
using CyborgUnicorn.ZOSCII;

public static class CTest
{
    private static string m_strHandle = "";
    private static bool m_blnConnected = false;
    private static bool m_blnTerminated = false;
    private static byte[] m_arrSharedSecret = null;
    private static string m_strMicroROM = "";

    public static void Main(string[] arrArgs_a)
    {
        Console.WriteLine("ROMExchange Client Test Harness");
        Console.WriteLine("(c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE LICENSE");
        Console.WriteLine();

        string strIP = "";
        int intPort = 0;

        if (arrArgs_a.Length < 2 || !int.TryParse(arrArgs_a[1], out intPort))
        {
            Console.WriteLine("Usage:   ctest.exe <ip> <port>");
            Console.WriteLine();
            Console.WriteLine("Example: ctest.exe 127.0.0.1 9000   (same machine)");
            Console.WriteLine("         ctest.exe 192.168.1.5 9000  (remote machine)");
            return;
        }

        strIP = arrArgs_a[0];

        ROMExchange objExchange = new ROMExchange();

        objExchange.OnConnection += delegate(string strHandle_a, string strPeerIP_a)
        {
            m_strHandle = strHandle_a;
            m_blnConnected = true;
            Console.WriteLine();
            Console.WriteLine("[EVENT] Connected to " + strPeerIP_a);
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
        // Step 1 - Connect
        // -------------------------------------------------------------------------

        Console.WriteLine("Step 1 - Connect");
        Console.WriteLine("  Press ENTER to connect to " + strIP + ":" + intPort + "...");
        Console.ReadLine();

        string strConnectHandle = objExchange.Connect(strIP, intPort);

        if (strConnectHandle == null)
        {
            Console.WriteLine("[FAIL] Could not initiate connection.");
            return;
        }

        Console.WriteLine("  Connecting...");

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
        // Step 2 - DH Exchange
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 2 - DH Key Exchange");
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
        Console.WriteLine("  >> Compare these with stest output - they must match <<");

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

        Console.WriteLine("  >> Compare chunks with stest output - they must match <<");

        // -------------------------------------------------------------------------
        // Step 3 - Receive ROM
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 3 - Receive ROM");
        Console.WriteLine("  Press ENTER to receive and decode ROM...");
        Console.ReadLine();

        ROMExchangeResult objResult = objExchange.ReceiveROM(m_strHandle, m_strMicroROM);

        if (!objResult.Success)
        {
            Console.WriteLine("[FAIL] ReceiveROM failed: " + objResult.ErrorMessage);
            return;
        }

        uint intChecksum = crc32(objResult.ROMBytes);

        Console.WriteLine("  ROM received: " + objResult.ROMBytes.Length + " bytes");
        Console.WriteLine("  CRC32:        " + intChecksum.ToString("X8"));
        Console.WriteLine("  >> Compare CRC32 with stest output - they must match <<");

        // -------------------------------------------------------------------------
        // Step 4 - Ping/pong (automatic, runs in background)
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 4 - Ping/Pong");
        Console.WriteLine("  Starting keepalive...");
        objExchange.StartKeepalive(m_strHandle);
        Console.WriteLine("  Keepalive is running in the background.");
        Console.WriteLine("  Press ENTER to terminate...");
        Console.ReadLine();

        // -------------------------------------------------------------------------
        // Step 5 - Terminate
        // -------------------------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("Step 5 - Terminate");
        objExchange.Terminate(m_strHandle);
        Console.WriteLine("  Terminated.");
        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

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