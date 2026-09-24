@echo off
REM ZOSCII Web Radio Serve - test launcher
REM Runs in this window. Only one line runs at a time - REM the main one and un-REM a test to switch.
cd /d %~dp0

REM --- main: Cyborg Unicorn, one session per listener, loops at end of queue ---
zwrserve -i 8000 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -p 5

REM --- other channels ---
REM zwrserve -i 8001 https://cyborgunicorn.com.au/radio/indexmq.php Marketing -z logo.png -p 5
REM zwrserve -i 8002 https://cyborgunicorn.com.au/radio/indexmq.php "ZOSCII Tech" -z logo.png -p 5

REM --- test: shared radio, everyone hears the same thing (any URL on 8010) ---
REM zwrserve -i 8010 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -s -p 5

REM --- test: live mode, silence at end of queue until a new track is published ---
REM zwrserve -i 8011 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -w -p 5

REM --- test: live mode with elevator music while waiting (elevator.mp3 must match the queue's sample rate and channels) ---
REM zwrserve -i 8012 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -w -e elevator.mp3 -p 5

REM --- test: shared live radio with elevator ---
REM zwrserve -i 8013 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z logo.png -s -w -e elevator.mp3 -p 5

REM --- test: wrong key, every message should log "skip ... (not MP3)" ---
REM zwrserve -i 8014 https://cyborgunicorn.com.au/radio/indexmq.php "Cyborg Unicorn" -z zwrserve.exe -p 5