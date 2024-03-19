global _start
_start:
    ;; let
    mov rax, 2
    push rax
    mov rax, 3
    push rax
    mov rax, 2
    push rax
    pop rax
    pop rbx
    mul rbx
    push rax
    mov rax, 10
    push rax
    pop rax
    pop rbx
    sub rax, rbx
    push rax
    pop rax
    pop rbx
    div rbx
    push rax
    ;; /let
    ;; let
    mov rax, 7
    push rax
    ;; /let
    ;; if
    mov rax, 0
    push rax
    pop rax
    test rax, rax
    jz label0
    mov rax, 1
    push rax
    pop rax
    mov [rsp + 0], rax
    jmp label1
label0:
    ;; elif
    mov rax, 0
    push rax
    pop rax
    test rax, rax
    jz label2
    mov rax, 2
    push rax
    pop rax
    mov [rsp + 0], rax
    jmp label1
label2:
    ;; else
    mov rax, 3
    push rax
    pop rax
    mov [rsp + 0], rax
label1:
    ;; /if
    ;; print
    mov rdi, 1
    mov rsi, message
    mov rdx, length
    mov rax, 1
    syscall
    ;; /print
    message: db 'hello world!',10
    length: equ $-message
    ;; exit
    push QWORD [rsp + 0]
    mov rax, 60
    pop rdi
    syscall
    ;; /exit
    mov rax, 60
    mov rdi, 0
    syscall
