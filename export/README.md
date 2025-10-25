# Export Icons

This folder contains platform-specific application icons.

## Files

- `icon.ico` - Windows icon (multi-size: 16, 32, 48, 64, 128, 256 px)
- `icon.icns` - macOS icon (optional, for future macOS builds)
- `icon.png` - Linux icon (256x256 px, optional)

## Generating Windows .ico

### Option 1: Online Converter (Easiest) ✅

1. Visit: https://convertico.com or https://icoconvert.com
2. Upload: `../assets/app-icon/app-icon.png` (512x512 px)
3. Download the generated `.ico` file
4. Save as: `export/icon.ico`

### Option 2: PowerShell Script (Windows)

```powershell
# Run from project root:
.\scripts\generate_icon.ps1
```

**Requirements:** ImageMagick installed
Download: https://imagemagick.org/script/download.php#windows

### Option 3: ImageMagick CLI

```bash
# Requires ImageMagick installed
magick convert assets/app-icon/app-icon.png \
  -define icon:auto-resize=256,128,64,48,32,16 \
  export/icon.ico
```

## Usage in Godot Export

### Windows Desktop Export
```
Project → Export → Add... → Windows Desktop
→ Application → Icon: res://export/icon.ico
```

### macOS Export
```
Project → Export → Add... → macOS
→ Application → Icon: res://export/icon.icns
```

### Linux Export
```
Project → Export → Add... → Linux/X11
→ Application → Icon: res://export/icon.png
```

## Source

Base icon: `../assets/app-icon/app-icon.png` (512x512 px, RGBA)
