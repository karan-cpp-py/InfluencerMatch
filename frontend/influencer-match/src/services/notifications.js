import api from './api';

export async function fetchNotifications({ unreadOnly = false, take = 20 } = {}) {
  const { data } = await api.get('/notifications', { params: { unreadOnly, take } });
  return data;
}

export async function markNotificationRead(id) {
  await api.post(`/notifications/${id}/read`);
}

export async function markAllNotificationsRead() {
  await api.post('/notifications/read-all');
}
