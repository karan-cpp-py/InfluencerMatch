<template>
  <div>
    <div class="container-fluid px-4 py-4" style="max-width:1400px">

      <!-- Header -->
      <div class="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h2 class="fw-bold mb-1">Creator Discovery</h2>
          <p class="text-muted mb-0">Find and track YouTube creators by niche</p>
        </div>
        <button class="btn btn-outline-secondary btn-sm" @click="fetchAll" :disabled="loading">
          <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
          Refresh
        </button>
      </div>

      <!-- API key warning -->
      <div v-if="stats && stats.youtubeConfigured === false" class="alert alert-warning d-flex align-items-center mb-4">
        <i class="bi bi-exclamation-triangle-fill me-2"></i>
        <div>YouTube API key is not configured. Set <strong>YouTube:ApiKey</strong> in <code>appsettings.json</code> to enable live discovery.</div>
      </div>

      <!-- Stat cards -->
      <div class="row g-3 mb-4" v-if="stats">
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
              <div class="text-muted small mb-1">Total Creators</div>
              <div class="fs-3 fw-bold text-primary">{{ fmtNum(stats.total) }}</div>
            </div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
              <div class="text-muted small mb-1">Added Today</div>
              <div class="fs-3 fw-bold text-success">+{{ fmtNum(stats.addedToday) }}</div>
            </div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
              <div class="text-muted small mb-1">Total Subscribers</div>
              <div class="fs-3 fw-bold text-danger">{{ fmtCompact(stats.totalSubscribers) }}</div>
            </div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
              <div class="text-muted small mb-2">Top Categories</div>
              <div class="d-flex flex-wrap gap-1">
                <span v-for="cat in stats.topCategories" :key="cat.category"
                  class="badge rounded-pill"
                  :style="catBadgeStyle(cat.category)"
                  style="font-size:0.7rem; cursor:pointer"
                  @click="filterCategory = cat.category; fetchCreators()">
                  {{ cat.category }} <span class="opacity-75">({{ cat.count }})</span>
                </span>
                <span v-if="!stats.topCategories || stats.topCategories.length === 0" class="text-muted small">—</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Discovery form -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body">
          <h6 class="fw-semibold mb-3">Run Discovery</h6>
          <div class="row g-2 align-items-end">
            <div class="col-md-6">
              <label class="form-label small text-muted">Keyword</label>
              <input v-model="keyword" class="form-control" placeholder="e.g. fitness, tech, cooking" @keyup.enter="startDiscovery" />
            </div>
            <div class="col-md-auto">
              <button class="btn btn-primary px-4" @click="startDiscovery" :disabled="loading || !keyword">
                <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
                Discover Creators
              </button>
            </div>
            <div class="col-md-auto">
              <button class="btn btn-outline-secondary" @click="keyword=''; startDiscovery()" :disabled="loading" title="Run batch discovery on all default keywords">
                Run Batch (8 keywords)
              </button>
            </div>
          </div>
          <div v-if="message" class="mt-3">
            <div :class="['alert py-2 mb-0', messageType === 'error' ? 'alert-danger' : 'alert-success']">{{ message }}</div>
          </div>
        </div>
      </div>

      <!-- Creators table -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-bottom py-3">
          <div class="row g-2 align-items-center">
            <div class="col-md-4">
              <input v-model="search" class="form-control form-control-sm" placeholder="Search creators…" @input="debounceSearch" />
            </div>
            <div class="col-md-3">
              <select v-model="filterCategory" class="form-select form-select-sm" @change="fetchCreators">
                <option value="">All categories</option>
                <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <select v-model="sortBy" class="form-select form-select-sm" @change="fetchCreators">
                <option value="subscribers">Sort: Subscribers</option>
                <option value="views">Sort: Total Views</option>
                <option value="videos">Sort: Videos</option>
                <option value="newest">Sort: Newest</option>
              </select>
            </div>
            <div class="col-md-2 text-end">
              <span class="text-muted small">{{ fmtNum(totalCount) }} results</span>
            </div>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th class="ps-3">#</th>
                <th>Channel</th>
                <th>Category</th>
                <th class="text-end">Subscribers</th>
                <th class="text-end">Total Views</th>
                <th class="text-end">Videos</th>
                <th class="text-center">Platform</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="tableLoading">
                <td colspan="8" class="text-center py-5">
                  <div class="spinner-border text-primary"></div>
                </td>
              </tr>
              <tr v-else-if="creators.length === 0">
                <td colspan="8" class="text-center py-5 text-muted">
                  No creators found. Run discovery to populate the database.
                </td>
              </tr>
              <tr v-for="(c, idx) in creators" :key="c.creatorId" v-else>
                <td class="ps-3 text-muted small">{{ (page - 1) * pageSize + idx + 1 }}</td>
                <td>
                  <div class="d-flex align-items-center gap-2">
                    <div class="avatar-circle bg-primary text-white fw-bold d-flex align-items-center justify-content-center"
                      style="width:38px;height:38px;border-radius:50%;font-size:14px;flex-shrink:0">
                      {{ avatarLetter(c.channelName) }}
                    </div>
                    <div>
                      <div class="fw-semibold" style="max-width:200px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap">{{ c.channelName || '—' }}</div>
                      <div class="text-muted" style="font-size:0.75rem">{{ c.channelId }}</div>
                    </div>
                  </div>
                </td>
                <td>
                  <span class="badge rounded-pill text-bg-light border" :style="catBadgeStyle(c.category)" style="font-size:0.75rem">
                    {{ c.category || '—' }}
                  </span>
                </td>
                <td class="text-end fw-semibold">{{ fmtCompact(c.subscribers) }}</td>
                <td class="text-end text-muted">{{ fmtCompact(c.totalViews) }}</td>
                <td class="text-end text-muted">{{ fmtCompact(c.videoCount) }}</td>
                <td class="text-center">
                  <span class="badge text-bg-danger" style="font-size:0.7rem">{{ c.platform }}</span>
                </td>
                <td class="pe-3">
                  <div class="d-flex gap-1 justify-content-end">
                    <router-link :to="`/creator/${c.creatorId}/analytics`"
                      class="btn btn-sm btn-primary py-0 px-2" style="font-size:0.75rem">
                      Analytics
                    </router-link>
                    <a :href="`https://youtube.com/channel/${c.channelId}`" target="_blank" rel="noopener"
                      class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.75rem">
                      YouTube ↗
                    </a>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div class="card-footer bg-white border-top py-3 d-flex align-items-center justify-content-between" v-if="totalPages > 1">
          <span class="text-muted small">Page {{ page }} of {{ totalPages }}</span>
          <div class="d-flex gap-2">
            <button class="btn btn-sm btn-outline-secondary" :disabled="page <= 1" @click="page--; fetchCreators()">← Prev</button>
            <button class="btn btn-sm btn-outline-secondary" :disabled="page >= totalPages" @click="page++; fetchCreators()">Next →</button>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import api from '../services/api';

const keyword      = ref('');
const loading      = ref(false);
const tableLoading = ref(false);
const stats        = ref(null);
const message      = ref('');
const messageType  = ref('success');

const creators     = ref([]);
const categories   = ref([]);
const search       = ref('');
const filterCategory = ref('');
const sortBy       = ref('subscribers');
const page         = ref(1);
const pageSize     = ref(20);
const totalCount   = ref(0);
const totalPages   = ref(1);

let searchTimer = null;

const CATEGORY_COLORS = {
  tech: '#3b82f6', fitness: '#10b981', gaming: '#8b5cf6',
  beauty: '#ec4899', finance: '#f59e0b', food: '#ef4444',
  travel: '#06b6d4', education: '#6366f1',
};

function catBadgeStyle(cat) {
  const c = CATEGORY_COLORS[(cat || '').toLowerCase()] || '#6b7280';
  return `background:${c}18; color:${c}; border-color:${c}40`;
}

function avatarLetter(name) {
  return (name || '?').trim().charAt(0).toUpperCase();
}

function fmtNum(n) {
  if (n == null) return '0';
  return Number(n).toLocaleString();
}

function fmtCompact(n) {
  if (n == null) return '0';
  const num = Number(n);
  if (num >= 1_000_000_000) return (num / 1_000_000_000).toFixed(1) + 'B';
  if (num >= 1_000_000)     return (num / 1_000_000).toFixed(1) + 'M';
  if (num >= 1_000)         return (num / 1_000).toFixed(1) + 'K';
  return String(num);
}

async function fetchStats() {
  try {
    const res = await api.get('/discovery/stats');
    stats.value = res.data;
  } catch {}
}

async function fetchCategories() {
  try {
    const res = await api.get('/discovery/categories');
    categories.value = res.data;
  } catch {}
}

async function fetchCreators() {
  tableLoading.value = true;
  try {
    const res = await api.get('/discovery/creators', {
      params: { search: search.value, category: filterCategory.value, sort: sortBy.value, page: page.value, pageSize: pageSize.value }
    });
    creators.value    = res.data.creators;
    totalCount.value  = res.data.totalCount;
    totalPages.value  = res.data.totalPages;
  } catch {
    creators.value = [];
  } finally {
    tableLoading.value = false;
  }
}

function debounceSearch() {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => { page.value = 1; fetchCreators(); }, 400);
}

async function fetchAll() {
  await Promise.all([fetchStats(), fetchCategories(), fetchCreators()]);
}

async function startDiscovery() {
  loading.value = true;
  message.value = '';
  messageType.value = 'success';
  try {
    const payload = keyword.value ? { keyword: keyword.value } : {};
    const res = await api.post('/discovery/start', payload);
    message.value = `Discovery complete. Creators added: ${res.data.added}`;
    await fetchAll();
  } catch (e) {
    messageType.value = 'error';
    message.value = e?.response?.data?.error || 'Error starting discovery.';
  } finally {
    loading.value = false;
  }
}

onMounted(fetchAll);
</script>
