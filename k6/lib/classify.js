export function classifyResponse(response) {
  if (!response || response.status === 0) return { outcome: 'transport_failure', statusClass: 'transport' };
  if (response.status === 201) return { outcome: 'accepted', statusClass: '2xx' };
  if (response.status === 200) return { outcome: 'replayed', statusClass: '2xx' };
  if (response.status === 409) return { outcome: 'rejected/conflict', statusClass: '4xx' };
  if (response.status === 429) return { outcome: 'backpressure', statusClass: '4xx' };
  if (response.status === 503) return { outcome: 'unavailable', statusClass: '5xx' };
  return { outcome: 'unexpected', statusClass: `${Math.floor(response.status / 100)}xx` };
}
