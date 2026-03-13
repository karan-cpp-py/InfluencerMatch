<template>
  <div class="container py-5" style="max-width: 720px;">
    <div class="card border-0 shadow-sm">
      <div class="card-body p-4 p-lg-5">
        <h3 class="fw-bold mb-2">Verify Your Email</h3>
        <p class="text-muted mb-4">
          Verify your account to continue. If email delivery is enabled, use the verification link sent to your inbox.
        </p>

        <div v-if="awaitingEmailLink" class="alert alert-info">
          Verification email sent to <strong>{{ pendingEmail || 'your inbox' }}</strong>. Open the link in that email to continue.
        </div>

        <div v-if="success" class="alert alert-success">
          {{ success }}
        </div>
        <div v-if="error" class="alert alert-danger">
          {{ error }}
        </div>

        <form v-if="showManualEntry" @submit.prevent="verify">
          <label class="form-label">Verification token</label>
          <input v-model="token" class="form-control mb-3" placeholder="Paste token" required />
          <button class="btn btn-primary" type="submit" :disabled="loading">
            <span v-if="loading" class="spinner-border spinner-border-sm me-1"></span>
            Verify Email
          </button>
        </form>
        <router-link class="btn btn-link ps-0" to="/login">Back to login</router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import api from '../services/api';

const route = useRoute();
const token = ref('');
const pendingEmail = ref('');
const loading = ref(false);
const error = ref('');
const success = ref('');

const awaitingEmailLink = computed(() => route.query.sent === '1' && !token.value && !success.value);
const showManualEntry = computed(() => Boolean(token.value) || !awaitingEmailLink.value);

async function verify() {
  error.value = '';
  success.value = '';
  loading.value = true;
  try {
    await api.post('/auth/verify-email', { token: token.value.trim() });
    success.value = 'Email verified. You can now sign in.';
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Email verification failed.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  const fromQuery = String(route.query.token || '').trim();
  pendingEmail.value = String(route.query.email || '').trim();
  if (fromQuery) {
    token.value = fromQuery;
    verify();
  }
});
</script>
