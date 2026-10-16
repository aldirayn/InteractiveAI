-- Seed bank data master (varian SQLite).
-- Cara pakai dari folder src/InteractiveAI.Backend: sqlite3 chat.db < ../../scripts/seed-master-sqlite.sql
-- Aman dijalankan ulang: tidak dobel (INSERT OR IGNORE / WHERE NOT EXISTS).

INSERT OR IGNORE INTO "VehicleUnits" ("Nopol", "NopolNorm", "Active", "TruckType")
VALUES ('B 9002 UXU', 'B9002UXU', 1, 'Truk Tronton');

INSERT INTO "DriverPairings" ("NopolNorm", "DriverCode", "DriverName", "FullName")
SELECT 'B9002UXU', 'ANTO', 'Anto', 'Anto Wijaya'
WHERE NOT EXISTS (SELECT 1 FROM "DriverPairings" WHERE "NopolNorm" = 'B9002UXU' AND "DriverCode" = 'ANTO');
