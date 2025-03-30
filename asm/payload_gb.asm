INCLUDE "hardware.inc"

SECTION "RB Joy Input", HRAM[$FFF8]
hRBJoyInput: db
hRGDisableJoypadPolling: db

; 5 free bytes in HRAM in RB
SECTION "HRAM Variables", HRAM[$FFFA]
hTempVol: db

SECTION "SRAM Buffers", SRAM[$A000]
; 16*144 tiles, 16 bytes per scanline, 2304 bytes total
sBGPBuffer:
	ds 16 * 144
; 2377 bytes get read, 73 more than really needed
sBGPBufferPadding:
	ds 73

SECTION "Payload", ROM0
Payload:

LOAD "Initial Payload", WRAM0[$D301]
InitialPayload:
	; maximum of 15 bytes large
	; not much, but enough to write a better payload to write the main payload
	di
	ld hl,WriteMainPayloadEnd-1
.loop
	call $015F ; ReadJoypad
	ldh a,[hRBJoyInput]
	ld [hl-],a
	bit 4,h
	jr nz,.loop
	jp hl ; jumps to $CFFF, which is just a nop
ENDL

LOAD "Write Main Payload", WRAM0[$D000]
WriteMainPayload:
	ld a,$0A
	ld [$0000],a ; enable sram
	xor a
	ld [$4000],a ; switch to sram bank 0
	ld hl,MainPayloadEnd-1
	ld c,a
	ldh [c],a
.loop
	ldh a,[c] ; 2
	swap a ; 2
	ld b,a ; 1
	ldh a,[c] ; 2
	xor a,b ; 1
	ld [hl-],a ; 2
	bit 5,h ; 2
	jr nz,.loop ; 3
	jp MainPayload
WriteMainPayloadEnd:
ENDL

MACRO STORE_VOL ; 10 m-cycles (40 dots), assumes C = $00, L = $47.
	ldh a,[c] ; 2
	xor a,l ; 1
	ld b,a ; 1
	swap a ; 2
	or a,b ; 1
	ldh [hTempVol],a ; 3
ENDM

MACRO ADJUST_VOL_DRAW ; 10 m-cycles (40 dots), assumes C = $00, L = $47. Final m-cycle actually changes volume
	ldh a,[c] ; 2
	xor a,l ; 1
	ld b,a ; 1
	swap a ; 2
	or a,b ; 1
	ldh [rNR50],a ; 3
ENDM

MACRO ADJUST_VOL_NONDRAW ; 10 m-cycles (40 dots), assumes C = $00, D = $0F. Final m-cycle actually changes volume
	ldh a,[c] ; 2
	and a,d ; 1
	ld b,a ; 1
	swap a ; 2
	or a,b ; 1
	ldh [rNR50],a ; 3
ENDM

MACRO LOAD_SAMPLE ; 11 m-cycles (44 dots), assumes C = 0
	ldh a,[c] ; 2
	swap a ; 2
	ld b,a ; 1
	ldh a,[c] ; 2
	xor a,b ; 1
	ldh [$FF30],a ; 3
ENDM

MACRO JOYPAD_TO_HLI ; 10 m-cycles (40 dots), assumes C = 0, HL = Next BGP Buffer
	ldh a,[c] ; 2
	swap a ; 2
	ld b,a ; 1
	ldh a,[c] ; 2
	xor a,b ; 1
	ld [hl+],a ; 2
ENDM

MACRO JOYPAD_FIRST_HALF_TO_E ; 5 m-cycles (20 dots), assumes C = 0, E won't be trashed before JOYPAD_E_TO_HLI
	ldh a,[c] ; 2
	swap a ; 2
	ld e,a ; 1
ENDM

MACRO JOYPAD_E_TO_HLI ; 5 m-cycles (20 dots), assumes C = 0, E = first joypad half, HL = Next BGP Buffer
	ldh a,[c] ; 2
	xor a,e ; 1
	ld [hl+],a ; 2
ENDM

; a scanline is 456 dots (114 m-cycles)

; argument is OAM section
MACRO DO_UNROLLED_SCANLINE_DRAW

	pop de ; 3
	ld [hl],e ; 2

	STORE_VOL ; 10
	ADJUST_VOL_DRAW ; 10

	; rendering begins here
	ld [hl],d ; 2
	; 8 pixels drawn with E palette
	; next 12 pixels drawn with D palette
REPT 7 ; 49 m-cycles (7 * 7)
	pop de ; 3
	ld [hl],e ; 2
	ld [hl],d ; 2
ENDR

	ldh a,[hTempVol] ; 3
	ldh [rNR50],a ; 3

	; write to OAM first so the last write won't be blocked (all writes should be safely in HBlank)
	ldh a,[rLY] ; 3
	add a,OAM_Y_OFS+1 ; 2
	ld [(_OAMRAM + OAMA_Y) + (0 + \1 * 4) * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + (1 + \1 * 4) * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + (2 + \1 * 4) * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + (3 + \1 * 4) * sizeof_OAM_ATTRS],a ; 4

	LOAD_SAMPLE ; 11

ENDM

MACRO DO_LOOPED_SCANLINE_DRAW

	pop de ; 3
	ld [hl],e ; 2

	STORE_VOL ; 10
	ADJUST_VOL_DRAW ; 10

	; rendering begins here
	ld [hl],d ; 2
	; 8 pixels drawn with E palette
	; next 12 pixels drawn with D palette
REPT 7 ; 49 m-cycles (7 * 7)
	pop de ; 3
	ld [hl],e ; 2
	ld [hl],d ; 2
ENDR

	ldh a,[hTempVol] ; 3
	ldh [rNR50],a ; 3

	LOAD_SAMPLE ; 11

	ldh a,[rLY] ; 3
	add a,OAM_Y_OFS+1 ; 2
	ld [(_OAMRAM + OAMA_Y) + (8 * sizeof_OAM_ATTRS)],a ; 4

	sub a,OAM_Y_OFS+1 ; 2
	cp a,\1 ; 2
	; later jump is 4 m-cycles

REPT 4 ; 4 nops
	nop ; 1
ENDR

ENDM

MACRO DO_UNROLLED_SCANLINE_PREP_NONDRAW

	ld hl,sBGPBuffer ; 3
	ld d,$0F ; 2 / constant for ADJUST_VOL_NONDRAW

	JOYPAD_TO_HLI ; 10

	ADJUST_VOL_NONDRAW ; 10

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10
	LOAD_SAMPLE ; 11

REPT 2 ; 2 joypads, 10 * 2 (20) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	nop ; 1

ENDM

MACRO DO_UNROLLED_SCANLINE_NONDRAW

	JOYPAD_TO_HLI ; 10

	JOYPAD_FIRST_HALF_TO_E ; 5

	ADJUST_VOL_NONDRAW ; 10

	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10
	LOAD_SAMPLE ; 11

REPT 2 ; 2 joypads, 10 * 2 (20) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

	nop ; 1

ENDM

MACRO DO_LOOPED_SCANLINE_NONDRAW

	JOYPAD_TO_HLI ; 10

	JOYPAD_FIRST_HALF_TO_E ; 5

	ADJUST_VOL_NONDRAW ; 10

	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10
	LOAD_SAMPLE ; 11

	JOYPAD_TO_HLI ; 10

	; later jump is 4 m-cycles
	ldh a,[rLY] ; 3
	cp a,\1 ; 2

	nop ; 1
	nop ; 1

ENDM

MACRO DO_UNROLLED_SCANLINE_TOGGLE_LCDC

	JOYPAD_TO_HLI ; 10

	JOYPAD_FIRST_HALF_TO_E ; 5

	ADJUST_VOL_NONDRAW ; 10

	JOYPAD_E_TO_HLI ; 5

REPT 4 ; 4 joypads, 10 * 4 (40) m-cycles
	JOYPAD_TO_HLI ; 10
ENDR

REPT 2 ; 2 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10
	LOAD_SAMPLE ; 11

	JOYPAD_TO_HLI ; 10

	ld a,LCDCF_OFF ; 2
	ldh [rLCDC],a ; 3

	nop ; 1

	ld a,LCDCF_ON|LCDCF_OBJON|LCDCF_BGON|LCDCF_BG9800|LCDCF_BLK01 ; 2
	ldh [rLCDC],a ; 3

ENDM

MACRO DO_LOOPED_SCANLINE_FINAL

REPT 15 ; 15 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10

	ld a,OAM_Y_OFS ; 2
	ld [(_OAMRAM + OAMA_Y) + 0 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 1 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 2 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 3 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 4 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 5 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 6 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 7 * sizeof_OAM_ATTRS],a ; 4
	ld [(_OAMRAM + OAMA_Y) + 8 * sizeof_OAM_ATTRS],a ; 4

REPT 9 ; 9 nops
	nop ; 1
ENDR

	ADJUST_VOL_NONDRAW ; 10
	LOAD_SAMPLE ; 11

REPT 7 ; 7 nops
	nop ; 1
ENDR

	ld sp,sBGPBuffer ; 3
	ld hl,rBGP ; 3

	; exit condition, press any input at end
	ldh a,[c] ; 2
	cp a,$CF ; 2
	jp z,VideoStart ; 3/4 (usually 4, 3 is at video end where timing doesn't matter)

ENDM

LOAD "Main Payload", SRAM

MACRO MAKE_TILE_DATA
	REPT 8
		db \1, \2
	ENDR
ENDM

; Only 5 tiles need to be used
; These 5 tiles have 40x8 pixels total
; The below 40 pixel pattern is used on all 8 rows
; 00112233|00011122|23330011|22330001|11222333
TileData:
	MAKE_TILE_DATA %00110011, %00001111
	MAKE_TILE_DATA %00011100, %00000011
	MAKE_TILE_DATA %01110011, %11110000
	MAKE_TILE_DATA %00110001, %11110000
	MAKE_TILE_DATA %11000111, %00111111
TileDataEnd:

MainPayload:
	; disable LCD while VRAM etc is prepped
	xor a
	ldh [rLCDC],a

	; BGP buffer should be clear already (handled in WriteMainPayload)

	; clear VRAM
	xor a
	ld hl,$8000
.clear_vram_loop
	ld [hl+],a
	bit 5,h
	jr z,.clear_vram_loop

	; clear OAM
	ld e,$A0
	ld hl,_OAMRAM
.clear_oam_loop
	ld [hl+],a
	dec e
	jr nz,.clear_oam_loop

	; set tiles
	; only 5 tiles are actually used
	ld hl,$8000
	ld de,TileData
	ld c,TileDataEnd-TileData
.vram_tiledata_loop
	ld a,[de]
	inc de
	ld [hl+],a
	dec c
	jr nz,.vram_tiledata_loop

	; set tilemap
	; this just repeats tiles 0-4
	xor a
	ld bc,$000C
	ld de,$1214
	ld hl,$9800
.vram_tilemap_loop
	ld [hl+],a
	inc a
	cp a,5
	jr nz,.skip_reset
	xor a
.skip_reset
	dec e
	jr nz,.vram_tilemap_loop
	ld e,$14
	add hl,bc
	dec d
	jr nz,.vram_tilemap_loop

	; set objects

	; first object comes early, just to delay initial rendering (by 9 dots)
	; (needed due to tight timing restraints with audio handling, also the 1 dot difference for usual BGP write boundary)
	; later objects usually just come every second tile (but not quite, since the rendering steps are 2.5 tiles each)

	ld hl,_OAMRAM + OAMA_X
	ld bc,sizeof_OAM_ATTRS
	ld [hl],2
	add hl,bc
	ld [hl],0x1B
	add hl,bc
	ld [hl],0x2B
	add hl,bc
	ld [hl],0x43
	add hl,bc
	ld [hl],0x53
	add hl,bc
	ld [hl],0x6B
	add hl,bc
	ld [hl],0x7B
	add hl,bc
	ld [hl],0x93
	add hl,bc
	ld [hl],0xA3

	ld a,OAM_Y_OFS
	ld hl,_OAMRAM + OAMA_Y
	ld e,9
.oam_y_loop
	ld [hl],a
	add hl,bc
	dec e
	jr nz,.oam_y_loop

	ld a,5 ; tiles 0-4 are used for BG, tile 5 is all 0s (i.e. transparent)
	ld hl,_OAMRAM + OAMA_TILEID
	ld e,9
.oam_tileid_loop
	ld [hl],a
	add hl,bc
	dec e
	jr nz,.oam_tileid_loop

	; clear scroll
	xor a
	ldh [rSCX],a
	ldh [rSCY],a

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

	ld a,0x8E-1
	ldh [rNR33],a

	ld a,0x87
	ldh [rNR34],a

	; 57.5+1.5 cycles until next wave sample read
	; period gets adjust immediately after so it's 57.5->57 cycles after next sample read
	; audio must be aligned after the sample after the next sample is read (so 57+57.5+1.5)

	ld a,0x8E ; 2
	ldh [rNR33],a ; 3

	ld hl,rBGP ; 3
	ld c,$00 ; 2
	ld sp,sBGPBuffer ; 3

REPT 73 ; 73 nops, perfectly aligns timing
	nop ; 1
ENDR

	; enable LCD and "start" the video
	ld a,LCDCF_ON|LCDCF_OBJON|LCDCF_BGON|LCDCF_BG9800|LCDCF_BLK01 ; 2
	ldh [rLCDC],a ; 3

VideoStart:
	; ld c,$00 must be present (constant)
	; b is a spare register used for joypad reading
	; de is used for popping for draw frame, otherwise free to trash
	; hl = $FF47 (for draw frame), otherwise sBGPBuffer position
	; sp = current sBGPBuffer position
	; hTempVol is a pseudo spare register to hold onto a pending volume write (out of registers for draw frame)

; Draw the frame using sBGPBuffer, switch to writing to sBGPBuffer at vblank
Frame0:
	DO_UNROLLED_SCANLINE_DRAW 0
	DO_UNROLLED_SCANLINE_DRAW 1
	DO_LOOPED_SCANLINE_DRAW (144-1)
	jp nz,Frame0 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 144, prep and start reading into sBGPBuffer
	DO_UNROLLED_SCANLINE_PREP_NONDRAW

.ly145
	DO_UNROLLED_SCANLINE_NONDRAW
	DO_LOOPED_SCANLINE_NONDRAW (153-1)
	jp nz,.ly145 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 153, toggle LCDC at the end to force a frame repeat
	DO_UNROLLED_SCANLINE_TOGGLE_LCDC

; Continue writing to sBGPBuffer
Frame1:
	DO_UNROLLED_SCANLINE_NONDRAW
	DO_LOOPED_SCANLINE_NONDRAW (152-1)
	jp nz,Frame1 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 152, 2 more scanlines left
	DO_UNROLLED_SCANLINE_NONDRAW

	; LY 153, toggle LCDC at the end to force a frame repeat
	DO_UNROLLED_SCANLINE_TOGGLE_LCDC

; Continue writing to sBGPBuffer, prep swap to rendering
Frame2:
	DO_UNROLLED_SCANLINE_NONDRAW
	DO_LOOPED_SCANLINE_NONDRAW (152-1)
	jp nz,Frame2 ; (3/4, 4 usually)
	nop ; 1 (ensures untaken doesn't deviate timing)

	; LY 152, 2 more scanlines left
	DO_UNROLLED_SCANLINE_NONDRAW

	; LY 153, the final scanline, must check if we can exit and jump back to the beginning if not
	DO_LOOPED_SCANLINE_FINAL

VideoEnd:
	xor a
	ldh [rLCDC],a
	ldh [rNR52],a

	; set variables for game completion
	ld a,$BD
	ld [$D36E],a
	ld a,$64
	ld [$D36F],a
	ld a,$CF
	ld [$D35E],a
	ld a,$FF
	ld hl,$D2F7
	ld e,7
.pokedex_owned_loop
	ld [hl+],a
	dec e
	jr nz,.pokedex_owned_loop
	dec a
	ld [hl],a

	; disable input polling, as it'll be simulated in the OAM DMA hook
	ld a,1
	ldh [hRGDisableJoypadPolling],a
	xor a
	ldh [hRBJoyInput],a

	; prep OAM DMA hook
	ld a,$CD ; call wOAMDMAHook
	ldh [$FF80],a
	ld a,LOW(wOAMDMAHook)
	ldh [$FF81],a
	ld a,HIGH(wOAMDMAHook)
	ldh [$FF82],a
	ld a,$E2 ; ldh [c],a
	ldh [$FF83],a

	; copy over OAM DMA hook
	ld hl,OAMDMAHook
	ld de,wOAMDMAHook
	ld bc,OAMDMAHookEnd-OAMDMAHook
.copy_dma_hook_loop
	ld a,[hl+]
	ld [de],a
	inc de
	dec bc
	ld a,b
	or c
	jr nz,.copy_dma_hook_loop

	; set wHallOfFameCurScript to non-zero
	; this gets cleared out by the end, that'll be the signal to end the OAM DMA hook
	ld a,1
	ld [$D64B],a

	; restore stack pointer
	ld sp,$DFF9

	; enable audio (game should be able to handle restoring audio state)
	ld a,$80
	ldh [rNR52],a

	; proper LCDC, game should handle the rest
	ld a,$E3
	ldh [rLCDC],a
	xor a
	ldh [rIF],a ; clear whatever pending interrupts
	reti

OldWavePattern:
	db $13, $69, $BD, $EE, $EE, $FF, $FF, $ED
	db $DE, $FF, $FF, $EE, $EE, $DB, $96, $31
OldWavePatternEnd:

	; copied over to box data ($DA80+)
OAMDMAHook:
	; [D64B] = 0 means end of credits
	ld a,[$D64B]
	and a
	jr z,.end_dma_hook
	; spam A/B alternating inputs
	ldh a,[hRBJoyInput]
	cp a,1
	ld a,2
	jr z,.inject_input
	ld a,1
.inject_input
	ldh [hRBJoyInput],a
	; needed for shortened routine
	ld a,$C3
	ld c,LOW(rDMA)
	ret

.end_dma_hook
	; end the movie with a nice message :)

	; write in new apple tile (yes it's god awful)
	ld hl,$8C00
	ld a,$70
	ld [hl+],a
	ld [hl+],a
	ld a,$3C
	ld [hl+],a
	ld [hl+],a
	ld a,$7E
	ld [hl+],a
	ld [hl+],a
	ld a,$FF
	ld [hl+],a
	ld [hl+],a
	ld [hl+],a
	ld [hl+],a
	ld [hl+],a
	ld [hl+],a
	ld a,$7E
	ld [hl+],a
	ld [hl+],a
	ld a,$24
	ld [hl+],a
	ld [hl+],a

	; "THANKS"
	ld hl,$C409
	ld a,$93
	ld [hl+],a
	ld a,$87
	ld [hl+],a
	ld a,$80
	ld [hl+],a
	ld a,$8D
	ld [hl+],a
	ld a,$8A
	ld [hl+],a
	ld a,$92
	ld [hl+],a
	inc l
	; FOR
	ld a,$85
	ld [hl+],a
	ld a,$8E
	ld [hl+],a
	ld a,$91
	ld [hl+],a
	; WATCHING
	ld hl,$C41E
	ld a,$96
	ld [hl+],a
	ld a,$80
	ld [hl+],a
	ld a,$93
	ld [hl+],a
	ld a,$82
	ld [hl+],a
	ld a,$87
	ld [hl+],a
	ld a,$88
	ld [hl+],a
	ld a,$8D
	ld [hl+],a
	ld a,$86
	ld [hl+],a
	; BAD
	ld hl,$C481
	ld a,$81
	ld [hl+],a
	ld a,$80
	ld [hl+],a
	ld a,$83
	ld [hl+],a
	inc l
	; APPLE!!
	ld a,$80
	ld [hl+],a
	ld a,$8F
	ld [hl+],a
	ld [hl+],a
	ld a,$8B
	ld [hl+],a
	ld a,$84
	ld [hl+],a
	ld a,$E7
	ld [hl+],a
	ld [hl+],a

	; apple tile
	ld hl,$C498
	ld a,$C0
	ld [hl+],a
	inc l
	; :)
	ld a,$9C
	ld [hl+],a
	ld a,$9B
	ld [hl+],a

	; re-allow joypad polling
	xor a
	ldh [hRGDisableJoypadPolling],a
	; restore old OAM DMA routine
	ld a,$3E ; ld a,$C3
	ldh [$FF80],a
	ld a,$C3
	ldh [$FF81],a
	ld a,$E0 ; ldh [rDMA],a
	ldh [$FF82],a
	ld a,LOW(rDMA)
	ldh [$FF83],a
	pop af
	ret ; probably too late to OAM DMA, missing DMA doesn't matter here anyways

OAMDMAHookEnd:

MainPayloadEnd:

ENDL

SECTION "OAM DMA Hook", WRAM0[$DA80]
wOAMDMAHook:
	ds OAMDMAHookEnd-OAMDMAHook
