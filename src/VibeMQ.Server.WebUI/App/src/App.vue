<template>
  <div class="layout">
    <header class="header">
      <h1 class="header-title">VibeMQ Dashboard</h1>
      <div class="header-actions">
        <span
          class="status-badge"
          :class="health ? (health.is_healthy ? 'status-badge-ok' : 'status-badge-error') : 'status-badge-loading'"
        >
          {{ statusLabel }}
        </span>
        <button
          type="button"
          class="theme-toggle"
          :aria-label="theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'"
          @click="toggleTheme"
        >
          {{ theme === 'dark' ? '☀️' : '🌙' }}
        </button>
        <span class="header-refresh">Refresh {{ pollInterval }}s</span>
      </div>
    </header>

    <main class="main">
      <router-view v-slot="{ Component }">
        <component :is="Component" :key="route.path" />
      </router-view>
    </main>

    <footer class="footer">
      <span class="footer-text">VibeMQ Web UI</span>
      <span v-if="version" class="footer-text footer-version">Server {{ version.server_version }} · Web UI {{ version.webui_version }}</span>
      <span v-if="health?.timestamp" class="footer-text mono">Last update: {{ formatTime(health.timestamp) }}</span>
    </footer>
  </div>
</template>

<script setup lang="ts">
  import { ref, computed, onMounted, onUnmounted } from 'vue';
  import { useRoute } from 'vue-router';
  import { formatTime } from './utils/format';
  import * as api from './services/api';
  import type { HealthResponse, VersionResponse } from './types/api';

  const pollInterval = 5;

  const route = useRoute();

  const theme = ref<'dark' | 'light'>('dark');
  const health = ref<HealthResponse | null>(null);
  const version = ref<VersionResponse | null>(null);
  let pollTimer: ReturnType<typeof setInterval> | null = null;

  const statusLabel = computed(() => {
    if (!health.value) return 'Loading…';
    return health.value.is_healthy ? 'Healthy' : 'Unhealthy';
  });

  function toggleTheme() {
    theme.value = theme.value === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', theme.value);
  }

  async function fetchVersion() {
    const data = await api.getVersion();
    if (data) version.value = data;
  }

  async function fetchHealth() {
    const data = await api.getHealth();
    if (data) health.value = data;
  }

  function poll() {
    fetchHealth();
  }

  function initialLoad() {
    fetchVersion();
    fetchHealth();
  }

  onMounted(() => {
    document.documentElement.setAttribute('data-theme', theme.value);
    initialLoad();
    pollTimer = setInterval(poll, pollInterval * 1000);
  });

  onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
  });
</script>

<style lang="scss">
  .layout {
    max-width: 1200px;
    margin: 0 auto;
    padding: 1.5rem;

    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 1rem;
      margin-bottom: 1.5rem;

      &-title {
        font-size: 1.5rem;
        font-weight: 700;
        color: var(--text-primary);
      }

      &-actions {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }

      &-refresh {
        font-size: 0.75rem;
        color: var(--text-secondary);
      }
    }

    .status-badge {
      padding: 0.35rem 0.75rem;
      border-radius: 8px;
      font-size: 0.8rem;
      font-weight: 600;

      &-ok {
        background: rgba(34, 197, 94, 0.2);
        color: var(--success);
      }

      &-error {
        background: rgba(239, 68, 68, 0.2);
        color: var(--error);
      }

      &-loading {
        background: var(--bg-card);
        color: var(--text-secondary);
      }
    }

    .theme-toggle {
      padding: 0.4rem 0.6rem;
      background: var(--bg-card);
      border: 1px solid var(--border);
      font-size: 1.1rem;

      &:hover {
        background: var(--bg-secondary);
      }
    }

    .main {
      min-height: 20vh;
    }

    .footer {
      display: flex;
      justify-content: space-between;
      padding-top: 1rem;
      border-top: 1px solid var(--border);
      font-size: 0.8rem;
      color: var(--text-secondary);

      &-text.mono {
        font-size: 0.75rem;
      }

      &-version {
        margin-left: 0.5rem;
        padding-left: 0.5rem;
      }
    }
  }
</style>
