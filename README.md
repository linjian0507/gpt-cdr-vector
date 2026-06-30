# GPT CDR Vector

GPT CDR Vector is a CorelDRAW helper for generating editable SVG vector artwork from text prompts and reference images. It focuses on CorelDRAW-friendly SVG output for posters, icons, logos, object extraction, labels, stickers, diagrams, line art, engraving paths, patterns, and other vector workflows.

## Features

- Safe CorelDRAW Addons package with a non-VBA external panel; it does not load a WPF toolbar inside CorelDRAW.
- Direct relay API calls from the panel; Python is not required for the Addon package.
- Model selector for relay models such as `gpt-5.4-mini`, `gpt-5.4`, `gpt-5.5`, and `gpt-4.1-mini`.
- SVG presets for icon/button, logo/wordmark, single object, reference-to-SVG, subject extraction, line art, cutting/engraving, sticker/badge, product label, poster, infographic, diagram, pattern, and background texture.
- Reference image input from file, current CorelDRAW selection, or clipboard screenshot.
- Optional two-step reference workflow: first generate a near 1:1 reconstruction prompt from the reference image, then generate editable SVG from that prompt.
- Hybrid reconstruction mode that embeds the reference image as a bottom bitmap guide and adds editable SVG text/simple-shape overlay layers.
- API settings page for changing relay API key, API URL, model, and timeout.
- Self-contained installer and ready-to-copy Addons package under `dist/coreldraw_addon`.

## No API Keys Included

This repository does not include any personal API key. Configure your own key after installation.

The application reads these Windows user environment variables:

```text
OPENAI_RELAY_API_KEY
OPENAI_VECTOR_API_URL
OPENAI_VECTOR_MODEL
OPENAI_API_TIMEOUT
```

The Addon panel and installer also provide a settings page that writes the same values to the current Windows user environment.

## Quick Install

1. Close CorelDRAW.
2. Download and run the single-file installer:

```text
dist/coreldraw_addon/GptCdrVectorInstaller.exe
```

3. Let the installer detect your CorelDRAW Addons directory and install the embedded package.
4. Restart CorelDRAW.
5. Open the installed `gpt-cdr-vector` folder and run `app.exe` or `start-gpt-cdr-vector.cmd`.
6. Click `设置` or `设置 API` and enter your own relay API key.

The package is installed under:

```text
<CorelDRAW>\Programs64\Addons\gpt-cdr-vector
```

This safe package intentionally does not include `CorelDrw.addon`, so CorelDRAW will not auto-load it at startup. Older WPF toolbar-host builds could make CorelDRAW 2018 stop responding on some systems.

## Build Package

To rebuild the Addons package and installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build_coreldraw_addon.ps1 -Zip
```

Outputs:

```text
dist/coreldraw_addon/gpt-cdr-vector/
dist/coreldraw_addon/GptCdrVectorInstaller.exe
dist/coreldraw_addon/gpt-cdr-vector-addon.zip
```

`GptCdrVectorInstaller.exe` is self-contained and embeds the Addons package, so it can be distributed by itself. The zip is kept as a manual-install fallback.

## Python CLI

The Python generator is still available for direct SVG generation:

```powershell
python scripts\generate_cdr_svg.py "minimal coffee shop badge logo, two colors, no text" --preset logo --style logo --output coffee-badge.svg
```

Set `OPENAI_RELAY_API_KEY` before calling the API.

## Documentation

- Addon and CorelDRAW install guide: `docs/coreldraw-plugin-install.md`
- CorelDRAW SVG workflow notes: `references/coreldraw-svg-workflow.md`
- Codex skill metadata and workflow notes: `SKILL.md`

## Safety Notes

- Generated SVG is validated before saving/importing.
- The tool avoids embedding raster data in generated SVG.
- For final production, review imported artwork in CorelDRAW, convert text to curves when needed, and simplify paths before delivery.
