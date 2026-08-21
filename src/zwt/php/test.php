<?php
// CyborgUnicorn.ZOSCII - ZWT PHP test harness
// Runs from CLI (php test.php) or browser (via test.html -> test.php?run=1).
// No third-party libraries.
// (c) 2026 Cyborg Unicorn Pty Ltd

require_once 'Zoscii.php';
require_once 'Unsignal.php';
require_once 'Zwt.php';

// --- Tiny test framework ---

$g_intPass = 0;
$g_intFail = 0;
$g_blnHtml = false;

function out($strText_a)
{
    global $g_blnHtml;

    if ($g_blnHtml)
    {
        echo htmlspecialchars($strText_a) . "<br>\n";
    }
    else
    {
        echo $strText_a . "\n";
    }
}

function section($strName_a)
{
    out('');
    out('--- ' . $strName_a . ' ---');
}

function ok($strMsg_a)
{
    global $g_intPass;

    $g_intPass++;
    out('  [PASS] ' . $strMsg_a);
}

function bad($strMsg_a)
{
    global $g_intFail;

    $g_intFail++;
    out('  [FAIL] ' . $strMsg_a);
}

function check($blnCond_a, $strMsg_a)
{
    if ($blnCond_a)
    {
        ok($strMsg_a);
    }
    else
    {
        bad($strMsg_a);
    }
}

// --- Build a deterministic-but-distinct test ROM (128KB, all byte values present) ---

function makeRom($intSeed_a)
{
    $strRom = '';
    $intI = 0;
    $intState = 0;

    $intState = $intSeed_a;

    // Guarantee every byte 0..255 appears (needed for UNSIGNAL header bytes),
    // then fill the rest pseudo-randomly from the seed so ROMs differ.
    for ($intI = 0; $intI < 256; $intI++)
    {
        $strRom .= chr($intI);
    }

    for ($intI = 256; $intI < 131072; $intI++)
    {
        $intState = (($intState * 1103515245) + 12345) & 0x7FFFFFFF;
        $strRom .= chr(($intState >> 16) & 0xFF);
    }

    return $strRom;
}

// --- Tests ---

function runTests()
{
    $objShared = null;
    $objIssuer1 = null;
    $objIssuer2 = null;
    $strSig = '';
    $strPriv = '';
    $strShared = '';
    $arrIssue = null;
    $arrOpen = null;
    $arrIntro = null;
    $arrVerify = null;
    $strTampered = '';
    $intMid = 0;
    $strDiffSig = '';
    $arrIntro2 = null;
    $arrIntro3 = null;
    $strRoundIn = '';
    $strEnc = false;
    $strDec = false;

    // ROMs
    $objShared = Zoscii::fromString(makeRom(1));
    $objIssuer1 = Zoscii::fromString(makeRom(2));
    $objIssuer2 = Zoscii::fromString(makeRom(3));

    section('Zoscii');
    check($objShared !== null && $objShared->isLoaded(), 'Zoscii::fromString - loaded, size ' . $objShared->lngRomSize);
    $strRoundIn = 'The quick brown fox 0123456789';
    $strEnc = $objShared->encode($strRoundIn);
    check($strEnc !== false, 'Zoscii::encode - returns data');
    $strDec = $objShared->decode($strEnc);
    check($strDec === $strRoundIn, 'Zoscii::decode - round trip matches');

    section('Unsignal');
    $strRoundIn = "binary\x00\x01\x02 payload with nulls";
    $strEnc = Unsignal::encode($objShared, $strRoundIn);
    check($strEnc !== false, 'Unsignal::encode - returns data');
    check(strlen($strEnc) > strlen($strRoundIn) * 2, 'Unsignal::encode - larger than 2x (header+prefix+suffix)');
    $strDec = Unsignal::decode($objShared, $strEnc);
    check($strDec === $strRoundIn, 'Unsignal::decode - round trip matches (binary-safe)');
    $strEnc2 = Unsignal::encode($objShared, $strRoundIn);
    check($strEnc2 !== $strEnc, 'Unsignal::encode - non-deterministic (random header/prefix/suffix)');

    section('ZWT');
    $strSig = Zwt::newSignature();
    $strPriv = '{"uid":"42","role":"admin"}';
    $strShared = '{"scope":"read"}';

    $arrIssue = Zwt::issue($strSig, $strPriv, $strShared, $objIssuer1, $objIssuer2, $objShared);
    check($arrIssue['success'], 'Zwt::issue - token minted');

    if ($arrIssue['success'])
    {
        out('    [SIZE] sharedsignature:  ' . strlen($strSig) . ' bytes');
        out('    [SIZE] privateclaims:    ' . strlen($strPriv) . ' bytes');
        out('    [SIZE] sharedclaims:     ' . strlen($strShared) . ' bytes');
        out('    [SIZE] issuerdata:       ' . strlen($arrIssue['issuerData']) . ' bytes');
        out('    [SIZE] ZWT token total:  ' . strlen($arrIssue['token']) . ' bytes');

        // Open (relying party)
        $arrOpen = Zwt::open($arrIssue['token'], $objShared);
        check($arrOpen['success'] && $arrOpen['sharedSig'] === $strSig, 'Zwt::open - RP reads sharedsignature (before)');
        check($arrOpen['sharedClaims'] === $strShared, 'Zwt::open - RP reads sharedclaims (before)');

        // Introspect (issuer)
        $arrIntro = Zwt::introspect($arrOpen['issuerData'], $arrOpen['sharedSig'], $objIssuer1, $objIssuer2);
        check($arrIntro['success'] && $arrIntro['privateClaims'] === $strPriv,
              'Zwt::introspect - issuer reads privateclaims, sharedsig matches (before)');

        // Verify (both ROMs)
        $arrVerify = Zwt::verify($arrIssue['token'], $objShared, $objIssuer1, $objIssuer2);
        check($arrVerify['success'], 'Zwt::verify - full verify passes (before)');

        // Tamper the shared region (inside the length header + sharedstuff) -> open must reject.
        // Flip a byte just after the length header, which lands in sharedstuff.
        $strTampered = $arrIssue['token'];
        $intMid = 10; // past the 8-byte length header, inside sharedstuff
        $strTampered[$intMid] = chr(ord($strTampered[$intMid]) ^ 0xFF);
        $arrOpen2 = Zwt::open($strTampered, $objShared);
        check(!$arrOpen2['success'], 'Zwt tamper (shared region) - open correctly rejects');

        // Tamper the issuerdata -> open still succeeds (open does not validate issuer data),
        // but introspect must reject it. Flip a byte at the very start of issuerdata (its UNSIGNAL
        // header region), which is always meaningful; a flip there always breaks the double-decode
        // or the sealed frame's integrity hash. (Flips landing in UNSIGNAL slack/suffix bytes are
        // absorbed harmlessly - they cannot alter the sealed sharedsig or privateclaims.)
        $strTamperTail = $arrIssue['token'];
        $intSharedLenForTest = strlen($arrIssue['token']) - strlen($arrIssue['issuerData']);
        $intTail = $intSharedLenForTest; // first byte of issuerdata (its UNSIGNAL header)
        $strTamperTail[$intTail] = chr(ord($strTamperTail[$intTail]) ^ 0xFF);
        $arrTailOpen = Zwt::open($strTamperTail, $objShared);
        $arrTailIntro = null;
        if ($arrTailOpen['success'])
        {
            $arrTailIntro = Zwt::introspect($arrTailOpen['issuerData'], $arrTailOpen['sharedSig'], $objIssuer1, $objIssuer2);
        }
        check($arrTailOpen['success'] && $arrTailIntro !== null && !$arrTailIntro['success'],
              'Zwt tamper (issuer header) - introspect correctly rejects');

        // Change shared signature: introspect with a different sig must fail
        $strDiffSig = Zwt::newSignature();
        $arrIntro2 = Zwt::introspect($arrOpen['issuerData'], $strDiffSig, $objIssuer1, $objIssuer2);
        check(!$arrIntro2['success'], 'Zwt changed sharedsig - different sig correctly fails (after)');

        // Original sig still verifies after the change attempt
        $arrIntro3 = Zwt::introspect($arrOpen['issuerData'], $strSig, $objIssuer1, $objIssuer2);
        check($arrIntro3['success'], 'Zwt changed sharedsig - original sig still verifies (after)');

        // Empty claims baseline
        $arrEmpty = Zwt::issue(Zwt::newSignature(), null, null, $objIssuer1, $objIssuer2, $objShared);
        check($arrEmpty['success'], 'Zwt::issue - empty-claims token minted');
        if ($arrEmpty['success'])
        {
            out('    [SIZE] empty-claims token: ' . strlen($arrEmpty['token']) . ' bytes');
        }

        // Wrong ROM cannot open
        $arrWrong = Zwt::open($arrIssue['token'], $objIssuer1);
        check(!$arrWrong['success'], 'Zwt::open - wrong ROM correctly fails');

        // Bare token: no issuer ROMs -> no issuerdata
        $strBareSig = Zwt::newSignature();
        $arrBare = Zwt::issue($strBareSig, null, '{"scope":"guest"}', null, null, $objShared);
        check($arrBare['success'], 'Zwt::issue - bare token (no issuer ROMs) minted');
        if ($arrBare['success'])
        {
            out('    [SIZE] bare token (no issuerdata): ' . strlen($arrBare['token']) . ' bytes');
            check(strlen($arrBare['issuerData']) == 0, 'Zwt bare - issuerData is empty');

            $arrBareOpen = Zwt::open($arrBare['token'], $objShared);
            check($arrBareOpen['success'] && $arrBareOpen['sharedSig'] === $strBareSig,
                  'Zwt bare - open reads sharedsig');
            check(strlen($arrBareOpen['issuerData']) == 0, 'Zwt bare - open sees no issuerdata (remainder empty)');

            $arrBareVerify = Zwt::verify($arrBare['token'], $objShared, null, null);
            check($arrBareVerify['success'] && $arrBareVerify['sharedClaims'] === '{"scope":"guest"}',
                  'Zwt bare - verify passes with no attestation');
        }
    }
}

// --- Entry point ---

function main()
{
    global $g_intPass, $g_intFail, $g_blnHtml;

    $g_blnHtml = (php_sapi_name() != 'cli');

    out('ZWT PHP Test Harness');
    out('(c) 2026 Cyborg Unicorn Pty Ltd');

    runTests();

    out('');
    out('=================================================');
    out(' Results: ' . $g_intPass . ' passed, ' . $g_intFail . ' failed');
    out('=================================================');
}

main();