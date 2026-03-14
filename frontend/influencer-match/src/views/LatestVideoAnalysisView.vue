<template>
  <div class="container py-4" style="max-width: 1200px;">
    <router-link :to="isSelfMode ? '/creator-dashboard' : `/creator/${creatorId}/analytics`" class="text-muted text-decoration-none small d-inline-block mb-3">
      ← Back to Creator Analytics
    </router-link>

    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body d-flex flex-wrap justify-content-between align-items-start gap-3">
        <div>
          <h3 class="fw-bold mb-1">Latest Video AI Analysis</h3>
          <p class="text-muted mb-0">Creator + Brand readout from latest video JSON payload.</p>
        </div>
        <div class="d-flex gap-2">
          <button class="btn btn-outline-secondary btn-sm" @click="loadTemplate" :disabled="loadingTemplate || autoRunning">Use Template</button>
          <button class="btn btn-success btn-sm" @click="analyzeLatestAuto" :disabled="autoRunning || running || loadingTemplate">
            <span v-if="autoRunning" class="spinner-border spinner-border-sm me-1"></span>
            Analyze Latest Video
          </button>
          <button class="btn btn-primary btn-sm" @click="runAnalysis" :disabled="running">
            <span v-if="running" class="spinner-border spinner-border-sm me-1"></span>
            Run Analysis
          </button>
        </div>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-lg-5">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-body">
            <h6 class="fw-semibold mb-2">Input JSON</h6>
            <p class="text-muted small mb-2">Paste YouTube API payload in request format.</p>
            <textarea v-model="inputJson" class="form-control font-monospace" rows="26"></textarea>
            <div v-if="inputError" class="text-danger small mt-2">{{ inputError }}</div>
          </div>
        </div>
      </div>

      <div class="col-lg-7">
        <div v-if="apiError" :class="['alert', apiNoticeKind === 'info' ? 'alert-info' : 'alert-warning']">{{ apiError }}</div>

        <div v-if="result" class="d-flex flex-column gap-3">
          <div v-if="result.ai_final_verdict" class="card border-0 shadow-sm border-start border-4" :class="verdictBorderClass(result.ai_final_verdict.go_no_go)">
            <div class="card-body">
              <div class="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">
                <h6 class="fw-semibold mb-0">AI Final Verdict</h6>
                <div class="d-flex flex-wrap gap-2">
                  <span class="badge" :class="goNoGoBadgeClass(result.ai_final_verdict.go_no_go)">
                    {{ (result.ai_final_verdict.go_no_go || 'unknown').toUpperCase() }}
                  </span>
                  <span class="badge bg-dark">Readiness {{ clampScore(result.ai_final_verdict.brand_readiness_score) }}/100</span>
                  <span class="badge" :class="confidenceBadgeClass(result.ai_final_verdict.confidence)">
                    Confidence {{ capitalize(result.ai_final_verdict.confidence || 'medium') }}
                  </span>
                  <span class="badge bg-secondary">{{ result.ai_final_verdict.source || 'ai' }}</span>
                </div>
              </div>

              <div class="progress mb-2" style="height: 10px;">
                <div
                  class="progress-bar"
                  :class="readinessBarClass(result.ai_final_verdict.brand_readiness_score)"
                  :style="{ width: `${clampScore(result.ai_final_verdict.brand_readiness_score)}%` }"
                ></div>
              </div>

              <p class="mb-2 small">{{ result.ai_final_verdict.final_verdict || 'No AI verdict generated.' }}</p>
              <div class="small mb-2"><strong>Recommended Format:</strong> {{ result.ai_final_verdict.recommended_format || '—' }}</div>

              <div class="small fw-semibold mb-1">Top Reasons</div>
              <ul class="small ps-3 mb-0">
                <li v-for="(reason, i) in (result.ai_final_verdict.top_reasons || [])" :key="`vr-${i}`" class="mb-1">{{ reason }}</li>
              </ul>
            </div>
          </div>

          <div v-if="result.ai_model_diagnostics" class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">AI Model Diagnostics</h6>
              <div class="row g-2 small mb-2">
                <div class="col-md-4"><strong>HF Token:</strong> {{ result.ai_model_diagnostics?.huggingface?.token_configured ? 'Configured' : 'Missing' }}</div>
                <div class="col-md-4"><strong>Sentiment Status:</strong> {{ result.ai_model_diagnostics?.runtime?.sentiment_status || '—' }}</div>
                <div class="col-md-4"><strong>Emotion Status:</strong> {{ result.ai_model_diagnostics?.runtime?.emotion_status || '—' }}</div>
                <div class="col-md-4"><strong>Sentiment Evaluated:</strong> {{ fmtNum(result.ai_model_diagnostics?.runtime?.sentiment_evaluated_comments) }}</div>
                <div class="col-md-4"><strong>Emotion Evaluated:</strong> {{ fmtNum(result.ai_model_diagnostics?.runtime?.emotion_evaluated_comments) }}</div>
                <div class="col-md-4"><strong>NER Entities:</strong> {{ fmtNum(result.ai_model_diagnostics?.runtime?.ner_entities_detected) }}</div>
              </div>
              <pre class="small bg-light p-2 rounded mb-0">{{ toPretty(result.ai_model_diagnostics) }}</pre>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Video Summary</h6>
              <div v-if="autoMeta" class="small text-muted mb-2">
                <strong>Auto source:</strong> {{ autoMeta.title || 'Latest row' }}
              </div>
              <!-- AI-generated summary (Groq) takes priority when available -->
              <p v-if="result.video_summary?.ai_summary" class="mb-1 fw-semibold text-primary">
                {{ result.video_summary.ai_summary }}
              </p>
              <p class="mb-2 text-muted small">{{ result.video_summary?.summary || 'No summary available.' }}</p>
              <div class="small text-muted">
                <div><strong>Publish:</strong> {{ fmtDate(result.video_summary?.metadata?.publish_date) }}</div>
                <div><strong>Duration:</strong> {{ result.video_summary?.metadata?.duration || '—' }}</div>
                <div><strong>Language:</strong> {{ result.video_summary?.metadata?.language || '—' }}</div>
                <div><strong>Category:</strong> {{ result.video_summary?.metadata?.category_id || '—' }}</div>
                <div><strong>Tag Count:</strong> {{ result.video_summary?.metadata?.tags?.length ?? 0 }}</div>
              </div>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Collaboration Detection</h6>
              <div class="d-flex flex-wrap gap-2 mb-2">
                <span class="badge bg-dark">{{ result.collaboration_detection?.collaboration_status || 'Unclear' }}</span>
                <span class="badge bg-primary">Confidence {{ result.collaboration_detection?.confidence_score ?? 0 }}/100</span>
              </div>
              <div class="small mb-2">{{ result.collaboration_detection?.confidence_reason || '—' }}</div>
              <div class="small"><strong>Brands (regex):</strong> {{ joinList(result.collaboration_detection?.brands_detected) }}</div>
              <div class="mt-2">
                <div class="small fw-semibold mb-1">Evidence</div>
                <ul class="small mb-0 ps-3">
                  <li v-for="(e, i) in (result.collaboration_detection?.evidence || [])" :key="i">{{ e }}</li>
                </ul>
              </div>
            </div>
          </div>

          <!-- NER Analysis (ML-detected entities) -->
          <div v-if="result.ner_analysis" class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">
                🤖 NER Entity Analysis
                <span class="badge bg-secondary ms-1 fw-normal" style="font-size:0.65rem">{{ result.ner_analysis.model }}</span>
              </h6>
              <div class="row g-2 small">
                <div class="col-md-6">
                  <div class="fw-semibold mb-1 text-warning">Brands &amp; Orgs Detected</div>
                  <span v-for="(b, i) in (result.ner_analysis.brands_and_orgs || [])" :key="i"
                    class="badge bg-warning text-dark me-1 mb-1">{{ b }}</span>
                  <span v-if="!result.ner_analysis.brands_and_orgs?.length" class="text-muted">None found</span>
                </div>
                <div class="col-md-6">
                  <div class="fw-semibold mb-1 text-info">People Mentioned</div>
                  <span v-for="(p, i) in (result.ner_analysis.people_mentioned || [])" :key="i"
                    class="badge bg-info text-dark me-1 mb-1">{{ p }}</span>
                  <span v-if="!result.ner_analysis.people_mentioned?.length" class="text-muted">None found</span>
                </div>
              </div>
              <div class="small text-muted mt-2">Total entities: {{ result.ner_analysis.entity_count ?? 0 }}</div>
            </div>
          </div>

          <!-- Emotion ML Analysis -->
          <div v-if="result.emotion_ml_analysis" class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">
                🧠 Emotion Analysis (ML)
                <span class="badge bg-secondary ms-1 fw-normal" style="font-size:0.65rem">{{ result.emotion_ml_analysis.model }}</span>
              </h6>
              <div v-if="result.emotion_ml_analysis.succeeded">
                <div class="mb-2">
                  <span class="fw-semibold">Dominant: </span>
                  <span :class="emotionBadgeClass(result.emotion_ml_analysis.dominant_emotion)" class="badge">
                    {{ result.emotion_ml_analysis.dominant_emotion }}
                  </span>
                  <span class="text-muted small ms-2">from {{ result.emotion_ml_analysis.evaluated_count }} comments</span>
                </div>
                <div class="d-flex flex-wrap gap-1">
                  <template v-for="(score, emotion) in (result.emotion_ml_analysis.scores || {})" :key="emotion">
                    <div class="d-flex align-items-center gap-1 small">
                      <span class="text-muted">{{ emotion }}:</span>
                      <div class="progress" style="width:60px;height:8px;">
                        <div class="progress-bar bg-primary" :style="{ width: `${score * 100}%` }"></div>
                      </div>
                      <span>{{ (score * 100).toFixed(0) }}%</span>
                    </div>
                  </template>
                </div>
              </div>
              <div v-else class="small text-muted">{{ result.emotion_ml_analysis.note || 'Model unavailable' }}</div>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Growth Analysis</h6>
              <div class="small">
                <div><strong>Views:</strong> {{ fmtNum(result.growth_analysis?.performance_snapshot?.view_count) }}</div>
                <div><strong>Likes:</strong> {{ fmtNum(result.growth_analysis?.performance_snapshot?.like_count) }}</div>
                <div><strong>Comments:</strong> {{ fmtNum(result.growth_analysis?.performance_snapshot?.comment_count) }}</div>
                <div><strong>Likes/View:</strong> {{ percent(result.growth_analysis?.engagement_ratios?.likes_per_view) }}</div>
                <div><strong>Comments/View:</strong> {{ percent(result.growth_analysis?.engagement_ratios?.comments_per_view) }}</div>
                <div><strong>Benchmark note:</strong> {{ result.growth_analysis?.benchmarks || '—' }}</div>
              </div>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Comment Intelligence</h6>
              <div class="small mb-2"><strong>Sentiment:</strong> {{ result.comment_intelligence?.overall_sentiment || '—' }}</div>
              <div class="small mb-2"><strong>Sample Size:</strong> {{ fmtNum(result.comment_intelligence?.sample_coverage?.sample_size) }}</div>
              <div class="small mb-2"><strong>Evaluated by Model:</strong> {{ fmtNum(result.comment_intelligence?.sample_coverage?.sentiment_model?.evaluated_comments) }}</div>
              <div class="small mb-2"><strong>Fetch Mode:</strong> {{ result.comment_intelligence?.sample_coverage?.fetch?.mode || '—' }}</div>
              <div class="small mb-2"><strong>Fetched Count:</strong> {{ fmtNum(result.comment_intelligence?.sample_coverage?.fetch?.fetched_count) }}</div>
              <div class="small mb-2"><strong>Videos Considered:</strong> {{ fmtNum(result.comment_intelligence?.sample_coverage?.fetch?.videos_considered) }}</div>
              <div class="small mb-2"><strong>Fetch Note:</strong> {{ result.comment_intelligence?.sample_coverage?.fetch?.note || '—' }}</div>
              <div class="small mb-2"><strong>Sentiment Source:</strong> {{ result.comment_intelligence?.sample_coverage?.sentiment_model?.source || '—' }}</div>
              <div class="small mb-2"><strong>Sentiment Model Status:</strong> {{ result.comment_intelligence?.sample_coverage?.sentiment_model?.status || '—' }}</div>
              <div class="small mb-2"><strong>Sentiment Note:</strong> {{ result.comment_intelligence?.sample_coverage?.sentiment_model?.note || '—' }}</div>

              <div class="small fw-semibold mb-1">Sentiment Breakdown</div>
              <div class="d-flex flex-column gap-1 mb-2">
                <div class="d-flex align-items-center gap-2">
                  <span class="small text-muted" style="width:70px">Positive</span>
                  <div class="progress flex-grow-1" style="height:8px;"><div class="progress-bar bg-success" :style="{ width: `${result.comment_intelligence?.sentiment_breakdown?.positive_pct ?? 0}%` }"></div></div>
                  <span class="small" style="width:40px">{{ result.comment_intelligence?.sentiment_breakdown?.positive_pct ?? 0 }}%</span>
                </div>
                <div class="d-flex align-items-center gap-2">
                  <span class="small text-muted" style="width:70px">Mixed</span>
                  <div class="progress flex-grow-1" style="height:8px;"><div class="progress-bar bg-warning" :style="{ width: `${result.comment_intelligence?.sentiment_breakdown?.mixed_pct ?? 0}%` }"></div></div>
                  <span class="small" style="width:40px">{{ result.comment_intelligence?.sentiment_breakdown?.mixed_pct ?? 0 }}%</span>
                </div>
                <div class="d-flex align-items-center gap-2">
                  <span class="small text-muted" style="width:70px">Negative</span>
                  <div class="progress flex-grow-1" style="height:8px;"><div class="progress-bar bg-danger" :style="{ width: `${result.comment_intelligence?.sentiment_breakdown?.negative_pct ?? 0}%` }"></div></div>
                  <span class="small" style="width:40px">{{ result.comment_intelligence?.sentiment_breakdown?.negative_pct ?? 0 }}%</span>
                </div>
              </div>

              <div class="small mb-2"><strong>Top Themes:</strong> {{ joinList(result.comment_intelligence?.top_5_themes) }}</div>
              <div class="small mb-1 fw-semibold">Audience Questions</div>
              <ul class="small mb-2 ps-3">
                <li v-for="(q, i) in (result.comment_intelligence?.audience_questions || [])" :key="i">{{ q }}</li>
              </ul>
              <div class="small mb-1 fw-semibold">Brand Safety</div>
              <pre class="small bg-light p-2 rounded mb-0">{{ toPretty(result.comment_intelligence?.brand_safety) }}</pre>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Brand & Agency Readout</h6>
              <pre class="small bg-light p-2 rounded mb-0">{{ toPretty(result.brand_agency_readout) }}</pre>
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <h6 class="fw-semibold mb-2">Recommendations</h6>
              <div class="row g-3">
                <div class="col-md-6">
                  <div class="small fw-semibold mb-2 text-primary">For Creator</div>
                  <ul class="small ps-3 mb-0">
                    <li v-for="(r, i) in (result.recommendations?.for_creator || [])" :key="`c-${i}`" class="mb-1">{{ r }}</li>
                  </ul>
                </div>
                <div class="col-md-6">
                  <div class="small fw-semibold mb-2 text-success">For Brands / Agencies</div>
                  <ul class="small ps-3 mb-0">
                    <li v-for="(r, i) in (result.recommendations?.for_brands_agencies || [])" :key="`b-${i}`" class="mb-1">{{ r }}</li>
                  </ul>
                </div>
              </div>
              <template v-if="result.missing_data_checklist?.length">
                <hr>
                <div class="small fw-semibold mb-1 text-warning">Data Gaps Detected</div>
                <ul class="small ps-3 mb-0">
                  <li v-for="(m, i) in result.missing_data_checklist" :key="`m-${i}`">{{ m }}</li>
                </ul>
              </template>
            </div>
          </div>
        </div>

        <div v-else class="card border-0 shadow-sm">
          <div class="card-body text-muted">
            Run analysis to see results here.
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';
import { analyzeLatestVideo } from '../services/videoAnalysis';

const route = useRoute();
const creatorId = route.params.id;
const isSelfMode = !creatorId;

const inputJson = ref('');
const inputError = ref('');
const apiError = ref('');
const apiNoticeKind = ref('warning');
const running = ref(false);
const autoRunning = ref(false);
const loadingTemplate = ref(false);
const result = ref(null);
const autoMeta = ref(null);

function fmtDate(v) {
  if (!v) return '—';
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString();
}

function fmtNum(v) {
  if (v === null || v === undefined) return '—';
  const n = Number(v);
  return Number.isFinite(n) ? n.toLocaleString() : '—';
}

function percent(v) {
  if (v === null || v === undefined) return '—';
  const n = Number(v);
  return Number.isFinite(n) ? `${(n * 100).toFixed(2)}%` : '—';
}

function clampScore(v) {
  const n = Number(v);
  if (!Number.isFinite(n)) return 0;
  return Math.max(0, Math.min(100, Math.round(n)));
}

function capitalize(v) {
  const s = String(v || '').trim();
  if (!s) return 'Unknown';
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}

function goNoGoBadgeClass(v) {
  const key = String(v || '').toLowerCase();
  if (key === 'go') return 'bg-success';
  if (key === 'conditional_go') return 'bg-warning text-dark';
  if (key === 'no_go') return 'bg-danger';
  return 'bg-secondary';
}

function confidenceBadgeClass(v) {
  const key = String(v || '').toLowerCase();
  if (key === 'high') return 'bg-success';
  if (key === 'medium') return 'bg-info text-dark';
  if (key === 'low') return 'bg-secondary';
  return 'bg-secondary';
}

function verdictBorderClass(v) {
  const key = String(v || '').toLowerCase();
  if (key === 'go') return 'border-success';
  if (key === 'conditional_go') return 'border-warning';
  if (key === 'no_go') return 'border-danger';
  return 'border-secondary';
}

function readinessBarClass(v) {
  const score = clampScore(v);
  if (score >= 75) return 'bg-success';
  if (score >= 55) return 'bg-warning';
  return 'bg-danger';
}

function toPretty(v) {
  return JSON.stringify(v ?? {}, null, 2);
}

function joinList(v) {
  return Array.isArray(v) && v.length ? v.join(', ') : 'None';
}

function emotionBadgeClass(emotion) {
  const map = {
    joy: 'bg-success', anger: 'bg-danger', sadness: 'bg-secondary',
    fear: 'bg-warning text-dark', surprise: 'bg-info text-dark', neutral: 'bg-light text-dark',
    disgust: 'bg-dark'
  };
  return map[(emotion || '').toLowerCase()] || 'bg-secondary';
}

function isAuthError(error) {
  const status = error?.response?.status;
  return status === 401 || status === 403;
}

function isNotFoundError(error) {
  return error?.response?.status === 404;
}

function normalizePayload(raw) {
  const toNullableLong = (value) => {
    if (value === null || value === undefined || value === '') return null;
    const n = Number(value);
    if (!Number.isFinite(n)) return null;
    return Math.max(0, Math.round(n));
  };

  const payload = (raw && typeof raw === 'object') ? { ...raw } : {};
  payload.video = (payload.video && typeof payload.video === 'object') ? { ...payload.video } : {};
  payload.video.statistics = (payload.video.statistics && typeof payload.video.statistics === 'object')
    ? { ...payload.video.statistics }
    : {};

  if (!Array.isArray(payload.comments)) payload.comments = [];
  if (!Array.isArray(payload.timeSeries)) payload.timeSeries = [];
  if (!Array.isArray(payload.video.tags)) payload.video.tags = [];

  if (typeof payload.autoFetchComments !== 'boolean') payload.autoFetchComments = true;
  if (!Number.isFinite(Number(payload.maxCommentsToFetch))) payload.maxCommentsToFetch = 500;
  payload.maxCommentsToFetch = Math.max(50, Math.min(2000, Math.round(Number(payload.maxCommentsToFetch))));

  payload.video.statistics.viewCount = toNullableLong(payload.video.statistics.viewCount);
  payload.video.statistics.likeCount = toNullableLong(payload.video.statistics.likeCount);
  payload.video.statistics.commentCount = toNullableLong(payload.video.statistics.commentCount);
  payload.video.statistics.favoriteCount = toNullableLong(payload.video.statistics.favoriteCount);

  payload.timeSeries = payload.timeSeries.map(point => {
    const p = (point && typeof point === 'object') ? { ...point } : {};
    p.viewCount = toNullableLong(p.viewCount);
    p.likeCount = toNullableLong(p.likeCount);
    p.commentCount = toNullableLong(p.commentCount);
    if (!p.timestampUtc) p.timestampUtc = new Date().toISOString();
    return p;
  });

  return payload;
}

async function loadTemplate() {
  loadingTemplate.value = true;
  apiError.value = '';
  apiNoticeKind.value = 'warning';
  try {
    const { data } = isSelfMode
      ? await api.get('/creator/dashboard')
      : await api.get(`/creators/${creatorId}/analytics`);

    const creator = isSelfMode
      ? {
          channelName: data?.channel?.channelName || data?.profile?.name || null,
          channelId: data?.channel?.channelId || null,
          subscribers: data?.channel?.subscribers ?? null,
          primaryLanguage: data?.profile?.language || null
        }
      : data;

    const template = {
      creatorName: creator?.channelName || null,
      channelId: creator?.channelId || null,
      todayUtc: new Date().toISOString(),
      autoFetchComments: true,
      maxCommentsToFetch: 500,
      video: {
        videoId: null,
        title: '',
        description: '',
        tags: [],
        categoryId: null,
        publishedAt: null,
        duration: null,
        madeForKids: null,
        defaultLanguage: null,
        defaultAudioLanguage: null,
        statistics: {
          viewCount: null,
          likeCount: null,
          commentCount: null,
          favoriteCount: null
        }
      },
      channelContext: {
        title: creator?.channelName || null,
        country: null,
        subscriberCount: creator?.subscribers ?? null
      },
      comments: [],
      timeSeries: []
    };
    inputJson.value = JSON.stringify(template, null, 2);
  } catch (e) {
    apiNoticeKind.value = 'warning';
    apiError.value = e.response?.data?.error || 'Failed to load creator template context.';
  } finally {
    loadingTemplate.value = false;
  }
}

function buildPayloadFromContext(creatorData, latestVideoRow) {
  const hasBrand = Boolean(latestVideoRow?.brandName);
  const title = latestVideoRow?.title || '';
  const description = hasBrand
    ? `Auto-generated from latest analytics row. Possible brand mention: ${latestVideoRow.brandName}.`
    : 'Auto-generated from latest analytics row.';

  return {
    creatorName: creatorData?.channelName || null,
    channelId: creatorData?.channelId || null,
    todayUtc: new Date().toISOString(),
    autoFetchComments: true,
    maxCommentsToFetch: 500,
    video: {
      videoId: latestVideoRow?.youtubeVideoId || null,
      title,
      description,
      tags: hasBrand ? [String(latestVideoRow.brandName)] : [],
      categoryId: null,
      publishedAt: latestVideoRow?.publishedAt || null,
      duration: null,
      madeForKids: null,
      defaultLanguage: creatorData?.primaryLanguage || null,
      defaultAudioLanguage: creatorData?.primaryLanguage || null,
      statistics: {
        viewCount: latestVideoRow?.views ?? null,
        likeCount: latestVideoRow?.likes ?? null,
        commentCount: latestVideoRow?.comments ?? null,
        favoriteCount: null
      }
    },
    channelContext: {
      title: creatorData?.channelName || null,
      country: 'IN',
      subscriberCount: creatorData?.subscribers ?? null
    },
    comments: [],
    timeSeries: latestVideoRow
      ? [{
          timestampUtc: latestVideoRow.publishedAt || new Date().toISOString(),
          viewCount: latestVideoRow.views ?? null,
          likeCount: latestVideoRow.likes ?? null,
          commentCount: latestVideoRow.comments ?? null
        }]
      : []
  };
}

async function analyzeLatestAuto() {
  autoRunning.value = true;
  apiError.value = '';
  apiNoticeKind.value = 'warning';
  inputError.value = '';
  result.value = null;
  autoMeta.value = null;

  try {
    let creatorData;
    let latestVideo = null;
    let usedFallback = false;

    if (isSelfMode) {
      const { data: dashboardData } = await api.get('/creator/dashboard');
      creatorData = {
        channelName: dashboardData?.channel?.channelName || dashboardData?.profile?.name || null,
        channelId: dashboardData?.channel?.channelId || null,
        subscribers: dashboardData?.channel?.subscribers ?? null,
        primaryLanguage: dashboardData?.profile?.language || null,
        averageViews: dashboardData?.avgViewsPerVideo ?? null
      };

      latestVideo = Array.isArray(dashboardData?.recentVideos) && dashboardData.recentVideos.length
        ? {
            title: dashboardData.recentVideos[0].title,
            brandName: null,
            publishedAt: dashboardData.recentVideos[0].publishedAt,
            views: dashboardData.recentVideos[0].viewCount,
            likes: dashboardData.recentVideos[0].likeCount,
            comments: dashboardData.recentVideos[0].commentCount,
            youtubeVideoId: dashboardData.recentVideos[0].youtubeVideoId
          }
        : null;

      if (!latestVideo) {
        usedFallback = true;
      }
    } else {
      try {
        const { data } = await api.get(`/creators/${creatorId}/analytics`);
        creatorData = data;
      } catch (creatorError) {
        if (isAuthError(creatorError)) throw creatorError;

        usedFallback = true;
        creatorData = {
          channelName: `Creator #${creatorId}`,
          channelId: null,
          subscribers: null,
          primaryLanguage: null,
          averageViews: null
        };
      }

      try {
        const { data: videoData } = await api.get(`/creators/${creatorId}/video-analytics`);
        latestVideo = Array.isArray(videoData?.videos) && videoData.videos.length
          ? videoData.videos[0]
          : null;
      } catch (videoError) {
        if (isAuthError(videoError)) {
          throw videoError;
        }

        if (!isNotFoundError(videoError)) {
          // Non-auth, non-404 failures should not block analysis; we proceed with fallback data.
          console.warn('Video analytics fetch failed, using fallback latest video payload.', videoError);
        }

        usedFallback = true;
      }
    }

    if (!latestVideo) {
      usedFallback = true;
      latestVideo = {
        title: `Latest upload by ${creatorData?.channelName || 'creator'}`,
        brandName: null,
        publishedAt: new Date().toISOString(),
        views: creatorData?.averageViews ?? null,
        likes: null,
        comments: null,
        youtubeVideoId: null
      };
    }

    const payload = normalizePayload(buildPayloadFromContext(creatorData, latestVideo));
    inputJson.value = JSON.stringify(payload, null, 2);
    autoMeta.value = {
      title: usedFallback
        ? `${latestVideo.title || 'Latest upload'} (fallback)`
        : (latestVideo.title || null),
      id: latestVideo.youtubeVideoId || null
    };

    if (usedFallback) {
      apiNoticeKind.value = 'info';
      apiError.value = 'Used limited fallback context because latest video analytics is unavailable for this account.';
    }

    const data = await analyzeLatestVideo(payload);
    result.value = data;
    // If YouTube API enriched the video, clear the fallback warning
    if (usedFallback && data?.enriched_from_youtube) {
      apiError.value = 'Analytics row unavailable; video data was retrieved directly from YouTube API.';
      apiNoticeKind.value = 'info';
    }
  } catch (e) {
    apiNoticeKind.value = 'warning';
    apiError.value = e.userMessage || e.response?.data?.error || e.response?.data?.message || 'Failed to auto-analyze latest video.';
  } finally {
    autoRunning.value = false;
  }
}

async function runAnalysis() {
  inputError.value = '';
  apiError.value = '';
  apiNoticeKind.value = 'warning';
  running.value = true;
  try {
    let payload;
    try {
      payload = JSON.parse(inputJson.value || '{}');
    } catch {
      inputError.value = 'Invalid JSON. Please fix syntax and retry.';
      return;
    }

    const normalizedPayload = normalizePayload(payload);
    if (!normalizedPayload.video || typeof normalizedPayload.video !== 'object') {
      inputError.value = 'Video payload is required.';
      return;
    }

    const data = await analyzeLatestVideo(normalizedPayload);
    result.value = data;
  } catch (e) {
    apiNoticeKind.value = 'warning';
    apiError.value = e.userMessage || e.response?.data?.error || e.response?.data?.message || 'Failed to run analysis.';
  } finally {
    running.value = false;
  }
}

loadTemplate();
</script>
