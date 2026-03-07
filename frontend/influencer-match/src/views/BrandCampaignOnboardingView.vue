<template>
  <div class="container py-4" style="max-width: 980px;">
    <section class="wizard-hero mb-4">
      <div>
        <p class="kicker mb-2">Activation</p>
        <h3 class="fw-bold mb-1">First Campaign Wizard</h3>
        <p class="mb-0 text-light-emphasis">Launch your first campaign quickly with guided defaults and best-practice hints.</p>
      </div>
      <router-link class="btn btn-light btn-sm" to="/brand">Advanced Editor</router-link>
    </section>

    <div class="card border-0 shadow-sm panel-card mb-3" v-if="wizard">
      <div class="card-body p-3 p-md-4">
        <div class="row g-3 align-items-center">
          <div class="col-md-8">
            <div class="small text-muted">Recommended budget range</div>
            <div class="fw-semibold">INR {{ fmtMoney(wizard.guidance.suggestedBudgetMin) }} - {{ fmtMoney(wizard.guidance.suggestedBudgetMax) }}</div>
            <div class="small text-muted" v-if="wizard.guidance.averagePreviousBudget > 0">Average of your previous campaigns: INR {{ fmtMoney(wizard.guidance.averagePreviousBudget) }}</div>
          </div>
          <div class="col-md-4 text-md-end">
            <span class="badge" :class="wizard.hasCampaign ? 'text-bg-success' : 'text-bg-secondary'">
              {{ wizard.hasCampaign ? 'Returning Campaign Builder' : 'First Campaign' }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm panel-card">
      <div class="card-body p-4">
        <div class="mb-3">
          <div class="small text-muted">Step 1 of 3</div>
          <h5 class="fw-bold mb-0">Campaign Basics</h5>
        </div>

        <div class="row g-3">
          <div class="col-md-4">
            <label class="form-label small">Budget (INR)</label>
            <input type="number" min="0" step="100" v-model.number="campaign.budget" class="form-control" />
          </div>
          <div class="col-md-4">
            <label class="form-label small">Category</label>
            <input v-model.trim="campaign.category" class="form-control" placeholder="Technology" />
            <div class="d-flex flex-wrap gap-2 mt-2" v-if="wizard">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                v-for="cat in wizard.guidance.suggestedCategories"
                :key="cat"
                @click="campaign.category = cat">
                {{ cat }}
              </button>
            </div>
          </div>
          <div class="col-md-4">
            <label class="form-label small">Target Location</label>
            <input v-model.trim="campaign.targetLocation" class="form-control" placeholder="India" />
            <div class="d-flex flex-wrap gap-2 mt-2" v-if="wizard">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                v-for="loc in wizard.guidance.suggestedLocations"
                :key="loc"
                @click="campaign.targetLocation = loc">
                {{ loc }}
              </button>
            </div>
          </div>
        </div>

        <div v-if="error" class="alert alert-danger py-2 mt-3 mb-0">{{ error }}</div>

        <div class="d-flex justify-content-end mt-4 gap-2">
          <router-link to="/onboarding" class="btn btn-outline-secondary">Back</router-link>
          <button class="btn btn-primary" :disabled="saving" @click="createCampaign">
            {{ saving ? 'Creating...' : 'Create Campaign and Find Matches' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';

const router = useRouter();
const wizard = ref(null);
const saving = ref(false);
const error = ref('');

const campaign = ref({
  budget: 100000,
  category: 'Technology',
  targetLocation: 'India',
});

onMounted(loadWizard);

async function loadWizard() {
  try {
    const { data } = await api.get('/onboarding/brand-campaign-wizard');
    wizard.value = data;

    if (data?.latestCampaign) {
      campaign.value.budget = Number(data.latestCampaign.budget || campaign.value.budget);
      campaign.value.category = data.latestCampaign.category || campaign.value.category;
      campaign.value.targetLocation = data.latestCampaign.targetLocation || campaign.value.targetLocation;
    }
  } catch {
    // keep defaults when unavailable
  }
}

async function createCampaign() {
  error.value = '';

  if (!campaign.value.budget || !campaign.value.category || !campaign.value.targetLocation) {
    error.value = 'Budget, category, and target location are required.';
    return;
  }

  saving.value = true;
  try {
    const { data } = await api.post('/campaign', campaign.value);
    await api.post('/funnel/events', {
      eventName: 'CampaignWizardCompleted',
      metadataJson: JSON.stringify({ source: 'brand_campaign_wizard' }),
    });
    router.push(`/results/${data.campaignId}`);
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to create campaign.';
  } finally {
    saving.value = false;
  }
}

function fmtMoney(value) {
  return Number(value || 0).toLocaleString('en-IN');
}
</script>

<style scoped>
.wizard-hero {
  border-radius: 20px;
  padding: 1.15rem;
  color: #e2e8f0;
  background: linear-gradient(124deg, #111827 0%, #1d4ed8 55%, #0f766e 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.kicker {
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

@media (max-width: 768px) {
  .wizard-hero {
    flex-direction: column;
  }
}
</style>
