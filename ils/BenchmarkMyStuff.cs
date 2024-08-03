using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class BenchmarkMyStuff
    {
        DataTable dataTable = new();
       // MathEvaluator mathEvaluator = new();


        [Benchmark]
        public void BenchmarkMathEvaluator()
        {
            var result = MathEvaluator.Evaluate("1 + 2 * (3 + 4)");
        }

        [Benchmark]
        public void BenchmarkDataTable()
        {
            var result = dataTable.Compute("1 + 2 * (3 + 4)", "");
        }
    }
}
