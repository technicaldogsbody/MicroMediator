
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
internal class Program
{
    internal static void Main()
    {
        var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
