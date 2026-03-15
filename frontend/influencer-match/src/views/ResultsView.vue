<template>
  <div class="container py-4 results-shell">
    <section class="results-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Match Intelligence</p>
        <h3 class="fw-bold mb-1">Matching Results</h3>
        <p class="mb-0 text-light-emphasis">Score quality, trust bands, shortlist strength, and AI rationale in one view.</p>
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
        <div class="col-6 col-xl-3" v-for="stat in headlineStats" :key="stat.label">
          <div class="card border-0 shadow-sm panel-card metric-card h-100">
            <div class="card-body">
              <div class="metric-label">{{ stat.label }}</div>
              <div class="metric-value">{{ stat.value }}</div>
              <div class="metric-note">{{ stat.note }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="row g-3 mb-4" v-if="results.length">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm panel-card h-100">
            <div class="card-body p-3 p-md-4">
              <h6 class="fw-semibold mb-1">Score Distribution</h6>
              <p class="small text-muted mb-3">Buckets rescale to the actual campaign score range instead of assuming a 0 to 1 model.</p>
              <div class="chart-box">
                <AppBarChart :data="scoreDistributionData" :options="barOptions" />
              </div>
            </div>
          </div>
        </div>
        <div class="col-lg-5">
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

      <div v-if="results.length" class="results-stack">
        <article
          v-for="(result, index) in results"
          :key="resultKey(result)"
          class="card border-0 shadow-sm panel-card result-card mb-3"
          :class="{ expanded: expandedResultKey === resultKey(result) }"
        >
          <div class="card-body p-3 p-md-4">
            <div class="d-flex flex-column flex-lg-row gap-3 justify-content-between align-items-start mb-3">
              <div>
                <div class="d-flex flex-wrap gap-2 align-items-center mb-2">
                  <span class="rank-pill">#{{ index + 1 }}</span>
                  <span class="badge" :class="(result.sourceType || 'Influencer') === 'Creator' ? 'text-bg-info' : 'text-bg-primary'">
                    {{ result.sourceType || 'Influencer' }}
                  </span>
                  <span class="trust-pill" :class="trustBandClass(result.trustBand)">
                    {{ result.trustBand || 'Unrated' }} trust
                  </span>
                  <span v-if="hasSemanticSimilarity(result)" class="semantic-pill">
                    {{ formatSimilarity(result.semanticSimilarity) }} semantic fit
                  </span>
                </div>
                <h5 class="mb-1 fw-semibold">{{ result.name }}</h5>
                <p class="mb-0 text-muted">{{ summaryLine(result) }}</p>
              </div>

              <div class="text-lg-end w-100 w-lg-auto score-summary">
                <div class="score-label">Composite Match Score</div>
                <div class="score-value">{{ formatScore(result.score) }}</div>
                <div class="progress score-progress mt-2">
                  <div class="progress-bar" :class="scoreProgressClass(result.score)" :style="{ width: `${scoreProgress(result.score)}%` }"></div>
                </div>
              </div>
            </div>

            <div class="result-metrics mb-3">
              <div class="metric-chip">
                <span>Followers</span>
                <strong>{{ formatCompactNumber(result.followers) }}</strong>
              </div>
              <div class="metric-chip">
                <span>Engagement</span>
                <strong>{{ resultEngagementMeta(result).formatted }}</strong>
              </div>
              <div class="metric-chip">
                <span>Price</span>
                <strong>{{ formatCurrency(result.pricePerPost) }}</strong>
              </div>
              <div class="metric-chip">
                <span>Response</span>
                <strong>{{ formatPercent(result.responseRate) }}</strong>
              </div>
              <div class="metric-chip">
                <span>Completion</span>
                <strong>{{ formatPercent(result.completionRate) }}</strong>
              </div>
            </div>

            <div class="ai-callout mb-3" v-if="result.aiMatchExplanation">
              <div class="ai-label">AI Match Readout</div>
              <p class="mb-0">{{ result.aiMatchExplanation }}</p>
            </div>

            <div class="d-flex flex-wrap gap-2 mb-3" v-if="(result.whyRecommended || []).length">
              <span v-for="reason in result.whyRecommended" :key="reason" class="reason-chip">
                {{ reason }}
              </span>
            </div>

            <div class="d-flex flex-wrap gap-2 align-items-center">
              <router-link class="btn btn-outline-primary btn-sm" :to="profileLink(result)">
                Open Profile
              </router-link>
              <button class="btn btn-sm btn-link px-0 text-decoration-none" @click="toggleExpanded(result)">
                {{ expandedResultKey === resultKey(result) ? 'Hide details' : 'Show more details' }}
              </button>
            </div>

            <div v-if="expandedResultKey === resultKey(result)" class="expanded-panel mt-3 pt-3">
              <div class="row g-3">
                <div class="col-md-6">
                  <div class="detail-card h-100">
                    <div class="detail-label">Operational trust</div>
                    <div class="detail-value">{{ result.trustBand || 'Unrated' }}</div>
                    <div class="detail-list">
                      <div>Response rate: {{ formatPercent(result.responseRate) }}</div>
                      <div>Completion rate: {{ formatPercent(result.completionRate) }}</div>
                      <div>Previous outcomes: {{ result.previousCampaignOutcomes || 'No history provided' }}</div>
                    </div>
                  </div>
                </div>
                <div class="col-md-6">
                  <div class="detail-card h-100">
                    <div class="detail-label">Recommendation signals</div>
                    <div class="detail-value">{{ detailHeadline(result) }}</div>
                    <div class="detail-list">
                      <div v-if="hasSemanticSimilarity(result)">Semantic similarity: {{ formatSimilarity(result.semanticSimilarity) }}</div>
                      <div>Match type: {{ result.sourceType || 'Influencer' }}</div>
                      <div>Budget mode: {{ includeOverBudget ? 'All candidates included' : 'Budget-fit only' }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </article>
      </div>

      <div v-if="!results.length" class="alert alert-secondary mb-0">
        No relevant matches found for this campaign.
        Try broadening budget, using a clearer category, or changing target location.
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
import { engagementMeta } from '../utils/engagement';

const route = useRoute();
const results = ref([]);
const loading = ref(false);
const error = ref('');
const includeOverBudget = ref(false);
const expandedResultKey = ref('');

const compactNumberFormatter = new Intl.NumberFormat('en', {
  notation: 'compact',
  maximumFractionDigits: 1,
});

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
});

const modeDescription = computed(() =>
  includeOverBudget.value
    ? 'Showing all matched creators and influencers, including over-budget options.'
    : 'Showing only matches that fit campaign budget.'
);

const maxScore = computed(() => Math.max(0, ...results.value.map(item => Number(item?.score || 0))));

const scoreBuckets = computed(() => {
  const ceiling = Math.max(maxScore.value, 1);
  const step = ceiling / 5;
  const buckets = Array.from({ length: 5 }, (_, index) => {
    const min = index * step;
    const max = index === 4 ? ceiling + 0.001 : (index + 1) * step;
    return {
      label: `${min.toFixed(1)} - ${Math.min((index + 1) * step, ceiling).toFixed(1)}`,
      min,
      max,
      count: 0,
    };
  });

  for (const item of results.value) {
    const score = Number(item?.score || 0);
    const bucket = buckets.find(entry => score >= entry.min && score < entry.max);
    if (bucket) {
      bucket.count += 1;
    }
  }

  return buckets;
});

const averageScore = computed(() => {
  if (!results.value.length) return 0;
  return results.value.reduce((sum, item) => sum + Number(item?.score || 0), 0) / results.value.length;
});

const averageEngagementRate = computed(() => {
  if (!results.value.length) return 0;
  const total = results.value.reduce((sum, item) => {
    const value = Number(item?.engagementRate || 0);
    return sum + (value > 1 ? value / 100 : value);
  }, 0);

  return total / results.value.length;
});

const aiExplainedCount = computed(() => results.value.filter(item => item?.aiMatchExplanation).length);

const averageSemanticSimilarity = computed(() => {
  const values = results.value
    .map(item => Number(item?.semanticSimilarity))
    .filter(value => Number.isFinite(value) && value > 0);

  if (!values.length) {
    return null;
  }

  const total = values.reduce((sum, value) => sum + value, 0);
  return total / values.length;
});

const headlineStats = computed(() => [
  {
    label: 'Top candidate',
    value: results.value[0]?.name || '—',
    note: results.value[0] ? `${formatScore(results.value[0].score)} score` : 'Waiting for matches',
  },
  {
    label: 'Average score',
    value: results.value.length ? formatScore(averageScore.value) : '—',
    note: `${results.value.length} profiles ranked`,
  },
  {
    label: 'Average engagement',
    value: results.value.length ? formatPercent(averageEngagementRate.value * 100) : '—',
    note: 'Normalized across mixed data sources',
  },
  {
    label: 'AI explained',
    value: `${aiExplainedCount.value}/${results.value.length || 0}`,
    note: averageSemanticSimilarity.value == null
      ? 'No semantic fit signal returned yet'
      : `${formatSimilarity(averageSemanticSimilarity.value)} average semantic fit`,
  },
]);

const scoreDistributionData = computed(() => ({
  labels: scoreBuckets.value.map(item => item.label),
  datasets: [
    {
      label: 'Creators',
      data: scoreBuckets.value.map(item => item.count),
      borderRadius: 8,
      backgroundColor: ['#c7d2fe', '#93c5fd', '#60a5fa', '#2563eb', '#1d4ed8'],
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

function resultKey(item) {
  return `${item?.sourceType || 'Influencer'}-${item?.sourceId || item?.influencerId || item?.name || 'unknown'}`;
}

function resultEngagementMeta(item) {
  return engagementMeta(item?.engagementRate, {
    mode: 'auto',
    fallback: '—',
  });
}

function formatCompactNumber(value) {
  const number = Number(value || 0);
  if (!Number.isFinite(number) || number <= 0) return '—';
  return compactNumberFormatter.format(number);
}

function formatCurrency(value) {
  const number = Number(value || 0);
  if (!Number.isFinite(number) || number <= 0) return '—';
  return currencyFormatter.format(number);
}

function formatScore(value) {
  const number = Number(value || 0);
  return Number.isFinite(number) ? number.toFixed(2) : '0.00';
}

function formatPercent(value) {
  const number = Number(value || 0);
  if (!Number.isFinite(number) || number <= 0) return '—';
  return `${number.toFixed(1)}%`;
}

function hasSemanticSimilarity(item) {
  const value = Number(item?.semanticSimilarity);
  return Number.isFinite(value) && value > 0;
}

function formatSimilarity(value) {
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) return '—';
  const ratio = number > 1 ? number / 100 : number;
  return `${(ratio * 100).toFixed(0)}%`;
}

function trustBandClass(trustBand) {
  const normalized = (trustBand || '').toLowerCase();
  if (normalized.includes('high')) return 'trust-high';
  if (normalized.includes('medium')) return 'trust-medium';
  if (normalized.includes('low')) return 'trust-low';
  return 'trust-neutral';
}

function scoreProgress(score) {
  const ceiling = Math.max(maxScore.value, 1);
  return Math.max(6, Math.min(100, (Number(score || 0) / ceiling) * 100));
}

function scoreProgressClass(score) {
  const ratio = scoreProgress(score);
  if (ratio >= 75) return 'bg-success';
  if (ratio >= 45) return 'bg-primary';
  return 'bg-warning';
}

function summaryLine(result) {
  const engagement = resultEngagementMeta(result).formatted;
  const price = formatCurrency(result.pricePerPost);
  const response = formatPercent(result.responseRate);
  return `Engagement ${engagement} • Price ${price} • Response ${response}`;
}

function detailHeadline(result) {
  if (result?.whyRecommended?.length) {
    return result.whyRecommended[0];
  }

  if (result?.aiMatchExplanation) {
    return 'AI rationale available';
  }

  return 'Ranking based on campaign fit signals';
}

function profileLink(result) {
  return (result?.sourceType || 'Influencer') === 'Creator'
    ? `/creator/${result.sourceId || result.influencerId}/analytics`
    : `/influencer/${result.sourceId || result.influencerId}`;
}

function toggleExpanded(result) {
  const key = resultKey(result);
  expandedResultKey.value = expandedResultKey.value === key ? '' : key;
}

async function loadMatches() {
  loading.value = true;
  error.value = '';
  try {
    const id = route.params.campaignId;
    const res = await api.get(`/match/campaign/${id}`, {
      params: { includeOverBudget: includeOverBudget.value },
    });
    results.value = Array.isArray(res.data) ? res.data : [];
    expandedResultKey.value = results.value.length ? resultKey(results.value[0]) : '';
  } catch (e) {
    results.value = [];
    expandedResultKey.value = '';
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to load matching results.';
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
  border-radius: 24px;
  padding: 1.35rem;
  color: #e2e8f0;
  background:
    radial-gradient(circle at top left, rgba(255, 255, 255, 0.18), transparent 28%),
    linear-gradient(124deg, #0f172a 0%, #1d4ed8 55%, #0f766e 100%);
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
  border-radius: 18px;
}

.mode-switch {
  margin-top: -0.15rem;
}

.metric-card {
  background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
}

.metric-label {
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #64748b;
  margin-bottom: 0.35rem;
}

.metric-value {
  font-size: 1.35rem;
  font-weight: 700;
  color: #0f172a;
}

.metric-note {
  font-size: 0.86rem;
  color: #64748b;
  margin-top: 0.35rem;
}

.chart-box {
  height: 250px;
}

.chart-box.compact {
  height: 250px;
}

.results-stack {
  display: grid;
  gap: 1rem;
}

.result-card {
  transition: transform 160ms ease, box-shadow 160ms ease;
}

.result-card.expanded,
.result-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 1rem 2.4rem rgba(15, 23, 42, 0.08) !important;
}

.rank-pill,
.trust-pill,
.semantic-pill {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.28rem 0.68rem;
  font-size: 0.76rem;
  font-weight: 600;
}

.rank-pill {
  background: #dbeafe;
  color: #1d4ed8;
}

.semantic-pill {
  background: #ecfeff;
  color: #0f766e;
}

.trust-high {
  background: #dcfce7;
  color: #166534;
}

.trust-medium {
  background: #fef3c7;
  color: #92400e;
}

.trust-low {
  background: #fee2e2;
  color: #991b1b;
}

.trust-neutral {
  background: #e2e8f0;
  color: #334155;
}

.score-summary {
  min-width: 190px;
}

.score-label {
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #64748b;
}

.score-value {
  font-size: 1.65rem;
  font-weight: 700;
  color: #0f172a;
}

.score-progress {
  height: 0.55rem;
  border-radius: 999px;
  background: #e2e8f0;
}

.result-metrics {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
}

.metric-chip {
  border-radius: 14px;
  padding: 0.8rem 0.9rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
}

.metric-chip span {
  display: block;
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #64748b;
  margin-bottom: 0.25rem;
}

.metric-chip strong {
  font-size: 1rem;
  color: #0f172a;
}

.ai-callout {
  border-radius: 16px;
  padding: 0.95rem 1rem;
  background: linear-gradient(180deg, #f0fdf4 0%, #ecfeff 100%);
  border: 1px solid #bae6fd;
}

.ai-label {
  font-size: 0.74rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #0f766e;
  margin-bottom: 0.45rem;
  font-weight: 700;
}

.reason-chip {
  border-radius: 999px;
  padding: 0.35rem 0.7rem;
  background: #eff6ff;
  color: #1d4ed8;
  font-size: 0.8rem;
}

.expanded-panel {
  border-top: 1px solid #e2e8f0;
}

.detail-card {
  border-radius: 16px;
  padding: 1rem;
  background: #fcfdff;
  border: 1px solid #e2e8f0;
}

.detail-label {
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #64748b;
  margin-bottom: 0.35rem;
}

.detail-value {
  font-size: 1rem;
  font-weight: 700;
  color: #0f172a;
  margin-bottom: 0.65rem;
}

.detail-list {
  display: grid;
  gap: 0.35rem;
  color: #475569;
  font-size: 0.92rem;
}

@media (max-width: 767px) {
  .results-hero {
    padding: 1rem;
  }

  .score-summary {
    min-width: 100%;
  }
}
</style>
