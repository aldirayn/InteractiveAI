MODEL AI — STATUS & CARA PASANG
==============================
Folder ini dibaca backend via appsettings.json -> Vision.

1) yolo11n-coco.onnx  [TERPASANG]
   - Deteksi orang (class 0). Wajib ada agar kamera aktif.

2) facenet.onnx  [BELUM ADA — opsional tapi disarankan]
   - Identitas wajah sopir (FaceNet 128-d, repo MIT).
   - Sumber: https://huggingface.co/astaileyyoung/facenet-onnx (file facenet.onnx, ±91 MB)
   - Cara: download file facenet.onnx dari link di atas, taruh di folder ini
     dengan nama persis "facenet.onnx".
   - Fungsi: wajah baru otomatis jadi SOPIR-001/002/...; sopir yang SAMA
     kembali <30 menit → draft dilanjutkan; wajah beda → mulai baru.
   - Terukur: foto-sama 0.66–0.99, orang-beda ≤0.31, threshold 0.45.
   - Tanpa file ini: sistem tetap jalan mode presence (siapa pun kembali
     ditawari lanjutkan + konfirmasi suara "mulai baru").

Cek status: GET /api/vision/status
  -> enabled:true (YOLO) dan faceEnabled:true (FaceNet) berarti siap.
