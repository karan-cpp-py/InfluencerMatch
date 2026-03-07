import api from './api';

export async function bookDemo(payload) {
  const { data } = await api.post('/gtm/book-demo', payload);
  return data;
}

export async function fetchReferral() {
  const { data } = await api.get('/gtm/referral');
  return data;
}
