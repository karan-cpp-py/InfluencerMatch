<template>
  <nav class="navbar navbar-expand-lg navbar-dark sticky-top nav-modern">
    <div class="container-fluid">
      <router-link class="navbar-brand fw-bold d-flex align-items-center gap-2" to="/">
        <span class="badge rounded-pill text-bg-light text-dark fw-bold">IM</span>
        <span class="brand-text">InfluencerMatch</span>
      </router-link>
      <button
        class="navbar-toggler"
        type="button"
        data-bs-toggle="collapse"
        data-bs-target="#navbarSupportedContent"
        aria-controls="navbarSupportedContent"
        aria-expanded="false"
        aria-label="Toggle navigation"
      >
        <span class="navbar-toggler-icon"></span>
      </button>

      <div
        ref="navCollapseRef"
        class="collapse navbar-collapse"
        id="navbarSupportedContent"
        @click="handleMenuClick"
      >
        <ul class="navbar-nav me-auto mb-2 mb-lg-0">
          <li class="nav-item">
            <router-link class="nav-link" to="/">Home</router-link>
          </li>
          <li class="nav-item">
            <router-link class="nav-link" to="/plans">Plans</router-link>
          </li>
          <li class="nav-item">
            <router-link class="nav-link" :to="{ path: '/plans', hash: '#book-demo' }">Book Demo</router-link>
          </li>

          <!-- Guest links -->
          <li v-if="!token" class="nav-item">
            <router-link class="nav-link" to="/login">Login</router-link>
          </li>
          <li v-if="!token" class="nav-item">
            <router-link class="nav-link" to="/register">Register</router-link>
          </li>

          <!-- Creator links -->
          <li v-if="token && role === 'Creator'" class="nav-item">
            <router-link class="nav-link" to="/creator-dashboard">My Dashboard</router-link>
          </li>
          <li v-if="token && role === 'Creator'" class="nav-item">
            <router-link class="nav-link" to="/creator/latest-video-analysis">Video Analysis</router-link>
          </li>

          <!-- SuperAdmin links -->
          <li v-if="token && role === 'SuperAdmin'" class="nav-item">
            <router-link class="nav-link fw-semibold" to="/admin">⚙️ Admin Panel</router-link>
          </li>

          <!-- Customer links (Brand / Agency / Individual / CreatorManager) -->
          <template v-if="token && customerUserRoles.includes(role)">

            <!-- Marketplace -->
            <li class="nav-item" v-if="platformConfig.features.enableMarketplace">
              <router-link class="nav-link" to="/marketplace">Marketplace</router-link>
            </li>

            <!-- Campaigns dropdown (Brand / Agency only, behind feature flag) -->
            <li class="nav-item dropdown" v-if="brandOpsRoles.includes(role) && platformConfig.features.enableBrandActivation">
              <a class="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                Campaigns
              </a>
              <ul class="dropdown-menu dropdown-menu-dark">
                <li><router-link class="dropdown-item" to="/brand">✏️ Create Campaign</router-link></li>
                <li><router-link class="dropdown-item" to="/brand/campaigns">📋 My Campaigns</router-link></li>
                <li><router-link class="dropdown-item" to="/brand/campaign-onboarding">🧭 Campaign Wizard</router-link></li>
              </ul>
            </li>

            <!-- Discover dropdown -->
            <li class="nav-item dropdown">
              <a class="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                Discover
              </a>
              <ul class="dropdown-menu dropdown-menu-dark">
                <li><router-link class="dropdown-item" to="/brand/youtube-creators">📺 YouTube Catalogue</router-link></li>
                <li><router-link class="dropdown-item" to="/discovery">🔍 Creator Discovery</router-link></li>
                <li><router-link class="dropdown-item" to="/creators/search">🔎 Creator Search</router-link></li>
                <li><router-link class="dropdown-item" to="/creators/rising">🚀 Rising Creators</router-link></li>
                <li><router-link class="dropdown-item" to="/creators/leaderboard">🏆 Leaderboard</router-link></li>
                <li><router-link class="dropdown-item" to="/creators/compare">⚖️ Compare Creators</router-link></li>
                <li><router-link class="dropdown-item" to="/videos/trending">🔥 Trending Videos</router-link></li>
                <template v-if="brandOpsRoles.includes(role) && platformConfig.features.enableBrandActivation">
                  <li><hr class="dropdown-divider"/></li>
                  <li><router-link class="dropdown-item" to="/brand/analytics">📊 Brand Analytics</router-link></li>
                  <li><router-link class="dropdown-item" to="/brand/opportunities">💡 Opportunities</router-link></li>
                </template>
              </ul>
            </li>

            <!-- Team Workspace (Brand / Agency) -->
            <li class="nav-item" v-if="brandOpsRoles.includes(role)">
              <router-link class="nav-link" to="/workspace/team">Team</router-link>
            </li>

          </template>
        </ul>

        <!-- Right side: notification bell + account dropdown -->
        <div v-if="token" class="d-flex align-items-center gap-2 nav-user-tools">
          <NotificationBell />
          <div class="dropdown">
            <button
              class="btn btn-sm btn-outline-light dropdown-toggle d-flex align-items-center gap-2"
              type="button"
              data-bs-toggle="dropdown"
              aria-expanded="false"
            >
              <span class="fw-semibold d-none d-md-inline text-truncate" style="max-width:120px">{{ userName }}</span>
              <span class="badge rounded-pill text-bg-light text-dark" style="font-size:0.62rem">{{ role }}</span>
            </button>
            <ul class="dropdown-menu dropdown-menu-end dropdown-menu-dark">
              <!-- Customer account items -->
              <template v-if="customerUserRoles.includes(role)">
                <li><router-link class="dropdown-item" to="/subscriptions">💳 Subscription</router-link></li>
                <li><router-link class="dropdown-item" to="/notifications">🔔 Notifications</router-link></li>
                <li><router-link class="dropdown-item" to="/onboarding">🚀 Onboarding</router-link></li>
                <li><router-link class="dropdown-item" to="/dashboard-config">⚙️ Dashboard Config</router-link></li>
                <li v-if="brandOpsRoles.includes(role) && !platformConfig.features.enableBrandActivation">
                  <router-link class="dropdown-item" to="/brand/waitlist">📋 Brand Waitlist</router-link>
                </li>
                <li><hr class="dropdown-divider"/></li>
              </template>
              <!-- Creator account items -->
              <template v-if="role === 'Creator'">
                <li><router-link class="dropdown-item" to="/subscriptions">💳 Subscription</router-link></li>
                <li><router-link class="dropdown-item" to="/creator/onboarding">🚀 Onboarding</router-link></li>
                <li><hr class="dropdown-divider"/></li>
              </template>
              <li>
                <button class="dropdown-item text-danger fw-semibold" @click="logout">🚪 Logout</button>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  </nav>
</template>

<script setup>
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { authToken, authRole, authUserName, clearAuth } from '../services/auth';
import { platformConfig } from '../services/platform';
import NotificationBell from './NotificationBell.vue';

const router = useRouter();
const route = useRoute();
const token = computed(() => authToken.value);
const role = computed(() => authRole.value);
const userName = computed(() => authUserName.value);
const customerUserRoles = ['Brand', 'Agency', 'Individual', 'CreatorManager'];
const brandOpsRoles = ['Brand', 'Agency'];
const navCollapseRef = ref(null);

function collapseMobileMenu() {
  if (typeof window === 'undefined' || window.innerWidth >= 992) return;

  const collapseEl = navCollapseRef.value;
  if (!collapseEl?.classList?.contains('show')) return;

  collapseEl.classList.remove('show');

  const toggler = document.querySelector('.navbar-toggler[aria-controls="navbarSupportedContent"]');
  if (toggler) {
    toggler.setAttribute('aria-expanded', 'false');
  }
}

function handleMenuClick(event) {
  const target = event?.target;
  if (!target?.closest) return;

  const clickable = target.closest('a, button');
  if (!clickable) return;

  // Keep dropdown toggles functional; close only when a real navigation/action item is clicked.
  if (clickable.classList?.contains('dropdown-toggle')) return;

  collapseMobileMenu();
}

watch(
  () => route.fullPath,
  () => {
    nextTick(() => collapseMobileMenu());
  }
);

function logout() {
  collapseMobileMenu();
  clearAuth();
  router.push('/');
}
</script>

<style scoped>
.nav-modern .container-fluid {
  gap: 0.45rem;
}

.nav-modern .navbar-brand {
  min-width: 0;
  max-width: min(52vw, 640px);
}

.brand-text {
  display: inline-block;
  min-width: 0;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (min-width: 992px) {
  .nav-modern .navbar-collapse {
    min-width: 0;
  }

  .nav-modern .navbar-nav {
    flex-wrap: wrap;
    row-gap: 0.2rem;
  }

  .nav-user-tools {
    flex-shrink: 0;
    min-width: 0;
  }

  .nav-user-tools .navbar-text {
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    display: inline-block;
  }
}

@media (max-width: 991.98px) {
  .nav-modern .navbar-brand {
    max-width: calc(100% - 62px);
  }
}
</style>

