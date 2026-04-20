import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function OAuthCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const error = searchParams.get('error');

    if (error) {
      setStatus('error');
      setMessage(searchParams.get('error_description') ?? error);
    } else {
      // The backend handles the token exchange and redirects here on success
      setStatus('success');
      setMessage('Account connected successfully!');
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
