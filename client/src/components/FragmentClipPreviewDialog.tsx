import { Dialog } from 'primereact/dialog';
import { Message } from 'primereact/message';
import { ProgressSpinner } from 'primereact/progressspinner';
import { useFragmentClipUrl } from '../hooks/useFragments';

interface Props {
  fragmentId: string | null;
  visible: boolean;
  onHide: () => void;
}

export default function FragmentClipPreviewDialog({ fragmentId, visible, onHide }: Props) {
  const { data, loading, error } = useFragmentClipUrl(fragmentId ?? '', visible && !!fragmentId);

  return (
    <Dialog
      header="Clip preview"
      visible={visible}
      onHide={onHide}
      modal
      dismissableMask
      style={{ width: 'min(92vw, 420px)' }}
      contentClassName="!pt-0"
    >
      <div className="flex justify-center">
        <div
          className="relative flex w-full max-w-[340px] items-center justify-center overflow-hidden rounded-lg bg-black"
          style={{ aspectRatio: '9 / 16', maxHeight: '72vh' }}
        >
          {loading && (
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-gray-950 text-white">
              <ProgressSpinner style={{ width: '40px', height: '40px' }} strokeWidth="4" />
              <span className="text-sm text-white/70">Loading clip...</span>
            </div>
          )}

          {error && !loading && (
            <div className="absolute inset-0 flex items-center justify-center bg-white p-4">
              <Message severity="error" text={error} className="w-full" />
            </div>
          )}

          {data?.url && !error && (
            <video
              key={data.url}
              src={data.url}
              controls
              autoPlay
              playsInline
              className="h-full w-full bg-black object-contain"
            />
          )}
        </div>
      </div>
    </Dialog>
  );
}
