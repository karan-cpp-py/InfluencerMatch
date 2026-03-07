<template>
  <div>
    <div class="container-fluid px-4 py-4" style="max-width:1200px">

      <!-- Header -->
      <div class="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h2 class="fw-bold mb-1">Creator Leaderboard</h2>
          <p class="text-muted mb-0">Ranked by composite score (Engagement · Views · Growth · Frequency)</p>
        </div>
        <div class="d-flex gap-2 align-items-center">
          <!-- India toggle -->
          <button class="btn btn-sm d-flex align-items-center gap-1"
            :class="country === 'IN' ? 'btn-warning' : 'btn-outline-secondary'"
            @click="country === 'IN' ? country = '' : country = 'IN'; page = 1; load()"
            title="Toggle India-only mode">
            🇮🇳 India Only
          </button>
          <select v-model="category" class="form-select form-select-sm" style="width:160px" @change="page=1; load()">
            <option value="">All categories</option>
            <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
          </select>
          <button class="btn btn-sm btn-outline-success" @click="refreshCountries" :disabled="refreshingCountries" title="Fetch country data for all creators from YouTube">
            <span v-if="refreshingCountries" class="spinner-border spinner-border-sm me-1"></span>
            <span v-else>🌍 Fetch Countries</span>
          </button>
          <button class="btn btn-sm btn-outline-secondary" @click="load" :disabled="loading">
            <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
            Refresh
          </button>
        </div>
      </div>

      <!-- Refresh countries status -->
      <div v-if="countryMsg" class="alert py-2 px-3 mb-3 small"
        :class="countryMsg.startsWith('Error') ? 'alert-danger' : 'alert-success'">
        {{ countryMsg }}
      </div>

      <!-- Score formula pill -->
      <div class="alert alert-light border py-2 px-3 mb-4 d-inline-flex gap-2 flex-wrap align-items-center text-muted small">
        <strong>Score formula (0–100):</strong>
        <span class="badge text-bg-primary">40% Engagement</span>
        <span class="badge text-bg-info text-dark">30% Avg Views</span>
        <span class="badge text-bg-success">20% Subscriber Growth</span>
        <span class="badge text-bg-warning text-dark">10% Upload Frequency</span>
      </div>

      <!-- Spinner -->
      <div v-if="loading && !items.length" class="text-center py-5">
        <div class="spinner-border text-primary"></div>
      </div>

      <!-- Empty -->
      <div v-else-if="!loading && !items.length" class="text-center py-5 text-muted">
        <div style="font-size:3rem">🏆</div>
        <p class="mt-2">No creators found{{ category ? ` in category "${category}"` : '' }}.</p>
      </div>

      <!-- Leaderboard table -->
      <div v-else class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th class="ps-3" style="width:52px">Rank</th>
                <th>Creator</th>
                <th>Category</th>
                <th class="text-end">Subscribers</th>
                <th style="min-width:200px">Score breakdown</th>
                <th class="text-end pe-3">Score</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, idx) in items" :key="item.creatorId">
                <td class="ps-3">
                  <span v-if="rankNum(idx) === 1" class="fs-5">🥇</span>
                  <span v-else-if="rankNum(idx) === 2" class="fs-5">🥈</span>
                  <span v-else-if="rankNum(idx) === 3" class="fs-5">🥉</span>
                  <span v-else class="text-muted fw-semibold">{{ rankNum(idx) }}</span>
                </td>
                <td>
                  <div class="d-flex align-items-center gap-2">
                    <div class="fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                      :style="avatarStyle(item.category)"
                      style="width:36px;height:36px;border-radius:50%;font-size:13px">
                      {{ letter(item.channelName) }}
                    </div>
                    <div>
                      <div class="fw-semibold" style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">{{ item.channelName }}</div>
                      <div class="text-muted" style="font-size:0.7rem">
                        {{ item.platform }} · {{ compact(item.subscribers) }} subs · {{ engagementMetaFor(item).formatted }} eng
                        <span
                          v-if="engagementMetaFor(item).badgeText"
                          class="badge ms-1"
                          :class="engagementMetaFor(item).badgeClass"
                          :title="engagementMetaFor(item).tooltip"
                          style="font-size:10px;"
                        >
                          {{ engagementMetaFor(item).badgeText }}
                        </span>
                        <span v-if="item.country" class="ms-1">· {{ countryFlag(item.country) }}</span>
                      </div>
                    </div>
                  </div>
                </td>
                <td>
                  <span class="badge rounded-pill border" :style="catColor(item.category)" style="font-size:0.72rem">{{ item.category || '—' }}</span>
                </td>
                <td class="text-end">{{ compact(item.subscribers) }}</td>
                <td>
                  <div class="d-flex flex-column gap-1" style="font-size:0.7rem">
                    <div class="d-flex align-items-center gap-1">
                      <span class="text-muted" style="width:64px">Engage</span>
                      <div class="flex-grow-1 bg-light rounded" style="height:6px">
                        <div class="rounded bg-primary" :style="`width:${barPct(item.engagementComponent, 40)}%`" style="height:6px"></div>
                      </div>
                      <span style="width:30px;text-align:right">{{ item.engagementComponent.toFixed(1) }}</span>
                    </div>
                    <div class="d-flex align-items-center gap-1">
                      <span class="text-muted" style="width:64px">Views</span>
                      <div class="flex-grow-1 bg-light rounded" style="height:6px">
                        <div class="rounded bg-info" :style="`width:${barPct(item.viewsComponent, 30)}%`" style="height:6px"></div>
                      </div>
                      <span style="width:30px;text-align:right">{{ item.viewsComponent.toFixed(1) }}</span>
                    </div>
                    <div class="d-flex align-items-center gap-1">
                      <span class="text-muted" style="width:64px">Growth</span>
                      <div class="flex-grow-1 bg-light rounded" style="height:6px">
                        <div class="rounded bg-success" :style="`width:${barPct(item.growthComponent, 20)}%`" style="height:6px"></div>
                      </div>
                      <span style="width:30px;text-align:right">{{ item.growthComponent.toFixed(1) }}</span>
                    </div>
                    <div class="d-flex align-items-center gap-1">
                      <span class="text-muted" style="width:64px">Freq</span>
                      <div class="flex-grow-1 bg-light rounded" style="height:6px">
                        <div class="rounded bg-warning" :style="`width:${barPct(item.frequencyComponent, 10)}%`" style="height:6px"></div>
                      </div>
                      <span style="width:30px;text-align:right">{{ item.frequencyComponent.toFixed(1) }}</span>
                    </div>
                  </div>
                </td>
                <td class="text-end pe-3">
                  <div class="d-flex flex-column align-items-end gap-1">
                    <div :class="['fw-bold fs-5', scoreClass(item.score)]">{{ item.score.toFixed(1) }}</div>
                    <div class="d-flex gap-1">
                      <router-link :to="`/creator/${item.creatorId}/analytics`"
                        class="btn btn-sm btn-outline-primary py-0 px-2" style="font-size:0.7rem">View</router-link>
                      <router-link :to="`/creators/compare?creatorId1=${item.creatorId}`"
                        class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.7rem">Compare</router-link>
                    </div>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div v-if="totalPages > 1" class="card-footer bg-white border-top py-3 d-flex justify-content-between align-items-center">
          <span class="text-muted small">Page {{ page }} of {{ totalPages }} · {{ fmtNum(totalCount) }} scored creators</span>
          <div class="d-flex gap-2">
            <button class="btn btn-sm btn-outline-secondary" :disabled="page <= 1" @click="page--; load()">← Prev</button>
            <button class="btn btn-sm btn-outline-secondary" :disabled="page >= totalPages" @click="page++; load()">Next →</button>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';
import { engagementMeta } from '../utils/engagement';

const items      = ref([]);
const categories = ref([]);
const loading    = ref(false);
const category   = ref('');
const country    = ref('IN');   // default: India only
const page       = ref(1);
const pageSize   = ref(20);
const totalCount = ref(0);
const totalPages = ref(1);
const refreshingCountries = ref(false);
const countryMsg = ref('');

const COLORS = { tech:'#3b82f6',fitness:'#10b981',gaming:'#8b5cf6',beauty:'#ec4899',finance:'#f59e0b',food:'#ef4444',travel:'#06b6d4',education:'#6366f1' };
function catColor(cat) { const c=COLORS[(cat||'').toLowerCase()]||'#6b7280'; return `background:${c}18;color:${c};border-color:${c}40`; }
function avatarStyle(cat) { const c=COLORS[(cat||'').toLowerCase()]||'#6b7280'; return `background:${c}`; }
function letter(n) { return (n||'?').trim().charAt(0).toUpperCase(); }
function compact(n) { const v=Number(n||0); if(v>=1e9) return (v/1e9).toFixed(1)+'B'; if(v>=1e6) return (v/1e6).toFixed(1)+'M'; if(v>=1e3) return (v/1e3).toFixed(1)+'K'; return String(v); }
function engagementMetaFor(item) {
  return engagementMeta(item?.engagementRate, {
    mode: 'ratio',
    decimals: 1,
    sampleCount: item?.videoCount,
    minSampleCount: 3,
    fallback: '—'
  });
}
function fmtNum(n) { return Number(n||0).toLocaleString(); }
function barPct(component, maxWeight) { return Math.min((component / maxWeight) * 100, 100).toFixed(1); }
function scoreClass(s) { if(s>=60) return 'text-success'; if(s>=30) return 'text-warning'; return 'text-danger'; }
function rankNum(idx) { return (page.value - 1) * pageSize.value + idx + 1; }
function countryFlag(code) {
  if (!code) return '';
  return code.toUpperCase().split('').map(c => String.fromCodePoint(0x1F1E6 + c.charCodeAt(0) - 65)).join('');
}

async function load() {
  loading.value = true;
  try {
    const r = await api.get('/creators/leaderboard', {
      params: { page: page.value, pageSize: pageSize.value, category: category.value || undefined, country: country.value || undefined }
    });
    items.value      = r.data.items;
    totalCount.value = r.data.totalCount;
    totalPages.value = r.data.totalPages;
  } catch { items.value = []; }
  finally  { loading.value = false; }
}

async function refreshCountries() {
  refreshingCountries.value = true;
  countryMsg.value = '';
  try {
    const r = await api.post('/discovery/refresh-countries');
    countryMsg.value = `✅ ${r.data.message}`;
    await load();
  } catch (e) {
    countryMsg.value = `Error: ${e.response?.data?.error || 'Failed to refresh countries'}`;
  } finally {
    refreshingCountries.value = false;
  }
}

async function fetchCategories() {
  try { const r = await api.get('/discovery/categories'); categories.value = r.data; } catch {}
}

onMounted(() => { fetchCategories(); load(); });
</script>
