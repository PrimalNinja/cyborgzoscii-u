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
    /// (same style of check as zwrserve.cs's MP3Reader), picks up a matching .jpg
    /// (album art) and .txt (comment) next to each mp3, and - when the caller passes
    /// no artist (-a wasn't given) - reads whatever artist is already tagged on the
    /// file first and carries it over unchanged, rather than blanking it out.
    /// </summary>
    internal static class Mp3TagWriter
    {
        /// <summary>
        /// Processes one mp3 file in place. Throws on any failure (caller logs the message).
        /// Returns a human-readable summary of what was written, for the success log.
        /// requestedArtist: the -a "<artist>" value, or null/empty if -a wasn't given - in
        /// which case any artist already tagged on the file is read first and carried over
        /// unchanged; if the file has no existing artist tag either, no TPE1 frame is written.
        /// forcedAlbumArt/forcedAlbumArtName: when supplied (the -l <logofile> flag), this
        /// exact image is used as the album art for every file and <filename>.jpg next to
        /// the mp3 is never even looked at. When null, the per-file <filename>.jpg (if any)
        /// is used instead, same as before.
        /// </summary>
        public static string ProcessFile(string filePath, string requestedArtist, string copyright, byte[] forcedAlbumArt, string forcedAlbumArtName)
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

            // Resolve the artist to write: -a wins if given; otherwise look for the artist
            // already tagged on the file (in the ID3v2 header(s) we're about to strip) and
            // carry it over unchanged; if neither is available, no artist frame is written.
            string finalArtist = requestedArtist;
            string artistNote = null;

            if (string.IsNullOrEmpty(finalArtist))
            {
                string existingArtist = Id3TagReader.TryReadArtist(content, audioStart);

                if (existingArtist != null)
                {
                    finalArtist = existingArtist;
                    artistNote = "preserved from existing tag";
                }
                else
                {
                    artistNote = "none - no -a given and no existing artist tag found";
                }
            }

            byte[] audioData = new byte[audioLength];
            Array.Copy(content, audioStart, audioData, 0, audioLength);

            string title = FilenameToTitle(Path.GetFileNameWithoutExtension(filePath));

            string directory = Path.GetDirectoryName(filePath) ?? "";
            string baseNameNoExt = Path.Combine(directory, Path.GetFileNameWithoutExtension(filePath));
            string jpgPath = baseNameNoExt + ".jpg";
            string txtPath = baseNameNoExt + ".txt";
            string lyricsPath = baseNameNoExt + ".lyrics.txt";

            byte[] albumArt;
            string albumArtLabel;
            string comment = null;
            string lyrics = null;

            if (forcedAlbumArt != null)
            {
                // -l <logofile> was given: use it for every file, don't even check for a
                // matching <filename>.jpg.
                albumArt = forcedAlbumArt;
                albumArtLabel = forcedAlbumArtName + " (logo, used for every file)";
            }
            else if (File.Exists(jpgPath))
            {
                try
                {
                    albumArt = File.ReadAllBytes(jpgPath);
                    albumArtLabel = Path.GetFileName(jpgPath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Found " + Path.GetFileName(jpgPath) + " but could not read it - " + ex.Message);
                }
            }
            else
            {
                albumArt = null;
                albumArtLabel = null;
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

            byte[] newTag = Id3TagBuilder.BuildMinimalId3Tag(title, finalArtist, year, copyright, comment, lyrics, albumArt);

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
            parts.Add(finalArtist != null
                ? "Artist='" + finalArtist + "'" + (artistNote != null ? " (" + artistNote + ")" : "")
                : "Artist=" + artistNote);
            parts.Add("Year='" + year + "'");
            parts.Add("Copyright='" + copyright + "'");
            parts.Add(albumArt != null
                ? "AlbumArt='" + albumArtLabel + "' (" + albumArt.Length + " bytes)"
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