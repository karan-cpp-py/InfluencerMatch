import api from './api';

export async function trackFunnelEvent(eventName, metadata = null) {
  try {
    await api.post('/funnel/events', {
      eventName,
      metadataJson: metadata ? JSON.stringify(metadata) : null,
    });
  } catch {
    // Funnel tracking should never block user actions.
  }
}
