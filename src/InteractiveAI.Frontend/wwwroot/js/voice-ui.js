
(function () {
  const $ = (id) => document.getElementById(id);
  const screens = ['home', 'voice', 'wo'];

  function goto(name) {
    if (!screens.includes(name)) return;
    screens.forEach((s) => {
      const el = $('screen-' + s);
      if (el) el.classList.toggle('active', s === name);
    });
    document.querySelectorAll('.ai-bottomnav button').forEach((b) =>
      b.classList.toggle('active', b.dataset.goto === name));
    if (name === 'voice') { const cb = $('chatBox'); if (cb) cb.scrollTop = cb.scrollHeight; }
  }
  document.querySelectorAll('[data-goto]').forEach((b) =>
    b.addEventListener('click', () => goto(b.dataset.goto)));

  const drawer = $('aiDrawer'), scrim = $('drawerScrim');
  const setDrawer = (open) => {
    if (drawer) { drawer.classList.toggle('open', open); drawer.setAttribute('aria-hidden', String(!open)); }
    if (scrim) scrim.classList.toggle('open', open);
  };
  setDrawer(false);
  if ($('btnDrawer')) $('btnDrawer').onclick = () => setDrawer(true);
  if ($('btnDrawerClose')) $('btnDrawerClose').onclick = () => setDrawer(false);
  if (scrim) scrim.onclick = () => setDrawer(false);
  document.addEventListener('click', (e) => {
    if (e.target.closest && e.target.closest('#sessionList a')) setDrawer(false);
  });

  if ($('btnMicTop')) $('btnMicTop').onclick = () => { goto('voice'); if ($('btnMic')) $('btnMic').click(); };
  if ($('btnVoiceRefresh')) $('btnVoiceRefresh').onclick = () => { if ($('btnNew')) $('btnNew').click(); goto('voice'); };
  if ($('btnMakeWo2')) $('btnMakeWo2').onclick = () => { if ($('btnMakeWo')) $('btnMakeWo').click(); };

  document.querySelectorAll('.ai-qpill').forEach((b) => b.addEventListener('click', () => {
    goto('voice');
    const inp = $('inputMsg');
    if (inp) inp.value = b.dataset.prompt || '';
    if ($('btnSend')) $('btnSend').click();
  }));

  // Cermin status: statusBar (logika) -> statusPill + listenLabel + header Aura
  // (status-text/dot) + speaking-wave + avatar-ring.
  function auraState(kind) {
    const st = $('status-text'), dot = $('status-indicator-dot');
    const wave = $('speaking-wave'), ring = $('avatar-ring');
    const base = 'absolute w-64 h-64 sm:w-72 sm:h-72 rounded-full transition-all duration-300 ';
    if (kind === 'speaking') {
      if (st) st.textContent = 'Sedang Berbicara...';
      if (dot) dot.className = 'w-1.5 h-1.5 rounded-full bg-purple-400 inline-block animate-ping';
      if (wave) { wave.classList.remove('hidden'); wave.style.display = 'flex'; }
      if (ring) ring.className = base + 'border-2 border-purple-400/50 bg-purple-500/20 shadow-2xl shadow-purple-500/30 animate-pulse';
    } else if (kind === 'listening') {
      if (st) st.textContent = 'Mendengarkan Suara Anda...';
      if (dot) dot.className = 'w-1.5 h-1.5 rounded-full bg-red-400 inline-block animate-ping';
      if (wave) { wave.classList.add('hidden'); wave.style.display = 'none'; }
      if (ring) ring.className = base + 'border-2 border-red-500/50 bg-red-500/10';
    } else {
      if (st) st.textContent = 'Siap Membantu';
      if (dot) dot.className = 'w-1.5 h-1.5 rounded-full bg-emerald-400 inline-block animate-pulse';
      if (wave) { wave.classList.add('hidden'); wave.style.display = 'none'; }
      if (ring) ring.className = base + 'border border-purple-500/20 bg-purple-500/5 transition-all duration-500';
    }
  }
  function mirror() {
    const sb = $('statusBar'), pill = $('statusPill');
    const t = sb ? sb.textContent : '';
    if (pill && t) pill.textContent = t;
    const label = $('listenLabel');
    if (label) {
      if (/mendengar|dengarkan|silakan bicara/i.test(t)) label.textContent = "I'm Listening";
      else if (/bicara|mengetik|menyapa/i.test(t)) label.textContent = 'AI berbicara…';
      else if (/mencari|memindai|mic aktif/i.test(t)) label.textContent = 'Mencari manusia…';
      else label.textContent = t || 'Siaga';
    }
    if (/mendengar|dengarkan|silakan bicara|Mencari manusia/i.test(t)) auraState('listening');
    else if (/bicara|mengetik|menyapa/i.test(t)) auraState('speaking');
    else auraState('idle');
    const cap = $('voiceCaption');
    const box = $('chatBox');
    if (cap && box) {
      const items = box.querySelectorAll('.direct-chat-msg:not(.right) .direct-chat-text');
      if (items.length) cap.textContent = items[items.length - 1].textContent.slice(0, 160);
    }
    const orb = $('voiceOrb');
    if (orb) {
      const micLive = $('btnMic') && $('btnMic').innerHTML.includes('fa-stop');
      const speaking = ('speechSynthesis' in window && speechSynthesis.speaking) ||
        /AI bicara|bicara\.\.\./i.test(t || '');
      orb.classList.toggle('speaking', !!speaking);
      orb.classList.toggle('listening', !!(!speaking && micLive));
    }
  }
  setInterval(mirror, 500);

  const chatView = $('chatView'), startScreen = $('startScreen');
  function afterStart() { setDrawer(false); goto('voice'); try { ensureViz(); } catch {  } }
  if (typeof window.beginSession === 'function' && !window.beginSession.__voiceUiWrapped) {
    const origBegin = window.beginSession;
    const wrapped = async function () {
      const r = await origBegin.apply(this, arguments);
      try { afterStart(); } catch {  }
      return r;
    };
    wrapped.__voiceUiWrapped = true;
    window.beginSession = wrapped;
  }
  if (chatView) new MutationObserver(() => {
    if (chatView.style.display !== 'none') afterStart();
  }).observe(chatView, { attributes: true, attributeFilter: ['style'] });
  if (startScreen) new MutationObserver(() => {
    if (startScreen.style.display !== 'none') goto('home');
  }).observe(startScreen, { attributes: true, attributeFilter: ['style'] });

  const orb = $('voiceOrb');
  if (orb) {
    orb.style.cursor = 'pointer';
    orb.title = 'Ketuk untuk menyalakan/mematikan mic';
    orb.addEventListener('click', () => { if ($('btnMic')) $('btnMic').click(); });
  }

  let vizCtx = null, aiAnalyser = null, aiData = null, aiSrc = null, hookedAudio = null;
  let vizLevel = 0;

  function ensureViz() {
    try {
      if (!vizCtx) vizCtx = new (window.AudioContext || window.webkitAudioContext)();
      if (vizCtx.state === 'suspended') vizCtx.resume().catch(() => {});
      if (!aiAnalyser) {
        aiAnalyser = vizCtx.createAnalyser();
        aiAnalyser.fftSize = 512;
        aiData = new Uint8Array(aiAnalyser.fftSize);
      }
    } catch {  }
  }

  document.addEventListener('click', ensureViz);

  function rmsOf(analyser, buf) {
    analyser.getByteTimeDomainData(buf);
    let sum = 0;
    for (let i = 0; i < buf.length; i++) {
      const v = (buf[i] - 128) / 128;
      sum += v * v;
    }

    return Math.sqrt(sum / buf.length);
  }

  function hookAiAudio() {
    let cur = null;
    try { cur = (typeof neuralPlaying !== 'undefined' && neuralPlaying) ? neuralAudio : null; } catch { cur = null; }
    if (cur === hookedAudio) return;
    try { if (aiSrc) aiSrc.disconnect(); } catch {  }
    aiSrc = null;
    hookedAudio = cur;
    if (!cur || !vizCtx || !aiAnalyser) return;
    try {
      aiSrc = vizCtx.createMediaElementSource(cur);
      aiSrc.connect(aiAnalyser);
      aiAnalyser.connect(vizCtx.destination);
    } catch { aiSrc = null;  }
  }

  let tickLast = 0;
  function tick(now) {
    requestAnimationFrame(tick);
    if (document.hidden) return;
    if (now - tickLast < 66) return;
    tickLast = now;
    const el = $('voiceOrb');
    if (!el || el.offsetParent === null) return;
    let target = 0;
    let wave = null;
    try {
      if (vizCtx && aiAnalyser) {
        hookAiAudio();
        if (hookedAudio && !hookedAudio.paused && !hookedAudio.ended) {
          aiAnalyser.getByteTimeDomainData(aiData);
          wave = aiData;
          target = Math.max(target, Math.min(1, rmsOf(aiAnalyser, aiData) * 4));
        }
      }
    } catch {  }
    try {
      if (!wave && typeof vadTimeData === 'function') {
        const mic = vadTimeData();
        if (mic) {
          wave = mic;
          target = Math.max(target, Math.min(1, (typeof vadLevel === 'function' ? vadLevel() : 0) * 4));
        }
      } else if (typeof vadLevel === 'function') {
        target = Math.max(target, Math.min(1, vadLevel() * 4));
      }
    } catch {  }
    let speaking = false;
    try { speaking = (typeof isSpeaking === 'function') && isSpeaking(); } catch { speaking = false; }
    if (target < 0.05 && speaking) target = 0.3 + 0.22 * Math.abs(Math.sin(now / 220));
    vizLevel += (target - vizLevel) * (target > vizLevel ? 0.45 : 0.1);
    // Hanya elemen orb klasik yang di-scale; avatar memakai glow class saja.
    if (el.classList.contains('ai-orb')) el.style.transform = 'scale(' + (1 + vizLevel * 0.35).toFixed(3) + ')';
    el.classList.toggle('live', vizLevel > 0.03);
    if (vizLevel <= 0.03 && !speaking) return;
    drawOrbWave(now, speaking);
  }

  requestAnimationFrame(tick);

  function drawOrbWave(now, speaking) {
    const cv = $('orbWave');
    const el = $('voiceOrb');
    if (!cv || !el) return;
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const w = el.clientWidth || 150, h = el.clientHeight || 150;
    if (cv.width !== Math.round(w * dpr) || cv.height !== Math.round(h * dpr)) {
      cv.width = Math.round(w * dpr);
      cv.height = Math.round(h * dpr);
    }
    const ctx = cv.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);
    let data = null;
    try {
      if (vizCtx && aiAnalyser && hookedAudio && !hookedAudio.paused && !hookedAudio.ended) {
        aiAnalyser.getByteTimeDomainData(aiData);
        data = aiData;
      } else if (typeof vadTimeData === 'function') {
        data = vadTimeData();
      }
    } catch { data = null; }
    const cx = w / 2, cy = h / 2, base = Math.min(w, h) * 0.36;
    const N = 96;
    ctx.beginPath();
    for (let i = 0; i <= N; i++) {
      let v = 0;
      if (data && data.length) {
        v = (data[Math.floor(i / N * (data.length - 1))] - 128) / 128;
      } else if (speaking) {
        v = 0.35 * Math.sin(i * 0.6 + now / 130) * Math.abs(Math.sin(now / 500));
      }
      const r = base + v * Math.min(w, h) * 0.14;
      const a = (i / N) * Math.PI * 2 - Math.PI / 2;
      const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r;
      if (i) ctx.lineTo(x, y); else ctx.moveTo(x, y);
    }
    ctx.closePath();
    ctx.strokeStyle = speaking || vizLevel > 0.05
      ? 'rgba(255,255,255,.9)'
      : 'rgba(255,255,255,.35)';
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  // Interseptor hasil WO: isi kartu WO saja. JANGAN navigasi (goto) — mode
  // voice-only menyembunyikan layar home/WO; navigasi ke sana hanya
  // mencabut .active dari screen-voice sehingga orb hilang sesaat.
  const origFetch = window.fetch.bind(window);
  window.fetch = function (url, opts) {
    const p = origFetch(url, opts);
    try {
      const u = typeof url === 'string' ? url : (url && url.url) || '';
      if (u.includes('/Chat/WO')) {
        p.then((r) => r.clone().json().then((w) => {
          const box = $('woResult');
          if (box && (w.woNumber || r.ok)) {
            box.textContent = r.ok
              ? `WO ${w.woNumber || ''} tercatat.\n${w.driverName || w.nama || ''} ${w.unit || w.nopol || ''}\n${w.complaint || w.keluhan || ''}`.trim()
              : ('Gagal buat WO: ' + (w.error || r.status));
          }
        }).catch(() => {}));
      }
    } catch {  }
    return p;
  };
})();
