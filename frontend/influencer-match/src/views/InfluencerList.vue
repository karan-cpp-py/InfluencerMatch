<template>
  <div class="influencer-list-page py-4">
    <div class="container" style="max-width: 1140px;">
      <section class="list-hero mb-4">
        <p class="hero-kicker mb-2">Discovery Pool</p>
        <h3 class="mb-1 fw-bold">All Influencers</h3>
        <p class="mb-0 text-light-emphasis">Browse profiles and open detailed pages to evaluate fit for your campaign goals.</p>
      </section>

      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Profiles</p>
            <p class="summary-value">{{ influencers.length }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Avg Followers</p>
            <p class="summary-value">{{ compact(avgFollowers) }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Avg Price</p>
            <p class="summary-value">${{ Math.round(avgPrice).toLocaleString() }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Categories</p>
            <p class="summary-value">{{ categoryCount }}</p>
          </article>
        </div>
      </div>

      <div class="card border-0 shadow-sm panel-card mb-4">
        <div class="card-body p-3 d-flex flex-wrap gap-2 align-items-center">
          <input v-model="search" class="form-control form-control-sm filter-input" placeholder="Search by name, category or location" />
          <select v-model="selectedCategory" class="form-select form-select-sm filter-select">
            <option value="">All Categories</option>
            <option v-for="cat in categoryOptions" :key="cat" :value="cat">{{ cat }}</option>
          </select>
          <button class="btn btn-outline-secondary btn-sm" @click="resetFilters">Reset</button>
        </div>
      </div>

      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary" role="status"></div>
      </div>

      <div v-else-if="!filteredInfluencers.length" class="text-center text-muted py-5">
        <p class="fw-semibold mb-1">No influencers match your filters.</p>
        <p class="mb-0">Try clearing filters or searching with a different keyword.</p>
      </div>

      <div v-else class="row g-3">
        <div v-for="inf in filteredInfluencers" :key="inf.influencerId" class="col-md-6 col-xl-4">
          <router-link :to="`/influencer/${inf.influencerId}`" class="text-decoration-none text-dark">
            <div class="card h-100 shadow-sm border-0 panel-card influencer-card">
              <div class="card-body">
                <div class="d-flex justify-content-between align-items-start mb-2">
                  <h5 class="card-title mb-0">{{ inf.userName || 'Unnamed' }}</h5>
                  <span class="badge text-bg-primary">${{ Number(inf.pricePerPost || 0).toLocaleString() }}</span>
                </div>
                <div class="small text-muted mb-2">{{ inf.category || '-' }} · {{ inf.location || '-' }}</div>
                <div class="stat-grid">
                  <div class="stat-cell">
                    <div class="stat-label">Followers</div>
                    <div class="stat-value">{{ compact(inf.followers) }}</div>
                  </div>
                  <div class="stat-cell">
                    <div class="stat-label">Engagement</div>
                    <div class="stat-value">{{ pct(inf.engagementRate) }}</div>
                  </div>
                </div>
              </div>
            </div>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import api from '../services/api';

const influencers = ref([]);
const loading = ref(false);
const search = ref('');
const selectedCategory = ref('');

const categoryOptions = computed(() =>
  [...new Set(influencers.value.map((i) => String(i.category || '').trim()).filter(Boolean))].sort()
);

const filteredInfluencers = computed(() => {
  const q = search.value.trim().toLowerCase();
  return influencers.value.filter((inf) => {
    const name = String(inf.userName || '').toLowerCase();
    const category = String(inf.category || '').toLowerCase();
    const location = String(inf.location || '').toLowerCase();
    const matchCategory = !selectedCategory.value || inf.category === selectedCategory.value;
    const matchSearch = !q || name.includes(q) || category.includes(q) || location.includes(q);
    return matchCategory && matchSearch;
  });
});

const avgFollowers = computed(() => {
  if (!influencers.value.length) return 0;
  return influencers.value.reduce((sum, i) => sum + Number(i.followers || 0), 0) / influencers.value.length;
});

const avgPrice = computed(() => {
  if (!influencers.value.length) return 0;
  return influencers.value.reduce((sum, i) => sum + Number(i.pricePerPost || 0), 0) / influencers.value.length;
});

const categoryCount = computed(() => categoryOptions.value.length);

onMounted(async () => {
  loading.value = true;
  try {
    const res = await api.get('/influencer/all');
    influencers.value = res.data;
  } finally {
    loading.value = false;
  }
});

function compact(n) {
  const v = Number(n || 0);
  if (v >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M';
  if (v >= 1_000) return (v / 1_000).toFixed(1) + 'K';
  return String(v);
}

function pct(v) {
  if (v == null || Number.isNaN(Number(v))) return '-';
  return Number(v).toFixed(2) + '%';
}

function resetFilters() {
  search.value = '';
  selectedCategory.value = '';
}
</script>

<style scoped>
.influencer-list-page {
  background: radial-gradient(circle at 10% 0%, rgba(14, 165, 233, 0.08), transparent 40%),
    radial-gradient(circle at 90% 14%, rgba(16, 185, 129, 0.08), transparent 36%);
}

.list-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #dbeafe;
  background: linear-gradient(124deg, #0f172a 0%, #1d4ed8 58%, #0369a1 100%);
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.56rem;
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.24);
}

.summary-card {
  border-radius: 14px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  background: #fff;
  box-shadow: 0 7px 16px rgba(15, 23, 42, 0.06);
  padding: 0.75rem;
}

.summary-label {
  margin-bottom: 0.1rem;
  font-size: 0.72rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #64748b;
}

.summary-value {
  margin-bottom: 0;
  font-weight: 700;
  font-size: 1.3rem;
}

.panel-card {
  border-radius: 16px;
}

.filter-input {
  min-width: 230px;
}

.filter-select {
  min-width: 180px;
}

.influencer-card {
  transition: transform .16s ease, box-shadow .16s ease;
}

.influencer-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 11px 20px rgba(15, 23, 42, 0.11) !important;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.5rem;
}

.stat-cell {
  border-radius: 10px;
  background: #f8fafc;
  padding: 0.5rem;
}

.stat-label {
  font-size: 0.7rem;
  color: #64748b;
}

.stat-value {
  font-size: 0.95rem;
  font-weight: 700;
  color: #0f172a;
}
</style>
