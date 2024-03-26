global _start
_start:
    ; variable a
    mov rax, 5
    push rax

    mov rax, 10
    push rax
    pop rax
    mov [rsp + 0], rax
    ;; if
    push QWORD [rsp + 0]
    pop rax
label0:
    test rax, rax
    jz label1
    mov rax, 1
    push rax
    push QWORD [rsp + 8]
    pop rax
    pop rbx
    add rax, rbx
    push rax
    pop rax
    mov [rsp + 0], rax
    jmp label0
label1:
    ; exit
    push QWORD [rsp + 0]
    mov rax, 60
    pop rdi
    syscall

    mov rax, 60
    mov rdi, 69
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
