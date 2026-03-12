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

      <div class="card border-0 shadow-sm table-card mb-4" v-if="competitorSov">
        <div class="card-body p-3 p-md-4">
          <div class="d-flex justify-content-between align-items-center mb-2 gap-2 flex-wrap">
            <h5 class="fw-semibold mb-0">Competitor Share of Voice</h5>
            <small class="text-muted">Brand: {{ competitorSov.brandName || 'N/A' }}</small>
          </div>
          <div class="small text-muted mb-3">Category: {{ competitorSov.category || 'General' }}</div>
          <div class="table-responsive">
            <table class="table table-sm mb-0">
              <thead class="table-light">
                <tr>
                  <th>Competitor</th>
                  <th class="text-end">SOV</th>
                  <th class="text-end">Videos</th>
                  <th class="text-end">Creators</th>
                  <th class="text-end">Trend</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in competitorSov.competitors || []" :key="row.competitorBrand">
                  <td>{{ row.competitorBrand }}</td>
                  <td class="text-end">{{ Number(row.shareOfVoicePercent || 0).toFixed(1) }}%</td>
                  <td class="text-end">{{ fmtCompact(row.mentionedVideos) }}</td>
                  <td class="text-end">{{ fmtCompact(row.mentionedByCreators) }}</td>
                  <td class="text-end text-capitalize">{{ row.trend || 'flat' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <ul class="small mt-3 mb-0" v-if="competitorSov.whiteSpaceOpportunities?.length">
            <li v-for="tip in competitorSov.whiteSpaceOpportunities" :key="tip">{{ tip }}</li>
          </ul>
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
                <template v-for="c in filteredCampaigns" :key="c.campaignId">
                  <tr>
                    <td>#{{ c.campaignId }}</td>
                    <td class="fw-semibold">${{ Number(c.budget || 0).toLocaleString() }}</td>
                    <td>{{ c.category || '-' }}</td>
                    <td>{{ c.targetLocation || '-' }}</td>
                    <td class="text-end">
                      <button class="btn btn-sm btn-primary me-2" @click="viewResults(c.campaignId)">Results</button>
                      <button class="btn btn-sm btn-outline-info me-2" @click="toggleAnalytics(c)">
                        {{ expandedCampaignId === c.campaignId ? 'Hide Insights' : 'Insights' }}
                      </button>
                      <button class="btn btn-sm btn-outline-secondary" @click="editCampaign(c.campaignId)">Edit</button>
                    </td>
                  </tr>

                  <tr v-if="expandedCampaignId === c.campaignId">
                    <td colspan="5" class="bg-light">
                      <div v-if="analyticsLoading" class="text-center py-3">
                        <span class="spinner-border spinner-border-sm text-primary me-2"></span>Loading campaign insights...
                      </div>

                      <div v-else-if="analyticsError" class="alert alert-warning py-2 mb-2">{{ analyticsError }}</div>

                      <div v-else-if="campaignAnalytics[c.campaignId]" class="p-2">
                        <div class="row g-2 mb-2">
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">Reach</div><div class="summary-value small-value">{{ fmtCompact(campaignAnalytics[c.campaignId].reach) }}</div></div></div>
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">Engaged Views</div><div class="summary-value small-value">{{ fmtCompact(campaignAnalytics[c.campaignId].engagedViews) }}</div></div></div>
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">Engagement</div><div class="summary-value small-value">{{ pct(campaignAnalytics[c.campaignId].engagementRate) }}</div></div></div>
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">CPM</div><div class="summary-value small-value">${{ money(campaignAnalytics[c.campaignId].cpm) }}</div></div></div>
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">CPE</div><div class="summary-value small-value">${{ money(campaignAnalytics[c.campaignId].cpe) }}</div></div></div>
                          <div class="col-6 col-md-2"><div class="summary-card p-2"><div class="summary-label">CPC Proxy</div><div class="summary-value small-value">${{ money(campaignAnalytics[c.campaignId].cpcLikeProxy) }}</div></div></div>
                        </div>

                        <div v-if="campaignForecasts[c.campaignId]" class="mb-2">
                          <h6 class="fw-semibold mb-1">Pre-Campaign Forecast <span class="badge bg-secondary ms-1">{{ campaignForecasts[c.campaignId].confidenceTier }}</span></h6>
                          <div class="small text-muted mb-2">Confidence: {{ (campaignForecasts[c.campaignId].confidenceScore * 100).toFixed(0) }}%</div>
                          <div class="table-responsive">
                            <table class="table table-sm table-bordered mb-0">
                              <thead class="table-light">
                                <tr>
                                  <th>Scenario</th>
                                  <th class="text-end">Estimated Views</th>
                                  <th class="text-end">Estimated Engagements</th>
                                  <th class="text-end">CPM</th>
                                  <th class="text-end">CPE</th>
                                </tr>
                              </thead>
                              <tbody>
                                <tr v-for="s in campaignForecasts[c.campaignId].budgetScenarios" :key="s.name">
                                  <td class="text-capitalize">{{ s.name }}</td>
                                  <td class="text-end">{{ fmtCompact(s.estimatedViews) }}</td>
                                  <td class="text-end">{{ fmtCompact(s.estimatedEngagements) }}</td>
                                  <td class="text-end">${{ money(s.estimatedCpm) }}</td>
                                  <td class="text-end">${{ money(s.estimatedCpe) }}</td>
                                </tr>
                              </tbody>
                            </table>
                          </div>
                        </div>

                        <div v-if="campaignNegotiation[c.campaignId]" class="mb-2">
                          <h6 class="fw-semibold mb-1">Negotiation Intelligence</h6>
                          <div class="small text-muted mb-2">Risk profile: {{ campaignNegotiation[c.campaignId].riskProfile }}</div>
                          <div class="row g-2 mb-2">
                            <div class="col-6 col-md-3"><div class="summary-card p-2"><div class="summary-label">Fair Min</div><div class="summary-value small-value">${{ money(campaignNegotiation[c.campaignId].fairPriceMin) }}</div></div></div>
                            <div class="col-6 col-md-3"><div class="summary-card p-2"><div class="summary-label">Fair Median</div><div class="summary-value small-value">${{ money(campaignNegotiation[c.campaignId].fairPriceMedian) }}</div></div></div>
                            <div class="col-6 col-md-3"><div class="summary-card p-2"><div class="summary-label">Fair Max</div><div class="summary-value small-value">${{ money(campaignNegotiation[c.campaignId].fairPriceMax) }}</div></div></div>
                            <div class="col-6 col-md-3"><div class="summary-card p-2"><div class="summary-label">Contract</div><div class="summary-value small-value">{{ campaignNegotiation[c.campaignId].suggestedContractStructure }}</div></div></div>
                          </div>
                        </div>

                        <div v-if="campaignCreativeBrief[c.campaignId]" class="mb-2">
                          <h6 class="fw-semibold mb-1">Creative Brief Intelligence</h6>
                          <div class="small text-muted mb-2">Goal: {{ campaignCreativeBrief[c.campaignId].campaignGoal }}</div>
                          <div class="row g-2 mb-2">
                            <div class="col-6 col-md-4"><div class="summary-card p-2"><div class="summary-label">Brief Style</div><div class="summary-value small-value">{{ campaignCreativeBrief[c.campaignId].bestBriefStyle }}</div></div></div>
                            <div class="col-6 col-md-4"><div class="summary-card p-2"><div class="summary-label">Angles</div><div class="summary-value small-value">{{ (campaignCreativeBrief[c.campaignId].suggestedContentAngles || []).length }}</div></div></div>
                            <div class="col-12 col-md-4"><div class="summary-card p-2"><div class="summary-label">Variants</div><div class="summary-value small-value">{{ (campaignCreativeBrief[c.campaignId].testVariants || []).length }}</div></div></div>
                          </div>
                          <ul class="small mb-2" v-if="campaignCreativeBrief[c.campaignId].suggestedContentAngles?.length">
                            <li v-for="point in campaignCreativeBrief[c.campaignId].suggestedContentAngles" :key="point">{{ point }}</li>
                          </ul>
                          <div class="small text-muted" v-if="campaignCreativeBrief[c.campaignId].suggestedCreatorMix?.length">
                            Mix: {{ campaignCreativeBrief[c.campaignId].suggestedCreatorMix.map((x) => `${x.segment} (${x.creatorCount})`).join(', ') }}
                          </div>
                        </div>

                        <h6 class="fw-semibold mb-1">Creator Contribution Ranking</h6>
                        <div class="table-responsive">
                          <table class="table table-sm mb-0">
                            <thead class="table-light">
                              <tr>
                                <th>Creator</th>
                                <th class="text-end">Reach</th>
                                <th class="text-end">Engaged Views</th>
                                <th class="text-end">Contribution</th>
                                <th class="text-end">Indicator</th>
                              </tr>
                            </thead>
                            <tbody>
                              <tr v-if="!campaignAnalytics[c.campaignId].creatorContributions?.length">
                                <td colspan="5" class="text-center text-muted">No contribution data available.</td>
                              </tr>
                              <tr v-for="r in campaignAnalytics[c.campaignId].creatorContributions || []" :key="r.creatorId">
                                <td>{{ r.creatorName }}</td>
                                <td class="text-end">{{ fmtCompact(r.reach) }}</td>
                                <td class="text-end">{{ fmtCompact(r.engagedViews) }}</td>
                                <td class="text-end">{{ Number(r.contributionPercent || 0).toFixed(1) }}%</td>
                                <td class="text-end">
                                  <span class="badge" :class="performanceBadge(r.performanceTag)">{{ r.performanceTag }}</span>
                                </td>
                              </tr>
                            </tbody>
                          </table>
                        </div>
                      </div>
                    </td>
                  </tr>
                </template>
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
const campaignAnalytics = ref({});
const campaignForecasts = ref({});
const campaignNegotiation = ref({});
const campaignCreativeBrief = ref({});
const competitorSov = ref(null);
const expandedCampaignId = ref(null);
const analyticsLoading = ref(false);
const analyticsError = ref('');

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
    await fetchCompetitorSov();
  } finally {
    loading.value = false;
  }
});

async function fetchCompetitorSov() {
  try {
    const categories = campaigns.value
      .map((x) => String(x.category || '').trim())
      .filter(Boolean);
    const unique = [...new Set(categories)];
    const params = {
      brandName: 'BrandPortfolio',
      primaryCompetitor: unique[0] || undefined,
      secondaryCompetitor: unique[1] || undefined,
    };
    const res = await api.get('/campaign/competitor-share-of-voice', { params });
    competitorSov.value = res.data;
  } catch {
    competitorSov.value = null;
  }
}

function viewResults(id) {
  router.push(`/results/${id}`);
}

function editCampaign(id) {
  router.push({ path: '/brand', query: { campaignId: id } });
}

async function toggleAnalytics(campaign) {
  if (expandedCampaignId.value === campaign.campaignId) {
    expandedCampaignId.value = null;
    return;
  }

  expandedCampaignId.value = campaign.campaignId;
  analyticsError.value = '';
  analyticsLoading.value = true;
  try {
    const [outcomeRes, forecastRes, negotiationRes, creativeRes] = await Promise.all([
      api.get(`/campaign/${campaign.campaignId}/outcomes`),
      api.post(`/campaign/${campaign.campaignId}/forecast`, { budgetOverride: campaign.budget }),
      api.get(`/campaign/${campaign.campaignId}/negotiation-intelligence`, { params: { proposedPrice: campaign.budget } }),
      api.post(`/campaign/${campaign.campaignId}/creative-brief-intelligence`, { campaignGoal: `Drive ${campaign.category || 'brand'} awareness in ${campaign.targetLocation || 'target market'}` })
    ]);

    campaignAnalytics.value = {
      ...campaignAnalytics.value,
      [campaign.campaignId]: outcomeRes.data,
    };

    campaignForecasts.value = {
      ...campaignForecasts.value,
      [campaign.campaignId]: forecastRes.data,
    };

    campaignNegotiation.value = {
      ...campaignNegotiation.value,
      [campaign.campaignId]: negotiationRes.data,
    };

    campaignCreativeBrief.value = {
      ...campaignCreativeBrief.value,
      [campaign.campaignId]: creativeRes.data,
    };
  } catch (e) {
    analyticsError.value = e?.userMessage || e?.response?.data?.error || 'Unable to load campaign insights.';
  } finally {
    analyticsLoading.value = false;
  }
}

function fmtCompact(n) {
  const v = Number(n || 0);
  if (v >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M';
  if (v >= 1_000) return (v / 1_000).toFixed(1) + 'K';
  return v.toFixed(0);
}

function money(n) {
  return Number(n || 0).toFixed(2);
}

function pct(ratio) {
  return `${(Number(ratio || 0) * 100).toFixed(2)}%`;
}

function performanceBadge(tag) {
  if (tag === 'Overperformer') return 'bg-success';
  if (tag === 'Underperformer') return 'bg-danger';
  return 'bg-secondary';
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