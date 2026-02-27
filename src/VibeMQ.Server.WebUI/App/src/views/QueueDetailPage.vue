<template>
  <section class="section queue-detail">
    <nav class="breadcrumb">
      <router-link to="/" class="link">← Dashboard</router-link>
      <span class="breadcrumb-sep">/</span>
      <span class="mono">{{ queueName }}</span>
    </nav>
    <div class="queue-detail-actions">
      <button type="button" class="btn btn-danger" @click="askPurge" :disabled="!queueInfo || queueInfo.message_count === 0">Purge all</button>
      <button type="button" class="btn btn-danger" @click="askDeleteQueue">Delete queue</button>
    </div>
    <div class="card queue-info" v-if="queueInfo">
      <div class="queue-info-row">
        <span>Messages: {{ queueInfo.message_count }}</span>
        <span>Subscribers: {{ queueInfo.subscriber_count }}</span>
        <span>Delivery: {{ deliveryLabel(queueInfo.delivery_mode) }}</span>
        <span>Max size: {{ queueInfo.max_size }}</span>
      </div>
    </div>
    <div class="card table-wrap">
      <table v-if="queueMessages.length > 0">
        <thead>
          <tr>
            <th>Id</th>
            <th>Timestamp</th>
            <th>Priority</th>
            <th>Attempts</th>
            <th>Preview</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="msg in queueMessages" :key="msg.id">
            <td class="mono mono-small">{{ msg.id }}</td>
            <td class="mono">{{ formatDate(msg.timestamp) }}</td>
            <td>{{ msg.priority ?? '—' }}</td>
            <td>{{ msg.delivery_attempts ?? 0 }}</td>
            <td class="preview">{{ payloadPreview(msg.payload) }}</td>
            <td>
              <button type="button" class="btn btn-sm" @click="viewMessage(msg)">View</button>
              <button type="button" class="btn btn-sm btn-danger" @click="deleteMessage(msg)" :disabled="deletingMessageId === msg.id">{{ deletingMessageId === msg.id ? '…' : 'Delete' }}</button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-else-if="!loadingMessages" class="empty">No messages in this queue.</p>
      <p v-else class="empty">Loading…</p>
    </div>

    <message-modal v-if="messageModal" :message="messageModal" @close="closeMessageModal" />
    <confirm-modal
      v-if="confirmAction"
      :confirm-action="confirmAction"
      v-model:confirm-queue-name="confirmQueueName"
      @cancel="confirmCancel"
      @confirm="confirmOk"
    />
  </section>
</template>

<script setup lang="ts">
  import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
  import { useRoute, useRouter } from 'vue-router';
  import MessageModal from '../components/MessageModal.vue';
  import ConfirmModal from '../components/ConfirmModal.vue';
  import { deliveryLabel, formatDate, payloadPreview } from '../utils/format';
  import * as api from '../services/api';
  import type { QueueInfoResponse, QueueMessageResponse, ConfirmAction } from '../types/api';

  const POLL_INTERVAL_MS = 5000;

  const route = useRoute();
  const router = useRouter();
  const queueName = computed(() => (route.params.name as string) ?? '');

  const queueInfo = ref<QueueInfoResponse | null>(null);
  const queueMessages = ref<QueueMessageResponse[]>([]);
  const loadingMessages = ref(false);
  const deletingMessageId = ref<string | null>(null);
  const messageModal = ref<QueueMessageResponse | null>(null);
  const confirmAction = ref<ConfirmAction | null>(null);
  const confirmQueueName = ref('');
  let pollTimer: ReturnType<typeof setInterval> | null = null;

  async function fetchQueueDetail() {
    if (!queueName.value) return;
    loadingMessages.value = true;
    try {
      const [info, messages] = await Promise.all([
        api.getQueueInfo(queueName.value),
        api.getQueueMessages(queueName.value, 50, 0)
      ]);
      if (info) queueInfo.value = info;
      if (messages) queueMessages.value = messages;
    } finally {
      loadingMessages.value = false;
    }
  }

  function poll() {
    fetchQueueDetail();
  }

  watch(queueName, (name) => {
    if (!name) {
      queueInfo.value = null;
      queueMessages.value = [];
      return;
    }
    queueInfo.value = null;
    queueMessages.value = [];
    fetchQueueDetail();
  }, { immediate: true });

  function viewMessage(msg: QueueMessageResponse) {
    messageModal.value = msg;
  }

  function closeMessageModal() {
    messageModal.value = null;
  }

  function askPurge() {
    if (!queueName.value) return;
    confirmAction.value = { type: 'purge', queueName: queueName.value };
    confirmQueueName.value = '';
  }

  function askDeleteQueue() {
    if (!queueName.value) return;
    confirmAction.value = { type: 'deleteQueue', queueName: queueName.value };
    confirmQueueName.value = '';
  }

  function confirmCancel() {
    confirmAction.value = null;
    confirmQueueName.value = '';
  }

  async function confirmOk() {
    if (!confirmAction.value) return;
    const { type, queueName: name } = confirmAction.value;
    if (type === 'purge') {
      const ok = await api.purgeQueue(name);
      if (ok) await fetchQueueDetail();
    } else {
      const ok = await api.deleteQueue(name);
      if (ok) router.push('/');
    }
    confirmCancel();
  }

  onMounted(() => {
    pollTimer = setInterval(poll, POLL_INTERVAL_MS);
  });

  onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
  });

  async function deleteMessage(msg: QueueMessageResponse) {
    if (!queueName.value) return;
    deletingMessageId.value = msg.id;
    try {
      const ok = await api.deleteMessage(queueName.value, msg.id);
      if (ok) await fetchQueueDetail();
    } finally {
      deletingMessageId.value = null;
    }
  }
</script>

<style lang="scss">
  .queue-detail {
    .breadcrumb {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;

      &-sep {
        color: var(--text-secondary);
      }
    }

    &-actions {
      display: flex;
      gap: 0.5rem;
      margin-bottom: 1.5rem;
    }

    .queue-info {
      margin-bottom: 1.5rem;

      &-row {
        display: flex;
        flex-wrap: wrap;
        gap: 1rem;
        font-size: 0.9rem;
      }
    }

    .preview {
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: 0.85rem;
    }

    .mono-small {
      font-size: 0.75rem;
    }

    .btn {
      &-sm {
        padding: 0.25rem 0.5rem;
        font-size: 0.8rem;
        margin-right: 0.25rem;
      }

      &-danger {
        background: var(--error);

        &:hover {
          filter: brightness(1.1);
        }
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
