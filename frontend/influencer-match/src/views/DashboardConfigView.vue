<template>
  <div class="container py-4" style="max-width: 900px;">
    <h2 class="fw-bold mb-3">Dashboard Configuration</h2>
    <p class="text-muted">Your dashboard is personalized by customer type and active subscription plan.</p>

    <div v-if="loading" class="text-center py-5"><div class="spinner-border text-primary"></div></div>

    <div v-else-if="config" class="card border-0 shadow-sm">
      <div class="card-body">
        <div class="row g-3 mb-3">
          <div class="col-md-4"><div class="small text-muted">Customer Type</div><div class="fw-semibold">{{ config.customerType }}</div></div>
          <div class="col-md-4"><div class="small text-muted">Plan</div><div class="fw-semibold">{{ config.planName }}</div></div>
          <div class="col-md-4"><div class="small text-muted">Analytics</div><div class="fw-semibold">{{ config.analyticsAccessLevel }}</div></div>
        </div>

        <h6 class="fw-semibold mb-2">Enabled Modules</h6>
        <div class="d-flex flex-wrap gap-2">
          <span v-for="m in config.enabledModules" :key="m" class="badge bg-primary-subtle text-primary border">{{ m }}</span>
        </div>
      </div>
    </div>

    <div v-if="error" class="alert alert-danger mt-3">{{ error }}</div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import api from '../services/api';

const loading = ref(false);
const config = ref(null);
const error = ref('');

onMounted(loadConfig);

async function loadConfig() {
  loading.value = true;
  error.value = '';
  try {
    const res = await api.get('/dashboard/config');
    config.value = res.data;
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to load dashboard configuration.';
  } finally {
    loading.value = false;
  }
}
</script>
