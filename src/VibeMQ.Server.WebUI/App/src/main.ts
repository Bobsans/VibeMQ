import { createApp, h } from 'vue';
import { RouterView } from 'vue-router';
import { router } from './router';
import './styles.scss';

const app = createApp({
  render: () => h(RouterView),
});

app.config.errorHandler = (err: unknown, _instance, info: string) => {
  console.error('[VibeMQ] Vue error:', info, err);
};

app.use(router);

try {
  app.mount('#app');
} catch (err) {
  const message = err instanceof Error ? err.message : String(err);
  console.error('[VibeMQ] Mount failed:', err);
  const el = document.getElementById('app');
  if (el) el.innerHTML = `<p style="padding: 1rem; color: #ef4444;">Failed to start: ${message}</p>`;
}
