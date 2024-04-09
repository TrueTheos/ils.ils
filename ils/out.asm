global main
main:
	.SCOPE_1_START:
	mov dword [TEMP_0], 1
	mov dword [TEMP_1], 1
	mov dword [number], TEMP_1
	mov dword [TEMP_2], number
	.SCOPE_1_END:
xD:
	.SCOPE_2_START:
	.SCOPE_2_END:
	mov rax, 60
	mov rdi, 1
	syscall
section .data
	strFormat db "%s", 0
	intFormat db "%d", 0
	charFormat db "%c", 0
	extern printf
number:
	.long 10
