// CyborgUnicorn.ZOSCII - ROMExchange
// Peer-to-peer ROM exchange over TCP.
// Step 1: Establish connection (Listen / Connect) with optional manual authorisation.
// Step 2: Exchange ROM (SendROM / ReceiveROM) using a microROM derived by the caller
//         via MicroZOSCII.FromBytes or any other method (typed seeds, barcode, contact list).
// Connection is maintained with a random GUID ping/pong keepalive until Terminate is called.
// No identifying information is sent at any point.
// (c) 2026 Cyborg Unicorn Pty Ltd - UNINTELLIGENCE License

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CyborgUnicorn.ZOSCII
{
    // -------------------------------------------------------------------------
    // Result / status types
    // -------------------------------------------------------------------------

    /// <summary>Status of a ROMExchange session.</summary>
    public enum ROMExchangeStatus
    {
        /// <summary>No active session.</summary>
        Idle,
        /// <summary>Listening for an incoming connection.</summary>
        Listening,
        /// <summary>Connected, awaiting manual authorisation.</summary>
        PendingAuthorisation,
        /// <summary>Connected and active (ping/pong keepalive running).</summary>
        Connected,
        /// <summary>ROM transmission in progress.</summary>
        Exchanging,
        /// <summary>Session terminated.</summary>
        Terminated,
        /// <summary>An error occurred.</summary>
        Error
    }

    /// <summary>Result of a SendROM or ReceiveROM operation.</summary>
    public class ROMExchangeResult
    {
        /// <summary>True if the operation completed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The recovered ROM bytes (ReceiveROM only).</summary>
        public byte[] ROMBytes { get; set; }

        /// <summary>Error description if Success is false.</summary>
        public string ErrorMessage { get; set; }
    }

    // -------------------------------------------------------------------------
    // ROMExchange
    // -------------------------------------------------------------------------

    /// <summary>
    /// Peer-to-peer ROM exchange over TCP.
    ///
    /// Listener workflow:
    ///   1. Call Listen(port, autoAccept)
    ///   2. OnConnection fires when a peer connects (supplies handle and peer IP)
    ///   3. If autoAccept=false, call Authorise(handle) or Reject(handle)
    ///   4. When ready, call SendROM(handle, microROM, romBytes) to transmit the ROM
    ///   5. Call Terminate(handle) when done, or handle OnTerminated event
    ///
    /// Initiator workflow:
    ///   1. Call Connect(ip, port)
    ///   2. OnConnection fires on success
    ///   3. Call ReceiveROM(handle, microROM) to receive and decode the ROM
    ///   4. Call Terminate(handle) when done, or handle OnTerminated event
    ///
    /// DH key exchange is a separate concern — use GetBootstrapMethods / DHExchange
    /// to derive a microROM, then pass it to SendROM / ReceiveROM.
    /// </summary>
    public class ROMExchange
    {
        // -------------------------------------------------------------------------
        // Fields
        // -------------------------------------------------------------------------

        private readonly object m_objLock = new object();
        private Dictionary<string, ROMExchangeSession> m_objSessions;
        private int m_intTimeoutSeconds;
        private int m_intPingIntervalMs;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Fires when a peer connects (listen or connect side).
        /// Parameters: strHandle, strPeerIP
        /// </summary>
        public event Action<string, string> OnConnection;

        /// <summary>
        /// Fires when a connection is terminated from either end.
        /// Parameter: strHandle
        /// </summary>
        public event Action<string> OnTerminated;

        /// <summary>
        /// Fires when an error occurs on a session.
        /// Parameters: strHandle, strErrorMessage
        /// </summary>
        public event Action<string, string> OnError;

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------

        /// <summary>
        /// Create a new ROMExchange instance.
        /// intTimeoutSeconds_a  - socket read/write timeout (default 60).
        /// intPingIntervalMs_a  - ping/pong keepalive interval in ms (default 15000).
        /// </summary>
        public ROMExchange(int intTimeoutSeconds_a = 60, int intPingIntervalMs_a = 15000)
        {
            m_intTimeoutSeconds = intTimeoutSeconds_a;
            m_intPingIntervalMs = intPingIntervalMs_a;
            m_objSessions = new Dictionary<string, ROMExchangeSession>();
        }

        // -------------------------------------------------------------------------
        // Bootstrap method registry
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns the available bootstrap methods as a 2D array of [code, description].
        /// The caller presents these to the user to choose a method before exchanging.
        /// </summary>
        public static string[,] GetBootstrapMethods()
        {
            string[,] arrResult = new string[,]
            {
                { "DH", "Diffie-Hellman Key Exchange" }
            };

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // DH key exchange
        // -------------------------------------------------------------------------

        /// <summary>
        /// Perform a Diffie-Hellman key exchange over an established connection,
        /// returning the shared secret bytes for use with MicroZOSCII.FromBytes.
        /// strHandle_a - session handle from OnConnection.
        /// objROM_a    - caller's own ZOSCII ROM, used to derive the DH private key.
        ///               Each side uses their own ROM — the shared secret emerges from the math.
        /// Returns the DH shared secret bytes, or null on failure.
        /// Both sides call this independently — the exchange is symmetric.
        /// </summary>
        public byte[] DHExchange(string strHandle_a, ZOSCIIRom objROM_a)
        {
            byte[] arrResult = null;

            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.Connected
                    && objROM_a != null && objROM_a.IsLoaded)
                {
                    // Standard DH parameters (RFC 3526 group 14 - 2048-bit MODP)
                    // p is the safe prime, g is the generator
                    byte[] arrP = getRFC3526Group14Prime();
                    byte[] arrG = new byte[] { 0x02 };

                    // Derive private key by ZOSCII-encoding a 256-byte zero array with the
                    // caller's ROM. Non-deterministic (random position selection) and I(M;A)=0 —
                    // no RNG needed. Each call produces different 256 bytes.
                    byte[] arrPrivate = ZEncode.Bytes(new byte[256], objROM_a);

                    if (arrPrivate != null && arrPrivate.Length >= 256)
                    {
                        // Clamp to exactly 256 bytes
                        if (arrPrivate.Length > 256)
                        {
                            byte[] arrTrimmed = new byte[256];
                            Array.Copy(arrPrivate, arrTrimmed, 256);
                            arrPrivate = arrTrimmed;
                        }

                        // Compute public key: g^private mod p
                        byte[] arrPublic = modPow(arrG, arrPrivate, arrP);

                        // Send our public key length + public key
                        byte[] arrLenBytes = intToLittleEndian4(arrPublic.Length);
                        writeBytes(objSession.Socket, arrLenBytes);
                        writeBytes(objSession.Socket, arrPublic);

                        // Receive peer public key
                        byte[] arrPeerLenBytes = readBytes(objSession.Socket, 4);
                        int intPeerLen = littleEndian4ToInt(arrPeerLenBytes);

                        if (intPeerLen > 0 && intPeerLen <= 512)
                        {
                            byte[] arrPeerPublic = readBytes(objSession.Socket, intPeerLen);

                            // Compute shared secret: peerPublic^private mod p
                            arrResult = modPow(arrPeerPublic, arrPrivate, arrP);
                        }
                    }
                }
            }
            catch { }

            return arrResult;
        }

        // -------------------------------------------------------------------------
        // Listen / Connect
        // -------------------------------------------------------------------------

        /// <summary>
        /// Start listening for an incoming connection on the given port.
        /// blnAutoAccept_a              - if true, accept immediately; if false, OnConnection fires
        ///                                and the caller must call Authorise or Reject.
        /// intAuthoriseTimeoutSeconds_a - seconds to wait for Authorise/Reject before auto-rejecting (default 60).
        /// Returns the listener handle, or null on failure.
        /// </summary>
        public string Listen(int intPort_a, bool blnAutoAccept_a = true, int intAuthoriseTimeoutSeconds_a = 60)
        {
            string strHandle = null;

            try
            {
                strHandle = Guid.NewGuid().ToString();
                ROMExchangeSession objSession = new ROMExchangeSession();
                objSession.Handle = strHandle;
                objSession.Status = ROMExchangeStatus.Listening;
                objSession.AutoAccept = blnAutoAccept_a;
                objSession.AuthoriseTimeoutMs = intAuthoriseTimeoutSeconds_a * 1000;
                objSession.IsListener = true;

                lock (m_objLock)
                {
                    m_objSessions[strHandle] = objSession;
                }

                Thread objThread = new Thread(delegate()
                {
                    listenThreadProc(strHandle, intPort_a);
                });
                objThread.IsBackground = true;
                objThread.Start();
            }
            catch { }

            return strHandle;
        }

        /// <summary>
        /// Connect to a listening peer.
        /// strIP_a   - remote IP address.
        /// intPort_a - remote port.
        /// Returns the session handle, or null on failure.
        /// OnConnection fires on success.
        /// </summary>
        public string Connect(string strIP_a, int intPort_a)
        {
            string strHandle = null;

            try
            {
                strHandle = Guid.NewGuid().ToString();
                ROMExchangeSession objSession = new ROMExchangeSession();
                objSession.Handle = strHandle;
                objSession.Status = ROMExchangeStatus.Idle;
                objSession.IsListener = false;

                lock (m_objLock)
                {
                    m_objSessions[strHandle] = objSession;
                }

                Thread objThread = new Thread(delegate()
                {
                    connectThreadProc(strHandle, strIP_a, intPort_a);
                });
                objThread.IsBackground = true;
                objThread.Start();
            }
            catch { }

            return strHandle;
        }

        // -------------------------------------------------------------------------
        // Authorise / Reject
        // -------------------------------------------------------------------------

        /// <summary>
        /// Authorise a pending connection (manual accept mode only).
        /// strHandle_a - handle from OnConnection.
        /// </summary>
        public void Authorise(string strHandle_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.PendingAuthorisation)
                {
                    objSession.AuthoriseEvent.Set();
                }
            }
            catch { }
        }

        /// <summary>
        /// Reject a pending connection (manual accept mode only).
        /// strHandle_a - handle from OnConnection.
        /// </summary>
        public void Reject(string strHandle_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.PendingAuthorisation)
                {
                    objSession.Rejected = true;
                    objSession.AuthoriseEvent.Set();
                }
            }
            catch { }
        }

        // -------------------------------------------------------------------------
        // ROM send / receive
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encode and send a ROM to the connected peer (listener side).
        /// strHandle_a  - session handle.
        /// strMicroROM_a - microROM string from MicroZOSCII.FromBytes or equivalent.
        /// arrROM_a     - the full ROM bytes to send (up to 128KB).
        /// </summary>
        public ROMExchangeResult SendROM(string strHandle_a, string strMicroROM_a, byte[] arrROM_a)
        {
            ROMExchangeResult objResult = new ROMExchangeResult();

            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.Connected)
                {
                    objSession.Status = ROMExchangeStatus.Exchanging;

                    byte[] arrAddresses = MicroZOSCII.Encode(strMicroROM_a, arrROM_a);

                    if (arrAddresses != null)
                    {
                        // Send address count as 4-byte little-endian
                        byte[] arrCount = intToLittleEndian4(arrAddresses.Length);
                        writeBytes(objSession.Socket, arrCount);

                        // Send address stream — 1 byte per address
                        writeBytes(objSession.Socket, arrAddresses);
                        objSession.Status = ROMExchangeStatus.Connected;
                        objResult.Success = true;
                    }
                    else
                    {
                        objSession.Status = ROMExchangeStatus.Connected;
                        objResult.ErrorMessage = "Encode failed";
                    }
                }
                else
                {
                    objResult.ErrorMessage = "Session not connected";
                }
            }
            catch (Exception objEx)
            {
                objResult.ErrorMessage = objEx.Message;
            }

            return objResult;
        }

        /// <summary>
        /// Receive and decode a ROM from the connected peer (initiator side).
        /// strHandle_a   - session handle.
        /// strMicroROM_a - microROM string from MicroZOSCII.FromBytes or equivalent.
        /// Returns ROMExchangeResult with ROMBytes populated on success.
        /// </summary>
        public ROMExchangeResult ReceiveROM(string strHandle_a, string strMicroROM_a)
        {
            ROMExchangeResult objResult = new ROMExchangeResult();

            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.Connected)
                {
                    objSession.Status = ROMExchangeStatus.Exchanging;

                    // Read address count
                    byte[] arrCountBytes = readBytes(objSession.Socket, 4);
                    int intCount = littleEndian4ToInt(arrCountBytes);

                    if (intCount > 0 && intCount <= 262144 * 2)
                    {
                        // Read address stream — 1 byte per address
                        byte[] arrAddresses = readBytes(objSession.Socket, intCount);

                        byte[] arrROM = MicroZOSCII.Decode(strMicroROM_a, arrAddresses);

                        if (arrROM != null)
                        {
                            objSession.Status = ROMExchangeStatus.Connected;
                            objResult.Success = true;
                            objResult.ROMBytes = arrROM;
                        }
                        else
                        {
                            objSession.Status = ROMExchangeStatus.Connected;
                            objResult.ErrorMessage = "Decode failed";
                        }
                    }
                    else
                    {
                        objSession.Status = ROMExchangeStatus.Connected;
                        objResult.ErrorMessage = "Invalid address count";
                    }
                }
                else
                {
                    objResult.ErrorMessage = "Session not connected";
                }
            }
            catch (Exception objEx)
            {
                objResult.ErrorMessage = objEx.Message;
            }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Keepalive
        // -------------------------------------------------------------------------

        /// <summary>
        /// Start the ping/pong keepalive loop on a background thread.
        /// Call this when all exchanges are complete and the connection should be kept alive.
        /// Either side can call this. Terminate() stops it.
        /// strHandle_a - session handle.
        /// </summary>
        public void StartKeepalive(string strHandle_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && objSession.Status == ROMExchangeStatus.Connected)
                {
                    Thread objThread = new Thread(delegate()
                    {
                        pingPongLoop(strHandle_a);
                    });
                    objThread.IsBackground = true;
                    objThread.Start();
                }
            }
            catch { }
        }

        // -------------------------------------------------------------------------
        // Terminate
        // -------------------------------------------------------------------------

        /// <summary>
        /// Terminate a session from either end.
        /// strHandle_a - session handle.
        /// </summary>
        public void Terminate(string strHandle_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null)
                {
                    objSession.Terminated = true;

                    if (objSession.Socket != null)
                    {
                        try { objSession.Socket.Close(); } catch { }
                    }

                    if (objSession.Listener != null)
                    {
                        try { objSession.Listener.Stop(); } catch { }
                    }

                    objSession.Status = ROMExchangeStatus.Terminated;
                }
            }
            catch { }
        }

        /// <summary>
        /// Returns the current status of a session.
        /// </summary>
        public ROMExchangeStatus GetStatus(string strHandle_a)
        {
            ROMExchangeStatus objResult = ROMExchangeStatus.Idle;

            ROMExchangeSession objSession = getSession(strHandle_a);

            if (objSession != null)
            {
                objResult = objSession.Status;
            }

            return objResult;
        }

        // -------------------------------------------------------------------------
        // Private — thread procedures
        // -------------------------------------------------------------------------

        private void listenThreadProc(string strHandle_a, int intPort_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null)
                {
                    TcpListener objListener = new TcpListener(IPAddress.Any, intPort_a);
                    objSession.Listener = objListener;
                    objListener.Start();

                    TcpClient objClient = objListener.AcceptTcpClient();
                    objListener.Stop();

                    if (!objSession.Terminated)
                    {
                        string strPeerIP = ((IPEndPoint)objClient.Client.RemoteEndPoint).Address.ToString();
                        objSession.Socket = objClient.Client;
                        objSession.Socket.SendTimeout    = m_intTimeoutSeconds * 1000;
                        objSession.Socket.ReceiveTimeout = m_intTimeoutSeconds * 1000;

                        if (objSession.AutoAccept)
                        {
                            objSession.Status = ROMExchangeStatus.Connected;
                            fireOnConnection(strHandle_a, strPeerIP);
                        }
                        else
                        {
                            objSession.Status = ROMExchangeStatus.PendingAuthorisation;
                            fireOnConnection(strHandle_a, strPeerIP);
                            bool blnSignalled = objSession.AuthoriseEvent.WaitOne(objSession.AuthoriseTimeoutMs);

                            if (blnSignalled && !objSession.Rejected && !objSession.Terminated)
                            {
                                objSession.Status = ROMExchangeStatus.Connected;
                            }
                            else
                            {
                                objSession.Socket.Close();
                                objSession.Status = ROMExchangeStatus.Terminated;
                                fireOnTerminated(strHandle_a);
                            }
                        }
                    }
                }
            }
            catch (Exception objEx)
            {
                fireOnError(strHandle_a, objEx.Message);
            }
        }

        private void connectThreadProc(string strHandle_a, string strIP_a, int intPort_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null)
                {
                    TcpClient objClient = new TcpClient();
                    objClient.Connect(strIP_a, intPort_a);

                    if (!objSession.Terminated)
                    {
                        objSession.Socket = objClient.Client;
                        objSession.Socket.SendTimeout    = m_intTimeoutSeconds * 1000;
                        objSession.Socket.ReceiveTimeout = m_intTimeoutSeconds * 1000;
                        objSession.Status = ROMExchangeStatus.Connected;

                        fireOnConnection(strHandle_a, strIP_a);
                    }
                }
            }
            catch (Exception objEx)
            {
                fireOnError(strHandle_a, objEx.Message);
            }
        }

        private void pingPongLoop(string strHandle_a)
        {
            try
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null)
                {
                    bool blnDone = false;

                    while (!blnDone)
                    {
                        if (objSession.Terminated)
                        {
                            blnDone = true;
                        }
                        else if (objSession.Status == ROMExchangeStatus.Connected)
                        {
                            Thread.Sleep(m_intPingIntervalMs);

                            if (!objSession.Terminated && objSession.Status == ROMExchangeStatus.Connected)
                            {
                                string strPing = Guid.NewGuid().ToString();
                                char[] arrChars = strPing.ToCharArray();
                                Array.Reverse(arrChars);
                                string strPong = new string(arrChars);

                                byte[] arrPing = Encoding.ASCII.GetBytes(strPing);
                                writeBytes(objSession.Socket, arrPing);

                                byte[] arrPongReceived = readBytes(objSession.Socket, arrPing.Length);
                                string strPongReceived = Encoding.ASCII.GetString(arrPongReceived);

                                if (strPongReceived != strPong)
                                {
                                    blnDone = true;
                                    objSession.Status = ROMExchangeStatus.Terminated;
                                    fireOnTerminated(strHandle_a);
                                }
                            }
                        }
                        else
                        {
                            // Exchanging — wait briefly and check again
                            Thread.Sleep(100);
                        }
                    }
                }
            }
            catch
            {
                ROMExchangeSession objSession = getSession(strHandle_a);

                if (objSession != null && !objSession.Terminated)
                {
                    objSession.Status = ROMExchangeStatus.Terminated;
                    fireOnTerminated(strHandle_a);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Private — helpers
        // -------------------------------------------------------------------------

        private ROMExchangeSession getSession(string strHandle_a)
        {
            ROMExchangeSession objResult = null;

            lock (m_objLock)
            {
                if (m_objSessions.ContainsKey(strHandle_a))
                {
                    objResult = m_objSessions[strHandle_a];
                }
            }

            return objResult;
        }

        private void fireOnConnection(string strHandle_a, string strPeerIP_a)
        {
            try
            {
                if (OnConnection != null)
                {
                    OnConnection(strHandle_a, strPeerIP_a);
                }
            }
            catch { }
        }

        private void fireOnTerminated(string strHandle_a)
        {
            try
            {
                if (OnTerminated != null)
                {
                    OnTerminated(strHandle_a);
                }
            }
            catch { }
        }

        private void fireOnError(string strHandle_a, string strMessage_a)
        {
            try
            {
                if (OnError != null)
                {
                    OnError(strHandle_a, strMessage_a);
                }
            }
            catch { }
        }

        private static void writeBytes(Socket objSocket_a, byte[] arrData_a)
        {
            int intSent = 0;

            while (intSent < arrData_a.Length)
            {
                intSent += objSocket_a.Send(arrData_a, intSent, arrData_a.Length - intSent, SocketFlags.None);
            }
        }

        private static byte[] readBytes(Socket objSocket_a, int intCount_a)
        {
            byte[] arrResult = new byte[intCount_a];
            int intRead = 0;

            while (intRead < intCount_a)
            {
                intRead += objSocket_a.Receive(arrResult, intRead, intCount_a - intRead, SocketFlags.None);
            }

            return arrResult;
        }

        private static byte[] intToLittleEndian4(int intValue_a)
        {
            byte[] arrResult = new byte[4];
            arrResult[0] = (byte)( intValue_a        & 0xFF);
            arrResult[1] = (byte)((intValue_a >>  8) & 0xFF);
            arrResult[2] = (byte)((intValue_a >> 16) & 0xFF);
            arrResult[3] = (byte)((intValue_a >> 24) & 0xFF);
            return arrResult;
        }

        private static int littleEndian4ToInt(byte[] arrBytes_a)
        {
            int intResult = 0;
            intResult  =  arrBytes_a[0];
            intResult |= (arrBytes_a[1] << 8);
            intResult |= (arrBytes_a[2] << 16);
            intResult |= (arrBytes_a[3] << 24);
            return intResult;
        }

        // -------------------------------------------------------------------------
        // Private — DH helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Modular exponentiation: base^exp mod modulus, all as big-endian byte arrays.
        /// </summary>
        private static byte[] modPow(byte[] arrBase_a, byte[] arrExp_a, byte[] arrMod_a)
        {
            System.Numerics.BigInteger objBase = new System.Numerics.BigInteger(prependZero(arrBase_a));
            System.Numerics.BigInteger objExp  = new System.Numerics.BigInteger(prependZero(arrExp_a));
            System.Numerics.BigInteger objMod  = new System.Numerics.BigInteger(prependZero(arrMod_a));
            System.Numerics.BigInteger objResult = System.Numerics.BigInteger.ModPow(objBase, objExp, objMod);

            byte[] arrResult = objResult.ToByteArray();

            // Remove sign byte if present, ensure 256 bytes
            if (arrResult[arrResult.Length - 1] == 0)
            {
                Array.Resize(ref arrResult, arrResult.Length - 1);
            }

            // Reverse from little-endian (BigInteger) to big-endian
            Array.Reverse(arrResult);

            // Pad or trim to 256 bytes
            byte[] arrPadded = new byte[256];
            int intCopy = Math.Min(arrResult.Length, 256);
            Array.Copy(arrResult, 0, arrPadded, 256 - intCopy, intCopy);

            return arrPadded;
        }

        /// <summary>
        /// Prepend a zero byte so BigInteger treats the value as positive (little-endian input).
        /// Input is big-endian; we reverse then prepend zero.
        /// </summary>
        private static byte[] prependZero(byte[] arrBytes_a)
        {
            byte[] arrReversed = new byte[arrBytes_a.Length + 1];
            int intI = 0;

            for (intI = 0; intI < arrBytes_a.Length; intI++)
            {
                arrReversed[intI] = arrBytes_a[arrBytes_a.Length - 1 - intI];
            }

            arrReversed[arrBytes_a.Length] = 0x00;
            return arrReversed;
        }

        /// <summary>
        /// RFC 3526 Group 14 — 2048-bit MODP safe prime.
        /// </summary>
        private static byte[] getRFC3526Group14Prime()
        {
            return new byte[]
            {
                0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xC9,0x0F,0xDA,0xA2,0x21,0x68,0xC2,0x34,
                0xC4,0xC6,0x62,0x8B,0x80,0xDC,0x1C,0xD1,0x29,0x02,0x4E,0x08,0x8A,0x67,0xCC,0x74,
                0x02,0x0B,0xBE,0xA6,0x3B,0x13,0x9B,0x22,0x51,0x4A,0x08,0x79,0x8E,0x34,0x04,0xDD,
                0xEF,0x95,0x19,0xB3,0xCD,0x3A,0x43,0x1B,0x30,0x2B,0x0A,0x6D,0xF2,0x5F,0x14,0x37,
                0x4F,0xE1,0x35,0x6D,0x6D,0x51,0xC2,0x45,0xE4,0x85,0xB5,0x76,0x62,0x5E,0x7E,0xC6,
                0xF4,0x4C,0x42,0xE9,0xA6,0x37,0xED,0x6B,0x0B,0xFF,0x5C,0xB6,0xF4,0x06,0xB7,0xED,
                0xEE,0x38,0x6B,0xFB,0x5A,0x89,0x9F,0xA5,0xAE,0x9F,0x24,0x11,0x7C,0x4B,0x1F,0xE6,
                0x49,0x28,0x66,0x51,0xEC,0xE4,0x5B,0x3D,0xC2,0x00,0x7C,0xB8,0xA1,0x63,0xBF,0x05,
                0x98,0xDA,0x48,0x36,0x1C,0x55,0xD3,0x9A,0x69,0x16,0x3F,0xA8,0xFD,0x24,0xCF,0x5F,
                0x83,0x65,0x5D,0x23,0xDC,0xA3,0xAD,0x96,0x1C,0x62,0xF3,0x56,0x20,0x85,0x52,0xBB,
                0x9E,0xD5,0x29,0x07,0x70,0x96,0x96,0x6D,0x67,0x0C,0x35,0x4E,0x4A,0xBC,0x98,0x04,
                0xF1,0x74,0x6C,0x08,0xCA,0x18,0x21,0x7C,0x32,0x90,0x5E,0x46,0x2E,0x36,0xCE,0x3B,
                0xE3,0x9E,0x77,0x2C,0x18,0x0E,0x86,0x03,0x9B,0x27,0x83,0xA2,0xEC,0x07,0xA2,0x8F,
                0xB5,0xC5,0x5D,0xF0,0x6F,0x4C,0x52,0xC9,0xDE,0x2B,0xCB,0xF6,0x95,0x58,0x17,0x18,
                0x39,0x95,0x49,0x7C,0xEA,0x95,0x6A,0xE5,0x15,0xD2,0x26,0x18,0x98,0xFA,0x05,0x10,
                0x15,0x72,0x8E,0x5A,0x8A,0xAC,0xAA,0x68,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF
            };
        }
    }

    // -------------------------------------------------------------------------
    // Internal session state
    // -------------------------------------------------------------------------

    internal class ROMExchangeSession
    {
        public string Handle { get; set; }
        public ROMExchangeStatus Status { get; set; }
        public Socket Socket { get; set; }
        public TcpListener Listener { get; set; }
        public bool IsListener { get; set; }
        public bool AutoAccept { get; set; }
        public bool Rejected { get; set; }
        public bool Terminated { get; set; }
        public int AuthoriseTimeoutMs { get; set; }
        public ManualResetEvent AuthoriseEvent { get; set; }

        public ROMExchangeSession()
        {
            AuthoriseEvent = new ManualResetEvent(false);
        }
    }
}