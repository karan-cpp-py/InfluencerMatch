<template>
  <div class="container-fluid py-4" style="max-width:1200px; margin:0 auto;">
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
      <div>
        <h3 class="fw-bold mb-1">Manage Platform Users</h3>
        <p class="text-muted mb-0">View and manage all account types from one place.</p>
      </div>
      <router-link to="/admin" class="btn btn-outline-secondary btn-sm">Back to Admin</router-link>
    </div>

    <div class="card border-0 shadow-sm mb-3">
      <div class="card-body">
        <div class="row g-2 align-items-end">
          <div class="col-md-4">
            <label class="form-label small fw-semibold mb-1">Search</label>
            <input v-model="filters.query" class="form-control form-control-sm" placeholder="Name or email" @keyup.enter="loadUsers(1)" />
          </div>
          <div class="col-md-3">
            <label class="form-label small fw-semibold mb-1">Role</label>
            <select v-model="filters.role" class="form-select form-select-sm">
              <option value="">All</option>
              <option v-for="opt in roleOptions" :key="opt" :value="opt">{{ opt }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small fw-semibold mb-1">Customer Type</label>
            <select v-model="filters.customerType" class="form-select form-select-sm">
              <option value="">All</option>
              <option v-for="opt in customerTypeOptions" :key="opt" :value="opt">{{ opt }}</option>
            </select>
          </div>
          <div class="col-md-2 d-flex gap-1">
            <button class="btn btn-primary btn-sm w-100" @click="loadUsers(1)" :disabled="loading">Search</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="error" class="alert alert-warning">{{ error }}</div>

    <div class="card border-0 shadow-sm">
      <div class="card-body p-0">
        <div class="table-responsive">
          <table class="table table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>User</th>
                <th>Role</th>
                <th>Customer Type</th>
                <th>Verified</th>
                <th>Auth</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!loading && users.length === 0">
                <td colspan="7" class="text-center py-4 text-muted">No users found.</td>
              </tr>
              <tr v-for="u in users" :key="u.userId">
                <td>
                  <div class="fw-semibold">{{ u.name }}</div>
                  <div class="small text-muted">{{ u.email }}</div>
                </td>
                <td>
                  <select class="form-select form-select-sm" v-model="u._editRole">
                    <option v-for="opt in roleOptions" :key="`r-${u.userId}-${opt}`" :value="opt">{{ opt }}</option>
                  </select>
                </td>
                <td>
                  <select class="form-select form-select-sm" v-model="u._editCustomerType">
                    <option v-for="opt in customerTypeOptions" :key="`c-${u.userId}-${opt}`" :value="opt">{{ opt }}</option>
                  </select>
                </td>
                <td>
                  <div class="form-check form-switch mb-0">
                    <input class="form-check-input" type="checkbox" v-model="u._editEmailVerified" />
                  </div>
                </td>
                <td class="small">{{ u.authProvider || 'password' }}</td>
                <td class="small text-muted">{{ fmtDate(u.createdAt) }}</td>
                <td>
                  <button class="btn btn-outline-primary btn-sm" :disabled="savingUserId === u.userId" @click="saveUser(u)">
                    {{ savingUserId === u.userId ? 'Saving...' : 'Save' }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <div class="card-footer bg-white d-flex justify-content-between align-items-center">
        <span class="small text-muted">Total: {{ total }}</span>
        <div class="d-flex gap-1">
          <button class="btn btn-outline-secondary btn-sm" :disabled="page <= 1" @click="loadUsers(page - 1)">Prev</button>
          <span class="small align-self-center px-2">Page {{ page }}</span>
          <button class="btn btn-outline-secondary btn-sm" :disabled="page * pageSize >= total" @click="loadUsers(page + 1)">Next</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import api from '../services/api';

const users = ref([]);
const total = ref(0);
const page = ref(1);
const pageSize = 30;
const loading = ref(false);
const error = ref('');
const savingUserId = ref(null);

const roleOptions = ['Brand', 'Agency', 'Creator', 'CreatorManager', 'Individual', 'SuperAdmin'];
const customerTypeOptions = ['Brand', 'Agency', 'Creator', 'CreatorManager', 'Individual', 'Internal'];

const filters = ref({
  query: '',
  role: '',
  customerType: ''
});

function hydrateRows(rows) {
  return (rows || []).map((u) => ({
    ...u,
    _editRole: u.role,
    _editCustomerType: u.customerType,
    _editEmailVerified: Boolean(u.emailVerified)
  }));
}

async function loadUsers(targetPage = 1) {
  loading.value = true;
  error.value = '';
  page.value = targetPage;
  try {
    const { data } = await api.get('/admin/users', {
      params: {
        page: page.value,
        pageSize,
        query: filters.value.query || undefined,
        role: filters.value.role || undefined,
        customerType: filters.value.customerType || undefined
      }
    });
    users.value = hydrateRows(data?.items);
    total.value = Number(data?.total || 0);
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to load users.';
    users.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

async function saveUser(user) {
  savingUserId.value = user.userId;
  error.value = '';
  try {
    await api.patch(`/admin/users/${user.userId}`, {
      role: user._editRole,
      customerType: user._editCustomerType,
      emailVerified: user._editEmailVerified
    });
    await loadUsers(page.value);
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'Failed to update user.';
  } finally {
    savingUserId.value = null;
  }
}

function fmtDate(value) {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString();
}

onMounted(() => {
  loadUsers(1);
});
</script>
