<template>
  <div class="container py-4 pricing-shell">
    <section class="pricing-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Week 11</p>
        <h2 class="fw-bold mb-1">Pricing and GTM Hub</h2>
        <p class="mb-0 text-light-emphasis">Compare plans, book demos, and track referral growth from one page.</p>
      </div>
      <router-link class="btn btn-light btn-sm fw-semibold" to="/subscriptions">Manage Subscription</router-link>
    </section>

    <div v-if="loading" class="text-center py-5">
      <div class="spinner-border text-primary"></div>
    </div>

    <div v-else class="row g-3 mb-4">
      <div class="col-md-6 col-lg-3" v-for="plan in plans" :key="plan.planId">
        <div class="card h-100 border-0 shadow-sm panel-card">
          <div class="card-body d-flex flex-column">
            <h5 class="fw-bold mb-2">{{ plan.planName }}</h5>
            <div class="mb-2">
              <span class="fs-4 fw-bold">{{ formatMoney(plan.priceMonthly, plan.planName) }}</span>
              <span class="text-muted">/month</span>
            </div>
            <div class="small text-muted mb-3">Yearly: {{ formatMoney(plan.priceYearly, plan.planName, true) }}</div>
            <ul class="small mb-3 ps-3">
              <li>Max creator searches: {{ plan.maxCreatorSearch ?? 'Unlimited' }}</li>
              <li>Analytics: {{ plan.analyticsAccessLevel }}</li>
              <li>Export: {{ plan.exportAllowed ? 'Allowed' : 'Not included' }}</li>
            </ul>
            <button class="btn btn-primary mt-auto" @click="choosePlan(plan)">
              {{ token ? 'Choose Plan' : 'Login to Subscribe' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm panel-card mb-4" v-if="plans.length">
      <div class="card-body p-3 p-md-4">
        <h5 class="fw-bold mb-3">Plan Comparison</h5>
        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Plan</th>
                <th>Monthly</th>
                <th>Yearly</th>
                <th>Creator Searches</th>
                <th>Analytics</th>
                <th>Exports</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="plan in plans" :key="`cmp-${plan.planId}`">
                <td class="fw-semibold">{{ plan.planName }}</td>
                <td>{{ formatMoney(plan.priceMonthly, plan.planName) }}</td>
                <td>{{ formatMoney(plan.priceYearly, plan.planName, true) }}</td>
                <td>{{ plan.maxCreatorSearch ?? 'Unlimited' }}</td>
                <td>{{ plan.analyticsAccessLevel }}</td>
                <td>{{ plan.exportAllowed ? 'Yes' : 'No' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm panel-card mb-4" v-if="plans.length">
      <div class="card-body p-3 p-md-4">
        <h5 class="fw-bold mb-1">Plan Pricing Visual</h5>
        <p class="small text-muted mb-3">Monthly and yearly pricing mapped as a quick comparison chart.</p>
        <div class="chart-box">
          <AppBarChart :data="planPriceChartData" :options="priceBarOptions" />
        </div>
      </div>
    </div>

    <div class="row g-3 mb-4">
      <div class="col-lg-7">
        <div class="card border-0 shadow-sm panel-card h-100" id="book-demo">
          <div class="card-body p-3 p-md-4">
            <h5 class="fw-bold mb-2">Book Demo / Enterprise Inquiry</h5>
            <p class="text-muted small mb-3">Route high-intent opportunities directly to your GTM pipeline.</p>
            <div class="row g-2">
              <div class="col-md-6">
                <input v-model.trim="lead.fullName" class="form-control" placeholder="Full name" />
              </div>
              <div class="col-md-6">
                <input v-model.trim="lead.workEmail" type="email" class="form-control" placeholder="Work email" />
              </div>
              <div class="col-md-6">
                <input v-model.trim="lead.companyName" class="form-control" placeholder="Company" />
              </div>
              <div class="col-md-6">
                <select v-model="lead.teamSize" class="form-select">
                  <option value="">Team size</option>
                  <option value="1-10">1-10</option>
                  <option value="11-50">11-50</option>
                  <option value="51-200">51-200</option>
                  <option value="200+">200+</option>
                </select>
              </div>
              <div class="col-12">
                <textarea v-model.trim="lead.notes" class="form-control" rows="3" placeholder="Goals, integrations, or timelines"></textarea>
              </div>
              <div class="col-12 d-flex justify-content-end">
                <button class="btn btn-primary" :disabled="submittingLead" @click="submitLead">
                  {{ submittingLead ? 'Submitting...' : 'Book Demo' }}
                </button>
              </div>
            </div>
            <div v-if="leadMessage" class="alert alert-success mt-3 py-2">{{ leadMessage }}</div>
            <div v-if="leadError" class="alert alert-danger mt-3 py-2">{{ leadError }}</div>
          </div>
        </div>
      </div>

      <div class="col-lg-5" v-if="token">
        <div class="card border-0 shadow-sm panel-card h-100">
          <div class="card-body p-3 p-md-4">
            <h5 class="fw-bold mb-2">Referral Program</h5>
            <p class="text-muted small mb-2">Invite peers using your code and monitor accepted signups.</p>

            <div class="input-group mb-3" v-if="referral.summary">
              <input class="form-control" :value="referral.summary.code" readonly />
              <button class="btn btn-outline-secondary" @click="copyCode(referral.summary.code)">Copy</button>
            </div>

            <div class="small text-muted mb-2" v-if="referral.summary">
              Total referrals: <strong>{{ referral.summary.totalReferrals }}</strong>
            </div>

            <div class="table-responsive" v-if="referral.usage?.length">
              <table class="table table-sm mb-0 align-middle">
                <thead class="table-light">
                  <tr>
                    <th>User</th>
                    <th>When</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="u in referral.usage" :key="`${u.referredUserId}-${u.createdAt}`">
                    <td>
                      <div class="fw-semibold">{{ u.referredUserName }}</div>
                      <div class="small text-muted">{{ u.referredEmail }}</div>
                    </td>
                    <td class="small text-muted">{{ fmtDate(u.createdAt) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="small text-muted">No referrals yet. Share your code in onboarding emails and campaign briefs.</div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="error" class="alert alert-danger mt-3">{{ error }}</div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { authToken } from '../services/auth';
import { bookDemo, fetchReferral } from '../services/gtm';
import AppBarChart from '../components/charts/AppBarChart.vue';

const router = useRouter();
const token = authToken;
const plans = ref([]);
const loading = ref(false);
const error = ref('');

const lead = ref({
  fullName: '',
  workEmail: '',
  companyName: '',
  teamSize: '',
  notes: '',
  source: 'PricingPage',
});

const referral = ref({ summary: null, usage: [] });
const submittingLead = ref(false);
const leadMessage = ref('');
const leadError = ref('');

const planPriceChartData = computed(() => ({
  labels: plans.value.map(p => p.planName),
  datasets: [
    {
      label: 'Monthly (INR)',
      data: plans.value.map(p => Number(p.priceMonthly || 0)),
      backgroundColor: '#2563eb',
      borderRadius: 8,
    },
    {
      label: 'Yearly (INR)',
      data: plans.value.map(p => Number(p.priceYearly || 0)),
      backgroundColor: '#0f766e',
      borderRadius: 8,
    },
  ],
}));

const priceBarOptions = {
  scales: {
    y: {
      beginAtZero: true,
      ticks: {
        callback: value => Number(value).toLocaleString('en-IN'),
      },
    },
  },
};

onMounted(async () => {
  await loadPlans();
  if (token.value) {
    await loadReferral();
  }
});

async function loadPlans() {
  loading.value = true;
  error.value = '';
  try {
    const res = await api.get('/plans');
    plans.value = res.data || [];
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to load plans.';
  } finally {
    loading.value = false;
  }
}

async function loadReferral() {
  try {
    referral.value = await fetchReferral();
  } catch {
    referral.value = { summary: null, usage: [] };
  }
}

async function submitLead() {
  submittingLead.value = true;
  leadMessage.value = '';
  leadError.value = '';

  try {
    await bookDemo(lead.value);
    leadMessage.value = 'Thanks. Our GTM team will reach out shortly.';
    lead.value = { fullName: '', workEmail: '', companyName: '', teamSize: '', notes: '', source: 'PricingPage' };
  } catch (e) {
    leadError.value = e.response?.data?.error || 'Failed to submit demo request.';
  } finally {
    submittingLead.value = false;
  }
}

function choosePlan(plan) {
  if (!token.value) {
    router.push('/login');
    return;
  }
  router.push({ path: '/subscriptions', query: { planId: plan.planId } });
}

function formatMoney(value, planName = '', isYearly = false) {
  if ((planName || '').toLowerCase() === 'enterprise') {
    return isYearly ? 'Custom billing' : 'Custom';
  }

  const num = Number(value || 0);
  if (num === 0) return 'Free';
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(num);
}

function fmtDate(value) {
  if (!value) return '';
  return new Date(value).toLocaleDateString();
}

async function copyCode(code) {
  if (!code) return;
  try {
    await navigator.clipboard.writeText(code);
  } catch {
    // no-op fallback for older browsers
  }
}
</script>

<style scoped>
.pricing-shell {
  max-width: 1120px;
}

.pricing-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #e2e8f0;
  background: linear-gradient(128deg, #111827 0%, #1e3a8a 52%, #0f766e 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.22rem 0.56rem;
  font-size: 0.7rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.25);
  color: #dbeafe;
}

.panel-card {
  border-radius: 16px;
}

.chart-box {
  height: 280px;
}

@media (max-width: 768px) {
  .pricing-hero {
    flex-direction: column;
  }
}
</style>
