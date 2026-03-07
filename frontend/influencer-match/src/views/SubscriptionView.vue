<template>
  <div class="container py-4 subscription-shell" style="max-width: 980px;">
    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
      <h2 class="fw-bold mb-0">Subscriptions</h2>
      <span class="badge rounded-pill text-bg-light border px-3 py-2">Billing Control Center</span>
    </div>

    <div v-if="loading" class="text-center py-5"><div class="spinner-border text-primary"></div></div>

    <div v-else>
      <div class="card border-0 shadow-sm mb-4" v-if="recoveryStatus && recoveryStatus.inGracePeriod">
        <div class="card-body">
          <h5 class="mb-2 text-danger">Payment Recovery In Progress</h5>
          <p class="small mb-1">
            Grace period ends on <strong>{{ formatDate(recoveryStatus.gracePeriodEndsAt) }}</strong>
            ({{ recoveryStatus.graceDaysRemaining }} day{{ recoveryStatus.graceDaysRemaining === 1 ? '' : 's' }} left).
          </p>
          <p class="small text-muted mb-3">{{ recoveryStatus.suggestedAction }}</p>
          <div class="d-flex gap-2 flex-wrap">
            <button class="btn btn-warning btn-sm" :disabled="retryingRecovery" @click="retryRecoveryPayment">
              {{ retryingRecovery ? 'Retrying...' : 'Retry Payment Now' }}
            </button>
            <button class="btn btn-outline-primary btn-sm" @click="savePaymentMethod">Update Payment Method</button>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4" v-if="current">
        <div class="card-body">
          <h5 class="mb-1">Current Plan: {{ current.plan.planName }}</h5>
          <p class="text-muted small mb-2">
            {{ current.billingCycle }} | {{ current.status }} | Payment: {{ current.paymentStatus }}
          </p>
          <p class="small mb-1">Active till: {{ formatDate(current.endDate) }}</p>
          <p class="small mb-1">Next billing date: <strong>{{ formatDate(current.nextBillingDate || current.endDate) }}</strong></p>
          <p class="small mb-0" v-if="current.paymentMethodDisplay">Payment method: {{ current.paymentMethodDisplay }}</p>

          <div class="mt-3 d-flex gap-2 flex-wrap">
            <button v-if="!current.cancelAtPeriodEnd" class="btn btn-outline-danger btn-sm" @click="cancelSubscription">Cancel at Period End</button>
            <button v-else class="btn btn-outline-success btn-sm" @click="reactivateSubscription">Reactivate</button>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4" v-if="checkoutContext">
        <div class="card-body">
          <h5 class="mb-2">Checkout Confirmation</h5>
          <p class="mb-2 small text-muted">
            Provider: <strong>{{ checkoutContext.paymentProvider || 'N/A' }}</strong>
          </p>
          <p class="mb-2 small text-muted" v-if="checkoutContext.providerPaymentId">
            Payment Reference: <code>{{ checkoutContext.providerPaymentId }}</code>
          </p>
          <p class="mb-2 small" v-if="checkoutContext.paymentMessage">{{ checkoutContext.paymentMessage }}</p>

          <div class="alert alert-warning small mb-0" v-if="checkoutContext.paymentStatus === 'Pending'">
            Payment is pending. Complete payment with your selected provider using the reference above.
            This page auto-refreshes status every 10 seconds while pending.
          </div>

          <div class="alert alert-success small mb-0" v-else-if="checkoutContext.paymentStatus === 'Succeeded'">
            Payment has been recorded successfully. Your subscription is active.
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body">
          <h5 class="mb-3">Change or Start Plan</h5>
          <div class="row g-2">
            <div class="col-md-6">
              <label class="form-label">Plan</label>
              <select class="form-select" v-model.number="form.planId">
                <option v-for="p in plans" :key="p.planId" :value="p.planId">
                  {{ p.planName }} ({{ isCustomPlan(p.planName) ? formatMoney(p.priceMonthly, p.planName) : `${formatMoney(p.priceMonthly, p.planName)}/month` }})
                </option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">Billing</label>
              <select class="form-select" v-model="form.billingCycle">
                <option value="monthly">monthly</option>
                <option value="yearly">yearly</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">Provider</label>
              <select class="form-select" v-model="form.paymentProvider">
                <option value="stripe">Stripe</option>
                <option value="razorpay">Razorpay</option>
                <option value="paypal">PayPal</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">Currency</label>
              <input class="form-control" v-model="form.currency" />
            </div>
            <div class="col-md-9 d-flex align-items-end">
              <button class="btn btn-primary" :disabled="submitting" @click="subscribe">
                <span v-if="submitting" class="spinner-border spinner-border-sm me-1"></span>
                Subscribe
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4" v-if="selectedPlan">
        <div class="card-body">
          <h6 class="fw-semibold mb-2">Selected Plan Preview</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <div class="small text-muted">Plan</div>
              <div class="fw-semibold">{{ selectedPlan.planName }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Monthly</div>
              <div class="fw-semibold">{{ formatMoney(selectedPlan.priceMonthly, selectedPlan.planName) }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Analytics</div>
              <div class="fw-semibold">{{ selectedPlan.analyticsAccessLevel }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Search limit</div>
              <div class="fw-semibold">{{ selectedPlan.maxCreatorSearch ?? 'Unlimited' }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Export</div>
              <div class="fw-semibold">{{ selectedPlan.exportAllowed ? 'Enabled' : 'Not included' }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Billing Cycle</div>
              <div class="fw-semibold text-capitalize">{{ form.billingCycle }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body">
          <h6 class="fw-semibold mb-3">Payment Method</h6>
          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label small">Brand</label>
              <input class="form-control" v-model="paymentMethod.brand" placeholder="Visa / Mastercard" />
            </div>
            <div class="col-md-3">
              <label class="form-label small">Last 4 Digits</label>
              <input class="form-control" v-model="paymentMethod.last4" maxlength="4" placeholder="4242" />
            </div>
            <div class="col-md-5">
              <button class="btn btn-outline-primary" @click="savePaymentMethod">Update Payment Method</button>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4" v-if="billingSummary">
        <div class="card-body">
          <h6 class="fw-semibold mb-3">Billing Summary</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <div class="small text-muted">Current cycle amount</div>
              <div class="fw-semibold">{{ formatMoney(billingSummary.currentCycleAmount, billingSummary.currentPlanName) }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Next billing date</div>
              <div class="fw-semibold">{{ formatDate(billingSummary.nextBillingDate) }}</div>
            </div>
            <div class="col-md-4">
              <div class="small text-muted">Proration preview</div>
              <div class="fw-semibold">{{ billingSummary.prorationPreviewAmount == null ? '-' : formatMoney(billingSummary.prorationPreviewAmount, '') }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body">
          <h6 class="fw-semibold mb-3">Invoices</h6>
          <div class="table-responsive">
            <table class="table table-sm align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>Date</th>
                  <th>Provider</th>
                  <th>Status</th>
                  <th>Amount</th>
                  <th class="text-end">Receipt</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in invoices" :key="item.invoiceId">
                  <td>{{ formatDate(item.createdAt) }}</td>
                  <td>{{ item.provider }}</td>
                  <td>{{ item.status }}</td>
                  <td>{{ formatMoney(item.amount, '') }}</td>
                  <td class="text-end">
                    <button class="btn btn-outline-secondary btn-sm" @click="downloadReceipt(item.invoiceId)">Download</button>
                  </td>
                </tr>
                <tr v-if="invoices.length === 0">
                  <td colspan="5" class="text-center text-muted">No invoices yet.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div v-if="message" class="alert alert-success">{{ message }}</div>
      <div v-if="error" class="alert alert-danger">{{ error }}</div>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';
import { trackFunnelEvent } from '../services/funnel';

const route = useRoute();
const loading = ref(false);
const submitting = ref(false);
const message = ref('');
const error = ref('');
const plans = ref([]);
const current = ref(null);
const checkoutContext = ref(null);
const invoices = ref([]);
const billingSummary = ref(null);
const recoveryStatus = ref(null);
const retryingRecovery = ref(false);
const paymentMethod = ref({ brand: 'Card', last4: '' });
let pendingPollTimer = null;

const form = ref({
  planId: null,
  billingCycle: 'monthly',
  paymentProvider: 'stripe',
  currency: 'INR'
});

const selectedPlan = computed(() => plans.value.find(p => p.planId === form.value.planId) || null);

onMounted(async () => {
  await loadPlans();
  await loadCurrent();
  await loadInvoices();
  await loadBillingSummary();
  await loadRecoveryStatus();

  const fromQuery = Number(route.query.planId || 0);
  if (fromQuery) {
    form.value.planId = fromQuery;
  } else if (plans.value.length > 0 && !form.value.planId) {
    form.value.planId = plans.value[0].planId;
  }

  syncPendingPolling();
});

onBeforeUnmount(() => {
  stopPendingPolling();
});

async function loadPlans() {
  loading.value = true;
  try {
    const res = await api.get('/plans');
    plans.value = res.data || [];
  } catch (e) {
    error.value = e.response?.data?.error || 'Unable to fetch plans.';
  } finally {
    loading.value = false;
  }
}

async function loadCurrent() {
  try {
    const res = await api.get('/subscriptions/current');
    current.value = res.data;
    checkoutContext.value = res.data;
    paymentMethod.value.brand = current.value?.paymentMethodDisplay?.split(' ')[0] || 'Card';
    syncPendingPolling();
  } catch {
    current.value = null;
    checkoutContext.value = null;
    stopPendingPolling();
  }

  await loadRecoveryStatus();
}

async function subscribe() {
  submitting.value = true;
  message.value = '';
  error.value = '';

  try {
    const idempotencyKey = makeIdempotencyKey('checkout');
    const res = await api.post('/subscriptions/subscribe', form.value, {
      headers: {
        'Idempotency-Key': idempotencyKey
      }
    });
    current.value = res.data;
    checkoutContext.value = res.data;
    message.value = 'Subscription updated successfully.';
    await trackFunnelEvent('subscription_conversion', {
      planId: form.value.planId,
      billingCycle: form.value.billingCycle,
      provider: form.value.paymentProvider,
    });
    await loadInvoices();
    await loadBillingSummary();
    syncPendingPolling();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to subscribe.';
  } finally {
    submitting.value = false;
  }
}

async function loadInvoices() {
  try {
    const res = await api.get('/subscriptions/invoices');
    invoices.value = res.data || [];
  } catch {
    invoices.value = [];
  }
}

async function loadBillingSummary() {
  try {
    const params = {
      targetPlanId: form.value.planId || undefined,
      billingCycle: form.value.billingCycle,
    };
    const res = await api.get('/subscriptions/billing-summary', { params });
    billingSummary.value = res.data;
  } catch {
    billingSummary.value = null;
  }
}

async function loadRecoveryStatus() {
  try {
    const res = await api.get('/subscriptions/recovery-status');
    recoveryStatus.value = res.data || null;
  } catch {
    recoveryStatus.value = null;
  }
}

async function savePaymentMethod() {
  try {
    const idempotencyKey = makeIdempotencyKey('payment-method');
    const res = await api.post('/subscriptions/payment-method', paymentMethod.value, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
    current.value = res.data;
    message.value = 'Payment method updated.';
  } catch (e) {
    error.value = e.response?.data?.error || 'Unable to update payment method.';
  }
}

async function cancelSubscription() {
  try {
    const idempotencyKey = makeIdempotencyKey('cancel');
    const res = await api.post('/subscriptions/cancel', { reason: 'User requested cancellation' }, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
    current.value = res.data;
    message.value = 'Cancellation scheduled at period end.';
  } catch (e) {
    error.value = e.response?.data?.error || 'Unable to cancel subscription.';
  }
}

async function reactivateSubscription() {
  try {
    const idempotencyKey = makeIdempotencyKey('reactivate');
    const res = await api.post('/subscriptions/reactivate', { reason: 'User requested reactivation' }, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
    current.value = res.data;
    message.value = 'Subscription reactivated.';
  } catch (e) {
    error.value = e.response?.data?.error || 'Unable to reactivate subscription.';
  }
}

async function retryRecoveryPayment() {
  retryingRecovery.value = true;
  error.value = '';
  message.value = '';

  try {
    const idempotencyKey = makeIdempotencyKey('retry-payment');
    const res = await api.post('/subscriptions/retry-payment', {}, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
    current.value = res.data;
    checkoutContext.value = res.data;
    message.value = 'Payment retry succeeded. Premium access is restored.';
    await loadInvoices();
    await loadBillingSummary();
    await loadRecoveryStatus();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to retry payment.';
  } finally {
    retryingRecovery.value = false;
  }
}

async function downloadReceipt(invoiceId) {
  try {
    const res = await api.get(`/subscriptions/invoices/${invoiceId}/receipt`, { responseType: 'blob' });
    const blob = new Blob([res.data], { type: 'text/plain;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `receipt-${invoiceId}.txt`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  } catch {
    error.value = 'Failed to download receipt.';
  }
}

function syncPendingPolling() {
  const isPending = checkoutContext.value?.paymentStatus === 'Pending';
  if (!isPending) {
    stopPendingPolling();
    return;
  }

  if (pendingPollTimer) return;

  pendingPollTimer = setInterval(async () => {
    await loadCurrent();
  }, 10000);
}

function stopPendingPolling() {
  if (pendingPollTimer) {
    clearInterval(pendingPollTimer);
    pendingPollTimer = null;
  }
}

function isCustomPlan(planName) {
  return (planName || '').toLowerCase() === 'enterprise';
}

function formatMoney(value, planName = '') {
  if (isCustomPlan(planName)) return 'Custom pricing';

  const num = Number(value || 0);
  if (num === 0) return 'Free';
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(num);
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString();
}

function makeIdempotencyKey(scope) {
  return `${scope}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}
</script>

<style scoped>
.subscription-shell {
  animation: fade-up 0.35s ease both;
}
</style>
