-- Seed bank data master: contoh nopol + pairing driver.
-- Cara pakai (MySQL live): mysql -h 127.0.0.1 -u root -p interactiveai < scripts/seed-master.sql
-- Aman dijalankan ulang: tidak dobel (INSERT IGNORE / WHERE NOT EXISTS).

INSERT IGNORE INTO `VehicleUnits` (`Nopol`, `NopolNorm`, `Active`, `TruckType`)
VALUES ('B 9002 UXU', 'B9002UXU', 1, 'Truk Tronton');

INSERT INTO `DriverPairings` (`NopolNorm`, `DriverCode`, `DriverName`, `FullName`)
SELECT 'B9002UXU', 'ANTO', 'Anto', 'Anto Wijaya' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `DriverPairings` WHERE `NopolNorm` = 'B9002UXU' AND `DriverCode` = 'ANTO');

-- ============================================================
-- Varian SQLite (bila backend pakai Provider=Sqlite).
-- Cara pakai: sqlite3 chat.db < scripts/seed-master-sqlite.sql
-- (dipisah file agar sintaks tidak tertukar)
-- ============================================================
