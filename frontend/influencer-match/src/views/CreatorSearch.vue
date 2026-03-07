<template>
  <div class="creator-search-shell">
    <div class="container-fluid px-4 py-4" style="max-width:1400px">

      <!-- Header -->
      <div class="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h2 class="fw-bold mb-1">Creator Search Intelligence</h2>
          <p class="text-muted mb-0">Filter by category, subscribers, engagement rate and platform</p>
        </div>
        <span class="badge rounded-pill text-bg-light border px-3 py-2">{{ fmtNum(totalCount) }} indexed</span>
      </div>

      <div class="d-flex flex-wrap gap-2 mb-3" v-if="activeFilterCount > 0">
        <span class="badge rounded-pill text-bg-light border px-3 py-2">
          {{ activeFilterCount }} active filters
        </span>
      </div>

      <div class="row g-4">
        <!-- ── Filters sidebar ─────────────────────────── -->
        <div class="col-lg-3">
          <div class="card border-0 shadow-sm sticky-top" style="top:16px">
            <div class="card-body">
              <div class="d-flex justify-content-between align-items-center mb-3">
                <h6 class="fw-semibold mb-0">Filters</h6>
                <button class="btn btn-link btn-sm p-0 text-muted" @click="resetFilters">Reset</button>
              </div>

              <!-- Search -->
              <div class="mb-3">
                <label class="form-label small text-muted">Channel name</label>
                <input v-model="filters.search" class="form-control form-control-sm" placeholder="Search…" @input="debounce" />
              </div>

              <!-- Platform -->
              <div class="mb-3">
                <label class="form-label small text-muted">Platform</label>
                <select v-model="filters.platform" class="form-select form-select-sm" @change="runSearch">
                  <option value="">All platforms</option>
                  <option value="YouTube">YouTube</option>
                  <option value="Instagram">Instagram</option>
                  <option value="TikTok">TikTok</option>
                </select>
              </div>

              <!-- Category -->
              <div class="mb-3">
                <label class="form-label small text-muted">Category</label>
                <select v-model="filters.category" class="form-select form-select-sm" @change="runSearch">
                  <option value="">All categories</option>
                  <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
                </select>
              </div>

              <!-- Country -->
              <div class="mb-3">
                <label class="form-label small text-muted">Country</label>
                <select v-model="filters.country" class="form-select form-select-sm" @change="runSearch">
                  <option value="">All countries</option>
                  <option value="IN">🇮🇳 India (IN)</option>
                  <option value="US">🇺🇸 United States (US)</option>
                  <option value="GB">🇬🇧 United Kingdom (GB)</option>
                  <option value="CA">🇨🇦 Canada (CA)</option>
                  <option value="AU">🇦🇺 Australia (AU)</option>
                </select>
              </div>

              <!-- Language -->
              <div class="mb-3">
                <label class="form-label small text-muted">Language</label>
                <select v-model="filters.language" class="form-select form-select-sm" @change="runSearch">
                  <option value="">All languages</option>
                  <option v-for="l in languages" :key="l" :value="l">{{ l }}</option>
                </select>
              </div>

              <!-- Subscribers range -->
              <div class="mb-3">
                <label class="form-label small text-muted">Min subscribers</label>
                <input v-model.number="filters.minSubscribers" type="number" min="0" step="1000"
                  class="form-control form-control-sm" placeholder="e.g. 100000" @change="runSearch" />
              </div>
              <div class="mb-3">
                <label class="form-label small text-muted">Max subscribers</label>
                <input v-model.number="filters.maxSubscribers" type="number" min="0" step="1000"
                  class="form-control form-control-sm" placeholder="e.g. 10000000" @change="runSearch" />
              </div>

              <!-- Engagement rate -->
              <div class="mb-3">
                <label class="form-label small text-muted">Min engagement rate</label>
                <div class="input-group input-group-sm">
                  <input v-model.number="filters.minEngagement" type="number" min="0" max="1" step="0.001"
                    class="form-control" placeholder="0.03" @change="runSearch" />
                  <span class="input-group-text">×</span>
                </div>
                <div class="form-text">e.g. 0.03 = 3 %</div>
              </div>

              <!-- Sort -->
              <div class="mb-2">
                <label class="form-label small text-muted">Sort by</label>
                <select v-model="filters.sortBy" class="form-select form-select-sm" @change="runSearch">
                  <option value="subscribers">Subscribers ↓</option>
                  <option value="views">Total views ↓</option>
                  <option value="engagement">Engagement rate ↓</option>
                  <option value="videos">Video count ↓</option>
                  <option value="newest">Newest first</option>
                </select>
              </div>

              <button class="btn btn-primary btn-sm w-100 mt-2" @click="runSearch" :disabled="loading">
                <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
                Apply Filters
              </button>
            </div>
          </div>
        </div>

        <!-- ── Results ─────────────────────────────────── -->
        <div class="col-lg-9">

          <div v-if="apiError" class="alert alert-warning">
            {{ apiError }}
          </div>

          <!-- Summary bar -->
          <div class="d-flex justify-content-between align-items-center mb-3">
            <span class="text-muted small">
              <strong>{{ fmtNum(totalCount) }}</strong> creators found
            </span>
            <div class="btn-group btn-group-sm" role="group">
              <button :class="['btn', viewMode==='grid'?'btn-primary':'btn-outline-secondary']" @click="viewMode='grid'" title="Grid">⊞</button>
              <button :class="['btn', viewMode==='table'?'btn-primary':'btn-outline-secondary']" @click="viewMode='table'" title="Table">☰</button>
            </div>
          </div>

          <!-- Spinner -->
          <div v-if="loading" class="text-center py-5">
            <div class="row g-3">
              <div class="col-md-6 col-xl-4" v-for="n in 6" :key="n">
                <div class="card border-0 p-3 placeholder-glow">
                  <div class="placeholder col-6 mb-2"></div>
                  <div class="placeholder col-9 mb-2"></div>
                  <div class="placeholder col-7"></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty state -->
          <div v-else-if="creators.length === 0" class="text-center py-5 text-muted">
            <div style="font-size:3rem">🔍</div>
            <p class="mt-2">No creators match your filters.</p>
          </div>

          <!-- Grid view -->
          <div v-else-if="viewMode === 'grid'" class="row g-3">
            <div v-for="c in creators" :key="c.creatorId" class="col-md-6 col-xl-4">
              <div class="card h-100 border-0 shadow-sm hover-lift">
                <div class="card-body d-flex flex-column">
                  <div class="d-flex align-items-center gap-2 mb-3">
                    <div class="avatar-circle fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                      :style="avatarStyle(c.category)"
                      style="width:42px;height:42px;border-radius:50%;font-size:16px">
                      {{ letter(c.channelName) }}
                    </div>
                    <div class="overflow-hidden">
                      <div class="fw-semibold text-truncate" style="max-width:160px">{{ c.channelName || '—' }}</div>
                      <span class="badge text-bg-light border rounded-pill" :style="catColor(c.category)" style="font-size:0.7rem">{{ c.category || '—' }}</span>
                    </div>
                    <span class="badge text-bg-danger ms-auto" style="font-size:0.65rem">{{ c.platform }}</span>
                    <span v-if="c.country" class="ms-1" style="font-size:0.85rem" :title="c.country">{{ countryFlag(c.country) }}</span>
                    <span v-if="c.language" class="badge text-bg-info ms-1" style="font-size:0.6rem">{{ c.language }}</span>
                    <span v-if="c.creatorTier" class="badge border ms-1" :style="tierColor(c.creatorTier)" style="font-size:0.6rem">{{ c.creatorTier }}</span>
                  </div>

                  <div class="row g-2 text-center mb-3">
                    <div class="col-4">
                      <div class="text-muted" style="font-size:0.65rem">SUBSCRIBERS</div>
                      <div class="fw-semibold small">{{ compact(c.subscribers) }}</div>
                    </div>
                    <div class="col-4">
                      <div class="text-muted" style="font-size:0.65rem">VIEWS</div>
                      <div class="fw-semibold small">{{ compact(c.totalViews) }}</div>
                    </div>
                    <div class="col-4">
                      <div class="text-muted" style="font-size:0.65rem">ENG. RATE</div>
                      <div class="fw-semibold small" :class="creatorEngagementMeta(c).className">{{ creatorEngagementMeta(c).formatted }}</div>
                      <span
                        v-if="creatorEngagementMeta(c).badgeText"
                        class="badge mt-1"
                        :class="creatorEngagementMeta(c).badgeClass"
                        :title="creatorEngagementMeta(c).tooltip"
                        style="font-size:10px;"
                      >
                        {{ creatorEngagementMeta(c).badgeText }}
                      </span>
                    </div>
                  </div>

                  <div class="mt-auto d-flex gap-2">
                    <router-link :to="`/creator/${c.creatorId}/analytics`"
                      class="btn btn-sm btn-primary flex-grow-1">Analytics</router-link>
                    <router-link :to="`/creator/${c.creatorId}/video-analytics`"
                      class="btn btn-sm btn-outline-primary" title="Video Analytics">📊</router-link>
                    <a :href="`https://youtube.com/channel/${c.channelId}`" target="_blank"
                      class="btn btn-sm btn-outline-secondary">↗</a>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Table view -->
          <div v-else class="card border-0 shadow-sm table-shell">
            <div class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th class="ps-3">Channel</th>
                    <th>Category</th>
                    <th>Platform</th>
                    <th class="text-end">Subscribers</th>
                    <th class="text-end">Views</th>
                    <th class="text-end">Eng. Rate</th>
                    <th class="text-end">Videos</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="c in creators" :key="c.creatorId">
                    <td class="ps-3">
                      <div class="d-flex align-items-center gap-2">
                        <div class="avatar-circle fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                          :style="avatarStyle(c.category)"
                          style="width:32px;height:32px;border-radius:50%;font-size:13px">
                          {{ letter(c.channelName) }}
                        </div>
                        <div>
                          <div class="fw-semibold" style="max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">{{ c.channelName || '—' }}</div>
                          <div class="text-muted" style="font-size:0.7rem">{{ c.channelId }}</div>
                        </div>
                      </div>
                    </td>
                    <td><span class="badge rounded-pill border" :style="catColor(c.category)" style="font-size:0.72rem">{{ c.category || '—' }}</span></td>
                    <td><span class="badge text-bg-danger" style="font-size:0.7rem">{{ c.platform }}</span> <span v-if="c.country" :title="c.country">{{ countryFlag(c.country) }}</span><span v-if="c.language" class="badge text-bg-info ms-1" style="font-size:0.65rem">{{ c.language }}</span><span v-if="c.creatorTier" class="badge border ms-1" :style="tierColor(c.creatorTier)" style="font-size:0.65rem">{{ c.creatorTier }}</span></td>
                    <td class="text-end fw-semibold">{{ compact(c.subscribers) }}</td>
                    <td class="text-end text-muted">{{ compact(c.totalViews) }}</td>
                    <td class="text-end fw-semibold" :class="creatorEngagementMeta(c).className">
                      <div>{{ creatorEngagementMeta(c).formatted }}</div>
                      <span
                        v-if="creatorEngagementMeta(c).badgeText"
                        class="badge"
                        :class="creatorEngagementMeta(c).badgeClass"
                        :title="creatorEngagementMeta(c).tooltip"
                        style="font-size:10px;"
                      >
                        {{ creatorEngagementMeta(c).badgeText }}
                      </span>
                    </td>
                    <td class="text-end text-muted">{{ compact(c.videoCount) }}</td>
                    <td class="pe-3">
                      <div class="d-flex gap-1 justify-content-end">
                        <router-link :to="`/creator/${c.creatorId}/analytics`" class="btn btn-sm btn-primary py-0 px-2" style="font-size:0.75rem">Analytics</router-link>
                        <router-link :to="`/creator/${c.creatorId}/video-analytics`" class="btn btn-sm btn-outline-primary py-0 px-2" style="font-size:0.75rem" title="Video Analytics">📊</router-link>
                        <a :href="`https://youtube.com/channel/${c.channelId}`" target="_blank" class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.75rem">↗</a>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Pagination -->
          <div v-if="totalPages > 1" class="d-flex justify-content-between align-items-center mt-4">
            <span class="text-muted small">Page {{ page }} of {{ totalPages }}</span>
            <div class="d-flex gap-2">
              <button class="btn btn-sm btn-outline-secondary" :disabled="page <= 1" @click="prevPage">← Prev</button>
              <button
                v-for="p in pageList" :key="p"
                :class="['btn btn-sm', p === page ? 'btn-primary' : 'btn-outline-secondary']"
                @click="goPage(p)">{{ p }}</button>
              <button class="btn btn-sm btn-outline-secondary" :disabled="page >= totalPages" @click="nextPage">Next →</button>
            </div>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import api from '../services/api';
import { trackFunnelEvent } from '../services/funnel';
import { engagementMeta } from '../utils/engagement';

const creators   = ref([]);
const categories = ref([]);
const languages  = ref([]);
const loading    = ref(false);
const viewMode   = ref('grid');
const apiError   = ref('');

const page       = ref(1);
const pageSize   = ref(18);
const totalCount = ref(0);
const totalPages = ref(1);
const trackedFirstSearch = ref(false);

const activeFilterCount = computed(() => {
  const f = filters.value;
  let n = 0;
  if (f.search) n += 1;
  if (f.platform) n += 1;
  if (f.category) n += 1;
  if (f.country && f.country !== 'IN') n += 1;
  if (f.language) n += 1;
  if (f.minSubscribers !== 1000) n += 1;
  if (f.maxSubscribers !== 500000) n += 1;
  if (f.minEngagement !== null && f.minEngagement !== undefined && f.minEngagement !== '') n += 1;
  if (f.sortBy !== 'subscribers') n += 1;
  return n;
});

const filters = ref({
  search: '',
  platform: '',
  category: '',
  country: 'IN',   // default India
  language: '',
  minSubscribers: 1000,      // show only small/micro/mid-tier creators by default
  maxSubscribers: 500000,
  minEngagement: null,
  sortBy: 'subscribers',
});

let debounceTimer = null;
function debounce() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => { page.value = 1; fetchCreators(); }, 400);
}

const pageList = computed(() => {
  const total = totalPages.value;
  const cur = page.value;
  const delta = 2;
  const pages = [];
  for (let i = Math.max(1, cur - delta); i <= Math.min(total, cur + delta); i++) pages.push(i);
  return pages;
});

const COLORS = { tech:'#3b82f6',fitness:'#10b981',gaming:'#8b5cf6',beauty:'#ec4899',finance:'#f59e0b',food:'#ef4444',travel:'#06b6d4',education:'#6366f1' };
const TIER_COLORS = { Nano:'#f59e0b', Micro:'#10b981', MidTier:'#3b82f6', Macro:'#ef4444', Mega:'#111827' };
function tierColor(tier) {
  const c = TIER_COLORS[tier] || '#6b7280';
  return `background:${c}22;color:${c};border-color:${c}55`;
}
function catColor(cat) {
  const c = COLORS[(cat||'').toLowerCase()] || '#6b7280';
  return `background:${c}18;color:${c};border-color:${c}40`;
}
function avatarStyle(cat) {
  const c = COLORS[(cat||'').toLowerCase()] || '#6b7280';
  return `background:${c}`;
}
function letter(name) { return (name||'?').trim().charAt(0).toUpperCase(); }
function fmtNum(n) { return Number(n||0).toLocaleString(); }
function compact(n) {
  const v = Number(n||0);
  if (v>=1e9) return (v/1e9).toFixed(1)+'B';
  if (v>=1e6) return (v/1e6).toFixed(1)+'M';
  if (v>=1e3) return (v/1e3).toFixed(1)+'K';
  return String(v);
}
function creatorEngagementMeta(creator) {
  return engagementMeta(creator?.engagementRate, {
    mode: 'ratio',
    sampleCount: creator?.videoCount,
    minSampleCount: 3,
    fallback: '—'
  });
}
function countryFlag(code) {
  if (!code) return '';
  return code.toUpperCase().split('').map(c => String.fromCodePoint(0x1F1E6 + c.charCodeAt(0) - 65)).join('');
}

async function fetchCreators() {
  loading.value = true;
  apiError.value = '';
  try {
    const res = await api.get('/creators/search', {
      params: {
        search:          filters.value.search     || undefined,
        platform:        filters.value.platform   || undefined,
        category:        filters.value.category   || undefined,
        country:         filters.value.country    || undefined,
        language:        filters.value.language   || undefined,
        minSubscribers:  filters.value.minSubscribers || undefined,
        maxSubscribers:  filters.value.maxSubscribers || undefined,
        minEngagement:   filters.value.minEngagement  || undefined,
        sortBy:          filters.value.sortBy,
        page:            page.value,
        pageSize:        pageSize.value,
      }
    });
    creators.value   = res.data.items;
    totalCount.value = res.data.totalCount;
    totalPages.value = res.data.totalPages;

    if (!trackedFirstSearch.value) {
      trackedFirstSearch.value = true;
      await trackFunnelEvent('first_search', {
        source: 'creator_search',
        total: totalCount.value,
      });
    }
  } catch (e) {
    creators.value = [];
    apiError.value = e.response?.data?.error || 'Failed to fetch creators.';
  }
  finally  { loading.value = false; }
}

async function fetchCategories() {
  try { const r = await api.get('/discovery/categories'); categories.value = r.data; } catch {}
}

async function fetchLanguages() {
  try { const r = await api.get('/creators/languages'); languages.value = r.data; } catch {}
}

function runSearch() { page.value = 1; fetchCreators(); }
function resetFilters() {
  filters.value = { search:'', platform:'', category:'', country:'IN', language:'', minSubscribers:1000, maxSubscribers:500000, minEngagement:null, sortBy:'subscribers' };
  page.value = 1;
  fetchCreators();
}
function prevPage() { if (page.value > 1) { page.value--; fetchCreators(); } }
function nextPage() { if (page.value < totalPages.value) { page.value++; fetchCreators(); } }
function goPage(p)  { page.value = p; fetchCreators(); }

onMounted(() => { fetchCategories(); fetchLanguages(); fetchCreators(); });
</script>

<style scoped>
.hover-lift { transition: transform .15s, box-shadow .15s; }
.hover-lift:hover { transform: translateY(-3px); box-shadow: 0 6px 20px rgba(0,0,0,.1) !important; }

.creator-search-shell {
  animation: fade-up 0.35s ease both;
}

.table-shell {
  overflow: hidden;
}
</style>
