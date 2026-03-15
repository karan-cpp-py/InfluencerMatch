<template>
  <div class="creator-analytics-page">
    <div class="container py-4" style="max-width:1100px">

      <!-- Back link -->
      <router-link to="/creators/search" class="text-muted text-decoration-none small d-inline-flex align-items-center gap-1 mb-3">
        ← Back to Creator Search
      </router-link>

      <section class="analytics-hero mb-3">
        <p class="hero-kicker mb-2">Performance Lens</p>
        <h3 class="fw-bold mb-1">Creator Deep Analytics</h3>
        <p class="mb-0">Track engagement quality, growth momentum, and top video contribution at a glance.</p>
      </section>

      <!-- Loading state -->
      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary"></div>
        <p class="text-muted mt-2">Loading analytics…</p>
      </div>

      <!-- Error state -->
      <div v-else-if="error" class="alert alert-danger">{{ error }}</div>

      <!-- Content -->
      <template v-else-if="data">

        <!-- ── Header card ───────────────────────────── -->
        <div class="card border-0 shadow-sm mb-4 panel-card">
          <div class="card-body d-flex flex-wrap align-items-center gap-3">
            <div class="avatar-circle fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
              :style="avatarStyle(data.category)"
              style="width:64px;height:64px;border-radius:50%;font-size:26px">
              {{ letter(data.channelName) }}
            </div>
            <div class="flex-grow-1">
              <h4 class="fw-bold mb-1">{{ data.channelName }}</h4>
              <div class="d-flex flex-wrap gap-2 align-items-center">
                <span class="badge text-bg-danger">{{ data.platform }}</span>
                <span class="badge rounded-pill border" :style="catColor(data.category)" style="font-size:0.75rem">{{ data.category || '—' }}</span>
                <span class="text-muted small">Updated {{ timeAgo(data.calculatedAt) }}</span>
              </div>
            </div>
            <div class="d-flex gap-2 ms-auto">
              <button class="btn btn-sm btn-outline-secondary" @click="refreshAnalytics" :disabled="refreshing">
                <span v-if="refreshing" class="spinner-border spinner-border-sm me-1"></span>
                Refresh
              </button>
              <router-link :to="`/creator/${creatorId}/video-analytics`" class="btn btn-sm btn-outline-primary">📊 Video Analytics</router-link>
              <button class="btn btn-sm btn-outline-dark" @click="analyzeLatestTopVideo" :disabled="!data.topVideos?.length">AI Latest Video</button>
              <router-link :to="`/creators/compare?creatorId1=${creatorId}`" class="btn btn-sm btn-outline-secondary">Compare ⇄</router-link>
              <a v-if="data.channelId" :href="`https://youtube.com/channel/${data.channelId}`" target="_blank"
                class="btn btn-sm btn-outline-danger">YouTube ↗</a>
            </div>
          </div>
        </div>

        <!-- ── Metric cards row ──────────────────────── -->
        <div class="row g-3 mb-4">
          <div class="col-6 col-md-3">
            <div class="card border-0 shadow-sm h-100 panel-card">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">SUBSCRIBERS</div>
                <div class="fs-4 fw-bold">{{ compact(data.subscribers) }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 shadow-sm h-100 panel-card">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">AVG VIEWS</div>
                <div class="fs-4 fw-bold">{{ compact(data.averageViews) }}</div>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 shadow-sm h-100 panel-card">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">ENG. RATE</div>
                <div class="fs-4 fw-bold" :class="engagementInfo.className">{{ engagementInfo.formatted }}</div>
                <span
                  v-if="engagementInfo.badgeText"
                  class="badge mt-1"
                  :class="engagementInfo.badgeClass"
                  :title="engagementInfo.tooltip"
                >
                  {{ engagementInfo.badgeText }}
                </span>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 shadow-sm h-100 panel-card">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">TOTAL VIDEOS</div>
                <div class="fs-4 fw-bold">{{ fmtNum(data.totalVideos) }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Creator Score panel ─────────────────── -->
        <div class="card border-0 shadow-sm mb-4 panel-card">
          <div class="card-body">
            <div class="d-flex align-items-center justify-content-between mb-3">
              <h6 class="fw-semibold mb-0">Creator Score</h6>
              <button class="btn btn-sm btn-outline-secondary" @click="refreshScore" :disabled="scoreRefreshing">
                <span v-if="scoreRefreshing" class="spinner-border spinner-border-sm me-1"></span>
                Recalculate
              </button>
            </div>
            <div v-if="scoreLoading" class="text-center text-muted py-2">
              <span class="spinner-border spinner-border-sm me-1"></span> Loading score…
            </div>
            <div v-else-if="score">
              <div class="d-flex align-items-center gap-4 mb-3">
                <div class="text-center">
                  <div :class="['fw-bold', scoreClass(score.score)]" style="font-size:2.8rem;line-height:1">
                    {{ score.score.toFixed(1) }}
                  </div>
                  <div class="text-muted" style="font-size:0.7rem">out of 100</div>
                </div>
                <div class="flex-grow-1">
                  <div v-for="comp in scoreComponents" :key="comp.label" class="mb-2">
                    <div class="d-flex justify-content-between mb-1">
                      <span class="small text-muted">{{ comp.label }} <span class="text-secondary">({{ comp.weight }})</span></span>
                      <span class="small fw-semibold">{{ (score[comp.key]*100).toFixed(1) }}</span>
                    </div>
                    <div class="progress" style="height:6px">
                      <div class="progress-bar" :class="comp.color" role="progressbar"
                        :style="`width:${Math.min(score[comp.key]*100, 100)}%`"></div>
                    </div>
                  </div>
                </div>
              </div>
              <div class="text-muted" style="font-size:0.7rem">Last calculated {{ timeAgo(score.calculatedAt) }}</div>
            </div>
            <div v-else class="text-center text-muted py-3">
              Score not yet calculated.
              <button class="btn btn-sm btn-primary ms-2" @click="refreshScore" :disabled="scoreRefreshing">Calculate Score</button>
            </div>
          </div>
        </div>

        <!-- ── Growth chart ──────────────────────────── -->
        <div class="card border-0 shadow-sm mb-4 panel-card">
          <div class="card-body">
            <h6 class="fw-semibold mb-3">Subscriber Growth</h6>
            <template v-if="sparkPoints.length > 1">
              <div style="position:relative">
                <svg :viewBox="`0 0 ${svgW} ${svgH}`" width="100%" :height="svgH" style="display:block">
                  <!-- Grid lines -->
                  <line v-for="i in 4" :key="i" :x1="0" :y1="gridY(i-1)" :x2="svgW" :y2="gridY(i-1)"
                    stroke="#e5e7eb" stroke-width="1" />
                  <!-- Area fill -->
                  <path :d="areaPath" fill="url(#areaGrad)" />
                  <defs>
                    <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stop-color="#3b82f6" stop-opacity="0.25" />
                      <stop offset="100%" stop-color="#3b82f6" stop-opacity="0.02" />
                    </linearGradient>
                  </defs>
                  <!-- Line -->
                  <polyline :points="polylineStr" fill="none" stroke="#3b82f6" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" />
                  <!-- Dots -->
                  <circle v-for="(pt, i) in sparkPoints" :key="i"
                    :cx="pt.x" :cy="pt.y" r="4"
                    fill="white" stroke="#3b82f6" stroke-width="2.5"
                    @mouseenter="tooltip = { i, x: pt.x, y: pt.y, ...sparkData[i] }"
                    @mouseleave="tooltip = null"
                    style="cursor: pointer" />
                  <!-- Tooltip -->
                  <g v-if="tooltip" :transform="`translate(${tooltipX(tooltip.x)}, ${Math.max(tooltip.y - 40, 8)})`">
                    <rect x="0" y="0" width="140" height="42" rx="6" fill="#1e293b" opacity="0.9" />
                    <text x="8" y="16" fill="white" font-size="11">{{ tooltip.date }}</text>
                    <text x="8" y="32" fill="#93c5fd" font-size="13" font-weight="600">{{ compact(tooltip.subscribers) }}</text>
                    <text v-if="tooltip.delta != null" x="80" y="32" fill="#34d399" font-size="11">{{ tooltip.delta >= 0?'+':'' }}{{ compact(tooltip.delta) }}</text>
                  </g>
                  <!-- Y-axis labels -->
                  <text v-for="i in 5" :key="'y'+i" :x="4" :y="gridY((i-1)*0.25) - 4" font-size="10" fill="#9ca3af">{{ compact(yLabel((i-1)*0.25)) }}</text>
                </svg>
              </div>
              <!-- X-axis labels -->
              <div class="d-flex justify-content-between mt-1 px-1">
                <span v-for="(d, i) in xLabels" :key="i" class="text-muted" style="font-size:0.68rem">{{ d }}</span>
              </div>
            </template>
            <div v-else class="text-muted text-center py-4">Not enough growth data yet. Run a refresh to record a snapshot.</div>
          </div>
        </div>

        <!-- ── Top Videos ────────────────────────────── -->
        <div class="card border-0 shadow-sm panel-card">
          <div class="card-body">
            <h6 class="fw-semibold mb-3">Top Videos</h6>
            <div v-if="data.topVideos && data.topVideos.length" class="row g-3">
              <div v-for="v in data.topVideos" :key="v.videoId" class="col-md-6 col-lg-4">
                <div class="card h-100 border-0 hover-lift" style="box-shadow:0 1px 6px rgba(0,0,0,.08)">
                  <img v-if="v.thumbnailUrl" :src="v.thumbnailUrl" class="card-img-top" style="object-fit:cover;height:140px" :alt="v.title" />
                  <div v-else class="bg-light d-flex align-items-center justify-content-center" style="height:140px;font-size:2rem">📹</div>
                  <div class="card-body p-2">
                    <p class="mb-1 fw-semibold" style="font-size:0.82rem;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden">{{ v.title || '(no title)' }}</p>
                    <div class="d-flex gap-2 text-muted" style="font-size:0.72rem">
                      <span>👁 {{ compact(v.views) }}</span>
                      <span>👍 {{ compact(v.likes) }}</span>
                    </div>
                    <div class="d-flex gap-2 mt-2">
                      <button
                        class="btn btn-sm btn-outline-dark"
                        @click="goToLatestVideoAnalysis(v?.videoId)"
                      >
                        Analyze with AI
                      </button>
                      <a :href="`https://youtube.com/watch?v=${v.videoId}`" target="_blank" class="btn btn-sm btn-outline-secondary">Watch</a>
                    </div>
                  </div>
                </div>
              </div>
            </div>
            <div v-else class="text-muted text-center py-4">Top video data requires YouTube Data API quota. Refresh analytics to populate.</div>
          </div>
        </div>

      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import api from '../services/api';
import { engagementMeta } from '../utils/engagement';

const route     = useRoute();
const router    = useRouter();
const creatorId = route.params.id;

const data           = ref(null);
const loading        = ref(true);
const refreshing     = ref(false);
const score          = ref(null);
const scoreLoading   = ref(false);
const scoreRefreshing= ref(false);
const error          = ref('');
const tooltip        = ref(null);

const engagementInfo = computed(() => {
  if (!data.value) return engagementMeta(null);
  return engagementMeta(data.value.engagementRate, {
    mode: 'ratio',
    sampleCount: Number(data.value.totalVideos),
    minSampleCount: 3,
    fallback: '—'
  });
});

const scoreComponents = [
  { key: 'engagementComponent', label: 'Engagement',  weight: '40%', color: 'bg-primary'   },
  { key: 'viewsComponent',      label: 'Views',       weight: '30%', color: 'bg-info'      },
  { key: 'growthComponent',     label: 'Growth',      weight: '20%', color: 'bg-success'   },
  { key: 'frequencyComponent',  label: 'Frequency',   weight: '10%', color: 'bg-warning'   },
];
function scoreClass(s) { if(s>=60) return 'text-success'; if(s>=30) return 'text-warning'; return 'text-danger'; }

const svgW = 800;
const svgH = 200;
const padX = 16;
const padY = 20;

const COLORS = { tech:'#3b82f6',fitness:'#10b981',gaming:'#8b5cf6',beauty:'#ec4899',finance:'#f59e0b',food:'#ef4444',travel:'#06b6d4',education:'#6366f1' };
function catColor(cat) { const c=COLORS[(cat||'').toLowerCase()]||'#6b7280'; return `background:${c}18;color:${c};border-color:${c}40`; }
function avatarStyle(cat) { const c=COLORS[(cat||'').toLowerCase()]||'#6b7280'; return `background:${c}`; }
function letter(name) { return (name||'?').trim().charAt(0).toUpperCase(); }
function fmtNum(n) { return Number(n||0).toLocaleString(); }
function compact(n) {
  const v = Number(n||0);
  if (v>=1e9) return (v/1e9).toFixed(1)+'B';
  if (v>=1e6) return (v/1e6).toFixed(1)+'M';
  if (v>=1e3) return (v/1e3).toFixed(1)+'K';
  return String(v);
}
function timeAgo(dateStr) {
  if (!dateStr) return '';
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff/60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins/60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs/24)}d ago`;
}

// ── Sparkline helpers ─────────────────────────────────────
const sparkData = computed(() => {
  if (!data.value?.growthHistory) return [];
  return [...data.value.growthHistory]
    .sort((a, b) => new Date(a.recordedAt) - new Date(b.recordedAt))
    .map(p => ({ subscribers: p.subscribers, delta: p.delta, date: new Date(p.recordedAt).toLocaleDateString() }));
});

const sparkPoints = computed(() => {
  const pts = sparkData.value;
  if (pts.length < 2) return [];
  const vals = pts.map(p => p.subscribers);
  const minV = Math.min(...vals);
  const maxV = Math.max(...vals) || minV + 1;
  const range = maxV - minV || 1;
  return pts.map((p, i) => ({
    x: padX + (i / (pts.length - 1)) * (svgW - 2 * padX),
    y: svgH - padY - ((p.subscribers - minV) / range) * (svgH - 2 * padY),
  }));
});

const polylineStr = computed(() =>
  sparkPoints.value.map(p => `${p.x},${p.y}`).join(' ')
);

const areaPath = computed(() => {
  const pts = sparkPoints.value;
  if (!pts.length) return '';
  const top = pts.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x},${p.y}`).join(' ');
  const bot = `L${pts[pts.length-1].x},${svgH-padY} L${pts[0].x},${svgH-padY} Z`;
  return top + ' ' + bot;
});

function gridY(fraction) { return padY + fraction * (svgH - 2 * padY); }
function yLabel(fraction) {
  if (!sparkData.value.length) return 0;
  const vals = sparkData.value.map(p=>p.subscribers);
  const minV = Math.min(...vals), maxV = Math.max(...vals)||minV+1;
  return maxV - fraction * (maxV - minV);
}
function tooltipX(x) { return Math.min(x, svgW - 150); }

const xLabels = computed(() => {
  const pts = sparkData.value;
  if (pts.length < 2) return [];
  const step = Math.ceil(pts.length / 5);
  const result = [];
  for (let i = 0; i < pts.length; i += step) result.push(pts[i].date);
  if (result[result.length-1] !== pts[pts.length-1].date) result.push(pts[pts.length-1].date);
  return result;
});

async function fetchData() {
  loading.value = true; error.value = '';
  try {
    const r = await api.get(`/creators/${creatorId}/analytics`);
    data.value = r.data;
  } catch (e) {
    error.value = e.userMessage || e.response?.data?.message || 'Failed to load analytics.';
  } finally {
    loading.value = false;
  }
}

async function refreshAnalytics() {
  refreshing.value = true;
  try {
    await api.post(`/creators/${creatorId}/analytics/refresh`);
    await fetchData();
  } catch (e) {
    alert(e.userMessage || e.response?.data?.message || 'Refresh failed.');
  } finally {
    refreshing.value = false;
  }
}


function goToLatestVideoAnalysis(videoId = null) {
  const query = {};
  if (videoId) query.videoId = videoId;
  router.push({ path: `/creator/${creatorId}/latest-video-analysis`, query });
}

function analyzeLatestTopVideo() {
  const latest = data.value?.topVideos?.[0];
  if (!latest) return;
  goToLatestVideoAnalysis(latest.videoId);
}
async function fetchScore() {
  scoreLoading.value = true;
  try {
    const r = await api.get(`/creators/${creatorId}/score`);
    score.value = r.data;
  } catch { score.value = null; }
  finally { scoreLoading.value = false; }
}

async function refreshScore() {
  scoreRefreshing.value = true;
  try {
    const r = await api.post(`/creators/${creatorId}/score/refresh`);
    score.value = r.data;
  } catch (e) {
    alert(e.userMessage || e.response?.data?.message || 'Score calculation failed.');
  } finally { scoreRefreshing.value = false; }
}

onMounted(() => { fetchData(); fetchScore(); });
</script>

<style scoped>
.creator-analytics-page {
  background: radial-gradient(circle at 8% 0%, rgba(59, 130, 246, 0.1), transparent 42%),
    radial-gradient(circle at 90% 12%, rgba(20, 184, 166, 0.08), transparent 35%);
}

.analytics-hero {
  border-radius: 18px;
  padding: 1rem 1.1rem;
  color: #dbeafe;
  background: linear-gradient(130deg, #111827, #1e3a8a 56%, #0ea5e9);
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.18);
}

.analytics-hero h3 {
  color: #f8fafc;
}

.hero-kicker {
  display: inline-flex;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  font-size: 0.7rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.25);
}

.panel-card {
  border-radius: 16px;
}

.hover-lift { transition: transform .15s, box-shadow .15s; }
.hover-lift:hover { transform: translateY(-2px); box-shadow: 0 4px 16px rgba(0,0,0,.12) !important; }
</style>
