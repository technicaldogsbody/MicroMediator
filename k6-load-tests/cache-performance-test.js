import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

export let options = {
  vus: 10,
  duration: '10m',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
    errors: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  // Focus on cacheable queries to test cache performance
  const productId = Math.floor(Math.random() * 10) + 1; // Only 10 different products
  
  let res = http.get(`${BASE_URL}/api/products/${productId}`);
  
  let success = check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 100ms': (r) => r.timings.duration < 100, // Should be fast due to caching
    'has product data': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.id === productId;
      } catch {
        return false;
      }
    },
  });
  
  errorRate.add(!success);
}
