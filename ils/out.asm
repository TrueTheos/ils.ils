global main
main:
	push rbp
	mov rbp, rsp
	sub rsp, 8
	.SCOPE_1_START:
	mov qword [rbp-8], rax
	push 30
	call a
	mov rbx, [rbp-8]
	mov rdi, intFormat
	mov rsi, rbx
	mov rax, 0
	call printf
	mov rax, 0
	jmp .SCOPE_1_END
	.SCOPE_1_END:
	mov rsp, rbp
	pop rbp
	ret
a:
	push rbp
	mov rbp, rsp
	sub rsp, 8
	.SCOPE_2_START:
	mov rax, [rbp+16]
	jmp .SCOPE_2_END
	.SCOPE_2_END:
	mov rsp, rbp
	pop rbp
	ret
section .data
	strFormat db "%s", 10, 0
	intFormat db "%d", 10, 0
	charFormat db "%c", 10, 0
section .text
	extern printf
