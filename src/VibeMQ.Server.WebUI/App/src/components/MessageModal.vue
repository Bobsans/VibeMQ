<template>
  <div class="message-modal">
    <modal v-model="isOpen" :title="title" close-label="Close message" size="large">
      <pre class="modal-payload">{{ messageBodyText }}</pre>
    </modal>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue';
  import Modal from './Modal.vue';
  import type { QueueMessageResponse } from '../types/api';

  const props = withDefaults(
    defineProps<{ message?: QueueMessageResponse | null }>(),
    { message: null }
  );

  const emit = defineEmits<{ close: [] }>();

  const isOpen = computed({
    get: () => !!props.message,
    set: (v) => {
      if (!v) emit('close');
    }
  });

  const title = computed(() =>
    props.message ? `Message ${props.message.id}` : ''
  );

  const messageBodyText = computed(() => {
    if (!props.message) return '';
    const p = props.message.payload;
    if (p == null) return '(empty)';
    if (typeof p === 'string') return p;
    try {
      return typeof p === 'object' ? JSON.stringify(p, null, 2) : String(p);
    } catch {
      return String(p);
    }
  });
</script>

<style lang="scss">
  .message-modal {
    .modal {
      &-payload {
        margin: 0;
        font-size: 0.8rem;
        white-space: pre-wrap;
        word-break: break-all;
      }
    }
  }
</style>
