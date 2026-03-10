<template>
  <div class="creator-dashboard py-4">
    <div class="container-fluid" style="max-width: 1020px; margin: 0 auto;">

      <section class="creator-hero mb-4">
        <div>
          <p class="eyebrow mb-2">Creator Command Center</p>
          <h2 class="fw-bold mb-1">Welcome, {{ profile?.name || userName }}</h2>
          <p class="mb-0 hero-subtitle">Manage your profile, channel visibility, and collaboration pipeline.</p>
        </div>
        <div class="hero-actions">
          <span v-if="channel" class="status-pill linked">Channel linked</span>
          <span v-else class="status-pill pending">Channel not linked</span>
          <span class="status-pill soft">Profile {{ profileStrength }}% complete</span>
          <router-link
            to="/creator/latest-video-analysis"
            class="btn btn-sm btn-light fw-semibold"
          >
            AI Latest Video
          </router-link>
        </div>
      </section>

      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary" role="status"></div>
        <p class="mt-2 text-muted">Loading your dashboard...</p>
      </div>

      <div v-else-if="!profile" class="alert alert-warning">
        Profile not found. Please register as a Creator first.
      </div>

      <div v-else>
        <div v-if="channel" class="row g-3 mb-4">
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Subscribers</p>
              <p class="metric-value text-primary">{{ fmtNum(channel.subscribers) }}</p>
            </article>
          </div>
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Total Views</p>
              <p class="metric-value text-success">{{ fmtNum(channel.totalViews) }}</p>
            </article>
          </div>
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Engagement</p>
              <p class="metric-value text-warning">{{ creatorEngagement.formatted }}</p>
              <span
                v-if="creatorEngagement.badgeText"
                class="badge mt-1"
                :class="creatorEngagement.badgeClass"
                :title="creatorEngagement.tooltip"
              >
                {{ creatorEngagement.badgeText }}
              </span>
            </article>
          </div>
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Pending Requests</p>
              <p class="metric-value text-info">{{ pendingCollabs }}</p>
            </article>
          </div>
        </div>

        <div v-if="!channel" class="channel-alert mb-4">
          <span class="fs-5">📢</span>
          <span>You have not linked a YouTube channel yet. Link one below to appear in Brand Marketplace discovery.</span>
        </div>

        <div class="tab-strip mb-4" id="creatorTabs">
          <button class="tab-pill" :class="{ active: tab === 'profile' }" @click="tab = 'profile'">Profile</button>
          <button class="tab-pill" :class="{ active: tab === 'channel' }" @click="tab = 'channel'">Channel</button>
          <button v-if="channel" class="tab-pill" :class="{ active: tab === 'videos' }" @click="tab = 'videos'">Recent Videos</button>
          <button class="tab-pill position-relative" :class="{ active: tab === 'collabs' }" @click="tab = 'collabs'; loadCollabs()">
            Collaborations
            <span v-if="pendingCollabs > 0" class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
              {{ pendingCollabs }}
            </span>
          </button>
        </div>

      <!-- ── TAB: Profile ───────────────────────────────────────────────── -->
      <div v-if="tab === 'profile'" class="card border-0 shadow-sm section-card">
        <div class="card-body p-4">
          <h5 class="fw-semibold mb-3">Your Creator Profile</h5>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label small fw-semibold">Name</label>
              <input :value="profile.name" class="form-control" disabled />
            </div>
            <div class="col-md-6">
              <label class="form-label small fw-semibold">Email</label>
              <input :value="profile.email" class="form-control" disabled />
            </div>
            <div class="col-md-4">
              <label class="form-label small fw-semibold">Country</label>
              <input v-model="editProfile.country" class="form-control" placeholder="e.g. IN" />
            </div>
            <div class="col-md-4">
              <label class="form-label small fw-semibold">Primary Language</label>
              <select v-model="editProfile.language" class="form-select">
                <option value="">Select…</option>
                <option v-for="l in languages" :key="l" :value="l">{{ l }}</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label small fw-semibold">Category</label>
              <input v-model="editProfile.category" class="form-control" placeholder="e.g. Tech, Gaming" />
            </div>
            <div class="col-md-6">
              <label class="form-label small fw-semibold">Instagram Handle</label>
              <div class="input-group">
                <span class="input-group-text">@</span>
                <input v-model="editProfile.instagramHandle" class="form-control" placeholder="your_handle" />
              </div>
            </div>
            <div class="col-md-6">
              <label class="form-label small fw-semibold">Contact Email (shown to brands)</label>
              <input v-model="editProfile.contactEmail" type="email" class="form-control" />
            </div>
            <div class="col-12">
              <label class="form-label small fw-semibold">Bio</label>
              <textarea v-model="editProfile.bio" class="form-control" rows="3" placeholder="Tell brands about yourself…"></textarea>
            </div>
          </div>
          <div class="mt-3 d-flex gap-2">
            <button class="btn btn-primary" @click="saveProfile" :disabled="savingProfile">
              <span v-if="savingProfile" class="spinner-border spinner-border-sm me-1"></span>
              Save Profile
            </button>
            <span v-if="profileSaved" class="text-success align-self-center">✓ Saved!</span>
          </div>
        </div>
      </div>

      <!-- ── TAB: Channel ───────────────────────────────────────────────── -->
      <div v-if="tab === 'channel'">
        <!-- Already linked -->
        <div v-if="channel" class="card border-0 shadow-sm mb-3 section-card">
          <div class="card-body p-4">
            <div class="d-flex gap-4 align-items-start">
              <img v-if="channel.thumbnailUrl" :src="channel.thumbnailUrl" class="rounded-circle" width="80" height="80" :alt="channel.channelName" />
              <div class="flex-grow-1">
                <div class="d-flex align-items-center gap-2 mb-1">
                  <h5 class="fw-bold mb-0">{{ channel.channelName }}</h5>
                  <span v-if="channel.isVerified" class="badge bg-primary">✓ Verified</span>
                  <span v-if="channel.creatorTier" class="badge bg-secondary">{{ channel.creatorTier }}</span>
                </div>
                <p class="text-muted small mb-2">{{ channel.description }}</p>
                <div class="d-flex gap-3 small text-muted">
                  <span>📅 Published: {{ fmtDate(channel.channelPublishedAt) }}</span>
                  <span>🔄 Stats updated: {{ fmtDate(channel.lastStatsUpdatedAt) }}</span>
                </div>
                <a v-if="channel.channelUrl" :href="channel.channelUrl" target="_blank" class="btn btn-outline-danger btn-sm mt-2">
                  ▶ Open on YouTube
                </a>
              </div>
            </div>
          </div>
        </div>

        <!-- Link a channel -->
        <div class="card border-0 shadow-sm section-card">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-1">{{ channel ? 'Re-link Channel' : 'Link Your YouTube Channel' }}</h5>
            <p class="text-muted small mb-3">
              Supported formats: <code>youtube.com/channel/UCxxx</code>, <code>youtube.com/@handle</code>, <code>youtube.com/c/name</code>
            </p>
            <div class="input-group">
              <span class="input-group-text">🔗</span>
              <input v-model="channelUrl" class="form-control" placeholder="https://youtube.com/@YourChannel" />
              <button class="btn btn-primary" @click="linkChannel" :disabled="linkingChannel || !channelUrl">
                <span v-if="linkingChannel" class="spinner-border spinner-border-sm me-1"></span>
                {{ channel ? 'Re-link' : 'Link Channel' }}
              </button>
            </div>
            <div v-if="linkError" class="alert alert-danger mt-2 mb-0 py-2 small">{{ linkError }}</div>
            <div v-if="linkSuccess" class="alert alert-success mt-2 mb-0 py-2 small">✓ {{ linkSuccess }}</div>
          </div>
        </div>
      </div>

      <!-- ── TAB: Recent Videos ─────────────────────────────────────────── -->
      <div v-if="tab === 'videos'">
        <div v-if="recentVideos.length === 0" class="text-center text-muted py-5">No videos fetched yet.</div>
        <div class="row g-3">
          <div v-for="v in recentVideos" :key="v.youtubeVideoId" class="col-md-6 col-lg-4">
            <div class="card border-0 shadow-sm h-100 section-card">
              <img v-if="v.thumbnailUrl" :src="v.thumbnailUrl" class="card-img-top" style="height:160px;object-fit:cover;" :alt="v.title" />
              <div class="card-body p-3">
                <p class="fw-semibold small mb-2 lh-sm" style="display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;">{{ v.title }}</p>
                <div class="d-flex justify-content-between small text-muted">
                  <span>👁 {{ fmtNum(v.viewCount) }}</span>
                  <span>👍 {{ fmtNum(v.likeCount) }}</span>
                  <span>💬 {{ fmtNum(v.commentCount) }}</span>
                </div>
                <div class="small text-muted mt-1">{{ fmtDate(v.publishedAt) }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── TAB: Collaborations ───────────────────────────────────────── -->
      <div v-if="tab === 'collabs'">
        <div v-if="loadingCollabs" class="text-center py-4">
          <div class="spinner-border text-primary"></div>
        </div>
        <div v-else-if="collabs.length === 0" class="text-center text-muted py-5">
          <p class="fs-5">No collaboration requests yet.</p>
          <p class="small">Once you link your channel, brands can discover and contact you.</p>
        </div>
        <div v-else class="d-flex flex-column gap-3">
          <div v-for="c in collabs" :key="c.requestId" class="card border-0 shadow-sm section-card">
            <div class="card-body p-4">
              <div class="d-flex justify-content-between align-items-start">
                <div>
                  <h6 class="fw-bold mb-1">{{ c.campaignTitle }}</h6>
                  <p class="text-muted small mb-1">From: <strong>{{ c.brandName }}</strong></p>
                  <p class="text-muted small mb-1">Budget: <strong>${{ c.budget?.toLocaleString() }}</strong></p>
                  <p v-if="c.message" class="small mb-0 fst-italic">"{{ c.message }}"</p>
                </div>
                <div class="text-end">
                  <span :class="statusBadge(c.status)" class="badge fs-6 mb-2 d-block">{{ c.status }}</span>
                  <div v-if="c.status === 'Pending'" class="d-flex gap-1">
                    <button class="btn btn-success btn-sm" @click="acceptCollab(c.requestId)">Accept</button>
                    <button class="btn btn-outline-danger btn-sm" @click="rejectCollab(c.requestId)">Reject</button>
                  </div>
                  <div class="mt-2">
                    <button class="btn btn-outline-primary btn-sm" @click="openWorkflow(c.requestId)">Open Workflow</button>
                  </div>
                </div>
              </div>
              <div class="text-muted small mt-2">Received: {{ fmtDate(c.createdAt) }}</div>

              <div v-if="selectedWorkflow?.request?.requestId === c.requestId" class="mt-3 border-top pt-3">
                <div class="d-flex justify-content-between align-items-center mb-2">
                  <h6 class="fw-semibold mb-0">Milestones</h6>
                  <span class="small text-muted">{{ selectedWorkflow.completionPercent }}% complete</span>
                </div>

                <div v-if="loadingWorkflow" class="text-center py-2"><div class="spinner-border spinner-border-sm text-primary"></div></div>
                <div v-else>
                  <div v-for="m in selectedWorkflow.milestones" :key="m.collaborationMilestoneId" class="border rounded p-2 mb-2">
                    <div class="d-flex justify-content-between align-items-center">
                      <div>
                        <div class="fw-semibold">{{ m.title }}</div>
                        <div class="small text-muted">{{ m.description || 'No description' }}</div>
                      </div>
                      <span class="badge bg-light text-dark border">{{ m.status }}</span>
                    </div>
                    <div class="small text-muted mt-1" v-if="m.revisionNotes">Revision: {{ m.revisionNotes }}</div>
                    <div class="d-flex gap-2 mt-2">
                      <button class="btn btn-outline-secondary btn-sm" @click="updateMilestone(m, 'InProgress')">Start</button>
                      <button class="btn btn-outline-primary btn-sm" @click="updateMilestone(m, 'Submitted')">Submit</button>
                      <button class="btn btn-outline-success btn-sm" @click="updateMilestone(m, 'Completed')">Mark Complete</button>
                    </div>
                  </div>

                  <h6 class="fw-semibold mt-3 mb-2">Activity Feed</h6>
                  <div class="small text-muted" v-if="!selectedWorkflow.activityFeed?.length">No activity yet.</div>
                  <ul v-else class="small ps-3 mb-0">
                    <li v-for="a in selectedWorkflow.activityFeed" :key="a.collaborationActivityId" class="mb-1">
                      <strong>{{ a.actorRole }}</strong>: {{ a.message }}
                      <span class="text-muted">({{ fmtDate(a.createdAt) }})</span>
                    </li>
                  </ul>
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
import { ref, computed, onMounted } from 'vue';
import api from '../services/api';
import { authUserName } from '../services/auth';
import { trackFunnelEvent } from '../services/funnel';
import { engagementMeta } from '../utils/engagement';

const userName = computed(() => authUserName.value);

const loading = ref(true);
const tab = ref('profile');

// Profile
const profile = ref(null);
const editProfile = ref({ country: '', language: '', category: '', instagramHandle: '', contactEmail: '', bio: '' });
const savingProfile = ref(false);
const profileSaved = ref(false);

// Channel
const channel = ref(null);
const channelUrl = ref('');
const linkingChannel = ref(false);
const linkError = ref('');
const linkSuccess = ref('');

// Videos
const recentVideos = ref([]);
const pendingCollabs = ref(0);

// Collabs
const collabs = ref([]);
const loadingCollabs = ref(false);
const loadingWorkflow = ref(false);
const selectedWorkflow = ref(null);

const languages = ['Hindi', 'English', 'Tamil', 'Telugu', 'Kannada', 'Malayalam', 'Punjabi', 'Bengali', 'Marathi', 'Haryanvi'];

const profileStrength = computed(() => {
  const values = [
    editProfile.value.country,
    editProfile.value.language,
    editProfile.value.category,
    editProfile.value.instagramHandle,
    editProfile.value.contactEmail,
    editProfile.value.bio,
  ];
  const filled = values.filter((v) => String(v || '').trim().length > 0).length;
  return Math.round((filled / values.length) * 100);
});

const creatorEngagement = computed(() => {
  const raw = Number(channel.value?.engagementRate);
  const rawIsUsable = Number.isFinite(raw) && raw > 0;

  if (rawIsUsable) {
    return engagementMeta(raw, { mode: 'auto', decimals: 2 });
  }

  const withViews = (recentVideos.value || []).filter(v => Number(v?.viewCount) > 0);
  if (!withViews.length) {
    return engagementMeta(null, { fallback: '—' });
  }

  const avgRatio = withViews.reduce((sum, v) => {
    const views = Number(v.viewCount || 0);
    const likes = Number(v.likeCount || 0);
    const comments = Number(v.commentCount || 0);
    return sum + ((likes + comments) / views);
  }, 0) / withViews.length;

  return engagementMeta(avgRatio, {
    mode: 'auto',
    decimals: 2,
    estimated: true,
    sampleCount: withViews.length,
    minSampleCount: 3,
    fallback: '—'
  });
});

onMounted(async () => {
  try {
    // Load dashboard data from API
    const res = await api.get('/creator/dashboard');
    const d = res.data;
    profile.value = d.profile;
    channel.value = d.channel;
    recentVideos.value = d.recentVideos || [];
    pendingCollabs.value = d.pendingCollaborations || 0;
    // Prefill edit form
    editProfile.value = {
      country: d.profile.country || '',
      language: d.profile.language || '',
      category: d.profile.category || '',
      instagramHandle: d.profile.instagramHandle || '',
      contactEmail: d.profile.contactEmail || '',
      bio: d.profile.bio || ''
    };
  } catch (e) {
    console.error('Dashboard load failed', e);
    // Fallback: try profile endpoint directly
    try {
      const pRes = await api.get('/creator/profile');
      profile.value = pRes.data;
      editProfile.value = {
        country: pRes.data.country || '',
        language: pRes.data.language || '',
        category: pRes.data.category || '',
        instagramHandle: pRes.data.instagramHandle || '',
        contactEmail: pRes.data.contactEmail || '',
        bio: pRes.data.bio || ''
      };
    } catch (e2) {
      console.error('Profile fallback failed', e2);
    }
  } finally {
    loading.value = false;
  }
});

async function saveProfile() {
  savingProfile.value = true;
  profileSaved.value = false;
  try {
    const res = await api.put('/creator/profile', editProfile.value);
    profile.value = { ...profile.value, ...res.data };
    await trackFunnelEvent('profile_completion', {
      role: 'Creator',
      completionPercent: profileStrength.value,
    });
    profileSaved.value = true;
    setTimeout(() => (profileSaved.value = false), 3000);
  } catch (e) {
    console.error('Save profile failed', e);
  } finally {
    savingProfile.value = false;
  }
}

async function linkChannel() {
  linkError.value = '';
  linkSuccess.value = '';
  linkingChannel.value = true;
  try {
    const res = await api.post('/creator/link-channel', { channelUrl: channelUrl.value });
    channel.value = res.data;
    linkSuccess.value = `Channel "${res.data.channelName}" linked successfully! Stats will update in the background.`;
    channelUrl.value = '';
    // Reload recent videos
    const vRes = await api.get('/creator/dashboard');
    recentVideos.value = vRes.data.recentVideos || [];
  } catch (e) {
    linkError.value = e.userMessage || e.response?.data?.error || 'Failed to link channel. Check the URL and try again.';
  } finally {
    linkingChannel.value = false;
  }
}

async function loadCollabs() {
  if (collabs.value.length > 0) return;
  loadingCollabs.value = true;
  try {
    const res = await api.get('/creator/collaborations');
    collabs.value = res.data;
    pendingCollabs.value = res.data.filter(c => c.status === 'Pending').length;
  } catch (e) {
    console.error('Load collabs failed', e);
  } finally {
    loadingCollabs.value = false;
  }
}

async function acceptCollab(id) {
  try {
    const res = await api.patch(`/creator/collaborations/${id}/accept`);
    updateCollabStatus(id, res.data.status);
    pendingCollabs.value = Math.max(0, pendingCollabs.value - 1);
  } catch (e) {
    console.error('Accept failed', e);
  }
}

async function rejectCollab(id) {
  try {
    const res = await api.patch(`/creator/collaborations/${id}/reject`);
    updateCollabStatus(id, res.data.status);
    pendingCollabs.value = Math.max(0, pendingCollabs.value - 1);
  } catch (e) {
    console.error('Reject failed', e);
  }
}

async function openWorkflow(requestId) {
  loadingWorkflow.value = true;
  try {
    const res = await api.get(`/collaborations/${requestId}/workflow`);
    selectedWorkflow.value = res.data;
  } catch (e) {
    console.error('Workflow load failed', e);
  } finally {
    loadingWorkflow.value = false;
  }
}

async function updateMilestone(milestone, status) {
  try {
    await api.patch(`/collaborations/milestones/${milestone.collaborationMilestoneId}/status`, {
      status,
      deliverableUrl: milestone.deliverableUrl || null,
    });
    if (selectedWorkflow.value?.request?.requestId) {
      await openWorkflow(selectedWorkflow.value.request.requestId);
    }
  } catch (e) {
    console.error('Milestone update failed', e);
  }
}

function updateCollabStatus(id, status) {
  const c = collabs.value.find(x => x.requestId === id);
  if (c) c.status = status;
}

function statusBadge(status) {
  return { 'bg-warning text-dark': status === 'Pending', 'bg-success': status === 'Accepted', 'bg-danger': status === 'Rejected' };
}

function fmtNum(n) {
  if (!n && n !== 0) return '—';
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
  return n.toString();
}

function fmtDate(d) {
  if (!d) return '—';
  return new Date(d).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
}

</script>

<style scoped>
.creator-dashboard {
  --hero-bg: linear-gradient(120deg, rgba(15, 23, 42, 0.96), rgba(37, 99, 235, 0.9));
}

.creator-hero {
  background: var(--hero-bg);
  border-radius: 20px;
  color: #f8fafc;
  padding: 1.4rem;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  box-shadow: 0 12px 30px rgba(15, 23, 42, 0.22);
}

.eyebrow {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.22rem 0.65rem;
  font-size: 0.72rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(148, 163, 184, 0.2);
  color: #bfdbfe;
}

.hero-subtitle {
  color: rgba(226, 232, 240, 0.92);
}

.hero-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.status-pill {
  border-radius: 999px;
  font-size: 0.76rem;
  padding: 0.35rem 0.65rem;
  font-weight: 600;
}

.status-pill.linked {
  color: #065f46;
  background: rgba(110, 231, 183, 0.95);
}

.status-pill.pending {
  color: #7c2d12;
  background: rgba(253, 186, 116, 0.95);
}

.status-pill.soft {
  color: #f8fafc;
  background: rgba(100, 116, 139, 0.35);
  border: 1px solid rgba(148, 163, 184, 0.3);
}

.metric-card {
  border-radius: 16px;
  padding: 0.9rem;
  background: #ffffff;
  border: 1px solid rgba(148, 163, 184, 0.18);
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.06);
}

.metric-label {
  margin-bottom: 0.1rem;
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: #64748b;
}

.metric-value {
  margin-bottom: 0;
  font-weight: 700;
  font-size: 1.5rem;
}

.channel-alert {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  border-radius: 14px;
  border: 1px solid #bae6fd;
  background: #f0f9ff;
  color: #0c4a6e;
  padding: 0.7rem 0.85rem;
}

.tab-strip {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.tab-pill {
  border: 1px solid rgba(148, 163, 184, 0.3);
  background: #f8fafc;
  color: #0f172a;
  border-radius: 999px;
  padding: 0.45rem 0.85rem;
  font-weight: 600;
  font-size: 0.88rem;
}

.tab-pill.active {
  background: #0f172a;
  color: #fff;
  border-color: #0f172a;
}

.section-card {
  border-radius: 18px;
}

@media (max-width: 768px) {
  .creator-hero {
    flex-direction: column;
  }

  .hero-actions {
    justify-content: flex-start;
  }
}
</style>
