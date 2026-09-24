# ICY Stream Port Redirector

This directory contains an `index.php` script designed to dynamically route incoming internet radio listener traffic to a local broadcasting home server running **`zwrserve.exe`**.

## What It Does
* **Port-Preserving Redirection**: Detects the incoming port a listener or media player connects to and forwards them to the target stream server while maintaining the exact same port number.
* **Stream Path Preservation**: Retains all mounting points and query arguments (e.g., `/stream?icy=true`), ensuring seamless handoffs for media clients like VLC, Winamp, or web audio tags.
* **Security Whitelist**: Only permits redirection on pre-approved streaming ports. Any unauthorized port access is instantly blocked with an HTTP 403 Forbidden response.

## Current Architecture
1. **Source Stream**: A home PC runs `zwrserve.exe` (the ICY streamer), actively listening on specific broadcast ports.
2. **The Proxy/Web Host**: This PHP script acts as a lightweight traffic director hosted on a public web server.
3. **The Target IP**: Traffic is currently forwarded to the static home IP address: **`127.0.0.1`**.

> ⚠️ **Note**: For this setup to function properly, your public web server (Apache/Nginx) must be configured to bind to and listen on all the whitelisted stream ports you plan to distribute.

## Future Enhancements
* **Dynamic IP Publishing**: The script currently uses a hardcoded target IP address. A future update to `zwrserve.exe` will introduce a "Publish IP" feature. 
* **Automated Updates**: Once deployed, the home PC will periodically ping this web directory via an API call to update its current external IP automatically—completely eliminating stream downtime caused by residential dynamic IP changes.

## Configuration
To change or expand the allowed streaming ports, open `index.php` and update the `$allowed_ports` array:

```php
\$allowed_ports = array(8000, 8002, 8500, 9000);
```
