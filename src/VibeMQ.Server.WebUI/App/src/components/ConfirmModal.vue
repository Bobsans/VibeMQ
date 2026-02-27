<template>
  <div class="confirm-modal">
    <modal v-model="isOpen" :title="modalTitle" close-label="Cancel">
      <template #default>
      <p v-if="confirmAction?.type === 'purge'">
        All messages in <strong>{{ confirmAction?.queueName }}</strong> will be removed. This cannot be undone.
      </p>
      <p v-else-if="confirmAction?.type === 'deleteQueue'">
        Queue <strong>{{ confirmAction?.queueName }}</strong> and all its messages will be deleted. This cannot be undone.
      </p>
      <p v-if="confirmAction?.type === 'deleteQueue'">
        <label>
          Type queue name to confirm:
          <input
            v-model="localConfirmName"
            class="input"
            :placeholder="confirmAction?.queueName"
          />
        </label>
      </p>
    </template>
    <template #footer>
      <button type="button" class="btn secondary" @click="cancel">Cancel</button>
      <button
        type="button"
        class="btn btn-danger"
        :disabled="confirmAction?.type === 'deleteQueue' && localConfirmName !== confirmAction?.queueName"
        @click="confirm"
      >
        Confirm
      </button>
    </template>
    </modal>
  </div>
</template>

<script setup lang="ts">
  import { computed, watch } from 'vue';
  import Modal from './Modal.vue';
  import type { ConfirmAction } from '../types/api';

  const props = withDefaults(
    defineProps<{
      confirmAction?: ConfirmAction | null;
      confirmQueueName?: string;
    }>(),
    { confirmAction: null, confirmQueueName: '' }
  );

  const emit = defineEmits<{
    cancel: [];
    confirm: [];
    'update:confirmQueueName': [value: string];
  }>();

  const isOpen = computed({
    get: () => !!props.confirmAction,
    set: (v) => {
      if (!v) emit('cancel');
    }
  });

  const localConfirmName = computed({
    get: () => props.confirmQueueName,
    set: (v) => emit('update:confirmQueueName', v)
  });

  watch(() => props.confirmAction, (action) => {
    if (action) emit('update:confirmQueueName', '');
  });

  const modalTitle = computed(() => {
    if (!props.confirmAction) return '';
    return props.confirmAction.type === 'purge' ? 'Purge queue?' : 'Delete queue?';
  });

  function cancel() {
    emit('cancel');
  }

  function confirm() {
    emit('confirm');
  }
</script>

<style lang="scss">
  .confirm-modal {
    .input {
      font-family: inherit;
      padding: 0.35rem 0.5rem;
      border: 1px solid var(--border);
      border-radius: 6px;
      background: var(--bg-primary);
      color: var(--text-primary);
      margin-left: 0.5rem;
      min-width: 180px;
    }
  }
</style>
