using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public class ASMGenerator
    {
        private Dictionary<int, Word> _words = new()
        {
            { 1, new Word(){ byteCount = 1, shortName = Word.ShortName.db, longName = Word.LongName.@byte, reserve = ".byte" } },
            { 2, new Word(){ byteCount = 2, shortName = Word.ShortName.dw, longName = Word.LongName.word, reserve = ".word" } },
            { 4, new Word(){ byteCount = 4, shortName = Word.ShortName.dd, longName = Word.LongName.dword, reserve = ".long" } },
            { 8, new Word(){ byteCount = 8, shortName = Word.ShortName.dq, longName = Word.LongName.qword, reserve = ".quad" } }
        };

        private string asm = "";

        private Dictionary<string, ReservedVariable> dataSection = new();

        public string GenerateASM(List<IRNode> ir)
        {
            AddAsm("global main", 0);

            foreach (IRNode node in ir) 
            {
                GenerateIRNode(node);
            }

            AddAsm("mov rax, 60");
            AddAsm("mov rdi, 1");
            AddAsm("syscall");

            AddAsm("section .data", 0);
            AddAsm("strFormat db \"%s\", 0");
            AddAsm("intFormat db \"%d\", 0");
            AddAsm("charFormat db \"%c\", 0");
            AddAsm("extern printf");
            foreach (var data in dataSection)
            {
                AddAsm($"{data.Value.name}:", 0);
                AddAsm($"{data.Value.word.reserve} {data.Value.var.value}");
            }

            Console.WriteLine('\n' + asm);

            return asm;
        }

        private void AddAsm(string code, int tabsCount = 1)
        {
            string tabs = new string('\t', tabsCount);
            asm += tabs + code + '\n';
        }

        private bool VarExists(string name)
        {
            return dataSection.ContainsKey(name);
        }

        private void GenerateFunction(IRFunction func)
        {
            AddAsm($"{func.name}:", 0);
        }

        private void GenerateVariable(Variable var)
        {
            if(var is NamedVariable namedVar)
            {
                if(VarExists(namedVar.variableName))
                {
                    ErrorHandler.Custom($"Variable {namedVar.variableName} already exists!");
                    return;
                }

                switch (namedVar.variableType)
                {
                    case VariableType.CHAR:
                        dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[1],
                            var = namedVar});
                        break;
                    case VariableType.INT:
                        dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[4],
                            var = namedVar});
                        break;
                    case VariableType.BOOL:
                        dataSection.Add(namedVar.variableName, new ReservedVariable() { name = namedVar.variableName, word = _words[1],
                            var = namedVar});
                        break;
                    case VariableType.STRING:
                        //todo
                        break;
                }
            }           
        }

        private void GenerateAssign(IRAssign asign)
        {
            switch (asign.assignedType)
            {
                case VariableType.STRING:
                    //todo
                    break;
                case VariableType.INT:
                    AddAsm($"mov {_words[4].longName} [{asign.identifier}], {asign.value}");
                    break;
                case VariableType.CHAR:
                    AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case VariableType.BOOL:
                    AddAsm($"mov {_words[1].longName} [{asign.identifier}], {asign.value}");
                    break;
                case VariableType.IDENTIFIER:
                    AddAsm($"mov {dataSection[asign.identifier].word.longName} [{asign.identifier}], {asign.value}");
                    break;
            }
        }

        private void GenerateIRNode(IRNode node)
        {
            if(node is IRLabel label) AddAsm($".{label.labelName}:");
            if (node is Variable variable) GenerateVariable(variable);
            if(node is IRAssign asign) GenerateAssign(asign);
            if(node is IRFunction func) GenerateFunction(func);
        }

        private struct Word
        {
            public enum ShortName { db, dw, dd, dq}
            public ShortName shortName;
            public enum LongName { @byte, word, dword, qword}
            public LongName longName;
            public string reserve;
            public int byteCount;
        }

        private struct ReservedVariable
        {
            public string name;
            public Word word;
            public Variable var;
            public string initialValue;
        }
    }
}
