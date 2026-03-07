<template>
  <div class="dropdown" v-if="isAuthenticated">
    <button
      class="btn btn-sm btn-outline-light position-relative"
      data-bs-toggle="dropdown"
      @click="openPanel"
      type="button"
      aria-label="Notifications">
      Notifications
      <span v-if="unreadCount > 0" class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
        {{ unreadCount > 99 ? '99+' : unreadCount }}
      </span>
    </button>

    <div class="dropdown-menu dropdown-menu-end p-0 shadow" style="width: 360px; max-width: 92vw;">
      <div class="d-flex justify-content-between align-items-center px-3 py-2 border-bottom">
        <strong class="small">Notifications</strong>
        <div class="d-flex align-items-center gap-2">
          <router-link class="small text-decoration-none" to="/notifications">View all</router-link>
          <button class="btn btn-link btn-sm p-0 text-decoration-none" @click="markAll" type="button">Mark all read</button>
        </div>
      </div>

      <div v-if="loading" class="p-3 text-center">
        <div class="spinner-border spinner-border-sm text-primary"></div>
      </div>

      <div v-else-if="items.length === 0" class="p-3 small text-muted">No notifications yet.</div>

      <div v-else style="max-height: 360px; overflow: auto;">
        <button
          v-for="item in items"
          :key="item.userNotificationId"
          class="dropdown-item border-bottom py-2"
          :class="item.isRead ? '' : 'bg-light'
          "
          type="button"
          @click="markRead(item)">
          <div class="d-flex justify-content-between gap-2">
            <div class="small fw-semibold text-wrap">{{ item.title }}</div>
            <span class="small text-muted">{{ fmt(item.createdAt) }}</span>
          </div>
          <div class="small text-muted text-wrap">{{ item.message }}</div>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { authToken } from '../services/auth';
import { fetchNotifications, markAllNotificationsRead, markNotificationRead } from '../services/notifications';

const items = ref([]);
const unreadCount = ref(0);
const loading = ref(false);

const isAuthenticated = computed(() => Boolean(authToken.value));

async function openPanel() {
  if (!isAuthenticated.value) return;
  loading.value = true;
  try {
    const data = await fetchNotifications({ take: 20 });
    items.value = data.items || [];
    unreadCount.value = data.unreadCount || 0;
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  if (!isAuthenticated.value) return;
  try {
    const data = await fetchNotifications({ unreadOnly: true, take: 1 });
    unreadCount.value = data.unreadCount || 0;
  } catch {
    unreadCount.value = 0;
  }
});

async function markRead(item) {
  if (item.isRead) return;
  await markNotificationRead(item.userNotificationId);
  item.isRead = true;
  unreadCount.value = Math.max(0, unreadCount.value - 1);
}

async function markAll() {
  await markAllNotificationsRead();
  items.value = items.value.map(x => ({ ...x, isRead: true }));
  unreadCount.value = 0;
}

function fmt(date) {
  return new Date(date).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}
</script>
