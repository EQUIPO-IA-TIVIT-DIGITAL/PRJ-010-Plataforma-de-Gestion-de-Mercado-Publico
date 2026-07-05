import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider, App, theme as antTheme } from 'antd';
import esES from 'antd/locale/es_ES';
import App_ from './App';
import { registerSessionExpiredHandler } from './lib/apiClient';
import { sessionExpired } from './hooks/useAuth';
import './styles/global.css';

registerSessionExpiredHandler(sessionExpired);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 2, refetchOnWindowFocus: false },
  },
});

const theme = {
  algorithm: antTheme.defaultAlgorithm,
  token: {
    // Brand
    colorPrimary: '#E30613',
    colorError: '#ef4444',
    colorSuccess: '#10b981',
    colorWarning: '#f59e0b',
    colorInfo: '#3b82f6',

    // Backgrounds
    colorBgBase: '#ffffff',
    colorBgContainer: '#ffffff',
    colorBgElevated: '#ffffff',
    colorBgLayout: '#f0f4ff',

    // Text
    colorText: '#0f172a',
    colorTextSecondary: '#64748b',
    colorTextTertiary: '#94a3b8',
    colorTextDisabled: '#cbd5e1',

    // Borders
    colorBorder: '#e2e8f0',
    colorBorderSecondary: '#f1f5f9',

    // Typography
    fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    fontSize: 14,
    fontSizeLG: 16,
    fontSizeSM: 12,
    fontWeightStrong: 600,

    // Shape
    borderRadius: 10,
    borderRadiusSM: 6,
    borderRadiusLG: 14,
    borderRadiusXS: 4,

    // Sizing
    controlHeight: 38,
    controlHeightSM: 30,
    controlHeightLG: 44,

    // Motion
    motionDurationFast: '0.15s',
    motionDurationMid: '0.25s',
    motionDurationSlow: '0.4s',

    // Box shadow
    boxShadow: '0 1px 3px rgba(0,0,0,0.06), 0 8px 24px rgba(99,120,180,0.08)',
    boxShadowSecondary: '0 4px 12px rgba(0,0,0,0.08), 0 16px 40px rgba(99,120,180,0.14)',
  },
  components: {
    Layout: {
      siderBg: '#0f172a',
      triggerBg: '#1e293b',
    },
    Menu: {
      darkItemBg: 'transparent',
      darkItemSelectedBg: 'rgba(227, 6, 19, 0.15)',
      darkItemColor: 'rgba(255,255,255,0.75)',
      darkItemSelectedColor: '#ffffff',
      darkItemHoverBg: 'rgba(255,255,255,0.07)',
      darkItemHoverColor: '#ffffff',
    },
    Button: {
      primaryColor: '#ffffff',
      borderRadius: 10,
      fontWeight: 600,
    },
    Input: {
      borderRadius: 10,
      paddingBlock: 8,
    },
    Select: {
      borderRadius: 10,
    },
    Card: {
      borderRadius: 14,
      paddingLG: 20,
    },
    Table: {
      borderRadius: 14,
      headerBg: '#f1f5f9',
      headerColor: '#64748b',
      headerFontSize: 12,
      rowHoverBg: 'rgba(227, 6, 19, 0.04)',
    },
    Tag: {
      borderRadius: 999,
    },
    Modal: {
      borderRadius: 20,
    },
    Drawer: {
      borderRadius: 0,
    },
    Typography: {
      fontWeightStrong: 700,
    },
  },
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <ConfigProvider locale={esES} theme={theme}>
        <App>
          <App_ />
        </App>
      </ConfigProvider>
    </QueryClientProvider>
  </React.StrictMode>
);
