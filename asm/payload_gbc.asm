INCLUDE "hardware.inc"

SECTION "SRAM Buffers", SRAM[$A000]
; 20*18 tiles, 16 bytes per tile, 5760 bytes total
sVRAMBuffer:
	ds 20 * 18 * 16

SECTION "Payload", ROM0
Payload:

LOAD "Write Main Payload", WRAM0[$C000]
WriteMainPayload:
	; 25 bytes large
	di
	ld a,$03 ; switch to wram bank 3 (unused in Crystal)
	ldh [rSVBK],a
	xor a
	ld hl,$D000
	ld c,a
	ldh [c],a
.loop
	ldh a,[c] ; 2
	swap a ; 2
	ld d,a ; 1
	ldh a,[c] ; 2
	xor a,d ; 1
	ld [hl+],a ; 2
	bit 5,h ; 2
	jr z,.loop ; 3
	jp MainPayload
ENDL

MACRO ADJUST_VOL ; 10 m-cycles (20 dots), assumes BC = $0F00. Final m-cycle actually changes volume
	ldh a,[c] ; 2
	and a,b ; 1
	ld d,a ; 1
	swap a ; 2
	or a,d ; 1
	ldh [rNR50],a ; 3
ENDM

MACRO LOAD_SAMPLE ; 11 m-cycles (22 dots), assumes C = 0
	ldh a,[c] ; 2
	swap a ; 2
	ld d,a ; 1
	ldh a,[c] ; 2
	xor a,d ; 1
	ldh [$FF30],a ; 3
ENDM

MACRO JOYPAD_TO_HLI ; 10 m-cycles (20 dots), assumes C = 0, HL = Next VRAM Buffer
	ldh a,[c] ; 2
	swap a ; 2
	ld d,a ; 1
	ldh a,[c] ; 2
	xor a,d ; 1
	ld [hl+],a ; 2
ENDM

MACRO JOYPAD_FIRST_HALF_TO_E ; 5 m-cycles (10 dots), assumes C = 0, E won't be trashed before JOYPAD_E_TO_HLI
	ldh a,[c] ; 2
	swap a ; 2
	ld e,a ; 1
ENDM

MACRO JOYPAD_E_TO_HLI ; 5 m-cycles (10 dots), assumes C = 0, E = first joypad half, HL = Next VRAM Buffer
	ldh a,[c] ; 2
	xor a,e ; 1
	ld [hl+],a ; 2
ENDM

; a scanline is 456 dots

; unused
MACRO DO_UNROLLED_SCANLINE
	; each unrolled scanline writes 16 bytes from joypad

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	; start split joypad read
	JOYPAD_FIRST_HALF_TO_E ; 5
	nop ; 1

	ADJUST_VOL ; 10

	; complete split joypad read
	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	; start split joypad read
	JOYPAD_FIRST_HALF_TO_E ; 5
	nop ; 1

	ADJUST_VOL ; 10

	; complete split joypad read
	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

ENDM

MACRO DO_UNROLLED_SCANLINE_PREP_LCDC
	; each unrolled scanline writes 15 bytes from joypad

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

	ld a,\1 ; 2
	ldh [rLCDC],a ; 3
	nop ; 1

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	; start split joypad read
	JOYPAD_FIRST_HALF_TO_E ; 5
	nop ; 1

	ADJUST_VOL ; 10

	; complete split joypad read
	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

ENDM

MACRO DO_LOOPED_SCANLINE
	; each looped scanline writes 15 bytes from joypad

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	; start split joypad read
	JOYPAD_FIRST_HALF_TO_E ; 5
	nop ; 1

	ADJUST_VOL ; 10

	; complete split joypad read
	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR
	ldh a,[rLY] ; 3
	ld e,a ; 1

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ld a,e ; 1
	cp a,\1 ; 2
	; later jump is 4 m-cycles

ENDM

MACRO DO_LOOPED_SCANLINE_FINAL

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 6 ; 6 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	; exit condition, press any input at end
	ld e,$CF ; 2

REPT 4 ; 4 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ldh a,[c] ; 2
	cp a,e ; 1
	jp z,VideoStart ; 3/4 (usually 4, 3 is at video end where timing doesn't matter)
ENDM

MACRO DO_UNROLLED_SCANLINE_PREP_GDMA

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ld a,HIGH(sVRAMBuffer) ; 2
	ldh [rHDMA1],a ; 3
	nop ; 1

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	xor a ; 1
	ldh [rHDMA2],a ; 3
REPT 3 ; 3 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

REPT 3 ; 3 joypads, 10 * 3 (30) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ld a,HIGH(_VRAM8000) ; 2
	ldh [rHDMA3],a ; 3
	nop ; 1

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	xor a ; 1
	ldh [rHDMA4],a ; 3
	; also reset HL
	; GDMA will happen before the next joypad read, so this is fine
	ld hl,sVRAMBuffer ; 3

ENDM

; unused
MACRO DO_UNROLLED_SCANLINE_GDMA
	; each unrolled scanline with GDMA writes 10 bytes from joypad

	ADJUST_VOL ; 10

	; have to DMA once now
	; we can't do 2 at once and still loop
	; and mode 2 won't last long enough if it's done after the sample load
	; sample load timing luckily doesn't matter much here
	xor a ; 1
	ldh [rHDMA5],a ; 3

	; halted for 1+16 m-cycles (there's some 1 m-cycle warmup period?)
	nop ; 1

	LOAD_SAMPLE ; 11
	JOYPAD_TO_HLI ; 10

REPT 4 ; 4 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

	; should be safe actually to do this before loading the sample
	; but rather not risk it
	xor a ; 1
	ldh [rHDMA5],a ; 3

	; halted for 1+16 m-cycles (there's some 1 m-cycle warmup period?)
	nop ; 1

	JOYPAD_TO_HLI ; 10

REPT 4 ; 4 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

ENDM

MACRO DO_LOOPED_SCANLINE_GDMA
	; each unrolled scanline with GDMA writes 10 bytes from joypad

	ADJUST_VOL ; 10

	; have to DMA once now
	; we can't do 2 at once and still loop
	; and mode 2 won't last long enough if it's done after the sample load
	; sample load timing luckily doesn't matter much here
	xor a ; 1
	ldh [rHDMA5],a ; 3

	; halted for 1+16 m-cycles (there's some 1 m-cycle warmup period?)
	nop ; 1

	LOAD_SAMPLE ; 11
	JOYPAD_TO_HLI ; 10

REPT 4 ; 4 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ADJUST_VOL ; 10
	LOAD_SAMPLE ; 11

	; should be safe actually to do this before loading the sample
	; but rather not risk it
	xor a ; 1
	ldh [rHDMA5],a ; 3

	; halted for 1+16 m-cycles (there's some 1 m-cycle warmup period?)
	nop ; 1

	JOYPAD_TO_HLI ; 10

	ldh a,[rLY] ; 3
	ld e,a ; 1

	ADJUST_VOL ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	ld a,e ; 1
	cp a,\1 ; 2
	; later jump is 4 m-cycles

ENDM

LOAD "Main Payload", WRAM0[$D000]
MainPayload:
	; switch to double speed mode
	ld a,$30
	ldh [c],a
	xor a
	ldh [rIE],a
	inc a
	ldh [rKEY1],a
	stop

	; disable LCD while VRAM etc is prepped
	ldh [rLCDC],a

	; map and clear vram buffer in sram
	ld a,$0A
	ld [$0000],a
	xor a
	ld [$4000],a
	ld hl,sVRAMBuffer
	ld bc,(20 * 18 * 16)
.clear_vram_buffer_loop
	xor a
	ld [hl+],a
	dec bc
	ld a,b
	or c
	jr nz,.clear_vram_buffer_loop

	; clear vram
	ld a,$01
	ldh [rVBK],a
	xor a
	ld hl,$8000
.clear_vram_loop_bank1
	ld [hl+],a
	bit 5,h
	jr z,.clear_vram_loop_bank1
	ldh [rVBK],a
	ld hl,$8000
.clear_vram_loop_bank0
	ld [hl+],a
	bit 5,h
	jr z,.clear_vram_loop_bank0

	; clear OAM
	ld e,$A0
	ld hl,$FE00
.clear_oam_loop
	ld [hl+],a
	dec e
	jr nz,.clear_oam_loop

	; set tilemap
	xor a
	ld bc,$000C
	ld de,$1214
	ld hl,$9800
.vram_tilemap_loop
	ld [hl+],a
	inc a
	dec e
	jr nz,.vram_tilemap_loop
	ld e,$14
	add hl,bc
	dec d
	jr nz,.vram_tilemap_loop

	; set palettes
	ld a,$80
	ldh [rBGPI],a

	; pal 00 ($0000)
	ld a,$00
	ldh [rBGPD],a
	ld a,$00
	ldh [rBGPD],a
	; pal 01 ($294A)
	ld a,$4A
	ldh [rBGPD],a
	ld a,$29
	ldh [rBGPD],a
	; pal 10 ($56B5)
	ld a,$B5
	ldh [rBGPD],a
	ld a,$56
	ldh [rBGPD],a
	; pal 11 ($7FFF)
	ld a,$FF
	ldh [rBGPD],a
	ld a,$7F
	ldh [rBGPD],a

	; clear scroll
	xor a
	ldh [rSCX],a
	ldh [rSCY],a

	; reopen joypad port
	xor a
	ldh [rP1],a

	; setup audio
	xor a
	ldh [rNR52],a

	; clear wave ram
	ld e,$10
	ld hl,$FF30
	ld a,$88
.clear_wave_ram_loop
	ld [hl+],a
	dec e
	jr nz,.clear_wave_ram_loop

	ld a,$80
	ldh [rNR52],a

	ld a,$44
	ldh [rNR51],a

	ld a,$00
	ldh [rNR50],a

	ld a,$80
	ldh [rNR30],a

	ld a,AUD3LEVEL_100
	ldh [rNR32],a

	ld a,0xC7
	ldh [rNR33],a

	ld a,0x87
	ldh [rNR34],a

	; 57+3 cycles until next wave sample read, audio must be aligned so this happens when first volume is adjusted

	ld hl,sVRAMBuffer ; 3
	ld bc,$0F00 ; 3

REPT 39 ; 39 nops, perfectly aligns timing
	nop ; 1
ENDR

	; enable LCD and "start" the video
	ld a,LCDCF_ON ; 2
	ldh [rLCDC],a ; 3

VideoStart:
	; ld bc,$0F00 must be present (constants)
	; d is trashed usually, e is secondary for trashing
	; hl = current sVRAMBuffer position

; Write to sVRAMBuffer
Frame0:
	; before rendering, swap to $8000 BG tile addressing
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK01)

.ly1
	DO_LOOPED_SCANLINE (72-1)
	jp nz,.ly1 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 72, swap to the other BG tile addressing
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK21)

.ly73
	DO_LOOPED_SCANLINE 0 ; LY153 reads out as 0, so this exits when LY actually equals 0
	jp nz,.ly73 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

; Write to sVRAMBuffer + start GDMAs
Frame1:
	; before rendering, swap to $8000 BG tile addressing
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK01)

.ly1
	DO_LOOPED_SCANLINE (7-1)
	jp nz,.ly1 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 7, prep VRAM DMA regs and reset HL (joypad reads are slower than GDMAs, and a GDMA below happens before any joypad reads)
	DO_UNROLLED_SCANLINE_PREP_GDMA

	; LY 8, we can start looping now
.ly8
	DO_LOOPED_SCANLINE_GDMA (72-1)
	jp nz,.ly8 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 72, swap to the other BG tile data
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK21)

.ly73
	DO_LOOPED_SCANLINE_GDMA 0 ; LY153 reads out as 0, so this exits when LY actually equals 0
	jp nz,.ly73 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

; Finish GDMAing sVRAMBuffer over, start writing to sVRAMBuffer
Frame2:
	; before rendering, swap to $8000 BG tile addressing
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK01)

.ly1
	DO_LOOPED_SCANLINE_GDMA (36-1)
	jp nz,.ly1 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; we're done GDMA'ing, continue on normal path
.ly36
	DO_LOOPED_SCANLINE (72-1)
	jp nz,.ly36 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 72, swap to the other BG tile data
	DO_UNROLLED_SCANLINE_PREP_LCDC (LCDCF_ON|LCDCF_BLK21)

.ly73
	DO_LOOPED_SCANLINE (153-1)
	jp nz,.ly73 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 153, the final scanline, must check if we can exit and jump back to the beginning if not
	DO_LOOPED_SCANLINE_FINAL

VideoEnd:
	xor a
	ldh [rLCDC],a

	ld hl,EndPayload
	ld de,WriteMainPayload
	ld c,EndPayloadEnd-EndPayload
.copy_end_payload_loop
	ld a,[hl+]
	ld [de],a
	inc e
	dec c
	jr nz,.copy_end_payload_loop

	; set variables for game completion
	ld a,$FF
	ld [$C2C7],a
	ld a,$5E
	ld [$C0F0],a
	ld a,$76
	ld [$C0EF],a
	ld sp,$C0DF

	; switch back to single speed mode
	ld a,$30
	ldh [c],a
	ld a,$01
	ldh [rKEY1],a
	stop
	; set IE back to its original value
	ld a,$0F
	ldh [rIE],a
	; proper LCDC, game should handle the rest
	ld a,$E3
	ldh [rLCDC],a
	xor a
	jp WriteMainPayload

	; copied over WriteMainPayload
EndPayload:
	ldh [rSVBK],a ; go back to bank 1
	; set bank 1 variables for game completion
	ld a,$49
	ld [$D4E9],a
	ld a,$06
	ld [$D1BE],a
	ld a,$09
	ld [$D1BD],a
	ld a,$4C
	ld [$D1B6],a
	ld a,$03
	ld [$D1B5],a
	ld a,$43
	ld [$DB5E],a
	xor a
	ld [$DCD7],a
	ldh [rIF],a ; clear whatever pending interrupts
	reti
EndPayloadEnd:

ENDL
