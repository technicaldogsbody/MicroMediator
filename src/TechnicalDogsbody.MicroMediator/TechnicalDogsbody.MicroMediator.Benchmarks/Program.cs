using BenchmarkDotNet.Running;
using System.Diagnostics.CodeAnalysis;

namespace TechnicalDogsbody.MicroMediator.Benchmarks;

[ExcludeFromCodeCoverage]
internal class Program
{
    internal static void Main(string[] args)
    {
        var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
