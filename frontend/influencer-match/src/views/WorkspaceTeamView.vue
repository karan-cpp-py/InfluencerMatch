<template>
  <div class="container py-4 workspace-shell">
    <section class="workspace-hero mb-4">
      <div>
        <p class="hero-kicker mb-2">Week 9</p>
        <h2 class="fw-bold mb-1">Team Workspace</h2>
        <p class="mb-0 text-light-emphasis">Manage multi-seat access with Owner, Manager, and Analyst roles.</p>
      </div>
      <button class="btn btn-light btn-sm fw-semibold" @click="loadWorkspace">Refresh</button>
    </section>

    <div v-if="error" class="alert alert-danger">{{ error }}</div>

    <div v-if="!workspace" class="card border-0 shadow-sm panel-card">
      <div class="card-body p-4">
        <h5 class="fw-bold">Create your team workspace</h5>
        <p class="text-muted mb-3">Agencies and brands can invite teammates and track all access changes in audit logs.</p>
        <div class="row g-2">
          <div class="col-md-8">
            <input v-model.trim="workspaceName" class="form-control" placeholder="Workspace name" />
          </div>
          <div class="col-md-4">
            <button class="btn btn-primary w-100" :disabled="loading" @click="createMyWorkspace">
              {{ loading ? 'Creating...' : 'Create Workspace' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-else class="row g-3">
      <div class="col-lg-7">
        <div class="card border-0 shadow-sm panel-card h-100">
          <div class="card-body p-3 p-md-4">
            <div class="d-flex justify-content-between align-items-center mb-3">
              <div>
                <h5 class="fw-bold mb-0">{{ workspace.name }}</h5>
                <div class="small text-muted">Your role: <strong>{{ workspace.myRole }}</strong></div>
              </div>
            </div>

            <div class="seat-strip mb-3">
              <div class="d-flex justify-content-between small mb-1">
                <span>Seats used: <strong>{{ workspace.usedSeats }}</strong> + pending invites: <strong>{{ workspace.pendingInvites }}</strong></span>
                <span>Plan limit: <strong>{{ workspace.seatLimit }}</strong> | Remaining: <strong>{{ workspace.remainingSeats }}</strong></span>
              </div>
              <div class="progress" style="height:8px;">
                <div class="progress-bar" :class="seatBarClass" :style="{ width: `${seatUsagePercent}%` }"></div>
              </div>
            </div>

            <div class="row g-3 mb-3" v-if="workspace.members?.length">
              <div class="col-md-6">
                <div class="card border-0 bg-light-subtle chart-panel h-100">
                  <div class="card-body p-3">
                    <div class="small fw-semibold mb-2">Seat Utilization</div>
                    <div class="chart-box compact">
                      <AppDoughnutChart :data="seatUtilizationData" :options="doughnutOptions" />
                    </div>
                  </div>
                </div>
              </div>
              <div class="col-md-6">
                <div class="card border-0 bg-light-subtle chart-panel h-100">
                  <div class="card-body p-3">
                    <div class="small fw-semibold mb-2">Role Distribution</div>
                    <div class="chart-box compact">
                      <AppDoughnutChart :data="roleDistributionData" :options="doughnutOptions" />
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div class="table-responsive">
              <table class="table table-sm align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Member</th>
                    <th>Role</th>
                    <th>Joined</th>
                    <th v-if="canManage">Action</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="m in workspace.members" :key="m.workspaceMemberId">
                    <td>
                      <div class="fw-semibold">{{ m.name }}</div>
                      <div class="small text-muted">{{ m.email }}</div>
                    </td>
                    <td>
                      <template v-if="canManage && m.role !== 'Owner'">
                        <select class="form-select form-select-sm" :value="m.role" @change="changeRole(m, $event.target.value)">
                          <option value="Manager">Manager</option>
                          <option value="Analyst">Analyst</option>
                        </select>
                      </template>
                      <span v-else class="badge" :class="roleBadge(m.role)">{{ m.role }}</span>
                    </td>
                    <td class="small text-muted">{{ fmtDate(m.joinedAt) }}</td>
                    <td v-if="canManage">
                      <button v-if="m.role !== 'Owner'" class="btn btn-outline-danger btn-sm" @click="removeMember(m)">Remove</button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <div class="col-lg-5">
        <div class="card border-0 shadow-sm panel-card mb-3" v-if="canManage">
          <div class="card-body p-3 p-md-4">
            <h6 class="fw-semibold mb-3">Invite Teammate</h6>
            <div class="row g-2">
              <div class="col-12">
                <input v-model.trim="invite.email" type="email" class="form-control" placeholder="teammate@company.com" />
              </div>
              <div class="col-8">
                <select v-model="invite.role" class="form-select">
                  <option value="Manager">Manager</option>
                  <option value="Analyst">Analyst</option>
                </select>
              </div>
              <div class="col-4">
                <button class="btn btn-primary w-100" :disabled="loading" @click="sendInvite">Invite</button>
              </div>
            </div>
            <div class="small text-muted mt-2">Invite links are token-based and expire in 7 days.</div>
          </div>
        </div>

        <div class="card border-0 shadow-sm panel-card" v-if="workspace.invites?.length">
          <div class="card-body p-3 p-md-4">
            <h6 class="fw-semibold mb-3">Pending Invites</h6>
            <div class="list-group list-group-flush">
              <div class="list-group-item px-0" v-for="inv in workspace.invites" :key="inv.workspaceInviteId">
                <div class="fw-semibold">{{ inv.email }}</div>
                <div class="small text-muted mb-2">Role: {{ inv.role }} | Expires: {{ fmtDate(inv.expiresAt) }}</div>
                <div class="small text-muted">Token: <code>{{ inv.inviteToken }}</code></div>
                <div class="small"><a :href="inv.inviteUrl" target="_blank" rel="noopener">Open one-click invite link</a></div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="col-12">
        <div class="card border-0 shadow-sm panel-card">
          <div class="card-body p-3 p-md-4">
            <h6 class="fw-semibold mb-3">Audit Log</h6>

            <div class="card border-0 bg-light-subtle chart-panel mb-3" v-if="workspace.auditLogs?.length">
              <div class="card-body p-3">
                <div class="small fw-semibold mb-2">Audit Activity by Action</div>
                <div class="chart-box">
                  <AppBarChart :data="auditActionData" :options="barOptions" />
                </div>
              </div>
            </div>

            <div class="table-responsive">
              <table class="table table-sm align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Time</th>
                    <th>Action</th>
                    <th>Actor</th>
                    <th>Target</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="log in workspace.auditLogs" :key="log.workspaceAuditLogId">
                    <td class="small text-muted">{{ fmtDate(log.createdAt) }}</td>
                    <td class="fw-semibold">{{ log.action }}</td>
                    <td>{{ log.actorName }}</td>
                    <td>{{ log.target }}</td>
                  </tr>
                  <tr v-if="!workspace.auditLogs?.length">
                    <td colspan="4" class="text-center text-muted py-3">No audit events yet.</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="card border-0 shadow-sm panel-card mt-3" v-if="myInvites.length">
      <div class="card-body p-3 p-md-4">
        <h6 class="fw-semibold mb-3">Invites For Your Email</h6>
        <div class="d-flex flex-column gap-2">
          <div class="d-flex flex-wrap justify-content-between align-items-center gap-2" v-for="inv in myInvites" :key="`mine-${inv.workspaceInviteId}`">
            <div>
              <div class="fw-semibold">{{ inv.email }}</div>
              <div class="small text-muted">Role: {{ inv.role }} | Expires: {{ fmtDate(inv.expiresAt) }}</div>
            </div>
            <button class="btn btn-outline-primary btn-sm" @click="acceptInvite(inv.inviteToken)">Accept Invite</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import AppBarChart from '../components/charts/AppBarChart.vue';
import AppDoughnutChart from '../components/charts/AppDoughnutChart.vue';
import {
  acceptWorkspaceInvite,
  createWorkspace,
  fetchMyWorkspaceInvites,
  fetchWorkspace,
  inviteWorkspaceMember,
  removeWorkspaceMember,
  updateWorkspaceMemberRole,
} from '../services/workspace';

const route = useRoute();

const workspace = ref(null);
const workspaceName = ref('');
const invite = ref({ email: '', role: 'Manager' });
const myInvites = ref([]);
const loading = ref(false);
const error = ref('');

const canManage = computed(() => {
  const role = workspace.value?.myRole;
  return role === 'Owner' || role === 'Manager';
});

const seatUsagePercent = computed(() => {
  const limit = Number(workspace.value?.seatLimit || 0);
  if (!limit) return 0;
  const used = Number(workspace.value?.usedSeats || 0) + Number(workspace.value?.pendingInvites || 0);
  return Math.min(100, Math.round((used / limit) * 100));
});

const seatBarClass = computed(() => {
  const pct = seatUsagePercent.value;
  if (pct >= 90) return 'bg-danger';
  if (pct >= 70) return 'bg-warning';
  return 'bg-success';
});

const seatUtilizationData = computed(() => {
  const used = Number(workspace.value?.usedSeats || 0);
  const pending = Number(workspace.value?.pendingInvites || 0);
  const remaining = Math.max(0, Number(workspace.value?.remainingSeats || 0));

  return {
    labels: ['Used', 'Pending', 'Remaining'],
    datasets: [
      {
        data: [used, pending, remaining],
        backgroundColor: ['#2563eb', '#f59e0b', '#22c55e'],
        borderWidth: 0,
      },
    ],
  };
});

const roleDistributionData = computed(() => {
  const members = workspace.value?.members || [];
  const countByRole = members.reduce((acc, m) => {
    const role = m?.role || 'Unknown';
    acc[role] = (acc[role] || 0) + 1;
    return acc;
  }, {});

  const labels = Object.keys(countByRole);
  return {
    labels,
    datasets: [
      {
        data: labels.map(label => countByRole[label]),
        backgroundColor: ['#3b82f6', '#14b8a6', '#8b5cf6', '#f97316'],
        borderWidth: 0,
      },
    ],
  };
});

const auditActionData = computed(() => {
  const logs = workspace.value?.auditLogs || [];
  const counts = logs.reduce((acc, log) => {
    const action = log?.action || 'Unknown';
    acc[action] = (acc[action] || 0) + 1;
    return acc;
  }, {});

  const labels = Object.keys(counts);
  return {
    labels,
    datasets: [
      {
        label: 'Events',
        data: labels.map(label => counts[label]),
        backgroundColor: '#0ea5e9',
        borderRadius: 8,
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

const doughnutOptions = {
  plugins: {
    legend: {
      position: 'bottom',
    },
  },
};

onMounted(async () => {
  await Promise.allSettled([loadWorkspace(), loadMyInvites()]);

  const inviteToken = String(route.query.inviteToken || '').trim();
  if (inviteToken) {
    await acceptInvite(inviteToken);
  }
});

async function loadWorkspace() {
  error.value = '';
  try {
    const data = await fetchWorkspace();
    workspace.value = data?.hasWorkspace ? data.workspace : null;
    if (!workspaceName.value) {
      workspaceName.value = workspace.value?.name || '';
    }
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to load workspace.';
  }
}

async function createMyWorkspace() {
  loading.value = true;
  error.value = '';
  try {
    workspace.value = await createWorkspace(workspaceName.value);
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to create workspace.';
  } finally {
    loading.value = false;
  }
}

async function sendInvite() {
  loading.value = true;
  error.value = '';
  try {
    await inviteWorkspaceMember(invite.value);
    invite.value = { email: '', role: 'Manager' };
    await loadWorkspace();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to send invite.';
  } finally {
    loading.value = false;
  }
}

async function loadMyInvites() {
  try {
    myInvites.value = await fetchMyWorkspaceInvites();
  } catch {
    myInvites.value = [];
  }
}

async function acceptInvite(token) {
  loading.value = true;
  error.value = '';
  try {
    workspace.value = await acceptWorkspaceInvite(token);
    await loadMyInvites();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to accept invite.';
  } finally {
    loading.value = false;
  }
}

async function changeRole(member, role) {
  try {
    await updateWorkspaceMemberRole(member.workspaceMemberId, role);
    await loadWorkspace();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to update role.';
  }
}

async function removeMember(member) {
  try {
    await removeWorkspaceMember(member.workspaceMemberId);
    await loadWorkspace();
  } catch (e) {
    error.value = e.response?.data?.error || 'Failed to remove member.';
  }
}

function fmtDate(value) {
  if (!value) return '';
  return new Date(value).toLocaleString();
}

function roleBadge(role) {
  if (role === 'Owner') return 'bg-primary';
  if (role === 'Manager') return 'bg-info text-dark';
  return 'bg-secondary';
}
</script>

<style scoped>
.workspace-shell {
  max-width: 1100px;
}

.workspace-hero {
  border-radius: 20px;
  padding: 1.2rem;
  color: #e2e8f0;
  background: linear-gradient(124deg, #1f2937 0%, #0f766e 55%, #0ea5e9 100%);
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
  background: rgba(191, 219, 254, 0.26);
  color: #dbeafe;
}

.panel-card {
  border-radius: 16px;
}

.chart-panel {
  border-radius: 14px;
}

.chart-box {
  height: 220px;
}

.chart-box.compact {
  height: 190px;
}

@media (max-width: 768px) {
  .workspace-hero {
    flex-direction: column;
  }
}
</style>
