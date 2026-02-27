/**
 * API response types (snake_case to match backend JSON).
 */

export interface VersionResponse {
  server_version: string;
  webui_version: string;
}

export interface HealthResponse {
  is_healthy: boolean;
  status: string;
  active_connections: number;
  queue_count: number;
  in_flight_messages: number;
  total_messages_published: number;
  total_messages_delivered: number;
  memory_usage_mb: number;
  timestamp: string;
}

export interface MetricsResponse {
  total_messages_published: number;
  total_messages_delivered: number;
  total_messages_acknowledged: number;
  total_retries: number;
  total_dead_lettered: number;
  total_errors: number;
  total_connections_accepted: number;
  total_connections_rejected: number;
  active_connections: number;
  active_queues: number;
  in_flight_messages: number;
  memory_usage_bytes: number;
  average_delivery_latency_ms: number;
  timestamp: string;
  /** ISO duration string or seconds number */
  uptime: string | { seconds?: number };
}

export type DeliveryMode = 0 | 1 | 2 | 3;

export interface QueueInfoResponse {
  name: string;
  message_count: number;
  subscriber_count: number;
  delivery_mode: DeliveryMode;
  max_size: number;
  created_at: string;
  message_ttl?: string | null;
  enable_dead_letter_queue?: boolean;
  dead_letter_queue_name?: string | null;
  overflow_strategy?: number;
  max_retry_attempts?: number;
}

export interface QueueMessageResponse {
  id: string;
  queue_name: string;
  payload: unknown;
  timestamp: string;
  headers?: Record<string, string>;
  version?: number;
  priority?: number;
  delivery_attempts?: number;
}

export type ConfirmActionType = 'purge' | 'deleteQueue';

export interface ConfirmAction {
  type: ConfirmActionType;
  queueName: string;
}
