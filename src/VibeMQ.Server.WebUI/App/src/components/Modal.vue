<template>
  <div class="modal-overlay" v-if="modelValue" @click.self="$emit('update:modelValue', false)">
    <div class="modal" :class="{ 'modal-large': size === 'large' }">
      <div class="modal-header">
        <slot name="header">
          <h3 v-if="title">{{ title }}</h3>
        </slot>
        <button
          type="button"
          class="modal-close"
          :aria-label="closeLabel"
          @click="$emit('update:modelValue', false)"
        >
          ×
        </button>
      </div>
      <div class="modal-body" v-if="$slots.default">
        <slot />
      </div>
      <div class="modal-footer" v-if="$slots.footer">
        <slot name="footer" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  withDefaults(
    defineProps<{
      modelValue?: boolean;
      title?: string;
      closeLabel?: string;
      size?: 'default' | 'large';
    }>(),
    { modelValue: false, title: '', closeLabel: 'Close', size: 'default' }
  );

  defineEmits<{ 'update:modelValue': [value: boolean] }>();
</script>

<style lang="scss">
  .modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;

    .modal {
      background: var(--bg-card);
      border: 1px solid var(--border);
      border-radius: 12px;
      max-width: 90vw;
      max-height: 90vh;
      display: flex;
      flex-direction: column;

      &-large {
        min-width: 32rem;
        width: 85vw;
        max-width: 56rem;
        max-height: 85vh;
      }

      &-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 1rem 1.25rem;
        border-bottom: 1px solid var(--border);

        h3 {
          margin: 0;
          font-size: 1rem;
        }
      }

      &-close {
        background: none;
        border: none;
        font-size: 1.5rem;
        cursor: pointer;
        color: var(--text-secondary);
        padding: 0 0.25rem;
        line-height: 1;

        &:hover {
          color: var(--text-primary);
        }
      }

      &-body {
        padding: 1.25rem;
        overflow: auto;
        flex: 1;
      }

      &-footer {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
        padding: 1rem 1.25rem;
        border-top: 1px solid var(--border);
      }
    }
  }
</style>
