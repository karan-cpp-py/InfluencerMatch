<template>
  <div class="influencer-detail-page py-4">
    <div class="container" style="max-width: 960px;">
      <div v-if="loading" class="text-center py-5">
        <div class="spinner-border text-primary" role="status"></div>
      </div>

      <div v-else-if="influencer">
        <section class="detail-hero mb-4">
          <div>
            <p class="hero-kicker mb-2">Influencer Profile</p>
            <h3 class="mb-1 fw-bold">{{ influencer.userName || 'Influencer' }}</h3>
            <p class="mb-0 text-light-emphasis">Category: {{ influencer.category || '-' }} · Location: {{ influencer.location || '-' }}</p>
          </div>
          <router-link to="/influencers" class="btn btn-light btn-sm fw-semibold">Back to List</router-link>
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
              <p class="metric-value">{{ influencerEngagementMeta.formatted }}</p>
              <span
                v-if="influencerEngagementMeta.badgeText"
                class="badge mt-1"
                :class="influencerEngagementMeta.badgeClass"
                :title="influencerEngagementMeta.tooltip"
              >
                {{ influencerEngagementMeta.badgeText }}
              </span>
            </article>
          </div>
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Price / Post</p>
              <p class="metric-value">${{ Number(influencer.pricePerPost || 0).toLocaleString() }}</p>
            </article>
          </div>
          <div class="col-6 col-md-3">
            <article class="metric-card">
              <p class="metric-label">Profile ID</p>
              <p class="metric-value">#{{ influencer.influencerId }}</p>
            </article>
          </div>
        </div>

        <div class="card shadow-sm border-0 detail-card mb-3">
          <div class="card-body p-4">
            <h5 class="fw-semibold mb-3">Profile Details</h5>
            <div class="row g-3">
              <div class="col-md-6">
                <p class="detail-item"><span>Followers</span><strong>{{ Number(influencer.followers || 0).toLocaleString() }}</strong></p>
                <p class="detail-item">
                  <span>Engagement Rate</span>
                  <strong class="d-inline-flex align-items-center gap-1">
                    {{ influencerEngagementMeta.formatted }}
                    <span
                      v-if="influencerEngagementMeta.badgeText"
                      class="badge"
                      :class="influencerEngagementMeta.badgeClass"
                      :title="influencerEngagementMeta.tooltip"
                      style="font-size:10px;"
                    >
                      {{ influencerEngagementMeta.badgeText }}
                    </span>
                  </strong>
                </p>
                <p class="detail-item"><span>Category</span><strong>{{ influencer.category || '-' }}</strong></p>
                <p class="detail-item"><span>Location</span><strong>{{ influencer.location || '-' }}</strong></p>
                <p class="detail-item"><span>Price per post</span><strong>${{ Number(influencer.pricePerPost || 0).toLocaleString() }}</strong></p>
              </div>
              <div class="col-md-6">
                <div class="link-box">
                  <p class="mb-1 fw-semibold">Social Links</p>
                  <p class="mb-2">
                    <strong>Instagram:</strong>
                    <a :href="safeUrl(influencer.instagramLink)" target="_blank" rel="noopener" class="ms-1">Profile</a>
                  </p>
                  <p class="mb-0">
                    <strong>YouTube:</strong>
                    <a :href="safeUrl(influencer.youTubeLink)" target="_blank" rel="noopener" class="ms-1">Channel</a>
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="text-center py-5 text-muted">
        Influencer not found.
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';
import { engagementMeta } from '../utils/engagement';

const route = useRoute();
const influencer = ref(null);
const loading = ref(false);

const influencerEngagementMeta = computed(() => {
  const i = influencer.value;
  if (!i) return engagementMeta(null);

  const sampleCandidates = [i.totalVideos, i.videoCount, i.totalPosts, i.postCount];
  let sampleCount = null;
  for (const count of sampleCandidates) {
    const n = Number(count);
    if (Number.isFinite(n) && n >= 0) {
      sampleCount = n;
      break;
    }
  }

  return engagementMeta(i.engagementRate, {
    mode: 'auto',
    sampleCount,
    minSampleCount: 3,
    fallback: '—'
  });
});

onMounted(async () => {
  const id = route.params.id;
  loading.value = true;
  try {
    const res = await api.get(`/influencer/${id}`);
    influencer.value = res.data;
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
});

function compact(n) {
  const v = Number(n || 0);
  if (v >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M';
  if (v >= 1_000) return (v / 1_000).toFixed(1) + 'K';
  return String(v);
}

function safeUrl(url) {
  const value = String(url || '').trim();
  if (!value) return '#';
  if (value.startsWith('http://') || value.startsWith('https://')) return value;
  return `https://${value}`;
}
</script>

<style scoped>
.influencer-detail-page {
  background: radial-gradient(circle at 10% 0%, rgba(14, 165, 233, 0.08), transparent 40%),
    radial-gradient(circle at 90% 14%, rgba(16, 185, 129, 0.07), transparent 36%);
}

.detail-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #dbeafe;
  background: linear-gradient(122deg, #0f172a 0%, #1e3a8a 60%, #0369a1 100%);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.55rem;
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.24);
}

.metric-card {
  border-radius: 14px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: #fff;
  box-shadow: 0 7px 16px rgba(15, 23, 42, 0.06);
  padding: 0.75rem;
  height: 100%;
}

.metric-label {
  margin-bottom: 0.1rem;
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #64748b;
}

.metric-value {
  margin-bottom: 0;
  font-size: 1.2rem;
  font-weight: 700;
  color: #0f172a;
}

.detail-card {
  border-radius: 18px;
}

.detail-item {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  border-bottom: 1px dashed rgba(148, 163, 184, 0.35);
  padding-bottom: 0.4rem;
  margin-bottom: 0.55rem;
}

.detail-item span {
  color: #64748b;
}

.link-box {
  border-radius: 12px;
  border: 1px solid rgba(148, 163, 184, 0.28);
  background: #f8fafc;
  padding: 0.75rem;
}

@media (max-width: 768px) {
  .detail-hero {
    flex-direction: column;
  }
}
</style>
