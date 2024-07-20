global main
add:
	push rbp
	mov rbp, rsp
	sub rsp, 16
	.SCOPE_1_START:
	mov rcx, 0
	mov rcx, [rbp+16]
	add rcx, [rbp+24]
	mov rax, rcx
	jmp .SCOPE_1_END
	.SCOPE_1_END:
	mov rsp, rbp
	pop rbp
	ret
main:
	push rbp
	mov rbp, rsp
	.SCOPE_2_START:
	.WHILE_6_START:
	cmp byte [s], 1
	jne .WHILE_6_END
	.SCOPE_3_START:
	cmp qword [limit], 100
	jle .IF_10
	.SCOPE_4_START:
	jmp .SCOPE_3_END
	.SCOPE_4_END:
	.IF_10:
	mov rbx, [limit]
	mov rdi, intFormatNl
	mov rsi, rbx
	mov rax, 0
	call printf
	mov r8, [limit]
	mov r9, [limit]
	push r9
	push r8
	call add
	mov qword [limit], rax
	.SCOPE_3_END:
	jmp .WHILE_6_START
	.WHILE_6_END:
	mov rax, 0
	jmp .SCOPE_2_END
	.SCOPE_2_END:
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
	limit dq 1
section .text
	extern printf
	extern puts
