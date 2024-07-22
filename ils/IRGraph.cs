using DotNetGraph.Compilation;
using DotNetGraph.Core;
using DotNetGraph.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ils.ASTCondition;
using static ils.IRGenerator;

namespace ils
{
    public class IRGraph
    {
        public List<GraphNode> entryNodes;
        public List<IRNode> ir;

        private List<string> edges = new();

        public Dictionary<string, GraphNode> blocks = new();

        public List<IRNode> OptimizeIR(List<IRNode> _ir)
        {
            ir = _ir;
            GenerateGraph();

            EliminateUnusedFunctions();
            //EliminateDeadCode();

            ir = ir.Where(x => x != null).ToList();

            DrawGraph();

            return ir;
        }

        public void GenerateGraph()
        {
            GraphNode lastBlock = null;
            for (int i = 0; i < ir.Count; i++)
            {
                if (ir[i] is IRLabel label)
                {
                    if(lastBlock != null)
                    {
                        lastBlock.endIndex = i;
                    }

                    blocks[label.labelName] = new(i, label.labelName);
                    lastBlock = blocks[label.labelName];
                }
            }

            foreach (var node in blocks.Values)
            {
                ProcessNode(node);
            }
        }

        private void EliminateUnusedFunctions()
        {
            List<IRFunctionCall> funcCalls = ir.Where(x => x is IRFunctionCall).Select(x => x as IRFunctionCall).ToList();

            List<IRFunction> functions = ir.Where(x => x is IRFunction func && func.name != MAIN_FUNCTION_NAME).Select(x => x as IRFunction).ToList();

            foreach (var function in functions)
            {
                if(!funcCalls.Any(x => x.name == function.name))
                {
                    foreach (var node in function.nodes)
                    {
                        ir.Remove(node);
                    }
                }
            }
        }

        private void EliminateDeadCode()
        {
            List<GraphNode> nodes = GetParentlessNodes();

            foreach (var node in nodes) 
            {
                for (int i = node.startIndex; i <= node.endIndex; i++)
                {
                    ir[i] = null;
                }
            }
        }

        private List<GraphNode> GetParentlessNodes()
        {
            return blocks.Values.Where(x => x.labelName != MAIN_FUNCTION_LABEL && x.parentNodes.Count == 0).ToList();
        }

        private void ProcessNode(GraphNode node)
        {
            for (int i = node.startIndex; i <= node.endIndex; i++)
            {
                IRNode instruction = ir[i];
                if (instruction is IRJump jump)
                {
                    node.branches.Add(new(node, blocks[jump.label], jump.conditionType));
                    blocks[jump.label].parentNodes.Add(node);

                    if (jump.conditionType == ASTCondition.ConditionType.NONE) return;
                }
                else if (instruction is IRLabel label && i != node.startIndex)
                {
                    node.branches.Add(new(node, blocks[label.labelName], ConditionType.NONE));
                    blocks[label.labelName].parentNodes.Add(node);
                }
                else if(instruction is IRFunctionEpilogue)
                {
                    if (ir[i + 1] is IRFunctionEpilogue epilogue)
                    {
                        node.AddInstruction(epilogue);
                        if (ir[i + 2] is IRScopeEnd scopeEnd)
                        {
                            node.instructions.Add(scopeEnd);
                        }
                    }
                    return;
                }
                else
                {
                    node.AddInstruction(instruction);
                }
            }
        }

        private void DrawGraph()
        {
            var graph = new DotGraph().WithIdentifier("MyGraph");

            foreach (var node in blocks.Values)
            {
                var myNode = new DotNode()
                    .WithIdentifier(node.labelName)
                    .WithShape(DotNodeShape.Ellipse)
                    .WithLabel(node.labelName)
                    .WithFillColor(DotColor.Coral)
                    .WithFontColor(DotColor.Black)
                    .WithStyle(DotNodeStyle.Filled)
                    .WithWidth(0.5)
                    .WithHeight(0.5)
                    .WithPenWidth(2);

                graph.Add(myNode);

                DotNode lastNode = myNode;
                foreach (var inst in node.instructions)
                {
                    var nod = new DotNode()
                    .WithIdentifier(inst.GetHashCode().ToString())
                    .WithShape(DotNodeShape.Ellipse)
                    .WithLabel(inst.GetString())
                    .WithFillColor(DotColor.Coral)
                    .WithFontColor(DotColor.Black)
                    .WithStyle(DotNodeStyle.Bold)
                    .WithWidth(0.5)
                    .WithHeight(0.5)
                    .WithPenWidth(2);

                    var edge = new DotEdge()
                        .From(lastNode).To(nod)
                        .WithArrowHead(DotEdgeArrowType.Normal)
                        .WithArrowTail(DotEdgeArrowType.Normal)
                        .WithColor(DotColor.Red)
                        .WithFontColor(DotColor.Black)
                        .WithStyle(DotEdgeStyle.Solid)
                        .WithPenWidth(1.5);

                    graph.Add(edge);
                    graph.Add(nod);

                    lastNode = nod;
                }

                if (lastNode != null)
                {
                    myNode = lastNode;
                }
                foreach (var branch in node.branches)
                {
                    DotEdge edge = null;

                    if (branch.condition != ConditionType.NONE)
                    {
                        edge = new DotEdge()
                            .From(lastNode).To(branch.child.labelName)
                            .WithLabel(branch.condition.ToString())
                            .WithArrowHead(DotEdgeArrowType.Box)
                            .WithColor(DotColor.Red)
                            .WithPenWidth(1.5);
                    }
                    else
                    {
                        edge = new DotEdge()
                            .From(lastNode).To(branch.child.labelName)
                            .WithArrowHead(DotEdgeArrowType.Box)
                            .WithColor(DotColor.Red)
                            .WithPenWidth(1.5);
                    }

                    graph.Add(edge);
                }
            }

            var writer = new StringWriter();
            var context = new CompilationContext(writer, new CompilationOptions());
            graph.CompileAsync(context);

            var result = writer.GetStringBuilder().ToString();

            File.WriteAllText("graph.dot", result);
        }
    }

    public class GraphNode
    {
        public int startIndex;
        public string labelName;
        public int endIndex;
        public List<IRNode> instructions = new();
        public List<GraphNode> parentNodes = new(); // nodes that jump to this
        public List<Edge> branches = new();

        public GraphNode(int index, string name)
        {
            this.startIndex = index;
            this.labelName = name;
        }

        public void AddInstruction(IRNode irNode)
        {
            instructions.Add(irNode);
        }
    }

    public class Edge
    {
        public GraphNode parent;
        public GraphNode child;
        public ConditionType condition;

        public Edge(GraphNode parent, GraphNode child, ConditionType condition)
        {
            this.parent = parent;
            this.child = child;
            this.condition = condition;
        }
    }
}
