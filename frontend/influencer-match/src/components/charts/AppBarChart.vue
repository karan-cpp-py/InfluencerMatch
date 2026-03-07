<template>
  <Bar :data="data" :options="mergedOptions" />
</template>

<script setup>
import { computed } from 'vue';
import { Bar } from 'vue-chartjs';
import {
  Chart as ChartJS,
  BarElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
} from 'chart.js';

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend);

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
    duration: 650,
    easing: 'easeOutCubic',
  },
  transitions: {
    active: {
      animation: {
        duration: 250,
      },
    },
  },
  plugins: {
    legend: {
      display: true,
      labels: {
        boxWidth: 10,
        usePointStyle: true,
      },
    },
  },
  ...props.options,
}));
</script>
