<template>
  <div class="container-fluid py-4" style="max-width: 1080px; margin: 0 auto;">
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
      <div>
        <h3 class="fw-bold mb-1">Platform Setup Guides</h3>
        <p class="text-muted mb-0">Quick walkthroughs for onboarding and core workflows.</p>
      </div>
      <router-link to="/" class="btn btn-outline-secondary btn-sm">Back Home</router-link>
    </div>

    <section class="card border-0 shadow-sm mb-3">
      <div class="card-body">
        <h5 class="fw-semibold mb-2">Creator Dashboard Setup</h5>
        <ol class="mb-0 ps-3 small">
          <li>Open My Dashboard and complete profile details (country, language, category, bio).</li>
          <li>Link your YouTube channel URL from the Channel tab.</li>
          <li>For audience demographics, connect Google OAuth and paste returned code.</li>
          <li>Run Ingest from Connected Account to fetch demographics snapshot.</li>
        </ol>
      </div>
    </section>

    <section class="card border-0 shadow-sm mb-3">
      <div class="card-body">
        <h5 class="fw-semibold mb-2">Google OAuth Fix (redirect_uri_mismatch)</h5>
        <ol class="mb-0 ps-3 small">
          <li>In Google Cloud Console, open the OAuth client configured for this app.</li>
          <li>Add the exact redirect URI your frontend uses for creator dashboard, for example: <code>https://influencer-match-rho.vercel.app/creator-dashboard</code>.</li>
          <li>Add all active domains if you use multiple environments (Vercel + Render frontend domains).</li>
          <li>Save, wait 1-2 minutes, then retry Connect Google.</li>
        </ol>
      </div>
    </section>

    <section class="card border-0 shadow-sm mb-3">
      <div class="card-body">
        <h5 class="fw-semibold mb-2">Trending Videos</h5>
        <ol class="mb-0 ps-3 small">
          <li>Use country/category filters and refresh feed.</li>
          <li>Use Analyze with AI per video to open latest-video analysis directly.</li>
        </ol>
      </div>
    </section>

    <section class="card border-0 shadow-sm mb-3">
      <div class="card-body">
        <h5 class="fw-semibold mb-2">YouTube Search Intelligence</h5>
        <ol class="mb-0 ps-3 small">
          <li>Paste a channel URL and click Find Creator.</li>
          <li>Use Analyze Latest 10 Videos or Analyze Latest 20 Videos.</li>
          <li>Open Channel Analytics for detailed creator metrics.</li>
          <li>Use shortlist/compare actions for campaign planning.</li>
        </ol>
      </div>
    </section>
  </div>
</template>

<script setup>
</script>
