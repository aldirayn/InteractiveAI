# InteractiveAI — C# + Ollama Cloud (ASP.NET WebAPI + MVC AdminLTE)

Backend WebAPI ke Ollama Cloud (native `/api/*` + Bearer key) + MySQL/SQLite history. Frontend MVC AdminLTE.

## Database (live MySQL 5.7 lokal)
`Database:Provider = Mysql` → `ConnectionStrings:Mysql` (`127.0.0.1:3306`, database `interactiveai`).
Tabel dibuat otomatis saat start (`Sessions/Messages/WorkOrders/VehicleUnits/DriverPairings` + seed master).
Balik ke SQLite: ubah `Database:Provider` ke `Sqlite`. Override koneksi via env `MYSQL_CONNECTION`.

## Prasyarat
- .NET 8 SDK
- Akun + API key Ollama Cloud: https://ollama.com/settings/keys
- Nama model Cloud persis, misal: `gpt-oss:120b-cloud`

Jangan taruh API key di file. Set env var sebelum run:
```
$env:OLLAMA_API_KEY="kunci-kamu"
$env:ELEVENLABS_API_KEY="kunci-kamu"
```
Ubah model default di `src/InteractiveAI.Backend/appsettings.json` → `Ollama:DefaultModel`.
Key di `appsettings.json` sengaja dikosongkan (env var menang bila diisi).

## Voice + kamera otomatis (tab Chat `/`, Chrome)
Buka web → **layar awal hanya tombol Mulai** (header/chat/kamera belum tampil, kamera & deteksi belum jalan) → klik **Mulai** (satu user gesture: izin kamera + mic + buka kunci audio) → langsung masuk **view percakapan**: kamera nyala di panel kanan → tiap 3 detik frame discan (`POST /api/vision/detect`):
- Manusia terdeteksi → Kiat Ananda AI otomatis menyapa via suara: "Halo, saya Kiat Ananda AI dari loket WO..." (`POST /api/chat/greet`, tanpa LLM call) → percakapan dimulai sendiri
- Belum ada manusia → status "Mencari manusia di depan kamera..." (deteksi ulang tiap 3 detik; YOLO11-COCO kelas `person`)
- Tombol **Chat baru** (sidebar) / toggle Diskusi → kembali ke layar tombol **Mulai**: kamera mati, deteksi berhenti, tunggu klik Mulai lagi
- Suara: provider dipilih di `Voice:Provider` = `ElevenLabs` (**default sekarang**, cloud, butuh API key) / `Local` (MMS-TTS lokal) / `Vits` (Coqui lokal) / `Edge` (Edge TTS cloud, gratis). Dropdown Gadis (wanita) / Ardi (pria) + kecepatan Santai/Cerah/Cepat; normalisasi lisan + cache MP3 backend; fallback suara browser bila TTS gagal. Endpoint: `GET /api/voice/tts?text=...&voice=...&rate=...`, `GET /api/voice/voices`
- **STT backend (Hush + Whisper, opsional)**: `Voice:Stt:Provider = Backend` → mic direkam per ujaran (VAD endpoint / tahan mic) → `POST /api/stt` (wav 16kHz) → denoise Hush → transkrip Whisper id → masuk alur chat biasa. Gagal model → fallback Web Speech browser. File model ditaruh manual di `model/` (`weya_nc.dll`, `hush_onnx.tar.gz`, `ggml-base.bin`, diabaikan git). Default `Browser` (tanpa file model pun jalan).
- **ElevenLabs** (default): isi `Voice:ElevenLabs:ApiKey` di `appsettings.json` (atau env var `ELEVENLABS_API_KEY`; env var menang). Nama voice dropdown dipetakan ke voice id lewat `Voice:ElevenLabs:Voices` (`id-ID-GadisNeural` / `id-ID-ArdiNeural`); bila isinya kosong, semua nama memakai `Voice:ElevenLabs:DefaultVoiceId` (Gadis & Ardi akan bersuara sama sampai `Voices` diisi). Rate aplikasi (0/+12/+25) jadi `speed` ElevenLabs 0.7-1.2 (`MapRateToSpeed`). Cari voice id: dashboard ElevenLabs, atau (butuh API key dengan permission `voices_read`) `GET /api/voice/elevenlabs`. Ganti provider lain cukup ubah `Voice:Provider` (tanpa ubah kode).
  - Voice id default yang sudah diuji jalan di akun ini: `id-ID-GadisNeural` = `RbNgJzKAV7jpYJNtCBpj` (Citra, wanita Indonesia), `id-ID-ArdiNeural` = `ErXwobaYiN019PkySvjV` (Antoni, pria). Alternatif: `EXAVITQu4vr4xnSDxMaL` (Sarah, wanita Inggris), `pNInz6obpgDQGcFmaJgB` (Adam, pria Inggris).
  - **Penting**: Free plan tidak boleh memakai *library/community voice* lewat API → balasan `402 payment_required` ("Free users cannot use library voices via the API."). Kalau TTS gagal 402, ganti `DefaultVoiceId`/`Voices` ke voice premade di atas (bukan voice hasil browse library).
  - **Hasil uji langsung (plan Free, 29 Sep 2026)** — `Voices` diisi hanya dengan yang lolos:
    - Jalan (balasan MP3): `EXAVITQu4vr4xnSDxMaL` = Sarah (wanita), `cgSgspJ2msm6clMCkdW9` = Jessica (wanita), `pNInz6obpgDQGcFmaJgB` = Adam (pria).
    - 402 library voice (butuh plan berbayar): `RbNgJzKAV7jpYJNtCBpj` = Citra (wanita Indonesia), `21m00Tcm4TlvDq8ikWAM` = Rachel, `AZnzlk1XvdvUeBnXmlld` = Domi, `MF3mGyEYCl7XYWbV9V6O` = Elli, `XB0fDUnXU5powFXDhCwa` = Charlotte, `ErXwobaYiN019PkySvjV` = Antoni.
    - Jadi di plan Free: suara wanita = **Sarah/Jessica** (logat Inggris saat baca Bahasa Indonesia). Suara wanita Indonesia asli (Citra) butuh upgrade plan, atau pakai fallback lokal `Vits` (gadis) / `Mms`.
    - **Status 402 ≠ kredit habis.** ElevenLabs memakai 402 `payment_required` untuk *dua* hal: (a) `paid_plan_required` = voice library/community tidak boleh lewat API di plan Free, dan (b) kredit benar-benar habis. Pesan error backend sekarang membedakan keduanya (dulu selalu tertulis "kuota habis" → menyesatkan walau kredit masih ada).
    - **Pertahanan otomatis**: bila voice yang dipetakan ditolak 402 `paid_plan_required`, backend mencoba `DefaultVoiceId` **sekali** dulu sebelum jatuh ke fallback lokal — supaya salah map voice tidak langsung berubah jadi suara MMS.
    - **Kasus nyata "kok jadi Sarah"**: `Voices[Gadis]=Citra` + plan Free → Citra ditolak 402 → backend diam-diam memakai `DefaultVoiceId` (Sarah) yang lolos. Bukan salah ID — ID Citra benar (`RbNg...`), tapi plan Free melarang library voice via API. Cek suara yang benar-benar berbunyi via header respons `X-Voice-Id` (lihat di DevTools Network) atau `GET /api/voice/status`.
    - `GET /api/voice/elevenlabs` bisa balas 502 bila API key belum punya permission `voices_read` — bukan error TTS.
    - **Penting saat ganti config**: `appsettings.json` dibaca sekali saat start. Kalau aplikasi masih jalan, (1) hentikan dulu (proses mengunci `bin\...\InteractiveAI.Backend.exe` sehingga `dotnet build` gagal `MSB3027 file is locked`), (2) `dotnet build`, (3) baru jalankan. Kalau tidak, app tetap memakai config lama.
  - **Fallback otomatis ke MMS lokal**: bila ElevenLabs gagal (kredit habis 402 / 401 / network putus), backend otomatis memakai MMS-TTS lokal (`model/model.onnx`, Bahasa Indonesia, gratis selamanya) — loket tetap bersuara walau kualitas turun. Frontend tidak perlu diubah: tetap 1 endpoint `GET /api/voice/tts` (MP3 bila Citra sukses, WAV bila jatuh ke MMS). Atur di `Voice:Fallback:Enabled` (default `true`) + `Voice:Fallback:QuotaCooldownMinutes` (default `30`: sekali kena 402, 30 menit berikutnya langsung MMS tanpa buang waktu coba ElevenLabs tiap sapaan; otomatis coba Citra lagi setelah jeda habis / kuota reset bulanan). Cek status: `GET /api/voice/status` (ada `quotaTripped` + `retryPrimaryAfter`).
  - Model default `eleven_flash_v2_5` (cepat + hemat 50% kredit, cocok ngobrol loket real-time; sudah diuji 200 OK mengembalikan audio/mpeg untuk teks Bahasa Indonesia). Alternatif: `eleven_multilingual_v2` (kualitas narasi), `eleven_v3` (paling ekspresif, butuh plan berbayar). Set `Voice:ElevenLabs:ModelId` bila kuota Free plan (10.000 kredit/bulan) cepat habis.
- Barge-in: sopir menyela saat AI bicara → AI berhenti + langsung dengarkan (deteksi level mic lokal)
- Mic otomatis menyala → ngobrol full suara bolak-balik dalam session yang sama (persona Gay AI, jawaban singkat gaya lisan)
- Orang pergi >15 detik → reset, sapa lagi saat ada yang datang
- Catatan browser: Chrome kadang mengunci audio sebelum ada klik pertama di halaman — klik di mana saja sekali bila suara belum keluar. Izin kamera + mic diminta browser saat pertama kali.
- Tombol mic / speaker / Diskusi tetap ada sebagai kontrol manual; input ketik tetap bisa dipakai.

## Jalankan
Terminal 1 (Backend :5000):
```
dotnet run --project src/InteractiveAI.Backend
# Swagger: http://localhost:5000/swagger
# Health:  http://localhost:5000/health
```

Terminal 2 (Frontend :5001):
```
dotnet run --project src/InteractiveAI.Frontend
# UI: http://localhost:5001
```

## API Backend
- `POST /api/chat` `{ sessionId?, model?, message, temperature?, systemPrompt? }`
- `POST /api/chat/greet` `{ model?, systemPrompt?, greeting }` — sapaan pasti tanpa LLM call (dipakai Mode Pemandu: manusia terdeteksi → "Halo, saya Gay AI. Ada yang bisa saya bantu?"), session baru + system + sapaan tersimpan
- `GET /api/models` → proxy `GET https://ollama.com/api/tags`
- `GET /api/sessions`
- `GET /api/history/{sessionId}`
- `DELETE /api/history/{sessionId}`
- `GET /api/voice/tts?text=...&voice=...&rate=...` → audio (MP3 untuk provider cloud Edge/ElevenLabs, WAV untuk provider lokal Vits/MMS)
- `GET /api/voice/voices` → nama voice yang tersedia di provider aktif
- `GET /api/voice/elevenlabs` → daftar voice akun ElevenLabs (butuh `Voice:Provider=ElevenLabs` + API key) untuk mencari `voice_id`
- `GET /api/voice/status` → status provider utama + fallback (masih Citra atau sudah MMS?)
- `GET /health`

Ollama Cloud yang dipakai (native API, auth `Authorization: Bearer $OLLAMA_API_KEY`):
- `POST https://ollama.com/api/chat`
- `GET https://ollama.com/api/tags`

## Face detection (YOLO11-COCO, lokal, tanpa key)
Backend menjalankan `yolo11n.onnx` (±10 MB, di `src/InteractiveAI.Backend/Models/`) via ONNX Runtime — preprocess letterbox 640px, NMS IoU 0.45. Unduh ulang bila file hilang:
```
curl -L -o src/InteractiveAI.Backend/Models/yolo11n.onnx https://github.com/ultralytics/assets/releases/download/v8.3.0/yolo11n.onnx
```
- `GET /api/vision/info` — info model + kelas default
- `POST /api/vision/detect` (multipart field `image`, maks 10 MB, query opsional `confidence`, `classes=person,cat`) → `{ imageWidth, imageHeight, model, inferenceMs, detections[{ label, confidence, x, y, width, height }] }`
- Halaman Frontend: `/Vision` — webcam (tombol kamera + deteksi frame) atau upload gambar, box hijau + label + confidence digambar di canvas
- **Mode Pemandu AI** (di halaman `/Vision`, Chrome): nyalakan kamera → aktifkan pemandu → tiap 3 detik frame discan; saat orang terdeteksi, `gemma4:31b` otomatis menyapa ("perkenalkan diri sebagai InteractiveAI pemandu...") via TTS, lalu mic menyala untuk lanjut ngobrol dalam session yang sama. Orang pergi >15 detik → reset, sapa lagi saat ada yang datang. Persona bisa diedit di textarea.

**Batasan jujur:** YOLO11-COCO tidak punya kelas `face` (80 kelas COCO hanya ada `person`), jadi "deteksi wajah" = deteksi orang. Untuk box wajah murni, tukar `Vision:ModelPath` ke model face ONNX ber-label `face` — pipeline + NMS + UI langsung cocok tanpa ubah kode.

## Chatbot Loket WO (sopir truk + QR KIMO)
Alur di tab Chat `/`: sopir terdeteksi → Gay AI (petugas loket WO) menyapa → tanya nama sopir → nopol/unit → keluhan/perbaikan → sopir tunjukkan QR aplikasi KIMO ke kamera (auto-decode tiap scan, badge "QR KIMO", tercatat ke percakapan sekali per QR) → klik **Buat WO** → LLM mengekstrak data → WO tersimpan (`WO-YYYYMMDD-0001`, status OPEN).
- `POST /api/vision/detect?qr=true` → tambah `qrCodes[{ format, text }]`
- `POST /api/wo/from-session` `{ sessionId }` → WO (nama sopir, unit, keluhan, QR KIMO)
- `GET /api/wo` → daftar WO
- `GET /api/master/lookup?nopol=...&driver=...` → cek nopol + pairing driver (`found`, `active`, `truckType`, `driverFullName`, `pairMatched`); provider `Local` (SQLite seed, contoh B 9002 UXU/Anto) atau `Api` (set `Master:BaseUrl` + `LookupPath` saat URL eksternal sudah ada)
- `GET /api/master/units`, `POST /api/master/units` → lihat/tambah bank data lokal
