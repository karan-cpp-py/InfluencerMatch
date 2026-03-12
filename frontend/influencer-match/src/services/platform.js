import { reactive } from 'vue';
import api from './api';

export const platformConfig = reactive({
  loaded: false,
  positioningLine: 'AI Creator Intelligence Platform for growth and sponsorship readiness.',
  phase: 'Brand Activation',
  features: {
    // Testing default: keep all major product surfaces accessible.
    enableMarketplace: true,
    enableBrandActivation: true,
    enableCreatorGraph: true,
  },
  phases: {
    creatorIntelligence: true,
    creatorGraph: true,
    creatorGraphPublicOptIn: true,
    brandActivation: true,
    brandPilotInviteOnly: false,
  },
  kpiGates: {
    activeCreatorsWeekly: 0,
    brandActivationCreatorThreshold: 1000,
    creatorThresholdReached: false,
  }
});

export async function loadPlatformConfig() {
  try {
    const res = await api.get('/platform/config');
    const data = res.data || {};
    platformConfig.positioningLine = data.positioningLine || platformConfig.positioningLine;
    platformConfig.phases = {
      ...platformConfig.phases,
      ...(data.phases || {})
    };

    // Testing default: do not hide or gate menus/routes by platform switches.
    platformConfig.features = {
      enableMarketplace: true,
      enableBrandActivation: true,
      enableCreatorGraph: Boolean(data.phases?.creatorGraph ?? true),
    };

    if (platformConfig.phases.creatorGraph) {
      platformConfig.phase = 'Creator Graph';
    } else {
      platformConfig.phase = 'Brand Activation';
    }

    platformConfig.kpiGates = {
      ...platformConfig.kpiGates,
      ...(data.kpiGates || {})
    };
  } catch {
    // Keep sensible creator-first defaults when config API is unavailable.
  } finally {
    platformConfig.loaded = true;
  }
}

export async function ensurePlatformConfigLoaded() {
  if (!platformConfig.loaded) {
    await loadPlatformConfig();
  }
}

export async function joinBrandWaitlist(payload) {
  const { data } = await api.post('/platform/brand-waitlist', payload);
  return data;
}
