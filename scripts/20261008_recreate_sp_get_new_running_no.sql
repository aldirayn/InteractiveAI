-- =====================================================================
-- Tujuan: membuat ulang sp_get_new_running_no di database lokal agar bisa
--   dipanggil aplikasi (nomor WO: WO-YYMM-XXXXX, mis. WO-2609-00001).
-- Database: mkpool (MySQL)
-- MASALAH yang diselesaikan:
--   SP bawaan dari environment lain memakai DEFINER='mySqlDB'@'%' + default
--   SQL SECURITY DEFINER, sehingga CALL di lokal gagal:
--   "The user specified as a definer ('mySqlDB'@'%') does not exist".
-- CATATAN / AMAN / DAMPAK:
--   - DROP + CREATE ulang SP (isi logika SAMA PERSIS dengan SP asal).
--   - Destruktif ringan: nomor running (ms_running_no.lastno) TIDAK diubah,
--     jadi urutan nomor tetap sambung. Selama eksekusi (<1 dtk), CALL bersamaan
--     akan menunggu lock normal; jalankan di luar jam transaksi bila paranoid.
--   - SQL SECURITY INVOKER: berlaku dgn user pemanggil apa pun (root lokal maupun
--     user lain), kebal beda environment. Pemanggil butuh EXECUTE (root punya).
-- Dijalankan MANUAL oleh user (mysql client / Workbench / DBeaver).
-- Agent tidak mengeksekusi file ini.
-- Contoh: mysql -h 127.0.0.1 -u root -p mkpool < scripts/20261008_recreate_sp_get_new_running_no.sql
-- =====================================================================

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_get_new_running_no`$$

CREATE DEFINER=`root`@`localhost` PROCEDURE `sp_get_new_running_no`(
    IN in_prefix VARCHAR(3)
)
SQL SECURITY INVOKER
BEGIN
    DECLARE v_period VARCHAR(4);
    DECLARE v_lastno INT DEFAULT 0;
    DECLARE v_newno INT;

    -- Buat period: 2 digit tahun + 2 digit bulan
    SET v_period = DATE_FORMAT(CURDATE(), '%y%m');

    -- Insert record baru kalau belum ada
    INSERT INTO ms_running_no (prefix, period, lastno)
    SELECT in_prefix, v_period, 0
    FROM DUAL
    WHERE NOT EXISTS (
        SELECT 1 FROM ms_running_no
        WHERE prefix = in_prefix AND period = v_period
    );

    -- Ambil nilai terakhir (lock row biar aman)
    SELECT lastno INTO v_lastno
    FROM ms_running_no
    WHERE prefix = in_prefix AND period = v_period
    FOR UPDATE;

    -- Hitung nomor baru
    SET v_newno = v_lastno + 1;

    -- Update lastno
    UPDATE ms_running_no
    SET lastno = v_newno
    WHERE prefix = in_prefix AND period = v_period;

    -- Return hasil
    SELECT CONCAT(in_prefix, '-', v_period, '-', LPAD(v_newno, 5, '0')) AS new_running_no;
END$$

DELIMITER ;

-- Verifikasi (jalankan manual setelah file di atas sukses):
-- SHOW CREATE PROCEDURE sp_get_new_running_no;
-- CALL sp_get_new_running_no('WO');
-- (ekspektasi: 1 baris new_running_no, mis. WO-2610-00001; jalankan ROLLBACK
--  setelahnya bila tidak ingin nomor terpakai: START TRANSACTION; CALL ...; ROLLBACK;)
-- SELECT prefix, period, lastno FROM ms_running_no WHERE prefix = 'WO';
