<template>
  <div class="py-4 unified-discovery-page">
    <div class="container-fluid" style="max-width: 1440px;">
      <div class="unified-hero mb-4">
        <div class="d-flex justify-content-between align-items-start gap-3 flex-wrap">
          <div>
            <div class="eyebrow mb-2">Unified Discovery</div>
            <h3 class="fw-bold mb-1">Creator Discovery Command Center</h3>
            <p class="mb-0 text-muted">All catalogue intelligence and advanced creator search in one place.</p>
          </div>
          <div class="d-flex gap-2">
            <button
              class="btn btn-sm"
              :class="activeTab === 'catalogue' ? 'btn-primary' : 'btn-outline-primary'"
              @click="setTab('catalogue')"
            >
              YouTube Catalogue
            </button>
            <button
              class="btn btn-sm"
              :class="activeTab === 'search' ? 'btn-primary' : 'btn-outline-primary'"
              @click="setTab('search')"
            >
              Advanced Search
            </button>
          </div>
        </div>
      </div>

      <div v-show="activeTab === 'catalogue'">
        <BrandYouTubeCreatorsView />
      </div>
      <div v-show="activeTab === 'search'">
        <CreatorSearch />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BrandYouTubeCreatorsView from './BrandYouTubeCreatorsView.vue';
import CreatorSearch from './CreatorSearch.vue';

const route = useRoute();
const router = useRouter();
const activeTab = ref(route.query.tab === 'search' ? 'search' : 'catalogue');

function setTab(tab) {
  activeTab.value = tab;
  router.replace({
    path: '/brand/youtube-creators',
    query: tab === 'search' ? { tab: 'search' } : {}
  });
}

watch(
  () => route.query.tab,
  (tab) => {
    activeTab.value = tab === 'search' ? 'search' : 'catalogue';
  }
);
</script>

<style scoped>
.unified-discovery-page {
  background:
    radial-gradient(circle at 8% 0%, rgba(14, 165, 233, 0.09), transparent 30%),
    radial-gradient(circle at 92% 10%, rgba(16, 185, 129, 0.1), transparent 34%);
}

.unified-hero {
  border-radius: 18px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  background: linear-gradient(125deg, #ffffff 0%, #f8fafc 56%, #eef6ff 100%);
  box-shadow: 0 8px 22px rgba(15, 23, 42, 0.07);
  padding: 1rem 1.2rem;
}

.eyebrow {
  display: inline-flex;
  border-radius: 999px;
  padding: 0.2rem 0.6rem;
  font-size: 0.68rem;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: #0f766e;
  background: rgba(204, 251, 241, 0.7);
}
</style>
