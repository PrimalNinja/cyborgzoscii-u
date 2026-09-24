using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mp3Id
{
    /// <summary>
    /// Builds a minimal ID3v2.3 tag: TIT2 (title), TPE1 (artist), TYER (year),
    /// TCOP (copyright), and optionally COMM (comment), USLT (lyrics) and APIC
    /// (front-cover album art).
    /// Text frames (TIT2/TPE1/TYER/TCOP/COMM/USLT) are written UTF-8 (encoding byte 0x03)
    /// so Chinese, Korean, and any other non-Latin-1 text round-trips correctly. APIC's own
    /// encoding byte is the one exception: it's set to 0x00 (Latin-1) regardless, since its
    /// description field is always empty here and 0x00 is the one value every ID3v2.3 reader
    /// is guaranteed to handle for that field, including strict/older parsers that reject or
    /// skip an APIC frame over an unexpected encoding byte.
    /// </summary>
    internal static class Id3TagBuilder
    {
        private const byte EncodingUtf8 = 0x03;     // used for TIT2/TPE1/TYER/TCOP/COMM/USLT - full Unicode (CJK etc.)
        private const byte EncodingIso8859 = 0x00;  // used only for APIC's encoding byte - description is always empty, and 0x00 is the one encoding value every ID3v2.3 reader is guaranteed to accept for that field
        private const byte PictureTypeFrontCover = 0x03;

        public static byte[] BuildMinimalId3Tag(string title, string artist, string year, string copyright, string comment, string lyrics, byte[] albumArtJpeg)
        {
            List<byte[]> frames = new List<byte[]>
            {
                BuildTextFrame("TIT2", title),
                BuildTextFrame("TPE1", artist),
                BuildTextFrame("TYER", year),   // Official ID3v2.3 year tracking frame
                BuildTextFrame("TCOP", copyright),
            };

            if (!string.IsNullOrEmpty(comment))
            {
                frames.Add(BuildCommentFrame("eng", comment));
            }

            if (!string.IsNullOrEmpty(lyrics))
            {
                frames.Add(BuildLyricsFrame("eng", lyrics));
            }

            if (albumArtJpeg != null && albumArtJpeg.Length > 0)
            {
                frames.Add(BuildApicFrame("image/jpeg", PictureTypeFrontCover, "", albumArtJpeg));
            }

            int totalLength = frames.Sum(f => f.Length);
            byte[] frameBytes = new byte[totalLength];
            int offset = 0;

            foreach (byte[] frame in frames)
            {
                Array.Copy(frame, 0, frameBytes, offset, frame.Length);
                offset += frame.Length;
            }

            int tagSize = frameBytes.Length;
            byte[] sizeBytes =
            {
                (byte)((tagSize >> 21) & 0x7F),
                (byte)((tagSize >> 14) & 0x7F),
                (byte)((tagSize >> 7)  & 0x7F),
                (byte)( tagSize        & 0x7F),
            };

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("ID3"), 0, 3);
                ms.WriteByte(0x03); // version 2.3
                ms.WriteByte(0x00); // revision 0
                ms.WriteByte(0x00); // flags: no unsynchronisation, no extended header
                ms.Write(sizeBytes, 0, 4);
                ms.Write(frameBytes, 0, frameBytes.Length);
                return ms.ToArray();
            }
        }

        // TIT2/TPE1/TYER/TCOP-style frame: encoding byte + text + null terminator.
        private static byte[] BuildTextFrame(string frameId, string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text ?? "");
            byte[] textContent = new byte[1 + textBytes.Length + 1];

            textContent[0] = EncodingUtf8;
            Array.Copy(textBytes, 0, textContent, 1, textBytes.Length);
            textContent[textContent.Length - 1] = 0x00;

            return WrapFrame(frameId, textContent);
        }

        // COMM: encoding(1) + language(3, ISO-639-2) + short description + \0 + comment text + \0
        private static byte[] BuildCommentFrame(string language, string text)
        {
            byte[] langBytes = Encoding.ASCII.GetBytes(PadLanguageCode(language));
            byte[] textBytes = Encoding.UTF8.GetBytes(text ?? "");

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(EncodingUtf8);
                ms.Write(langBytes, 0, 3);
                ms.WriteByte(0x00); // empty short description string + its null terminator
                ms.Write(textBytes, 0, textBytes.Length);
                ms.WriteByte(0x00); // terminator for the comment text body itself

                return WrapFrame("COMM", ms.ToArray());
            }
        }

        // USLT: encoding(1) + language(3, ISO-639-2) + content descriptor + \0 + lyrics text + \0
        // Unsynchronised = plain lyrics, no per-line/per-word timing. That's what this writes.
        private static byte[] BuildLyricsFrame(string language, string text)
        {
            byte[] langBytes = Encoding.ASCII.GetBytes(PadLanguageCode(language));
            byte[] textBytes = Encoding.UTF8.GetBytes(text ?? "");

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(EncodingUtf8);
                ms.Write(langBytes, 0, 3);
                ms.WriteByte(0x00); // empty content descriptor string + its null terminator
                ms.Write(textBytes, 0, textBytes.Length);
                ms.WriteByte(0x00); // terminator for the lyrics text body itself

                return WrapFrame("USLT", ms.ToArray());
            }
        }

        // APIC: encoding(1) + MIME type + \0 + picture type(1) + description + \0 + picture data
        private static byte[] BuildApicFrame(string mimeType, byte pictureType, string description, byte[] imageData)
        {
            byte[] mimeBytes = Encoding.ASCII.GetBytes(mimeType);

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(EncodingIso8859);              // Corrected text encoding identifier (0x00)
                ms.Write(mimeBytes, 0, mimeBytes.Length);   // MIME string payload
                ms.WriteByte(0x00);                         // MIME terminator bit
                ms.WriteByte(pictureType);                  // Front Cover tag identifier byte
                ms.WriteByte(0x00);                         // Empty description byte block configuration
                ms.Write(imageData, 0, imageData.Length);   // Safe uncorrupted graphic image sequence buffer

                return WrapFrame("APIC", ms.ToArray());
            }
        }

        private static string PadLanguageCode(string language)
        {
            string code = (language ?? "eng").ToLowerInvariant();

            if (code.Length > 3)
            {
                code = code.Substring(0, 3);
            }

            return code.PadRight(3, ' ');
        }

        private static byte[] WrapFrame(string frameId, byte[] frameContent)
        {
            byte[] idBytes = Encoding.ASCII.GetBytes(frameId);
            int frameSize = frameContent.Length;

            byte[] sizeBytes =
            {
                (byte)((frameSize >> 24) & 0xFF),
                (byte)((frameSize >> 16) & 0xFF),
                (byte)((frameSize >> 8)  & 0xFF),
                (byte)( frameSize        & 0xFF),
            };

            byte[] flagsBytes = { 0x00, 0x00 };

            byte[] result = new byte[idBytes.Length + sizeBytes.Length + flagsBytes.Length + frameContent.Length];
            int offset = 0;

            Array.Copy(idBytes, 0, result, offset, idBytes.Length);
            offset += idBytes.Length;

            Array.Copy(sizeBytes, 0, result, offset, sizeBytes.Length);
            offset += sizeBytes.Length;

            Array.Copy(flagsBytes, 0, result, offset, flagsBytes.Length);
            offset += flagsBytes.Length;

            Array.Copy(frameContent, 0, result, offset, frameContent.Length);

            return result;
        }
    }
}