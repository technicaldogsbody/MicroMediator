# K6 Load Tests for MicroMediator

Battle testing scripts to verify production readiness.

## Prerequisites

Install K6:
```bash
# Windows (using Chocolatey)
choco install k6

# macOS
brew install k6

# Linux
wget https://github.com/grafana/k6/releases/download/v0.48.0/k6-v0.48.0-linux-amd64.tar.gz
tar -xzf k6-v0.48.0-linux-amd64.tar.gz
sudo cp k6-v0.48.0-linux-amd64/k6 /usr/local/bin/
```

## Running Tests

First, start the example API:
```bash
cd ../src/TechnicalDogsbody.MicroMediator/TechnicalDogsbody.MicroMediator.Examples
dotnet run -c Release
```

Then run the tests:

### Stress Test (Progressive Load)
Tests system behaviour under increasing load (50 → 500 concurrent users).

```bash
k6 run stress-test.js
```

**Success Criteria:**
- p95 latency < 500ms
- p99 latency < 1000ms
- Error rate < 1%

### Cache Performance Test
Tests caching effectiveness with repeated requests.

```bash
k6 run cache-performance-test.js
```

**Success Criteria:**
- p95 latency < 100ms (cached)
- Error rate < 1%
- Memory stable over 10 minutes

### Spike Test
Tests system recovery from sudden traffic spike.

```bash
k6 run spike-test.js
```

**Success Criteria:**
- No errors during spike
- Quick recovery after spike
- No memory leaks

## Custom Configuration

Override base URL:
```bash
k6 run --env BASE_URL=http://localhost:8080 stress-test.js
```

Adjust duration:
```bash
k6 run --stage 5m:100,10m:100,5m:0 stress-test.js
```

## Monitoring

View real-time metrics:
```bash
k6 run --out json=results.json stress-test.js
```

View in browser (requires K6 Cloud or Grafana):
```bash
k6 run --out cloud stress-test.js
```

## Expected Results

**MicroMediator should maintain:**
- Consistent sub-500ms p95 latency under 500 concurrent users
- Zero memory growth over 10+ minute runs
- <1% error rate under normal conditions
- Graceful degradation under extreme load (1000+ users)

## Troubleshooting

If tests fail:

1. Check API is running: `curl http://localhost:5000/api/products/1`
2. Verify no memory constraints: Watch Task Manager during test
3. Check logs: API should show minimal errors
4. Reduce VUs if system overloaded: `k6 run --vus 10 stress-test.js`

## Next Steps

After K6 tests pass:
1. Run BenchmarkDotNet suite: `cd ../benchmarks && dotnet run -c Release`
2. Memory profiling: Use dotMemory or PerfView
3. Production deployment: Azure Functions or Kubernetes
