<template>
  <div class="container py-4" style="max-width: 980px;">
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body p-4">
        <h3 class="fw-bold mb-2">Role Onboarding</h3>
        <p class="text-muted mb-0">Complete these steps to accelerate activation and unlock paid value quickly.</p>
      </div>
    </div>

    <div class="row g-3">
      <div class="col-lg-7">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-3">Checklist</h5>
            <div v-if="loading" class="text-center py-3"><div class="spinner-border text-primary"></div></div>
            <ul v-else class="list-group list-group-flush">
              <li class="list-group-item d-flex justify-content-between align-items-center" v-for="item in checklist.items" :key="item.key">
                <span>{{ item.title }}</span>
                <span class="badge" :class="item.completed ? 'text-bg-success' : 'text-bg-secondary'">{{ item.completed ? 'Done' : 'Pending' }}</span>
              </li>
            </ul>
          </div>
        </div>
      </div>
      <div class="col-lg-5">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-body p-4">
            <div class="form-check form-switch mb-3">
              <input class="form-check-input" type="checkbox" id="demoToggle" v-model="demoMode" @change="toggleDemoMode" />
              <label class="form-check-label" for="demoToggle">Enable Demo Data Mode</label>
            </div>

            <h6 class="fw-semibold">Demo Insights</h6>
            <div v-if="demoLoading" class="text-center py-3"><div class="spinner-border text-primary"></div></div>
            <div v-else-if="!demoMode" class="text-muted small">Turn on demo mode to view sample cards.</div>
            <div v-else class="d-flex flex-column gap-2">
              <div class="border rounded p-2" v-for="(card, idx) in demoCards" :key="idx">
                <div class="small text-muted">{{ card.title }}</div>
                <div class="fw-bold">{{ card.value }}</div>
                <div class="small text-muted">{{ card.hint }}</div>
              </div>
            </div>
          </div>
        </div>

        <div
          v-if="checklist.role === 'Brand' || checklist.role === 'Agency'"
          class="card border-0 shadow-sm mt-3">
          <div class="card-body p-4">
            <h6 class="fw-semibold mb-1">Need a faster launch?</h6>
            <p class="small text-muted mb-3">Use the guided first-campaign wizard to create a campaign with best-practice defaults.</p>
            <router-link class="btn btn-primary btn-sm" to="/brand/campaign-onboarding">Open Campaign Wizard</router-link>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import api from '../services/api';

const loading = ref(false);
const demoLoading = ref(false);
const checklist = ref({ role: '', items: [] });
const demoCards = ref([]);
const demoMode = ref(localStorage.getItem('demoMode') === '1');

onMounted(async () => {
  await loadChecklist();
  if (demoMode.value) {
    await loadDemo();
  }
});

async function loadChecklist() {
  loading.value = true;
  try {
    const { data } = await api.get('/onboarding/checklist');
    checklist.value = data || { role: '', items: [] };
  } finally {
    loading.value = false;
  }
}

async function toggleDemoMode() {
  localStorage.setItem('demoMode', demoMode.value ? '1' : '0');
  if (demoMode.value) {
    await loadDemo();
  } else {
    demoCards.value = [];
  }
}

async function loadDemo() {
  demoLoading.value = true;
  try {
    const { data } = await api.get('/onboarding/demo-data');
    demoCards.value = data?.cards || [];
  } finally {
    demoLoading.value = false;
  }
}
</script>
