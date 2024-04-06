global _start
_start:
	SCOPE_0_START:
	mov dword [x], 10
	mov dword [TEMP_4], 1
	mov dword [TEMP_1], TEMP_4
	mov dword [TEMP_5], 0
	SCOPE_2_START:
	mov dword [TEMP_6], 2
	mov dword [TEMP_7], 3
	mov dword [x], TEMP_7
	SCOPE_2_END:
	IF_2:
	mov dword [TEMP_11], 5
	SCOPE_6_START:
	mov dword [TEMP_13], 5
	mov dword [TEMP_14], 5
	mov dword [TEMP_12], TEMP_14
	mov dword [TEMP_15], 2
	mov dword [x], TEMP_15
	SCOPE_6_END:
	ELSE_6_START:
	SCOPE_9_START:
	mov dword [x], 1
	SCOPE_9_END:
	IF_2_END:
	SCOPE_0_END:
	mov rax, 60
	mov rdi, 1
	syscall
section .data
	extern printf
	x dd 1
	TEMP_0 db 1
	TEMP_1 dd 1
	TEMP_2 dd 1
	TEMP_4 dd 1
	TEMP_5 dd 1
	TEMP_6 dd 1
	TEMP_7 dd 1
	TEMP_8 db 1
	TEMP_9 dd 1
	TEMP_11 dd 1
	TEMP_12 dd 1
	TEMP_13 dd 1
	TEMP_14 dd 1
	TEMP_15 dd 1
