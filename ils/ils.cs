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
        /*var config = DefaultConfig.Instance
            .AddJob(Job
                 .WithLaunchCount(1)
                 .WithToolchain(InProcessEmitToolchain.Instance));*/
        //var summary = BenchmarkRunner.Run<BenchmarkMyStuff>();

        
        if (File.Exists(SOURCE_FILE))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Read all lines from the file
            string source = File.ReadAllText(SOURCE_FILE);

            if (source.Last() != '\n') source += '\n';

            Tokenizer tokenizer = new();
            List<Token> tokens = tokenizer.Tokenize(source);

            //int line = 1;
            //foreach (Token token in tokens)
            //{
            //    if (token.Line != line)
            //    {
            //        line = token.Line;
            //        Console.Write("\n");
            //    }
            //    Console.Write(token.TokenType.ToString() + " ");            
            //}

            Parser parser = new();
            ASTScope mainScope = parser.Parse(tokens);

            Verificator verificator = new Verificator();
            verificator.Verify(mainScope);

            irGen = new();
            var ir = irGen.Generate(mainScope);

            irGraph = new IRGraph();
            var optimizedIR = irGraph.OptimizeIR(ir);

            asmGen = new();
            string asm = asmGen.GenerateASM(optimizedIR);

            using (StreamWriter writer = new StreamWriter(OUTPUT_FILE))
            {
                writer.Write(asm);
            }

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

public class Node
{
    public string Name { get; set; }
    public Dictionary<string, int> Connections { get; set; }

    public Node(string name)
    {
        Name = name;
        Connections = new Dictionary<string, int>();
    }
}

public class Graph
{
    private Dictionary<string, Node> nodes;

    public Graph()
    {
        nodes = new Dictionary<string, Node>();
    }

    public void AddNode(string name)
    {
        if (!nodes.ContainsKey(name))
        {
            nodes[name] = new Node(name);
        }
    }

    public void AddConnection(string from, string to, int weight)
    {
        AddNode(from);
        AddNode(to);
        nodes[from].Connections[to] = weight;
    }

    public void PrintGraph()
    {
        foreach (var node in nodes.Values)
        {
            Console.Write($"{node.Name}: ");
            foreach (var connection in node.Connections)
            {
                Console.Write($"{connection.Key}({connection.Value}) ");
            }
            Console.WriteLine();
        }
    }

    public static Graph ConstructFromInstructions(string[] instructions)
    {
        var graph = new Graph();
        string currentLabel = null;
        int nodeCount = 0;

        foreach (var instruction in instructions)
        {
            var parts = instruction.Trim('(', ')').Split(',').Select(p => p.Trim()).ToArray();
            var op = parts[0];

            if (op == "LABEL")
            {
                currentLabel = parts[1];
                graph.AddNode(currentLabel);
                nodeCount = 0;
            }
            else if (op == "JMP" || op == "JLE" || op == "JNE")
            {
                var target = parts[1];
                if (currentLabel != null)
                {
                    graph.AddConnection(currentLabel, target, nodeCount);
                    nodeCount = 0;
                }
            }
            else
            {
                nodeCount++;
            }
        }

        return graph;
    }

    public string ToAsciiArt()
    {
        var result = new System.Text.StringBuilder();
        var visited = new HashSet<string>();
        var inProgress = new HashSet<string>();

        void DfsAscii(string nodeName, string prefix, bool isLast, int depth)
        {
            if (depth > 10 || inProgress.Contains(nodeName)) // Prevent infinite recursion
            {
                result.AppendLine($"{prefix}{(isLast ? "└── " : "├── ")}{nodeName} (cycle)");
                return;
            }

            if (visited.Contains(nodeName))
            {
                result.AppendLine($"{prefix}{(isLast ? "└── " : "├── ")}{nodeName} (visited)");
                return;
            }

            visited.Add(nodeName);
            inProgress.Add(nodeName);

            result.AppendLine($"{prefix}{(isLast ? "└── " : "├── ")}{nodeName}");

            var node = nodes[nodeName];
            var connections = node.Connections.ToList();
            for (int i = 0; i < connections.Count; i++)
            {
                var (target, weight) = connections[i];
                var newPrefix = prefix + (isLast ? "    " : "│   ");
                result.AppendLine($"{newPrefix}│  ({weight})");
                DfsAscii(target, newPrefix, i == connections.Count - 1, depth + 1);
            }

            inProgress.Remove(nodeName);
        }

        foreach (var nodeName in nodes.Keys)
        {
            if (!visited.Contains(nodeName))
            {
                DfsAscii(nodeName, "", true, 0);
            }
        }

        return result.ToString();
    }
}