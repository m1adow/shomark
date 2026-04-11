import { createBrowserRouter } from 'react-router-dom';
import AppLayout from '../components/layout/AppLayout';
import DashboardPage from '../pages/DashboardPage';
import CampaignsPage from '../pages/CampaignsPage';
import CampaignDetailsPage from '../pages/CampaignDetailsPage';
import CreateCampaignPage from '../pages/create-campaign/CreateCampaignPage';
import AnalyticsPage from '../pages/AnalyticsPage';
import SettingsPage from '../pages/SettingsPage';

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
    path: '/analytics',
    element: (
      <AppLayout>
        <AnalyticsPage />
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
]);
