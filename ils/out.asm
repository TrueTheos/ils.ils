global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	.WHILE_4_START:
	cmp byte [s], 1
	jne .WHILE_4_END
	.SCOPE_2_START:
	cmp qword [limit], 0
	jge .IF_8
	.SCOPE_3_START:
	jmp .SCOPE_2_END
	.SCOPE_3_END:
	.IF_8:
	mov rbx, limit
	mov rdi, intFormatNl
	mov rsi, rbx
	mov rax, 0
	call printf
	mov r12, limit
	sub r12, 1
	mov qword [limit], r12
	.SCOPE_2_END:
	jmp .WHILE_4_START
	.WHILE_4_END:
	mov rax, 0
	jmp .SCOPE_1_END
	.SCOPE_1_END:
	mov rsp, rbp
	pop rbp
	ret
section .data
	strFormat db "%s", 0
	intFormat db "%d", 0
	charFormat db "%c", 0
	strFormatNl db "%s", 10, 0
	intFormatNl db "%d", 10, 0
	charFormatNl db "%c", 10, 0
	s db 1
	limit dq 10
section .text
	extern printf
	extern puts
