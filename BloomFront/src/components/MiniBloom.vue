<template>
  <div
    class="mini-bloom"
    :style="{ width: stageSize + 'px', height: stageSize + 'px' }"
    ref="stage"
  >
    <div class="mini-bloom__petals" ref="petalsContainer"></div>
    <button
      class="bloom-btn"
      :class="{ open: isOpen }"
      @click="toggle"
      @contextmenu.prevent="toggleSettingsBloom"
      aria-label="Toggle Bloom"
      :style="{ width: btnSize + 'px', height: btnSize + 'px', borderRadius: btnSize/2 + 'px' }"
    >
      <svg width="24" height="24" viewBox="0 0 24 24">
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
    <div class="bloom-tip" ref="tipEl"></div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'

const props = defineProps({
  items: { type: Array, required: true },
  settingsItems: { type: Array, default: () => [] },
  labelMode: { type: String, default: 'tooltip' },
  autoOpen: { type: Boolean, default: true },
  stageSize: { type: Number, default: 200 },
  btnSize: { type: Number, default: 40 },
  petalSize: { type: Number, default: 34 },
})

const emit = defineEmits(['petal-right-click'])

const REPEL_RADIUS = 50
const REPEL_STRENGTH = 5

const stage = ref(null)
const petalsContainer = ref(null)
const tipEl = ref(null)
const isOpen = ref(false)
const isSettingsMode = ref(false)

let isAnimating = false
let petalEls = []
let petalPos = []
let mX = 0, mY = 0, rafId = null

function computeLayerCounts(total) {
  if (total <= 5) return [total]
  if (total <= 10) {
    const inner = Math.max(3, Math.ceil(total * 0.6))
    return [inner, total - inner]
  }
  if (total <= 24) {
    const inner = Math.max(4, Math.floor(total * 0.35))
    return [inner, total - inner]
  }
  const inner = Math.max(5, Math.floor(total * 0.15))
  const middle = Math.floor(total * 0.35)
  return [inner, middle, total - inner - middle]
}

function computeLayout(n) {
  const layers = computeLayerCounts(n)
  const radii = []
  radii[0] = props.btnSize / 2 + props.petalSize * 0.3
  for (let i = 1; i < layers.length; i++) {
    radii[i] = radii[i - 1] + props.petalSize * 0.6
  }

  const out = []
  let idx = 0
  for (let layer = 0; layer < layers.length; layer++) {
    const count = layers[layer]
    const r = radii[layer]

    let rotationOffset = 0
    if (layer > 0) {
      const prevSpacing = 360 / layers[layer - 1]
      rotationOffset = prevSpacing / 2
    }

    for (let i = 0; i < count; i++) {
      const deg = (360 * i) / count - 90 + rotationOffset
      const rad = deg * Math.PI / 180
      out.push({ x: Math.cos(rad) * r, y: Math.sin(rad) * r })
      idx++
    }
  }
  return { positions: out, ringCount: layers.length }
}

function setT(el, i, s, rx = 0, ry = 0) {
  const p = petalPos[i]
  if (!p) return
  el.style.transform = `translate(calc(-50% + ${p.x + rx}px), calc(-50% + ${p.y + ry}px)) scale(${s})`
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)) }

function activeItems() {
  return isSettingsMode.value && props.settingsItems.length > 0
    ? props.settingsItems
    : props.items
}

function buildPetals() {
  const container = petalsContainer.value
  if (!container) return
  container.innerHTML = ''
  petalEls = []
  petalPos = []

  const items = activeItems()
  const { positions } = computeLayout(items.length)
  items.forEach((it, i) => {
    const el = document.createElement('div')
    el.className = 'mini-bloom__petal'

    if (isSettingsMode.value && it.color) {
      el.dataset.color = 'custom'
    } else {
      el.dataset.color = String(i % 8)
    }

    el.style.zIndex = i
    el.style.width = props.petalSize + 'px'
    el.style.height = props.petalSize + 'px'

    // Icon
    const iconEl = document.createElement('div')
    iconEl.className = 'mini-bloom__petal-icon'
    const svgEl = `<svg viewBox="0 0 24 24"${isSettingsMode.value && it.color ? ` style="stroke:${it.color}"` : ''}>${it.icon}</svg>`
    iconEl.innerHTML = svgEl
    el.appendChild(iconEl)

    // Label (for below/overlay modes)
    if (props.labelMode === 'below' || props.labelMode === 'overlay') {
      const lbl = document.createElement('div')
      lbl.className = 'mini-bloom__label mini-bloom__label--' + props.labelMode
      lbl.textContent = it.label
      el.appendChild(lbl)
    }

    el.addEventListener('mouseenter', () => {
      if (!isOpen.value) return
      el.style.zIndex = '99'
      el.style.transition = 'transform .15s cubic-bezier(.33,1,.68,1),opacity .15s'
      el.dataset.h = '1'
      setT(el, i, 1.1)
      if (props.labelMode === 'tooltip' && tipEl.value) {
        tipEl.value.textContent = it.label
        tipEl.value.style.opacity = '1'
      }
    })
    el.addEventListener('mouseleave', () => {
      el.style.zIndex = String(i)
      el.dataset.h = ''
      if (props.labelMode === 'tooltip' && tipEl.value) {
        tipEl.value.style.opacity = '0'
      }
      if (isOpen.value) setT(el, i, 1)
    })
    el.addEventListener('mousemove', e => {
      if (props.labelMode === 'tooltip' && tipEl.value && stage.value) {
        const rect = stage.value.getBoundingClientRect()
        tipEl.value.style.left = (e.clientX - rect.left + 10) + 'px'
        tipEl.value.style.top = (e.clientY - rect.top + 10) + 'px'
      }
    })

    // Right-click on petal
    el.addEventListener('contextmenu', e => {
      e.preventDefault()
      const rect = el.getBoundingClientRect()
      emit('petal-right-click', { index: i, item: it, rect })
    })

    container.appendChild(el)
    petalEls.push(el)
    petalPos.push(positions[i])
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
    if (!p) return
    const dx = p.x - mX, dy = p.y - mY
    const d = Math.sqrt(dx * dx + dy * dy)
    let rx = 0, ry = 0
    if (d < REPEL_RADIUS && d > 0.1) {
      const str = (1 - d / REPEL_RADIUS) * REPEL_STRENGTH
      const a = Math.atan2(dy, dx)
      rx = Math.cos(a) * str
      ry = Math.sin(a) * str
    }
    el.style.transition = 'transform .12s ease-out,opacity .15s'
    setT(el, i, 1, rx, ry)
  })
  rafId = requestAnimationFrame(repel)
}

async function bloomOpen() {
  if (isAnimating) return
  isAnimating = true
  petalEls.forEach(el => {
    el.style.transition = 'none'
    el.style.opacity = '0'
    el.style.transform = 'translate(-50%,-50%) scale(0)'
    el.style.pointerEvents = 'auto'
  })
  if (petalsContainer.value) void petalsContainer.value.offsetHeight

  for (let i = 0; i < petalEls.length; i++) {
    const el = petalEls[i]
    el.style.transition = 'opacity .18s cubic-bezier(.33,1,.68,1),transform .28s cubic-bezier(.33,1,.68,1)'
    el.style.opacity = '1'
    setT(el, i, 1)
    if (i < petalEls.length - 1) await sleep(20)
  }
  await sleep(280)
  isOpen.value = true
  isAnimating = false
  if (!rafId) rafId = requestAnimationFrame(repel)
}

async function bloomClose() {
  if (isAnimating) return
  isAnimating = true
  isOpen.value = false
  if (rafId) { cancelAnimationFrame(rafId); rafId = null }

  for (let i = petalEls.length - 1; i >= 0; i--) {
    const el = petalEls[i]
    el.style.transition = 'opacity .2s cubic-bezier(.33,1,.68,1),transform .25s cubic-bezier(.55,0,1,.45)'
    el.style.opacity = '0'
    el.style.transform = 'translate(-50%,-50%) scale(0.15)'
    el.style.pointerEvents = 'none'
    el.dataset.h = ''
    if (i > 0) await sleep(25)
  }
  await sleep(280)
  isAnimating = false
}

async function toggle() {
  if (isAnimating) return
  if (isSettingsMode.value && isOpen.value) {
    // Settings open + left-click → close settings, switch to app, open app
    await bloomClose()
    isSettingsMode.value = false
    buildPetals()
    await nextTick()
    setTimeout(() => bloomOpen(), 100)
  } else {
    isOpen.value ? bloomClose() : bloomOpen()
  }
}

async function toggleSettingsBloom() {
  if (props.settingsItems.length === 0) return
  if (isAnimating) return
  if (isSettingsMode.value && isOpen.value) {
    // Settings open + right-click → just close (nothing open)
    await bloomClose()
    isSettingsMode.value = false
    buildPetals()
  } else {
    // App open or nothing open + right-click → close current, open settings
    if (isOpen.value) await bloomClose()
    isSettingsMode.value = true
    buildPetals()
    await nextTick()
    setTimeout(() => bloomOpen(), 100)
  }
}

// Rebuild petals when labelMode changes
watch(() => props.labelMode, async () => {
  if (isOpen.value) await bloomClose()
  isSettingsMode.value = false
  buildPetals()
  await nextTick()
  if (props.autoOpen) setTimeout(() => bloomOpen(), 100)
})

onMounted(() => {
  buildPetals()
  document.addEventListener('mousemove', onMove)
  if (props.autoOpen) setTimeout(() => bloomOpen(), 400)
})

onUnmounted(() => {
  document.removeEventListener('mousemove', onMove)
  if (rafId) cancelAnimationFrame(rafId)
})
</script>

<style>
.mini-bloom {
  position: relative;
  flex-shrink: 0;
}
.mini-bloom__petals {
  position: absolute;
  inset: 0;
  pointer-events: none;
}
.mini-bloom__petal {
  position: absolute;
  border-radius: 50%;
  background: rgba(34,34,34,0.88);
  border: 1px solid rgba(255,255,255,0.07);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  cursor: pointer;
  pointer-events: auto;
  opacity: 0;
  transform: translate(-50%,-50%) scale(0);
  top: 50%;
  left: 50%;
  will-change: transform, opacity;
}
.mini-bloom__petal:hover { z-index: 99 !important }
.mini-bloom__petal-icon {
  display: flex;
  align-items: center;
  justify-content: center;
}
.mini-bloom__petal-icon svg {
  width: 15px;
  height: 15px;
  stroke-width: 1.8;
  fill: none;
  stroke-linecap: round;
  stroke-linejoin: round;
  transition: stroke .15s;
}
/* Petal colors */
.mini-bloom__petal[data-color="0"] svg { stroke: var(--p1) }
.mini-bloom__petal[data-color="1"] svg { stroke: var(--p2) }
.mini-bloom__petal[data-color="2"] svg { stroke: var(--p3) }
.mini-bloom__petal[data-color="3"] svg { stroke: var(--p4) }
.mini-bloom__petal[data-color="4"] svg { stroke: var(--p5) }
.mini-bloom__petal[data-color="5"] svg { stroke: var(--p6) }
.mini-bloom__petal[data-color="6"] svg { stroke: var(--p7) }
.mini-bloom__petal[data-color="7"] svg { stroke: var(--p8) }

/* Label: Below */
.mini-bloom__label--below {
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  margin-top: 4px;
  font-size: 8px;
  font-weight: 600;
  color: var(--t3);
  white-space: nowrap;
  pointer-events: none;
}
/* Label: Overlay */
.mini-bloom__label--overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 7px;
  font-weight: 700;
  color: var(--t1);
  text-align: center;
  line-height: 1.1;
  padding: 2px;
  pointer-events: none;
  text-shadow: 0 1px 3px rgba(0,0,0,0.6);
}
</style>
