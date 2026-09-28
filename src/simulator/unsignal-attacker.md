# ZOSCII / UNSIGNAL Attacker - Custom Attack API

A custom attack is a `.js` file that defines one function:

```js
function attack(api) {
  // your attack here
  return 'optional result';
}
```

Load it with **CUSTOM ATTACK**, set ATTACK TYPE to **Custom attack**, then click **RUN ATTACK**.

## The `api` object

| Field | What it is |
|---|---|
| `api.mode` | `'unsignal'` or `'zoscii'` |
| `api.wire` | Full file bytes as a number array (a copy) |
| `api.structures` | Array of candidate readings: 4 in UNSIGNAL mode, 2 in ZOSCII mode. Each is `{name, reading, parity, addrs}` |
| `api.readAddrs(bytes, start)` | Pairs bytes into little-endian 16-bit addresses from byte `start` |
| `api.peel(bytes)` | Keyless BRAINLESS inverse |
| `api.crib` | Crib box text as typed, line breaks kept |
| `api.cribBytes` | Crib as UTF-8 bytes, encoded the same way the encoder encodes the message |
| `api.dictionary` | Loaded attack file text, or `''` if none |
| `api.allow` | Allowed positions as a number array (empty = all) |
| `api.forbid` | Forbidden positions as a number array |
| `api.log(text)` | Prints a line to the attack console |

### Fields of each `api.structures` entry

| Field | Values |
|---|---|
| `name` | `'RAW/EVEN'`, `'RAW/ODD'`, `'PEELED/EVEN'`, `'PEELED/ODD'` (ZOSCII: `'RAW/start0'`, `'PEELED/start0'`) |
| `reading` | `'RAW'` or `'PEELED'` |
| `parity` | `0` = EVEN, `1` = ODD |
| `addrs` | Address array for that reading (a copy) |

## What is NOT passed

Nothing that an attacker does not hold:

- the ROM (private key)
- prefix or suffix length
- message length or position
- BRAINLESS on/off
- the plaintext

If your attack needs any of these, it is using access, not attacking.

## Return value

Whatever `attack` returns is printed as `result: ...`. Strings print as-is, and anything else is printed as JSON. It may also return a Promise.

Errors are caught and printed as `[custom] error: ...`.

## Honesty note

The attack runs inside the same page as the encoder, so it could read the encoder panel from the page directly. An honest attack uses `api` only.

## Example

```js
function attack(api) {
  for (const s of api.structures) api.log(s.name + ': ' + s.addrs.length + ' addresses');
  return 'example finished';
}
```