#!/usr/bin/env bash
set -euo pipefail

base_url="${BASE_URL:-http://localhost:8080}"
health_status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' "${base_url}/api/v1/health")"
ready_status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' "${base_url}/api/v1/ready")"
test "${health_status}" = 200
printf 'health=%s ready=%s\n' "${health_status}" "${ready_status}"

if test "${ready_status}" = 200; then
  curl --fail --silent --show-error "${base_url}/api/v1/order-book" >/dev/null
else
  test "${ready_status}" = 503
fi
