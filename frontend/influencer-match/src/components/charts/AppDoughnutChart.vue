<template>
  <Doughnut :data="data" :options="mergedOptions" />
</template>

<script setup>
import { computed } from 'vue';
import { Doughnut } from 'vue-chartjs';
import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend,
} from 'chart.js';

ChartJS.register(ArcElement, Tooltip, Legend);

const props = defineProps({
  data: {
    type: Object,
    required: true,
  },
  options: {
    type: Object,
    default: () => ({}),
  },
});

const mergedOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  animation: {
    duration: 620,
    easing: 'easeOutCubic',
  },
  transitions: {
    active: {
      animation: {
        duration: 220,
      },
    },
  },
  cutout: '62%',
  plugins: {
    legend: {
      position: 'bottom',
      labels: {
        boxWidth: 10,
        usePointStyle: true,
      },
    },
  },
  ...props.options,
}));
</script>
