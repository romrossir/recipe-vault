from __future__ import annotations

from pathlib import Path

import pymupdf as fitz


def pdf_to_images(pdf_path: Path, output_dir: Path, dpi: int = 300) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    zoom = dpi / 72
    matrix = fitz.Matrix(zoom, zoom)

    paths: list[Path] = []
    doc = fitz.open(pdf_path)
    try:
        for page_num in range(len(doc)):
            page = doc[page_num]
            pix = page.get_pixmap(matrix=matrix)
            out_path = output_dir / f"page_{page_num + 1:03d}.png"
            pix.save(str(out_path))
            paths.append(out_path)
    finally:
        doc.close()

    return paths


def pdf_page_count(pdf_path: Path) -> int:
    doc = fitz.open(pdf_path)
    try:
        return len(doc)
    finally:
        doc.close()
