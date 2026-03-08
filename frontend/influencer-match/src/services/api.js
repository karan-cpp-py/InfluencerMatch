import axios from 'axios';
import { clearAuth, normalizeAuthPayload, setAuth } from './auth';

// Vite uses import.meta.env for env variables; prefix custom vars with VITE_
const defaultLocalApi = 'http://localhost:60587/api';
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || defaultLocalApi,
});

api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let refreshInFlight = null;

api.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config;
    const status = error?.response?.status;
    const endpoint = String(originalRequest?.url || '');

    if (status === 403) {
      const payload = error?.response?.data || {};
      if (payload?.errorCode === 'PLAN_LIMIT_REACHED' || payload?.requiredPlan || payload?.remainingSearches === 0) {
        window.dispatchEvent(new CustomEvent('plan-limit-reached', {
          detail: {
            message: payload?.error || 'You have reached your plan limit. Upgrade to continue.',
            requiredPlan: payload?.requiredPlan || null,
            currentPlan: payload?.currentPlan || null,
            upgradePath: payload?.upgradePath || '/plans',
            remainingSearches: payload?.remainingSearches,
          }
        }));
      }
    }

    if (
      status !== 401
      || originalRequest?._retry
      || endpoint.includes('/auth/login')
      || endpoint.includes('/auth/register')
      || endpoint.includes('/auth/refresh')
    ) {
      return Promise.reject(error);
    }

    const storedRefresh = localStorage.getItem('refreshToken');
    if (!storedRefresh) {
      clearAuth();
      window.dispatchEvent(new CustomEvent('session-expired', {
        detail: { reason: 'missing_refresh_token' }
      }));
      return Promise.reject(error);
    }

    if (!refreshInFlight) {
      refreshInFlight = axios
        .post(`${api.defaults.baseURL}/auth/refresh`, { refreshToken: storedRefresh })
        .then(res => {
          const auth = normalizeAuthPayload(res.data || {});
          if (!auth.accessToken) {
            throw new Error('No access token returned from refresh endpoint.');
          }

          setAuth(auth.accessToken, '', auth.refreshToken || storedRefresh);
          return auth.accessToken;
        })
        .finally(() => {
          refreshInFlight = null;
        });
    }

    try {
      const newToken = await refreshInFlight;
      originalRequest._retry = true;
      originalRequest.headers = originalRequest.headers || {};
      originalRequest.headers.Authorization = `Bearer ${newToken}`;
      return api(originalRequest);
    } catch (refreshError) {
      clearAuth();
      window.dispatchEvent(new CustomEvent('session-expired', {
        detail: { reason: 'refresh_failed' }
      }));
      return Promise.reject(refreshError);
    }
  }
);

export default api;