#!/usr/bin/env python3
"""
Extract shift-type icons from a POLYPOINT/PEP shift plan PDF.

The script reads the Dienst-Legende on the last page of the PDF, matches each
icon's position to the nearby shift code and name, then saves the icons as
JPEG files into config/icons/.

Usage:
    python scripts/extract_icons.py "Mai Plan Definitiv.pdf"
    python scripts/extract_icons.py path/to/plan.pdf --out config/icons

Requirements:
    pip install pymupdf
"""

import argparse
import os
import sys

try:
    import fitz
except ImportError:
    sys.exit("PyMuPDF is required: pip install pymupdf")


def extract_icons(pdf_path: str, out_dir: str) -> None:
    doc = fitz.open(pdf_path)
    legend_page = doc[-1]

    # Collect image bounding boxes: xref -> list of Rect
    xref_to_rects: dict[int, list] = {}
    for img in legend_page.get_images(full=True):
        xref = img[0]
        rects = legend_page.get_image_rects(xref)
        xref_to_rects[xref] = list(rects)

    # Collect text spans with positions
    spans: list[dict] = []
    for block in legend_page.get_text("dict")["blocks"]:
        if block["type"] != 0:
            continue
        for line in block["lines"]:
            for span in line["spans"]:
                spans.append({"text": span["text"].strip(), "bbox": span["bbox"]})

    def text_near(x: float, y: float, dx: float = 40, dy: float = 6) -> str:
        """Return concatenated text of spans within (dx, dy) of (x, y)."""
        matches = [
            s["text"]
            for s in spans
            if abs(s["bbox"][0] - x) < dx and abs(s["bbox"][1] - y) < dy and s["text"]
        ]
        return " ".join(matches)

    os.makedirs(out_dir, exist_ok=True)
    saved = 0

    for xref, rects in xref_to_rects.items():
        img_info = doc.extract_image(xref)
        w, h = img_info["width"], img_info["height"]

        for rect in rects:
            # Look for a shift code (numeric) just to the right of the icon
            nearby = text_near(rect.x1, rect.y0, dx=50, dy=8)
            parts = nearby.split()
            code = next((p for p in parts if p.isdigit()), None)
            if code is None:
                continue

            filename = f"{code}.png"
            path = os.path.join(out_dir, filename)
            fitz.Pixmap(img_info["image"]).save(path)
            print(f"Saved {path} ({w}x{h} png)  —  {nearby}")
            saved += 1
            break  # one file per xref

    print(f"\n{saved} icons saved to {out_dir}/")


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract shift icons from a PEP PDF.")
    parser.add_argument("pdf", help="Path to the shift plan PDF")
    parser.add_argument("--out", default="Server/config/icons", help="Output directory (default: Server/config/icons)")
    args = parser.parse_args()

    if not os.path.isfile(args.pdf):
        sys.exit(f"File not found: {args.pdf}")

    extract_icons(args.pdf, args.out)


if __name__ == "__main__":
    main()
