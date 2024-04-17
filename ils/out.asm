global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rcx, 0
	.WHILE_4_START:
	cmp dword rcx, 10
	jge .WHILE_4_END
	.SCOPE_2_START:
	mov rdx, 0
	mov rdx, rcx
	add rdx, 1
	mov rcx, rdx
	mov r8, rcx
	mov rdi, intFormat
	mov rsi, r8
	mov rax, 0
	call printf
	.SCOPE_2_END:
	jmp .WHILE_4_START
	.WHILE_4_END:
	pop rbp
	mov rax, 0
	ret 0
	.SCOPE_1_END:
section .data
	strFormat db "%s", 10, 0
	intFormat db "%d", 10, 0
	charFormat db "%c", 10, 0
number:
	dd 15
section .text
	extern printf
