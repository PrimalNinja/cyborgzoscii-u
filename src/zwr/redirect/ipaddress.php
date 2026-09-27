<?php
// Diagnostic only - shows what HOME_PC_IP is currently set to, as a
// clickable link. Not linked from anywhere; hit it directly to check
// inc-redirect.php's current contents without opening the file over
// FTP/SSH.
require __DIR__ . '/inc-redirect.php';

$url = "http://" . HOME_PC_IP . ":8000";
echo "<html><body><a href=\"" . $url . "\">" . $url . "</a></body></html>";