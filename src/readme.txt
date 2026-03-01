ZOSCII/UNSIGNAL Demonstration Guide
====================================
(c) 2026 Cyborg Unicorn Pty Ltd
MIT License / UNINTELLIGENCE SOFTWARE LICENSE v1.1

This guide demonstrates how ZOSCII encoding produces completely different
output every time, even with identical input files, and how UNSIGNAL wraps
the output in statistical perfection.

REQUIRED FILES
-------------
- zencode.exe      (ZOSCII encoder)
- zdecode.exe      (ZOSCII decoder)
- uencode.exe      (UNSIGNAL encoder)
- udecode.exe      (UNSIGNAL decoder)
- zstrength.exe    (ROM strength analyzer)
- ent.exe          (Fourmilab randomness tester, download from: https://www.fourmilab.ch/random/)

- selfie.jpg       (Your ROM file - use any photo of yourself!)
- hello.txt        (Create this: contains "Hello" on first line)

THE DEMONSTRATION
-----------------

STEP 1: Create a simple test file
---------------------------------
Create hello.txt with exactly:
Hello

(That's 5 letters plus a newline = 8 characters total)

STEP 2: Encode the same file twice with ZOSCII
-----------------------------------------------
zencode selfie.jpg hello.txt hello1.zoc
zencode selfie.jpg hello.txt hello2.zoc

STEP 3: Encode the same file twice with UNSIGNAL
-------------------------------------------------
uencode selfie.jpg hello.txt hello1.sig
uencode selfie.jpg hello.txt hello2.sig

STEP 4: Examine the raw bytes
-----------------------------
Use any hex viewer, or for a quick look:
certutil -encodehex hello1.zoc hello1.hex
certutil -encodehex hello2.zoc hello2.hex
type hello1.hex
type hello2.hex

Notice: The two .zoc files have NO bytes in common!
Same input, completely different output.

STEP 5: Test randomness with ENT
--------------------------------
ent hello1.zoc
ent hello2.zoc
ent hello1.sig
ent hello2.sig

Observe:
- .zoc files: Low entropy (4.0), obvious patterns, small size
- .sig files: Near-perfect entropy (7.2-7.3), excellent chi-square,
              sizes vary (different random prefix/suffix lengths)

STEP 6: Analyze with zstrength
------------------------------
zstrength selfie.jpg hello1.zoc
zstrength selfie.jpg hello2.zoc
zstrength selfie.jpg hello1.sig
zstrength selfie.jpg hello2.sig

Observe:
- .zoc files: Only 16 bytes used, limited character set
- .sig files: 60-70% of all possible bytes appear, perfect distribution

STEP 7: Verify you can decode
------------------------------
zdecode selfie.jpg hello1.zoc hello1-out.txt
zdecode selfie.jpg hello2.zoc hello2-out.txt
type hello1-out.txt
type hello2-out.txt

udecode selfie.jpg hello1.sig hello1-sig-out.txt
udecode selfie.jpg hello2.sig hello2-sig-out.txt
type hello1-sig-out.txt
type hello2-sig-out.txt

All should contain the original "Hello"!

WHAT THIS PROVES
----------------

1. Same input + same ROM + same algorithm = TOTALLY DIFFERENT output
2. ZOSCII output is deterministic but unpredictable (random address selection)
3. UNSIGNAL wraps the output in statistical perfection
4. File sizes vary due to random prefix/suffix lengths
5. Even with multiple encoded versions, an attacker learns NOTHING

THE MAGIC NUMBERS
-----------------

Your .sig files should show:
- Entropy: ~7.2-7.3 bits/byte (near maximum 8)
- Chi-square: 10-90% (ideal randomness)
- Mean: 125-128 (near-perfect 127.5)
- Distinct bytes: 65-75% of all 256 possibilities

Your .zoc files will show:
- Entropy: 4.0 (only 16 bytes)
- Tiny file size (16 bytes)
- Obvious patterns

WHY THIS MATTERS
----------------

If you encoded "Top Secret Document" a million times, you'd get
a million completely different outputs. An attacker with all million
versions still couldn't determine the original content.

This is perfect forward security - each encoding is unique!

NOTES
-----
- Results may vary slightly due to random number generation
- Different ROMs will produce different address sets
- The magic is that it works EVERY time, differently every time

Enjoy your mathematically unbreakable encoding!

TESTS BY EXAMPLE
================

1. let's first test our file strength against the ROM we are going to use.

	note: for pure ZOSCII only the Low 64KB is used, but for UNSIGNAL we
		  have a random sliding window between the two 64KB halves.

	D:\dev\zosciibin>zstrength selfie.jpg hello.txt
	ZOSCII ROM Strength Analyzer
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

	ROM Strength Analysis
	=====================

	Input Information:
	- Text Length: 5 characters
	- Characters Utilized: 4 of 256 (1.6%)

	General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
	This File Security: ~10^14 (~38.4 trillion permutations)

2. let's zencode and uencode the hello.txt file twice:

	D:\dev\zosciibin>zencode selfie.jpg hello.txt hello1.zoc
	ZOSCII Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License


	D:\dev\zosciibin>zencode selfie.jpg hello.txt hello2.zoc
	ZOSCII Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License


	D:\dev\zosciibin>uencode selfie.jpg hello.txt hello1.sig
	UNSIGNAL Protocol Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1


	D:\dev\zosciibin>uencode selfie.jpg hello.txt hello2.sig
	UNSIGNAL Protocol Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1

3. now let's analyse the inputs and the outputs

ANALYSE the selfie image:

	D:\dev\zosciibin>ent selfie.jpg
	Entropy = 7.951053 bits per byte.

	Optimum compression would reduce the size
	of this 188836 byte file by 0 percent.

	Chi square distribution for 188836 samples is 15430.49, and randomly
	would exceed this value less than 0.01 percent of the times.

	Arithmetic mean value of data bytes is 121.0219 (127.5 = random).
	Monte Carlo value for Pi is 3.238942552 (error 3.10 percent).
	Serial correlation coefficient is 0.090584 (totally uncorrelated = 0.0).


ANALYSE the hello text file:

	D:\dev\zosciibin>ent hello.txt
-->	Entropy = 1.921928 bits per byte.			<-- highly patterned

	Optimum compression would reduce the size
	of this 5 byte file by 75 percent.

	Chi square distribution for 5 samples is 353.40, and randomly
	would exceed this value less than 0.01 percent of the times.

-->	Arithmetic mean value of data bytes is 100.0000 (127.5 = random).	<-- biased towards lowercase letters
	Monte Carlo value for Pi is -1.#IND00000 (error 1.#R percent).
	Serial correlation coefficient is -0.170213 (totally uncorrelated = 0.0).


ANALYSE the two zoc files:

	D:\dev\zosciibin>ent hello1.zoc
-->	Entropy = 3.321928 bits per byte.			<-- much higher than the input

	Optimum compression would reduce the size
	of this 10 byte file by 58 percent.

	Chi square distribution for 10 samples is 246.00, and randomly
	would exceed this value 64.57 percent of the times.

-->	Arithmetic mean value of data bytes is 114.4000 (127.5 = random).	<-- centered around lower-mid values
	Monte Carlo value for Pi is 4.000000000 (error 27.32 percent).
	Serial correlation coefficient is -0.612172 (totally uncorrelated = 0.0).

	D:\dev\zosciibin>ent hello2.zoc
-->	Entropy = 3.321928 bits per byte.			<-- much higher than the input

	Optimum compression would reduce the size
	of this 10 byte file by 58 percent.

	Chi square distribution for 10 samples is 246.00, and randomly
	would exceed this value 64.57 percent of the times.

-->	Arithmetic mean value of data bytes is 180.2000 (127.5 = random).	<-- centered around high values
	Monte Carlo value for Pi is 4.000000000 (error 27.32 percent).
	Serial correlation coefficient is 0.224456 (totally uncorrelated = 0.0).


ANALYSE the two sig files:

	D:\dev\zosciibin>ent hello1.sig
-->	Entropy = 7.305502 bits per byte.			<-- near perfect randomness

	Optimum compression would reduce the size
	of this 309 byte file by 8 percent.

	Chi square distribution for 309 samples is 260.17, and randomly
	would exceed this value 39.87 percent of the times.		<-- ideal in range

-->	Arithmetic mean value of data bytes is 123.5178 (127.5 = random).
	Monte Carlo value for Pi is 2.980392157 (error 5.13 percent).
	Serial correlation coefficient is -0.029079 (totally uncorrelated = 0.0).

	D:\dev\zosciibin>ent hello2.sig
-->	Entropy = 7.159204 bits per byte.			<-- near perfect randomness

	Optimum compression would reduce the size
	of this 235 byte file by 10 percent.

	Chi square distribution for 235 samples is 232.34, and randomly
	would exceed this value 84.26 percent of the times.		<-- ideal in range

-->	Arithmetic mean value of data bytes is 123.3191 (127.5 = random).
	Monte Carlo value for Pi is 2.871794872 (error 8.59 percent).
	Serial correlation coefficient is 0.010892 (totally uncorrelated = 0.0).


ANALYSE the character distribution (input bytes) beween the sig and zoc files:

	D:\dev\zosciibin>zstrength.exe selfie.jpg hello1.zoc
	ZOSCII ROM Strength Analyzer
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

	ROM Strength Analysis
	=====================

	Input Information:
	- Text Length: 10 characters
	- Characters Utilized: 10 of 256 (3.9%)

	General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
	This File Security: ~10^24 (More than all atoms in the observable universe (10^24 permutations))

	D:\dev\zosciibin>zstrength.exe selfie.jpg hello1.sig
	ZOSCII ROM Strength Analyzer
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

	ROM Strength Analysis
	=====================

	Input Information:
	- Text Length: 309 characters
	- Characters Utilized: 180 of 256 (70.3%)

	General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
	This File Security: ~10^729 (Incomprehensibly massive (10^729 permutations))

	D:\dev\zosciibin>zstrength.exe selfie.jpg hello2.zoc
	ZOSCII ROM Strength Analyzer
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

	ROM Strength Analysis
	=====================

	Input Information:
	- Text Length: 10 characters
	- Characters Utilized: 10 of 256 (3.9%)

	General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
	This File Security: ~10^22 (More than all atoms in the observable universe (10^22 permutations))

	D:\dev\zosciibin>zstrength.exe selfie.jpg hello2.sig
	ZOSCII ROM Strength Analyzer
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

	ROM Strength Analysis
	=====================

	Input Information:
	- Text Length: 235 characters
	- Characters Utilized: 158 of 256 (61.7%)

	General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
	This File Security: ~10^556 (Incomprehensibly massive (10^556 permutations))

NOW FOR FUN, LETS zencode selfie.jpg with itself and uencode selfie.jpg with itself, twice:

	D:\dev\zosciibin>zencode selfie.jpg selfie.jpg selfie1.zoc
	ZOSCII Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License


	D:\dev\zosciibin>zencode selfie.jpg selfie.jpg selfie2.zoc
	ZOSCII Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License


	D:\dev\zosciibin>uencode selfie.jpg selfie.jpg selfie1.sig
	UNSIGNAL Protocol Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1


	D:\dev\zosciibin>uencode selfie.jpg selfie.jpg selfie2.sig
	UNSIGNAL Protocol Encoder
	(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - UNINTELLIGENCE SOFTWARE LICENSE v1.1


ANALYSE the two zoc files:

	D:\dev\zosciibin>ent selfie1.zoc
	Entropy = 7.993025 bits per byte.

	Optimum compression would reduce the size
	of this 377672 byte file by 0 percent.

	Chi square distribution for 377672 samples is 3577.58, and randomly
	would exceed this value less than 0.01 percent of the times.

	Arithmetic mean value of data bytes is 131.0223 (127.5 = random).
	Monte Carlo value for Pi is 3.062737310 (error 2.51 percent).
	Serial correlation coefficient is -0.003865 (totally uncorrelated = 0.0).

	D:\dev\zosciibin>ent selfie2.zoc
	Entropy = 7.993010 bits per byte.

	Optimum compression would reduce the size
	of this 377672 byte file by 0 percent.

	Chi square distribution for 377672 samples is 3585.68, and randomly
	would exceed this value less than 0.01 percent of the times.

	Arithmetic mean value of data bytes is 130.6956 (127.5 = random).
	Monte Carlo value for Pi is 3.058416078 (error 2.65 percent).
	Serial correlation coefficient is -0.003997 (totally uncorrelated = 0.0).


ANALYSE the two sig files:

	D:\dev\zosciibin>ent selfie1.sig
	Entropy = 7.995541 bits per byte.

	Optimum compression would reduce the size
	of this 377929 byte file by 0 percent.

	Chi square distribution for 377929 samples is 2274.21, and randomly
	would exceed this value less than 0.01 percent of the times.

	Arithmetic mean value of data bytes is 131.5025 (127.5 = random).
	Monte Carlo value for Pi is 3.073855338 (error 2.16 percent).
	Serial correlation coefficient is -0.005961 (totally uncorrelated = 0.0).

	D:\dev\zosciibin>ent selfie2.sig
	Entropy = 7.994839 bits per byte.

	Optimum compression would reduce the size
	of this 378002 byte file by 0 percent.

	Chi square distribution for 378002 samples is 2640.05, and randomly
	would exceed this value less than 0.01 percent of the times.

	Arithmetic mean value of data bytes is 132.0366 (127.5 = random).
	Monte Carlo value for Pi is 3.046285714 (error 3.03 percent).
	Serial correlation coefficient is -0.001876 (totally uncorrelated = 0.0).


ANALYSE the character distribution (input bytes) beween the sig and zoc files:

D:\dev\zosciibin>zstrength.exe selfie.jpg selfie1.zoc
ZOSCII ROM Strength Analyzer
(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

ROM Strength Analysis
=====================

Input Information:
- Text Length: 377672 characters
- Characters Utilized: 256 of 256 (100.0%)

General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
This File Security: ~10^881981 (Astronomically secure (10^0.9M permutations))

D:\dev\zosciibin>zstrength.exe selfie.jpg selfie2.zoc
ZOSCII ROM Strength Analyzer
(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

ROM Strength Analysis
=====================

Input Information:
- Text Length: 377672 characters
- Characters Utilized: 256 of 256 (100.0%)

General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
This File Security: ~10^882234 (Astronomically secure (10^0.9M permutations))

D:\dev\zosciibin>zstrength.exe selfie.jpg selfie1.sig
ZOSCII ROM Strength Analyzer
(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

ROM Strength Analysis
=====================

Input Information:
- Text Length: 377929 characters
- Characters Utilized: 256 of 256 (100.0%)

General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
This File Security: ~10^883636 (Astronomically secure (10^0.9M permutations))

D:\dev\zosciibin>zstrength.exe selfie.jpg selfie2.sig
ZOSCII ROM Strength Analyzer
(c) 2026 Cyborg Unicorn Pty Ltd v20260301 - MIT License

ROM Strength Analysis
=====================

Input Information:
- Text Length: 378002 characters
- Characters Utilized: 256 of 256 (100.0%)

General ROM Capacity: ~10^600 (Incomprehensibly massive (10^600 permutations))
This File Security: ~10^883781 (Astronomically secure (10^0.9M permutations))

That is correct, the encoded ones are not jpegs anymore, in fact the original data is absent from the encoded files.
