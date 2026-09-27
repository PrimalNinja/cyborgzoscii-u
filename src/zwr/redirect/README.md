# ICY Stream Port Redirector

This directory contains an `index.php`-style script (`redirect.php`) designed to dynamically
route incoming internet radio listener traffic to a local broadcasting home server running
**`zwrserve.exe`**.

## What It Does
* **Port-Preserving Redirection**: Detects the incoming port a listener or media player connects to and forwards them to the target stream server while maintaining the exact same port number.
* **Stream Path Preservation**: Retains all mounting points and query arguments (e.g., `/stream?icy=true`), ensuring seamless handoffs for media clients like VLC, Winamp, or web audio tags.
* **Security Whitelist**: Only permits redirection on pre-approved streaming ports. Any unauthorized port access is instantly blocked with an HTTP 403 Forbidden response.
* **Self-Updating Target IP**: The home PC's address is no longer hardcoded. It lives in a
  small generated include file that a separate script rewrites whenever the home PC's IP
  changes - see [Dynamic IP Updates](#dynamic-ip-updates) below.

## Current Architecture
1. **Source Stream**: A home PC runs `zwrserve.exe` (the ICY streamer), actively listening on specific broadcast ports.
2. **The Proxy/Web Host**: `redirect.php` acts as a lightweight traffic director hosted on a public web server.
3. **The Target IP**: Traffic is forwarded to whatever IP is currently defined in `inc-redirect.php` (see below) - no more hardcoded address.

> ⚠️ **Note**: For this setup to function properly, your public web server (Apache/Nginx) must be configured to bind to and listen on all the whitelisted stream ports you plan to distribute.

## Files

| File | Purpose |
|---|---|
| `redirect.php` | The redirector itself. Includes `inc-redirect.php` and 302s whitelisted-port requests to `HOME_PC_IP`. |
| `inc-template.php` | Template for the generated include. Contains the placeholder `%%IPADDRESS%%` in a `define('HOME_PC_IP', ...)` line. Edit this if you want to change what gets generated - not `inc-redirect.php`. |
| `inc-redirect.php` | **Auto-generated.** Defines the `HOME_PC_IP` constant that `redirect.php` reads. Overwritten every time the updater script runs; don't hand-edit it. Ships with a placeholder default of `127.0.0.1` until the first update comes in. |
| `secret.php` | The updater. Takes an IP address as input, copies `inc-template.php` over `inc-redirect.php` with `%%IPADDRESS%%` replaced by that value. **Rename this file** to something private/unguessable before deploying (see below). |

## Dynamic IP Updates

`secret.php` is the endpoint `zwrserve`'s `-a <url>` option posts to whenever the home PC's
public IP changes (see the `zwrserve` README). It accepts the IP two ways:

- **POST** with form field `ip` - what `zwrserve -a` sends by default.
- **GET** with `?ip=1.2.3.4` - handy for manual testing.

On success it rewrites `inc-redirect.php` and every subsequent request to `redirect.php` picks
up the new `HOME_PC_IP` automatically - no manual editing, no redeploy.

```
zwrserve -i 8000 https://example.com/zosciimq/index.php test_radio -z radio.rom \
  -a "https://example.com/whatever-you-renamed-secret-php-to.php"
```

### ⚠️ Rename `secret.php` before deploying

`secret.php` has no authentication of its own - anyone who can guess its URL and pass a valid
IP can change where `redirect.php` sends listener traffic. Its filename is the only thing
standing in the way, so:

- Rename it to something long and unguessable (e.g. `update-a1b2c3d4e5.php`) and point
  `zwrserve -a` at that name instead.
- Keep the new filename out of version control history / public repos.
- Consider adding your own extra check (a shared-secret query param, an IP allowlist for who's
  permitted to call it, etc.) if you want stronger protection than obscurity alone.

## Routers and Firewalls

`redirect.php` doesn't proxy audio itself - it just 302s the listener to
`http://<HOME_PC_IP>:<port>/...`. That means the listener's player connects **directly** to
your home PC, so the port has to actually be open all the way through, not just whitelisted
in this script.

### On the home PC
- Allow inbound TCP on every port `zwrserve` listens on (whatever you pass to `-i <port>`).
- Windows Firewall usually prompts the first time you run `zwrserve.exe` - accept for both
  **Private** and **Public** networks. If it doesn't prompt, add a manual inbound rule for
  the port.

### On the router
- Forward the same port(s), **TCP**, from the router's WAN side to the PC's **local** IP on
  that port (e.g. `192.168.1.50:8000`).
- Give the PC a static local IP or a DHCP reservation first, so the forward doesn't silently
  break when its lease renews.
- The port you forward must match both `$allowed_ports` in `redirect.php` and the `-i <port>`
  you start `zwrserve` with.

### Things that will bite you
- **CGNAT**: if your ISP doesn't give you a real routable public IP, forwarding on your own
  router won't help - there's no WAN-facing port on your router to forward from. Check your
  router's WAN IP against `https://whatismyip.com` from your ISP-facing side; if it doesn't
  match, you're likely behind CGNAT and will need to talk to your ISP or use a tunnel/VPN
  workaround instead.
- **Stale reservations**: if the PC's local IP changes and the port forward wasn't updated,
  the public IP will still update fine via `secret.php`, but the router will forward the port
  to the wrong (or no) machine.
- **Test in isolation**: before trusting the full `redirect.php`/`secret.php` chain, hit
  `http://<your_public_ip>:<port>/` directly from outside your LAN (e.g. phone on mobile
  data). That confirms the port is actually open end-to-end, separate from whether the
  redirect logic is working.

## Configuration
To change or expand the allowed streaming ports, open `redirect.php` and update the
`$allowed_ports` array:

```php
$allowed_ports = array(8000, 8002, 8500, 9000);
```

## Future Enhancements
* ~~Dynamic IP Publishing~~ - done: see [Dynamic IP Updates](#dynamic-ip-updates) above.
* Optional shared-secret / token check on the updater endpoint for defense in depth beyond an obscure filename.