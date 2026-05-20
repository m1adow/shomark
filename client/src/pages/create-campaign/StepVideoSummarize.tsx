import { Dropdown } from 'primereact/dropdown';
import { InputTextarea } from 'primereact/inputtextarea';
import { Button } from 'primereact/button';
import { Message } from 'primereact/message';
import { ProgressSpinner } from 'primereact/progressspinner';
import { TargetAudience } from '../../api/types';

const audienceOptions = [
  { label: 'Applicants (Bachelors)', value: TargetAudience.Applicants },
  { label: 'Masters', value: TargetAudience.Masters },
  { label: 'IT Professionals', value: TargetAudience.Professionals },
];

export interface VideoSummarizeData {
  targetAudience: (typeof TargetAudience)[keyof typeof TargetAudience] | null;
  description: string;
}

interface Props {
  data: VideoSummarizeData;
  onChange: (data: VideoSummarizeData) => void;
  /** AI-generated summary text; null means still loading */
  summary: string | null;
  summaryLoading: boolean;
  onNext: () => void;
  processing: boolean;
  processError: string | null;
}

export default function StepVideoSummarize({
  data,
  onChange,
  summary,
  summaryLoading,
  onNext,
  processing,
  processError,
}: Props) {
  const canProceed = data.targetAudience !== null && !processing;

  return (
    <div className="max-w-2xl mx-auto space-y-6">

      {/* Video Summary Card */}
      <div className="bg-white border border-gray-200 rounded-xl shadow-sm p-4">
        <div className="flex items-start justify-between gap-3">
          <h3 className="text-base font-semibold text-gray-900 flex items-center gap-2">
            <i className="pi pi-sparkles text-blue-500" />
            Video Summary
          </h3>
          {summaryLoading && (
            <span className="text-xs text-gray-400 italic flex items-center gap-1.5 mt-0.5">
              <ProgressSpinner style={{ width: '14px', height: '14px' }} strokeWidth="5" />
              Analysing video…
            </span>
          )}
        </div>

        {summaryLoading ? (
          <div className="mt-3 space-y-2">
            <div className="h-3 bg-gray-100 rounded animate-pulse w-full" />
            <div className="h-3 bg-gray-100 rounded animate-pulse w-5/6" />
            <div className="h-3 bg-gray-100 rounded animate-pulse w-4/6" />
          </div>
        ) : summary ? (
          <p className="text-sm text-gray-700 leading-relaxed mt-3">{summary}</p>
        ) : (
          <p className="text-sm text-gray-400 italic">
            Summary not available. You can still continue below.
          </p>
        )}
      </div>

      {/* Target Audience — required */}
      <div className="flex flex-col gap-2">
        <label htmlFor="targetAudience" className="text-sm font-medium text-gray-700">
          Target Audience *
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

      {/* Context / Description — optional */}
      <div className="flex flex-col gap-2">
        <label htmlFor="description" className="text-sm font-medium text-gray-700">
          Context / Description
          <span className="text-gray-400 font-normal ml-1">(optional)</span>
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

      {processError && <Message severity="error" text={processError} className="w-full" />}

      {/* Actions */}
      <div className="flex justify-end gap-3 pt-4">
        <Button
          label="Analyse with AI"
          icon="pi pi-sparkles"
          onClick={onNext}
          disabled={!canProceed}
          loading={processing}
        />
      </div>
    </div>
  );
}
