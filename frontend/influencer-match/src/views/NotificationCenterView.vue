<template>
  <div class="container py-4" style="max-width: 980px;">
    <section class="hero mb-3">
      <div>
        <p class="kicker mb-1">Inbox</p>
        <h3 class="fw-bold mb-1">Notification Center</h3>
        <p class="mb-0 text-light-emphasis">Track assignments, system events, and activity updates in one stream.</p>
      </div>
      <div class="d-flex gap-2">
        <button class="btn btn-light btn-sm" @click="loadItems">Refresh</button>
        <button class="btn btn-outline-light btn-sm" @click="markAll" :disabled="!unreadCount">Mark all read</button>
      </div>
    </section>

    <div class="card border-0 shadow-sm panel-card mb-3">
      <div class="card-body d-flex flex-wrap align-items-center gap-2">
        <span class="small text-muted">Filter:</span>
        <button class="btn btn-sm" :class="unreadOnly ? 'btn-primary' : 'btn-outline-secondary'" @click="setUnreadOnly(true)">Unread</button>
        <button class="btn btn-sm" :class="!unreadOnly ? 'btn-primary' : 'btn-outline-secondary'" @click="setUnreadOnly(false)">All</button>
        <span class="small text-muted ms-auto">Unread: <strong>{{ unreadCount }}</strong></span>
      </div>
    </div>

    <div class="card border-0 shadow-sm panel-card">
      <div class="card-body p-0">
        <div v-if="loading" class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        <div v-else-if="items.length === 0" class="text-muted small p-4">No notifications found.</div>
        <div v-else class="list-group list-group-flush">
          <button
            v-for="item in items"
            :key="item.userNotificationId"
            class="list-group-item list-group-item-action py-3"
            :class="item.isRead ? '' : 'bg-light'"
            type="button"
            @click="markRead(item)">
            <div class="d-flex justify-content-between align-items-start gap-3">
              <div>
                <div class="fw-semibold">{{ item.title }}</div>
                <div class="small text-muted">{{ item.message }}</div>
                <div class="small text-muted mt-1">{{ item.type }}</div>
              </div>
              <div class="small text-muted text-nowrap">{{ fmt(item.createdAt) }}</div>
            </div>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import { fetchNotifications, markAllNotificationsRead, markNotificationRead } from '../services/notifications';

const items = ref([]);
const unreadCount = ref(0);
const unreadOnly = ref(false);
const loading = ref(false);

onMounted(loadItems);

async function loadItems() {
  loading.value = true;
  try {
    const data = await fetchNotifications({ unreadOnly: unreadOnly.value, take: 100 });
    items.value = data?.items || [];
    unreadCount.value = Number(data?.unreadCount || 0);
  } finally {
    loading.value = false;
  }
}

async function setUnreadOnly(value) {
  unreadOnly.value = value;
  await loadItems();
}

async function markRead(item) {
  if (item.isRead) return;
  await markNotificationRead(item.userNotificationId);
  item.isRead = true;
  unreadCount.value = Math.max(0, unreadCount.value - 1);
  if (unreadOnly.value) {
    items.value = items.value.filter(x => !x.isRead);
  }
}

async function markAll() {
  await markAllNotificationsRead();
  items.value = items.value.map(x => ({ ...x, isRead: true }));
  unreadCount.value = 0;
  if (unreadOnly.value) {
    items.value = [];
  }
}

function fmt(value) {
  if (!value) return '';
  return new Date(value).toLocaleString();
}
</script>

<style scoped>
.hero {
  border-radius: 20px;
  padding: 1.1rem;
  color: #e2e8f0;
  background: linear-gradient(124deg, #1e1b4b 0%, #1d4ed8 56%, #0891b2 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.22rem 0.56rem;
  font-size: 0.7rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(191, 219, 254, 0.25);
  color: #dbeafe;
}

.panel-card {
  border-radius: 16px;
}

@media (max-width: 768px) {
  .hero {
    flex-direction: column;
  }
}
</style>
