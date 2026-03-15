<template>
  <div class="brand-campaign py-4">
    <div class="container" style="max-width: 940px;">
      <section class="campaign-hero mb-4">
        <div>
          <p class="eyebrow mb-2">Campaign Studio</p>
          <h3 class="mb-1 fw-bold">{{ campaign.campaignId ? 'Edit Campaign' : 'Create Campaign' }}</h3>
          <p class="mb-0 text-light-emphasis">Define your budget, audience niche, and location targets to find better creator matches.</p>
        </div>
        <div class="hero-tip">
          <p class="tip-title">Quick tip</p>
          <p class="mb-0">Higher budgets and sharper category targeting usually produce stronger match quality.</p>
        </div>
      </section>

      <div class="row g-3">
        <div class="col-lg-8">
          <div class="card border-0 shadow-sm form-card">
            <div class="card-body p-4 p-md-5">
              <form @submit.prevent="submit">
                <div class="mb-4">
                  <label class="form-label fw-semibold">Budget (USD)</label>
                  <input type="number" step="0.01" min="1" v-model.number="campaign.budget" class="form-control form-control-lg" placeholder="2500" />
                </div>

                <div class="mb-4">
                  <label class="form-label fw-semibold">Category</label>
                  <input v-model="campaign.category" class="form-control form-control-lg" placeholder="e.g. Tech, Fashion, Fitness" />
                  <div class="mt-2 d-flex flex-wrap gap-2">
                    <button type="button" v-for="tag in categoryPresets" :key="tag" class="chip-btn" @click="campaign.category = tag">{{ tag }}</button>
                  </div>
                </div>

                <div class="mb-4">
                  <label class="form-label fw-semibold">Target Location</label>
                  <input v-model="campaign.targetLocation" class="form-control form-control-lg" placeholder="India, UAE, US Metro Cities" />
                </div>

                <button class="btn btn-primary btn-lg w-100" type="submit">
                  {{ campaign.campaignId ? 'Update and Continue' : 'Create and Find Matches' }}
                </button>
                <div v-if="formError" class="alert alert-warning mt-3 mb-0 py-2">{{ formError }}</div>
              </form>
            </div>
          </div>
        </div>

        <div class="col-lg-4">
          <div class="card border-0 shadow-sm summary-card h-100">
            <div class="card-body p-4">
              <p class="small text-uppercase text-muted fw-semibold mb-2">Live Summary</p>
              <h5 class="fw-bold mb-3">Campaign Snapshot</h5>
              <div class="summary-line">
                <span>Budget</span>
                <strong>${{ (Number(campaign.budget) || 0).toLocaleString() }}</strong>
              </div>
              <div class="summary-line">
                <span>Category</span>
                <strong>{{ campaign.category || 'Not set' }}</strong>
              </div>
              <div class="summary-line">
                <span>Location</span>
                <strong>{{ campaign.targetLocation || 'Not set' }}</strong>
              </div>
              <hr />
              <p class="small text-muted mb-0">After saving, you will be redirected to creator match results for this campaign.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import api from '../services/api';
import { parseJwt } from '../services/jwt';

const router = useRouter();
const route = useRoute();
const campaign = ref({ budget: 0, category: '', targetLocation: '' });
const formError = ref('');
const categoryPresets = ['Technology', 'Fashion', 'Finance', 'Food', 'Gaming', 'Travel'];

onMounted(async () => {
  const id = route.query.campaignId;
  if (id) {
    try {
      const res = await api.get(`/campaign/${id}`);
      campaign.value = res.data;
    } catch (e) {
      console.error(e);
    }
  }
});

async function submit() {
  formError.value = '';

  const budget = Number(campaign.value.budget || 0);
  const category = String(campaign.value.category || '').trim();
  const location = String(campaign.value.targetLocation || '').trim();

  if (!Number.isFinite(budget) || budget < 1) {
    formError.value = 'Please enter a valid budget greater than 0.';
    return;
  }
  if (category.length < 2) {
    formError.value = 'Please enter a campaign category (at least 2 characters).';
    return;
  }
  if (location.length < 2) {
    formError.value = 'Please enter a target location (at least 2 characters).';
    return;
  }

  try {
    const token = localStorage.getItem('token');
    const payload = parseJwt(token);
    campaign.value.brandId = payload.nameid;
    let res;
    if (campaign.value.campaignId) {
      res = await api.put(`/campaign/${campaign.value.campaignId}`, campaign.value);
    } else {
      res = await api.post('/campaign', campaign.value);
    }
    router.push(`/results/${res.data.campaignId}`);
  } catch (err) {
    formError.value = err?.userMessage || err?.response?.data?.error || 'Could not save campaign. Please check your inputs and try again.';
  }
}
</script>

<style scoped>
.brand-campaign {
  background: radial-gradient(circle at top left, rgba(14, 165, 233, 0.12), transparent 40%),
    radial-gradient(circle at bottom right, rgba(34, 197, 94, 0.1), transparent 45%);
}

.campaign-hero {
  border-radius: 22px;
  padding: 1.2rem;
  background: linear-gradient(130deg, #0f172a, #1d4ed8 55%, #0284c7);
  color: #f8fafc;
  display: flex;
  gap: 1rem;
  align-items: flex-start;
  justify-content: space-between;
}

.eyebrow {
  border-radius: 999px;
  font-size: 0.72rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.28);
  color: #dbeafe;
  padding: 0.2rem 0.55rem;
  display: inline-flex;
}

.hero-tip {
  max-width: 280px;
  border: 1px solid rgba(191, 219, 254, 0.35);
  background: rgba(30, 64, 175, 0.35);
  border-radius: 14px;
  padding: 0.75rem;
  font-size: 0.82rem;
}

.tip-title {
  margin-bottom: 0.2rem;
  font-weight: 700;
}

.form-card,
.summary-card {
  border-radius: 20px;
}

.chip-btn {
  border: 1px solid #dbeafe;
  background: #eff6ff;
  color: #1e3a8a;
  border-radius: 999px;
  padding: 0.2rem 0.68rem;
  font-size: 0.76rem;
  font-weight: 600;
}

.chip-btn:hover {
  background: #dbeafe;
}

.summary-line {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}

@media (max-width: 768px) {
  .campaign-hero {
    flex-direction: column;
  }

  .hero-tip {
    max-width: 100%;
  }
}
</style>