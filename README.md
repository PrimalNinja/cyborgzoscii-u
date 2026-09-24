# CyborgZOSCII UNINTELLIGENCE Version v20260924

ZOSCII Zero Overhead Secure Code Information Interchange.

All Sourcecode, Tools and Applications under this repository are released uner the UNINTELLIGENCE SOFTWARE LICENSE v1.2.

For MIT Licenced versions: https://github.com/PrimalNinja/cyborgzoscii 

An innovative character encoding system that eliminates lookup table overhead while providing built-in security properties through direct memory addressing.

## Overview

CyborgZOSCII is an alternative to ASCII/PETSCII that uses direct ROM addressing instead of traditional character-to-value mapping. This approach provides significant advantages for resource-constrained systems while offering unique security properties.

## Developer Resources

- CyborgUnicorn.UNINTELLIGENCE nuget source 
(also visit https://www.nuget.org/profiles/cyborgunicornau)

## microZOSCII: Quantum-Proof Bootstrap Protocol

microZOSCII is a ZOSCII-derived, Quantum Proof mechanism to bootstrap full ZOSCII.

## UNSIGNAL / microUNSIGNAL Protocol

UNSIGNAL and microUNSIGNAL Protocol Specifications - using ZOSCII for the ultimate Quantum Proof protection.

## BRICS / microBRICS Protocol

BRICS and microBRICS Protocol Specifications - using ZOSCII for the ultimate ultimate Quantum Proof protection.

## BRAINLESS Protocol

ZOSCII already provides information-theoretic security (mathematically unbreakable, quantum-proof forever). BRAINLESS adds one thing: an Ouroboros XOR chain.

**ZOSCII: Where information theory meets practical engineering, and encryption becomes obsolete.**

## ZOSCII Web Radio

Start your own ZOSCII Web Radio stream with the tools within the zwr folder.

 mp3ids to fix mp3 tags in bulk.
 zwrpublish to publish a folder of mp3s into a nominated ZOSCII MQ queue (existing ZOSCII MQ Radio Player will work with ZOSCII encoded tracks)
 zwrserve to serve the queue as an ICY protocol stream (SHOUTcast and Icecast) (not required for the native MQ Players)
 
 https://github.com/PrimalNinja/cyborgzoscii for ZOSCII MQ and player which is released under MIT License.

## HTTP Noise Generator

The purpose of the HTTP Noise Generator is to fetch URLs that are part of a ZOSCII MQ queue that is monitored and fetch them round robin style.  
Your IP address is not hidden, the intent is to give you the ability to plausibly deny you consiously visited a website or URL.
Nothing is stored, cached or logged.

HTTP Noise Generator

Usage: noisegen <mq-endpoint> <queue-name> <rom-file> [delay-seconds] [-ua random|clear|"agent string"]

    delay-seconds minimum 30, default 30

    -ua random (default), -ua clear, -ua "my user agent"

Examples:

 noisegen https://mq.example.com/index.php myqueue mykey.jpg
 noisegen https://mq.example.com/index.php myqueue mykey.jpg 60
 noisegen https://mq.example.com/index.php myqueue mykey.jpg 60 -ua clear
 noisegen https://mq.example.com/index.php myqueue mykey.jpg -ua "Mozilla/5.0"
 
## NoEyes Handshake Protocol (Useful?)

NoEyes Handshake Protocol Specification - using microZOSCII and ZOSCII for the ultimate Quantum Proof 'real' zero-trust end to end handshake.

## superZOSCII Protocol (The Fourth Protocol - Deprecated)

superZOSCII Protocol (The Fourth Protocol) is a theoretical extension to the UNSIGNAL Protocol that introduces per-packet window rotation across arbitrarily large ROMs. All packet indirection metadata resides within the currently paged-in 64KB window, maintaining the self-referential security model of ZOSCII.

