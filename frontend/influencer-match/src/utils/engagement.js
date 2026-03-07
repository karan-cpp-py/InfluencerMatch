export function normalizeEngagement(value, mode = 'auto') {
  const n = Number(value);
  if (!Number.isFinite(n) || n < 0) {
    return null;
  }

  if (mode === 'ratio') {
    return n * 100;
  }

  if (mode === 'percent') {
    return n;
  }

  // Auto mode handles mixed payloads: ratios (0-1) and percent points (>1).
  return n <= 1 ? n * 100 : n;
}

export function formatEngagement(value, options = {}) {
  const {
    decimals = 2,
    mode = 'auto',
    fallback = '--'
  } = options;

  const pct = normalizeEngagement(value, mode);
  if (pct == null) {
    return fallback;
  }

  return `${pct.toFixed(decimals)}%`;
}

export function engagementClass(value, options = {}) {
  const { mode = 'auto' } = options;
  const pct = normalizeEngagement(value, mode);

  if (pct == null) {
    return 'text-muted';
  }

  if (pct >= 5) return 'text-success fw-bold';
  if (pct >= 2) return 'text-warning';
  return 'text-danger';
}

export function engagementMeta(value, options = {}) {
  const {
    mode = 'auto',
    decimals = 2,
    fallback = '--',
    estimated = false,
    sampleCount = null,
    minSampleCount = 3
  } = options;

  const pct = normalizeEngagement(value, mode);
  const formatted = pct == null ? fallback : `${pct.toFixed(decimals)}%`;

  let status = 'exact';
  let badgeText = '';
  let badgeClass = '';
  let tooltip = '';

  if (pct == null) {
    status = 'low-data';
    badgeText = 'Low data';
    badgeClass = 'bg-secondary-subtle text-secondary border';
    tooltip = 'Not enough reliable analytics data to compute engagement.';
  } else if (estimated) {
    status = 'estimated';
    badgeText = 'Estimated';
    badgeClass = 'bg-info-subtle text-info border';
    tooltip = 'Engagement is estimated from recent video performance.';
  }

  if (status === 'exact' && Number.isFinite(sampleCount) && sampleCount < minSampleCount) {
    status = 'low-data';
    badgeText = 'Low data';
    badgeClass = 'bg-secondary-subtle text-secondary border';
    tooltip = `Based on a small sample (${sampleCount} items).`;
  }

  return {
    status,
    percent: pct,
    formatted,
    className: engagementClass(value, { mode }),
    badgeText,
    badgeClass,
    tooltip
  };
}
