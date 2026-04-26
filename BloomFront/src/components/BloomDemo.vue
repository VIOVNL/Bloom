<template>
  <div class="bloom-stage" ref="stage">
    <div class="bloom-petals" ref="petalsContainer"></div>
    <button
      class="bloom-btn"
      :class="{ open: isOpen }"
      ref="btnEl"
      @click="toggle"
      aria-label="Toggle Bloom"
    >
      <svg width="36" height="36" viewBox="0 0 24 24">
        <path class="bp" data-i="0" d="M12,12 C8.5,10 7.8,6.5 12,4.5 C16.2,6.5 15.5,10 12,12 Z"/>
        <path class="bp" data-i="1" d="M12,12 C10.9,8.1 12.9,5.1 17.3,6.7 C18.9,11.1 15.9,13.1 12,12 Z"/>
        <path class="bp" data-i="2" d="M12,12 C14,8.5 17.5,7.8 19.5,12 C17.5,16.2 14,15.5 12,12 Z"/>
        <path class="bp" data-i="3" d="M12,12 C15.9,10.9 18.9,12.9 17.3,17.3 C12.9,18.9 10.9,15.9 12,12 Z"/>
        <path class="bp" data-i="4" d="M12,12 C15.5,14 16.2,17.5 12,19.5 C7.8,17.5 8.5,14 12,12 Z"/>
        <path class="bp" data-i="5" d="M12,12 C13.1,15.9 11.1,18.9 6.7,17.3 C5.1,12.9 8.1,10.9 12,12 Z"/>
        <path class="bp" data-i="6" d="M12,12 C10,15.5 6.5,16.2 4.5,12 C6.5,7.8 10,8.5 12,12 Z"/>
        <path class="bp" data-i="7" d="M12,12 C8.1,13.1 5.1,11.1 6.7,6.7 C11.1,5.1 13.1,8.1 12,12 Z"/>
        <circle class="bc" cx="12" cy="12" r="2.5"/>
      </svg>
    </button>
    <div class="bloom-ring bloom-ring--1"></div>
    <div class="bloom-ring bloom-ring--2"></div>
    <div class="bloom-tip" ref="tipEl"></div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const emit = defineEmits(['petal-click'])

const BUTTON_SIZE = 52
const PETAL_SIZE = 42
const REPEL_RADIUS = 60
const REPEL_STRENGTH = 6

const ITEMS = [
  { label:'Features',   action:'features',  icon:'<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>' },
  { label:'Download',   action:'download',  icon:'<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>' },
  { label:'Changelog',  action:'changelog', icon:'<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>' },
  { label:'Docs',       action:'docs',      icon:'<path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>' },
  { label:'Request',    action:'request',   icon:'<path d="M12 2L15.09 8.26L22 9.27L17 14.14L18.18 21.02L12 17.77L5.82 21.02L7 14.14L2 9.27L8.91 8.26L12 2Z"/>' },
  { label:'Bug Report', action:'bug',       icon:'<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>' },
]

const stage = ref(null)
const petalsContainer = ref(null)
const btnEl = ref(null)
const tipEl = ref(null)
const isOpen = ref(false)

let isAnimating = false
let petalEls = []
let petalPos = []
let mX = 0, mY = 0, rafId = null

function computeLayout(n) {
  const r = BUTTON_SIZE / 2 + PETAL_SIZE * 0.85
  const out = []
  for (let i = 0; i < n; i++) {
    const deg = (360 * i) / n - 90
    const rad = deg * Math.PI / 180
    out.push({ x: Math.cos(rad) * r, y: Math.sin(rad) * r })
  }
  return out
}

function setT(el, i, s, rx = 0, ry = 0) {
  const p = petalPos[i]
  el.style.transform = `translate(calc(-50% + ${p.x + rx}px), calc(-50% + ${p.y + ry}px)) scale(${s})`
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)) }

function createPetals() {
  const pos = computeLayout(ITEMS.length)
  const container = petalsContainer.value
  ITEMS.forEach((it, i) => {
    const el = document.createElement('div')
    el.className = 'bloom-petal'
    el.dataset.color = String(i)
    el.style.zIndex = i
    el.innerHTML = `<svg viewBox="0 0 24 24">${it.icon}</svg>`

    el.addEventListener('mouseenter', () => {
      if (!isOpen.value) return
      el.style.zIndex = '99'
      el.style.transition = 'transform .15s cubic-bezier(.33,1,.68,1),opacity .15s'
      el.dataset.h = '1'
      tipEl.value.textContent = it.label
      tipEl.value.style.opacity = '1'
      setT(el, i, 1.1)
    })
    el.addEventListener('mouseleave', () => {
      el.style.zIndex = String(i)
      el.dataset.h = ''
      tipEl.value.style.opacity = '0'
      if (isOpen.value) setT(el, i, 1)
    })
    el.addEventListener('mousemove', e => {
      const rect = stage.value.getBoundingClientRect()
      tipEl.value.style.left = (e.clientX - rect.left + 12) + 'px'
      tipEl.value.style.top = (e.clientY - rect.top + 12) + 'px'
    })
    el.addEventListener('click', () => {
      if (!isOpen.value) return
      emit('petal-click', it.action)
    })

    container.appendChild(el)
    petalEls.push(el)
    petalPos.push(pos[i])
  })
}

function onMove(e) {
  if (!stage.value) return
  const r = stage.value.getBoundingClientRect()
  mX = e.clientX - (r.left + r.width / 2)
  mY = e.clientY - (r.top + r.height / 2)
}

function repel() {
  if (!isOpen.value) { rafId = null; return }
  petalEls.forEach((el, i) => {
    if (el.dataset.h === '1') return
    const p = petalPos[i]
    const dx = p.x - mX, dy = p.y - mY
    const d = Math.sqrt(dx * dx + dy * dy)
    let rx = 0, ry = 0
    if (d < REPEL_RADIUS && d > 0.1) {
      const str = (1 - d / REPEL_RADIUS) * REPEL_STRENGTH
      const a = Math.atan2(dy, dx)
      rx = Math.cos(a) * str
      ry = Math.sin(a) * str
    }
    el.style.transition = 'transform .12s ease-out,opacity .15s,background .15s,border-color .15s,box-shadow .15s'
    setT(el, i, 1, rx, ry)
  })
  rafId = requestAnimationFrame(repel)
}

async function bloomOpen() {
  isAnimating = true
  petalEls.forEach(el => {
    el.style.transition = 'none'
    el.style.opacity = '0'
    el.style.transform = 'translate(-50%,-50%) scale(0)'
    el.style.pointerEvents = 'auto'
  })
  void petalsContainer.value.offsetHeight

  for (let i = 0; i < petalEls.length; i++) {
    const el = petalEls[i]
    el.style.transition = 'opacity .18s cubic-bezier(.33,1,.68,1),transform .28s cubic-bezier(.33,1,.68,1)'
    el.style.opacity = '1'
    setT(el, i, 1)
    if (i < petalEls.length - 1) await sleep(20)
  }
  await sleep(300)
  isOpen.value = true
  isAnimating = false
  if (!rafId) rafId = requestAnimationFrame(repel)
}

async function bloomClose() {
  isAnimating = true
  isOpen.value = false
  if (rafId) { cancelAnimationFrame(rafId); rafId = null }

  for (let i = petalEls.length - 1; i >= 0; i--) {
    const el = petalEls[i]
    el.style.transition = 'opacity .25s cubic-bezier(.33,1,.68,1),transform .3s cubic-bezier(.55,0,1,.45)'
    el.style.opacity = '0'
    el.style.transform = 'translate(-50%,-50%) scale(0.15)'
    el.style.pointerEvents = 'none'
    el.style.background = ''; el.style.borderColor = ''; el.style.boxShadow = ''
    el.dataset.h = ''
    if (i > 0) await sleep(30)
  }
  await sleep(320)
  isAnimating = false
}

function toggle() {
  if (!isAnimating) isOpen.value ? bloomClose() : bloomOpen()
}

onMounted(() => {
  createPetals()
  document.addEventListener('mousemove', onMove)
  setTimeout(() => bloomOpen(), 600)
})

onUnmounted(() => {
  document.removeEventListener('mousemove', onMove)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>
