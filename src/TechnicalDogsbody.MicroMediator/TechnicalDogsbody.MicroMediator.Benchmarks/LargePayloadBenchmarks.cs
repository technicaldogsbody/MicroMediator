namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Tests performance with large payloads.
/// Verifies memory efficiency with KB-MB sized messages.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class LargePayloadBenchmarks
{
    private IMediator _microMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [Params(1, 10, 100, 1000)]  // KB
    public int PayloadSizeKB { get; set; }

    private LargePayloadQuery _microQuery = null!;
    private MediatrLargePayloadQuery _mediatrQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup MicroMediator
        var microServices = new ServiceCollection();
        microServices.AddMediator()
            .AddSingletonHandler<LargePayloadQuery, int, LargePayloadQueryHandler>();

        var microProvider = microServices.BuildServiceProvider();
        _microMediator = microProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<LargePayloadBenchmarks>());

        var mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatr = mediatrProvider.GetRequiredService<MediatR.IMediator>();

        // Generate test payloads
        byte[] data = new byte[PayloadSizeKB * 1024];
        Random.Shared.NextBytes(data);

        _microQuery = new LargePayloadQuery { Data = data };
        _mediatrQuery = new MediatrLargePayloadQuery { Data = data };
    }

    [Benchmark(Baseline = true)]
    public async Task<int> MicroMediator_LargePayload() => await _microMediator.SendAsync(_microQuery);

    [Benchmark]
    public async Task<int> MediatR_LargePayload() => await _mediatr.Send(_mediatrQuery);

    [Benchmark]
    public async Task<int> MicroMediator_MultiplePayloads()
    {
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            total += await _microMediator.SendAsync(_microQuery);
        }

        return total;
    }

    [Benchmark]
    public async Task<int> MediatR_MultiplePayloads()
    {
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            total += await _mediatr.Send(_mediatrQuery);
        }

        return total;
    }

    // MicroMediator types
    public record LargePayloadQuery : Abstractions.IRequest<int>
    {
        public required byte[] Data { get; init; }
    }

    public class LargePayloadQueryHandler : Abstractions.IRequestHandler<LargePayloadQuery, int>
    {
        public ValueTask<int> HandleAsync(LargePayloadQuery request, CancellationToken cancellationToken)
        {
            // Simulate processing
            int checksum = 0;
            for (int i = 0; i < Math.Min(request.Data.Length, 1000); i++)
            {
                checksum ^= request.Data[i];
            }

            return ValueTask.FromResult(checksum);
        }
    }

    // MediatR types
    public record MediatrLargePayloadQuery : MediatR.IRequest<int>
    {
        public required byte[] Data { get; init; }
    }

    public class MediatrLargePayloadQueryHandler : MediatR.IRequestHandler<MediatrLargePayloadQuery, int>
    {
        public Task<int> Handle(MediatrLargePayloadQuery request, CancellationToken cancellationToken)
        {
            int checksum = 0;
            for (int i = 0; i < Math.Min(request.Data.Length, 1000); i++)
            {
                checksum ^= request.Data[i];
            }

            return Task.FromResult(checksum);
        }
    }
}
