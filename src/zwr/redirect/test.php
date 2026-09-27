<?php
// Diagnostic only - checks whether HOME_PC_IP:port is reachable from
// THIS server (a genuine external vantage point, unlike testing from
// your home LAN). Not linked from anywhere; hit it directly.
require __DIR__ . '/inc-redirect.php';

$port    = isset($_GET['port']) ? intval($_GET['port']) : 8000;
$timeout = 5; // seconds

header('Content-Type: text/plain');

echo "Checking " . HOME_PC_IP . ":" . $port . " ...\n\n";

$start = microtime(true);
$errno = 0;
$errstr = '';
$conn = @fsockopen(HOME_PC_IP, $port, $errno, $errstr, $timeout);
$elapsed = round((microtime(true) - $start) * 1000);

if ($conn) {
    echo "OPEN - connected in {$elapsed}ms\n";
    fclose($conn);
} else {
    echo "CLOSED or unreachable after {$elapsed}ms\n";
    echo "Error {$errno}: {$errstr}\n";
}

echo "\n(Add ?port=8002 etc. to the URL to check a different port.)\n";