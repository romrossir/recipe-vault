from __future__ import annotations

import json
from pathlib import Path

import click
from rich.console import Console
from rich.table import Table

from .api_client import RecipeApiClient
from .config import load_config
from .ollama_client import OllamaClient
from .stages.ocr import parse_page_spec, run_ocr
from .stages.push import run_push
from .stages.structure import run_structure

console = Console()


def _resolve_config_dir() -> Path:
    return Path(__file__).resolve().parent.parent.parent


@click.group()
def cli() -> None:
    """Recipe extractor pipeline — extract recipes from magazine PDFs and photos."""


@cli.command()
@click.argument("input_path", type=click.Path(exists=True, path_type=Path))
@click.option("-o", "--output-dir", type=click.Path(path_type=Path), default=None)
@click.option("-l", "--lang", default=None, help="OCR language (default: from config)")
@click.option("--dpi", type=int, default=None, help="DPI for PDF rendering (default: from config)")
@click.option("-p", "--pages", default=None, help="Pages to process, e.g. '5,12,45-60,73'")
def ocr(input_path: Path, output_dir: Path | None, lang: str | None, dpi: int | None, pages: str | None) -> None:
    """Stage 1: Run OCR on images or PDFs and produce Markdown files."""
    config = load_config(_resolve_config_dir())

    if lang:
        config.ocr.lang = lang
    if dpi:
        config.ocr.dpi = dpi

    work_dir = output_dir or Path(config.work_dir)
    page_set = parse_page_spec(pages) if pages else None

    result_dir = run_ocr(input_path, config.ocr, work_dir, pages=page_set)

    console.print()
    console.print("[bold green]OCR complete.[/bold green]")
    console.print(f"Review and edit the Markdown files in: [cyan]{result_dir}[/cyan]")
    console.print()
    console.print("When done reviewing, run:")
    console.print(f"  [bold]recipe-extractor structure {result_dir}[/bold]")


@cli.command()
@click.argument("md_dir", type=click.Path(exists=True, path_type=Path))
@click.option("-o", "--output-dir", type=click.Path(path_type=Path), default=None)
@click.option("--model", default=None, help="Override Ollama model")
def structure(md_dir: Path, output_dir: Path | None, model: str | None) -> None:
    """Stage 3: Convert validated Markdown files to structured JSON using LLM."""
    config = load_config(_resolve_config_dir())

    if model:
        config.ollama.model = model

    if output_dir is None:
        output_dir = md_dir.parent / "json"

    ollama = OllamaClient(config.ollama)
    try:
        written = run_structure(md_dir, output_dir, ollama)
    finally:
        ollama.close()

    if written:
        console.print()
        console.print("To push recipes to the API, run:")
        console.print(f"  [bold]recipe-extractor push {output_dir}[/bold]")


@cli.command()
@click.argument("json_dir", type=click.Path(exists=True, path_type=Path))
@click.option(
    "--source-file",
    type=click.Path(exists=True, path_type=Path),
    default=None,
    help="Original PDF to upload as source file",
)
@click.option("--dry-run", is_flag=True, help="Show what would be pushed without calling the API")
def push(json_dir: Path, source_file: Path | None, dry_run: bool) -> None:
    """Stage 4: Push structured JSON recipes to the RecipeApi."""
    config = load_config(_resolve_config_dir())

    api_client = RecipeApiClient(config.api)
    try:
        run_push(json_dir, api_client, source_file=source_file, dry_run=dry_run)
    finally:
        api_client.close()


@cli.command()
@click.argument("input_path", type=click.Path(exists=True, path_type=Path))
@click.option("--no-push", is_flag=True, help="Stop after structuring (skip push)")
@click.option("--skip-ocr", is_flag=True, help="Skip OCR, start from existing .md files")
@click.option("--source-file", type=click.Path(exists=True, path_type=Path), default=None)
@click.option("-p", "--pages", default=None, help="Pages to process, e.g. '5,12,45-60,73'")
def run(
    input_path: Path,
    no_push: bool,
    skip_ocr: bool,
    source_file: Path | None,
    pages: str | None,
) -> None:
    """Run the full pipeline: OCR -> manual review -> structure -> push."""
    config = load_config(_resolve_config_dir())
    work_dir = Path(config.work_dir)
    page_set = parse_page_spec(pages) if pages else None

    if not skip_ocr:
        console.print("[bold]Stage 1: OCR extraction[/bold]")
        run_ocr(input_path, config.ocr, work_dir, pages=page_set)

        console.print()
        console.print("[bold yellow]Stage 2: Manual review[/bold yellow]")
        console.print(f"Edit the .md files in [cyan]{work_dir}[/cyan], then press Enter to continue.")
        console.print("Compare with page images in the pages/ subdirectory.")
        click.pause("Press any key when ready to continue...")
        console.print()

    md_dirs = sorted(work_dir.glob("*/ocr"))
    if not md_dirs:
        console.print("[red]No OCR output found.[/red]")
        return

    console.print("[bold]Stage 3: LLM structuring[/bold]")
    ollama = OllamaClient(config.ollama)
    all_json_dirs: list[Path] = []
    try:
        for md_dir in md_dirs:
            json_dir = md_dir.parent / "json"
            run_structure(md_dir, json_dir, ollama)
            all_json_dirs.append(json_dir)
    finally:
        ollama.close()

    if no_push:
        console.print("[cyan]Stopped before push (--no-push).[/cyan]")
        return

    console.print()
    console.print("[bold]Stage 4: Push to RecipeApi[/bold]")
    api_client = RecipeApiClient(config.api)
    try:
        for json_dir in all_json_dirs:
            src = source_file or _find_source_file(json_dir.parent / "source")
            run_push(json_dir, api_client, source_file=src)
    finally:
        api_client.close()

    console.print()
    console.print("[bold green]Pipeline complete![/bold green]")


@cli.command()
@click.argument("work_dir", type=click.Path(exists=True, path_type=Path))
def status(work_dir: Path) -> None:
    """Show the processing status of a work directory."""
    work_dir = Path(work_dir).resolve()

    for sub in sorted(work_dir.iterdir()):
        manifest_path = sub / "_manifest.json"
        if not manifest_path.exists():
            continue

        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)

        table = Table(title=manifest.get("source", sub.name))
        table.add_column("Metric", style="cyan")
        table.add_column("Value", style="green")

        total = manifest.get("total_pages", 0)
        pages = manifest.get("pages", {})
        ocr_done = sum(1 for p in pages.values() if p.get("ocr_status") == "done")
        with_content = sum(1 for p in pages.values() if p.get("has_content"))

        json_dir = sub / "json"
        json_count = len(list(json_dir.glob("*.json"))) if json_dir.exists() else 0

        push_log = sub / "push" / "_push_log.json"
        pushed = 0
        if push_log.exists():
            with open(push_log, encoding="utf-8") as f:
                log = json.load(f)
                pushed = log.get("success", 0)

        table.add_row("Total pages", str(total))
        table.add_row("OCR done", str(ocr_done))
        table.add_row("Pages with content", str(with_content))
        table.add_row("Recipes extracted (JSON)", str(json_count))
        table.add_row("Recipes pushed", str(pushed))

        console.print(table)
        console.print()


def _find_source_file(source_dir: Path) -> Path | None:
    if not source_dir.exists():
        return None
    files = list(source_dir.iterdir())
    return files[0] if files else None
