#!/bin/sh
mkdir -p ../output
rgbasm -p 0xFF -Wall -Wextra -o payload.o payload_gbc.asm
rgblink -p 0xFF -w -t -m ../output/bad_apple.map -n ../output/bad_apple.sym -o ../output/bad_apple.gbc payload.o
