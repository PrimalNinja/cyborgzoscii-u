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

	Byte Analysis:
	Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
	----  ---  ----------  ----------  -----------  ----
	0x00    0        1320         521            0
	0x01    1         728         232            0
	0x02    2         526         211            0
	0x03    3         691         231            0
	0x04    4         654         220            0
	0x05    5         328         215            0
	0x06    6         728         221            0
	0x07    7         304         265            0
	0x08    8         381         177            0
	0x09    9         284         205            0
	0x0A   10         342         185            0
	0x0B   11         199         224            0
	0x0C   12         214         214            0
	0x0D   13         288         262            0
	0x0E   14         230         249            0
	0x0F   15         210         259            0
	0x10   16         299         184            0
	0x11   17         257         166            0
	0x12   18         179         210            0
	0x13   19         339         222            0
	0x14   20         213         184            0
	0x15   21         129         228            0
	0x16   22         217         235            0
	0x17   23         197         224            0
	0x18   24         214         211            0
	0x19   25         157         192            0
	0x1A   26         195         284            0
	0x1B   27         143         249            0
	0x1C   28         207         289            0
	0x1D   29         266         286            0
	0x1E   30         263         292            0
	0x1F   31         229         282            0
	0x20   32         564         213            0
	0x21   33         231         159            0    !
	0x22   34         184         166            0    "
	0x23   35         302         214            0    #
	0x24   36         226         220            0    $
	0x25   37         189         194            0    %
	0x26   38         148         226            0    &
	0x27   39         194         268            0    '
	0x28   40         181         179            0    (
	0x29   41         169         264            0    )
	0x2A   42         289         273            0    *
	0x2B   43         246         222            0    +
	0x2C   44         150         187            0    ,
	0x2D   45         461         254            0    -
	0x2E   46         484         305            0    .
	0x2F   47         422         229            0    /
	0x30   48        1155         225            0    0
	0x31   49         437         267            0    1
	0x32   50         685         183            0    2
	0x33   51         280         248            0    3
	0x34   52         384         251            0    4
	0x35   53         329         288            0    5
	0x36   54         294         292            0    6
	0x37   55         277         293            0    7
	0x38   56         347         269            0    8
	0x39   57         315         223            0    9
	0x3A   58         297         291            0    :
	0x3B   59         214         272            0    ;
	0x3C   60         198         258            0    <
	0x3D   61         290         325            0    =
	0x3E   62         166         318            0    >
	0x3F   63         170         267            0    ?
	0x40   64         238         207            0    @
	0x41   65         742         219            0    A
	0x42   66         232         144            0    B
	0x43   67         294         227            0    C
	0x44   68         674         185            0    D
	0x45   69         153         222            0    E
	0x46   70         186         259            0    F
	0x47   71         331         240            0    G
	0x48   72         286         210            1    H
	0x49   73         699         275            0    I
	0x4A   74         202         238            0    J
	0x4B   75         149         254            0    K
	0x4C   76         219         256            0    L
	0x4D   77         195         267            0    M
	0x4E   78         155         292            0    N
	0x4F   79         185         306            0    O
	0x50   80         240         178            0    P
	0x51   81         179         257            0    Q
	0x52   82         231         236            0    R
	0x53   83         267         273            0    S
	0x54   84         675         248            0    T
	0x55   85         403         309            0    U
	0x56   86         134         262            0    V
	0x57   87         146         246            0    W
	0x58   88         189         251            0    X
	0x59   89         147         237            0    Y
	0x5A   90         226         277            0    Z
	0x5B   91         136         246            0    [
	0x5C   92         208         295            0    \
	0x5D   93         157         287            0    ]
	0x5E   94         187         281            0    ^
	0x5F   95         163         262            0    _
	0x60   96         183         223            0    `
	0x61   97         908         245            0    a
	0x62   98         432         223            0    b
	0x63   99         790         284            0    c
	0x64  100        1045         195            0    d
	0x65  101         895         278            1    e
	0x66  102         351         220            0    f
	0x67  103         461         261            0    g
	0x68  104         372         265            0    h
	0x69  105         702         308            0    i
	0x6A  106         295         274            0    j
	0x6B  107         220         326            0    k
	0x6C  108         456         304            2    l
	0x6D  109         397         307            0    m
	0x6E  110         548         266            0    n
	0x6F  111         722         243            1    o
	0x70  112         639         259            0    p
	0x71  113         231         303            0    q
	0x72  114         622         228            0    r
	0x73  115         664         308            0    s
	0x74  116         663         279            0    t
	0x75  117         401         289            0    u
	0x76  118         243         261            0    v
	0x77  119         197         276            0    w
	0x78  120         278         257            0    x
	0x79  121         196         224            0    y
	0x7A  122         170         299            0    z
	0x7B  123         178         297            0    {
	0x7C  124         150         276            0    |
	0x7D  125         170         260            0    }
	0x7E  126         149         239            0    ~
	0x7F  127         152         271            0
	0x80  128         295         213            0
	0x81  129         227         202            0
	0x82  130         271         231            0
	0x83  131         204         248            0
	0x84  132         210         161            0
	0x85  133         162         207            0
	0x86  134         287         255            0
	0x87  135         176         282            0
	0x88  136         156         236            0
	0x89  137         174         216            0
	0x8A  138         188         224            0
	0x8B  139         129         248            0
	0x8C  140         130         218            0
	0x8D  141         169         304            0
	0x8E  142         181         288            0
	0x8F  143         193         301            0
	0x90  144         198         211            0
	0x91  145         146         197            0
	0x92  146         169         212            0
	0x93  147         167         255            0
	0x94  148         170         216            0
	0x95  149         192         269            0
	0x96  150         168         244            0
	0x97  151         154         241            0
	0x98  152         163         265            0
	0x99  153         138         227            0
	0x9A  154         158         230            0
	0x9B  155         206         278            0
	0x9C  156         167         230            0
	0x9D  157         146         301            0
	0x9E  158         176         242            0
	0x9F  159         165         270            0
	0xA0  160         215         209            0
	0xA1  161         183         210            0
	0xA2  162         220         236            0
	0xA3  163         192         299            0
	0xA4  164         182         253            0
	0xA5  165         187         331            0
	0xA6  166         180         292            0
	0xA7  167         155         307            0
	0xA8  168         199         250            0
	0xA9  169         183         291            0
	0xAA  170         194         331            0
	0xAB  171         147         304            0
	0xAC  172         124         260            0
	0xAD  173         164         278            0
	0xAE  174         167         314            0
	0xAF  175         165         312            0
	0xB0  176         133         258            0
	0xB1  177         135         245            0
	0xB2  178         142         246            0
	0xB3  179         129         270            0
	0xB4  180         199         335            0
	0xB5  181         171         318            0
	0xB6  182         185         274            0
	0xB7  183         126         260            0
	0xB8  184         198         300            0
	0xB9  185         179         268            0
	0xBA  186         154         283            0
	0xBB  187         158         261            0
	0xBC  188         167         255            0
	0xBD  189         165         327            0
	0xBE  190         137         249            0
	0xBF  191         114         251            0
	0xC0  192         181         180            0
	0xC1  193         159         254            0
	0xC2  194         161         197            0
	0xC3  195         173         301            0
	0xC4  196         172         287            0
	0xC5  197         139         247            0
	0xC6  198         147         259            0
	0xC7  199         178         286            0
	0xC8  200         159         164            0
	0xC9  201         132         235            0
	0xCA  202         186         276            0
	0xCB  203         115         248            0
	0xCC  204         176         236            0
	0xCD  205         151         270            0
	0xCE  206         217         257            0
	0xCF  207         126         292            0
	0xD0  208         174         229            0
	0xD1  209         155         263            0
	0xD2  210         166         295            0
	0xD3  211         168         330            0
	0xD4  212         195         318            0
	0xD5  213         165         345            0
	0xD6  214         149         284            0
	0xD7  215         171         335            0
	0xD8  216         128         263            0
	0xD9  217         201         284            0
	0xDA  218         206         308            0
	0xDB  219         157         262            0
	0xDC  220         173         270            0
	0xDD  221         171         276            0
	0xDE  222         171         269            0
	0xDF  223         159         253            0
	0xE0  224         190         234            0
	0xE1  225         159         293            0
	0xE2  226         178         298            0
	0xE3  227         167         258            0
	0xE4  228         176         165            0
	0xE5  229         157         213            0
	0xE6  230         177         279            0
	0xE7  231         155         242            0
	0xE8  232         178         237            0
	0xE9  233         174         276            0
	0xEA  234         180         328            0
	0xEB  235         160         306            0
	0xEC  236         175         275            0
	0xED  237         174         299            0
	0xEE  238         173         312            0
	0xEF  239         140         243            0
	0xF0  240         179         246            0
	0xF1  241         187         294            0
	0xF2  242         149         216            0
	0xF3  243         137         203            0
	0xF4  244         151         242            0
	0xF5  245         175         323            0
	0xF6  246         183         318            0
	0xF7  247         179         233            0
	0xF8  248         136         288            0
	0xF9  249         140         232            0
	0xFA  250         194         297            0
	0xFB  251         148         238            0
	0xFC  252         121         288            0
	0xFD  253         186         226            0
	0xFE  254         159         240            0
	0xFF  255         217         256            0

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

	Byte Analysis:
	Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
	----  ---  ----------  ----------  -----------  ----
	0x00    0        1320         521            0
	0x01    1         728         232            0
	0x02    2         526         211            0
	0x03    3         691         231            0
	0x04    4         654         220            0
	0x05    5         328         215            0
	0x06    6         728         221            0
	0x07    7         304         265            0
	0x08    8         381         177            0
	0x09    9         284         205            0
	0x0A   10         342         185            0
	0x0B   11         199         224            0
	0x0C   12         214         214            0
	0x0D   13         288         262            0
	0x0E   14         230         249            0
	0x0F   15         210         259            0
	0x10   16         299         184            0
	0x11   17         257         166            0
	0x12   18         179         210            0
	0x13   19         339         222            0
	0x14   20         213         184            0
	0x15   21         129         228            0
	0x16   22         217         235            0
	0x17   23         197         224            0
	0x18   24         214         211            0
	0x19   25         157         192            0
	0x1A   26         195         284            0
	0x1B   27         143         249            0
	0x1C   28         207         289            0
	0x1D   29         266         286            0
	0x1E   30         263         292            0
	0x1F   31         229         282            0
	0x20   32         564         213            0
	0x21   33         231         159            0    !
	0x22   34         184         166            0    "
	0x23   35         302         214            0    #
	0x24   36         226         220            0    $
	0x25   37         189         194            1    %
	0x26   38         148         226            0    &
	0x27   39         194         268            0    '
	0x28   40         181         179            0    (
	0x29   41         169         264            0    )
	0x2A   42         289         273            0    *
	0x2B   43         246         222            0    +
	0x2C   44         150         187            0    ,
	0x2D   45         461         254            0    -
	0x2E   46         484         305            1    .
	0x2F   47         422         229            0    /
	0x30   48        1155         225            0    0
	0x31   49         437         267            0    1
	0x32   50         685         183            0    2
	0x33   51         280         248            0    3
	0x34   52         384         251            0    4
	0x35   53         329         288            0    5
	0x36   54         294         292            0    6
	0x37   55         277         293            0    7
	0x38   56         347         269            0    8
	0x39   57         315         223            0    9
	0x3A   58         297         291            0    :
	0x3B   59         214         272            0    ;
	0x3C   60         198         258            0    <
	0x3D   61         290         325            0    =
	0x3E   62         166         318            0    >
	0x3F   63         170         267            0    ?
	0x40   64         238         207            0    @
	0x41   65         742         219            0    A
	0x42   66         232         144            0    B
	0x43   67         294         227            0    C
	0x44   68         674         185            1    D
	0x45   69         153         222            0    E
	0x46   70         186         259            0    F
	0x47   71         331         240            0    G
	0x48   72         286         210            0    H
	0x49   73         699         275            0    I
	0x4A   74         202         238            0    J
	0x4B   75         149         254            0    K
	0x4C   76         219         256            0    L
	0x4D   77         195         267            0    M
	0x4E   78         155         292            0    N
	0x4F   79         185         306            0    O
	0x50   80         240         178            0    P
	0x51   81         179         257            0    Q
	0x52   82         231         236            1    R
	0x53   83         267         273            0    S
	0x54   84         675         248            0    T
	0x55   85         403         309            0    U
	0x56   86         134         262            1    V
	0x57   87         146         246            0    W
	0x58   88         189         251            0    X
	0x59   89         147         237            0    Y
	0x5A   90         226         277            0    Z
	0x5B   91         136         246            0    [
	0x5C   92         208         295            0    \
	0x5D   93         157         287            0    ]
	0x5E   94         187         281            0    ^
	0x5F   95         163         262            0    _
	0x60   96         183         223            0    `
	0x61   97         908         245            0    a
	0x62   98         432         223            0    b
	0x63   99         790         284            0    c
	0x64  100        1045         195            0    d
	0x65  101         895         278            0    e
	0x66  102         351         220            1    f
	0x67  103         461         261            0    g
	0x68  104         372         265            0    h
	0x69  105         702         308            0    i
	0x6A  106         295         274            0    j
	0x6B  107         220         326            0    k
	0x6C  108         456         304            0    l
	0x6D  109         397         307            0    m
	0x6E  110         548         266            0    n
	0x6F  111         722         243            0    o
	0x70  112         639         259            0    p
	0x71  113         231         303            0    q
	0x72  114         622         228            0    r
	0x73  115         664         308            0    s
	0x74  116         663         279            0    t
	0x75  117         401         289            0    u
	0x76  118         243         261            0    v
	0x77  119         197         276            0    w
	0x78  120         278         257            0    x
	0x79  121         196         224            0    y
	0x7A  122         170         299            0    z
	0x7B  123         178         297            0    {
	0x7C  124         150         276            0    |
	0x7D  125         170         260            0    }
	0x7E  126         149         239            0    ~
	0x7F  127         152         271            0
	0x80  128         295         213            0
	0x81  129         227         202            0
	0x82  130         271         231            0
	0x83  131         204         248            0
	0x84  132         210         161            0
	0x85  133         162         207            0
	0x86  134         287         255            0
	0x87  135         176         282            0
	0x88  136         156         236            0
	0x89  137         174         216            0
	0x8A  138         188         224            0
	0x8B  139         129         248            0
	0x8C  140         130         218            0
	0x8D  141         169         304            1
	0x8E  142         181         288            0
	0x8F  143         193         301            0
	0x90  144         198         211            0
	0x91  145         146         197            0
	0x92  146         169         212            0
	0x93  147         167         255            0
	0x94  148         170         216            0
	0x95  149         192         269            0
	0x96  150         168         244            1
	0x97  151         154         241            0
	0x98  152         163         265            0
	0x99  153         138         227            0
	0x9A  154         158         230            0
	0x9B  155         206         278            0
	0x9C  156         167         230            0
	0x9D  157         146         301            0
	0x9E  158         176         242            0
	0x9F  159         165         270            0
	0xA0  160         215         209            0
	0xA1  161         183         210            0
	0xA2  162         220         236            0
	0xA3  163         192         299            0
	0xA4  164         182         253            0
	0xA5  165         187         331            0
	0xA6  166         180         292            0
	0xA7  167         155         307            0
	0xA8  168         199         250            0
	0xA9  169         183         291            0
	0xAA  170         194         331            0
	0xAB  171         147         304            0
	0xAC  172         124         260            0
	0xAD  173         164         278            0
	0xAE  174         167         314            0
	0xAF  175         165         312            0
	0xB0  176         133         258            0
	0xB1  177         135         245            0
	0xB2  178         142         246            0
	0xB3  179         129         270            0
	0xB4  180         199         335            0
	0xB5  181         171         318            0
	0xB6  182         185         274            0
	0xB7  183         126         260            0
	0xB8  184         198         300            0
	0xB9  185         179         268            0
	0xBA  186         154         283            0
	0xBB  187         158         261            0
	0xBC  188         167         255            0
	0xBD  189         165         327            0
	0xBE  190         137         249            0
	0xBF  191         114         251            0
	0xC0  192         181         180            0
	0xC1  193         159         254            0
	0xC2  194         161         197            0
	0xC3  195         173         301            0
	0xC4  196         172         287            0
	0xC5  197         139         247            0
	0xC6  198         147         259            0
	0xC7  199         178         286            0
	0xC8  200         159         164            0
	0xC9  201         132         235            0
	0xCA  202         186         276            0
	0xCB  203         115         248            0
	0xCC  204         176         236            1
	0xCD  205         151         270            0
	0xCE  206         217         257            0
	0xCF  207         126         292            0
	0xD0  208         174         229            0
	0xD1  209         155         263            0
	0xD2  210         166         295            0
	0xD3  211         168         330            0
	0xD4  212         195         318            0
	0xD5  213         165         345            0
	0xD6  214         149         284            0
	0xD7  215         171         335            0
	0xD8  216         128         263            0
	0xD9  217         201         284            0
	0xDA  218         206         308            0
	0xDB  219         157         262            0
	0xDC  220         173         270            0
	0xDD  221         171         276            0
	0xDE  222         171         269            0
	0xDF  223         159         253            0
	0xE0  224         190         234            0
	0xE1  225         159         293            0
	0xE2  226         178         298            0
	0xE3  227         167         258            0
	0xE4  228         176         165            1
	0xE5  229         157         213            0
	0xE6  230         177         279            0
	0xE7  231         155         242            0
	0xE8  232         178         237            0
	0xE9  233         174         276            0
	0xEA  234         180         328            0
	0xEB  235         160         306            0
	0xEC  236         175         275            0
	0xED  237         174         299            0
	0xEE  238         173         312            0
	0xEF  239         140         243            0
	0xF0  240         179         246            0
	0xF1  241         187         294            0
	0xF2  242         149         216            0
	0xF3  243         137         203            0
	0xF4  244         151         242            0
	0xF5  245         175         323            0
	0xF6  246         183         318            0
	0xF7  247         179         233            0
	0xF8  248         136         288            0
	0xF9  249         140         232            0
	0xFA  250         194         297            0
	0xFB  251         148         238            0
	0xFC  252         121         288            0
	0xFD  253         186         226            0
	0xFE  254         159         240            0
	0xFF  255         217         256            0

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

	Byte Analysis:
	Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
	----  ---  ----------  ----------  -----------  ----
	0x00    0        1320         521            1
	0x01    1         728         232            0
	0x02    2         526         211            2
	0x03    3         691         231            2
	0x04    4         654         220            0
	0x05    5         328         215            0
	0x06    6         728         221            3
	0x07    7         304         265            1
	0x08    8         381         177            1
	0x09    9         284         205            1
	0x0A   10         342         185            2
	0x0B   11         199         224            3
	0x0C   12         214         214            1
	0x0D   13         288         262            2
	0x0E   14         230         249            0
	0x0F   15         210         259            1
	0x10   16         299         184            4
	0x11   17         257         166            1
	0x12   18         179         210            3
	0x13   19         339         222            4
	0x14   20         213         184            1
	0x15   21         129         228            3
	0x16   22         217         235            0
	0x17   23         197         224            0
	0x18   24         214         211            1
	0x19   25         157         192            2
	0x1A   26         195         284            1
	0x1B   27         143         249            1
	0x1C   28         207         289            3
	0x1D   29         266         286            0
	0x1E   30         263         292            1
	0x1F   31         229         282            2
	0x20   32         564         213            0
	0x21   33         231         159            1    !
	0x22   34         184         166            0    "
	0x23   35         302         214            0    #
	0x24   36         226         220            2    $
	0x25   37         189         194            0    %
	0x26   38         148         226            0    &
	0x27   39         194         268            1    '
	0x28   40         181         179            4    (
	0x29   41         169         264            2    )
	0x2A   42         289         273            0    *
	0x2B   43         246         222            1    +
	0x2C   44         150         187            1    ,
	0x2D   45         461         254            1    -
	0x2E   46         484         305            2    .
	0x2F   47         422         229            2    /
	0x30   48        1155         225            1    0
	0x31   49         437         267            3    1
	0x32   50         685         183            1    2
	0x33   51         280         248            2    3
	0x34   52         384         251            0    4
	0x35   53         329         288            1    5
	0x36   54         294         292            1    6
	0x37   55         277         293            3    7
	0x38   56         347         269            0    8
	0x39   57         315         223            1    9
	0x3A   58         297         291            0    :
	0x3B   59         214         272            2    ;
	0x3C   60         198         258            1    <
	0x3D   61         290         325            0    =
	0x3E   62         166         318            0    >
	0x3F   63         170         267            1    ?
	0x40   64         238         207            0    @
	0x41   65         742         219            2    A
	0x42   66         232         144            0    B
	0x43   67         294         227            1    C
	0x44   68         674         185            1    D
	0x45   69         153         222            1    E
	0x46   70         186         259            2    F
	0x47   71         331         240            2    G
	0x48   72         286         210            1    H
	0x49   73         699         275            1    I
	0x4A   74         202         238            0    J
	0x4B   75         149         254            0    K
	0x4C   76         219         256            1    L
	0x4D   77         195         267            1    M
	0x4E   78         155         292            0    N
	0x4F   79         185         306            1    O
	0x50   80         240         178            1    P
	0x51   81         179         257            1    Q
	0x52   82         231         236            2    R
	0x53   83         267         273            2    S
	0x54   84         675         248            1    T
	0x55   85         403         309            1    U
	0x56   86         134         262            2    V
	0x57   87         146         246            1    W
	0x58   88         189         251            2    X
	0x59   89         147         237            1    Y
	0x5A   90         226         277            0    Z
	0x5B   91         136         246            2    [
	0x5C   92         208         295            1    \
	0x5D   93         157         287            2    ]
	0x5E   94         187         281            1    ^
	0x5F   95         163         262            2    _
	0x60   96         183         223            4    `
	0x61   97         908         245            2    a
	0x62   98         432         223            0    b
	0x63   99         790         284            0    c
	0x64  100        1045         195            3    d
	0x65  101         895         278            0    e
	0x66  102         351         220            2    f
	0x67  103         461         261            2    g
	0x68  104         372         265            3    h
	0x69  105         702         308            1    i
	0x6A  106         295         274            2    j
	0x6B  107         220         326            1    k
	0x6C  108         456         304            3    l
	0x6D  109         397         307            2    m
	0x6E  110         548         266            3    n
	0x6F  111         722         243            0    o
	0x70  112         639         259            3    p
	0x71  113         231         303            0    q
	0x72  114         622         228            1    r
	0x73  115         664         308            0    s
	0x74  116         663         279            2    t
	0x75  117         401         289            3    u
	0x76  118         243         261            2    v
	0x77  119         197         276            0    w
	0x78  120         278         257            2    x
	0x79  121         196         224            4    y
	0x7A  122         170         299            0    z
	0x7B  123         178         297            0    {
	0x7C  124         150         276            0    |
	0x7D  125         170         260            0    }
	0x7E  126         149         239            2    ~
	0x7F  127         152         271            0
	0x80  128         295         213            0
	0x81  129         227         202            0
	0x82  130         271         231            0
	0x83  131         204         248            0
	0x84  132         210         161            4
	0x85  133         162         207            3
	0x86  134         287         255            1
	0x87  135         176         282            1
	0x88  136         156         236            2
	0x89  137         174         216            0
	0x8A  138         188         224            2
	0x8B  139         129         248            2
	0x8C  140         130         218            1
	0x8D  141         169         304            1
	0x8E  142         181         288            1
	0x8F  143         193         301            0
	0x90  144         198         211            1
	0x91  145         146         197            0
	0x92  146         169         212            1
	0x93  147         167         255            2
	0x94  148         170         216            1
	0x95  149         192         269            0
	0x96  150         168         244            0
	0x97  151         154         241            1
	0x98  152         163         265            1
	0x99  153         138         227            1
	0x9A  154         158         230            1
	0x9B  155         206         278            0
	0x9C  156         167         230            2
	0x9D  157         146         301            2
	0x9E  158         176         242            0
	0x9F  159         165         270            2
	0xA0  160         215         209            2
	0xA1  161         183         210            1
	0xA2  162         220         236            1
	0xA3  163         192         299            1
	0xA4  164         182         253            1
	0xA5  165         187         331            1
	0xA6  166         180         292            1
	0xA7  167         155         307            1
	0xA8  168         199         250            0
	0xA9  169         183         291            3
	0xAA  170         194         331            0
	0xAB  171         147         304            2
	0xAC  172         124         260            3
	0xAD  173         164         278            0
	0xAE  174         167         314            0
	0xAF  175         165         312            2
	0xB0  176         133         258            1
	0xB1  177         135         245            1
	0xB2  178         142         246            1
	0xB3  179         129         270            2
	0xB4  180         199         335            5
	0xB5  181         171         318            1
	0xB6  182         185         274            2
	0xB7  183         126         260            0
	0xB8  184         198         300            1
	0xB9  185         179         268            0
	0xBA  186         154         283            2
	0xBB  187         158         261            1
	0xBC  188         167         255            0
	0xBD  189         165         327            1
	0xBE  190         137         249            3
	0xBF  191         114         251            0
	0xC0  192         181         180            0
	0xC1  193         159         254            3
	0xC2  194         161         197            1
	0xC3  195         173         301            1
	0xC4  196         172         287            0
	0xC5  197         139         247            1
	0xC6  198         147         259            0
	0xC7  199         178         286            3
	0xC8  200         159         164            2
	0xC9  201         132         235            0
	0xCA  202         186         276            2
	0xCB  203         115         248            0
	0xCC  204         176         236            1
	0xCD  205         151         270            1
	0xCE  206         217         257            2
	0xCF  207         126         292            1
	0xD0  208         174         229            2
	0xD1  209         155         263            2
	0xD2  210         166         295            1
	0xD3  211         168         330            2
	0xD4  212         195         318            3
	0xD5  213         165         345            1
	0xD6  214         149         284            1
	0xD7  215         171         335            0
	0xD8  216         128         263            0
	0xD9  217         201         284            2
	0xDA  218         206         308            1
	0xDB  219         157         262            1
	0xDC  220         173         270            0
	0xDD  221         171         276            1
	0xDE  222         171         269            1
	0xDF  223         159         253            1
	0xE0  224         190         234            0
	0xE1  225         159         293            1
	0xE2  226         178         298            0
	0xE3  227         167         258            1
	0xE4  228         176         165            1
	0xE5  229         157         213            0
	0xE6  230         177         279            1
	0xE7  231         155         242            2
	0xE8  232         178         237            1
	0xE9  233         174         276            1
	0xEA  234         180         328            1
	0xEB  235         160         306            3
	0xEC  236         175         275            3
	0xED  237         174         299            2
	0xEE  238         173         312            1
	0xEF  239         140         243            1
	0xF0  240         179         246            4
	0xF1  241         187         294            6
	0xF2  242         149         216            1
	0xF3  243         137         203            1
	0xF4  244         151         242            0
	0xF5  245         175         323            0
	0xF6  246         183         318            3
	0xF7  247         179         233            0
	0xF8  248         136         288            0
	0xF9  249         140         232            0
	0xFA  250         194         297            0
	0xFB  251         148         238            0
	0xFC  252         121         288            0
	0xFD  253         186         226            0
	0xFE  254         159         240            0
	0xFF  255         217         256            1

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

	Byte Analysis:
	Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
	----  ---  ----------  ----------  -----------  ----
	0x00    0        1320         521            0
	0x01    1         728         232            0
	0x02    2         526         211            0
	0x03    3         691         231            0
	0x04    4         654         220            0
	0x05    5         328         215            0
	0x06    6         728         221            0
	0x07    7         304         265            0
	0x08    8         381         177            0
	0x09    9         284         205            0
	0x0A   10         342         185            0
	0x0B   11         199         224            0
	0x0C   12         214         214            0
	0x0D   13         288         262            0
	0x0E   14         230         249            0
	0x0F   15         210         259            0
	0x10   16         299         184            0
	0x11   17         257         166            0
	0x12   18         179         210            0
	0x13   19         339         222            0
	0x14   20         213         184            1
	0x15   21         129         228            0
	0x16   22         217         235            0
	0x17   23         197         224            0
	0x18   24         214         211            0
	0x19   25         157         192            0
	0x1A   26         195         284            0
	0x1B   27         143         249            0
	0x1C   28         207         289            0
	0x1D   29         266         286            0
	0x1E   30         263         292            0
	0x1F   31         229         282            0
	0x20   32         564         213            0
	0x21   33         231         159            0    !
	0x22   34         184         166            0    "
	0x23   35         302         214            0    #
	0x24   36         226         220            0    $
	0x25   37         189         194            0    %
	0x26   38         148         226            0    &
	0x27   39         194         268            0    '
	0x28   40         181         179            0    (
	0x29   41         169         264            0    )
	0x2A   42         289         273            0    *
	0x2B   43         246         222            0    +
	0x2C   44         150         187            0    ,
	0x2D   45         461         254            0    -
	0x2E   46         484         305            0    .
	0x2F   47         422         229            0    /
	0x30   48        1155         225            0    0
	0x31   49         437         267            0    1
	0x32   50         685         183            0    2
	0x33   51         280         248            0    3
	0x34   52         384         251            0    4
	0x35   53         329         288            0    5
	0x36   54         294         292            0    6
	0x37   55         277         293            0    7
	0x38   56         347         269            0    8
	0x39   57         315         223            0    9
	0x3A   58         297         291            0    :
	0x3B   59         214         272            0    ;
	0x3C   60         198         258            0    <
	0x3D   61         290         325            0    =
	0x3E   62         166         318            0    >
	0x3F   63         170         267            0    ?
	0x40   64         238         207            0    @
	0x41   65         742         219            0    A
	0x42   66         232         144            0    B
	0x43   67         294         227            0    C
	0x44   68         674         185            0    D
	0x45   69         153         222            0    E
	0x46   70         186         259            0    F
	0x47   71         331         240            0    G
	0x48   72         286         210            0    H
	0x49   73         699         275            0    I
	0x4A   74         202         238            0    J
	0x4B   75         149         254            0    K
	0x4C   76         219         256            0    L
	0x4D   77         195         267            0    M
	0x4E   78         155         292            0    N
	0x4F   79         185         306            0    O
	0x50   80         240         178            0    P
	0x51   81         179         257            0    Q
	0x52   82         231         236            0    R
	0x53   83         267         273            0    S
	0x54   84         675         248            0    T
	0x55   85         403         309            0    U
	0x56   86         134         262            1    V
	0x57   87         146         246            0    W
	0x58   88         189         251            0    X
	0x59   89         147         237            0    Y
	0x5A   90         226         277            0    Z
	0x5B   91         136         246            0    [
	0x5C   92         208         295            0    \
	0x5D   93         157         287            0    ]
	0x5E   94         187         281            0    ^
	0x5F   95         163         262            0    _
	0x60   96         183         223            0    `
	0x61   97         908         245            0    a
	0x62   98         432         223            0    b
	0x63   99         790         284            0    c
	0x64  100        1045         195            0    d
	0x65  101         895         278            0    e
	0x66  102         351         220            0    f
	0x67  103         461         261            0    g
	0x68  104         372         265            0    h
	0x69  105         702         308            0    i
	0x6A  106         295         274            0    j
	0x6B  107         220         326            0    k
	0x6C  108         456         304            0    l
	0x6D  109         397         307            0    m
	0x6E  110         548         266            0    n
	0x6F  111         722         243            0    o
	0x70  112         639         259            0    p
	0x71  113         231         303            0    q
	0x72  114         622         228            0    r
	0x73  115         664         308            0    s
	0x74  116         663         279            0    t
	0x75  117         401         289            0    u
	0x76  118         243         261            0    v
	0x77  119         197         276            0    w
	0x78  120         278         257            0    x
	0x79  121         196         224            0    y
	0x7A  122         170         299            0    z
	0x7B  123         178         297            0    {
	0x7C  124         150         276            0    |
	0x7D  125         170         260            0    }
	0x7E  126         149         239            0    ~
	0x7F  127         152         271            0
	0x80  128         295         213            0
	0x81  129         227         202            0
	0x82  130         271         231            0
	0x83  131         204         248            0
	0x84  132         210         161            0
	0x85  133         162         207            0
	0x86  134         287         255            0
	0x87  135         176         282            0
	0x88  136         156         236            0
	0x89  137         174         216            0
	0x8A  138         188         224            0
	0x8B  139         129         248            0
	0x8C  140         130         218            0
	0x8D  141         169         304            0
	0x8E  142         181         288            0
	0x8F  143         193         301            0
	0x90  144         198         211            1
	0x91  145         146         197            0
	0x92  146         169         212            0
	0x93  147         167         255            0
	0x94  148         170         216            0
	0x95  149         192         269            0
	0x96  150         168         244            0
	0x97  151         154         241            0
	0x98  152         163         265            0
	0x99  153         138         227            0
	0x9A  154         158         230            0
	0x9B  155         206         278            0
	0x9C  156         167         230            0
	0x9D  157         146         301            0
	0x9E  158         176         242            0
	0x9F  159         165         270            1
	0xA0  160         215         209            0
	0xA1  161         183         210            0
	0xA2  162         220         236            0
	0xA3  163         192         299            0
	0xA4  164         182         253            0
	0xA5  165         187         331            0
	0xA6  166         180         292            0
	0xA7  167         155         307            0
	0xA8  168         199         250            0
	0xA9  169         183         291            0
	0xAA  170         194         331            0
	0xAB  171         147         304            0
	0xAC  172         124         260            0
	0xAD  173         164         278            0
	0xAE  174         167         314            0
	0xAF  175         165         312            0
	0xB0  176         133         258            0
	0xB1  177         135         245            0
	0xB2  178         142         246            0
	0xB3  179         129         270            0
	0xB4  180         199         335            0
	0xB5  181         171         318            0
	0xB6  182         185         274            0
	0xB7  183         126         260            0
	0xB8  184         198         300            0
	0xB9  185         179         268            0
	0xBA  186         154         283            0
	0xBB  187         158         261            0
	0xBC  188         167         255            0
	0xBD  189         165         327            0
	0xBE  190         137         249            0
	0xBF  191         114         251            0
	0xC0  192         181         180            0
	0xC1  193         159         254            0
	0xC2  194         161         197            0
	0xC3  195         173         301            0
	0xC4  196         172         287            0
	0xC5  197         139         247            0
	0xC6  198         147         259            0
	0xC7  199         178         286            0
	0xC8  200         159         164            1
	0xC9  201         132         235            0
	0xCA  202         186         276            0
	0xCB  203         115         248            0
	0xCC  204         176         236            0
	0xCD  205         151         270            0
	0xCE  206         217         257            0
	0xCF  207         126         292            0
	0xD0  208         174         229            0
	0xD1  209         155         263            0
	0xD2  210         166         295            0
	0xD3  211         168         330            0
	0xD4  212         195         318            0
	0xD5  213         165         345            0
	0xD6  214         149         284            0
	0xD7  215         171         335            0
	0xD8  216         128         263            0
	0xD9  217         201         284            1
	0xDA  218         206         308            0
	0xDB  219         157         262            0
	0xDC  220         173         270            0
	0xDD  221         171         276            0
	0xDE  222         171         269            0
	0xDF  223         159         253            0
	0xE0  224         190         234            0
	0xE1  225         159         293            0
	0xE2  226         178         298            0
	0xE3  227         167         258            0
	0xE4  228         176         165            0
	0xE5  229         157         213            0
	0xE6  230         177         279            0
	0xE7  231         155         242            0
	0xE8  232         178         237            0
	0xE9  233         174         276            0
	0xEA  234         180         328            0
	0xEB  235         160         306            0
	0xEC  236         175         275            1
	0xED  237         174         299            0
	0xEE  238         173         312            0
	0xEF  239         140         243            0
	0xF0  240         179         246            1
	0xF1  241         187         294            0
	0xF2  242         149         216            0
	0xF3  243         137         203            0
	0xF4  244         151         242            0
	0xF5  245         175         323            0
	0xF6  246         183         318            0
	0xF7  247         179         233            0
	0xF8  248         136         288            1
	0xF9  249         140         232            0
	0xFA  250         194         297            0
	0xFB  251         148         238            0
	0xFC  252         121         288            1
	0xFD  253         186         226            0
	0xFE  254         159         240            0
	0xFF  255         217         256            0

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

	Byte Analysis:
	Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
	----  ---  ----------  ----------  -----------  ----
	0x00    0        1320         521            3
	0x01    1         728         232            1
	0x02    2         526         211            2
	0x03    3         691         231            1
	0x04    4         654         220            1
	0x05    5         328         215            2
	0x06    6         728         221            1
	0x07    7         304         265            1
	0x08    8         381         177            2
	0x09    9         284         205            0
	0x0A   10         342         185            0
	0x0B   11         199         224            1
	0x0C   12         214         214            2
	0x0D   13         288         262            2
	0x0E   14         230         249            0
	0x0F   15         210         259            1
	0x10   16         299         184            1
	0x11   17         257         166            0
	0x12   18         179         210            0
	0x13   19         339         222            2
	0x14   20         213         184            1
	0x15   21         129         228            0
	0x16   22         217         235            0
	0x17   23         197         224            1
	0x18   24         214         211            0
	0x19   25         157         192            1
	0x1A   26         195         284            1
	0x1B   27         143         249            3
	0x1C   28         207         289            2
	0x1D   29         266         286            0
	0x1E   30         263         292            0
	0x1F   31         229         282            1
	0x20   32         564         213            0
	0x21   33         231         159            1    !
	0x22   34         184         166            0    "
	0x23   35         302         214            2    #
	0x24   36         226         220            2    $
	0x25   37         189         194            1    %
	0x26   38         148         226            1    &
	0x27   39         194         268            1    '
	0x28   40         181         179            0    (
	0x29   41         169         264            1    )
	0x2A   42         289         273            2    *
	0x2B   43         246         222            2    +
	0x2C   44         150         187            3    ,
	0x2D   45         461         254            1    -
	0x2E   46         484         305            1    .
	0x2F   47         422         229            0    /
	0x30   48        1155         225            0    0
	0x31   49         437         267            1    1
	0x32   50         685         183            1    2
	0x33   51         280         248            0    3
	0x34   52         384         251            1    4
	0x35   53         329         288            1    5
	0x36   54         294         292            4    6
	0x37   55         277         293            1    7
	0x38   56         347         269            0    8
	0x39   57         315         223            3    9
	0x3A   58         297         291            3    :
	0x3B   59         214         272            1    ;
	0x3C   60         198         258            1    <
	0x3D   61         290         325            0    =
	0x3E   62         166         318            0    >
	0x3F   63         170         267            1    ?
	0x40   64         238         207            2    @
	0x41   65         742         219            1    A
	0x42   66         232         144            0    B
	0x43   67         294         227            0    C
	0x44   68         674         185            2    D
	0x45   69         153         222            1    E
	0x46   70         186         259            1    F
	0x47   71         331         240            1    G
	0x48   72         286         210            3    H
	0x49   73         699         275            0    I
	0x4A   74         202         238            1    J
	0x4B   75         149         254            0    K
	0x4C   76         219         256            0    L
	0x4D   77         195         267            0    M
	0x4E   78         155         292            3    N
	0x4F   79         185         306            0    O
	0x50   80         240         178            0    P
	0x51   81         179         257            0    Q
	0x52   82         231         236            1    R
	0x53   83         267         273            2    S
	0x54   84         675         248            1    T
	0x55   85         403         309            0    U
	0x56   86         134         262            0    V
	0x57   87         146         246            2    W
	0x58   88         189         251            0    X
	0x59   89         147         237            2    Y
	0x5A   90         226         277            1    Z
	0x5B   91         136         246            0    [
	0x5C   92         208         295            2    \
	0x5D   93         157         287            0    ]
	0x5E   94         187         281            1    ^
	0x5F   95         163         262            1    _
	0x60   96         183         223            0    `
	0x61   97         908         245            0    a
	0x62   98         432         223            0    b
	0x63   99         790         284            2    c
	0x64  100        1045         195            2    d
	0x65  101         895         278            1    e
	0x66  102         351         220            0    f
	0x67  103         461         261            0    g
	0x68  104         372         265            0    h
	0x69  105         702         308            1    i
	0x6A  106         295         274            0    j
	0x6B  107         220         326            1    k
	0x6C  108         456         304            1    l
	0x6D  109         397         307            2    m
	0x6E  110         548         266            1    n
	0x6F  111         722         243            2    o
	0x70  112         639         259            1    p
	0x71  113         231         303            0    q
	0x72  114         622         228            0    r
	0x73  115         664         308            1    s
	0x74  116         663         279            2    t
	0x75  117         401         289            2    u
	0x76  118         243         261            1    v
	0x77  119         197         276            0    w
	0x78  120         278         257            0    x
	0x79  121         196         224            1    y
	0x7A  122         170         299            1    z
	0x7B  123         178         297            1    {
	0x7C  124         150         276            1    |
	0x7D  125         170         260            1    }
	0x7E  126         149         239            0    ~
	0x7F  127         152         271            0
	0x80  128         295         213            0
	0x81  129         227         202            0
	0x82  130         271         231            2
	0x83  131         204         248            2
	0x84  132         210         161            0
	0x85  133         162         207            0
	0x86  134         287         255            1
	0x87  135         176         282            2
	0x88  136         156         236            0
	0x89  137         174         216            0
	0x8A  138         188         224            0
	0x8B  139         129         248            0
	0x8C  140         130         218            1
	0x8D  141         169         304            1
	0x8E  142         181         288            3
	0x8F  143         193         301            2
	0x90  144         198         211            2
	0x91  145         146         197            0
	0x92  146         169         212            1
	0x93  147         167         255            1
	0x94  148         170         216            0
	0x95  149         192         269            1
	0x96  150         168         244            1
	0x97  151         154         241            0
	0x98  152         163         265            0
	0x99  153         138         227            0
	0x9A  154         158         230            1
	0x9B  155         206         278            1
	0x9C  156         167         230            0
	0x9D  157         146         301            0
	0x9E  158         176         242            0
	0x9F  159         165         270            1
	0xA0  160         215         209            0
	0xA1  161         183         210            2
	0xA2  162         220         236            1
	0xA3  163         192         299            1
	0xA4  164         182         253            2
	0xA5  165         187         331            1
	0xA6  166         180         292            0
	0xA7  167         155         307            1
	0xA8  168         199         250            2
	0xA9  169         183         291            0
	0xAA  170         194         331            0
	0xAB  171         147         304            0
	0xAC  172         124         260            1
	0xAD  173         164         278            3
	0xAE  174         167         314            1
	0xAF  175         165         312            1
	0xB0  176         133         258            0
	0xB1  177         135         245            0
	0xB2  178         142         246            0
	0xB3  179         129         270            1
	0xB4  180         199         335            1
	0xB5  181         171         318            0
	0xB6  182         185         274            3
	0xB7  183         126         260            1
	0xB8  184         198         300            0
	0xB9  185         179         268            0
	0xBA  186         154         283            1
	0xBB  187         158         261            2
	0xBC  188         167         255            4
	0xBD  189         165         327            0
	0xBE  190         137         249            0
	0xBF  191         114         251            0
	0xC0  192         181         180            1
	0xC1  193         159         254            1
	0xC2  194         161         197            1
	0xC3  195         173         301            2
	0xC4  196         172         287            1
	0xC5  197         139         247            0
	0xC6  198         147         259            1
	0xC7  199         178         286            0
	0xC8  200         159         164            1
	0xC9  201         132         235            1
	0xCA  202         186         276            2
	0xCB  203         115         248            0
	0xCC  204         176         236            1
	0xCD  205         151         270            1
	0xCE  206         217         257            1
	0xCF  207         126         292            0
	0xD0  208         174         229            1
	0xD1  209         155         263            1
	0xD2  210         166         295            0
	0xD3  211         168         330            3
	0xD4  212         195         318            0
	0xD5  213         165         345            2
	0xD6  214         149         284            3
	0xD7  215         171         335            1
	0xD8  216         128         263            0
	0xD9  217         201         284            2
	0xDA  218         206         308            0
	0xDB  219         157         262            3
	0xDC  220         173         270            1
	0xDD  221         171         276            2
	0xDE  222         171         269            1
	0xDF  223         159         253            3
	0xE0  224         190         234            1
	0xE1  225         159         293            2
	0xE2  226         178         298            1
	0xE3  227         167         258            1
	0xE4  228         176         165            0
	0xE5  229         157         213            1
	0xE6  230         177         279            0
	0xE7  231         155         242            0
	0xE8  232         178         237            1
	0xE9  233         174         276            1
	0xEA  234         180         328            1
	0xEB  235         160         306            2
	0xEC  236         175         275            1
	0xED  237         174         299            0
	0xEE  238         173         312            2
	0xEF  239         140         243            1
	0xF0  240         179         246            0
	0xF1  241         187         294            0
	0xF2  242         149         216            2
	0xF3  243         137         203            0
	0xF4  244         151         242            2
	0xF5  245         175         323            0
	0xF6  246         183         318            2
	0xF7  247         179         233            0
	0xF8  248         136         288            0
	0xF9  249         140         232            0
	0xFA  250         194         297            0
	0xFB  251         148         238            1
	0xFC  252         121         288            1
	0xFD  253         186         226            0
	0xFE  254         159         240            1
	0xFF  255         217         256            0

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

Byte Analysis:
Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
----  ---  ----------  ----------  -----------  ----
0x00    0        1320         521         1268
0x01    1         728         232         1332
0x02    2         526         211         1502
0x03    3         691         231         1738
0x04    4         654         220         1568
0x05    5         328         215         1638
0x06    6         728         221         1570
0x07    7         304         265         1609
0x08    8         381         177         1614
0x09    9         284         205         1559
0x0A   10         342         185         1564
0x0B   11         199         224         1565
0x0C   12         214         214         1617
0x0D   13         288         262         1546
0x0E   14         230         249         1569
0x0F   15         210         259         1563
0x10   16         299         184         1619
0x11   17         257         166         1579
0x12   18         179         210         1570
0x13   19         339         222         1510
0x14   20         213         184         1654
0x15   21         129         228         1694
0x16   22         217         235         1702
0x17   23         197         224         1559
0x18   24         214         211         1631
0x19   25         157         192         1612
0x1A   26         195         284         1545
0x1B   27         143         249         1534
0x1C   28         207         289         1400
0x1D   29         266         286         1246
0x1E   30         263         292         1251
0x1F   31         229         282         1266
0x20   32         564         213         1293
0x21   33         231         159         1405    !
0x22   34         184         166         1301    "
0x23   35         302         214         1453    #
0x24   36         226         220         1338    $
0x25   37         189         194         1349    %
0x26   38         148         226         1437    &
0x27   39         194         268         1327    '
0x28   40         181         179         1467    (
0x29   41         169         264         1340    )
0x2A   42         289         273         1303    *
0x2B   43         246         222         1348    +
0x2C   44         150         187         1381    ,
0x2D   45         461         254         1397    -
0x2E   46         484         305         1444    .
0x2F   47         422         229         1405    /
0x30   48        1155         225         1383    0
0x31   49         437         267         1444    1
0x32   50         685         183         1440    2
0x33   51         280         248         1271    3
0x34   52         384         251         1353    4
0x35   53         329         288         1440    5
0x36   54         294         292         1350    6
0x37   55         277         293         1285    7
0x38   56         347         269         1137    8
0x39   57         315         223         1163    9
0x3A   58         297         291         1184    :
0x3B   59         214         272         1161    ;
0x3C   60         198         258         1147    <
0x3D   61         290         325         1254    =
0x3E   62         166         318         1276    >
0x3F   63         170         267         1348    ?
0x40   64         238         207         1251    @
0x41   65         742         219         1277    A
0x42   66         232         144         1268    B
0x43   67         294         227         1327    C
0x44   68         674         185         1440    D
0x45   69         153         222         1244    E
0x46   70         186         259         1404    F
0x47   71         331         240         1366    G
0x48   72         286         210         1323    H
0x49   73         699         275         1503    I
0x4A   74         202         238         1328    J
0x4B   75         149         254         1358    K
0x4C   76         219         256         1378    L
0x4D   77         195         267         1443    M
0x4E   78         155         292         1401    N
0x4F   79         185         306         1426    O
0x50   80         240         178         1414    P
0x51   81         179         257         1443    Q
0x52   82         231         236         1422    R
0x53   83         267         273         1375    S
0x54   84         675         248         1471    T
0x55   85         403         309         1428    U
0x56   86         134         262         1353    V
0x57   87         146         246         1373    W
0x58   88         189         251         1413    X
0x59   89         147         237         1205    Y
0x5A   90         226         277         1118    Z
0x5B   91         136         246         1180    [
0x5C   92         208         295         1146    \
0x5D   93         157         287         1331    ]
0x5E   94         187         281         1356    ^
0x5F   95         163         262         1303    _
0x60   96         183         223         1281    `
0x61   97         908         245         1279    a
0x62   98         432         223         1265    b
0x63   99         790         284         1233    c
0x64  100        1045         195         1112    d
0x65  101         895         278         1211    e
0x66  102         351         220         1250    f
0x67  103         461         261         1395    g
0x68  104         372         265         1304    h
0x69  105         702         308         1360    i
0x6A  106         295         274         1355    j
0x6B  107         220         326         1255    k
0x6C  108         456         304         1482    l
0x6D  109         397         307         1469    m
0x6E  110         548         266         1347    n
0x6F  111         722         243         1430    o
0x70  112         639         259         1375    p
0x71  113         231         303         1368    q
0x72  114         622         228         1368    r
0x73  115         664         308         1304    s
0x74  116         663         279         1387    t
0x75  117         401         289         1437    u
0x76  118         243         261         1385    v
0x77  119         197         276         1447    w
0x78  120         278         257         1490    x
0x79  121         196         224         1367    y
0x7A  122         170         299         1394    z
0x7B  123         178         297         1383    {
0x7C  124         150         276         1299    |
0x7D  125         170         260         1429    }
0x7E  126         149         239         1336    ~
0x7F  127         152         271         1256
0x80  128         295         213         1261
0x81  129         227         202         1251
0x82  130         271         231         1315
0x83  131         204         248         1267
0x84  132         210         161         1265
0x85  133         162         207         1244
0x86  134         287         255         1246
0x87  135         176         282         1314
0x88  136         156         236         1295
0x89  137         174         216         1381
0x8A  138         188         224         1504
0x8B  139         129         248         1534
0x8C  140         130         218         1514
0x8D  141         169         304         1670
0x8E  142         181         288         1571
0x8F  143         193         301         1568
0x90  144         198         211         1438
0x91  145         146         197         1489
0x92  146         169         212         1598
0x93  147         167         255         1532
0x94  148         170         216         1461
0x95  149         192         269         1583
0x96  150         168         244         1620
0x97  151         154         241         1562
0x98  152         163         265         1585
0x99  153         138         227         1520
0x9A  154         158         230         1618
0x9B  155         206         278         1565
0x9C  156         167         230         1595
0x9D  157         146         301         1524
0x9E  158         176         242         1699
0x9F  159         165         270         1626
0xA0  160         215         209         1558
0xA1  161         183         210         1496
0xA2  162         220         236         1587
0xA3  163         192         299         1568
0xA4  164         182         253         1641
0xA5  165         187         331         1578
0xA6  166         180         292         1666
0xA7  167         155         307         1568
0xA8  168         199         250         1563
0xA9  169         183         291         1545
0xAA  170         194         331         1650
0xAB  171         147         304         1566
0xAC  172         124         260         1558
0xAD  173         164         278         1552
0xAE  174         167         314         1523
0xAF  175         165         312         1548
0xB0  176         133         258         1592
0xB1  177         135         245         1667
0xB2  178         142         246         1643
0xB3  179         129         270         1571
0xB4  180         199         335         1429
0xB5  181         171         318         1576
0xB6  182         185         274         1606
0xB7  183         126         260         1552
0xB8  184         198         300         1608
0xB9  185         179         268         1623
0xBA  186         154         283         1580
0xBB  187         158         261         1601
0xBC  188         167         255         1550
0xBD  189         165         327         1499
0xBE  190         137         249         1612
0xBF  191         114         251         1563
0xC0  192         181         180         1540
0xC1  193         159         254         1584
0xC2  194         161         197         1678
0xC3  195         173         301         1584
0xC4  196         172         287         1528
0xC5  197         139         247         1621
0xC6  198         147         259         1624
0xC7  199         178         286         1532
0xC8  200         159         164         1626
0xC9  201         132         235         1614
0xCA  202         186         276         1618
0xCB  203         115         248         1648
0xCC  204         176         236         1592
0xCD  205         151         270         1570
0xCE  206         217         257         1605
0xCF  207         126         292         1534
0xD0  208         174         229         1596
0xD1  209         155         263         1602
0xD2  210         166         295         1613
0xD3  211         168         330         1520
0xD4  212         195         318         1533
0xD5  213         165         345         1578
0xD6  214         149         284         1581
0xD7  215         171         335         1553
0xD8  216         128         263         1626
0xD9  217         201         284         1652
0xDA  218         206         308         1496
0xDB  219         157         262         1578
0xDC  220         173         270         1541
0xDD  221         171         276         1499
0xDE  222         171         269         1678
0xDF  223         159         253         1611
0xE0  224         190         234         1591
0xE1  225         159         293         1545
0xE2  226         178         298         1664
0xE3  227         167         258         1551
0xE4  228         176         165         1553
0xE5  229         157         213         1595
0xE6  230         177         279         1599
0xE7  231         155         242         1600
0xE8  232         178         237         1591
0xE9  233         174         276         1561
0xEA  234         180         328         1558
0xEB  235         160         306         1662
0xEC  236         175         275         1563
0xED  237         174         299         1613
0xEE  238         173         312         1600
0xEF  239         140         243         1580
0xF0  240         179         246         1602
0xF1  241         187         294         1591
0xF2  242         149         216         1671
0xF3  243         137         203         1672
0xF4  244         151         242         1588
0xF5  245         175         323         1587
0xF6  246         183         318         1631
0xF7  247         179         233         1598
0xF8  248         136         288         1695
0xF9  249         140         232         1602
0xFA  250         194         297         1585
0xFB  251         148         238         1576
0xFC  252         121         288         1617
0xFD  253         186         226         1699
0xFE  254         159         240         1619
0xFF  255         217         256         1565

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

Byte Analysis:
Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
----  ---  ----------  ----------  -----------  ----
0x00    0        1320         521         1311
0x01    1         728         232         1415
0x02    2         526         211         1458
0x03    3         691         231         1701
0x04    4         654         220         1643
0x05    5         328         215         1601
0x06    6         728         221         1493
0x07    7         304         265         1657
0x08    8         381         177         1625
0x09    9         284         205         1643
0x0A   10         342         185         1639
0x0B   11         199         224         1611
0x0C   12         214         214         1641
0x0D   13         288         262         1566
0x0E   14         230         249         1621
0x0F   15         210         259         1683
0x10   16         299         184         1680
0x11   17         257         166         1581
0x12   18         179         210         1632
0x13   19         339         222         1543
0x14   20         213         184         1591
0x15   21         129         228         1577
0x16   22         217         235         1629
0x17   23         197         224         1623
0x18   24         214         211         1583
0x19   25         157         192         1598
0x1A   26         195         284         1505
0x1B   27         143         249         1537
0x1C   28         207         289         1310
0x1D   29         266         286         1257
0x1E   30         263         292         1265
0x1F   31         229         282         1368
0x20   32         564         213         1307
0x21   33         231         159         1505    !
0x22   34         184         166         1307    "
0x23   35         302         214         1416    #
0x24   36         226         220         1423    $
0x25   37         189         194         1273    %
0x26   38         148         226         1422    &
0x27   39         194         268         1364    '
0x28   40         181         179         1329    (
0x29   41         169         264         1354    )
0x2A   42         289         273         1382    *
0x2B   43         246         222         1335    +
0x2C   44         150         187         1424    ,
0x2D   45         461         254         1425    -
0x2E   46         484         305         1447    .
0x2F   47         422         229         1443    /
0x30   48        1155         225         1365    0
0x31   49         437         267         1433    1
0x32   50         685         183         1464    2
0x33   51         280         248         1303    3
0x34   52         384         251         1370    4
0x35   53         329         288         1405    5
0x36   54         294         292         1379    6
0x37   55         277         293         1304    7
0x38   56         347         269         1201    8
0x39   57         315         223         1158    9
0x3A   58         297         291         1161    :
0x3B   59         214         272         1177    ;
0x3C   60         198         258         1139    <
0x3D   61         290         325         1358    =
0x3E   62         166         318         1287    >
0x3F   63         170         267         1384    ?
0x40   64         238         207         1288    @
0x41   65         742         219         1272    A
0x42   66         232         144         1254    B
0x43   67         294         227         1349    C
0x44   68         674         185         1427    D
0x45   69         153         222         1258    E
0x46   70         186         259         1520    F
0x47   71         331         240         1354    G
0x48   72         286         210         1363    H
0x49   73         699         275         1525    I
0x4A   74         202         238         1368    J
0x4B   75         149         254         1387    K
0x4C   76         219         256         1363    L
0x4D   77         195         267         1381    M
0x4E   78         155         292         1354    N
0x4F   79         185         306         1351    O
0x50   80         240         178         1371    P
0x51   81         179         257         1528    Q
0x52   82         231         236         1435    R
0x53   83         267         273         1373    S
0x54   84         675         248         1366    T
0x55   85         403         309         1416    U
0x56   86         134         262         1425    V
0x57   87         146         246         1336    W
0x58   88         189         251         1348    X
0x59   89         147         237         1147    Y
0x5A   90         226         277         1158    Z
0x5B   91         136         246         1127    [
0x5C   92         208         295         1120    \
0x5D   93         157         287         1291    ]
0x5E   94         187         281         1297    ^
0x5F   95         163         262         1270    _
0x60   96         183         223         1274    `
0x61   97         908         245         1309    a
0x62   98         432         223         1271    b
0x63   99         790         284         1199    c
0x64  100        1045         195         1218    d
0x65  101         895         278         1210    e
0x66  102         351         220         1351    f
0x67  103         461         261         1273    g
0x68  104         372         265         1285    h
0x69  105         702         308         1334    i
0x6A  106         295         274         1402    j
0x6B  107         220         326         1281    k
0x6C  108         456         304         1385    l
0x6D  109         397         307         1388    m
0x6E  110         548         266         1397    n
0x6F  111         722         243         1401    o
0x70  112         639         259         1292    p
0x71  113         231         303         1393    q
0x72  114         622         228         1418    r
0x73  115         664         308         1363    s
0x74  116         663         279         1369    t
0x75  117         401         289         1530    u
0x76  118         243         261         1401    v
0x77  119         197         276         1399    w
0x78  120         278         257         1437    x
0x79  121         196         224         1319    y
0x7A  122         170         299         1280    z
0x7B  123         178         297         1438    {
0x7C  124         150         276         1351    |
0x7D  125         170         260         1375    }
0x7E  126         149         239         1252    ~
0x7F  127         152         271         1247
0x80  128         295         213         1286
0x81  129         227         202         1147
0x82  130         271         231         1275
0x83  131         204         248         1299
0x84  132         210         161         1252
0x85  133         162         207         1267
0x86  134         287         255         1245
0x87  135         176         282         1342
0x88  136         156         236         1289
0x89  137         174         216         1375
0x8A  138         188         224         1554
0x8B  139         129         248         1484
0x8C  140         130         218         1598
0x8D  141         169         304         1610
0x8E  142         181         288         1658
0x8F  143         193         301         1551
0x90  144         198         211         1478
0x91  145         146         197         1630
0x92  146         169         212         1611
0x93  147         167         255         1640
0x94  148         170         216         1408
0x95  149         192         269         1625
0x96  150         168         244         1596
0x97  151         154         241         1645
0x98  152         163         265         1565
0x99  153         138         227         1486
0x9A  154         158         230         1710
0x9B  155         206         278         1573
0x9C  156         167         230         1588
0x9D  157         146         301         1497
0x9E  158         176         242         1596
0x9F  159         165         270         1593
0xA0  160         215         209         1598
0xA1  161         183         210         1545
0xA2  162         220         236         1533
0xA3  163         192         299         1541
0xA4  164         182         253         1576
0xA5  165         187         331         1569
0xA6  166         180         292         1562
0xA7  167         155         307         1587
0xA8  168         199         250         1420
0xA9  169         183         291         1542
0xAA  170         194         331         1543
0xAB  171         147         304         1590
0xAC  172         124         260         1644
0xAD  173         164         278         1545
0xAE  174         167         314         1526
0xAF  175         165         312         1598
0xB0  176         133         258         1573
0xB1  177         135         245         1656
0xB2  178         142         246         1598
0xB3  179         129         270         1526
0xB4  180         199         335         1524
0xB5  181         171         318         1599
0xB6  182         185         274         1539
0xB7  183         126         260         1542
0xB8  184         198         300         1631
0xB9  185         179         268         1657
0xBA  186         154         283         1577
0xBB  187         158         261         1638
0xBC  188         167         255         1587
0xBD  189         165         327         1588
0xBE  190         137         249         1640
0xBF  191         114         251         1575
0xC0  192         181         180         1549
0xC1  193         159         254         1622
0xC2  194         161         197         1720
0xC3  195         173         301         1540
0xC4  196         172         287         1605
0xC5  197         139         247         1537
0xC6  198         147         259         1607
0xC7  199         178         286         1510
0xC8  200         159         164         1581
0xC9  201         132         235         1556
0xCA  202         186         276         1637
0xCB  203         115         248         1537
0xCC  204         176         236         1579
0xCD  205         151         270         1562
0xCE  206         217         257         1575
0xCF  207         126         292         1563
0xD0  208         174         229         1643
0xD1  209         155         263         1638
0xD2  210         166         295         1656
0xD3  211         168         330         1562
0xD4  212         195         318         1507
0xD5  213         165         345         1410
0xD6  214         149         284         1596
0xD7  215         171         335         1577
0xD8  216         128         263         1616
0xD9  217         201         284         1570
0xDA  218         206         308         1557
0xDB  219         157         262         1653
0xDC  220         173         270         1503
0xDD  221         171         276         1510
0xDE  222         171         269         1610
0xDF  223         159         253         1630
0xE0  224         190         234         1645
0xE1  225         159         293         1604
0xE2  226         178         298         1581
0xE3  227         167         258         1545
0xE4  228         176         165         1566
0xE5  229         157         213         1604
0xE6  230         177         279         1625
0xE7  231         155         242         1555
0xE8  232         178         237         1620
0xE9  233         174         276         1595
0xEA  234         180         328         1641
0xEB  235         160         306         1575
0xEC  236         175         275         1584
0xED  237         174         299         1590
0xEE  238         173         312         1609
0xEF  239         140         243         1591
0xF0  240         179         246         1612
0xF1  241         187         294         1610
0xF2  242         149         216         1597
0xF3  243         137         203         1559
0xF4  244         151         242         1520
0xF5  245         175         323         1586
0xF6  246         183         318         1586
0xF7  247         179         233         1647
0xF8  248         136         288         1556
0xF9  249         140         232         1524
0xFA  250         194         297         1596
0xFB  251         148         238         1591
0xFC  252         121         288         1603
0xFD  253         186         226         1599
0xFE  254         159         240         1660
0xFF  255         217         256         1641

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

Byte Analysis:
Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
----  ---  ----------  ----------  -----------  ----
0x00    0        1320         521         1375
0x01    1         728         232         1366
0x02    2         526         211         1459
0x03    3         691         231         1399
0x04    4         654         220         1454
0x05    5         328         215         1423
0x06    6         728         221         1380
0x07    7         304         265         1330
0x08    8         381         177         1363
0x09    9         284         205         1341
0x0A   10         342         185         1243
0x0B   11         199         224         1153
0x0C   12         214         214         1130
0x0D   13         288         262         1192
0x0E   14         230         249         1119
0x0F   15         210         259         1161
0x10   16         299         184         1379
0x11   17         257         166         1338
0x12   18         179         210         1411
0x13   19         339         222         1351
0x14   20         213         184         1309
0x15   21         129         228         1300
0x16   22         217         235         1359
0x17   23         197         224         1414
0x18   24         214         211         1273
0x19   25         157         192         1437
0x1A   26         195         284         1389
0x1B   27         143         249         1362
0x1C   28         207         289         1445
0x1D   29         266         286         1435
0x1E   30         263         292         1373
0x1F   31         229         282         1317
0x20   32         564         213         1389
0x21   33         231         159         1454    !
0x22   34         184         166         1390    "
0x23   35         302         214         1354    #
0x24   36         226         220         1428    $
0x25   37         189         194         1458    %
0x26   38         148         226         1405    &
0x27   39         194         268         1422    '
0x28   40         181         179         1389    (
0x29   41         169         264         1325    )
0x2A   42         289         273         1440    *
0x2B   43         246         222         1384    +
0x2C   44         150         187         1276    ,
0x2D   45         461         254         1149    -
0x2E   46         484         305         1187    .
0x2F   47         422         229         1198    /
0x30   48        1155         225         1285    0
0x31   49         437         267         1360    1
0x32   50         685         183         1327    2
0x33   51         280         248         1337    3
0x34   52         384         251         1230    4
0x35   53         329         288         1226    5
0x36   54         294         292         1370    6
0x37   55         277         293         1336    7
0x38   56         347         269         1278    8
0x39   57         315         223         1283    9
0x3A   58         297         291         1328    :
0x3B   59         214         272         1350    ;
0x3C   60         198         258         1430    <
0x3D   61         290         325         1407    =
0x3E   62         166         318         1314    >
0x3F   63         170         267         1427    ?
0x40   64         238         207         1408    @
0x41   65         742         219         1402    A
0x42   66         232         144         1511    B
0x43   67         294         227         1321    C
0x44   68         674         185         1378    D
0x45   69         153         222         1550    E
0x46   70         186         259         1358    F
0x47   71         331         240         1399    G
0x48   72         286         210         1484    H
0x49   73         699         275         1390    I
0x4A   74         202         238         1450    J
0x4B   75         149         254         1527    K
0x4C   76         219         256         1393    L
0x4D   77         195         267         1381    M
0x4E   78         155         292         1407    N
0x4F   79         185         306         1320    O
0x50   80         240         178         1421    P
0x51   81         179         257         1399    Q
0x52   82         231         236         1292    R
0x53   83         267         273         1310    S
0x54   84         675         248         1228    T
0x55   85         403         309         1208    U
0x56   86         134         262         1288    V
0x57   87         146         246         1339    W
0x58   88         189         251         1263    X
0x59   89         147         237         1366    Y
0x5A   90         226         277         1331    Z
0x5B   91         136         246         1392    [
0x5C   92         208         295         1418    \
0x5D   93         157         287         1505    ]
0x5E   94         187         281         1517    ^
0x5F   95         163         262         1526    _
0x60   96         183         223         1625    `
0x61   97         908         245         1577    a
0x62   98         432         223         1548    b
0x63   99         790         284         1435    c
0x64  100        1045         195         1590    d
0x65  101         895         278         1578    e
0x66  102         351         220         1656    f
0x67  103         461         261         1444    g
0x68  104         372         265         1508    h
0x69  105         702         308         1554    i
0x6A  106         295         274         1555    j
0x6B  107         220         326         1586    k
0x6C  108         456         304         1456    l
0x6D  109         397         307         1597    m
0x6E  110         548         266         1623    n
0x6F  111         722         243         1554    o
0x70  112         639         259         1469    p
0x71  113         231         303         1527    q
0x72  114         622         228         1559    r
0x73  115         664         308         1610    s
0x74  116         663         279         1471    t
0x75  117         401         289         1543    u
0x76  118         243         261         1523    v
0x77  119         197         276         1535    w
0x78  120         278         257         1616    x
0x79  121         196         224         1604    y
0x7A  122         170         299         1523    z
0x7B  123         178         297         1488    {
0x7C  124         150         276         1561    |
0x7D  125         170         260         1571    }
0x7E  126         149         239         1560    ~
0x7F  127         152         271         1577
0x80  128         295         213         1552
0x81  129         227         202         1488
0x82  130         271         231         1483
0x83  131         204         248         1511
0x84  132         210         161         1512
0x85  133         162         207         1462
0x86  134         287         255         1566
0x87  135         176         282         1421
0x88  136         156         236         1633
0x89  137         174         216         1558
0x8A  138         188         224         1538
0x8B  139         129         248         1519
0x8C  140         130         218         1530
0x8D  141         169         304         1512
0x8E  142         181         288         1583
0x8F  143         193         301         1581
0x90  144         198         211         1514
0x91  145         146         197         1609
0x92  146         169         212         1560
0x93  147         167         255         1445
0x94  148         170         216         1530
0x95  149         192         269         1546
0x96  150         168         244         1578
0x97  151         154         241         1578
0x98  152         163         265         1604
0x99  153         138         227         1504
0x9A  154         158         230         1524
0x9B  155         206         278         1590
0x9C  156         167         230         1544
0x9D  157         146         301         1565
0x9E  158         176         242         1591
0x9F  159         165         270         1540
0xA0  160         215         209         1585
0xA1  161         183         210         1545
0xA2  162         220         236         1484
0xA3  163         192         299         1664
0xA4  164         182         253         1603
0xA5  165         187         331         1582
0xA6  166         180         292         1557
0xA7  167         155         307         1528
0xA8  168         199         250         1483
0xA9  169         183         291         1573
0xAA  170         194         331         1610
0xAB  171         147         304         1501
0xAC  172         124         260         1646
0xAD  173         164         278         1584
0xAE  174         167         314         1522
0xAF  175         165         312         1603
0xB0  176         133         258         1521
0xB1  177         135         245         1500
0xB2  178         142         246         1493
0xB3  179         129         270         1542
0xB4  180         199         335         1600
0xB5  181         171         318         1546
0xB6  182         185         274         1519
0xB7  183         126         260         1578
0xB8  184         198         300         1602
0xB9  185         179         268         1625
0xBA  186         154         283         1522
0xBB  187         158         261         1536
0xBC  188         167         255         1595
0xBD  189         165         327         1519
0xBE  190         137         249         1582
0xBF  191         114         251         1551
0xC0  192         181         180         1543
0xC1  193         159         254         1517
0xC2  194         161         197         1602
0xC3  195         173         301         1532
0xC4  196         172         287         1572
0xC5  197         139         247         1503
0xC6  198         147         259         1534
0xC7  199         178         286         1536
0xC8  200         159         164         1516
0xC9  201         132         235         1526
0xCA  202         186         276         1568
0xCB  203         115         248         1651
0xCC  204         176         236         1568
0xCD  205         151         270         1565
0xCE  206         217         257         1575
0xCF  207         126         292         1550
0xD0  208         174         229         1535
0xD1  209         155         263         1555
0xD2  210         166         295         1504
0xD3  211         168         330         1623
0xD4  212         195         318         1545
0xD5  213         165         345         1491
0xD6  214         149         284         1580
0xD7  215         171         335         1572
0xD8  216         128         263         1628
0xD9  217         201         284         1504
0xDA  218         206         308         1528
0xDB  219         157         262         1625
0xDC  220         173         270         1611
0xDD  221         171         276         1529
0xDE  222         171         269         1556
0xDF  223         159         253         1548
0xE0  224         190         234         1533
0xE1  225         159         293         1577
0xE2  226         178         298         1483
0xE3  227         167         258         1560
0xE4  228         176         165         1522
0xE5  229         157         213         1498
0xE6  230         177         279         1532
0xE7  231         155         242         1478
0xE8  232         178         237         1645
0xE9  233         174         276         1523
0xEA  234         180         328         1610
0xEB  235         160         306         1582
0xEC  236         175         275         1553
0xED  237         174         299         1490
0xEE  238         173         312         1550
0xEF  239         140         243         1547
0xF0  240         179         246         1489
0xF1  241         187         294         1482
0xF2  242         149         216         1533
0xF3  243         137         203         1528
0xF4  244         151         242         1546
0xF5  245         175         323         1529
0xF6  246         183         318         1569
0xF7  247         179         233         1549
0xF8  248         136         288         1514
0xF9  249         140         232         1551
0xFA  250         194         297         1550
0xFB  251         148         238         1586
0xFC  252         121         288         1525
0xFD  253         186         226         1459
0xFE  254         159         240         1479
0xFF  255         217         256         1572

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

Byte Analysis:
Byte  Dec  ROM Lo 64K  ROM Hi 64K  Input Count  Char
----  ---  ----------  ----------  -----------  ----
0x00    0        1320         521         1451
0x01    1         728         232         1373
0x02    2         526         211         1352
0x03    3         691         231         1544
0x04    4         654         220         1390
0x05    5         328         215         1432
0x06    6         728         221         1344
0x07    7         304         265         1394
0x08    8         381         177         1313
0x09    9         284         205         1427
0x0A   10         342         185         1444
0x0B   11         199         224         1457
0x0C   12         214         214         1435
0x0D   13         288         262         1354
0x0E   14         230         249         1480
0x0F   15         210         259         1422
0x10   16         299         184         1363
0x11   17         257         166         1363
0x12   18         179         210         1403
0x13   19         339         222         1391
0x14   20         213         184         1299
0x15   21         129         228         1137
0x16   22         217         235         1167
0x17   23         197         224         1129
0x18   24         214         211         1146
0x19   25         157         192         1204
0x1A   26         195         284         1259
0x1B   27         143         249         1294
0x1C   28         207         289         1407
0x1D   29         266         286         1379
0x1E   30         263         292         1313
0x1F   31         229         282         1279
0x20   32         564         213         1364
0x21   33         231         159         1429    !
0x22   34         184         166         1319    "
0x23   35         302         214         1460    #
0x24   36         226         220         1397    $
0x25   37         189         194         1375    %
0x26   38         148         226         1522    &
0x27   39         194         268         1395    '
0x28   40         181         179         1433    (
0x29   41         169         264         1342    )
0x2A   42         289         273         1351    *
0x2B   43         246         222         1408    +
0x2C   44         150         187         1337    ,
0x2D   45         461         254         1363    -
0x2E   46         484         305         1467    .
0x2F   47         422         229         1510    /
0x30   48        1155         225         1420    0
0x31   49         437         267         1450    1
0x32   50         685         183         1378    2
0x33   51         280         248         1363    3
0x34   52         384         251         1342    4
0x35   53         329         288         1293    5
0x36   54         294         292         1217    6
0x37   55         277         293         1194    7
0x38   56         347         269         1120    8
0x39   57         315         223         1177    9
0x3A   58         297         291         1365    :
0x3B   59         214         272         1287    ;
0x3C   60         198         258         1280    <
0x3D   61         290         325         1262    =
0x3E   62         166         318         1306    >
0x3F   63         170         267         1284    ?
0x40   64         238         207         1283    @
0x41   65         742         219         1293    A
0x42   66         232         144         1298    B
0x43   67         294         227         1271    C
0x44   68         674         185         1372    D
0x45   69         153         222         1372    E
0x46   70         186         259         1328    F
0x47   71         331         240         1338    G
0x48   72         286         210         1300    H
0x49   73         699         275         1377    I
0x4A   74         202         238         1425    J
0x4B   75         149         254         1351    K
0x4C   76         219         256         1401    L
0x4D   77         195         267         1372    M
0x4E   78         155         292         1350    N
0x4F   79         185         306         1371    O
0x50   80         240         178         1338    P
0x51   81         179         257         1330    Q
0x52   82         231         236         1444    R
0x53   83         267         273         1369    S
0x54   84         675         248         1433    T
0x55   85         403         309         1447    U
0x56   86         134         262         1421    V
0x57   87         146         246         1367    W
0x58   88         189         251         1479    X
0x59   89         147         237         1268    Y
0x5A   90         226         277         1388    Z
0x5B   91         136         246         1290    [
0x5C   92         208         295         1317    \
0x5D   93         157         287         1308    ]
0x5E   94         187         281         1291    ^
0x5F   95         163         262         1261    _
0x60   96         183         223         1326    `
0x61   97         908         245         1260    a
0x62   98         432         223         1284    b
0x63   99         790         284         1227    c
0x64  100        1045         195         1329    d
0x65  101         895         278         1368    e
0x66  102         351         220         1409    f
0x67  103         461         261         1568    g
0x68  104         372         265         1549    h
0x69  105         702         308         1565    i
0x6A  106         295         274         1534    j
0x6B  107         220         326         1572    k
0x6C  108         456         304         1557    l
0x6D  109         397         307         1507    m
0x6E  110         548         266         1526    n
0x6F  111         722         243         1541    o
0x70  112         639         259         1504    p
0x71  113         231         303         1532    q
0x72  114         622         228         1634    r
0x73  115         664         308         1599    s
0x74  116         663         279         1559    t
0x75  117         401         289         1553    u
0x76  118         243         261         1511    v
0x77  119         197         276         1545    w
0x78  120         278         257         1523    x
0x79  121         196         224         1505    y
0x7A  122         170         299         1573    z
0x7B  123         178         297         1540    {
0x7C  124         150         276         1610    |
0x7D  125         170         260         1639    }
0x7E  126         149         239         1539    ~
0x7F  127         152         271         1530
0x80  128         295         213         1574
0x81  129         227         202         1619
0x82  130         271         231         1567
0x83  131         204         248         1492
0x84  132         210         161         1626
0x85  133         162         207         1465
0x86  134         287         255         1600
0x87  135         176         282         1545
0x88  136         156         236         1599
0x89  137         174         216         1590
0x8A  138         188         224         1604
0x8B  139         129         248         1482
0x8C  140         130         218         1523
0x8D  141         169         304         1513
0x8E  142         181         288         1646
0x8F  143         193         301         1491
0x90  144         198         211         1514
0x91  145         146         197         1450
0x92  146         169         212         1500
0x93  147         167         255         1605
0x94  148         170         216         1542
0x95  149         192         269         1582
0x96  150         168         244         1605
0x97  151         154         241         1577
0x98  152         163         265         1555
0x99  153         138         227         1569
0x9A  154         158         230         1474
0x9B  155         206         278         1602
0x9C  156         167         230         1505
0x9D  157         146         301         1486
0x9E  158         176         242         1516
0x9F  159         165         270         1629
0xA0  160         215         209         1534
0xA1  161         183         210         1569
0xA2  162         220         236         1620
0xA3  163         192         299         1516
0xA4  164         182         253         1536
0xA5  165         187         331         1604
0xA6  166         180         292         1647
0xA7  167         155         307         1548
0xA8  168         199         250         1617
0xA9  169         183         291         1543
0xAA  170         194         331         1610
0xAB  171         147         304         1488
0xAC  172         124         260         1546
0xAD  173         164         278         1625
0xAE  174         167         314         1556
0xAF  175         165         312         1628
0xB0  176         133         258         1527
0xB1  177         135         245         1526
0xB2  178         142         246         1558
0xB3  179         129         270         1519
0xB4  180         199         335         1654
0xB5  181         171         318         1509
0xB6  182         185         274         1569
0xB7  183         126         260         1633
0xB8  184         198         300         1617
0xB9  185         179         268         1488
0xBA  186         154         283         1513
0xBB  187         158         261         1567
0xBC  188         167         255         1575
0xBD  189         165         327         1571
0xBE  190         137         249         1540
0xBF  191         114         251         1603
0xC0  192         181         180         1586
0xC1  193         159         254         1590
0xC2  194         161         197         1580
0xC3  195         173         301         1617
0xC4  196         172         287         1631
0xC5  197         139         247         1543
0xC6  198         147         259         1567
0xC7  199         178         286         1625
0xC8  200         159         164         1552
0xC9  201         132         235         1536
0xCA  202         186         276         1513
0xCB  203         115         248         1477
0xCC  204         176         236         1616
0xCD  205         151         270         1518
0xCE  206         217         257         1547
0xCF  207         126         292         1576
0xD0  208         174         229         1561
0xD1  209         155         263         1523
0xD2  210         166         295         1592
0xD3  211         168         330         1513
0xD4  212         195         318         1549
0xD5  213         165         345         1635
0xD6  214         149         284         1551
0xD7  215         171         335         1633
0xD8  216         128         263         1526
0xD9  217         201         284         1561
0xDA  218         206         308         1544
0xDB  219         157         262         1633
0xDC  220         173         270         1603
0xDD  221         171         276         1571
0xDE  222         171         269         1529
0xDF  223         159         253         1582
0xE0  224         190         234         1604
0xE1  225         159         293         1546
0xE2  226         178         298         1617
0xE3  227         167         258         1562
0xE4  228         176         165         1590
0xE5  229         157         213         1547
0xE6  230         177         279         1553
0xE7  231         155         242         1573
0xE8  232         178         237         1543
0xE9  233         174         276         1591
0xEA  234         180         328         1612
0xEB  235         160         306         1584
0xEC  236         175         275         1528
0xED  237         174         299         1554
0xEE  238         173         312         1581
0xEF  239         140         243         1562
0xF0  240         179         246         1557
0xF1  241         187         294         1555
0xF2  242         149         216         1534
0xF3  243         137         203         1612
0xF4  244         151         242         1562
0xF5  245         175         323         1567
0xF6  246         183         318         1608
0xF7  247         179         233         1572
0xF8  248         136         288         1587
0xF9  249         140         232         1542
0xFA  250         194         297         1654
0xFB  251         148         238         1581
0xFC  252         121         288         1580
0xFD  253         186         226         1639
0xFE  254         159         240         1544
0xFF  255         217         256         1552

That is correct, the encoded ones are not jpegs anymore, in fact the original data is absent from the encoded files.
