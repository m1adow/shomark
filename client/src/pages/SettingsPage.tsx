import { useMyPlatforms, useConnectPlatform, useDisconnectPlatform } from '../hooks/usePlatforms';
import type { OAuthPlatform, PlatformDto } from '../api/types';

const SUPPORTED_PLATFORMS: { name: OAuthPlatform; label: string; color: string; icon: string }[] = [
  { name: 'Instagram', label: 'Instagram', color: 'bg-gradient-to-r from-purple-500 to-pink-500', icon: '📸' },
  { name: 'TikTok', label: 'TikTok', color: 'bg-black', icon: '🎵' },
  { name: 'YouTube', label: 'YouTube Shorts', color: 'bg-red-600', icon: '▶️' },
  { name: 'X', label: 'X (Twitter)', color: 'bg-gray-900', icon: '𝕏' },
];

function PlatformCard({
  platform,
  connected,
  onConnect,
  onDisconnect,
  connecting,
  disconnecting,
}: {
  platform: typeof SUPPORTED_PLATFORMS[number];
  connected: PlatformDto | undefined;
  onConnect: () => void;
  onDisconnect: () => void;
  connecting: boolean;
  disconnecting: boolean;
}) {
  const isExpired = connected?.tokenExpiresAt
    ? new Date(connected.tokenExpiresAt) < new Date()
    : false;

  return (
    <div className="border border-gray-200 rounded-lg p-5 flex items-center justify-between">
      <div className="flex items-center gap-4">
        <div className={`w-12 h-12 ${platform.color} rounded-lg flex items-center justify-center text-white text-xl`}>
          {platform.icon}
        </div>
        <div>
          <h3 className="font-medium text-gray-900">{platform.label}</h3>
          {connected ? (
            <div className="text-sm text-gray-500">
              <span className="text-green-600 font-medium">Connected</span>
              {connected.accountName && <span> · {connected.accountName}</span>}
              {isExpired && <span className="text-red-500 ml-2">(Token expired)</span>}
              {connected.tokenExpiresAt && !isExpired && (
                <span className="ml-2">
                  · Expires {new Date(connected.tokenExpiresAt).toLocaleDateString()}
                </span>
              )}
            </div>
          ) : (
            <p className="text-sm text-gray-400">Not connected</p>
          )}
        </div>
      </div>
      <div>
        {connected ? (
          <button
            onClick={onDisconnect}
            disabled={disconnecting}
            className="px-4 py-2 text-sm font-medium text-red-600 border border-red-200 rounded-lg hover:bg-red-50 disabled:opacity-50"
          >
            {disconnecting ? 'Disconnecting...' : 'Disconnect'}
          </button>
        ) : (
          <button
            onClick={onConnect}
            disabled={connecting}
            className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50"
          >
            {connecting ? 'Connecting...' : 'Connect'}
          </button>
        )}
      </div>
    </div>
  );
}

export default function SettingsPage() {
  const { data: platforms, refetch } = useMyPlatforms();
  const { execute: connect, loading: connecting } = useConnectPlatform();
  const { execute: disconnect, loading: disconnecting } = useDisconnectPlatform();

  const handleConnect = async (platformName: OAuthPlatform) => {
    const result = await connect(platformName);
    if (result?.authorizationUrl) {
      // Open OAuth flow in the same window
      window.location.href = result.authorizationUrl;
    }
  };

  const handleDisconnect = async (platformName: OAuthPlatform) => {
    await disconnect(platformName);
    refetch();
  };

  const getConnectedPlatform = (name: string) =>
    platforms?.find((p) => p.platformType === name);

  return (
    <div>
      <h1 className="text-2xl font-semibold text-gray-900 mb-6">Settings</h1>

      <section className="mb-8">
        <h2 className="text-lg font-medium text-gray-900 mb-4">Connected Accounts</h2>
        <p className="text-sm text-gray-500 mb-4">
          Connect your social media accounts to publish content directly from ShoMark.
        </p>
        <div className="space-y-3">
          {SUPPORTED_PLATFORMS.map((platform) => (
            <PlatformCard
              key={platform.name}
              platform={platform}
              connected={getConnectedPlatform(platform.name)}
              onConnect={() => handleConnect(platform.name)}
              onDisconnect={() => handleDisconnect(platform.name)}
              connecting={connecting}
              disconnecting={disconnecting}
            />
          ))}
        </div>
      </section>
    </div>
  );
}
