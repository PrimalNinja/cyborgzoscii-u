// Encrypt - Plugin-based encryption registry
// Probes named DLLs for IEncryptionProvider implementations.
// (c) 2026 Cyborg Unicorn Pty Ltd - MIT License

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Encrypt
{
    // -------------------------------------------------------------------------
    // IEncryptionProvider
    // -------------------------------------------------------------------------

    /// <summary>
    /// Contract that every encryption plugin DLL must satisfy.
    /// Implement this interface, compile to a DLL, drop it in the plugins folder.
    /// </summary>
    public interface IEncryptionProvider
    {
        /// <summary>
        /// Returns a 2D array of [code, name] for this encryption plugin.
        /// Matches GetBootstrapMethods() pattern exactly.
        /// Example: return new string[,] { { "XOR", "XOR" } };
        /// </summary>
        string[,] GetEncryptionType();

        /// <summary>Encrypt plaintext. Returns ciphertext or null on failure.</summary>
        byte[]? Encrypt(byte[]? arrPlaintext_a, byte[]? arrKey_a, byte[]? arrIV_a = null);

        /// <summary>Decrypt ciphertext. Returns plaintext or null on failure.</summary>
        byte[]? Decrypt(byte[]? arrCiphertext_a, byte[]? arrKey_a, byte[]? arrIV_a = null);

        /// <summary>Generate a suitable key for this algorithm.</summary>
        byte[]? GenerateKey();

        /// <summary>Generate a suitable IV for this algorithm. May return null if IV is not used.</summary>
        byte[]? GenerateIV();
    }

    // -------------------------------------------------------------------------
    // Encryption
    // -------------------------------------------------------------------------

    /// <summary>
    /// Plugin-based encryption registry.
    ///
    /// Usage:
    ///   1. Call Probe(folder, fileNames) to load named DLLs.
    ///   2. Call GetMethods() to get [code, name] pairs for a dropdown.
    ///   3. Call Encrypt / Decrypt / GenerateKey / GenerateIV with a code.
    /// </summary>
    public class Encryption
    {
        // --- Fields ---

        private readonly Dictionary<string, IEncryptionProvider> m_dict = new Dictionary<string, IEncryptionProvider>();

        // --- Probe ---

        public void Probe(string strFolder_a, string[] arrFileNames_a)
        {
            for (int intI = 0; intI < arrFileNames_a.Length; intI++)
            {
                string strPath = Path.Combine(strFolder_a, arrFileNames_a[intI]);

                if (!File.Exists(strPath))
                {
                    continue;
                }

                try
                {
                    Assembly objAssembly = Assembly.LoadFrom(strPath);

                    foreach (Type objType in objAssembly.GetTypes())
                    {
                        if (typeof(IEncryptionProvider).IsAssignableFrom(objType) && !objType.IsInterface && !objType.IsAbstract)
                        {
                            try
                            {
                                IEncryptionProvider? objProvider = (IEncryptionProvider?)Activator.CreateInstance(objType);

                                if (objProvider != null)
                                {
                                    string[,] arrType = objProvider.GetEncryptionType();

                                    if (arrType != null && arrType.GetLength(0) > 0 && arrType.GetLength(1) >= 2)
                                    {
                                        string strCode = arrType[0, 0];
                                        if (!string.IsNullOrEmpty(strCode))
                                        {
                                            m_dict[strCode] = objProvider;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Provider failed to instantiate — skip
                            }
                        }
                    }
                }
                catch
                {
                    // DLL failed to load — skip
                }
            }
        }

        // --- Discovery ---

        public string[,] GetMethods()
        {
            string[,] arrResult = new string[m_dict.Count, 2];
            int intI = 0;

            foreach (KeyValuePair<string, IEncryptionProvider> objPair in m_dict)
            {
                string[,] arrType = objPair.Value.GetEncryptionType();

                if (arrType != null && arrType.GetLength(0) > 0 && arrType.GetLength(1) >= 2)
                {
                    arrResult[intI, 0] = arrType[0, 0];
                    arrResult[intI, 1] = arrType[0, 1];
                }

                intI++;
            }

            return arrResult;
        }

        public bool HasProvider(string strCode_a)
        {
            return m_dict.ContainsKey(strCode_a);
        }

        // --- Encrypt / Decrypt ---

        public byte[]? Encrypt(string strCode_a, byte[]? arrPlaintext_a, byte[]? arrKey_a, byte[]? arrIV_a = null)
        {
            if (m_dict.TryGetValue(strCode_a, out IEncryptionProvider? objProvider))
            {
                try
                {
                    return objProvider.Encrypt(arrPlaintext_a, arrKey_a, arrIV_a);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public byte[]? Decrypt(string strCode_a, byte[]? arrCiphertext_a, byte[]? arrKey_a, byte[]? arrIV_a = null)
        {
            if (m_dict.TryGetValue(strCode_a, out IEncryptionProvider? objProvider))
            {
                try
                {
                    return objProvider.Decrypt(arrCiphertext_a, arrKey_a, arrIV_a);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        // --- Key / IV generation ---

        public byte[]? GenerateKey(string strCode_a)
        {
            if (m_dict.TryGetValue(strCode_a, out IEncryptionProvider? objProvider))
            {
                try
                {
                    return objProvider.GenerateKey();
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public byte[]? GenerateIV(string strCode_a)
        {
            if (m_dict.TryGetValue(strCode_a, out IEncryptionProvider? objProvider))
            {
                try
                {
                    return objProvider.GenerateIV();
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}