import { useEffect, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { getSourceContentUrl } from '../api/recipeApi';
import type { RecipeSearchResponse } from '../api/types';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

interface SourceViewerProps {
  recipe: RecipeSearchResponse;
}

export default function SourceViewer({ recipe }: SourceViewerProps) {
  const { sourceFileId, sourcePages } = recipe;
  const [contentType, setContentType] = useState<string | null>(null);

  const url = sourceFileId ? getSourceContentUrl(sourceFileId) : null;

  useEffect(() => {
    if (!url) return;
    setContentType(null);
    fetch(url, { method: 'HEAD' }).then((res) => {
      setContentType(res.headers.get('Content-Type') ?? '');
    });
  }, [url]);

  if (!sourceFileId || !url) {
    return (
      <div className="source-viewer empty">
        <p>Pas de fichier source pour cette recette.</p>
      </div>
    );
  }

  if (contentType === null) {
    return (
      <div className="source-viewer empty">
        <p>Chargement...</p>
      </div>
    );
  }

  if (contentType.startsWith('image/')) {
    return <ImageViewer url={url} />;
  }

  const initialPage = sourcePages.length > 0 ? sourcePages[0] : 1;
  return <PdfViewer url={url} initialPage={initialPage} />;
}

function ImageViewer({ url }: { url: string }) {
  return (
    <div className="source-viewer image-viewer">
      <img src={url} alt="Source" />
    </div>
  );
}

function PdfViewer({ url, initialPage }: { url: string; initialPage: number }) {
  const [numPages, setNumPages] = useState<number>(0);
  const [pageNumber, setPageNumber] = useState<number>(initialPage);

  function onDocumentLoadSuccess({ numPages }: { numPages: number }) {
    setNumPages(numPages);
    setPageNumber(Math.min(initialPage, numPages));
  }

  return (
    <div className="source-viewer pdf-viewer">
      <div className="pdf-controls">
        <button
          onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
          disabled={pageNumber <= 1}
        >
          &larr;
        </button>
        <span>
          Page {pageNumber} / {numPages || '...'}
        </span>
        <button
          onClick={() => setPageNumber((p) => Math.min(numPages, p + 1))}
          disabled={pageNumber >= numPages}
        >
          &rarr;
        </button>
      </div>

      <div className="pdf-document">
        <Document file={url} onLoadSuccess={onDocumentLoadSuccess}>
          <Page pageNumber={pageNumber} width={700} />
        </Document>
      </div>
    </div>
  );
}
