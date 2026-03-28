import { useRef, useState, type DragEvent } from 'react';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Dropdown } from 'primereact/dropdown';
import { Button } from 'primereact/button';
import { ProgressBar } from 'primereact/progressbar';
import { Message } from 'primereact/message';
import { TargetAudience } from '../../api/types';

const audienceOptions = [
  { label: 'Applicants (Bachelors)', value: TargetAudience.Applicants },
  { label: 'Masters', value: TargetAudience.Masters },
  { label: 'IT Professionals', value: TargetAudience.Professionals },
];

const MAX_FILE_SIZE = 2 * 1024 * 1024 * 1024; // 2 GB
const ACCEPTED_TYPES = ['video/mp4', 'video/quicktime'];

export interface CampaignSetupData {
  name: string;
  targetAudience: (typeof TargetAudience)[keyof typeof TargetAudience] | null;
  description: string;
  file: File | null;
}

interface Props {
  data: CampaignSetupData;
  onChange: (data: CampaignSetupData) => void;
  onNext: () => void;
  uploading: boolean;
  uploadProgress: number;
  uploadError: string | null;
}

export default function StepCampaignSetup({
  data,
  onChange,
  onNext,
  uploading,
  uploadProgress,
  uploadError,
}: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);

  const canProceed = data.name.trim() !== '' && data.file !== null && !uploading;

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
    const file = e.dataTransfer.files[0];
    if (file) validateAndSetFile(file);
  }

  function handleFileInput(e: React.ChangeEvent<HTMLInputElement>) {
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

      {/* Target Audience */}
      <div className="flex flex-col gap-2">
        <label htmlFor="targetAudience" className="text-sm font-medium text-gray-700">
          Target Audience
        </label>
        <Dropdown
          id="targetAudience"
          value={data.targetAudience}
          options={audienceOptions}
          onChange={(e) => onChange({ ...data, targetAudience: e.value })}
          placeholder="Select audience"
          className="w-full"
        />
      </div>

      {/* Description */}
      <div className="flex flex-col gap-2">
        <label htmlFor="description" className="text-sm font-medium text-gray-700">
          Context / Description
        </label>
        <InputTextarea
          id="description"
          value={data.description}
          onChange={(e) => onChange({ ...data, description: e.target.value })}
          rows={3}
          placeholder="Briefly describe the event or campaign goal…"
          className="w-full"
        />
      </div>

      {/* Drag & Drop Video Upload */}
      <div className="flex flex-col gap-2">
        <label className="text-sm font-medium text-gray-700">Video File *</label>
        <div
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          onClick={() => fileInputRef.current?.click()}
          className={`flex flex-col items-center justify-center border-2 border-dashed rounded-xl p-10 cursor-pointer transition-colors ${
            dragOver
              ? 'border-blue-500 bg-blue-50'
              : 'border-gray-300 bg-gray-50 hover:border-gray-400'
          }`}
        >
          <i className="pi pi-cloud-upload text-4xl text-gray-400 mb-3" />
          {data.file ? (
            <div className="text-center">
              <p className="text-sm font-medium text-gray-900">{data.file.name}</p>
              <p className="text-xs text-gray-500 mt-1">
                {(data.file.size / (1024 * 1024)).toFixed(1)} MB
              </p>
            </div>
          ) : (
            <>
              <p className="text-sm text-gray-600">
                Drag & drop your video here, or <span className="text-blue-600 font-medium">browse</span>
              </p>
              <p className="text-xs text-gray-400 mt-1">MP4 or MOV, up to 2 GB</p>
            </>
          )}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept=".mp4,.mov,video/mp4,video/quicktime"
          onChange={handleFileInput}
          className="hidden"
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
          label="Analyze with AI"
          icon="pi pi-sparkles"
          onClick={onNext}
          disabled={!canProceed}
          loading={uploading}
        />
      </div>
    </div>
  );
}
