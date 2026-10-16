# Catatan Percakapan — InteractiveAI (disimpan 19 Sep 2026, lanjut besok)

## Ringkasan
Proyek: chatbot AI loket WO bengkel truk. Sopir terdeteksi kamera → AI "Gay AI" menyapa via suara →
ngobrol full suara (sela-menyela bisa) → sopir tunjukkan QR KIMO → verifikasi → klik Buat WO → WO tersimpan.

## Arsitektur
- `src/InteractiveAI.Backend` (ASP.NET WebAPI .NET 8, `:5000`, SQLite `chat.db`)
  - Ollama Cloud `https://ollama.com` (native `/api/*`, Bearer `OLLAMA_API_KEY`), model default `gemma4:31b`
  - YOLO11-COCO `Models/yolo11n.onnx` (ONNX Runtime, deteksi `person`; COCO tidak punya kelas `face`)
  - QR decode ZXing.Net (`GET /api/vision/detect?qr=true` → `qrCodes[{format,text}]`)
  - Edge TTS neural (`GET /api/voice/tts?text=&voice=` → MP3; Gadis/Ardi)
  - Chat: `POST /api/chat`, `POST /api/chat/greet` (sapaan pasti tanpa LLM), history sessions
  - WO: `POST /api/wo/from-session` (LLM ekstrak nama/unit/keluhan), `GET /api/wo`
- `src/InteractiveAI.Frontend` (ASP.NET MVC + AdminLTE, `:5001`)
  - Tab Chat `/`: kamera auto-nyala, scan tiap 3 dtk, sapaan + ngobrol full suara, badge QR, tombol Buat WO
  - Halaman `/Vision`: webcam manual + upload + Mode Pemandu
  - Proxy: `/Chat/*`, `/Vision/*` (Backend URL tidak terekspos ke browser)

## Keputusan penting
1. Ollama **Cloud** (bukan lokal). Key JANGAN di-commit (saat ini masih polos di `appsettings.json` → pindah ke `$env:OLLAMA_API_KEY`).
2. Nama AI: **Gay AI**, persona petugas loket WO yang supel (1-2 kalimat, satu pertanyaan per giliran).
3. Sapaan pasti: "Saya Gay AI, ada yang bisa saya bantu?" / varian loket — via `/api/chat/greet`.
4. Kamera hanya bisa dipakai **satu tab** → tab tersembunyi melepas kamera otomatis; izin kamera butuh 1 klik (`Aktifkan Kamera & Suara`).
5. Audio browser dikunci sampai 1 klik pertama (aturan Chrome) → prime + banner bila terkunci.
6. Barge-in = deteksi level mic lokal (bukan VAD neural); ambang `VAD_THRESHOLD` di `chat.js` bila loket berisik.
7. ImageSharp dipakai v3 (Apache) bukan v4 (komersial).
8. `chat.db` berisi data tes (Budi Santoso / WO-20260919-0001) — hapus bila mau bersih.
9. `.gitignore`: `bin/ obj/ *.db *.onnx .vs/`.

## Status terakhir (19 Sep 2026 malam)
- Build solution 0 error. Semua endpoint terverifikasi live (chat, greet, vision, QR, TTS MP3 valid, WO extract tepat).
- Terakhir dikerjakan: suara neural Edge TTS + dropdown Gadis/Ardi/Browser + fallback + barge-in tetap jalan.
- Sidebar mini diperbaiki (collapse → icon + dan icon chat saja).

## TODO besok (belum dikerjakan)
- [ ] Uji fisik di loket: kamera auto, sapaan suara neural, sela-menyela, scan QR KIMO asli, Buat WO
- [ ] Pindahkan `Ollama:ApiKey` dari `appsettings.json` ke env var / user-secrets
- [ ] Pertimbangkan: daftar WO di UI (`GET /Chat/WO` sudah ada, belum ada halamannya), status WO (OPEN→PROSES→SELESAI), print/struk WO
- [ ] Bila barge-in kepicu suara mesin: naikkan `VAD_THRESHOLD` atau tambah VAD neural
- [ ] Bila ingin wajah murni (bukan person-box): ganti `Vision:ModelPath` ke model face ONNX berlabel `face`

## Cara jalan
```
$env:OLLAMA_API_KEY="kunci-dari-ollama.com/settings/keys"
dotnet run --project src/InteractiveAI.Backend    # :5000
dotnet run --project src/InteractiveAI.Frontend   # :5001 (buka di Chrome)
```
