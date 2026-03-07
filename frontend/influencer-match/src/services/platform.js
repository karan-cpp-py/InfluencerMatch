import { reactive } from 'vue';
import api from './api';

export const platformConfig = reactive({
  loaded: false,
  positioningLine: 'AI Creator Intelligence Platform for growth and sponsorship readiness.',
  phase: 'Creator Intelligence',
  features: {
    enableMarketplace: false,
    enableBrandActivation: false,
    enableCreatorGraph: true,
  },
  phases: {
    creatorIntelligence: true,
    creatorGraph: true,
    creatorGraphPublicOptIn: true,
    brandActivation: false,
    brandPilotInviteOnly: true,
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

    platformConfig.features = {
      enableMarketplace: Boolean(data.phases?.creatorGraph),
      enableBrandActivation: Boolean(data.phases?.brandActivation),
      enableCreatorGraph: Boolean(data.phases?.creatorGraph),
    };

    if (platformConfig.phases.brandActivation) {
      platformConfig.phase = 'Brand Activation';
    } else if (platformConfig.phases.creatorGraph) {
      platformConfig.phase = 'Creator Graph';
    } else {
      platformConfig.phase = 'Creator Intelligence';
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
