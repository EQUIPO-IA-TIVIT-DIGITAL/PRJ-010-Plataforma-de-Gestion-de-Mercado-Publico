import { BrowserRouter, Routes, Route, Navigate, Outlet } from 'react-router-dom';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { LicitacionesPage } from './pages/LicitacionesPage';
import { MensajeriaPage } from './pages/MensajeriaPage';
import { CatalogoPage } from './pages/CatalogoPage';
import { AnalisisListPage } from './pages/AnalisisListPage';
import { AnalisisWorkspacePage } from './pages/AnalisisWorkspacePage';
import { AnalisisDashboardPage } from './pages/AnalisisDashboardPage';
import { AnalisisChatPage } from './pages/AnalisisChatPage';
import EjecutivoDashboardPage from './pages/EjecutivoDashboardPage';
import NotificacionesPage from './pages/NotificacionesPage';
import { AlertasPage } from './pages/AlertasPage';
// Oculto temporalmente para el deploy 2026-07-10 -- ver AppLayout.tsx para el detalle.
// import { CompetidoresPage } from './pages/CompetidoresPage';
import { useAuth } from './hooks/useAuth';

function ProtectedRoute() {
  const { user, loading } = useAuth();
  if (loading) return <div style={{ padding: 40, textAlign: 'center' }}>Cargando...</div>;
  if (!user) return <Navigate to="/login" replace />;
  return (
    <AppLayout>
      <Outlet />
    </AppLayout>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password/:token" element={<ResetPasswordPage />} />
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<Navigate to="/licitaciones" replace />} />
          <Route path="/licitaciones" element={<LicitacionesPage />} />
          <Route path="/catalogos" element={<CatalogoPage />} />
          <Route path="/mensajes" element={<MensajeriaPage />} />
          <Route path="/analisis" element={<AnalisisListPage />} />
          <Route path="/analisis/ejecutivo" element={<EjecutivoDashboardPage />} />
          <Route path="/analisis/:id" element={<AnalisisWorkspacePage />} />
          <Route path="/analisis/:id/dashboard" element={<AnalisisDashboardPage />} />
          <Route path="/analisis/:id/chat" element={<AnalisisChatPage />} />
          <Route path="/notificaciones" element={<NotificacionesPage />} />
          <Route path="/alertas" element={<AlertasPage />} />
          {/* Oculto temporalmente para el deploy 2026-07-10 -- ver AppLayout.tsx. */}
          <Route path="/competidores" element={<Navigate to="/licitaciones" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}