import { RouterProvider } from 'react-router-dom';
import { PrimeReactProvider } from 'primereact/api';
import { router } from './router';
import { AuthProvider } from './auth';
import { AuthGate } from './auth/AuthGate';

export default function App() {
  return (
    <PrimeReactProvider>
      <AuthProvider>
        <AuthGate>
          <RouterProvider router={router} />
        </AuthGate>
      </AuthProvider>
    </PrimeReactProvider>
  );
}
