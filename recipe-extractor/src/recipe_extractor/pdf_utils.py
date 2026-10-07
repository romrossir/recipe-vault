from __future__ import annotations

from pathlib import Path

import pymupdf as fitz


def pdf_to_images(
    pdf_path: Path,
    output_dir: Path,
    dpi: int = 300,
    pages: set[int] | None = None,
) -> list[tuple[Path, int]]:
    output_dir.mkdir(parents=True, exist_ok=True)
    zoom = dpi / 72
    matrix = fitz.Matrix(zoom, zoom)

    result: list[tuple[Path, int]] = []
    doc = fitz.open(pdf_path)
    try:
        for page_idx in range(len(doc)):
            page_num = page_idx + 1
            if pages and page_num not in pages:
                continue
            page = doc[page_idx]
            pix = page.get_pixmap(matrix=matrix)
            out_path = output_dir / f"page_{page_num:03d}.png"
            pix.save(str(out_path))
            result.append((out_path, page_num))
    finally:
        doc.close()

    return result


def pdf_page_count(pdf_path: Path) -> int:
    doc = fitz.open(pdf_path)
    try:
        return len(doc)
    finally:
        doc.close()
