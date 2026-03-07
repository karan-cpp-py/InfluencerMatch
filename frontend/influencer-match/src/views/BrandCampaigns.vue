<template>
  <div class="campaigns-page py-4">
    <div class="container" style="max-width: 1080px;">
      <section class="campaigns-hero mb-4">
        <div>
          <p class="hero-kicker mb-2">Brand Workspace</p>
          <h3 class="mb-1 fw-bold">My Campaigns</h3>
          <p class="mb-0 text-light-emphasis">Track performance-ready campaigns and jump directly to match results.</p>
        </div>
        <router-link to="/brand" class="btn btn-light btn-sm fw-semibold">+ New Campaign</router-link>
      </section>

      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Total</p>
            <p class="summary-value">{{ campaigns.length }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Avg Budget</p>
            <p class="summary-value">${{ avgBudget }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Top Category</p>
            <p class="summary-value small-value">{{ topCategory }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="summary-card">
            <p class="summary-label">Locations</p>
            <p class="summary-value">{{ locationCount }}</p>
          </article>
        </div>
      </div>

      <div class="card border-0 shadow-sm table-card">
        <div class="card-body p-3 p-md-4">
          <div class="d-flex flex-wrap gap-2 align-items-center justify-content-between mb-3">
            <h5 class="fw-semibold mb-0">Campaign Table</h5>
            <input v-model="search" class="form-control form-control-sm table-search" placeholder="Filter by category or location" />
          </div>

          <div v-if="loading" class="text-center py-4">
            <div class="spinner-border text-primary" role="status"></div>
          </div>

          <div v-else-if="!filteredCampaigns.length" class="empty-state text-center py-5 text-muted">
            <div class="fs-2 mb-2">No campaigns found</div>
            <p class="mb-3">Create your first campaign to start receiving creator matches.</p>
            <router-link to="/brand" class="btn btn-primary btn-sm">Create Campaign</router-link>
          </div>

          <div v-else class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>ID</th>
                  <th>Budget</th>
                  <th>Category</th>
                  <th>Target Location</th>
                  <th class="text-end">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="c in filteredCampaigns" :key="c.campaignId">
                  <td>#{{ c.campaignId }}</td>
                  <td class="fw-semibold">${{ Number(c.budget || 0).toLocaleString() }}</td>
                  <td>{{ c.category || '-' }}</td>
                  <td>{{ c.targetLocation || '-' }}</td>
                  <td class="text-end">
                    <button class="btn btn-sm btn-primary me-2" @click="viewResults(c.campaignId)">Results</button>
                    <button class="btn btn-sm btn-outline-secondary" @click="editCampaign(c.campaignId)">Edit</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { parseJwt } from '../services/jwt';

const campaigns = ref([]);
const loading = ref(false);
const router = useRouter();
const search = ref('');

const filteredCampaigns = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return campaigns.value;
  return campaigns.value.filter((c) =>
    String(c.category || '').toLowerCase().includes(q) ||
    String(c.targetLocation || '').toLowerCase().includes(q)
  );
});

const avgBudget = computed(() => {
  if (!campaigns.value.length) return '0';
  const total = campaigns.value.reduce((sum, c) => sum + Number(c.budget || 0), 0);
  return Math.round(total / campaigns.value.length).toLocaleString();
});

const topCategory = computed(() => {
  if (!campaigns.value.length) return '-';
  const counter = {};
  for (const c of campaigns.value) {
    const key = c.category || 'Uncategorized';
    counter[key] = (counter[key] || 0) + 1;
  }
  return Object.entries(counter).sort((a, b) => b[1] - a[1])[0][0];
});

const locationCount = computed(() => {
  const set = new Set(campaigns.value.map((c) => String(c.targetLocation || '').trim()).filter(Boolean));
  return set.size;
});

onMounted(async () => {
  loading.value = true;
  try {
    const token = localStorage.getItem('token');
    const payload = parseJwt(token);
    const brandId = payload.nameid;
    const res = await api.get(`/campaign/brand/${brandId}`);
    campaigns.value = res.data;
  } finally {
    loading.value = false;
  }
});

function viewResults(id) {
  router.push(`/results/${id}`);
}

function editCampaign(id) {
  router.push({ path: '/brand', query: { campaignId: id } });
}
</script>

<style scoped>
.campaigns-page {
  background: radial-gradient(circle at 10% 0%, rgba(14, 165, 233, 0.08), transparent 38%),
    radial-gradient(circle at 90% 16%, rgba(34, 197, 94, 0.07), transparent 33%);
}

.campaigns-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #e2e8f0;
  background: linear-gradient(120deg, #0f172a 0%, #1e3a8a 60%, #0369a1 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.hero-kicker {
  display: inline-flex;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  border-radius: 999px;
  padding: 0.2rem 0.55rem;
  background: rgba(147, 197, 253, 0.22);
  color: #dbeafe;
}

.summary-card {
  border-radius: 16px;
  background: #fff;
  border: 1px solid rgba(148, 163, 184, 0.18);
  box-shadow: 0 8px 18px rgba(15, 23, 42, 0.06);
  padding: 0.8rem;
  height: 100%;
}

.summary-label {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: #64748b;
  margin-bottom: 0.15rem;
}

.summary-value {
  margin-bottom: 0;
  font-size: 1.38rem;
  font-weight: 700;
  color: #0f172a;
}

.small-value {
  font-size: 1rem;
}

.table-card {
  border-radius: 18px;
}

.table-search {
  width: 260px;
}

@media (max-width: 768px) {
  .campaigns-hero {
    flex-direction: column;
  }

  .table-search {
    width: 100%;
  }
}
</style>