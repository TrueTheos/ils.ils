using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ils;
using System.Data;
using static System.Net.Mime.MediaTypeNames;

class ILS
{
    const string SOURCE_FILE = @"C:\Projects\ils.ils\ils\test.ils";
    const string OUTPUT_FILE = @"C:\Projects\ils.ils\ils\out.asm";

    public static ASMGenerator asmGen;
    public static IRGenerator irGen;
    public static IRGraph irGraph;

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

            irGen = new();
            var ir = irGen.Generate(mainScope);

            irGraph = new IRGraph();
            var optimizedIR = irGraph.OptimizeIR(ir);

            asmGen = new();

            StreamWriter streamWriter = new(OUTPUT_FILE);
            asmGen.GenerateASM(optimizedIR, streamWriter);

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