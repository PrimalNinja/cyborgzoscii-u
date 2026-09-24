using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mp3Id
{
    /// <summary>
    /// Ports mp3id.php's cleanAndRewriteTags()/buildMinimalID3Tag()/filenameToTitle(),
    /// adds the new rules: fail hard on files that don't pass the basic MP3 check
    /// (same style of check as zwrserve.cs's MP3Reader), and picks up a matching
    /// .jpg (album art) and .txt (comment) next to each mp3.
    /// </summary>
    internal static class Mp3TagWriter
    {
        /// <summary>
        /// Processes one mp3 file in place. Throws on any failure (caller logs the message).
        /// Returns a human-readable summary of what was written, for the success log.
        /// </summary>
        public static string ProcessFile(string filePath, string artistName, string copyright)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception("File does not exist");
            }

            DateTime originalWriteTime;

            try
            {
                originalWriteTime = File.GetLastWriteTime(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot read file modification time - " + ex.Message);
            }

            string year = originalWriteTime.Year.ToString();

            byte[] content;

            try
            {
                content = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read file contents (permissions?) - " + ex.Message);
            }

            // Skip every leading ID3v2 tag (there can be more than one, as in the PHP tool).
            int audioStart = SkipLeadingId3v2Tags(content);

            if (audioStart > content.Length)
            {
                throw new Exception("ID3 tag size overflows file - file may be corrupt");
            }

            int audioLength = content.Length - audioStart;

            if (audioLength <= 0)
            {
                throw new Exception("No audio data found after ID3 header - file may be corrupt");
            }

            // Basic MP3 check: look for a valid MPEG-1/2 Layer III frame sync in the audio data,
            // confirmed by a second valid frame immediately following it (or landing exactly on
            // end of file) - the same test zwrserve.cs uses to decide IsMP3.
            if (!Mp3FrameCheck.LooksLikeMp3(content, audioStart))
            {
                throw new Exception("Basic MP3 check failed - no valid MPEG Layer III frame sync found");
            }

            byte[] audioData = new byte[audioLength];
            Array.Copy(content, audioStart, audioData, 0, audioLength);

            string title = FilenameToTitle(Path.GetFileNameWithoutExtension(filePath));

            string directory = Path.GetDirectoryName(filePath) ?? "";
            string baseNameNoExt = Path.Combine(directory, Path.GetFileNameWithoutExtension(filePath));
            string jpgPath = baseNameNoExt + ".jpg";
            string txtPath = baseNameNoExt + ".txt";
            string lyricsPath = baseNameNoExt + ".lyrics.txt";

            byte[] albumArt = null;
            string comment = null;
            string lyrics = null;

            if (File.Exists(jpgPath))
            {
                try
                {
                    albumArt = File.ReadAllBytes(jpgPath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Found " + Path.GetFileName(jpgPath) + " but could not read it - " + ex.Message);
                }
            }

            if (File.Exists(txtPath))
            {
                try
                {
                    comment = File.ReadAllText(txtPath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Found " + Path.GetFileName(txtPath) + " but could not read it - " + ex.Message);
                }
            }

            if (File.Exists(lyricsPath))
            {
                try
                {
                    lyrics = File.ReadAllText(lyricsPath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Found " + Path.GetFileName(lyricsPath) + " but could not read it - " + ex.Message);
                }
            }

            byte[] newTag = Id3TagBuilder.BuildMinimalId3Tag(title, artistName, year, copyright, comment, lyrics, albumArt);

            byte[] newContent = new byte[newTag.Length + audioData.Length];
            Array.Copy(newTag, 0, newContent, 0, newTag.Length);
            Array.Copy(audioData, 0, newContent, newTag.Length, audioData.Length);

            try
            {
                File.WriteAllBytes(filePath, newContent);
            }
            catch (Exception ex)
            {
                throw new Exception("file write failed - disk full or permissions issue - " + ex.Message);
            }

            try
            {
                // Restore original modification time, same as the PHP tool's touch() call.
                File.SetLastWriteTime(filePath, originalWriteTime);
            }
            catch
            {
                // Non-fatal: tags were written successfully even if the timestamp couldn't be restored.
            }

            List<string> parts = new List<string>();
            parts.Add("Title='" + title + "'");
            parts.Add("Artist='" + artistName + "'");
            parts.Add("Year='" + year + "'");
            parts.Add("Copyright='" + copyright + "'");
            parts.Add(albumArt != null
                ? "AlbumArt='" + Path.GetFileName(jpgPath) + "' (" + albumArt.Length + " bytes)"
                : "AlbumArt=none");
            parts.Add(comment != null
                ? "Comment='" + Path.GetFileName(txtPath) + "' (" + comment.Length + " chars)"
                : "Comment=none");
            parts.Add(lyrics != null
                ? "Lyrics='" + Path.GetFileName(lyricsPath) + "' (" + lyrics.Length + " chars)"
                : "Lyrics=none");
            parts.Add("Written " + newContent.Length + " bytes total");

            return string.Join(", ", parts);
        }

        // Walks past every leading ID3v2 tag (there is normally at most one, but the PHP tool
        // defensively loops, so this does too) and returns the offset of the first audio byte.
        private static int SkipLeadingId3v2Tags(byte[] content)
        {
            int pos = 0;
            int contentLen = content.Length;

            while (pos + 10 <= contentLen &&
                   content[pos] == (byte)'I' &&
                   content[pos + 1] == (byte)'D' &&
                   content[pos + 2] == (byte)'3')
            {
                int b0 = content[pos + 6];
                int b1 = content[pos + 7];
                int b2 = content[pos + 8];
                int b3 = content[pos + 9];

                // Synchsafe integer: 7 bits per byte.
                int tagSize = (b0 << 21) | (b1 << 14) | (b2 << 7) | b3;

                pos += 10 + tagSize;
            }

            return pos;
        }

        // Filename -> title: verbatim. The user names the file exactly how they want the
        // title to read, so nothing is changed here beyond dropping the .mp3 extension.
        internal static string FilenameToTitle(string filenameNoExt)
        {
            return filenameNoExt;
        }
    }
}