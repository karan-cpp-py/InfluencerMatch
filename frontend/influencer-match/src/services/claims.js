export function parseJwt(token) {
  if (!token) return {};
  try {
    return JSON.parse(atob(token.split('.')[1]));
  } catch {
    return {};
  }
}

function getClaim(payload, keys) {
  for (const key of keys) {
    if (payload[key] !== undefined && payload[key] !== null && payload[key] !== '') {
      return payload[key];
    }
  }
  return '';
}

export function resolveRole(payload) {
  return getClaim(payload, [
    'role',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
  ]);
}

export function resolveUserName(payload) {
  return getClaim(payload, [
    'unique_name',
    'name',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
  ]);
}

export function resolveCustomerType(payload) {
  const customer = getClaim(payload, ['customer_type']);
  if (customer) return customer;

  const role = resolveRole(payload);
  if (role === 'Brand') return 'Brand';
  if (role === 'Agency') return 'Agency';
  if (role === 'CreatorManager') return 'CreatorManager';
  if (role === 'Individual') return 'Individual';
  return '';
}

export function authFromToken(token) {
  const payload = parseJwt(token);
  return {
    role: resolveRole(payload),
    customerType: resolveCustomerType(payload),
    userName: resolveUserName(payload)
  };
}

export function homeRouteForRole(role) {
  if (role === 'SuperAdmin') return '/admin';
  if (role === 'Creator') return '/creator-dashboard';
  if (role === 'Influencer') return '/influencer';
  if (role === 'Brand') return '/marketplace';
  if (role === 'Agency') return '/marketplace';
  if (role === 'Individual') return '/dashboard-config';
  if (role === 'CreatorManager') return '/dashboard-config';
  return '/';
}
