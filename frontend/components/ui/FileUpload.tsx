"use client";

import { useCallback, useRef, useState } from "react";
import {
  CloudUpload,
  FileText,
  X,
  AlertCircle,
  CheckCircle2,
  Loader2,
  Lock,
} from "lucide-react";

/* ── Types ── */
interface PendingFile {
  file: File;
  id: string;
  progress: number;
  status: "pending" | "uploading" | "done" | "error";
  error?: string;
}

interface FileUploadProps {
  /** Accepted MIME types / extensions */
  accept?: string;
  /** Max file size in bytes (default: 20 MB) */
  maxSizeBytes?: number;
  /** Max number of files (default: 10) */
  maxFiles?: number;
  /** Called for each file when user confirms upload */
  onUpload?: (file: File, onProgress: (pct: number) => void) => Promise<void>;
  /** Called when all files are uploaded */
  onComplete?: (files: File[]) => void;
  /** Descriptive label shown above the dropzone */
  label?: string;
}

const ALLOWED_TYPES = [
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "image/jpeg",
  "image/png",
];

const ALLOWED_EXTENSIONS = [".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png"];

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function getExtension(name: string): string {
  const idx = name.lastIndexOf(".");
  return idx >= 0 ? name.slice(idx).toLowerCase() : "";
}

let fileCounter = 0;

export default function FileUpload({
  accept = ".pdf,.doc,.docx,.jpg,.jpeg,.png",
  maxSizeBytes = 20 * 1024 * 1024,
  maxFiles = 10,
  onUpload,
  onComplete,
  label = "Učitajte pravne dokumente",
}: FileUploadProps) {
  const [files, setFiles] = useState<PendingFile[]>([]);
  const [dragOver, setDragOver] = useState(false);
  const [globalError, setGlobalError] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const addFiles = useCallback(
    (incoming: FileList | File[]) => {
      setGlobalError("");
      const arr = Array.from(incoming);
      const newFiles: PendingFile[] = [];

      for (const file of arr) {
        // Count check
        if (files.length + newFiles.length >= maxFiles) {
          setGlobalError(`Maksimalno ${maxFiles} datoteka odjednom.`);
          break;
        }
        // Type check
        const ext = getExtension(file.name);
        if (!ALLOWED_TYPES.includes(file.type) && !ALLOWED_EXTENSIONS.includes(ext)) {
          setGlobalError(`Format "${ext || file.type}" nije dopušten. Koristite: ${ALLOWED_EXTENSIONS.join(", ")}`);
          continue;
        }
        // Size check
        if (file.size > maxSizeBytes) {
          setGlobalError(`Datoteka "${file.name}" prelazi ograničenje od ${formatSize(maxSizeBytes)}.`);
          continue;
        }

        newFiles.push({
          file,
          id: `file-${++fileCounter}`,
          progress: 0,
          status: "pending",
        });
      }

      if (newFiles.length > 0) {
        setFiles((prev) => [...prev, ...newFiles]);
      }
    },
    [files.length, maxFiles, maxSizeBytes]
  );

  const removeFile = (id: string) => {
    setFiles((prev) => prev.filter((f) => f.id !== id));
  };

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      if (e.dataTransfer.files.length) addFiles(e.dataTransfer.files);
    },
    [addFiles]
  );

  const handleUploadAll = async () => {
    if (!onUpload) return;
    const pending = files.filter((f) => f.status === "pending");

    for (const pf of pending) {
      setFiles((prev) =>
        prev.map((f) => (f.id === pf.id ? { ...f, status: "uploading" as const } : f))
      );
      try {
        await onUpload(pf.file, (pct) => {
          setFiles((prev) =>
            prev.map((f) => (f.id === pf.id ? { ...f, progress: pct } : f))
          );
        });
        setFiles((prev) =>
          prev.map((f) => (f.id === pf.id ? { ...f, status: "done" as const, progress: 100 } : f))
        );
      } catch (err) {
        setFiles((prev) =>
          prev.map((f) =>
            f.id === pf.id
              ? { ...f, status: "error" as const, error: err instanceof Error ? err.message : "Greška pri uploadu" }
              : f
          )
        );
      }
    }

    const doneFiles = files.filter((f) => f.status === "pending").map((f) => f.file);
    onComplete?.(doneFiles);
  };

  const pendingCount = files.filter((f) => f.status === "pending").length;
  const uploadingCount = files.filter((f) => f.status === "uploading").length;

  return (
    <div className="space-y-4">
      {/* Label */}
      {label && <p className="text-sm font-medium text-slate-700">{label}</p>}

      {/* Dropzone */}
      <div
        onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        onClick={() => inputRef.current?.click()}
        className={`relative cursor-pointer rounded-xl border-2 border-dashed p-8 text-center transition-all ${
          dragOver
            ? "border-blue-400 bg-blue-50/50"
            : "border-slate-300 bg-[#1B2A4A]/[0.02] hover:border-[#1B2A4A]/40 hover:bg-[#1B2A4A]/[0.04]"
        }`}
      >
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={accept}
          onChange={(e) => {
            if (e.target.files) addFiles(e.target.files);
            e.target.value = "";
          }}
          className="hidden"
        />

        <div className="flex flex-col items-center gap-3">
          <div className={`rounded-full p-3 transition ${dragOver ? "bg-blue-100" : "bg-slate-100"}`}>
            <CloudUpload className={`h-8 w-8 ${dragOver ? "text-blue-500" : "text-[#1B2A4A]/60"}`} />
          </div>
          <div>
            <p className="text-sm font-medium text-slate-700">
              Povucite datoteke ovdje ili <span className="text-[#1B2A4A] underline underline-offset-2">odaberite ručno</span>
            </p>
            <p className="mt-1 text-xs text-slate-400">
              PDF, DOCX, JPG · Maks. {formatSize(maxSizeBytes)} po datoteci
            </p>
          </div>
        </div>
      </div>

      {/* Error banner */}
      {globalError && (
        <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <AlertCircle className="h-4 w-4 shrink-0" />
          {globalError}
        </div>
      )}

      {/* File list */}
      {files.length > 0 && (
        <div className="space-y-2">
          {files.map((pf) => (
            <div
              key={pf.id}
              className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white px-4 py-3"
            >
              <FileText className="h-5 w-5 text-slate-400 shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-slate-700 truncate">{pf.file.name}</p>
                <p className="text-xs text-slate-400">{formatSize(pf.file.size)}</p>
                {/* Progress bar */}
                {(pf.status === "uploading" || pf.status === "done") && (
                  <div className="mt-1.5 h-1 rounded-full bg-slate-100 overflow-hidden">
                    <div
                      className={`h-full rounded-full transition-all duration-300 ${
                        pf.status === "done" ? "bg-green-500" : "bg-blue-500"
                      }`}
                      style={{ width: `${pf.progress}%` }}
                    />
                  </div>
                )}
                {pf.status === "error" && (
                  <p className="mt-1 text-xs text-red-500">{pf.error}</p>
                )}
              </div>
              {/* Status icon or remove */}
              {pf.status === "done" ? (
                <CheckCircle2 className="h-4 w-4 text-green-500 shrink-0" />
              ) : pf.status === "uploading" ? (
                <Loader2 className="h-4 w-4 animate-spin text-blue-500 shrink-0" />
              ) : (
                <button
                  onClick={(e) => { e.stopPropagation(); removeFile(pf.id); }}
                  className="p-1 rounded hover:bg-red-50 text-slate-400 hover:text-red-500 transition"
                  title="Ukloni"
                >
                  <X className="h-4 w-4" />
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Upload button */}
      {pendingCount > 0 && onUpload && (
        <button
          onClick={handleUploadAll}
          disabled={uploadingCount > 0}
          className="inline-flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#243660] disabled:opacity-50 transition shadow-sm"
        >
          {uploadingCount > 0 ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" />
              Učitavanje...
            </>
          ) : (
            <>
              <CloudUpload className="h-4 w-4" />
              Učitaj {pendingCount} {pendingCount === 1 ? "datoteku" : "datoteka"}
            </>
          )}
        </button>
      )}

      {/* Security notice */}
      <div className="flex items-center gap-2 text-[11px] text-slate-400">
        <Lock className="h-3 w-3 shrink-0" />
        Sve datoteke su enkriptirane (AES-256) prije pohrane.
      </div>
    </div>
  );
}
