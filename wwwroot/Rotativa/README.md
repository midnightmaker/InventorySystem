# Rotativa wkhtmltopdf Binary

Place the `wkhtmltopdf.exe` (Windows) or `wkhtmltopdf` (Linux) binary in this folder.

## Download

Download the appropriate binary from: https://wkhtmltopdf.org/downloads.html

Recommended version: **0.12.6** (latest stable)

## Windows Setup

1. Download the Windows installer from the link above.
2. After installing, copy `wkhtmltopdf.exe` from the install directory
   (typically `C:\Program Files\wkhtmltopdf\bin\`) into this folder.

## Linux / Docker Setup

Install via package manager:
```
apt-get install -y wkhtmltopdf
```
Or copy the binary into this folder and ensure it has execute permissions:
```
chmod +x wkhtmltopdf
```

## Notes

- This folder is referenced by `RotativaConfiguration.Setup(webRootPath, "Rotativa")` in `Program.cs`.
- The binary is NOT included in source control. Add `Rotativa/*.exe` to `.gitignore` if needed.
- wkhtmltopdf is licensed under LGPL v3. Using the binary in a proprietary application
  without modifying the binary itself is permitted under LGPL v3.
