global main
main:
	push rbp
	mov rbp, rsp
	sub rsp, 8
	.FUNC_MAIN_START:
	mov qword [rbp-8], 1
	mov rbx, [rbp-8]
	mov rdi, intFormatNl
	mov rsi, rbx
	mov rax, 0
	call printf
	mov r8, [integer]
	mov qword [rbp-8], r8
	mov r12, [rbp-8]
	mov rdi, intFormatNl
	mov rsi, r12
	mov rax, 0
	call printf
	mov r13, 2
	mov rsi, [array + r13 * 8]
	mov qword [rbp-8], rsi
	mov r14, [rbp-8]
	mov rdi, intFormatNl
	mov rsi, r14
	mov rax, 0
	call printf
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
	integer dq 13
	array dq 2, 2, 3, 4, 5
section .text
	extern printf
	extern puts
