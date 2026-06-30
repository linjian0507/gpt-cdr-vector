GPT CDR Vector - CorelDRAW Addons package

Files:
- app.exe: main non-VBA panel. It calls the relay API directly and imports SVG into CorelDRAW through COM.
- start-gpt-cdr-vector.cmd: convenience launcher for app.exe.
- AppUI.xslt / UserUI.xslt: safe no-op UI transforms. They do not inject a toolbar or load an in-process WPF host.
- config.json: package metadata and environment variable names.
- msc.json: preset descriptions for humans and future UI extension.
- uisettings.ini: simple Addons-style grouping reference.

Install:
1. Prefer running the single-file GptCdrVectorInstaller.exe. It embeds this package, can auto-detect CorelDRAW Addons roots, and installs without needing the zip beside it.
2. Manual install fallback: keep CorelDRAW closed.
3. Copy this whole "gpt-cdr-vector" folder to your CorelDRAW Addons root, for example:
   <CorelDRAW>\Programs64\Addons\gpt-cdr-vector
4. Start CorelDRAW.
5. This safe package does not add a CorelDRAW toolbar button, because the previous in-process WPF host could freeze CorelDRAW 2018 on some installations.
6. Open the panel by running app.exe or start-gpt-cdr-vector.cmd from this folder.

If CorelDRAW becomes unresponsive:
- Close CorelDRAW.
- Delete the old Programs64\Addons\gpt-cdr-vector folder.
- Install this safe package again.
- Confirm the installed folder has no CorelDrw.addon, no GptCdrVectorHost.dll, and AppUI.xslt does not contain wpfhost.

Required environment variables:
- OPENAI_RELAY_API_KEY: your relay API key.

API settings:
- Run GptCdrVectorInstaller.exe and click the settings button, or open app.exe and click settings.
- The settings page writes OPENAI_RELAY_API_KEY, OPENAI_VECTOR_API_URL, OPENAI_VECTOR_MODEL, and OPENAI_API_TIMEOUT to the current Windows user environment.

Optional environment variables:
- OPENAI_API_TIMEOUT: request timeout in seconds, default 600.
- GPT_CDR_VECTOR_COREL_VERSION: CorelDRAW COM version, default 20 for CorelDRAW 2018.

Notes:
- This package does not require VBA.
- It does not require Python for generation.
- It does not load a .NET/WPF toolbar inside CorelDRAW.
- It does not modify any existing Addons folder unless you copy or install it yourself.
