<template>
  <div class="brand-analytics-page">
    <div class="container py-4" style="max-width:1200px">

      <section class="analytics-hero mb-4">
        <div>
          <p class="hero-kicker mb-2">Insight Engine</p>
          <h2 class="fw-bold mb-1">Brand Analytics</h2>
          <p class="mb-0">Discover which creators are promoting your brand and measure campaign reach.</p>
        </div>
      </section>

      <!-- Search bar -->
      <div class="card border-0 shadow-sm mb-4 panel-card">
        <div class="card-body">
          <label class="form-label fw-semibold small text-muted">Brand Name</label>
          <div class="input-group" style="max-width:520px">
            <input v-model="brandInput" class="form-control" placeholder="e.g. Nike, Apple, Samsung…"
              @keyup.enter="analyze" />
            <button class="btn btn-primary px-4" @click="analyze" :disabled="loading">
              <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
              Analyze
            </button>
            <button class="btn btn-outline-secondary" @click="triggerScan" :disabled="scanning" title="Trigger background scan">
              <span v-if="scanning" class="spinner-border spinner-border-sm"></span>
              <span v-else>⟳ Scan</span>
            </button>
          </div>
          <div v-if="scanMsg" class="mt-2 text-success small">{{ scanMsg }}</div>
        </div>
      </div>

      <div v-if="error" class="alert alert-danger">{{ error }}</div>

      <!-- ── Tab bar ────────────────────────────────────────────── -->
      <ul v-if="analytics || vaData" class="nav nav-tabs mb-4 sticky-tabs">
        <li class="nav-item">
          <button class="nav-link" :class="activeTab==='mentions'?'active':''"
            @click="activeTab='mentions'">📋 Mention Analytics</button>
        </li>
        <li class="nav-item">
          <button class="nav-link" :class="activeTab==='video'?'active':''"
            @click="switchToVideo">📊 Video Analytics</button>
        </li>
      </ul>

      <template v-if="activeTab==='mentions' && analytics">

        <!-- ── Stat cards ──────────────────────────────────────────── -->
        <div class="row g-3 mb-4">
          <div class="col-6 col-md-4 col-lg-2half" v-for="s in statCards" :key="s.label">
            <div class="card border-0 shadow-sm h-100 panel-card">
              <div class="card-body text-center py-3">
                <div class="text-muted small mb-1">{{ s.label }}</div>
                <div class="fw-bold fs-4" :class="s.cls||''">{{ s.val }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Promoting creators table ───────────────────────────── -->
        <div class="card border-0 shadow-sm mb-4 panel-card">
          <div class="card-header bg-transparent py-2 px-3 d-flex align-items-center justify-content-between">
            <span class="fw-semibold">Promoting Creators <span class="badge bg-primary ms-1">{{ analytics.creators.length }}</span></span>
            <input v-model="crFilter" class="form-control form-control-sm" style="max-width:200px" placeholder="Filter…" />
          </div>
          <div class="table-responsive">
            <table class="table table-hover mb-0 small align-middle">
              <thead class="table-light">
                <tr>
                  <th>Creator</th>
                  <th>Platform</th>
                  <th class="text-end">Subscribers</th>
                  <th class="text-end">Eng. Rate</th>
                  <th class="text-end">Mentions</th>
                  <th class="text-end">Est. Views</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!filteredCreators.length">
                  <td colspan="7" class="text-center text-muted py-4">No creators found for this brand yet.</td>
                </tr>
                <tr v-for="c in filteredCreators" :key="c.creatorId">
                  <td>
                    <div class="d-flex align-items-center gap-2">
                      <div class="text-white d-flex align-items-center justify-content-center flex-shrink-0"
                        :style="`background:${catHex(c.category)};width:28px;height:28px;border-radius:50%;font-size:11px`">
                        {{ letter(c.channelName) }}
                      </div>
                      <div>
                        <div class="fw-semibold">{{ c.channelName }}</div>
                        <span class="badge rounded-pill border" :style="catCss(c.category)" style="font-size:0.65rem">{{ c.category||'—' }}</span>
                      </div>
                    </div>
                  </td>
                  <td><span class="badge text-bg-danger small">{{ c.platform }}</span></td>
                  <td class="text-end">{{ compact(c.subscribers) }}</td>
                  <td class="text-end" :class="mentionEngagementMeta(c).className">
                    <div>{{ mentionEngagementMeta(c).formatted }}</div>
                    <span
                      v-if="mentionEngagementMeta(c).badgeText"
                      class="badge"
                      :class="mentionEngagementMeta(c).badgeClass"
                      :title="mentionEngagementMeta(c).tooltip"
                      style="font-size:10px;"
                    >
                      {{ mentionEngagementMeta(c).badgeText }}
                    </span>
                  </td>
                  <td class="text-end fw-bold">{{ c.mentionCount }}</td>
                  <td class="text-end">{{ compact(c.estimatedViews) }}</td>
                  <td>
                    <div class="d-flex gap-1 justify-content-end">
                      <router-link :to="`/creator/${c.creatorId}/analytics`" class="btn btn-sm btn-outline-primary py-0 px-2" style="font-size:0.7rem">Analytics</router-link>
                      <router-link :to="`/creators/compare?creatorId1=${c.creatorId}`" class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.7rem">Compare</router-link>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- ── Mentions section ───────────────────────────────────── -->
        <div class="card border-0 shadow-sm panel-card">
          <div class="card-header bg-transparent py-2 px-3 d-flex align-items-center gap-2">
            <span class="fw-semibold">Video Mentions</span>
            <span class="badge bg-secondary ms-1">{{ mentions.length }}</span>
            <button class="btn btn-sm btn-outline-secondary ms-auto" @click="showMentions=!showMentions">
              {{ showMentions ? 'Collapse ▲' : 'Expand ▼' }}
            </button>
            <button v-if="!mentionsLoaded && !showMentions" class="btn btn-sm btn-primary ms-1" @click="loadMentions">
              Load Mentions
            </button>
          </div>
          <div v-if="showMentions">
            <div v-if="mentionsLoading" class="text-center py-3">
              <span class="spinner-border spinner-border-sm me-1"></span> Loading…
            </div>
            <div class="table-responsive" v-else>
              <table class="table table-hover mb-0 small align-middle">
                <thead class="table-light">
                  <tr>
                    <th>Video</th>
                    <th>Creator</th>
                    <th>Detection Method</th>
                    <th class="text-end">Confidence</th>
                    <th>Detected</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="!mentions.length">
                    <td colspan="5" class="text-center text-muted py-3">No mentions loaded.</td>
                  </tr>
                  <tr v-for="m in mentions" :key="m.brandMentionId">
                    <td style="max-width:280px">
                      <div class="text-truncate" :title="m.videoTitle">{{ m.videoTitle || m.videoId }}</div>
                      <div class="text-muted" style="font-size:0.65rem">{{ m.videoId }}</div>
                    </td>
                    <td>{{ m.channelName || m.creatorId }}</td>
                    <td>
                      <span class="badge"
                        :class="m.detectionMethod==='Hashtag'?'text-bg-success':m.detectionMethod==='Mention'?'text-bg-info':'text-bg-secondary'">
                        {{ m.detectionMethod }}
                      </span>
                    </td>
                    <td class="text-end">
                      <span :class="confClass(m.confidenceScore)">{{ (m.confidenceScore*100).toFixed(0) }}%</span>
                    </td>
                    <td class="text-muted" style="font-size:0.72rem">{{ fmtDate(m.detectedAt) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

      </template>

      <!-- Empty state -->
      <div v-else-if="!loading && !vaLoading && activeTab==='mentions'" class="text-center text-muted py-5">
        <div class="fs-1 mb-2">🔍</div>
        <div>Enter a brand name to analyze creator promotions</div>
      </div>

      <!-- ── Video Analytics tab ────────────────────────────── -->
      <template v-if="activeTab==='video'">
        <div v-if="vaLoading" class="text-center py-5">
          <div class="spinner-border text-primary"></div>
          <p class="text-muted mt-2">Loading video analytics…</p>
        </div>
        <div v-else-if="vaError" class="alert alert-warning">{{ vaError }}</div>
        <template v-else-if="vaData">

          <!-- Summary stats -->
          <div class="row g-3 mb-4">
            <div class="col-6 col-md-3">
              <div class="card border-0 shadow-sm h-100 panel-card">
                <div class="card-body text-center py-3">
                  <div class="text-muted small mb-1">CREATORS</div>
                  <div class="fs-4 fw-bold">{{ vaData.totalCreators }}</div>
                </div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="card border-0 shadow-sm h-100">
                <div class="card-body text-center py-3">
                  <div class="text-muted small mb-1">TOTAL VIDEOS</div>
                  <div class="fs-4 fw-bold">{{ vaData.totalVideos }}</div>
                </div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="card border-0 shadow-sm h-100">
                <div class="card-body text-center py-3">
                  <div class="text-muted small mb-1">TOTAL VIEWS</div>
                  <div class="fs-4 fw-bold">{{ compact(vaData.totalViews) }}</div>
                </div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="card border-0 shadow-sm h-100">
                <div class="card-body text-center py-3">
                  <div class="text-muted small mb-1">AVG ENGAGEMENT</div>
                  <div class="fs-4 fw-bold" :class="videoAvgEngagementMeta.className">
                    {{ videoAvgEngagementMeta.formatted }}
                  </div>
                  <span
                    v-if="videoAvgEngagementMeta.badgeText"
                    class="badge mt-1"
                    :class="videoAvgEngagementMeta.badgeClass"
                    :title="videoAvgEngagementMeta.tooltip"
                  >
                    {{ videoAvgEngagementMeta.badgeText }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Creators table -->
          <div class="card border-0 shadow-sm panel-card">
            <div class="card-header bg-transparent py-2 px-3 d-flex align-items-center justify-content-between">
              <span class="fw-semibold">Promoting Creators
                <span class="badge bg-primary ms-1">{{ vaData.creators.length }}</span>
              </span>
              <input v-model="vaFilter" class="form-control form-control-sm" style="max-width:200px"
                placeholder="Filter…" />
            </div>
            <div class="table-responsive">
              <table class="table table-hover mb-0 small align-middle">
                <thead class="table-light">
                  <tr>
                    <th>Creator</th>
                    <th class="text-end">Subscribers</th>
                    <th class="text-end">Videos</th>
                    <th class="text-end">Total Views</th>
                    <th class="text-end">Avg Engagement</th>
                    <th>Last Detected</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="!filteredVaCreators.length">
                    <td colspan="7" class="text-center text-muted py-4">No creators found for this brand in Video Analytics table.</td>
                  </tr>
                  <tr v-for="c in filteredVaCreators" :key="c.creatorId">
                    <td>
                      <div class="d-flex align-items-center gap-2">
                        <div class="text-white d-flex align-items-center justify-content-center flex-shrink-0"
                          :style="`background:${catHex(c.category)};width:28px;height:28px;border-radius:50%;font-size:11px`">
                          {{ letter(c.channelName) }}
                        </div>
                        <div>
                          <div class="fw-semibold">{{ c.channelName }}</div>
                          <span class="badge rounded-pill border" :style="catCss(c.category)" style="font-size:0.65rem">{{ c.category||'—' }}</span>
                        </div>
                      </div>
                    </td>
                    <td class="text-end">{{ compact(c.subscribers) }}</td>
                    <td class="text-end fw-bold">{{ c.videoCount }}</td>
                    <td class="text-end">{{ compact(c.totalViews) }}</td>
                    <td class="text-end" :class="videoCreatorEngagementMeta(c).className">
                      <div>{{ videoCreatorEngagementMeta(c).formatted }}</div>
                      <span
                        v-if="videoCreatorEngagementMeta(c).badgeText"
                        class="badge"
                        :class="videoCreatorEngagementMeta(c).badgeClass"
                        :title="videoCreatorEngagementMeta(c).tooltip"
                        style="font-size:10px;"
                      >
                        {{ videoCreatorEngagementMeta(c).badgeText }}
                      </span>
                    </td>
                    <td class="text-muted" style="font-size:0.72rem">{{ fmtDate(c.lastDetectedAt) }}</td>
                    <td>
                      <div class="d-flex gap-1 justify-content-end">
                        <router-link :to="`/creator/${c.creatorId}/video-analytics`"
                          class="btn btn-sm btn-outline-primary py-0 px-2" style="font-size:0.7rem">Video Analytics</router-link>
                        <router-link :to="`/creator/${c.creatorId}/analytics`"
                          class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.7rem">Analytics</router-link>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </template>
        <div v-else-if="!vaLoading" class="text-center text-muted py-5">
          <div class="fs-1 mb-2">📊</div>
          <div>Enter a brand name above and click <strong>Analyze</strong> to load video analytics.</div>
        </div>
      </template>

    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import api from '../services/api';
import { formatEngagement, engagementMeta } from '../utils/engagement';

const brandInput = ref('');
const loading    = ref(false);
const scanning   = ref(false);
const scanMsg    = ref('');
const error      = ref('');
const analytics  = ref(null);
const mentions   = ref([]);
const mentionsLoaded   = ref(false);
const mentionsLoading  = ref(false);
const showMentions     = ref(false);
const crFilter   = ref('');
const activeTab  = ref('mentions');

// ── Video Analytics tab state ─────────────────────────────────
const vaData    = ref(null);
const vaLoading = ref(false);
const vaError   = ref('');
const vaFilter  = ref('');

const filteredVaCreators = computed(() => {
  if (!vaData.value) return [];
  const f = vaFilter.value.toLowerCase();
  if (!f) return vaData.value.creators;
  return vaData.value.creators.filter(c =>
    (c.channelName||'').toLowerCase().includes(f) ||
    (c.category||'').toLowerCase().includes(f)
  );
});

const videoAvgEngagementMeta = computed(() => {
  return engagementMeta(vaData.value?.avgEngagement, {
    mode: 'percent',
    sampleCount: Number(vaData.value?.totalCreators),
    minSampleCount: 3,
    fallback: '—'
  });
});

function videoCreatorEngagementMeta(creator) {
  return engagementMeta(creator?.avgEngagement, {
    mode: 'percent',
    sampleCount: Number(creator?.videoCount),
    minSampleCount: 3,
    fallback: '—'
  });
}

function vaEngClass(r) {
  const v = Number(r || 0);
  if (v >= 5) return 'text-success fw-bold';
  if (v >= 2) return 'text-warning';
  return 'text-danger';
}

const COLORS = { tech:'#3b82f6',fitness:'#10b981',gaming:'#8b5cf6',beauty:'#ec4899',finance:'#f59e0b',food:'#ef4444',travel:'#06b6d4',education:'#6366f1' };
function catHex(cat) { return COLORS[(cat||'').toLowerCase()]||'#6b7280'; }
function catCss(cat) { const c=catHex(cat); return `background:${c}18;color:${c};border-color:${c}40`; }
function letter(n) { return (n||'?').trim().charAt(0).toUpperCase(); }
function compact(n) { const v=Number(n||0); if(v>=1e9) return (v/1e9).toFixed(1)+'B'; if(v>=1e6) return (v/1e6).toFixed(1)+'M'; if(v>=1e3) return (v/1e3).toFixed(1)+'K'; return String(v); }
function mentionEngagementMeta(creator) {
  return engagementMeta(creator?.engagementRate, {
    mode: 'ratio',
    sampleCount: Number(creator?.mentionCount),
    minSampleCount: 3,
    fallback: '—'
  });
}
function fmtDate(d) { return d ? new Date(d).toLocaleDateString() : '—'; }
function confClass(c) { if(c>=0.9) return 'text-success fw-bold'; if(c>=0.7) return 'text-warning'; return 'text-secondary'; }

const statCards = computed(() => {
  if (!analytics.value) return [];
  const a = analytics.value;
  return [
    { label: 'Creators', val: a.totalCreators },
    { label: 'Videos',   val: a.totalVideos },
    { label: 'Est. Views', val: compact(a.estimatedTotalViews) },
    { label: 'Est. Engagement', val: compact(a.estimatedTotalEngagement) },
    { label: 'Avg Confidence', val: a.averageConfidenceScore != null ? (a.averageConfidenceScore*100).toFixed(1)+'%' : '—', cls: confClass(a.averageConfidenceScore) },
  ];
});

const filteredCreators = computed(() => {
  if (!analytics.value) return [];
  const f = crFilter.value.toLowerCase();
  if (!f) return analytics.value.creators;
  return analytics.value.creators.filter(c =>
    (c.channelName||'').toLowerCase().includes(f) ||
    (c.category||'').toLowerCase().includes(f) ||
    (c.platform||'').toLowerCase().includes(f)
  );
});

async function switchToVideo() {
  activeTab.value = 'video';
  const brand = brandInput.value.trim();
  if (!brand || vaData.value) return;
  await loadVideoAnalytics();
}

async function loadVideoAnalytics() {
  const brand = brandInput.value.trim();
  if (!brand) return;
  vaLoading.value = true; vaError.value = ''; vaData.value = null;
  try {
    const r = await api.get(`/brands/${encodeURIComponent(brand)}/creators`);
    vaData.value = r.data;
  } catch (e) {
    vaError.value = e.response?.data?.message || 'Failed to load video analytics data.';
  } finally {
    vaLoading.value = false;
  }
}

async function analyze() {
  const brand = brandInput.value.trim();
  if (!brand) return;
  // Reset both tabs
  analytics.value = null; mentions.value = []; mentionsLoaded.value = false;
  vaData.value = null; vaError.value = '';
  loading.value = true; error.value = '';
  try {
    const r = await api.get(`/brands/${encodeURIComponent(brand)}/analytics`);
    analytics.value = r.data;
  } catch (e) {
    error.value = e.response?.data?.message || e.response?.data || 'Failed to load brand analytics.';
  } finally {
    loading.value = false;
  }
  // Also pre-fetch video analytics for the video tab
  loadVideoAnalytics();
}

async function loadMentions() {
  const brand = brandInput.value.trim();
  if (!brand) return;
  mentionsLoading.value = true; showMentions.value = true;
  try {
    const r = await api.get(`/brands/${encodeURIComponent(brand)}/mentions`);
    mentions.value = r.data;
    mentionsLoaded.value = true;
  } catch {}
  finally { mentionsLoading.value = false; }
}

async function triggerScan() {
  scanning.value = true; scanMsg.value = '';
  try {
    await api.post('/brands/scan');
    scanMsg.value = 'Background scan triggered. Results will appear after the scan completes.';
  } catch { scanMsg.value = 'Scan trigger failed.'; }
  finally { scanning.value = false; }
}

// auto-load mentions when section is opened
function toggleMentions() {
  showMentions.value = !showMentions.value;
  if (showMentions.value && !mentionsLoaded.value) loadMentions();
}
</script>

<style scoped>
.brand-analytics-page {
  background: radial-gradient(circle at 10% 0%, rgba(14, 165, 233, 0.08), transparent 38%),
    radial-gradient(circle at 95% 20%, rgba(34, 197, 94, 0.06), transparent 36%);
}

.analytics-hero {
  border-radius: 20px;
  padding: 1.25rem;
  color: #e2e8f0;
  background: linear-gradient(120deg, #111827 0%, #0f172a 35%, #1d4ed8 100%);
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.2);
}

.analytics-hero h2 {
  color: #f8fafc;
}

.hero-kicker {
  display: inline-flex;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.7rem;
  color: #bfdbfe;
  background: rgba(147, 197, 253, 0.2);
}

.panel-card {
  border-radius: 16px;
}

.sticky-tabs {
  position: sticky;
  top: 72px;
  z-index: 2;
  backdrop-filter: blur(4px);
}

@media (max-width: 768px) {
  .sticky-tabs {
    top: 56px;
  }
}
</style>
