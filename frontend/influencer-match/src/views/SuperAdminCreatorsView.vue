<template>
  <div class="container py-4" style="max-width: 1240px;">
    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
      <div>
        <h3 class="fw-bold mb-0">Creator Profile Management</h3>
        <small class="text-muted">SuperAdmin actions: view, update, AI refresh, delete profile.</small>
      </div>
      <div class="d-flex gap-2">
        <router-link to="/admin" class="btn btn-outline-secondary btn-sm">Back to Admin</router-link>
        <button class="btn btn-primary btn-sm" @click="loadList" :disabled="loadingList">Refresh</button>
      </div>
    </div>

    <div class="card border-0 shadow-sm mb-3">
      <div class="card-body row g-2 align-items-end">
        <div class="col-md-6">
          <label class="form-label small mb-1">Search</label>
          <input v-model.trim="filters.query" class="form-control" placeholder="Name, email, category, channel" @keyup.enter="loadList" />
        </div>
        <div class="col-md-2">
          <label class="form-label small mb-1">Page Size</label>
          <select v-model.number="filters.pageSize" class="form-select" @change="loadList">
            <option :value="10">10</option>
            <option :value="25">25</option>
            <option :value="50">50</option>
          </select>
        </div>
        <div class="col-md-2">
          <button class="btn btn-outline-primary w-100" @click="applySearch" :disabled="loadingList">Search</button>
        </div>
      </div>
    </div>

    <div v-if="error" class="alert alert-danger">{{ error }}</div>

    <div class="card border-0 shadow-sm">
      <div class="table-responsive">
        <table class="table table-sm align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th>Creator</th>
              <th>Type</th>
              <th>Channel</th>
              <th>AI Health</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loadingList">
              <td colspan="5" class="text-center py-4 text-muted">Loading creators…</td>
            </tr>
            <tr v-else-if="items.length === 0">
              <td colspan="5" class="text-center py-4 text-muted">No creator profiles found.</td>
            </tr>
            <tr v-for="row in items" :key="row.creatorProfileId">
              <td>
                <div class="fw-semibold">{{ row.userName }}</div>
                <div class="small text-muted">{{ row.userEmail }}</div>
                <div class="small text-muted">{{ row.country || '—' }} · {{ row.language || '—' }} · {{ row.category || '—' }}</div>
              </td>
              <td>
                <span class="badge bg-dark">{{ row.role }}</span>
                <div class="small text-muted mt-1">{{ row.customerType }}</div>
              </td>
              <td>
                <div class="fw-semibold">{{ row.channelName || 'Unlinked' }}</div>
                <div class="small text-muted">{{ row.channelId || '—' }}</div>
                <div class="small text-muted" v-if="row.subscribers != null">{{ compact(row.subscribers) }} subs · {{ percent(row.engagementRate) }}</div>
              </td>
              <td>
                <span class="badge" :class="aiBadgeClass(row.aiScore)">{{ aiLabel(row.aiScore) }}</span>
                <div class="small text-muted mt-1" v-if="row.creatorLastRefreshedAt">{{ fmtDate(row.creatorLastRefreshedAt) }}</div>
              </td>
              <td>
                <div class="d-flex flex-wrap gap-1">
                  <button class="btn btn-outline-secondary btn-sm" @click="viewDetail(row.creatorProfileId)">View</button>
                  <button class="btn btn-outline-primary btn-sm" @click="editProfile(row)">Edit</button>
                  <button class="btn btn-outline-info btn-sm" @click="refreshAi(row)" :disabled="refreshingAiId === row.creatorProfileId">
                    {{ refreshingAiId === row.creatorProfileId ? 'Refreshing...' : 'Refresh AI' }}
                  </button>
                  <button class="btn btn-outline-danger btn-sm" @click="deleteProfile(row)" :disabled="deletingId === row.creatorProfileId">
                    {{ deletingId === row.creatorProfileId ? 'Deleting...' : 'Delete' }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="card-footer d-flex justify-content-between align-items-center">
        <small class="text-muted">Total: {{ total }}</small>
        <div class="d-flex align-items-center gap-2">
          <button class="btn btn-sm btn-outline-secondary" :disabled="filters.page <= 1 || loadingList" @click="changePage(filters.page - 1)">Prev</button>
          <small>Page {{ filters.page }}</small>
          <button class="btn btn-sm btn-outline-secondary" :disabled="filters.page * filters.pageSize >= total || loadingList" @click="changePage(filters.page + 1)">Next</button>
        </div>
      </div>
    </div>

    <div v-if="detail" class="card border-0 shadow-sm mt-3">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-center mb-2">
          <h6 class="fw-semibold mb-0">Profile Detail</h6>
          <button class="btn-close" @click="detail = null"></button>
        </div>
        <div class="row g-3">
          <div class="col-md-6">
            <div class="small text-muted">User</div>
            <div class="fw-semibold">{{ detail.profile.userName }} ({{ detail.profile.userEmail }})</div>
            <div class="small">{{ detail.profile.role }} · {{ detail.profile.customerType }}</div>
          </div>
          <div class="col-md-6">
            <div class="small text-muted">Profile</div>
            <div class="small">{{ detail.profile.country || '—' }} · {{ detail.profile.language || '—' }} · {{ detail.profile.category || '—' }}</div>
          </div>
          <div class="col-12" v-if="detail.recentVideos?.length">
            <div class="small text-muted mb-1">Recent Videos</div>
            <div class="d-flex flex-column gap-2">
              <div v-for="v in detail.recentVideos.slice(0,5)" :key="v.youtubeVideoId" class="border rounded p-2 d-flex justify-content-between align-items-center">
                <div>
                  <div class="small fw-semibold">{{ v.title }}</div>
                  <div class="small text-muted">{{ compact(v.viewCount) }} views · {{ fmtDate(v.publishedAt) }}</div>
                </div>
                <router-link :to="`/creator/${detail.creator?.creatorId}/latest-video-analysis?videoId=${v.youtubeVideoId}&videoTitle=${encodeURIComponent(v.title || '')}`" class="btn btn-sm btn-outline-dark" v-if="detail.creator?.creatorId">
                  Analyze with AI
                </router-link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="editing" class="card border-0 shadow-sm mt-3">
      <div class="card-body">
        <h6 class="fw-semibold mb-3">Edit Creator Profile</h6>
        <div v-if="formError" class="alert alert-warning py-2">{{ formError }}</div>
        <div class="row g-2">
          <div class="col-md-4"><input v-model="editing.userName" class="form-control" placeholder="Name" /></div>
          <div class="col-md-4"><input v-model="editing.userEmail" class="form-control" placeholder="Email" /></div>
          <div class="col-md-4"><input v-model="editing.country" class="form-control" placeholder="Country" /></div>
          <div class="col-md-4"><input v-model="editing.language" class="form-control" placeholder="Language" /></div>
          <div class="col-md-4"><input v-model="editing.category" class="form-control" placeholder="Category" /></div>
          <div class="col-md-4"><input v-model="editing.contactEmail" class="form-control" placeholder="Contact Email" /></div>
          <div class="col-md-12"><textarea v-model="editing.bio" class="form-control" rows="2" placeholder="Bio"></textarea></div>
        </div>
        <div class="d-flex gap-2 mt-3">
          <button class="btn btn-primary btn-sm" @click="saveEdit" :disabled="savingEdit">{{ savingEdit ? 'Saving...' : 'Save' }}</button>
          <button class="btn btn-outline-secondary btn-sm" @click="editing = null">Cancel</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref, reactive } from 'vue';
import api from '../services/api';

const items = ref([]);
const total = ref(0);
const loadingList = ref(false);
const error = ref('');
const detail = ref(null);
const formError = ref('');

const refreshingAiId = ref(null);
const deletingId = ref(null);
const savingEdit = ref(false);
const editing = ref(null);

const filters = reactive({
  query: '',
  page: 1,
  pageSize: 25
});

function compact(n) {
  const v = Number(n || 0);
  if (!Number.isFinite(v) || v <= 0) return '0';
  if (v >= 1_000_000) return `${(v / 1_000_000).toFixed(1)}M`;
  if (v >= 1_000) return `${(v / 1_000).toFixed(1)}K`;
  return String(Math.round(v));
}

function percent(v) {
  const n = Number(v || 0);
  const ratio = n > 1 ? n / 100 : n;
  return `${(ratio * 100).toFixed(2)}%`;
}

function fmtDate(v) {
  if (!v) return '—';
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString();
}

function aiLabel(score) {
  const n = Number(score || 0);
  if (n >= 70) return `Strong (${n.toFixed(1)})`;
  if (n >= 45) return `Average (${n.toFixed(1)})`;
  if (n > 0) return `Low (${n.toFixed(1)})`;
  return 'Not scored';
}

function aiBadgeClass(score) {
  const n = Number(score || 0);
  if (n >= 70) return 'bg-success';
  if (n >= 45) return 'bg-warning text-dark';
  if (n > 0) return 'bg-secondary';
  return 'bg-light text-dark border';
}

async function loadList() {
  loadingList.value = true;
  error.value = '';
  try {
    const { data } = await api.get('/admin/creator-profiles', {
      params: {
        query: filters.query || undefined,
        page: filters.page,
        pageSize: filters.pageSize
      }
    });
    items.value = Array.isArray(data?.items) ? data.items : [];
    total.value = Number(data?.total || 0);
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to load creator profiles.';
  } finally {
    loadingList.value = false;
  }
}

function applySearch() {
  filters.page = 1;
  loadList();
}

function changePage(page) {
  filters.page = page;
  loadList();
}

async function viewDetail(creatorProfileId) {
  try {
    const { data } = await api.get(`/admin/creator-profiles/${creatorProfileId}`);
    detail.value = data;
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to load creator detail.';
  }
}

function editProfile(row) {
  formError.value = '';
  editing.value = {
    creatorProfileId: row.creatorProfileId,
    userName: row.userName || '',
    userEmail: row.userEmail || '',
    country: row.country || '',
    language: row.language || '',
    category: row.category || '',
    contactEmail: '',
    bio: ''
  };
}

async function saveEdit() {
  if (!editing.value) return;
  formError.value = '';

  const name = String(editing.value.userName || '').trim();
  const email = String(editing.value.userEmail || '').trim();
  const contactEmail = String(editing.value.contactEmail || '').trim();

  if (name.length < 2) {
    formError.value = 'Name must be at least 2 characters.';
    return;
  }

  if (email) {
    const ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    if (!ok) {
      formError.value = 'Please enter a valid user email address.';
      return;
    }
  }

  if (contactEmail) {
    const ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contactEmail);
    if (!ok) {
      formError.value = 'Please enter a valid contact email address.';
      return;
    }
  }

  if (String(editing.value.bio || '').length > 1000) {
    formError.value = 'Bio is too long. Keep it under 1000 characters.';
    return;
  }

  savingEdit.value = true;
  try {
    await api.put(`/admin/creator-profiles/${editing.value.creatorProfileId}`, {
      userName: editing.value.userName,
      userEmail: editing.value.userEmail,
      country: editing.value.country,
      language: editing.value.language,
      category: editing.value.category,
      contactEmail: editing.value.contactEmail,
      bio: editing.value.bio
    });
    editing.value = null;
    await loadList();
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to save creator profile.';
  } finally {
    savingEdit.value = false;
  }
}

async function refreshAi(row) {
  refreshingAiId.value = row.creatorProfileId;
  try {
    await api.post(`/admin/creator-profiles/${row.creatorProfileId}/refresh-ai`);
    await loadList();
    if (detail.value?.profile?.creatorProfileId === row.creatorProfileId) {
      await viewDetail(row.creatorProfileId);
    }
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to refresh AI signals.';
  } finally {
    refreshingAiId.value = null;
  }
}

async function deleteProfile(row) {
  const ok = window.confirm(`Delete creator profile for ${row.userName}? This removes creator profile/channel data.`);
  if (!ok) return;

  deletingId.value = row.creatorProfileId;
  try {
    await api.delete(`/admin/creator-profiles/${row.creatorProfileId}`);
    if (detail.value?.profile?.creatorProfileId === row.creatorProfileId) {
      detail.value = null;
    }
    await loadList();
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to delete creator profile.';
  } finally {
    deletingId.value = null;
  }
}

onMounted(loadList);
</script>
