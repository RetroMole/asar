;`+
;`A9 03 00 A9 F4 7F 22 08 80 90
;`80000 53 54 41 52 02 00 FD FF 00 00 02 
;`FFFFF 00
;`warnW1027
;`warnW1028
;`warnW1027
;`warnW1028

org $008000
main:

lda #datasize(my_table)		;3
; RPG Hacker: Don't quite get why this throws each warning twice.
; Seems a bit buggy, but I couldn't find anything out, and really don't care enough.
lda #datasize(other_label)	;0xFFFFFF
autoclean jsl my_table
freecode
my_table:
	db $00, $00, $02
other_label:
