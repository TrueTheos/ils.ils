using ils;

class ILS
{
    const string SOURCE_FILE = @"C:\Projects\ils.ils\ils\test.ils";
    const string OUTPUT_FILE = @"C:\Projects\ils.ils\ils\out.asm";

    public static ASMGenerator AsmGen;
    public static IRGenerator IrGen;
    public static IRGraph IrGraph;

    static void Main(string[] args)
    {
        if (File.Exists(SOURCE_FILE))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            string source = File.ReadAllText(SOURCE_FILE);

            if (source.Last() != '\n') source += '\n';

            Tokenizer tokenizer = new();
            List<Token> tokens = tokenizer.Tokenize(source);

            int line = 1;
            foreach (Token token in tokens)
            {
                if (token.Line != line)
                {
                    line = token.Line;
                    Console.Write("\n");
                }
                Console.Write(token.TokenType.ToString() + " ");            
            }

            Parser parser = new();
            ASTScope mainScope = parser.Parse(tokens);

            IrGen = new();
            var ir = IrGen.Generate(mainScope);

            IrGraph = new IRGraph();
            var optimizedIR = IrGraph.OptimizeIR(ir);

            Console.WriteLine();
            foreach (var node in optimizedIR)
            {
                Console.WriteLine(node.GetString());
            }

            AsmGen = new();

            StreamWriter streamWriter = new(OUTPUT_FILE);
            AsmGen.GenerateASM(optimizedIR, streamWriter);

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine($"Compiled in {elapsedMs}ms");
        }
        else
        {
            Console.WriteLine("File not found.");
        }
    } 
}