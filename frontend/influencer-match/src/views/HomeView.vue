<template>
  <div class="home-wrap">
    <header class="home-hero py-5 mb-4">
      <div class="container position-relative">
        <div class="row align-items-center g-4">
          <div class="col-lg-7 text-white">
            <div class="mb-2">
              <PhaseBadge :phase="platformConfig.phase" />
            </div>
            <h1 class="display-4 fw-bold mb-3">AI Creator Intelligence Platform for Growth and Sponsorship Readiness</h1>
            <p class="lead mb-4 opacity-75">
              Build your creator graph, understand performance shifts, and prepare creators for high-fit brand opportunities.
            </p>
            <div class="d-flex flex-wrap gap-2">
              <router-link class="btn btn-light" :to="token && role === 'Creator' ? '/creator/onboarding' : '/register'">Start Creator Onboarding</router-link>
              <router-link class="btn btn-outline-light" to="/creators/leaderboard">Explore Creator Graph</router-link>
            </div>
          </div>
          <div class="col-lg-5">
            <div class="hero-stat-card card border-0 p-3">
              <div class="d-flex justify-content-between mb-2 small text-muted">
                <span>Current Phase</span>
                <span>{{ platformConfig.phase }}</span>
              </div>
              <div class="h3 fw-bold mb-3">{{ platformConfig.positioningLine }}</div>
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
            </div>
          </div>
        </div>
      </div>
    </header>

    <section class="container pb-4">
      <div class="row g-3 mb-4">
        <div class="col-md-4">
          <div class="card border-0 h-100 p-3">
            <div class="small text-muted">Creator Intelligence</div>
            <div class="h4 fw-bold mb-2">Live</div>
            <div class="text-muted small">Score explanations, weekly reports, and onboarding progress.</div>
          </div>
        </div>
        <div class="col-md-4">
          <div class="card border-0 h-100 p-3">
            <div class="small text-muted">Creator Graph</div>
            <div class="h4 fw-bold mb-2">Growing</div>
            <div class="text-muted small">Leaderboard, compare view, and rising creator discovery.</div>
          </div>
        </div>
        <div class="col-md-4">
          <div class="card border-0 h-100 p-3">
            <div class="small text-muted">Brand Activation</div>
            <div class="h4 fw-bold mb-2">{{ platformConfig.features.enableBrandActivation ? 'Open' : 'Waitlist' }}</div>
            <div class="text-muted small">Unlocks automatically when creator-side KPI thresholds are met.</div>
          </div>
        </div>
      </div>

      <div class="card border-0 p-4 text-center">
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
</script>

<style scoped>
.home-wrap {
  min-height: calc(100vh - 130px);
}

.home-hero {
  background:
    radial-gradient(circle at 10% 10%, rgba(34, 211, 238, 0.35), transparent 35%),
    linear-gradient(115deg, #0f172a, #0e7490 52%, #1d4ed8);
  border-radius: 0 0 28px 28px;
}

.tracking {
  letter-spacing: 0.08em;
}

.hero-stat-card {
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.92);
}

.mini-stat {
  background: rgba(14, 116, 144, 0.09);
}

@media (max-width: 992px) {
  .home-hero {
    border-radius: 0;
  }
}
</style>
