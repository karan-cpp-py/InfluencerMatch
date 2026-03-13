<template>
  <div class="container py-5" style="max-width: 720px;">
    <div class="card border-0 shadow-sm">
      <div class="card-body p-4 p-lg-5">
        <h3 class="fw-bold mb-2">Reset Password</h3>
        <p class="text-muted mb-4">Use your token to set a new password.</p>

        <div v-if="success" class="alert alert-success">{{ success }}</div>
        <div v-if="error" class="alert alert-danger">{{ error }}</div>

        <form @submit.prevent="resetPassword">
          <label class="form-label">Reset token</label>
          <input v-model="token" class="form-control mb-3" required />

          <label class="form-label">New password</label>
          <input v-model="newPassword" type="password" class="form-control mb-3" minlength="8" required />

          <button class="btn btn-primary" type="submit" :disabled="loading">
            <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
            Reset Password
          </button>
          <router-link class="btn btn-link" to="/login">Back to login</router-link>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';

const route = useRoute();
const token = ref('');
const newPassword = ref('');
const loading = ref(false);
const error = ref('');
const success = ref('');

async function resetPassword() {
  loading.value = true;
  error.value = '';
  success.value = '';
  try {
    await api.post('/auth/reset-password', {
      token: token.value.trim(),
      newPassword: newPassword.value
    });
    success.value = 'Password reset complete. You can now sign in.';
    newPassword.value = '';
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Password reset failed.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  token.value = String(route.query.token || '').trim();
});
</script>
