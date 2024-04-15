global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rdx, 0
	mov rdx, 1
	add rdx, 1
	mov rcx, rcx
	mov rdi, 
	mov rsi, rcx
	mov rax, 0
	call printf
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
