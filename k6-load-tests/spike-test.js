import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 1000 },  // Ramp up to 1000 users
    { duration: '1m', target: 1000 },   // Stay at 1000 users
    { duration: '30s', target: 0 },     // Ramp down
  ],
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export default function () {
  let res = http.get(`${BASE_URL}/api/products/${Math.floor(Math.random() * 100) + 1}`);
  
  check(res, {
    'status is 200': (r) => r.status === 200,
  });
  
  sleep(0.1);
}
