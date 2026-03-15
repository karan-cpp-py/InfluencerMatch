<template>
  <div class="container-fluid py-4">
    <!-- Header -->
    <div class="row mb-4">
      <div class="col">
        <h2 class="fw-bold mb-1">
          <span style="font-size:1.4rem">🚀</span> Rising Creators
        </h2>
        <p class="text-muted mb-0">Creators with the fastest-growing subscriber bases</p>
      </div>
    </div>

    <!-- Filters -->
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body">
        <div class="row g-3 align-items-end">
          <div class="col-md-3">
            <label class="form-label fw-semibold small">Growth Category</label>
            <select v-model="filters.growthCategory" class="form-select form-select-sm">
              <option value="">All Categories</option>
              <option value="Rising">Rising (5%+/mo)</option>
              <option value="Stable">Stable</option>
              <option value="Declining">Declining</option>
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
              <option :value="25">25</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" @click="fetchRising" :disabled="loading">
              <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
              Search
            </button>
          </div>
          <div class="col-md-2">
            <button class="btn btn-outline-secondary btn-sm w-100" @click="triggerRecalc" :disabled="recalcLoading">
              <span v-if="recalcLoading" class="spinner-border spinner-border-sm me-1"></span>
              Recalculate
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Stats row -->
    <div v-if="creators.length" class="row g-3 mb-4">
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3">
          <div class="fs-3 fw-bold text-success">{{ risingCount }}</div>
          <div class="small text-muted">Rising Creators</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3">
          <div class="fs-3 fw-bold text-primary">{{ (avgGrowth * 100).toFixed(1) }}%</div>
          <div class="small text-muted">Avg Growth Rate</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3">
          <div class="fs-3 fw-bold text-info">{{ compact(totalDelta) }}</div>
          <div class="small text-muted">Total Subscriber Gains</div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-0 shadow-sm text-center py-3">
          <div class="fs-3 fw-bold text-warning">{{ avgEngagementInfo.formatted }}</div>
          <span
            v-if="avgEngagementInfo.badgeText"
            class="badge mt-1"
            :class="avgEngagementInfo.badgeClass"
            :title="avgEngagementInfo.tooltip"
            style="font-size:10px;"
          >
            {{ avgEngagementInfo.badgeText }}
          </span>
          <div class="small text-muted">Avg Engagement</div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-if="error" class="alert alert-danger">{{ error }}</div>

    <!-- Table -->
    <div v-if="creators.length" class="card border-0 shadow-sm">
      <div class="card-header bg-white py-3">
        <h6 class="mb-0 fw-semibold">{{ creators.length }} Creator{{ creators.length !== 1 ? 's' : '' }} Found</h6>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th>#</th>
              <th>Creator</th>
              <th>Category</th>
              <th>Country</th>
              <th>Subscribers</th>
              <th>Growth Rate</th>
              <th>+/- Subs (30d)</th>
              <th>Engagement</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(c, idx) in creators" :key="c.creatorId">
              <td class="text-muted">{{ idx + 1 }}</td>
              <td>
                <div class="d-flex align-items-center gap-2">
                  <div class="avatar-circle" :style="avatarStyle(c.category)">
                    {{ (c.channelName || '?')[0].toUpperCase() }}
                  </div>
                  <div>
                    <div class="fw-semibold small">{{ c.channelName }}</div>
                    <div class="text-muted" style="font-size:.75rem">{{ c.platform }}</div>
                  </div>
                </div>
              </td>
              <td>
                <span class="badge rounded-pill" :style="`background:${catColor(c.category)}22;color:${catColor(c.category)};`">
                  {{ c.category || '—' }}
                </span>
              </td>
              <td>{{ flagEmoji(c.country) }} {{ c.country || '—' }}</td>
              <td class="fw-semibold">{{ compact(c.subscribers) }}</td>
              <td>
                <div class="d-flex align-items-center gap-2">
                  <div class="growth-bar-bg">
                    <div class="growth-bar-fill" :style="growthBarStyle(c.growthRate)"></div>
                  </div>
                  <span :class="growthClass(c.growthRate)" class="small fw-bold">
                    {{ growthText(c.growthRate) }}
                  </span>
                </div>
              </td>
              <td>
                <span :class="c.subscriberDelta >= 0 ? 'text-success' : 'text-danger'" class="fw-semibold small">
                  {{ c.subscriberDelta >= 0 ? '+' : '' }}{{ compact(c.subscriberDelta) }}
                </span>
              </td>
              <td>
                <div>{{ creatorEngagementMeta(c).formatted }}</div>
                <span
                  v-if="creatorEngagementMeta(c).badgeText"
                  class="badge"
                  :class="creatorEngagementMeta(c).badgeClass"
                  :title="creatorEngagementMeta(c).tooltip"
                  style="font-size:10px;"
                >
                  {{ creatorEngagementMeta(c).badgeText }}
                </span>
              </td>
              <td>
                <span class="badge" :class="categoryBadgeClass(c.growthCategory)">
                  {{ c.growthCategory }}
                </span>
              </td>
              <td>
                <router-link :to="`/creator/${c.creatorId}/analytics`" class="btn btn-sm btn-outline-primary py-0 px-2">
                  View
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else-if="!loading" class="text-center py-5 text-muted">
      <div style="font-size:3rem">📈</div>
      <p class="mt-2">No creators found. Try relaxing the filters or clicking <strong>Recalculate</strong> first.</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../services/api'
import { engagementMeta } from '../utils/engagement'

const creators = ref([])
const loading  = ref(false)
const recalcLoading = ref(false)
const error    = ref('')

const filters = ref({ growthCategory: '', country: '', topN: 50 })

const COLORS = {
  tech: '#3b82f6', fitness: '#10b981', gaming: '#8b5cf6',
  food: '#f59e0b', travel: '#06b6d4', fashion: '#ec4899',
  education: '#14b8a6', music: '#f97316', comedy: '#a855f7',
  beauty: '#e879f9', finance: '#22c55e', default: '#64748b'
}
const catColor = cat => COLORS[(cat || '').toLowerCase()] || COLORS.default
const avatarStyle = cat => ({
  background: catColor(cat) + '22', color: catColor(cat),
  width: '36px', height: '36px', borderRadius: '50%',
  display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: '700', fontSize: '.85rem', flexShrink: '0'
})
const compact = n => {
  if (n == null) return '—'
  const abs = Math.abs(n)
  const sign = n < 0 ? '-' : ''
  if (abs >= 1e9) return sign + (abs / 1e9).toFixed(1) + 'B'
  if (abs >= 1e6) return sign + (abs / 1e6).toFixed(1) + 'M'
  if (abs >= 1e3) return sign + (abs / 1e3).toFixed(1) + 'K'
  return sign + abs
}
const flagEmoji = code => {
  if (!code || code.length !== 2) return ''
  return String.fromCodePoint(...[...code.toUpperCase()].map(c => 0x1F1E0 - 65 + c.charCodeAt(0)))
}
const growthBarStyle = rate => {
  const pct = Math.min(Math.abs(rate) * 400, 100)
  const color = rate >= 0.10 ? '#10b981' : rate >= 0.03 ? '#3b82f6' : rate <= -0.02 ? '#ef4444' : '#94a3b8'
  return { width: pct + '%', background: color, height: '100%', borderRadius: '2px', transition: 'width .3s' }
}
const growthClass = rate => rate >= 0.10 ? 'text-success' : rate >= 0.03 ? 'text-primary' : rate <= -0.02 ? 'text-danger' : 'text-muted'
const categoryBadgeClass = cat =>
  cat === 'Rising' ? 'bg-success' : cat === 'Stable' ? 'bg-primary' : 'bg-danger'

function growthText(rate) {
  const n = Number(rate)
  if (!Number.isFinite(n)) return 'Not available'
  return `${(n * 100).toFixed(1)}%`
}

// Stats
const risingCount   = computed(() => creators.value.filter(c => c.growthCategory === 'Rising').length)
const avgGrowth     = computed(() => creators.value.length ? creators.value.reduce((s, c) => s + c.growthRate, 0) / creators.value.length : 0)
const totalDelta    = computed(() => creators.value.reduce((s, c) => s + (c.subscriberDelta || 0), 0))
const avgEngagement = computed(() => creators.value.length ? creators.value.reduce((s, c) => s + c.engagementRate, 0) / creators.value.length : 0)
const avgEngagementInfo = computed(() => engagementMeta(avgEngagement.value, {
  mode: 'ratio',
  sampleCount: creators.value.length,
  minSampleCount: 3,
  fallback: '—'
}))

function creatorEngagementMeta(creator) {
  return engagementMeta(creator?.engagementRate, {
    mode: 'ratio',
    sampleCount: creator?.videoCount,
    minSampleCount: 3,
    fallback: '—'
  })
}

async function fetchRising() {
  loading.value = true
  error.value = ''
  try {
    const params = { topN: filters.value.topN }
    if (filters.value.growthCategory) params.growthCategory = filters.value.growthCategory
    if (filters.value.country)        params.country        = filters.value.country
    const res = await api.get('/creators/rising', { params })
    creators.value = res.data
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.message || 'Failed to load rising creators.'
  } finally {
    loading.value = false
  }
}

async function triggerRecalc() {
  recalcLoading.value = true
  try {
    await api.post('/creators/rising/recalculate')
    await fetchRising()
  } catch (e) {
    error.value = 'Recalculation failed: ' + (e?.response?.data?.message || e.message)
  } finally {
    recalcLoading.value = false
  }
}

onMounted(fetchRising)
</script>

<style scoped>
.growth-bar-bg { width: 60px; height: 8px; background: #e2e8f0; border-radius: 2px; overflow: hidden; }
.avatar-circle  { flex-shrink: 0; }
</style>
