<template>
  <div class="container-fluid py-4">
    <!-- Header -->
    <div class="d-flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
      <div>
        <h2 class="mb-0 fw-bold">
          <span class="me-2">🔥</span>Trending Videos
        </h2>
        <p class="text-muted mb-0 small">Viral Content Prediction — scored by velocity, acceleration & engagement</p>
      </div>
      <button class="btn btn-primary btn-sm" @click="refresh" :disabled="refreshing">
        <span v-if="refreshing" class="spinner-border spinner-border-sm me-1"></span>
        {{ refreshing ? 'Refreshing…' : '↺ Refresh Scores' }}
      </button>
    </div>

    <!-- Algorithm explainer -->
    <div class="alert alert-info py-2 small mb-3">
      <strong>Algorithm:</strong>
      ViralScore = 0.5 × <em>ViewsVelocity</em> + 0.3 × <em>GrowthAcceleration</em> + 0.2 × <em>EngagementMomentum</em>
      &nbsp;|&nbsp; All components normalised 0–1 across the batch.
    </div>

    <!-- Filters -->
    <div class="row g-2 mb-4">
      <div class="col-auto">
        <select class="form-select form-select-sm" v-model="filters.category" @change="load">
          <option value="">All Categories</option>
          <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="col-auto">
        <select class="form-select form-select-sm" v-model="filters.country" @change="load">
          <option value="">All Countries</option>
          <option v-for="c in countries" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="col-auto">
        <select class="form-select form-select-sm" v-model.number="filters.topN" @change="load">
          <option :value="20">Top 20</option>
          <option :value="50">Top 50</option>
          <option :value="100">Top 100</option>
        </select>
      </div>
    </div>

    <!-- Loading / empty states -->
    <div v-if="loading" class="text-center py-5">
      <div class="spinner-border text-primary"></div>
      <p class="mt-2 text-muted">Loading trending videos…</p>
    </div>

    <div v-else-if="error" class="alert alert-danger">{{ error }}</div>

    <div v-else-if="videos.length === 0" class="alert alert-secondary">
      No trending videos found. Click <strong>↺ Refresh Scores</strong> to compute viral scores.
    </div>

    <!-- Video grid -->
    <div v-else class="row row-cols-1 row-cols-md-2 row-cols-xl-3 g-4">
      <div v-for="(v, idx) in videos" :key="v.videoId" class="col">
        <div class="card h-100 shadow-sm border-0 position-relative">

          <!-- Rank badge -->
          <span class="position-absolute top-0 start-0 m-2 badge bg-dark opacity-75">#{{ idx + 1 }}</span>

          <!-- Thumbnail -->
          <div class="ratio ratio-16x9 bg-dark overflow-hidden rounded-top">
            <img
              v-if="v.thumbnailUrl"
              :src="v.thumbnailUrl"
              :alt="v.title"
              class="img-fluid w-100 h-100"
              style="object-fit:cover"
            />
            <div v-else class="d-flex align-items-center justify-content-center bg-secondary text-white fs-4">
              📹
            </div>
          </div>

          <div class="card-body pb-2">
            <!-- Title -->
            <h6 class="card-title text-truncate mb-1" :title="v.title">{{ v.title }}</h6>

            <!-- Channel + meta -->
            <div class="d-flex align-items-center gap-2 mb-2 flex-wrap">
              <span class="badge bg-primary-subtle text-primary-emphasis">{{ v.channelName }}</span>
              <span v-if="v.category" class="badge bg-secondary-subtle text-secondary-emphasis">{{ v.category }}</span>
              <span v-if="v.country"  class="badge bg-light text-muted border">{{ v.country }}</span>
              <span class="ms-auto badge bg-warning-subtle text-warning-emphasis small">
                {{ hoursAgo(v.hoursSincePublish) }}
              </span>
            </div>

            <!-- Stats row -->
            <div class="row row-cols-3 text-center small mb-3">
              <div>
                <div class="fw-bold">{{ fmt(v.viewCount) }}</div>
                <div class="text-muted">Views</div>
              </div>
              <div>
                <div class="fw-bold">{{ fmt(v.likeCount) }}</div>
                <div class="text-muted">Likes</div>
              </div>
              <div>
                <div class="fw-bold">{{ fmt(v.commentCount) }}</div>
                <div class="text-muted">Comments</div>
              </div>
            </div>

            <!-- ViralScore bar -->
            <div class="mb-2">
              <div class="d-flex justify-content-between small mb-1">
                <span class="fw-semibold">ViralScore</span>
                <span class="fw-bold" :class="scoreColor(v.viralScore)">
                  {{ pct(v.viralScore) }}
                </span>
              </div>
              <div class="progress" style="height:10px">
                <div
                  class="progress-bar"
                  :class="scoreBarClass(v.viralScore)"
                  :style="{ width: pct(v.viralScore) }"
                ></div>
              </div>
            </div>

            <!-- Component bars -->
            <div class="row g-1 small">
              <div class="col-12">
                <div class="d-flex justify-content-between">
                  <span class="text-muted">Velocity</span>
                  <span>{{ pct(v.viewsVelocity) }}</span>
                </div>
                <div class="progress mb-1" style="height:5px">
                  <div class="progress-bar bg-info" :style="{ width: pct(v.viewsVelocity) }"></div>
                </div>
              </div>
              <div class="col-12">
                <div class="d-flex justify-content-between">
                  <span class="text-muted">Acceleration</span>
                  <span>{{ pct(v.growthAcceleration) }}</span>
                </div>
                <div class="progress mb-1" style="height:5px">
                  <div class="progress-bar bg-warning" :style="{ width: pct(v.growthAcceleration) }"></div>
                </div>
              </div>
              <div class="col-12">
                <div class="d-flex justify-content-between">
                  <span class="text-muted">Engagement</span>
                  <span>{{ pct(v.engagementMomentum) }}</span>
                </div>
                <div class="progress" style="height:5px">
                  <div class="progress-bar bg-success" :style="{ width: pct(v.engagementMomentum) }"></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Card footer -->
          <div class="card-footer bg-transparent border-0 small text-muted pt-1 pb-2">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
              <span>Scored {{ calcAgo(v.calculatedAt) }} &bull; {{ v.subscribers?.toLocaleString() }} subs</span>
              <router-link
                :to="`/creator/${v.creatorId}/latest-video-analysis?videoId=${v.videoId}&videoTitle=${encodeURIComponent(v.title || '')}`"
                class="btn btn-sm btn-outline-dark"
              >
                Analyze with AI
              </router-link>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue';
import api from '../services/api';

const videos     = ref([]);
const loading    = ref(false);
const refreshing = ref(false);
const error      = ref('');

const filters = reactive({ topN: 50, category: '', country: '' });

// Unique category/country lists derived from loaded data
const categories = ref([]);
const countries  = ref([]);

async function load() {
  loading.value = true;
  error.value   = '';
  try {
    const params = { topN: filters.topN };
    if (filters.category) params.category = filters.category;
    if (filters.country)  params.country  = filters.country;

    const { data } = await api.get('/videos/trending', { params });
    videos.value = data;

    // Populate filter dropdowns from all data (first load only)
    if (!filters.category && !filters.country) {
      categories.value = [...new Set(data.map(v => v.category).filter(Boolean))].sort();
      countries.value  = [...new Set(data.map(v => v.country ).filter(Boolean))].sort();
    }
  } catch (e) {
    error.value = e?.response?.data?.message ?? e.message ?? 'Failed to load trending videos.';
  } finally {
    loading.value = false;
  }
}

async function refresh() {
  refreshing.value = true;
  error.value = '';
  try {
    await api.post('/videos/trending/refresh');
    await load();
  } catch (e) {
    error.value = e?.response?.data?.message ?? 'Refresh failed.';
  } finally {
    refreshing.value = false;
  }
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

function calcAgo(dt) {
  if (!dt) return '';
  const mins = Math.round((Date.now() - new Date(dt)) / 60000);
  if (mins < 1)   return 'just now';
  if (mins < 60)  return mins + 'm ago';
  return Math.round(mins / 60) + 'h ago';
}

function scoreColor(s) {
  if (s >= 0.7) return 'text-danger';
  if (s >= 0.4) return 'text-warning';
  return 'text-success';
}

function scoreBarClass(s) {
  if (s >= 0.7) return 'bg-danger';
  if (s >= 0.4) return 'bg-warning';
  return 'bg-success';
}

onMounted(load);
</script>
