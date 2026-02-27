/**
 * Web UI API client. All methods return parsed JSON on success or null on failure.
 */

import type {
  VersionResponse,
  HealthResponse,
  MetricsResponse,
  QueueInfoResponse,
  QueueMessageResponse,
} from '../types/api';

const API_BASE = '/api';

async function request<T>(url: string, options?: RequestInit): Promise<T | null> {
  try {
    const r = await fetch(url, options);
    if (!r.ok) return null;
    return await r.json();
  } catch (e) {
    console.warn('API request failed', url, e);
    return null;
  }
}

export async function getVersion(): Promise<VersionResponse | null> {
  return request<VersionResponse>(`${API_BASE}/version`);
}

export async function getHealth(): Promise<HealthResponse | null> {
  return request<HealthResponse>(`${API_BASE}/health`);
}

export async function getMetrics(): Promise<MetricsResponse | null> {
  return request<MetricsResponse>(`${API_BASE}/metrics`);
}

export async function getQueueNames(): Promise<string[] | null> {
  const data = await request<string[]>(`${API_BASE}/queues`);
  return data ?? null;
}

export async function getQueueInfo(queueName: string): Promise<QueueInfoResponse | null> {
  return request<QueueInfoResponse>(`${API_BASE}/queues/${encodeURIComponent(queueName)}`);
}

export async function getQueueMessages(
  queueName: string,
  limit = 50,
  offset = 0
): Promise<QueueMessageResponse[] | null> {
  const params = new URLSearchParams({ limit: String(limit), offset: String(offset) });
  return request<QueueMessageResponse[]>(
    `${API_BASE}/queues/${encodeURIComponent(queueName)}/messages?${params}`
  );
}

export async function deleteMessage(
  queueName: string,
  messageId: string
): Promise<boolean> {
  try {
    const r = await fetch(
      `${API_BASE}/queues/${encodeURIComponent(queueName)}/messages/${encodeURIComponent(messageId)}`,
      { method: 'DELETE' }
    );
    return r.ok;
  } catch (e) {
    console.warn('Delete message failed', e);
    return false;
  }
}

export async function purgeQueue(queueName: string): Promise<boolean> {
  try {
    const r = await fetch(
      `${API_BASE}/queues/${encodeURIComponent(queueName)}/messages`,
      { method: 'DELETE' }
    );
    return r.ok;
  } catch (e) {
    console.warn('Purge queue failed', e);
    return false;
  }
}

export async function deleteQueue(queueName: string): Promise<boolean> {
  try {
    const r = await fetch(
      `${API_BASE}/queues/${encodeURIComponent(queueName)}`,
      { method: 'DELETE' }
    );
    return r.ok;
  } catch (e) {
    console.warn('Delete queue failed', e);
    return false;
  }
}
