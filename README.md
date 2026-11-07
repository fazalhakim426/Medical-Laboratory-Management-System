# Lab ProX — Setup Guide (VS Code)

## STEP 1 — Install required software (one-time)

1. **.NET 8 SDK** (required to build/run WPF apps)
   Download: https://dotnet.microsoft.com/download/dotnet/8.0
   After installing, verify in a terminal:
   ```
   dotnet --version
   ```
   It should print something like `8.0.xxx`.

2. **VS Code**
   Download: https://code.visualstudio.com/

3. **VS Code Extensions** — open VS Code, go to Extensions (Ctrl+Shift+X), install:
   - **C#** (by Microsoft / "C# Dev Kit")
   - **XAML** (optional, gives XAML syntax highlighting)

> Note: WPF apps only run on **Windows**. You must do this on a Windows PC.

## STEP 2 — Open the project

1. Extract the ZIP file you were given, you should have the project folder.
2. Open VS Code → File → Open Folder → select that folder.

## STEP 3 — Restore NuGet packages (external libraries)

Open a terminal inside VS Code (Terminal → New Terminal) and run:

```
dotnet restore
```

This will automatically download all 4 external libraries listed at the bottom of this file.

## STEP 4 — Edit your Lab's settings

Open `Config/AppSettings.cs` and change:
- `LabName`, `LabAddress`, `LabContact` — your lab's real details
- `ReportsBaseUrl` — the public folder URL on your website where reports will be hosted
- `FtpHost`, `FtpUsername`, `FtpPassword`, `FtpRemoteDirectory` — your web hosting's FTP details (ask your hosting provider / cPanel → FTP Accounts)
- `SmsApiUrlTemplate` — your SMS provider's API URL

**If you don't have web hosting or an SMS provider yet**, set these two lines to `false` so the app still works fully offline (it will just skip upload/SMS and only save the PDF locally):
```csharp
public const bool EnableSmsSending = false;
public const bool EnableCloudUpload = false;
```

## STEP 5 — Build the project

In the terminal:
```
dotnet build
```
If there are no red errors, the build succeeded.

## STEP 6 — Run the app

```
dotnet run
```
The Lab Report window should open.

## STEP 7 — Using the app

1. Fill in Patient Name, Phone, Age, Gender, Referred By, Report Heading (e.g. "Complete Blood Count (CBC)").
2. Fill the test rows: Test Name, Result, Unit, Normal Range. Click **Add Row** for more.
3. Click **Save, Generate PDF, Upload & Send SMS**.
4. The app will:
   - Save the record with a unique serial number (e.g. LAB-000001) into the local database (`LabProX.db`, created automatically next to the .exe)
   - Generate a PDF in the `Reports` folder, with a QR code linking to the online copy
   - Upload the PDF to your website via FTP (if enabled)
   - Send an SMS with the download link to the patient's phone (if enabled)
5. Click **View Past Reports** any time to see old entries and reopen their PDFs.

## STEP 8 — Building a distributable .exe (to install on the Lab's PC)

When you're ready to give this app to the lab (not just run via `dotnet run`), publish it as a self-contained .exe:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The final `.exe` will appear inside:
```
bin\Release\net8.0-windows\win-x64\publish\
```
Copy that whole `publish` folder to the lab's PC — no need to install .NET or VS Code there.

---

## Project Structure

```
LabProX/
├── LabProX.csproj             → Project file (lists all dependencies)
├── App.xaml / App.xaml.cs     → App startup, initializes database
├── MainWindow.xaml(.cs)       → Main data-entry screen
├── HistoryWindow.xaml(.cs)    → View past reports
├── Config/
│   └── AppSettings.cs         → YOUR lab details, FTP & SMS settings (edit this!)
├── Models/
│   ├── Patient.cs
│   └── TestResultItem.cs
├── Database/
│   └── DatabaseHelper.cs      → SQLite: saves/reads patients & test results
├── Services/
│   ├── PdfReportService.cs    → Builds the PDF report
│   ├── QrCodeService.cs       → Generates the QR code
│   ├── FtpUploadService.cs    → Uploads PDF to your website
│   └── SmsService.cs          → Sends SMS with the report link
└── Reports/                   → Generated PDFs are saved here
```

---

## EXTERNAL DEPENDENCIES (NuGet packages used)

These are downloaded automatically by `dotnet restore` — you don't install them manually.
Listed here so you know what they are and can look up their docs if needed:

| Package | Purpose | Docs |
|---|---|---|
| **Microsoft.Data.Sqlite** (v8.0.8) | Local offline database (patients + test results) | https://learn.microsoft.com/dotnet/standard/data/sqlite/ |
| **QuestPDF** (v2024.10.3) | Generates the PDF lab report | https://www.questpdf.com/ |
| **QRCoder** (v1.6.0) | Generates the QR code image | https://github.com/codebude/QRCoder |
| **FluentFTP** (v49.0.2) | Uploads the PDF to your website via FTP | https://github.com/robinrodricks/FluentFTP |

### Other things YOU need to arrange separately (not code — real-world accounts):
1. **Web hosting with FTP access** — any cheap shared hosting (e.g. Hostinger, GoDaddy, local Pakistani hosts) works, as long as it gives you an FTP username/password and a public URL. This is where reports become "online."
2. **An SMS gateway account** — e.g. a local provider (Whitesms.pk, SMSAlert.pk) or Twilio. You'll get an API URL/key from them to put into `AppSettings.cs`.

> Important note on QuestPDF licensing: this project uses QuestPDF's **Community license** (free), which is free for companies with less than $1M annual gross revenue — this covers the vast majority of small labs, but please read https://www.questpdf.com/license/ to confirm it applies to you.

---

## Development & Deployment Commands

Run these from the project folder (e.g. `F:\www\Medical-Laboratory-Management-System`).

> WPF apps only run on **Windows**.

### Development (daily)

```powershell
# Check .NET 8 is installed
dotnet --version

# Download NuGet packages
dotnet restore

# Build
dotnet build

# Run (opens the WPF window)
dotnet run
```

Faster loop after a successful build:

```powershell
dotnet build
dotnet run --no-build
```

Edit lab settings in `Config\AppSettings.cs` before real use (lab name, FTP, backup path, etc.).

### Deployment (lab PC)

Self-contained single-file `.exe` (no .NET install needed on the lab PC):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output folder:

```text
bin\Release\net8.0-windows\win-x64\publish\
```

Copy that whole `publish` folder to the lab PC and run `LabProX.exe`.

Framework-dependent (smaller; requires .NET 8 Desktop Runtime on the PC):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```
