<?php
// Home PC's current IP lives in HOME_PC_IP, defined in inc-redirect.php.
// inc-redirect.php is auto-generated - see inc-template.php and secret.php.
require __DIR__ . '/inc-redirect.php';

// Define your whitelisted stream ports here
$allowed_ports = array(8000);

// Detect the port number used to access this script
$current_port = intval($_SERVER['SERVER_PORT']);

// Check if the current port is in the whitelist
if (in_array($current_port, $allowed_ports)) {
    // Construct the destination URL preserving the port and trailing stream path
    $redirect_url = "http://" . HOME_PC_IP . ":" . $current_port . $_SERVER['REQUEST_URI'];

    // Execute the HTTP 302 redirect
    header("Location: " . $redirect_url, true, 302);
    exit();
} else {
    // If the port is not whitelisted, return a 403 Forbidden status
    header("HTTP/1.1 403 Forbidden");
    echo "Access Denied: Port not whitelisted.";
    exit();
}
?>