<template>
  <div class="brand-opportunities-page container-fluid py-4">
    <!-- Header -->
    <section class="opportunities-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Collaboration Pipeline</p>
        <h2 class="fw-bold mb-1">Brand Opportunity Finder</h2>
        <p class="mb-0">Discover the best creator partners ranked by engagement, growth, and reach.</p>
      </div>
    </section>

    <!-- Filters -->
    <div class="card border-0 shadow-sm mb-4 panel-card">
      <div class="card-body">
        <div class="row g-3 align-items-end">
          <div class="col-md-3">
            <label class="form-label fw-semibold small">Brand / Creator Category</label>
            <select v-model="filters.brandCategory" class="form-select form-select-sm">
              <option value="">All Categories</option>
              <option v-for="cat in CATEGORIES" :key="cat" :value="cat">{{ cat }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label fw-semibold small">Country</label>
            <select v-model="filters.country" class="form-select form-select-sm">
              <option value="">All Countries</option>
              <option value="IN">🇮🇳 India</option>
              <option value="US">🇺🇸 USA</option>
              <option value="GB">🇬🇧 UK</option>
              <option value="CA">🇨🇦 Canada</option>
              <option value="AU">🇦🇺 Australia</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label fw-semibold small">Top N</label>
            <select v-model="filters.topN" class="form-select form-select-sm">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" @click="findOpportunities" :disabled="loading">
              <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
              Find Opportunities
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Stats -->
    <div v-if="results.length" class="row g-3 mb-4">
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3 panel-card">
          <div class="fs-3 fw-bold text-primary">{{ results.length }}</div>
          <div class="small text-muted">Opportunities Found</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3 panel-card">
          <div class="fs-3 fw-bold text-success">{{ (topOpportunityScore * 100).toFixed(1) }}%</div>
          <div class="small text-muted">Top Opportunity Score</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3 panel-card">
          <div class="fs-3 fw-bold text-warning">${{ formatPrice(avgEstimatedPrice) }}</div>
          <div class="small text-muted">Avg Est. Price (USD)</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3 panel-card">
          <div class="fs-3 fw-bold text-info">{{ compact(totalReach) }}</div>
          <div class="small text-muted">Combined Subscribers</div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-if="error" class="alert alert-danger">{{ error }}</div>

    <!-- Grid cards -->
    <div v-if="results.length" class="row g-3">
      <div v-for="(c, idx) in results" :key="c.creatorId" class="col-md-6 col-xl-4">
        <div class="card border-0 shadow-sm h-100 panel-card opportunity-card">
          <div class="card-body">
            <!-- Rank badge + header -->
            <div class="d-flex align-items-center gap-3 mb-3">
              <div class="rank-badge" :style="rankBadgeStyle(idx)">
                #{{ idx + 1 }}
              </div>
              <div class="avatar-circle" :style="avatarStyle(c.category)">
                {{ (c.channelName || '?')[0].toUpperCase() }}
              </div>
              <div class="flex-grow-1 min-w-0">
                <div class="fw-semibold text-truncate">{{ c.channelName }}</div>
                <div class="small text-muted">{{ c.platform }} · {{ flagEmoji(c.country) }} {{ c.country || '—' }}</div>
              </div>
              <span class="badge" :class="categoryBadgeClass(c.growthCategory)">{{ c.growthCategory }}</span>
            </div>

            <!-- Opportunity score bar -->
            <div class="mb-3">
              <div class="d-flex justify-content-between small mb-1">
                <span class="text-muted fw-semibold">Opportunity Score</span>
                <span class="fw-bold" :style="`color:${scoreColor(c.opportunityScore)}`">
                  {{ (c.opportunityScore * 100).toFixed(1) }}%
                </span>
              </div>
              <div class="score-bar-bg">
                <div class="score-bar-fill" :style="scoreBarStyle(c.opportunityScore)"></div>
              </div>
            </div>

            <!-- Metrics row -->
            <div class="row g-2 text-center mb-3">
              <div class="col-4">
                <div class="p-2 rounded" style="background:#f8fafc">
                  <div class="fw-bold small">{{ compact(c.subscribers) }}</div>
                  <div style="font-size:.7rem" class="text-muted">Subscribers</div>
                </div>
              </div>
              <div class="col-4">
                <div class="p-2 rounded" style="background:#f8fafc">
                  <div class="fw-bold small">{{ (c.engagementRate * 100).toFixed(2) }}%</div>
                  <div style="font-size:.7rem" class="text-muted">Engagement</div>
                </div>
              </div>
              <div class="col-4">
                <div class="p-2 rounded" style="background:#f8fafc">
                  <div class="fw-bold small" :class="c.growthRate >= 0 ? 'text-success' : 'text-danger'">
                    {{ c.growthRate >= 0 ? '+' : '' }}{{ (c.growthRate * 100).toFixed(1) }}%
                  </div>
                  <div style="font-size:.7rem" class="text-muted">Growth</div>
                </div>
              </div>
            </div>

            <!-- Price -->
            <div class="d-flex justify-content-between align-items-center small border-top pt-2">
              <div>
                <span class="text-muted">Est. Price: </span>
                <span class="fw-bold text-success">${{ formatPrice(c.estimatedPrice) }}</span>
                <span class="text-muted ms-1">/ ₹{{ formatPrice(c.estimatedPrice * 83) }}</span>
              </div>
              <router-link :to="`/creator/${c.creatorId}/analytics`" class="btn btn-sm btn-outline-primary py-0 px-2">
                View Analytics
              </router-link>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else-if="!loading && searched" class="text-center py-5 text-muted">
      <div style="font-size:3rem">🔍</div>
      <p class="mt-2">No opportunities found for the selected filters.</p>
    </div>
    <div v-else-if="!loading && !searched" class="text-center py-5 text-muted">
      <div style="font-size:3rem">💡</div>
      <p class="mt-2">Set your filters and click <strong>Find Opportunities</strong> to discover the best creator partners.</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../services/api'

const results = ref([])
const loading = ref(false)
const error   = ref('')
const searched = ref(false)

const filters = ref({ brandCategory: '', country: '', topN: 20 })

const CATEGORIES = [
  'Tech', 'Fitness', 'Gaming', 'Food', 'Travel', 'Fashion',
  'Education', 'Music', 'Comedy', 'Beauty', 'Finance'
]

const COLORS = {
  tech: '#3b82f6', fitness: '#10b981', gaming: '#8b5cf6',
  food: '#f59e0b', travel: '#06b6d4', fashion: '#ec4899',
  education: '#14b8a6', music: '#f97316', comedy: '#a855f7',
  beauty: '#e879f9', finance: '#22c55e', default: '#64748b'
}
const catColor    = cat => COLORS[(cat || '').toLowerCase()] || COLORS.default
const avatarStyle = cat => ({
  background: catColor(cat) + '22', color: catColor(cat),
  width: '40px', height: '40px', borderRadius: '50%',
  display: 'flex', alignItems: 'center', justifyContent: 'center',
  fontWeight: '700', fontSize: '.9rem', flexShrink: '0'
})
const rankBadgeStyle = idx => ({
  background: idx === 0 ? '#f59e0b' : idx === 1 ? '#94a3b8' : idx === 2 ? '#b45309' : '#e2e8f0',
  color: idx < 3 ? '#fff' : '#64748b',
  width: '32px', height: '32px', borderRadius: '50%', flexShrink: '0',
  display: 'flex', alignItems: 'center', justifyContent: 'center',
  fontSize: '.7rem', fontWeight: '800'
})
const compact = n => {
  if (n == null) return '—'
  const abs = Math.abs(n)
  if (abs >= 1e9) return (abs / 1e9).toFixed(1) + 'B'
  if (abs >= 1e6) return (abs / 1e6).toFixed(1) + 'M'
  if (abs >= 1e3) return (abs / 1e3).toFixed(1) + 'K'
  return abs
}
const flagEmoji = code => {
  if (!code || code.length !== 2) return ''
  return String.fromCodePoint(...[...code.toUpperCase()].map(c => 0x1F1E0 - 65 + c.charCodeAt(0)))
}
const formatPrice = p => {
  if (!p) return '0'
  if (p >= 1000) return (p / 1000).toFixed(1) + 'K'
  return p.toFixed(0)
}
const scoreColor = s => s >= 0.7 ? '#10b981' : s >= 0.4 ? '#3b82f6' : '#94a3b8'
const scoreBarStyle = s => ({
  width: (s * 100) + '%',
  background: `linear-gradient(90deg, ${scoreColor(s)}, ${scoreColor(s)}88)`,
  height: '100%', borderRadius: '4px', transition: 'width .4s'
})
const categoryBadgeClass = cat =>
  cat === 'Rising' ? 'bg-success' : cat === 'Stable' ? 'bg-primary' : cat === 'Declining' ? 'bg-danger' : 'bg-secondary'

// Stats
const topOpportunityScore = computed(() => results.value[0]?.opportunityScore ?? 0)
const avgEstimatedPrice   = computed(() => results.value.length ? results.value.reduce((s, c) => s + c.estimatedPrice, 0) / results.value.length : 0)
const totalReach          = computed(() => results.value.reduce((s, c) => s + c.subscribers, 0))

async function findOpportunities() {
  loading.value  = true
  error.value    = ''
  searched.value = true
  try {
    const res = await api.post('/brands/opportunities', {
      brandCategory: filters.value.brandCategory || null,
      country:       filters.value.country       || null,
      topN:          filters.value.topN
    })
    results.value = res.data
  } catch (e) {
    error.value = e?.response?.data?.message || 'Failed to load opportunities.'
  } finally {
    loading.value = false
  }
}

onMounted(() => findOpportunities())
</script>

<style scoped>
.score-bar-bg { width: 100%; height: 10px; background: #e2e8f0; border-radius: 4px; overflow: hidden; }
.avatar-circle { flex-shrink: 0; }

.brand-opportunities-page {
  background: radial-gradient(circle at 8% 0%, rgba(14, 165, 233, 0.08), transparent 40%),
    radial-gradient(circle at 92% 16%, rgba(34, 197, 94, 0.07), transparent 35%);
}

.opportunities-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #dbeafe;
  background: linear-gradient(122deg, #0f172a 0%, #1e3a8a 58%, #0ea5e9 100%);
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.2);
}

.hero-kicker {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.6rem;
  font-size: 0.7rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  background: rgba(147, 197, 253, 0.25);
}

.panel-card {
  border-radius: 16px;
}

.opportunity-card {
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.opportunity-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 24px rgba(15, 23, 42, 0.11) !important;
}
</style>
