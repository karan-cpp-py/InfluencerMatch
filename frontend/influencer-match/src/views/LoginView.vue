<template>
  <div class="container py-5 auth-shell">
    <div class="row justify-content-center">
      <div class="col-lg-10">
        <div class="card border-0 overflow-hidden">
          <div class="row g-0">
            <div class="col-md-5 auth-side p-4 d-flex flex-column justify-content-between text-white">
              <div>
                <div class="small text-uppercase fw-semibold tracking">Welcome Back</div>
                <h3 class="fw-bold mt-2">Sign in to continue creator discovery</h3>
                <p class="opacity-75 mb-0">Access your campaigns, analytics, and subscription controls.</p>
              </div>
              <div class="small opacity-75">Secure JWT authentication enabled</div>
            </div>
            <div class="col-md-7">
              <div class="card-body p-4 p-lg-5">
                <h3 class="card-title mb-4">Login</h3>
                <form @submit.prevent="submit">
                  <div class="mb-3">
                    <label class="form-label">Email</label>
                    <input v-model="email" type="email" class="form-control" required />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Password</label>
                    <input v-model="password" type="password" class="form-control" required />
                  </div>
                  <button class="btn btn-primary w-100" type="submit">Login</button>
                </form>
                <div v-if="error" class="alert alert-danger mt-3">{{ error }}</div>
                <div class="text-center mt-3">
                  <small>Don't have an account? <router-link to="/register">Register</router-link></small>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { normalizeAuthPayload, setAuth } from '../services/auth';
import { authFromToken, homeRouteForRole } from '../services/claims';
import { ensurePlatformConfigLoaded, platformConfig } from '../services/platform';

const router = useRouter();
const email = ref('');
const password = ref('');
const error = ref('');

async function submit() {
  try {
    const res = await api.post('/auth/login', { email: email.value, password: password.value });
    const payload = normalizeAuthPayload(res.data);
    const auth = authFromToken(payload.accessToken);
    setAuth(payload.accessToken, auth.role, payload.refreshToken);
    await ensurePlatformConfigLoaded();
    router.push(homeRouteForRole(auth.role, platformConfig.features));
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Login failed';
  }
}
</script>

<style scoped>
.auth-shell {
  max-width: 1080px;
}

.auth-side {
  background: linear-gradient(145deg, #0f172a, #155e75 56%, #1d4ed8);
}

.tracking {
  letter-spacing: 0.08em;
}
</style>
