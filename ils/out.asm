global _start
_start:
	SCOPE_0_START:
	mov byte [x], 'a'
	SCOPE_0_END:
	mov rax, 60
	mov rdi, 1
	syscall
section .data
	extern printf
x:
	.byte 1
