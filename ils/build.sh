#!/bin/bash
nasm -felf64 out.asm -o out.o
ld out.o -o out -lc

#echo $?what 