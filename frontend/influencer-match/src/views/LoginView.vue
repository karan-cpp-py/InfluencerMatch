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
                  <div class="text-end mt-2">
                    <router-link to="/forgot-password" class="small">Forgot password?</router-link>
                  </div>
                </form>
                <div class="divider my-3"><span>or</span></div>
                <div id="google-signin-button" class="d-flex justify-content-center"></div>
                <small v-if="googleHint" class="text-muted d-block mt-2 text-center">{{ googleHint }}</small>
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
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { normalizeAuthPayload, setAuth } from '../services/auth';
import { authFromToken, homeRouteForRole } from '../services/claims';
import { ensurePlatformConfigLoaded, platformConfig } from '../services/platform';

const router = useRouter();
const email = ref('');
const password = ref('');
const error = ref('');
const googleHint = ref('');

function goToHome(payload) {
  const auth = authFromToken(payload.accessToken);
  setAuth(payload.accessToken, auth.role, payload.refreshToken);
  ensurePlatformConfigLoaded().then(() => {
    router.push(homeRouteForRole(auth.role, platformConfig.features));
  });
}

async function submit() {
  try {
    const res = await api.post('/auth/login', { email: email.value, password: password.value });
    const payload = normalizeAuthPayload(res.data);
    goToHome(payload);
  } catch (err) {
    const message = err.userMessage || err.response?.data?.error || 'Login failed';
    if (String(message).toLowerCase().includes('verify your email')) {
      router.push(`/verify-email?email=${encodeURIComponent(email.value)}`);
      return;
    }
    error.value = message;
  }
}

async function handleGoogleCredential(credential) {
  if (!credential) return;
  try {
    const res = await api.post('/auth/google', { idToken: credential });
    const payload = normalizeAuthPayload(res.data);
    goToHome(payload);
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Google login failed';
  }
}

function renderGoogleButton() {
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
  if (!clientId) {
    googleHint.value = 'Set VITE_GOOGLE_CLIENT_ID to enable Google login.';
    return;
  }

  if (!window.google?.accounts?.id) {
    googleHint.value = 'Google login is temporarily unavailable.';
    return;
  }

  window.google.accounts.id.initialize({
    client_id: clientId,
    callback: response => handleGoogleCredential(response.credential),
  });

  const target = document.getElementById('google-signin-button');
  if (target) {
    window.google.accounts.id.renderButton(target, {
      theme: 'outline',
      size: 'large',
      width: 300,
      text: 'signin_with'
    });
  }
}

onMounted(() => {
  const existing = document.getElementById('google-identity-script');
  if (existing) {
    renderGoogleButton();
    return;
  }

  const script = document.createElement('script');
  script.id = 'google-identity-script';
  script.src = 'https://accounts.google.com/gsi/client';
  script.async = true;
  script.defer = true;
  script.onload = () => renderGoogleButton();
  script.onerror = () => {
    googleHint.value = 'Unable to load Google sign-in.';
  };
  document.head.appendChild(script);
});
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

.divider {
  display: flex;
  align-items: center;
  text-align: center;
  color: #64748b;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  border-bottom: 1px solid #e2e8f0;
}

.divider span {
  padding: 0 10px;
  font-size: 0.875rem;
}
</style>
