<template>
  <div id="app">
    <Navbar />

    <div v-if="showSessionExpired" class="session-expired-toast" role="alert" aria-live="assertive" aria-atomic="true">
      <div class="session-expired-title">Session expired</div>
      <p class="mb-2">Your session timed out. Please login again to continue securely.</p>
      <button class="btn btn-sm btn-light" @click="goToLogin">Go to Login</button>
    </div>

    <div v-if="showPlanLimitToast" class="plan-limit-toast" role="alert" aria-live="assertive" aria-atomic="true">
      <div class="plan-limit-title">Upgrade Recommended</div>
      <p class="mb-2">{{ planLimitMessage }}</p>
      <div class="d-flex gap-2">
        <button class="btn btn-sm btn-light" @click="goToUpgrade">View Plans</button>
        <button class="btn btn-sm btn-outline-light" @click="showPlanLimitToast = false">Dismiss</button>
      </div>
    </div>

    <div v-if="showApiErrorToast" class="api-error-toast" role="alert" aria-live="assertive" aria-atomic="true">
      <div class="api-error-title">Action could not be completed</div>
      <p class="mb-2">{{ apiErrorMessage }}</p>
      <div class="d-flex gap-2">
        <button class="btn btn-sm btn-light" @click="showApiErrorToast = false">Dismiss</button>
      </div>
    </div>

    <main class="app-main fade-up">
      <router-view />
    </main>
    <footer class="app-footer text-center py-3 mt-4">
      <div class="container">
        <small>&copy; 2026 InfluencerMatch. All rights reserved.</small>
      </div>
    </footer>
  </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import Navbar from './components/Navbar.vue';

const router = useRouter();
const showSessionExpired = ref(false);
const showPlanLimitToast = ref(false);
const planLimitMessage = ref('You have reached your plan limit. Upgrade to continue.');
const upgradePath = ref('/plans');
const showApiErrorToast = ref(false);
const apiErrorMessage = ref('Something went wrong. Please try again.');
let sessionRedirectTimer = null;
let planLimitTimer = null;
let apiErrorTimer = null;

function onSessionExpired() {
  showSessionExpired.value = true;
  if (sessionRedirectTimer) {
    clearTimeout(sessionRedirectTimer);
  }

  sessionRedirectTimer = setTimeout(() => {
    goToLogin();
  }, 1800);
}

function goToLogin() {
  showSessionExpired.value = false;
  if (router.currentRoute.value.path !== '/login') {
    router.push('/login');
  }
}

function onPlanLimitReached(event) {
  const detail = event?.detail || {};
  const requiredPlan = detail?.requiredPlan ? ` Upgrade to ${detail.requiredPlan}.` : '';
  planLimitMessage.value = `${detail?.message || 'You have reached your plan limit.'}${requiredPlan}`.trim();
  upgradePath.value = detail?.upgradePath || '/plans';
  showPlanLimitToast.value = true;

  if (planLimitTimer) {
    clearTimeout(planLimitTimer);
  }

  planLimitTimer = setTimeout(() => {
    showPlanLimitToast.value = false;
  }, 5000);
}

function goToUpgrade() {
  showPlanLimitToast.value = false;
  router.push(upgradePath.value || '/plans');
}

function onApiError(event) {
  const detail = event?.detail || {};
  const msg = String(detail?.message || '').trim();
  if (!msg) return;

  apiErrorMessage.value = msg;
  showApiErrorToast.value = true;

  if (apiErrorTimer) {
    clearTimeout(apiErrorTimer);
  }

  apiErrorTimer = setTimeout(() => {
    showApiErrorToast.value = false;
  }, 4500);
}

onMounted(() => {
  window.addEventListener('session-expired', onSessionExpired);
  window.addEventListener('plan-limit-reached', onPlanLimitReached);
  window.addEventListener('api-error', onApiError);
});

onBeforeUnmount(() => {
  window.removeEventListener('session-expired', onSessionExpired);
  window.removeEventListener('plan-limit-reached', onPlanLimitReached);
  window.removeEventListener('api-error', onApiError);
  if (sessionRedirectTimer) {
    clearTimeout(sessionRedirectTimer);
  }
  if (planLimitTimer) {
    clearTimeout(planLimitTimer);
  }
  if (apiErrorTimer) {
    clearTimeout(apiErrorTimer);
  }
});
</script>

<style scoped>
.session-expired-toast {
  position: fixed;
  right: 1rem;
  top: 1rem;
  z-index: 1080;
  width: min(360px, calc(100vw - 2rem));
  border-radius: 14px;
  border: 1px solid rgba(252, 165, 165, 0.35);
  background: linear-gradient(135deg, #7f1d1d, #b91c1c);
  color: #fee2e2;
  box-shadow: 0 14px 28px rgba(127, 29, 29, 0.35);
  padding: 0.9rem;
  animation: toast-in 0.22s ease both;
}

.session-expired-title {
  font-weight: 700;
  margin-bottom: 0.2rem;
}

.plan-limit-toast {
  position: fixed;
  right: 1rem;
  top: 6.3rem;
  z-index: 1080;
  width: min(380px, calc(100vw - 2rem));
  border-radius: 14px;
  border: 1px solid rgba(147, 197, 253, 0.35);
  background: linear-gradient(135deg, #1e3a8a, #2563eb);
  color: #dbeafe;
  box-shadow: 0 14px 28px rgba(37, 99, 235, 0.35);
  padding: 0.9rem;
  animation: toast-in 0.22s ease both;
}

.plan-limit-title {
  font-weight: 700;
  margin-bottom: 0.2rem;
}

.api-error-toast {
  position: fixed;
  right: 1rem;
  top: 11.7rem;
  z-index: 1080;
  width: min(420px, calc(100vw - 2rem));
  border-radius: 14px;
  border: 1px solid rgba(251, 191, 36, 0.35);
  background: linear-gradient(135deg, #78350f, #b45309);
  color: #ffedd5;
  box-shadow: 0 14px 28px rgba(180, 83, 9, 0.35);
  padding: 0.9rem;
  animation: toast-in 0.22s ease both;
}

.api-error-title {
  font-weight: 700;
  margin-bottom: 0.2rem;
}

@keyframes toast-in {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>