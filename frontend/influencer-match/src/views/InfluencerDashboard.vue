<template>
  <div class="influencer-dashboard-page py-4">
    <div class="container" style="max-width: 920px;">
      <section class="profile-hero mb-4">
        <div>
          <p class="hero-kicker mb-2">Creator Identity</p>
          <h3 class="mb-1 fw-bold">Influencer Profile</h3>
          <p class="mb-0 text-light-emphasis">Welcome, {{ userName }}. Keep your profile fresh to improve campaign visibility.</p>
        </div>
        <router-link class="btn btn-light btn-sm fw-semibold" to="/campaigns">Browse Campaigns</router-link>
      </section>

      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <article class="metric-card">
            <p class="metric-label">Followers</p>
            <p class="metric-value">{{ compact(influencer.followers) }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="metric-card">
            <p class="metric-label">Engagement</p>
            <p class="metric-value">{{ pct(influencer.engagementRate) }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="metric-card">
            <p class="metric-label">Category</p>
            <p class="metric-value small-value">{{ influencer.category || '-' }}</p>
          </article>
        </div>
        <div class="col-6 col-md-3">
          <article class="metric-card">
            <p class="metric-label">Price / Post</p>
            <p class="metric-value">${{ Number(influencer.pricePerPost || 0).toLocaleString() }}</p>
          </article>
        </div>
      </div>

      <div class="card border-0 shadow-sm form-card">
        <div class="card-body p-4 p-md-5">
          <p class="text-muted mb-4">Update your information to appear in campaign searches.</p>
          <form @submit.prevent="submit" class="mt-1">
            <div class="row">
              <div class="col-md-6 mb-3">
                <label class="form-label fw-semibold">Instagram Link</label>
                <input v-model="influencer.instagramLink" class="form-control form-control-lg" />
              </div>
              <div class="col-md-6 mb-3">
                <label class="form-label fw-semibold">YouTube Link</label>
                <input v-model="influencer.youTubeLink" class="form-control form-control-lg" />
              </div>
            </div>

            <div class="row">
              <div class="col-md-4 mb-3">
                <label class="form-label fw-semibold">Followers</label>
                <input type="number" v-model="influencer.followers" class="form-control" />
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label fw-semibold">Engagement Rate</label>
                <input type="number" step="0.01" v-model="influencer.engagementRate" class="form-control" />
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label fw-semibold">Price Per Post</label>
                <input type="number" step="0.01" v-model="influencer.pricePerPost" class="form-control" />
              </div>
            </div>

            <div class="mb-3">
              <label class="form-label fw-semibold">Category</label>
              <input v-model="influencer.category" class="form-control" />
            </div>
            <div class="mb-3">
              <label class="form-label fw-semibold">Location</label>
              <input v-model="influencer.location" class="form-control" />
            </div>
            <button class="btn btn-primary btn-lg w-100" type="submit">Save Profile</button>
          </form>
          <div v-if="message" class="alert alert-success mt-3">{{ message }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import api from '../services/api';
import { parseJwt } from '../services/jwt';

const influencer = ref({
  instagramLink: '',
  youTubeLink: '',
  followers: 0,
  engagementRate: 0,
  category: '',
  location: '',
  pricePerPost: 0
});

const message = ref('');

const userName = computed(() => {
  const token = localStorage.getItem('token');
  if (!token) return '';
  const p = parseJwt(token);
  return p.unique_name || p.name || '';
});

onMounted(async () => {
  const token = localStorage.getItem('token');
  const payload = parseJwt(token);
  try {
    const resAll = await api.get('/influencer/all');
    const me = resAll.data.find(i => i.userId == payload.nameid);
    if (me) influencer.value = me;
  } catch (e) {
    console.error(e);
  }
});

async function submit() {
  try {
    const token = localStorage.getItem('token');
    const payload = parseJwt(token);
    influencer.value.userId = payload.nameid;
    // if we have an id stored, include it so API knows update vs create
    if (influencer.value.influencerId) {
      await api.put(`/influencer/${influencer.value.influencerId}`, influencer.value);
      message.value = 'Updated successfully';
    } else {
      const res = await api.post('/influencer', influencer.value);
      influencer.value.influencerId = res.data.influencerId;
      message.value = 'Saved successfully';
    }
  } catch (err) {
    console.error(err);
  }
}

function compact(n) {
  const v = Number(n || 0);
  if (v >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M';
  if (v >= 1_000) return (v / 1_000).toFixed(1) + 'K';
  return String(v);
}

function pct(v) {
  if (v == null || Number.isNaN(Number(v))) return '-';
  const n = Number(v);
  const percent = n <= 1 ? n * 100 : n;
  return percent.toFixed(2) + '%';
}
</script>

<style scoped>
.influencer-dashboard-page {
  background: radial-gradient(circle at 8% 0%, rgba(14, 165, 233, 0.08), transparent 40%),
    radial-gradient(circle at 92% 14%, rgba(16, 185, 129, 0.08), transparent 36%);
}

.profile-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #dbeafe;
  background: linear-gradient(124deg, #0f172a 0%, #1e3a8a 58%, #0369a1 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.56rem;
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.25);
}

.metric-card {
  border-radius: 14px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: #fff;
  box-shadow: 0 8px 18px rgba(15, 23, 42, 0.06);
  padding: 0.75rem;
  height: 100%;
}

.metric-label {
  margin-bottom: 0.1rem;
  font-size: 0.72rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #64748b;
}

.metric-value {
  margin-bottom: 0;
  font-weight: 700;
  font-size: 1.2rem;
}

.small-value {
  font-size: 1rem;
}

.form-card {
  border-radius: 18px;
}

@media (max-width: 768px) {
  .profile-hero {
    flex-direction: column;
  }
}
</style>