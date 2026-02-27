/**
 * Shared formatting and display helpers for the Web UI.
 */

import type { MetricsResponse } from '../types/api';

export function parseUptime(str: string): number {
  if (typeof str !== 'string') return 0;
  const withDays = str.match(/^(\d+)\.(\d+):(\d+):(\d+)/);
  if (withDays) {
    const [, d, h, m, s] = withDays.map(Number);
    return d * 86400 + h * 3600 + m * 60 + s;
  }
  const noDays = str.match(/^(\d+):(\d+):(\d+)/);
  if (noDays) {
    const [, h, m, s] = noDays.map(Number);
    return h * 3600 + m * 60 + s;
  }
  return 0;
}

const DELIVERY_LABELS: Record<number, string> = {
  0: 'RoundRobin',
  1: 'FanOutWithAck',
  2: 'FanOutWithoutAck',
  3: 'PriorityBased',
};

export function deliveryLabel(mode: number): string {
  return DELIVERY_LABELS[mode] ?? String(mode);
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    const d = new Date(iso);
    return d.toLocaleString();
  } catch {
    return String(iso);
  }
}

export function formatTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleTimeString();
  } catch {
    return String(iso);
  }
}

export function payloadPreview(payload: unknown): string {
  if (payload == null) return '—';
  const s =
    typeof payload === 'string'
      ? payload
      : typeof payload === 'object'
        ? JSON.stringify(payload)
        : String(payload);
  return s.length > 60 ? s.slice(0, 60) + '…' : s;
}

export function formatUptime(metrics: MetricsResponse | null | undefined): string {
  const u = metrics?.uptime;
  if (!u) return '—';
  const sec =
    typeof u === 'string' ? parseUptime(u) : (u as { seconds?: number }).seconds ?? 0;
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = Math.floor(sec % 60);
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}
