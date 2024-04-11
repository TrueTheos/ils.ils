global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rsi, 0
	mov rax, 10
	mov rbx, 2
	cqo
	div rbx
	mov rsi, rax
	mov [number], rsi
	mov rdx, [number]
	mov rcx, 0
	mov rcx, [number]
	mov r8, 0
	mov r8, [number]
	imul r8, 2
	mov r9, 0
	mov r9, r8
	mov r10, 0
	mov r10, rdx
	add r10, r8
	mov [number], r10
	mov r11, 0
	mov r11, [number]
	mov rax, 60
	mov rdi, r11
	syscall
	.SCOPE_1_END:
	pop rbp
section .data
	strFormat db "%s", 0
	intFormat db "%d", 0
	charFormat db "%c", 0
	extern printf
number:
	dd 0
