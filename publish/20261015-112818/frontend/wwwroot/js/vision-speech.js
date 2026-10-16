

import { FaceLandmarker, FilesetResolver } from '@mediapipe/tasks-vision';

const TICK_MS = 300;
const MAR_N = 12;
const MAR_OPEN = 0.12;
const MAR_VAR = 0.0004;
const WASM_URL = '/lib/mediapipe/wasm';
const MODEL_URL = '/lib/mediapipe/face_landmarker.task';

const LIP_TOP = 13, LIP_BOTTOM = 14, MOUTH_LEFT = 61, MOUTH_RIGHT = 291;

const state = { mode: 'off', speaking: false, face: false, mar: 0, lastSpeakAt: 0, lastError: '', detectError: false };
let landmarker = null;
let lastVideoTime = -1;
let frozenTicks = 0;
let videoEl = null;
let marHist = [];
let initBusy = false;

function marOf(lm) {
  const d = (a, b) => Math.hypot(lm[a].x - lm[b].x, lm[a].y - lm[b].y);
  const horiz = Math.max(1e-6, d(MOUTH_LEFT, MOUTH_RIGHT));
  return d(LIP_TOP, LIP_BOTTOM) / horiz;
}

async function makeLandmarker(delegate) {
  const vision = await FilesetResolver.forVisionTasks(WASM_URL);
  return FaceLandmarker.createFromOptions(vision, {
    baseOptions: { modelAssetPath: MODEL_URL, delegate: delegate || 'GPU' },
    runningMode: 'VIDEO',
    numFaces: 1,
    minFaceDetectionConfidence: 0.5,
    minFacePresenceConfidence: 0.5,
    minTrackingConfidence: 0.5
  });
}

async function ensureInit() {
  if (landmarker || initBusy) return !!landmarker;
  initBusy = true;
  try {
    try {
      landmarker = await makeLandmarker('GPU');
    } catch (eGpu) {
      try { landmarker = await makeLandmarker('CPU'); }
      catch (eCpu) { throw eCpu; }
    }
    state.mode = 'on';
    state.lastError = '';
    return true;
  } catch (err) {
    state.mode = 'off';
    state.lastError = (err && err.message) || String(err);
    try { console.warn('[visual] init gagal:', state.lastError); } catch { }
    return false;
  } finally {
    initBusy = false;
  }
}

function tick() {
  try {
    if (!landmarker) return;
    videoEl = videoEl || document.getElementById('camMini');
    if (!videoEl || !videoEl.videoWidth) return;
    if (videoEl.currentTime === lastVideoTime) {
      frozenTicks++;
      if (frozenTicks > 30 && videoEl.paused && videoEl.srcObject) {
        try { videoEl.play().catch(() => { }); } catch { }
        frozenTicks = 0;
      }
      return;
    }
    frozenTicks = 0;
    lastVideoTime = videoEl.currentTime;
    const res = landmarker.detectForVideo(videoEl, performance.now());
    state.detectError = false;
    const face = res && res.faceLandmarks && res.faceLandmarks[0];
    state.face = !!face;
    if (!face) {
      state.speaking = false;
      state.mar = 0;
      marHist = [];
      return;
    }
    const mar = marOf(face);
    state.mar = mar;
    marHist.push(mar);
    if (marHist.length > MAR_N) marHist.shift();
    let mean = 0;
    for (const m of marHist) mean += m;
    mean /= marHist.length;
    let v = 0;
    for (const m of marHist) v += (m - mean) * (m - mean);
    v /= marHist.length;

    state.speaking = mean > MAR_OPEN && v > MAR_VAR;
    if (state.speaking) state.lastSpeakAt = Date.now();
  } catch (err) {
    if (!state.detectError) {
      state.detectError = true;
      state.lastError = 'detect: ' + ((err && err.message) || String(err));
      try { console.warn('[visual] detect gagal:', state.lastError); } catch { }
    }
  }
}

setInterval(tick, TICK_MS);
setInterval(() => {
  try { if (!landmarker && !initBusy) ensureInit(); } catch { }
}, 15000);

window.__visual = state;
window.__visualInit = () => { ensureInit(); };
