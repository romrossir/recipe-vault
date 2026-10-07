from __future__ import annotations

import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path

from rich.progress import Progress

from ..config import OcrConfig
from ..md_template import build_page_markdown
from ..pdf_utils import pdf_to_images


def parse_page_spec(spec: str) -> set[int]:
    pages: set[int] = set()
    for part in spec.split(","):
        part = part.strip()
        if "-" in part:
            start, end = part.split("-", 1)
            pages.update(range(int(start), int(end) + 1))
        else:
            pages.add(int(part))
    return pages


def _is_pdf(path: Path) -> bool:
    return path.suffix.lower() == ".pdf"


def _is_image(path: Path) -> bool:
    return path.suffix.lower() in {".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"}


def _sanitize_stem(name: str) -> str:
    stem = Path(name).stem
    return re.sub(r"[^\w\-]", "_", stem).strip("_")


def _sort_blocks_reading_order(blocks: list[dict]) -> list[dict]:
    """Sort OCR blocks in reading order: top-to-bottom, left-to-right.

    Detects columns by clustering x-coordinates and processes
    each column top-to-bottom before moving to the next.
    """
    if not blocks:
        return blocks

    def y_center(b: dict) -> float:
        bbox = b["bbox"]
        return (bbox[0][1] + bbox[2][1]) / 2

    def x_center(b: dict) -> float:
        bbox = b["bbox"]
        return (bbox[0][0] + bbox[2][0]) / 2

    avg_height = sum(
        abs(b["bbox"][2][1] - b["bbox"][0][1]) for b in blocks
    ) / len(blocks)

    x_centers = sorted(x_center(b) for b in blocks)
    columns: list[list[float]] = []
    for xc in x_centers:
        placed = False
        for col in columns:
            if abs(xc - (sum(col) / len(col))) < avg_height * 3:
                col.append(xc)
                placed = True
                break
        if not placed:
            columns.append([xc])

    col_centers = sorted(sum(c) / len(c) for c in columns)

    def sort_key(b: dict) -> tuple[int, float, float]:
        xc = x_center(b)
        col_idx = 0
        min_dist = float("inf")
        for i, cc in enumerate(col_centers):
            dist = abs(xc - cc)
            if dist < min_dist:
                min_dist = dist
                col_idx = i
        return (col_idx, y_center(b), xc)

    return sorted(blocks, key=sort_key)


def _run_ocr_on_image(ocr_engine, image_path: Path) -> list[dict]:
    result = ocr_engine.predict(str(image_path))

    blocks: list[dict] = []
    if not result:
        return blocks

    for page_result in result:
        res = page_result.json.get("res", {}) if hasattr(page_result, "json") else {}
        texts = res.get("rec_texts") or []
        scores = res.get("rec_scores") or []
        polys = res.get("dt_polys") or []

        for i, text in enumerate(texts):
            confidence = float(scores[i]) if i < len(scores) else 0.0
            bbox = polys[i] if i < len(polys) else [[0, 0], [0, 0], [0, 0], [0, 0]]
            if hasattr(bbox, "tolist"):
                bbox = bbox.tolist()
            blocks.append({
                "text": text,
                "confidence": confidence,
                "bbox": bbox,
            })

    return _sort_blocks_reading_order(blocks)


def _run_layout_ocr_on_page(layout_engine, page_result) -> list[dict]:
    """Extract structured blocks from a PPStructureV3 page result."""
    res = page_result.json.get("res", {}) if hasattr(page_result, "json") else {}
    parsing_list = res.get("parsing_res_list") or []

    blocks: list[dict] = []
    for item in parsing_list:
        label = item.get("block_label", "")
        content = (item.get("block_content") or "").strip()
        if not content:
            continue
        bbox = item.get("block_bbox", [0, 0, 0, 0])
        order = item.get("block_order")
        blocks.append({
            "text": content,
            "label": label,
            "bbox": bbox,
            "block_order": order,
        })

    ordered = [b for b in blocks if b["block_order"] is not None]
    unordered = [b for b in blocks if b["block_order"] is None]
    ordered.sort(key=lambda b: b["block_order"])
    return ordered + unordered


def _write_manifest(manifest_path: Path, manifest: dict) -> None:
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)


def _create_layout_engine(config: OcrConfig):
    from paddleocr import PPStructureV3

    return PPStructureV3(
        lang=config.lang,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        use_seal_recognition=False,
        use_table_recognition=False,
        use_formula_recognition=False,
        use_chart_recognition=False,
        use_region_detection=False,
        engine="onnxruntime",
    )


def _create_ocr_engine(config: OcrConfig):
    from paddleocr import PaddleOCR

    return PaddleOCR(
        lang=config.lang,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        engine="onnxruntime",
    )


def run_ocr(
    input_path: Path,
    config: OcrConfig,
    work_dir: Path,
    pages: set[int] | None = None,
) -> Path:
    """Run OCR on a PDF, image, or directory. Returns the work directory."""
    if config.use_layout:
        engine = _create_layout_engine(config)
    else:
        engine = _create_ocr_engine(config)

    input_path = input_path.resolve()
    files_to_process: list[tuple[Path, bool]] = []

    if input_path.is_dir():
        for child in sorted(input_path.iterdir()):
            if _is_pdf(child):
                files_to_process.append((child, True))
            elif _is_image(child):
                files_to_process.append((child, False))
    elif _is_pdf(input_path):
        files_to_process.append((input_path, True))
    elif _is_image(input_path):
        files_to_process.append((input_path, False))
    else:
        raise ValueError(f"Unsupported file type: {input_path.suffix}")

    for source_file, is_pdf in files_to_process:
        stem = _sanitize_stem(source_file.name)
        source_work_dir = work_dir / stem
        if config.use_layout:
            _process_single_source_layout(engine, source_file, is_pdf, source_work_dir, config, pages)
        else:
            _process_single_source(engine, source_file, is_pdf, source_work_dir, config, pages)

    return work_dir


def _process_single_source_layout(
    layout_engine,
    source_file: Path,
    is_pdf: bool,
    work_dir: Path,
    config: OcrConfig,
    pages: set[int] | None = None,
) -> None:
    source_dir = work_dir / "source"
    pages_dir = work_dir / "pages"
    ocr_dir = work_dir / "ocr"
    json_dir = work_dir / "json"
    push_dir = work_dir / "push"

    for d in [source_dir, pages_dir, ocr_dir, json_dir, push_dir]:
        d.mkdir(parents=True, exist_ok=True)

    dest = source_dir / source_file.name
    if not dest.exists():
        shutil.copy2(source_file, dest)

    if is_pdf:
        image_paths = pdf_to_images(source_file, pages_dir, dpi=config.dpi, pages=pages)
    else:
        dest_page = pages_dir / source_file.name
        shutil.copy2(source_file, dest_page)
        image_paths = [(dest_page, 1)]

    manifest_path = work_dir / "_manifest.json"
    manifest = {
        "source": source_file.name,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "dpi": config.dpi,
        "lang": config.lang,
        "layout": True,
        "total_pages": len(image_paths),
        "pages": {},
    }

    with Progress() as progress:
        task = progress.add_task(f"OCR (layout) {source_file.name}", total=len(image_paths))

        for image_path, page_num in image_paths:
            result = layout_engine.predict(str(image_path))
            blocks: list[dict] = []
            for page_result in result:
                blocks = _run_layout_ocr_on_page(layout_engine, page_result)

            has_content = len(blocks) > 0
            md_content = build_page_markdown(
                source_name=source_file.name,
                page_number=page_num,
                text_blocks=blocks,
                lang=config.lang,
                confidence_threshold=config.confidence_threshold,
                layout_mode=True,
            )

            md_path = ocr_dir / f"page_{page_num:03d}.md"
            with open(md_path, "w", encoding="utf-8") as f:
                f.write(md_content)

            manifest["pages"][str(page_num)] = {
                "ocr_status": "done",
                "has_content": has_content,
                "block_count": len(blocks),
            }

            progress.advance(task)

    _write_manifest(manifest_path, manifest)


def _process_single_source(
    ocr_engine,
    source_file: Path,
    is_pdf: bool,
    work_dir: Path,
    config: OcrConfig,
    pages: set[int] | None = None,
) -> None:
    source_dir = work_dir / "source"
    pages_dir = work_dir / "pages"
    ocr_dir = work_dir / "ocr"
    json_dir = work_dir / "json"
    push_dir = work_dir / "push"

    for d in [source_dir, pages_dir, ocr_dir, json_dir, push_dir]:
        d.mkdir(parents=True, exist_ok=True)

    dest = source_dir / source_file.name
    if not dest.exists():
        shutil.copy2(source_file, dest)

    manifest_path = work_dir / "_manifest.json"
    manifest = {
        "source": source_file.name,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "dpi": config.dpi,
        "lang": config.lang,
        "pages": {},
    }

    if is_pdf:
        image_paths = pdf_to_images(source_file, pages_dir, dpi=config.dpi, pages=pages)
        manifest["total_pages"] = len(image_paths)
    else:
        dest_page = pages_dir / source_file.name
        shutil.copy2(source_file, dest_page)
        image_paths = [(dest_page, 1)]
        manifest["total_pages"] = 1

    with Progress() as progress:
        task = progress.add_task(f"OCR {source_file.name}", total=len(image_paths))

        for image_path, page_num in image_paths:
            blocks = _run_ocr_on_image(ocr_engine, image_path)

            has_content = len(blocks) > 0
            md_content = build_page_markdown(
                source_name=source_file.name,
                page_number=page_num,
                text_blocks=blocks,
                lang=config.lang,
                confidence_threshold=config.confidence_threshold,
            )

            md_path = ocr_dir / f"page_{page_num:03d}.md"
            with open(md_path, "w", encoding="utf-8") as f:
                f.write(md_content)

            manifest["pages"][str(page_num)] = {
                "ocr_status": "done",
                "has_content": has_content,
                "block_count": len(blocks),
            }

            progress.advance(task)

    _write_manifest(manifest_path, manifest)
