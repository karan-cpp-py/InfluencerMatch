import axios from 'axios';
import { clearAuth, normalizeAuthPayload, setAuth } from './auth';

// Vite uses import.meta.env for env variables; prefix custom vars with VITE_
const defaultLocalApi = 'http://localhost:60588/api';
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || defaultLocalApi,
});

function toText(value) {
  if (typeof value === 'string') return value.trim();
  if (value == null) return '';
  return String(value).trim();
}

function extractRawErrorMessage(error) {
  const data = error?.response?.data;
  const direct = [
    data?.error,
    data?.message,
    data?.title,
    data?.detail,
    data?.errors,
    error?.message,
  ];

  for (const candidate of direct) {
    if (typeof candidate === 'string' && candidate.trim()) return candidate.trim();
  }

  if (Array.isArray(data?.errors) && data.errors.length) {
    return toText(data.errors[0]);
  }

  if (data?.errors && typeof data.errors === 'object') {
    const firstKey = Object.keys(data.errors)[0];
    const firstVal = firstKey ? data.errors[firstKey] : null;
    if (Array.isArray(firstVal) && firstVal.length) return toText(firstVal[0]);
    if (firstVal) return toText(firstVal);
  }

  return '';
}

function looksTechnical(raw) {
  const text = (raw || '').toLowerCase();
  if (!text) return false;
  return [
    'exception',
    'stack trace',
    'inner exception',
    'npgsql',
    'sqlstate',
    'timestamp with time zone',
    'object reference',
    'nullreference',
    'at influencermatch',
    'system.',
    'microsoft.',
    'timeout of',
    'network error',
    'failed to fetch',
    'cors',
    'status code 500',
  ].some(x => text.includes(x));
}

function defaultMessageByStatus(status) {
  switch (status) {
    case 400:
      return 'Some details are invalid. Please review your input and try again.';
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return 'You do not have access to this action.';
    case 404:
      return 'The requested data could not be found.';
    case 409:
      return 'This action conflicts with existing data. Please refresh and retry.';
    case 422:
      return 'Some fields need attention before we can continue.';
    case 429:
      return 'Too many requests right now. Please wait a moment and retry.';
    case 500:
    case 502:
    case 503:
    case 504:
      return 'Something went wrong on our side. Please try again shortly.';
    default:
      return 'Something went wrong. Please try again.';
  }
}

function buildHumanMessage(error) {
  const status = error?.response?.status;
  const raw = extractRawErrorMessage(error);

  if (!status) {
    return 'Cannot reach the server right now. Please check your connection and try again.';
  }

  if (!raw) {
    return defaultMessageByStatus(status);
  }

  if (looksTechnical(raw)) {
    return defaultMessageByStatus(status);
  }

  return raw;
}

function applyHumanError(error) {
  const human = buildHumanMessage(error);
  error.userMessage = human;
  error.message = human;

  if (error?.response?.data && typeof error.response.data === 'object') {
    error.response.data.error = human;
    error.response.data.message = human;
  }

  return error;
}

function shouldBroadcastUiError(error) {
  const status = error?.response?.status;
  if (!status) return true;
  if (status === 401 || status === 403) return false; // handled by dedicated flows
  return true;
}

function notifyUiError(error) {
  if (typeof window === 'undefined') return;
  if (!shouldBroadcastUiError(error)) return;

  const status = error?.response?.status || 0;
  window.dispatchEvent(new CustomEvent('api-error', {
    detail: {
      message: error?.userMessage || 'Something went wrong. Please try again.',
      status,
      endpoint: String(error?.config?.url || ''),
      method: String(error?.config?.method || 'get').toUpperCase(),
      at: Date.now(),
    }
  }));
}

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
    applyHumanError(error);
    notifyUiError(error);

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
      || endpoint.includes('/auth/google')
      || endpoint.includes('/auth/verify-email')
      || endpoint.includes('/auth/request-password-reset')
      || endpoint.includes('/auth/reset-password')
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
      applyHumanError(refreshError);
      clearAuth();
      window.dispatchEvent(new CustomEvent('session-expired', {
        detail: { reason: 'refresh_failed' }
      }));
      return Promise.reject(refreshError);
    }
  }
);

export default api;