<template>
  <div class="py-4">
    <div class="container" style="max-width: 1100px;">

      <!-- ── Header ──────────────────────────────────────────────────────── -->
      <div class="d-flex align-items-start justify-content-between mb-4 flex-wrap gap-2">
        <div>
          <h3 class="fw-bold mb-1">YouTube Creator Catalogue</h3>
          <p class="text-muted mb-0">Browse {{ totalCount.toLocaleString() }} imported YouTube creators with full analytics and contact info.</p>
        </div>
      </div>

      <div class="row g-3 mb-4" v-if="opportunityRadar || regionalLanguage">
        <div class="col-12 col-lg-7" v-if="opportunityRadar">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body p-3">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <h6 class="fw-semibold mb-0">Opportunity Radar</h6>
                <small class="text-muted">{{ opportunityRadar.category }} · {{ opportunityRadar.country }}</small>
              </div>
              <div class="small text-muted mb-2">{{ opportunityRadar.alertSummary }}</div>
              <div class="table-responsive">
                <table class="table table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>Creator</th>
                      <th class="text-end">Score</th>
                      <th class="text-end">Subscribers</th>
                      <th>Signal</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="r in (opportunityRadar.risingCreators || []).slice(0, 6)" :key="r.creatorId">
                      <td>{{ r.channelName }}</td>
                      <td class="text-end">{{ Number(r.growthSignalScore || 0).toFixed(1) }}</td>
                      <td class="text-end">{{ fmtNum(r.subscribers) }}</td>
                      <td>{{ r.whyRisingNow }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
        <div class="col-12 col-lg-5" v-if="regionalLanguage">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body p-3">
              <h6 class="fw-semibold mb-2">Regional Language Performance</h6>
              <div class="small text-muted mb-2">Best fit: {{ regionalLanguage.bestFitLanguageRegion || 'N/A' }}</div>
              <div class="d-flex flex-column gap-2">
                <div v-for="row in (regionalLanguage.clusters || []).slice(0, 4)" :key="`${row.language}-${row.region}`" class="d-flex justify-content-between align-items-center rounded p-2 bg-light">
                  <div>
                    <div class="fw-semibold">{{ row.language }}</div>
                    <div class="small text-muted">{{ row.region }}</div>
                  </div>
                  <div class="text-end">
                    <div class="small">Engagement {{ Number(row.avgEngagementPercent || 0).toFixed(2) }}%</div>
                    <div class="small fw-semibold">Views {{ fmtNum(Number(row.avgViews || 0)) }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Filters ─────────────────────────────────────────────────────── -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2">
            <div class="col-12 col-sm-4 col-lg-3">
              <input v-model="filters.search" @input="debouncedFetch" class="form-control" placeholder="Search by channel name…" />
            </div>
            <div class="col-6 col-sm-3 col-lg-2">
              <select v-model="filters.category" @change="fetchCreators" class="form-select">
                <option value="">All Categories</option>
                <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
            <div class="col-6 col-sm-3 col-lg-2">
              <select v-model="filters.tier" @change="fetchCreators" class="form-select">
                <option value="">All Tiers</option>
                <option value="Nano">Nano (&lt;10K)</option>
                <option value="Micro">Micro (10K–100K)</option>
                <option value="MidTier">Mid-Tier (100K–500K)</option>
                <option value="Macro">Macro (500K–5M)</option>
                <option value="Mega">Mega (5M+)</option>
              </select>
            </div>
            <div class="col-6 col-sm-3 col-lg-2">
              <select v-model="filters.sort" @change="fetchCreators" class="form-select">
                <option value="subscribers">Top Subscribers</option>
                <option value="engagement">Top Engagement</option>
                <option value="views">Top Views</option>
                <option value="newest">Recently Added</option>
              </select>
            </div>
            <div class="col-6 col-sm-4 col-lg-2">
              <input v-model.number="filters.minEngagement" @change="fetchCreators"
                type="number" min="0" step="0.1" class="form-control" placeholder="Min engagement %" />
            </div>
            <div class="col-6 col-sm-3 col-lg-1">
              <button class="btn btn-outline-secondary w-100" @click="clearFilters" title="Clear filters">✕</button>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Loading / empty ────────────────────────────────────────────── -->
      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary" role="status"></div>
        <p class="text-muted mt-3">Loading creators…</p>
      </div>

      <div v-else-if="!loading && creators.length === 0" class="text-center py-5 text-muted">
        <div style="font-size:2.5rem">🔍</div>
        <h5 class="mt-3">No creators found</h5>
        <p>Ask your admin to run the YouTube Import job, or adjust your filters.</p>
      </div>

      <!-- ── Creator Grid ────────────────────────────────────────────────── -->
      <div v-else class="row g-3 mb-4">
        <div v-for="c in creators" :key="c.creatorId" class="col-12 col-md-6 col-xl-4">
          <div class="card creator-card h-100 border-0 shadow-sm" @click="openDetail(c)" style="cursor:pointer">

            <!-- Banner -->
            <div class="creator-card-banner"></div>

            <div class="card-body pt-0 pb-3">
              <!-- Avatar + name -->
              <div class="d-flex align-items-center gap-3 mb-3" style="margin-top:-28px">
                <img
                  :src="c.thumbnailUrl || 'https://via.placeholder.com/56?text=YT'"
                  class="rounded-circle border border-3 border-white shadow-sm"
                  style="width:56px;height:56px;object-fit:cover" />
                <div class="overflow-hidden" style="flex:1">
                  <h6 class="fw-bold mb-0 text-truncate">{{ c.channelName }}</h6>
                  <small class="text-muted">{{ c.country || '—' }} · {{ c.category || '—' }}</small>
                </div>
                <span class="badge rounded-pill ms-auto"
                  :class="tierBadge(c.creatorTier)">{{ c.creatorTier }}</span>
              </div>

              <!-- Stats row -->
              <div class="row g-2 text-center mb-3">
                <div class="col-4">
                  <div class="stat-box">
                    <div class="stat-val">{{ fmtNum(c.subscribers) }}</div>
                    <div class="stat-label">Subscribers</div>
                  </div>
                </div>
                <div class="col-4">
                  <div class="stat-box">
                    <div class="stat-val text-success">{{ c.engagementRate?.toFixed(2) }}%</div>
                    <div class="stat-label">Engagement</div>
                  </div>
                </div>
                <div class="col-4">
                  <div class="stat-box">
                    <div class="stat-val">{{ fmtNum(c.avgViews) }}</div>
                    <div class="stat-label">Avg Views</div>
                  </div>
                </div>
              </div>

              <!-- Contact badges -->
              <div class="d-flex flex-wrap gap-1 mb-0">
                <span v-if="c.publicEmail"   class="badge bg-primary-subtle text-primary">✉ Email</span>
                <span v-if="c.instagramHandle" class="badge bg-danger-subtle text-danger">📸 Instagram</span>
                <span v-if="c.twitterHandle"   class="badge bg-info-subtle text-info">🐦 Twitter/X</span>
                <span v-if="c.channelUrl"       class="badge bg-success-subtle text-success">▶ YouTube</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Pagination ──────────────────────────────────────────────────── -->
      <div v-if="totalPages > 1" class="d-flex justify-content-center gap-2">
        <button class="btn btn-sm btn-outline-secondary" :disabled="page <= 1" @click="goPage(page - 1)">&laquo;</button>
        <span class="btn btn-sm btn-light disabled">{{ page }} / {{ totalPages }}</span>
        <button class="btn btn-sm btn-outline-secondary" :disabled="page >= totalPages" @click="goPage(page + 1)">&raquo;</button>
      </div>

    </div>

    <!-- ── Creator Detail Modal ────────────────────────────────────────── -->
    <div v-if="selectedCreator" class="modal-backdrop-custom" @click.self="selectedCreator = null">
      <div class="detail-panel" @click.stop>
        <button class="btn btn-sm btn-outline-secondary float-end" @click="selectedCreator = null">✕ Close</button>
        <h5 class="fw-bold mb-1">{{ selectedCreator.channelName }}</h5>
        <small class="text-muted">Imported YouTube Creator</small>

        <div v-if="detailLoading" class="text-center py-4">
          <div class="spinner-border text-primary" role="status"></div>
        </div>

        <div v-if="detail">

          <!-- Channel header -->
          <div class="d-flex align-items-center gap-3 mt-3 mb-3">
            <img :src="detail.creator.thumbnailUrl || 'https://via.placeholder.com/72?text=YT'"
              class="rounded-circle" style="width:72px;height:72px;object-fit:cover" />
            <div>
              <div class="fw-bold">{{ detail.creator.channelName }}</div>
              <div class="text-muted small">{{ detail.creator.country }} · {{ detail.creator.category }} · {{ detail.creator.creatorTier }}</div>
              <a v-if="detail.creator.channelUrl" :href="detail.creator.channelUrl" target="_blank" class="small">
                Open on YouTube ↗
              </a>
            </div>
          </div>

          <!-- Analytics grid -->
          <div class="row g-2 mb-3">
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val">{{ fmtNum(detail.creator.subscribers) }}</div>
                <div class="stat-label">Subscribers</div>
              </div>
            </div>
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val">{{ fmtNum(detail.creator.totalViews) }}</div>
                <div class="stat-label">Total Views</div>
              </div>
            </div>
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val">{{ detail.creator.videoCount }}</div>
                <div class="stat-label">Videos</div>
              </div>
            </div>
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val text-success">{{ detail.creator.engagementRate?.toFixed(2) }}%</div>
                <div class="stat-label">Engagement</div>
              </div>
            </div>
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val">{{ fmtNum(detail.creator.avgViews) }}</div>
                <div class="stat-label">Avg Views</div>
              </div>
            </div>
            <div class="col-4">
              <div class="stat-box text-center">
                <div class="stat-val">{{ fmtNum(detail.creator.avgLikes) }}</div>
                <div class="stat-label">Avg Likes</div>
              </div>
            </div>
          </div>

          <!-- Contact info -->
          <div class="mb-3">
            <h6 class="fw-semibold mb-2">Contact Information</h6>
            <div v-if="!detail.creator.publicEmail && !detail.creator.instagramHandle && !detail.creator.twitterHandle" class="text-muted small">No contact info found in channel description.</div>
            <div v-if="detail.creator.publicEmail" class="mb-1">
              <span class="badge bg-primary-subtle text-primary me-2">Email</span>
              <a :href="'mailto:' + detail.creator.publicEmail">{{ detail.creator.publicEmail }}</a>
            </div>
            <div v-if="detail.creator.instagramHandle" class="mb-1">
              <span class="badge bg-danger-subtle text-danger me-2">Instagram</span>
              <a :href="'https://instagram.com/' + detail.creator.instagramHandle" target="_blank">@{{ detail.creator.instagramHandle }}</a>
            </div>
            <div v-if="detail.creator.twitterHandle" class="mb-1">
              <span class="badge bg-info-subtle text-info me-2">Twitter/X</span>
              <a :href="'https://x.com/' + detail.creator.twitterHandle" target="_blank">@{{ detail.creator.twitterHandle }}</a>
            </div>
          </div>

          <!-- Description -->
          <div v-if="detail.creator.description" class="mb-3">
            <h6 class="fw-semibold mb-1">About</h6>
            <p class="small text-muted mb-0" style="white-space:pre-line">{{ detail.creator.description.substring(0, 400) }}{{ detail.creator.description.length > 400 ? '…' : '' }}</p>
          </div>

          <!-- Tags -->
          <div v-if="detail.creator.channelTags" class="mb-3">
            <h6 class="fw-semibold mb-1">Channel Tags</h6>
            <div class="d-flex flex-wrap gap-1">
              <span v-for="tag in detail.creator.channelTags.split(',').slice(0, 12)" :key="tag"
                class="badge bg-secondary-subtle text-secondary">{{ tag.trim() }}</span>
            </div>
          </div>

          <!-- Recent videos -->
          <div v-if="detail.videos?.length">
            <h6 class="fw-semibold mb-2">Recent Videos</h6>
            <div class="video-list">
              <div v-for="v in detail.videos" :key="v.videoId" class="video-row d-flex gap-2 align-items-start">
                <img :src="v.thumbnailUrl || 'https://via.placeholder.com/80x45'" class="rounded" style="width:80px;height:45px;object-fit:cover;flex-shrink:0" />
                <div class="overflow-hidden">
                  <div class="small fw-semibold text-truncate">{{ v.title }}</div>
                  <div class="d-flex gap-2 mt-1">
                    <small class="text-muted">👁 {{ fmtNum(v.viewCount) }}</small>
                    <small class="text-muted">👍 {{ fmtNum(v.likeCount) }}</small>
                    <small class="text-success fw-semibold">{{ v.engagementRate?.toFixed(2) }}%</small>
                  </div>
                </div>
                <a :href="'https://youtube.com/watch?v=' + v.videoId" target="_blank"
                  class="btn btn-sm btn-outline-secondary ms-auto flex-shrink-0">▶</a>
              </div>
            </div>
          </div>

          <div class="mt-3" v-if="insights">
            <h6 class="fw-semibold mb-2">Creator Health Scorecard</h6>
            <div class="row g-2 mb-2">
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">Composite</div><div class="stat-val">{{ Number(insights.healthScorecard.compositeScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">Safety</div><div class="stat-val">{{ Number(insights.healthScorecard.brandSafetyScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">7d</div><div class="stat-val text-capitalize">{{ insights.healthScorecard.trend?.trend7d || 'flat' }}</div></div></div>
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">30d</div><div class="stat-val text-capitalize">{{ insights.healthScorecard.trend?.trend30d || 'flat' }}</div></div></div>
            </div>
            <p class="small text-muted mb-2">{{ insights.healthScorecard.whyExplanation }}</p>

            <h6 class="fw-semibold mb-2">Audience Quality</h6>
            <div class="row g-2 mb-2">
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">Suspicious Ratio</div><div class="stat-val">{{ (Number(insights.audienceQuality.suspiciousEngagementRatio || 0) * 100).toFixed(1) }}%</div></div></div>
              <div class="col-6"><div class="stat-box text-center"><div class="stat-label">Volatility</div><div class="stat-val">{{ Number(insights.audienceQuality.engagementVolatilityScore || 0).toFixed(1) }}</div></div></div>
            </div>
            <p class="small text-muted mb-0">{{ insights.audienceQuality.explanation }}</p>
          </div>

          <div class="mt-3" v-if="fit">
            <h6 class="fw-semibold mb-2">Creator-Brand Fit</h6>
            <div class="row g-2 mb-2">
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Overall</div><div class="stat-val">{{ Number(fit.overallFitScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Category</div><div class="stat-val">{{ Number(fit.categoryFitScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Geo/Language</div><div class="stat-val">{{ Number(fit.languageGeoFitScore || 0).toFixed(1) }}</div></div></div>
            </div>
            <p class="small text-muted mb-0">{{ fit.explanation }}</p>
          </div>

          <div class="mt-3" v-if="readiness">
            <h6 class="fw-semibold mb-2">Sponsorship Readiness Index</h6>
            <div class="row g-2 mb-2">
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Readiness</div><div class="stat-val">{{ Number(readiness.sponsorshipReadinessIndex || 0).toFixed(1) }}</div></div></div>
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Content Hygiene</div><div class="stat-val">{{ Number(readiness.contentHygieneScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-4"><div class="stat-box text-center"><div class="stat-label">Reliability</div><div class="stat-val">{{ Number(readiness.reliabilityScore || 0).toFixed(1) }}</div></div></div>
            </div>
            <ul class="small mb-0" v-if="readiness.improvementRoadmap?.length">
              <li v-for="tip in readiness.improvementRoadmap" :key="tip">{{ tip }}</li>
            </ul>
          </div>

          <div class="mt-3 text-muted small">
            Last refreshed: {{ detail.creator.lastRefreshedAt ? new Date(detail.creator.lastRefreshedAt).toLocaleDateString() : 'Unknown' }}
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue';
import api from '../services/api.js';

const creators    = ref([]);
const totalCount  = ref(0);
const totalPages  = ref(1);
const page        = ref(1);
const loading     = ref(false);
const categories  = ref([]);

const selectedCreator = ref(null);
const detail          = ref(null);
const detailLoading   = ref(false);
const insights        = ref(null);
const fit             = ref(null);
const readiness       = ref(null);
const opportunityRadar = ref(null);
const regionalLanguage = ref(null);

const filters = ref({
  search: '',
  category: '',
  tier: '',
  sort: 'subscribers',
  minEngagement: null,
});

// ── Fetch creators list ───────────────────────────────────────────────────────
async function fetchCreators() {
  loading.value = true;
  try {
    const params = {
      page: page.value,
      pageSize: 18,
      ...(filters.value.search        && { search:        filters.value.search }),
      ...(filters.value.category      && { category:      filters.value.category }),
      ...(filters.value.tier          && { tier:          filters.value.tier }),
      ...(filters.value.minEngagement && { minEngagement: filters.value.minEngagement }),
      sort: filters.value.sort,
    };
    const { data } = await api.get('/discovery/youtube-creators', { params });
    creators.value   = data.creators  ?? [];
    totalCount.value = data.totalCount ?? 0;
    totalPages.value = data.totalPages ?? 1;
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
}

// ── Categories ────────────────────────────────────────────────────────────────
async function fetchCategories() {
  try {
    const { data } = await api.get('/discovery/youtube-creators/categories');
    categories.value = data ?? [];
  } catch {}
}

// ── Detail modal ──────────────────────────────────────────────────────────────
async function openDetail(creator) {
  selectedCreator.value = creator;
  detail.value          = null;
  insights.value        = null;
  fit.value             = null;
  readiness.value       = null;
  detailLoading.value   = true;
  try {
    const [detailRes, insightsRes, fitRes, readinessRes] = await Promise.allSettled([
      api.get(`/discovery/youtube-creators/${creator.creatorId}`),
      api.get(`/discovery/youtube-creators/${creator.creatorId}/insights`, {
        params: {
          brandCategory: filters.value.category || null,
          brandCountry: null,
          brandLanguage: null,
        }
      }),
      api.get(`/discovery/youtube-creators/${creator.creatorId}/fit`, {
        params: {
          brandCategory: filters.value.category || null,
          brandCountry: null,
          brandLanguage: null,
        }
      }),
      api.get(`/discovery/youtube-creators/${creator.creatorId}/readiness`)
    ]);

    if (detailRes.status === 'fulfilled') {
      detail.value = detailRes.value.data;
    }
    if (insightsRes.status === 'fulfilled') {
      insights.value = insightsRes.value.data;
    }
    if (fitRes.status === 'fulfilled') {
      fit.value = fitRes.value.data;
    }
    if (readinessRes.status === 'fulfilled') {
      readiness.value = readinessRes.value.data;
    }
  } catch (e) {
    console.error(e);
  } finally {
    detailLoading.value = false;
  }
}

async function fetchDifferentiatorLayers() {
  try {
    const [oppRes, regionalRes] = await Promise.allSettled([
      api.get('/discovery/opportunity-radar', {
        params: {
          category: filters.value.category || null,
          country: null,
          language: null,
          limit: 8,
        }
      }),
      api.get('/discovery/regional-language-performance', {
        params: {
          category: filters.value.category || null,
          country: null,
          brandLanguage: null,
        }
      })
    ]);

    opportunityRadar.value = oppRes.status === 'fulfilled' ? oppRes.value.data : null;
    regionalLanguage.value = regionalRes.status === 'fulfilled' ? regionalRes.value.data : null;
  } catch {
    opportunityRadar.value = null;
    regionalLanguage.value = null;
  }
}

// ── Pagination ────────────────────────────────────────────────────────────────
function goPage(p) {
  page.value = p;
  fetchCreators();
}

// ── Clear filters ─────────────────────────────────────────────────────────────
function clearFilters() {
  filters.value = { search: '', category: '', tier: '', sort: 'subscribers', minEngagement: null };
  page.value    = 1;
  fetchCreators();
}

// ── Debounce for search ───────────────────────────────────────────────────────
let debounceTimer;
function debouncedFetch() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => { page.value = 1; fetchCreators(); }, 350);
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function fmtNum(n) {
  if (!n && n !== 0) return '—';
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 1_000)     return (n / 1_000).toFixed(1) + 'K';
  return n.toString();
}

function tierBadge(tier) {
  return {
    'Mega':    'bg-danger',
    'Macro':   'bg-warning text-dark',
    'MidTier': 'bg-primary',
    'Micro':   'bg-info text-dark',
    'Nano':    'bg-secondary',
  }[tier] ?? 'bg-secondary';
}

onMounted(() => {
  fetchCreators();
  fetchCategories();
  fetchDifferentiatorLayers();
});

watch(
  () => filters.value.category,
  () => {
    fetchDifferentiatorLayers();
  }
);
</script>

<style scoped>
.creator-card { border-radius: 12px; transition: transform .15s, box-shadow .15s; }
.creator-card:hover { transform: translateY(-3px); box-shadow: 0 8px 24px rgba(0,0,0,.12) !important; }

.creator-card-banner {
  height: 52px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 12px 12px 0 0;
}

.stat-box { background: #f8f9fa; border-radius: 8px; padding: 6px 4px; }
.stat-val  { font-size: .95rem; font-weight: 700; line-height: 1.1; }
.stat-label { font-size: .68rem; color: #6c757d; text-transform: uppercase; letter-spacing: .03em; }

.modal-backdrop-custom {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.45);
  z-index: 1050;
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding: 1rem;
}

.detail-panel {
  background: #fff;
  border-radius: 16px;
  width: min(560px, 100%);
  max-height: calc(100vh - 2rem);
  overflow-y: auto;
  padding: 1.5rem;
  box-shadow: 0 16px 48px rgba(0,0,0,.2);
}

.video-list { display: flex; flex-direction: column; gap: .75rem; }
.video-row  { padding: .5rem; background: #f8f9fa; border-radius: 8px; }
</style>
