<template>
  <div class="yt-importer-page py-4">
    <div class="container-fluid" style="max-width:1100px;margin:0 auto;">

      <!-- Header -->
      <div class="d-flex align-items-center gap-3 mb-4">
        <router-link to="/admin" class="btn btn-sm btn-outline-secondary">← Back</router-link>
        <div>
          <h2 class="fw-bold mb-0">YouTube Creator Importer</h2>
          <p class="text-muted mb-0 small">Search YouTube for creators and import their channel data into the platform database.</p>
        </div>
      </div>

      <!-- How it works -->
      <div class="alert alert-info d-flex gap-2 align-items-start mb-4" style="font-size:13px;">
        <span style="font-size:18px;">ℹ️</span>
        <div>
          <strong>How it works:</strong> Enter a search query (e.g. "cooking hindi india") and an optional country code.
          The importer will call YouTube Data API, fetch channel stats, parse emails / Instagram / Twitter handles from
          public descriptions, and upsert the results into the Creators table. Each run costs ~150 YouTube API quota units.
          <br><strong>Email sending is disabled</strong> — creator invite emails are not sent during development.
        </div>
      </div>

      <!-- Import Form -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-header fw-semibold bg-white border-bottom">Import Settings</div>
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-5">
              <label class="form-label small fw-semibold">Search Query <span class="text-danger">*</span></label>
              <input v-model="form.query" class="form-control" placeholder='e.g. "cooking recipes hindi" or "varanasi tiles business"' />
              <div class="form-text">Best results: include niche + language + region in the query.</div>
            </div>
            <div class="col-md-2">
              <label class="form-label small fw-semibold">Country Code</label>
              <input v-model="form.countryCode" class="form-control" placeholder="IN" maxlength="2" style="text-transform:uppercase;" />
              <div class="form-text">ISO-2, e.g. IN, US</div>
            </div>
            <div class="col-md-2">
              <label class="form-label small fw-semibold">Category</label>
              <select v-model="form.category" class="form-select">
                <option value="">Auto-detect</option>
                <option v-for="cat in categoryOptions" :key="cat" :value="cat">{{ cat }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small fw-semibold">Max Results</label>
              <select v-model.number="form.maxResults" class="form-select">
                <option :value="5">5</option>
                <option :value="10">10</option>
                <option :value="20">20 (default)</option>
                <option :value="30">30</option>
                <option :value="50">50 (max)</option>
              </select>
            </div>
            <div class="col-md-1 d-flex align-items-end">
              <button class="btn btn-primary w-100" :disabled="importing || !form.query.trim()" @click="runImport">
                <span v-if="importing" class="spinner-border spinner-border-sm me-1"></span>
                <span v-else>Run</span>
              </button>
            </div>
          </div>

          <!-- Quick presets -->
          <div class="mt-3">
            <span class="small text-muted me-2">Quick presets:</span>
            <button v-for="p in presets" :key="p.label"
                    class="btn btn-outline-secondary btn-sm me-1 mb-1"
                    @click="applyPreset(p)">{{ p.label }}</button>
          </div>

          <!-- Error / success message -->
          <div v-if="importError" class="alert alert-danger mt-3 mb-0 py-2">{{ importError }}</div>
          <div v-if="importResult" class="alert alert-success mt-3 mb-0 py-2">
            ✅ Import complete —
            <strong>{{ importResult.imported }} new</strong>,
            <strong>{{ importResult.updated }} updated</strong>,
            <strong>{{ importResult.skipped }} skipped</strong>.
            Quota used today: {{ importResult.quotaUsed }}/9000 units.
          </div>
        </div>
      </div>

      <!-- Import Results Table -->
      <div v-if="importResult && importResult.rows.length" class="card border-0 shadow-sm mb-4">
        <div class="card-header fw-semibold bg-white border-bottom d-flex justify-content-between align-items-center">
          <span>Import Results ({{ importResult.rows.length }} channels)</span>
          <span class="badge bg-secondary">{{ fmtTime(importResult.timestamp) }}</span>
        </div>
        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Channel</th>
                <th>Subscribers</th>
                <th>Avg Views</th>
                <th>Engagement</th>
                <th>Country / Category</th>
                <th>Contact Found</th>
                <th>Status</th>
                <th>Error</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in importResult.rows" :key="row.channelId">
                <td>
                  <div class="d-flex align-items-center gap-2">
                    <img v-if="row.thumbnailUrl" :src="row.thumbnailUrl" class="rounded-circle" style="width:32px;height:32px;object-fit:cover" />
                    <div v-else class="rounded-circle bg-secondary d-flex align-items-center justify-content-center text-white" style="width:32px;height:32px;font-size:12px">YT</div>
                    <div>
                      <div class="fw-semibold small">{{ row.channelName }}</div>
                      <div class="text-muted" style="font-size:10px;">{{ row.channelId }}</div>
                    </div>
                  </div>
                </td>
                <td>{{ fmtNum(row.subscribers) }}</td>
                <td>{{ row.avgViews ? fmtNum(row.avgViews) : '—' }}</td>
                <td>
                  <span v-if="row.engagementRate" class="badge"
                    :class="row.engagementRate >= 3 ? 'bg-success' : row.engagementRate >= 1 ? 'bg-warning text-dark' : 'bg-secondary'">
                    {{ row.engagementRate.toFixed(2) }}%
                  </span>
                  <span v-else class="text-muted small">—</span>
                </td>
                <td>{{ row.country || '—' }} / {{ row.category || '—' }}</td>
                <td>
                  <span v-if="row.email" class="badge bg-success me-1" title="Email found">📧</span>
                  <span v-if="isValidInstagramHandle(row.instagramHandle)" class="badge bg-warning text-dark me-1" title="Instagram found">📸</span>
                  <span v-if="isValidTwitterHandle(row.twitterHandle)" class="badge bg-info text-dark me-1" title="X/Twitter found">🐦</span>
                  <span v-if="!row.email && !isValidInstagramHandle(row.instagramHandle) && !isValidTwitterHandle(row.twitterHandle)" class="text-muted small">None</span>
                </td>
                <td>
                  <span class="badge" :class="statusBadge(row.status)" :title="row.error || ''">{{ row.status }}</span>
                </td>
                <td class="small text-danger" style="max-width:240px">
                  {{ row.error || '—' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Browse Creators Database -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-header fw-semibold bg-white border-bottom d-flex justify-content-between align-items-center flex-wrap gap-2">
          <span>Creators Database ({{ totalCreators.toLocaleString() }} total)</span>
          <button class="btn btn-outline-primary btn-sm" :disabled="browsing" @click="loadCreators(1)">
            {{ browsing ? 'Loading...' : 'Refresh' }}
          </button>
        </div>
        <div class="card-body pb-2">
          <!-- Filters -->
          <div class="row g-2 mb-3">
            <div class="col-md-4">
              <input v-model="browseFilters.search" class="form-control form-control-sm"
                     placeholder="Search by name..." @keyup.enter="loadCreators(1)" />
            </div>
            <div class="col-md-2">
              <select v-model="browseFilters.tier" class="form-select form-select-sm" @change="loadCreators(1)">
                <option value="">All tiers</option>
                <option value="Nano">Nano</option>
                <option value="Micro">Micro</option>
                <option value="MidTier">MidTier</option>
                <option value="Macro">Macro</option>
                <option value="Mega">Mega</option>
              </select>
            </div>
            <div class="col-md-2">
              <select v-model="browseFilters.country" class="form-select form-select-sm" @change="loadCreators(1)">
                <option value="">All countries</option>
                <option value="IN">India (IN)</option>
                <option value="US">USA (US)</option>
                <option value="GB">UK (GB)</option>
                <option value="CA">Canada (CA)</option>
                <option value="AU">Australia (AU)</option>
              </select>
            </div>
            <div class="col-md-2">
              <select v-model="browseFilters.category" class="form-select form-select-sm" @change="loadCreators(1)">
                <option value="">All categories</option>
                <option v-for="cat in categoryOptions" :key="cat" :value="cat">{{ cat }}</option>
              </select>
            </div>
            <div class="col-md-2 d-flex gap-1">
              <button class="btn btn-primary btn-sm flex-fill" @click="loadCreators(1)">Filter</button>
              <button class="btn btn-outline-secondary btn-sm" @click="clearFilters">Clear</button>
            </div>
          </div>

          <!-- Error -->
          <div v-if="browseError" class="alert alert-danger py-2 mb-2">{{ browseError }}</div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>Channel</th>
                  <th>Subscribers</th>
                  <th>Engagement</th>
                  <th>Avg Views</th>
                  <th>Category</th>
                  <th>Country</th>
                  <th>Contacts</th>
                  <th>Refreshed</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="browsing">
                  <td colspan="9" class="text-center py-3 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>Loading…
                  </td>
                </tr>
                <tr v-else-if="!creators.length">
                  <td colspan="9" class="text-center py-3 text-muted">No creators found. Run an import above to populate the database.</td>
                </tr>
                <tr v-for="(c, idx) in creators" :key="c.creatorId" v-else>
                  <td class="text-muted small">{{ (browsePage - 1) * browsePageSize + idx + 1 }}</td>
                  <td>
                    <div class="d-flex align-items-center gap-2">
                      <img v-if="c.thumbnailUrl" :src="c.thumbnailUrl" class="rounded-circle" style="width:30px;height:30px;object-fit:cover;flex-shrink:0" />
                      <div v-else class="rounded-circle bg-secondary d-flex align-items-center justify-content-center text-white flex-shrink-0" style="width:30px;height:30px;font-size:10px">YT</div>
                      <div class="overflow-hidden">
                        <div class="fw-semibold small text-truncate" style="max-width:150px">{{ c.channelName }}</div>
                        <a :href="c.channelUrl || 'https://youtube.com/channel/' + c.channelId" target="_blank"
                           rel="noopener noreferrer" class="text-muted" style="font-size:10px;">{{ c.channelId }}</a>
                      </div>
                    </div>
                  </td>
                  <td>{{ fmtNum(c.subscribers) }}<br><span class="badge" :class="tierBadge(c.subscribers)" style="font-size:9px">{{ c.creatorTier || computeTier(c.subscribers) }}</span></td>
                  <td>
                    <span v-if="c.engagementRate"
                      :class="c.engagementRate >= 3 ? 'text-success fw-semibold' : c.engagementRate >= 1 ? 'text-warning' : 'text-muted'">
                      {{ c.engagementRate?.toFixed(2) }}%
                    </span>
                    <span v-else class="text-muted">—</span>
                  </td>
                  <td>{{ c.avgViews ? fmtNum(c.avgViews) : '—' }}</td>
                  <td>{{ c.category || '—' }}</td>
                  <td>{{ c.country || '—' }}</td>
                  <td>
                    <div v-if="c.publicEmail" class="small"><span class="text-muted">📧</span> {{ c.publicEmail }}</div>
                    <div v-if="isValidInstagramHandle(c.instagramHandle)" class="small"><span class="text-muted">📸</span> @{{ c.instagramHandle }}</div>
                    <div v-if="isValidTwitterHandle(c.twitterHandle)" class="small"><span class="text-muted">🐦</span> @{{ c.twitterHandle }}</div>
                    <span v-if="!c.publicEmail && !isValidInstagramHandle(c.instagramHandle) && !isValidTwitterHandle(c.twitterHandle)" class="text-muted small">—</span>
                  </td>
                  <td class="text-muted small">{{ c.lastRefreshedAt ? fmtDate(c.lastRefreshedAt) : fmtDate(c.createdAt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          <div class="d-flex justify-content-between align-items-center mt-3 flex-wrap gap-2">
            <div class="small text-muted">
              Page {{ browsePage }} of {{ browsePageCount }} ({{ totalCreators.toLocaleString() }} creators)
            </div>
            <div class="d-flex gap-1">
              <button class="btn btn-outline-secondary btn-sm" :disabled="browsePage <= 1" @click="loadCreators(browsePage - 1)">‹ Prev</button>
              <button class="btn btn-outline-secondary btn-sm" :disabled="browsePage >= browsePageCount" @click="loadCreators(browsePage + 1)">Next ›</button>
            </div>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../services/api.js'

// ── Form state ──────────────────────────────────────────────────────────
const form = ref({
  query: '',
  countryCode: 'IN',
  category: '',
  maxResults: 20,
})

const importing    = ref(false)
const importError  = ref(null)
const importResult = ref(null)

// ── Browse state ────────────────────────────────────────────────────────
const creators      = ref([])
const totalCreators = ref(0)
const browsePage    = ref(1)
const browsePageSize = 50
const browsing      = ref(false)
const browseError   = ref(null)
const browseFilters = ref({ search: '', tier: '', country: '', category: '' })

const browsePageCount = computed(() => Math.max(1, Math.ceil(totalCreators.value / browsePageSize)))

// ── Constants ───────────────────────────────────────────────────────────
const categoryOptions = [
  'Food', 'Tech', 'Fashion', 'Fitness', 'Travel', 'Gaming',
  'Education', 'Entertainment', 'Music', 'Finance', 'General',
]

const presets = [
  { label: '🍲 Food India', query: 'cooking food recipe hindi india', countryCode: 'IN', category: 'Food' },
  { label: '💻 Tech Hindi', query: 'tech gadgets phones hindi review', countryCode: 'IN', category: 'Tech' },
  { label: '👗 Fashion India', query: 'fashion style beauty hindi india', countryCode: 'IN', category: 'Fashion' },
  { label: '💪 Fitness India', query: 'fitness gym workout hindi', countryCode: 'IN', category: 'Fitness' },
  { label: '✈️ Travel India', query: 'travel vlog india hindi', countryCode: 'IN', category: 'Travel' },
  { label: '🎓 Education Hindi', query: 'education study learning hindi', countryCode: 'IN', category: 'Education' },
  { label: '🏠 Home Decor India', query: 'home decor tiles marble interior india', countryCode: 'IN', category: 'General' },
  { label: '💼 Business Hindi', query: 'business finance money hindi india', countryCode: 'IN', category: 'Finance' },
]

// ── Methods ─────────────────────────────────────────────────────────────
async function runImport() {
  if (!form.value.query.trim()) return
  importing.value   = true
  importError.value = null
  importResult.value = null

  try {
    const payload = {
      query:       form.value.query.trim(),
      countryCode: form.value.countryCode.trim().toUpperCase() || null,
      category:    form.value.category || null,
      maxResults:  form.value.maxResults,
    }
    const { data } = await api.post('/admin/jobs/youtube-import', payload)
    importResult.value = data
    // Refresh the browse table after a successful import
    await loadCreators(1)
  } catch (e) {
    importError.value = e?.userMessage || e?.response?.data?.error || 'Import failed. Check backend logs and API key.'
  } finally {
    importing.value = false
  }
}

async function loadCreators(page = 1) {
  browsing.value  = true
  browseError.value = null
  browsePage.value  = page

  try {
    const params = {
      page,
      pageSize: browsePageSize,
      search:   browseFilters.value.search || undefined,
      tier:     browseFilters.value.tier || undefined,
      country:  browseFilters.value.country || undefined,
      category: browseFilters.value.category || undefined,
    }
    const { data } = await api.get('/admin/creators', { params })
    creators.value      = data.items ?? []
    totalCreators.value = data.total ?? 0
  } catch (e) {
    browseError.value = e?.userMessage || e?.response?.data?.error || 'Failed to load creators.'
  } finally {
    browsing.value = false
  }
}

function clearFilters() {
  browseFilters.value = { search: '', tier: '', country: '', category: '' }
  loadCreators(1)
}

function applyPreset(p) {
  form.value.query       = p.query
  form.value.countryCode = p.countryCode ?? 'IN'
  form.value.category    = p.category ?? ''
}

// ── Formatters ──────────────────────────────────────────────────────────
function fmtNum(n) {
  if (n == null) return '—'
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M'
  if (n >= 1_000)     return (n / 1_000).toFixed(1) + 'K'
  return n.toString()
}

function fmtTime(ts) {
  if (!ts) return ''
  return new Date(ts).toLocaleString()
}

function fmtDate(ts) {
  if (!ts) return '—'
  return new Date(ts).toLocaleDateString()
}

function computeTier(subs) {
  if (subs >= 5_000_000) return 'Mega'
  if (subs >= 500_000)   return 'Macro'
  if (subs >= 100_000)   return 'MidTier'
  if (subs >= 10_000)    return 'Micro'
  return 'Nano'
}

function tierBadge(subs) {
  const tier = computeTier(subs)
  return {
    Mega:    'bg-danger',
    Macro:   'bg-warning text-dark',
    MidTier: 'bg-primary',
    Micro:   'bg-info text-dark',
    Nano:    'bg-secondary',
  }[tier] ?? 'bg-secondary'
}

function statusBadge(status) {
  return {
    new:     'bg-success',
    updated: 'bg-primary',
    skipped: 'bg-secondary',
  }[status] ?? 'bg-secondary'
}

function isValidInstagramHandle(handle) {
  if (!handle) return false
  const h = String(handle).trim().replace(/^@+/, '').toLowerCase()
  if (!/^[a-z0-9._]{2,30}$/.test(h)) return false
  if (h.startsWith('.') || h.endsWith('.')) return false
  if (h.endsWith('.com') || h.endsWith('.net') || h.endsWith('.org') || h.endsWith('.in') || h.endsWith('.co')) return false
  if (['gmail', 'yahoo', 'hotmail', 'outlook', 'protonmail'].includes(h)) return false
  return true
}

function isValidTwitterHandle(handle) {
  if (!handle) return false
  const h = String(handle).trim().replace(/^@+/, '').toLowerCase()
  if (!/^[a-z0-9_]{2,15}$/.test(h)) return false
  return true
}

// ── Lifecycle ───────────────────────────────────────────────────────────
onMounted(() => loadCreators(1))
</script>

<style scoped>
.yt-importer-page {
  background: #f8f9fa;
  min-height: 100vh;
}
</style>
