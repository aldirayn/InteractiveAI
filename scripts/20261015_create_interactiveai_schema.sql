-- =====================================================================
-- Tujuan: Membuat (manual) seluruh skema database `interactiveai`
--   (MySQL) sesuai model code-first EF Core yang dipakai aplikasi.
--   Berguna untuk provisioning DB baru / review struktur tanpa harus
--   menunggu EnsureCreated() saat aplikasi pertama kali start.
-- Database: interactiveai (MySQL) — dari ConnectionStrings:Mysql
--   (server=127.0.0.1;port=3306;database=interactiveai;CharSet=utf8mb4)
-- MASALAH yang diselesaikan:
--   Saat ini tabel dibuat otomatis oleh aplikasi (AppDbContext /
--   EnsureCreated). Script ini menyediakan DDL eksplisit & idempotent
--   agar tim bisa membuat skema lebih awal (CI, staging, production)
--   tanpa menjalankan aplikasi, serta sebagai dokumentasi struktur.
-- CATATAN / AMAN / DAMPAK:
--   - IDEMPOTENT: memakai CREATE DATABASE IF NOT EXISTS dan
--     CREATE TABLE IF NOT EXISTS -> aman dijalankan ulang.
--   - NON-DESTRUKTIF: TIDAK ada DROP / DELETE / TRUNCATE. Tabel yang
--     sudah ada akan dibiarkan (tidak diubah strukturnya).
--   - Charset utf8mb4 mengikuti connection string aplikasi.
--   - Tipe kolom mengikuti mapping EF Core + Pomelo (MySQL):
--       Guid -> CHAR(36), long PK -> BIGINT AUTO_INCREMENT,
--       DateTime -> DATETIME(6), bool -> TINYINT(1),
--       string ber-maxlength -> VARCHAR(n), string lain -> LONGTEXT.
-- Dijalankan MANUAL oleh user (mysql client / Workbench / DBeaver).
-- Agent tidak mengeksekusi file ini.
-- Contoh: mysql -h 127.0.0.1 -u root -p < scripts/20261015_create_interactiveai_schema.sql
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `interactiveai`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `interactiveai`;

-- ---------------------------------------------------------------------
-- ChatSessions (entity: ChatSession) — sesi chat 1 user.
-- PK: Id (Guid). Title/Model dibatasi panjang oleh OnModelCreating.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `ChatSessions` (
  `Id`        CHAR(36)     NOT NULL,
  `Title`     VARCHAR(200) NOT NULL,
  `Model`     VARCHAR(100) NOT NULL,
  `CreatedAt` DATETIME(6)  NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- Messages (entity: ChatMessage) — pesan dalam sebuah sesi.
-- PK: Id (BIGINT AI). FK SessionId -> ChatSessions(Id) ON DELETE CASCADE.
-- Index pada SessionId (HasIndex) mempercepat query histori pesan.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Messages` (
  `Id`        BIGINT      NOT NULL AUTO_INCREMENT,
  `SessionId` CHAR(36)    NOT NULL,
  `Role`      VARCHAR(20) NOT NULL,
  `Content`   LONGTEXT    NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_Messages_SessionId` (`SessionId`),
  CONSTRAINT `FK_Messages_ChatSessions_SessionId`
    FOREIGN KEY (`SessionId`) REFERENCES `ChatSessions` (`Id`)
    ON DELETE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;


-- ---------------------------------------------------------------------
-- WorkOrders (entity: WorkOrder) — work order hasil rangkuman sesi.
-- PK: Id (BIGINT AI). SessionId tidak memakai FK eksplisit di model
-- (tanpa navigation property), jadi disimpan sebagai kolom saja.
-- CATATAN: aplikasi bertanggung jawab mengisi semua kolom NOT NULL
-- (semua kolom string di WorkOrder bertipe non-nullable -> NOT NULL
--  karena <Nullable>enable</Nullable>; Status default 'OPEN' di aplikasi).
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `WorkOrders` (
  `Id`               BIGINT      NOT NULL AUTO_INCREMENT,
  `WoNumber`         LONGTEXT    NOT NULL,
  `SessionId`        CHAR(36)    NOT NULL,
  `Model`            LONGTEXT    NOT NULL,
  `DriverName`       LONGTEXT    NOT NULL,
  `UnitNumber`       LONGTEXT    NOT NULL,
  `Complaint`        LONGTEXT    NOT NULL,
  `QrKimo`           LONGTEXT    NOT NULL,
  `Status`           LONGTEXT    NOT NULL,
  `CreatedAt`        DATETIME(6) NOT NULL,
  `DriverCode`       LONGTEXT    NOT NULL,
  `RepairCategories` LONGTEXT    NOT NULL,
  `ParkingLocation`  LONGTEXT    NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- VehicleUnits (entity: VehicleUnit) — bank data unit/kendaraan.
-- PK: Id (BIGINT AI). NopolNorm UNIQUE (HasIndex IsUnique).
-- Active bertipe bool -> TINYINT(1), default 1 (true).
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `VehicleUnits` (
  `Id`        BIGINT      NOT NULL AUTO_INCREMENT,
  `Nopol`     LONGTEXT    NOT NULL,
  `NopolNorm` VARCHAR(50) NOT NULL,
  `Active`    TINYINT(1)  NOT NULL DEFAULT 1,
  `TruckType` LONGTEXT    NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_VehicleUnits_NopolNorm` (`NopolNorm`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- DriverPairings (entity: DriverPairing) — pairing sopir per unit.
-- PK: Id (BIGINT AI). Index (non-unique) pada NopolNorm.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `DriverPairings` (
  `Id`         BIGINT      NOT NULL AUTO_INCREMENT,
  `NopolNorm`  VARCHAR(50) NOT NULL,
  `DriverCode` LONGTEXT    NOT NULL,
  `DriverName` LONGTEXT    NOT NULL,
  `FullName`   LONGTEXT    NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_DriverPairings_NopolNorm` (`NopolNorm`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

-- =====================================================================
-- VERIFIKASI (jalankan manual untuk memastikan skema terbentuk benar)
-- =====================================================================
-- SHOW TABLES;
-- SHOW CREATE TABLE `ChatSessions`;
-- SHOW CREATE TABLE `Messages`;
-- SHOW CREATE TABLE `WorkOrders`;
-- SHOW CREATE TABLE `VehicleUnits`;
-- SHOW CREATE TABLE `DriverPairings`;
--
-- Cek kolom & tipe tiap tabel:
-- SHOW COLUMNS FROM `Messages`;
-- SHOW COLUMNS FROM `WorkOrders`;
--
-- Opsional: isi bank data master contoh (idempotent, dari seed-master.sql):
--   mysql -h 127.0.0.1 -u root -p interactiveai < scripts/seed-master.sql

