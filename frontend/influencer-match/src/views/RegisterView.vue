<template>
  <div class="container py-5 auth-shell">
    <div class="row justify-content-center">
      <div class="col-xl-11">
        <div class="card border-0 overflow-hidden">
          <div class="row g-0">
            <div class="col-lg-5 auth-side p-4 p-lg-5 text-white d-flex flex-column justify-content-between">
              <div>
                <div class="small text-uppercase fw-semibold tracking">Get Started</div>
                <h3 class="fw-bold mt-2">Build your plan-aware creator workspace</h3>
                <p class="opacity-75 mb-0">
                  Register as a brand, agency, individual, or creator manager and unlock role-based dashboard modules.
                </p>
              </div>
              <ul class="small ps-3 mb-0 opacity-75">
                <li>Creator discovery access by subscription</li>
                <li>Dashboard modules by customer type</li>
                <li>Integrated billing and payment providers</li>
              </ul>
            </div>
            <div class="col-lg-7">
              <div class="card-body p-4 p-lg-5">
                <h3 class="card-title mb-4">Register</h3>
                <form @submit.prevent="submit">
                  <div class="mb-3">
                    <label class="form-label">Name</label>
                    <input v-model="name" class="form-control" required />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Email</label>
                    <input v-model="email" type="email" class="form-control" required />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Password</label>
                    <input v-model="password" type="password" class="form-control" required />
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Account Type</label>
                    <select v-model="accountType" class="form-select" required>
                      <option value="Brand">Brand</option>
                      <option value="Agency">Advertising Agency</option>
                      <option value="Individual">Individual User</option>
                      <option value="CreatorManager">Creator Manager / Talent Agency</option>
                      <option value="Creator">Creator (YouTube Channel)</option>
                    </select>
                  </div>

                  <div v-if="accountType !== 'Creator'" class="mb-3">
                    <label class="form-label">Company Name (optional)</label>
                    <input v-model="companyName" class="form-control" />
                  </div>

                  <div class="mb-3">
                    <label class="form-label">Country</label>
                    <input v-model="country" class="form-control" required />
                  </div>

                  <div v-if="accountType !== 'Creator'" class="mb-3">
                    <label class="form-label">Phone Number</label>
                    <input v-model="phoneNumber" class="form-control" placeholder="Optional" />
                  </div>

                  <div class="mb-3">
                    <label class="form-label">Referral Code (optional)</label>
                    <input v-model.trim="referralCode" class="form-control" placeholder="e.g. IM0123098" />
                  </div>

                  <button class="btn btn-primary w-100" type="submit">Register</button>
                </form>
                <div v-if="error" class="alert alert-danger mt-3">{{ error }}</div>
                <div class="text-center mt-3">
                  <small>Already have an account? <router-link to="/login">Login</router-link></small>
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
import { trackFunnelEvent } from '../services/funnel';
import { ensurePlatformConfigLoaded, platformConfig } from '../services/platform';

const router = useRouter();
const name = ref('');
const email = ref('');
const password = ref('');
const accountType = ref('Brand');
const companyName = ref('');
const country = ref('India');
const phoneNumber = ref('');
const referralCode = ref('');
const error = ref('');

async function submit() {
  try {
    let res;
    if (accountType.value === 'Creator') {
      // Creator registration uses a dedicated endpoint
      res = await api.post('/creator/register', {
        name: name.value,
        email: email.value,
        password: password.value,
        country: country.value
      });
      // Response has { token, creatorProfileId, userId }
      const payload = normalizeAuthPayload(res.data);
      setAuth(payload.accessToken, 'Creator', payload.refreshToken);
      await trackFunnelEvent('signup', { role: 'Creator' });
      router.push('/creator-dashboard');
    } else {
      res = await api.post('/auth/register', {
        name: name.value,
        email: email.value,
        password: password.value,
        companyName: companyName.value || null,
        customerType: accountType.value,
        country: country.value,
        phoneNumber: phoneNumber.value || null,
        referralCode: referralCode.value || null
      });

      const payload = normalizeAuthPayload(res.data);
      const auth = authFromToken(payload.accessToken);
      setAuth(payload.accessToken, auth.role, payload.refreshToken);
      await trackFunnelEvent('signup', { role: auth.role });
      await ensurePlatformConfigLoaded();
      router.push(homeRouteForRole(auth.role, platformConfig.features));
    }
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Registration failed';
  }
}
</script>

<style scoped>
.auth-shell {
  max-width: 1120px;
}

.auth-side {
  background: linear-gradient(145deg, #0f172a, #0e7490 55%, #1d4ed8);
}

.tracking {
  letter-spacing: 0.08em;
}
</style>