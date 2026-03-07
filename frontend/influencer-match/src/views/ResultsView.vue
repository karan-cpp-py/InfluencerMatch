<template>
  <div class="container py-4 results-shell">
    <section class="results-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Match Intelligence</p>
        <h3 class="fw-bold mb-1">Matching Results</h3>
        <p class="mb-0 text-light-emphasis">Score quality, trust bands, and recommendation rationale in one view.</p>
      </div>
    </section>

    <div class="d-flex flex-wrap gap-2 mb-3 mode-switch">
      <button
        class="btn btn-sm"
        :class="includeOverBudget ? 'btn-outline-primary' : 'btn-primary'"
        @click="setMode(false)"
        :disabled="loading"
      >
        Only Budget-Fit
      </button>
      <button
        class="btn btn-sm"
        :class="includeOverBudget ? 'btn-primary' : 'btn-outline-primary'"
        @click="setMode(true)"
        :disabled="loading"
      >
        All Candidates
      </button>
      <span class="small text-muted align-self-center">{{ modeDescription }}</span>
    </div>

    <div v-if="loading" class="text-center py-5">
      <div class="spinner-border text-primary"></div>
    </div>

    <div v-else>
      <div class="row g-3 mb-3" v-if="results.length">
        <div class="col-md-7">
          <div class="card border-0 shadow-sm panel-card h-100">
            <div class="card-body p-3 p-md-4">
              <h6 class="fw-semibold mb-1">Score Distribution</h6>
              <p class="small text-muted mb-3">See how creators spread across match score buckets.</p>
              <div class="chart-box">
                <AppBarChart :data="scoreDistributionData" :options="barOptions" />
              </div>
            </div>
          </div>
        </div>
        <div class="col-md-5">
          <div class="card border-0 shadow-sm panel-card h-100">
            <div class="card-body p-3 p-md-4">
              <h6 class="fw-semibold mb-1">Trust Band Mix</h6>
              <p class="small text-muted mb-3">Confidence profile of this shortlist.</p>
              <div class="chart-box compact">
                <AppDoughnutChart :data="trustBandData" :options="doughnutOptions" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="table-responsive shadow-sm panel-card" v-if="results.length">
        <table class="table table-striped mb-0 align-middle">
          <thead class="table-primary">
            <tr>
              <th>Type</th>
              <th>Name</th>
              <th>Followers</th>
              <th>Engagement Rate</th>
              <th>Price</th>
              <th>Score</th>
              <th>Trust</th>
              <th>Why Recommended</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in results" :key="`${r.sourceType || 'Influencer'}-${r.sourceId || r.influencerId}`">
              <td>
                <span class="badge" :class="(r.sourceType || 'Influencer') === 'Creator' ? 'text-bg-info' : 'text-bg-primary'">
                  {{ r.sourceType || 'Influencer' }}
                </span>
              </td>
              <td>
                <router-link v-if="(r.sourceType || 'Influencer') === 'Influencer'" :to="`/influencer/${r.sourceId || r.influencerId}`">{{ r.name }}</router-link>
                <router-link v-else :to="`/creator/${r.sourceId || r.influencerId}/analytics`">{{ r.name }}</router-link>
              </td>
              <td>{{ r.followers }}</td>
              <td>{{ r.engagementRate }}</td>
              <td>{{ r.pricePerPost }}</td>
              <td>{{ Number(r.score || 0).toFixed(2) }}</td>
              <td>
                <div class="small fw-semibold">{{ r.trustBand }}</div>
                <div class="small text-muted">Response: {{ r.responseRate?.toFixed?.(0) || r.responseRate }}%</div>
                <div class="small text-muted">Completion: {{ r.completionRate?.toFixed?.(0) || r.completionRate }}%</div>
                <div class="small text-muted">Outcomes: {{ r.previousCampaignOutcomes }}</div>
              </td>
              <td>
                <ul class="small mb-0 ps-3">
                  <li v-for="reason in (r.whyRecommended || [])" :key="reason">{{ reason }}</li>
                </ul>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="!results.length" class="alert alert-secondary mb-0">
        No matching results found for this campaign yet.
      </div>
    </div>

    <div v-if="error" class="alert alert-danger mt-3 mb-0">{{ error }}</div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';
import AppBarChart from '../components/charts/AppBarChart.vue';
import AppDoughnutChart from '../components/charts/AppDoughnutChart.vue';

const route = useRoute();
const results = ref([]);
const loading = ref(false);
const error = ref('');
const includeOverBudget = ref(false);

const modeDescription = computed(() =>
  includeOverBudget.value
    ? 'Showing all matched creators/influencers, including over-budget options.'
    : 'Showing only matches that fit campaign budget.'
);

const scoreBuckets = computed(() => {
  const buckets = [
    { label: '0.0 - 0.2', min: 0, max: 0.2, count: 0 },
    { label: '0.2 - 0.4', min: 0.2, max: 0.4, count: 0 },
    { label: '0.4 - 0.6', min: 0.4, max: 0.6, count: 0 },
    { label: '0.6 - 0.8', min: 0.6, max: 0.8, count: 0 },
    { label: '0.8 - 1.0', min: 0.8, max: 1.001, count: 0 },
  ];

  for (const item of results.value) {
    const score = Number(item?.score || 0);
    const bucket = buckets.find(b => score >= b.min && score < b.max);
    if (bucket) bucket.count += 1;
  }

  return buckets;
});

const scoreDistributionData = computed(() => ({
  labels: scoreBuckets.value.map(x => x.label),
  datasets: [
    {
      label: 'Creators',
      data: scoreBuckets.value.map(x => x.count),
      borderRadius: 8,
      backgroundColor: ['#93c5fd', '#60a5fa', '#3b82f6', '#2563eb', '#1d4ed8'],
    },
  ],
}));

const trustBandData = computed(() => {
  const counts = {};
  for (const item of results.value) {
    const key = item?.trustBand || 'Unknown';
    counts[key] = (counts[key] || 0) + 1;
  }

  const labels = Object.keys(counts);
  return {
    labels,
    datasets: [
      {
        label: 'Trust Band',
        data: labels.map(label => counts[label]),
        backgroundColor: ['#22c55e', '#f59e0b', '#ef4444', '#0ea5e9', '#8b5cf6'],
        borderWidth: 0,
      },
    ],
  };
});

const barOptions = {
  plugins: {
    legend: { display: false },
  },
  scales: {
    y: {
      beginAtZero: true,
      ticks: { precision: 0 },
    },
  },
};

const doughnutOptions = {
  plugins: {
    legend: {
      position: 'bottom',
    },
  },
};

async function loadMatches() {
  loading.value = true;
  error.value = '';
  try {
    const id = route.params.campaignId;
    const res = await api.get(`/match/campaign/${id}`, {
      params: { includeOverBudget: includeOverBudget.value }
    });
    results.value = Array.isArray(res.data) ? res.data : [];
  } catch (e) {
    results.value = [];
    error.value = e.response?.data?.error || 'Failed to load matching results.';
  } finally {
    loading.value = false;
  }
}

function setMode(nextValue) {
  if (includeOverBudget.value === nextValue) return;
  includeOverBudget.value = nextValue;
  loadMatches();
}

onMounted(async () => {
  await loadMatches();
});
</script>

<style scoped>
.results-shell {
  max-width: 1120px;
}

.results-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #e2e8f0;
  background: linear-gradient(124deg, #0f172a 0%, #1d4ed8 55%, #0f766e 100%);
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.22rem 0.56rem;
  font-size: 0.7rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.25);
  color: #dbeafe;
}

.panel-card {
  border-radius: 16px;
}

.mode-switch {
  margin-top: -0.15rem;
}

.chart-box {
  height: 250px;
}

.chart-box.compact {
  height: 250px;
}
</style>