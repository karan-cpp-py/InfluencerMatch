<template>
  <div v-if="visible" class="ai-modal-backdrop" @click.self="close">
    <div class="ai-modal-panel">
      <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
        <div>
          <h5 class="mb-1">AI Video Analysis</h5>
          <div class="small text-muted">
            {{ result?.video_summary?.metadata?.publish_date || currentVideo?.title || 'Video analysis' }}
          </div>
        </div>
        <button class="btn btn-sm btn-outline-secondary" @click="close">Close</button>
      </div>

      <div v-if="loading" class="py-3 text-center text-muted">
        <span class="spinner-border spinner-border-sm me-2"></span>
        Running LLM analysis...
      </div>

      <div v-else-if="error" class="alert alert-warning py-2 mb-0">{{ error }}</div>

      <div v-else-if="result">
        <p class="small fw-semibold mb-2">
          {{ result.video_summary?.ai_summary || result.video_summary?.summary || 'No AI summary available.' }}
        </p>
        <div class="small mb-2"><strong>Sentiment:</strong> {{ result.comment_intelligence?.overall_sentiment || 'N/A' }}</div>
        <div class="small mb-2"><strong>Top themes:</strong> {{ joinList(result.comment_intelligence?.top_5_themes) }}</div>
        <div class="small mb-2">
          <strong>Verdict:</strong>
          {{ result.ai_final_verdict?.go_no_go || 'N/A' }}
          · Score {{ result.ai_final_verdict?.brand_readiness_score ?? 'N/A' }}
        </div>
        <div class="small text-muted" v-if="result.ai_final_verdict?.final_verdict">
          {{ result.ai_final_verdict.final_verdict }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import api from '../services/api';

const visible = ref(false);
const loading = ref(false);
const error = ref('');
const result = ref(null);
const currentVideo = ref(null);

function close() {
  visible.value = false;
}

function joinList(list) {
  return Array.isArray(list) && list.length ? list.join(', ') : 'N/A';
}

async function open(video, creatorName = '') {
  if (!video?.videoId) {
    error.value = 'Missing video id for AI analysis.';
    result.value = null;
    visible.value = true;
    return;
  }

  currentVideo.value = video;
  visible.value = true;
  loading.value = true;
  error.value = '';
  result.value = null;

  try {
    const payload = {
      creatorName: creatorName || video.channelName || 'Creator',
      channelId: video.channelId || null,
      autoFetchComments: true,
      maxCommentsToFetch: 250,
      video: {
        videoId: video.videoId,
        title: video.title || '',
        description: video.description || '',
        tags: Array.isArray(video.tags) ? video.tags : [],
        publishedAt: video.publishedAt || null,
        statistics: {
          viewCount: Number(video.viewCount || 0),
          likeCount: Number(video.likeCount || 0),
          commentCount: Number(video.commentCount || 0)
        }
      }
    };

    const { data } = await api.post('/videos/latest-analysis', payload);
    result.value = data;
  } catch (e) {
    error.value = e?.userMessage || e?.response?.data?.error || 'AI analysis failed for this video.';
  } finally {
    loading.value = false;
  }
}

defineExpose({ open, close });
</script>

<style scoped>
.ai-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1060;
  background: rgba(15, 23, 42, 0.45);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 2rem 1rem;
}

.ai-modal-panel {
  width: min(820px, 100%);
  background: #fff;
  border-radius: 14px;
  padding: 1rem;
  box-shadow: 0 22px 48px rgba(15, 23, 42, 0.28);
  max-height: calc(100vh - 4rem);
  overflow-y: auto;
}
</style>
