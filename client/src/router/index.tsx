import { createBrowserRouter } from 'react-router-dom';
import AppLayout from '../components/layout/AppLayout';
import DashboardPage from '../pages/DashboardPage';
import CampaignsPage from '../pages/CampaignsPage';
import SettingsPage from '../pages/SettingsPage';
import OAuthCallbackPage from '../pages/OAuthCallbackPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <AppLayout>
        <DashboardPage />
      </AppLayout>
    ),
  },
  {
    path: '/campaigns',
    element: (
      <AppLayout>
        <CampaignsPage />
      </AppLayout>
    ),
  },
  {
    path: '/campaigns/create',
    element: (
      <AppLayout>
        <CreateCampaignPage />
      </AppLayout>
    ),
  },
  {
    path: '/campaigns/:id',
    element: (
      <AppLayout>
        <CampaignDetailsPage />
      </AppLayout>
    ),
  },
  {
    path: '/campaigns/:id/edit',
    element: (
      <AppLayout>
        <CreateCampaignPage />
      </AppLayout>
    ),
  },
  {
    path: '/settings',
    element: (
      <AppLayout>
        <SettingsPage />
      </AppLayout>
    ),
  },
  {
    path: '/oauth/callback',
    element: (
      <AppLayout>
        <OAuthCallbackPage />
      </AppLayout>
    ),
  },
]);
