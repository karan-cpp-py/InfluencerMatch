<template>
  <div>
    <div class="container py-4" style="max-width:1200px">

      <div class="mb-4">
        <h2 class="fw-bold mb-1">Creator Comparison</h2>
        <p class="text-muted mb-0">Compare two creators side-by-side across all key metrics</p>
      </div>

      <!-- Creator picker row -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-5">
              <label class="form-label small text-muted fw-semibold">Creator 1 — ID or search</label>
              <div class="input-group">
                <input v-model="q1" class="form-control" placeholder="Channel name…" @input="debounce(1)" @keyup.enter="pickFirst(1)" />
                <button class="btn btn-outline-secondary btn-sm px-2" @click="pickFirst(1)" :disabled="searching1">
                  <span v-if="searching1" class="spinner-border spinner-border-sm"></span>
                  <span v-else>Search</span>
                </button>
              </div>
              <!-- search results 1 -->
              <div v-if="results1.length" class="list-group mt-1 shadow-sm" style="position:absolute;z-index:100;width:calc(100% - 24px)">
                <button v-for="c in results1" :key="c.creatorId"
                  class="list-group-item list-group-item-action py-2 px-3 d-flex align-items-center gap-2"
                  @click="selectCreator(1, c)">
                  <span class="fw-semibold small">{{ c.channelName }}</span>
                  <span class="text-muted" style="font-size:0.72rem">{{ c.platform }} · {{ compact(c.subscribers) }}</span>
                </button>
              </div>
              <div v-if="selected1" class="mt-2 p-2 rounded border d-flex align-items-center gap-2" :style="`border-color:${catHex(selected1.category)}40!important;background:${catHex(selected1.category)}10`">
                <div class="fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                  :style="`background:${catHex(selected1.category)};width:30px;height:30px;border-radius:50%;font-size:12px`">
                  {{ letter(selected1.channelName) }}
                </div>
                <div>
                  <div class="fw-semibold small">{{ selected1.channelName }}</div>
                  <div class="text-muted" style="font-size:0.7rem">{{ selected1.platform }} · {{ selected1.category }}</div>
                </div>
                <button class="btn btn-sm btn-link ms-auto text-muted p-0" @click="selected1=null;q1=''">✕</button>
              </div>
            </div>

            <div class="col-md-2 text-center">
              <span class="fw-bold text-muted" style="font-size:1.4rem">VS</span>
            </div>

            <div class="col-md-5" style="position:relative">
              <label class="form-label small text-muted fw-semibold">Creator 2 — ID or search</label>
              <div class="input-group">
                <input v-model="q2" class="form-control" placeholder="Channel name…" @input="debounce(2)" @keyup.enter="pickFirst(2)" />
                <button class="btn btn-outline-secondary btn-sm px-2" @click="pickFirst(2)" :disabled="searching2">
                  <span v-if="searching2" class="spinner-border spinner-border-sm"></span>
                  <span v-else>Search</span>
                </button>
              </div>
              <div v-if="results2.length" class="list-group mt-1 shadow-sm" style="position:absolute;z-index:100;width:calc(100% - 12px)">
                <button v-for="c in results2" :key="c.creatorId"
                  class="list-group-item list-group-item-action py-2 px-3 d-flex align-items-center gap-2"
                  @click="selectCreator(2, c)">
                  <span class="fw-semibold small">{{ c.channelName }}</span>
                  <span class="text-muted" style="font-size:0.72rem">{{ c.platform }} · {{ compact(c.subscribers) }}</span>
                </button>
              </div>
              <div v-if="selected2" class="mt-2 p-2 rounded border d-flex align-items-center gap-2" :style="`border-color:${catHex(selected2.category)}40!important;background:${catHex(selected2.category)}10`">
                <div class="fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                  :style="`background:${catHex(selected2.category)};width:30px;height:30px;border-radius:50%;font-size:12px`">
                  {{ letter(selected2.channelName) }}
                </div>
                <div>
                  <div class="fw-semibold small">{{ selected2.channelName }}</div>
                  <div class="text-muted" style="font-size:0.7rem">{{ selected2.platform }} · {{ selected2.category }}</div>
                </div>
                <button class="btn btn-sm btn-link ms-auto text-muted p-0" @click="selected2=null;q2=''">✕</button>
              </div>
            </div>
          </div>

          <div class="mt-3">
            <button class="btn btn-primary px-4" @click="compare"
              :disabled="!selected1 || !selected2 || comparing">
              <span v-if="comparing" class="spinner-border spinner-border-sm me-1"></span>
              Compare Creators
            </button>
          </div>
        </div>
      </div>

      <!-- Error -->
      <div v-if="error" class="alert alert-danger">{{ error }}</div>

      <!-- Comparison result -->
      <template v-if="result">

        <!-- ── Header tiles ────────────────────────────────────────── -->
        <div class="row g-4 mb-4">
          <div v-for="(side, key) in { creator1: result.creator1, creator2: result.creator2 }" :key="key"
            class="col-12 col-md-6">
            <div class="card border-0 shadow-sm h-100" :class="key === 'creator1' ? 'border-start border-4 border-primary' : 'border-start border-4 border-warning'">
              <div class="card-body d-flex align-items-center gap-3">
                <div class="fw-bold text-white d-flex align-items-center justify-content-center flex-shrink-0"
                  :style="`background:${catHex(side.category)};width:56px;height:56px;border-radius:50%;font-size:22px`">
                  {{ letter(side.channelName) }}
                </div>
                <div>
                  <h5 class="fw-bold mb-0">{{ side.channelName || '(unknown)' }}</h5>
                  <div class="d-flex gap-2 mt-1">
                    <span class="badge text-bg-danger" style="font-size:0.7rem">{{ side.platform }}</span>
                    <span class="badge rounded-pill border" :style="catCss(side.category)" style="font-size:0.7rem">{{ side.category || '—' }}</span>
                  </div>
                </div>
                <div v-if="side.creatorScore != null" class="ms-auto text-center">
                  <div class="text-muted" style="font-size:0.68rem">Score</div>
                  <div :class="['fw-bold fs-4', scoreClass(side.creatorScore)]">{{ side.creatorScore.toFixed(1) }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Metric comparison rows ────────────────────────────── -->
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-body p-0">
            <div v-for="metric in metrics" :key="metric.key" class="d-flex align-items-center border-bottom py-3 px-4">
              <!-- Left value -->
              <div class="col-4 text-end pe-3">
                <div class="fw-semibold" :class="winnerClass(metric.key, 'creator1')">
                  {{ metric.fmt(result.creator1[metric.key]) }}
                </div>
                <div v-if="isWinner(metric.key, 'creator1')" class="text-primary" style="font-size:0.68rem">▲ higher</div>
              </div>
              <!-- Label + bar -->
              <div class="col-4 text-center px-2">
                <div class="fw-semibold text-muted small mb-2">{{ metric.label }}</div>
                <!-- dual bar -->
                <div class="d-flex align-items-center gap-1" style="height:10px">
                  <div class="flex-grow-1 d-flex justify-content-end" style="height:10px">
                    <div class="rounded-start bg-primary" :style="`width:${barW(metric.key, 'creator1')}%;height:10px`"></div>
                  </div>
                  <div style="width:4px;height:10px;background:#e5e7eb"></div>
                  <div class="flex-grow-1" style="height:10px">
                    <div class="rounded-end bg-warning" :style="`width:${barW(metric.key, 'creator2')}%;height:10px`"></div>
                  </div>
                </div>
              </div>
              <!-- Right value -->
              <div class="col-4 text-start ps-3">
                <div class="fw-semibold" :class="winnerClass(metric.key, 'creator2')">
                  {{ metric.fmt(result.creator2[metric.key]) }}
                </div>
                <div v-if="isWinner(metric.key, 'creator2')" class="text-warning" style="font-size:0.68rem">▲ higher</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Score breakdown if available -->
        <div v-if="result.creator1.scoreBreakdown || result.creator2.scoreBreakdown"
          class="row g-4">
          <div v-for="(side, key) in { creator1: result.creator1, creator2: result.creator2 }" :key="key"
            class="col-12 col-md-6">
            <div v-if="side.scoreBreakdown" class="card border-0 shadow-sm">
              <div class="card-body">
                <h6 class="fw-semibold mb-2">Score Breakdown</h6>
                <div class="text-muted small">{{ side.scoreBreakdown }}</div>
              </div>
            </div>
          </div>
        </div>

      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';

const q1 = ref('');
const q2 = ref('');
const results1 = ref([]);
const results2 = ref([]);
const selected1 = ref(null);
const selected2 = ref(null);
const searching1 = ref(false);
const searching2 = ref(false);
const comparing  = ref(false);
const result     = ref(null);
const error      = ref('');

const route = useRoute();

const COLORS = { tech:'#3b82f6',fitness:'#10b981',gaming:'#8b5cf6',beauty:'#ec4899',finance:'#f59e0b',food:'#ef4444',travel:'#06b6d4',education:'#6366f1' };
function catHex(cat) { return COLORS[(cat||'').toLowerCase()]||'#6b7280'; }
function catCss(cat) { const c=catHex(cat); return `background:${c}18;color:${c};border-color:${c}40`; }
function letter(n) { return (n||'?').trim().charAt(0).toUpperCase(); }
function compact(n) { const v=Number(n||0); if(v>=1e9) return (v/1e9).toFixed(1)+'B'; if(v>=1e6) return (v/1e6).toFixed(1)+'M'; if(v>=1e3) return (v/1e3).toFixed(1)+'K'; return String(v); }
function pct(n) { return n!=null?(Number(n)*100).toFixed(2)+'%':'—'; }
function scoreClass(s) { if(s>=60) return 'text-success'; if(s>=30) return 'text-warning'; return 'text-danger'; }

const metrics = [
  { key: 'subscribers',     label: 'Subscribers',      fmt: compact },
  { key: 'averageViews',    label: 'Avg Views',         fmt: compact },
  { key: 'engagementRate',  label: 'Engagement Rate',   fmt: pct     },
  { key: 'uploadFrequency', label: 'Videos / Week',     fmt: v => Number(v??0).toFixed(2) },
  { key: 'creatorScore',    label: 'Creator Score',     fmt: v => v!=null ? Number(v).toFixed(1) : '—' },
];

function getVal(key, side) { return Number(result.value[side][key] ?? 0); }
function isWinner(key, side) {
  const other = side === 'creator1' ? 'creator2' : 'creator1';
  return getVal(key, side) > getVal(key, other);
}
function winnerClass(key, side) { return isWinner(key, side) ? 'text-success fw-bold' : ''; }
function barW(key, side) {
  const a = getVal(key, 'creator1');
  const b = getVal(key, 'creator2');
  const max = Math.max(a, b);
  if (max === 0) return 50;
  return Math.round((getVal(key, side) / max) * 100);
}

let debTimers = { 1: null, 2: null };
function debounce(n) {
  clearTimeout(debTimers[n]);
  debTimers[n] = setTimeout(() => search(n), 350);
}

async function search(n) {
  const q = n === 1 ? q1.value : q2.value;
  if (!q || q.length < 2) { if(n===1) results1.value=[]; else results2.value=[]; return; }
  if (n === 1) searching1.value = true; else searching2.value = true;
  try {
    const r = await api.get('/creators/search', { params: { search: q, pageSize: 6 } });
    if (n === 1) results1.value = r.data.items;
    else         results2.value = r.data.items;
  } catch { }
  finally { if (n===1) searching1.value=false; else searching2.value=false; }
}

function selectCreator(n, c) {
  if (n === 1) { selected1.value = c; q1.value = c.channelName; results1.value = []; }
  else         { selected2.value = c; q2.value = c.channelName; results2.value = []; }
}

async function pickFirst(n) {
  await search(n);
  const list = n === 1 ? results1.value : results2.value;
  if (list.length) selectCreator(n, list[0]);
}

async function compare() {
  if (!selected1.value || !selected2.value) return;
  comparing.value = true; error.value = '';
  try {
    const r = await api.get('/creators/compare', {
      params: { creatorId1: selected1.value.creatorId, creatorId2: selected2.value.creatorId }
    });
    result.value = r.data;
  } catch (e) {
    error.value = e.response?.data?.message || 'Comparison failed.';
  } finally {
    comparing.value = false;
  }
}

onMounted(async () => {
  // Pre-fill from query params (e.g. from leaderboard "Compare" link)
  const id1 = Number(route.query.creatorId1);
  if (id1 > 0) {
    try {
      const r = await api.get('/creators/search', { params: { page: 1, pageSize: 100 } });
      const found = r.data.items.find(x => x.creatorId === id1);
      if (found) { selected1.value = found; q1.value = found.channelName; }
    } catch {}
  }
});
</script>
