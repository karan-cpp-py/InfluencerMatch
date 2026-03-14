<template>
  <div class="container py-5 auth-shell">
    <div class="row justify-content-center">
      <div class="col-xl-11">
        <div class="card border-0 overflow-hidden">
          <div class="row g-0">
            <!-- ── Side panel ───────────── -->
            <div class="col-lg-5 auth-side p-4 p-lg-5 text-white d-flex flex-column justify-content-between">
              <div>
                <div class="small text-uppercase fw-semibold tracking">Get Started</div>
                <h3 class="fw-bold mt-2">Your creator-first influencer workspace</h3>
                <p class="opacity-75 mb-0">
                  Join as a Brand, Agency, or Creator and unlock role-based dashboards, AI analytics, and collaboration tools.
                </p>
              </div>
              <ul class="small ps-3 mb-0 opacity-75">
                <li>Instant Google sign-up — verified in seconds</li>
                <li>Creator discovery access by subscription</li>
                <li>Dashboard modules by account type</li>
                <li>Integrated billing and payment providers</li>
              </ul>
            </div>

            <!-- ── Form panel ──────────── -->
            <div class="col-lg-7">
              <div class="card-body p-4 p-lg-5">
                <h3 class="card-title mb-1">Create Account</h3>
                <p class="text-muted small mb-4">Choose how you'd like to sign up.</p>

                <!-- ── Sign-up method toggle ── -->
                <div class="method-tabs mb-4">
                  <button
                    :class="['method-tab', signupMethod === 'email' ? 'active' : '']"
                    @click="switchMethod('email')"
                    type="button"
                  >✉ Email & Password</button>
                  <button
                    :class="['method-tab', signupMethod === 'google' ? 'active' : '']"
                    @click="switchMethod('google')"
                    type="button"
                  >
                    <svg width="16" height="16" viewBox="0 0 48 48" class="me-1" style="vertical-align:-2px"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/><path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.18 1.48-4.97 2.35-8.16 2.35-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/><path fill="none" d="M0 0h48v48H0z"/></svg>
                    Continue with Google
                  </button>
                </div>

                <!-- ══════════════════════════════════════════════ -->
                <!--  Method A: Email & Password                   -->
                <!-- ══════════════════════════════════════════════ -->
                <form v-if="signupMethod === 'email'" @submit.prevent="submit">
                  <div class="mb-3">
                    <label class="form-label">Full Name</label>
                    <input v-model="name" class="form-control" required placeholder="e.g. Ravi Sharma" />
                  </div>

                  <div class="mb-3">
                    <label class="form-label">Email Address</label>
                    <input v-model="email" type="email" class="form-control" required placeholder="you@example.com" />
                  </div>

                  <!-- Password + strength indicator -->
                  <div class="mb-1">
                    <label class="form-label">Password</label>
                    <div class="input-group">
                      <input
                        v-model="password"
                        :type="showPassword ? 'text' : 'password'"
                        class="form-control"
                        required
                        placeholder="Create a strong password"
                        @input="computePasswordStrength"
                      />
                      <button class="btn btn-outline-secondary" type="button" @click="showPassword = !showPassword">
                        {{ showPassword ? '🙈' : '👁' }}
                      </button>
                    </div>
                  </div>
                  <!-- Password rules -->
                  <div class="password-rules mb-2">
                    <span :class="['rule', pwRules.length ? 'ok' : '']">✓ 8+ characters</span>
                    <span :class="['rule', pwRules.upper ? 'ok' : '']">✓ Uppercase</span>
                    <span :class="['rule', pwRules.lower ? 'ok' : '']">✓ Lowercase</span>
                    <span :class="['rule', pwRules.number ? 'ok' : '']">✓ Number</span>
                    <span :class="['rule', pwRules.special ? 'ok' : '']">✓ Special char</span>
                  </div>
                  <!-- Strength bar -->
                  <div v-if="password" class="mb-3">
                    <div class="progress" style="height:5px">
                      <div
                        class="progress-bar"
                        :class="pwStrengthBarClass"
                        :style="{ width: pwStrengthPercent + '%' }"
                      ></div>
                    </div>
                    <div class="form-text" :class="pwStrengthTextClass">{{ pwStrengthLabel }}</div>
                  </div>

                  <!-- Confirm password -->
                  <div class="mb-3">
                    <label class="form-label">Confirm Password</label>
                    <div class="input-group">
                      <input
                        v-model="confirmPassword"
                        :type="showConfirm ? 'text' : 'password'"
                        class="form-control"
                        :class="confirmMismatch ? 'is-invalid' : (confirmPassword && !confirmMismatch ? 'is-valid' : '')"
                        required
                        placeholder="Re-enter password"
                      />
                      <button class="btn btn-outline-secondary" type="button" @click="showConfirm = !showConfirm">
                        {{ showConfirm ? '🙈' : '👁' }}
                      </button>
                      <div v-if="confirmMismatch" class="invalid-feedback">Passwords do not match.</div>
                    </div>
                  </div>

                  <!-- Account type (Individual & CreatorManager removed) -->
                  <div class="mb-3">
                    <label class="form-label">Account Type</label>
                    <select v-model="accountType" class="form-select" required>
                      <option value="Brand">Brand</option>
                      <option value="Agency">Advertising Agency</option>
                      <option value="Creator">Creator (YouTube Channel)</option>
                    </select>
                  </div>

                  <div v-if="accountType !== 'Creator'" class="mb-3">
                    <label class="form-label">Company Name <span class="text-muted">(optional)</span></label>
                    <input v-model="companyName" class="form-control" />
                  </div>

                  <div class="mb-3">
                    <label class="form-label">Country</label>
                    <input v-model="country" class="form-control" required placeholder="e.g. India" />
                  </div>

                  <div v-if="accountType !== 'Creator'" class="mb-3">
                    <label class="form-label">Phone Number <span class="text-muted">(optional)</span></label>
                    <input v-model="phoneNumber" class="form-control" placeholder="+91 98765 43210" />
                  </div>

                  <div class="mb-3">
                    <label class="form-label">Referral Code <span class="text-muted">(optional)</span></label>
                    <input v-model.trim="referralCode" class="form-control" placeholder="e.g. IM0123098" />
                  </div>

                  <div class="form-check mb-3">
                    <input id="acceptTerms" v-model="acceptTerms" class="form-check-input" type="checkbox" />
                    <label class="form-check-label" for="acceptTerms">
                      I agree to the <router-link to="/terms" target="_blank">Terms and Conditions</router-link>
                    </label>
                  </div>

                  <button
                    class="btn btn-primary w-100"
                    type="submit"
                    :disabled="!acceptTerms || !isPasswordValid || confirmMismatch || submitting"
                  >
                    <span v-if="submitting" class="spinner-border spinner-border-sm me-1"></span>
                    Create Account
                  </button>
                </form>

                <!-- ══════════════════════════════════════════════ -->
                <!--  Method B: Google OAuth                       -->
                <!-- ══════════════════════════════════════════════ -->
                <div v-else-if="signupMethod === 'google'">
                  <!-- Step 1: account type -->
                  <div v-if="googleStep === 'type'">
                    <p class="text-muted small mb-3">
                      Select your account type and accept the Terms below.
                      Google verifies your Gmail identity instantly — <strong>no password or OTP needed</strong>.
                    </p>
                    <div class="mb-3">
                      <label class="form-label">Account Type</label>
                      <select v-model="accountType" class="form-select" required>
                        <option value="Brand">Brand</option>
                        <option value="Agency">Advertising Agency</option>
                        <option value="Creator">Creator (YouTube Channel)</option>
                      </select>
                    </div>
                    <div class="mb-3">
                      <label class="form-label">Country</label>
                      <input v-model="country" class="form-control" placeholder="e.g. India" />
                    </div>
                    <div class="form-check mb-3">
                      <input id="acceptTermsGoogle" v-model="acceptTerms" class="form-check-input" type="checkbox" />
                      <label class="form-check-label" for="acceptTermsGoogle">
                        I agree to the <router-link to="/terms" target="_blank">Terms and Conditions</router-link>
                      </label>
                    </div>
                    <!-- GIS button shown only after terms accepted -->
                    <div v-if="acceptTerms" class="d-flex justify-content-center mt-3">
                      <div id="google-signup-button"></div>
                    </div>
                    <p v-else class="text-center small text-muted fst-italic mt-3">
                      ☝ Accept the Terms above to reveal the Google sign-up button.
                    </p>
                    <p class="text-muted small text-center mt-2">
                      Google verifies your Gmail instantly — <strong>no OTP, no password</strong>.
                    </p>
                  </div>

                  <!-- Step 2: processing (Google popup closed, awaiting callback) -->
                  <div v-else-if="googleStep === 'pending'" class="text-center py-4">
                    <div class="spinner-border text-primary mb-3"></div>
                    <p class="text-muted">Verifying your Google account…</p>
                  </div>

                  <!-- Step 3: done -->
                  <div v-else-if="googleStep === 'done'" class="text-center py-4">
                    <div class="text-success fs-2 mb-2">✓</div>
                    <p class="fw-semibold">Account created! Redirecting…</p>
                  </div>
                </div>

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
import { ref, computed, nextTick, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import api from '../services/api';
import { normalizeAuthPayload, setAuth } from '../services/auth';
import { authFromToken, homeRouteForRole } from '../services/claims';
import { trackFunnelEvent } from '../services/funnel';
import { ensurePlatformConfigLoaded, platformConfig } from '../services/platform';

const router = useRouter();

// ── common fields ─────────────────────────────────────────────────────────────
const signupMethod  = ref('email'); // 'email' | 'google'
const googleStep    = ref('type');  // 'type' | 'pending' | 'done'
const submitting    = ref(false);
const error         = ref('');

const name          = ref('');
const email         = ref('');
const password      = ref('');
const confirmPassword = ref('');
const showPassword  = ref(false);
const showConfirm   = ref(false);
const accountType   = ref('Brand');
const companyName   = ref('');
const country       = ref('India');
const phoneNumber   = ref('');
const referralCode  = ref('');
const acceptTerms   = ref(false);

// ── password strength ─────────────────────────────────────────────────────────
const pwRules = ref({ length: false, upper: false, lower: false, number: false, special: false });

function computePasswordStrength() {
  const p = password.value;
  pwRules.value = {
    length:  p.length >= 8,
    upper:   /[A-Z]/.test(p),
    lower:   /[a-z]/.test(p),
    number:  /[0-9]/.test(p),
    special: /[^A-Za-z0-9]/.test(p),
  };
}

const pwScore = computed(() => Object.values(pwRules.value).filter(Boolean).length);
const isPasswordValid = computed(() => pwScore.value === 5);

const pwStrengthPercent = computed(() => (pwScore.value / 5) * 100);
const pwStrengthLabel = computed(() => {
  const s = pwScore.value;
  if (s <= 1) return 'Very weak';
  if (s === 2) return 'Weak';
  if (s === 3) return 'Fair';
  if (s === 4) return 'Good';
  return 'Strong';
});
const pwStrengthBarClass = computed(() => {
  const s = pwScore.value;
  if (s <= 1) return 'bg-danger';
  if (s === 2) return 'bg-warning';
  if (s === 3) return 'bg-info';
  if (s === 4) return 'bg-primary';
  return 'bg-success';
});
const pwStrengthTextClass = computed(() => {
  const s = pwScore.value;
  if (s <= 1) return 'text-danger';
  if (s === 2) return 'text-warning';
  if (s === 3) return 'text-info';
  if (s === 4) return 'text-primary';
  return 'text-success';
});
const confirmMismatch = computed(() =>
  confirmPassword.value.length > 0 && confirmPassword.value !== password.value
);

// ── method toggle ─────────────────────────────────────────────────────────────
function switchMethod(m) {
  signupMethod.value = m;
  error.value = '';
  googleStep.value = 'type';
  if (m === 'google' && acceptTerms.value) {
    nextTick(() => renderGoogleSignupButton());
  }
}

// Render GIS button when user accepts terms while already on the Google tab
watch(acceptTerms, (val) => {
  if (val && signupMethod.value === 'google' && googleStep.value === 'type') {
    nextTick(() => renderGoogleSignupButton());
  }
});

// ── email/password submit ─────────────────────────────────────────────────────
async function submit() {
  if (!isPasswordValid.value) {
    error.value = 'Password must be at least 8 characters and include an uppercase letter, lowercase letter, number, and special character.';
    return;
  }
  if (confirmMismatch.value) {
    error.value = 'Passwords do not match.';
    return;
  }

  submitting.value = true;
  error.value = '';
  try {
    let res;
    if (accountType.value === 'Creator') {
      res = await api.post('/creator/register', {
        name: name.value,
        email: email.value,
        password: password.value,
        country: country.value
      });
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
        referralCode: referralCode.value || null,
        acceptTerms: acceptTerms.value
      });

      const payload = normalizeAuthPayload(res.data);
      if (!payload.emailVerified) {
        const query = new URLSearchParams({ email: email.value, sent: '1' });
        if (payload.verificationToken) query.set('token', payload.verificationToken);
        router.push(`/verify-email?${query.toString()}`);
        return;
      }

      const auth = authFromToken(payload.accessToken);
      setAuth(payload.accessToken, auth.role, payload.refreshToken);
      await trackFunnelEvent('signup', { role: auth.role });
      await ensurePlatformConfigLoaded();
      router.push(homeRouteForRole(auth.role, platformConfig.features));
    }
  } catch (err) {
    error.value = err.userMessage || err.response?.data?.error || 'Registration failed';
  } finally {
    submitting.value = false;
  }
}

// ── Google Identity Services (GIS) sign-up ───────────────────────────────────
// Uses the same GIS library already used on the login page.
// Google verifies the user's Gmail via its own consent screen — no OTP needed.

function handleGoogleSignupCredential(credential) {
  if (!acceptTerms.value) {
    error.value = 'Please accept the Terms and Conditions first.';
    return;
  }
  if (!accountType.value) {
    error.value = 'Please select your account type first.';
    return;
  }
  completeGoogleSignup(credential);
}

function renderGoogleSignupButton() {
  if (!window.google?.accounts?.id) {
    // Script still loading — retry shortly
    setTimeout(() => renderGoogleSignupButton(), 350);
    return;
  }
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
  if (!clientId) {
    error.value = 'Google sign-up is not configured. Please use email registration.';
    return;
  }
  window.google.accounts.id.initialize({
    client_id: clientId,
    callback: response => handleGoogleSignupCredential(response.credential),
    context: 'signup',
  });
  const target = document.getElementById('google-signup-button');
  if (target) {
    window.google.accounts.id.renderButton(target, {
      theme: 'outline',
      size: 'large',
      width: 280,
      text: 'signup_with',
      logo_alignment: 'center',
    });
  }
}

async function completeGoogleSignup(idToken) {
  submitting.value = true;
  try {
    const res = await api.post('/auth/google', {
      idToken,
      customerType: accountType.value,
      country: country.value,
    });
    const payload = normalizeAuthPayload(res.data);
    const auth = authFromToken(payload.accessToken);
    setAuth(payload.accessToken, auth.role, payload.refreshToken);
    await trackFunnelEvent('signup', { role: auth.role, method: 'google' });
    googleStep.value = 'done';
    await ensurePlatformConfigLoaded();
    setTimeout(() => router.push(homeRouteForRole(auth.role, platformConfig.features)), 1200);
  } catch (err) {
    googleStep.value = 'type';
    error.value = err.userMessage || err.response?.data?.error || 'Google sign-up failed. Please try again or use email registration.';
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  // Pre-load the Google Identity Services script so it's ready when user switches to the Google tab
  const existing = document.getElementById('google-identity-script');
  if (!existing) {
    const script = document.createElement('script');
    script.id = 'google-identity-script';
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }
});
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

/* Sign-up method tabs */
.method-tabs {
  display: flex;
  gap: 0.5rem;
}
.method-tab {
  flex: 1;
  padding: 0.55rem 0.75rem;
  border: 1.5px solid #cbd5e1;
  border-radius: 10px;
  background: #fff;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s, color 0.15s;
  color: #475569;
}
.method-tab:hover { border-color: #0e7490; color: #0e7490; }
.method-tab.active {
  border-color: #0e7490;
  background: #ecfeff;
  color: #0e7490;
}

/* Password rules */
.password-rules {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}
.rule {
  font-size: 0.73rem;
  padding: 0.18rem 0.55rem;
  border-radius: 999px;
  background: #f1f5f9;
  color: #94a3b8;
  border: 1px solid #e2e8f0;
  transition: background 0.2s, color 0.2s;
}
.rule.ok {
  background: #dcfce7;
  color: #15803d;
  border-color: #86efac;
}
</style>