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
          <a
            href="mailto:partnerships@influencermatch.ai?subject=Creator%20Brand%20Partnership%20Inquiry"
            class="btn btn-sm btn-outline-light fw-semibold"
          >
            Contact Brands
          </a>
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

        <div class="card border-0 shadow-sm section-card mb-4 competitor-card">
          <div class="card-body p-4 d-flex flex-column flex-lg-row align-items-start align-items-lg-center justify-content-between gap-3">
            <div>
              <h5 class="fw-semibold mb-1">Competitor Search and Compare</h5>
              <p class="text-muted mb-0">
                Discover channels in your niche and compare performance signals to sharpen your content strategy.
              </p>
            </div>
            <router-link to="/youtube/search-intelligence" class="btn btn-outline-primary fw-semibold">
              Open Search Intelligence
            </router-link>
          </div>
        </div>

        <div class="tab-strip mb-4" id="creatorTabs">
          <button class="tab-pill" :class="{ active: tab === 'profile' }" @click="tab = 'profile'">Profile</button>
          <button class="tab-pill" :class="{ active: tab === 'channel' }" @click="tab = 'channel'">Channel</button>
          <button v-if="channel" class="tab-pill" :class="{ active: tab === 'videos' }" @click="tab = 'videos'">Recent Videos</button>
          <button class="tab-pill" :class="{ active: tab === 'insights' }" @click="tab = 'insights'; loadInsights()">Insights</button>
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

        <div v-if="channel" class="card border-0 shadow-sm section-card mt-3">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-1">YouTube Audience Demographics</h5>
            <p class="text-muted small mb-3">
              Connect once using Google OAuth, then ingest demographics without manually pasting access tokens.
            </p>
            <div class="row g-2 mb-2">
              <div class="col-lg-9">
                <input v-model.trim="oauthCode" class="form-control" placeholder="Paste Google authorization code after consent" />
              </div>
              <div class="col-lg-3 d-grid">
                <button class="btn btn-outline-primary" @click="openGoogleConsent" :disabled="connectingGoogle">
                  <span v-if="connectingGoogle" class="spinner-border spinner-border-sm me-1"></span>
                  Connect Google
                </button>
              </div>
            </div>
            <div class="d-flex gap-2 mb-2">
              <button class="btn btn-sm btn-outline-primary" @click="exchangeOAuthCode" :disabled="linkingGoogleCode || !oauthCode">Link Code</button>
            </div>
            <div class="row g-2 mb-2">
              <div class="col-lg-9">
                <input v-model.trim="demographicsToken" class="form-control" placeholder="Optional: manual access token override" />
              </div>
              <div class="col-lg-3 d-grid">
                <button class="btn btn-primary" @click="ingestDemographics" :disabled="ingestingDemographics || !demographicsToken">
                  <span v-if="ingestingDemographics" class="spinner-border spinner-border-sm me-1"></span>
                  Ingest with Token
                </button>
              </div>
            </div>
            <div class="d-flex gap-2 mb-2">
              <button class="btn btn-sm btn-primary" @click="ingestDemographicsWithoutToken" :disabled="ingestingDemographics">Ingest from Connected Account</button>
              <button class="btn btn-sm btn-outline-secondary" @click="loadDemographics" :disabled="loadingDemographics">Load Snapshot</button>
            </div>
            <div v-if="demographicsError" class="alert alert-warning py-2 small mb-2">{{ demographicsError }}</div>
            <div v-if="demographicsLoaded" class="alert alert-success py-2 small mb-2">Demographics snapshot updated.</div>
            <div v-if="demographics" class="row g-3">
              <div class="col-md-4">
                <div class="stat-box text-center">
                  <div class="stat-label">Top Country</div>
                  <div class="stat-val">{{ demographics.countryBreakdown?.[0]?.key || '—' }}</div>
                </div>
              </div>
              <div class="col-md-4">
                <div class="stat-box text-center">
                  <div class="stat-label">Top Age Group</div>
                  <div class="stat-val">{{ demographics.ageBreakdown?.[0]?.key || '—' }}</div>
                </div>
              </div>
              <div class="col-md-4">
                <div class="stat-box text-center">
                  <div class="stat-label">Top Gender</div>
                  <div class="stat-val">{{ demographics.genderBreakdown?.[0]?.key || '—' }}</div>
                </div>
              </div>
              <div class="col-12 small text-muted">
                Snapshot window: {{ fmtDate(demographics.windowStartDate) }} to {{ fmtDate(demographics.windowEndDate) }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── TAB: Recent Videos ─────────────────────────────────────────── -->
      <div v-if="tab === 'videos'">
        <!-- Bulk analysis bar -->
        <div class="d-flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
          <h6 class="fw-semibold mb-0">Your Recent Videos</h6>
          <button
            class="btn btn-success fw-semibold"
            @click="analyseAllVideos"
            :disabled="bulkAnalysisLoading || recentVideos.length === 0"
          >
            <span v-if="bulkAnalysisLoading" class="spinner-border spinner-border-sm me-1"></span>
            <span v-else>🤖</span>
            Analyse Recent {{ Math.min(recentVideos.length, 10) }} Videos with AI
          </button>
        </div>

        <div v-if="recentVideos.length === 0" class="text-center text-muted py-5">No videos fetched yet.</div>
        <div class="row g-3">
          <div v-for="v in recentVideos" :key="v.youtubeVideoId" class="col-md-6 col-lg-4">
            <div class="card border-0 shadow-sm h-100 section-card">
              <img v-if="v.thumbnailUrl" :src="v.thumbnailUrl" class="card-img-top" style="height:160px;object-fit:cover;" :alt="v.title" />
              <div class="card-body p-3 d-flex flex-column">
                <p class="fw-semibold small mb-2 lh-sm" style="display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;">{{ v.title }}</p>
                <div class="d-flex justify-content-between small text-muted">
                  <span>👁 {{ fmtNum(v.viewCount) }}</span>
                  <span>👍 {{ fmtNum(v.likeCount) }}</span>
                  <span>💬 {{ fmtNum(v.commentCount) }}</span>
                </div>
                <div class="small text-muted mt-1 mb-2">{{ fmtDate(v.publishedAt) }}</div>
                <!-- Per-video analyse button -->
                <div class="mt-auto">
                  <router-link
                    :to="`/creator/latest-video-analysis`"
                    class="btn btn-sm btn-outline-primary w-100"
                  >🔍 Analyse with AI</router-link>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Bulk AI Analysis Results ────────────────────────────────── -->
        <div v-if="bulkAnalysisResult" class="mt-4">
          <div class="card border-0 shadow-sm section-card">
            <div class="card-body p-4">
              <div class="d-flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
                <div>
                  <h5 class="fw-bold mb-1">🤖 AI Video Performance Report</h5>
                  <p class="text-muted small mb-0">
                    Videos ranked by composite engagement score · Channel avg: {{ fmtNum(bulkAnalysisResult.avgViews) }} views
                  </p>
                </div>
                <button class="btn btn-sm btn-outline-secondary" @click="bulkAnalysisResult = null">✕ Close</button>
              </div>

              <!-- Ranked video list -->
              <div class="d-flex flex-column gap-3 mb-4">
                <div
                  v-for="item in bulkAnalysisResult.ranked"
                  :key="item.youtubeVideoId"
                  class="ranked-video-card p-3 rounded-3 border"
                  :class="item.rank <= 3 ? 'border-success bg-success bg-opacity-10' : item.rank >= 8 ? 'border-danger bg-danger bg-opacity-10' : 'border-secondary'"
                >
                  <div class="d-flex gap-3 align-items-start">
                    <!-- Rank badge -->
                    <div class="rank-badge flex-shrink-0" :class="rankBadgeClass(item.rank)">
                      #{{ item.rank }}
                    </div>
                    <!-- Thumbnail -->
                    <img
                      v-if="item.thumbnailUrl"
                      :src="item.thumbnailUrl"
                      class="rounded-2 flex-shrink-0"
                      style="width:80px;height:52px;object-fit:cover;"
                      :alt="item.title"
                    />
                    <!-- Info -->
                    <div class="flex-grow-1 min-w-0">
                      <div class="d-flex align-items-start justify-content-between gap-2 flex-wrap mb-1">
                        <p class="fw-semibold small mb-0" style="line-height:1.3">{{ item.title }}</p>
                        <span class="badge flex-shrink-0" :class="performanceBadgeClass(item.performanceLabel)">
                          {{ item.performanceLabel }}
                        </span>
                      </div>
                      <div class="d-flex flex-wrap gap-3 small text-muted mb-2">
                        <span>👁 {{ fmtNum(item.viewCount) }}</span>
                        <span>👍 {{ fmtNum(item.likeCount) }}</span>
                        <span>💬 {{ fmtNum(item.commentCount) }}</span>
                        <span>📅 {{ item.daysAgo }}d ago</span>
                        <span>⚡ {{ item.engagementRate }}% ER</span>
                        <span class="text-primary fw-semibold">Score: {{ item.compositeScore }}</span>
                      </div>
                      <!-- LLM narrative -->
                      <div class="small p-2 rounded-2 bg-white border lh-sm">
                        <span class="text-muted me-1">🧠</span>{{ item.narrative }}
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Topic recommendations -->
              <div v-if="bulkAnalysisResult.topicAdvice && bulkAnalysisResult.topicAdvice.length" class="topic-advice-card p-3 rounded-3 border border-primary bg-primary bg-opacity-10">
                <h6 class="fw-bold mb-2 text-primary">🎯 Content Strategy Recommendations</h6>
                <p class="small text-muted mb-2">Based on your top 3 videos, here's what you should make next:</p>
                <ol class="mb-0 ps-3">
                  <li v-for="(line, i) in bulkAnalysisResult.topicAdvice" :key="i" class="small mb-1 lh-sm">{{ line }}</li>
                </ol>
              </div>
            </div>
          </div>
        </div>

        <!-- Bulk analysis error -->
        <div v-if="bulkAnalysisError" class="alert alert-warning mt-3">
          {{ bulkAnalysisError }}
        </div>
      </div>

      <!-- ── TAB: Insights ──────────────────────────────────────────────── -->
      <div v-if="tab === 'insights'" class="card border-0 shadow-sm section-card">
        <div class="card-body p-4">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h5 class="fw-semibold mb-0">Creator Health & Coaching Insights</h5>
            <button class="btn btn-sm btn-outline-secondary" @click="loadInsights">Refresh</button>
          </div>

          <div v-if="insightsLoading" class="text-center py-4">
            <div class="spinner-border text-primary"></div>
          </div>

          <div v-else-if="insightsError" class="alert alert-warning">{{ insightsError }}</div>

          <div v-else-if="insights">
            <div class="row g-2 mb-3">
              <div class="col-6 col-md-3">
                <article class="metric-card"><p class="metric-label">Health Score</p><p class="metric-value">{{ Number(insights.healthScorecard.compositeScore || 0).toFixed(1) }}</p></article>
              </div>
              <div class="col-6 col-md-3">
                <article class="metric-card"><p class="metric-label">7d Trend</p><p class="metric-value text-capitalize">{{ insights.healthScorecard.trend?.trend7d || 'flat' }}</p></article>
              </div>
              <div class="col-6 col-md-3">
                <article class="metric-card"><p class="metric-label">30d Trend</p><p class="metric-value text-capitalize">{{ insights.healthScorecard.trend?.trend30d || 'flat' }}</p></article>
              </div>
              <div class="col-6 col-md-3">
                <article class="metric-card"><p class="metric-label">Brand Safety</p><p class="metric-value">{{ Number(insights.healthScorecard.brandSafetyScore || 0).toFixed(1) }}</p></article>
              </div>
            </div>

            <div class="alert alert-info py-2 small mb-3">{{ insights.healthScorecard.whyExplanation }}</div>

            <h6 class="fw-semibold">Audience Quality & Authenticity</h6>
            <div class="row g-2 mb-3">
              <div class="col-md-3"><div class="stat-box text-center"><div class="stat-label">Suspicious Ratio</div><div class="stat-val">{{ (Number(insights.audienceQuality.suspiciousEngagementRatio || 0) * 100).toFixed(1) }}%</div></div></div>
              <div class="col-md-3"><div class="stat-box text-center"><div class="stat-label">LCV Consistency</div><div class="stat-val">{{ Number(insights.audienceQuality.likeCommentViewConsistencyScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-md-3"><div class="stat-box text-center"><div class="stat-label">Volatility</div><div class="stat-val">{{ Number(insights.audienceQuality.engagementVolatilityScore || 0).toFixed(1) }}</div></div></div>
              <div class="col-md-3"><div class="stat-box text-center"><div class="stat-label">Reused Pattern</div><div class="stat-val">{{ Number(insights.audienceQuality.reusedCommentPatternScore || 0).toFixed(1) }}</div></div></div>
            </div>

            <div class="small text-muted mb-3">{{ insights.audienceQuality.explanation }}</div>

            <h6 class="fw-semibold">Best Posting Window</h6>
            <p class="mb-2">{{ insights.coaching.bestPostingWindow || 'Not enough data yet' }}</p>

            <h6 class="fw-semibold">Content Format Performance</h6>
            <div class="table-responsive mb-3">
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>Format</th>
                    <th class="text-end">Avg Views</th>
                    <th class="text-end">Avg Engagement</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="f in insights.coaching.contentFormatPerformance || []" :key="f.format">
                    <td>{{ f.format }}</td>
                    <td class="text-end">{{ fmtNum(f.avgViews) }}</td>
                    <td class="text-end">{{ (Number(f.avgEngagementRate || 0) * 100).toFixed(2) }}%</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <h6 class="fw-semibold">Weekly Action List</h6>
            <ul class="mb-0">
              <li v-for="a in insights.coaching.weeklyActionList || []" :key="a" class="mb-1">{{ a }}</li>
            </ul>
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

                <div class="d-flex gap-2 flex-wrap mb-3">
                  <span class="badge border">Workflow Status: {{ selectedWorkflow.request.status }}</span>
                  <button
                    v-if="selectedWorkflow.request.status === 'ContractDrafted' || selectedWorkflow.request.status === 'ProposalSent'"
                    class="btn btn-outline-success btn-sm"
                    @click="signContract(selectedWorkflow.request.requestId)"
                  >
                    Sign Contract
                  </button>
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
import { useRoute } from 'vue-router';
import api from '../services/api';
import { authUserName } from '../services/auth';
import { trackFunnelEvent } from '../services/funnel';
import { engagementMeta } from '../utils/engagement';

const route = useRoute();

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
const demographicsToken = ref('');
const demographics = ref(null);
const ingestingDemographics = ref(false);
const loadingDemographics = ref(false);
const demographicsError = ref('');
const demographicsLoaded = ref(false);
const connectingGoogle = ref(false);
const linkingGoogleCode = ref(false);
const oauthCode = ref('');

// Videos
const recentVideos = ref([]);
const pendingCollabs = ref(0);

// Bulk AI video analysis
const bulkAnalysisLoading = ref(false);
const bulkAnalysisError   = ref('');
const bulkAnalysisResult  = ref(null);

async function analyseAllVideos() {
  if (bulkAnalysisLoading.value) return;
  bulkAnalysisLoading.value = true;
  bulkAnalysisError.value   = '';
  bulkAnalysisResult.value  = null;
  try {
    const { data } = await api.get('/creator/videos/ranked-insights');
    bulkAnalysisResult.value = data;
  } catch (e) {
    bulkAnalysisError.value = e.userMessage || e.response?.data?.error || 'AI analysis failed. Please try again.';
  } finally {
    bulkAnalysisLoading.value = false;
  }
}

function rankBadgeClass(rank) {
  if (rank === 1) return 'rank-gold';
  if (rank === 2) return 'rank-silver';
  if (rank === 3) return 'rank-bronze';
  if (rank >= 8)  return 'rank-poor';
  return 'rank-mid';
}

function performanceBadgeClass(label) {
  if (label === 'Top Performer') return 'bg-success';
  if (label === 'Needs Attention') return 'bg-danger';
  return 'bg-secondary';
}

// Collabs
const collabs = ref([]);
const loadingCollabs = ref(false);
const loadingWorkflow = ref(false);
const selectedWorkflow = ref(null);
const insights = ref(null);
const insightsLoading = ref(false);
const insightsError = ref('');

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
  const codeFromQuery = String(route.query.code || '').trim();
  if (codeFromQuery) {
    oauthCode.value = codeFromQuery;
  }

  try {
    // Load dashboard data from API
    const res = await api.get('/creator/dashboard');
    const d = res.data;
    profile.value = d.profile;
    channel.value = d.channel;
    recentVideos.value = d.recentVideos || [];
    pendingCollabs.value = d.pendingCollaborations || 0;
    await loadDemographics();
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
    await loadDemographics();
  } catch (e) {
    linkError.value = e.userMessage || e.response?.data?.error || 'Failed to link channel. Check the URL and try again.';
  } finally {
    linkingChannel.value = false;
  }
}

async function loadDemographics() {
  if (!channel.value) return;
  loadingDemographics.value = true;
  demographicsLoaded.value = false;
  demographicsError.value = '';
  try {
    const res = await api.get('/creator/audience-demographics');
    demographics.value = res.data;
  } catch (e) {
    demographics.value = null;
    const status = Number(e?.response?.status || 0);
    if (status !== 404) {
      demographicsError.value = e.userMessage || e.response?.data?.error || 'Unable to load demographics snapshot.';
    }
  } finally {
    loadingDemographics.value = false;
  }
}

async function ingestDemographics() {
  if (!demographicsToken.value) return;
  ingestingDemographics.value = true;
  demographicsLoaded.value = false;
  demographicsError.value = '';
  try {
    const res = await api.post('/creator/audience-demographics/ingest', {
      accessToken: demographicsToken.value
    });
    demographics.value = res.data;
    demographicsLoaded.value = true;
    demographicsToken.value = '';
  } catch (e) {
    demographicsError.value = e.userMessage || e.response?.data?.error || 'Demographics ingestion failed.';
  } finally {
    ingestingDemographics.value = false;
  }
}

async function ingestDemographicsWithoutToken() {
  ingestingDemographics.value = true;
  demographicsLoaded.value = false;
  demographicsError.value = '';
  try {
    const res = await api.post('/creator/audience-demographics/ingest', {});
    demographics.value = res.data;
    demographicsLoaded.value = true;
  } catch (e) {
    demographicsError.value = e.userMessage || e.response?.data?.error || 'Demographics ingestion failed.';
  } finally {
    ingestingDemographics.value = false;
  }
}

async function openGoogleConsent() {
  connectingGoogle.value = true;
  demographicsError.value = '';
  try {
    const redirectUri = `${window.location.origin}${window.location.pathname}`;
    const res = await api.post('/creator/audience-demographics/connect-url', { redirectUri });
    const url = res.data?.url;
    if (!url) throw new Error('No OAuth URL returned');
    window.open(url, '_blank', 'noopener,noreferrer');
  } catch (e) {
    demographicsError.value = e.userMessage || e.response?.data?.error || 'Unable to start Google OAuth consent.';
  } finally {
    connectingGoogle.value = false;
  }
}

async function exchangeOAuthCode() {
  if (!oauthCode.value) return;
  linkingGoogleCode.value = true;
  demographicsError.value = '';
  try {
    const redirectUri = `${window.location.origin}${window.location.pathname}`;
    await api.post('/creator/audience-demographics/exchange-code', {
      redirectUri,
      code: oauthCode.value
    });
    oauthCode.value = '';
    demographicsLoaded.value = true;
  } catch (e) {
    demographicsError.value = e.userMessage || e.response?.data?.error || 'Unable to link Google authorization code.';
  } finally {
    linkingGoogleCode.value = false;
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

async function loadInsights() {
  insightsLoading.value = true;
  insightsError.value = '';
  try {
    const res = await api.get('/creator/insights');
    insights.value = res.data;
  } catch (e) {
    insightsError.value = e?.userMessage || e?.response?.data?.error || 'Unable to load insights.';
  } finally {
    insightsLoading.value = false;
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

async function signContract(requestId) {
  const notes = window.prompt('Optional contract note', 'Creator reviewed and signed the contract.');
  if (notes === null) return;

  try {
    await api.post(`/collaborations/${requestId}/contract`, {
      notes: notes || 'Creator reviewed and signed the contract.'
    });

    updateCollabStatus(requestId, 'ContractSigned');
    await openWorkflow(requestId);
  } catch (e) {
    console.error('Contract signing failed', e);
  }
}

function updateCollabStatus(id, status) {
  const c = collabs.value.find(x => x.requestId === id);
  if (c) c.status = status;
}

function statusBadge(status) {
  return {
    'bg-warning text-dark': status === 'Pending' || status === 'ProposalSent',
    'bg-success': status === 'Accepted' || status === 'ContractSigned' || status === 'PaymentReleased' || status === 'Completed',
    'bg-info text-dark': status === 'ContractDrafted',
    'bg-danger': status === 'Rejected'
  };
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

.competitor-card {
  border: 1px solid rgba(13, 110, 253, 0.2);
  background: linear-gradient(135deg, rgba(13, 110, 253, 0.06), rgba(255, 255, 255, 0.96));
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

/* ── Bulk AI Video Analysis ─────────────────────────────────────── */
.ranked-video-card {
  transition: box-shadow 0.16s ease;
}
.ranked-video-card:hover {
  box-shadow: 0 4px 16px rgba(15, 23, 42, 0.08);
}

.rank-badge {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.78rem;
  font-weight: 700;
}
.rank-gold   { background: #fef9c3; color: #854d0e; border: 2px solid #fbbf24; }
.rank-silver { background: #f1f5f9; color: #1e293b; border: 2px solid #94a3b8; }
.rank-bronze { background: #fff7ed; color: #92400e; border: 2px solid #fb923c; }
.rank-mid    { background: #f8fafc; color: #475569; border: 2px solid #e2e8f0; }
.rank-poor   { background: #fff1f2; color: #9f1239; border: 2px solid #fda4af; }

.topic-advice-card { border-radius: 14px; }

@media (max-width: 768px) {
  .creator-hero {
    flex-direction: column;
  }

  .hero-actions {
    justify-content: flex-start;
  }
}
</style>
