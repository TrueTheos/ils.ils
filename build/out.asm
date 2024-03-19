global _start
_start:
    ; let x
    mov rax, 7
    push rax

    ; let y
    mov rax, 91
    push rax

    ; print
    push QWORD [rsp + 0]
    pop rax
    call print_number
    ; exit
    mov rax, 1
    push rax
    mov rax, 1
    push rax
    pop rax
    pop rbx
    add rax, rbx
    push rax
    mov rax, 60
    pop rdi
    syscall

    ; exit
    push QWORD [rsp + 0]
    mov rax, 60
    pop rdi
    syscall

print_number:
    push rax
    mov rcx, 10
    mov rbx, rsp
    add rbx, 20
    mov byte [rbx], 0
.convert_loop:
    dec rbx
    xor rdx, rdx
    div rcx
    add dl, '0'
    mov [rbx], dl
    test rax, rax
    jnz .convert_loop
    mov rsi, rbx
    mov rdx, 21
    mov rax, 1
    mov rdi, 1
    syscall
    pop rax
    ret
print_string:
.print:
    mov rsi, rdi
    mov rdx, rax
    mov rax, 1
    mov rdi, 1
    syscall
    ret

section .data
