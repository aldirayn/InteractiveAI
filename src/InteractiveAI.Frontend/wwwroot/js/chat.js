let sessionId = null;

let voiceMode = false;
let speakerOn = true;
let listening = false;
let recognition = null;
let idVoice = null;
let lockedVoice = null; // nilai voice terkunci per sesi (bagian dari lockedCfg)
let lockedCfg = null;   // snapshot {voice, rate} — SATU-SATUNYA cfg suara per sesi

function ensureVoiceLock() {
    if (!lockedCfg) {
        let v = 'browser';
        let rate = 12;
        try { const c = voiceCfg(); v = c.voice || 'browser'; rate = c.rate; } catch { }
        lockedVoice = v;
        lockedCfg = { voice: v, rate: rate };
        try { console.info('[TTS] locked path:', v); } catch { }
    }

    return lockedCfg;
}

function effectiveVoice() { return ensureVoiceLock().voice; }

const AI_TAIL_MS = 700;
const STT_FALLBACK_SEND_MS = 1000;

let USER_SPEAK_LEVEL = 0.08;
let BARGE_THRESHOLD = 0.10;
let ECHO_EARLY_LVL = 0.14;
const VAD_SILENCE_SEND_MS = 1000;
const WAIT_FINAL_MS = 600;
const FINAL_GRACE_MS = 800;
const BARGE_HITS = 4;
const BARGE_GRACE_MS = 900;
const ECHO_EARLY_HITS = 3;
let continuousOn = false;
let restartPending = false;
let finalBuffer = '';
let lastInterim = '';
let lastSpeechAt = 0;
let lastResultAt = 0;
let sendDebounceTimer = null;
let echoMuteUntil = 0;
let echoEarlyHits = 0;
let listenGen = 0;
let aiSpeaking = false;
let aiSpeechGen = 0;
let noBarge = false;
let closing = false;
let aiSpeechStartMs = 0;

const VOICE_SYSTEM_PROMPT = 'Kamu adalah Mira, petugas loket Bengkel truk yang ramah dan supel. Ikuti alur TETAP ini, satu langkah per giliran, 1-2 kalimat pendek gaya lisan sehari-hari, tanpa markdown, tanpa emoji. Setiap giliran WAJIB diakhiri dengan satu pertanyaan ATAU salah satu baris penutup ([WO_SIAP]/[SELESAI_ADMIN_WO]/[SELESAI_LOKET_WO]). Jangan pernah menutup giliran dengan pernyataan atau konfirmasi saja. '
    + 'Langkah 0: sopir sudah terdeteksi kamera dan disapa, sapaan sudah menanyakan apakah punya QR Code aplikasi Kimo. '
    + 'Langkah 1: Bila sopir bilang PUNYA/ADA QR: minta QR KIMO ditunjukkan ke kamera (maksimal dua kali bertanya). Tunggu sistem menyisipkan pesan penanda QR KIMO berisi status dan data acuan. Bila status terminal (Batal/Cancel/Done): konfirmasi singkat, suruh sopir langsung ke petugas loket manusia, tutup dengan baris [SELESAI_ADMIN_WO] sendirian di baris terakhir. Bila status Draft/Open/Accept: jadikan data nama, nopol, dan jenis sebagai acuan, sebutkan nama driver persis dari data KIMO (field NamaDriver) setiap menyebut sopir, LEWATI tanya nama/nopol/jenis, langsung tanyakan keluhan dan kategori perbaikan (Truk atau Reefer atau Cabin atau GPS atau Genset atau Box atau Ban; boleh lebih dari satu), lalu parkir, rangkum, dan tutup [WO_SIAP]. Bila pesan bilang tak terdaftar atau gagal cek: anggap sopir tanpa QR, lanjut Langkah 1b. Bila sopir bilang TIDAK PUNYA/TIDAK ADA QR: anggap sopir tanpa QR, lalu LANGSUNG di giliran yang sama tanyakan nama dan nopol truk dengan kalimat: Boleh sebutkan nama abang dan nopol truknya? Nopol boleh dieja satu per satu (contoh B 9 0 0 2 U X U). Bila sopir hanya menyebut nopol tanpa nama, tetap lanjut (nama tidak wajib). JANGAN PERNAH tanyakan kode driver. Jangan menanyakan apakah mau dilayani petugas lain. Bila jawaban tak jelas punya/tidak: tanya ulang sekali secara singkat. '
    + 'Langkah 1b (tanpa QR): tanyakan NAMA dan NOPOL dengan kalimat: Boleh sebutkan nama abang dan nopol truknya? Nopol boleh dieja satu per satu (contoh B 9 0 0 2 U X U). Bila sopir hanya menyebut nopol tanpa nama, tetap lanjut (nama tidak wajib). JANGAN PERNAH tanyakan kode driver. Bila sopir menyebut nama yang beda dengan data, catat apa adanya tanpa konfirmasi dan langsung lanjut. Minta sopir menyebut nopol LENGKAP dalam satu ucapan dengan pola huruf spasi angka spasi huruf (contoh: B 9002 UXU). Bila suara sopir terpotong, minta ulang seluruhnya, jangan menggabungkan potongan. Pada kegagalan nopol PERTAMA sistem otomatis membuka keyboard di layar: kabari sopir bahwa dia boleh mengetik nopolnya sendiri di keyboard itu, dan jangan menyuruh menyentuh kolom form. Bila pesan sistem menandai NOPOL BELUM TERBACA, minta nopol lengkap sambil mengulang bahwa ada keyboard di layar. Bila pesan menandai NOPOL DITEMUKAN: JANGAN verifikasi dulu. Minta konfirmasi sopir dengan pola kaku: Oke {nopol dieja per huruf} jenis truknya {jenis}. Benar ya bang? Eja nopol per huruf terpisah (contoh: B 9 0 0 2 U X U), sebut jenis persis dari data. Tunggu jawaban: bila sopir jawab iya/benar/betul maka anggap terverifikasi lalu tanyakan: Bisa dijelaskan dari truk apa yang mau di periksa atau di perbaiki bang? Bila sopir jawab salah/bukan maka minta eja ulang nopol lengkap atau ketik di Virtual Keyboard dengan kalimat: Bisa di ulangi nopolnya dengan cara di eja lagi bang? atau bisa ketik Virtual Keyboard bang. '
    + 'Bila sistem menemukan nopol (dengan atau tanpa nama sopir), JANGAN tanyakan identitas apa pun — langsung lanjut ke keluhan. Bila pesan menandai NOPOL DITEMUKAN, minta konfirmasi dengan pola kaku Benar-ya-bang seperti Langkah 1b (jangan langsung ke keluhan). Bila TIDAK DITEMUKAN / tak cocok: pada kegagalan pertama tanyakan ulang (mungkin salah dengar) sambil menawarkan keyboard yang sudah terbuka di layar untuk mengetik nopol manual; bila gagal lagi, sistem memintamu meminta QR KIMO (ikuti instruksi pesannya: bandingkan, konfirmasi, atau lewati bila tak punya). Bila sistem menyisipkan pesan GAGAL TOTAL, sampaikan sopir langsung ke loket WO manusia lalu tutup dengan [SELESAI_LOKET_WO]. SELALU sebutkan nopol dengan pola huruf spasi angka spasi huruf (contoh: B 9002 UXU), janganmenyebut tanpa spasi. Bila pesan menandai NAMA DITERIMA APA ADANYA (nopol cocok view tapi nama sopir beda): PAKAI NAMA YANG DISEBUT SOPIR untuk WO (bukan nama di view), minta konfirmasi Benar-ya-bang seperti Langkah 1b (tanpa tanya ulang). Data yang sudah tercatat (nopol, jenis truk) JANGAN pernah ditanyakan ulang; bila nopol ketemu dan cocok (sendiri maupun via keyboard/QR), langsung tanyakan keluhannya. '
    + 'Langkah 3: tanyakan permintaan perbaikan: Truk atau Reefer atau Cabin atau GPS atau Genset atau Box atau Ban (hanya bila nopol sudah jelas dari master atau acuan QR). Ingat: keluhan AC (tidak dingin, freon) masuk Reefer, bukan Cabin; Cabin hanya untuk kabin non-pendingin (jok, kaca, pintu, dashboard). Tanyakan kategori dan detail keluhannya SATU PER SATU LEWAT SUARA; JANGAN pernah menyuruh sopir mengisi, menyentuh, atau melihat baris form — form adalah catatan sistem, bukan tugas sopir. Setiap isian baris form yang masuk sebagai pesan [KELUHAN Kategori: isi] adalah DATA (bukan ucapan) — jangan tanyakan ulang yang sudah terisi di baris. Bila jawaban suara di luar tujuh kategori itu, ulangi sampai sopir menyebut salah satunya. Bila sopir menambah keluhan untuk kategori yang sudah ada (mis. ada tambahan untuk truk): catat sebagai tambahan pada kategori itu, jangan ganti atau hapus yang lama. Lalu tanyakan keluhan detail tiap kategori yang ada isinya. '
    + 'Langkah 4: tanyakan truk parkir di mana saat ini. '
    + 'Langkah 5: QR KIMO sudah ditangani tuntas di Langkah 1, jangan tanya atau bahas QR lagi. '
    + 'Langkah 6: bila kategori + keluhan detail + parkir SUDAH diketahui (walau datang sekaligus dalam satu kalimat atau dari baris form): jangan konfirmasi berputar-putar, langsung tanyakan satu pertanyaan: masih ada keluhan lain? Bila sopir jawab tidak/cukup/tidak ada: di giliran berikutnya langsung ke Langkah 7. Bila ya: tanyakan kategori tambahannya lalu detailnya lewat suara, dan perlakukan sebagai tambahan pada kategori itu bila kategorinya sudah ada. Bila jawaban sopir tidak jelas/cukup-atau-belum (mis. hanya "halo", "iya", gumaman): JANGAN anggap cukup — tanyakan ulang sekali dengan menyebut menu kategorinya (Truk atau Reefer atau Cabin atau GPS atau Genset atau Box atau Ban). Bila sopir menyebut masalah tanpa kategori jelas (mis. error, rusak, mati, bermasalah): WAJIB tanyakan itu keluhan untuk kategori apa sebelum lanjut. Bila sopir bertanya "sudah ditulis/dicatat belum" tentang sesuatu: JANGAN pernah mengiyakan bila hal itu belum tercatat di percakapan — tanyakan detail + kategorinya dulu. JANGAN pernah mengklaim "sudah saya catat semua" kecuali setiap itemnya benar-benar sudah terkonfirmasi. '
    + 'Langkah 7 (rekap): mulai giliran rekap dengan baris persis [REKAP] sendirian di baris pertama (jangan dibacakan, jangan dijelaskan), lalu rangkum semua (nopol, jenis truk, kategori, keluhan detail, parkir; sertakan nama hanya bila diketahui dari QR/data) dengan natural 1-3 kalimat, dan akhiri dengan SATU pertanyaan: masih ada tambahan? (TANPA [WO_SIAP] di giliran ini). Bila sopir jawab tidak/cukup/tidak ada: di giliran berikutnya katakan WO akan dicatatkan lalu tutup dengan baris persis [WO_SIAP] sendirian di baris terakhir. Bila sopir menambah keluhan: catat sebagai tambahan pada kategorinya (atau kategori baru bila beda), ulangi rekap singkat + tanya tambahan lagi; bila sudah tidak ada tambahan, tutup [WO_SIAP]. Dan sampaikan ke Sopir kalau WO sudah dibuat dan dipersilahkan menunggu.'
    + 'Berkomunikasilah senatural mungkin, sopan, dan komunikatif dan gunakan kata Bang dan jangan dijeda menggunakan koma atau titik. Jika perlu konfirmasi, ulangi keluhan sopir dan pastikan semua informasi kamu dapatkan.';

const chatBox = () => document.getElementById('chatBox');
const statusBar = () => document.getElementById('statusBar');

const PLATE_TOKEN_RE = /\b([A-Z](?:\s*[A-Z])?)?(\s*)(\d(?:\s*\d){0,3})(\s*)([A-Z](?:\s*[A-Z]){0,2})\b/gi;

function compactPlateGroup(s) { return (s || '').replace(/\s+/g, ''); }

function renderPlate(lead, digits, tail) {
    lead = compactPlateGroup(lead);
    digits = compactPlateGroup(digits);
    tail = compactPlateGroup(tail);
    return (lead ? lead + ' ' : '') + digits + (tail ? ' ' + tail : '');
}

function extractStrictPlate(text) {
    const m = (text || '').match(/\b([A-Z](?:\s*[A-Z])?)\s*(\d(?:\s*\d){0,3})\s*([A-Z](?:\s*[A-Z]){0,2})\b/i);
    if (!m) return null;
    const lead = compactPlateGroup(m[1]);
    const digits = compactPlateGroup(m[2]);
    const tail = compactPlateGroup(m[3]);
    if (!digits) return null;
    return { lead, digits, tail, display: renderPlate(lead, digits, tail) };
}

function formatPlateText(text) {
    return (text || '').replace(PLATE_TOKEN_RE,
        (m, lead, s1, digits, s2, tail) => renderPlate(lead, digits, tail));
}

function formatPlateInput(inp) {
    if (!inp) return false;
    const v = inp.value || '';
    const caret = inp.selectionStart == null ? v.length : inp.selectionStart;
    let out = '';
    let last = 0;
    let shift = 0;
    PLATE_TOKEN_RE.lastIndex = 0;
    let m;
    while ((m = PLATE_TOKEN_RE.exec(v)) !== null) {
        const idx = m.index;
        const rep = renderPlate(m[1], m[3], m[5]);
        out += v.slice(last, idx) + rep;
        if (idx + m[0].length <= caret) shift += rep.length - m[0].length;
        last = idx + m[0].length;
        if (m[0].length === 0) PLATE_TOKEN_RE.lastIndex++;
    }

    out += v.slice(last);
    if (out === v) return false;
    inp.value = out;
    const nc = Math.max(0, Math.min(out.length, caret + shift));
    try { inp.setSelectionRange(nc, nc); } catch { }
    syncTypingBubble();
    return true;
}

function followChatBottom() {
    if (chatCollapsed) return;
    const cb = chatBox();
    if (!cb) return;
    const nearBottom = cb.scrollHeight - cb.scrollTop - cb.clientHeight < 140;
    if (nearBottom) cb.scrollTop = cb.scrollHeight;
}

function bubble(role, content) {
    if (role === 'system') return null;
    const isUser = role === 'user';
    const div = document.createElement('div');
    div.className = 'flex gap-3 w-full justify-start' + (isUser ? ' flex-row-reverse' : '');
    div.innerHTML = `
    <div class="w-8 h-8 rounded-full ${isUser ? 'bg-cyan-600/30 border-cyan-400/40 text-cyan-300' : 'bg-purple-600/30 border-purple-400/40 text-purple-300'} border flex items-center justify-center shrink-0 text-xs font-bold">
      ${isUser ? '<i class="fa-solid fa-user"></i>' : 'AI'}
    </div>
    <div class="p-3.5 rounded-2xl max-w-[85%] min-w-0 bg-slate-900/80 border border-white/10 ${isUser ? 'rounded-tr-none border-purple-500/30' : 'rounded-tl-none'} text-sm text-slate-200 leading-relaxed shadow-lg">
      <p class="font-semibold ${isUser ? 'text-cyan-300' : 'text-purple-300'} text-xs mb-1">${isUser ? 'Anda' : 'Mira • Loket WO'}</p>
      <div class="direct-chat-text" style="white-space:pre-wrap">${escapeHtml(content)}</div>
    </div>`;
    chatBox().appendChild(div);
    noteChatActivity(role, content);
    followChatBottom();
    return div.querySelector('.direct-chat-text');
}

const CHAT_OPEN_KEY = 'loketChatOpen';
const THEME_KEY = 'loketTheme';
let chatCollapsed = true;
let chatHiddenCount = 0;

function applyTheme() {
    let saved = null;
    try { saved = localStorage.getItem(THEME_KEY); } catch { }
    const light = saved === 'light';
    document.documentElement.setAttribute('data-theme', light ? 'light' : 'dark');
    const ic = document.getElementById('iconTheme');
    if (ic) ic.className = light ? 'fas fa-moon' : 'fas fa-sun';
    const btn = document.getElementById('btnTheme');
    if (btn) btn.title = light ? 'Tema gelap' : 'Tema terang';
}
function toggleTheme() {
    const nowLight = document.documentElement.getAttribute('data-theme') === 'light';
    try { localStorage.setItem(THEME_KEY, nowLight ? 'dark' : 'light'); } catch { }
    applyTheme();
    noteSystem('Tema ' + (nowLight ? 'gelap' : 'terang') + ' aktif.');
}
function setChatBadge(n) {
    const b = document.getElementById('chatNewBadge');
    if (!b) return;
    b.textContent = String(n);
    b.style.display = n > 0 ? 'inline-flex' : 'none';
}
function setChatPreview(txt) {
    const prev = document.getElementById('chatPreview');
    if (!prev) return;
    const t = (txt || '').replace(/\s+/g, ' ').trim();
    prev.textContent = t.length > 140 ? t.slice(0, 140) + '...' : t;
    prev.title = t;
}

function noteChatActivity(role, content) {
    setChatPreview(content || '');
    if (!chatCollapsed) { chatHiddenCount = 0; setChatBadge(0); return; }
    chatHiddenCount++;
    setChatBadge(chatHiddenCount);
}

function noteSystem(msg) {
    setStatus(msg);
    if (!chatCollapsed) return;
    setChatPreview(msg);
    chatHiddenCount++;
    setChatBadge(chatHiddenCount);
}

function applyChatView() {
    document.documentElement.classList.toggle('chat-open', !chatCollapsed);
    const btn = document.getElementById('btnChatToggle');
    if (btn) {
        btn.setAttribute('aria-expanded', chatCollapsed ? 'false' : 'true');
        btn.title = chatCollapsed ? 'Tampilkan riwayat percakapan' : 'Sembunyikan riwayat (fokus ke nopol & keluhan)';
    }
    const ic = document.getElementById('iconChatToggle');
    if (ic) ic.className = chatCollapsed ? 'fas fa-chevron-up' : 'fas fa-chevron-down';
    if (!chatCollapsed) { chatHiddenCount = 0; setChatBadge(0); }
}
function toggleChatPanel() {
    chatCollapsed = !chatCollapsed;
    try {
        if (chatCollapsed) localStorage.removeItem(CHAT_OPEN_KEY);
        else localStorage.setItem(CHAT_OPEN_KEY, '1');
    } catch { }
    applyChatView();
}
function restoreChatPanel() {
    let open = null;
    try { open = localStorage.getItem(CHAT_OPEN_KEY); } catch { }
    chatCollapsed = open !== '1';
    applyChatView();
}
function escapeHtml(s) {
    return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
function setStatus(t) { statusBar().textContent = t; }

let knownDriverName = '';
let viewDriverWin = null;
let pendingPlateWin = null;
const CONFIRM_YES_RE = /\b(iya|ya|yup|betul|benar|oke|ok)\b/i;
const CONFIRM_NO_RE = /\b(tidak|nggak|ngga|gak|bukan|salah|keliru|ralat|ganti|ulang)\b/i;
function ejaNopol(spaced) { return ((spaced || '').replace(/([A-Za-z0-9])/g, '$1 ') || '').replace(/\s+/g, ' ').trim(); }
let awaitingName = false;
let lastPlateText = '';
const NAME_FILLER_RE = /^(halo|hai|ya|iya|betul|benar|yup|ok|oke|tidak|nggak|ngga|gak|cukup|sudah|belum|belom|baik|siap|terima kasih|makasih)\W*$/i;
let pendingSysNotes = [];
function stashSysNote(note) {
    pendingSysNotes.push(note);
    if (pendingSysNotes.length > 5) pendingSysNotes.shift();
}
function takeSysNotes() {
    const out = pendingSysNotes.join('');
    pendingSysNotes = [];
    return out;
}

function forSpeech(s) {
    return (s || '')
        .replace(/\[[^\]]*\]/g, ' ')
        .replace(/```[\s\S]*?```/g, ' ')
        .replace(/`([^`]*)`/g, '$1')
        .replace(/^#{1,6}\s+/gm, '')
        .replace(/(\*\*|__)(.*?)\1/g, '$2')
        .replace(/(\*|_)(.*?)\1/g, '$2')
        .replace(/^\s*[-*+]\s+/gm, '')
        .replace(/^\s*\d+\.\s+/gm, '')
        .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
        .replace(/,/g, '')
        .replace(/\s+/g, ' ')
        .trim();
}

const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
const TTS = ('speechSynthesis' in window);

let browserVoicePref = '';
let browserVoiceWarned = false;
function pickIdVoice(preferred) {
    if (typeof preferred === 'string') browserVoicePref = preferred;
    if (!TTS) { idVoice = null; return; }
    const all = speechSynthesis.getVoices();
    const ids = all.filter(v => v.lang && v.lang.toLowerCase().startsWith('id'));
    const pool = ids.length ? ids : all;
    const want = (browserVoicePref || '').toLowerCase();
    idVoice = (want && pool.find(v => (v.name || '').toLowerCase().includes(want)))
        || pool.find(v => (v.name || '').toLowerCase().includes('google'))
        || pool[0] || null;
    labelBrowserOption();
    if (want && idVoice && !(idVoice.name || '').toLowerCase().includes(want) && !browserVoiceWarned) {
        browserVoiceWarned = true;
        showToast('Suara browser "' + browserVoicePref + '" tak ada di perangkat ini; dipakai ' + (idVoice.name || '?') + '.', 'err');
    }
    try { console.debug('[TTS] browser voice:', idVoice && idVoice.name, '| pref:', browserVoicePref || '(auto)'); } catch { }
}
function labelBrowserOption() {
    const sel = document.getElementById('voiceSelect');
    if (!sel) return;
    const opt = [...sel.options].find(o => o.value === 'browser');
    if (!opt) return;
    const nm = (idVoice && idVoice.name) ? idVoice.name : 'otomatis';
    opt.textContent = 'Browser — ' + nm + (/google/i.test(nm) ? ' (butuh internet)' : '');
}
if (TTS) {
    fetch('/Chat/VoiceStatus').then(r => r.json()).then(j => {
        pickIdVoice(j && j.browserVoice ? j.browserVoice : '');
    }).catch(() => pickIdVoice(''));
    speechSynthesis.onvoiceschanged = () => pickIdVoice();
}

function voiceCfg() {
    const v = document.getElementById('voiceSelect');
    const r = document.getElementById('rateSelect');
    return {
        voice: v ? v.value : 'browser',
        rate: r ? parseInt(r.value, 10) : 12
    };
}

function ttsUrl(clean, cfg) {
    return '/Chat/Tts?text=' + encodeURIComponent(clean)
        + '&voice=' + encodeURIComponent(cfg.voice)
        + '&rate=' + encodeURIComponent(cfg.rate);
}

function splitClauses(clean) {
    return clean.split(/(?<=[.!?…])\s+|\n+/).map(s => s.trim()).filter(s => s.length > 0);
}

function speak(text, onend) {
    if (!speakerOn) { if (onend) onend(); return; }
    const clean = forSpeech(text);
    if (!clean) { if (onend) onend(); return; }
    const cfg = ensureVoiceLock();

    rememberAiSpeech(clean);

    const done = () => { openDuplex(); if (onend) onend(); };
    closeDuplex();
    if (cfg.voice === 'browser') { browserSpeak(clean, done); return; }

    neuralSpeak(clean, cfg, done);
}

function fetchTts(url, ms) {
    const c = new AbortController();
    const t = setTimeout(() => c.abort(), ms || 45000);
    return fetch(url, { signal: c.signal }).finally(() => clearTimeout(t));
}

function prefetchTts(text) {
    try {
        const clean = forSpeech(text);
        if (!clean) return;
        const cfg = voiceCfg();
        if (cfg.voice === 'browser') return;
        splitClauses(clean).forEach(c => fetchTts(ttsUrl(c, cfg), 20000).catch(() => { }));
    } catch { }
}

function closeDuplex() {
    aiSpeechGen++;
    aiSpeaking = true;
    aiSpeechStartMs = Date.now();
    echoMuteUntil = Date.now() + AI_TAIL_MS;
    echoEarlyHits = 0;
    clearSendState();
    lastInterim = '';
    listenGen++;
    stopListening();
}

function openDuplex(skipTail) {
    aiSpeaking = false;

    echoMuteUntil = skipTail ? 0 : Date.now() + AI_TAIL_MS;
    echoEarlyHits = 0;
    lastInterim = '';
    if (continuousOn && !listening) startListening();
}

function maybeEarlyOpenTail(lvl) {
    if (aiSpeaking || Date.now() >= echoMuteUntil) { echoEarlyHits = 0; return; }

    if (!visGatePass()) { echoEarlyHits = 0; return; }
    if (lvl > ECHO_EARLY_LVL) {
        if (++echoEarlyHits >= ECHO_EARLY_HITS) {
            echoMuteUntil = 0;
            echoEarlyHits = 0;
        }
    } else echoEarlyHits = 0;
}

function stopSpeak() {
    stopVad();
    stopNeural();
    browserGen++;
    clearInterval(resumeTimer);
    if (TTS) speechSynthesis.cancel();
}

function isSpeaking() {
    return neuralPlaying || (TTS && speechSynthesis.speaking);
}


let aiSpokenRecently = [];
const ECHO_MEMORY_MS = 5000;

const ECHO_WORD_OVERLAP = 0.8;

let shownInterimNorm = '';

function matchesShownInterim(transcript) {
    if (!shownInterimNorm) return false;
    const w = normalizeWords(transcript).split(' ').filter(Boolean);
    if (!w.length) return false;
    const shown = new Set(shownInterimNorm.split(' ').filter(Boolean));
    if (!shown.size) return false;
    let hit = 0;
    for (const x of w) if (shown.has(x)) hit++;
    return hit / Math.min(w.length, shown.size) >= 0.5;
}

function normalizeWords(s) {
    return (s || '')
        .toLowerCase()
        .replace(/[^a-z0-9\u00c0-\u024f\s]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

function rememberAiSpeech(text) {
    const norm = normalizeWords(text);
    if (!norm) return;
    const now = Date.now();
    aiSpokenRecently = aiSpokenRecently.filter(x => now - x.at < ECHO_MEMORY_MS);
    aiSpokenRecently.push({ text: norm, at: now });
    if (aiSpokenRecently.length > 24) aiSpokenRecently.shift();
}

function isEchoOfAi(transcript) {
    if (aiSpeaking || Date.now() < echoMuteUntil) return true;
    const words = normalizeWords(transcript).split(' ').filter(Boolean);

    if (!words.length) return true;

    if (words.length < 3) return false;
    const now = Date.now();
    aiSpokenRecently = aiSpokenRecently.filter(x => now - x.at < ECHO_MEMORY_MS);
    for (const said of aiSpokenRecently) {
        const aiWords = new Set(said.text.split(' ').filter(Boolean));
        let hit = 0;
        for (const w of words) if (aiWords.has(w)) hit++;
        if (hit / words.length >= ECHO_WORD_OVERLAP) return true;
    }
    return false;
}

let neuralAudio = null;
let neuralPlaying = false;
let clauseAudios = [];
let speechGen = 0;

function stopNeural() {
    speechGen++;
    neuralPlaying = false;
    if (neuralAudio) {
        try { neuralAudio.pause(); } catch { }
        neuralAudio = null;
    }
    clauseAudios.forEach(a => { try { a.pause(); } catch { } });
    clauseAudios = [];
}

function playNeuralBlob(clean, blob, cfg, gen, onend, onBargeCapable) {
    if (gen !== speechGen) return;
    const url = URL.createObjectURL(blob);
    const a = new Audio(url);
    a.preload = 'auto';
    clauseAudios.push(a);
    neuralAudio = a;
    neuralPlaying = true;
    a.onended = () => {
        URL.revokeObjectURL(url);
        clauseAudios = clauseAudios.filter(x => x !== a);
        if (gen !== speechGen) return;
        doneChain(onend);
    };

    a.onerror = () => { URL.revokeObjectURL(url); if (gen === speechGen) { doneChain(null); browserSpeak(clean, onend); } };
    const p = a.play();

    if (p && p.catch) p.catch(() => {
        if (gen !== speechGen) return;
        neuralPlaying = false;
        const b = document.getElementById('permBanner');
        if (b) {
            document.getElementById('permText').textContent =
                'Browser mengunci suara otomatis. Klik tombol ini sekali, setelah itu full otomatis.';
            b.style.display = 'block';
        }
        const retry = () => {
            if (gen !== speechGen) return;
            neuralPlaying = true;
            a.play().catch(() => { if (gen === speechGen) browserSpeak(clean, onend); });
            document.removeEventListener('click', retry);
        };
        document.addEventListener('click', retry);
    });

    if (onBargeCapable) startVad(() => {
        if (noBarge) return;
        if (gen !== speechGen) return;
        vkbPendingShow = false;
        setStatus('Disela sopir — mendengarkan...');
        aiSpeechGen++;
        aiSpeaking = false;
        stopNeural();
        openDuplex(true);
    });
}

function doneChain(onend) {
    stopVad();
    stopNeural();
    clearInterval(resumeTimer);
    if (onend) onend();
}

function neuralSpeak(clean, cfg, onend) {
    stopVad();
    stopNeural();
    if (TTS) speechSynthesis.cancel();
    const gen = speechGen;

    const clauses = groupClauses(splitClauses(clean));
    if (clauses.length <= 1) {
        fetchTts(ttsUrl(clean, cfg), 45000)
            .then(r => {
                if (!r.ok) throw new Error('TTS ' + r.status);
                try {
                    const vid = r.headers.get('X-Voice-Id');
                    if (vid) console.debug('[TTS] voice_id aktual:', vid);
                } catch { }
                return r.blob();
            })
            .then(blob => playNeuralBlob(clean, blob, cfg, gen, onend, true))
            .catch(() => { if (gen === speechGen) browserSpeak(clean, onend); });
        return;
    }

    const pending = clauses.map(c =>
        fetchTts(ttsUrl(c, cfg), 45000).then(r => { if (!r.ok) throw new Error('TTS ' + r.status); return r.blob(); }));

    const playIdx = (i) => {
        if (gen !== speechGen) return;
        if (i >= clauses.length) { doneChain(onend); return; }
        pending[i].then(
            blob => { if (gen === speechGen) playNeuralClause(clauses, i, blob, cfg, gen, onend, pending); },
            () => {
                if (gen !== speechGen) return;
                doneChain(null);
                browserSpeak(clauses.slice(i).join(' '), onend);
            });
    };

    playIdx(0);
}

function groupClauses(clauses, maxChars = 220) {
    const out = [];
    let buf = '';
    for (const c of clauses) {
        if (!buf) { buf = c; continue; }
        if (buf.length + 1 + c.length <= maxChars) { buf += ' ' + c; continue; }
        out.push(buf);
        buf = c;
    }
    if (buf) out.push(buf);
    return out;
}

function playNeuralClause(clauses, idx, blob, cfg, gen, onend, pending) {
    if (gen !== speechGen) return;
    const url = URL.createObjectURL(blob);
    const a = new Audio(url);
    a.preload = 'auto';
    clauseAudios.push(a);
    neuralAudio = a;
    neuralPlaying = true;
    const full = clauses.join(' ');
    a.onended = () => {
        URL.revokeObjectURL(url);
        clauseAudios = clauseAudios.filter(x => x !== a);
        if (gen !== speechGen) return;
        if (idx + 1 >= clauses.length) { doneChain(onend); return; }

        const next = (pending && pending[idx + 1])
            ? pending[idx + 1]
            : fetchTts(ttsUrl(clauses[idx + 1], cfg), 45000)
                .then(r => { if (!r.ok) throw new Error('TTS ' + r.status); return r.blob(); });
        next.then(
            b => { if (gen === speechGen) playNeuralClause(clauses, idx + 1, b, cfg, gen, onend, pending); },
            () => {
                if (gen !== speechGen) return;
                doneChain(null);
                browserSpeak(clauses.slice(idx + 1).join(' '), onend);
            });
    };

    a.onerror = () => {
        URL.revokeObjectURL(url);
        if (gen !== speechGen) return;
        doneChain(null);
        browserSpeak(full, onend);
    };

    const p = a.play();

    if (p && p.catch) p.catch(() => {
        if (gen !== speechGen) return;
        neuralPlaying = false;
        const b = document.getElementById('permBanner');
        if (b) {
            document.getElementById('permText').textContent =
                'Browser mengunci suara otomatis. Klik tombol ini sekali, setelah itu full otomatis.';
            b.style.display = 'block';
        }

        const retry = () => {
            if (gen !== speechGen) return;
            neuralPlaying = true;
            a.play().catch(() => { if (gen === speechGen) browserSpeak(full, onend); });
            document.removeEventListener('click', retry);
        };

        document.addEventListener('click', retry);
    });

    if (idx === 0) startVad(() => {
        if (noBarge) return;
        if (gen !== speechGen) return;
        setStatus('Disela sopir — mendengarkan...');
        aiSpeechGen++;
        aiSpeaking = false;
        stopNeural();
        openDuplex(true);
    });
}

const BROWSER_CHUNK_CHARS = 160;
const BROWSER_CHUNK_MAX_MS = 25000;
const BROWSER_NUDGE_MS = 8000;

function chunkForSpeech(text, maxChars = BROWSER_CHUNK_CHARS) {
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

function browserSpeak(clean, onend) {
    if (!TTS) { if (onend) onend(); return; }
    stopVad();
    stopNeural();
    speechSynthesis.cancel();
    const chunks = chunkForSpeech(clean);
    if (!chunks.length) { if (onend) onend(); return; }

    const gen = ++browserGen;
    let idx = 0, watchdog = null;

    const finish = () => {
        if (gen !== browserGen) return;
        browserGen++;
        currentUtt = null;
        clearTimeout(watchdog);
        clearInterval(resumeTimer);
        stopVad();
        if (onend) onend();
    };

    const speakNext = () => {
        if (gen !== browserGen) return;
        if (idx >= chunks.length) { finish(); return; }
        const piece = chunks[idx++];
        const u = new SpeechSynthesisUtterance(piece);
        currentUtt = u;
        u.lang = 'id-ID';
        if (idVoice) u.voice = idVoice;
        let advanced = false, stuck = false;
        const next = () => {
            if (advanced || gen !== browserGen) return;
            advanced = true;
            clearTimeout(watchdog);

            if (stuck) { try { speechSynthesis.cancel(); } catch { } }
            speakNext();
        };

        u.onend = next;
        u.onerror = next;
        speechSynthesis.speak(u);
        clearTimeout(watchdog);
        watchdog = setTimeout(() => { stuck = true; next(); }, BROWSER_CHUNK_MAX_MS);

        setTimeout(() => {
            if (advanced || gen !== browserGen) return;
            if (!speechSynthesis.speaking && !speechSynthesis.pending) next();
        }, 3000);

        if (idx === 1) setTimeout(() => {
            if (gen !== browserGen) return;
            if (!userGestured && !speechSynthesis.speaking && !speechSynthesis.pending && currentUtt) {
                const b = document.getElementById('permBanner');
                if (b) {
                    document.getElementById('permText').textContent =
                        'Browser mengunci suara otomatis. Klik tombol ini sekali, setelah itu full otomatis.';
                    b.style.display = 'block';
                }
            }
        }, 1200);
    };


    clearInterval(resumeTimer);
    resumeTimer = setInterval(() => {
        if (gen !== browserGen) { clearInterval(resumeTimer); return; }
        if (!speechSynthesis.speaking) return;
        try { speechSynthesis.pause(); speechSynthesis.resume(); } catch { }
    }, BROWSER_NUDGE_MS);

    speakNext();


    startVad(() => {
        if (noBarge) return;
        if (gen !== browserGen) return;
        vkbPendingShow = false;
        setStatus('Disela sopir — mendengarkan...');
        aiSpeechGen++;
        try { speechSynthesis.cancel(); } catch { }
        finish();
    });
}

let currentUtt = null;
let resumeTimer = null;
let browserGen = 0;
let userGestured = false;

let vadStream = null, vadCtx = null, vadAnalyser = null, vadTimer = null;
let userSpeaking = false;
let silenceSinceMs = 0;
let endpointArmed = false;
let waitFinalTimer = null;

let micAgc = true;
let micNsOff = false;
let micDeviceId = '';
try { micDeviceId = localStorage.getItem('loketMic') || ''; } catch { }
let sttGainVal = 1.0;
let sttGainNode = null;

try {
    const g = parseFloat(localStorage.getItem('loketGain') || '1');
    if (g >= 0.5 && g <= 4) sttGainVal = g;
} catch { }

async function ensureVadMic() {
    if (vadAnalyser) return true;
    try {

        const audioCfg = { echoCancellation: true, noiseSuppression: micNsOff ? false : { ideal: true }, autoGainControl: micAgc, channelCount: 1 };
        if (micDeviceId) audioCfg.deviceId = { exact: micDeviceId };
        vadStream = await navigator.mediaDevices.getUserMedia({ audio: audioCfg });
        try {
            vadStream.getAudioTracks().forEach(tr => {
                tr.onmute = () => {
                    setStatus('Mic dibisukan OS & browser — cek izin & perangkat mic.');
                    showToast('Mic dibisukan OS & browser — cek izin & perangkat mic.', 'err');
                };
                tr.onunmute = () => { setStatus('Mic aktif kembali.'); };
                tr.onended = () => {
                    setStatus('Mic dimatikan — cek perangkat mic lalu mulai ulang.');
                    showToast('Mic dimatikan — cek perangkat mic lalu mulai ulang.', 'err');
                };
            });
        } catch { }
        vadCtx = new (window.AudioContext || window.webkitAudioContext)();
        if (vadCtx.state === 'suspended') await vadCtx.resume().catch(() => { });
        const src = vadCtx.createMediaStreamSource(vadStream);
        vadAnalyser = vadCtx.createAnalyser();
        vadAnalyser.fftSize = 512;

        try {

            const hp = vadCtx.createBiquadFilter();
            hp.type = 'highpass';
            hp.frequency.value = 150;
            src.connect(hp);
            hp.connect(vadAnalyser);
        } catch { src.connect(vadAnalyser); }
        return true;
    } catch { return false; }
}

function releaseVadMic() {
    try {
        if (sttRecProc) { try { sttRecProc.disconnect(); } catch { } sttRecProc = null; }
        if (sttRecSrc) { try { sttRecSrc.disconnect(); } catch { } sttRecSrc = null; }
        sttGainNode = null;
        if (sttRecCtx) { sttRecCtx.close().catch(() => { }); sttRecCtx = null; }
    } catch { }
    sttRecording = false;
    sttChunks = [];
    try {
        if (vadStream) vadStream.getAudioTracks().forEach(t => { try { t.onmute = null; t.onunmute = null; t.onended = null; } catch { } });
    } catch { }
    try {
        if (vadStream) vadStream.getTracks().forEach(t => { try { t.stop(); } catch { } });
    } catch { }
    vadStream = null;
    vadAnalyser = null;
    if (vadCtx) { vadCtx.close().catch(() => { }); vadCtx = null; }
}

function vadLevel() {
    if (!vadAnalyser) return 0;
    const buf = new Uint8Array(vadAnalyser.fftSize);
    vadAnalyser.getByteTimeDomainData(buf);
    let sum = 0;
    for (let i = 0; i < buf.length; i++) {
        const v = (buf[i] - 128) / 128;
        sum += v * v;
    }
    return Math.sqrt(sum / buf.length);
}

function vadTimeData() {
    if (!vadAnalyser) return null;
    const buf = new Uint8Array(vadAnalyser.fftSize);
    vadAnalyser.getByteTimeDomainData(buf);
    return Array.from(buf);
}

function startVad(onBargeIn) {
    stopVad();
    if (!SR) return;
    const myAiGen = aiSpeechGen;
    ensureVadMic().then(ok => {
        if (!ok) return;
        const t0 = Date.now();
        let hits = 0;
        vadTimer = setInterval(() => {
            if (myAiGen !== aiSpeechGen) { stopVad(); return; }
            if (Date.now() - t0 < BARGE_GRACE_MS) return;
            const lvl = vadLevel();

            if (aiSpeaking && !recapLock && Date.now() - aiSpeechStartMs > BARGE_GRACE_MS) {
                if (lvl > BARGE_THRESHOLD && visGatePass()) {
                    if (++hits >= BARGE_HITS) {
                        stopVad();
                        onBargeIn();
                    }
                } else hits = 0;
            }

            trackUserSpeech(lvl);
        }, 100);
    });
}


function markUserSpeech() {
    endpointArmed = true;
    userSpeaking = true;
    silenceSinceMs = 0;
    lastSpeechAt = Date.now();
    ensureVadEndpoint();
}

function ensureVadEndpoint() {
    if (!continuousOn || vadTimer) return;
    ensureVadMic().then(ok => {
        if (!ok || vadTimer) return;
        vadTimer = setInterval(() => trackUserSpeech(vadLevel()), 100);
    });
}


function trackUserSpeech(lvl) {
    if (recapLock) {
        userSpeaking = false;
        silenceSinceMs = 0;
        return;
    }
    if (!continuousOn || aiSpeaking) {
        userSpeaking = false;
        if (!aiSpeaking) silenceSinceMs = 0;
        return;
    }

    if (sttBackendActive()) { trackVadBackend(lvl); return; }

    if (Date.now() < echoMuteUntil) {
        maybeEarlyOpenTail(lvl);
        if (Date.now() < echoMuteUntil) return;
    }

    if (pttActive) {
        userSpeaking = true;
        silenceSinceMs = 0;
        return;
    }
    if (lvl > USER_SPEAK_LEVEL && visGatePass()) {
        userSpeaking = true;
        silenceSinceMs = 0;
        return;
    }
    if (!userSpeaking) return;
    if (!silenceSinceMs) silenceSinceMs = Date.now();
    if (Date.now() - silenceSinceMs < VAD_SILENCE_SEND_MS) return;
    userSpeaking = false;
    silenceSinceMs = 0;
    if (endpointArmed) sendSoon();
}


function finalSettled() {
    return (Date.now() - lastSpeechAt) >= FINAL_GRACE_MS;
}
function sendSoon() {
    if (sendDebounceTimer) { clearTimeout(sendDebounceTimer); sendDebounceTimer = null; }
    if (waitFinalTimer) { clearTimeout(waitFinalTimer); waitFinalTimer = null; }
    const t = finalBuffer.trim();
    endpointArmed = false;
    if (t && autoSendAfterMic) {
        if (!finalSettled()) {
            waitFinalTimer = setTimeout(() => { waitFinalTimer = null; sendSoon(); }, Math.max(200, FINAL_GRACE_MS - (Date.now() - lastSpeechAt)));
            return;
        }
        sendText(t);
        return;
    }
    const interim = (lastInterim || '').trim();
    if (!interim || !autoSendAfterMic) {
        scheduleSend();
        return;
    }
    waitFinalTimer = setTimeout(() => {
        waitFinalTimer = null;
        const late = finalBuffer.trim();
        const body = (late || interim).trim();
        finalBuffer = '';
        if (body && autoSendAfterMic) sendText(body);
    }, WAIT_FINAL_MS);
}

function stopVad() {
    if (vadTimer) { clearInterval(vadTimer); vadTimer = null; }
}


let autoSendAfterMic = false;

function startListening() {
    if (!SR || listening || sttBackendActive()) return;
    if (recognition) { try { recognition.abort(); } catch { } recognition = null; }
    const gen = ++listenGen;
    recognition = new SR();
    recognition.lang = 'id-ID';
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;
    recognition.continuous = true;
    listening = true;
    lastResultAt = Date.now();
    updateMicBtn();
    setStatus('Mendengarkan... (mic selalu aktif)');

    let interim = '';
    recognition.onresult = (e) => {

        const myGen = gen;
        lastResultAt = Date.now();

        if (myGen !== listenGen || aiSpeaking || recapLock || Date.now() < echoMuteUntil) return;
        let newInterim = '';
        let clearShown = false;

        const mouthFinalOk = visMouthRecent(2500);
        const mouthOk = visMouthRecent(800);
        for (let i = e.resultIndex; i < e.results.length; i++) {
            const r = e.results[i];
            const t = r[0].transcript.trim();
            if (!t) continue;

            if (myGen !== listenGen || aiSpeaking || recapLock || Date.now() < echoMuteUntil) continue;
            if (r.isFinal) {
                if (!mouthFinalOk) { noteGateDrop(); continue; }
                if (isEchoOfAi(t) && !matchesShownInterim(t)) continue;
                lastSpeechAt = Date.now();
                finalBuffer += (finalBuffer ? ' ' : '') + t;
                clearShown = true;
                markUserSpeech();
                scheduleSend();
            } else {
                if (!mouthOk) continue;
                if (isEchoOfAi(t)) continue;
                lastSpeechAt = Date.now();
                newInterim += (newInterim ? ' ' : '') + t;
            }
        }

        shownInterimNorm = clearShown ? '' : normalizeWords(newInterim);
        interim = newInterim;
        lastInterim = newInterim.trim();

        const shown = (finalBuffer + ' ' + newInterim).trim();
        if (shown) { document.getElementById('inputMsg').value = shown; noteGatePass(); }
    };
    recognition.onend = () => {
        lastResultAt = Date.now();
        listening = false;
        updateMicBtn();
        if (gen !== listenGen || aiSpeaking) return;
        scheduleRestart();
    };
    recognition.onerror = (e) => {
        lastResultAt = Date.now();
        listening = false;
        updateMicBtn();
        if (gen !== listenGen || aiSpeaking) return;

        if (e.error === 'no-speech' || e.error === 'aborted' || e.error === 'network' || e.error === 'audio-capture') {
            scheduleRestart();
        } else {
            setStatus('Mic error: ' + e.error);
            scheduleRestart(1500);
        }
    };
    try { recognition.start(); }
    catch { listening = false; updateMicBtn(); scheduleRestart(1000); }
}

setInterval(() => {
  try {
    if (!continuousOn || !listening || aiSpeaking || userSpeaking || !lastResultAt) return;
    if ((finalBuffer || '').trim() || (lastInterim || '').trim()) return;
    if (Date.now() - lastResultAt < 20000) return;
    lastResultAt = 0;
    stopListening();
    setStatus('Mic dimulai ulang otomatis...');
  } catch { }
}, 2000);

let quietSince = 0;
let quietHintAt = 0;
setInterval(() => {
  try {
    if (!started || !continuousOn || !vadAnalyser) { quietSince = 0; return; }
    if (vadLevel() >= 0.005) { quietSince = 0; return; }
    if (!quietSince) quietSince = Date.now();
    if (Date.now() - quietSince < 15000) return;
    if (Date.now() - quietHintAt < 60000) return;
    quietHintAt = Date.now();
    setStatus('Mic hening total — cek volume input & perangkat mic.');
  } catch { quietSince = 0; }
}, 2000);


function scheduleSend(delay) {
    if (sendDebounceTimer) clearTimeout(sendDebounceTimer);
    sendDebounceTimer = setTimeout(() => {
        sendDebounceTimer = null;
        if (!finalSettled()) {
            scheduleSend(Math.max(200, FINAL_GRACE_MS - (Date.now() - lastSpeechAt)));
            return;
        }
        endpointArmed = false;
        if (waitFinalTimer) { clearTimeout(waitFinalTimer); waitFinalTimer = null; }
        const t = finalBuffer.trim();
        finalBuffer = '';
        lastInterim = '';
        if (t && autoSendAfterMic) sendText(t);
    }, delay || STT_FALLBACK_SEND_MS);
}

function sendText(text) {
    const input = document.getElementById('inputMsg');
    input.value = text;
    send();
}

function clearSendState() {
    if (sendDebounceTimer) { clearTimeout(sendDebounceTimer); sendDebounceTimer = null; }
    if (waitFinalTimer) { clearTimeout(waitFinalTimer); waitFinalTimer = null; }
    finalBuffer = '';
    lastInterim = '';
    endpointArmed = false;
    userSpeaking = false;
    silenceSinceMs = 0;
    shownInterimNorm = '';
    sttRecording = false;
    sttChunks = [];
}


function scheduleRestart(delay) {
    if (restartPending || !continuousOn || listening || aiSpeaking) return;
    restartPending = true;
    setTimeout(() => {
        restartPending = false;
        if (continuousOn && !listening && !aiSpeaking) startListening();
    }, delay || 300);
}

function stopListening() {
    clearSendState();
    listenGen++;
    if (recognition) {
        try { recognition.abort(); } catch { }
        recognition = null;
    }
    listening = false;
    updateMicBtn();

    if (continuousOn && !aiSpeaking) scheduleRestart();
}


function setContinuous(on) {
    continuousOn = on;
    if (on) {
        autoSendAfterMic = true;
        if (!listening) startListening();
    } else {
        autoSendAfterMic = false;
        stopListening();
    }
}


function startListeningAfterSpeech() {
    autoSendAfterMic = true;
    openDuplex();
    ensureVadEndpoint();
}

function updateMicBtn() {
    const b = document.getElementById('btnMic');
    b.classList.toggle('btn-danger', listening);
    b.classList.toggle('btn-outline-danger', !listening);
    b.innerHTML = listening ? '<i class="fas fa-stop"></i>' : '<i class="fas fa-microphone"></i>';
}


async function loadModels() {
    setStatus('Memuat model...');
    try {
        const r = await fetch('/Chat/Models');
        const j = await r.json();
        const sel = document.getElementById('modelSelect');
        sel.innerHTML = '';
        if (!r.ok) throw new Error(j.error || 'gagal');
        (j.data || []).forEach(m => {
            const o = document.createElement('option');
            o.value = m; o.textContent = m; sel.appendChild(o);
        });
        const def = (j.data || []).find(m => m.toLowerCase() === 'gpt-oss:120b');
        if (def) sel.value = def;
        if (!j.data?.length) sel.innerHTML = '<option value="">(tidak ada model)</option>';
        setStatus('Siap.');
    } catch (e) { setStatus('Gagal load model: ' + e.message); }
}

async function loadSessions() {
    try {
        const r = await fetch('/Chat/Sessions');
        const list = await r.json();
        const ul = document.getElementById('sessionList');
        ul.innerHTML = '';
        list.forEach(s => {
            const li = document.createElement('li');
            li.className = 'nav-item';
            li.innerHTML = `<a href="#" class="nav-link ${s.id === sessionId ? 'active' : ''}">
        <i class="far fa-comment nav-icon"></i><p title="${escapeHtml(s.title || 'Chat')}">${escapeHtml(s.title || 'Chat')}</p>
        <span class="badge badge-danger ml-1 del" title="hapus"><i class="fas fa-trash fa-xs"></i></span></a>`;
            li.querySelector('a').onclick = async (e) => {
                if (e.target.closest('.del')) {
                    e.preventDefault();
                    await fetch('/Chat/Sessions/' + s.id, { method: 'DELETE' });
                    if (sessionId === s.id) newChat();
                    loadSessions();
                    return;
                }
                e.preventDefault();
                stopSpeak(); stopListening();
                sessionId = s.id;
                await openSession(s.id);
                loadSessions();
            };
            ul.appendChild(li);
        });
    } catch { }
}

async function openSession(id) {
    chatBox().innerHTML = '';
    const r = await fetch('/Chat/History/' + id);
    const msgs = await r.json();
    msgs.forEach(m => bubble(m.role, m.content));
}

function newChat() {
    stopSpeak(); stopListening();
    lockedVoice = null;
    lockedCfg = null;
    sessionId = null;
    lastQrSent = null;
    masterFailCount = 0;
    plateUnreadCount = 0;
    knownDriverName = '';
    qrUsedForVerify = false;
    masterVerifiedOk = false;
    awaitingPlate = true;
    viewDriverWin = null;
    pendingPlateWin = null;
    woSavedFor = '';
    woSaveState = '';
    closeBlockedKeys = [];
    pendingSysNotes = [];
    awaitingName = false;
    lastPlateText = '';
    vkbPendingShow = false;
    hideToast();
    chatBox().innerHTML = '';
    const prevEl = document.getElementById('chatPreview');
    if (prevEl) { prevEl.textContent = ''; prevEl.title = ''; }
    chatHiddenCount = 0;
    setChatBadge(0);
    clearFormHead();
    document.getElementById('inputMsg').value = '';
    const qb = document.getElementById('qrBadge');
    if (qb) { qb.textContent = 'belum discan'; }

    resetSessionGate();
    setStatus('Percakapan baru. Klik Mulai untuk mulai.');
}


const WO_READY_RE = /\[WO_SIAP\]/i;
const WO_OK_RE = /\[WO_CUKUP\]/i;
const REKAP_RE = /\[REKAP\]/i;
let recapLock = false;
let recapLockTimer = null;
function setRecapLock(on) {
    recapLock = on;
    if (recapLockTimer) { clearTimeout(recapLockTimer); recapLockTimer = null; }
    if (on) recapLockTimer = setTimeout(() => { recapLock = false; recapLockTimer = null; }, 90000);
}
const ADMIN_DONE_RE = /\[SELESAI_ADMIN_WO\]/i;
const LOKET_DONE_RE = /\[SELESAI_LOKET_WO\]/i;
const LAYANAN_RE = /\[PILIH_LAYANAN\]/i;
const SYS_MARKER_RE = /\[(WO_SIAP|WO_CUKUP|QR_MENANG|INTERVIEW_MENANG|SELESAI_ADMIN_WO|SELESAI_LOKET_WO|LANJUT_AGENT|PILIH_LAYANAN|NAMA_SOPIR|REKAP|SAVE_DITAHAN|SAVE_GAGAL|KODE_TAK_DIKETAHUI)\]|\[HASIL CEK MASTER[^\]]*\]|\[TAHAN PENUTUPAN[^\]]*\]/gi;
function stripWoMarker(s) { return (s || '').replace(SYS_MARKER_RE, '').trim(); }

function resetToGate() {
    sessionId = null;
    lastQrSent = null;
    masterFailCount = 0;
    plateUnreadCount = 0;
    knownDriverName = '';
    qrUsedForVerify = false;
    masterVerifiedOk = false;
    awaitingPlate = true;
    viewDriverWin = null;
    pendingPlateWin = null;
    woSavedFor = '';
    woSaveState = '';
    closeBlockedKeys = [];
    pendingSysNotes = [];
    awaitingName = false;
    lastPlateText = '';
    vkbPendingShow = false;
    resetSessionGate();
}

function afterAssistant(rawResp) {
    hideVKB();
    const said = stripWoMarker(rawResp);
    if (said) bubble('assistant', said);
    loadSessions();
    completeAssistantTurn(rawResp, false);
}

const VERIFY_RE = /(udah|belum|sudah|belom)\s+(ditulis|dicatat|dicatet|masuk|kecatat|tertulis|tercatat)|ditulis\s+belum|dicatat\s+belum|masuk\s+belum/i;
const CLOSE_CATS = ['truk', 'reefer', 'cabin', 'gps', 'genset', 'box', 'ban', 'tracker'];
function userBubbleTexts() {
    const out = [];
    try {
        document.querySelectorAll('#chatBox .direct-chat-msg.right .direct-chat-text').forEach(el => {
            out.push(el.textContent || '');
        });
    } catch { }
    return out;
}
function findUncoveredVerify(closingText) {
    const close = (closingText || '').toLowerCase();
    const hits = [];
    for (const t of userBubbleTexts()) {
        if (!VERIFY_RE.test(t)) continue;
        const low = t.toLowerCase();
        const covered = CLOSE_CATS.some(c => low.includes(c) && close.includes(c));
        if (!covered) hits.push(t.trim().slice(0, 80));
    }
    return hits;
}
let closeBlockedKeys = [];
function blockUncoveredClose(rawResp) {
    const hits = findUncoveredVerify(rawResp);
    if (!hits.length) return false;
    const key = hits[0].toLowerCase().replace(/[^a-z0-9 ]/g, '');
    if (closeBlockedKeys.includes(key)) {
        showToast('Penutupan ditahan: ada yang belum tercatat, tanyakan dulu.', 'err');
        setStatus('Penutupan ditahan — ada yang belum tercatat.');
        return true;
    }
    closeBlockedKeys.push(key);
    if (closeBlockedKeys.length > 5) closeBlockedKeys.shift();
    const msg = `[TAHAN PENUTUPAN: sopir sempat menanyakan status pencatatan ("${hits[0]}") tapi hal itu tidak merujuk ke kategori yang sudah direkap. JANGAN tutup dengan [WO_SIAP]. Koreksi dengan sopan bahwa itu belum tercatat, lalu tanyakan detail + kategorinya (Truk, Reefer, Cabin, GPS, Genset, Box, Ban) sebelum merangkum.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
    showToast('Penutupan ditahan: ada yang belum tercatat — AI diminta tanya dulu.', 'err');
    setStatus('Penutupan ditahan — AI menanyakan yang belum tercatat.');
    try {
        const mel = document.getElementById('modelSelect');
        fetch('/Chat/Send', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionId, model: mel ? mel.value : null, message: msg })
        }).then(r => r.json()).then(j => {
            if (j && j.sessionId) sessionId = j.sessionId;
            if (j && j.response) afterAssistant(j.response);
        }).catch(() => { setStatus('Gagal meminta koreksi AI.'); });
    } catch { }
    return true;
}

let jsServerHash = '';
let versionTimer = null;
let versionWarned = false;
async function pollJsVersion() {
    if (!started || versionWarned) return;
    try {
        const r = await fetch('/Chat/Version');
        const j = await r.json();
        if (!r.ok || !j || !j.jsHash || j.jsHash === '0') return;
        if (!jsServerHash) { jsServerHash = j.jsHash; return; }
        if (j.jsHash !== jsServerHash) {
            versionWarned = true;
            showToast('Ada versi baru — muat ulang halaman (Ctrl+F5) agar perbaikan berlaku.', 'err');
            setStatus('Versi baru tersedia — muat ulang halaman.');
        }
    } catch { }
}
function startVersionWatch() {
    stopVersionWatch();
    versionWarned = false;
    jsServerHash = '';
    versionTimer = setInterval(pollJsVersion, 5 * 60 * 1000);
    pollJsVersion();
}
function stopVersionWatch() {
    if (versionTimer) { clearInterval(versionTimer); versionTimer = null; }
}

let vkbPendingShow = false;
function armVkb() { vkbPendingShow = true; }
function flushVkbPending() {
    if (!vkbPendingShow) return;
    vkbPendingShow = false;
    showVKB();
}

const KELUHAN_NORESP_MS = 15000;
const KELUHAN_NORESP_MAX = 8;
const KELUHAN_CLOSE_DELAY_MS = 5000;
const KELUHAN_ASK = 'Bisa dijelaskan dari truk apa yang mau di periksa atau di perbaiki bang?';
const KELUHAN_CLOSE = 'Baik bang, jika tidak ada keluhan maka percakapan ini ditutup.';
let keluhanWatchArmed = false;
let keluhanWatchTimer = null;
let keluhanWatchCount = 0;
function stopKeluhanWatch() {
    if (keluhanWatchTimer) { clearTimeout(keluhanWatchTimer); keluhanWatchTimer = null; }
    keluhanWatchArmed = false;
    keluhanWatchCount = 0;
}
function maybeStartKeluhanWatch() {
    if (!keluhanWatchArmed) return;
    keluhanWatchArmed = false;
    startKeluhanWatch();
}
function startKeluhanWatch() {
    stopKeluhanWatch();
    if (!sessionId || closing) return;
    const tick = () => {
        keluhanWatchTimer = null;
        if (!sessionId || closing) return;
        if (isSpeaking()) { keluhanWatchTimer = setTimeout(tick, KELUHAN_NORESP_MS); return; }
        keluhanWatchCount++;
        if (keluhanWatchCount > KELUHAN_NORESP_MAX) { closeKeluhanWatch(); return; }
        bubble('assistant', KELUHAN_ASK);
        if (speakerOn) {
            setStatus('AI bicara...');
            try { stopListening(); } catch { }
            speak(KELUHAN_ASK, () => {
                setStatus('Siap.');
                if (voiceMode) startListeningAfterSpeech();
                if (sessionId && !closing) keluhanWatchTimer = setTimeout(tick, KELUHAN_NORESP_MS);
            });
        } else {
            setStatus('Siap.');
            if (voiceMode) startListeningAfterSpeech();
            keluhanWatchTimer = setTimeout(tick, KELUHAN_NORESP_MS);
        }
    };
    keluhanWatchTimer = setTimeout(tick, KELUHAN_NORESP_MS);
}

function closeKeluhanWatch() {
    stopKeluhanWatch();
    if (!sessionId) { resetToGate(); return; }
    closing = true;
    bubble('assistant', KELUHAN_CLOSE);
    showToast('Tidak ada keluhan — percakapan ditutup.', 'ok');
    const done = () => { setTimeout(() => { resetToGate(); }, KELUHAN_CLOSE_DELAY_MS); };
    if (speakerOn) {
        setStatus('AI bicara...');
        try { stopListening(); } catch { }
        noBarge = true;
        speak(KELUHAN_CLOSE, () => { noBarge = false; done(); });
    } else done();
}

function completeAssistantTurn(rawResp, alreadySpoken) {
    if (ADMIN_DONE_RE.test(rawResp || '')) {
    closing = true;
    stopKeluhanWatch();
    showToast('Diarahkan ke petugas loket.', 'ok');
    if (!alreadySpoken && speakerOn) {
      const said = stripWoMarker(rawResp);
      if (said) {
        setStatus('AI bicara...');
        try { stopListening(); } catch { }
        noBarge = true;
        speak(said, () => { noBarge = false; setStatus('Siap.'); resetToGate(); });
        return;
      }
    }
        if (!alreadySpoken) setStatus('Siap.');
        resetToGate();
        return;
    }

    if (LOKET_DONE_RE.test(rawResp || '')) {
    closing = true;
    stopKeluhanWatch();
    showToast('Diarahkan ke loket WO manusia.', 'ok');
    if (!alreadySpoken && speakerOn) {
      const said = stripWoMarker(rawResp);
      if (said) {
        setStatus('AI bicara...');
        try { stopListening(); } catch { }
        noBarge = true;
        speak(said, () => { noBarge = false; setStatus('Siap.'); resetToGate(); });
        return;
      }
    }
        if (!alreadySpoken) setStatus('Siap.');
        resetToGate();
        return;
    }

    if (!alreadySpoken) {
        const said = stripWoMarker(rawResp);
        if (REKAP_RE.test(rawResp || '')) setRecapLock(true);
        if (speakerOn) {
            setStatus('AI bicara...');
            speak(said, () => {
                setRecapLock(false);
                flushVkbPending();
                maybeStartKeluhanWatch();
                setStatus('Siap.');
                if (voiceMode) startListeningAfterSpeech();
            });
        } else {
            setRecapLock(false);
            flushVkbPending();
            maybeStartKeluhanWatch();
            setStatus('Siap.');
            if (voiceMode) startListeningAfterSpeech();
        }
    } else {
        setRecapLock(false);
        flushVkbPending();
        maybeStartKeluhanWatch();
        setStatus('Siap.');
        if (voiceMode) startListeningAfterSpeech();
    }

    if (WO_READY_RE.test(rawResp || '')) {
        if (!blockUncoveredClose(rawResp)) saveWoAuto();
    }
    if (WO_OK_RE.test(rawResp || '')) {
        showToast('WO sudah ada — tidak buat baru.', 'ok');
        setStatus('WO sudah ada — sesi selesai tanpa simpan baru.');
        resetToGate();
        return;
    }
    if (LAYANAN_RE.test(rawResp || '')) pickSrvAgent();
}

function showToast(msg, kind) {
    if (chatCollapsed) {
        noteSystem(msg);
        return;
    }
    const t = document.getElementById('toast');
    if (!t) { setStatus(msg); return; }
    t.textContent = msg;
    t.className = 'ai-toast show ' + (kind === 'err' ? 'ai-toast-err' : 'ai-toast-ok');
    clearTimeout(showToast._t);
    if (kind !== 'err') showToast._t = setTimeout(hideToast, 6000);
}
function hideToast() {
    const t = document.getElementById('toast');
    if (t) t.className = 'ai-toast';
}

let woSaveState = '';
let woSavedFor = '';
function onWoFailed(errMsg) {
  closing = true;
  const msg = 'Mohon maaf, WO gagal dibuat. Silakan langsung ke loket WO manusia untuk dibantu petugas.';
    showToast('Gagal simpan WO: ' + errMsg + ' — diarahkan ke loket WO manusia.', 'err');
    setStatus('Gagal simpan WO — diarahkan ke loket WO manusia.');
    bubble('assistant', msg);
    stashSysNote('\n[SAVE_GAGAL: ' + String(errMsg || '').slice(0, 200) + '] (Hasil sistem otomatis, bukan ucapan sopir.)');
    stopKeluhanWatch();
    const mySeq = ++woAnnounceSeq;
    const done = () => { if (mySeq === woAnnounceSeq) resetToGate(); };
    if (speakerOn) speak(msg, done);
    else done();
}

async function saveWoAuto() {
    if (!sessionId || woSavedFor === sessionId || woSaveState === 'saving') return;
    woSaveState = 'saving';
    setStatus('Menyimpan WO...');
    showWoSaving('Menghubungi sistem...');
    try {
        const qr = (lastQrSent || '').trim();
        if (qr) {
            let dup = false;
            let dupWos = [];
            try {
                const rc = await fetch('/Chat/KimoExists?srd=' + encodeURIComponent(qr));
                const jc = await rc.json();
                dup = !!(rc.ok && jc && jc.exists);
                if (dup && Array.isArray(jc.wos)) dupWos = jc.wos;
            } catch { dup = false; }
            if (dup) {
                closeWoSaving();
                onWoDuplicate(qr, dupWos);
                return;
            }
        }

        showWoSaving('Menyimpan ke sistem...');
        const w = await doCreateWo();
        woSavedFor = sessionId;
        closeWoSaving();
        onWoCreated(w);
    } catch (e) {
        closeWoSaving();
        onWoFailed(e.message);
    } finally {
        woSaveState = '';
    }
}

async function doCreateWo() {
    const r = await fetch('/Chat/WO', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId })
    });
    const w = await r.json();
    if (!r.ok) {
        let msg = (w && w.error) || 'gagal';
        try {
            const inner = JSON.parse(msg);
            if (inner && inner.error) msg = inner.error;
        } catch { }
        throw new Error(msg);
    }
    return w;
}

function resetForNextDriver() {
    sessionId = null;
    autoGreeted = false;
    autoNoPersonSince = null;
    lastQrSent = null;
    masterFailCount = 0;
    plateUnreadCount = 0;
    knownDriverName = '';
    qrUsedForVerify = false;
    masterVerifiedOk = false;
    awaitingPlate = true;
    viewDriverWin = null;
    pendingPlateWin = null;
    setContinuous(false);
    stopVad();
    stopSpeak();
    stopVersionWatch();
    setRecapLock(false);
    stopKeluhanWatch();
    woSaveState = '';
    closeBlockedKeys = [];
    pendingSysNotes = [];
    awaitingName = false;
    lastPlateText = '';
    vkbPendingShow = false;
    const qb = document.getElementById('qrBadge');
    if (qb) { qb.textContent = 'belum discan'; }
  hideVKB();
}

function dupRecapText(wos) {
    const rows = Array.isArray(wos) ? wos : [];
    return rows.map(w => {
        const no = w.woNo || w.WoNo || '-';
        const tgl = w.woDate || w.WoDate || '';
        const np = w.nopol || w.Nopol || '';
        const jn = w.jenis || w.Jenis || '';
        const dr = w.driver || w.Driver || '';
        const kl = w.keluhan || w.Keluhan || '';
        return `\u2022 ${no}${tgl ? ' (' + tgl + ')' : ''}${jn ? ' [' + jn + ']' : ''}: ${np} — ${dr}: ${kl}`;
    }).join('\n');
}
function onWoDuplicate(qr, wos) {
  stopKeluhanWatch();
  const recap = dupRecapText(wos);
  const msg = 'WO untuk nomor Kimo ' + qr + ' pernah dibuat.' + (recap ? '\n' + recap : '');
  showToast(msg.split('\n')[0] + ' Lanjut diproses oleh Agent AI.', 'ok');
  setStatus(msg.split('\n')[0]);
  bubble('assistant', msg);
  pickDupAgent(qr, wos);
}
async function pickSrvAgent() {
  if (!sessionId) return;
  lastQrSent = null;
  woSavedFor = '';
  setStatus('AI mengetik...');
  try {
    const r = await fetch('/Chat/Send', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sessionId,
        model: document.getElementById('modelSelect').value || null,
        message: '[LANJUT_AGENT] (Sopir tanpa QR KIMO dilayani AI. Interview manual penuh: tanyakan nopol (minta eja satu per satu), keluhan dan kategori perbaikan, lokasi parkir, lalu rangkum dan tutup [WO_SIAP] sebagai WO baru. JANGAN tanyakan nama/kode driver; bila sopir sukarela menyebut namanya, catat apa adanya tanpa konfirmasi.)'
      })
    });
    const j = await r.json();
    if (!r.ok) throw new Error(j.error || 'gagal');
    afterAssistant(j.response);
  } catch (e) { setStatus('Gagal lanjut agent: ' + e.message); }
}
async function pickDupAgent(qr, wos) {
  closing = false;
  if (!sessionId) return;
    lastQrSent = null;
    woSavedFor = '';
    setStatus('AI mengetik...');
    try {
        const recap = dupRecapText(wos);
        const r = await fetch('/Chat/Send', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId,
                model: document.getElementById('modelSelect').value || null,
                message: '[LANJUT_AGENT] (QR KIMO duplikat: untuk nomor Kimo ' + (qr || '') + ' SUDAH pernah dibuat WO berikut:\n' + (recap || '(rekap tak tersedia)') + '\nSebutkan rekap itu secara natural (nomor WO, tanggal, nopol, driver, keluhan tiap baris). JANGAN tanyakan ulang nama/kode driver/nopol/jenis (pakai data rekap). Lalu tanyakan SATU pertanyaan: sudah cukup atau ada tambahan keluhan baru? Bila sopir bilang cukup/sudah/tidak ada: tutup dengan baris [WO_CUKUP] sendirian di baris terakhir (JANGAN buat WO baru). Bila ada tambahan: tanyakan kategori + detailnya lewat suara, lalu rangkum dan tutup [WO_SIAP] sebagai WO baru.)'
            })
        });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'gagal');
        afterAssistant(j.response);
    } catch (e) { setStatus('Gagal lanjut agent: ' + e.message); }
}
function onWoCreated(w) {
    closing = true;
    try { stopListening(); } catch { }
    resetForNextDriver();
    stopCam();
    showToast('WO sudah selesai disimpan, silahkan menunggu.', 'ok');
    setFormHead(w.unitNumber || w.UnitNumber, w.driverName || w.DriverName);
    fillKeluhanRows(w.keluhanRows || w.KeluhanRows);
    const wn = w.woNumbers || w.WoNumbers;
    fillWoNumbers(w.woNumber || w.WoNumber, wn);
    const ringkas = woNumberSummary(wn);
    setStatus(ringkas ? 'WO tercatat: ' + ringkas : 'WO tercatat. Menunggu sopir berikutnya...');
const mySeq = ++woAnnounceSeq;
    const backToStart = () => {
        closeWoDone();
        loadSessions();
        setStatus('Menunggu tombol Mulai...');
        resetSessionGate();
    };
    backToStartRef = backToStart;
    const announce = () => {
        if (mySeq !== woAnnounceSeq) { backToStart(); return; }
        try { stopListening(); } catch { }
        noBarge = true;
        setStatus(ringkas ? 'WO tercatat: ' + ringkas : 'WO tercatat.');
        if (speakerOn) {
            const gateTimer = setTimeout(() => {
                clearTimeout(gateTimer);
                noBarge = false;
                showWoDone(w, mySeq);
            }, 12000);
            speak('Terima kasih telah melapor ke loket WO. WO sudah selesai disimpan, silahkan menunggu.', () => {
                clearTimeout(gateTimer);
                noBarge = false;
                setTimeout(() => showWoDone(w, mySeq), 1500);
            });
        } else {
            noBarge = false;
            setTimeout(() => showWoDone(w, mySeq), 1500);
        }
    };
    setStatus('Menutup percakapan...');
    waitForQuiet(announce);
}

let woDoneTimer = null;
let woSaveTimer = null;
let woSaveWatchdog = null;
const WO_DONE_SECONDS = 60;
const WO_SAVE_MAX_MS = 90000;

function closeWoSaving() {
    if (woSaveTimer) { clearInterval(woSaveTimer); woSaveTimer = null; }
    if (woSaveWatchdog) { clearTimeout(woSaveWatchdog); woSaveWatchdog = null; }
    const box = document.getElementById('woDone');
    const saving = document.getElementById('woDoneSave');
    if (saving) saving.style.display = 'none';
    if (!box) return;
    const view = document.getElementById('woDoneView');
    if (view && view.style.display === 'flex') return;
    box.style.display = 'none';
}

function showWoSaving(stepText) {
    const box = document.getElementById('woDone');
    if (!box) return;
    const saving = document.getElementById('woDoneSave');
    const view = document.getElementById('woDoneView');
    if (view) view.style.display = 'none';
    if (saving) saving.style.display = 'block';
    const step = document.getElementById('woSaveStep');
    if (step && stepText) step.textContent = stepText;
    const el = document.getElementById('woSaveElapsed');
    let sec = 0;
    box.style.display = 'flex';
    if (el) el.textContent = '0 dtk';
    if (woSaveTimer) clearInterval(woSaveTimer);
    woSaveTimer = setInterval(() => { sec++; if (el) el.textContent = sec + ' dtk'; }, 1000);
    if (woSaveWatchdog) clearTimeout(woSaveWatchdog);
    woSaveWatchdog = setTimeout(() => {
        closeWoSaving();
        showToast('Penyimpanan WO melebihi batas waktu. Silakan cek ke petugas.', 'err');
        setStatus('Gagal menyimpan WO — melebihi batas waktu.');
        resetToGate();
    }, WO_SAVE_MAX_MS);
}

function closeWoDone() {
    if (woDoneTimer) { clearInterval(woDoneTimer); woDoneTimer = null; }
    const m = document.getElementById('woDone');
    if (m) m.style.display = 'none';
}

function showWoDone(w, mySeq) {
    if (mySeq !== woAnnounceSeq) { backToStartRef?.(); return; }
    const box = document.getElementById('woDone');
    if (!box) { backToStartRef?.(); return; }
    const wn = w.woNumbers || w.WoNumbers || {};
    const keys = Object.keys(wn);
    const nopol = w.unitNumber || w.UnitNumber || '';
    const nama = w.driverName || w.DriverName || '';
    const park = w.parkingLocation || w.ParkingLocation || '';
    const html = [];
    if (nopol || nama) {
        html.push(`<div class="wo-meta">${escapeHtml(`${nopol}${nopol && nama ? ' · ' : ''}${nama}`.trim())}</div>`);
    }
    keys.forEach((k) => {
        html.push(`<div class="wo-row"><span class="wo-k">${escapeHtml(k)}</span><span class="wo-v">${escapeHtml(wn[k] || '')}</span></div>`);
    });
    if (park) html.push(`<div class="wo-meta">Parkir: ${escapeHtml(park)}</div>`);
    const list = document.getElementById('woDoneList');
    if (list) list.innerHTML = html.join('');
    const saving = document.getElementById('woDoneSave');
    const view = document.getElementById('woDoneView');
    if (saving) saving.style.display = 'none';
    if (view) view.style.display = 'block';
    const note = document.getElementById('woDoneNote');
    if (note) note.textContent = keys.length > 1
        ? `${keys.length} WO tersimpan di sistem.`
        : 'Nomor WO sudah tersimpan di sistem.';
    const cd = document.getElementById('woDoneCountdown');
    let left = WO_DONE_SECONDS;
    const tick = () => {
        if (left <= 0) { backToStartRef?.(); return; }
        const mm = Math.floor(left / 60);
        const ss = String(left % 60).padStart(2, '0');
        if (cd) cd.textContent = 'Kembali otomatis dalam ' + mm + ':' + ss;
        left--;
    };
    box.style.display = 'flex';
    tick();
    if (woDoneTimer) clearInterval(woDoneTimer);
    woDoneTimer = setInterval(tick, 1000);
}

let backToStartRef = null;
const woDoneNextBtn = document.getElementById('btnWoDoneNext');
if (woDoneNextBtn) woDoneNextBtn.onclick = () => backToStartRef?.();

// Tunggu AI selesai bicara (rekap) sebelum callback. Poll 300ms, maks 12 dtk.
function waitForQuiet(cb, waited) {
    waited = waited || 0;
    let quiet = false;
    try {
        const sa = streamActive;
        quiet = !isSpeaking() && (!sa || sa.done);
    } catch { quiet = true; }
    if (quiet || waited > 12000) { cb(); return; }
    setTimeout(() => waitForQuiet(cb, waited + 300), 300);
}
let woAnnounceSeq = 0;


let sQueue = [];
let sBuf = '';
let sFull = '';
let sGenRef = -1;
let sWantAudio = true;
let streamActive = null;
let finishSayEl = null;
let streamDoneInfo = null;
let streamPartialError = '';
let sAudioPlayed = false; // true = audio stream sudah berbunyi (speak ulang tak perlu)

const sleepMs = (ms) => new Promise(res => setTimeout(res, ms));

function streamAudioBegin() {
    stopVad();
    stopNeural();
    if (TTS) speechSynthesis.cancel();
    sGenRef = speechGen;
    sQueue = [];
    sBuf = '';
    sFull = '';
    sAudioPlayed = false;
    streamActive = { done: false, doneInfo: null, playResolve: null };
    closeDuplex();
    streamAudioLoop();
}

function queueStreamClause(c) {
    if (!sWantAudio || sGenRef !== speechGen || !streamActive) return;
    rememberAiSpeech(c);
    // SATU source of truth: cfg terkunci per sesi — jangan baca voiceCfg() mentah
    // (fetch nyasar ke server = bug Sarah). Browser: tanpa fetch server.
    const cfg = ensureVoiceLock();
    if (cfg.voice === 'browser') return;
    sQueue.push({
        text: c,
        promise: fetchTts(ttsUrl(c, cfg), 45000)
            .then(r => { if (!r.ok) throw new Error('TTS ' + r.status); return r.blob(); })
    });
}

function feedStreamText(chunk) {
    sFull += chunk;
    if (!sWantAudio || sGenRef !== speechGen || !streamActive) return;
    sBuf += chunk;
    const parts = sBuf.split(/(?<=[.!?…])\s+|\n+/);
    sBuf = parts.pop();
    for (const p of parts) {
        const c = (p || '').trim();
        if (!c) continue;

        for (const piece of chunkForSpeech(c, 220)) queueStreamClause(piece);
    }
}

function flushStreamBuf() {
    const c = (sBuf || '').trim();
    sBuf = '';
    if (!c || !sWantAudio || sGenRef !== speechGen || !streamActive) return;
    for (const piece of chunkForSpeech(c, 220)) queueStreamClause(piece);
}

function streamTail(fromIdx) {
    const tail = sQueue.slice(fromIdx).map(q => q.text).join(' ') + ' ' + (sBuf || '');
    return tail.trim();
}

function streamBrowserDone() {
    flushVkbPending();
    setStatus('Siap.');
    if (voiceMode) startListeningAfterSpeech();
}


async function streamAudioLoop() {
    const myGen = sGenRef;
    let i = 0;
    let first = true;
    while (true) {
        while (i >= sQueue.length) {
            if (!streamActive) return;
            if (streamActive.done) {
                const di = streamActive.doneInfo;
                streamActive = null;
                doneChain(() => finishStreamTurn(di));
                return;
            }
            await sleepMs(120);
            if (myGen !== speechGen) return;
        }
        let blob;
        try {
            blob = await sQueue[i].promise;
        } catch {

            if (streamActive) streamActive.done = true;
            if (myGen === speechGen) {
                stopNeural();
                streamActive = null;
                doneChain(null);
                sAudioPlayed = true; // ekor dibacakan via fallback
                browserSpeak(streamTail(i) || sFull, streamBrowserDone);
            }
            return;
        }
        if (myGen !== speechGen) return;
        const st = await playStreamBlob(blob, first);
        if (st !== 'ended') {

            if (st === 'barged' && streamDoneInfo) {
                const di = streamDoneInfo;
                streamDoneInfo = null;
                streamActive = null;
                vkbPendingShow = false;
                sessionId = di.sessionId || sessionId;
                loadSessions();
                completeAssistantTurn(sFull, true);
            } else {
                streamActive = null;
            }
            return;
        }
        sAudioPlayed = true; // klausa selesai berbunyi
        i++;
        first = false;
    }
}

function playStreamBlob(blob, isFirst) {
    return new Promise((resolve) => {
        if (sGenRef !== speechGen || !streamActive) return resolve('aborted');
        const url = URL.createObjectURL(blob);
        const a = new Audio(url);
        a.preload = 'auto';
        clauseAudios.push(a);
        neuralAudio = a;
        neuralPlaying = true;
        if (streamActive) streamActive.playResolve = (how) => { try { resolve(how); } catch { } };
        const cleanup = () => {
            URL.revokeObjectURL(url);
            clauseAudios = clauseAudios.filter(x => x !== a);
            if (streamActive && streamActive.playResolve) streamActive.playResolve = null;
        };

        a.onended = () => {
            cleanup();
            if (sGenRef !== speechGen || !streamActive) return resolve('aborted');
            resolve('ended');
        };

        a.onerror = () => { cleanup(); resolve('error'); };
        const p = a.play();

        if (p && p.catch) p.catch(() => {
            if (sGenRef !== speechGen || !streamActive) return resolve('aborted');
            neuralPlaying = false;
            const b = document.getElementById('permBanner');
            if (b) {
                document.getElementById('permText').textContent =
                    'Browser mengunci suara otomatis. Klik tombol ini sekali, setelah itu full otomatis.';
                b.style.display = 'block';
            }

            const retry = () => {
                document.removeEventListener('click', retry);
                if (sGenRef !== speechGen || !streamActive) return resolve('aborted');
                neuralPlaying = true;
                a.play().then(() => { }).catch(() => resolve('error'));
            };

            document.addEventListener('click', retry);
        });

        if (isFirst) startVad(() => {
            if (sGenRef !== speechGen) return;
            vkbPendingShow = false;
            setStatus('Disela sopir — mendengarkan...');
            aiSpeechGen++;
            aiSpeaking = false;
            stopNeural();
            if (streamActive && streamActive.playResolve) {
                const pr = streamActive.playResolve;
                streamActive.playResolve = null;
                try { pr('barged'); } catch { }
            }
            openDuplex(true);
        });
    });
}

function finishStreamTurn(di) {
    if (di && di.sessionId) sessionId = di.sessionId;
    streamDoneInfo = null;
    const clean = stripWoMarker(sFull);
    if (finishSayEl) {
        if (clean.trim()) finishSayEl.textContent = clean;
        else if (finishSayEl.parentElement) finishSayEl.parentElement.remove();
        finishSayEl = null;
    } else if (clean.trim()) {
        bubble('assistant', clean);
    }
    loadSessions();
    if (streamPartialError) {
        showToast(streamPartialError, 'err');
        streamPartialError = '';
    }
    // sAudioPlayed=false (mis. lock browser: tanpa fetch server) -> bicara sekali di sini.
    completeAssistantTurn(sFull, sAudioPlayed);
}

function cleanupStreamEarly() {
    streamActive = null;
    streamDoneInfo = null;
    if (finishSayEl && finishSayEl.parentElement) finishSayEl.parentElement.remove();
    finishSayEl = null;
    sFull = '';
    sBuf = '';
    sQueue = [];
    openDuplex();
}


async function sendStream(payload) {
    let r;
    try {
        r = await fetch('/Chat/StreamSend', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
    } catch { return 'fallback'; }
    if (!r.ok || !r.body) {
        try { r.body && await r.body.cancel(); } catch { }
        return 'fallback';
    }

    finishSayEl = bubble('assistant', '…');
    streamPartialError = '';
    streamDoneInfo = null;
    sFull = '';
    sBuf = '';
    sQueue = [];
    sAudioPlayed = false;
    sWantAudio = speakerOn;
    if (sWantAudio) {
        streamAudioBegin();
    } else {
        sGenRef = speechGen;
        streamActive = null;
    }

    const reader = r.body.getReader();
    const dec = new TextDecoder();
    let buf = '', full = '', gotToken = false, doneInfo = null, streamErr = '';
    try {
        while (true) {
            if (sGenRef !== speechGen) { try { await reader.cancel(); } catch { } return 'aborted'; }
            const { done, value } = await reader.read();
            if (sGenRef !== speechGen) { try { await reader.cancel(); } catch { } return 'aborted'; }
            if (done) break;
            buf += dec.decode(value, { stream: true });
            let idx;
            while ((idx = buf.indexOf('\n\n')) >= 0) {
                const raw = buf.slice(0, idx);
                buf = buf.slice(idx + 2);
                for (const line of raw.split('\n')) {
                    const t = line.trim();
                    if (!t.startsWith('data:')) continue;
                    let ev;
                    try { ev = JSON.parse(t.slice(5)); } catch { continue; }
                    if (ev && ev.type === 'token' && typeof ev.text === 'string' && ev.text) {
                        gotToken = true;
                        full += ev.text;
                        if (finishSayEl) {
                            finishSayEl.textContent = full;
                            followChatBottom();
                        }
                        feedStreamText(ev.text);
                    } else if (ev && ev.type === 'done') {
                        doneInfo = ev;
                    } else if (ev && ev.type === 'error') {
                        streamErr = ev.error || 'stream error';
                    }
                }
                if (doneInfo || streamErr) break;
            }
            if (doneInfo || streamErr) { try { await reader.cancel(); } catch { } break; }
        }
    } catch {
        if (!gotToken) { cleanupStreamEarly(); return 'fallback'; }
        streamErr = streamErr || 'koneksi terputus';
    }
    if (!gotToken) { cleanupStreamEarly(); return 'fallback'; }
    if (streamErr) {

        streamPartialError = 'Respons terputus di tengah; ditampilkan seadanya.';
        streamDoneInfo = doneInfo;
        if (!sWantAudio || !streamActive) {
            if (sWantAudio) flushStreamBuf();
            finishStreamTurn(doneInfo);
        } else {
            flushStreamBuf();
            streamActive.done = true;
        }
        return true;
    }

    streamDoneInfo = doneInfo;
    if (!sWantAudio) {
        finishStreamTurn(doneInfo);
        return true;
    }
    flushStreamBuf();
    if (streamActive) {
        streamActive.done = true;
        streamActive.doneInfo = doneInfo;
    } else {
        finishStreamTurn(doneInfo);
    }
    return true;
}

async function send() {
    if (!started) return;
    vkbPendingShow = false;
    stopKeluhanWatch();
    hideVKB();
    const input = document.getElementById('inputMsg');
    const rawText = input.value.trim();
    if (!rawText) return;
    const text = formatPlateText(rawText);
    if (text !== rawText) input.value = text;
    if (!sessionId && closing) return;
    woAnnounceSeq++; // giliran user baru: batalkan ucapan terima-kasih yang tertunda
    clearSendState();
    const model = document.getElementById('modelSelect').value;
    input.value = '';
    clearTypingBubble();
    const tagM = /^\[KELUHAN ([^\]]+):([\s\S]*)\]$/.exec(text);
    bubble('user', tagM ? ('Keluhan ' + tagM[1] + ': ' + tagM[2].trim()) : text);
    setStatus('AI mengetik...');
    document.getElementById('btnSend').disabled = true;
    try {

        let fillerP = Promise.resolve();
        if (!tagM && isStrictPlate(text) && speakerOn) {
            setStatus('Mengecek nopol...');
            showToast('Mengecek ke bank data...', 'ok');
            fillerP = speakAwait(pickFiller());
        }
        const verifyNote = tagM ? '' : await checkMasterNote(text);
        await fillerP;
        setStatus('AI mengetik...');
        const payload = { sessionId, model, message: text + verifyNote + takeSysNotes() };
        if (!sessionId && (voiceMode || speakerOn)) payload.systemPrompt = voicePrompt();

        const streamed = await sendStream(payload);
        if (streamed === true) return;
        if (streamed === 'aborted') return;
        const r = await fetch('/Chat/Send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'gagal');
        sessionId = j.sessionId;
        afterAssistant(j.response);
    } catch (e) {
        bubble('assistant', 'Error: ' + e.message);
        setStatus('Error.');
    } finally {
        document.getElementById('btnSend').disabled = false;
    }
}


const chatForm = document.getElementById('chat-form');
if (chatForm) chatForm.addEventListener('submit', (e) => { e.preventDefault(); send(); });
else document.getElementById('btnSend').onclick = send;

const inputMsgEl = document.getElementById('inputMsg');
if (inputMsgEl) inputMsgEl.addEventListener('input', () => formatPlateInput(inputMsgEl));

function setFormHead(nopol, nama, jenis) {
  if (nopol) {
    const p = document.getElementById('plateHead');
    if (p) p.textContent = nopol.toUpperCase();
  }
  if (nama) {
    knownDriverName = nama;
    const n = document.getElementById('fNama');
    if (n) n.value = nama;
  }
  if (jenis) {
    const j = document.getElementById('fJenis');
    if (j) j.value = jenis;
  }
}
function clearFormHead() {
  const p = document.getElementById('plateHead');
  if (p) p.textContent = '—';
  const n = document.getElementById('fNama');
  if (n) n.value = '';
  const j = document.getElementById('fJenis');
  if (j) j.value = '';
  document.querySelectorAll('.fKeluhan').forEach((el) => { el.value = ''; });
  document.querySelectorAll('.fWo').forEach((el) => { el.value = ''; });
}
function fillKeluhanRows(map) {
  if (!map) return;
  const norm = {};
  Object.keys(map).forEach((k) => { norm[String(k).trim().toLowerCase()] = map[k]; });
  document.querySelectorAll('.fKeluhan').forEach((el) => {
    const cat = ((el.dataset && el.dataset.cat) || '').trim().toLowerCase();
    const v = norm[cat];
    if (cat && v) {
      const cur = (el.value || '').trim();
      el.value = cur ? (cur + '; ' + String(v).trim()) : String(v).trim();
    }
  });
}
function fillWoNumbers(woNo, woNumbers) {
    if (!woNo && !woNumbers) return;
    const map = {};
    if (woNumbers && typeof woNumbers === 'object') {
        Object.keys(woNumbers).forEach((k) => {
            const cat = String(k).trim().toLowerCase();
            const v = (woNumbers[k] || '').trim();
            if (cat && v) map[cat] = v;
        });
    }
    document.querySelectorAll('.loket-frow').forEach((row) => {
        const k = row.querySelector('.fKeluhan');
        const w = row.querySelector('.fWo');
        if (!w) return;
        if (Object.keys(map).length > 0) {
            const cat = ((k && k.dataset && k.dataset.cat) || '').trim().toLowerCase();
            if (cat && map[cat]) { w.value = map[cat]; return; }
        }
        if (woNo && k && (k.value || '').trim()) w.value = woNo;
    });
}
function woNumberSummary(woNumbers) {
    if (!woNumbers || typeof woNumbers !== 'object') return '';
    return Object.keys(woNumbers).map((k) => `${k} ${woNumbers[k]}`).join(' • ');
}
function sendKeluhan(el) {
  const t = ((el && el.value) || '').trim();
  if (!t || !sessionId) return;
  const cat = (el.dataset && el.dataset.cat) || 'Umum';
  const input = document.getElementById('inputMsg');
  if (!input) return;
  input.value = `[KELUHAN ${cat}: ${t}]`;
  send();
}
document.querySelectorAll('.fKeluhan').forEach((el) => {
  el.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); sendKeluhan(el); }
  });
});
document.getElementById('btnNew').onclick = newChat;
document.getElementById('btnReloadModels').onclick = loadModels;

function toggleMicContinuous() {
    if (!SR) { setStatus('Browser tidak mendukung SpeechRecognition (pakai Chrome).'); return; }
    if (listening) {
        setContinuous(false);
        setStatus('Mic mati. Klik lagi untuk selalu dengar.');
    } else {
        setContinuous(true);
        setStatus('Mic selalu aktif — bicara kapan saja.');
    }
}


let micPressTimer = null, micHolding = false, micWasPtt = false, suppressMicClick = false;
document.getElementById('btnMic').onclick = () => {
    if (suppressMicClick) { suppressMicClick = false; return; }
    toggleMicContinuous();
};
document.getElementById('btnMic').onpointerdown = (e) => {
    try { e.preventDefault(); } catch { }
    micHolding = true;
    micWasPtt = false;
    micPressTimer = setTimeout(() => {
        if (micHolding) { micWasPtt = true; pttStart(); }
    }, 350);
};
const micRelease = () => {
    if (!micHolding) return;
    micHolding = false;
    if (micPressTimer) { clearTimeout(micPressTimer); micPressTimer = null; }
    if (micWasPtt) { suppressMicClick = true; pttEnd(); }
    micWasPtt = false;
};
document.getElementById('btnMic').onpointerup = micRelease;
document.getElementById('btnMic').onpointercancel = micRelease;
document.getElementById('btnMic').onpointerleave = () => { if (micWasPtt) micRelease(); };

document.getElementById('btnSpeaker').onclick = function () {
    speakerOn = !speakerOn;
    this.classList.toggle('active', speakerOn);
    this.classList.toggle('btn-outline-secondary', !speakerOn);
    this.classList.toggle('btn-secondary', speakerOn);
    const ic = this.querySelector('i');
    if (ic) ic.className = speakerOn ? 'fa-solid fa-volume-high text-cyan-400' : 'fa-solid fa-volume-xmark text-slate-500';
    if (!speakerOn) stopSpeak();
    setStatus(speakerOn ? 'Speaker nyala.' : 'Speaker mati.');
};

document.getElementById('btnVoiceMode').onclick = function () {
    if (!SR || !TTS) { setStatus('Browser tidak mendukung voice (pakai Chrome).'); return; }
    voiceMode = !voiceMode;
    this.classList.toggle('btn-success', voiceMode);
    this.classList.toggle('btn-outline-success', !voiceMode);
    if (voiceMode) {

        newChat();
        setStatus('Mode diskusi nyala — klik Mulai untuk mulai.');
    } else {
        stopSpeak();
        setContinuous(false);
        setStatus('Mode diskusi mati.');
    }
};

if (!SR) {
    document.getElementById('btnMic').disabled = true;
    document.getElementById('btnMic').title = 'SpeechRecognition tidak didukung browser ini';
}

if (!TTS) {
    document.getElementById('btnSpeaker').disabled = true;
}

loadModels();
loadSessions();

function dayPart() {
    const h = new Date().getHours();
    if (h >= 5 && h < 11) return 'Selamat pagi';
    if (h >= 11 && h < 15) return 'Selamat siang';
    if (h >= 15 && h < 18) return 'Selamat sore';
    return 'Selamat malam';
}
function buildGreeting() {
    return `${dayPart()}, saya Mira dari Loket Bengkel. Apakah abang punya QR Code dari aplikasi Kimo?`;
}

const PLATE_HINT_RE = /\d|\b(ribu|ratus|puluh|belas|nol|kosong|satu|dua|tiga|empat|lima|enam|tujuh|delapan|sembilan|sepuluh|sebelas|seratus|seribu)\b/i;
const PLATE_CORRECT_RE = /salah|bukan|keliru|ralat|ganti|ulang/i;
let masterVerifiedOk = false;
let awaitingPlate = true;

function isStrictPlate(text) {
    if (!sessionId || masterVerifiedOk || !awaitingPlate) return false;
    return !!extractStrictPlate(text);
}


const FILLER_TEKS = [
    'Saya akan cari datanya dulu ya'
];
let fillerIdx = 0;
function pickFiller() {
    const t = FILLER_TEKS[fillerIdx % FILLER_TEKS.length];
    fillerIdx++;
    return t;
}

function speakAwait(text) {
    return new Promise((resolve) => {
        const g = speechGen;
        const done = () => { clearInterval(timer); clearTimeout(kill); resolve(); };
        const timer = setInterval(() => {
            if (g !== speechGen) done();
        }, 200);
        const kill = setTimeout(done, 10000);
        try {
            speak(text, done);
        } catch { done(); }
    });
}

async function checkMasterNote(userText) {
    if (!sessionId || masterVerifiedOk || !awaitingPlate) return '';
    if (PLATE_CORRECT_RE.test(userText || '')) {
        viewDriverWin = null;
        pendingPlateWin = null;
        awaitingName = false;
        armVkb();
        noteSystem('Sopir menyatakan nopol salah — keyboard dibuka untuk ketik manual');
        return `\n[HASIL CEK MASTER: NOPOL DINYATAKAN SALAH oleh sopir — abaikan nopol/identitas sebelumnya, JANGAN pakai untuk WO. Ucapkan dengan kalimat: Bisa di ulangi nopolnya dengan cara di eja lagi bang? atau bisa ketik Virtual Keyboard bang. Jangan isi form dan jangan anggap master sudah terkonfirmasi.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
    }
    const strict = extractStrictPlate(userText);
    if (strict) {
        lastPlateText = strict.display;
        awaitingName = false;
        viewDriverWin = null;
        pendingPlateWin = null;
    }
    if (pendingPlateWin && CONFIRM_YES_RE.test(userText || '') && !CONFIRM_NO_RE.test(userText || '')) {
        const p = pendingPlateWin;
        pendingPlateWin = null;
        masterVerifiedOk = true;
        awaitingPlate = false;
        masterFailCount = 0;
        plateUnreadCount = 0;
        hideVKB();
        keluhanWatchArmed = true;
        setFormHead(p.nopol, '', p.truckType);
        return `\n[HASIL CEK MASTER: NOPOL DIKONFIRMASI SOPIR — nopol ${p.nopol} (jenis ${p.truckType || '-'}), sopir menjawab ya/benar. Nopol sudah tepat; JANGAN tanya apa pun lagi — tutup giliran dengan kalimat: Bisa dijelaskan dari truk apa yang mau di periksa atau di perbaiki bang?] (kode: ${p.candidate || ''}${p.sopirPart || ''})${p.marker || ''} (Hasil sistem otomatis, bukan ucapan sopir.)`;
    }
    let lookupText = strict ? strict.display : (userText || '');
    if (!strict && !PLATE_HINT_RE.test(userText || '')) {
        if (!awaitingName || !lastPlateText || NAME_FILLER_RE.test((userText || '').trim()) || !/[A-Za-z]{3,}/.test(userText || '')) return '';
        lookupText = lastPlateText;
    }
    try {
        const r = await fetch('/Chat/MasterLookup?nopol=' + encodeURIComponent(lookupText)
            + '&driver=' + encodeURIComponent(userText));
        const j = await r.json();
        if (!r.ok || !j) {
            noteSystem('Cek master gagal dari layanan: ' + ((j && j.error) || r.status));
            awaitingName = true;
            return '';
        }
        if (!j.candidate) {
            plateUnreadCount++;
            if (plateUnreadCount >= MASTER_FAIL_VKB_AT) armVkb();
            noteSystem('Nopol belum terbaca dari suara (percobaan ' + plateUnreadCount + ') — keyboard dibuka untuk ketik manual');
            return `\n[HASIL CEK MASTER: NOPOL BELUM TERBACA dari suara ini (percobaan ${plateUnreadCount}) — yang tertangkap bukan nopol lengkap, ini BUKAN berarti nopolnya salah. Minta sopir menyebut nopol LENGKAP dalam satu ucapan (contoh: B 9002 UXU). Bila tetap gagal, kabari sopir bahwa di layar ada keyboard untuk mengetik nopolnya sendiri. Jangan isi form dan jangan anggap master sudah terkonfirmasi.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
        }
    if (j.found && (j.pairMatched || j.driverMissing)) {
      const eja = ejaNopol(j.nopol);
      const jenisPart = j.truckType ? ` jenis truknya ${j.truckType}` : '';
      if (j.matchExact === false) {
        masterFailCount++;
        awaitingName = true;
        const heard = (j.nopolHeard || j.candidate || '-').toUpperCase();
        if (masterFailCount >= MASTER_FAIL_VKB_AT) armVkb();
        pendingPlateWin = { nopol: j.nopol, truckType: j.truckType || '', candidate: j.candidate || '', sopirPart: '', marker: '' };
        setFormHead(j.nopol, j.driverMissing ? '' : j.driverFullName, j.truckType);
        return `\n[HASIL CEK MASTER: TIDAK COCOK PERSIS (percobaan ${masterFailCount}). Yang didengar: ${heard}, sedangkan sistem menemukan nopol lain ${j.nopol} (jenis ${j.truckType || '-'}). JANGAN verifikasi dulu. Minta konfirmasi sopir dengan pola kaku: Oke ${eja}${jenisPart}. Benar ya bang? Bila sopir jawab ya/benar maka anggap nopol itu yang benar; bila sopir jawab salah/bukan maka minta eja ulang nopol lengkap atau ketik di Virtual Keyboard dengan kalimat: Bisa di ulangi nopolnya dengan cara di eja lagi bang? atau bisa ketik Virtual Keyboard bang.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
      }
      hideVKB();
      if (j.driverMissing) {
        knownDriverName = '';
        awaitingName = true;
        pendingPlateWin = { nopol: j.nopol, truckType: j.truckType || '', candidate: j.candidate || '', sopirPart: '', marker: '' };
        setFormHead(j.nopol, '', j.truckType);
      } else {
        pendingPlateWin = { nopol: j.nopol, truckType: j.truckType || '', candidate: j.candidate || '', sopirPart: ` | sopir: ${(j.driverFullName || '').toUpperCase()}`, marker: '' };
        setFormHead(j.nopol, j.driverFullName, j.truckType);
      }
      return `\n[HASIL CEK MASTER: NOPOL DITEMUKAN — ${j.nopol}${j.active ? '' : ' (NONAKTIF)'}, jenis ${j.truckType || '-'}. JANGAN verifikasi dulu. Minta konfirmasi sopir dengan pola kaku: Oke ${eja}${jenisPart}. Benar ya bang? Tunggu jawaban: bila sopir jawab iya/benar/betul maka anggap terverifikasi lalu tanyakan keluhan; bila sopir jawab salah/bukan maka minta eja ulang atau ketik keyboard.] (kode: ${j.candidate || ''}${j.driverMissing ? '' : ` | sopir: ${(j.driverFullName || '').toUpperCase()}`}) (Hasil sistem otomatis, bukan ucapan sopir.)`;
        }

        if (j.found && !j.pairMatched && !j.driverMissing) {
            const eja = ejaNopol(j.nopol);
            const jenisPart = j.truckType ? ` jenis truknya ${j.truckType}` : '';
            pendingPlateWin = { nopol: j.nopol, truckType: j.truckType || '', candidate: j.candidate || '', sopirPart: ' | sopir: -', marker: ' [NAMA_SOPIR]' };
            hideVKB();
            setFormHead(j.nopol, '', j.truckType);
            return `\n[HASIL CEK MASTER: NAMA DITERIMA APA ADANYA — nopol ${j.nopol} (jenis ${j.truckType || '-'}) tercatat atas nama ${j.driverFullName || '-'} di view, tapi sopir menyebut nama lain. PAKAI NAMA YANG DISEBUT SOPIR untuk WO (bukan nama di view). JANGAN verifikasi dulu. Minta konfirmasi sopir dengan pola kaku: Oke ${eja}${jenisPart}. Benar ya bang? Bila sopir jawab ya/benar maka anggap terverifikasi (pakai nama sopir); bila sopir jawab salah/bukan maka minta eja ulang atau ketik keyboard.] (kode: ${j.candidate || ''} | sopir: -) [NAMA_SOPIR] (Hasil sistem otomatis, bukan ucapan sopir.)`;
        }

        masterFailCount++;
        awaitingName = true;
        if (masterFailCount >= MASTER_FAIL_MAX) {
            return `\n[HASIL CEK MASTER: GAGAL TOTAL (percobaan ${masterFailCount}). Data nopol tetap tak cocok meski sudah tanya ulang, QR, dan ketik manual. Sampaikan dengan sopan agar sopir langsung ke loket WO manusia untuk dibantu petugas, lalu TUTUP dengan baris [SELESAI_LOKET_WO] sendirian di baris terakhir.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
        }
        if (masterFailCount >= MASTER_FAIL_VKB_AT) armVkb();
        if (masterFailCount >= MASTER_FAIL_QR_AT && !qrUsedForVerify) {
            showToast('Nopol belum ketemu (percobaan ' + masterFailCount + ') — ketik manual lewat keyboard atau arahkan QR KIMO ke kamera', 'ok');
            return `\n[HASIL CEK MASTER: TIDAK COCOK (percobaan ${masterFailCount}). Kemungkinan suara salah tangkap. Minta sopir menunjukkan QR KIMO ke kamera untuk memastikan (cukup sekali, katakan: "boleh tunjukkan QR KIMO-nya ke kamera?"); sistem otomatis mencatat saat terlihat dan menyisipkan hasil ceknya, lalu bandingkan dengan ucapan tadi. Bila sopir bilang tidak punya QR, kembali tanyakan ulang nopolnya saja — keyboard untuk mengetik juga sudah terbuka di layar.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
        }
        if (j.found)
            return `\n[HASIL CEK MASTER: NOPOL DITEMUKAN tapi driver bukan pasangannya (nopol ${j.nopol}, pasangan: ${j.driverFullName || '-'}). Katakan info tidak cocok, lalu tanyakan ulang nopolnya saja atau suruh sopir mengetik sendiri lewat keyboard yang sudah terbuka di layar.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
        return `\n[HASIL CEK MASTER: TIDAK DITEMUKAN — nopol ${j.nopol || j.candidate} tidak ada di bank data (percobaan ${masterFailCount}). Katakan info tidak ditemukan, lalu tanyakan ulang nopolnya saja atau suruh sopir mengetik sendiri lewat keyboard yang sudah terbuka di layar.] (Hasil sistem otomatis, bukan ucapan sopir.)`;
    } catch { return ''; }
}

let lastQrSent = null;
const MASTER_FAIL_VKB_AT = 1;
const MASTER_FAIL_QR_AT = 2;
const MASTER_FAIL_MAX = 4;
let masterFailCount = 0;
let plateUnreadCount = 0;
let qrUsedForVerify = false;
let camStream = null;
let autoGreeted = false;
let autoNoPersonSince = null;
let autoScanTimer = null;
let started = false;

function camBadge(text, cls) {
    const b = document.getElementById('camDot');
    if (!b) return;
    b.textContent = text;
    b.className = 'ai-badge badge ' + (cls || 'badge-secondary');
}

function stopCam() {
    if (camStream) {
        camStream.getTracks().forEach(t => t.stop());
        camStream = null;
    }
    const v = document.getElementById('camMini');
    if (v) v.srcObject = null;
    if (autoScanTimer) { clearInterval(autoScanTimer); autoScanTimer = null; }
    try {
        const viz = window.__visual;
        if (viz) { viz.face = false; viz.speaking = false; viz.lastSpeakAt = 0; viz.mar = 0; }
    } catch { }
}

function showStartScreen() {
    const s = document.getElementById('startScreen');
    if (s) s.style.display = 'flex';
    const v = document.getElementById('chatView');
    if (v) v.style.display = 'none';
}

function hideStartScreen() {
    const s = document.getElementById('startScreen');
    if (s) s.style.display = 'none';
    const v = document.getElementById('chatView');

    if (v) { v.style.display = 'flex'; v.style.flexDirection = 'column'; }
    applyChatView();
    applyTheme();
}

const SILENT_WAV = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAgD4AAIA+AAABAAgAZGF0YQAAAAA=';

function unlockAudio() {
    userGestured = true;
    try { const a = new Audio(SILENT_WAV); a.volume = 0; a.play().catch(() => { }); } catch { }
    if (TTS) { speechSynthesis.resume(); primeTts(); }
}

function resetSessionGate() {
    closing = false;
    chatBox().innerHTML = '';
    const prevEl = document.getElementById('chatPreview');
    if (prevEl) { prevEl.textContent = ''; prevEl.title = ''; }
    chatHiddenCount = 0;
    setChatBadge(0);
    applyChatView();
    typingBubbleEl = null;
    started = false;
    setContinuous(false);
    stopVad();
    stopSpeak();
    stopVersionWatch();
    setRecapLock(false);
    stopKeluhanWatch();
    lockedVoice = null;
    lockedCfg = null;
    autoGreeted = false;
    autoNoPersonSince = null;
    hideVKB();
    stopCam();
    camBadge('kamera mati', 'badge-secondary');
    const info = document.getElementById('detectInfo');
    if (info) info.textContent = 'Menunggu tombol Mulai...';
    showStartScreen();
}

let noiseFloor = 0;
let noisyMode = false;

function applyNoiseFloor(floor) {
    noiseFloor = Math.max(0, floor || 0);
    noisyMode = noiseFloor > 0.09;
    USER_SPEAK_LEVEL = Math.min(noisyMode ? 0.22 : 0.20, Math.max(0.06, noiseFloor + (noisyMode ? 0.05 : noiseFloor * 2)));
    BARGE_THRESHOLD = Math.min(0.30, Math.max(0.10, noiseFloor + (noisyMode ? 0.08 : noiseFloor * 3)));
    ECHO_EARLY_LVL = Math.min(0.35, Math.max(0.14, noiseFloor * 5));
}

try {
    const saved = JSON.parse(localStorage.getItem('loketNoise') || 'null');
    if (saved && typeof saved.floor === 'number' && Date.now() - saved.ts < 24 * 3600 * 1000)
        applyNoiseFloor(saved.floor);
} catch { }

async function calibrateNoise(sampleMs) {
    try {
        const ok = await ensureVadMic();
        if (!ok) return false;
        setStatus('Mengukur kebisingan...');
        const vals = [];
        const t0 = Date.now();
        const dur = sampleMs || 2500;
        while (Date.now() - t0 < dur) {
            vals.push(vadLevel());
            await sleepMs(100);
        }

        if (!vals.length) return false;
        vals.sort((a, b) => a - b);
        const med = vals[Math.floor(vals.length / 2)];
        applyNoiseFloor(med);
        try { localStorage.setItem('loketNoise', JSON.stringify({ floor: med, ts: Date.now() })); } catch { }
        return true;
    } catch { return false; }
}

let lastSpeechMed = 0;
async function calibrateVoice(sampleMs) {
    try {
        const ok = await ensureVadMic();
        if (!ok) return false;
        setStatus('Bicara NORMAL seperti ke sopir... (jangan teriak)');
        const vals = [];
        const t0 = Date.now();
        const dur = sampleMs || 2500;
        while (Date.now() - t0 < dur) {
            vals.push(vadLevel());
            await sleepMs(100);
        }
        if (!vals.length) return false;
        vals.sort((a, b) => a - b);
        const med = vals[Math.floor(vals.length / 2)];
        lastSpeechMed = med;
        if (med < 0.015) {
            setStatus('Suara tak terukur — mendekat ke laptop lalu ulangi.');
            return false;
        }
        const target = Math.max(0.18, USER_SPEAK_LEVEL * 3.5);
        sttGainVal = Math.round(Math.min(4, Math.max(0.5, target / med)) * 2) / 2;
        try { localStorage.setItem('loketGain', String(sttGainVal)); } catch { }
        if (sttGainNode) { try { sttGainNode.gain.value = sttGainVal; } catch { } }
        setStatus('Gain otomatis ' + sttGainVal.toFixed(1) + ' (suara ' + med.toFixed(3) + ' target ' + target.toFixed(3) + ').');
        return true;
    } catch { return false; }
}

function voicePrompt() {
    return VOICE_SYSTEM_PROMPT + (noisyMode
        ? ' Suasana loket BISING: bicara SANGAT singkat dan tegas, tanpa basa-basi, tanpa sapaan ulang, satu info penting per giliran.'
        : '');
}

let pttActive = false;
function updatePttUI(on) {
    const orb = document.getElementById('voiceOrb');
    if (orb) orb.classList.toggle('ptt', !!on);
    const label = document.getElementById('listenLabel');
    if (label) label.textContent = on ? 'Bicara... lepas untuk kirim' : label.textContent;
}

async function pttStart() {
    if (pttActive) return;
    if (recapLock) { setStatus('Rekap sedang dibacakan — tahan dulu sampai selesai.'); return; }
    const backendPtt = sttBackendActive();
    if (!backendPtt && !SR) { setStatus('Browser tidak mendukung SpeechRecognition (pakai Chrome).'); return; }
    pttActive = true;

    if (aiSpeaking) {
        setStatus('Disela sopir — mendengarkan...');
        aiSpeechGen++;
        aiSpeaking = false;
        stopNeural();
        try { if (TTS) speechSynthesis.cancel(); } catch { }
        openDuplex(true);
    }

    if (!continuousOn) setContinuous(true);
    else if (!listening && !backendPtt) startListening();
    if (backendPtt) {
        markUserSpeech();
        const micOk = await ensureVadMic();
        if (micOk && pttActive) startSttCapture();
        if (!micOk) {
            pttActive = false;
            updatePttUI(false);
            setStatus('Mic tidak tersedia — cek izin & perangkat mic, lalu tahan tombol lagi.');
            return;
        }
    }
    autoSendAfterMic = true;
    updatePttUI(true);
    setStatus('Bicara... lepas untuk kirim.');
}

function pttEnd() {
    if (!pttActive) return;
    pttActive = false;
    updatePttUI(false);
    if (sttBackendActive()) { finalizeSttCapture(true); return; }
    sendSoon();
}

function initCalibrateOverlay() {
    const d = document.createElement('div');
    d.id = 'calOverlay';
    d.innerHTML = '<b>KALIBRASI LOKET</b>'
        + '<div class="cal-row"><span>RMS live</span><b id="calLive">-</b></div>'
        + '<div class="cal-row"><span>Lantai noise</span><b id="calFloor">-</b></div>'
        + '<div class="cal-row"><span>USER_SPEAK</span><b id="calUser">-</b></div>'
        + '<div class="cal-row"><span>BARGE</span><b id="calBarge">-</b></div>'
        + '<div class="cal-row"><span>Mode bising</span><b id="calNoisy">-</b></div>'
        + '<div class="cal-row"><span>Wajah/mulut</span><b id="calFace">-</b></div>'
        + '<div class="cal-actions"><button id="calRecal" type="button">Kalibrasi ulang (2,5 dtk hening)</button>'
        + '<button id="calVoice" type="button">Kalibrasi suara (bicara normal)</button>'
        + '<button id="calClear" type="button">Hapus kalibrasi tersimpan</button>'
        + '<button id="calClose" type="button">Tutup</button></div>'
        + '<div class="cal-row"><span>AGC mic</span><button id="calAgc" type="button">MATI</button></div>'
        + '<div class="cal-row"><span>NS browser</span><button id="calNs" type="button">NYALA</button></div>'
        + '<div class="cal-row"><span>Gain rekam</span><span><input id="calGain" type="range" min="0.5" max="4" step="0.5" value="1"> <b id="calGainV">1.0</b></span></div>'
        + '<div class="cal-row"><span>Mic aktif</span><b id="calMic">-</b></div>'
        + '<div class="cal-row"><span>STT engine</span><b id="calEngine">-</b></div>'
        + '<div class="cal-row"><span>Rekaman terakhir</span><b id="calWav">-</b></div>'
        + '<div class="cal-row"><span>Suara (kalibrasi)</span><b id="calSpeech">-</b></div>'
        + '<div class="cal-row"><span>Pilih mic</span><span><select id="calMicSel"></select> <button id="calMicRe" type="button">Muat ulang</button></span></div>'
        + '<div class="cal-row"><span>Hush denoise</span><button id="calHush" type="button">NYALA</button></div>'
        + '<div class="cal-actions"><button id="calWavDl" type="button">Unduh rekaman terakhir</button></div>'
        + '<small>Bicara normal: RMS harus jauh di atas USER_SPEAK. Hening: RMS ≈ lantai.</small>';

    document.body.appendChild(d);
    const paint = () => {
        const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
        let lvl = 0;
        try { lvl = vadLevel(); } catch { }
        set('calLive', lvl.toFixed(3));
        set('calFloor', noiseFloor.toFixed(3));
        set('calUser', USER_SPEAK_LEVEL.toFixed(3));
        set('calBarge', BARGE_THRESHOLD.toFixed(3));
        set('calNoisy', noisyMode ? 'YA' : 'tidak');
        try {
            const tr = (vadStream && vadStream.getAudioTracks && vadStream.getAudioTracks()[0]) || null;
            set('calMic', tr ? (tr.label || 'tanpa label') : 'belum ada stream');
        } catch { set('calMic', '?'); }
        set('calEngine', lastSttEngine || '-');
        set('calWav', lastSttWavUrl ? (Number(lastSttSecs).toFixed(1) + ' dtk, siap unduh') : 'belum ada');
        set('calSpeech', lastSpeechMed ? lastSpeechMed.toFixed(3) : '-');

        try {
            const v = window.__visual;
            let faceTxt = 'modul belum load';
            if (v && v.mode === 'on') {
                if (!v.face) faceTxt = 'tak ada wajah';
                else faceTxt = 'ya / MAR ' + Number(v.mar || 0).toFixed(3) + (v.speaking ? ' BICARA' : ' diam');
      } else if (v) {
        faceTxt = 'off';
      }
      try {
        if (v && v.lastError) faceTxt += ' | ' + String(v.lastError).slice(0, 60);
      } catch { }
      set('calFace', faceTxt);
        } catch { set('calFace', '?'); }
        d.classList.toggle('cal-hot', lvl > USER_SPEAK_LEVEL);
    };

    setInterval(paint, 250);
    paint();

    document.getElementById('calRecal').onclick = async () => {
        setStatus('Mengukur kebisingan... (hening 2,5 dtk)');
        await calibrateNoise(2500);
    };

    document.getElementById('calClose').onclick = () => d.remove();
    document.getElementById('calClear').onclick = () => {
        try { localStorage.removeItem('loketNoise'); } catch { }
        applyNoiseFloor(0);
        setStatus('Kalibrasi tersimpan dihapus — lakukan kalibrasi ulang saat hening.');
    };
    const paintMic = () => {
        const a = document.getElementById('calAgc'), n = document.getElementById('calNs');
        if (a) a.textContent = micAgc ? 'NYALA' : 'MATI';
        if (n) n.textContent = micNsOff ? 'MATI' : 'NYALA';
        const h = document.getElementById('calHush');
        if (h) h.textContent = sttHushOff ? 'MATI' : 'NYALA';
        const g = document.getElementById('calGain'), gv = document.getElementById('calGainV');
        if (g) g.value = String(sttGainVal);
        if (gv) gv.textContent = Number(sttGainVal).toFixed(1);
    };

    document.getElementById('calAgc').onclick = async () => {
        micAgc = !micAgc;
        releaseVadMic();
        await ensureVadMic();
        paintMic();
        setStatus('AGC mic ' + (micAgc ? 'NYALA' : 'MATI') + ' — kalibrasi ulang disarankan.');
    };

    document.getElementById('calNs').onclick = async () => {
        micNsOff = !micNsOff;
        releaseVadMic();
        await ensureVadMic();
        paintMic();
        setStatus('NS browser ' + (micNsOff ? 'MATI' : 'NYALA') + ' — kalibrasi ulang disarankan.');
    };

    document.getElementById('calGain').oninput = (e) => {
        sttGainVal = Math.min(4, Math.max(0.5, parseFloat(e.target.value) || 1));
        try { localStorage.setItem('loketGain', String(sttGainVal)); } catch { }
        if (sttGainNode) { try { sttGainNode.gain.value = sttGainVal; } catch { } }
        paintMic();
    };

    document.getElementById('calVoice').onclick = async () => {
        await calibrateVoice(2500);
        paintMic();
    };
    const refreshMicList = async () => {
        const sel = document.getElementById('calMicSel');
        if (!sel) return;
        let devs = [];
        try { devs = await navigator.mediaDevices.enumerateDevices(); } catch { devs = []; }
        sel.innerHTML = '';
        const def = document.createElement('option');
        def.value = '';
        def.textContent = 'Default browser';
        sel.appendChild(def);
        devs.filter(x => x && x.kind === 'audioinput').forEach((x, i) => {
            const o = document.createElement('option');
            o.value = x.deviceId || '';
            o.textContent = x.label || ('Mic ' + (i + 1));
            sel.appendChild(o);
        });
        sel.value = micDeviceId || '';
    };
    document.getElementById('calMicSel').onchange = async (e) => {
        micDeviceId = (e.target.value || '');
        try { localStorage.setItem('loketMic', micDeviceId); } catch { }
        releaseVadMic();
        const ok = await ensureVadMic();
        if (!ok && micDeviceId) {
            micDeviceId = '';
            try { localStorage.setItem('loketMic', ''); } catch { }
            await ensureVadMic();
            setStatus('Mic pilihan tak tersedia — kembali ke default.');
        } else setStatus('Mic diganti — kalibrasi ulang disarankan.');
        refreshMicList();
    };
    document.getElementById('calMicRe').onclick = () => { refreshMicList(); };
    refreshMicList();

    document.getElementById('calHush').onclick = () => {
        sttHushOff = !sttHushOff;
        try { localStorage.setItem('loketHushOff', sttHushOff ? '1' : '0'); } catch { }
        paintMic();
        setStatus('Hush denoise ' + (sttHushOff ? 'MATI — bandingkan hasil ucapan yang sama.' : 'NYALA.'));
    };

    document.getElementById('calWavDl').onclick = () => {
        if (!lastSttWavUrl) { setStatus('Belum ada rekaman — tahan tombol mic dulu.'); return; }
        const a = document.createElement('a');
        a.href = lastSttWavUrl;
        a.download = 'stt-terakhir.wav';
        document.body.appendChild(a);
        a.click();
        a.remove();
    };

    paintMic();
}
if (typeof location !== 'undefined' && /[?&]calibrate\b/.test(location.search || '')) {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initCalibrateOverlay);
    else initCalibrateOverlay();
}

document.addEventListener('keydown', (e) => {
    if (e.code !== 'Space' || e.repeat) return;
    const tag = (document.activeElement && document.activeElement.tagName) || '';
    if (tag === 'INPUT' || tag === 'TEXTAREA') return;
    if (!started) return;
    e.preventDefault();
    pttStart();
});
document.addEventListener('keyup', (e) => {
    if (e.code !== 'Space') return;
    pttEnd();
});
document.addEventListener('pointerdown', () => {
    try {
        if (typeof vadCtx !== 'undefined' && vadCtx && vadCtx.state === 'suspended') vadCtx.resume().catch(() => { });
        if (typeof sttRecCtx !== 'undefined' && sttRecCtx && sttRecCtx.state === 'suspended') sttRecCtx.resume().catch(() => { });
    } catch { }
});

let sttMode = 'browser';
const sttBackendActive = () => sttMode === 'backend';
let sttRecCtx = null, sttRecSrc = null, sttRecProc = null;
let sttChunks = [];
let sttRecording = false;
let sttDeviceRate = 48000;

async function refreshSttMode() {
    try {
        const r = await fetch('/Chat/SttStatus');
        const j = await r.json();
        sttMode = (r.ok && j.provider === 'Backend') ? 'backend' : 'browser';
    } catch { sttMode = 'browser'; }
    return sttMode;
}

function ensureSttCapture() {
    if (sttRecProc) return true;
    if (!vadStream) return false;
    try {
        sttRecCtx = sttRecCtx || new (window.AudioContext || window.webkitAudioContext)();
        if (sttRecCtx.state === 'suspended') sttRecCtx.resume().catch(() => { });
        sttDeviceRate = sttRecCtx.sampleRate || 48000;
        sttRecSrc = sttRecCtx.createMediaStreamSource(vadStream);
        sttRecProc = sttRecCtx.createScriptProcessor(4096, 1, 1);
        sttRecProc.onaudioprocess = (e) => {
            if (!sttRecording) return;
            sttChunks.push(new Float32Array(e.inputBuffer.getChannelData(0)));
        };

        const mute = sttRecCtx.createGain();
        mute.gain.value = 0;
        sttGainNode = sttRecCtx.createGain();
        sttGainNode.gain.value = sttGainVal;
        sttRecSrc.connect(sttGainNode);
        sttGainNode.connect(sttRecProc);
        sttRecProc.connect(mute);
        mute.connect(sttRecCtx.destination);
        return true;
    } catch { return false; }
}

function startSttCapture() {
    if (sttRecording) return;
    if (!ensureSttCapture()) return;
    sttChunks = [];
    sttRecording = true;
    endpointArmed = true;
}

function downsampleTo16k(chunks, fromRate) {
    let total = 0;
    for (const c of chunks) total += c.length;
    const flat = new Float32Array(total);
    let o = 0;
    for (const c of chunks) { flat.set(c, o); o += c.length; }
    if (fromRate === 16000) return flat;
    const ratio = fromRate / 16000;
    const len = Math.floor(flat.length / ratio);
    const out = new Float32Array(len);
    for (let i = 0; i < len; i++) {
        const pos = i * ratio;
        const i0 = Math.floor(pos), frac = pos - i0;
        const a = flat[i0], b = flat[Math.min(i0 + 1, flat.length - 1)];
        out[i] = a + (b - a) * frac;
    }
    return out;
}

function encodeWav16k(samples) {
    const buf = new ArrayBuffer(44 + samples.length * 2);
    const v = new DataView(buf);
    const wstr = (at, s) => { for (let i = 0; i < s.length; i++) v.setUint8(at + i, s.charCodeAt(i)); };
    wstr(0, 'RIFF'); v.setUint32(4, 36 + samples.length * 2, true); wstr(8, 'WAVE');
    wstr(12, 'fmt '); v.setUint32(16, 16, true); v.setUint16(20, 1, true); v.setUint16(22, 1, true);
    v.setUint32(24, 16000, true); v.setUint32(28, 16000 * 2, true); v.setUint16(32, 2, true); v.setUint16(34, 16, true);
    wstr(36, 'data'); v.setUint32(40, samples.length * 2, true);
    for (let i = 0; i < samples.length; i++)
        v.setInt16(44 + i * 2, Math.round(Math.max(-1, Math.min(1, samples[i])) * 32767), true);
    return new Blob([buf], { type: 'audio/wav' });
}

let lastSttWavUrl = '';
let lastSttEngine = '-';
let lastSttSecs = 0;
let sttHushOff = false;
try { sttHushOff = localStorage.getItem('loketHushOff') === '1'; } catch { }
function isSttHallucination(t) {
    const s = (t || '').trim();
    if (!s) return true;
    if (/^\[[^\]]*\]$/.test(s)) return true;
    if (s.replace(/[^A-Za-z0-9]/g, '').length < 2) return true;
    return false;
}
function rememberSttDebug(blob, engine, secs) {
    try { if (lastSttWavUrl) URL.revokeObjectURL(lastSttWavUrl); } catch { }
    lastSttWavUrl = '';
    try { lastSttWavUrl = URL.createObjectURL(blob); } catch { }
    lastSttEngine = engine || '-';
    lastSttSecs = secs || 0;
}

async function finalizeSttCapture(force) {
    sttRecording = false;
    if (!sttChunks.length) {
        if (force) setStatus('Suara terlalu pelan/tidak masuk — dekatkan ke laptop atau naikkan gain, lalu tahan tombol mic lagi.');
        return;
    }

    if (!force && !visMouthRecent(2500)) { sttChunks = []; noteGateDrop(); return; }
    const pcm = downsampleTo16k(sttChunks, sttDeviceRate);
    sttChunks = [];
    if (!force && pcm.length < 16000 * 0.5) return;
    setStatus('Mengenali suara...');
    try {
        const fd = new FormData();
        const wav = encodeWav16k(pcm);
        fd.append('audio', wav, 'utterance.wav');
        fd.append('denoise', sttHushOff ? '0' : '1');
        const r = await fetch('/Chat/Stt', { method: 'POST', body: fd });
        const j = await r.json();
        if (!r.ok) throw new Error((j && j.error) || 'stt gagal');
        rememberSttDebug(wav, (j && j.engine) || '-', pcm.length / 16000);
        const text = ((j && j.text) || '').trim();
        const heard = text && !isSttHallucination(text);
        if (heard) { noteGatePass(); lastSpeechAt = Date.now(); }
        if (heard && autoSendAfterMic) sendText(text);
        else setStatus('Tak terdengar jelas — coba lagi.');
    } catch (e) { setStatus('STT gagal: ' + e.message); }
}

function trackVadBackend(lvl) {
    if (recapLock) {
        userSpeaking = false;
        silenceSinceMs = 0;
        return;
    }
    if (Date.now() < echoMuteUntil) {
        maybeEarlyOpenTail(lvl);
        if (Date.now() < echoMuteUntil) return;
    }

    if (pttActive) {
        if (!userSpeaking) { userSpeaking = true; startSttCapture(); }
        silenceSinceMs = 0;
        endpointArmed = true;
        return;
    }
    if (lvl > USER_SPEAK_LEVEL && visGatePass()) {
        if (!userSpeaking) { userSpeaking = true; startSttCapture(); }
        silenceSinceMs = 0;
        return;
    }

    if (!userSpeaking) return;
    if (!silenceSinceMs) silenceSinceMs = Date.now();
    if (Date.now() - silenceSinceMs < VAD_SILENCE_SEND_MS) return;
    userSpeaking = false;
    silenceSinceMs = 0;
    if (endpointArmed) finalizeSttCapture(false);
}

let gateDropStreak = 0;
let gateDropLastToast = 0;
let mouthGateOn = false;
function noteGateDrop() {
  gateDropStreak++;
  if (gateDropStreak < 3) return;
  const now = Date.now();
  if (now - gateDropLastToast < 15000) return;
  gateDropLastToast = now;
  gateDropStreak = 0;
  let msg = 'Suara terdengar tapi wajah tak terdeteksi — mendekat ke kamera, atau tahan tombol mic.';
  try {
    const v = window.__visual;
    if (!v) msg = 'Modul visual belum termuat — muat ulang halaman, atau tahan tombol mic untuk bicara.';
    else if (v.mode !== 'on') msg = 'Deteksi wajah mati (' + (v.lastError || 'init gagal') + ') — muat ulang halaman, atau tahan tombol mic.';
  } catch { }
  showToast(msg, 'ok');
  setStatus(msg);
}
function noteGatePass() { gateDropStreak = 0; }

function visMouthRecent(windowMs) {
    if (!mouthGateOn) return true;
    if (pttActive) return true;
    try {
        const v = window.__visual;
        if (!v || v.mode !== 'on' || !v.face) return false;
        return (Date.now() - (v.lastSpeakAt || 0)) < (windowMs || 800);
    } catch { return false; }
}

function visState() {
    try {
        const v = window.__visual;
        if (!v || v.mode !== 'on') return null;
        return v;
    } catch { return null; }
}

function visGatePass() {
    if (!mouthGateOn) return true;
    const v = visState();
    if (!v) return false;
    if (!v.face) return false;
    return !!v.speaking;
}

async function beginSession() {
    if (started) return;
    started = true;
    startVersionWatch();
    lockedVoice = null;
    lockedCfg = null;
    hideStartScreen();
    const banner = document.getElementById('permBanner');
    if (banner) banner.style.display = 'none';
    unlockAudio();

    try { if (window.__visualInit) window.__visualInit(); } catch { }

    await refreshSttMode();
    micNsOff = sttBackendActive();
    if (sttBackendActive()) setStatus('STT backend aktif (Hush+Whisper).');
    await calibrateNoise(2500);
    if (noisyMode) showToast('Suasana bising terdeteksi — tahan tombol mic saat bicara', 'ok');
    prefetchTts(buildGreeting());

    if (SR) setContinuous(true);
    setStatus('Mengaktifkan kamera...');
    const info = document.getElementById('detectInfo');
    if (info) info.textContent = 'Mencari manusia di depan kamera...';
    await autoStartCamera();
    if (started && camStream) setStatus('Mic aktif — bicara saja, auto-kirim.');
    if (started && !autoGreeted) await autoGreet();
}

function camErrorText(e) {
    const n = (e && e.name) || '';
    if (n === 'NotAllowedError' || n === 'SecurityError')
        return 'Izin kamera ditolak. Klik ikon kamera di address bar Chrome lalu izinkan.';
    if (n === 'NotReadableError' || n === 'TrackStartError' || n === 'AbortError')
        return 'Kamera dipakai tab lain. Matikan kamera di tab sebelah (Vision), lalu klik Ulangi.';
    if (n === 'NotFoundError' || n === 'OverconstrainedError')
        return 'Tidak ada kamera yang ditemukan di perangkat ini.';

    return 'Kamera error: ' + (e.message || n);
}

async function autoStartCamera() {
    const v = document.getElementById('camMini');
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        camBadge('no kamera', 'badge-danger');
        document.getElementById('detectInfo').textContent = 'Browser tidak mendukung kamera.';
        return;
    }
    stopCam();
    try {
        camStream = await navigator.mediaDevices.getUserMedia({ video: { width: 640 }, audio: false });
        v.srcObject = camStream;
        await v.play();
        camBadge('kamera nyala', 'badge-success');
        document.getElementById('permBanner').style.display = 'none';
        autoScanTimer = setInterval(autoScan, 3000);
        autoScan();
    } catch (e) {
        camBadge('kamera mati', 'badge-danger');
        document.getElementById('detectInfo').textContent = camErrorText(e);

        const n = (e && e.name) || '';
        if (n === 'NotAllowedError' || n === 'SecurityError') {
            document.getElementById('permBanner').style.display = 'block';
        }
    }
}


document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        if (camStream) { stopCam(); camBadge('jeda', 'badge-secondary'); }
    } else if (started && !camStream) {
        autoStartCamera();
    }
});

async function autoScan() {
    const v = document.getElementById('camMini');
    if (!camStream || !v.videoWidth) return;
    const c = document.createElement('canvas');
    c.width = 480;
    c.height = Math.round(480 * v.videoHeight / v.videoWidth) || 360;
    c.getContext('2d').drawImage(v, 0, 0, c.width, c.height);
    const blob = await new Promise(res => c.toBlob(res, 'image/jpeg', 0.85));
    try {
        const fd = new FormData();
        fd.append('image', blob, 'scan.jpg');
        const r = await fetch('/Vision/Detect?qr=true', { method: 'POST', body: fd });
        const j = await r.json();
        if (!r.ok) return;
        handleQr(j.qrCodes || []);
        const n = (j.detections || []).filter(d => d.label === 'person').length;
        const info = document.getElementById('detectInfo');
        if (n > 0) {
            autoNoPersonSince = null;
            camBadge(n + ' orang', 'badge-success');
            info.textContent = started ? `Terdeteksi ${n} manusia di depan kamera.` : `Terdeteksi ${n} manusia — tekan Mulai untuk memulai.`;
        } else {
            camBadge('mencari...', 'badge-warning');
            info.textContent = 'Belum ada manusia. Tampilkan wajahmu ke kamera...';
            if (!autoNoPersonSince) autoNoPersonSince = Date.now();
            if (autoGreeted && Date.now() - autoNoPersonSince > 15000) {
                autoGreeted = false;
            }
        }
    } catch { }
}

async function autoGreet() {
    autoGreeted = true;
    awaitingPlate = true;
    viewDriverWin = null;
    pendingPlateWin = null;
    awaitingName = false;
    lastPlateText = '';

    speakerOn = true;
    voiceMode = true;
    syncVoiceButtons();
    setContinuous(true);
    setStatus('Manusia terdeteksi — Mira menyapa...');
    try {
        const gr = await fetch('/Chat/Greet', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                model: document.getElementById('modelSelect').value || null,
                systemPrompt: voicePrompt(),
                greeting: buildGreeting()
            })
        });
        const gj = await gr.json();
        if (!gr.ok) throw new Error(gj.error || 'greet gagal');
        sessionId = gj.sessionId;
        chatBox().innerHTML = '';
        const prevG = document.getElementById('chatPreview');
        if (prevG) { prevG.textContent = ''; prevG.title = ''; }
        chatHiddenCount = 0;
        setChatBadge(0);
        applyChatView();
        applyTheme();
        hideVKB();
        clearFormHead();
        bubble('assistant', gj.response);
        loadSessions();
        setStatus('Mira bicara...');
        speak(gj.response, () => {
            setStatus('Silakan bicara...');
            startListeningAfterSpeech();
        });
    } catch (e) {
        autoGreeted = false;
        setStatus('Gagal menyapa: ' + e.message);
    }
}

function syncVoiceButtons() {
    const sp = document.getElementById('btnSpeaker');
    sp.classList.toggle('active', speakerOn);
    const vm = document.getElementById('btnVoiceMode');
    vm.classList.toggle('btn-success', voiceMode);
    vm.classList.toggle('btn-outline-success', !voiceMode);
}


async function handleQr(qrCodes) {
    const badge = document.getElementById('qrBadge');
    applyChatView();
    if (!qrCodes.length) return;
    const raw = (qrCodes[0].text || '').trim();
    const text = raw.replace(/[\0-\x1F\x7F\u200B-\u200D\uFEFF]/g, '').trim();
    if (!text) return;
    badge.textContent = text.length > 40 ? text.slice(0, 40) + '...' : text;
    if (!sessionId || text === lastQrSent) return;
    lastQrSent = text;
    hideVKB();

    if (awaitingPlate) {
        qrUsedForVerify = true;
        masterFailCount = 0;
        plateUnreadCount = 0;
        knownDriverName = '';
    }
    let qr = null;
    try {
        const r = await fetch('/Chat/QrLookup?srd=' + encodeURIComponent(text));
        qr = await r.json();
        if (!r.ok) qr = null;
    } catch { qr = null; }
    let msg;
    if (qr && qr.checked && qr.found && qr.usable) {
        let dup = false;
        let dupWos = [];
        try {
            const rc = await fetch('/Chat/KimoExists?srd=' + encodeURIComponent(text));
            const jc = await rc.json();
            dup = !!(rc.ok && jc && jc.exists);
            if (dup && Array.isArray(jc.wos)) dupWos = jc.wos;
        } catch { dup = false; }
        if (dup) { onWoDuplicate(text, dupWos); return; }
        if (masterVerifiedOk) {
            setFormHead(qr.nopol, qr.driverName, qr.truckType);
            msg = `[QR KIMO: ${text}] (Terdaftar di KIMO: driver ${qr.driverName} (${qr.driverCode}), nopol ${qr.nopol}, jenis ${qr.truckType}, status ${qr.status}. Bandingkan dengan data interview terverifikasi: BILA SAMA, sebutkan singkat lalu lanjut (data KIMO jadi acuan; nama driver WAJIB disebut persis "${qr.driverName}"). BILA BEDA, tanyakan mana yang benar; bila sopir membenarkan data KIMO, sebut nama persis "${qr.driverName}" dan akhiri giliran dengan [QR_MENANG]; bila bersikeras jawabannya, akhiri dengan [INTERVIEW_MENANG] dan pakai jawaban sopir.)`;
        } else {
            viewDriverWin = null;
            pendingPlateWin = { nopol: qr.nopol, truckType: qr.truckType || '', candidate: '', sopirPart: '', marker: '' };
                    awaitingName = false;
            lastPlateText = '';
            masterFailCount = 0;
            plateUnreadCount = 0;
            knownDriverName = '';
            hideVKB();
            setFormHead(qr.nopol, qr.driverName, qr.truckType);
            msg = `[QR KIMO: ${text}] (Terdaftar di KIMO status ${qr.status}: driver ${qr.driverName} (${qr.driverCode}), nopol ${qr.nopol}, jenis ${qr.truckType}. JADIKAN ACUAN: LEWATI tanya nama, kode driver, nopol, dan jenis truk. Nama driver WAJIB disebut persis "${qr.driverName}" (dari KIMO) setiap menyebut sopir, termasuk saat rangkuman; jangan pakai ejaan, singkatan, atau varian dari ucapan sopir. JANGAN verifikasi dulu. Minta konfirmasi sopir dengan pola kaku: Oke ${ejaNopol(qr.nopol)}${qr.truckType ? ` jenis truknya ${qr.truckType}` : ''}. Benar ya bang? Tunggu jawaban; bila sopir jawab iya/benar/betul maka anggap terverifikasi lalu tanyakan: Bisa dijelaskan dari truk apa yang mau di periksa atau di perbaiki bang? Lalu tanyakan lokasi parkir, rangkum semua, dan tutup [WO_SIAP].)`;
        }
    } else if (qr && qr.checked && qr.found && !qr.usable) {
        msg = `[QR KIMO: ${text}] (Pengajuan KIMO status ${qr.status || 'selesai/dibatalkan'}: SUDAH TIDAK BERLAKU. Konfirmasi singkat bahwa pengajuannya sudah ${qr.status}, suruh sopir langsung ke petugas loket manusia untuk dibantu, lalu TUTUP dengan baris [SELESAI_ADMIN_WO] sendirian di baris terakhir.)`;
    } else if (qr && qr.checked && !qr.found) {
        msg = `[QR KIMO: ${text}] (Tidak terdaftar di KIMO. Sampaikan singkat (belum terdaftar), anggap sopir tanpa QR: tanyakan nopol (minta eja satu per satu), lalu lanjutkan alur normal sampai simpan WO. JANGAN tanyakan nama/kode driver.)`;
    } else {
        msg = `[QR KIMO: ${text}] (Belum bisa dicek ke sistem. Anggap sopir tanpa QR: tanyakan nopol (minta eja satu per satu), lalu lanjutkan alur normal sampai simpan WO. JANGAN tanyakan nama/kode driver.)`;
    }
    try {
        const r = await fetch('/Chat/Send', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId,
                model: document.getElementById('modelSelect').value || null,
                message: msg
            })
        });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'gagal');
        afterAssistant(j.response);
    } catch (e) { setStatus('QR tercatat, AI gagal merespons: ' + e.message); }
}

document.getElementById('btnKeyboard').onclick = () => {
    const p = document.getElementById('vkb');
    if (!p || p.style.display === 'none' || !p.style.display) showVKB();
    else hideVKB();
};

const btnChatToggle = document.getElementById('btnChatToggle');
if (btnChatToggle) btnChatToggle.onclick = toggleChatPanel;
const btnTheme = document.getElementById('btnTheme');
if (btnTheme) btnTheme.onclick = toggleTheme;
restoreChatPanel();
applyTheme();

const vkbCloseBtn = document.getElementById('vkbClose');
if (vkbCloseBtn) vkbCloseBtn.onclick = hideVKB;

let typingBubbleEl = null;

function clearTypingBubble() {
    if (typingBubbleEl && typingBubbleEl.parentNode) typingBubbleEl.parentNode.removeChild(typingBubbleEl);
    typingBubbleEl = null;
}

function syncTypingBubble() {
    const i = document.getElementById('inputMsg');
    if (!i) return;
    const val = (i.value || '').replace(/\s+$/, '');
    if (chatCollapsed && val) setChatPreview(val);
    if (!val) { clearTypingBubble(); return; }
    if (!typingBubbleEl || !typingBubbleEl.isConnected) {
        const cb = chatBox();
        if (!cb) return;
        typingBubbleEl = document.createElement('div');
        typingBubbleEl.className = 'flex gap-3 w-full justify-start flex-row-reverse typing-bubble';
        typingBubbleEl.innerHTML = `<div class="shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center"><i class="fa-solid fa-user text-white text-xs"></i></div>`
            + `<div class="max-w-[85%] min-w-0 bg-slate-900/80 border border-cyan-500/30 rounded-2xl rounded-tr-none px-3.5 py-3 text-sm text-slate-200 leading-relaxed">`
            + `<p class="font-semibold text-cyan-300 text-xs mb-1">Anda <i class="fa-solid fa-pen text-[10px] text-slate-500"></i></p>`
            + `<div class="direct-chat-text" style="white-space:pre-wrap"></div></div>`;
        cb.appendChild(typingBubbleEl);
    }
    const target = typingBubbleEl.querySelector('.direct-chat-text');
    if (target) target.textContent = val;
    followChatBottom();
}

function buildVKB() {
    const box = document.getElementById('vkbKeys');
    if (!box || box.dataset.built) return;
    box.dataset.built = '1';
    const inp = () => document.getElementById('inputMsg');
    const addKey = (label, cls, fn) => {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'glass-button rounded-lg py-2.5 text-sm font-bold text-slate-100 active:bg-purple-600/40 transition-all ' + (cls || '');
        b.innerHTML = label;
        b.addEventListener('pointerdown', (e) => { e.preventDefault(); });
        b.addEventListener('pointerup', (e) => {
            if (vkbDragSuppress) return;
            e.preventDefault();
            fn();
        });
        box.appendChild(b);
    };

    const type = (ch) => {
        const i = inp(); if (!i) return;
        const s = i.selectionStart == null ? i.value.length : i.selectionStart;
        const e = i.selectionEnd == null ? s : i.selectionEnd;
        i.setRangeText(ch, s, e, 'end');
        formatPlateInput(i);
        syncTypingBubble();
    };
    const backspace = () => {
        const i = inp(); if (!i) return;
        const s = i.selectionStart == null ? i.value.length : i.selectionStart;
        const e = i.selectionEnd == null ? s : i.selectionEnd;
        if (s === 0 && e === 0) return;
        if (s === e) i.setRangeText('', s - 1, s, 'end');
        else i.setRangeText('', s, e, 'end');
        formatPlateInput(i);
        syncTypingBubble();
    };
    const moveCaret = (delta) => {
        const i = inp(); if (!i) return;
        const pos = Math.max(0, Math.min(i.value.length, (i.selectionStart == null ? i.value.length : i.selectionStart) + delta));
        try { i.setSelectionRange(pos, pos); } catch { }
    };
    const clearAll = () => {
        const i = inp(); if (!i) return;
        i.value = '';
        syncTypingBubble();
    };
    const submit = () => {
        masterFailCount = 0;
        plateUnreadCount = 0;
        clearTypingBubble();
        send();
    };

    [...'1234567890'].forEach((ch) => addKey(ch, '', () => type(ch)));
    [...'QWERTYUIOP'].forEach((ch) => addKey(ch, '', () => type(ch)));
    [...'ASDFGHJKL'].forEach((ch) => addKey(ch, '', () => type(ch)));
    addKey('<i class="fas fa-delete-left"></i>', '', backspace);
    [...'ZXCVBNM'].forEach((ch) => addKey(ch, '', () => type(ch)));
    addKey('Spasi', 'col-span-3 text-xs', () => type(' '));
    addKey('<i class="fas fa-caret-left"></i>', '', () => moveCaret(-1));
    addKey('<i class="fas fa-caret-right"></i>', '', () => moveCaret(1));
    addKey('Hapus', 'col-span-2 text-xs', clearAll);
    addKey('Spasi', 'col-span-6 text-xs', () => type(' '));
    addKey('<i class="fa-solid fa-paper-plane"></i> KIRIM', 'col-span-10 bg-gradient-to-r from-purple-600 to-indigo-600', submit);
}

const VKB_DRAG_THRESHOLD = 4;
let vkbDragSuppress = false;
let vkbPos = null;

function enableVkbDrag() {
    const p = document.getElementById('vkb');
    if (!p || p.dataset.drag) return;
    p.dataset.drag = '1';
    let sx = 0, sy = 0, dx = 0, dy = 0, on = false;
    const stop = (e) => {
        if (!on) { vkbDragSuppress = false; return; }
        on = false;
        p.classList.remove('vkb-drag');
        vkbDragSuppress = false;
        try { p.releasePointerCapture(e.pointerId); } catch { }
    };
    p.addEventListener('pointerdown', (e) => {
        if (e.button != null && e.button !== 0) return;
        if (e.target.closest('#vkbClose')) return;
        sx = e.clientX;
        sy = e.clientY;
        on = false;
        e.preventDefault();
    });
    p.addEventListener('pointermove', (e) => {
        if (e.pointerType === 'mouse' && e.buttons === 0) return;
        if (!on) {
            if (Math.abs(e.clientX - sx) < VKB_DRAG_THRESHOLD && Math.abs(e.clientY - sy) < VKB_DRAG_THRESHOLD) return;
            const r = p.getBoundingClientRect();
            p.style.left = r.left + 'px';
            p.style.top = r.top + 'px';
            p.style.right = 'auto';
            p.style.bottom = 'auto';
            p.style.transform = 'none';
            dx = e.clientX - r.left;
            dy = e.clientY - r.top;
            on = true;
            vkbDragSuppress = true;
            p.classList.add('vkb-drag');
            try { p.setPointerCapture(e.pointerId); } catch { }
        }
        const w = p.offsetWidth, h = p.offsetHeight;
        const nx = Math.max(0, Math.min(window.innerWidth - w, e.clientX - dx));
        const ny = Math.max(0, Math.min(window.innerHeight - h, e.clientY - dy));
        p.style.left = nx + 'px';
        p.style.top = ny + 'px';
        vkbPos = { left: nx, top: ny };
    });
    p.addEventListener('pointerup', stop);
    p.addEventListener('pointercancel', stop);
    const head = document.getElementById('vkbHead');
    if (head) head.addEventListener('dblclick', () => {
        p.style.left = '';
        p.style.top = '';
        p.style.right = '';
        p.style.bottom = '';
        p.style.transform = '';
        vkbPos = null;
    });
}

function showVKB() {
    buildVKB();
    enableVkbDrag();
    const p = document.getElementById('vkb');
    if (p) {
        const wasHidden = p.style.display !== 'block';
        if (wasHidden) {
            p.style.left = '';
            p.style.top = '';
            p.style.right = '';
            p.style.bottom = '';
            p.style.transform = '';
            p.style.display = 'block';
            if (vkbPos) {
                const w = p.offsetWidth, h = p.offsetHeight;
                p.style.left = Math.max(0, Math.min(window.innerWidth - w, vkbPos.left)) + 'px';
                p.style.top = Math.max(0, Math.min(window.innerHeight - h, vkbPos.top)) + 'px';
            }
        }
    }
    document.documentElement.classList.add('vkb-open');
}

function hideVKB() {
    const p = document.getElementById('vkb');
    if (p) p.style.display = 'none';
    document.documentElement.classList.remove('vkb-open');
    clearTypingBubble();
}

document.getElementById('btnMakeWo').onclick = async () => {
    if (!sessionId) { setStatus('Belum ada percakapan. Tunggu sopir terdeteksi dulu.'); return; }
if (woSavedFor === sessionId || woSaveState === 'saving') return;
    woSaveState = 'saving';
    setStatus('Membuat WO dari percakapan...');
    showWoSaving('Menyimpan ke sistem...');
    try {
        const w = await doCreateWo();
        woSavedFor = sessionId;
        closeWoSaving();
        onWoCreated(w);
    } catch (e) {
        closeWoSaving();
        onWoFailed(e.message);
    } finally {
        woSaveState = '';
    }
};

document.addEventListener('click', () => {
    userGestured = true;
    if (TTS) { speechSynthesis.resume(); primeTts(); }
});

let primed = false;
function primeTts() {
    if (!TTS || primed) return;
    primed = true;
    try {
        const u = new SpeechSynthesisUtterance('Aktif.');
        u.lang = 'id-ID';
        u.volume = 0;
        speechSynthesis.speak(u);
    } catch { }
}
document.getElementById('btnCamRetry').onclick = () => {
    if (!started) { setStatus('Klik Mulai dulu untuk menyalakan kamera.'); showStartScreen(); return; }
    setStatus('Mencoba menyalakan kamera...');
    autoStartCamera();
};


document.getElementById('btnStart').onclick = () => { beginSession(); };


document.getElementById('btnEnableAll').onclick = async () => {
    document.getElementById('permBanner').style.display = 'none';
    if (!started) { await beginSession(); return; }
    unlockAudio();
    setStatus('Meminta izin kamera...');
    await autoStartCamera();
};

prefetchTts(buildGreeting());
document.getElementById('voiceSelect').addEventListener('change', () => prefetchTts(buildGreeting()));
document.getElementById('rateSelect').addEventListener('change', () => prefetchTts(buildGreeting()));

resetSessionGate();
