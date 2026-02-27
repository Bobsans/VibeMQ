<template>
  <div class="dashboard-page">
    <section class="cards">
      <div class="card card-metric">
        <div class="card-label">Connections</div>
        <div class="card-value mono">{{ health?.active_connections ?? '—' }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Queues</div>
        <div class="card-value mono">{{ health?.queue_count ?? '—' }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">In-flight</div>
        <div class="card-value mono">{{ health?.in_flight_messages ?? '—' }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Memory</div>
        <div class="card-value mono">{{ memoryMb }} MB</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Uptime</div>
        <div class="card-value mono">{{ uptimeFormatted }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Published</div>
        <div class="card-value mono">{{ metrics?.total_messages_published ?? '—' }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Delivered</div>
        <div class="card-value mono">{{ metrics?.total_messages_delivered ?? '—' }}</div>
      </div>
      <div class="card card-metric">
        <div class="card-label">Avg latency</div>
        <div class="card-value mono">{{ latencyMs }} ms</div>
      </div>
    </section>

    <section class="section">
      <h2 class="section-title">Queues</h2>
      <div class="card table-wrap">
        <table v-if="queues.length > 0">
          <thead>
            <tr>
              <th>Name</th>
              <th>Messages</th>
              <th>Subscribers</th>
              <th>Delivery</th>
              <th>Max size</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="q in queues" :key="q.name">
              <td class="mono">
                <router-link :to="'/queues/' + encodeURIComponent(q.name)" class="link">{{ q.name }}</router-link>
              </td>
              <td>{{ q.message_count }}</td>
              <td>{{ q.subscriber_count }}</td>
              <td><span class="badge badge-success">{{ deliveryLabel(q.delivery_mode) }}</span></td>
              <td>{{ q.max_size }}</td>
              <td class="mono">{{ formatDate(q.created_at) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else-if="!loadingQueues && queues.length === 0" class="empty">No queues yet.</p>
        <p v-else class="empty">Loading…</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
  import { ref, computed, onMounted, onUnmounted } from 'vue';
  import { deliveryLabel, formatDate, formatUptime } from '../utils/format';
  import * as api from '../services/api';
  import type { HealthResponse, MetricsResponse, QueueInfoResponse } from '../types/api';

  const POLL_INTERVAL_MS = 5000;

  const health = ref<HealthResponse | null>(null);
  const metrics = ref<MetricsResponse | null>(null);
  const queues = ref<QueueInfoResponse[]>([]);
  const loadingQueues = ref(false);
  let pollTimer: ReturnType<typeof setInterval> | null = null;

  async function fetchDashboard() {
    const [healthData, metricsData] = await Promise.all([
      api.getHealth(),
      api.getMetrics()
    ]);
    if (healthData) health.value = healthData;
    if (metricsData) metrics.value = metricsData;
  }

  async function fetchQueues() {
    loadingQueues.value = true;
    try {
      const names = await api.getQueueNames();
      const list: QueueInfoResponse[] = [];
      for (const name of names ?? []) {
        const info = await api.getQueueInfo(name);
        if (info) list.push(info);
      }
      queues.value = list;
    } finally {
      loadingQueues.value = false;
    }
  }

  function poll() {
    fetchDashboard();
    fetchQueues();
  }

  onMounted(() => {
    poll();
    pollTimer = setInterval(poll, POLL_INTERVAL_MS);
  });

  onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
  });

  const memoryMb = computed(() => {
    if (health.value?.memory_usage_mb != null) return health.value.memory_usage_mb;
    if (metrics.value?.memory_usage_bytes != null) return Math.round(metrics.value.memory_usage_bytes / 1024 / 1024);
    return '—';
  });

  const uptimeFormatted = computed(() => formatUptime(metrics.value));

  const latencyMs = computed(() => {
    const v = metrics.value?.average_delivery_latency_ms;
    if (v == null) return '—';
    return typeof v === 'number' ? v.toFixed(2) : v;
  });
</script>

<style lang="scss">
  .dashboard-page {
    margin-bottom: 2rem;

    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 1rem;
      margin-bottom: 2rem;
    }

    .card {
      &-metric {
        text-align: center;
      }

      &-label {
        font-size: 0.7rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--text-secondary);
        margin-bottom: 0.35rem;
      }

      &-value {
        font-size: 1.25rem;
        font-weight: 600;
      }
    }

    .section {
      margin-bottom: 2rem;

      &-title {
        font-size: 1.1rem;
        margin-bottom: 0.75rem;
        color: var(--text-primary);
      }
    }

    .empty {
      color: var(--text-secondary);
      padding: 1.5rem;
      margin: 0;
    }

    .link {
      background: none;
      color: var(--accent);
      padding: 0;
      font-size: inherit;
      text-decoration: underline;
      cursor: pointer;

      &:hover {
        color: var(--accent-dim);
      }
    }
  }
</style>
