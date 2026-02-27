import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import App from './App.vue';
import DashboardPage from './views/DashboardPage.vue';
import QueueDetailPage from './views/QueueDetailPage.vue';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: App,
    children: [
      { path: '', component: DashboardPage, name: 'dashboard' },
      { path: 'queues/:name', component: QueueDetailPage, name: 'queue' },
    ],
  },
];

export const router = createRouter({
  history: createWebHistory('/'),
  routes,
});
