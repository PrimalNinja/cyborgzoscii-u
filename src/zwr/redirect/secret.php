<?php
// ---------------------------------------------------------------------
// RENAME THIS FILE before you deploy it (e.g. to something long and
// unguessable). Its filename is the only thing standing between the
// public internet and the ability to overwrite HOME_PC_IP.
//
// This is the endpoint zwrserve's "-a <url>" option posts to whenever
// your home PC's public IP changes, e.g.:
//   zwrserve -i 8000 ... -a "https://example.com/whatever-you-renamed-this-to.php"
// It also accepts a plain GET with ?ip=1.2.3.4 for manual/testing use.
// ---------------------------------------------------------------------

$ip = '';
if (isset($_POST['ip'])) {
    $ip = trim($_POST['ip']);
} elseif (isset($_GET['ip'])) {
    $ip = trim($_GET['ip']);
}

if ($ip === '' || filter_var($ip, FILTER_VALIDATE_IP) === false) {
    header("HTTP/1.1 400 Bad Request");
    echo "Invalid or missing IP address.";
    exit();
}

$templatePath = __DIR__ . '/inc-template.php';
$outputPath   = __DIR__ . '/inc-redirect.php';

if (!is_readable($templatePath)) {
    header("HTTP/1.1 500 Internal Server Error");
    echo "Template file (inc-template.php) is missing or unreadable.";
    exit();
}

$template = file_get_contents($templatePath);
$generated = str_replace('%%IPADDRESS%%', $ip, $template);

// Atomic-ish write: write to a temp file then rename, so redirect.php
// never sees a half-written include.
$tmpPath = $outputPath . '.tmp';
if (file_put_contents($tmpPath, $generated) === false || !rename($tmpPath, $outputPath)) {
    header("HTTP/1.1 500 Internal Server Error");
    echo "Failed to write inc-redirect.php.";
    exit();
}

echo "OK: HOME_PC_IP updated to $ip";