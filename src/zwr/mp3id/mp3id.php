<?php
/**
 * MP3 Title Tag Updater - WEB VERSION (FIXED)
 * Scans current folder for MP3s, sets title from filename,
 * sets artist from constant, ADDS year from file date
 */

// ==================== CONFIGURATION ====================
define('ARTIST_NAME',    'Cyborg Unicorn / Primal Ninja');
define('COPYRIGHT',      'Copyright (c) 2026 Cyborg Unicorn / Primal Ninja. All rights reserved. Permission granted to broadcast anywhere including traditional radio.');
define('CREATE_BACKUPS', false);

// ==================== FIX: Set working directory to script location ====================
chdir(__DIR__);

// ==================== FUNCTIONS ====================

function getMp3Files() {
    $files = glob("*.mp3");
    if ($files === false) {
        $files = [];
    }
    if (empty($files)) {
        $allFiles = scandir('.');
        $files = array_filter($allFiles, function($file) {
            return preg_match('/\.mp3$/i', $file);
        });
    }
    return array_values($files);
}

function filenameToTitle($filename) {
    // Verbatim: the user names the file exactly how the title should read.
    // Nothing is changed here beyond dropping the .mp3 extension.
    return preg_replace('/\.mp3$/i', '', $filename);
}

function cleanAndRewriteTags($filepath, $title, &$debug = '') {
    if (!file_exists($filepath)) {
        $debug = "File does not exist: $filepath";
        return false;
    }
    if (!is_readable($filepath)) {
        $debug = "File is not readable (check permissions): $filepath";
        return false;
    }
    if (!is_writable($filepath)) {
        $debug = "File is not writable (check permissions): $filepath";
        return false;
    }

    // Save original file modification time
    $original_mtime = filemtime($filepath);
    $year = date('Y', $original_mtime);

    $content = file_get_contents($filepath);
    if ($content === false) {
        $debug = "Failed to read file contents";
        return false;
    }

    // Find where audio data starts — skip ALL leading ID3 tags
    $pos = 0;
    $contentLen = strlen($content);

    while ($pos + 10 <= $contentLen && substr($content, $pos, 3) === 'ID3') {
        // Read the 4 synchsafe size bytes (offset +6)
        $b = [
            ord($content[$pos + 6]),
            ord($content[$pos + 7]),
            ord($content[$pos + 8]),
            ord($content[$pos + 9]),
        ];
        // Synchsafe integer decode: each byte uses only 7 bits
        $tagSize = ($b[0] << 21) | ($b[1] << 14) | ($b[2] << 7) | $b[3];

        // Check for extended header flag (bit 6 of flags byte at offset +5)
        // If set, there is an extra extended header after the main header
        // We don't parse it — just note it exists; tagSize already includes it
        // per the spec, so no adjustment needed.

        $pos += 10 + $tagSize;
    }

    // $pos now points to the first non-ID3 byte (the actual audio data)
    if ($pos > $contentLen) {
        $debug = "ID3 tag size overflows file — file may be corrupt";
        return false;
    }

    $audioData = substr($content, $pos);

    if (strlen($audioData) === 0) {
        $debug = "No audio data found after ID3 header — file may be corrupt";
        return false;
    }

    $newTag = buildMinimalID3Tag($title, ARTIST_NAME, $year);
    $newContent = $newTag . $audioData;

    // Write the file
    $result = file_put_contents($filepath, $newContent);

    if ($result !== false) {
        // Restore original file modification time
        touch($filepath, $original_mtime);
        $debug = "Written " . strlen($newContent) . " bytes. Title='$title', Artist='" . ARTIST_NAME . "', Year='$year', Copyright='" . COPYRIGHT . "'";
        return true;
    } else {
        $debug = "file_put_contents() failed — disk full or permissions issue";
        return false;
    }
}

function buildMinimalID3Tag($title, $artist, $year) {
    // Build ID3v2.3 frames
    $tit2Frame = buildTextFrame('TIT2', $title);
    $tpe1Frame = buildTextFrame('TPE1', $artist);

    // TYER = Year (ID3v2.3)
    $tyerFrame = buildTextFrame('TYER', $year);

    // TDRC = Recording time (ID3v2.4 players also read this)
    $tdrcFrame = buildTextFrame('TDRC', $year);

    // TCOP = Copyright (standard ID3 copyright frame)
    $tcopFrame = buildTextFrame('TCOP', COPYRIGHT);

    $frames = $tit2Frame . $tpe1Frame . $tyerFrame . $tdrcFrame . $tcopFrame;

    // Encode total frame length as a synchsafe integer (7 bits per byte)
    $tagSize = strlen($frames);
    $sizeBytes = [
        ($tagSize >> 21) & 0x7F,
        ($tagSize >> 14) & 0x7F,
        ($tagSize >> 7)  & 0x7F,
         $tagSize        & 0x7F,
    ];

    // ID3v2.3 header: "ID3" + version 2.3 + revision 0 + flags 0x00 + synchsafe size
    $header = 'ID3'
        . "\x03\x00"          // version 2.3, revision 0
        . "\x00"              // flags (no unsynchronisation, no extended header)
        . chr($sizeBytes[0])
        . chr($sizeBytes[1])
        . chr($sizeBytes[2])
        . chr($sizeBytes[3]);

    return $header . $frames;
}

function buildTextFrame($frameId, $text) {
    // Encoding byte: 0x03 = UTF-8
    $encoding = "\x03";
    // Frame content: encoding byte + text + null terminator
    $textContent = $encoding . $text . "\x00";
    $frameSize = strlen($textContent);
    // ID3v2.3 frame sizes are plain big-endian 32-bit (NOT synchsafe)
    $sizeBytes = pack('N', $frameSize);
    $flags = "\x00\x00";
    return $frameId . $sizeBytes . $flags . $textContent;
}

function backupFile($filepath) {
    $backupPath = $filepath . '.backup';
    if (!file_exists($backupPath)) {
        return copy($filepath, $backupPath);
    }
    return true;
}

// ==================== HANDLE FORM SUBMISSION ====================
$message = '';
$messageType = '';
$results = [];
$cwd = getcwd(); // For display/debugging

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action'])) {
    if ($_POST['action'] === 'update') {
        $mp3Files = getMp3Files();

        if (empty($mp3Files)) {
            $message = 'No MP3 files found in: ' . htmlspecialchars($cwd);
            $messageType = 'error';
        } else {
            $successCount = 0;
            $failCount = 0;

            foreach ($mp3Files as $filename) {
                $title = filenameToTitle($filename);
                $debug = '';

                if (CREATE_BACKUPS) {
                    backupFile($filename);
                }

                if (cleanAndRewriteTags($filename, $title, $debug)) {
                    $successCount++;
                    $results[] = [
                        'file'   => $filename,
                        'title'  => $title,
                        'artist' => ARTIST_NAME,
                        'year'   => date('Y', filemtime($filename)),
                        'status' => 'success',
                        'debug'  => $debug,
                    ];
                } else {
                    $failCount++;
                    $results[] = [
                        'file'   => $filename,
                        'title'  => $title,
                        'status' => 'failed',
                        'debug'  => $debug,
                    ];
                }
            }

            $message = "Complete: $successCount updated, $failCount failed";
            $messageType = $failCount > 0 ? 'warning' : 'success';
        }
    } elseif ($_POST['action'] === 'restore') {
        $backupFiles = glob("*.mp3.backup");
        $restoredCount = 0;

        foreach ($backupFiles as $backup) {
            $original = str_replace('.backup', '', $backup);
            if (copy($backup, $original)) {
                $restoredCount++;
                $results[] = [
                    'file'   => $original,
                    'status' => 'restored',
                ];
            }
        }

        $message = "Restored $restoredCount files from backups";
        $messageType = 'success';
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>MP3 Tag Cleaner</title>
    <style>
        * { box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif; max-width: 1200px; margin: 0 auto; padding: 20px; background: #f5f5f5; }
        .container { background: white; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); padding: 20px; margin-bottom: 20px; }
        h1 { margin-top: 0; color: #333; }
        .warning { background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; border-radius: 4px; }
        .error { background: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0; border-radius: 4px; color: #721c24; }
        .success { background: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0; border-radius: 4px; color: #155724; }
        .warning-message { background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; border-radius: 4px; color: #856404; }
        button { background: #007bff; color: white; border: none; padding: 12px 24px; font-size: 16px; border-radius: 4px; cursor: pointer; margin-right: 10px; }
        button:hover { background: #0056b3; }
        button.danger { background: #dc3545; }
        button.danger:hover { background: #c82333; }
        table { width: 100%; border-collapse: collapse; }
        th, td { text-align: left; padding: 10px; border-bottom: 1px solid #dee2e6; }
        th { background: #f8f9fa; font-weight: 600; }
        .status-success { color: #28a745; font-weight: bold; }
        .status-failed { color: #dc3545; font-weight: bold; }
        .status-restored { color: #17a2b8; font-weight: bold; }
        .config { background: #e9ecef; padding: 10px; border-radius: 4px; margin-bottom: 15px; font-family: monospace; font-size: 13px; }
        .debug { font-family: monospace; font-size: 11px; color: #666; margin-top: 4px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>🎵 MP3 Tag Cleaner &amp; Updater</h1>

        <div class="config">
            <strong>Current Configuration:</strong><br>
            Script directory: <code><?php echo htmlspecialchars(__DIR__); ?></code><br>
            Working directory: <code><?php echo htmlspecialchars($cwd); ?></code><br>
            Artist Name: <code><?php echo htmlspecialchars(ARTIST_NAME); ?></code><br>
            Year: <code>Auto-set from file's modification date</code><br>
            MP3 files found: <code><?php echo count(getMp3Files()); ?></code>
        </div>

        <div class="warning">
            <strong>⚠️ WARNING:</strong> This tool REMOVES all existing ID3 tags.<br>
            New tags written: <strong>Title</strong> (from filename), <strong>Artist</strong>, <strong>Year</strong> (from file date)<br>
            All other metadata (album, genre, album art, comments) will be DELETED.
        </div>

        <?php if ($message): ?>
            <div class="<?php echo htmlspecialchars($messageType); ?>">
                <strong><?php echo ucfirst(htmlspecialchars($messageType)); ?>:</strong> <?php echo htmlspecialchars($message); ?>
            </div>
        <?php endif; ?>

        <form method="post" style="display: inline-block;">
            <input type="hidden" name="action" value="update">
            <button type="submit">🎯 Update All MP3 Tags</button>
        </form>

        <?php if (CREATE_BACKUPS && count(glob("*.mp3.backup")) > 0): ?>
        <form method="post" style="display: inline-block;">
            <input type="hidden" name="action" value="restore">
            <button type="button" class="danger" onclick="confirmRestore(this.form)">↩️ Restore from Backups</button>
        </form>
        <?php endif; ?>
    </div>

    <?php if (!empty($results)): ?>
    <div class="container">
        <h2>📋 Results</h2>
        <table>
            <thead>
                <tr>
                    <th>File</th>
                    <th>Title Written</th>
                    <th>Artist Written</th>
                    <th>Year Written</th>
                    <th>Status</th>
                </tr>
            </thead>
            <tbody>
                <?php foreach ($results as $result): ?>
                <tr>
                    <td><?php echo htmlspecialchars($result['file']); ?></td>
                    <td><?php echo isset($result['title'])  ? htmlspecialchars($result['title'])  : '-'; ?></td>
                    <td><?php echo isset($result['artist']) ? htmlspecialchars($result['artist']) : '-'; ?></td>
                    <td><?php echo isset($result['year'])   ? htmlspecialchars($result['year'])   : '-'; ?></td>
                    <td>
                        <?php if ($result['status'] === 'success'): ?>
                            <span class="status-success">✓ Updated</span>
                            <?php if (isset($result['debug'])): ?>
                                <div class="debug"><?php echo htmlspecialchars($result['debug']); ?></div>
                            <?php endif; ?>
                        <?php elseif ($result['status'] === 'failed'): ?>
                            <span class="status-failed">✗ Failed</span>
                            <div class="debug"><?php echo htmlspecialchars($result['debug']); ?></div>
                        <?php elseif ($result['status'] === 'restored'): ?>
                            <span class="status-restored">↩ Restored</span>
                        <?php endif; ?>
                    </td>
                </tr>
                <?php endforeach; ?>
            </tbody>
        </table>
    </div>
    <?php endif; ?>

    <div class="container">
        <h3>🔍 Verify Tags</h3>
        <p>After running, use a command line tool to verify the tags:</p>
        <pre>
# On Linux/Mac:
id3v2 -l "filename.mp3"

# Or using ffprobe:
ffprobe -v quiet -show_format "filename.mp3" | grep TAG

# Or just play the file in your music player and check Properties/Info
        </pre>
    </div>

<script>
function confirmRestore(form) {
    if (confirm('⚠️ WARNING: This will restore ALL backed up MP3 files.\n\n' +
                'Any changes made after the last update will be lost.\n\n' +
                'Are you absolutely sure?')) {
        form.submit();
    }
}
</script>
</body>
</html>