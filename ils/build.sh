#!/bin/bash
nasm -f elf64 out.asm -gdwarf 
gcc -no-pie out.o -o out -lc -z noexecstack

#echo $?what 