<template>
  <div class="container py-5" style="max-width: 860px;">
    <div class="card border-0 shadow-sm">
      <div class="card-body p-4 p-md-5">
        <div class="text-center">
          <PhaseBadge :phase="platformConfig.phase" />
        </div>
        <h2 class="fw-bold mb-2">Brand Activation is Invite-Only</h2>
        <p class="text-muted mb-3 text-center">
          We are currently scaling high-quality creator intelligence before opening full brand marketplace access.
        </p>

        <div class="alert alert-info text-start small mb-4">
          <strong>Current strategy:</strong> {{ platformConfig.positioningLine }}
          <br />
          Active creators: <strong>{{ platformConfig.kpiGates.activeCreatorsWeekly }}</strong>
          / Target: <strong>{{ platformConfig.kpiGates.brandActivationCreatorThreshold }}</strong>
        </div>

        <form class="row g-3 mb-4" @submit.prevent="submitWaitlist">
          <div class="col-md-6">
            <label class="form-label">Work Email</label>
            <input v-model.trim="form.email" type="email" class="form-control" required placeholder="team@company.com" />
          </div>
          <div class="col-md-6">
            <label class="form-label">Company Name</label>
            <input v-model.trim="form.companyName" type="text" class="form-control" required placeholder="Acme Brands" />
          </div>
          <div class="col-12">
            <label class="form-label">What do you want to launch first?</label>
            <textarea v-model.trim="form.notes" class="form-control" rows="3" placeholder="Tell us your creator categories, campaign goals, and expected timelines."></textarea>
          </div>
          <div class="col-12 d-flex align-items-center gap-2">
            <button class="btn btn-primary" :disabled="submitting" type="submit">
              {{ submitting ? 'Submitting...' : 'Join Brand Waitlist' }}
            </button>
            <span v-if="feedback" class="small" :class="isError ? 'text-danger' : 'text-success'">{{ feedback }}</span>
          </div>
        </form>

        <div class="d-flex justify-content-center gap-2 flex-wrap">
          <router-link class="btn btn-primary" to="/creator-dashboard">Open Creator Intelligence</router-link>
          <router-link class="btn btn-outline-primary" to="/creators/leaderboard">View Creator Graph</router-link>
          <router-link class="btn btn-outline-secondary" to="/">Back Home</router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue';
import PhaseBadge from '../components/PhaseBadge.vue';
import { joinBrandWaitlist, platformConfig } from '../services/platform';

const submitting = ref(false);
const feedback = ref('');
const isError = ref(false);
const form = reactive({
  email: '',
  companyName: '',
  notes: '',
});

async function submitWaitlist() {
  feedback.value = '';
  isError.value = false;
  submitting.value = true;

  try {
    const result = await joinBrandWaitlist({
      email: form.email,
      companyName: form.companyName,
      notes: form.notes,
    });

    feedback.value = result?.message || 'Waitlist request submitted.';
    form.notes = '';
  } catch (error) {
    isError.value = true;
    feedback.value = error?.response?.data?.error || 'Unable to submit waitlist request right now.';
  } finally {
    submitting.value = false;
  }
}
</script>
