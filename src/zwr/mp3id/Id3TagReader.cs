using System;
using System.Text;

namespace Mp3Id
{
    /// <summary>
    /// Best-effort reader for an existing artist frame in the ID3v2 tag(s) already at the
    /// start of an mp3. Used only when -a isn't given on the command line, so the file's
    /// current artist can be carried over unchanged into the fresh tag instead of being
    /// blanked out.
    ///
    /// Supports the frame layouts used by ID3v2.2 ("TP1", 3-byte non-synchsafe frame size),
    /// v2.3 ("TPE1", 4-byte non-synchsafe frame size) and v2.4 ("TPE1", 4-byte synchsafe
    /// frame size). Text encodings 0x00 (ISO-8859-1), 0x01 (UTF-16 with BOM), 0x02 (UTF-16BE,
    /// v2.4 only) and 0x03 (UTF-8) are all decoded.
    ///
    /// This is intentionally best-effort only: an extended header's declared size is skipped
    /// but the (rare) unsynchronisation scheme is not undone, and any parsing problem simply
    /// returns null rather than throwing. Returning null just means "no existing artist found",
    /// which the caller always treats as a safe fallback (no TPE1 frame is written), so there's
    /// no way a weird or corrupt tag here can produce a wrong artist or crash the run.
    /// </summary>
    internal static class Id3TagReader
    {
        // scanEnd is the offset where the audio data starts (i.e. audioStart from
        // Mp3TagWriter) - the same boundary used to skip the leading ID3v2 tag(s).
        public static string TryReadArtist(byte[] content, int scanEnd)
        {
            string result = null;

            try
            {
                int pos = 0;

                while (result == null && pos + 10 <= scanEnd &&
                       content[pos] == (byte)'I' && content[pos + 1] == (byte)'D' && content[pos + 2] == (byte)'3')
                {
                    int version = content[pos + 3];
                    int flags = content[pos + 5];
                    int tagSize = SynchsafeToInt(content, pos + 6);
                    int bodyStart = pos + 10;
                    int bodyEnd = Math.Min(bodyStart + tagSize, scanEnd);

                    bool hasExtendedHeader = (flags & 0x40) != 0;

                    if (hasExtendedHeader && bodyStart + 4 <= bodyEnd)
                    {
                        // Extended header size is synchsafe in v2.4 but a plain 32-bit value in
                        // v2.3. Reading it as synchsafe for v2.3 under-counts rather than
                        // over-counts, so worst case we fail to find TPE1 and return null - a
                        // safe outcome - rather than reading past the real frame data.
                        int extSize = (version >= 4) ? SynchsafeToInt(content, bodyStart) : BigEndian4ToInt(content, bodyStart);
                        bodyStart += 4 + extSize;
                    }

                    if (bodyStart < bodyEnd)
                    {
                        if (version == 2)
                        {
                            result = FindFrameV2(content, bodyStart, bodyEnd, "TP1");
                        }
                        else if (version == 3 || version == 4)
                        {
                            result = FindFrameV34(content, bodyStart, bodyEnd, "TPE1", version >= 4);
                        }
                    }

                    pos += 10 + tagSize;
                }
            }
            catch
            {
                result = null;
            }

            return result;
        }

        // ID3v2.2 frame header: 3-char id + 3-byte big-endian size (6 bytes total, no flags).
        private static string FindFrameV2(byte[] content, int start, int end, string frameId)
        {
            string result = null;
            int pos = start;

            while (result == null && pos + 6 <= end)
            {
                if (content[pos] == 0)
                {
                    break; // padding reached
                }

                string id = Encoding.ASCII.GetString(content, pos, 3);
                int size = BigEndian3ToInt(content, pos + 3);

                if (size < 0 || pos + 6 + size > end)
                {
                    break;
                }

                if (id == frameId && size > 0)
                {
                    result = DecodeTextFrameBody(content, pos + 6, size);
                }

                pos += 6 + size;
            }

            return result;
        }

        // ID3v2.3/2.4 frame header: 4-char id + 4-byte size + 2 bytes flags (10 bytes total).
        // The size field is synchsafe in v2.4 and a plain big-endian value in v2.3.
        private static string FindFrameV34(byte[] content, int start, int end, string frameId, bool synchsafeSize)
        {
            string result = null;
            int pos = start;

            while (result == null && pos + 10 <= end)
            {
                if (content[pos] == 0)
                {
                    break; // padding reached
                }

                string id = Encoding.ASCII.GetString(content, pos, 4);
                int size = synchsafeSize ? SynchsafeToInt(content, pos + 4) : BigEndian4ToInt(content, pos + 4);

                if (size < 0 || pos + 10 + size > end)
                {
                    break;
                }

                if (id == frameId && size > 0)
                {
                    result = DecodeTextFrameBody(content, pos + 10, size);
                }

                pos += 10 + size;
            }

            return result;
        }

        private static string DecodeTextFrameBody(byte[] content, int offset, int size)
        {
            if (size < 1)
            {
                return null;
            }

            byte encoding = content[offset];
            int textOffset = offset + 1;
            int textLength = size - 1;
            string text;

            switch (encoding)
            {
                case 0x00: // ISO-8859-1
                    text = Encoding.GetEncoding("ISO-8859-1").GetString(content, textOffset, textLength);
                    break;

                case 0x01: // UTF-16 with BOM
                    if (textLength >= 2 && content[textOffset] == 0xFF && content[textOffset + 1] == 0xFE)
                    {
                        text = Encoding.Unicode.GetString(content, textOffset + 2, textLength - 2);
                    }
                    else if (textLength >= 2 && content[textOffset] == 0xFE && content[textOffset + 1] == 0xFF)
                    {
                        text = Encoding.BigEndianUnicode.GetString(content, textOffset + 2, textLength - 2);
                    }
                    else
                    {
                        text = Encoding.Unicode.GetString(content, textOffset, textLength);
                    }
                    break;

                case 0x02: // UTF-16BE, no BOM (v2.4 only)
                    text = Encoding.BigEndianUnicode.GetString(content, textOffset, textLength);
                    break;

                case 0x03: // UTF-8
                    text = Encoding.UTF8.GetString(content, textOffset, textLength);
                    break;

                default:
                    return null;
            }

            text = text.TrimEnd('\0').Trim();

            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static int SynchsafeToInt(byte[] data, int offset)
        {
            return (data[offset] << 21) | (data[offset + 1] << 14) | (data[offset + 2] << 7) | data[offset + 3];
        }

        private static int BigEndian4ToInt(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static int BigEndian3ToInt(byte[] data, int offset)
        {
            return (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
        }
    }
}