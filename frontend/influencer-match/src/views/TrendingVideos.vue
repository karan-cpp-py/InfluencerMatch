<template>
  <div class="trending-shell py-4">
    <div class="container-fluid" style="max-width: 1400px;">
      <section class="hero mb-4">
        <div class="d-flex justify-content-between align-items-start gap-3 flex-wrap">
          <div>
            <div class="hero-kicker mb-2">Creator Only</div>
            <h2 class="fw-bold mb-1">Trending on YouTube Today</h2>
            <p class="mb-0 text-muted">Live feed from YouTube most-popular API. Fresh fetch every visit with AI video analysis actions.</p>
          </div>
          <div class="d-flex gap-2">
            <span class="badge text-bg-light border px-3 py-2">{{ videos.length }} videos</span>
            <span class="badge text-bg-light border px-3 py-2">Region {{ filters.country || 'IN' }}</span>
          </div>
        </div>
      </section>

      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body py-3">
          <div class="row g-2 align-items-end">
            <div class="col-lg-2 col-md-4">
              <label class="form-label small text-muted">Country</label>
              <select class="form-select form-select-sm" v-model="filters.country" @change="load">
                <option value="IN">India</option>
                <option value="US">United States</option>
                <option value="GB">United Kingdom</option>
                <option value="CA">Canada</option>
                <option value="AU">Australia</option>
              </select>
            </div>
            <div class="col-lg-3 col-md-4">
              <label class="form-label small text-muted">Category</label>
              <select class="form-select form-select-sm" v-model="filters.category" @change="load">
                <option value="">All</option>
                <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
            <div class="col-lg-2 col-md-4">
              <label class="form-label small text-muted">Top</label>
              <select class="form-select form-select-sm" v-model.number="filters.topN" @change="load">
                <option :value="20">20</option>
                <option :value="35">35</option>
                <option :value="50">50</option>
              </select>
            </div>
            <div class="col-lg-5 d-flex justify-content-lg-end">
              <button class="btn btn-sm btn-primary" @click="load" :disabled="loading">
                <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
                Refresh Live Feed
              </button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary"></div>
        <p class="text-muted mt-2 mb-0">Fetching live trending videos from YouTube...</p>
      </div>

      <div v-else-if="error" class="alert alert-warning">{{ error }}</div>

      <div v-else class="row g-3">
        <div v-for="(v, idx) in videos" :key="v.videoId" class="col-xl-3 col-lg-4 col-md-6">
          <article class="video-card h-100">
            <div class="position-relative">
              <img v-if="v.thumbnailUrl" :src="v.thumbnailUrl" :alt="v.title" class="video-thumb" />
              <div v-else class="video-thumb thumb-fallback">YT</div>
              <span class="badge bg-dark rank-chip">#{{ idx + 1 }}</span>
            </div>
            <div class="p-2 p-lg-3 d-flex flex-column h-100">
              <h6 class="fw-semibold mb-1 text-truncate" :title="v.title">{{ v.title }}</h6>
              <div class="small text-muted mb-2 text-truncate">{{ v.channelName }}</div>

              <div class="d-flex flex-wrap gap-1 mb-2">
                <span class="badge text-bg-light border">{{ v.category || 'General' }}</span>
                <span class="badge text-bg-light border">{{ hoursAgo(v.hoursSincePublish) }}</span>
                <span class="badge" :class="scoreBadge(v.viralScore)">Score {{ pct(v.viralScore) }}</span>
              </div>

              <div class="small d-flex justify-content-between text-muted mb-2">
                <span>{{ fmt(v.viewCount) }} views</span>
                <span>{{ fmt(v.likeCount) }} likes</span>
                <span>{{ fmt(v.commentCount) }} comments</span>
              </div>

              <div class="progress mb-2" style="height:7px">
                <div class="progress-bar bg-danger" :style="{ width: pct(v.viralScore) }"></div>
              </div>

              <div class="d-grid gap-2 mt-auto">
                <button class="btn btn-sm trend-btn trend-btn-primary" @click="analyzeVideo(v)">
                  Analyze with AI
                </button>
                <a :href="`https://youtube.com/watch?v=${v.videoId}`" target="_blank" rel="noopener noreferrer" class="btn btn-sm trend-btn trend-btn-neutral">Watch on YouTube</a>
              </div>
            </div>
          </article>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';

const router = useRouter();

const videos     = ref([]);
const loading    = ref(false);
const error      = ref('');

const filters = reactive({ topN: 35, category: '', country: 'IN' });

const categories = [
  'General', 'Music', 'Gaming', 'Sports', 'Entertainment', 'News', 'Education', 'Tech', 'Comedy', 'Travel'
];

async function load() {
  loading.value = true;
  error.value   = '';
  try {
    const params = { topN: filters.topN };
    if (filters.category) params.category = filters.category;
    if (filters.country) params.country = filters.country;

    const { data } = await api.get('/videos/trending', { params });
    videos.value = Array.isArray(data) ? data : [];
  } catch (e) {
    error.value = e?.response?.data?.message ?? e.message ?? 'Failed to load trending videos.';
  } finally {
    loading.value = false;
  }
}

async function analyzeVideo(video) {
  router.push({
    path: '/creator/latest-video-analysis',
    query: { videoId: video?.videoId }
  });
}

// ── Formatting helpers ────────────────────────────────────────────────────

function pct(val) {
  if (val == null) return '0%';
  return Math.round(val * 100) + '%';
}

function fmt(n) {
  if (n == null) return '0';
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 1_000)     return (n / 1_000    ).toFixed(1) + 'K';
  return n.toString();
}

function hoursAgo(h) {
  if (h == null) return '';
  if (h < 1)   return 'Just now';
  if (h < 24)  return Math.round(h) + 'h ago';
  return Math.round(h / 24) + 'd ago';
}

function scoreBadge(s) {
  if (s >= 0.7) return 'text-bg-danger';
  if (s >= 0.4) return 'text-bg-warning';
  return 'text-bg-success';
}

onMounted(load);
</script>

<style scoped>
.trending-shell {
  background:
    radial-gradient(circle at 0% 0%, rgba(239, 68, 68, 0.12), transparent 30%),
    radial-gradient(circle at 100% 0%, rgba(14, 165, 233, 0.13), transparent 34%),
    #f8fafc;
}

.hero {
  border-radius: 18px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  background: linear-gradient(125deg, #ffffff 0%, #f8fafc 56%, #eff6ff 100%);
  padding: 1rem 1.2rem;
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.18rem 0.55rem;
  font-size: 0.68rem;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: #991b1b;
  background: rgba(254, 226, 226, 0.8);
}

.video-card {
  border-radius: 14px;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.24);
  background: #ffffff;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}

/* Ensure the card is a column flex container so the content (including buttons)
   stays inside the card and sticky to the bottom when necessary. */
.video-card {
  display: flex;
  flex-direction: column;
}

.video-thumb {
  width: 100%;
  aspect-ratio: 16/9;
  object-fit: cover;
  display: block;
}

/* Prevent the thumbnail wrapper from shrinking and make the content area
   take the remaining space so action buttons remain inside the card. */
.video-card .position-relative {
  flex-shrink: 0;
}

.video-card .p-2.p-lg-3 {
  flex: 1 1 auto;
  display: flex;
  flex-direction: column;
}

.thumb-fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0f172a;
  color: #e2e8f0;
}

.rank-chip {
  position: absolute;
  top: 8px;
  left: 8px;
}

.trend-btn {
  width: 100%;
  font-weight: 700;
  border-width: 1px;
  opacity: 1;
}

.trend-btn-primary {
  color: #ffffff !important;
  background: #0f172a !important;
  border-color: #0f172a !important;
}

.trend-btn-outline {
  color: #1d4ed8 !important;
  background: #ffffff !important;
  border-color: #93c5fd !important;
}

.trend-btn-neutral {
  color: #334155 !important;
  background: #f8fafc !important;
  border-color: #cbd5e1 !important;
}

.trend-btn:disabled {
  opacity: 0.65;
}

</style>
