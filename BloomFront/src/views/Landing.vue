<template>
  <section class="hero">
    <BloomDemo @petal-click="handlePetal" />

    <div class="hero__text">
      <p class="hero__above">One button. Every shortcut.</p>
      <h1 class="hero__title">Bloom</h1>
      <p class="hero__sub">A radial petal launcher that sits on your desktop. Click it, and your apps, folders, commands, system actions, and keyboard shortcuts fan out around it.</p>
      <div class="hero__cta">
        <a href="/downloads/Bloom-win-Setup.exe" @click="trackDownload" class="btn-primary">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
          Download for Windows
        </a>
        <div style="display:flex;gap:8px">
          <a :href="REQUEST_URL" target="_blank" rel="noopener" class="btn-secondary">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2L15.09 8.26L22 9.27L17 14.14L18.18 21.02L12 17.77L5.82 21.02L7 14.14L2 9.27L8.91 8.26L12 2Z"/></svg>
            Request a Feature
          </a>
          <a :href="BUG_URL" target="_blank" rel="noopener" class="btn-secondary">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
            Report a Bug
          </a>
        </div>
        <span class="hero__meta">Free &middot; Windows 10+ &middot; .NET 10 &middot; v{{ latestVersion }}</span>
      </div>
    </div>
  </section>
</template>

<script setup>
import BloomDemo from '../components/BloomDemo.vue'
import changelog from '../../changelog.json'

const REPO_URL = 'https://github.com/VIOVNL/Bloom'
const REQUEST_URL = `${REPO_URL}/issues/new?labels=enhancement`
const BUG_URL = `${REPO_URL}/issues/new?labels=bug`
const RELEASES_URL = `${REPO_URL}/releases`
const README_URL = `${REPO_URL}#readme`

const latestVersion = changelog[0].version

function handlePetal(action) {
  const targets = {
    request: REQUEST_URL,
    bug: BUG_URL,
    changelog: RELEASES_URL,
    docs: README_URL,
    features: README_URL,
  }
  if (targets[action]) {
    window.open(targets[action], '_blank', 'noopener')
  } else if (action === 'download') {
    trackDownload()
    window.location.href = '/downloads/Bloom-win-Setup.exe'
  }
}

function trackDownload() {
  fetch('/api/downloads', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source: 'landing' }),
  }).catch(() => {})
}
</script>
