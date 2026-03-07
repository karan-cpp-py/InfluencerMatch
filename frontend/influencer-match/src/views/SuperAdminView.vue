<template>
  <div class="super-admin-page py-4">
    <div class="container-fluid" style="max-width:1000px;margin:0 auto;">

    <section class="admin-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Operations Console</p>
        <h2 class="fw-bold mb-1">SuperAdmin Control Panel</h2>
        <p class="mb-0 text-light-emphasis">Manually trigger YouTube jobs and monitor platform health in one place.</p>
      </div>
      <button class="btn btn-light btn-sm fw-semibold" @click="refreshDashboard">Refresh Dashboard</button>
    </section>

    <!-- Setup card (shown when no admin exists yet) -->
    <div v-if="showSeedForm" class="card border-warning shadow-sm mb-4 panel-card">
      <div class="card-header bg-warning text-dark fw-bold">First-time Setup - Create SuperAdmin Account</div>
      <div class="card-body">
        <p class="text-muted small mb-3">No SuperAdmin exists yet. Fill in the details to create the first one.</p>
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <input v-model="seed.name" class="form-control" placeholder="Display name" />
          </div>
          <div class="col-md-4">
            <input v-model="seed.email" type="email" class="form-control" placeholder="Email" />
          </div>
          <div class="col-md-4">
            <input v-model="seed.password" type="password" class="form-control" placeholder="Password" />
          </div>
        </div>
        <div v-if="seedMsg" :class="['alert', seedError ? 'alert-danger' : 'alert-success', 'py-2 mb-2']">{{ seedMsg }}</div>
        <button class="btn btn-warning" :disabled="seedLoading" @click="createAdmin">
          <span v-if="seedLoading" class="spinner-border spinner-border-sm me-1"></span>
          Create SuperAdmin
        </button>
      </div>
    </div>

    <!-- Stats cards -->
    <div v-if="stats" class="row g-3 mb-4">
      <div class="col-6 col-md-3" v-for="(val, key) in statCards" :key="key">
        <div class="card border-0 shadow-sm text-center p-3 panel-card">
          <div class="fw-bold fs-4 text-primary">{{ val }}</div>
          <div class="text-muted small">{{ key }}</div>
        </div>
      </div>
      <!-- Quota progress -->
      <div class="col-12">
        <div class="card border-0 shadow-sm p-3 panel-card">
          <div class="d-flex justify-content-between mb-1">
            <span class="small fw-semibold">YouTube API Quota Today</span>
            <span class="small text-muted">{{ stats.quotaUsedToday }} / {{ stats.quotaDailyLimit }} units</span>
          </div>
          <div class="progress" style="height:8px;">
            <div class="progress-bar" :class="quotaBarClass"
                 :style="{width: Math.min(100, (stats.quotaUsedToday / stats.quotaDailyLimit) * 100) + '%'}">
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mb-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Subscription Recovery Queue</h6>
          <button class="btn btn-outline-primary btn-sm" :disabled="recoveryLoading" @click="loadRecoveryQueue">
            {{ recoveryLoading ? 'Loading...' : 'Refresh' }}
          </button>
        </div>

        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <label class="form-label small">Scope</label>
            <select v-model="recoveryFilters.scope" class="form-select form-select-sm" @change="loadRecoveryQueue">
              <option value="grace">Grace period only</option>
              <option value="all">All recovery statuses</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">Payment Status</label>
            <select v-model="recoveryFilters.paymentStatus" class="form-select form-select-sm" @change="loadRecoveryQueue">
              <option value="">All</option>
              <option value="Failed">Failed</option>
              <option value="Pending">Pending</option>
              <option value="Succeeded">Succeeded</option>
            </select>
          </div>
          <div class="col-md-6 d-flex align-items-end">
            <div class="small text-muted">
              Rows: <strong>{{ recoveryRows.length }}</strong>
              <span v-if="recoveryRows.length"> | At risk (<=24h): <strong>{{ recoveryAtRiskCount }}</strong></span>
            </div>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>User</th>
                <th>Plan</th>
                <th>Status</th>
                <th>Grace Ends</th>
                <th>Retries</th>
                <th>Payment Method</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in recoveryRows" :key="`recovery-${row.subscriptionId}`">
                <td>
                  <div class="fw-semibold">{{ row.userName }}</div>
                  <div class="small text-muted">{{ row.userEmail }}</div>
                </td>
                <td class="small">{{ row.planName }}</td>
                <td>
                  <div><span class="badge" :class="row.status === 'GracePeriod' ? 'bg-danger' : 'bg-secondary'">{{ row.status }}</span></div>
                  <div class="small text-muted">Payment: {{ row.paymentStatus }}</div>
                </td>
                <td class="small">
                  <div>{{ row.gracePeriodEndsAt ? fmtDate(row.gracePeriodEndsAt) : '-' }}</div>
                  <div class="text-muted" v-if="row.graceDaysRemaining != null">{{ row.graceDaysRemaining }} day(s) left</div>
                </td>
                <td class="small">
                  <div>{{ row.paymentRetryCount }}</div>
                  <div class="text-muted" v-if="row.lastPaymentRetryAt">Last: {{ fmtDate(row.lastPaymentRetryAt) }}</div>
                </td>
                <td class="small">{{ row.paymentMethodDisplay || '-' }}</td>
                <td>
                  <button
                    class="btn btn-outline-warning btn-sm"
                    :disabled="recoveryOutreachLoadingId === row.subscriptionId"
                    @click="triggerRecoveryOutreach(row)">
                    {{ recoveryOutreachLoadingId === row.subscriptionId ? 'Sending...' : 'Send Outreach' }}
                  </button>
                </td>
              </tr>
              <tr v-if="!recoveryLoading && recoveryRows.length === 0">
                <td colspan="7" class="text-center text-muted py-3">No subscription recovery rows found for this filter.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mb-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Operational Insights</h6>
          <div class="d-flex flex-wrap align-items-center gap-2">
            <span class="small text-muted">Range:</span>
            <button
              v-for="opt in insightRanges"
              :key="`range-${opt.days}`"
              class="btn btn-sm"
              :class="insightRange === opt.days ? 'btn-primary' : 'btn-outline-secondary'"
              :disabled="insightRefreshing"
              @click="applyInsightRange(opt.days)">
              {{ opt.label }}
            </button>
            <span class="small text-muted" v-if="insightRefreshing">Updating...</span>
          </div>
        </div>
        <div class="small text-muted mb-3" v-if="insightRangeLabel">
          Showing {{ insightRangeLabel }}
        </div>
        <div class="row g-3">
          <div class="col-lg-6">
            <div class="chart-block h-100">
              <div class="small fw-semibold mb-2">Funnel Events</div>
              <div class="chart-box">
                <AppBarChart :data="funnelChartData" :options="barOptions" />
              </div>
            </div>
          </div>
          <div class="col-lg-6">
            <div class="chart-block h-100">
              <div class="small fw-semibold mb-2">Enterprise Lead Inflow</div>
              <div class="chart-box">
                <AppLineChart :data="leadTrendChartData" :options="lineOptions" />
              </div>
            </div>
          </div>
          <div class="col-lg-6">
            <div class="chart-block h-100">
              <div class="small fw-semibold mb-2">Waitlist Status Mix</div>
              <div class="chart-box compact">
                <AppDoughnutChart :data="waitlistStatusChartData" :options="doughnutOptions" />
              </div>
            </div>
          </div>
          <div class="col-lg-6">
            <div class="chart-block h-100">
              <div class="small fw-semibold mb-2">Enterprise SLA Health</div>
              <div class="chart-box compact">
                <AppDoughnutChart :data="enterpriseSlaChartData" :options="doughnutOptions" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Job cards -->
    <h5 class="fw-bold mb-3">YouTube API Jobs</h5>
    <div class="row g-3">
      <div class="col-md-6" v-for="job in jobs" :key="job.id">
        <div class="card border-0 shadow-sm h-100 panel-card job-card">
          <div class="card-body">
            <div class="d-flex align-items-start gap-2 mb-2">
              <span class="fs-4">{{ job.icon }}</span>
              <div>
                <h6 class="fw-bold mb-0">{{ job.title }}</h6>
                <p class="text-muted small mb-0">{{ job.description }}</p>
              </div>
            </div>
            <div class="d-flex align-items-center justify-content-between mt-3">
              <span v-if="job.lastRun" class="text-muted" style="font-size:11px;">
                Last run: {{ fmtTime(job.lastRun) }}
              </span>
              <span v-else class="text-muted" style="font-size:11px;">Never run</span>
              <div class="d-flex align-items-center gap-2">
                <span v-if="job.result" class="badge" :class="job.error ? 'bg-danger' : 'bg-success'" style="font-size:10px;">
                  {{ job.result }}
                </span>
                <button class="btn btn-primary btn-sm"
                        :disabled="job.loading"
                        @click="runJob(job)">
                  <span v-if="job.loading" class="spinner-border spinner-border-sm me-1"></span>
                  <span v-else>Run</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mt-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Job Status Table</h6>
          <span class="small text-muted">Live runtime state</span>
        </div>
        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Last Run</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="job in jobs" :key="`table-${job.id}`">
                <td>
                  <div class="fw-semibold">{{ job.title }}</div>
                  <div class="small text-muted">{{ job.id }}</div>
                </td>
                <td>
                  <span class="badge" :class="job.loading ? 'bg-primary' : job.error ? 'bg-danger' : job.result ? 'bg-success' : 'bg-secondary'">
                    {{ job.loading ? 'Running' : job.error ? 'Error' : job.result ? 'Done' : 'Idle' }}
                  </span>
                </td>
                <td class="small text-muted">{{ job.lastRun ? fmtTime(job.lastRun) : 'Never' }}</td>
                <td class="small">{{ job.result || '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mt-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
          <h6 class="fw-semibold mb-0">Funnel Summary</h6>
          <div class="d-flex align-items-center gap-2">
            <label class="small text-muted" for="funnel-days">Range</label>
            <select id="funnel-days" v-model.number="funnelDays" class="form-select form-select-sm" @change="loadFunnelSummary">
              <option :value="7">Last 7 days</option>
              <option :value="14">Last 14 days</option>
              <option :value="30">Last 30 days</option>
              <option :value="60">Last 60 days</option>
              <option :value="90">Last 90 days</option>
            </select>
            <button class="btn btn-outline-primary btn-sm" :disabled="funnelLoading" @click="loadFunnelSummary">
              {{ funnelLoading ? 'Loading...' : 'Refresh' }}
            </button>
          </div>
        </div>

        <div class="small text-muted mb-2" v-if="insightRangeLabel">
          Active window: {{ insightRangeLabel }}
        </div>

        <div class="small text-muted mb-2" v-if="funnelSince">
          Since {{ fmtDate(funnelSince) }} | Total events: <strong>{{ funnelTotal }}</strong>
        </div>
        <div v-if="funnelError" class="alert alert-danger py-2 mb-3">{{ funnelError }}</div>

        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Event</th>
                <th class="text-end">Count</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in funnelRows" :key="row.eventName">
                <td class="fw-semibold">{{ row.eventName }}</td>
                <td class="text-end">{{ row.count }}</td>
              </tr>
              <tr v-if="!funnelLoading && funnelRows.length === 0">
                <td colspan="2" class="text-center text-muted py-3">No funnel events found in this range.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mt-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Brand Waitlist Queue</h6>
          <div class="d-flex gap-2">
            <button class="btn btn-outline-secondary btn-sm" :disabled="waitlistExporting" @click="exportWaitlistCsv">
              {{ waitlistExporting ? 'Exporting...' : 'Export CSV' }}
            </button>
            <button class="btn btn-outline-primary btn-sm" :disabled="waitlistLoading" @click="loadWaitlist">
              {{ waitlistLoading ? 'Loading...' : 'Refresh' }}
            </button>
          </div>
        </div>

        <div class="small text-muted mb-2" v-if="waitlistWindowLabel">
          Active window: {{ waitlistWindowLabel }}
        </div>

        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <label class="form-label small">Status</label>
            <select v-model="waitlistFilters.status" class="form-select form-select-sm" @change="loadWaitlist">
              <option value="">All</option>
              <option value="Pending">Pending</option>
              <option value="Contacted">Contacted</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">Customer Type</label>
            <select v-model="waitlistFilters.customerType" class="form-select form-select-sm" @change="loadWaitlist">
              <option value="">All</option>
              <option value="Brand">Brand</option>
              <option value="Agency">Agency</option>
              <option value="Individual">Individual</option>
              <option value="CreatorManager">CreatorManager</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small">From</label>
            <input v-model="waitlistFilters.from" type="date" class="form-control form-control-sm" @change="waitlistFilters.preset = ''; loadWaitlist()" />
          </div>
          <div class="col-md-2">
            <label class="form-label small">To</label>
            <input v-model="waitlistFilters.to" type="date" class="form-control form-control-sm" @change="waitlistFilters.preset = ''; loadWaitlist()" />
          </div>
          <div class="col-md-12 d-flex flex-wrap gap-2 align-items-center">
            <span class="small text-muted">Quick range:</span>
            <button
              class="btn btn-outline-secondary btn-sm"
              :class="waitlistFilters.preset === 'last7' ? 'active' : ''"
              type="button"
              @click="applyDatePreset('last7')">
              Last 7 days
            </button>
            <button
              class="btn btn-outline-secondary btn-sm"
              :class="waitlistFilters.preset === 'last30' ? 'active' : ''"
              type="button"
              @click="applyDatePreset('last30')">
              Last 30 days
            </button>
            <button
              class="btn btn-outline-secondary btn-sm"
              :class="waitlistFilters.preset === 'thisMonth' ? 'active' : ''"
              type="button"
              @click="applyDatePreset('thisMonth')">
              This month
            </button>
            <button
              class="btn btn-link btn-sm text-decoration-none"
              type="button"
              @click="clearDateFilters">
              Clear
            </button>
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <div class="small text-muted">
              Total: <strong>{{ waitlistTotal }}</strong>
              <span v-if="waitlistSummaryLine"> | {{ waitlistSummaryLine }}</span>
            </div>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Created</th>
                <th>Company</th>
                <th>Email</th>
                <th>Type</th>
                <th>Status</th>
                <th>Notes</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in waitlistItems" :key="entry.brandWaitlistEntryId">
                <td class="small text-muted">{{ fmtDate(entry.createdAt) }}</td>
                <td class="fw-semibold">{{ entry.companyName }}</td>
                <td class="small">{{ entry.email }}</td>
                <td class="small">{{ entry.customerType }}</td>
                <td>
                  <span class="badge" :class="waitlistStatusClass(entry.status)">{{ entry.status }}</span>
                </td>
                <td class="small text-truncate" style="max-width: 220px;">{{ entry.notes || '-' }}</td>
                <td>
                  <select
                    class="form-select form-select-sm"
                    :value="entry.status"
                    @change="updateWaitlistStatus(entry, $event.target.value)">
                    <option value="Pending">Pending</option>
                    <option value="Contacted">Contacted</option>
                    <option value="Approved">Approved</option>
                    <option value="Rejected">Rejected</option>
                  </select>
                </td>
              </tr>
              <tr v-if="!waitlistLoading && waitlistItems.length === 0">
                <td colspan="7" class="text-center text-muted py-3">No waitlist entries found for this filter.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mt-4 panel-card">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Enterprise Lead Pipeline</h6>
          <div class="d-flex gap-2">
            <button class="btn btn-outline-secondary btn-sm" :disabled="enterpriseLeadsExporting" @click="exportEnterpriseLeadsCsv">
              {{ enterpriseLeadsExporting ? 'Exporting...' : 'Export CSV' }}
            </button>
            <button class="btn btn-outline-primary btn-sm" :disabled="enterpriseLeadsLoading" @click="loadEnterpriseLeads">
              {{ enterpriseLeadsLoading ? 'Loading...' : 'Refresh' }}
            </button>
          </div>
        </div>

        <div class="small text-muted mb-2" v-if="enterpriseWindowLabel">
          Active window: {{ enterpriseWindowLabel }}
        </div>

        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <label class="form-label small">Source</label>
            <select v-model="enterpriseLeadFilters.source" class="form-select form-select-sm" @change="loadEnterpriseLeads">
              <option value="">All</option>
              <option value="BookDemo">BookDemo</option>
              <option value="PricingPage">PricingPage</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small">Status</label>
            <select v-model="enterpriseLeadFilters.status" class="form-select form-select-sm" @change="loadEnterpriseLeads">
              <option value="">All</option>
              <option value="New">New</option>
              <option value="Contacted">Contacted</option>
              <option value="Qualified">Qualified</option>
              <option value="ClosedWon">ClosedWon</option>
              <option value="ClosedLost">ClosedLost</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small">Owner</label>
            <select v-model.number="enterpriseLeadFilters.ownerUserId" class="form-select form-select-sm" @change="loadEnterpriseLeads">
              <option :value="0">All</option>
              <option v-for="owner in enterpriseLeadOwners" :key="owner.userId" :value="owner.userId">{{ owner.name }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">From</label>
            <input v-model="enterpriseLeadFilters.from" type="date" class="form-control form-control-sm" @change="loadEnterpriseLeads" />
          </div>
          <div class="col-md-3">
            <label class="form-label small">To</label>
            <input v-model="enterpriseLeadFilters.to" type="date" class="form-control form-control-sm" @change="loadEnterpriseLeads" />
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <div class="small text-muted">
              Total: <strong>{{ enterpriseLeadsTotal }}</strong>
              <span v-if="enterpriseSummaryText"> | {{ enterpriseSummaryText }}</span>
            </div>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>Created</th>
                <th>Lead</th>
                <th>Company</th>
                <th>Source</th>
                <th>Owner</th>
                <th>Status</th>
                <th>SLA</th>
                <th>Notes</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="lead in enterpriseLeads" :key="lead.enterpriseLeadId">
                <td class="small text-muted">{{ fmtDate(lead.createdAt) }}</td>
                <td>
                  <div class="fw-semibold">{{ lead.fullName }}</div>
                  <div class="small text-muted">{{ lead.workEmail }}</div>
                </td>
                <td class="small">{{ lead.companyName }} <span v-if="lead.teamSize" class="text-muted">({{ lead.teamSize }})</span></td>
                <td class="small">{{ lead.source }}</td>
                <td>
                  <select
                    class="form-select form-select-sm"
                    :value="lead.ownerUserId || 0"
                    @change="updateEnterpriseLead(lead, { ownerUserId: Number($event.target.value) })">
                    <option :value="0">Unassigned</option>
                    <option v-for="owner in enterpriseLeadOwners" :key="`lead-owner-${lead.enterpriseLeadId}-${owner.userId}`" :value="owner.userId">{{ owner.name }}</option>
                  </select>
                  <div class="small text-muted" v-if="lead.ownerName">{{ lead.ownerName }}</div>
                </td>
                <td>
                  <select
                    class="form-select form-select-sm"
                    :value="lead.status || 'New'"
                    @change="updateEnterpriseLead(lead, { status: $event.target.value })">
                    <option value="New">New</option>
                    <option value="Contacted">Contacted</option>
                    <option value="Qualified">Qualified</option>
                    <option value="ClosedWon">ClosedWon</option>
                    <option value="ClosedLost">ClosedLost</option>
                  </select>
                </td>
                <td>
                  <span class="badge" :class="slaBadgeClass(lead.slaStatus)">{{ lead.slaStatus }}</span>
                  <div class="small text-muted">{{ lead.slaHoursElapsed }}h</div>
                </td>
                <td class="small text-truncate" style="max-width: 260px;">{{ lead.notes || '-' }}</td>
                <td>
                  <div class="d-flex flex-column gap-1">
                    <button class="btn btn-outline-primary btn-sm" @click="loadLeadTimeline(lead)">Timeline</button>
                    <button class="btn btn-outline-secondary btn-sm" @click="autoAssignLead(lead)">Auto Assign</button>
                  </div>
                </td>
              </tr>
              <tr v-if="!enterpriseLeadsLoading && enterpriseLeads.length === 0">
                <td colspan="9" class="text-center text-muted py-3">No enterprise leads found for this filter.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm mt-4 panel-card" v-if="selectedLeadId">
      <div class="card-body p-3 p-md-4">
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h6 class="fw-semibold mb-0">Lead Timeline #{{ selectedLeadId }}</h6>
          <button class="btn btn-outline-primary btn-sm" :disabled="leadTimelineLoading" @click="loadLeadTimelineById(selectedLeadId)">
            {{ leadTimelineLoading ? 'Loading...' : 'Refresh Timeline' }}
          </button>
        </div>

        <div class="row g-2 mb-3">
          <div class="col-md-10">
            <input v-model.trim="leadNote" class="form-control form-control-sm" placeholder="Add internal note for this lead" />
          </div>
          <div class="col-md-2 d-grid">
            <button class="btn btn-sm btn-primary" :disabled="!leadNote || !selectedLeadId" @click="addLeadNote">Add Note</button>
          </div>
        </div>

        <div v-if="leadTimelineError" class="alert alert-danger py-2">{{ leadTimelineError }}</div>
        <div v-if="leadTimelineLoading" class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        <div v-else-if="!leadTimelineItems.length" class="small text-muted">No timeline activities yet.</div>
        <div v-else class="list-group list-group-flush">
          <div class="list-group-item px-0" v-for="item in leadTimelineItems" :key="item.enterpriseLeadActivityId">
            <div class="d-flex justify-content-between gap-2">
              <div>
                <div class="fw-semibold small">{{ item.activityType }}</div>
                <div class="small text-muted">{{ item.message }}</div>
                <div class="small text-muted" v-if="item.actorName">By {{ item.actorName }}</div>
              </div>
              <div class="small text-muted">{{ fmtDate(item.createdAt) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Global error -->
    <div v-if="globalError" class="alert alert-danger mt-4">{{ globalError }}</div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import api from '../services/api';
import AppBarChart from '../components/charts/AppBarChart.vue';
import AppLineChart from '../components/charts/AppLineChart.vue';
import AppDoughnutChart from '../components/charts/AppDoughnutChart.vue';

// ── Seed form ──────────────────────────────────────────────────────────────
const showSeedForm = ref(false);
const seed = ref({ name: 'Super Admin', email: '', password: '' });
const seedLoading = ref(false);
const seedMsg = ref('');
const seedError = ref(false);

async function createAdmin() {
  seedLoading.value = true;
  seedMsg.value = '';
  try {
    await api.post('/admin/seed', seed.value);
    seedMsg.value = 'SuperAdmin created! Please login to continue.';
    seedError.value = false;
    showSeedForm.value = false;
  } catch (err) {
    seedMsg.value = err.response?.data?.error || 'Failed to create SuperAdmin';
    seedError.value = true;
  } finally {
    seedLoading.value = false;
  }
}

// ── Stats ──────────────────────────────────────────────────────────────────
const stats = ref(null);
const globalError = ref('');

const statCards = computed(() => stats.value ? {
  'Creators':         stats.value.creators,
  'Creator Profiles': stats.value.creatorProfiles,
  'Linked Channels':  stats.value.linkedChannels,
  'Users':            stats.value.users,
  'Waitlist Pending': stats.value.waitlistPending,
  'Recovery Grace':   stats.value.recoveryGraceUsers,
} : {});

const quotaBarClass = computed(() => {
  if (!stats.value) return '';
  const pct = stats.value.quotaUsedToday / stats.value.quotaDailyLimit;
  if (pct > 0.9) return 'bg-danger';
  if (pct > 0.6) return 'bg-warning';
  return 'bg-success';
});

async function loadStats() {
  try {
    const res = await api.get('/admin/stats');
    stats.value = res.data;
  } catch (err) {
    if (err.response?.status === 401 || err.response?.status === 403) {
      globalError.value = 'Access denied. You must be logged in as SuperAdmin.';
    }
  }
}

// ── Jobs ───────────────────────────────────────────────────────────────────
const jobs = ref([
  {
    id: 'analytics',
    icon: '📊',
    title: 'Creator Analytics',
    description: 'Refresh avg views / likes / comments + record daily subscriber snapshots.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'language',
    icon: '🌐',
    title: 'Language Detection',
    description: 'Detect content language for each creator using YouTube video titles, descriptions and comments.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'viral',
    icon: '🔥',
    title: 'Viral Content Scoring',
    description: 'Score creators on viral content potential based on engagement patterns.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'rising',
    icon: '📈',
    title: 'Rising Creator Detection',
    description: 'Recalculate growth momentum scores to identify fast-rising creators.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'marketing',
    icon: '💼',
    title: 'Marketing Intelligence',
    description: 'Recalculate composite creator scores and scan videos for brand-promotion signals.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'creator-stats',
    icon: '📡',
    title: 'Creator Channel Stats',
    description: 'Refresh subscriber counts, view counts and video counts for all registered Feature-7 channels.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'legacy-channels',
    icon: '🎬',
    title: 'Legacy Influencer Channels',
    description: 'Fetch live YouTube stats + recent videos for legacy Influencer accounts and cache them for the Marketplace. Run this after every server restart.',
    loading: false, lastRun: null, result: '', error: false
  },
  {
    id: 'video-analytics',
    icon: '📊',
    title: 'Video Analytics & Brand Detection',
    description: 'Fetch the 50 most-recent videos for every creator, compute engagement rates, detect brand collaborations (Samsung, Nike, boAt, etc.) and store results in VideoAnalytics table.',
    loading: false, lastRun: null, result: '', error: false
  },
]);

// ── Brand waitlist management ────────────────────────────────────────────
const waitlistLoading = ref(false);
const waitlistExporting = ref(false);
const waitlistItems = ref([]);
const waitlistTotal = ref(0);
const waitlistSummary = ref([]);
const recoveryLoading = ref(false);
const recoveryRows = ref([]);
const recoveryOutreachLoadingId = ref(0);
const recoveryFilters = ref({
  scope: 'grace',
  paymentStatus: '',
});
const waitlistFilters = ref({
  status: '',
  customerType: '',
  from: '',
  to: '',
  preset: '',
});

const enterpriseLeadsLoading = ref(false);
const enterpriseLeadsExporting = ref(false);
const enterpriseLeads = ref([]);
const enterpriseLeadsTotal = ref(0);
const enterpriseLeadsSummary = ref({ total: 0, healthy: 0, atRisk: 0, breached: 0 });
const selectedLeadId = ref(0);
const leadTimelineItems = ref([]);
const leadTimelineLoading = ref(false);
const leadTimelineError = ref('');
const leadNote = ref('');
const enterpriseLeadOwners = ref([]);
const enterpriseLeadFilters = ref({
  source: '',
  status: '',
  ownerUserId: 0,
  from: '',
  to: '',
});

const funnelDays = ref(30);
const insightRange = ref(30);
const insightRefreshing = ref(false);
const insightRanges = [
  { days: 7, label: '7d' },
  { days: 30, label: '30d' },
  { days: 90, label: '90d' },
];
const insightRangeLabel = ref('');
const funnelRows = ref([]);
const funnelSince = ref('');
const funnelLoading = ref(false);
const funnelError = ref('');

const funnelTotal = computed(() => (
  funnelRows.value.reduce((sum, row) => sum + (row.count || 0), 0)
));

const funnelChartData = computed(() => ({
  labels: funnelRows.value.map(row => row.eventName),
  datasets: [
    {
      label: 'Events',
      data: funnelRows.value.map(row => row.count || 0),
      backgroundColor: ['#0ea5e9', '#22c55e', '#f59e0b', '#6366f1', '#14b8a6', '#f97316'],
      borderRadius: 8,
    },
  ],
}));

const waitlistStatusChartData = computed(() => ({
  labels: (waitlistSummary.value || []).map(x => x.status),
  datasets: [
    {
      label: 'Waitlist',
      data: (waitlistSummary.value || []).map(x => x.count || 0),
      backgroundColor: ['#64748b', '#0ea5e9', '#22c55e', '#ef4444'],
      borderWidth: 0,
    },
  ],
}));

const enterpriseSlaChartData = computed(() => {
  const s = enterpriseLeadsSummary.value || {};
  return {
    labels: ['Healthy', 'At Risk', 'Breached'],
    datasets: [
      {
        label: 'SLA',
        data: [s.healthy || 0, s.atRisk || 0, s.breached || 0],
        backgroundColor: ['#22c55e', '#f59e0b', '#ef4444'],
        borderWidth: 0,
      },
    ],
  };
});

const leadTrendChartData = computed(() => {
  const dayCounts = {};
  for (const lead of enterpriseLeads.value) {
    const date = lead?.createdAt ? new Date(lead.createdAt) : null;
    if (!date || Number.isNaN(date.getTime())) continue;
    const key = date.toISOString().slice(0, 10);
    dayCounts[key] = (dayCounts[key] || 0) + 1;
  }

  const labels = Object.keys(dayCounts).sort();
  return {
    labels,
    datasets: [
      {
        label: 'Leads per day',
        data: labels.map(label => dayCounts[label]),
        borderColor: '#2563eb',
        backgroundColor: 'rgba(37,99,235,0.18)',
        fill: true,
        tension: 0.32,
      },
    ],
  };
});

const barOptions = {
  plugins: {
    legend: { display: false },
  },
  scales: {
    y: {
      beginAtZero: true,
      ticks: { precision: 0 },
    },
  },
};

const lineOptions = {
  plugins: {
    legend: { display: true },
  },
  scales: {
    y: {
      beginAtZero: true,
      ticks: { precision: 0 },
    },
  },
};

const doughnutOptions = {
  plugins: {
    legend: {
      position: 'bottom',
    },
  },
};

const enterpriseSummaryText = computed(() => {
  const s = enterpriseLeadsSummary.value || {};
  if (!enterpriseLeadsTotal.value) return '';
  return `Healthy: ${s.healthy || 0}, At Risk: ${s.atRisk || 0}, Breached: ${s.breached || 0}`;
});

const recoveryAtRiskCount = computed(() => (
  recoveryRows.value.filter(x => x.graceHoursRemaining != null && x.graceHoursRemaining <= 24).length
));

function toInputDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

async function applyDatePreset(preset) {
  const now = new Date();
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  let start = new Date(end);

  if (preset === 'last7') {
    start.setDate(end.getDate() - 6);
  } else if (preset === 'last30') {
    start.setDate(end.getDate() - 29);
  } else if (preset === 'thisMonth') {
    start = new Date(end.getFullYear(), end.getMonth(), 1);
  }

  waitlistFilters.value.from = toInputDate(start);
  waitlistFilters.value.to = toInputDate(end);
  waitlistFilters.value.preset = preset;
  await loadWaitlist();
}

async function clearDateFilters() {
  waitlistFilters.value.from = '';
  waitlistFilters.value.to = '';
  waitlistFilters.value.preset = '';
  await loadWaitlist();
}

async function applyInsightRange(days) {
  insightRange.value = Number(days);
  funnelDays.value = Number(days);

  const end = new Date();
  const start = new Date(end);
  start.setDate(end.getDate() - (Number(days) - 1));

  const from = toInputDate(start);
  const to = toInputDate(end);

  insightRangeLabel.value = `${Number(days)} days (${formatShortDate(start)} to ${formatShortDate(end)})`;

  waitlistFilters.value.from = from;
  waitlistFilters.value.to = to;
  waitlistFilters.value.preset = '';

  enterpriseLeadFilters.value.from = from;
  enterpriseLeadFilters.value.to = to;

  insightRefreshing.value = true;
  try {
    await Promise.allSettled([
      loadFunnelSummary(),
      loadWaitlist(),
      loadEnterpriseLeads(),
    ]);
  } finally {
    insightRefreshing.value = false;
  }
}

const waitlistSummaryLine = computed(() => {
  if (!waitlistSummary.value?.length) return '';
  return waitlistSummary.value.map(x => `${x.status}: ${x.count}`).join(', ');
});

const waitlistWindowLabel = computed(() => (
  toWindowLabel(waitlistFilters.value.from, waitlistFilters.value.to)
));

const enterpriseWindowLabel = computed(() => (
  toWindowLabel(enterpriseLeadFilters.value.from, enterpriseLeadFilters.value.to)
));

function waitlistStatusClass(status) {
  if (status === 'Approved') return 'bg-success';
  if (status === 'Rejected') return 'bg-danger';
  if (status === 'Contacted') return 'bg-info text-dark';
  return 'bg-secondary';
}

async function loadWaitlist() {
  waitlistLoading.value = true;
  try {
    const params = {
      page: 1,
      pageSize: 50,
    };

    if (waitlistFilters.value.status) params.status = waitlistFilters.value.status;
    if (waitlistFilters.value.customerType) params.customerType = waitlistFilters.value.customerType;
    if (waitlistFilters.value.from) params.from = waitlistFilters.value.from;
    if (waitlistFilters.value.to) params.to = waitlistFilters.value.to;

    const res = await api.get('/admin/brand-waitlist', { params });
    waitlistItems.value = res.data?.items || [];
    waitlistTotal.value = res.data?.total || 0;
    waitlistSummary.value = res.data?.statusSummary || [];
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to load brand waitlist.';
  } finally {
    waitlistLoading.value = false;
  }
}

async function loadRecoveryQueue() {
  recoveryLoading.value = true;
  try {
    const params = {
      graceOnly: recoveryFilters.value.scope === 'grace',
      page: 1,
      pageSize: 100,
    };

    if (recoveryFilters.value.paymentStatus) {
      params.paymentStatus = recoveryFilters.value.paymentStatus;
    }

    const { data } = await api.get('/admin/subscription-recovery', { params });
    recoveryRows.value = data?.items || [];
  } catch (err) {
    recoveryRows.value = [];
    globalError.value = err.response?.data?.error || 'Failed to load subscription recovery queue.';
  } finally {
    recoveryLoading.value = false;
  }
}

async function triggerRecoveryOutreach(row) {
  recoveryOutreachLoadingId.value = row.subscriptionId;
  try {
    await api.post(`/admin/subscription-recovery/${row.subscriptionId}/outreach`, {
      message: `Hi ${row.userName}, we noticed your ${row.planName} subscription is in recovery mode. Please update payment details and retry payment to avoid downgrade.`,
    });
    await loadRecoveryQueue();
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to send outreach reminder.';
  } finally {
    recoveryOutreachLoadingId.value = 0;
  }
}

async function updateWaitlistStatus(entry, status) {
  try {
    await api.patch(`/admin/brand-waitlist/${entry.brandWaitlistEntryId}/status`, { status });
    entry.status = status;
    await loadStats();
    await loadWaitlist();
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to update waitlist status.';
  }
}

async function exportWaitlistCsv() {
  waitlistExporting.value = true;
  globalError.value = '';

  try {
    const params = {};
    if (waitlistFilters.value.status) params.status = waitlistFilters.value.status;
    if (waitlistFilters.value.customerType) params.customerType = waitlistFilters.value.customerType;
    if (waitlistFilters.value.from) params.from = waitlistFilters.value.from;
    if (waitlistFilters.value.to) params.to = waitlistFilters.value.to;

    const res = await api.get('/admin/brand-waitlist/export', {
      params,
      responseType: 'blob',
    });

    const blob = new Blob([res.data], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');

    anchor.href = url;
    anchor.download = `brand-waitlist-${timestamp}.csv`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    window.URL.revokeObjectURL(url);
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to export waitlist CSV.';
  } finally {
    waitlistExporting.value = false;
  }
}

async function runJob(job) {
  job.loading = true;
  job.result  = '';
  job.error   = false;
  globalError.value = '';
  try {
    const res = await api.post(`/admin/jobs/${job.id}`);
    job.lastRun = new Date();
    const d = res.data;
    if (d.added !== undefined)          job.result = `Added: ${d.added}`;
    else if (d.upserted !== undefined)  job.result = `Upserted: ${d.upserted}`;
    else if (d.refreshed !== undefined) job.result = `Refreshed: ${d.refreshed}`;
    else if (d.fetched !== undefined)   job.result = `Fetched: ${d.fetched}${d.failed ? ` | Failed: ${d.failed}` : ''}`;
    else if (d.error)                   job.result = d.error;
    else job.result = 'Done ✓';
    // refresh stats after a job
    await loadStats();
  } catch (err) {
    const status = err.response?.status;
    const d = err.response?.data;
    // 408 = job timed out (partial success, not a crash)
    if (status === 408 && d) {
      job.lastRun = new Date();
      job.result  = d.error || 'Timed out';
      job.error   = true;
    } else {
      job.error = true;
      const msg = d?.error || d || err.message;
      job.result = typeof msg === 'string' ? msg.slice(0, 80) : 'Failed';
    }
  } finally {
    job.loading = false;
  }
}

async function loadFunnelSummary() {
  funnelLoading.value = true;
  funnelError.value = '';

  try {
    const res = await api.get('/funnel/summary', {
      params: { days: funnelDays.value },
    });
    funnelRows.value = res.data?.rows || [];
    funnelSince.value = res.data?.since || '';
  } catch (err) {
    funnelRows.value = [];
    funnelSince.value = '';
    funnelError.value = err.response?.data?.error || 'Failed to load funnel summary.';
  } finally {
    funnelLoading.value = false;
  }
}

async function loadEnterpriseLeads() {
  enterpriseLeadsLoading.value = true;
  try {
    const params = { page: 1, pageSize: 50 };
    if (enterpriseLeadFilters.value.source) params.source = enterpriseLeadFilters.value.source;
    if (enterpriseLeadFilters.value.status) params.status = enterpriseLeadFilters.value.status;
    if (enterpriseLeadFilters.value.ownerUserId) params.ownerUserId = enterpriseLeadFilters.value.ownerUserId;
    if (enterpriseLeadFilters.value.from) params.from = enterpriseLeadFilters.value.from;
    if (enterpriseLeadFilters.value.to) params.to = enterpriseLeadFilters.value.to;

    const res = await api.get('/admin/enterprise-leads', { params });
    enterpriseLeads.value = res.data?.items || [];
    enterpriseLeadsTotal.value = res.data?.total || 0;
    enterpriseLeadsSummary.value = res.data?.summary || { total: 0, healthy: 0, atRisk: 0, breached: 0 };

    if (enterpriseLeads.value.length && !selectedLeadId.value) {
      await loadLeadTimelineById(enterpriseLeads.value[0].enterpriseLeadId);
    }
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to load enterprise leads.';
  } finally {
    enterpriseLeadsLoading.value = false;
  }
}

async function loadEnterpriseLeadOwners() {
  try {
    const res = await api.get('/admin/enterprise-lead-owners');
    enterpriseLeadOwners.value = res.data || [];
  } catch {
    enterpriseLeadOwners.value = [];
  }
}

async function updateEnterpriseLead(lead, payload) {
  try {
    await api.patch(`/admin/enterprise-leads/${lead.enterpriseLeadId}`, payload);
    if (Object.prototype.hasOwnProperty.call(payload, 'status')) {
      lead.status = payload.status;
    }
    if (Object.prototype.hasOwnProperty.call(payload, 'ownerUserId')) {
      lead.ownerUserId = payload.ownerUserId > 0 ? payload.ownerUserId : null;
      const owner = enterpriseLeadOwners.value.find(x => x.userId === lead.ownerUserId);
      lead.ownerName = owner?.name || null;
    }
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to update enterprise lead.';
  }
}

async function autoAssignLead(lead) {
  try {
    await api.post(`/admin/enterprise-leads/${lead.enterpriseLeadId}/auto-assign`);
    await loadEnterpriseLeads();
    await loadLeadTimelineById(lead.enterpriseLeadId);
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to auto-assign enterprise lead.';
  }
}

async function loadLeadTimeline(lead) {
  await loadLeadTimelineById(lead.enterpriseLeadId);
}

async function loadLeadTimelineById(leadId) {
  selectedLeadId.value = Number(leadId || 0);
  if (!selectedLeadId.value) {
    leadTimelineItems.value = [];
    return;
  }

  leadTimelineLoading.value = true;
  leadTimelineError.value = '';
  try {
    const { data } = await api.get(`/admin/enterprise-leads/${selectedLeadId.value}/timeline`);
    leadTimelineItems.value = data?.items || [];
  } catch (err) {
    leadTimelineItems.value = [];
    leadTimelineError.value = err.response?.data?.error || 'Failed to load lead timeline.';
  } finally {
    leadTimelineLoading.value = false;
  }
}

async function addLeadNote() {
  if (!selectedLeadId.value || !leadNote.value) return;

  try {
    await api.patch(`/admin/enterprise-leads/${selectedLeadId.value}`, { notes: leadNote.value });
    leadNote.value = '';
    await loadLeadTimelineById(selectedLeadId.value);
  } catch (err) {
    leadTimelineError.value = err.response?.data?.error || 'Failed to add lead note.';
  }
}

async function exportEnterpriseLeadsCsv() {
  enterpriseLeadsExporting.value = true;
  try {
    const params = {};
    if (enterpriseLeadFilters.value.source) params.source = enterpriseLeadFilters.value.source;
    if (enterpriseLeadFilters.value.status) params.status = enterpriseLeadFilters.value.status;
    if (enterpriseLeadFilters.value.ownerUserId) params.ownerUserId = enterpriseLeadFilters.value.ownerUserId;
    if (enterpriseLeadFilters.value.from) params.from = enterpriseLeadFilters.value.from;
    if (enterpriseLeadFilters.value.to) params.to = enterpriseLeadFilters.value.to;

    const res = await api.get('/admin/enterprise-leads/export', {
      params,
      responseType: 'blob',
    });

    const blob = new Blob([res.data], { type: 'text/csv;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');

    anchor.href = url;
    anchor.download = `enterprise-leads-${timestamp}.csv`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    window.URL.revokeObjectURL(url);
  } catch (err) {
    globalError.value = err.response?.data?.error || 'Failed to export enterprise leads CSV.';
  } finally {
    enterpriseLeadsExporting.value = false;
  }
}

async function refreshDashboard() {
  await Promise.allSettled([
    loadStats(),
    loadEnterpriseLeadOwners(),
    loadRecoveryQueue(),
    applyInsightRange(insightRange.value),
  ]);
}

function fmtTime(d) {
  if (!d) return '';
  return new Date(d).toLocaleTimeString();
}

function fmtDate(d) {
  if (!d) return '';
  return new Date(d).toLocaleString();
}

function formatShortDate(value) {
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function toWindowLabel(from, to) {
  if (!from || !to) return '';
  return `${formatShortDate(from)} to ${formatShortDate(to)}`;
}

function slaBadgeClass(status) {
  if (status === 'Breached') return 'bg-danger';
  if (status === 'At Risk') return 'bg-warning text-dark';
  return 'bg-success';
}

// ── Lifecycle ──────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    await refreshDashboard();
  } catch {
    // if stats fail and we suspect no admin yet, show seed form
  }
  // Check if no admin exists (seed endpoint returns 200 if we can create, 409 if one exists)
  // We use GET stats 403 as a proxy — if we got stats it means we're logged in as admin
  if (!stats.value) {
    // Try to detect if there's no superadmin at all by calling seed with empty body (will 400 not 409)
    try {
      const probe = await api.post('/admin/seed', {});
    } catch (err) {
      if (err.response?.status === 409) {
        // Admin exists, user just isn't logged in as one
        globalError.value = 'Please login as a SuperAdmin to access this panel.';
      } else if (err.response?.status === 400 || err.response?.status === 200) {
        // 400 = validation (no email/pass) means no admin exists yet
        showSeedForm.value = true;
      }
    }
  }
});
</script>

<style scoped>
.super-admin-page {
  background: radial-gradient(circle at 5% 0%, rgba(56, 189, 248, 0.08), transparent 40%),
    radial-gradient(circle at 96% 14%, rgba(34, 197, 94, 0.08), transparent 38%);
}

.admin-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #e2e8f0;
  background: linear-gradient(124deg, #0f172a 0%, #1e3a8a 60%, #0369a1 100%);
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

.job-card {
  transition: transform .16s ease, box-shadow .16s ease;
}

.job-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(0, 0, 0, .11) !important;
}

.chart-block {
  border-radius: 14px;
  padding: 0.7rem;
  background: linear-gradient(180deg, rgba(248, 250, 252, 0.9) 0%, rgba(241, 245, 249, 0.92) 100%);
}

.chart-box {
  height: 240px;
}

.chart-box.compact {
  height: 210px;
}

@media (max-width: 768px) {
  .admin-hero {
    flex-direction: column;
  }
}
</style>
