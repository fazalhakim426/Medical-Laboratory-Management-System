using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using LabReportApp.Config;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp.Database
{
    public static class DatabaseHelper
    {
        // The database file lives next to the .exe, so the app is fully portable/offline.
        private static string DbFile => AppPaths.DatabaseFile;
        private static string ConnectionString => $"Data Source={DbFile}";

        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string DateFormat = "yyyy-MM-dd";

        private const string PatientColumns =
            "Id, SerialNumber, Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount, " +
            "AgeValue, AgeUnit, DateOfBirth, PregnancyStatus, SampleCollectedAt, ProfileId, PrintStatus, Cnic";

        private const string TestDefinitionColumns =
            "Id, CategoryHeading, TestName, Unit, NormalRange, Price, IsPanel, IsActive";

        private const string RangeColumns =
            "Id, TestDefinitionId, AgeMinDays, AgeMaxDays, Gender, PregnancyStatus, MinValue, MaxValue, ReferenceText, Unit, Methodology, IsQualitative, EffectiveFrom, EffectiveTo, IsActive, IsIllustrative";

        public const string IllustrativeMethodology = "ILLUSTRATIVE SAMPLE - NOT CLINICALLY APPROVED";

        public static void InitializeDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var createPatients = @"
                CREATE TABLE IF NOT EXISTS Patients (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SerialNumber TEXT UNIQUE,
                    Name TEXT NOT NULL,
                    Age INTEGER,
                    Gender TEXT,
                    ReferredBy TEXT,
                    LabName TEXT,
                    PhoneNumber TEXT,
                    ReportHeading TEXT,
                    CreatedAt TEXT,
                    Status TEXT DEFAULT 'Pending',
                    TotalAmount REAL DEFAULT 0,
                    DiscountPercentage REAL DEFAULT 0,
                    NetAmount REAL DEFAULT 0
                );";

            var createResults = @"
                CREATE TABLE IF NOT EXISTS TestResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PatientId INTEGER NOT NULL,
                    CategoryHeading TEXT,
                    TestName TEXT,
                    Result TEXT,
                    Unit TEXT,
                    NormalRange TEXT,
                    Price REAL DEFAULT 0,
                    Discount REAL DEFAULT 0,
                    FOREIGN KEY (PatientId) REFERENCES Patients(Id)
                );";

            // Master list of predefined tests maintained by the Lab Attendant.
            // TestName is UNIQUE so we can "upsert" (insert or update) by name.
            var createDefinitions = @"
                CREATE TABLE IF NOT EXISTS TestDefinitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryHeading TEXT NOT NULL,
                    TestName TEXT NOT NULL UNIQUE,
                    Unit TEXT,
                    NormalRange TEXT,
                    Price REAL DEFAULT 0
                );";

            using (var cmd1 = new SqliteCommand(createPatients, connection)) cmd1.ExecuteNonQuery();
            using (var cmd2 = new SqliteCommand(createResults, connection)) cmd2.ExecuteNonQuery();
            using (var cmd3 = new SqliteCommand(createDefinitions, connection)) cmd3.ExecuteNonQuery();

            // Migration: if you're upgrading from an older version of the app, these columns
            // won't exist yet on an existing database file. Add them automatically if missing.
            EnsureColumnExists(connection, "TestResults", "CategoryHeading", "TEXT");
            EnsureColumnExists(connection, "TestResults", "Price", "REAL");
            EnsureColumnExists(connection, "TestResults", "Discount", "REAL");
            EnsureColumnExists(connection, "TestDefinitions", "Price", "REAL");
            EnsureColumnExists(connection, "Patients", "Status", "TEXT");
            EnsureColumnExists(connection, "Patients", "TotalAmount", "REAL");
            EnsureColumnExists(connection, "Patients", "DiscountPercentage", "REAL");
            EnsureColumnExists(connection, "Patients", "NetAmount", "REAL");

            // ---- Dynamic reference range migration ----
            EnsureColumnExists(connection, "Patients", "AgeValue", "REAL");
            EnsureColumnExists(connection, "Patients", "AgeUnit", "TEXT");
            EnsureColumnExists(connection, "Patients", "DateOfBirth", "TEXT");
            EnsureColumnExists(connection, "Patients", "PregnancyStatus", "TEXT");
            EnsureColumnExists(connection, "Patients", "SampleCollectedAt", "TEXT");

            EnsureColumnExists(connection, "TestDefinitions", "IsPanel", "INTEGER DEFAULT 0");
            EnsureColumnExists(connection, "TestDefinitions", "IsActive", "INTEGER DEFAULT 1");

            EnsureColumnExists(connection, "TestResults", "AbnormalFlag", "TEXT");
            EnsureColumnExists(connection, "TestResults", "RangeMinValue", "REAL");
            EnsureColumnExists(connection, "TestResults", "RangeMaxValue", "REAL");
            EnsureColumnExists(connection, "TestResults", "IsQualitative", "INTEGER");
            EnsureColumnExists(connection, "TestResults", "Methodology", "TEXT");
            EnsureColumnExists(connection, "TestResults", "AgeAtTestDays", "INTEGER");
            EnsureColumnExists(connection, "TestResults", "AgeAtTestDisplay", "TEXT");
            EnsureColumnExists(connection, "TestResults", "GenderAtTest", "TEXT");
            EnsureColumnExists(connection, "TestResults", "PregnancyAtTest", "TEXT");
            EnsureColumnExists(connection, "TestResults", "TestDefinitionId", "INTEGER");
            EnsureColumnExists(connection, "TestResults", "IsPanelMember", "INTEGER");
            EnsureColumnExists(connection, "TestResults", "CatalogPrice", "REAL");
            EnsureColumnExists(connection, "TestResults", "DiscountMode", "TEXT");
            EnsureColumnExists(connection, "TestResults", "PanelId", "INTEGER");

            var createRanges = @"
                CREATE TABLE IF NOT EXISTS TestReferenceRanges (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TestDefinitionId INTEGER NOT NULL,
                    AgeMinDays INTEGER NOT NULL DEFAULT 0,
                    AgeMaxDays INTEGER,
                    Gender TEXT NOT NULL DEFAULT 'Both',
                    PregnancyStatus TEXT NOT NULL DEFAULT 'Not Applicable',
                    MinValue REAL,
                    MaxValue REAL,
                    ReferenceText TEXT,
                    Unit TEXT,
                    Methodology TEXT,
                    IsQualitative INTEGER DEFAULT 0,
                    EffectiveFrom TEXT,
                    EffectiveTo TEXT,
                    IsActive INTEGER DEFAULT 1,
                    IsIllustrative INTEGER DEFAULT 1,
                    FOREIGN KEY (TestDefinitionId) REFERENCES TestDefinitions(Id)
                );";

            var createPanelMembers = @"
                CREATE TABLE IF NOT EXISTS TestPanelMembers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PanelTestDefinitionId INTEGER NOT NULL,
                    MemberTestDefinitionId INTEGER NOT NULL,
                    SortOrder INTEGER DEFAULT 0,
                    UNIQUE(PanelTestDefinitionId, MemberTestDefinitionId)
                );";

            using (var cmd4 = new SqliteCommand(createRanges, connection)) cmd4.ExecuteNonQuery();
            using (var cmd5 = new SqliteCommand(createPanelMembers, connection)) cmd5.ExecuteNonQuery();
            using (var cmd6 = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS IX_TestReferenceRanges_Test ON TestReferenceRanges(TestDefinitionId);", connection))
                cmd6.ExecuteNonQuery();

            // Any patient created before this update has no Status - treat those as already
            // Completed (their old-style report was already generated), so they don't
            // suddenly show up as "Pending".
            using var backfillCmd = new SqliteCommand(
                "UPDATE Patients SET Status = 'Completed' WHERE Status IS NULL OR Status = '';", connection);
            backfillCmd.ExecuteNonQuery();

            // Backfill NULLs left behind by ALTER TABLE on pre-existing rows.
            using (var fixDefs = new SqliteCommand(
                "UPDATE TestDefinitions SET IsPanel = 0 WHERE IsPanel IS NULL; " +
                "UPDATE TestDefinitions SET IsActive = 1 WHERE IsActive IS NULL;", connection))
                fixDefs.ExecuteNonQuery();

            // Ensure the common laboratory tests master list is present (upsert from CSV).
            EnsureSeedTestDefinitions(connection);

            MigrateLegacyPatientAges(connection);
            EnsureSeedPanels(connection);
            EnsureSeedReferenceRanges(connection);

            // ---- Main-page redesign: reusable patient registry + print tracking ----
            using (var cmdProfiles = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS PatientProfiles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MrNumber TEXT UNIQUE,
                    Name TEXT NOT NULL,
                    PhoneNumber TEXT,
                    AgeValue REAL,
                    AgeUnit TEXT,
                    DateOfBirth TEXT,
                    Gender TEXT,
                    PregnancyStatus TEXT,
                    ReferredBy TEXT,
                    LabName TEXT,
                    CreatedAt TEXT,
                    UpdatedAt TEXT
                );", connection))
                cmdProfiles.ExecuteNonQuery();

            EnsureColumnExists(connection, "Patients", "ProfileId", "INTEGER");
            EnsureColumnExists(connection, "Patients", "PrintStatus", "TEXT");
            EnsureColumnExists(connection, "Patients", "Cnic", "TEXT");
            EnsureColumnExists(connection, "PatientProfiles", "Cnic", "TEXT");
            EnsureColumnExists(connection, "PatientProfiles", "CnicKey", "TEXT");

            using (var cnicIndex = new SqliteCommand(@"
                CREATE UNIQUE INDEX IF NOT EXISTS UX_PatientProfiles_CnicKey
                ON PatientProfiles(CnicKey)
                WHERE CnicKey IS NOT NULL AND CnicKey <> '';", connection))
                cnicIndex.ExecuteNonQuery();

            using (var fixPrint = new SqliteCommand(
                "UPDATE Patients SET PrintStatus = 'NotPrinted' WHERE PrintStatus IS NULL OR PrintStatus = '';", connection))
                fixPrint.ExecuteNonQuery();

            MigrateProfilesFromVisits(connection);

            // Held / parked bills (emergency interrupt workflow).
            using (var cmdHeld = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS HeldBills (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProfileId INTEGER,
                    PatientName TEXT NOT NULL,
                    MrNumber TEXT,
                    PhoneNumber TEXT,
                    AgeValue REAL,
                    AgeUnit TEXT,
                    DateOfBirth TEXT,
                    Gender TEXT,
                    PregnancyStatus TEXT,
                    ReferredBy TEXT,
                    LabName TEXT,
                    OverallDiscount REAL DEFAULT 0,
                    Note TEXT,
                    TestSummary TEXT,
                    HeldAt TEXT
                );", connection))
                cmdHeld.ExecuteNonQuery();

            using (var cmdHeldItems = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS HeldBillItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    HeldBillId INTEGER NOT NULL,
                    CategoryHeading TEXT,
                    TestName TEXT,
                    Unit TEXT,
                    NormalRange TEXT,
                    Price REAL DEFAULT 0,
                    Discount REAL DEFAULT 0,
                    TestDefinitionId INTEGER,
                    IsPanelMember INTEGER DEFAULT 0,
                    FOREIGN KEY (HeldBillId) REFERENCES HeldBills(Id) ON DELETE CASCADE
                );", connection))
                cmdHeldItems.ExecuteNonQuery();

            EnsureColumnExists(connection, "HeldBillItems", "CatalogPrice", "REAL");
            EnsureColumnExists(connection, "HeldBillItems", "DiscountMode", "TEXT");
            EnsureColumnExists(connection, "HeldBillItems", "PanelId", "INTEGER");
            EnsureColumnExists(connection, "HeldBillItems", "EntryRole", "TEXT");
            EnsureColumnExists(connection, "HeldBills", "Cnic", "TEXT");

            // Doctor registry for Referred By dropdown.
            using (var cmdDoctors = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS Doctors (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Specialty TEXT,
                    Phone TEXT,
                    Clinic TEXT,
                    IsActive INTEGER DEFAULT 1,
                    CreatedAt TEXT
                );", connection))
                cmdDoctors.ExecuteNonQuery();

            EnsureColumnExists(connection, "Patients", "RefundedAt", "TEXT");
            SeedDoctorsFromExistingReferrals(connection);
        }

        private static void SeedDoctorsFromExistingReferrals(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand(@"
                INSERT OR IGNORE INTO Doctors (Name, Specialty, Phone, Clinic, IsActive, CreatedAt)
                SELECT DISTINCT trim(src.Name), '', '', '', 1, @Now
                FROM (
                    SELECT ReferredBy AS Name FROM Patients WHERE IFNULL(trim(ReferredBy),'') <> ''
                    UNION
                    SELECT ReferredBy AS Name FROM PatientProfiles WHERE IFNULL(trim(ReferredBy),'') <> ''
                    UNION
                    SELECT ReferredBy AS Name FROM HeldBills WHERE IFNULL(trim(ReferredBy),'') <> ''
                ) src
                WHERE IFNULL(trim(src.Name),'') <> '';", connection);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        private static void MigrateProfilesFromVisits(SqliteConnection connection)
        {
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM PatientProfiles;", connection))
            {
                long count = (long)(countCmd.ExecuteScalar() ?? 0L);
                if (count > 0) return;
            }

            // Latest visit per Name+Phone becomes a reusable profile.
            using var cmd = new SqliteCommand(@"
                SELECT Name, PhoneNumber, Age, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus, ReferredBy, LabName, CreatedAt
                FROM Patients
                WHERE Id IN (
                    SELECT MAX(Id) FROM Patients
                    GROUP BY LOWER(TRIM(IFNULL(Name,''))), LOWER(TRIM(IFNULL(PhoneNumber,'')))
                )
                ORDER BY Id;", connection);

            using var reader = cmd.ExecuteReader();
            var rows = new List<(string Name, string Phone, int Age, double AgeValue, string AgeUnit, string Dob,
                string Gender, string Pregnancy, string Referred, string Lab, string Created)>();
            while (reader.Read())
            {
                rows.Add((
                    reader.IsDBNull(0) ? "" : reader.GetString(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    reader.IsDBNull(4) ? AgeUnits.Years : reader.GetString(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                    reader.IsDBNull(6) ? "" : reader.GetString(6),
                    reader.IsDBNull(7) ? PregnancyStatuses.NotApplicable : reader.GetString(7),
                    reader.IsDBNull(8) ? "" : reader.GetString(8),
                    reader.IsDBNull(9) ? "" : reader.GetString(9),
                    reader.IsDBNull(10) ? DateTime.Now.ToString(DateTimeFormat) : reader.GetString(10)
                ));
            }
            reader.Close();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name)) continue;
                double ageValue = row.AgeValue > 0 ? row.AgeValue : row.Age;
                using var insert = new SqliteCommand(@"
                    INSERT INTO PatientProfiles (Name, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus, ReferredBy, LabName, CreatedAt, UpdatedAt)
                    VALUES (@Name, @Phone, @AgeValue, @AgeUnit, @Dob, @Gender, @Pregnancy, @Referred, @Lab, @Created, @Updated);
                    SELECT last_insert_rowid();", connection);
                insert.Parameters.AddWithValue("@Name", row.Name);
                insert.Parameters.AddWithValue("@Phone", row.Phone);
                insert.Parameters.AddWithValue("@AgeValue", ageValue);
                insert.Parameters.AddWithValue("@AgeUnit", string.IsNullOrWhiteSpace(row.AgeUnit) ? AgeUnits.Years : row.AgeUnit);
                insert.Parameters.AddWithValue("@Dob", string.IsNullOrWhiteSpace(row.Dob) ? DBNull.Value : row.Dob);
                insert.Parameters.AddWithValue("@Gender", row.Gender);
                insert.Parameters.AddWithValue("@Pregnancy", string.IsNullOrWhiteSpace(row.Pregnancy) ? PregnancyStatuses.NotApplicable : row.Pregnancy);
                insert.Parameters.AddWithValue("@Referred", row.Referred);
                insert.Parameters.AddWithValue("@Lab", row.Lab);
                insert.Parameters.AddWithValue("@Created", row.Created);
                insert.Parameters.AddWithValue("@Updated", row.Created);
                long id = (long)(insert.ExecuteScalar() ?? 0L);
                string mr = $"PAT-{id:D6}";
                using var upd = new SqliteCommand("UPDATE PatientProfiles SET MrNumber = @Mr WHERE Id = @Id", connection);
                upd.Parameters.AddWithValue("@Mr", mr);
                upd.Parameters.AddWithValue("@Id", id);
                upd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Upserts Database/common_laboratory_tests_seed.csv into TestDefinitions.
        /// New tests are inserted; existing ones get Category/Unit/NormalRange refreshed from the
        /// CSV. Lab-edited prices are preserved when they are already non-zero.
        /// </summary>
        private static void EnsureSeedTestDefinitions(SqliteConnection connection)
        {
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "common_laboratory_tests_seed.csv");
            if (!File.Exists(csvPath))
                return;

            foreach (var def in ParseTestDefinitionsCsv(csvPath))
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO TestDefinitions (CategoryHeading, TestName, Unit, NormalRange, Price, IsPanel, IsActive)
                    VALUES (@Category, @TestName, @Unit, @NormalRange, @Price, 0, 1)
                    ON CONFLICT(TestName) DO UPDATE SET
                        CategoryHeading = excluded.CategoryHeading,
                        Unit = excluded.Unit,
                        NormalRange = excluded.NormalRange,
                        Price = CASE
                            WHEN TestDefinitions.Price IS NULL OR TestDefinitions.Price = 0
                            THEN excluded.Price
                            ELSE TestDefinitions.Price
                        END;", connection);

                cmd.Parameters.AddWithValue("@Category", def.CategoryHeading);
                cmd.Parameters.AddWithValue("@TestName", def.TestName);
                cmd.Parameters.AddWithValue("@Unit", def.Unit);
                cmd.Parameters.AddWithValue("@NormalRange", def.NormalRange);
                cmd.Parameters.AddWithValue("@Price", def.Price);
                cmd.ExecuteNonQuery();
            }
        }

        private static IEnumerable<TestDefinition> ParseTestDefinitionsCsv(string csvPath)
        {
            foreach (string rawLine in File.ReadLines(csvPath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                var fields = ParseCsvFields(line);
                if (fields.Count < 5)
                    continue;

                // Skip header row
                if (string.Equals(fields[0], "Category", StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal.TryParse(fields[4], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price);

                yield return new TestDefinition
                {
                    CategoryHeading = fields[0].Trim(),
                    TestName = fields[1].Trim(),
                    Unit = fields[2].Trim(),
                    NormalRange = fields[3].Trim(),
                    Price = price
                };
            }
        }

        /// <summary>
        /// Minimal CSV splitter that respects double-quoted fields (e.g. "4,000-11,000").
        /// </summary>
        public static List<string> ParseCsvFields(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        private static void EnsureColumnExists(SqliteConnection connection, string table, string column, string sqlType)
        {
            bool exists = false;
            using (var cmd = new SqliteCommand($"PRAGMA table_info({table});", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    // Column index 1 in PRAGMA table_info result is the column name.
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                using var alterCmd = new SqliteCommand($"ALTER TABLE {table} ADD COLUMN {column} {sqlType};", connection);
                alterCmd.ExecuteNonQuery();
            }
        }

        // ==================== MIGRATION / SEEDING FOR DYNAMIC REFERENCE RANGES ====================

        /// <summary>
        /// Older patient rows only have a whole-year Age. Copy that into the new AgeValue/AgeUnit
        /// model so range matching works for historical data.
        /// </summary>
        private static void MigrateLegacyPatientAges(SqliteConnection connection)
        {
            using var cmd = new SqliteCommand(@"
                UPDATE Patients
                SET AgeValue = COALESCE(Age, 0),
                    AgeUnit = 'Years',
                    PregnancyStatus = 'Not Applicable',
                    SampleCollectedAt = CreatedAt
                WHERE AgeUnit IS NULL OR AgeUnit = '';", connection);
            cmd.ExecuteNonQuery();
        }

        private static readonly (string Name, string Category, string[] Members)[] SeedPanels =
        {
            ("CBC", "Hematology", new[]
            {
                "Hemoglobin (Hb)", "RBC Count", "WBC Count", "Platelet Count", "Hematocrit (PCV)",
                "MCV", "MCH", "MCHC", "RDW-CV", "Neutrophils", "Lymphocytes", "Monocytes",
                "Eosinophils", "Basophils"
            }),
            ("LFT", "Biochemistry", new[]
            {
                "Total Bilirubin", "Direct Bilirubin", "Indirect Bilirubin", "ALT (SGPT)", "AST (SGOT)",
                "Alkaline Phosphatase (ALP)", "Total Protein", "Albumin", "Globulin", "A/G Ratio"
            }),
            ("RFT", "Biochemistry", new[]
            {
                "Blood Urea", "BUN", "Creatinine", "Uric Acid", "Sodium (Na)", "Potassium (K)",
                "Chloride (Cl)", "Calcium"
            })
        };

        /// <summary>
        /// Makes sure the CBC / LFT / RFT panel definitions exist (IsPanel = 1, price 0 until the lab
        /// sets one) and that they have their default members. Members are only seeded for a panel
        /// that has none yet, so lab edits made through SetPanelMembers are never overwritten.
        /// </summary>
        private static void EnsureSeedPanels(SqliteConnection connection)
        {
            foreach (var (name, category, members) in SeedPanels)
            {
                using (var cmd = new SqliteCommand(@"
                    INSERT INTO TestDefinitions (CategoryHeading, TestName, Unit, NormalRange, Price, IsPanel, IsActive)
                    VALUES (@Category, @TestName, '', '', 0, 1, 1)
                    ON CONFLICT(TestName) DO UPDATE SET IsPanel = 1;", connection))
                {
                    cmd.Parameters.AddWithValue("@Category", category);
                    cmd.Parameters.AddWithValue("@TestName", name);
                    cmd.ExecuteNonQuery();
                }

                var panel = GetTestDefinitionByName(connection, null, name);
                if (panel == null)
                    continue;

                long existingMembers;
                using (var countCmd = new SqliteCommand(
                    "SELECT COUNT(*) FROM TestPanelMembers WHERE PanelTestDefinitionId = @Id;", connection))
                {
                    countCmd.Parameters.AddWithValue("@Id", panel.Id);
                    existingMembers = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0L);
                }

                if (existingMembers > 0)
                    continue;

                int sort = 0;
                foreach (var memberName in members)
                {
                    var member = GetTestDefinitionByName(connection, null, memberName);
                    if (member == null || member.Id == panel.Id)
                        continue;

                    using var insertCmd = new SqliteCommand(@"
                        INSERT OR IGNORE INTO TestPanelMembers (PanelTestDefinitionId, MemberTestDefinitionId, SortOrder)
                        VALUES (@Panel, @Member, @Sort);", connection);
                    insertCmd.Parameters.AddWithValue("@Panel", panel.Id);
                    insertCmd.Parameters.AddWithValue("@Member", member.Id);
                    insertCmd.Parameters.AddWithValue("@Sort", sort++);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Loads Database/test_reference_ranges_seed.csv into TestReferenceRanges, but only when the
        /// table is empty so lab-managed ranges are never overwritten on startup.
        /// </summary>
        private static void EnsureSeedReferenceRanges(SqliteConnection connection)
        {
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM TestReferenceRanges;", connection))
            {
                long count = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0L);
                if (count > 0)
                    return;
            }

            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "test_reference_ranges_seed.csv");
            if (!File.Exists(csvPath))
                return;

            using var transaction = connection.BeginTransaction();
            try
            {
                ImportReferenceRangesCore(connection, transaction, csvPath);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== STAGE 1: BILLING (patient + selected tests, no results yet) ====================

        /// <summary>
        /// Saves the patient and the list of tests they've been billed for (Result is empty at this
        /// point). Saves Patient.Status as "Pending" and Patient.TotalAmount as given.
        /// A panel is stored as a package row at the price on the bill, plus its included tests at
        /// price 0. Those copied prices are never read back from the test catalog, so later edits
        /// to a panel or a test do not change this bill. Every result row gets the matching
        /// reference range and a demographic snapshot.
        /// Returns the generated unique serial number, e.g. LAB-000001.
        /// </summary>
        public static string SavePatientAndResults(Patient patient, List<TestResultItem> results)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                string ageUnit = string.IsNullOrWhiteSpace(patient.AgeUnit) ? AgeUnits.Years : patient.AgeUnit;
                double ageValue = patient.AgeValue > 0 ? patient.AgeValue : patient.Age;
                string pregnancy = string.IsNullOrWhiteSpace(patient.PregnancyStatus)
                    ? PregnancyStatuses.NotApplicable
                    : patient.PregnancyStatus;
                DateTime sampleCollectedAt = patient.SampleCollectedAt == default ? patient.CreatedAt : patient.SampleCollectedAt;

                // Normalised copy used for age maths / display so we never depend on UI-side defaults.
                var normalized = new Patient
                {
                    AgeValue = ageValue,
                    AgeUnit = ageUnit,
                    DateOfBirth = patient.DateOfBirth,
                    Gender = patient.Gender ?? string.Empty,
                    PregnancyStatus = pregnancy,
                    Age = patient.Age
                };
                int ageDays = AgeCalculator.GetAgeInDays(normalized, sampleCollectedAt);
                string ageDisplay = FormatAgeSnapshot(normalized, ageValue, ageUnit, sampleCollectedAt);

                int legacyAge = patient.Age > 0 ? patient.Age : (int)Math.Floor(ageDays / 365.0);

                var insertPatient = @"
                    INSERT INTO Patients (Name, Age, Gender, ReferredBy, LabName, PhoneNumber, ReportHeading, CreatedAt, Status, TotalAmount, DiscountPercentage, NetAmount,
                                          AgeValue, AgeUnit, DateOfBirth, PregnancyStatus, SampleCollectedAt, ProfileId, PrintStatus, Cnic)
                    VALUES (@Name, @Age, @Gender, @ReferredBy, @LabName, @PhoneNumber, @ReportHeading, @CreatedAt, @Status, @TotalAmount, @DiscountPercentage, @NetAmount,
                            @AgeValue, @AgeUnit, @DateOfBirth, @PregnancyStatus, @SampleCollectedAt, @ProfileId, @PrintStatus, @Cnic);
                    SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(insertPatient, connection, transaction);
                cmd.Parameters.AddWithValue("@Name", patient.Name);
                cmd.Parameters.AddWithValue("@Age", legacyAge);
                cmd.Parameters.AddWithValue("@Gender", patient.Gender);
                cmd.Parameters.AddWithValue("@ReferredBy", patient.ReferredBy);
                cmd.Parameters.AddWithValue("@LabName", patient.LabName);
                cmd.Parameters.AddWithValue("@PhoneNumber", patient.PhoneNumber);
                cmd.Parameters.AddWithValue("@ReportHeading", patient.ReportHeading);
                cmd.Parameters.AddWithValue("@CreatedAt", patient.CreatedAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@Status", patient.Status);
                cmd.Parameters.AddWithValue("@TotalAmount", patient.TotalAmount);
                cmd.Parameters.AddWithValue("@DiscountPercentage", patient.DiscountPercentage);
                cmd.Parameters.AddWithValue("@NetAmount", patient.NetAmount);
                cmd.Parameters.AddWithValue("@AgeValue", ageValue);
                cmd.Parameters.AddWithValue("@AgeUnit", ageUnit);
                cmd.Parameters.AddWithValue("@DateOfBirth",
                    patient.DateOfBirth.HasValue
                        ? patient.DateOfBirth.Value.ToString(DateFormat, CultureInfo.InvariantCulture)
                        : DBNull.Value);
                cmd.Parameters.AddWithValue("@PregnancyStatus", pregnancy);
                cmd.Parameters.AddWithValue("@SampleCollectedAt", sampleCollectedAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@ProfileId", patient.ProfileId.HasValue ? patient.ProfileId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@PrintStatus", string.IsNullOrWhiteSpace(patient.PrintStatus) ? "NotPrinted" : patient.PrintStatus);
                cmd.Parameters.AddWithValue("@Cnic", string.IsNullOrWhiteSpace(patient.Cnic) ? DBNull.Value : patient.Cnic.Trim());

                long newId = (long)(cmd.ExecuteScalar() ?? 0L);

                string serialNumber = $"LAB-{newId:D6}";

                using (var updateCmd = new SqliteCommand(
                    "UPDATE Patients SET SerialNumber = @SerialNumber WHERE Id = @Id", connection, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                    updateCmd.Parameters.AddWithValue("@Id", newId);
                    updateCmd.ExecuteNonQuery();
                }

                var snapshot = new DemographicSnapshot
                {
                    AgeDays = ageDays,
                    AgeDisplay = ageDisplay,
                    Gender = patient.Gender ?? string.Empty,
                    Pregnancy = pregnancy,
                    AsOf = sampleCollectedAt
                };

                // Pre-resolve every selected row to its definition so we can expand panels and avoid
                // inserting the same parameter twice (e.g. "Hemoglobin (Hb)" selected alongside CBC).
                var resolved = new List<(TestResultItem Row, TestDefinition? Def)>();
                foreach (var row in results)
                {
                    TestDefinition? def = null;
                    if (row.TestDefinitionId.HasValue && row.TestDefinitionId.Value > 0)
                        def = GetTestDefinitionById(connection, transaction, row.TestDefinitionId.Value);
                    if (def == null && !string.IsNullOrWhiteSpace(row.TestName))
                        def = GetTestDefinitionByName(connection, transaction, row.TestName.Trim());
                    resolved.Add((row, def));
                }

                // A test definition is stored once. Panel package rows are separate and keep the
                // price that was on the bill, not the current catalog price.
                var insertedResultIds = new HashSet<int>();

                foreach (var (row, def) in resolved)
                {
                    bool isHeader = row.EntryRole == "PanelBill"
                        || (def != null && def.IsPanel && row.EntryRole != "PanelParameter");
                    if (!isHeader || string.IsNullOrWhiteSpace(row.TestName))
                        continue;

                    int panelId = def?.Id ?? row.PanelId ?? row.TestDefinitionId ?? 0;
                    InsertResultRow(connection, transaction, newId, snapshot,
                        categoryHeading: string.IsNullOrWhiteSpace(row.CategoryHeading) ? "General" : row.CategoryHeading,
                        testName: row.TestName,
                        unit: "",
                        legacyRange: "(panel)",
                        price: row.Price,
                        discount: row.Discount,
                        definitionId: panelId > 0 ? panelId : def?.Id,
                        isPanelMember: false,
                        ranges: new List<TestReferenceRange>(),
                        resultText: "",
                        catalogPrice: row.CatalogPrice > 0 ? row.CatalogPrice : row.Price,
                        discountMode: row.DiscountMode,
                        panelId: panelId > 0 ? panelId : row.PanelId);

                    // A package added from the bill screen already lists the tests to keep.
                    // Do not pull the current catalog back in — the user may have removed a test,
                    // and a later catalog edit must not change this bill.
                    if (row.EntryRole == "PanelBill" || def == null)
                        continue;

                    foreach (var member in GetPanelMembers(connection, transaction, def.Id).Where(m => m.IsActive && !m.IsPanel))
                    {
                        bool billedOnItsOwn = resolved.Any(x =>
                            x.Def != null && x.Def.Id == member.Id && x.Row.EntryRole == "Standard");
                        if (billedOnItsOwn || !insertedResultIds.Add(member.Id))
                            continue;

                        InsertResultRow(connection, transaction, newId, snapshot,
                            categoryHeading: string.IsNullOrWhiteSpace(member.CategoryHeading) ? def.CategoryHeading : member.CategoryHeading,
                            testName: member.TestName,
                            unit: member.Unit,
                            legacyRange: member.NormalRange,
                            price: 0m,
                            discount: 0m,
                            definitionId: member.Id,
                            isPanelMember: true,
                            ranges: GetReferenceRangesForTest(connection, transaction, member.Id),
                            catalogPrice: member.Price,
                            discountMode: "Percent",
                            panelId: def.Id);
                    }
                }

                foreach (var (row, def) in resolved)
                {
                    bool isMember = row.EntryRole == "PanelParameter" || (row.IsPanelMember && row.EntryRole != "PanelBill");
                    if (!isMember || string.IsNullOrWhiteSpace(row.TestName))
                        continue;

                    int? memberId = def?.Id ?? row.TestDefinitionId;
                    if (memberId.HasValue && !insertedResultIds.Add(memberId.Value))
                        continue;

                    var ranges = memberId.HasValue
                        ? GetReferenceRangesForTest(connection, transaction, memberId.Value)
                        : new List<TestReferenceRange>();

                    InsertResultRow(connection, transaction, newId, snapshot,
                        categoryHeading: string.IsNullOrWhiteSpace(row.CategoryHeading) ? "General" : row.CategoryHeading,
                        testName: row.TestName,
                        unit: string.IsNullOrWhiteSpace(row.Unit) ? (def?.Unit ?? string.Empty) : row.Unit,
                        legacyRange: string.IsNullOrWhiteSpace(row.NormalRange) ? (def?.NormalRange ?? "") : row.NormalRange,
                        price: 0m,
                        discount: row.Discount,
                        definitionId: memberId,
                        isPanelMember: true,
                        ranges: ranges,
                        resultText: row.Result,
                        catalogPrice: row.CatalogPrice,
                        discountMode: row.DiscountMode,
                        panelId: row.PanelId ?? def?.Id);
                }

                foreach (var (row, def) in resolved)
                {
                    if (string.IsNullOrWhiteSpace(row.TestName))
                        continue;
                    if (row.EntryRole is "PanelBill" or "PanelParameter" || row.IsPanelMember)
                        continue;
                    if (def != null && def.IsPanel)
                        continue;
                    if (def != null && !insertedResultIds.Add(def.Id))
                        continue;

                    var ranges = def != null
                        ? GetReferenceRangesForTest(connection, transaction, def.Id)
                        : new List<TestReferenceRange>();

                    string legacyRange = !string.IsNullOrWhiteSpace(row.NormalRange)
                        ? row.NormalRange
                        : def?.NormalRange ?? string.Empty;

                    InsertResultRow(connection, transaction, newId, snapshot,
                        categoryHeading: string.IsNullOrWhiteSpace(row.CategoryHeading) ? "General" : row.CategoryHeading,
                        testName: row.TestName,
                        unit: string.IsNullOrWhiteSpace(row.Unit) ? (def?.Unit ?? string.Empty) : row.Unit,
                        legacyRange: legacyRange,
                        price: row.Price,
                        discount: row.Discount,
                        definitionId: def?.Id,
                        isPanelMember: false,
                        ranges: ranges,
                        resultText: row.Result,
                        catalogPrice: row.CatalogPrice > 0 ? row.CatalogPrice : row.Price,
                        discountMode: row.DiscountMode);
                }

                transaction.Commit();
                return serialNumber;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private sealed class DemographicSnapshot
        {
            public int AgeDays { get; set; }
            public string AgeDisplay { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public string Pregnancy { get; set; } = PregnancyStatuses.NotApplicable;
            public DateTime AsOf { get; set; } = DateTime.Now;
        }

        private static string FormatAgeSnapshot(Patient normalized, double ageValue, string ageUnit, DateTime asOf)
        {
            // When a DOB is supplied derive the most natural unit; otherwise echo what was entered.
            if (normalized.DateOfBirth.HasValue)
            {
                var (value, unit) = AgeCalculator.FromDateOfBirth(normalized.DateOfBirth.Value, asOf);
                var tmp = new Patient { AgeValue = value, AgeUnit = unit };
                return AgeCalculator.FormatAgeDisplay(tmp);
            }

            return AgeCalculator.FormatAgeDisplay(new Patient { AgeValue = ageValue, AgeUnit = ageUnit });
        }

        /// <summary>
        /// Resolves the reference range for the snapshot demographics and inserts one TestResults row
        /// carrying the resolved range text and frozen demographic snapshot.
        /// </summary>
        private static void InsertResultRow(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long patientId,
            DemographicSnapshot snapshot,
            string categoryHeading,
            string testName,
            string unit,
            string legacyRange,
            decimal price,
            decimal discount,
            int? definitionId,
            bool isPanelMember,
            List<TestReferenceRange> ranges,
            string? resultText = null,
            decimal? catalogPrice = null,
            string? discountMode = null,
            int? panelId = null)
        {
            string normalRange = legacyRange ?? string.Empty;
            string finalUnit = unit ?? string.Empty;
            TestReferenceRange? matched = null;

            if (ranges.Count > 0)
            {
                var resolution = ReferenceRangeResolver.Resolve(
                    ranges, snapshot.AgeDays, snapshot.Gender, snapshot.Pregnancy, snapshot.AsOf);

                normalRange = resolution.DisplayText;
                matched = resolution.Range;

                if (matched != null && string.IsNullOrWhiteSpace(finalUnit))
                    finalUnit = matched.Unit ?? string.Empty;
            }

            using var resultCmd = new SqliteCommand(@"
                INSERT INTO TestResults (PatientId, CategoryHeading, TestName, Result, Unit, NormalRange, Price, Discount,
                                         AbnormalFlag, RangeMinValue, RangeMaxValue, IsQualitative, Methodology,
                                         AgeAtTestDays, AgeAtTestDisplay, GenderAtTest, PregnancyAtTest,
                                         TestDefinitionId, IsPanelMember, CatalogPrice, DiscountMode, PanelId)
                VALUES (@PatientId, @CategoryHeading, @TestName, @Result, @Unit, @NormalRange, @Price, @Discount,
                        @AbnormalFlag, @RangeMin, @RangeMax, @IsQualitative, @Methodology,
                        @AgeDays, @AgeDisplay, @GenderAtTest, @PregnancyAtTest,
                        @TestDefinitionId, @IsPanelMember, @CatalogPrice, @DiscountMode, @PanelId);", connection, transaction);

            resultCmd.Parameters.AddWithValue("@PatientId", patientId);
            resultCmd.Parameters.AddWithValue("@CategoryHeading", string.IsNullOrWhiteSpace(categoryHeading) ? "General" : categoryHeading);
            resultCmd.Parameters.AddWithValue("@TestName", testName ?? string.Empty);
            resultCmd.Parameters.AddWithValue("@Result", resultText ?? string.Empty);
            resultCmd.Parameters.AddWithValue("@Unit", finalUnit);
            resultCmd.Parameters.AddWithValue("@NormalRange", normalRange);
            resultCmd.Parameters.AddWithValue("@Price", price);
            resultCmd.Parameters.AddWithValue("@Discount", discount);
            resultCmd.Parameters.AddWithValue("@AbnormalFlag", string.Empty);
            resultCmd.Parameters.AddWithValue("@RangeMin", matched?.MinValue.HasValue == true ? matched.MinValue!.Value : DBNull.Value);
            resultCmd.Parameters.AddWithValue("@RangeMax", matched?.MaxValue.HasValue == true ? matched.MaxValue!.Value : DBNull.Value);
            resultCmd.Parameters.AddWithValue("@IsQualitative", matched != null && matched.IsQualitative ? 1 : 0);
            resultCmd.Parameters.AddWithValue("@Methodology", matched?.Methodology ?? string.Empty);
            resultCmd.Parameters.AddWithValue("@AgeDays", snapshot.AgeDays);
            resultCmd.Parameters.AddWithValue("@AgeDisplay", snapshot.AgeDisplay);
            resultCmd.Parameters.AddWithValue("@GenderAtTest", snapshot.Gender);
            resultCmd.Parameters.AddWithValue("@PregnancyAtTest", snapshot.Pregnancy);
            resultCmd.Parameters.AddWithValue("@TestDefinitionId", definitionId.HasValue ? definitionId.Value : DBNull.Value);
            resultCmd.Parameters.AddWithValue("@IsPanelMember", isPanelMember ? 1 : 0);
            resultCmd.Parameters.AddWithValue("@CatalogPrice", catalogPrice ?? price);
            resultCmd.Parameters.AddWithValue("@DiscountMode",
                string.Equals(discountMode, "Fixed", StringComparison.OrdinalIgnoreCase) ? "Fixed" : "Percent");
            resultCmd.Parameters.AddWithValue("@PanelId", panelId.HasValue && panelId.Value > 0 ? panelId.Value : DBNull.Value);
            resultCmd.ExecuteNonQuery();
        }

        // ==================== STAGE 2: RESULTS ENTRY / COMPLETION ====================

        /// <summary>
        /// Returns every patient who has been billed but whose report has not been completed yet.
        /// </summary>
        public static List<Patient> GetPendingPatients()
        {
            var list = new List<Patient>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                $"SELECT {PatientColumns} FROM Patients WHERE Status = 'Pending' ORDER BY Id DESC", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadPatient(reader));

            return list;
        }

        /// <summary>Returns a single patient by their database Id, or null if not found.</summary>
        public static Patient? GetPatientById(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                $"SELECT {PatientColumns} FROM Patients WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadPatient(reader) : null;
        }

        /// <summary>
        /// Writes the entered Result value (and its abnormal flag) back into each existing TestResults
        /// row (matched by Id), freezes the range/snapshot fields when they are supplied on the item,
        /// and marks the patient's Status as "Completed". Call this once the Lab Attendant has
        /// finished performing the tests and typed in the results.
        /// </summary>
        public static void SaveResultsAndComplete(int patientId, List<TestResultItem> updatedResults)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var row in updatedResults)
                {
                    // Range/snapshot fields are only frozen when the item actually carries them
                    // (a resolved NormalRange); otherwise the values stored at billing time are kept.
                    bool snapshotProvided = !string.IsNullOrWhiteSpace(row.NormalRange);

                    using var cmd = new SqliteCommand(@"
                        UPDATE TestResults SET
                            Result = @Result,
                            AbnormalFlag = @AbnormalFlag,
                            NormalRange = CASE WHEN @Provided = 1 THEN @NormalRange ELSE NormalRange END,
                            RangeMinValue = CASE WHEN @Provided = 1 THEN @RangeMin ELSE RangeMinValue END,
                            RangeMaxValue = CASE WHEN @Provided = 1 THEN @RangeMax ELSE RangeMaxValue END,
                            IsQualitative = CASE WHEN @Provided = 1 THEN @IsQualitative ELSE IsQualitative END,
                            Methodology = CASE WHEN @Provided = 1 THEN @Methodology ELSE Methodology END,
                            AgeAtTestDays = CASE WHEN @AgeDays > 0 THEN @AgeDays ELSE AgeAtTestDays END,
                            AgeAtTestDisplay = CASE WHEN @AgeDisplay <> '' THEN @AgeDisplay ELSE AgeAtTestDisplay END,
                            GenderAtTest = CASE WHEN @GenderAtTest <> '' THEN @GenderAtTest ELSE GenderAtTest END,
                            PregnancyAtTest = CASE WHEN @PregnancyAtTest <> '' THEN @PregnancyAtTest ELSE PregnancyAtTest END
                        WHERE Id = @Id AND PatientId = @PatientId", connection, transaction);

                    cmd.Parameters.AddWithValue("@Result", row.Result ?? string.Empty);
                    cmd.Parameters.AddWithValue("@AbnormalFlag", row.AbnormalFlag ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Provided", snapshotProvided ? 1 : 0);
                    cmd.Parameters.AddWithValue("@NormalRange", row.NormalRange ?? string.Empty);
                    cmd.Parameters.AddWithValue("@RangeMin", row.RangeMinValue.HasValue ? row.RangeMinValue.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@RangeMax", row.RangeMaxValue.HasValue ? row.RangeMaxValue.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsQualitative", row.IsQualitative ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Methodology", row.Methodology ?? string.Empty);
                    cmd.Parameters.AddWithValue("@AgeDays", row.AgeAtTestDays);
                    cmd.Parameters.AddWithValue("@AgeDisplay", row.AgeAtTestDisplay ?? string.Empty);
                    cmd.Parameters.AddWithValue("@GenderAtTest", row.GenderAtTest ?? string.Empty);
                    cmd.Parameters.AddWithValue("@PregnancyAtTest", row.PregnancyAtTest ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Id", row.Id);
                    cmd.Parameters.AddWithValue("@PatientId", patientId);
                    cmd.ExecuteNonQuery();
                }

                using var statusCmd = new SqliteCommand(
                    "UPDATE Patients SET Status = 'Completed' WHERE Id = @PatientId", connection, transaction);
                statusCmd.Parameters.AddWithValue("@PatientId", patientId);
                statusCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== SHARED READ HELPERS ====================

        public static List<Patient> GetAllPatients()
        {
            var list = new List<Patient>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                $"SELECT {PatientColumns} FROM Patients ORDER BY Id DESC", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadPatient(reader));

            return list;
        }

        /// <summary>Returns the ordinal of a column by name, or -1 when the result set doesn't contain it.</summary>
        private static int TryGetOrdinal(SqliteDataReader reader, string name)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string ReadString(SqliteDataReader reader, int ordinal, string fallback = "")
        {
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;
            return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? fallback;
        }

        private static double? ReadNullableDouble(SqliteDataReader reader, int ordinal)
        {
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return null;
            return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static int? ReadNullableInt(SqliteDataReader reader, int ordinal)
        {
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return null;
            return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static bool ReadBool(SqliteDataReader reader, int ordinal, bool fallback)
        {
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;
            return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
        }

        private static DateTime? ParseStoredDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
        }

        private static Patient ReadPatient(SqliteDataReader reader)
        {
            var patient = new Patient
            {
                Id = reader.GetInt32(0),
                SerialNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Gender = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ReferredBy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LabName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                PhoneNumber = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ReportHeading = reader.IsDBNull(8) ? "" : reader.GetString(8),
                CreatedAt = reader.IsDBNull(9) ? DateTime.Now : DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
                Status = reader.IsDBNull(10) || string.IsNullOrWhiteSpace(reader.GetString(10)) ? "Completed" : reader.GetString(10),
                TotalAmount = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11)),
                DiscountPercentage = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader.GetDouble(12)),
                NetAmount = reader.IsDBNull(13) ? 0 : Convert.ToDecimal(reader.GetDouble(13))
            };

            // New dynamic-reference-range columns: read by name and fall back to sensible defaults
            // when a column is missing or NULL (older databases / legacy rows).
            int ordAgeValue = TryGetOrdinal(reader, "AgeValue");
            int ordAgeUnit = TryGetOrdinal(reader, "AgeUnit");
            int ordDob = TryGetOrdinal(reader, "DateOfBirth");
            int ordPregnancy = TryGetOrdinal(reader, "PregnancyStatus");
            int ordSample = TryGetOrdinal(reader, "SampleCollectedAt");

            patient.AgeValue = ReadNullableDouble(reader, ordAgeValue) ?? patient.Age;

            string ageUnit = ReadString(reader, ordAgeUnit);
            patient.AgeUnit = string.IsNullOrWhiteSpace(ageUnit) ? AgeUnits.Years : ageUnit;

            patient.DateOfBirth = ParseStoredDate(ReadString(reader, ordDob));

            string pregnancy = ReadString(reader, ordPregnancy);
            patient.PregnancyStatus = string.IsNullOrWhiteSpace(pregnancy) ? PregnancyStatuses.NotApplicable : pregnancy;

            patient.SampleCollectedAt = ParseStoredDate(ReadString(reader, ordSample)) ?? patient.CreatedAt;

            int ordProfile = TryGetOrdinal(reader, "ProfileId");
            int ordPrint = TryGetOrdinal(reader, "PrintStatus");
            patient.ProfileId = ReadNullableInt(reader, ordProfile);
            string printStatus = ReadString(reader, ordPrint);
            patient.PrintStatus = string.IsNullOrWhiteSpace(printStatus) ? "NotPrinted" : printStatus;
            patient.Cnic = ReadString(reader, TryGetOrdinal(reader, "Cnic"));

            // Backward compatibility: bills created before the discount feature existed have
            // NetAmount = 0 in the database. Treat those as "no discount" -> Net = Total.
            if (patient.NetAmount == 0 && patient.DiscountPercentage == 0 && patient.TotalAmount > 0)
                patient.NetAmount = patient.TotalAmount;

            return patient;
        }

        /// <summary>
        /// Returns the saved test-result rows for a given patient id, including Id, CategoryHeading
        /// and Price, in the order they were inserted (so grouping by category stays consistent).
        /// </summary>
        public static List<TestResultItem> GetResultsForPatient(int patientId)
        {
            var list = new List<TestResultItem>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(@"
                SELECT Id, CategoryHeading, TestName, Result, Unit, NormalRange, Price, Discount,
                       AbnormalFlag, RangeMinValue, RangeMaxValue, IsQualitative, Methodology,
                       AgeAtTestDays, AgeAtTestDisplay, GenderAtTest, PregnancyAtTest,
                       TestDefinitionId, IsPanelMember, CatalogPrice, DiscountMode, PanelId
                FROM TestResults WHERE PatientId = @PatientId ORDER BY Id", connection);
            cmd.Parameters.AddWithValue("@PatientId", patientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bool isPanelMember = ReadBool(reader, 18, false);
                string normalRange = reader.IsDBNull(5) ? "" : reader.GetString(5);
                decimal price = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6));
                int? definitionId = ReadNullableInt(reader, 17);
                int? panelId = ReadNullableInt(reader, 21);
                string entryRole = isPanelMember
                    ? "PanelParameter"
                    : (string.Equals(normalRange, "(panel)", StringComparison.OrdinalIgnoreCase) ? "PanelBill" : "Standard");
                if (entryRole == "PanelBill" && panelId == null)
                    panelId = definitionId;

                decimal catalog = reader.IsDBNull(19) ? price : Convert.ToDecimal(reader.GetDouble(19));
                if (entryRole == "PanelParameter" && reader.IsDBNull(19))
                    catalog = 0;

                list.Add(new TestResultItem
                {
                    Id = reader.GetInt32(0),
                    CategoryHeading = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    TestName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Result = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Unit = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    NormalRange = normalRange,
                    Price = price,
                    CatalogPrice = catalog,
                    Discount = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7)),
                    DiscountMode = reader.IsDBNull(20) ? "Percent" : reader.GetString(20),
                    PanelId = panelId,
                    AbnormalFlag = ReadString(reader, 8),
                    RangeMinValue = ReadNullableDouble(reader, 9),
                    RangeMaxValue = ReadNullableDouble(reader, 10),
                    IsQualitative = ReadBool(reader, 11, false),
                    Methodology = ReadString(reader, 12),
                    AgeAtTestDays = ReadNullableInt(reader, 13) ?? 0,
                    AgeAtTestDisplay = ReadString(reader, 14),
                    GenderAtTest = ReadString(reader, 15),
                    PregnancyAtTest = ReadString(reader, 16),
                    TestDefinitionId = definitionId,
                    IsPanelMember = isPanelMember,
                    EntryRole = entryRole
                });
            }

            TestResultItem.ApplyPanelMemberDiscounts(list);
            return list;
        }

        // ==================== TEST DEFINITIONS (master data / autocomplete source) ====================

        /// <summary>
        /// Inserts a new predefined test, or updates it if a test with the same Id (or same TestName) already exists.
        /// </summary>
        public static void InsertOrUpdateTestDefinition(TestDefinition def)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            if (def.Id == 0)
            {
                // New entry. If a test with the same name already exists, update it instead (upsert).
                using var cmd = new SqliteCommand(@"
                    INSERT INTO TestDefinitions (CategoryHeading, TestName, Unit, NormalRange, Price, IsPanel, IsActive)
                    VALUES (@Category, @TestName, @Unit, @NormalRange, @Price, @IsPanel, @IsActive)
                    ON CONFLICT(TestName) DO UPDATE SET
                        CategoryHeading = excluded.CategoryHeading,
                        Unit = excluded.Unit,
                        NormalRange = excluded.NormalRange,
                        Price = excluded.Price,
                        IsPanel = excluded.IsPanel,
                        IsActive = excluded.IsActive;", connection);

                cmd.Parameters.AddWithValue("@Category", def.CategoryHeading);
                cmd.Parameters.AddWithValue("@TestName", def.TestName);
                cmd.Parameters.AddWithValue("@Unit", def.Unit);
                cmd.Parameters.AddWithValue("@NormalRange", def.NormalRange);
                cmd.Parameters.AddWithValue("@Price", def.Price);
                cmd.Parameters.AddWithValue("@IsPanel", def.IsPanel ? 1 : 0);
                cmd.Parameters.AddWithValue("@IsActive", def.IsActive ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new SqliteCommand(@"
                    UPDATE TestDefinitions
                    SET CategoryHeading = @Category, TestName = @TestName, Unit = @Unit, NormalRange = @NormalRange, Price = @Price,
                        IsPanel = @IsPanel, IsActive = @IsActive
                    WHERE Id = @Id;", connection);

                cmd.Parameters.AddWithValue("@Category", def.CategoryHeading);
                cmd.Parameters.AddWithValue("@TestName", def.TestName);
                cmd.Parameters.AddWithValue("@Unit", def.Unit);
                cmd.Parameters.AddWithValue("@NormalRange", def.NormalRange);
                cmd.Parameters.AddWithValue("@Price", def.Price);
                cmd.Parameters.AddWithValue("@IsPanel", def.IsPanel ? 1 : 0);
                cmd.Parameters.AddWithValue("@IsActive", def.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@Id", def.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public static List<TestDefinition> GetAllTestDefinitions()
        {
            var list = new List<TestDefinition>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(
                $"SELECT {TestDefinitionColumns} FROM TestDefinitions ORDER BY CategoryHeading, TestName", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadTestDefinition(reader));
            return list;
        }

        public static void DeleteTestDefinition(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Remove dependent rows first (SQLite foreign keys are not enforced by default).
                foreach (var sql in new[]
                {
                    "DELETE FROM TestReferenceRanges WHERE TestDefinitionId = @Id",
                    "DELETE FROM TestPanelMembers WHERE PanelTestDefinitionId = @Id OR MemberTestDefinitionId = @Id",
                    "DELETE FROM TestDefinitions WHERE Id = @Id"
                })
                {
                    using var cmd = new SqliteCommand(sql, connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static TestDefinition ReadTestDefinition(SqliteDataReader reader)
        {
            return new TestDefinition
            {
                Id = reader.GetInt32(0),
                CategoryHeading = reader.IsDBNull(1) ? "" : reader.GetString(1),
                TestName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Unit = reader.IsDBNull(3) ? "" : reader.GetString(3),
                NormalRange = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Price = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5)),
                IsPanel = ReadBool(reader, 6, false),
                IsActive = ReadBool(reader, 7, true)
            };
        }

        /// <summary>Case-insensitive lookup of a test definition by its exact name, or null if unknown.</summary>
        public static TestDefinition? GetTestDefinitionByName(string name)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return GetTestDefinitionByName(connection, null, name);
        }

        public static TestDefinition? GetTestDefinitionById(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return GetTestDefinitionById(connection, null, id);
        }

        private static TestDefinition? GetTestDefinitionByName(SqliteConnection connection, SqliteTransaction? transaction, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            using var cmd = new SqliteCommand(
                $"SELECT {TestDefinitionColumns} FROM TestDefinitions WHERE TestName = @Name COLLATE NOCASE LIMIT 1",
                connection, transaction);
            cmd.Parameters.AddWithValue("@Name", name.Trim());

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadTestDefinition(reader) : null;
        }

        private static TestDefinition? GetTestDefinitionById(SqliteConnection connection, SqliteTransaction? transaction, int id)
        {
            using var cmd = new SqliteCommand(
                $"SELECT {TestDefinitionColumns} FROM TestDefinitions WHERE Id = @Id",
                connection, transaction);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadTestDefinition(reader) : null;
        }

        // ==================== PANEL MEMBERS ====================

        /// <summary>Returns the member tests of a panel in their configured display order.</summary>
        public static List<TestDefinition> GetPanelMembers(int panelTestDefinitionId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return GetPanelMembers(connection, null, panelTestDefinitionId);
        }

        private static List<TestDefinition> GetPanelMembers(SqliteConnection connection, SqliteTransaction? transaction, int panelId)
        {
            var list = new List<TestDefinition>();
            using var cmd = new SqliteCommand(@"
                SELECT d.Id, d.CategoryHeading, d.TestName, d.Unit, d.NormalRange, d.Price, d.IsPanel, d.IsActive
                FROM TestPanelMembers m
                INNER JOIN TestDefinitions d ON d.Id = m.MemberTestDefinitionId
                WHERE m.PanelTestDefinitionId = @PanelId
                ORDER BY m.SortOrder, m.Id", connection, transaction);
            cmd.Parameters.AddWithValue("@PanelId", panelId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadTestDefinition(reader));
            return list;
        }

        /// <summary>Replaces the panel's members with the given test ids, preserving the supplied order.</summary>
        public static void SetPanelMembers(int panelId, List<int> memberIdsInOrder)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                using (var delCmd = new SqliteCommand(
                    "DELETE FROM TestPanelMembers WHERE PanelTestDefinitionId = @PanelId", connection, transaction))
                {
                    delCmd.Parameters.AddWithValue("@PanelId", panelId);
                    delCmd.ExecuteNonQuery();
                }

                var seen = new HashSet<int>();
                int sort = 0;
                foreach (int memberId in memberIdsInOrder ?? new List<int>())
                {
                    if (memberId == panelId || !seen.Add(memberId))
                        continue;

                    using var insertCmd = new SqliteCommand(@"
                        INSERT INTO TestPanelMembers (PanelTestDefinitionId, MemberTestDefinitionId, SortOrder)
                        VALUES (@PanelId, @MemberId, @Sort);", connection, transaction);
                    insertCmd.Parameters.AddWithValue("@PanelId", panelId);
                    insertCmd.Parameters.AddWithValue("@MemberId", memberId);
                    insertCmd.Parameters.AddWithValue("@Sort", sort++);
                    insertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== REFERENCE RANGES ====================

        /// <summary>Returns every configured reference range (active or not) for the given test.</summary>
        public static List<TestReferenceRange> GetReferenceRangesForTest(int testDefinitionId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return GetReferenceRangesForTest(connection, null, testDefinitionId);
        }

        private static List<TestReferenceRange> GetReferenceRangesForTest(SqliteConnection connection, SqliteTransaction? transaction, int testDefinitionId)
        {
            var list = new List<TestReferenceRange>();
            using var cmd = new SqliteCommand(
                $"SELECT {RangeColumns} FROM TestReferenceRanges WHERE TestDefinitionId = @TestId ORDER BY AgeMinDays, Gender, PregnancyStatus, Id",
                connection, transaction);
            cmd.Parameters.AddWithValue("@TestId", testDefinitionId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadRange(reader));
            return list;
        }

        private static TestReferenceRange ReadRange(SqliteDataReader reader)
        {
            string gender = ReadString(reader, 4);
            string pregnancy = ReadString(reader, 5);

            return new TestReferenceRange
            {
                Id = reader.GetInt32(0),
                TestDefinitionId = reader.GetInt32(1),
                AgeMinDays = ReadNullableInt(reader, 2) ?? 0,
                AgeMaxDays = ReadNullableInt(reader, 3),
                Gender = string.IsNullOrWhiteSpace(gender) ? RangeGenders.Both : gender,
                PregnancyStatus = string.IsNullOrWhiteSpace(pregnancy) ? PregnancyStatuses.NotApplicable : pregnancy,
                MinValue = ReadNullableDouble(reader, 6),
                MaxValue = ReadNullableDouble(reader, 7),
                ReferenceText = ReadString(reader, 8),
                Unit = ReadString(reader, 9),
                Methodology = ReadString(reader, 10),
                EffectiveFrom = ParseStoredDate(ReadString(reader, 12)),
                EffectiveTo = ParseStoredDate(ReadString(reader, 13)),
                IsActive = ReadBool(reader, 14, true),
                IsIllustrative = ReadBool(reader, 15, true)
            };
        }

        /// <summary>Inserts the range when Id is 0, otherwise updates the existing row with that Id.</summary>
        public static void InsertOrUpdateReferenceRange(TestReferenceRange range) => SaveReferenceRange(range);

        public static void SaveReferenceRange(TestReferenceRange range)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string text = string.IsNullOrWhiteSpace(range.ReferenceText)
                ? BuildRangeText(range.MinValue, range.MaxValue)
                : range.ReferenceText.Trim();

            if (range.Id == 0)
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO TestReferenceRanges
                        (TestDefinitionId, AgeMinDays, AgeMaxDays, Gender, PregnancyStatus, MinValue, MaxValue, ReferenceText,
                         Unit, Methodology, IsQualitative, EffectiveFrom, EffectiveTo, IsActive, IsIllustrative)
                    VALUES
                        (@TestId, @AgeMin, @AgeMax, @Gender, @Pregnancy, @Min, @Max, @Text,
                         @Unit, @Methodology, @IsQualitative, @From, @To, @IsActive, @IsIllustrative);
                    SELECT last_insert_rowid();", connection);
                BindRangeParameters(cmd, range, text);
                range.Id = Convert.ToInt32(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
            }
            else
            {
                using var cmd = new SqliteCommand(@"
                    UPDATE TestReferenceRanges SET
                        TestDefinitionId = @TestId, AgeMinDays = @AgeMin, AgeMaxDays = @AgeMax, Gender = @Gender,
                        PregnancyStatus = @Pregnancy, MinValue = @Min, MaxValue = @Max, ReferenceText = @Text,
                        Unit = @Unit, Methodology = @Methodology, IsQualitative = @IsQualitative,
                        EffectiveFrom = @From, EffectiveTo = @To, IsActive = @IsActive, IsIllustrative = @IsIllustrative
                    WHERE Id = @Id;", connection);
                BindRangeParameters(cmd, range, text);
                cmd.Parameters.AddWithValue("@Id", range.Id);
                cmd.ExecuteNonQuery();
            }
        }

        private static void BindRangeParameters(SqliteCommand cmd, TestReferenceRange range, string referenceText)
        {
            cmd.Parameters.AddWithValue("@TestId", range.TestDefinitionId);
            cmd.Parameters.AddWithValue("@AgeMin", range.AgeMinDays ?? 0);
            cmd.Parameters.AddWithValue("@AgeMax", range.AgeMaxDays.HasValue ? range.AgeMaxDays.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", string.IsNullOrWhiteSpace(range.Gender) ? RangeGenders.Both : range.Gender);
            cmd.Parameters.AddWithValue("@Pregnancy", string.IsNullOrWhiteSpace(range.PregnancyStatus) ? PregnancyStatuses.NotApplicable : range.PregnancyStatus);
            cmd.Parameters.AddWithValue("@Min", range.MinValue.HasValue ? range.MinValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Max", range.MaxValue.HasValue ? range.MaxValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Text", referenceText);
            cmd.Parameters.AddWithValue("@Unit", range.Unit ?? string.Empty);
            cmd.Parameters.AddWithValue("@Methodology", range.Methodology ?? string.Empty);
            cmd.Parameters.AddWithValue("@IsQualitative", range.IsQualitative ? 1 : 0);
            cmd.Parameters.AddWithValue("@From", range.EffectiveFrom.HasValue
                ? range.EffectiveFrom.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@To", range.EffectiveTo.HasValue
                ? range.EffectiveTo.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", range.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@IsIllustrative", range.IsIllustrative ? 1 : 0);
        }

        public static void DeleteReferenceRange(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("DELETE FROM TestReferenceRanges WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        private static string FormatNumber(double value) =>
            value.ToString("0.####", CultureInfo.InvariantCulture);

        private static string BuildRangeText(double? min, double? max)
        {
            if (min.HasValue && max.HasValue) return $"{FormatNumber(min.Value)}-{FormatNumber(max.Value)}";
            if (min.HasValue) return $">= {FormatNumber(min.Value)}";
            if (max.HasValue) return $"<= {FormatNumber(max.Value)}";
            return string.Empty;
        }

        // ==================== REFERENCE RANGE CSV IMPORT / EXPORT ====================

        private static readonly string[] RangeCsvHeader =
        {
            "TestName", "AgeMinDays", "AgeMaxDays", "Gender", "PregnancyStatus",
            "MinValue", "MaxValue", "ReferenceText", "Unit", "Methodology", "IsQualitative"
        };

        /// <summary>
        /// Upserts reference ranges from a CSV with columns:
        /// TestName,AgeMinDays,AgeMaxDays,Gender,PregnancyStatus,MinValue,MaxValue,ReferenceText,Unit,Methodology,IsQualitative.
        /// A row matches an existing range by (test, age min, age max, gender, pregnancy status).
        /// Rows whose TestName is unknown are skipped.
        /// </summary>
        public static void ImportReferenceRangesFromCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A CSV file path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Reference range CSV file not found.", path);

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                ImportReferenceRangesCore(connection, transaction, path);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static void ImportReferenceRangesCore(SqliteConnection connection, SqliteTransaction transaction, string path)
        {
            var testIdCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                var f = ParseCsvFields(line);
                if (f.Count < 8)
                    continue;

                string testName = f[0].Trim();
                if (testName.Length == 0 || string.Equals(testName, "TestName", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!testIdCache.TryGetValue(testName, out int? testId))
                {
                    testId = GetTestDefinitionByName(connection, transaction, testName)?.Id;
                    testIdCache[testName] = testId;
                }
                if (!testId.HasValue)
                    continue; // unknown test name

                string Field(int i) => i < f.Count ? f[i].Trim() : string.Empty;

                int ageMin = ParseNullableInt(Field(1)) ?? 0;
                int? ageMax = ParseNullableInt(Field(2));
                string gender = NormalizeGender(Field(3));
                string pregnancy = NormalizePregnancy(Field(4));
                double? min = ParseNullableDouble(Field(5));
                double? max = ParseNullableDouble(Field(6));
                string text = Field(7);
                string unit = Field(8);
                string methodology = Field(9);
                string qualText = Field(10);

                if (text.Length == 0)
                    text = BuildRangeText(min, max);

                bool isQualitative = (!min.HasValue && !max.HasValue) || IsTruthy(qualText);
                bool isIllustrative = methodology.IndexOf("ILLUSTRATIVE", StringComparison.OrdinalIgnoreCase) >= 0;

                long existingId = 0;
                using (var findCmd = new SqliteCommand(@"
                    SELECT Id FROM TestReferenceRanges
                    WHERE TestDefinitionId = @TestId AND AgeMinDays = @AgeMin AND AgeMaxDays IS @AgeMax
                      AND Gender = @Gender AND PregnancyStatus = @Pregnancy
                    ORDER BY Id LIMIT 1;", connection, transaction))
                {
                    findCmd.Parameters.AddWithValue("@TestId", testId.Value);
                    findCmd.Parameters.AddWithValue("@AgeMin", ageMin);
                    findCmd.Parameters.AddWithValue("@AgeMax", ageMax.HasValue ? ageMax.Value : DBNull.Value);
                    findCmd.Parameters.AddWithValue("@Gender", gender);
                    findCmd.Parameters.AddWithValue("@Pregnancy", pregnancy);
                    var found = findCmd.ExecuteScalar();
                    if (found != null && found != DBNull.Value)
                        existingId = Convert.ToInt64(found, CultureInfo.InvariantCulture);
                }

                string sql = existingId > 0
                    ? @"UPDATE TestReferenceRanges SET
                            MinValue = @Min, MaxValue = @Max, ReferenceText = @Text, Unit = @Unit, Methodology = @Methodology,
                            IsQualitative = @IsQualitative, IsActive = 1, IsIllustrative = @IsIllustrative
                        WHERE Id = @Id;"
                    : @"INSERT INTO TestReferenceRanges
                            (TestDefinitionId, AgeMinDays, AgeMaxDays, Gender, PregnancyStatus, MinValue, MaxValue, ReferenceText,
                             Unit, Methodology, IsQualitative, IsActive, IsIllustrative)
                        VALUES
                            (@TestId, @AgeMin, @AgeMax, @Gender, @Pregnancy, @Min, @Max, @Text,
                             @Unit, @Methodology, @IsQualitative, 1, @IsIllustrative);";

                using var cmd = new SqliteCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Min", min.HasValue ? min.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Max", max.HasValue ? max.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Text", text);
                cmd.Parameters.AddWithValue("@Unit", unit);
                cmd.Parameters.AddWithValue("@Methodology", methodology);
                cmd.Parameters.AddWithValue("@IsQualitative", isQualitative ? 1 : 0);
                cmd.Parameters.AddWithValue("@IsIllustrative", isIllustrative ? 1 : 0);
                if (existingId > 0)
                {
                    cmd.Parameters.AddWithValue("@Id", existingId);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@TestId", testId.Value);
                    cmd.Parameters.AddWithValue("@AgeMin", ageMin);
                    cmd.Parameters.AddWithValue("@AgeMax", ageMax.HasValue ? ageMax.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", gender);
                    cmd.Parameters.AddWithValue("@Pregnancy", pregnancy);
                }
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Writes every reference range to a CSV that ImportReferenceRangesFromCsv can read back.</summary>
        public static void ExportReferenceRangesToCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A CSV file path is required.", nameof(path));

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(@"
                SELECT d.TestName, r.AgeMinDays, r.AgeMaxDays, r.Gender, r.PregnancyStatus, r.MinValue, r.MaxValue,
                       r.ReferenceText, r.Unit, r.Methodology, r.IsQualitative
                FROM TestReferenceRanges r
                INNER JOIN TestDefinitions d ON d.Id = r.TestDefinitionId
                ORDER BY d.TestName, r.AgeMinDays, r.Gender, r.PregnancyStatus, r.Id;", connection);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", RangeCsvHeader));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var fields = new[]
                {
                    ReadString(reader, 0),
                    ReadString(reader, 1, "0"),
                    ReadString(reader, 2),
                    ReadString(reader, 3, RangeGenders.Both),
                    ReadString(reader, 4, PregnancyStatuses.NotApplicable),
                    reader.IsDBNull(5) ? "" : FormatNumber(Convert.ToDouble(reader.GetValue(5), CultureInfo.InvariantCulture)),
                    reader.IsDBNull(6) ? "" : FormatNumber(Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture)),
                    ReadString(reader, 7),
                    ReadString(reader, 8),
                    ReadString(reader, 9),
                    ReadBool(reader, 10, false) ? "1" : "0"
                };
                sb.AppendLine(string.Join(",", fields.Select(CsvEscape)));
            }

            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // UTF-8 with BOM so Excel opens it correctly.
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string CsvEscape(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static int? ParseNullableInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return (int)Math.Round(d);
            return null;
        }

        private static double? ParseNullableDouble(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return double.TryParse(text.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : null;
        }

        private static bool IsTruthy(string text) =>
            text.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("y", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeGender(string text)
        {
            if (text.Equals(RangeGenders.Male, StringComparison.OrdinalIgnoreCase) || text.Equals("M", StringComparison.OrdinalIgnoreCase))
                return RangeGenders.Male;
            if (text.Equals(RangeGenders.Female, StringComparison.OrdinalIgnoreCase) || text.Equals("F", StringComparison.OrdinalIgnoreCase))
                return RangeGenders.Female;
            return RangeGenders.Both;
        }

        private static string NormalizePregnancy(string text)
        {
            if (text.Equals(PregnancyStatuses.Pregnant, StringComparison.OrdinalIgnoreCase))
                return PregnancyStatuses.Pregnant;
            if (text.Equals(PregnancyStatuses.NonPregnant, StringComparison.OrdinalIgnoreCase) ||
                text.Equals("NonPregnant", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Not Pregnant", StringComparison.OrdinalIgnoreCase))
                return PregnancyStatuses.NonPregnant;
            return PregnancyStatuses.NotApplicable;
        }

        // ==================== PATIENT PROFILES (reusable registry) ====================

        public static List<PatientProfile> SearchPatientProfiles(string? query, int limit = 80)
        {
            var list = new List<PatientProfile>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql;
            if (string.IsNullOrWhiteSpace(query))
            {
                sql = @"SELECT Id, MrNumber, Name, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus,
                               ReferredBy, LabName, CreatedAt, UpdatedAt, IFNULL(Cnic,'')
                        FROM PatientProfiles ORDER BY datetime(UpdatedAt) DESC LIMIT @Limit";
            }
            else
            {
                sql = @"SELECT Id, MrNumber, Name, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus,
                               ReferredBy, LabName, CreatedAt, UpdatedAt, IFNULL(Cnic,'')
                        FROM PatientProfiles
                        WHERE Name LIKE @Q OR PhoneNumber LIKE @Q OR MrNumber LIKE @Q OR IFNULL(ReferredBy,'') LIKE @Q
                           OR IFNULL(Cnic,'') LIKE @Q OR IFNULL(CnicKey,'') LIKE @Q
                        ORDER BY datetime(UpdatedAt) DESC LIMIT @Limit";
            }

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Limit", limit);
            if (!string.IsNullOrWhiteSpace(query))
                cmd.Parameters.AddWithValue("@Q", "%" + query.Trim() + "%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadProfile(reader));
            return list;
        }

        public static PatientProfile? GetPatientProfileById(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand(@"
                SELECT Id, MrNumber, Name, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus,
                       ReferredBy, LabName, CreatedAt, UpdatedAt, IFNULL(Cnic,'')
                FROM PatientProfiles WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadProfile(reader) : null;
        }

        public static PatientProfile SavePatientProfile(PatientProfile profile)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string now = DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            string ageUnit = string.IsNullOrWhiteSpace(profile.AgeUnit) ? AgeUnits.Years : profile.AgeUnit;
            string pregnancy = string.IsNullOrWhiteSpace(profile.PregnancyStatus)
                ? PregnancyStatuses.NotApplicable : profile.PregnancyStatus;

            if (!Cnic.TryNormalize(profile.Cnic, out string cnicKey, out string cnicFormatted, out string cnicError))
                throw new InvalidOperationException(cnicError);

            string? takenBy = FindProfileNameByCnic(connection, cnicKey, profile.Id);
            if (takenBy != null)
                throw new InvalidOperationException($"This CNIC is already registered to {takenBy}.");

            profile.Cnic = cnicFormatted;

            if (profile.Id == 0)
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO PatientProfiles (Name, PhoneNumber, Cnic, CnicKey, AgeValue, AgeUnit, DateOfBirth, Gender, PregnancyStatus,
                                                 ReferredBy, LabName, CreatedAt, UpdatedAt)
                    VALUES (@Name, @Phone, @Cnic, @CnicKey, @AgeValue, @AgeUnit, @Dob, @Gender, @Pregnancy, @Referred, @Lab, @Created, @Updated);
                    SELECT last_insert_rowid();", connection);
                BindProfile(cmd, profile, ageUnit, pregnancy, now, now);
                profile.Id = Convert.ToInt32(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
                profile.MrNumber = $"PAT-{profile.Id:D6}";
                profile.CreatedAt = DateTime.Now;
                profile.UpdatedAt = DateTime.Now;
                using var upd = new SqliteCommand("UPDATE PatientProfiles SET MrNumber = @Mr WHERE Id = @Id", connection);
                upd.Parameters.AddWithValue("@Mr", profile.MrNumber);
                upd.Parameters.AddWithValue("@Id", profile.Id);
                upd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new SqliteCommand(@"
                    UPDATE PatientProfiles SET
                        Name=@Name, PhoneNumber=@Phone, Cnic=@Cnic, CnicKey=@CnicKey, AgeValue=@AgeValue, AgeUnit=@AgeUnit, DateOfBirth=@Dob,
                        Gender=@Gender, PregnancyStatus=@Pregnancy, ReferredBy=@Referred, LabName=@Lab, UpdatedAt=@Updated
                    WHERE Id=@Id", connection);
                BindProfile(cmd, profile, ageUnit, pregnancy, now, now);
                cmd.Parameters.AddWithValue("@Id", profile.Id);
                cmd.ExecuteNonQuery();
                profile.UpdatedAt = DateTime.Now;
            }

            EnsureDoctorExists(profile.ReferredBy);
            return profile;
        }

        private static void BindProfile(SqliteCommand cmd, PatientProfile profile, string ageUnit, string pregnancy, string created, string updated)
        {
            cmd.Parameters.AddWithValue("@Name", profile.Name.Trim());
            cmd.Parameters.AddWithValue("@Phone", profile.PhoneNumber?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Cnic", profile.Cnic?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@CnicKey",
                Cnic.TryNormalize(profile.Cnic, out string key, out _, out _) ? key : DBNull.Value);
            cmd.Parameters.AddWithValue("@AgeValue", profile.AgeValue);
            cmd.Parameters.AddWithValue("@AgeUnit", ageUnit);
            cmd.Parameters.AddWithValue("@Dob", profile.DateOfBirth.HasValue
                ? profile.DateOfBirth.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", profile.Gender ?? "");
            cmd.Parameters.AddWithValue("@Pregnancy", pregnancy);
            cmd.Parameters.AddWithValue("@Referred", profile.ReferredBy?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Lab", profile.LabName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Created", created);
            cmd.Parameters.AddWithValue("@Updated", updated);
        }

        private static PatientProfile ReadProfile(SqliteDataReader reader)
        {
            return new PatientProfile
            {
                Id = reader.GetInt32(0),
                MrNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                PhoneNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AgeValue = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                AgeUnit = reader.IsDBNull(5) || string.IsNullOrWhiteSpace(reader.GetString(5)) ? AgeUnits.Years : reader.GetString(5),
                DateOfBirth = ParseStoredDate(reader.IsDBNull(6) ? "" : reader.GetString(6)),
                Gender = reader.IsDBNull(7) ? "" : reader.GetString(7),
                PregnancyStatus = reader.IsDBNull(8) || string.IsNullOrWhiteSpace(reader.GetString(8))
                    ? PregnancyStatuses.NotApplicable : reader.GetString(8),
                ReferredBy = reader.IsDBNull(9) ? "" : reader.GetString(9),
                LabName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                CreatedAt = ParseStoredDate(reader.IsDBNull(11) ? "" : reader.GetString(11)) ?? DateTime.Now,
                UpdatedAt = ParseStoredDate(reader.IsDBNull(12) ? "" : reader.GetString(12)) ?? DateTime.Now,
                Cnic = reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : ""
            };
        }

        private static string? FindProfileNameByCnic(SqliteConnection connection, string cnicKey, int exceptId)
        {
            using var cmd = new SqliteCommand(@"
                SELECT Name FROM PatientProfiles
                WHERE CnicKey = @Key AND Id <> @Id
                LIMIT 1", connection);
            cmd.Parameters.AddWithValue("@Key", cnicKey);
            cmd.Parameters.AddWithValue("@Id", exceptId);
            object? name = cmd.ExecuteScalar();
            return name == null || name == DBNull.Value ? null : name.ToString();
        }

        public static string GetVisitTestSummary(int patientId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            // Prefer billed (non-member) rows when present; otherwise list parameter names
            // (panels expand to members only at save time).
            using var cmd = new SqliteCommand(@"
                SELECT GROUP_CONCAT(TestName, ', ')
                FROM (
                    SELECT DISTINCT TestName FROM TestResults
                    WHERE PatientId = @Id AND IFNULL(IsPanelMember, 0) = 0 AND IFNULL(TestName,'') <> ''
                    UNION ALL
                    SELECT DISTINCT TestName FROM TestResults
                    WHERE PatientId = @Id AND IFNULL(TestName,'') <> ''
                      AND NOT EXISTS (
                          SELECT 1 FROM TestResults x
                          WHERE x.PatientId = @Id AND IFNULL(x.IsPanelMember, 0) = 0 AND IFNULL(x.TestName,'') <> ''
                      )
                )", connection);
            cmd.Parameters.AddWithValue("@Id", patientId);
            string? summary = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrWhiteSpace(summary))
                return summary;

            // Final fallback: any test names on the visit.
            using var fallback = new SqliteCommand(@"
                SELECT GROUP_CONCAT(TestName, ', ') FROM (
                    SELECT DISTINCT TestName FROM TestResults
                    WHERE PatientId = @Id AND IFNULL(TestName,'') <> '' LIMIT 8
                )", connection);
            fallback.Parameters.AddWithValue("@Id", patientId);
            return fallback.ExecuteScalar()?.ToString() ?? "";
        }

        public static List<VisitDashboardItem> GetPendingVisitDashboardItems(
            string? search = null,
            DateTime? from = null,
            DateTime? to = null,
            string? doctor = null,
            string? patientName = null)
        {
            return BuildVisitDashboard(
                status: "Pending",
                search: search,
                from: from,
                to: to,
                doctor: doctor,
                patientName: patientName,
                unprintedOnly: false);
        }

        public static List<VisitDashboardItem> GetUnprintedCompletedVisits(
            string? search = null,
            DateTime? from = null,
            DateTime? to = null,
            string? doctor = null,
            string? patientName = null,
            bool unprintedOnly = false)
        {
            return BuildVisitDashboard(
                status: "Completed",
                search: search,
                from: from,
                to: to,
                doctor: doctor,
                patientName: patientName,
                unprintedOnly: unprintedOnly);
        }

        public static List<VisitDashboardItem> GetBillHistory(
            DateTime? from = null,
            DateTime? to = null,
            string? search = null,
            string? doctor = null)
        {
            return BuildVisitDashboard(
                status: null,
                search: search,
                from: from,
                to: to,
                doctor: doctor,
                patientName: null,
                unprintedOnly: false);
        }

        private static List<VisitDashboardItem> BuildVisitDashboard(
            string? status,
            string? search,
            DateTime? from,
            DateTime? to,
            string? doctor,
            string? patientName,
            bool unprintedOnly)
        {
            var list = new List<VisitDashboardItem>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var sql = new StringBuilder(@"
                SELECT p.Id, p.SerialNumber, p.Name, IFNULL(pr.MrNumber,''), p.PhoneNumber,
                       IFNULL(p.ReferredBy,''), p.CreatedAt, p.Status,
                       IFNULL(p.PrintStatus,'NotPrinted'),
                       IFNULL(p.TotalAmount,0), IFNULL(p.DiscountPercentage,0), IFNULL(p.NetAmount,0),
                       p.RefundedAt
                FROM Patients p
                LEFT JOIN PatientProfiles pr ON pr.Id = p.ProfileId
                WHERE 1 = 1");

            if (!string.IsNullOrWhiteSpace(status))
                sql.Append(" AND p.Status = @Status");

            if (unprintedOnly)
                sql.Append(" AND (p.PrintStatus IS NULL OR p.PrintStatus = '' OR p.PrintStatus = 'NotPrinted')");

            if (from.HasValue)
                sql.Append(" AND date(p.CreatedAt) >= date(@From)");
            if (to.HasValue)
                sql.Append(" AND date(p.CreatedAt) <= date(@To)");
            if (!string.IsNullOrWhiteSpace(doctor))
                sql.Append(" AND IFNULL(p.ReferredBy,'') LIKE @Doctor");
            if (!string.IsNullOrWhiteSpace(patientName))
                sql.Append(" AND (p.Name LIKE @Patient OR IFNULL(pr.MrNumber,'') LIKE @Patient OR IFNULL(p.PhoneNumber,'') LIKE @Patient OR IFNULL(p.Cnic,'') LIKE @Patient OR IFNULL(pr.Cnic,'') LIKE @Patient)");
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql.Append(@" AND (
                    p.SerialNumber LIKE @Q OR p.Name LIKE @Q OR IFNULL(p.PhoneNumber,'') LIKE @Q
                    OR IFNULL(p.Cnic,'') LIKE @Q OR IFNULL(pr.Cnic,'') LIKE @Q
                    OR IFNULL(pr.MrNumber,'') LIKE @Q OR IFNULL(p.ReferredBy,'') LIKE @Q
                    OR EXISTS (SELECT 1 FROM TestResults t WHERE t.PatientId = p.Id AND t.TestName LIKE @Q)
                )");
            }

            sql.Append(" ORDER BY p.Id DESC");

            using var cmd = new SqliteCommand(sql.ToString(), connection);
            if (!string.IsNullOrWhiteSpace(status))
                cmd.Parameters.AddWithValue("@Status", status);
            if (from.HasValue)
                cmd.Parameters.AddWithValue("@From", from.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (to.HasValue)
                cmd.Parameters.AddWithValue("@To", to.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(doctor))
                cmd.Parameters.AddWithValue("@Doctor", "%" + doctor.Trim() + "%");
            if (!string.IsNullOrWhiteSpace(patientName))
                cmd.Parameters.AddWithValue("@Patient", "%" + patientName.Trim() + "%");
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@Q", "%" + search.Trim() + "%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int visitId = reader.GetInt32(0);
                list.Add(new VisitDashboardItem
                {
                    VisitId = visitId,
                    SerialNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PatientName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MrNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ReferredBy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    CreatedAt = reader.IsDBNull(6) ? DateTime.Now : DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
                    Status = reader.IsDBNull(7) ? (status ?? "") : reader.GetString(7),
                    PrintStatus = reader.IsDBNull(8) ? "NotPrinted" : reader.GetString(8),
                    TotalAmount = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9)),
                    DiscountPercentage = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader.GetDouble(10)),
                    NetAmount = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11)),
                    RefundedAt = reader.IsDBNull(12) ? null : ParseStoredDate(reader.GetString(12)),
                    TestSummary = GetVisitTestSummary(visitId)
                });
            }
            return list;
        }

        public static bool RefundPendingVisit(int visitId, out string error)
        {
            error = "";
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using (var check = new SqliteCommand(
                "SELECT Status, SerialNumber FROM Patients WHERE Id = @Id", connection))
            {
                check.Parameters.AddWithValue("@Id", visitId);
                using var reader = check.ExecuteReader();
                if (!reader.Read())
                {
                    error = "Bill not found.";
                    return false;
                }

                string status = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    error = "Only bills that are still awaiting results can be refunded.";
                    return false;
                }
            }

            using var cmd = new SqliteCommand(@"
                UPDATE Patients
                SET Status = 'Refunded',
                    RefundedAt = @RefundedAt,
                    NetAmount = 0
                WHERE Id = @Id AND Status = 'Pending'", connection);
            cmd.Parameters.AddWithValue("@Id", visitId);
            cmd.Parameters.AddWithValue("@RefundedAt", DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                error = "Could not refund this bill. It may have already been completed or refunded.";
                return false;
            }
            return true;
        }

        public static void MarkVisitPrinted(int visitId)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand(
                "UPDATE Patients SET PrintStatus = 'Printed' WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", visitId);
            cmd.ExecuteNonQuery();
        }

        // ==================== DOCTORS ====================

        public static List<Doctor> GetDoctors(bool activeOnly = true)
        {
            var list = new List<Doctor>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            string sql = activeOnly
                ? "SELECT Id, Name, Specialty, Phone, Clinic, IsActive, CreatedAt FROM Doctors WHERE IFNULL(IsActive,1)=1 ORDER BY Name COLLATE NOCASE"
                : "SELECT Id, Name, Specialty, Phone, Clinic, IsActive, CreatedAt FROM Doctors ORDER BY Name COLLATE NOCASE";
            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadDoctor(reader));
            return list;
        }

        public static Doctor SaveDoctor(Doctor doctor)
        {
            if (string.IsNullOrWhiteSpace(doctor.Name))
                throw new ArgumentException("Doctor name is required.");

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            if (doctor.Id > 0)
            {
                using var update = new SqliteCommand(@"
                    UPDATE Doctors
                    SET Name=@Name, Specialty=@Specialty, Phone=@Phone, Clinic=@Clinic, IsActive=@Active
                    WHERE Id=@Id", connection);
                update.Parameters.AddWithValue("@Id", doctor.Id);
                update.Parameters.AddWithValue("@Name", doctor.Name.Trim());
                update.Parameters.AddWithValue("@Specialty", doctor.Specialty?.Trim() ?? "");
                update.Parameters.AddWithValue("@Phone", doctor.Phone?.Trim() ?? "");
                update.Parameters.AddWithValue("@Clinic", doctor.Clinic?.Trim() ?? "");
                update.Parameters.AddWithValue("@Active", doctor.IsActive ? 1 : 0);
                update.ExecuteNonQuery();
                return doctor;
            }

            using var insert = new SqliteCommand(@"
                INSERT INTO Doctors (Name, Specialty, Phone, Clinic, IsActive, CreatedAt)
                VALUES (@Name, @Specialty, @Phone, @Clinic, @Active, @Created);
                SELECT last_insert_rowid();", connection);
            insert.Parameters.AddWithValue("@Name", doctor.Name.Trim());
            insert.Parameters.AddWithValue("@Specialty", doctor.Specialty?.Trim() ?? "");
            insert.Parameters.AddWithValue("@Phone", doctor.Phone?.Trim() ?? "");
            insert.Parameters.AddWithValue("@Clinic", doctor.Clinic?.Trim() ?? "");
            insert.Parameters.AddWithValue("@Active", doctor.IsActive ? 1 : 0);
            insert.Parameters.AddWithValue("@Created", DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            doctor.Id = Convert.ToInt32(insert.ExecuteScalar());
            return doctor;
        }

        public static void DeleteDoctor(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand("DELETE FROM Doctors WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public static void EnsureDoctorExists(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = new SqliteCommand(@"
                INSERT OR IGNORE INTO Doctors (Name, Specialty, Phone, Clinic, IsActive, CreatedAt)
                VALUES (@Name, '', '', '', 1, @Created)", connection);
            cmd.Parameters.AddWithValue("@Name", name.Trim());
            cmd.Parameters.AddWithValue("@Created", DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        private static Doctor ReadDoctor(SqliteDataReader reader)
        {
            return new Doctor
            {
                Id = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Specialty = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Clinic = reader.IsDBNull(4) ? "" : reader.GetString(4),
                IsActive = reader.IsDBNull(5) || Convert.ToInt64(reader.GetValue(5)) != 0,
                CreatedAt = reader.IsDBNull(6)
                    ? DateTime.Now
                    : (ParseStoredDate(reader.GetString(6)) ?? DateTime.Now)
            };
        }

        // ==================== HELD BILLS (park draft, do emergency bill, resume later) ====================

        public static int SaveHeldBill(HeldBill bill)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO HeldBills
                        (ProfileId, PatientName, MrNumber, PhoneNumber, Cnic, AgeValue, AgeUnit, DateOfBirth, Gender,
                         PregnancyStatus, ReferredBy, LabName, OverallDiscount, Note, TestSummary, HeldAt)
                    VALUES
                        (@ProfileId, @Name, @Mr, @Phone, @Cnic, @AgeValue, @AgeUnit, @Dob, @Gender,
                         @Pregnancy, @Referred, @Lab, @Discount, @Note, @Summary, @HeldAt);
                    SELECT last_insert_rowid();", connection, transaction);

                cmd.Parameters.AddWithValue("@ProfileId", bill.ProfileId.HasValue ? bill.ProfileId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Name", bill.PatientName ?? "");
                cmd.Parameters.AddWithValue("@Mr", bill.MrNumber ?? "");
                cmd.Parameters.AddWithValue("@Phone", bill.PhoneNumber ?? "");
                cmd.Parameters.AddWithValue("@Cnic", bill.Cnic ?? "");
                cmd.Parameters.AddWithValue("@AgeValue", bill.AgeValue);
                cmd.Parameters.AddWithValue("@AgeUnit", string.IsNullOrWhiteSpace(bill.AgeUnit) ? AgeUnits.Years : bill.AgeUnit);
                cmd.Parameters.AddWithValue("@Dob", bill.DateOfBirth.HasValue
                    ? bill.DateOfBirth.Value.ToString(DateFormat, CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", bill.Gender ?? "");
                cmd.Parameters.AddWithValue("@Pregnancy", string.IsNullOrWhiteSpace(bill.PregnancyStatus)
                    ? PregnancyStatuses.NotApplicable : bill.PregnancyStatus);
                cmd.Parameters.AddWithValue("@Referred", bill.ReferredBy ?? "");
                cmd.Parameters.AddWithValue("@Lab", bill.LabName ?? "");
                cmd.Parameters.AddWithValue("@Discount", bill.OverallDiscount);
                cmd.Parameters.AddWithValue("@Note", bill.Note ?? "");
                cmd.Parameters.AddWithValue("@Summary", bill.TestSummary ?? "");
                cmd.Parameters.AddWithValue("@HeldAt", bill.HeldAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture));

                int heldId = Convert.ToInt32(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);

                foreach (var item in bill.Items.Where(i => !string.IsNullOrWhiteSpace(i.TestName)))
                {
                    using var itemCmd = new SqliteCommand(@"
                        INSERT INTO HeldBillItems
                            (HeldBillId, CategoryHeading, TestName, Unit, NormalRange, Price, Discount, TestDefinitionId, IsPanelMember,
                             CatalogPrice, DiscountMode, PanelId, EntryRole)
                        VALUES
                            (@HeldId, @Category, @TestName, @Unit, @Range, @Price, @Discount, @DefId, @IsPanelMember,
                             @CatalogPrice, @DiscountMode, @PanelId, @EntryRole);",
                        connection, transaction);
                    itemCmd.Parameters.AddWithValue("@HeldId", heldId);
                    itemCmd.Parameters.AddWithValue("@Category", item.CategoryHeading ?? "");
                    itemCmd.Parameters.AddWithValue("@TestName", item.TestName ?? "");
                    itemCmd.Parameters.AddWithValue("@Unit", item.Unit ?? "");
                    itemCmd.Parameters.AddWithValue("@Range", item.NormalRange ?? "");
                    itemCmd.Parameters.AddWithValue("@Price", item.Price);
                    itemCmd.Parameters.AddWithValue("@Discount", item.Discount);
                    itemCmd.Parameters.AddWithValue("@DefId", item.TestDefinitionId.HasValue ? item.TestDefinitionId.Value : DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@IsPanelMember", item.IsPanelMember ? 1 : 0);
                    itemCmd.Parameters.AddWithValue("@CatalogPrice", item.CatalogPrice);
                    itemCmd.Parameters.AddWithValue("@DiscountMode",
                        string.Equals(item.DiscountMode, "Fixed", StringComparison.OrdinalIgnoreCase) ? "Fixed" : "Percent");
                    itemCmd.Parameters.AddWithValue("@PanelId", item.PanelId.HasValue ? item.PanelId.Value : DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@EntryRole", string.IsNullOrWhiteSpace(item.EntryRole) ? "Standard" : item.EntryRole);
                    itemCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return heldId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static List<HeldBill> GetHeldBills()
        {
            var list = new List<HeldBill>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(@"
                SELECT Id, ProfileId, PatientName, MrNumber, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender,
                       PregnancyStatus, ReferredBy, LabName, OverallDiscount, Note, TestSummary, HeldAt, IFNULL(Cnic,'')
                FROM HeldBills ORDER BY Id DESC", connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadHeldBillHeader(reader));
            return list;
        }

        public static HeldBill? GetHeldBillById(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var cmd = new SqliteCommand(@"
                SELECT Id, ProfileId, PatientName, MrNumber, PhoneNumber, AgeValue, AgeUnit, DateOfBirth, Gender,
                       PregnancyStatus, ReferredBy, LabName, OverallDiscount, Note, TestSummary, HeldAt, IFNULL(Cnic,'')
                FROM HeldBills WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var bill = ReadHeldBillHeader(reader);
            reader.Close();

            using var itemsCmd = new SqliteCommand(@"
                SELECT CategoryHeading, TestName, Unit, NormalRange, Price, Discount, TestDefinitionId, IsPanelMember,
                       CatalogPrice, DiscountMode, PanelId, EntryRole
                FROM HeldBillItems WHERE HeldBillId = @Id ORDER BY Id", connection);
            itemsCmd.Parameters.AddWithValue("@Id", id);
            using var itemsReader = itemsCmd.ExecuteReader();
            while (itemsReader.Read())
            {
                bool isMember = !itemsReader.IsDBNull(7) && itemsReader.GetInt32(7) == 1;
                string normalRange = itemsReader.IsDBNull(3) ? "" : itemsReader.GetString(3);
                string storedRole = itemsReader.IsDBNull(11) ? "" : itemsReader.GetString(11);
                string role = storedRole is "PanelBill" or "PanelParameter" or "Standard"
                    ? storedRole
                    : isMember
                        ? "PanelParameter"
                        : string.Equals(normalRange, "(panel)", StringComparison.OrdinalIgnoreCase) ? "PanelBill" : "Standard";
                if (isMember)
                    role = "PanelParameter";

                decimal price = itemsReader.IsDBNull(4) ? 0 : Convert.ToDecimal(itemsReader.GetDouble(4));
                int? definitionId = itemsReader.IsDBNull(6) ? null : itemsReader.GetInt32(6);
                int? panelId = itemsReader.IsDBNull(10) ? null : itemsReader.GetInt32(10);
                if (role == "PanelBill" && panelId == null)
                    panelId = definitionId;

                bill.Items.Add(new TestResultItem
                {
                    CategoryHeading = itemsReader.IsDBNull(0) ? "" : itemsReader.GetString(0),
                    TestName = itemsReader.IsDBNull(1) ? "" : itemsReader.GetString(1),
                    Unit = itemsReader.IsDBNull(2) ? "" : itemsReader.GetString(2),
                    NormalRange = normalRange,
                    Price = price,
                    CatalogPrice = itemsReader.IsDBNull(8)
                        ? (role == "PanelParameter" ? 0 : price)
                        : Convert.ToDecimal(itemsReader.GetDouble(8)),
                    Discount = itemsReader.IsDBNull(5) ? 0 : Convert.ToDecimal(itemsReader.GetDouble(5)),
                    DiscountMode = itemsReader.IsDBNull(9) ? "Percent" : itemsReader.GetString(9),
                    PanelId = panelId,
                    TestDefinitionId = definitionId,
                    IsPanelMember = isMember,
                    EntryRole = role
                });
            }

            TestResultItem.ApplyPanelMemberDiscounts(bill.Items);
            return bill;
        }

        public static void DeleteHeldBill(int id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var delItems = new SqliteCommand("DELETE FROM HeldBillItems WHERE HeldBillId = @Id", connection, transaction))
                {
                    delItems.Parameters.AddWithValue("@Id", id);
                    delItems.ExecuteNonQuery();
                }
                using (var del = new SqliteCommand("DELETE FROM HeldBills WHERE Id = @Id", connection, transaction))
                {
                    del.Parameters.AddWithValue("@Id", id);
                    del.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static HeldBill ReadHeldBillHeader(SqliteDataReader reader)
        {
            return new HeldBill
            {
                Id = reader.GetInt32(0),
                ProfileId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                PatientName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                MrNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                PhoneNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                AgeValue = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                AgeUnit = reader.IsDBNull(6) || string.IsNullOrWhiteSpace(reader.GetString(6)) ? AgeUnits.Years : reader.GetString(6),
                DateOfBirth = ParseStoredDate(reader.IsDBNull(7) ? "" : reader.GetString(7)),
                Gender = reader.IsDBNull(8) ? "" : reader.GetString(8),
                PregnancyStatus = reader.IsDBNull(9) || string.IsNullOrWhiteSpace(reader.GetString(9))
                    ? PregnancyStatuses.NotApplicable : reader.GetString(9),
                ReferredBy = reader.IsDBNull(10) ? "" : reader.GetString(10),
                LabName = reader.IsDBNull(11) ? "" : reader.GetString(11),
                OverallDiscount = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader.GetDouble(12)),
                Note = reader.IsDBNull(13) ? "" : reader.GetString(13),
                TestSummary = reader.IsDBNull(14) ? "" : reader.GetString(14),
                HeldAt = ParseStoredDate(reader.IsDBNull(15) ? "" : reader.GetString(15)) ?? DateTime.Now,
                Cnic = reader.FieldCount > 16 && !reader.IsDBNull(16) ? reader.GetString(16) : ""
            };
        }
    }
}
