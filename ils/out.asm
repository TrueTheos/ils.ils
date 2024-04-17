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
	mov rax, rcx
	mov rbx, 2
	cqo
	div rbx
	mov rdx, rdx
	cmp rdx, 0
	jne .IF_8
	.SCOPE_3_START:
	mov rbx, rcx
	mov rdi, intFormat
	mov rsi, rbx
	mov rax, 0
	call printf
	.SCOPE_3_END:
	.IF_8:
	mov r12, 0
	mov r12, rcx
	add r12, 1
	mov rcx, r12
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
section .text
	extern printf
