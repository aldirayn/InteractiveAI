-- =====================================================================
-- Tujuan: membersihkan baris WO yang tercemar nomor SRD hantu '...' di
--   kolom pre_saran_pm (akibat ekstraksi QR ikut membaca pesan sistem yang
--   berisi contoh "[QR KIMO: ...]" sebelum ada filter role).
-- Database: mkpool (MySQL)
-- MASALAH yang diselesaikan:
--   Setiap simpan WO tanpa scan QR gagal dengan:
--   "WO untuk SRDNumber '...' sudah pernah dibuat."
--   karena baris pertama tanpa QR tersimpan dengan pre_saran_pm = '...'.
-- CATATAN / AMAN / DAMPAK:
--   - Hanya menyentuh baris yang TRIM(pre_saran_pm) = '...' (dan tampilkan
--     baris kosong lain untuk inspeksi, TANPA mengubahnya).
--   - UPDATE mengeset pre_saran_pm = '' (kolom NOT NULL; CHAR fixed-length
--     menyimpan blank; aplikasi menulis "" untuk WO tanpa QR).
--   - Tidak menyentuh nomor WO, tidak menyentuh ms_running_no.
--   - Baca dulu hasil SELECT; jalankan UPDATE bila datanya sesuai harapan.
-- Dijalankan MANUAL oleh user (mysql client / Workbench / DBeaver).
-- Agent tidak mengeksekusi file ini.
-- Contoh: mysql -h 127.0.0.1 -u root -p mkpool < scripts/20261008_fix_kimo_ghost_rows.sql
-- =====================================================================

-- Bukti: baris tercemar di tabel proses.
SELECT pre_sr_no, pre_wo_date, pre_unit_no, pre_driver_name, pre_saran_pm
  FROM tr_wo_hdr_process
 WHERE TRIM(pre_saran_pm) = '...';

-- Bukti: baris tercemar di tabel histori.
SELECT pre_sr_no, pre_wo_date, pre_unit_no, pre_driver_name, pre_saran_pm
  FROM tr_wo_hdr_history
 WHERE TRIM(pre_saran_pm) = '...';

-- Inspeksi (tanpa ubah): WO tanpa QR era lama yang tersimpan string kosong.
SELECT pre_sr_no, pre_wo_date, pre_unit_no, pre_driver_name, pre_saran_pm
  FROM tr_wo_hdr_process
 WHERE TRIM(pre_saran_pm) = '';
SELECT pre_sr_no, pre_wo_date, pre_unit_no, pre_driver_name, pre_saran_pm
  FROM tr_wo_hdr_history
 WHERE TRIM(pre_saran_pm) = '';

-- Perbaikan: kembalikan baris hantu '...' menjadi kosong (tanpa QR).
UPDATE tr_wo_hdr_process
   SET pre_saran_pm = ''
 WHERE TRIM(pre_saran_pm) = '...';
UPDATE tr_wo_hdr_history
   SET pre_saran_pm = ''
 WHERE TRIM(pre_saran_pm) = '...';
