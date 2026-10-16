const video = document.getElementById('cam');
const overlay = document.getElementById('overlay');
const statusEl = document.getElementById('visionStatus');
const inferEl = document.getElementById('inferInfo');
let stream = null;

function setStatus(t) { statusEl.textContent = t; }

async function loadInfo() {
  try {
    const r = await fetch('/Vision/Info');
    const j = await r.json();
    document.getElementById('modelInfo').textContent =
      `Model: ${j.model} • input ${j.inputSize}px • default: ${(j.defaultClasses || []).join(', ')}`;
  } catch { document.getElementById('modelInfo').textContent = 'Model: backend tidak terjangkau.'; }
}

function drawBoxes(canvas, imgW, imgH, detections, dispW, dispH) {
  canvas.width = dispW; canvas.height = dispH;
  const ctx = canvas.getContext('2d');
  const sx = dispW / imgW, sy = dispH / imgH;
  ctx.lineWidth = 2;
  ctx.font = '14px sans-serif';
  detections.forEach(d => {
    const x = d.x * sx, y = d.y * sy, w = d.width * sx, h = d.height * sy;
    ctx.strokeStyle = '#00ff00';
    ctx.strokeRect(x, y, w, h);
    const label = `${d.label} ${(d.confidence * 100).toFixed(0)}%`;
    const tw = ctx.measureText(label).width + 8;
    ctx.fillStyle = '#00ff00';
    ctx.fillRect(x, y - 18, tw, 18);
    ctx.fillStyle = '#000';
    ctx.fillText(label, x + 4, y - 4);
  });
}

async function detectBlob(blob, onResult) {
  const fd = new FormData();
  fd.append('image', blob, 'frame.jpg');
  const r = await fetch('/Vision/Detect', { method: 'POST', body: fd });
  const j = await r.json();
  if (!r.ok) throw new Error(j.error || 'deteksi gagal');
  onResult(j);
}

function setCamUI(on) {
  document.getElementById('btnCam').innerHTML = on
    ? '<i class="fas fa-video-slash"></i> Matikan kamera'
    : '<i class="fas fa-video"></i> Nyalakan kamera';
  document.getElementById('btnGuide').disabled = !on;
  if (!on) document.getElementById('btnSnap').disabled = true;
}

document.getElementById('btnCam').onclick = async function () {
  if (stream) {
    stream.getTracks().forEach(t => t.stop());
    stream = null; video.srcObject = null;
    setCamUI(false);
    setStatus('Kamera mati.');
    return;
  }

  try {
    stream = await navigator.mediaDevices.getUserMedia({ video: { width: 1280 }, audio: false });
    video.srcObject = stream;
    await video.play();
    setCamUI(true);
    document.getElementById('btnSnap').disabled = false;
    setStatus('Kamera nyala. Klik "Deteksi frame ini".');
  } catch (e) { setStatus('Kamera error: ' + e.message); }
};

document.addEventListener('visibilitychange', () => {
  if (document.hidden && stream) {
    stream.getTracks().forEach(t => t.stop());
    stream = null; video.srcObject = null;
    setCamUI(false);
    setStatus('Kamera dilepas (tab tidak aktif). Nyalakan lagi bila kembali.');
  }
});

document.getElementById('btnSnap').onclick = async () => {
  if (!stream) return;
  setStatus('Mendeteksi...');
  const c = document.createElement('canvas');
  c.width = video.videoWidth; c.height = video.videoHeight;
  c.getContext('2d').drawImage(video, 0, 0);
  const blob = await new Promise(res => c.toBlob(res, 'image/jpeg', 0.9));
  try {
    await detectBlob(blob, (j) => {
      const rect = video.getBoundingClientRect();
      overlay.style.width = rect.width + 'px';
      overlay.style.height = rect.height + 'px';
      drawBoxes(overlay, j.imageWidth, j.imageHeight, j.detections, video.clientWidth, video.clientHeight);
      inferEl.textContent = `${j.detections.length} orang • ${j.inferenceMs} ms`;
      setStatus(`Ketemu ${j.detections.length} orang.`);
    });
  } catch (e) { setStatus('Error: ' + e.message); }
};

document.getElementById('btnUpload').onclick = async () => {
  const f = document.getElementById('fileInput').files[0];
  if (!f) { setStatus('Pilih file gambar dulu.'); return; }
  setStatus('Mendeteksi ' + f.name + '...');
  const url = URL.createObjectURL(f);
  const img = document.getElementById('preview');
  img.src = url;
  await new Promise(res => { img.onload = res; });
  try {
    await detectBlob(f, (j) => {
      const ov = document.getElementById('previewOverlay');
      ov.style.display = 'block';
      img.style.display = 'none';
      drawBoxes(ov, j.imageWidth, j.imageHeight, j.detections, img.clientWidth, img.clientHeight || img.clientWidth * j.imageHeight / j.imageWidth);
      setStatus(`Ketemu ${j.detections.length} orang di ${f.name} (${j.inferenceMs} ms).`);
    });
  } catch (e) { setStatus('Error: ' + e.message); }
};

loadInfo();

let guideOn = false;
let guideSessionId = null;
let greeted = false;
let noPersonSince = null;
let scanTimer = null;
let guideListening = false;
let guideRec = null;
let guideSpeakerOn = true;
const SR2 = window.SpeechRecognition || window.webkitSpeechRecognition;


const GREETING = 'Halo, saya Mira. Ada yang bisa saya bantu?';

function guideLog(role, text) {
  const box = document.getElementById('guideLog');
  const div = document.createElement('div');
  div.className = 'direct-chat-msg' + (role === 'Kamu' ? ' right' : '');
  div.innerHTML = `<div class="direct-chat-infos clearfix">
      <span class="direct-chat-name float-${role === 'Kamu' ? 'right' : 'left'}">${role}</span></div>
    <div class="direct-chat-text" style="white-space:pre-wrap">${escapeHtml2(text)}</div>`;
  box.appendChild(div);
  box.scrollTop = box.scrollHeight;
}
function escapeHtml2(s) {
  return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
function cleanSpeech(s) {
  return (s || '').replace(/```[\s\S]*?```/g, ' ').replace(/`([^`]*)`/g, '$1')
    .replace(/^#{1,6}\s+/gm, '').replace(/(\*\*|__)(.*?)\1/g, '$2')
    .replace(/^\s*[-*+]\s+/gm, '').replace(/^\s*\d+\.\s+/gm, '')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/\s+/g, ' ').trim();
}

const GUIDE_CHUNK_CHARS = 160;
const GUIDE_CHUNK_MAX_MS = 25000;
const GUIDE_NUDGE_MS = 8000;
let guideSpeechGen = 0;
let guideResumeTimer = null;


function guideChunk(text, maxChars = GUIDE_CHUNK_CHARS) {
  const parts = String(text || '')
    .split(/(?<=[.!?…;:])\s+|\n+/)
    .map(s => s.trim())
    .filter(Boolean);
  const out = [];
  let buf = '';
  const push = () => { if (buf.trim()) out.push(buf.trim()); buf = ''; };
  for (const p of parts) {
    if (p.length <= maxChars && (!buf || buf.length + 1 + p.length <= maxChars)) {
      buf = buf ? buf + ' ' + p : p;
      continue;
    }
    push();
    if (p.length <= maxChars) { buf = p; continue; }
    let rest = p;
    while (rest.length > maxChars) {
      let cut = rest.lastIndexOf(',', maxChars);
      if (cut < maxChars * 0.4) cut = rest.lastIndexOf(' ', maxChars);
      if (cut <= 0) cut = maxChars;
      out.push(rest.slice(0, cut).trim());
      rest = rest.slice(cut).replace(/^[,;:\s]+/, '').trim();
    }
    buf = rest;
  }
  push();
  return out;
}

function guideSpeak(text, onend) {
  if (!('speechSynthesis' in window)) { if (onend) onend(); return; }
  speechSynthesis.cancel();
  const clean = cleanSpeech(text);
  if (!clean || !guideSpeakerOn) { if (onend) onend(); return; }
  const chunks = guideChunk(clean);
  if (!chunks.length) { if (onend) onend(); return; }

  const gen = ++guideSpeechGen;
  let idx = 0, watchdog = null;

  const finish = () => {
    if (gen !== guideSpeechGen) return;
    guideSpeechGen++;
    clearTimeout(watchdog);
    clearInterval(guideResumeTimer);
    if (onend) onend();
  };

  const speakNext = () => {
    if (gen !== guideSpeechGen) return;
    if (idx >= chunks.length) { finish(); return; }
    const u = new SpeechSynthesisUtterance(chunks[idx++]);
    u.lang = 'id-ID';
    const idv = speechSynthesis.getVoices().find(v => v.lang && v.lang.toLowerCase().startsWith('id'));
    if (idv) u.voice = idv;
    let advanced = false, stuck = false;
    const next = () => {
      if (advanced || gen !== guideSpeechGen) return;
      advanced = true;
      clearTimeout(watchdog);

      if (stuck) { try { speechSynthesis.cancel(); } catch {  } }
      speakNext();
      };

    u.onend = next;
    u.onerror = next;
    speechSynthesis.speak(u);
    clearTimeout(watchdog);
    watchdog = setTimeout(() => { stuck = true; next(); }, GUIDE_CHUNK_MAX_MS);

    setTimeout(() => {
      if (advanced || gen !== guideSpeechGen) return;
      if (!speechSynthesis.speaking && !speechSynthesis.pending) next();
    }, 3000);
  };

  clearInterval(guideResumeTimer);
  guideResumeTimer = setInterval(() => {
    if (gen !== guideSpeechGen) { clearInterval(guideResumeTimer); return; }
    if (!speechSynthesis.speaking) return;
    try { speechSynthesis.pause(); speechSynthesis.resume(); } catch {  }
  }, GUIDE_NUDGE_MS);

  speakNext();
}

async function guideSend(text, isGreeting) {
  const modelSel = document.getElementById('modelSelect');
  const payload = { sessionId: guideSessionId, model: modelSel ? modelSel.value : null, message: text };
  if (!guideSessionId) payload.systemPrompt = document.getElementById('personaInput').value;
  const r = await fetch('/Chat/Send', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
  });
  const j = await r.json();
  if (!r.ok) throw new Error(j.error || 'chat gagal');
  guideSessionId = j.sessionId;
  return j.response;
}

function guideListen() {
  if (!SR2 || guideListening) return;
  speechSynthesis.cancel();
  guideSpeechGen++;
  guideRec = new SR2();
  guideRec.lang = 'id-ID';
  guideRec.interimResults = false;
  guideListening = true;
  setStatus('Pemandu mendengarkan jawabanmu...');
  guideRec.onresult = async (e) => {
    const text = e.results[0][0].transcript.trim();
    guideLog('Kamu', text);
    setStatus('AI berpikir...');
    try {
      const answer = await guideSend(text, false);
      guideLog('AI', answer);
      guideSpeak(answer, () => { if (guideOn) guideListen(); });
    } catch (err) { setStatus('Chat error: ' + err.message); guideListening = false; }
  };
  guideRec.onend = () => {
    guideListening = false;
    if (guideOn && !speechSynthesis.speaking) setStatus('Pemandu standby — bicara untuk lanjut ngobrol.');
  };
  guideRec.onerror = (e) => {
    guideListening = false;
    setStatus('Mic error: ' + e.error + ' — klik Aktifkan ulang bila perlu.');
  };
  guideRec.start();
}

async function scanOnce() {
  if (!guideOn || !stream) return;
  const c = document.createElement('canvas');
  c.width = 320;
  c.height = Math.round(320 * video.videoHeight / video.videoWidth) || 240;
  c.getContext('2d').drawImage(video, 0, 0, c.width, c.height);
  const blob = await new Promise(res => c.toBlob(res, 'image/jpeg', 0.7));
  try {
    const fd = new FormData();
    fd.append('image', blob, 'scan.jpg');
    const r = await fetch('/Vision/Detect', { method: 'POST', body: fd });
    const j = await r.json();
    if (!r.ok) return;
    const persons = (j.detections || []).filter(d => d.label === 'person');
    if (persons.length > 0) {
      noPersonSince = null;
      if (!greeted) {
        greeted = true;
        setStatus('Orang terdeteksi — Mira menyapa...');
        guideLog('Sistem', `Terdeteksi ${persons.length} orang di kamera.`);
        try {
          const gr = await fetch('/Chat/Greet', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              model: null,
              systemPrompt: document.getElementById('personaInput').value,
              greeting: GREETING
            })
          });
          const gj = await gr.json();
          if (!gr.ok) throw new Error(gj.error || 'greet gagal');
          guideSessionId = gj.sessionId;
          guideLog('AI', gj.response);
          guideSpeak(gj.response, () => { if (guideOn) guideListen(); });
        } catch (err) { setStatus('Chat error: ' + err.message); greeted = false; }
      }
    } else {
      if (!noPersonSince) noPersonSince = Date.now();
      if (greeted && Date.now() - noPersonSince > 15000) {
        greeted = false;
        setStatus('Menunggu orang di depan kamera...');
      }
    }
  } catch {  }
}

document.getElementById('btnGuide').onclick = function () {
  if (!stream) { setStatus('Nyalakan kamera dulu.'); return; }
  guideOn = !guideOn;
  this.classList.toggle('btn-danger', guideOn);
  this.classList.toggle('btn-success', !guideOn);
  this.innerHTML = guideOn ? '<i class="fas fa-stop"></i> Matikan pemandu' : '<i class="fas fa-play"></i> Aktifkan pemandu';
  if (guideOn) {
    greeted = false; guideSessionId = null; noPersonSince = null;
    document.getElementById('guideLog').innerHTML = '';
    guideLog('Sistem', 'Pemandu aktif. Tampilkan wajahmu ke kamera...');
    setStatus('Pemandu aktif — memindai kamera tiap 3 detik.');
    scanTimer = setInterval(scanOnce, 3000);
    scanOnce();
  } else {
    clearInterval(scanTimer);
    if (guideRec && guideListening) guideRec.stop();
    guideListening = false;
    speechSynthesis.cancel();
    setStatus('Pemandu mati.');
  }
};


document.getElementById('btnCam').addEventListener('click', () => {
  setTimeout(() => {
    document.getElementById('btnGuide').disabled = !stream;
  }, 500);
});
        