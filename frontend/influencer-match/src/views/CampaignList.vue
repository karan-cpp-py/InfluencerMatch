<template>
  <div class="campaign-list-page py-4">
    <div class="container" style="max-width: 1120px;">
      <section class="list-hero mb-4">
        <p class="hero-kicker mb-2">Creator Opportunities</p>
        <h3 class="mb-1 fw-bold">Available Campaigns</h3>
        <p class="mb-0 text-light-emphasis">Browse active brand briefs and identify campaigns aligned with your niche.</p>
      </section>

      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <article class="meta-card">
            <p class="meta-label">Total Campaigns</p>
            <p class="meta-value">{{ campaigns.length }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="meta-card">
            <p class="meta-label">Avg Budget</p>
            <p class="meta-value">${{ avgBudget }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="meta-card">
            <p class="meta-label">Unique Categories</p>
            <p class="meta-value">{{ categoryCount }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="meta-card">
            <p class="meta-label">Locations</p>
            <p class="meta-value">{{ locationCount }}</p>
          </article>
        </div>
      </div>

      <div class="card border-0 shadow-sm filter-card mb-4">
        <div class="card-body p-3 d-flex flex-wrap gap-2 align-items-center">
          <input v-model="search" class="form-control form-control-sm filter-input" placeholder="Search category or location" />
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

      <div v-else-if="!filteredCampaigns.length" class="text-center text-muted py-5">
        <p class="mb-1 fw-semibold">No campaigns match your filters.</p>
        <p class="mb-0">Try a broader category or clear search.</p>
      </div>

      <div v-else class="row g-3">
        <div v-for="c in filteredCampaigns" :key="c.campaignId" class="col-md-6 col-xl-4">
          <div class="card h-100 shadow-sm campaign-card border-0">
            <div class="card-body">
              <div class="d-flex justify-content-between align-items-start mb-3">
                <h5 class="card-title mb-0">Campaign #{{ c.campaignId }}</h5>
                <span class="badge text-bg-primary">${{ Number(c.budget || 0).toLocaleString() }}</span>
              </div>
              <p class="card-text mb-2">
                <strong>Category:</strong> {{ c.category || '-' }}
              </p>
              <p class="card-text mb-0">
                <strong>Target:</strong> {{ c.targetLocation || '-' }}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import api from '../services/api';

const campaigns = ref([]);
const loading = ref(false);
const search = ref('');
const selectedCategory = ref('');

const categoryOptions = computed(() =>
  [...new Set(campaigns.value.map((c) => String(c.category || '').trim()).filter(Boolean))].sort()
);

const filteredCampaigns = computed(() => {
  const q = search.value.trim().toLowerCase();
  return campaigns.value.filter((c) => {
    const category = String(c.category || '');
    const location = String(c.targetLocation || '');
    const matchesCategory = !selectedCategory.value || category === selectedCategory.value;
    const matchesSearch = !q || category.toLowerCase().includes(q) || location.toLowerCase().includes(q);
    return matchesCategory && matchesSearch;
  });
});

const avgBudget = computed(() => {
  if (!campaigns.value.length) return '0';
  const total = campaigns.value.reduce((sum, c) => sum + Number(c.budget || 0), 0);
  return Math.round(total / campaigns.value.length).toLocaleString();
});

const categoryCount = computed(() => categoryOptions.value.length);
const locationCount = computed(() => {
  const set = new Set(campaigns.value.map((c) => String(c.targetLocation || '').trim()).filter(Boolean));
  return set.size;
});

onMounted(async () => {
  loading.value = true;
  try {
    const res = await api.get('/campaign/all');
    campaigns.value = res.data;
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
});

function resetFilters() {
  search.value = '';
  selectedCategory.value = '';
}
</script>

<style scoped>
.campaign-list-page {
  background: radial-gradient(circle at 8% 0%, rgba(59, 130, 246, 0.08), transparent 42%),
    radial-gradient(circle at 96% 14%, rgba(16, 185, 129, 0.08), transparent 35%);
}

.list-hero {
  border-radius: 18px;
  padding: 1.15rem;
  color: #e2e8f0;
  background: linear-gradient(130deg, #0f172a, #1d4ed8 58%, #0369a1);
}

.hero-kicker {
  display: inline-flex;
  padding: 0.2rem 0.52rem;
  border-radius: 999px;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  background: rgba(147, 197, 253, 0.26);
  color: #dbeafe;
}

.meta-card {
  border-radius: 14px;
  background: #fff;
  border: 1px solid rgba(148, 163, 184, 0.2);
  box-shadow: 0 7px 16px rgba(15, 23, 42, 0.06);
  padding: 0.75rem;
}

.meta-label {
  margin-bottom: 0.1rem;
  font-size: 0.72rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  color: #64748b;
}

.meta-value {
  margin-bottom: 0;
  font-size: 1.35rem;
  font-weight: 700;
}

.filter-card,
.campaign-card {
  border-radius: 16px;
}

.filter-input {
  min-width: 220px;
}

.filter-select {
  min-width: 180px;
}
</style>
