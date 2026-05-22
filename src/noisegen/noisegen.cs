// HTTP Noise Generator
// Fetches UNSIGNAL-encoded URLs from a ZOSCII MQ queue, decodes them,
// fires silent HTTP GETs (streaming, nothing stored), then waits.
// Runs until Ctrl+C. Nothing logged, nothing saved.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using CyborgUnicorn.ZOSCII;

public static class HttpNoiseGenerator
{
    private static bool m_blnRunning = true;

    public static void Main(string[] arrArgs_a)
    {
        if (arrArgs_a.Length < 3)
        {
            Console.WriteLine("HTTP Noise Generator");
            Console.WriteLine("Usage: noisegen <mq-endpoint> <queue-name> <rom-file> [delay-seconds] [-ua random|clear|\"agent string\"]");
            Console.WriteLine("       delay-seconds minimum 30, default 30");
            Console.WriteLine("       -ua random (default), -ua clear, -ua \"my user agent\"");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  noisegen https://mq.example.com/index.php myqueue mykey.jpg");
            Console.WriteLine("  noisegen https://mq.example.com/index.php myqueue mykey.jpg 60");
            Console.WriteLine("  noisegen https://mq.example.com/index.php myqueue mykey.jpg 60 -ua clear");
            Console.WriteLine("  noisegen https://mq.example.com/index.php myqueue mykey.jpg -ua \"Mozilla/5.0\"");
            return;
        }

        string strEndpoint   = arrArgs_a[0];
        string strQueue      = arrArgs_a[1];
        string strROMFile    = arrArgs_a[2];
        int    intDelay      = 30;
        int    intUAMode     = 0;   // 0 = random (default), 1 = clear, 2 = fixed
        string strUserAgent  = "";

        int intI = 3;

        while (intI < arrArgs_a.Length)
        {
            if (arrArgs_a[intI] == "-ua" && intI + 1 < arrArgs_a.Length)
            {
                string strUAValue = arrArgs_a[intI + 1];

                if (strUAValue.ToLower() == "random")
                {
                    intUAMode = 0;
                }
                else if (strUAValue.ToLower() == "clear")
                {
                    intUAMode = 1;
                }
                else
                {
                    intUAMode    = 2;
                    strUserAgent = strUAValue;
                }

                intI += 2;
            }
            else
            {
                int intParsed = 0;

                if (int.TryParse(arrArgs_a[intI], out intParsed))
                {
                    if (intParsed > 30)
                    {
                        intDelay = intParsed;
                    }
                }

                intI++;
            }
        }

        Console.CancelKeyPress += delegate(object objSender, ConsoleCancelEventArgs objE)
        {
            objE.Cancel = true;
            m_blnRunning = false;
        };

        if (!File.Exists(strROMFile))
        {
            return;
        }

        using (ZOSCIIRom objROM = ZOSCIIRom.FromFile(strROMFile))
        {
            if (!objROM.IsLoaded)
            {
                return;
            }

            MQClient objMQ      = new MQClient(15);
            string   strPointer = "";
            Random   objRandom  = new Random();

            while (m_blnRunning)
            {
                MQFetchResult objFetch = null;

                try
                {
                    objFetch = objMQ.FetchNext(strEndpoint, strQueue, strPointer);
                }
                catch { }

                if (objFetch != null && objFetch.HasMessage)
                {
                    strPointer = objFetch.Pointer;

                    string strURL = null;

                    try
                    {
                        strURL = UDecode.ToString(objFetch.EncodedBytes, objROM);
                    }
                    catch { }

                    if (strURL != null && strURL.Length > 0)
                    {
                        fireAndForget(strURL, intUAMode, strUserAgent);
                    }

                    if (m_blnRunning)
                    {
                        int intJitter = objRandom.Next(0, 31);
                        Thread.Sleep((intDelay + intJitter) * 1000);
                    }
                }
                else
                {
                    // End of queue — reset pointer and start again
                    strPointer = "";

                    if (m_blnRunning)
                    {
                        int intJitter = objRandom.Next(0, 31);
                        Thread.Sleep((intDelay + intJitter) * 1000);
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void fireAndForget(string strURL_a, int intUAMode_a, string strUserAgent_a)
    {
        try
        {
            using (HttpClient objClient = new HttpClient())
            {
                objClient.Timeout = TimeSpan.FromSeconds(10);

                if (intUAMode_a == 0)
                {
                    objClient.DefaultRequestHeaders.Add("User-Agent", Guid.NewGuid().ToString());
                }
                else if (intUAMode_a == 2)
                {
                    objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent_a);
                }
                // intUAMode_a == 1 (clear) — no User-Agent header added

                using (HttpResponseMessage objResponse = objClient.GetAsync(
                    strURL_a, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    using (Stream objStream = objResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    {
                        byte[] arrBuffer = new byte[8192];
                        bool   blnDone   = false;

                        while (!blnDone)
                        {
                            int intRead = objStream.Read(arrBuffer, 0, arrBuffer.Length);

                            if (intRead <= 0)
                            {
                                blnDone = true;
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }
}