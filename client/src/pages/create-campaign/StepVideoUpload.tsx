import { useRef, useState, type DragEvent } from 'react';
import { InputText } from 'primereact/inputtext';
import { Button } from 'primereact/button';
import { ProgressBar } from 'primereact/progressbar';
import { Message } from 'primereact/message';

const MAX_FILE_SIZE = 2 * 1024 * 1024 * 1024; // 2 GB
const ACCEPTED_TYPES = ['video/mp4', 'video/quicktime'];

export interface VideoUploadData {
  name: string;
  file: File | null;
}

interface Props {
  data: VideoUploadData;
  onChange: (data: VideoUploadData) => void;
  onNext: () => void;
  uploading: boolean;
  uploadProgress: number;
  uploadError: string | null;
  creating?: boolean;
}

export default function StepVideoUpload({
  data,
  onChange,
  onNext,
  uploading,
  uploadProgress,
  uploadError,
  creating = false,
}: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);

  // Drop zone is locked while upload is in progress to prevent replacing file mid-upload
  const dropLocked = uploading || (data.file !== null && uploadProgress > 0);

  // "Next" requires name filled and upload fully complete (videoId exists, indicated by uploadProgress === 100)
  const canProceed = data.name.trim() !== '' && uploadProgress === 100 && !uploading;

  function validateAndSetFile(file: File) {
    if (!ACCEPTED_TYPES.includes(file.type)) {
      setFileError('Only MP4 and MOV files are supported.');
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      setFileError('File must be smaller than 2 GB.');
      return;
    }
    setFileError(null);
    onChange({ ...data, file });
  }

  function handleDrop(e: DragEvent) {
    e.preventDefault();
    setDragOver(false);
    if (dropLocked) return;
    const file = e.dataTransfer.files[0];
    if (file) validateAndSetFile(file);
  }

  function handleFileInput(e: React.ChangeEvent<HTMLInputElement>) {
    if (dropLocked) return;
    const file = e.target.files?.[0];
    if (file) validateAndSetFile(file);
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Campaign Name */}
      <div className="flex flex-col gap-2">
        <label htmlFor="campaignName" className="text-sm font-medium text-gray-700">
          Campaign Name *
        </label>
        <InputText
          id="campaignName"
          value={data.name}
          onChange={(e) => onChange({ ...data, name: e.target.value })}
          placeholder="e.g. Open Day Spring 2026"
          className="w-full"
        />
      </div>

      {/* Drag & Drop Video Upload */}
      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-gray-700">Video File *</label>
        <div
          onDragOver={(e) => { e.preventDefault(); if (!dropLocked) setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          onClick={() => { if (!dropLocked) fileInputRef.current?.click(); }}
          className={`flex flex-col items-center justify-center border-2 border-dashed rounded-xl p-10 transition-colors ${
            dropLocked
              ? 'border-gray-200 bg-gray-100 cursor-not-allowed opacity-70'
              : dragOver
              ? 'border-blue-500 bg-blue-50 cursor-pointer'
              : 'border-gray-300 bg-gray-50 hover:border-gray-400 cursor-pointer'
          }`}
        >
          {uploading ? (
            <i className="pi pi-spin pi-spinner text-3xl text-blue-500 mb-2" />
          ) : (
            <i className="pi pi-cloud-upload text-4xl text-gray-400 mb-3" />
          )}

          {data.file ? (
            <div className="text-center">
              <p className="text-sm font-medium text-gray-900">{data.file.name}</p>
              <p className="text-xs text-gray-500 mt-1">
                {(data.file.size / (1024 * 1024)).toFixed(1)} MB
              </p>
              {uploading && (
                <p className="text-xs text-blue-600 mt-1">Uploading & starting transcription…</p>
              )}
              {uploadProgress === 100 && !uploading && (
                <p className="text-xs text-green-600 mt-1">
                  <i className="pi pi-check mr-1" />
                  Uploaded — transcription running in background
                </p>
              )}
            </div>
          ) : (
            <>
              <p className="text-sm text-gray-600">
                Drag & drop your video here, or <span className="text-blue-600 font-medium">browse</span>
              </p>
              <p className="text-xs text-gray-400 mt-1">MP4 or MOV, up to 2 GB</p>
              <p className="text-xs text-gray-400">Transcription starts automatically on upload</p>
            </>
          )}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept=".mp4,.mov,video/mp4,video/quicktime"
          onChange={handleFileInput}
          className="hidden"
          disabled={dropLocked}
        />
      </div>

      {fileError && <Message severity="error" text={fileError} className="w-full" />}
      {uploadError && <Message severity="error" text={uploadError} className="w-full" />}

      {uploading && (
        <div className="space-y-1">
          <p className="text-sm text-gray-600">Uploading…</p>
          <ProgressBar value={uploadProgress} />
        </div>
      )}

      {/* Actions */}
      <div className="flex justify-end gap-3 pt-4">
        <Button
          label="Next"
          icon="pi pi-arrow-right"
          iconPos="right"
          onClick={onNext}
          loading={creating}
          disabled={!canProceed || creating}
        />
      </div>
    </div>
  );
}
