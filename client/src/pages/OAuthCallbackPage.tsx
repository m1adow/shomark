import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function OAuthCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const statusParam = searchParams.get('status');
    const platform = searchParams.get('platform');
    const messageParam = searchParams.get('message');
    const providerError = searchParams.get('error');
    const providerErrorDescription = searchParams.get('error_description');

    if (statusParam === 'success') {
      setStatus('success');
      setMessage(platform ? `${platform} connected successfully!` : 'Account connected successfully!');
    } else if (statusParam === 'error' || providerError) {
      setStatus('error');
      setMessage(messageParam ?? providerErrorDescription ?? providerError ?? 'Connection failed');
    } else {
      // No structured status (e.g. opened directly) — fall back to a neutral error.
      setStatus('error');
      setMessage('Missing OAuth response');
    }

    // Redirect to settings after a brief delay
    const timer = setTimeout(() => navigate('/settings'), 2000);
    return () => clearTimeout(timer);
  }, [searchParams, navigate]);

  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="text-center">
        {status === 'loading' && (
          <>
            <div className="animate-spin w-8 h-8 border-4 border-indigo-600 border-t-transparent rounded-full mx-auto mb-4" />
            <p className="text-gray-600">Completing connection...</p>
          </>
        )}
        {status === 'success' && (
          <>
            <div className="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <span className="text-green-600 text-2xl">✓</span>
            </div>
            <p className="text-gray-900 font-medium">{message}</p>
            <p className="text-sm text-gray-500 mt-1">Redirecting to settings...</p>
          </>
        )}
        {status === 'error' && (
          <>
            <div className="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <span className="text-red-600 text-2xl">✕</span>
            </div>
            <p className="text-gray-900 font-medium">Connection failed</p>
            <p className="text-sm text-red-500 mt-1">{message}</p>
            <p className="text-sm text-gray-500 mt-2">Redirecting to settings...</p>
          </>
        )}
      </div>
    </div>
  );
}
