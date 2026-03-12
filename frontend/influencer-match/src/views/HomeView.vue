<template>
  <div class="home-wrap">
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
              <router-link class="btn btn-light px-4" :to="token && role === 'Creator' ? '/creator/onboarding' : '/register'">Start Creator Onboarding</router-link>
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

    <section class="container pb-4">
      <div class="section-shell p-3 p-md-4 mb-4">
        <div class="row g-3 text-center text-md-start">
          <div class="col-md-3" v-for="item in highlights" :key="item.label">
            <div class="highlight-tile p-3 h-100">
              <div class="small text-muted">{{ item.label }}</div>
              <div class="h4 fw-bold mb-1">{{ item.value }}</div>
              <div class="small text-muted">{{ item.note }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="row g-3 mb-4">
        <div class="col-md-4" v-for="feature in featureCards" :key="feature.title">
          <div class="card border-0 h-100 p-3 feature-card">
            <div class="small text-muted">Creator Intelligence</div>
            <div class="h4 fw-bold mb-2">{{ feature.title }}</div>
            <div class="text-muted small">{{ feature.desc }}</div>
            <div class="mt-2 small fw-semibold" :class="feature.statusClass">{{ feature.status }}</div>
          </div>
        </div>
      </div>

      <div class="card border-0 p-4 text-center action-card">
        <p v-if="!token" class="mb-3">Get started — register as a Brand, Agency, or Creator.</p>
        <div v-if="!token" class="d-flex flex-wrap justify-content-center gap-2">
          <router-link class="btn btn-primary" to="/register">Create Account</router-link>
          <router-link class="btn btn-outline-secondary" to="/login">Login</router-link>
          <router-link class="btn btn-outline-light" to="/plans">View Plans</router-link>
        </div>
        <div v-else>
          <p class="mb-3">Welcome back!</p>
          <div v-if="['Brand', 'Agency'].includes(role)">
            <router-link v-if="platformConfig.features.enableBrandActivation" class="btn btn-primary me-2" to="/brand">Create Campaign</router-link>
            <router-link v-if="platformConfig.features.enableBrandActivation" class="btn btn-secondary me-2" to="/brand/campaigns">My Campaigns</router-link>
            <router-link v-if="platformConfig.features.enableMarketplace" class="btn btn-info text-white me-2" to="/marketplace">Marketplace</router-link>
            <router-link class="btn btn-outline-primary me-2" to="/brand/youtube-creators">YouTube Catalogue</router-link>
            <router-link v-if="!platformConfig.features.enableBrandActivation" class="btn btn-warning me-2" to="/brand/waitlist">Join Brand Waitlist</router-link>
          </div>
          <div v-else-if="['Individual', 'CreatorManager'].includes(role)">
            <router-link class="btn btn-primary me-2" to="/brand/youtube-creators">YouTube Catalogue</router-link>
            <router-link class="btn btn-info text-white me-2" to="/creators/search">Creator Search</router-link>
            <router-link class="btn btn-outline-primary me-2" to="/subscriptions">Subscription</router-link>
          </div>
          <div v-else-if="role === 'Creator'">
            <router-link class="btn btn-outline-primary me-2" to="/creator/onboarding">Onboarding</router-link>
            <router-link class="btn btn-primary me-2" to="/creator-dashboard">My Dashboard</router-link>
          </div>
          <div v-else-if="role === 'SuperAdmin'">
            <router-link class="btn btn-primary" to="/admin">Admin Panel</router-link>
          </div>
        </div>
      </div>

      <div class="row g-3 mt-2">
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
            <div class="small text-uppercase fw-semibold opacity-75">Next Step</div>
            <div class="h5 fw-bold mt-2">Open Discover and run your first shortlist.</div>
            <router-link class="btn btn-light btn-sm mt-3" to="/creators/search">Go To Creator Search</router-link>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { authToken, authRole } from '../services/auth';
import { platformConfig } from '../services/platform';
import PhaseBadge from '../components/PhaseBadge.vue';

const token = computed(() => authToken.value);
const role = computed(() => authRole.value);

const highlights = computed(() => [
  {
    label: 'Creators Indexed',
    value: Number(platformConfig.kpiGates?.activeCreatorsWeekly || 0).toLocaleString(),
    note: 'tracked in active weekly pipeline'
  },
  {
    label: 'Brand Readiness',
    value: platformConfig.features?.enableBrandActivation ? 'Open' : 'Waitlist',
    note: 'driven by creator-side KPI gate'
  },
  {
    label: 'Core Modules',
    value: '6+',
    note: 'discovery, scoring, analytics, onboarding'
  },
  {
    label: 'Decision Speed',
    value: 'Faster',
    note: 'single workspace for campaign planning'
  }
]);

const featureCards = computed(() => [
  {
    title: 'Live',
    desc: 'Score explanations, weekly reports, and onboarding progress with AI-assisted interpretation.',
    status: 'Creator Intelligence',
    statusClass: 'text-primary'
  },
  {
    title: 'Growing',
    desc: 'Leaderboard, compare view, and rising creator discovery in one visual workflow.',
    status: 'Creator Graph',
    statusClass: 'text-success'
  },
  {
    title: platformConfig.features?.enableBrandActivation ? 'Open' : 'Waitlist',
    desc: 'Unlocks automatically when creator-side KPI thresholds are met for qualified activation.',
    status: 'Brand Activation',
    statusClass: platformConfig.features?.enableBrandActivation ? 'text-success' : 'text-warning'
  }
]);
</script>

<style scoped>
.home-wrap {
  min-height: calc(100vh - 130px);
}

.home-hero {
  background:
    radial-gradient(circle at 10% 10%, rgba(52, 211, 153, 0.35), transparent 35%),
    radial-gradient(circle at 88% 12%, rgba(56, 189, 248, 0.3), transparent 34%),
    linear-gradient(115deg, #111827, #0f766e 46%, #2563eb);
  border-radius: 0 0 28px 28px;
  overflow: hidden;
}

.hero-title {
  line-height: 1.05;
}

.hero-subtitle {
  max-width: 720px;
}

.hero-pill {
  border: 1px solid rgba(255, 255, 255, 0.45);
  border-radius: 999px;
  padding: 0.32rem 0.78rem;
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(255, 255, 255, 0.1);
}

.tracking {
  letter-spacing: 0.08em;
}

.hero-stat-card {
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.95);
}

.mini-stat {
  background: rgba(14, 165, 233, 0.09);
}

.hero-sparkline {
  height: 48px;
  border-radius: 12px;
  background:
    linear-gradient(180deg, rgba(14, 165, 233, 0.15), rgba(37, 99, 235, 0.08)),
    repeating-linear-gradient(90deg, rgba(15, 23, 42, 0.08) 0 1px, transparent 1px 24px);
  position: relative;
}

.hero-sparkline::after {
  content: '';
  position: absolute;
  left: 10px;
  right: 10px;
  top: 11px;
  height: 24px;
  border-radius: 999px;
  background: linear-gradient(90deg, rgba(15, 118, 110, 0.14), rgba(37, 99, 235, 0.2));
}

.highlight-tile {
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.72);
  border: 1px solid rgba(255, 255, 255, 0.65);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.highlight-tile:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.12);
}

.feature-card {
  overflow: hidden;
  position: relative;
}

.feature-card::before {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(160deg, rgba(15, 118, 110, 0.06), transparent 40%, rgba(37, 99, 235, 0.08));
  pointer-events: none;
}

.action-card {
  background: linear-gradient(160deg, rgba(255, 255, 255, 0.92), rgba(255, 255, 255, 0.84));
}

.cta-panel {
  background: linear-gradient(145deg, #0f766e, #0ea5e9 55%, #2563eb);
  border: none;
}

@media (max-width: 992px) {
  .home-hero {
    border-radius: 0;
  }

  .hero-title {
    font-size: 2.2rem;
  }
}
</style>
