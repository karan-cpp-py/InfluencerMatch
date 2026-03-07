<template>
  <div class="container py-4" style="max-width: 980px;">
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body p-4">
        <div class="mb-2">
          <PhaseBadge :phase="platformConfig.phase" />
        </div>
        <h2 class="fw-bold mb-2">Creator Intelligence Onboarding</h2>
        <p class="text-muted mb-3">Complete your setup to unlock weekly insights, score explanations, and sponsorship readiness signals.</p>

        <div class="progress" style="height: 10px;">
          <div class="progress-bar" :style="{ width: `${status.profileCompletenessPercent || 0}%` }"></div>
        </div>
        <p class="small text-muted mt-2 mb-0">Profile completeness: <strong>{{ status.profileCompletenessPercent || 0 }}%</strong></p>
      </div>
    </div>

    <div class="row g-3 mb-4">
      <div class="col-md-6">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-3">Setup Checklist</h5>
            <div v-if="loading" class="text-center py-3"><div class="spinner-border text-primary"></div></div>
            <ul v-else class="list-group list-group-flush">
              <li v-for="step in status.steps" :key="step.key" class="list-group-item d-flex justify-content-between align-items-center">
                <span>{{ step.title }}</span>
                <span class="badge" :class="step.completed ? 'text-bg-success' : 'text-bg-secondary'">{{ step.completed ? 'Done' : 'Pending' }}</span>
              </li>
            </ul>
            <router-link class="btn btn-primary btn-sm mt-3" to="/creator-dashboard">Open Dashboard</router-link>
          </div>
        </div>
      </div>

      <div class="col-md-6">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-3">Why Your Score Changed</h5>
            <div v-if="loading" class="text-center py-3"><div class="spinner-border text-primary"></div></div>
            <ul v-else class="small mb-0">
              <li v-for="(line, i) in status.scoreChangeExplanations" :key="`reason-${i}`" class="mb-2">{{ line }}</li>
            </ul>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm">
      <div class="card-body p-4">
        <h5 class="fw-semibold mb-3">Weekly Auto-Report Highlights</h5>
        <div v-if="loading" class="text-center py-3"><div class="spinner-border text-primary"></div></div>
        <ul v-else class="mb-0">
          <li v-for="(insight, i) in status.weeklyInsights" :key="`insight-${i}`" class="mb-2">{{ insight }}</li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue';
import api from '../services/api';
import { platformConfig } from '../services/platform';
import PhaseBadge from '../components/PhaseBadge.vue';

const loading = ref(false);
const status = reactive({
  profileCompletenessPercent: 0,
  steps: [],
  scoreChangeExplanations: [],
  weeklyInsights: [],
  weeklyAlertCount: 0,
});

onMounted(async () => {
  loading.value = true;
  try {
    const res = await api.get('/creator/onboarding-status');
    Object.assign(status, res.data || {});
  } finally {
    loading.value = false;
  }
});
</script>
