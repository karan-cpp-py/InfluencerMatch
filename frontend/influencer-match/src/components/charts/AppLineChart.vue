<template>
  <Line :data="data" :options="mergedOptions" />
</template>

<script setup>
import { computed } from 'vue';
import { Line } from 'vue-chartjs';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Legend, Filler);

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
    duration: 700,
    easing: 'easeOutQuart',
  },
  transitions: {
    active: {
      animation: {
        duration: 280,
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
