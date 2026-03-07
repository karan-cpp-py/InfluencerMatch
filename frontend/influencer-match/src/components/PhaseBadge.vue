<template>
  <span class="phase-badge" :class="toneClass">{{ label }}</span>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  phase: {
    type: String,
    default: 'Creator Intelligence',
  },
});

const label = computed(() => `Phase: ${props.phase}`);

const toneClass = computed(() => {
  if (props.phase === 'Brand Activation') return 'phase-brand';
  if (props.phase === 'Creator Graph') return 'phase-graph';
  return 'phase-intelligence';
});
</script>

<style scoped>
.phase-badge {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  padding: 0.25rem 0.65rem;
}

.phase-intelligence {
  color: #1e3a8a;
  background: #dbeafe;
}

.phase-graph {
  color: #155e75;
  background: #cffafe;
}

.phase-brand {
  color: #7c2d12;
  background: #ffedd5;
}
</style>
