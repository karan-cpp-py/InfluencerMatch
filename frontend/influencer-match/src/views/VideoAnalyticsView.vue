<template>
  <div>
    <div class="container py-4" style="max-width:1140px">

      <!-- Back link -->
      <router-link :to="`/creator/${creatorId}/analytics`"
        class="text-muted text-decoration-none small d-inline-flex align-items-center gap-1 mb-3">
        ← Back to Analytics
      </router-link>

      <!-- Loading -->
      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary"></div>
        <p class="text-muted mt-2">Loading video analytics…</p>
      </div>

      <!-- Error -->
      <div v-else-if="loadError" class="alert alert-danger">{{ loadError }}</div>

      <!-- Empty — no data yet -->
      <div v-else-if="data && data.totalVideos === 0" class="text-center py-5">
        <div class="fs-1 mb-2">📊</div>
        <h5 class="fw-semibold">No video analytics yet</h5>
        <p class="text-muted mb-3">Run the SuperAdmin <strong>Video Analytics &amp; Brand Detection</strong> job first,<br>or click Refresh below to scan this creator.</p>
        <button class="btn btn-primary" @click="doRefresh" :disabled="refreshing">
          <span v-if="refreshing" class="spinner-border spinner-border-sm me-2"></span>
          Refresh This Creator
        </button>
      </div>

      <template v-else-if="data">

        <!-- ── Header card ─────────────────────────────────────── -->
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-body d-flex flex-wrap align-items-center gap-3">
            <div class="d-flex align-items-center justify-content-center flex-shrink-0 text-white fw-bold"
              :style="`background:${avatarColor};width:56px;height:56px;border-radius:50%;font-size:22px`">
              {{ letter(data.channelName) }}
            </div>
            <div class="flex-grow-1">
              <h4 class="fw-bold mb-0">{{ data.channelName }}</h4>
              <div class="text-muted small">Video Analytics &amp; Brand Collaboration Report</div>
            </div>
            <div class="d-flex gap-2 ms-auto">
              <button class="btn btn-sm btn-outline-secondary" @click="doRefresh" :disabled="refreshing">
                <span v-if="refreshing" class="spinner-border spinner-border-sm me-1"></span>
                {{ refreshing ? 'Refreshing…' : '⟳ Refresh' }}
              </button>
              <router-link :to="`/creator/${creatorId}/analytics`" class="btn btn-sm btn-outline-primary">
                Channel Analytics
              </router-link>
            </div>
          </div>
        </div>

        <!-- ── Overview stats ─────────────────────────────────── -->
        <div class="row g-3 mb-4">
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">TOTAL VIDEOS</div>
                <div class="fs-4 fw-bold">{{ data.totalVideos }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">AVG VIEWS</div>
                <div class="fs-4 fw-bold">{{ compact(data.avgViews) }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">AVG ENGAGEMENT</div>
                <div class="fs-4 fw-bold" :class="engClass(data.avgEngagementRate)">{{ fmtPct(data.avgEngagementRate) }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">ORGANIC</div>
                <div class="fs-4 fw-bold text-success">{{ data.organicVideos }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">SPONSORED</div>
                <div class="fs-4 fw-bold text-warning">{{ data.sponsoredVideos }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4 col-lg-2">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">BRANDS DETECTED</div>
                <div class="fs-4 fw-bold text-primary">{{ data.detectedBrands.length }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Organic vs Sponsored Comparison ───────────────── -->
        <div class="row g-3 mb-4">
          <div class="col-md-6">
            <div class="card border-0 shadow-sm h-100 border-start border-success border-3">
              <div class="card-body">
                <div class="d-flex align-items-center gap-2 mb-3">
                  <span class="badge bg-success fs-6 px-2">🌱 Organic</span>
                  <span class="text-muted small">{{ data.organicVideos }} videos</span>
                </div>
                <div class="row g-2 text-center">
                  <div class="col-6">
                    <div class="text-muted small">Avg Views</div>
                    <div class="fw-bold fs-5">{{ compact(data.avgOrganicViews) }}</div>
                  </div>
                  <div class="col-6">
                    <div class="text-muted small">Avg Engagement</div>
                    <div class="fw-bold fs-5" :class="engClass(data.avgOrganicEng)">{{ fmtPct(data.avgOrganicEng) }}</div>
                  </div>
                </div>
                <div class="mt-3">
                  <div class="text-muted small mb-1">Organic share</div>
                  <div class="progress" style="height:8px">
                    <div class="progress-bar bg-success" :style="`width:${organicPct}%`"></div>
                  </div>
                  <div class="text-muted" style="font-size:0.72rem">{{ organicPct.toFixed(0) }}% of all scanned videos</div>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card border-0 shadow-sm h-100 border-start border-warning border-3">
              <div class="card-body">
                <div class="d-flex align-items-center gap-2 mb-3">
                  <span class="badge bg-warning text-dark fs-6 px-2">🤝 Sponsored</span>
                  <span class="text-muted small">{{ data.sponsoredVideos }} videos</span>
                </div>
                <div class="row g-2 text-center">
                  <div class="col-6">
                    <div class="text-muted small">Avg Views</div>
                    <div class="fw-bold fs-5">{{ compact(data.avgSponsoredViews) }}</div>
                  </div>
                  <div class="col-6">
                    <div class="text-muted small">Avg Engagement</div>
                    <div class="fw-bold fs-5" :class="engClass(data.avgSponsoredEng)">{{ fmtPct(data.avgSponsoredEng) }}</div>
                  </div>
                </div>
                <div class="mt-3">
                  <div class="text-muted small mb-1">Sponsored share</div>
                  <div class="progress" style="height:8px">
                    <div class="progress-bar bg-warning" :style="`width:${100-organicPct}%`"></div>
                  </div>
                  <div class="text-muted" style="font-size:0.72rem">{{ (100-organicPct).toFixed(0) }}% of all scanned videos</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Sponsoring vs Organic engagement comparison bar ── -->
        <div v-if="data.sponsoredVideos > 0 && data.organicVideos > 0" class="card border-0 shadow-sm mb-4">
          <div class="card-body">
            <h6 class="fw-semibold mb-3">Engagement: Organic vs Sponsored</h6>
            <div class="d-flex flex-column gap-2">
              <div>
                <div class="d-flex justify-content-between mb-1">
                  <span class="small text-muted">🌱 Organic avg ({{ fmtPct(data.avgOrganicEng) }})</span>
                  <span class="small fw-semibold">{{ fmtPct(data.avgOrganicEng) }}</span>
                </div>
                <div class="progress" style="height:14px">
                  <div class="progress-bar bg-success"
                    :style="`width:${engBarWidth(data.avgOrganicEng)}%`"></div>
                </div>
              </div>
              <div>
                <div class="d-flex justify-content-between mb-1">
                  <span class="small text-muted">🤝 Sponsored avg ({{ fmtPct(data.avgSponsoredEng) }})</span>
                  <span class="small fw-semibold">{{ fmtPct(data.avgSponsoredEng) }}</span>
                </div>
                <div class="progress" style="height:14px">
                  <div class="progress-bar bg-warning"
                    :style="`width:${engBarWidth(data.avgSponsoredEng)}%`"></div>
                </div>
              </div>
            </div>
            <div class="mt-2 text-muted" style="font-size:0.75rem">
              <span v-if="data.avgSponsoredEng > data.avgOrganicEng" class="text-warning fw-semibold">
                ⚡ Sponsored content drives {{ engDiff }}% higher engagement for this creator.
              </span>
              <span v-else-if="data.avgOrganicEng > data.avgSponsoredEng" class="text-success fw-semibold">
                ✅ Organic content outperforms sponsored by {{ engDiff }}%.
              </span>
              <span v-else>Equal engagement across both types.</span>
            </div>
          </div>
        </div>

        <!-- ── Detected brands ───────────────────────────────── -->
        <div v-if="data.detectedBrands.length" class="card border-0 shadow-sm mb-4">
          <div class="card-body">
            <h6 class="fw-semibold mb-3">🏷 Detected Brands</h6>
            <div class="d-flex flex-wrap gap-2">
              <button v-for="b in data.detectedBrands" :key="b"
                class="btn btn-sm"
                :class="activeFilter === b ? 'btn-primary' : 'btn-outline-secondary'"
                @click="toggleBrandFilter(b)">
                {{ b }}
              </button>
            </div>
            <div v-if="activeFilter" class="mt-2 text-muted small">
              Showing videos with brand: <strong>{{ activeFilter }}</strong>
              <button class="btn btn-link btn-sm p-0 ms-1 text-danger" @click="activeFilter=''">✕ clear</button>
            </div>
          </div>
        </div>

        <!-- ── Video table ────────────────────────────────────── -->
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-transparent py-2 px-3 d-flex flex-wrap align-items-center gap-2">
            <span class="fw-semibold">Videos</span>
            <span class="badge bg-secondary ms-1">{{ filteredVideos.length }}</span>
            <!-- Type filter tabs -->
            <div class="btn-group btn-group-sm ms-2">
              <button v-for="t in typeFilters" :key="t"
                class="btn"
                :class="typeFilter===t ? 'btn-dark' : 'btn-outline-secondary'"
                @click="typeFilter=t">{{ t }}</button>
            </div>
            <!-- Text search -->
            <input v-model="searchQ" class="form-control form-control-sm ms-auto" style="max-width:220px"
              placeholder="Search title…" />
          </div>
          <div class="table-responsive">
            <table class="table table-hover table-sm mb-0 align-middle small">
              <thead class="table-light">
                <tr>
                  <th style="min-width:280px" class="ps-3">Video</th>
                  <th class="text-end">Views</th>
                  <th class="text-end">Likes</th>
                  <th class="text-end">Comments</th>
                  <th class="text-end">Eng. Rate</th>
                  <th>Type</th>
                  <th>Brand</th>
                  <th>Published</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!filteredVideos.length">
                  <td colspan="9" class="text-center text-muted py-4">No videos match your filters.</td>
                </tr>
                <tr v-for="v in pagedVideos" :key="v.youtubeVideoId">
                  <td class="ps-3" style="max-width:280px">
                    <div class="text-truncate fw-semibold" :title="v.title">{{ v.title || '(no title)' }}</div>
                    <div class="text-muted" style="font-size:0.65rem">{{ v.youtubeVideoId }}</div>
                  </td>
                  <td class="text-end">{{ compact(v.views) }}</td>
                  <td class="text-end">{{ compact(v.likes) }}</td>
                  <td class="text-end">{{ compact(v.comments) }}</td>
                  <td class="text-end" :class="engClass(v.engagementRate)">{{ fmtPct(v.engagementRate) }}</td>
                  <td>
                    <span class="badge"
                      :class="v.videoType==='Sponsored' ? 'bg-warning text-dark' : 'bg-success'">
                      {{ v.videoType === 'Sponsored' ? '🤝' : '🌱' }} {{ v.videoType }}
                    </span>
                  </td>
                  <td>
                    <span v-if="v.brandName" class="badge bg-primary bg-opacity-10 text-primary border border-primary-subtle">
                      {{ v.brandName }}
                    </span>
                    <span v-else class="text-muted small">—</span>
                  </td>
                  <td class="text-muted" style="font-size:0.72rem;white-space:nowrap">{{ fmtDate(v.publishedAt) }}</td>
                  <td>
                    <a :href="`https://youtube.com/watch?v=${v.youtubeVideoId}`" target="_blank"
                      class="btn btn-sm btn-outline-danger py-0 px-2" style="font-size:0.7rem">▶</a>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <!-- Pagination -->
          <div v-if="totalPages > 1" class="card-footer bg-transparent d-flex align-items-center justify-content-between py-2 px-3">
            <span class="text-muted small">Page {{ page }} of {{ totalPages }}</span>
            <div class="d-flex gap-1">
              <button class="btn btn-sm btn-outline-secondary" :disabled="page===1" @click="page--">‹</button>
              <button class="btn btn-sm btn-outline-secondary" :disabled="page===totalPages" @click="page++">›</button>
            </div>
          </div>
        </div>

      </template>

      <!-- Refresh notification -->
      <div v-if="refreshMsg" class="position-fixed bottom-0 end-0 m-3 alert alert-success shadow-sm d-flex align-items-center gap-2" style="z-index:9999">
        ✅ {{ refreshMsg }}
        <button class="btn-close btn-sm" @click="refreshMsg=''"></button>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';

const route      = useRoute();
const creatorId  = route.params.id;

const data        = ref(null);
const loading     = ref(true);
const loadError   = ref('');
const refreshing  = ref(false);
const refreshMsg  = ref('');

// Filters
const typeFilter  = ref('All');
const searchQ     = ref('');
const activeFilter= ref('');
const page        = ref(1);
const pageSize    = 20;
const typeFilters = ['All', 'Organic', 'Sponsored'];

// ── Helpers ───────────────────────────────────────────────────
const COLORS = ['#3b82f6','#10b981','#8b5cf6','#ec4899','#f59e0b','#ef4444','#06b6d4','#6366f1'];
const avatarColor = computed(() => {
  const n = Number(creatorId) || 0;
  return COLORS[n % COLORS.length];
});

function letter(name) { return (name || '?').trim().charAt(0).toUpperCase(); }
function compact(n) {
  const v = Number(n || 0);
  if (v >= 1e9) return (v / 1e9).toFixed(1) + 'B';
  if (v >= 1e6) return (v / 1e6).toFixed(1) + 'M';
  if (v >= 1e3) return (v / 1e3).toFixed(1) + 'K';
  return v.toLocaleString();
}
// engagementRate is already stored as percentage (e.g. 4.23 means 4.23%)
function fmtPct(n) { return n != null ? Number(n).toFixed(2) + '%' : '—'; }
function engClass(r) {
  const v = Number(r || 0);
  if (v >= 5)   return 'text-success fw-bold';
  if (v >= 2)   return 'text-warning';
  return 'text-danger';
}
function fmtDate(d) {
  if (!d) return '—';
  return new Date(d).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

// ── Computed stats ────────────────────────────────────────────
const organicPct = computed(() => {
  if (!data.value || data.value.totalVideos === 0) return 0;
  return (data.value.organicVideos / data.value.totalVideos) * 100;
});

const engDiff = computed(() => {
  if (!data.value) return 0;
  const diff = Math.abs(data.value.avgSponsoredEng - data.value.avgOrganicEng);
  const base = Math.max(data.value.avgOrganicEng, data.value.avgSponsoredEng, 0.001);
  return ((diff / base) * 100).toFixed(0);
});

const maxEng = computed(() => {
  if (!data.value) return 1;
  return Math.max(data.value.avgOrganicEng, data.value.avgSponsoredEng, 0.001);
});
function engBarWidth(val) {
  return Math.min((Number(val) / maxEng.value) * 100, 100);
}

// ── Filtered / paged videos ───────────────────────────────────
const filteredVideos = computed(() => {
  if (!data.value?.videos) return [];
  let list = data.value.videos;
  if (typeFilter.value !== 'All')
    list = list.filter(v => v.videoType === typeFilter.value);
  if (activeFilter.value)
    list = list.filter(v => (v.brandName || '').toLowerCase().includes(activeFilter.value.toLowerCase()));
  if (searchQ.value.trim())
    list = list.filter(v => (v.title || '').toLowerCase().includes(searchQ.value.trim().toLowerCase()));
  return list;
});

const totalPages = computed(() => Math.max(1, Math.ceil(filteredVideos.value.length / pageSize)));
const pagedVideos = computed(() => {
  const start = (page.value - 1) * pageSize;
  return filteredVideos.value.slice(start, start + pageSize);
});

// Reset page when filters change
function resetPage() { page.value = 1; }
// Watch filters
watch([typeFilter, searchQ, activeFilter], resetPage);

function toggleBrandFilter(b) {
  activeFilter.value = activeFilter.value === b ? '' : b;
}

// ── API calls ─────────────────────────────────────────────────
async function loadData() {
  loading.value = true;
  loadError.value = '';
  try {
    const r = await api.get(`/creators/${creatorId}/video-analytics`);
    data.value = r.data;
  } catch (e) {
    loadError.value = e.response?.data?.message || 'Failed to load video analytics.';
  } finally {
    loading.value = false;
  }
}

async function doRefresh() {
  refreshing.value = true;
  refreshMsg.value = '';
  try {
    const r = await api.post(`/creators/${creatorId}/video-analytics/refresh`);
    refreshMsg.value = r.data?.message || 'Refreshed!';
    await loadData();
    setTimeout(() => { refreshMsg.value = ''; }, 4000);
  } catch (e) {
    alert(e.response?.data?.error || e.response?.data?.message || 'Refresh failed.');
  } finally {
    refreshing.value = false;
  }
}

onMounted(loadData);
</script>

<style scoped>
.border-3 { border-width: 3px !important; }
.hover-lift:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,.12) !important; }
</style>
