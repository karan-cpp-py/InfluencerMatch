<template>
  <div class="home-wrap">

    <!-- ══════════════════════════ AUTHENTICATED ══════════════════════════ -->
    <template v-if="token">
      <!-- Feature Toolkit -->
      <section class="container py-5">
        <div class="mb-2">
          <h4 class="fw-bold mb-0">Your Feature Toolkit</h4>
          <p class="text-muted small mt-1 mb-0">
            Everything built for your <strong>{{ role }}</strong> account — click any card to jump in.
          </p>
        </div>

        <div class="quick-launch mb-4 d-flex flex-wrap gap-2">
          <router-link :to="primaryCta.to" class="quick-pill quick-pill-primary">
            {{ primaryCta.label }}
          </router-link>
          <router-link v-for="q in quickLinks" :key="q.to" :to="q.to" class="quick-pill">
            {{ q.icon }} {{ q.label }}
          </router-link>
        </div>

        <template v-for="(group, gi) in featureGroups" :key="group.label">
          <div class="group-header mt-5 mb-3">
            <span class="group-orb" :style="`background:${group.color};box-shadow:0 0 12px ${group.color}88`"></span>
            <span class="group-title">{{ group.label }}</span>
            <span class="group-subtitle ms-2">— {{ group.subtitle }}</span>
          </div>
          <div class="row g-3">
            <div
              v-for="(card, ci) in group.cards"
              :key="`${gi}-${ci}`"
              class="col-sm-6 col-lg-4"
              :style="`--ci:${gi * 8 + ci}`"
            >
              <router-link
                :to="card.to"
                class="feature-tile h-100 d-flex flex-column text-decoration-none"
                :style="`--tc:${group.color};--tca:${group.colorAlpha}`"
              >
                <div class="tile-icon-wrap">
                  <span class="tile-icon-inner">{{ card.icon }}</span>
                </div>
                <span class="tile-tag">{{ card.tag }}</span>
                <h6 class="tile-title fw-bold mt-2 mb-1">{{ card.title }}</h6>
                <p class="tile-desc flex-grow-1">{{ card.desc }}</p>
                <div class="tile-cta pt-3">Open <span class="cta-arrow">→</span></div>
              </router-link>
            </div>
          </div>
        </template>
      </section>

    </template>

    <!-- ══════════════════════════════ GUEST ═════════════════════════════ -->
    <template v-else>

      <!-- Marketing Hero -->
      <header class="home-hero py-5 mb-4">
        <div class="container position-relative">
          <div class="row align-items-center g-4">
            <div class="col-lg-7 text-white">
              <div class="mb-2">
                <PhaseBadge :phase="platformConfig.phase" />
              </div>
              <h1 class="display-4 fw-bold mb-3 hero-title">Creator Intelligence That Feels Like A Modern Social Platform</h1>
              <p class="lead mb-4 opacity-75 hero-subtitle">
                Track growth, decode audience intent, and activate brand-ready creators with live analytics that look and feel premium.
              </p>
              <div class="d-flex flex-wrap gap-2 align-items-center">
                <router-link class="btn btn-light px-4" to="/register">Get Started Free</router-link>
                <router-link class="btn btn-outline-light px-4" to="/creators/leaderboard">Explore Creator Graph</router-link>
                <span class="hero-pill">Live AI Signals</span>
              </div>
            </div>
            <div class="col-lg-5">
              <div class="hero-stat-card card border-0 p-3">
                <div class="d-flex justify-content-between mb-2 small text-muted">
                  <span>Current Phase</span>
                  <span>{{ platformConfig.phase }}</span>
                </div>
                <div class="h3 fw-bold mb-3 gradient-title">{{ platformConfig.positioningLine }}</div>
                <div class="row g-2">
                  <div class="col-6">
                    <div class="mini-stat p-2 rounded-3">
                      <div class="small text-muted">Active Creators / Week</div>
                      <div class="fw-bold">{{ platformConfig.kpiGates.activeCreatorsWeekly }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="mini-stat p-2 rounded-3">
                      <div class="small text-muted">Brand Activation Target</div>
                      <div class="fw-bold">{{ platformConfig.kpiGates.brandActivationCreatorThreshold }}</div>
                    </div>
                  </div>
                </div>
                <div class="hero-sparkline mt-3"></div>
              </div>
            </div>
          </div>
        </div>
      </header>

      <!-- Role Preview Section -->
      <section class="container pb-5">
        <div class="text-center mb-5">
          <span class="section-eyebrow">Role Showcase</span>
          <h3 class="fw-bold mt-2 mb-2">Built for every participant in the creator economy</h3>
          <p class="text-muted mb-0">Select your role to preview exactly what the platform unlocks for you.</p>
        </div>

        <!-- Role Tabs -->
        <div
          class="role-tabs d-flex justify-content-center flex-wrap gap-2 mb-3"
          @mouseenter="pauseGuestRotation"
          @mouseleave="resumeGuestRotation"
        >
          <button
            v-for="t in guestTabs"
            :key="t.key"
            class="role-tab-btn"
            :class="{ active: activeGuestTab === t.key }"
            :style="`--tc:${t.color}`"
            @click="activeGuestTab = t.key"
          >{{ t.icon }} {{ t.label }}</button>
        </div>

        <div class="d-flex justify-content-center align-items-center gap-2 mb-5">
          <button class="btn btn-sm btn-outline-secondary" @click="prevGuestTab">Prev</button>
          <button class="btn btn-sm" :class="guestAutoplay ? 'btn-dark' : 'btn-outline-dark'" @click="toggleGuestAutoplay">
            {{ guestAutoplay ? 'Autoplay On' : 'Autoplay Off' }}
          </button>
          <button class="btn btn-sm btn-outline-secondary" @click="nextGuestTab">Next</button>
          <span class="small text-muted" v-if="guestAutoplay">Next in {{ guestCountdown }}s</span>
        </div>

        <!-- Role Cards -->
        <transition name="tab-fade" mode="out-in">
          <div :key="activeGuestTab">
            <div class="row g-3 mb-4">
              <div
                v-for="(card, ci) in activeGuestCards"
                :key="card.title"
                class="col-sm-6 col-lg-4"
                :style="`--ci:${ci};--tc:${activeGuestColor};--tca:${activeGuestColorAlpha}`"
              >
                <div class="guest-tile h-100">
                  <div class="guest-icon mb-3">{{ card.icon }}</div>
                  <h6 class="fw-bold mb-1">{{ card.title }}</h6>
                  <p class="small text-muted mb-0">{{ card.desc }}</p>
                </div>
              </div>
            </div>
            <div class="text-center pt-2">
              <router-link class="btn btn-primary px-5 fw-semibold" to="/register">
                Sign Up as {{ activeGuestLabel }} →
              </router-link>
              <router-link class="btn btn-outline-secondary ms-2 px-4" to="/login">Already have an account</router-link>
            </div>
          </div>
        </transition>

        <!-- Why Switch + CTA panels -->
        <div class="row g-3 mt-5">
          <div class="col-lg-8">
            <div class="panel-card p-4 h-100">
              <div class="small text-uppercase text-muted fw-semibold mb-2">Why Teams Switch</div>
              <h4 class="fw-bold mb-2">From static dashboards to narrative intelligence</h4>
              <p class="text-muted mb-0">
                Build creator pools, evaluate campaign fitness, and act on audience signals in one continuous workflow.
                No more fragmented spreadsheets, stale snapshots, or blind outreach.
              </p>
            </div>
          </div>
          <div class="col-lg-4">
            <div class="panel-card p-4 h-100 cta-panel text-white">
              <div class="small text-uppercase fw-semibold opacity-75">Ready?</div>
              <div class="h5 fw-bold mt-2">Join and explore your first creator shortlist today.</div>
              <div class="d-flex gap-2 mt-3">
                <router-link class="btn btn-light btn-sm" to="/register">Register</router-link>
                <router-link class="btn btn-outline-light btn-sm" to="/login">Login</router-link>
              </div>
            </div>
          </div>
        </div>
      </section>

    </template>
  </div>
</template>

<script setup>
import { computed, ref, onMounted, onBeforeUnmount } from 'vue';
import { authToken, authRole, authUserName } from '../services/auth';
import { platformConfig } from '../services/platform';
import PhaseBadge from '../components/PhaseBadge.vue';

const token = computed(() => authToken.value);
const role = computed(() => authRole.value);
const displayName = computed(() => authUserName.value || 'there');

// ── Role presentation ──────────────────────────────────────────────────────
const roleClass = computed(() => ({
  Creator: 'creator', Brand: 'brand', Agency: 'agency',
  Individual: 'individual', CreatorManager: 'creatormanager', SuperAdmin: 'superadmin',
})[role.value] || 'default');

const roleEmoji = computed(() => ({
  Creator: '🎬', Brand: '🏷️', Agency: '🏢',
  Individual: '👤', CreatorManager: '🎯', SuperAdmin: '⚙️',
})[role.value] || '👋');

const roleLabel = computed(() => ({
  Creator: 'Creator Account', Brand: 'Brand Account', Agency: 'Agency Account',
  Individual: 'Individual Account', CreatorManager: 'Creator Manager', SuperAdmin: 'Super Admin',
})[role.value] || role.value);

const primaryCta = computed(() => {
  const r = role.value;
  if (r === 'Creator')  return { label: 'Go to Dashboard', to: '/creator-dashboard' };
  if (r === 'Brand' || r === 'Agency')
    return platformConfig.features?.enableBrandActivation
      ? { label: 'Create Campaign', to: '/brand' }
      : { label: 'Join Brand Waitlist', to: '/brand/waitlist' };
  if (r === 'Individual' || r === 'CreatorManager') return { label: 'Search Creators', to: '/brand/youtube-creators?tab=search' };
  if (r === 'SuperAdmin') return { label: 'Open Admin Panel', to: '/admin' };
  return { label: 'Explore', to: '/creators/leaderboard' };
});

const quickLinks = computed(() => {
  const r = role.value;
  if (r === 'Creator') return [
    { icon: '📊', label: 'Dashboard',        to: '/creator-dashboard' },
    { icon: '🎬', label: 'Trending + AI Analysis',   to: '/videos/trending' },
    { icon: '🔍', label: 'Competitor Search',to: '/youtube/search-intelligence' },
  ];
  if (r === 'Brand' || r === 'Agency') return [
    ...(platformConfig.features?.enableBrandActivation ? [{ icon: '📋', label: 'Campaigns', to: '/brand/campaigns' }] : []),
    { icon: '🎯', label: 'Search Intelligence', to: '/youtube/search-intelligence' },
    { icon: '🔎', label: 'Creator Search',      to: '/brand/youtube-creators?tab=search' },
    { icon: '👥', label: 'Team Workspace',       to: '/workspace/team' },
  ];
  if (r === 'Individual' || r === 'CreatorManager') return [
    { icon: '🎯', label: 'Search Intelligence', to: '/youtube/search-intelligence' },
    { icon: '🔎', label: 'Creator Search',      to: '/brand/youtube-creators?tab=search' },
    { icon: '🚀', label: 'Rising Creators',     to: '/creators/rising' },
  ];
  if (r === 'SuperAdmin') return [{ icon: '⚙️', label: 'Admin Panel', to: '/admin' }];
  return [];
});

// ── Feature groups per role ────────────────────────────────────────────────
const featureGroups = computed(() => {
  const r = role.value;

  // ── Creator ──
  if (r === 'Creator') return [
    {
      label: 'Channel Management', subtitle: 'Run your creator profile and growth metrics',
      color: '#0f766e', colorAlpha: 'rgba(15,118,110,0.09)',
      cards: [
        { icon: '🎯', title: 'Creator Dashboard', tag: 'Home Base',
          desc: 'Your central command — manage your profile, linked YouTube channel, engagement stats, and collaboration pipeline in one view.',
          to: '/creator-dashboard' },
        { icon: '🚀', title: 'Creator Onboarding', tag: 'Getting Started',
          desc: 'Complete your creator profile step-by-step to maximise brand visibility and unlock marketplace discovery.',
          to: '/creator/onboarding' },
        { icon: '📺', title: 'YouTube Audience Demographics', tag: 'Demographics',
          desc: 'Connect your Google account to ingest real audience demographics — country, age, and gender breakdowns direct from YouTube Analytics.',
          to: '/creator-dashboard' },
      ],
    },
    {
      label: 'AI Video Analysis', subtitle: 'Understand your content through AI',
      color: '#7c3aed', colorAlpha: 'rgba(124,58,237,0.09)',
      cards: [
        { icon: '🎬', title: 'Latest Video AI Analysis', tag: 'AI Powered',
          desc: 'Instant AI breakdown of your most recent YouTube upload: sentiment, pacing, engagement drivers, and actionable improvement tips.',
          to: '/videos/trending' },
        { icon: '📊', title: 'Channel Health Scorecard', tag: 'Analytics',
          desc: 'Composite health score, audience quality indicators, suspicious engagement ratio, volatility score, and your optimal posting windows.',
          to: '/creator-dashboard' },
      ],
    },
    {
      label: 'Competitive Intelligence', subtitle: 'Know your niche inside out',
      color: '#2563eb', colorAlpha: 'rgba(37,99,235,0.09)',
      cards: [
        { icon: '🔍', title: 'Competitor Search & Compare', tag: 'Live Search',
          desc: 'Search YouTube live via the Data API, find channels in your niche, and compare growth trajectories, engagement, and content strategy side-by-side.',
          to: '/youtube/search-intelligence' },
      ],
    },
    {
      label: 'Brand Collaborations', subtitle: 'Monetise your audience through partnerships',
      color: '#ea580c', colorAlpha: 'rgba(234,88,12,0.09)',
      cards: [
        { icon: '🤝', title: 'Collaboration Requests', tag: 'Partnerships',
          desc: 'View and manage incoming brand partnership requests. Accept, negotiate, or decline — all from your dashboard collaborations tab.',
          to: '/creator-dashboard' },
      ],
    },
  ];

  // ── Brand / Agency ──
  if (r === 'Brand' || r === 'Agency') {
    const groups = [];
    const hasBrand = platformConfig.features?.enableBrandActivation;
    const hasMarket = platformConfig.features?.enableMarketplace;

    // Campaigns
    const campCards = hasBrand
      ? [
          { icon: '✏️', title: 'Create Campaign', tag: 'New Campaign',
            desc: 'Launch a new influencer marketing campaign — define objectives, target audience, creator requirements, budget, and timeline.',
            to: '/brand' },
          { icon: '📋', title: 'My Campaigns', tag: 'Management',
            desc: 'Monitor all active and past campaigns, review creator applications, and track budget utilisation and overall reach performance.',
            to: '/brand/campaigns' },
          { icon: '🧭', title: 'Campaign Wizard', tag: 'Guided Setup',
            desc: 'Step-by-step guided wizard to set up a well-structured influencer campaign from creative brief to creator shortlist.',
            to: '/brand/campaign-onboarding' },
        ]
      : [
          { icon: '📋', title: 'Brand Activation Waitlist', tag: 'Coming Soon',
            desc: 'Campaign features unlock automatically when creator-side KPI thresholds are reached. Join the waitlist for priority early access.',
            to: '/brand/waitlist' },
        ];
    groups.push({ label: 'Campaign Management', subtitle: 'Plan, launch, and track influencer campaigns',
      color: '#2563eb', colorAlpha: 'rgba(37,99,235,0.09)', cards: campCards });

    // Discovery
    groups.push({
      label: 'Creator Discovery', subtitle: 'Find and shortlist the right creators',
      color: '#0f766e', colorAlpha: 'rgba(15,118,110,0.09)',
      cards: [
        { icon: '🎯', title: 'YouTube Search Intelligence', tag: 'AI + Live API',
          desc: 'Search YouTube live, rank results with AI scoring, bulk-analyze channels, compare up to 4 creators, and save shortlists to your workspace.',
          to: '/youtube/search-intelligence' },
        { icon: '📺', title: 'YouTube Catalogue', tag: 'Catalogue',
          desc: 'Browse registered creator catalogue and advanced search in one unified command center.',
          to: '/brand/youtube-creators' },
        { icon: '🚀', title: 'Rising Creators', tag: 'Momentum',
          desc: 'Spot fast-growing channels before they peak — ranked by subscriber velocity and momentum score so you can move first.',
          to: '/creators/rising' },
        { icon: '🔥', title: 'Trending Videos', tag: 'Content Intel',
          desc: 'See which video formats and topics are currently gaining the most traction — use this to write sharper creator briefs.',
          to: '/videos/trending' },
      ],
    });

    // Analytics
    if (hasBrand) {
      groups.push({
        label: 'Analytics & Intelligence', subtitle: 'Deep AI analysis and brand performance tracking',
        color: '#7c3aed', colorAlpha: 'rgba(124,58,237,0.09)',
        cards: [
          { icon: '🧠', title: 'Creator Intelligence', tag: 'AI Reports',
            desc: 'Deep AI intelligence reports on any creator: brand fit score, audience quality rating, content safety classification, and collaboration readiness signal.',
            to: '/brand/creator-intelligence' },
          { icon: '📊', title: 'Brand Analytics', tag: 'Performance',
            desc: 'Analyse your brand mention trends, campaign sentiment distribution, creator reach metrics, and ROI signals over rolling time windows.',
            to: '/brand/analytics' },
        ],
      });
    }

    // Operations
    const opsCards = [];
    if (hasMarket) opsCards.push({ icon: '🏬', title: 'Marketplace', tag: 'Marketplace',
      desc: 'Browse creator packages and direct collaboration offers — pricing tiers, deliverable formats, and past work examples in one place.',
      to: '/marketplace' });
    opsCards.push(
      { icon: '👥', title: 'Team Workspace', tag: 'Team',
        desc: 'Collaborate with your entire team on creator shortlists, campaign reviews, and action logs in a shared workspace with full audit trail.',
        to: '/workspace/team' },
      { icon: '💳', title: 'Subscription', tag: 'Account',
        desc: 'Manage your plan, monitor API quota usage, handle billing, and upgrade to unlock higher limits and advanced analytics.',
        to: '/subscriptions' },
    );
    groups.push({ label: 'Operations', subtitle: 'Manage your team, marketplace, and account',
      color: '#ea580c', colorAlpha: 'rgba(234,88,12,0.09)', cards: opsCards });

    return groups;
  }

  // ── Individual / CreatorManager ──
  if (r === 'Individual' || r === 'CreatorManager') return [
    {
      label: 'Discovery Tools', subtitle: 'Find and evaluate creators across YouTube',
      color: '#ea580c', colorAlpha: 'rgba(234,88,12,0.09)',
      cards: [
        { icon: '🎯', title: 'YouTube Search Intelligence', tag: 'AI + Live',
          desc: 'Search YouTube live via the Data API, rank channels with AI scoring, and compare up to 4 creators side-by-side in a single view.',
          to: '/youtube/search-intelligence' },
        { icon: '📺', title: 'YouTube Catalogue', tag: 'Catalogue',
          desc: 'Use one unified page for YouTube catalogue intelligence plus advanced creator search filters.',
          to: '/brand/youtube-creators' },
        { icon: '🚀', title: 'Rising Creators', tag: 'Trending',
          desc: 'Monitor fast-growing channels ranked by subscriber velocity and momentum score — spot breakout creators early.',
          to: '/creators/rising' },
        { icon: '🔥', title: 'Trending Videos', tag: 'Content',
          desc: "Stay ahead of content trends — see which video formats and topics are gaining traction right now.",
          to: '/videos/trending' },
        { icon: '🏆', title: 'Creator Leaderboard', tag: 'Rankings',
          desc: 'Ranked overview of top-scoring creators — a quick snapshot of the highest-performing channels on the platform.',
          to: '/creators/leaderboard' },
      ],
    },
    {
      label: 'Account', subtitle: 'Manage your plan and preferences',
      color: '#2563eb', colorAlpha: 'rgba(37,99,235,0.09)',
      cards: [
        { icon: '💳', title: 'Subscription', tag: 'Billing',
          desc: 'View and manage your current plan, API quota consumption, usage limits, and billing details.',
          to: '/subscriptions' },
      ],
    },
  ];

  // ── SuperAdmin ──
  if (r === 'SuperAdmin') return [
    {
      label: 'Platform Administration', subtitle: 'Full platform control and oversight',
      color: '#dc2626', colorAlpha: 'rgba(220,38,38,0.09)',
      cards: [
        { icon: '⚙️', title: 'Admin Panel', tag: 'Full Control',
          desc: 'Complete platform management: user administration, subscription oversight, creator database controls, plan configuration, and system settings.',
          to: '/admin' },
      ],
    },
  ];

  return [];
});

// ── Guest role-preview tabs ────────────────────────────────────────────────
const activeGuestTab = ref('brand');

const guestTabs = [
  { key: 'brand',      label: 'For Brands',      icon: '🏷️', color: '#2563eb', colorAlpha: 'rgba(37,99,235,0.09)' },
  { key: 'creator',    label: 'For Creators',     icon: '🎬', color: '#0f766e', colorAlpha: 'rgba(15,118,110,0.09)' },
  { key: 'agency',     label: 'For Agencies',     icon: '🏢', color: '#7c3aed', colorAlpha: 'rgba(124,58,237,0.09)' },
  { key: 'individual', label: 'For Individuals',  icon: '👤', color: '#ea580c', colorAlpha: 'rgba(234,88,12,0.09)' },
];

const guestPreviewData = {
  brand: [
    { icon: '✏️', title: 'Campaign Management',
      desc: 'Create, configure, and track influencer campaigns with full lifecycle management — from brief to post-campaign reporting.' },
    { icon: '🎯', title: 'YouTube Search Intelligence',
      desc: 'Search YouTube live with AI ranking, bulk-analyze channels, save shortlists to your workspace, and compare creators head-to-head.' },
    { icon: '🔎', title: 'Creator Search & Filtering',
      desc: 'Filter creators by niche, engagement rate, subscriber count, language, and country from a continuously updated database.' },
    { icon: '🧠', title: 'Creator Intelligence Reports',
      desc: 'Deep AI analysis: brand fit score, audience quality rating, content safety classification, and collaboration readiness signal.' },
    { icon: '📊', title: 'Brand Analytics',
      desc: 'Monitor brand mention trends, campaign sentiment distribution, creator reach, and ROI signals over time.' },
    { icon: '👥', title: 'Team Workspace',
      desc: 'Shared space for your team — shortlist creators, leave comments, assign tasks, and track campaign scope all in one place.' },
  ],
  creator: [
    { icon: '🎯', title: 'Creator Dashboard',
      desc: 'All-in-one command center: channel stats, pending collaborations, growth trend, and profile completeness at a glance.' },
    { icon: '🎬', title: 'AI Video Analysis',
      desc: 'Instant AI breakdown of your latest YouTube upload: sentiment, engagement drivers, pacing score, and improvement tips.' },
    { icon: '📊', title: 'Channel Health Insights',
      desc: 'Composite health scorecard, audience quality indicators, suspicious engagement detection, and optimal posting windows.' },
    { icon: '🔍', title: 'Competitor Intelligence',
      desc: 'Search YouTube live, find channels in your niche, and compare their metrics against yours in a side-by-side view.' },
    { icon: '🤝', title: 'Brand Collaboration Requests',
      desc: 'Receive and manage brand partnership requests directly on the platform — no cold outreach, brands come to you.' },
    { icon: '🚀', title: 'Marketplace Discovery',
      desc: 'Get featured in brand discovery flows when your profile is complete — powered by your engagement score and creator tier.' },
  ],
  agency: [
    { icon: '🧭', title: 'Campaign Wizard',
      desc: 'Guided multi-step campaign builder: objectives, creator profile targeting, deliverables, timeline, and shortlist creation.' },
    { icon: '🎯', title: 'Search Intelligence',
      desc: 'Live YouTube search with AI scoring, multi-creator comparison drawer, bulk analysis queues, and workspace shortlist saving.' },
    { icon: '🏬', title: 'Marketplace Access',
      desc: "Browse structured creator packages — pricing tiers, deliverable formats, and past content examples from active creators." },
    { icon: '👥', title: 'Team Workspace',
      desc: 'Multi-user space — assign tasks, share shortlists, add notes, all scoped to your campaign with a full audit trail.' },
    { icon: '💡', title: 'AI Opportunity Matching',
      desc: "AI-surfaced partnership recommendations matched to your clients' objectives and historical performance data." },
    { icon: '📋', title: 'Multi-Campaign View',
      desc: "Manage all client campaigns in one dashboard — creator statuses, budget pacing, and deployment timelines at once." },
  ],
  individual: [
    { icon: '🎯', title: 'YouTube Search Intelligence',
      desc: 'Search YouTube live with AI ranking, compare up to 4 channels side-by-side, and export shortlists for off-platform analysis.' },
    { icon: '🔎', title: 'Creator Database Search',
      desc: 'Full-text and filter search across all indexed creators — find the right fit by niche, audience size, and engagement.' },
    { icon: '🚀', title: 'Rising Creators',
      desc: "Spot high-momentum channels before they peak — ranked by subscriber velocity so you're always one step ahead." },
    { icon: '🔥', title: 'Trending Videos',
      desc: 'See which video formats and topics are currently picking up momentum across the indexed creator pool.' },
    { icon: '🏆', title: 'Creator Leaderboard',
      desc: 'Ranked leaderboard of top-scoring creators — quick inspiration and benchmarking for any research task.' },
    { icon: '📊', title: 'Creator Analytics',
      desc: "Deep-dive into any creator's page: audience breakdown, engagement history, scoring, and video performance." },
  ],
};

const activeGuestCards = computed(() => guestPreviewData[activeGuestTab.value] || []);
const activeGuestColor = computed(() => guestTabs.find(t => t.key === activeGuestTab.value)?.color || '#2563eb');
const activeGuestColorAlpha = computed(() => guestTabs.find(t => t.key === activeGuestTab.value)?.colorAlpha || 'rgba(37,99,235,0.09)');
const activeGuestLabel = computed(() => (guestTabs.find(t => t.key === activeGuestTab.value)?.label || 'Brand').replace('For ', ''));

const guestAutoplay = ref(true);
const guestCountdown = ref(6);
let guestInterval = null;

function nextGuestTab() {
  const idx = guestTabs.findIndex((t) => t.key === activeGuestTab.value);
  const next = (idx + 1) % guestTabs.length;
  activeGuestTab.value = guestTabs[next].key;
  guestCountdown.value = 6;
}

function prevGuestTab() {
  const idx = guestTabs.findIndex((t) => t.key === activeGuestTab.value);
  const prev = (idx - 1 + guestTabs.length) % guestTabs.length;
  activeGuestTab.value = guestTabs[prev].key;
  guestCountdown.value = 6;
}

function startGuestRotation() {
  stopGuestRotation();
  if (!guestAutoplay.value) return;
  guestInterval = window.setInterval(() => {
    guestCountdown.value -= 1;
    if (guestCountdown.value <= 0) {
      nextGuestTab();
    }
  }, 1000);
}

function stopGuestRotation() {
  if (guestInterval) {
    window.clearInterval(guestInterval);
    guestInterval = null;
  }
}

function pauseGuestRotation() {
  stopGuestRotation();
}

function resumeGuestRotation() {
  startGuestRotation();
}

function toggleGuestAutoplay() {
  guestAutoplay.value = !guestAutoplay.value;
  guestCountdown.value = 6;
  startGuestRotation();
}

onMounted(() => {
  startGuestRotation();
});

onBeforeUnmount(() => {
  stopGuestRotation();
});
</script>

<style scoped>
/* ── Core ─────────────────────────────────────────────────────────────── */
.home-wrap { min-height: calc(100vh - 130px); }

/* ── Keyframes ────────────────────────────────────────────────────────── */
@keyframes fadeSlideUp {
  from { opacity: 0; transform: translateY(22px); }
  to   { opacity: 1; transform: translateY(0); }
}
@keyframes pulseOrb {
  0%, 100% { transform: scale(1); opacity: 0.9; }
  50%       { transform: scale(1.35); opacity: 0.6; }
}

/* ── Auth Hero ────────────────────────────────────────────────────────── */
.auth-hero {
  border-radius: 0 0 28px 28px;
  overflow: hidden;
}
.hero-creator       { background: linear-gradient(130deg, #042f2e 0%, #0f766e 55%, #111827); }
.hero-brand         { background: linear-gradient(130deg, #1e3a8a 0%, #2563eb 50%, #111827); }
.hero-agency        { background: linear-gradient(130deg, #4c1d95 0%, #7c3aed 50%, #111827); }
.hero-individual    { background: linear-gradient(130deg, #7c2d12 0%, #ea580c 52%, #111827); }
.hero-creatormanager{ background: linear-gradient(130deg, #831843 0%, #db2777 50%, #111827); }
.hero-superadmin    { background: linear-gradient(130deg, #7f1d1d 0%, #dc2626 50%, #111827); }
.hero-default       { background: linear-gradient(130deg, #1e293b, #0f172a); }

.auth-avatar {
  width: 56px; height: 56px; border-radius: 50%; flex-shrink: 0;
  background: rgba(255,255,255,0.18);
  border: 2px solid rgba(255,255,255,0.32);
  display: flex; align-items: center; justify-content: center;
  font-size: 1.55rem; backdrop-filter: blur(8px);
}

.role-chip {
  padding: 0.17rem 0.58rem; border-radius: 999px;
  font-size: 0.68rem; font-weight: 700; letter-spacing: 0.07em; text-transform: uppercase;
  background: rgba(255,255,255,0.18); color: #fff;
  border: 1px solid rgba(255,255,255,0.3);
}

/* ── Quick-launch bar ─────────────────────────────────────────────────── */
.quick-pill {
  display: inline-flex; align-items: center; gap: 0.3rem;
  padding: 0.28rem 0.82rem; border-radius: 999px;
  background: #ffffff; border: 1px solid rgba(148,163,184,0.38);
  color: #0f172a; font-size: 0.82rem; font-weight: 600;
  text-decoration: none;
  transition: background 0.18s ease, transform 0.18s ease;
}
.quick-pill:hover { background: #f8fafc; transform: translateY(-2px); color: #0f172a; }

.quick-pill-primary {
  color: #ffffff;
  background: linear-gradient(120deg, #0ea5e9, #2563eb);
  border-color: #2563eb;
}

.quick-pill-primary:hover {
  color: #ffffff;
  background: linear-gradient(120deg, #0284c7, #1d4ed8);
}

/* ── Group header ─────────────────────────────────────────────────────── */
.group-header { display: flex; align-items: center; gap: 0.55rem; }
.group-orb {
  width: 11px; height: 11px; border-radius: 50%; flex-shrink: 0;
  animation: pulseOrb 3s ease-in-out infinite;
}
.group-title { font-weight: 700; font-size: 1.05rem; color: #0f172a; }
.group-subtitle { font-size: 0.82rem; color: #64748b; }

/* ── Feature tiles ────────────────────────────────────────────────────── */
.feature-tile {
  background: #fff;
  border: 1px solid rgba(148,163,184,0.22);
  border-radius: 20px; padding: 1.25rem;
  position: relative; overflow: hidden;
  transition: transform 0.22s ease, box-shadow 0.22s ease, border-color 0.22s ease;
  animation: fadeSlideUp 0.45s ease both;
  animation-delay: calc(var(--ci, 0) * 55ms);
}
.feature-tile::before {
  content: ''; position: absolute; inset: 0;
  background: linear-gradient(160deg, var(--tca, rgba(0,0,0,0.04)), transparent 55%);
  pointer-events: none; opacity: 0; transition: opacity 0.22s;
}
.feature-tile:hover {
  transform: translateY(-5px);
  box-shadow: 0 14px 38px var(--tca, rgba(0,0,0,0.1)), 0 2px 8px rgba(15,23,42,0.05);
  border-color: var(--tc, #94a3b8);
}
.feature-tile:hover::before { opacity: 1; }

.tile-icon-wrap {
  width: 46px; height: 46px; border-radius: 13px;
  background: var(--tca, rgba(100,116,139,0.1));
  display: flex; align-items: center; justify-content: center;
  margin-bottom: 0.75rem;
  transition: transform 0.22s;
}
.feature-tile:hover .tile-icon-wrap { transform: scale(1.1) rotate(-3deg); }
.tile-icon-inner { font-size: 1.35rem; line-height: 1; }

.tile-tag {
  display: inline-block; padding: 0.13rem 0.5rem; border-radius: 999px;
  font-size: 0.67rem; font-weight: 700; letter-spacing: 0.07em; text-transform: uppercase;
  background: var(--tca, rgba(100,116,139,0.1)); color: var(--tc, #64748b);
}
.tile-title { color: #0f172a; font-size: 0.96rem; line-height: 1.3; }
.tile-desc  { color: #64748b; font-size: 0.82rem; line-height: 1.55; margin-bottom: 0; }
.tile-cta {
  font-size: 0.82rem; font-weight: 600; color: var(--tc, #64748b);
  border-top: 1px solid rgba(148,163,184,0.18);
}
.cta-arrow { display: inline-block; transition: transform 0.18s; }
.feature-tile:hover .cta-arrow { transform: translateX(5px); }

/* ── Guest hero ───────────────────────────────────────────────────────── */
.home-hero {
  background:
    radial-gradient(circle at 10% 10%, rgba(52,211,153,0.35), transparent 35%),
    radial-gradient(circle at 88% 12%, rgba(56,189,248,0.3),  transparent 34%),
    linear-gradient(115deg, #111827, #0f766e 46%, #2563eb);
  border-radius: 0 0 28px 28px; overflow: hidden;
}
.hero-title    { line-height: 1.05; }
.hero-subtitle { max-width: 720px; }
.hero-pill {
  border: 1px solid rgba(255,255,255,0.45); border-radius: 999px;
  padding: 0.32rem 0.78rem; font-size: 0.78rem; font-weight: 700;
  letter-spacing: 0.08em; text-transform: uppercase;
  background: rgba(255,255,255,0.1); color: #fff;
}
.hero-stat-card { border-radius: 18px; background: rgba(255,255,255,0.95); }
.mini-stat      { background: rgba(14,165,233,0.09); }
.hero-sparkline {
  height: 48px; border-radius: 12px; position: relative;
  background:
    linear-gradient(180deg, rgba(14,165,233,0.15), rgba(37,99,235,0.08)),
    repeating-linear-gradient(90deg, rgba(15,23,42,0.08) 0 1px, transparent 1px 24px);
}
.hero-sparkline::after {
  content: ''; position: absolute; left: 10px; right: 10px; top: 11px;
  height: 24px; border-radius: 999px;
  background: linear-gradient(90deg, rgba(15,118,110,0.14), rgba(37,99,235,0.2));
}
.gradient-title {
  background: linear-gradient(90deg, #0f766e, #2563eb);
  -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;
}

/* ── Section eyebrow ──────────────────────────────────────────────────── */
.section-eyebrow {
  display: inline-block; padding: 0.2rem 0.75rem; border-radius: 999px;
  font-size: 0.72rem; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase;
  background: rgba(37,99,235,0.1); color: #2563eb;
  border: 1px solid rgba(37,99,235,0.2);
}

/* ── Guest role tabs ──────────────────────────────────────────────────── */
.role-tab-btn {
  border: 2px solid rgba(148,163,184,0.3); border-radius: 999px;
  background: #fff; color: #334155; padding: 0.46rem 1.2rem;
  font-weight: 600; font-size: 0.87rem; cursor: pointer;
  transition: all 0.2s ease;
}
.role-tab-btn:hover  { border-color: var(--tc); color: var(--tc); }
.role-tab-btn.active {
  background: var(--tc); border-color: var(--tc); color: #fff;
  box-shadow: 0 4px 18px rgba(0,0,0,0.17);
}

/* Tab transition */
.tab-fade-enter-active, .tab-fade-leave-active { transition: opacity 0.2s ease, transform 0.2s ease; }
.tab-fade-enter-from, .tab-fade-leave-to       { opacity: 0; transform: translateY(10px); }

/* ── Guest tiles ──────────────────────────────────────────────────────── */
.guest-tile {
  background: #fff; border: 1px solid rgba(148,163,184,0.2);
  border-radius: 18px; padding: 1.25rem;
  transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
  animation: fadeSlideUp 0.4s ease both;
  animation-delay: calc(var(--ci, 0) * 50ms);
}
.guest-tile:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 28px var(--tca, rgba(0,0,0,0.08));
  border-color: var(--tc, #94a3b8);
}
.guest-icon { font-size: 1.65rem; line-height: 1; }

/* ── Bottom panels ────────────────────────────────────────────────────── */
.panel-card {
  border-radius: 20px; border: 1px solid rgba(148,163,184,0.2);
  background: linear-gradient(160deg, rgba(255,255,255,0.96), rgba(248,250,252,0.92));
}
.cta-panel {
  background: linear-gradient(145deg, #0f766e, #0ea5e9 55%, #2563eb);
  border: none;
}

/* ── Responsive ───────────────────────────────────────────────────────── */
@media (max-width: 992px) {
  .auth-hero, .home-hero { border-radius: 0; }
  .hero-title { font-size: 2.2rem; }
}
</style>
