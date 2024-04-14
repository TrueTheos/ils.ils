global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rdx, 0
	mov rcx, 0
	call xd
	cmp rax, 1
	jne .IF_4
	.SCOPE_2_START:
	mov r8, 1
	mov rdi, intFormat
	mov rsi, r8
	mov rax, 0
	call printf
	.SCOPE_2_END:
	.IF_4:
	pop rbp
	mov rax, 1
	ret 0
	.SCOPE_1_END:
xd:
	push rbp
	mov rbp, rsp
	.SCOPE_3_START:
	pop rbp
	mov rax, 0
	ret 0
	.SCOPE_3_END:
section .data
	strFormat db "%s", 10, 0
	intFormat db "%d", 10, 0
	charFormat db "%c", 10, 0
number:
	dd 0
section .text
	extern printf
