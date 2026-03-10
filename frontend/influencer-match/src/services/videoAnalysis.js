import api from './api';

/**
 * Analyze the latest video payload and return strict JSON sections required by the platform UI.
 */
export async function analyzeLatestVideo(payload) {
  const { data } = await api.post('/videos/latest-analysis', payload);
  return data;
}
