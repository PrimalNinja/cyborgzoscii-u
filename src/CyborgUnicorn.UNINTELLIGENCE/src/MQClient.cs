// CyborgUnicorn.ZOSCII - MQClient
// ZOSCII Message Queue client - queue, store, replication, and monitoring
// Covers: index.php (publish/fetch/store/retrieve/scan/identify),
//         replikate.php (Replicate), statissa.php (Stats)
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace CyborgUnicorn.ZOSCII
{
    // -------------------------------------------------------------------------
    // Result types
    // -------------------------------------------------------------------------

    /// <summary>Status of a Check operation.</summary>
    public enum MQCheckStatus
    {
        /// <summary>New messages are available after the given pointer.</summary>
        New,
        /// <summary>Queue is up to date - no new messages.</summary>
        UpToDate,
        /// <summary>Network or server error.</summary>
        Error
    }

    /// <summary>Result of a Fetch or FetchNext operation.</summary>
    public class MQFetchResult
    {
        /// <summary>True if a message was returned.</summary>
        public bool HasMessage { get; set; }

        /// <summary>Raw bytes as received from the server.</summary>
        public byte[] EncodedBytes { get; set; }

        /// <summary>Filename returned by the server.</summary>
        public string Filename { get; set; }

        /// <summary>
        /// Pointer for this message - pass as strAfterPointer_a to the next Fetch call.
        /// Always advance the pointer only after the message has been fully processed.
        /// </summary>
        public string Pointer { get; set; }
    }

    /// <summary>Result of a Publish, Put, or Replicate operation.</summary>
    public class MQPublishResult
    {
        /// <summary>True if the server accepted the message.</summary>
        public bool Success { get; set; }

        /// <summary>Error message from the server, if any.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Informational message from the server, if any.</summary>
        public string ServerMessage { get; set; }

        /// <summary>For Put: the filename the server assigned to the stored message.</summary>
        public string StoredName { get; set; }
    }



    /// <summary>Stats for a single queue or store partition.</summary>
    public class MQPartitionStats
    {
        /// <summary>Queue or partition name.</summary>
        public string Name { get; set; }

        /// <summary>Number of messages/files.</summary>
        public int Count { get; set; }

        /// <summary>Total size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Filename of the oldest message.</summary>
        public string OldestFile { get; set; }

        /// <summary>Filename of the newest message.</summary>
        public string NewestFile { get; set; }
    }

/*     /// <summary>Result of a Stats operation.</summary>
    public class MQStatsResult
    {
        /// <summary>True if stats were successfully retrieved.</summary>
        public bool Success { get; set; }

        /// <summary>Stats per queue name.</summary>
        public Dictionary<string, MQPartitionStats> Queues { get; set; }

        /// <summary>Stats per store partition.</summary>
        public Dictionary<string, MQPartitionStats> Store { get; set; }

        /// <summary>Total bytes used across all queues.</summary>
        public long TotalQueueBytes { get; set; }

        /// <summary>Total bytes used across all store partitions.</summary>
        public long TotalStoreBytes { get; set; }
    } */

    // -------------------------------------------------------------------------
    // MQClient
    // -------------------------------------------------------------------------

    /// <summary>
    /// Message Queue client. Sends and receives raw bytes — encoding is the caller's responsibility.
    /// The caller is responsible for any payload framing (e.g. MSG:/FILE: prefixes)
    /// before passing data to Publish.
    ///
    /// Pointer tracking: the caller owns and persists the pointer (after parameter).
    /// Advance the pointer only after the message has been fully processed.
    /// </summary>
    public class MQClient
    {
        // --- Fields ---

        private int m_intTimeoutSeconds;
        private int m_intUserAgentMode;   // 0 = random GUID (default), 1 = none, 2 = fixed
        private string m_strUserAgent;

        // --- Constructor ---

        /// <summary>
        /// Create a new MQ client.
        /// intTimeoutSeconds_a - HTTP timeout in seconds (default 60).
        /// </summary>
        public MQClient(int intTimeoutSeconds_a = 60)
        {
            m_intTimeoutSeconds = intTimeoutSeconds_a;
            m_intUserAgentMode = 0;
            m_strUserAgent = "";
        }

        // -------------------------------------------------------------------------
        // User-Agent control
        // -------------------------------------------------------------------------

        /// <summary>
        /// Send a random GUID as User-Agent on every request. This is the default.
        /// Each call generates a fresh GUID — no two requests share the same agent string.
        /// </summary>
        public void SetUserAgentRandom()
        {
            m_intUserAgentMode = 0;
            m_strUserAgent = "";
        }

        /// <summary>
        /// Send no User-Agent header at all. The header is omitted entirely.
        /// </summary>
        public void SetUserAgentNone()
        {
            m_intUserAgentMode = 1;
            m_strUserAgent = "";
        }

        /// <summary>
        /// Send a fixed User-Agent string on every request.
        /// strUserAgent_a - the value to send (e.g. "MyApp/1.0").
        /// </summary>
        public void SetUserAgent(string strUserAgent_a)
        {
            m_intUserAgentMode = 2;
            m_strUserAgent = strUserAgent_a;
        }

        // -------------------------------------------------------------------------
        // Queue operations
        // -------------------------------------------------------------------------

        /// <summary>
        /// Check whether new messages exist after the given pointer without fetching content.
        /// strAfterPointer_a - filename of last processed message, or empty for first check.
        /// </summary>
        public MQCheckStatus Check(string strServerURL_a, string strQueueGUID_a, string strAfterPointer_a)
        {
            MQCheckStatus objResult = MQCheckStatus.Error;

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    Dictionary<string, string> arrFields = new Dictionary<string, string>
                    {
                        { "action", "fetch" },
                        { "q", strQueueGUID_a },
                        { "after", strAfterPointer_a }
                    };

                    FormUrlEncodedContent objContent = new FormUrlEncodedContent(arrFields);
                    HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objContent).GetAwaiter().GetResult();

                    if (objResponse.IsSuccessStatusCode)
                    {
                        string strDisposition = getDisposition(objResponse);
                        objResult = (strDisposition.Length > 0) ? MQCheckStatus.New : MQCheckStatus.UpToDate;
                        objResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch { }

            return objResult;
        }

        /// <summary>
        /// Fetch the next single message after the given pointer.
        /// strAfterPointer_a - filename of last processed message, or empty for first fetch.
        /// </summary>
        public MQFetchResult FetchNext(string strServerURL_a, string strQueueGUID_a, string strAfterPointer_a)
        {
            MQFetchResult objResult = new MQFetchResult();

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    Dictionary<string, string> arrFields = new Dictionary<string, string>
                    {
                        { "action", "fetch" },
                        { "q", strQueueGUID_a },
                        { "after", strAfterPointer_a }
                    };

                    FormUrlEncodedContent objContent = new FormUrlEncodedContent(arrFields);
                    HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objContent).GetAwaiter().GetResult();

                    if (objResponse.IsSuccessStatusCode)
                    {
                        string strDisposition = getDisposition(objResponse);

                        if (strDisposition.Length > 0)
                        {
                            objResult.HasMessage = true;
                            objResult.Filename = parseFilename(strDisposition);
                            objResult.Pointer = objResult.Filename;
                            objResult.EncodedBytes = objResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch { }

            return objResult;
        }

        /// <summary>
        /// Publish raw bytes to the queue. Caller is responsible for any encoding before calling.
        /// strNonce_a - optional GUID to prevent duplicates on retry.
        /// intRetentionDays_a - message retention in days (0 = immediate/testing).
        /// </summary>
        public MQPublishResult Publish(string strServerURL_a, string strQueueGUID_a, byte[] arrData_a,
            string strNonce_a = "", int intRetentionDays_a = 7)
        {
            MQPublishResult objResult = new MQPublishResult();
            try { objResult = postBytesToQueue(strServerURL_a, strQueueGUID_a, arrData_a, strNonce_a, intRetentionDays_a); }
            catch { }
            return objResult;
        }

        // -------------------------------------------------------------------------
        // Store operations
        // -------------------------------------------------------------------------

        /// <summary>
        /// Retrieve a stored file from the store by its assigned name.
        /// </summary>
        public MQFetchResult Get(string strServerURL_a, string strStoredName_a)
        {
            MQFetchResult objResult = new MQFetchResult();

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    Dictionary<string, string> arrFields = new Dictionary<string, string>
                    {
                        { "action", "retrieve" },
                        { "name", strStoredName_a }
                    };

                    FormUrlEncodedContent objContent = new FormUrlEncodedContent(arrFields);
                    HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objContent).GetAwaiter().GetResult();

                    if (objResponse.IsSuccessStatusCode)
                    {
                        string strDisposition = getDisposition(objResponse);

                        if (strDisposition.Length > 0)
                        {
                            objResult.HasMessage = true;
                            objResult.Filename = parseFilename(strDisposition);
                            objResult.Pointer = objResult.Filename;
                            objResult.EncodedBytes = objResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch { }

            return objResult;
        }

        /// <summary>
        /// Identify previously unidentified store files by removing their -u suffix.
        /// Pass the names returned by Scan that you have successfully decoded.
        /// </summary>
        public string[] Identify(string strServerURL_a, string[] arrNames_a)
        {
            string[] arrResult = null;

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    string strNamesJson = buildJsonStringArray(arrNames_a);

                    Dictionary<string, string> arrFields = new Dictionary<string, string>
                    {
                        { "action", "identify" },
                        { "names", strNamesJson }
                    };

                    FormUrlEncodedContent objContent = new FormUrlEncodedContent(arrFields);
                    HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objContent).GetAwaiter().GetResult();

                    if (objResponse.IsSuccessStatusCode)
                    {
                        string strBody = objResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        arrResult = parseJsonStringArray(strBody, "result");
                    }
                }
            }
            catch { }

            return arrResult;
        }

        /// <summary>
        /// Upload raw bytes to the store (no queue association). Caller is responsible for any encoding.
        /// Server assigns a filename - returned in result.StoredName.
        /// strNonce_a - optional GUID to prevent duplicates on retry.
        /// intRetentionDays_a - retention in days (0 = immediate/testing).
        /// </summary>
        public MQPublishResult Put(string strServerURL_a, byte[] arrData_a,
            string strNonce_a = "", int intRetentionDays_a = 7)
        {
            MQPublishResult objResult = new MQPublishResult();
            try { objResult = storeBytes(strServerURL_a, arrData_a, strNonce_a, intRetentionDays_a); }
            catch { }
            return objResult;
        }

        /// <summary>
        /// Scan the store for unidentified messages (files with -u suffix).
        /// </summary>
        public string[] Scan(string strServerURL_a)
        {
            string[] arrResult = null;

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    Dictionary<string, string> arrFields = new Dictionary<string, string>
                    {
                        { "action", "scan" }
                    };

                    FormUrlEncodedContent objContent = new FormUrlEncodedContent(arrFields);
                    HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objContent).GetAwaiter().GetResult();

                    if (objResponse.IsSuccessStatusCode)
                    {
                        string strBody = objResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        arrResult = parseJsonStringArray(strBody, "result");
                    }
                }
            }
            catch { }

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // Replication
        // -------------------------------------------------------------------------

        /// <summary>
        /// Pull ONE message from a remote queue after strLastPointer_a and publish or store it locally.
        /// Call repeatedly in a loop, advancing LastPointer each time, until !result.Success or !result.HasMessage.
        /// strRemoteServerURL_a  - URL of the remote MQ server index.php.
        /// strRemoteQueueGUID_a  - queue name on the remote server.
        /// strTargetQueueGUID_a  - local queue to publish into. Pass empty to save to local store.
        /// strLastPointer_a      - last filename processed, or empty to start from beginning.
        /// intRetentionDays_a    - retention for the re-published message.
        /// </summary>
        public MQPublishResult Replicate(
            string strRemoteServerURL_a,
            string strRemoteQueueGUID_a,
            string strLocalServerURL_a,
            string strTargetQueueGUID_a,
            string strLastPointer_a,
            out string strNewPointer_a,
            int intRetentionDays_a = 7)
        {
            MQPublishResult objResult = new MQPublishResult();
            strNewPointer_a = strLastPointer_a;

            try
            {
                MQClient objRemote = new MQClient(m_intTimeoutSeconds);
                MQFetchResult objFetch = objRemote.FetchNext(strRemoteServerURL_a, strRemoteQueueGUID_a, strLastPointer_a);

                if (!objFetch.HasMessage)
                {
                    // No new messages - return success with unchanged pointer
                    objResult.Success = true;
                    objResult.ServerMessage = "up-to-date";
                }
                else
                {
                    if (strTargetQueueGUID_a.Length > 0)
                    {
                        // Re-publish raw bytes (already encoded) to local queue
                        objResult = postBytesToQueue(
                            strLocalServerURL_a, strTargetQueueGUID_a, objFetch.EncodedBytes, "", intRetentionDays_a
                        );
                    }
                    else
                    {
                        // Save raw bytes to local store as unidentified
                        objResult = storeBytes(strLocalServerURL_a, objFetch.EncodedBytes, "", intRetentionDays_a, true);
                    }

                    if (objResult.Success)
                    {
                        strNewPointer_a = objFetch.Pointer;
                    }
                }
            }
            catch { }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Monitoring
        // -------------------------------------------------------------------------

        /// <summary>
        /// Retrieve queue and store statistics.
        /// strStatissaURL_a - URL to statissa.php (may differ from index.php URL).
        /// Note: statissa.php returns HTML. This method confirms reachability.
        /// A JSON-capable statissa endpoint would allow full stats parsing.
        /// </summary>
        // public MQStatsResult Stats(string strStatissaURL_a)
        // {
            // MQStatsResult objResult = new MQStatsResult();
            // objResult.Queues = new Dictionary<string, MQPartitionStats>();
            // objResult.Store = new Dictionary<string, MQPartitionStats>();

            // try
            // {
                // using (HttpClient objClient = new HttpClient())
                // {
                    // objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    // HttpResponseMessage objResponse = objClient.GetAsync(strStatissaURL_a).GetAwaiter().GetResult();
                    // objResult.Success = objResponse.IsSuccessStatusCode;
                // }
            // }
            // catch { }

            // return objResult;
        // }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns the User-Agent string for the current mode, or null if none should be sent.
        /// </summary>
        private string getUserAgent()
        {
            string strResult = null;

            if (m_intUserAgentMode == 0)
            {
                strResult = Guid.NewGuid().ToString();
            }
            else if (m_intUserAgentMode == 2)
            {
                strResult = m_strUserAgent;
            }

            return strResult;
        }
        private MQPublishResult postBytesToQueue(string strServerURL_a, string strQueueGUID_a, byte[] arrData_a,
            string strNonce_a, int intRetentionDays_a)
        {
            MQPublishResult objResult = new MQPublishResult();

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    string strBoundary = "----MQBoundary" + Guid.NewGuid().ToString("N");

                    using (MultipartFormDataContent objMultipart = new MultipartFormDataContent(strBoundary))
                    {
                        objMultipart.Add(new StringContent("publish"), "action");
                        objMultipart.Add(new StringContent(strQueueGUID_a), "q");
                        objMultipart.Add(new StringContent(intRetentionDays_a.ToString("D4")), "r");

                        if (strNonce_a.Length > 0)
                        {
                            objMultipart.Add(new StringContent(strNonce_a), "n");
                        }

                        ByteArrayContent objFileContent = new ByteArrayContent(arrData_a);
                        objFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        objMultipart.Add(objFileContent, "msg", "msg.bin");

                        HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objMultipart).GetAwaiter().GetResult();

                        if (objResponse.IsSuccessStatusCode)
                        {
                            string strBody = objResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            parseJsonResponse(strBody, objResult);
                        }
                    }
                }
            }
            catch { }

            return objResult;
        }

        private MQPublishResult storeBytes(string strServerURL_a, byte[] arrData_a, string strNonce_a, int intRetentionDays_a, bool blnUnidentified_a = false)
        {
            MQPublishResult objResult = new MQPublishResult();

            try
            {
                using (HttpClient objClient = new HttpClient())
                {
                    objClient.Timeout = TimeSpan.FromSeconds(m_intTimeoutSeconds);
                    string strUserAgent = getUserAgent();
                    if (strUserAgent != null) { objClient.DefaultRequestHeaders.Add("User-Agent", strUserAgent); }

                    string strBoundary = "----MQBoundary" + Guid.NewGuid().ToString("N");

                    using (MultipartFormDataContent objMultipart = new MultipartFormDataContent(strBoundary))
                    {
                        objMultipart.Add(new StringContent("store"), "action");
                        if (blnUnidentified_a) { objMultipart.Add(new StringContent("1"), "u"); }
                        objMultipart.Add(new StringContent(intRetentionDays_a.ToString("D4")), "r");

                        if (strNonce_a.Length > 0)
                        {
                            objMultipart.Add(new StringContent(strNonce_a), "n");
                        }

                        ByteArrayContent objFileContent = new ByteArrayContent(arrData_a);
                        objFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        objMultipart.Add(objFileContent, "msg", "msg.bin");

                        HttpResponseMessage objResponse = objClient.PostAsync(strServerURL_a, objMultipart).GetAwaiter().GetResult();

                        if (objResponse.IsSuccessStatusCode)
                        {
                            string strBody = objResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            parseJsonResponse(strBody, objResult);
                        }
                    }
                }
            }
            catch { }

            return objResult;
        }

        private static string buildJsonStringArray(string[] arrNames_a)
        {
            string strResult = "[";
            int intI = 0;

            for (intI = 0; intI < arrNames_a.Length; intI++)
            {
                if (intI > 0) { strResult += ","; }
                strResult += "\"" + arrNames_a[intI].Replace("\"", "_") + "\"";
            }

            strResult += "]";
            return strResult;
        }

        private static string getDisposition(HttpResponseMessage objResponse_a)
        {
            string strResult = "";

            if (objResponse_a.Content.Headers.Contains("Content-Disposition"))
            {
                IEnumerator<string> objEnum = objResponse_a.Content.Headers.GetValues("Content-Disposition").GetEnumerator();
                if (objEnum.MoveNext())
                {
                    strResult = objEnum.Current ?? "";
                }
            }

            return strResult;
        }

        private static void parseJsonResponse(string strBody_a, MQPublishResult objResult_a)
        {
            if (strBody_a.Contains("\"error\":\"\""  ) || strBody_a.Contains("\"error\": \"\""  ))
            {
                objResult_a.Success = true;
            }

            int intResultPos = strBody_a.IndexOf("\"result\":");
            if (intResultPos >= 0)
            {
                int intStart = strBody_a.IndexOf("\"", intResultPos + 9);
                if (intStart >= 0)
                {
                    int intEnd = strBody_a.IndexOf("\"", intStart + 1);
                    if (intEnd > intStart)
                    {
                        objResult_a.StoredName = strBody_a.Substring(intStart + 1, intEnd - intStart - 1);
                    }
                }
            }

            int intMsgPos = strBody_a.IndexOf("\"message\":\"");
            if (intMsgPos >= 0)
            {
                int intStart = intMsgPos + 11;
                int intEnd = strBody_a.IndexOf("\"", intStart);
                if (intEnd > intStart)
                {
                    objResult_a.ServerMessage = strBody_a.Substring(intStart, intEnd - intStart);
                }
            }

            int intErrPos = strBody_a.IndexOf("\"error\":\"");
            if (intErrPos >= 0)
            {
                int intStart = intErrPos + 9;
                int intEnd = strBody_a.IndexOf("\"", intStart);
                if (intEnd > intStart)
                {
                    string strErr = strBody_a.Substring(intStart, intEnd - intStart);
                    if (strErr.Length > 0) { objResult_a.ErrorMessage = strErr; }
                }
            }
        }

        private static string parseFilename(string strDisposition_a)
        {
            string strFilename = "";
            int intPos = strDisposition_a.IndexOf("filename=\"");

            if (intPos >= 0)
            {
                int intStart = intPos + 10;
                int intEnd = strDisposition_a.IndexOf("\"", intStart);
                if (intEnd > intStart)
                {
                    strFilename = strDisposition_a.Substring(intStart, intEnd - intStart);
                }
            }

            return strFilename;
        }

        private static string[] parseJsonStringArray(string strBody_a, string strKey_a)
        {
            string[] arrResult = null;

            try
            {
                string strSearch = "\"" + strKey_a + "\":[";
                int intPos = strBody_a.IndexOf(strSearch);

                if (intPos >= 0)
                {
                    int intStart = intPos + strSearch.Length;
                    int intEnd = strBody_a.IndexOf("]", intStart);

                    if (intEnd > intStart)
                    {
                        string strArray = strBody_a.Substring(intStart, intEnd - intStart).Trim();
                        List<string> objList = new List<string>();

                        if (strArray.Length > 0)
                        {
                            string[] arrParts = strArray.Split(',');
                            int intI = 0;

                            for (intI = 0; intI < arrParts.Length; intI++)
                            {
                                string strItem = arrParts[intI].Trim().Trim('"');
                                if (strItem.Length > 0) { objList.Add(strItem); }
                            }
                        }

                        arrResult = objList.ToArray();
                    }
                    else if (intEnd == intStart)
                    {
                        arrResult = new string[0];
                    }
                }
            }
            catch { }

            return arrResult;
        }
    }
}