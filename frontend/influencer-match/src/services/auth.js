import { ref } from 'vue';
import { authFromToken } from './claims';

// reactive references for authentication state
export const authToken = ref(localStorage.getItem('token') || '');
export const authRefreshToken = ref(localStorage.getItem('refreshToken') || '');
export const authRole = ref(localStorage.getItem('role') || '');
export const authCustomerType = ref(localStorage.getItem('customerType') || '');
export const authUserName = ref('');

// keep username in sync by decoding token
function updateUserName(token) {
  if (!token) {
    authRole.value = '';
    authCustomerType.value = '';
    authUserName.value = '';
    return;
  }

  const auth = authFromToken(token);
  authRole.value = auth.role || authRole.value;
  authCustomerType.value = auth.customerType || authCustomerType.value;
  authUserName.value = auth.userName;
}

// initialize username
updateUserName(authToken.value);

export function normalizeAuthPayload(payload = {}) {
  const accessToken = payload.accessToken || payload.token || payload.Token || '';
  const refreshToken = payload.refreshToken || payload.RefreshToken || '';

  return {
    accessToken,
    refreshToken,
    expiresInSeconds: Number(payload.expiresInSeconds || payload.ExpiresInSeconds || 0) || 0
  };
}

export function setAuth(token, roleOverride = '', refreshToken = '') {
  const auth = authFromToken(token);
  const role = roleOverride || auth.role;

  authToken.value = token;
  authRefreshToken.value = refreshToken || authRefreshToken.value;
  authRole.value = role;
  authCustomerType.value = auth.customerType;
  authUserName.value = auth.userName;

  localStorage.setItem('token', token);
  if (refreshToken) {
    localStorage.setItem('refreshToken', refreshToken);
  }
  localStorage.setItem('role', role);
  localStorage.setItem('customerType', auth.customerType || '');
}

export function clearAuth() {
  authToken.value = '';
  authRefreshToken.value = '';
  authRole.value = '';
  authCustomerType.value = '';
  authUserName.value = '';
  localStorage.removeItem('token');
  localStorage.removeItem('refreshToken');
  localStorage.removeItem('role');
  localStorage.removeItem('customerType');
}
