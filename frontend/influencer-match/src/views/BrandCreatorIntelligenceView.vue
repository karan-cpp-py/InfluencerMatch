<template>
  <div class="brand-intelligence-page container-fluid py-4">
    <section class="intelligence-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">AI Match Workbench</p>
        <h2 class="fw-bold mb-1">Brand Creator Intelligence</h2>
        <p class="mb-0">Shortlist creators with one view of match quality, sponsorship signals, audience risk, and growth projection.</p>
      </div>
    </section>

    <div v-if="campaignContext" class="alert alert-info d-flex justify-content-between align-items-start flex-wrap gap-2 mb-4">
      <div>
        <div class="fw-semibold">Campaign Prefill Active</div>
        <div class="small mb-0">
          Campaign #{{ campaignContext.campaignId }} · {{ campaignContext.category || 'Any category' }} · {{ campaignContext.targetLocation || 'Any location' }}
          · Budget ₹{{ Number(campaignContext.budget || 0).toLocaleString('en-IN') }}
        </div>
        <div v-if="campaignBudgetInr > 0" class="small mb-0 mt-1">
          Budget fit filter: showing creators with est. price up to ₹{{ Number(campaignBudgetInr).toLocaleString('en-IN') }}
        </div>
      </div>
      <button class="btn btn-sm btn-outline-secondary" @click="clearCampaignPrefill">Clear</button>
    </div>

    <div class="card border-0 shadow-sm mb-4 panel-card">
      <div class="card-body p-3">
        <div class="row g-3 align-items-end">
          <div class="col-md-3">
            <label class="form-label fw-semibold small">Industry</label>
            <select v-model="filters.brandCategory" class="form-select form-select-sm">
              <option value="">All Categories</option>
              <option v-for="cat in categories" :key="cat" :value="cat">{{ cat }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label fw-semibold small">Country</label>
            <select v-model="filters.country" class="form-select form-select-sm">
              <option value="">All Countries</option>
              <option value="IN">India</option>
              <option value="US">United States</option>
              <option value="GB">United Kingdom</option>
              <option value="CA">Canada</option>
              <option value="AU">Australia</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label fw-semibold small">Top Matches</label>
            <select v-model="filters.topN" class="form-select form-select-sm">
              <option :value="6">6</option>
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="30">30</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label fw-semibold small">Search Shortlist</label>
            <input v-model.trim="filters.search" class="form-control form-control-sm" placeholder="Creator or category..." />
          </div>
          <div class="col-md-2 d-grid">
            <button class="btn btn-primary btn-sm" @click="loadMatches" :disabled="loadingMatches">
              <span v-if="loadingMatches" class="spinner-border spinner-border-sm me-1"></span>
              Refresh Matches
            </button>
          </div>
        </div>
      </div>
    </div>

    <div class="row g-3 mb-4" v-if="shortlistedMatches.length">
      <div class="col-6 col-lg-3">
        <div class="card border-0 shadow-sm panel-card stat-card">
          <div class="card-body p-3">
            <div class="stat-label">Matches</div>
            <div class="stat-value">{{ shortlistedMatches.length }}</div>
            <div class="stat-subtext">AI-ranked creators</div>
          </div>
        </div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card border-0 shadow-sm panel-card stat-card">
          <div class="card-body p-3">
            <div class="stat-label">Avg Match Score</div>
            <div class="stat-value text-success">{{ avgScoreLabel }}</div>
            <div class="stat-subtext">Across current shortlist</div>
          </div>
        </div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card border-0 shadow-sm panel-card stat-card">
          <div class="card-body p-3">
            <div class="stat-label">Avg Est. Price</div>
            <div class="stat-value text-warning">₹{{ avgInrPriceLabel }}</div>
            <div class="stat-subtext">Brand opportunity baseline</div>
          </div>
        </div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card border-0 shadow-sm panel-card stat-card">
          <div class="card-body p-3">
            <div class="stat-label">Rising Creators</div>
            <div class="stat-value text-primary">{{ risingCount }}</div>
            <div class="stat-subtext">Fast-growth opportunities</div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="error" class="alert alert-warning mb-4">{{ error }}</div>

    <div v-if="pinnedCreators.length" class="card border-0 shadow-sm mb-4 panel-card">
      <div class="card-body p-3">
        <div class="d-flex justify-content-between align-items-center mb-2 flex-wrap gap-2">
          <div>
            <h5 class="fw-semibold mb-0">Compare Mode</h5>
            <div class="small text-muted">Pinned creators ({{ pinnedCreators.length }}/4) with side-by-side intelligence metrics.</div>
          </div>
          <button class="btn btn-sm btn-outline-danger" @click="clearPinnedCreators">Clear Compare</button>
        </div>
        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead>
              <tr>
                <th>Creator</th>
                <th class="text-end">Match</th>
                <th class="text-end">Subscribers</th>
                <th class="text-end">Engagement</th>
                <th>Top Geo</th>
                <th>Primary Audience</th>
                <th class="text-end">12m Projection</th>
                <th class="text-end">Sponsorship</th>
                <th class="text-end">Sponsored Videos</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="creator in pinnedCreators" :key="`compare-${creator.creatorId}`">
                <td>
                  <button class="btn btn-link btn-sm text-decoration-none p-0 fw-semibold" @click="selectCreator(creator)">
                    {{ creator.channelName }}
                  </button>
                  <div class="ai-compact-row mt-1" v-if="creator.aiReadinessLevel || creator.aiRecommendedCampaignGoal || Number.isFinite(Number(creator.aiRiskScore)) || creator.aiFitNarrative">
                    <span v-if="creator.aiReadinessLevel" class="badge" :class="readinessBadgeClass(creator.aiReadinessLevel)">
                      {{ creator.aiReadinessLevel }}
                    </span>
                    <span v-if="creator.aiRecommendedCampaignGoal" class="badge" :class="goalBadgeClass(creator.aiRecommendedCampaignGoal)">
                      {{ creator.aiRecommendedCampaignGoal }}
                    </span>
                    <span v-if="Number.isFinite(Number(creator.aiRiskScore)) && Number(creator.aiRiskScore) > 0" class="badge" :class="riskBadgeClass(creator.aiRiskScore)">
                      Risk {{ creator.aiRiskScore }}
                    </span>
                    <span v-if="compactAiWhy(creator)" class="ai-compact-why">{{ compactAiWhy(creator) }}</span>
                  </div>
                </td>
                <td class="text-end">{{ Math.round((creator.opportunityScore || 0) * 100) }}%</td>
                <td class="text-end">{{ compact(creator.subscribers) }}</td>
                <td class="text-end">{{ creatorEngagementMeta(creator).formatted }}</td>
                <td>{{ creatorAudienceMeta(creator).topGeo }}</td>
                <td>{{ creatorAudienceMeta(creator).primaryAge }} · {{ creatorAudienceMeta(creator).genderSplit }}</td>
                <td class="text-end">{{ compact(creator.predictedSubscribers12Months) }}</td>
                <td class="text-end">₹{{ formatInrRange(creator.estimatedSponsorshipValueInrMin, creator.estimatedSponsorshipValueInrMax) }}</td>
                <td class="text-end">{{ creator.sponsoredVideoCount || 0 }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-xl-5">
        <div class="card border-0 shadow-sm panel-card h-100">
          <div class="card-body p-3">
            <div class="d-flex justify-content-between align-items-center mb-3">
              <div>
                <h5 class="fw-semibold mb-0">AI Shortlist</h5>
                <div class="small text-muted">Top creators ranked by engagement, growth, and reach.</div>
              </div>
            </div>

            <div v-if="loadingMatches" class="text-center py-5">
              <div class="spinner-border text-primary"></div>
            </div>

            <div v-else-if="!shortlistedMatches.length" class="text-center text-muted py-5">
              <div class="fs-2 mb-2">No creators found</div>
              <p class="mb-0">Adjust category or country filters and refresh the AI shortlist.</p>
            </div>

            <div v-else class="d-flex flex-column gap-3">
              <button
                v-for="(creator, index) in shortlistedMatches"
                :key="creator.creatorId"
                type="button"
                class="creator-list-item text-start"
                :class="{ active: selectedMatch?.creatorId === creator.creatorId }"
                @click="selectCreator(creator)"
              >
                <div class="d-flex align-items-start gap-3">
                  <div class="rank-orb">#{{ index + 1 }}</div>
                  <div class="flex-grow-1 min-w-0">
                    <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                      <div>
                        <div class="fw-semibold text-truncate">{{ creator.channelName }}</div>
                        <div class="small text-muted">{{ creator.category }} · {{ creator.country || '—' }}</div>
                      </div>
                      <span class="badge" :class="growthBadgeClass(creator.growthCategory)">{{ creator.growthCategory }}</span>
                    </div>
                    <div class="score-strip mb-2">
                      <div class="score-strip-fill" :style="scoreBarStyle(creator.opportunityScore)"></div>
                    </div>
                    <div class="row g-2 small text-muted">
                      <div class="col-4">
                        <strong class="text-dark d-block">{{ compact(creator.subscribers) }}</strong>
                        Subscribers
                      </div>
                      <div class="col-4">
                        <strong class="text-dark d-block">{{ creatorEngagementMeta(creator).formatted }}</strong>
                        Engagement
                      </div>
                      <div class="col-4">
                        <strong class="text-dark d-block">₹{{ formatCompactInr(creator.estimatedPrice * usdToInr) }}</strong>
                        Est. Price
                      </div>
                    </div>
                    <div v-if="creator.aiFitNarrative" class="small text-muted mt-2 shortlist-narrative">
                      {{ creator.aiFitNarrative }}
                    </div>
                    <div v-if="creator.aiFitSignals?.length" class="d-flex flex-wrap gap-2 mt-2">
                      <span v-for="signal in creator.aiFitSignals.slice(0, 3)" :key="`${creator.creatorId}-${signal}`" class="badge rounded-pill text-bg-light border">
                        {{ signal }}
                      </span>
                    </div>
                    <div class="d-flex flex-wrap gap-2 mt-2" v-if="creator.aiReadinessLevel || creator.aiRecommendedCampaignGoal || creator.aiRiskScore">
                      <span v-if="creator.aiReadinessLevel" class="badge" :class="readinessBadgeClass(creator.aiReadinessLevel)">
                        {{ creator.aiReadinessLevel }} readiness
                      </span>
                      <span v-if="creator.aiRecommendedCampaignGoal" class="badge" :class="goalBadgeClass(creator.aiRecommendedCampaignGoal)">
                        Goal: {{ creator.aiRecommendedCampaignGoal }}
                      </span>
                      <span v-if="Number.isFinite(Number(creator.aiRiskScore)) && Number(creator.aiRiskScore) > 0" class="badge" :class="riskBadgeClass(creator.aiRiskScore)">
                        Risk {{ creator.aiRiskScore }}/100
                      </span>
                    </div>
                    <div class="d-flex justify-content-end mt-2">
                      <button
                        class="btn btn-sm"
                        :class="isPinned(creator) ? 'btn-outline-danger' : 'btn-outline-primary'"
                        @click.stop="togglePinnedCreator(creator)"
                      >
                        {{ isPinned(creator) ? 'Unpin' : 'Pin to Compare' }}
                      </button>
                    </div>
                  </div>
                </div>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="col-xl-7">
        <div class="card border-0 shadow-sm panel-card h-100 intelligence-detail-card">
          <div class="card-body p-3 p-lg-4">
            <div v-if="!selectedMatch" class="text-center text-muted py-5">
              <div class="fs-2 mb-2">Select a creator</div>
              <p class="mb-0">Open a shortlist item to inspect collaboration history, audience quality, pricing, and growth.</p>
            </div>

            <div v-else>
              <div class="d-flex justify-content-between align-items-start gap-3 mb-3 flex-wrap">
                <div>
                  <div class="eyebrow">Selected Creator</div>
                  <h4 class="fw-bold mb-1">{{ selectedMatch.channelName }}</h4>
                    <div class="text-muted small">{{ selectedMatch.category }} · {{ selectedMatch.country || '—' }} · Match score {{ Math.round((selectedMatch.opportunityScore || 0) * 100) }}%</div>
                </div>
                <div class="d-flex gap-2 flex-wrap">
                    <span class="badge text-bg-light border align-self-start">Audience fit {{ audienceFitScoreLabel }}</span>
                  <router-link
                    v-if="selectedMatch.creatorId"
                    :to="`/creator/${selectedMatch.creatorId}/analytics`"
                    class="btn btn-sm btn-outline-primary"
                  >
                    Channel Analytics
                  </router-link>
                  <router-link
                    v-if="selectedMatch.creatorId"
                    :to="`/creator/${selectedMatch.creatorId}/video-analytics`"
                    class="btn btn-sm btn-outline-success"
                  >
                    Video Analytics
                  </router-link>
                </div>
              </div>

              <div v-if="loadingDetail" class="text-center py-4">
                <div class="spinner-border text-primary"></div>
              </div>

              <div v-else-if="detailError" class="alert alert-warning">{{ detailError }}</div>

              <div v-else>
                <div class="row g-3 mb-3">
                  <div class="col-md-3 col-6">
                    <article class="mini-stat-card">
                      <div class="mini-label">Subscribers</div>
                      <div class="mini-value">{{ compact(detailData.subscribers) }}</div>
                    </article>
                  </div>
                  <div class="col-md-3 col-6">
                    <article class="mini-stat-card">
                      <div class="mini-label">Engagement</div>
                      <div class="mini-value text-success">{{ detailEngagementInfo.formatted }}</div>
                    </article>
                  </div>
                  <div class="col-md-3 col-6">
                    <article class="mini-stat-card">
                      <div class="mini-label">Projected 12m</div>
                      <div class="mini-value">{{ compact(detailData.predictedSubscribers12Months) }}</div>
                    </article>
                  </div>
                  <div class="col-md-3 col-6">
                    <article class="mini-stat-card">
                      <div class="mini-label">Sponsorship Range</div>
                      <div class="mini-value text-warning">₹{{ sponsorshipRangeLabel }}</div>
                    </article>
                  </div>
                </div>

                <div class="ai-fit-panel mb-3" v-if="selectedMatch?.aiFitNarrative">
                  <div class="ai-fit-label">AI Fit Brief</div>
                  <div class="fw-semibold mb-2">{{ selectedMatch.aiFitNarrative }}</div>
                  <div class="d-flex flex-wrap gap-2 mb-2" v-if="selectedMatch?.aiReadinessLevel || selectedMatch?.aiRecommendedCampaignGoal || selectedMatch?.aiRiskScore">
                    <span v-if="selectedMatch?.aiReadinessLevel" class="badge" :class="readinessBadgeClass(selectedMatch.aiReadinessLevel)">
                      {{ selectedMatch.aiReadinessLevel }} readiness
                    </span>
                    <span v-if="selectedMatch?.aiRecommendedCampaignGoal" class="badge" :class="goalBadgeClass(selectedMatch.aiRecommendedCampaignGoal)">
                      Recommended goal: {{ selectedMatch.aiRecommendedCampaignGoal }}
                    </span>
                    <span v-if="Number.isFinite(Number(selectedMatch?.aiRiskScore)) && Number(selectedMatch?.aiRiskScore) > 0" class="badge" :class="riskBadgeClass(selectedMatch.aiRiskScore)">
                      Risk score: {{ selectedMatch.aiRiskScore }}/100
                    </span>
                  </div>
                  <div class="d-flex flex-wrap gap-2 mb-2" v-if="selectedMatch.aiFitSignals?.length">
                    <span v-for="signal in selectedMatch.aiFitSignals" :key="signal" class="badge rounded-pill text-bg-light border">
                      {{ signal }}
                    </span>
                  </div>
                  <div class="small text-muted mb-1"><strong class="text-dark">Suggested activation:</strong> {{ selectedMatch.suggestedActivation }}</div>
                  <div class="small text-muted"><strong class="text-dark">Watchout:</strong> {{ selectedMatch.riskNote }}</div>
                </div>

                <div class="row g-3 mb-3">
                  <div class="col-lg-6">
                    <div class="insight-panel h-100">
                      <div class="d-flex justify-content-between align-items-center mb-2">
                        <h6 class="fw-semibold mb-0">Collaboration Intelligence</h6>
                        <span class="badge text-bg-light border">{{ detailData.sponsoredVideoCount || 0 }} sponsored videos</span>
                      </div>
                      <div class="small text-muted mb-2">{{ collaborationSummary }}</div>
                      <div v-if="detailData.brandCollaborations?.length" class="d-flex flex-column gap-2">
                        <div v-for="brand in detailData.brandCollaborations" :key="`${brand.brandName}-${brand.lastDetectedAt || ''}`" class="intel-chip-row">
                          <div>
                            <div class="fw-semibold">{{ brand.brandName }}</div>
                            <div class="small text-muted" v-if="brand.sampleVideoTitle">{{ brand.sampleVideoTitle }}</div>
                          </div>
                          <div class="small text-end text-muted">
                            <div>{{ brand.mentionCount }} mention{{ brand.mentionCount === 1 ? '' : 's' }}</div>
                            <div v-if="brand.lastDetectedAt">{{ formatDate(brand.lastDetectedAt) }}</div>
                          </div>
                        </div>
                      </div>
                      <div v-else class="small text-muted">No detected brand collaborations yet for this creator.</div>
                    </div>
                  </div>

                  <div class="col-lg-6">
                    <div class="insight-panel h-100">
                      <div class="d-flex justify-content-between align-items-center mb-2">
                        <h6 class="fw-semibold mb-0">Audience Fit & Risk</h6>
                        <span class="badge" :class="audienceRisk.badgeClass">{{ audienceRisk.label }}</span>
                      </div>
                      <div class="small text-muted mb-2">Demographics source: {{ audienceEstimate.source }}</div>
                      <div class="row g-2 mb-2 small">
                        <div class="col-6">
                          <div class="text-muted">Top Geo</div>
                          <div class="fw-semibold">{{ audienceEstimate.topGeo }}</div>
                        </div>
                        <div class="col-6">
                          <div class="text-muted">Primary Age</div>
                          <div class="fw-semibold">{{ audienceEstimate.primaryAge }}</div>
                        </div>
                        <div class="col-6">
                          <div class="text-muted">Gender Split</div>
                          <div class="fw-semibold">{{ audienceEstimate.genderSplit }}</div>
                        </div>
                        <div class="col-6">
                          <div class="text-muted">Authenticity</div>
                          <div class="fw-semibold">{{ audienceRisk.score }}/100</div>
                        </div>
                      </div>
                      <div class="small text-muted">{{ audienceRisk.summary }}</div>
                    </div>
                  </div>
                </div>

                <div class="row g-3">
                  <div class="col-lg-6">
                    <div class="insight-panel h-100">
                      <h6 class="fw-semibold mb-2">Content Analysis</h6>
                      <div class="small text-muted mb-2">Derived from recent video titles and creator category.</div>
                      <div class="d-flex flex-wrap gap-2 mb-3">
                        <span v-for="topic in extractedTopics" :key="topic" class="badge rounded-pill text-bg-light border">{{ topic }}</span>
                      </div>
                      <div class="small fw-semibold mb-1">Match rationale</div>
                      <ul class="small mb-0 text-muted ps-3">
                        <li v-for="reason in matchReasons" :key="reason" class="mb-1">{{ reason }}</li>
                      </ul>
                    </div>
                  </div>

                  <div class="col-lg-6">
                    <div class="insight-panel h-100">
                      <h6 class="fw-semibold mb-2">Recent Video Signals</h6>
                      <div v-if="detailData.recentVideos?.length" class="d-flex flex-column gap-2">
                        <div v-for="video in detailData.recentVideos.slice(0, 4)" :key="video.youtubeVideoId" class="video-signal-row">
                          <div class="fw-semibold small text-truncate">{{ video.title }}</div>
                          <div class="small text-muted">{{ compact(video.viewCount) }} views · {{ formatDate(video.publishedAt) }}</div>
                          <div class="mt-1">
                            <router-link
                              :to="`/creator/${selectedMatch.creatorId}/latest-video-analysis?videoId=${video.youtubeVideoId}&videoTitle=${encodeURIComponent(video.title || '')}`"
                              class="btn btn-sm btn-outline-dark"
                            >
                              Analyze with AI
                            </router-link>
                          </div>
                        </div>
                      </div>
                      <div v-else class="small text-muted">No recent videos available for signal extraction.</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import api from '../services/api';
import { engagementMeta } from '../utils/engagement';

const importedCreatorProfileOffset = 1_000_000_000;
const usdToInr = 83;
const route = useRoute();
const router = useRouter();

const categories = [
  'Tech', 'Fitness', 'Gaming', 'Food', 'Travel', 'Fashion',
  'Education', 'Music', 'Comedy', 'Beauty', 'Finance'
];

const filters = ref({
  brandCategory: '',
  country: 'IN',
  topN: 10,
  search: ''
});

const loadingMatches = ref(false);
const loadingDetail = ref(false);
const error = ref('');
const detailError = ref('');
const matches = ref([]);
const selectedMatch = ref(null);
const detail = ref(null);
const pinnedCreators = ref([]);
const campaignContext = ref(null);

const campaignBudgetInr = computed(() => {
  const value = Number(campaignContext.value?.budget || 0);
  return Number.isFinite(value) && value > 0 ? value : 0;
});

const budgetFilteredMatches = computed(() => {
  if (campaignBudgetInr.value <= 0) return matches.value;
  return matches.value.filter((creator) => (Number(creator.estimatedPrice || 0) * usdToInr) <= campaignBudgetInr.value);
});

const shortlistedMatches = computed(() => {
  const query = filters.value.search.trim().toLowerCase();
  if (!query) return budgetFilteredMatches.value;
  return budgetFilteredMatches.value.filter((creator) =>
    String(creator.channelName || '').toLowerCase().includes(query)
    || String(creator.category || '').toLowerCase().includes(query)
    || String(creator.country || '').toLowerCase().includes(query)
  );
});

const avgScoreLabel = computed(() => {
  if (!shortlistedMatches.value.length) return '0%';
  const avg = shortlistedMatches.value.reduce((sum, creator) => sum + Number(creator.opportunityScore || 0), 0) / shortlistedMatches.value.length;
  return `${Math.round(avg * 100)}%`;
});

const avgInrPriceLabel = computed(() => {
  if (!shortlistedMatches.value.length) return '0';
  const avg = shortlistedMatches.value.reduce((sum, creator) => sum + (Number(creator.estimatedPrice || 0) * usdToInr), 0) / shortlistedMatches.value.length;
  return formatCompactInr(avg);
});

const risingCount = computed(() => shortlistedMatches.value.filter((creator) => creator.growthCategory === 'Rising').length);

const detailData = computed(() => ({
  ...(selectedMatch.value || {}),
  ...(detail.value || {})
}));

const detailEngagementInfo = computed(() => engagementMeta(detailData.value?.engagementRate, {
  mode: 'ratio',
  decimals: 2,
  sampleCount: detailData.value?.recentVideos?.length,
  minSampleCount: 3,
  fallback: '—'
}));

const sponsorshipRangeLabel = computed(() => formatInrRange(
  detailData.value?.estimatedSponsorshipValueInrMin,
  detailData.value?.estimatedSponsorshipValueInrMax
));

const audienceEstimate = computed(() => resolveAudienceDemographics(detailData.value));
const audienceRisk = computed(() => estimateAudienceRisk(detailData.value));
const audienceFitScoreLabel = computed(() => {
  const score = Number(selectedMatch.value?.audienceFitScore || 0);
  if (!Number.isFinite(score) || score <= 0) return '—';
  return `${score}/100`;
});

const extractedTopics = computed(() => {
  const titles = (detailData.value?.recentVideos || []).map((video) => video.title || '').join(' ');
  const category = String(detailData.value?.category || 'creator').trim();
  const tokens = titles
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .split(/\s+/)
    .filter((token) => token.length > 3)
    .filter((token) => !['with', 'from', 'this', 'that', 'your', 'about', 'review', 'video', 'creator'].includes(token));

  const counts = new Map();
  for (const token of tokens) {
    counts.set(token, (counts.get(token) || 0) + 1);
  }

  const ranked = [...counts.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 5)
    .map(([token]) => token.charAt(0).toUpperCase() + token.slice(1));

  return [category, ...ranked].filter(Boolean).slice(0, 6);
});

const collaborationSummary = computed(() => {
  const count = Number(detailData.value?.sponsoredVideoCount || 0);
  if (!count) return 'No sponsored-video history is currently detected for this creator.';
  return `Detected ${count} sponsored or brand-linked videos. Use this as a proxy for collaboration frequency and pricing maturity.`;
});

const matchReasons = computed(() => {
  const creator = selectedMatch.value;
  if (!creator) return [];

  const reasons = [];
  if (creator.category && filters.value.brandCategory && creator.category.toLowerCase().includes(filters.value.brandCategory.toLowerCase())) {
    reasons.push(`Category alignment is strong for ${creator.category}.`);
  }
  if (Number(creator.engagementRate || 0) >= 0.04) {
    reasons.push('Engagement is high enough to support conversion-focused campaigns.');
  } else {
    reasons.push('Reach is stronger than engagement, so this creator may fit awareness campaigns better.');
  }
  if (creator.growthCategory === 'Rising') {
    reasons.push(`Growth momentum is positive at ${(Number(creator.growthRate || 0) * 100).toFixed(1)}% monthly.`);
  }
  if (Number(detailData.value?.brandCollaborations?.length || 0) > 0) {
    reasons.push('Past brand-collaboration history reduces sponsorship execution risk.');
  }
  reasons.push(`Estimated sponsorship baseline is ₹${sponsorshipRangeLabel.value}.`);
  return reasons;
});

onMounted(async () => {
  await applyCampaignPrefillFromRoute();
  await loadMatches();
});

async function applyCampaignPrefillFromRoute() {
  const campaignId = Number(route.query.campaignId || 0);
  if (!Number.isFinite(campaignId) || campaignId <= 0) return;

  try {
    const { data } = await api.get(`/campaign/${campaignId}`);
    campaignContext.value = data;
    filters.value.brandCategory = data?.category || '';
    filters.value.country = mapCampaignLocationToCountry(data?.targetLocation);
    filters.value.topN = 20;
  } catch {
    campaignContext.value = null;
  }
}

async function loadMatches() {
  loadingMatches.value = true;
  error.value = '';
  try {
    const { data } = await api.post('/brands/opportunities', {
      brandCategory: filters.value.brandCategory || null,
      country: filters.value.country || null,
      topN: filters.value.topN
    });
    matches.value = Array.isArray(data) ? data : [];
    const nextSelection = shortlistedMatches.value[0] || null;
    selectedMatch.value = nextSelection;
    detail.value = null;
    pinnedCreators.value = pinnedCreators.value
      .map((pinned) => matches.value.find((m) => m.creatorId === pinned.creatorId) || pinned)
      .slice(0, 4);
    if (nextSelection) {
      await loadCreatorDetail(nextSelection);
    }
  } catch (e) {
    matches.value = [];
    selectedMatch.value = null;
    detail.value = null;
    error.value = e?.userMessage || e?.response?.data?.message || 'Unable to load AI creator matches.';
  } finally {
    loadingMatches.value = false;
  }
}

async function selectCreator(creator) {
  selectedMatch.value = creator;
  await loadCreatorDetail(creator);
}

async function loadCreatorDetail(creator) {
  loadingDetail.value = true;
  detailError.value = '';
  try {
    const profileId = importedCreatorProfileOffset + Number(creator.creatorId || 0);
    const { data } = await api.get(`/marketplace/creators/${profileId}`);
    detail.value = data;
  } catch (e) {
    detail.value = null;
    detailError.value = e?.response?.data?.error || 'Unable to load creator intelligence details.';
  } finally {
    loadingDetail.value = false;
  }
}

function isPinned(creator) {
  return pinnedCreators.value.some((item) => item.creatorId === creator.creatorId);
}

async function togglePinnedCreator(creator) {
  if (isPinned(creator)) {
    pinnedCreators.value = pinnedCreators.value.filter((item) => item.creatorId !== creator.creatorId);
    return;
  }
  if (pinnedCreators.value.length >= 4) return;

  let enriched = creator;
  try {
    const profileId = importedCreatorProfileOffset + Number(creator.creatorId || 0);
    const { data } = await api.get(`/marketplace/creators/${profileId}`);
    enriched = { ...creator, ...data };
  } catch {
    enriched = creator;
  }

  pinnedCreators.value = [...pinnedCreators.value, enriched];
}

function clearPinnedCreators() {
  pinnedCreators.value = [];
}

function clearCampaignPrefill() {
  campaignContext.value = null;
  filters.value.brandCategory = '';
  filters.value.country = 'IN';
  filters.value.topN = 10;
  const nextQuery = { ...route.query };
  delete nextQuery.campaignId;
  router.replace({ query: nextQuery });
}

function compact(value) {
  const n = Number(value || 0);
  if (!Number.isFinite(n) || n <= 0) return '—';
  if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return Math.round(n).toString();
}

function creatorEngagementMeta(creator) {
  return engagementMeta(creator?.engagementRate, {
    mode: 'ratio',
    decimals: 2,
    fallback: '—'
  });
}

function creatorAudienceMeta(creator) {
  return resolveAudienceDemographics(creator);
}

function scoreBarStyle(score) {
  const pct = Math.max(0, Math.min(100, Number(score || 0) * 100));
  return { width: `${pct}%` };
}

function growthBadgeClass(category) {
  if (category === 'Rising') return 'text-bg-success';
  if (category === 'Declining') return 'text-bg-danger';
  return 'text-bg-primary';
}

function readinessBadgeClass(level) {
  const normalized = String(level || '').toLowerCase();
  if (normalized === 'high') return 'text-bg-success';
  if (normalized === 'medium') return 'text-bg-info';
  return 'text-bg-secondary';
}

function goalBadgeClass(goal) {
  const normalized = String(goal || '').toLowerCase();
  if (normalized.includes('performance') || normalized.includes('conversion')) {
    return 'text-bg-primary';
  }
  if (normalized.includes('consideration')) {
    return 'text-bg-info';
  }
  if (normalized.includes('awareness')) {
    return 'text-bg-light border';
  }
  return 'text-bg-light border';
}

function riskBadgeClass(scoreValue) {
  const score = Number(scoreValue || 0);
  if (!Number.isFinite(score) || score <= 0) return 'text-bg-light border';
  if (score <= 40) return 'text-bg-danger';
  if (score <= 65) return 'text-bg-warning';
  return 'text-bg-success';
}

function compactAiWhy(creator) {
  if (!creator) return '';
  const firstSignal = Array.isArray(creator.aiFitSignals) && creator.aiFitSignals.length
    ? String(creator.aiFitSignals[0] || '').trim()
    : '';
  if (firstSignal) return firstSignal;

  const narrative = String(creator.aiFitNarrative || '').trim();
  if (!narrative) return '';

  const sentence = narrative.split(/[.!?]/)[0]?.trim() || '';
  if (!sentence) return '';
  return sentence.length > 68 ? `${sentence.slice(0, 65)}...` : sentence;
}

function formatCompactInr(value) {
  const n = Number(value || 0);
  if (!Number.isFinite(n) || n <= 0) return '0';
  if (n >= 10_000_000) return `${(n / 10_000_000).toFixed(1)}Cr`;
  if (n >= 100_000) return `${(n / 100_000).toFixed(1)}L`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return Math.round(n).toLocaleString('en-IN');
}

function formatInrRange(minValue, maxValue) {
  const min = Number(minValue || 0);
  const max = Number(maxValue || 0);
  if (!min && !max) return '—';
  if (!max || min === max) return formatCompactInr(min);
  return `${formatCompactInr(min)} - ${formatCompactInr(max)}`;
}

function formatDate(value) {
  if (!value) return '—';
  return new Date(value).toLocaleDateString();
}

function normalizedEngagementRate(creator) {
  const raw = Number(creator?.engagementRate || 0);
  if (!Number.isFinite(raw) || raw <= 0) return 0.02;
  return raw > 1 ? raw / 100 : raw;
}

function resolveAudienceDemographics(creator) {
  const source = creator?.audienceDemographics;
  if (source && Array.isArray(source.countryBreakdown) && source.countryBreakdown.length) {
    const topGeo = source.countryBreakdown[0]?.key || '—';
    const primaryAge = source.ageBreakdown?.[0]?.key || '—';
    const genderA = source.genderBreakdown?.[0];
    const genderB = source.genderBreakdown?.[1];
    const genderSplit = genderA
      ? `${genderA.key} ${Math.round(Number(genderA.percentage || 0))}%${genderB ? ` / ${genderB.key} ${Math.round(Number(genderB.percentage || 0))}%` : ''}`
      : '—';

    return {
      primaryAge,
      genderSplit,
      topGeo,
      source: source.source || 'YouTube Analytics'
    };
  }

  const category = String(creator?.category || '').toLowerCase();
  const country = creator?.country || 'India';

  let primaryAge = '18-24';
  let genderSplit = '52% Male / 48% Female';

  if (['beauty', 'fashion', 'lifestyle'].some((x) => category.includes(x))) {
    primaryAge = '18-34';
    genderSplit = '35% Male / 65% Female';
  } else if (['gaming', 'tech', 'finance', 'automobile'].some((x) => category.includes(x))) {
    primaryAge = '18-34';
    genderSplit = '68% Male / 32% Female';
  } else if (['education', 'business', 'productivity'].some((x) => category.includes(x))) {
    primaryAge = '25-34';
    genderSplit = '56% Male / 44% Female';
  }

  return {
    primaryAge,
    genderSplit,
    topGeo: `${country}${creator?.region ? `, ${creator.region}` : ''}`,
    source: 'Estimated'
  };
}

function mapCampaignLocationToCountry(location) {
  const value = String(location || '').toLowerCase();
  if (!value) return 'IN';
  if (value.includes('india') || value.includes('in')) return 'IN';
  if (value.includes('united states') || value.includes('usa') || value.includes('us')) return 'US';
  if (value.includes('united kingdom') || value.includes('uk') || value.includes('britain')) return 'GB';
  if (value.includes('canada') || value.includes('ca')) return 'CA';
  if (value.includes('australia') || value.includes('au')) return 'AU';
  return '';
}

function estimateAudienceRisk(creator) {
  const engagement = normalizedEngagementRate(creator);
  const subscribers = Number(creator?.subscribers || 0);
  const tierPenalty = subscribers > 1_000_000 && engagement < 0.015 ? 18 : 0;
  const engagementPenalty = engagement < 0.01 ? 28 : engagement < 0.02 ? 14 : 4;
  const score = Math.max(42, Math.min(96, Math.round(100 - tierPenalty - engagementPenalty)));
  const label = score >= 82 ? 'Low Risk' : score >= 68 ? 'Watch' : 'Review';
  const badgeClass = score >= 82 ? 'text-bg-success' : score >= 68 ? 'text-bg-warning' : 'text-bg-danger';

  return {
    score,
    label,
    badgeClass,
    summary: 'Risk estimate is inferred from engagement consistency, creator size, and expected view efficiency.'
  };
}
</script>

<style scoped>
.brand-intelligence-page {
  background:
    radial-gradient(circle at 12% 0%, rgba(14, 165, 233, 0.08), transparent 35%),
    radial-gradient(circle at 88% 12%, rgba(16, 185, 129, 0.08), transparent 34%);
}

.intelligence-hero {
  border-radius: 20px;
  padding: 1.35rem;
  color: #e0f2fe;
  background: linear-gradient(125deg, #0f172a 0%, #155e75 45%, #0f766e 100%);
  box-shadow: 0 10px 28px rgba(15, 23, 42, 0.2);
}

.hero-kicker,
.eyebrow {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.6rem;
  font-size: 0.68rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(186, 230, 253, 0.18);
}

.panel-card,
.mini-stat-card,
.insight-panel,
.creator-list-item {
  border-radius: 16px;
}

.stat-card {
  background: rgba(255, 255, 255, 0.9);
}

.stat-label,
.mini-label {
  color: #64748b;
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.stat-value,
.mini-value {
  font-size: 1.5rem;
  font-weight: 800;
  line-height: 1.1;
}

.stat-subtext {
  font-size: 0.78rem;
  color: #94a3b8;
}

.creator-list-item {
  width: 100%;
  border: 1px solid rgba(148, 163, 184, 0.2);
  background: #fff;
  padding: 0.95rem;
  transition: transform 0.16s ease, box-shadow 0.16s ease, border-color 0.16s ease;
}

.creator-list-item:hover,
.creator-list-item.active {
  transform: translateY(-1px);
  box-shadow: 0 10px 22px rgba(15, 23, 42, 0.08);
  border-color: rgba(14, 116, 144, 0.45);
}

.rank-orb {
  width: 38px;
  height: 38px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.72rem;
  font-weight: 800;
  color: #0f172a;
  background: linear-gradient(135deg, #a5f3fc, #99f6e4);
  flex-shrink: 0;
}

.score-strip {
  width: 100%;
  height: 8px;
  background: #e2e8f0;
  border-radius: 999px;
  overflow: hidden;
}

.score-strip-fill {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, #0ea5e9, #10b981);
}

.intelligence-detail-card {
  background: rgba(255, 255, 255, 0.96);
}

.mini-stat-card,
.insight-panel {
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: #f8fafc;
  padding: 0.95rem;
  height: 100%;
}

.intel-chip-row,
.video-signal-row {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.7rem 0.8rem;
  border-radius: 12px;
  background: #fff;
  border: 1px solid rgba(148, 163, 184, 0.14);
}

.shortlist-narrative {
  line-height: 1.4;
}

.ai-fit-panel {
  border-radius: 16px;
  padding: 1rem;
  background: linear-gradient(180deg, #ecfeff 0%, #f8fafc 100%);
  border: 1px solid rgba(14, 165, 233, 0.18);
}

.ai-fit-label {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #0f766e;
  font-weight: 700;
  margin-bottom: 0.4rem;
}

.ai-compact-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem;
}

.ai-compact-row .badge {
  font-size: 0.66rem;
  font-weight: 700;
}

.ai-compact-why {
  font-size: 0.7rem;
  color: #64748b;
  line-height: 1.2;
}

@media (max-width: 991.98px) {
  .intelligence-hero {
    padding: 1.1rem;
  }
}
</style>