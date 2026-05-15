from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from pdfrw import PdfReader as PdfrwReader, PdfWriter as PdfrwWriter
from pypdf import PdfReader, PdfWriter
from reportlab.lib.pagesizes import letter
from reportlab.pdfgen import canvas

CURRENT_DATETIME = "2026-05-15T08:35:56.433+01:00"
OUTPUT_DIR = Path(__file__).resolve().parent
FIELD_VALUES = {"Name": "Alice", "City": "Hyrule"}
OBJECT_RE = re.compile(r"(?ms)^(\d+) 0 obj\n(.*?)\nendobj\n")
TRAILER_RE = re.compile(r"(?ms)trailer\s*(<<.*?>>)\s*startxref\s*\d+\s*%%EOF\s*$")
INFO_RE = re.compile(r"/Info\s+(\d+)\s+0\s+R")
SIZE_RE = re.compile(r"/Size\s+\d+")
LEADING_DOT_RE = re.compile(r"(?<![0-9])\.(\d+)")


def build_reportlab_source(path: Path) -> None:
    pdf = canvas.Canvas(str(path), pagesize=letter)
    pdf.setTitle("PDFFlatten Producer Fixture")
    pdf.setSubject("Non-sensitive AcroForm test fixture")
    pdf.setAuthor("PDFFlatten")
    pdf.setCreator("PDFFlatten")
    pdf.setFont("Helvetica", 12)
    pdf.drawString(72, 720, "Producer corpus fixture")
    form = pdf.acroForm
    form.textfield(name="Name", x=72, y=680, width=180, height=24, value=FIELD_VALUES["Name"], borderStyle="solid", forceBorder=True)
    form.textfield(name="City", x=72, y=640, width=180, height=24, value=FIELD_VALUES["City"], borderStyle="solid", forceBorder=True)
    pdf.save()


def build_pypdf_source(input_path: Path, output_path: Path) -> None:
    reader = PdfReader(str(input_path))
    writer = PdfWriter()
    writer.clone_document_from_reader(reader)
    writer.add_metadata({
        "/Producer": "pypdf",
        "/Title": "PDFFlatten Producer Fixture",
        "/Subject": "Non-sensitive AcroForm test fixture",
    })
    with output_path.open("wb") as stream:
        writer.write(stream)


def build_pdfrw_source(input_path: Path, output_path: Path) -> None:
    pdf = PdfrwReader(str(input_path))
    PdfrwWriter().write(str(output_path), pdf)


def sanitize_pdf(source_path: Path, destination_path: Path, *, producer: str, title: str, normalize_leading_dot_decimals: bool) -> dict[str, object]:
    text = source_path.read_text("latin1")
    trailer_match = TRAILER_RE.search(text)
    if trailer_match is None:
        raise RuntimeError(f"Could not find trailer in {source_path.name}.")

    header_end = text.index("1 0 obj\n")
    header = text[:header_end]
    trailer = trailer_match.group(1)
    info_match = INFO_RE.search(trailer)
    info_object_number = int(info_match.group(1)) if info_match else None

    objects: list[tuple[int, str]] = []
    for match in OBJECT_RE.finditer(text):
        object_number = int(match.group(1))
        body = match.group(2)
        if object_number == info_object_number:
            body = (
                "<<\n"
                f"/Producer ({producer})\n"
                f"/Title ({title})\n"
                "/Subject (Non-sensitive AcroForm test fixture)\n"
                "/Author ()\n"
                "/Creator ()\n"
                ">>"
            )
        if normalize_leading_dot_decimals:
            body = LEADING_DOT_RE.sub(r"0.\1", body)
        objects.append((object_number, body))

    max_object_number = max(number for number, _ in objects)
    trailer = SIZE_RE.sub(f"/Size {max_object_number + 1}", trailer)

    chunks = [header]
    offsets: dict[int, int] = {}
    current_offset = len(header.encode("latin1"))
    for object_number, body in sorted(objects):
        serialized = f"{object_number} 0 obj\n{body}\nendobj\n"
        offsets[object_number] = current_offset
        chunks.append(serialized)
        current_offset += len(serialized.encode("latin1"))

    startxref = current_offset
    xref_lines = ["xref\n", f"0 {max_object_number + 1}\n", "0000000000 65535 f \n"]
    for object_number in range(1, max_object_number + 1):
        offset = offsets.get(object_number, 0)
        status = "n" if object_number in offsets else "f"
        xref_lines.append(f"{offset:010d} 00000 {status} \n")

    chunks.extend(xref_lines)
    chunks.append(f"trailer\n{trailer}\nstartxref\n{startxref}\n%%EOF\n")
    destination_path.write_text("".join(chunks), "latin1")

    data = destination_path.read_bytes()
    sha256 = hashlib.sha256(data).hexdigest()
    return {
        "filename": destination_path.name,
        "sha256": sha256,
        "producer": producer,
        "normalize_leading_dot_decimals": normalize_leading_dot_decimals,
        "has_leading_dot_decimal_tokens": b" Tf .1 .1 .1 rg" in data or b"[.1 .1 .1]" in data,
    }


def main() -> None:
    intermediate = OUTPUT_DIR / ".build"
    intermediate.mkdir(exist_ok=True)

    reportlab_source = intermediate / "reportlab-source.pdf"
    pypdf_source = intermediate / "pypdf-source.pdf"
    pdfrw_source = intermediate / "pdfrw-source.pdf"

    build_reportlab_source(reportlab_source)
    build_pypdf_source(reportlab_source, pypdf_source)
    build_pdfrw_source(reportlab_source, pdfrw_source)

    fixtures = [
        {
            "path": OUTPUT_DIR / "reportlab-textfields-raw.pdf",
            "source": reportlab_source,
            "producer": r"ReportLab PDF Library - \(opensource\)",
            "manifest_producer": "ReportLab PDF Library - (opensource)",
            "title": "PDFFlatten Producer Fixture - ReportLab Raw",
            "normalize": False,
            "expected": {"outcome": "reject", "exception": "NotSupportedException", "message_contains": "Unsupported PDF keyword '.1'."},
            "provenance": [
                "Generated locally with reportlab.",
                "Metadata scrubbed to remove creation/modification timestamps.",
                "Kept with leading-dot decimals to lock current fail-closed parser behavior.",
            ],
        },
        {
            "path": OUTPUT_DIR / "reportlab-textfields-classic-xref.pdf",
            "source": reportlab_source,
            "producer": r"ReportLab PDF Library - \(opensource\)",
            "manifest_producer": "ReportLab PDF Library - (opensource)",
            "title": "PDFFlatten Producer Fixture - ReportLab Sanitized",
            "normalize": True,
            "expected": {"outcome": "success"},
            "provenance": [
                "Generated locally with reportlab.",
                "Metadata scrubbed to remove creation/modification timestamps.",
                "Leading-dot decimals normalized to zero-prefixed decimals and classic xref rebuilt.",
            ],
        },
        {
            "path": OUTPUT_DIR / "pdfrw-textfields-raw.pdf",
            "source": pdfrw_source,
            "producer": r"\(pdfrw\)",
            "manifest_producer": "pdfrw",
            "title": "PDFFlatten Producer Fixture - pdfrw Raw",
            "normalize": False,
            "expected": {"outcome": "reject", "exception": "NotSupportedException", "message_contains": "Unsupported PDF keyword '.1'."},
            "provenance": [
                "Generated by rewriting the reportlab source with pdfrw.",
                "Metadata scrubbed to remove creation/modification timestamps.",
                "Kept with leading-dot decimals to lock current fail-closed parser behavior.",
            ],
        },
        {
            "path": OUTPUT_DIR / "pdfrw-textfields-classic-xref.pdf",
            "source": pdfrw_source,
            "producer": r"\(pdfrw\)",
            "manifest_producer": "pdfrw",
            "title": "PDFFlatten Producer Fixture - pdfrw Sanitized",
            "normalize": True,
            "expected": {"outcome": "success"},
            "provenance": [
                "Generated by rewriting the reportlab source with pdfrw.",
                "Metadata scrubbed to remove creation/modification timestamps.",
                "Leading-dot decimals normalized to zero-prefixed decimals and classic xref rebuilt.",
            ],
        },
        {
            "path": OUTPUT_DIR / "pypdf-textfields-classic-xref.pdf",
            "source": pypdf_source,
            "producer": "pypdf",
            "manifest_producer": "pypdf",
            "title": "PDFFlatten Producer Fixture - pypdf",
            "normalize": False,
            "expected": {"outcome": "success"},
            "provenance": [
                "Generated by cloning the reportlab source with pypdf.",
                "Metadata scrubbed to remove creation/modification timestamps.",
                "No further structural normalization required beyond xref rebuild during metadata scrubbing.",
            ],
        },
    ]

    manifest_entries = []
    for fixture in fixtures:
        result = sanitize_pdf(
            fixture["source"],
            fixture["path"],
            producer=fixture["producer"],
            title=fixture["title"],
            normalize_leading_dot_decimals=fixture["normalize"],
        )
        manifest_entries.append(
            {
                "filename": result["filename"],
                "sha256": result["sha256"],
                "source_producer": fixture["manifest_producer"],
                "pdf_producer_literal": result["producer"],
                "contains_only_generic_field_values": True,
                "field_values": FIELD_VALUES,
                "expected": fixture["expected"],
                "sanitization": fixture["provenance"],
                "leading_dot_decimal_tokens_present": result["has_leading_dot_decimal_tokens"],
            }
        )

    manifest = {
        "reviewed_at": CURRENT_DATETIME,
        "safety": {
            "summary": "All fixtures are locally generated, non-sensitive, and limited to two generic text fields with placeholder values.",
            "checks": [
                "No personal or customer data.",
                "No JavaScript, attachments, encryption, or signatures.",
                "Classic xref tables only after sanitization.",
            ],
        },
        "fixtures": manifest_entries,
    }

    (OUTPUT_DIR / "provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    for file in intermediate.iterdir():
        file.unlink()
    intermediate.rmdir()


if __name__ == "__main__":
    main()
