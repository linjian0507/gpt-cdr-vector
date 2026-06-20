GPT CDR Vector - CorelDRAW Addons package

Files:
- CorelDrw.addon: marker file that lets CorelDRAW scan this Addons folder.
- AppUI.xslt: adds a fixed top toolbar hosted control to the CorelDRAW workspace.
- GptCdrVectorHost.dll: small WPF toolbar button host. Clicking it opens app.exe.
- app.exe: main non-VBA panel. It calls the relay API directly and imports SVG into CorelDRAW through COM.
- config.json: package metadata and environment variable names.
- msc.json: preset descriptions for humans and future UI extension.
- uisettings.ini: simple Addons-style grouping reference.

Install:
1. Prefer running GptCdrVectorInstaller.exe from the parent folder. It can auto-detect CorelDRAW Addons roots and install this package.
2. Manual install: keep CorelDRAW closed.
3. Copy this whole "gpt-cdr-vector" folder to your CorelDRAW Addons root, for example:
   <CorelDRAW>\Programs64\Addons\gpt-cdr-vector
4. Start CorelDRAW.
5. A "GPT CDR Vector" toolbar should appear at the top with a button.
6. Click the toolbar button to open the panel. You can also run app.exe directly from this folder.

If the toolbar does not appear:
- Make sure the folder name is exactly "gpt-cdr-vector" directly under Programs64\Addons.
- Restart CorelDRAW.
- If the workspace was already cached, start CorelDRAW while holding F8 to reapply workspace UI transforms.

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
- It does not modify any existing Addons folder unless you copy or install it yourself.
