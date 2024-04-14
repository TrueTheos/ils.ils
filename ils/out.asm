global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rdx, 12
	mov rcx, 5
	push rcx
	push rdx
	call div
	mov r8, rax
	mov rdi, intFormat
	mov rsi, r8
	mov rax, 0
	call printf
	pop rbp
	mov rax, 0
	ret 0
	.SCOPE_1_END:
div:
	push rbp
	mov rbp, rsp
	.SCOPE_2_START:
	mov r9, 0
	mov rax, [rsp+16]
	mov rbx, [rsp+24]
	cqo
	div rbx
	mov r9, rax
	pop rbp
	mov rax, r9
	ret 16
	.SCOPE_2_END:
section .data
	strFormat db "%s", 10, 0
	intFormat db "%d", 10, 0
	charFormat db "%c", 10, 0
number:
	dd 0
section .text
	extern printf
