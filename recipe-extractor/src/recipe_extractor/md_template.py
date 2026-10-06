from __future__ import annotations

from datetime import date


def build_page_markdown(
    source_name: str,
    page_number: int,
    text_blocks: list[dict],
    lang: str,
    confidence_threshold: float = 0.7,
) -> str:
    """Build a Markdown string from OCR text blocks.

    Each block in text_blocks is expected to have:
      - "text": the recognized text
      - "confidence": float 0-1
      - "bbox": list of 4 corner points [[x,y], ...]
    """
    lines: list[str] = []

    lines.append(
        f"<!-- source: {source_name} | page: {page_number} "
        f"| ocr_lang: {lang} | ocr_date: {date.today().isoformat()} -->"
    )
    lines.append("")
    lines.append(f"# Page {page_number}")
    lines.append("")

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

    lines.append("")
    return "\n".join(lines)
