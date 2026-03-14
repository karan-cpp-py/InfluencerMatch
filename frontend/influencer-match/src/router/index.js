import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '../views/HomeView.vue';
import LoginView from '../views/LoginView.vue';
import RegisterView from '../views/RegisterView.vue';
import InfluencerDashboard from '../views/InfluencerDashboard.vue';
import BrandDashboard from '../views/BrandDashboard.vue';
import BrandCampaigns from '../views/BrandCampaigns.vue';
import InfluencerList from '../views/InfluencerList.vue';
import InfluencerDetail from '../views/InfluencerDetail.vue';
import CampaignList from '../views/CampaignList.vue';
import ResultsView from '../views/ResultsView.vue';
import { homeRouteForRole } from '../services/claims';
import { ensurePlatformConfigLoaded, platformConfig } from '../services/platform';

const routes = [
  { path: '/', component: HomeView },
  { path: '/login', component: LoginView },
  { path: '/register', component: RegisterView },
  { path: '/verify-email', component: () => import('../views/VerifyEmailView.vue') },
  { path: '/forgot-password', component: () => import('../views/ForgotPasswordView.vue') },
  { path: '/reset-password', component: () => import('../views/ResetPasswordView.vue') },
  { path: '/terms', component: () => import('../views/TermsView.vue') },
  { path: '/plans', component: () => import('../views/PricingPlansView.vue') },
  { path: '/notifications', component: () => import('../views/NotificationCenterView.vue'), meta: { requiresAuth: true } },
  { path: '/onboarding', component: () => import('../views/OnboardingHubView.vue'), meta: { requiresAuth: true } },
  { path: '/brand/campaign-onboarding', component: () => import('../views/BrandCampaignOnboardingView.vue'), meta: { requiresAuth: true, role: ['Brand', 'Agency'] } },
  { path: '/subscriptions', component: () => import('../views/SubscriptionView.vue'), meta: { requiresAuth: true } },
  { path: '/workspace/team', component: () => import('../views/WorkspaceTeamView.vue'), meta: { requiresAuth: true, role: ['Brand', 'Agency'] } },
  { path: '/dashboard-config', component: () => import('../views/DashboardConfigView.vue'), meta: { requiresAuth: true } },

  // ── SuperAdmin ────────────────────────────────────────────────────────
  {
    path: '/admin',
    component: () => import('../views/SuperAdminView.vue'),
    meta: { requiresAuth: false } // access control handled in the view itself
  },
    {
      path: '/admin/youtube-importer',
      component: () => import('../views/YoutubeImporterView.vue'),
      meta: { requiresAuth: false } // access control handled in the view itself
    },

  // ── Creator (self-registered) ──────────────────────────────────────────
  {
    path: '/creator-dashboard',
    component: () => import('../views/CreatorDashboard.vue'),
    meta: { requiresAuth: true, role: 'Creator' }
  },
  {
    path: '/creator/onboarding',
    component: () => import('../views/CreatorOnboardingView.vue'),
    meta: { requiresAuth: true, role: 'Creator' }
  },
  {
    path: '/creator/latest-video-analysis',
    component: () => import('../views/LatestVideoAnalysisView.vue'),
    meta: { requiresAuth: true, role: 'Creator' }
  },

  {
    path: '/brand/waitlist',
    component: () => import('../views/BrandWaitlistView.vue'),
    meta: { requiresAuth: true, role: ['Brand', 'Agency', 'Individual', 'CreatorManager'] }
  },

  // ── Brand Marketplace ──────────────────────────────────────────────────
  {
    path: '/marketplace',
    component: () => import('../views/MarketplaceView.vue'),
    meta: {
      requiresAuth: true,
      role: ['Brand', 'Agency', 'Individual', 'CreatorManager'],
      strategyFeature: 'enableMarketplace'
    }
  },

  // ── Brand discovery / analytics ────────────────────────────────────────
  { path: '/brand/youtube-creators', component: () => import('../views/BrandYouTubeCreatorsView.vue'), meta: { requiresAuth: true, role: ['Brand', 'Agency', 'Individual', 'CreatorManager'] } },
  { path: '/youtube/search-intelligence', component: () => import('../views/YouTubeSearchIntelligenceView.vue'), meta: { requiresAuth: true, role: ['Brand', 'Agency', 'Individual', 'CreatorManager', 'Creator'] } },
  { path: '/discovery',            component: () => import('../views/CreatorDiscovery.vue'),    meta: { requiresAuth: true, role: ['Brand', 'Agency', 'Individual', 'CreatorManager'] } },
  { path: '/creators/search',      component: () => import('../views/CreatorSearch.vue'),       meta: { requiresAuth: true, role: ['Brand', 'Agency', 'Individual', 'CreatorManager'] } },
  { path: '/creator/:id/analytics',       component: () => import('../views/CreatorAnalyticsView.vue'),  meta: { requiresAuth: true } },
  { path: '/creator/:id/video-analytics', component: () => import('../views/VideoAnalyticsView.vue'),     meta: { requiresAuth: true } },
  { path: '/creator/:id/latest-video-analysis', component: () => import('../views/LatestVideoAnalysisView.vue'), meta: { requiresAuth: true } },
  { path: '/creators/leaderboard', component: () => import('../views/ScoreLeaderboard.vue'),    meta: { requiresAuth: true } },
  { path: '/creators/compare',     component: () => import('../views/CreatorCompare.vue'),      meta: { requiresAuth: true } },
  {
    path: '/brand/analytics',
    component: () => import('../views/BrandAnalytics.vue'),
    meta: { requiresAuth: true, role: ['Brand', 'Agency'], strategyFeature: 'enableBrandActivation' }
  },
  { path: '/creators/rising',      component: () => import('../views/RisingCreators.vue'),      meta: { requiresAuth: true } },
  {
    path: '/brand/opportunities',
    component: () => import('../views/BrandOpportunities.vue'),
    meta: { requiresAuth: true, role: ['Brand', 'Agency'], strategyFeature: 'enableBrandActivation' }
  },
  {
    path: '/brand/creator-intelligence',
    component: () => import('../views/BrandCreatorIntelligenceView.vue'),
    meta: { requiresAuth: true, role: ['Brand', 'Agency'], strategyFeature: 'enableBrandActivation' }
  },
  { path: '/videos/trending',      component: () => import('../views/TrendingVideos.vue'),      meta: { requiresAuth: true } },

  // ── Legacy Influencer / Brand ──────────────────────────────────────────
  { path: '/influencer',       component: InfluencerDashboard, meta: { requiresAuth: true, role: 'Influencer' } },
  { path: '/influencer/:id',   component: InfluencerDetail,    meta: { requiresAuth: true } },
  {
    path: '/brand',
    component: BrandDashboard,
    meta: { requiresAuth: true, role: ['Brand', 'Agency'], strategyFeature: 'enableBrandActivation' }
  },
  {
    path: '/brand/campaigns',
    component: BrandCampaigns,
    meta: { requiresAuth: true, role: ['Brand', 'Agency'], strategyFeature: 'enableBrandActivation' }
  },
  { path: '/campaigns',        component: CampaignList,        meta: { requiresAuth: true, role: 'Influencer' } },
  { path: '/influencers',      component: InfluencerList,      meta: { requiresAuth: true } },
  { path: '/results/:campaignId', component: ResultsView,      meta: { requiresAuth: true } },
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

router.beforeEach(async (to, from, next) => {
  const token = localStorage.getItem('token');

  await ensurePlatformConfigLoaded();

  if (to.meta.requiresAuth && !token) {
    return next('/login');
  }

  // Role and plan/feature restrictions intentionally disabled — all pages are accessible to any logged-in user.

  next();
});

export default router;
