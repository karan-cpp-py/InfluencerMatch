<template>
  <div class="yt-search-page">
    <div class="container-fluid px-4 py-4" style="max-width: 1440px;">
      <section class="hero-panel mb-4">
        <div class="row g-3 align-items-center">
          <div class="col-lg-8">
            <div class="kicker mb-2">New Feature</div>
            <h2 class="fw-bold mb-2">YouTube Search Intelligence</h2>
            <p class="mb-0 text-muted">
              Search like YouTube, analyze at scale, save shortlists to your team workspace, and compare creators side-by-side before campaign selection.
            </p>
          </div>
          <div class="col-lg-4">
            <div class="hero-metrics d-flex gap-2 flex-wrap justify-content-lg-end">
              <span class="badge text-bg-light border px-3 py-2">{{ searchResult.results.length }} results</span>
              <span class="badge text-bg-light border px-3 py-2">Intent: {{ searchResult.intentLabel || '—' }}</span>
              <span class="badge text-bg-light border px-3 py-2">Selected: {{ selectedChannelIds.length }}</span>
            </div>
          </div>
        </div>
      </section>

      <section class="card border-0 shadow-sm mb-3 search-panel">
        <div class="card-body">

          <!-- ── Search mode selector ───────────────────────────── -->
          <div class="search-mode-tabs mb-4">
            <button
              v-for="m in searchModes"
              :key="m.id"
              :class="['search-mode-tab', searchMode === m.id ? 'active' : '']"
              @click="switchMode(m.id)"
              type="button"
            >
              <span class="mode-icon">{{ m.icon }}</span>
              <div>
                <div class="mode-title">{{ m.label }}</div>
                <div class="mode-hint">{{ m.hint }}</div>
              </div>
            </button>
          </div>

          <!-- ══════════════════════════════════════════
               MODE A: Channel Link
          ══════════════════════════════════════════ -->
          <div v-if="searchMode === 'channel-link'">
            <div class="row g-2 align-items-end">
              <div class="col-lg-8">
                <label class="form-label small text-muted">YouTube Channel URL</label>
                <input
                  v-model.trim="channelUrl"
                  class="form-control"
                  placeholder="e.g. https://youtube.com/@MrBeast  or  youtube.com/channel/UCxxx"
                  @keyup.enter="resolveChannel"
                />
              </div>
              <div class="col-lg-2 d-grid">
                <button
                  class="btn btn-primary"
                  :disabled="loadingResolve || !channelUrl"
                  @click="resolveChannel"
                >
                  <span v-if="loadingResolve" class="spinner-border spinner-border-sm me-1"></span>
                  Find Creator
                </button>
              </div>
            </div>
            <p class="small text-muted mt-2 mb-0">
              Supported: <code>youtube.com/@handle</code> · <code>youtube.com/channel/UCxxx</code> · <code>youtube.com/c/name</code>
            </p>
            <div v-if="resolveError" class="alert alert-warning mt-3 mb-0 py-2">{{ resolveError }}</div>
          </div>

          <!-- ══════════════════════════════════════════
               MODE B: Video Link
          ══════════════════════════════════════════ -->
          <div v-if="searchMode === 'video-link'">
            <div class="row g-2 align-items-end">
              <div class="col-lg-8">
                <label class="form-label small text-muted">YouTube Video URL</label>
                <input
                  v-model.trim="videoUrl"
                  class="form-control"
                  placeholder="e.g. https://youtube.com/watch?v=dQw4w9WgXcQ  or  youtu.be/dQw4w9WgXcQ"
                  @keyup.enter="resolveVideo"
                />
              </div>
              <div class="col-lg-2 d-grid">
                <button
                  class="btn btn-primary"
                  :disabled="loadingResolve || !videoUrl"
                  @click="resolveVideo"
                >
                  <span v-if="loadingResolve" class="spinner-border spinner-border-sm me-1"></span>
                  Find Creator
                </button>
              </div>
            </div>
            <p class="small text-muted mt-2 mb-0">
              Paste any YouTube video link and we'll identify the creator behind it.
            </p>
            <div v-if="resolveError" class="alert alert-warning mt-3 mb-0 py-2">{{ resolveError }}</div>

            <!-- Resolved video info -->
            <div v-if="resolvedVideoTitle" class="alert alert-info mt-3 mb-0 py-2 small">
              🎬 <strong>Video found:</strong> {{ resolvedVideoTitle }}
            </div>
          </div>

          <!-- ══════════════════════════════════════════
               MODE C: General Search (existing)
          ══════════════════════════════════════════ -->
          <div v-if="searchMode === 'general'">
            <div class="row g-2 align-items-end">
              <div class="col-lg-4">
                <label class="form-label small text-muted">Search query</label>
                <div class="position-relative">
                  <input
                    v-model.trim="filters.query"
                    class="form-control"
                    placeholder="e.g. tech review hindi, gaming shorts india, skincare creators"
                    @keyup.enter="runSearch"
                    @input="onQueryInput"
                    @focus="showSuggestions = filters.query.length === 0"
                    @blur="hideSuggestionsDelayed"
                    autocomplete="off"
                  />
                  <!-- Typeahead dropdown (shows when query empty + focused, or with prefix matches) -->
                  <div
                    v-if="showSuggestions && filteredSuggestions.length"
                    class="suggestions-dropdown shadow-sm"
                  >
                    <div class="suggestions-section-label">🔥 Trending searches</div>
                    <button
                      v-for="s in filteredSuggestions"
                      :key="s"
                      class="suggestion-item"
                      type="button"
                      @mousedown.prevent="pickSuggestion(s)"
                    >
                      <span class="suggestion-icon">🔍</span> {{ s }}
                    </button>
                  </div>
                </div>
              </div>
              <div class="col-lg-2 col-md-4">
                <label class="form-label small text-muted">Category</label>
                <input v-model.trim="filters.category" class="form-control" placeholder="Optional" />
              </div>
              <div class="col-lg-2 col-md-4">
                <label class="form-label small text-muted">Country</label>
                <input v-model.trim="filters.country" class="form-control" placeholder="IN" />
              </div>
              <div class="col-lg-2 col-md-4">
                <label class="form-label small text-muted">Language</label>
                <input v-model.trim="filters.language" class="form-control" placeholder="Hindi" />
              </div>
              <div class="col-lg-2 d-grid">
                <button class="btn btn-primary" :disabled="loadingSearch || !filters.query" @click="runSearch">
                  <span v-if="loadingSearch" class="spinner-border spinner-border-sm me-1"></span>
                  Search
                </button>
              </div>
            </div>
            <div v-if="searchResult.aiSearchBrief" class="alert alert-info mt-3 mb-0 py-2">
              <strong>AI brief:</strong> {{ searchResult.aiSearchBrief }}
            </div>
            <div v-if="searchError" class="alert alert-warning mt-3 mb-0 py-2">{{ searchError }}</div>
          </div>

        </div>
      </section>

      <!-- ── Resolved creator (Channel / Video link modes) ──────── -->
      <section v-if="resolvedCreators.length && searchMode !== 'general'" class="mb-4">
        <div class="d-flex align-items-center gap-2 mb-3">
          <h5 class="fw-semibold mb-0">
            {{ resolvedCreators.length === 1 ? 'Matched Creator' : 'Matched Creators' }}
          </h5>
          <span class="badge text-bg-light border">{{ resolvedCreators.length }} found</span>
          <button class="btn btn-sm btn-outline-secondary ms-auto" @click="resolvedCreators = []">Clear</button>
        </div>
        <div class="row g-3">
          <div v-for="creator in resolvedCreators" :key="creator.creatorId || creator.channelId" class="col-lg-6 col-xl-4">
            <article class="card border-0 shadow-sm result-card h-100">
              <div class="card-body d-flex flex-column">
                <div class="d-flex gap-3 align-items-start mb-2">
                  <img v-if="creator.thumbnailUrl" :src="creator.thumbnailUrl" alt="thumbnail" class="thumb" />
                  <div v-else class="thumb thumb-fallback">{{ initial(creator.channelName) }}</div>
                  <div class="min-w-0 flex-grow-1">
                    <h6 class="mb-1 text-truncate">{{ creator.channelName }}</h6>
                    <div class="small text-muted text-truncate">
                      {{ creator.category || 'General' }} · {{ creator.country || '—' }} · {{ creator.language || '—' }}
                    </div>
                  </div>
                  <span class="badge text-bg-success">Exact Match</span>
                </div>
                <div class="row g-2 mb-3 small text-center">
                  <div class="col-4">
                    <div class="metric-label">Subscribers</div>
                    <div class="fw-semibold">{{ compact(creator.subscribers) }}</div>
                  </div>
                  <div class="col-4">
                    <div class="metric-label">Views</div>
                    <div class="fw-semibold">{{ compact(creator.totalViews) }}</div>
                  </div>
                  <div class="col-4">
                    <div class="metric-label">Engagement</div>
                    <div class="fw-semibold" :class="engagementClass(creator.engagementRate)">{{ percent(creator.engagementRate) }}</div>
                  </div>
                </div>
                <div class="d-flex flex-wrap gap-2 mb-3">
                  <button class="btn btn-sm btn-outline-primary" @click="analyzeCreator(creator, 'last10')">Analyze Last 10</button>
                  <button class="btn btn-sm btn-outline-primary" @click="analyzeCreator(creator, 'last20')">Analyze Last 20</button>
                  <button class="btn btn-sm btn-outline-dark" @click="analyzeCreator(creator, 'channel')">Whole Channel</button>
                </div>
                <div class="mt-auto d-flex gap-2">
                  <router-link v-if="creator.creatorId" :to="`/creator/${creator.creatorId}/analytics`" class="btn btn-sm btn-primary flex-grow-1">Analytics</router-link>
                  <a :href="creator.channelUrl" target="_blank" class="btn btn-sm btn-outline-secondary">Open YT</a>
                </div>
                <div v-if="analysisByCreator[creatorAnalysisKey(creator)]" class="analysis-panel border-top p-3 mt-2">
                  <h6 class="mb-1">AI Analysis</h6>
                  <div class="small text-muted">{{ analysisByCreator[creatorAnalysisKey(creator)].aiNarrative }}</div>
                </div>
              </div>
            </article>
          </div>
        </div>
      </section>

      <section v-if="searchResult.results.length" class="card border-0 shadow-sm mb-4 action-panel">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-xl-5">
              <div class="small text-muted mb-2">Bulk AI analysis queue</div>
              <div class="d-flex flex-wrap gap-2">
                <button class="btn btn-sm btn-outline-primary" :disabled="bulkState.running" @click="analyzeTopCreators(5, 'last10')">Analyze Top 5 (Last 10)</button>
                <button class="btn btn-sm btn-outline-primary" :disabled="bulkState.running" @click="analyzeTopCreators(10, 'last10')">Analyze Top 10 (Last 10)</button>
                <button class="btn btn-sm btn-outline-dark" :disabled="bulkState.running" @click="analyzeTopCreators(10, 'last20')">Analyze Top 10 (Last 20)</button>
              </div>
              <div v-if="bulkState.running || bulkState.total > 0" class="mt-2">
                <div class="d-flex justify-content-between small text-muted mb-1">
                  <span>Queue: {{ bulkState.mode }} · {{ bulkState.completed }}/{{ bulkState.total }}</span>
                  <span>{{ bulkProgressPercent }}%</span>
                </div>
                <div class="progress" style="height: 7px;">
                  <div class="progress-bar" :style="{ width: `${bulkProgressPercent}%` }"></div>
                </div>
              </div>
            </div>

            <div class="col-xl-7">
              <div class="small text-muted mb-2">Shortlist and campaign/workspace sync</div>
              <div class="row g-2">
                <div class="col-md-3">
                  <input v-model.trim="shortlistDraft.title" class="form-control form-control-sm" placeholder="Shortlist title" />
                </div>
                <div class="col-md-2">
                  <input v-model.number="shortlistDraft.campaignId" type="number" min="1" class="form-control form-control-sm" placeholder="Campaign ID" />
                </div>
                <div class="col-md-4">
                  <input v-model.trim="shortlistDraft.notes" class="form-control form-control-sm" placeholder="Notes for team workspace" />
                </div>
                <div class="col-md-3 d-flex gap-2">
                    <button class="btn btn-sm btn-success flex-grow-1" :disabled="savingShortlist || !selectedChannelIds.length" @click="saveShortlist">
                    <span v-if="savingShortlist" class="spinner-border spinner-border-sm me-1"></span>
                    Save Shortlist
                  </button>
                    <button class="btn btn-sm btn-outline-secondary" :disabled="selectedChannelIds.length < 2 || selectedChannelIds.length > 4" @click="compareDrawerOpen = true">
                      Compare ({{ selectedChannelIds.length }})
                  </button>
                </div>
              </div>
              <div v-if="shortlistResult.message" class="small mt-2" :class="shortlistResult.saved ? 'text-success' : 'text-muted'">
                {{ shortlistResult.message }}
              </div>
              <div v-if="shortlistResult.saved" class="d-flex flex-wrap gap-2 mt-2">
                <router-link v-if="shortlistResult.workspaceRoute" :to="shortlistResult.workspaceRoute" class="btn btn-sm btn-outline-primary">Open Workspace</router-link>
                <router-link v-if="shortlistResult.campaignRoute" :to="shortlistResult.campaignRoute" class="btn btn-sm btn-outline-primary">Open Campaign Results</router-link>
                <router-link v-if="shortlistResult.creatorIntelligenceRoute" :to="shortlistResult.creatorIntelligenceRoute" class="btn btn-sm btn-outline-primary">Open Creator Intelligence</router-link>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section v-if="loadingSearch" class="row g-3">
        <div v-for="n in 6" :key="n" class="col-lg-6 col-xl-4">
          <div class="card border-0 shadow-sm p-3 placeholder-glow">
            <div class="placeholder col-7 mb-2"></div>
            <div class="placeholder col-9 mb-2"></div>
            <div class="placeholder col-5"></div>
          </div>
        </div>
      </section>

      <section v-else-if="!searchResult.results.length" class="text-center text-muted py-5">
        <div class="fs-3 mb-2">Start with a query</div>
        <p class="mb-0">Try: fintech creators india, gaming hindi reviews, beauty shorts creators.</p>
      </section>

      <section v-else class="row g-3">
        <div v-for="creator in searchResult.results" :key="creator.creatorId" class="col-lg-6 col-xl-4">
          <article class="card border-0 shadow-sm result-card h-100" :class="{ 'result-card-selected': isSelected(creator.channelId) }">
            <div class="card-body d-flex flex-column">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <label class="form-check d-flex align-items-center gap-2 m-0">
                  <input class="form-check-input" type="checkbox" :checked="isSelected(creator.channelId)" @change="toggleSelectedCreator(creator.channelId)" />
                  <span class="small text-muted">Select</span>
                </label>
                <span class="badge" :class="scoreBadgeClass(creator.relevanceScore)">{{ Math.round((creator.relevanceScore || 0) * 100) }}%</span>
              </div>

              <div class="d-flex gap-3 align-items-start mb-2">
                <img v-if="creator.thumbnailUrl" :src="creator.thumbnailUrl" alt="thumbnail" class="thumb" />
                <div v-else class="thumb thumb-fallback">{{ initial(creator.channelName) }}</div>
                <div class="min-w-0 flex-grow-1">
                  <h6 class="mb-1 text-truncate">{{ creator.channelName }}</h6>
                  <div class="small text-muted text-truncate">
                    {{ creator.category || 'General' }} · {{ creator.country || '—' }} · {{ creator.language || '—' }}
                  </div>
                </div>
              </div>

              <div class="small text-muted mb-2">{{ creator.relevanceReason }}</div>

              <div class="row g-2 mb-3 small text-center">
                <div class="col-4">
                  <div class="metric-label">Subscribers</div>
                  <div class="fw-semibold">{{ compact(creator.subscribers) }}</div>
                </div>
                <div class="col-4">
                  <div class="metric-label">Views</div>
                  <div class="fw-semibold">{{ compact(creator.totalViews) }}</div>
                </div>
                <div class="col-4">
                  <div class="metric-label">Engagement</div>
                  <div class="fw-semibold" :class="engagementClass(creator.engagementRate)">{{ percent(creator.engagementRate) }}</div>
                </div>
              </div>

              <div class="d-flex flex-wrap gap-2 mb-3">
                <button class="btn btn-sm btn-outline-primary" :disabled="isAnalyzing(creator, 'last10') || bulkState.running" @click="analyzeCreator(creator, 'last10')">Analyze Last 10</button>
                <button class="btn btn-sm btn-outline-primary" :disabled="isAnalyzing(creator, 'last20') || bulkState.running" @click="analyzeCreator(creator, 'last20')">Analyze Last 20</button>
                <button class="btn btn-sm btn-outline-dark" :disabled="isAnalyzing(creator, 'channel') || bulkState.running" @click="analyzeCreator(creator, 'channel')">Analyze Whole Channel</button>
              </div>

              <div class="mt-auto d-flex gap-2">
                <router-link :to="`/creator/${creator.creatorId}/analytics`" class="btn btn-sm btn-primary flex-grow-1">Analytics</router-link>
                <router-link :to="`/creator/${creator.creatorId}/video-analytics`" class="btn btn-sm btn-outline-primary">Video</router-link>
                <a :href="creator.channelUrl || `https://youtube.com/channel/${creator.channelId}`" target="_blank" class="btn btn-sm btn-outline-secondary">Open</a>
              </div>
            </div>

            <div v-if="analysisByCreator[creatorAnalysisKey(creator)]" class="analysis-panel border-top p-3">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <h6 class="mb-0">AI Analysis · {{ analysisByCreator[creatorAnalysisKey(creator)].mode }}</h6>
                <span class="badge text-bg-light border">{{ analysisByCreator[creatorAnalysisKey(creator)].dataQuality }}</span>
              </div>

              <div class="row g-2 small mb-2">
                <div class="col-6"><div class="metric-label">Campaign Fit</div><div class="fw-semibold">{{ analysisByCreator[creatorAnalysisKey(creator)].campaignFitLabel }}</div></div>
                <div class="col-6"><div class="metric-label">Momentum</div><div class="fw-semibold">{{ analysisByCreator[creatorAnalysisKey(creator)].momentumScore }}/100</div></div>
                <div class="col-4"><div class="metric-label">Avg Views</div><div class="fw-semibold">{{ compact(analysisByCreator[creatorAnalysisKey(creator)].averageViews) }}</div></div>
                <div class="col-4"><div class="metric-label">Avg Likes</div><div class="fw-semibold">{{ compact(analysisByCreator[creatorAnalysisKey(creator)].averageLikes) }}</div></div>
                <div class="col-4"><div class="metric-label">Avg ER</div><div class="fw-semibold">{{ percent(analysisByCreator[creatorAnalysisKey(creator)].averageEngagementRate) }}</div></div>
              </div>

              <div class="small text-muted mb-2" v-if="analysisByCreator[creatorAnalysisKey(creator)].aiNarrative">{{ analysisByCreator[creatorAnalysisKey(creator)].aiNarrative }}</div>

              <div class="d-flex flex-wrap gap-1 mb-2" v-if="analysisByCreator[creatorAnalysisKey(creator)].topKeywords?.length">
                <span v-for="keyword in analysisByCreator[creatorAnalysisKey(creator)].topKeywords.slice(0, 6)" :key="`${creator.channelId}-${keyword}`" class="badge text-bg-light border">{{ keyword }}</span>
              </div>

              <ul class="small mb-0 ps-3 text-muted" v-if="analysisByCreator[creatorAnalysisKey(creator)].actionPlan?.length">
                <li v-for="line in analysisByCreator[creatorAnalysisKey(creator)].actionPlan.slice(0, 3)" :key="line">{{ line }}</li>
              </ul>
            </div>
          </article>
        </div>
      </section>
    </div>

    <div v-if="compareDrawerOpen" class="compare-overlay" @click.self="compareDrawerOpen = false">
      <aside class="compare-drawer">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h5 class="mb-0">Compare Selected Creators</h5>
          <button class="btn btn-sm btn-outline-secondary" @click="compareDrawerOpen = false">Close</button>
        </div>

        <div v-if="compareCreators.length < 2" class="text-muted small">Select at least 2 creators (max 4) to compare.</div>

        <div v-else class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead>
              <tr>
                <th>Metric</th>
                <th v-for="creator in compareCreators" :key="`head-${creator.creatorId}`">{{ creator.channelName }}</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Category</td>
                <td v-for="creator in compareCreators" :key="`cat-${creator.creatorId}`">{{ creator.category || '—' }}</td>
              </tr>
              <tr>
                <td>Subscribers</td>
                <td v-for="creator in compareCreators" :key="`sub-${creator.creatorId}`">{{ compact(creator.subscribers) }}</td>
              </tr>
              <tr>
                <td>Views</td>
                <td v-for="creator in compareCreators" :key="`view-${creator.creatorId}`">{{ compact(creator.totalViews) }}</td>
              </tr>
              <tr>
                <td>Engagement</td>
                <td v-for="creator in compareCreators" :key="`er-${creator.creatorId}`">{{ percent(creator.engagementRate) }}</td>
              </tr>
              <tr>
                <td>Relevance</td>
                <td v-for="creator in compareCreators" :key="`rel-${creator.creatorId}`">{{ Math.round((creator.relevanceScore || 0) * 100) }}%</td>
              </tr>
              <tr>
                <td>AI Campaign Fit</td>
                <td v-for="creator in compareCreators" :key="`fit-${creator.channelId}`">{{ analysisByCreator[creatorAnalysisKey(creator)]?.campaignFitLabel || 'Run analysis' }}</td>
              </tr>
              <tr>
                <td>AI Momentum</td>
                <td v-for="creator in compareCreators" :key="`mom-${creator.channelId}`">{{ analysisByCreator[creatorAnalysisKey(creator)]?.momentumScore ?? '—' }}</td>
              </tr>
              <tr>
                <td>Top Keywords</td>
                <td v-for="creator in compareCreators" :key="`kw-${creator.channelId}`">
                  <span class="small text-muted">{{ (analysisByCreator[creatorAnalysisKey(creator)]?.topKeywords || []).slice(0, 3).join(', ') || 'Run analysis' }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';

const route = useRoute();

// ── Search mode ───────────────────────────────────────────────────
const searchMode = ref('general'); // 'channel-link' | 'video-link' | 'general'

const searchModes = [
  { id: 'channel-link', icon: '🔗', label: 'Channel Link',    hint: 'Paste a YouTube channel URL' },
  { id: 'video-link',   icon: '🎬', label: 'Video Link',      hint: 'Paste a YouTube video URL' },
  { id: 'general',      icon: '🔍', label: 'General Search',  hint: 'Search by topic, category, language' },
];

function switchMode(m) {
  searchMode.value = m;
  resolvedCreators.value = [];
  resolveError.value = '';
  resolvedVideoTitle.value = '';
}

// ── Channel Link resolution ───────────────────────────────────────
const channelUrl      = ref('');
const loadingResolve  = ref(false);
const resolveError    = ref('');
const resolvedCreators = ref([]);

async function resolveChannel() {
  if (!channelUrl.value || loadingResolve.value) return;
  loadingResolve.value = true;
  resolveError.value = '';
  resolvedCreators.value = [];
  try {
    const { data } = await api.get('/youtube-search/resolve-channel', { params: { url: channelUrl.value } });
    if (data.resolved) {
      resolvedCreators.value = data.creators ? data.creators : (data.creator ? [data.creator] : []);
    } else {
      resolveError.value = data.message || 'Channel not found in platform index.';
    }
  } catch (e) {
    resolveError.value = e?.userMessage || 'Could not resolve channel. Check the URL and try again.';
  } finally {
    loadingResolve.value = false;
  }
}

// ── Video Link resolution ─────────────────────────────────────────
const videoUrl           = ref('');
const resolvedVideoTitle = ref('');

async function resolveVideo() {
  if (!videoUrl.value || loadingResolve.value) return;
  loadingResolve.value = true;
  resolveError.value = '';
  resolvedCreators.value = [];
  resolvedVideoTitle.value = '';
  try {
    const { data } = await api.get('/youtube-search/resolve-video', { params: { url: videoUrl.value } });
    if (data.resolved) {
      resolvedVideoTitle.value = data.videoTitle || '';
      resolvedCreators.value = data.creator ? [data.creator] : [];
    } else {
      resolveError.value = data.message || 'Video not found in platform index.';
    }
  } catch (e) {
    resolveError.value = e?.userMessage || 'Could not resolve video. Check the URL and try again.';
  } finally {
    loadingResolve.value = false;
  }
}

// ── General search typeahead suggestions ─────────────────────────
const showSuggestions = ref(false);

const trendingSuggestions = [
  'tech review hindi', 'gaming india shorts', 'skincare creators india',
  'finance tips hindi', 'cooking channel marathi', 'fitness india',
  'comedy sketches hindi', 'education youtube india', 'travel vlog india',
  'cricket commentary hindi', 'bollywood news hindi', 'startup india tech',
  'music covers hindi', 'art and craft hindi', 'automobile review india',
];

const filteredSuggestions = computed(() => {
  const q = (filters.query || '').toLowerCase().trim();
  if (!q) return trendingSuggestions.slice(0, 8);
  return trendingSuggestions.filter(s => s.includes(q)).slice(0, 6);
});

function onQueryInput() { showSuggestions.value = true; }
function hideSuggestionsDelayed() { setTimeout(() => { showSuggestions.value = false; }, 200); }
function pickSuggestion(s) {
  filters.query = s;
  showSuggestions.value = false;
  runSearch();
}

const filters = reactive({
  query: '',
  category: '',
  country: 'IN',
  language: '',
  limit: 18
});

const loadingSearch = ref(false);
const searchError = ref('');
const searchResult = ref({
  query: '',
  intentLabel: '',
  intentReason: '',
  aiSearchBrief: '',
  results: []
});
const analysisByCreator = ref({});
const loadingByKey = ref({});
const selectedChannelIds = ref([]);
const compareDrawerOpen = ref(false);

const bulkState = reactive({
  running: false,
  mode: 'last10',
  total: 0,
  completed: 0,
  failed: 0
});

const shortlistDraft = reactive({
  title: 'YouTube Intelligence Shortlist',
  campaignId: null,
  notes: ''
});

const savingShortlist = ref(false);
const shortlistResult = ref({
  saved: false,
  message: '',
  workspaceRoute: null,
  campaignRoute: null,
  creatorIntelligenceRoute: null
});

const bulkProgressPercent = computed(() => {
  if (!bulkState.total) return 0;
  return Math.round((bulkState.completed / bulkState.total) * 100);
});

const compareCreators = computed(() => {
  if (!selectedChannelIds.value.length) return [];
  return searchResult.value.results.filter((c) => selectedChannelIds.value.includes(c.channelId)).slice(0, 4);
});

onMounted(() => {
  const campaignId = Number(route.query.campaignId || 0);
  if (Number.isFinite(campaignId) && campaignId > 0) {
    shortlistDraft.campaignId = campaignId;
  }
});

async function runSearch() {
  if (!filters.query) return;

  loadingSearch.value = true;
  searchError.value = '';
  shortlistResult.value = { saved: false, message: '', workspaceRoute: null, campaignRoute: null, creatorIntelligenceRoute: null };
  try {
    const { data } = await api.get('/youtube-search/search', {
      params: {
        query: filters.query,
        category: filters.category || undefined,
        country: filters.country || undefined,
        language: filters.language || undefined,
        limit: filters.limit
      }
    });

    searchResult.value = {
      query: data?.query || filters.query,
      intentLabel: data?.intentLabel || '',
      intentReason: data?.intentReason || '',
      aiSearchBrief: data?.aiSearchBrief || '',
      results: Array.isArray(data?.results) ? data.results : []
    };
    analysisByCreator.value = {};
    selectedChannelIds.value = [];
    compareDrawerOpen.value = false;
  } catch (e) {
    searchResult.value = { query: '', intentLabel: '', intentReason: '', aiSearchBrief: '', results: [] };
    searchError.value = e?.userMessage || e?.response?.data?.message || 'Unable to run YouTube intelligence search right now.';
  } finally {
    loadingSearch.value = false;
  }
}

async function analyzeCreator(creator, mode) {
  const key = `${creatorAnalysisKey(creator)}:${mode}`;
  loadingByKey.value = { ...loadingByKey.value, [key]: true };

  try {
    const { data } = await api.post('/youtube-search/analyze', {
      creatorId: creator.creatorId,
      channelId: creator.channelId,
      mode,
      searchContext: filters.query
    });

    analysisByCreator.value = {
      ...analysisByCreator.value,
      [creatorAnalysisKey(creator)]: data
    };
    return true;
  } catch (e) {
    analysisByCreator.value = {
      ...analysisByCreator.value,
      [creatorAnalysisKey(creator)]: {
        mode,
        dataQuality: 'Unavailable',
        campaignFitLabel: 'Unknown',
        momentumScore: 0,
        averageViews: 0,
        averageLikes: 0,
        averageEngagementRate: 0,
        aiNarrative: e?.response?.data?.message || 'Analysis failed for this creator. Please retry.',
        topKeywords: [],
        actionPlan: []
      }
    };
    return false;
  } finally {
    loadingByKey.value = { ...loadingByKey.value, [key]: false };
  }
}

async function analyzeTopCreators(count, mode) {
  if (bulkState.running) return;

  const targets = searchResult.value.results.slice(0, count);
  if (!targets.length) return;

  bulkState.running = true;
  bulkState.mode = mode;
  bulkState.total = targets.length;
  bulkState.completed = 0;
  bulkState.failed = 0;

  for (const creator of targets) {
    const ok = await analyzeCreator(creator, mode);
    bulkState.completed += 1;
    if (!ok) {
      bulkState.failed += 1;
    }
  }

  bulkState.running = false;
}

function toggleSelectedCreator(channelId) {
  const exists = selectedChannelIds.value.includes(channelId);
  if (exists) {
    selectedChannelIds.value = selectedChannelIds.value.filter((id) => id !== channelId);
    return;
  }

  if (selectedChannelIds.value.length >= 4) return;
  selectedChannelIds.value = [...selectedChannelIds.value, channelId];
}

function isSelected(channelId) {
  return selectedChannelIds.value.includes(channelId);
}

async function saveShortlist() {
  if (!selectedChannelIds.value.length || savingShortlist.value) return;

  const selectedCreators = searchResult.value.results.filter((c) => selectedChannelIds.value.includes(c.channelId));
  const creatorIds = selectedCreators.map((c) => Number(c.creatorId || 0)).filter((id) => id > 0);
  const channelIds = selectedCreators.map((c) => c.channelId).filter(Boolean);

  savingShortlist.value = true;
  shortlistResult.value = { saved: false, message: '', workspaceRoute: null, campaignRoute: null, creatorIntelligenceRoute: null };
  try {
    const { data } = await api.post('/youtube-search/shortlist/save', {
      creatorIds,
      channelIds,
      campaignId: Number(shortlistDraft.campaignId || 0) > 0 ? Number(shortlistDraft.campaignId) : null,
      searchQuery: filters.query,
      title: shortlistDraft.title,
      notes: shortlistDraft.notes
    });

    shortlistResult.value = {
      saved: Boolean(data?.saved),
      message: data?.message || 'Shortlist saved.',
      workspaceRoute: data?.workspaceRoute || null,
      campaignRoute: data?.campaignRoute || null,
      creatorIntelligenceRoute: data?.creatorIntelligenceRoute || null
    };
  } catch (e) {
    shortlistResult.value = {
      saved: false,
      message: e?.userMessage || e?.response?.data?.message || 'Unable to save shortlist right now.',
      workspaceRoute: null,
      campaignRoute: null,
      creatorIntelligenceRoute: null
    };
  } finally {
    savingShortlist.value = false;
  }
}

function isAnalyzing(creator, mode) {
  return Boolean(loadingByKey.value[`${creatorAnalysisKey(creator)}:${mode}`]);
}

function creatorAnalysisKey(creator) {
  if (Number(creator?.creatorId || 0) > 0) {
    return `id:${creator.creatorId}`;
  }
  return `ch:${creator?.channelId || 'unknown'}`;
}

function compact(value) {
  const n = Number(value || 0);
  if (!Number.isFinite(n) || n <= 0) return '—';
  if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return Math.round(n).toString();
}

function percent(value) {
  const n = Number(value || 0);
  if (!Number.isFinite(n) || n <= 0) return '—';
  const ratio = n > 1 ? n / 100 : n;
  return `${(ratio * 100).toFixed(2)}%`;
}

function initial(name) {
  return String(name || 'C').trim().charAt(0).toUpperCase() || 'C';
}

function scoreBadgeClass(score) {
  const n = Number(score || 0);
  if (n >= 0.75) return 'text-bg-success';
  if (n >= 0.55) return 'text-bg-primary';
  if (n >= 0.4) return 'text-bg-warning';
  return 'text-bg-secondary';
}

function engagementClass(value) {
  const n = Number(value || 0);
  const ratio = n > 1 ? n / 100 : n;
  if (ratio >= 0.05) return 'text-success';
  if (ratio >= 0.03) return 'text-primary';
  if (ratio >= 0.015) return 'text-warning';
  return 'text-muted';
}
</script>

<style scoped>
.yt-search-page {
  background:
    radial-gradient(circle at 6% -10%, rgba(239, 68, 68, 0.12), transparent 34%),
    radial-gradient(circle at 92% 0%, rgba(14, 165, 233, 0.12), transparent 32%),
    linear-gradient(180deg, #f8fafc 0%, #f3f6fb 100%);
  min-height: calc(100vh - 64px);
}

.hero-panel {
  border-radius: 20px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  background: linear-gradient(132deg, #ffffff 0%, #f8fafc 58%, #eef6ff 100%);
  padding: 1.1rem 1.2rem;
}

.kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.22rem 0.65rem;
  font-size: 0.68rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #991b1b;
  background: rgba(254, 226, 226, 0.9);
}

.search-panel,
.action-panel {
  border-radius: 18px;
}

.result-card {
  border-radius: 16px;
  overflow: hidden;
  transition: transform 0.16s ease, box-shadow 0.16s ease;
}

.result-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 30px rgba(15, 23, 42, 0.08);
}

.result-card-selected {
  border: 1px solid rgba(14, 116, 144, 0.4);
  box-shadow: 0 10px 26px rgba(14, 116, 144, 0.1);
}

.thumb {
  width: 54px;
  height: 54px;
  border-radius: 14px;
  object-fit: cover;
  border: 1px solid rgba(148, 163, 184, 0.26);
}

.thumb-fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  color: #0f172a;
  background: linear-gradient(135deg, #bfdbfe, #bae6fd);
}

.metric-label {
  color: #64748b;
  font-size: 0.68rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.analysis-panel {
  background: linear-gradient(180deg, #fcfcff 0%, #f8fafc 100%);
}

.compare-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.36);
  z-index: 1060;
  display: flex;
  justify-content: flex-end;
}

.compare-drawer {
  width: min(980px, 100vw);
  height: 100%;
  background: #ffffff;
  border-left: 1px solid rgba(148, 163, 184, 0.3);
  padding: 1rem;
  overflow: auto;
}

@media (max-width: 991.98px) {
  .hero-panel {
    padding: 0.95rem;
  }
}

/* ── Search mode tabs ───────────────────────────────────────────── */
.search-mode-tabs {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}
.search-mode-tab {
  flex: 1;
  min-width: 160px;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.7rem 1rem;
  border: 1.5px solid #e2e8f0;
  border-radius: 14px;
  background: #fff;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
  text-align: left;
}
.search-mode-tab:hover { border-color: #0e7490; }
.search-mode-tab.active { border-color: #0e7490; background: #ecfeff; }
.mode-icon { font-size: 1.4rem; line-height: 1; }
.mode-title { font-weight: 600; font-size: 0.88rem; color: #0f172a; }
.mode-hint  { font-size: 0.72rem; color: #64748b; }

/* ── Typeahead suggestions ──────────────────────────────────────── */
.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  z-index: 100;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  overflow: hidden;
  max-height: 280px;
  overflow-y: auto;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.1);
}
.suggestions-section-label {
  padding: 0.5rem 0.85rem;
  font-size: 0.7rem;
  color: #94a3b8;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
.suggestion-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem 0.85rem;
  border: none;
  background: none;
  font-size: 0.85rem;
  cursor: pointer;
  text-align: left;
  transition: background 0.1s;
  color: #0f172a;
}
.suggestion-item:hover { background: #f0f9ff; }
.suggestion-icon { color: #94a3b8; font-size: 0.8rem; }
</style>
