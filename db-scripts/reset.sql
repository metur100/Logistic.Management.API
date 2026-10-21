-- Destructive reset: removes all LogistiTrack operational data and migration history.
-- Run only when you intend to start from a clean database. The API recreates the
-- schema on startup (EF migrations) and can optionally seed demo data.
DROP TABLE IF EXISTS [TripStatusHistories];
DROP TABLE IF EXISTS [Cargoes];
DROP TABLE IF EXISTS [Incidents];
DROP TABLE IF EXISTS [Messages];
DROP TABLE IF EXISTS [SupportTickets];
DROP TABLE IF EXISTS [Trips];
DROP TABLE IF EXISTS [Vehicles];
DROP TABLE IF EXISTS [Users];
DROP TABLE IF EXISTS [Companies];
DROP TABLE IF EXISTS [SmtpSettings];
DROP TABLE IF EXISTS [PlatformSettings];
DROP TABLE IF EXISTS [__EFMigrationsHistory];
