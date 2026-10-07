from __future__ import annotations

from datetime import date


_LABEL_TO_HEADING = {
    "doc_title": "## ",
    "paragraph_title": "### ",
}

_SKIP_LABELS = {"header", "footer", "number", "header_image", "footer_image", "aside_text"}


def build_page_markdown(
    source_name: str,
    page_number: int,
    text_blocks: list[dict],
    lang: str,
    confidence_threshold: float = 0.7,
    layout_mode: bool = False,
) -> str:
    lines: list[str] = []

    lines.append(
        f"<!-- source: {source_name} | page: {page_number} "
        f"| ocr_lang: {lang} | ocr_date: {date.today().isoformat()} -->"
    )
    lines.append("")
    lines.append(f"# Page {page_number}")
    lines.append("")

    if layout_mode:
        _build_layout_blocks(lines, text_blocks)
    else:
        _build_flat_blocks(lines, text_blocks, confidence_threshold)

    lines.append("")
    return "\n".join(lines)


def _build_flat_blocks(
    lines: list[str],
    text_blocks: list[dict],
    confidence_threshold: float,
) -> None:
    low_confidence: list[dict] = []

    for block in text_blocks:
        text = block["text"]
        conf = block["confidence"]

        if conf < confidence_threshold:
            text = f"{text} [?]"
            low_confidence.append(block)

        lines.append(text)

    if low_confidence:
        lines.append("")
        lines.append("---")
        for block in low_confidence:
            lines.append(
                f'<!-- LOW CONFIDENCE: "{block["text"]}" '
                f'(conf: {block["confidence"]:.2f}) -->'
            )


def _build_layout_blocks(lines: list[str], text_blocks: list[dict]) -> None:
    for block in text_blocks:
        label = block.get("label", "text")
        text = block["text"]

        if label in _SKIP_LABELS:
            continue

        if label == "image":
            if text:
                lines.append(f"<!-- [image: {text}] -->")
            continue

        if label == "figure_title":
            lines.append(f"*{text}*")
            lines.append("")
            continue

        prefix = _LABEL_TO_HEADING.get(label, "")
        lines.append(f"{prefix}{text}")
        lines.append("")
