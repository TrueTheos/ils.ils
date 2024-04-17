using ils;
using static System.Net.Mime.MediaTypeNames;

class ILS
{
    const string SOURCE_FILE = "C:\\Projects\\ils.ils\\ils\\test.ils";
    const string OUTPUT_FILE = "C:\\Projects\\ils.ils\\ils\\out.asm";

    public static ASMGenerator asmGen;

    static void Main(string[] args)
    {
        if (File.Exists(SOURCE_FILE))
        {
            // Read all lines from the file
            string source = File.ReadAllText(SOURCE_FILE);

            if (source.Last() != '\n') source += '\n';

            Tokenizer tokenizer = new();
            List<Token> tokens = tokenizer.Tokenize(source);

            /*int line = 1;
            foreach (Token token in tokens)
            {
                if (token.line != line)
                {
                    line = token.line;
                    Console.Write("\n");
                }
                Console.Write(token.tokenType.ToString() + " ");            
            }*/

            Parser parser = new();
            ASTScope mainScope = parser.Parse(tokens);

            Verificator verificator = new Verificator();
            verificator.Verify(mainScope);

            IRGenerator irGenerator = new();
            var ir = irGenerator.Generate(mainScope);
            IROptimizer optimizer = new();
            var optimizedIR = optimizer.GetOptimizedIR(ir);
            asmGen = new();
            string asm = asmGen.GenerateASM(optimizedIR);

            using (StreamWriter writer = new StreamWriter(OUTPUT_FILE))
            {
                writer.Write(asm);
            }

        }
        else
        {
            Console.WriteLine("File not found.");
        }
    }
}