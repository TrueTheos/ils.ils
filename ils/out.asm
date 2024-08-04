global main
sum:
	push rbp
	mov rbp, rsp
	sub rsp, 16
	.FUNC_sum_START:
	mov rcx, 0
	mov rcx, [rbp+16]
	add rcx, [rbp+24]
	mov rax, rcx
	jmp .FUNC_sum_END
	.FUNC_sum_END:
	mov rsp, rbp
	pop rbp
	ret
test:
	push rbp
	mov rbp, rsp
	.FUNC_test_START:
	mov rdi, strFormatNl
	mov rsi, STR_0
	mov rax, 0
	call printf
	.FUNC_test_END:
	mov rsp, rbp
	pop rbp
	ret
main:
	push rbp
	mov rbp, rsp
	sub rsp, 8
	.FUNC_MAIN_START:
	call test
	push 2
	push 3
	call sum
	push 3
	push 2
	call sum
	push rax
	push 1
	call sum
	mov qword [rbp-8], rax
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
	STR_0 db `Test`
	integer dq 13
	array dq 2, 2, 3, 4, 5
section .text
	extern printf
	extern puts
