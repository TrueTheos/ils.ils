global main
main:
	push rbp
	mov rbp, rsp
	.FUNC_MAIN_START:
	.WHILE_6_START:
	cmp qword [limit], 10
	jge .WHILE_6_END
	.LOOP_3_START:
	mov rcx, 0
	mov rcx, [limit]
	add rcx, 1
	mov qword [limit], rcx
	mov rbx, [xd]
	mov rdi, strFormatNl
	mov rsi, rbx
	mov rax, 0
	call printf
	.LOOP_3_END:
	jmp .WHILE_6_START
	.WHILE_6_END:
	mov rax, 0
	jmp .FUNC_main_END
	.FUNC_main_END:
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
	STR_0 db `XTDD`
	limit dq 1
	xd dq STR_0
section .text
	extern printf
	extern puts
