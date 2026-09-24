using System;

namespace Mp3Id
{
    /// <summary>
    /// Basic MPEG audio frame validity check, ported from the frame-sync logic in
    /// zwrserve.cs's MP3Reader (parseHeader / findSync / IsMP3). It does not decode
    /// audio - it just confirms the byte stream contains at least one valid MPEG-1/2
    /// Layer III frame header whose declared length either lands exactly on end of
    /// file or is immediately followed by another valid frame header, which is the
    /// same test zwrserve.cs uses to decide a file "IsMP3".
    /// </summary>
    internal static class Mp3FrameCheck
    {
        private static readonly int[] BitrateV1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        private static readonly int[] BitrateV2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

        // Indexed by the 2-bit version field: 0 = MPEG 2.5, 1 = reserved, 2 = MPEG 2, 3 = MPEG 1
        private static readonly int[,] SampleRateTable =
        {
            { 11025, 12000, 8000 },
            { 0, 0, 0 },
            { 22050, 24000, 16000 },
            { 44100, 48000, 32000 },
        };

        // How far into the audio data to scan for a sync point before giving up.
        private const int ScanLimitBytes = 262144; // 256 KB

        public static bool LooksLikeMp3(byte[] content, int audioStart)
        {
            if (content == null || audioStart < 0 || audioStart + 4 > content.Length)
            {
                return false;
            }

            int scanEnd = Math.Min(content.Length - 4, audioStart + ScanLimitBytes);
            bool found = false;

            for (int i = audioStart; i <= scanEnd && !found; i++)
            {
                int frameLength = ParseFrameHeader(content, i);

                if (frameLength <= 0)
                {
                    continue;
                }

                int next = i + frameLength;

                if (next == content.Length)
                {
                    // Frame reaches exactly to end of file - good enough on its own.
                    found = true;
                }
                else if (next + 4 <= content.Length && ParseFrameHeader(content, next) > 0)
                {
                    // Confirmed by a second valid frame immediately following.
                    found = true;
                }
            }

            return found;
        }

        // Returns the frame length in bytes for a valid MPEG-1/2 Layer III header at
        // data[offset..offset+4), or 0 if the header is not valid there.
        private static int ParseFrameHeader(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
            {
                return 0;
            }

            int b0 = data[offset];
            int b1 = data[offset + 1];
            int b2 = data[offset + 2];
            int b3 = data[offset + 3];

            if (b0 != 0xFF || (b1 & 0xE0) != 0xE0)
            {
                return 0;
            }

            int version = (b1 >> 3) & 3;      // 0=MPEG2.5, 1=reserved, 2=MPEG2, 3=MPEG1
            int layer = (b1 >> 1) & 3;        // Layer III is binary 01 = 1
            int bitrateIdx = (b2 >> 4) & 15;
            int rateIdx = (b2 >> 2) & 3;
            int padding = (b2 >> 1) & 1;
            int emphasis = b3 & 3;

            if (version == 1 || layer != 1 || bitrateIdx == 0 || bitrateIdx == 15 || rateIdx == 3 || emphasis == 2)
            {
                return 0;
            }

            int bitrate = ((version == 3) ? BitrateV1[bitrateIdx] : BitrateV2[bitrateIdx]) * 1000;
            int sampleRate = SampleRateTable[version, rateIdx];

            if (bitrate <= 0 || sampleRate <= 0)
            {
                return 0;
            }

            int frameLength = ((version == 3) ? 144 : 72) * bitrate / sampleRate + padding;

            return frameLength >= 4 ? frameLength : 0;
        }
    }
}
