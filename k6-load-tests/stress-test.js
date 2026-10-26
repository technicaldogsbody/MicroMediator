import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

export let options = {
  stages: [
    { duration: '1m', target: 50 },   // Warm up
    { duration: '3m', target: 100 },  // Normal load
    { duration: '2m', target: 200 },  // Peak load
    { duration: '2m', target: 500 },  // Stress test
    { duration: '1m', target: 0 },    // Cool down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed: ['rate<0.01'],
    errors: ['rate<0.1'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // Test 1: Get product by ID (cacheable query)
  let productRes = http.get(`${BASE_URL}/api/products/${randomInt(1, 100)}`);
  
  let productCheck = check(productRes, {
    'product status is 200': (r) => r.status === 200,
    'product response time < 200ms': (r) => r.timings.duration < 200,
  });
  
  errorRate.add(!productCheck);

  // Test 2: Search products (non-cacheable query)
  let searchRes = http.get(`${BASE_URL}/api/products/search?category=electronics&minPrice=50`);
  
  let searchCheck = check(searchRes, {
    'search status is 200': (r) => r.status === 200,
    'search response time < 500ms': (r) => r.timings.duration < 500,
  });
  
  errorRate.add(!searchCheck);

  // Test 3: Create order (command)
  let orderPayload = JSON.stringify({
    customerEmail: `test${randomInt(1, 10000)}@example.com`,
    items: [
      { productId: randomInt(1, 100), quantity: randomInt(1, 5) }
    ]
  });
  
  let orderRes = http.post(`${BASE_URL}/api/orders`, orderPayload, {
    headers: { 'Content-Type': 'application/json' },
  });
  
  let orderCheck = check(orderRes, {
    'order status is 201 or 400': (r) => r.status === 201 || r.status === 400,
    'order response time < 1000ms': (r) => r.timings.duration < 1000,
  });
  
  errorRate.add(!orderCheck && orderRes.status !== 400);

  // Test 4: Stream products (streaming query)
  let streamRes = http.get(`${BASE_URL}/api/products/stream?categoryFilter=electronics`);
  
  let streamCheck = check(streamRes, {
    'stream status is 200': (r) => r.status === 200,
    'stream response time < 2000ms': (r) => r.timings.duration < 2000,
  });
  
  errorRate.add(!streamCheck);

  sleep(randomInt(1, 3));
}

function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

export function handleSummary(data) {
  return {
    'summary.json': JSON.stringify(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options.indent || '';
  const enableColors = options.enableColors || false;
  
  let summary = `
${indent}Execution Summary
${indent}================
${indent}
${indent}Checks............: ${data.metrics.checks.values.passes}/${data.metrics.checks.values.passes + data.metrics.checks.values.fails}
${indent}Requests..........: ${data.metrics.http_reqs.values.count}
${indent}Request Duration..:
${indent}  p50: ${data.metrics.http_req_duration.values['p(50)']}ms
${indent}  p95: ${data.metrics.http_req_duration.values['p(95)']}ms
${indent}  p99: ${data.metrics.http_req_duration.values['p(99)']}ms
${indent}Request Failed....: ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%
${indent}Error Rate........: ${(data.metrics.errors.values.rate * 100).toFixed(2)}%
`;

  return summary;
}
