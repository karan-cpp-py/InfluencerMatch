<template>
  <div class="container-fluid py-4 marketplace-shell" style="max-width: 1140px; margin: 0 auto;">

    <!-- Header -->
    <div class="market-hero card border-0 mb-4 p-4">
      <div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
        <div>
          <h2 class="fw-bold mb-1">Creator Marketplace</h2>
          <p class="text-muted mb-0">Discover registered creators, inspect analytics, and send collaboration requests.</p>
        </div>
        <div class="d-flex gap-2 align-items-center">
          <span class="badge rounded-pill text-bg-light border fs-6 px-3 py-2">{{ totalResults }} creators</span>
          <span class="badge rounded-pill bg-primary-subtle text-primary border px-3 py-2">Live Filters</span>
        </div>
      </div>
    </div>

    <!-- Filters -->
    <div class="card border-0 shadow-sm mb-3 sticky-top filter-dock">
      <div class="card-body p-3">
        <div class="row g-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label small fw-semibold mb-1">Search</label>
            <input v-model="filters.search" class="form-control form-control-sm" placeholder="Channel name…" @keyup.enter="search(1)" />
          </div>
          <div class="col-md-2">
            <label class="form-label small fw-semibold mb-1">Language</label>
            <select v-model="filters.language" class="form-select form-select-sm">
              <option value="">All</option>
              <option v-for="l in languages" :key="l" :value="l">{{ l }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small fw-semibold mb-1">Category</label>
            <input v-model="filters.category" class="form-control form-control-sm" placeholder="Tech, Gaming…" />
          </div>
          <div class="col-md-2">
            <label class="form-label small fw-semibold mb-1">Tier</label>
            <select v-model="filters.creatorTier" class="form-select form-select-sm">
              <option value="">All Tiers</option>
              <option value="Nano">Nano (&lt;10K)</option>
              <option value="Micro">Micro (10K–100K)</option>
              <option value="MidTier">Mid-Tier (100K–500K)</option>
              <option value="Macro">Macro (500K–1M)</option>
              <option value="Mega">Mega (1M+)</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small fw-semibold mb-1">Sort By</label>
            <select v-model="filters.sortBy" class="form-select form-select-sm">
              <option value="subscribers">Subscribers</option>
              <option value="engagement">Engagement Rate</option>
              <option value="views">Total Views</option>
            </select>
          </div>
          <div class="col-md-1 d-flex gap-1">
            <button class="btn btn-primary btn-sm w-100" @click="search(1)">Go</button>
            <button class="btn btn-outline-secondary btn-sm" @click="resetFilters">✕</button>
          </div>
        </div>
      </div>
    </div>

    <div class="d-flex flex-wrap gap-2 mb-3" v-if="activeFilterEntries.length">
      <span class="badge rounded-pill text-bg-light border px-3 py-2" v-for="entry in activeFilterEntries" :key="entry[0]">
        {{ labelForFilter(entry[0]) }}: {{ entry[1] }}
        <button type="button" class="btn btn-sm p-0 ms-2 text-muted border-0" @click="clearFilter(entry[0])">x</button>
      </span>
    </div>

    <div v-if="apiError" class="alert alert-warning mb-3">{{ apiError }}</div>

    <div v-if="isBrandUser" class="card border-0 shadow-sm mb-4">
      <div class="card-body p-3">
        <div class="d-flex justify-content-between align-items-center mb-2">
          <h6 class="fw-semibold mb-0">My Collaboration Workflow</h6>
          <button class="btn btn-outline-primary btn-sm" @click="loadMyRequests">Refresh</button>
        </div>

        <div v-if="myRequests.length === 0" class="small text-muted">No collaboration requests yet.</div>
        <div v-else class="d-flex flex-column gap-2">
          <div v-for="req in myRequests" :key="req.requestId" class="border rounded p-2">
            <div class="d-flex justify-content-between align-items-start gap-2">
              <div>
                <div class="fw-semibold">{{ req.campaignTitle }}</div>
                <div class="small text-muted">Creator: {{ req.channelName }} | Status: {{ req.status }}</div>
              </div>
              <button class="btn btn-outline-secondary btn-sm" @click="openWorkflow(req.requestId)">Open</button>
            </div>
          </div>
        </div>

        <div v-if="workflow" class="mt-3 border-top pt-3">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="fw-semibold mb-0">Workflow: {{ workflow.request.campaignTitle }}</h6>
            <span class="small text-muted">{{ workflow.completionPercent }}% complete</span>
          </div>

          <div class="row g-2 mb-2">
            <div class="col-md-4">
              <input v-model="newMilestone.title" class="form-control form-control-sm" placeholder="Milestone title" />
            </div>
            <div class="col-md-4">
              <input v-model="newMilestone.description" class="form-control form-control-sm" placeholder="Deliverable details" />
            </div>
            <div class="col-md-3">
              <input v-model="newMilestone.dueDate" type="date" class="form-control form-control-sm" />
            </div>
            <div class="col-md-1">
              <button class="btn btn-primary btn-sm w-100" @click="addMilestone">Add</button>
            </div>
          </div>

          <div class="d-flex flex-column gap-2">
            <div v-for="m in workflow.milestones" :key="m.collaborationMilestoneId" class="border rounded p-2">
              <div class="d-flex justify-content-between align-items-center">
                <div>
                  <div class="fw-semibold">{{ m.title }}</div>
                  <div class="small text-muted">{{ m.description || 'No description' }}</div>
                </div>
                <span class="badge bg-light text-dark border">{{ m.status }}</span>
              </div>
              <div class="small text-muted" v-if="m.deliverableUrl">Deliverable: {{ m.deliverableUrl }}</div>
              <div class="d-flex gap-2 mt-2 flex-wrap">
                <button class="btn btn-outline-success btn-sm" @click="setMilestone(m, 'Approved')">Approve</button>
                <button class="btn btn-outline-secondary btn-sm" @click="setMilestone(m, 'Completed')">Complete</button>
                <button class="btn btn-outline-warning btn-sm" @click="requestRevision(m)">Request Revision</button>
              </div>
            </div>
          </div>

          <div class="mt-2">
            <button class="btn btn-success btn-sm" @click="completeWorkflow">Mark Collaboration Complete</button>
          </div>

          <h6 class="fw-semibold mt-3 mb-2">Activity Feed</h6>
          <ul class="small ps-3 mb-0" v-if="workflow.activityFeed?.length">
            <li v-for="a in workflow.activityFeed" :key="a.collaborationActivityId" class="mb-1">
              <strong>{{ a.actorRole }}</strong>: {{ a.message }} <span class="text-muted">({{ fmtDateTime(a.createdAt) }})</span>
            </li>
          </ul>
          <div v-else class="small text-muted">No activity yet.</div>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="py-2">
      <div class="row g-3 mb-2">
        <div class="col-md-6 col-lg-4" v-for="idx in 6" :key="idx">
          <div class="card border-0 p-3 placeholder-glow">
            <div class="d-flex gap-3 mb-3">
              <span class="placeholder rounded-circle" style="width:56px;height:56px;"></span>
              <div class="flex-grow-1">
                <div class="placeholder col-8 mb-2"></div>
                <div class="placeholder col-5"></div>
              </div>
            </div>
            <div class="placeholder col-12 mb-2"></div>
            <div class="placeholder col-10 mb-2"></div>
            <div class="placeholder col-6"></div>
          </div>
        </div>
      </div>
    </div>

    <!-- No results -->
    <div v-else-if="creators.length === 0" class="card border-0 p-5 text-center text-muted">
      <p class="fs-5 fw-semibold">No creators found for current filters.</p>
      <p class="small mb-3">Broaden your filters or move to a higher plan for wider discovery access.</p>
      <div class="d-flex justify-content-center gap-2">
        <router-link class="btn btn-outline-primary btn-sm" to="/plans">View Plans</router-link>
        <button class="btn btn-primary btn-sm" @click="resetFilters">Reset Filters</button>
      </div>
    </div>

    <!-- Creator grid -->
    <div v-else>
      <div class="row g-3 mb-4">
        <div v-for="c in creators" :key="c.creatorProfileId" class="col-md-6 col-lg-4">
          <div class="card h-100 border-0 shadow-sm creator-card" @click="openDetail(c)" style="cursor:pointer;">
            <div class="card-body p-3">
              <div class="d-flex gap-3 align-items-start mb-3">
                <img v-if="c.thumbnailUrl" :src="c.thumbnailUrl" class="rounded-circle flex-shrink-0"
                     width="56" height="56" :alt="c.channelName" />
                <div v-else class="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center flex-shrink-0 fs-5 fw-bold"
                     style="width:56px;height:56px;">{{ c.channelName?.[0] }}</div>
                <div class="min-w-0">
                  <div class="d-flex align-items-center gap-1 flex-wrap">
                    <h6 class="fw-bold mb-0 text-truncate" style="max-width:160px;">{{ c.channelName }}</h6>
                    <span v-if="c.isVerified" class="badge bg-primary" style="font-size:10px;">✓</span>
                  </div>
                  <div class="d-flex gap-1 flex-wrap mt-1">
                    <span v-if="c.creatorTier" class="badge bg-secondary" style="font-size:10px;">{{ c.creatorTier }}</span>
                    <span v-if="c.language" class="badge bg-light text-dark border" style="font-size:10px;">{{ c.language }}</span>
                    <span v-if="c.category" class="badge bg-light text-dark border" style="font-size:10px;">{{ c.category }}</span>
                  </div>
                </div>
              </div>

              <div class="row g-2 text-center mb-3">
                <div class="col-4">
                  <div class="fw-bold text-primary small">{{ fmtNum(c.subscribers) }}</div>
                  <div class="text-muted" style="font-size:10px;">Subscribers</div>
                </div>
                <div class="col-4">
                  <div class="fw-bold text-success small">{{ creatorEngagementMeta(c).formatted }}</div>
                  <span
                    v-if="creatorEngagementMeta(c).badgeText"
                    class="badge mt-1"
                    :class="creatorEngagementMeta(c).badgeClass"
                    :title="creatorEngagementMeta(c).tooltip"
                    style="font-size:9px;"
                  >
                    {{ creatorEngagementMeta(c).badgeText }}
                  </span>
                  <div class="text-muted" style="font-size:10px;">Engagement</div>
                </div>
                <div class="col-4">
                  <div class="fw-bold text-info small">{{ fmtNum(c.totalViews) }}</div>
                  <div class="text-muted" style="font-size:10px;">Total Views</div>
                </div>
              </div>

              <div class="d-flex justify-content-between align-items-center">
                <span v-if="c.country" class="text-muted small">📍 {{ c.country }}</span>
                <button class="btn btn-outline-primary btn-sm ms-auto" @click.stop="openDetail(c)">
                  View Profile
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="d-flex justify-content-center gap-1 mt-2">
        <button class="btn btn-outline-secondary btn-sm" :disabled="page <= 1" @click="search(page - 1)">‹</button>
        <button v-for="p in visiblePages" :key="p" class="btn btn-sm"
                :class="p === page ? 'btn-primary' : 'btn-outline-secondary'" @click="search(p)">{{ p }}</button>
        <button class="btn btn-outline-secondary btn-sm" :disabled="page >= totalPages" @click="search(page + 1)">›</button>
      </div>
    </div>

    <!-- ── Detail Modal ─────────────────────────────────────────────────── -->
    <div v-if="detail" class="modal-backdrop-custom" @click.self="detail = null">
      <div class="modal-panel shadow-lg" role="dialog">
        <button class="btn-close position-absolute top-0 end-0 m-3" @click="detail = null"></button>

        <div v-if="loadingDetail" class="text-center py-5">
          <div class="spinner-border text-primary"></div>
        </div>
        <div v-else>
          <!-- Header -->
          <div class="d-flex gap-3 align-items-start mb-3">
            <img v-if="detail.thumbnailUrl" :src="detail.thumbnailUrl" class="rounded-circle" width="72" height="72" />
            <div>
              <div class="d-flex align-items-center gap-2 flex-wrap">
                <h4 class="fw-bold mb-0">{{ detail.channelName }}</h4>
                <span v-if="detail.isVerified" class="badge bg-primary">✓ Verified</span>
                <span v-if="detail.creatorTier" class="badge bg-secondary">{{ detail.creatorTier }}</span>
              </div>
              <div class="d-flex gap-2 flex-wrap mt-1">
                <span v-if="detail.language" class="badge bg-light text-dark border">{{ detail.language }}</span>
                <span v-if="detail.category" class="badge bg-light text-dark border">{{ detail.category }}</span>
                <span v-if="detail.country" class="text-muted small">📍 {{ detail.country }}</span>
              </div>
            </div>
          </div>

          <p v-if="detail.bio || detail.description" class="text-muted small mb-3">{{ detail.bio || detail.description }}</p>

          <!-- Stats row -->
          <div class="row g-2 mb-3">
            <div class="col-4">
              <div class="card border-0 bg-light text-center py-2">
                <div class="fw-bold text-primary">{{ fmtNum(detail.subscribers) }}</div>
                <div class="text-muted" style="font-size:11px;">Subscribers</div>
              </div>
            </div>
            <div class="col-4">
              <div class="card border-0 bg-light text-center py-2">
                <div class="fw-bold text-success">{{ detailEngagementMeta.formatted }}</div>
                <span
                  v-if="detailEngagementMeta.badgeText"
                  class="badge mt-1"
                  :class="detailEngagementMeta.badgeClass"
                  :title="detailEngagementMeta.tooltip"
                  style="font-size:10px;"
                >
                  {{ detailEngagementMeta.badgeText }}
                </span>
                <div class="text-muted" style="font-size:11px;">Engagement</div>
              </div>
            </div>
            <div class="col-4">
              <div class="card border-0 bg-light text-center py-2">
                <div class="fw-bold text-info">{{ fmtNum(detail.totalViews) }}</div>
                <div class="text-muted" style="font-size:11px;">Total Views</div>
              </div>
            </div>
          </div>

          <!-- Analytics links -->
          <div v-if="detail.creatorId" class="d-flex gap-2 mb-3 flex-wrap">
            <router-link :to="`/creator/${detail.creatorId}/analytics`"
              class="btn btn-sm btn-outline-primary" @click="detail = null">
              📊 Channel Analytics
            </router-link>
            <router-link :to="`/creator/${detail.creatorId}/video-analytics`"
              class="btn btn-sm btn-outline-success" @click="detail = null">
              🎬 Video Analytics
            </router-link>
          </div>

          <!-- Contact -->
          <div v-if="detail.contactEmail || detail.instagramHandle" class="d-flex gap-3 mb-3 small text-muted">
            <span v-if="detail.contactEmail">✉ {{ detail.contactEmail }}</span>
            <span v-if="detail.instagramHandle">📷 @{{ detail.instagramHandle }}</span>
          </div>

          <!-- Recent videos -->
          <div v-if="detail.recentVideos?.length" class="mb-3">
            <h6 class="fw-semibold mb-2">Recent Videos</h6>
            <div class="row g-2">
              <div v-for="v in detail.recentVideos.slice(0, 6)" :key="v.youtubeVideoId" class="col-4">
                <div class="card border-0 bg-light overflow-hidden">
                  <img v-if="v.thumbnailUrl" :src="v.thumbnailUrl" class="w-100" style="height:70px;object-fit:cover;" />
                  <div class="p-1">
                    <p class="mb-0 small text-truncate fw-semibold" style="font-size:10px;">{{ v.title }}</p>
                    <p class="mb-0 text-muted" style="font-size:10px;">👁 {{ fmtNum(v.viewCount) }}</p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Send collab request -->
          <div class="card border-0 bg-light p-3">
            <h6 class="fw-semibold mb-2">Send Collaboration Request</h6>
            <div class="row g-2">
              <div class="col-md-7">
                <input v-model="collabForm.campaignTitle" class="form-control form-control-sm" placeholder="Campaign title…" />
              </div>
              <div class="col-md-5">
                <div class="input-group input-group-sm">
                  <span class="input-group-text">$</span>
                  <input v-model.number="collabForm.budget" type="number" class="form-control" placeholder="Budget" />
                </div>
              </div>
              <div class="col-12">
                <textarea v-model="collabForm.message" class="form-control form-control-sm" rows="2" placeholder="Your message to the creator…"></textarea>
              </div>
            </div>
            <div class="mt-2 d-flex align-items-center gap-2">
              <button class="btn btn-primary btn-sm" @click="sendCollab" :disabled="sendingCollab || !collabForm.campaignTitle">
                <span v-if="sendingCollab" class="spinner-border spinner-border-sm me-1"></span>
                Send Request
              </button>
              <span v-if="collabSent" class="text-success small">✓ Request sent!</span>
              <span v-if="collabError" class="text-danger small">{{ collabError }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import api from '../services/api';
import { trackFunnelEvent } from '../services/funnel';
import { authRole } from '../services/auth';
import { engagementMeta } from '../utils/engagement';

const loading = ref(false);
const loadingDetail = ref(false);
const creators = ref([]);
const totalResults = ref(0);
const page = ref(1);
const pageSize = 12;

const languages = ['Hindi', 'English', 'Tamil', 'Telugu', 'Kannada', 'Malayalam', 'Punjabi', 'Bengali', 'Marathi', 'Haryanvi'];

const filters = ref({
  search: '', language: '', category: '', creatorTier: '', sortBy: 'subscribers'
});

const detail = ref(null);
const collabForm = ref({ campaignTitle: '', budget: 0, message: '' });
const sendingCollab = ref(false);
const collabSent = ref(false);
const collabError = ref('');
const apiError = ref('');
const myRequests = ref([]);
const workflow = ref(null);
const newMilestone = ref({ title: '', description: '', dueDate: '' });

const isBrandUser = computed(() => ['Brand', 'Agency'].includes(authRole.value || localStorage.getItem('role')));

const activeFilterEntries = computed(() =>
  Object.entries(filters.value)
    .filter(([key, value]) => {
      if (key === 'sortBy' && value === 'subscribers') return false;
      return value !== '' && value !== null && value !== undefined;
    })
);

const totalPages = computed(() => Math.ceil(totalResults.value / pageSize));
const visiblePages = computed(() => {
  const total = totalPages.value;
  const cur = page.value;
  const pages = [];
  for (let p = Math.max(1, cur - 2); p <= Math.min(total, cur + 2); p++) pages.push(p);
  return pages;
});

const detailEngagementMeta = computed(() => {
  if (!detail.value) return engagementMeta(null);
  return creatorEngagementMeta(detail.value);
});

onMounted(async () => {
  await search(1);
  if (isBrandUser.value) {
    await loadMyRequests();
  }
});

async function search(p) {
  page.value = p;
  loading.value = true;
  apiError.value = '';
  try {
    const params = {
      page: p,
      pageSize,
      ...Object.fromEntries(Object.entries(filters.value).filter(([, v]) => v !== '' && v !== null))
    };
    const res = await api.get('/marketplace/creators', { params });
    creators.value = res.data.items || [];
    totalResults.value = res.data.total || 0;
  } catch (e) {
    console.error('Marketplace search failed', e);
    apiError.value = e.response?.data?.error || 'Failed to load marketplace creators.';
    creators.value = [];
    totalResults.value = 0;
  } finally {
    loading.value = false;
  }
}

function resetFilters() {
  filters.value = { search: '', language: '', category: '', creatorTier: '', sortBy: 'subscribers' };
  search(1);
}

function clearFilter(key) {
  if (key === 'sortBy') {
    filters.value.sortBy = 'subscribers';
  } else {
    filters.value[key] = '';
  }
  search(1);
}

function labelForFilter(key) {
  return {
    search: 'Search',
    language: 'Language',
    category: 'Category',
    creatorTier: 'Tier',
    sortBy: 'Sort'
  }[key] || key;
}

async function openDetail(creator) {
  detail.value = creator;   // show modal with basic data immediately
  collabForm.value = { campaignTitle: '', budget: 0, message: '' };
  collabSent.value = false;
  collabError.value = '';
  loadingDetail.value = true;
  try {
    const res = await api.get(`/marketplace/creators/${creator.creatorProfileId}`);
    detail.value = res.data;
  } catch (e) {
    console.error('Detail load failed', e);
  } finally {
    loadingDetail.value = false;
  }
}

async function sendCollab() {
  sendingCollab.value = true;
  collabSent.value = false;
  collabError.value = '';
  try {
    await api.post('/collaborations', {
      creatorProfileId: detail.value.creatorProfileId,
      campaignTitle: collabForm.value.campaignTitle,
      budget: collabForm.value.budget,
      message: collabForm.value.message
    });

    await trackFunnelEvent('first_request_sent', {
      creatorProfileId: detail.value.creatorProfileId,
      source: 'marketplace',
    });

    collabSent.value = true;
    collabForm.value = { campaignTitle: '', budget: 0, message: '' };
    await loadMyRequests();
  } catch (e) {
    collabError.value = e.response?.data?.error || 'Failed to send request.';
  } finally {
    sendingCollab.value = false;
  }
}

async function loadMyRequests() {
  if (!isBrandUser.value) return;
  try {
    const res = await api.get('/collaborations');
    myRequests.value = res.data || [];
  } catch {
    myRequests.value = [];
  }
}

async function openWorkflow(requestId) {
  try {
    const res = await api.get(`/collaborations/${requestId}/workflow`);
    workflow.value = res.data;
  } catch (e) {
    apiError.value = e.response?.data?.error || 'Failed to load workflow.';
  }
}

async function addMilestone() {
  if (!workflow.value?.request?.requestId || !newMilestone.value.title) return;
  try {
    await api.post(`/collaborations/${workflow.value.request.requestId}/milestones`, {
      title: newMilestone.value.title,
      description: newMilestone.value.description,
      dueDate: newMilestone.value.dueDate || null,
    });
    newMilestone.value = { title: '', description: '', dueDate: '' };
    await openWorkflow(workflow.value.request.requestId);
  } catch (e) {
    apiError.value = e.response?.data?.error || 'Failed to add milestone.';
  }
}

async function setMilestone(milestone, status) {
  try {
    await api.patch(`/collaborations/milestones/${milestone.collaborationMilestoneId}/status`, {
      status,
      deliverableUrl: milestone.deliverableUrl || null,
    });
    await openWorkflow(workflow.value.request.requestId);
  } catch (e) {
    apiError.value = e.response?.data?.error || 'Failed to update milestone status.';
  }
}

async function requestRevision(milestone) {
  const notes = window.prompt('Enter revision notes for creator:');
  if (!notes) return;
  try {
    await api.post(`/collaborations/milestones/${milestone.collaborationMilestoneId}/revision`, {
      revisionNotes: notes,
    });
    await openWorkflow(workflow.value.request.requestId);
  } catch (e) {
    apiError.value = e.response?.data?.error || 'Failed to request revision.';
  }
}

async function completeWorkflow() {
  if (!workflow.value?.request?.requestId) return;
  try {
    await api.post(`/collaborations/${workflow.value.request.requestId}/complete`);
    await loadMyRequests();
    await openWorkflow(workflow.value.request.requestId);
  } catch (e) {
    apiError.value = e.response?.data?.error || 'Unable to complete collaboration.';
  }
}

function fmtDateTime(d) {
  return new Date(d).toLocaleString();
}

function extractSampleCount(creator) {
  const candidates = [
    creator?.totalVideos,
    creator?.videoCount,
    creator?.recentVideosCount,
    Array.isArray(creator?.recentVideos) ? creator.recentVideos.length : null
  ];

  for (const count of candidates) {
    const n = Number(count);
    if (Number.isFinite(n) && n >= 0) return n;
  }

  return null;
}

function creatorEngagementMeta(creator) {
  return engagementMeta(creator?.engagementRate, {
    mode: 'auto',
    decimals: 2,
    sampleCount: extractSampleCount(creator),
    minSampleCount: 3,
    fallback: '—'
  });
}

function fmtNum(n) {
  if (!n && n !== 0) return '—';
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
  return n.toString();
}
</script>

<style scoped>
.creator-card { transition: transform .15s, box-shadow .15s; }
.creator-card:hover { transform: translateY(-2px); box-shadow: 0 6px 20px rgba(0,0,0,.12) !important; }

.marketplace-shell {
  animation: fade-up 0.35s ease both;
}

.market-hero {
  background: linear-gradient(125deg, rgba(15, 23, 42, 0.92), rgba(14, 116, 144, 0.88), rgba(29, 78, 216, 0.86));
  color: #fff;
}

.market-hero .text-muted {
  color: rgba(255, 255, 255, 0.8) !important;
}

.filter-dock {
  top: 86px;
  z-index: 9;
}

.modal-backdrop-custom {
  position: fixed; inset: 0;
  background: rgba(0,0,0,.45);
  z-index: 1050;
  display: flex; align-items: center; justify-content: center;
  padding: 1rem;
  overflow-y: auto;
}
.modal-panel {
  background: #fff;
  border-radius: 12px;
  max-width: 680px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  padding: 2rem;
  position: relative;
}
</style>
