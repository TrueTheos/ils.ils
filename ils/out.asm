global main
main:
	push rbp
	mov rbp, rsp
	.SCOPE_1_START:
	mov rsi, 0
	mov rsi, [number]
	mov rdx, 0
	add rdx, [number]
	add rdx, 2
	mov rcx, 0
	mov rcx, rdx
	mov r8, 0
	add r8, rdx
	add r8, 3
	mov [number], r8
	mov r9, 10
	mov r9, [number]
	mov rax, 60
	mov rdi, r9
	syscall
	.SCOPE_1_END:
	pop rbp
section .data
	strFormat db "%s", 0
	intFormat db "%d", 0
	charFormat db "%c", 0
	extern printf
number:
	dd 10
