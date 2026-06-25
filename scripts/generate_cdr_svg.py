#!/usr/bin/env python3
"""Generate CorelDRAW-friendly SVG vector artwork."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import socket
import sys
import textwrap
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path


DEFAULT_SVG_MODEL = "gpt-4.1-mini"
DEFAULT_API_TIMEOUT = 600
CHAT_COMPLETIONS_API_URL = "https://ai.opendoor.sbs/v1/chat/completions"


SYSTEM_PROMPT = """You are a senior vector designer preparing artwork for CorelDRAW.
Return exactly one valid standalone SVG XML document and nothing else.

Work like a professional SVG production designer:
- First reason internally about the selected task type, geometry, layout, focal hierarchy, spacing, palette, and editable layer structure.
- Then output only the final SVG. Do not include your reasoning, notes, or comments.
- Follow the selected SVG preset. If it conflicts with style, the SVG preset wins.
- Use a complete result appropriate to the task: single-object SVGs should be clean and isolated; icons should be simple and legible; diagrams should be structured; posters should feel complete.
- If the preset is not explicitly poster, never add poster/page/card/app-screen layout, title panels, social icon strips, or unrelated background sections.
- Keep all shapes intentional and aligned; avoid random symbols, unrelated brands, fake logos, and invented text.

CorelDRAW compatibility requirements:
- Use a viewBox and explicit width/height.
- Prefer paths, basic shapes, flat fills, simple strokes, and simple gradients.
- Do not use Markdown, comments explaining the design, scripts, animation, foreignObject,
  external links, embedded raster images, base64 data, CSS imports, or web fonts.
- Keep text minimal. If text is requested, use plain <text> elements only.
- Keep the SVG editable: organize major objects in <g> groups with useful ids.
- For poster work, use top-level layer groups such as background, key_visual,
  layout, typography, decorative_shapes, foreground_details, and polish.
- Use smooth closed paths, consistent stroke widths, regular curves, and clean geometry.
- Avoid filters, blur, masks, clipping, and excessive tiny paths unless essential.
- Keep every visible object inside the viewBox and leave balanced margins.
- Do a final internal quality check before output: clear hierarchy, coherent palette,
  no accidental overlaps, no broken paths, no placeholder text, no empty-looking canvas.
"""


STYLE_HINTS = {
    "auto": "match the selected SVG preset without adding poster layout unless the preset is poster",
    "logo": "logo mark, clean geometry, limited colors, scalable at small sizes",
    "icon": "simple icon, strong silhouette, minimal detail, consistent stroke weight",
    "sticker": "bold sticker illustration, clear contour, printable flat colors",
    "poster": "finished advertising poster, editorial hierarchy, rich layered vector composition, polished spacing, strong focal area",
    "illustration": "editable flat vector illustration, layered scene, clear silhouette, controlled detail density",
    "line-art": "single-color line art, clean strokes, no fills unless requested",
    "engraving": "engraving-ready vector, high contrast, closed paths, no raster shading",
    "pattern": "tileable vector pattern, repeat-friendly edges, no raster texture",
    "custom": "follow the user's requested vector style",
}

NON_POSTER_PRESETS = {
    "icon",
    "logo",
    "object",
    "subject",
    "line-art",
    "cut-path",
    "sticker",
    "label",
    "infographic",
    "diagram",
    "pattern",
    "background",
}

PRESET_HINTS = {
    "none": "No preset. Follow the user brief directly as editable SVG artwork.",
    "icon": "Text-to-SVG icon: one clear symbol, centered, scalable, minimal paths, consistent stroke weight, no background unless requested.",
    "logo": "Text-to-SVG logo or wordmark: memorable mark, clean geometry, limited colors, balanced negative space, avoid fake brand names unless supplied.",
    "object": "Single object SVG: one isolated subject with clear silhouette, editable grouped parts, no poster frame, no unrelated decorations.",
    "image-to-svg": "Reference image to SVG: rebuild the reference as clean editable vectors, preserve major shapes and layout, simplify raster texture into vector forms.",
    "subject": "Subject extraction SVG: focus on the main object from the prompt or reference image, remove background clutter, produce a clean editable subject layer.",
    "line-art": "Line art SVG: clean contour lines, optional simple hatching, consistent strokes, no shaded raster-like texture.",
    "cut-path": "Cutting/engraving SVG: closed paths, single-color or very limited colors, no overlapping duplicate strokes, production-friendly outlines.",
    "sticker": "Sticker/badge SVG: bold contour, compact composition, clear border, printable flat colors, strong silhouette.",
    "label": "Product label SVG: balanced label layout, editable text areas only from the brief, decorative border, icons or badges as vector shapes.",
    "poster": "Poster SVG: finished advertising/editorial composition, strong focal hierarchy, background depth, decorative accents, readable typography.",
    "infographic": "Infographic SVG: structured sections, icons, charts, labels, arrows, clear information hierarchy, no fake data beyond the brief.",
    "diagram": "Diagram-to-SVG: nodes, arrows, connectors, labels from the brief, aligned layout, readable structure, technical clarity.",
    "pattern": "Pattern SVG: repeat-friendly motif, consistent spacing, tileable edges when relevant, organized groups.",
    "background": "Decorative background SVG: abstract shapes, patterns, gradients made from vector elements, no central poster text unless requested.",
}

PRESET_RULES = {
    "icon": "Hard output rule: create exactly one icon/symbol. Do not create a poster, card, app screenshot, title area, social-logo strip, or background scene.",
    "logo": "Hard output rule: create a logo mark or wordmark only. Do not create a poster, app screenshot, ad layout, social-logo strip, or decorative page.",
    "object": "Hard output rule: create one isolated object on a transparent/simple canvas. No poster frame, title, text panel, background stage, or unrelated icons.",
    "subject": "Hard output rule: extract only the main subject. Ignore surrounding poster/card/page layout, background panels, decorative frames, social icons, and unrelated text.",
    "image-to-svg": "Hard output rule: convert the reference image content to editable SVG; preserve the source layout only when the selected preset is reference-to-SVG.",
    "line-art": "Hard output rule: output clean contours only. No poster layout, color blocks, badges, title panels, or filled advertising design.",
    "cut-path": "Hard output rule: output production paths only. No poster layout, shadows, gradients, tiny decorative filler, or text panels.",
    "sticker": "Hard output rule: output one compact sticker/badge. No full poster/page composition or multi-section layout.",
    "label": "Hard output rule: output a product label asset, not a full poster. Keep layout compact and label-shaped.",
    "infographic": "Hard output rule: output structured information graphics only. Do not add poster hero artwork unless requested.",
    "diagram": "Hard output rule: output nodes, arrows, connectors, and labels only. No poster hero, decorative ad frame, or unrelated illustration scene.",
    "pattern": "Hard output rule: output a repeatable pattern/motif. No poster, card, central title, or subject extraction layout.",
    "background": "Hard output rule: output background vector texture only. No poster title, logo strip, subject extraction, or content card.",
}


def parse_size(value: str) -> tuple[int, int]:
    match = re.fullmatch(r"\s*(\d{2,5})\s*[xX]\s*(\d{2,5})\s*", value)
    if not match:
        raise argparse.ArgumentTypeError("size must look like 1024x1024")
    return int(match.group(1)), int(match.group(2))


def parse_timeout(value: str) -> int:
    try:
        timeout = int(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("timeout must be an integer number of seconds") from exc
    if timeout < 30:
        raise argparse.ArgumentTypeError("timeout must be at least 30 seconds")
    return timeout


def effective_style(preset: str, style: str) -> str:
    if style == "poster" and preset in NON_POSTER_PRESETS:
        return "auto"
    return style


def preset_guard(preset: str) -> str:
    if preset == "none":
        return ""
    lines = []
    if preset in NON_POSTER_PRESETS:
        lines.append(
            "This is not a poster task. Do not create a poster canvas, advertisement card, app screenshot, "
            "large title panel, social icon row, background stage, or multi-section page unless the user explicitly asks for one."
        )
    rule = PRESET_RULES.get(preset)
    if rule:
        lines.append(rule)
    return "\n        - " + "\n        - ".join(lines) if lines else ""


def reference_instruction(preset: str) -> str:
    if preset in {"subject", "object", "icon", "logo", "line-art", "cut-path", "sticker"}:
        return (
            "\n        - Use the attached reference image only to identify the main subject, silhouette, proportions, "
            "important colors, and useful surface details. Ignore the reference background, page/card layout, title area, "
            "social logos, decorative frames, and unrelated text. Output the isolated preset asset as native SVG shapes."
        )
    if preset == "image-to-svg":
        return (
            "\n        - Rebuild the attached reference image as editable SVG shapes. Preserve the major source layout and visible objects, "
            "but do not embed, trace, or rasterize the image."
        )
    return (
        "\n        - Use the attached reference image for visual direction, spacing, color mood, and useful composition cues. "
        "Rebuild it as editable SVG shapes; do not embed or trace the image as raster data. "
        "Keep only the parts that match the selected preset and user brief."
    )


def build_user_prompt(args: argparse.Namespace) -> str:
    width, height = args.size
    color_line = f"Preferred colors: {args.colors}." if args.colors else "Choose a compact production-friendly palette."
    text_line = "Text is allowed if requested by the design brief." if args.allow_text else "Avoid text unless it is absolutely necessary."
    preset_line = f"SVG preset: {PRESET_HINTS[args.preset]}"
    style = effective_style(args.preset, args.style)
    reference_line = reference_instruction(args.preset) if args.reference_image else ""
    guard_line = preset_guard(args.preset)
    return textwrap.dedent(
        f"""
        Create CorelDRAW-ready SVG vector artwork.

        Design brief:
        {args.prompt}

        Production specs:
        - Canvas: {width}x{height}, viewBox 0 0 {width} {height}.
        - {preset_line}
        - Style: {STYLE_HINTS[style]}.
        - When the selected SVG preset conflicts with the visual style, prioritize the preset.
        {guard_line}
        - {color_line}
        - {text_line}
        - Target use: {args.target_use}.
        - Build the artwork with clear top-level SVG groups for editable CorelDRAW layers.
        - Use group names that match the preset, such as background, subject, icon_mark, logo_mark, label_layout, diagram_nodes, connectors, typography, decorative_shapes, foreground_details, and polish.
        - Use only readable text from the design brief. Do not invent garbled Chinese copy.
        - If the brief is short, expand only within the selected preset. For isolated assets, refine the subject itself instead of adding a surrounding scene.
        - Avoid a sparse row of isolated icons, generic circles, random social logos, placeholder brand names, and large empty panels unless the selected preset or brief specifically asks for them.
        - Use 3-6 coordinated colors with deliberate contrast. For isolated assets, add only subject details; for composition presets, add depth through layered vector shapes, subtle gradients, frames, grids, badges, or ornaments.
        - Make the result feel finished for its preset: icon clarity, object isolation, diagram structure, pattern consistency, label balance, or poster hierarchy as appropriate.
        - Keep text areas legible: no overlapping text, no tiny filler text, and no pseudo-letters.
        {reference_line}
        - Output only SVG XML.
        """
    ).strip()


def image_data_url(path: str) -> str:
    image_path = Path(path)
    if not image_path.exists():
        raise FileNotFoundError(f"Reference image not found: {path}")

    mime_types = {
        ".jpg": "image/jpeg",
        ".jpeg": "image/jpeg",
        ".png": "image/png",
        ".webp": "image/webp",
    }
    mime_type = mime_types.get(image_path.suffix.lower())
    if not mime_type:
        raise ValueError("Reference image must be a PNG, JPG, JPEG, or WEBP file.")

    encoded = base64.b64encode(image_path.read_bytes()).decode("ascii")
    return f"data:{mime_type};base64,{encoded}"


def chat_user_content(prompt: str, reference_image: str | None) -> str | list[dict]:
    if not reference_image:
        return prompt
    return [
        {"type": "text", "text": prompt},
        {"type": "image_url", "image_url": {"url": image_data_url(reference_image)}},
    ]


def responses_user_content(prompt: str, reference_image: str | None) -> str | list[dict]:
    if not reference_image:
        return prompt
    return [
        {"type": "input_text", "text": prompt},
        {"type": "input_image", "image_url": image_data_url(reference_image)},
    ]


def call_json_api(api_url: str, api_key: str, payload: dict, timeout_seconds: int) -> dict:
    request = urllib.request.Request(
        api_url,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"API request failed: HTTP {exc.code}: {detail}") from exc
    except (TimeoutError, socket.timeout) as exc:
        raise RuntimeError(
            f"API request timed out after {timeout_seconds} seconds. "
            "For gpt-5.5 with reference images, set OPENAI_API_TIMEOUT to a larger value such as 900."
        ) from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"API request failed: {exc}") from exc


def stream_chunk_text(data: dict) -> str:
    parts: list[str] = []
    choices = data.get("choices")
    if not isinstance(choices, list):
        return ""
    for choice in choices:
        if not isinstance(choice, dict):
            continue
        delta = choice.get("delta")
        if isinstance(delta, dict):
            content = delta.get("content")
            if isinstance(content, str):
                parts.append(content)
            elif isinstance(content, list):
                parts.extend(part.get("text") for part in content if isinstance(part, dict) and isinstance(part.get("text"), str))
        message = choice.get("message")
        if isinstance(message, dict):
            content = message.get("content")
            if isinstance(content, str):
                parts.append(content)
    return "".join(parts)


def call_chat_stream_api(api_url: str, api_key: str, payload: dict, timeout_seconds: int) -> dict:
    stream_payload = dict(payload)
    stream_payload["stream"] = True
    request = urllib.request.Request(
        api_url,
        data=json.dumps(stream_payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "Accept": "text/event-stream",
        },
        method="POST",
    )
    try:
        chunks: list[str] = []
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            for raw_line in response:
                line = raw_line.decode("utf-8", errors="replace").strip()
                if not line.startswith("data:"):
                    continue
                data_line = line[5:].strip()
                if data_line == "[DONE]":
                    break
                chunks.append(stream_chunk_text(json.loads(data_line)))
        if not chunks:
            raise RuntimeError("Streaming API returned no text content.")
        return {"choices": [{"message": {"content": "".join(chunks)}}]}
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"API request failed: HTTP {exc.code}: {detail}") from exc
    except (TimeoutError, socket.timeout) as exc:
        raise RuntimeError(
            f"Streaming API request timed out after {timeout_seconds} seconds. "
            "Increase OPENAI_API_TIMEOUT or use a faster model."
        ) from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Streaming API request failed: {exc}") from exc


def call_text_api(api_url: str, api_key: str, model: str, prompt: str, reference_image: str | None, timeout_seconds: int) -> dict:
    if "/chat/completions" in api_url:
        payload = {
            "model": model,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": chat_user_content(prompt, reference_image)},
            ],
            "temperature": 0.2,
        }
        return call_chat_stream_api(api_url, api_key, payload, timeout_seconds)

    payload = {
        "model": model,
        "input": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": responses_user_content(prompt, reference_image)},
        ],
    }
    return call_json_api(api_url, api_key, payload, timeout_seconds)


def response_text(data: dict) -> str:
    if isinstance(data.get("output_text"), str):
        return data["output_text"]

    choices = data.get("choices")
    if isinstance(choices, list):
        for choice in choices:
            if not isinstance(choice, dict):
                continue
            message = choice.get("message")
            if not isinstance(message, dict):
                continue
            content = message.get("content")
            if isinstance(content, str):
                return content
            if isinstance(content, list):
                text_parts = [part.get("text") for part in content if isinstance(part, dict) and isinstance(part.get("text"), str)]
                if text_parts:
                    return "\n".join(text_parts)

    parts: list[str] = []
    for item in data.get("output", []):
        for content in item.get("content", []):
            if isinstance(content, dict):
                if isinstance(content.get("text"), str):
                    parts.append(content["text"])
                elif isinstance(content.get("output_text"), str):
                    parts.append(content["output_text"])
    if parts:
        return "\n".join(parts)
    raise RuntimeError("Could not find text output in the API response.")


def extract_svg(raw: str) -> str:
    text = raw.strip()
    text = re.sub(r"^```(?:svg|xml)?\s*", "", text, flags=re.IGNORECASE)
    text = re.sub(r"\s*```$", "", text)
    match = re.search(r"<svg\b[\s\S]*?</svg>", text, flags=re.IGNORECASE)
    if not match:
        raise ValueError("The model response did not contain a complete <svg>...</svg> document.")
    return match.group(0).strip()


def validate_svg(svg: str, size: tuple[int, int]) -> str:
    lowered = svg.lower()
    blocked = ["<script", "<foreignobject", "data:image", "<image", "@import", "url(http"]
    found = [token for token in blocked if token in lowered]
    if re.search(r"\b(?:href|xlink:href)\s*=\s*['\"](?:https?:|data:)", lowered):
        found.append("external href")
    if found:
        raise ValueError(f"SVG contains CorelDRAW-unfriendly or unsafe content: {', '.join(found)}")

    try:
        root = ET.fromstring(svg)
    except ET.ParseError as exc:
        raise ValueError(f"SVG XML is not valid: {exc}") from exc

    if not root.tag.lower().endswith("svg"):
        raise ValueError("Root element is not <svg>.")

    width, height = size
    attr_names = {key.lower() for key in root.attrib}

    def add_svg_attr(current: str, name: str, value: str) -> str:
        open_tag = re.search(r"<svg\b[^>]*>", current, flags=re.IGNORECASE)
        if not open_tag:
            raise ValueError("Could not find opening <svg> tag.")
        insert_at = open_tag.end() - 1
        return current[:insert_at] + f' {name}="{value}"' + current[insert_at:]

    result = svg
    if "viewbox" not in attr_names:
        result = add_svg_attr(result, "viewBox", f"0 0 {width} {height}")
    if "width" not in attr_names:
        result = add_svg_attr(result, "width", str(width))
    if "height" not in attr_names:
        result = add_svg_attr(result, "height", str(height))
    if not re.search(r"<svg\b[^>]*\sxmlns\s*=", result, flags=re.IGNORECASE):
        result = add_svg_attr(result, "xmlns", "http://www.w3.org/2000/svg")

    return result


def self_test() -> None:
    sample = """```svg
<svg viewBox="0 0 100 100"><g id="mark"><circle cx="50" cy="50" r="40" fill="#111"/></g></svg>
```"""
    svg = validate_svg(extract_svg(sample), (100, 100))
    if "<svg" not in svg or "viewBox" not in svg:
        raise RuntimeError("self-test failed")
    chat_text = response_text({"choices": [{"message": {"content": sample}}]})
    if "<svg" not in extract_svg(chat_text):
        raise RuntimeError("chat response self-test failed")
    stream_text = stream_chunk_text({"choices": [{"delta": {"content": "<svg></svg>"}}]})
    if stream_text != "<svg></svg>":
        raise RuntimeError("stream chunk self-test failed")
    if parse_timeout("600") != 600:
        raise RuntimeError("timeout self-test failed")
    png = Path(os.getenv("TEMP", ".")) / "gpt-cdr-vector-reference-self-test.png"
    png.write_bytes(base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="))
    try:
        content = chat_user_content("use this layout", str(png))
        if not isinstance(content, list) or "data:image/png;base64," not in content[1]["image_url"]["url"]:
            raise RuntimeError("reference image self-test failed")
    finally:
        png.unlink(missing_ok=True)
    print("self-test ok")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate CorelDRAW-ready SVG vector artwork.")
    parser.add_argument("prompt", nargs="?", help="Artwork prompt or design brief.")
    parser.add_argument("--output", "-o", default="gpt-cdr-vector.svg", help="Output SVG path.")
    parser.add_argument("--api-url", help="Override API URL. Defaults to the third-party Chat Completions relay.")
    parser.add_argument("--model", help="Model name. Defaults to OPENAI_VECTOR_MODEL or gpt-4.1-mini.")
    parser.add_argument("--size", type=parse_size, default=(1024, 1024), help="Canvas size, e.g. 1024x1024.")
    parser.add_argument("--style", choices=sorted(STYLE_HINTS), default="auto", help="Vector style preset.")
    parser.add_argument("--colors", help="Comma-separated preferred colors, e.g. '#111111,#f4c542'.")
    parser.add_argument("--preset", choices=sorted(PRESET_HINTS), default="none", help="SVG task preset, e.g. icon, object, diagram, poster, cut-path.")
    parser.add_argument("--target-use", default="CorelDRAW editable SVG vector artwork", help="Production target or use case.")
    parser.add_argument("--allow-text", action="store_true", help="Allow SVG text elements when useful.")
    parser.add_argument("--reference-image", help="Optional reference PNG/JPG/WEBP image for preset-aware SVG guidance.")
    parser.add_argument("--timeout", type=parse_timeout, default=parse_timeout(os.getenv("OPENAI_API_TIMEOUT", str(DEFAULT_API_TIMEOUT))), help="API request timeout in seconds. Defaults to OPENAI_API_TIMEOUT or 600.")
    parser.add_argument("--raw-response", help="Optional path to save raw API JSON for debugging.")
    parser.add_argument("--self-test", action="store_true", help="Run local SVG extraction/validation test without calling the API.")
    args = parser.parse_args(argv)

    if args.self_test:
        self_test()
        return 0
    if not args.prompt:
        parser.error("prompt is required unless --self-test is used")

    api_key = os.getenv("OPENAI_RELAY_API_KEY") or os.getenv("OPENAI_API_KEY")
    if not api_key:
        raise SystemExit("OPENAI_RELAY_API_KEY or OPENAI_API_KEY is not set.")

    args.model = args.model or os.getenv("OPENAI_VECTOR_MODEL", DEFAULT_SVG_MODEL)
    api_url = args.api_url or os.getenv("OPENAI_VECTOR_API_URL") or os.getenv("OPENAI_RESPONSES_API_URL") or CHAT_COMPLETIONS_API_URL
    data = call_text_api(api_url, api_key, args.model, build_user_prompt(args), args.reference_image, args.timeout)
    if args.raw_response:
        Path(args.raw_response).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    svg = validate_svg(extract_svg(response_text(data)), args.size)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(svg + "\n", encoding="utf-8")
    print(str(output.resolve()))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
