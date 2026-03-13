<template>
  <div class="container py-5" style="max-width: 720px;">
    <div class="card border-0 shadow-sm">
      <div class="card-body p-4 p-lg-5">
        <h3 class="fw-bold mb-2">Forgot Password</h3>
        <p class="text-muted mb-4">Enter your email and we will send a password reset link.</p>

        <div v-if="message" class="alert alert-info">{{ message }}</div>
        <div v-if="error" class="alert alert-danger">{{ error }}</div>

        <form @submit.prevent="requestReset">
          <label class="form-label">Email</label>
          <input v-model="email" type="email" class="form-control mb-3" required />
          <button class="btn btn-primary" type="submit" :disabled="loading">
            <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
            Send Reset Link
          </button>
          <router-link class="btn btn-link" to="/login">Back to login</router-link>
        </form>

        <div v-if="resetToken" class="mt-4 p-3 border rounded bg-light">
          <div class="small text-muted mb-1">Reset token fallback</div>
          <code class="d-block mb-2">{{ resetToken }}</code>
          <router-link class="btn btn-sm btn-outline-primary" :to="`/reset-password?token=${encodeURIComponent(resetToken)}`">
            Continue to Reset Password
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import api from '../services/api';

const email = ref('');
const loading = ref(false);
const message = ref('');
const error = ref('');
const resetToken = ref('');

async function requestReset() {
  loading.value = true;
  error.value = '';
  message.value = '';
  resetToken.value = '';
  try {
    const res = await api.post('/auth/request-password-reset', { email: email.value.trim() });
    message.value = res.data?.message || 'If an account exists, reset instructions were generated.';
    resetToken.value = res.data?.resetToken || '';
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Failed to request password reset.';
  } finally {
    loading.value = false;
  }
}
</script>
