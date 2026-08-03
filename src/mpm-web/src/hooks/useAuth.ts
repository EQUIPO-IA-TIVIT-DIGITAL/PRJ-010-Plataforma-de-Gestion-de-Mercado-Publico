import { useState, useEffect } from 'react';

interface User {
  userId: string;
  nombre: string;
  email: string;
  roles: string[];
}

const REMEMBER_EMAIL_KEY = 'mpm_remember_email';
export const SESSION_EXPIRED_KEY = 'mpm_session_expired';

/**
 * Handler global de sesión expirada (registrado en apiClient desde main.tsx).
 * La redirección con replace() recarga la página completa, lo que también
 * cierra cualquier conexión SignalR activa y desmonta todo el árbol de React.
 */
export function sessionExpired() {
  localStorage.removeItem('mpm_token');
  localStorage.removeItem('mpm_user');
  sessionStorage.setItem(SESSION_EXPIRED_KEY, '1');
  window.location.replace('/login');
}

export function useAuth() {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [rememberedEmail, setRememberedEmail] = useState<string | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem('mpm_user');
    if (stored) {
      try { setUser(JSON.parse(stored)); } catch { /* ignore */ }
    }
    
    // Cargar email recordado
    const savedEmail = localStorage.getItem(REMEMBER_EMAIL_KEY);
    if (savedEmail) {
      setRememberedEmail(savedEmail);
    }
    
    setLoading(false);
  }, []);

  const login = (token: string, userData: User, remember?: boolean) => {
    localStorage.setItem('mpm_token', token);
    localStorage.setItem('mpm_user', JSON.stringify(userData));
    setUser(userData);
    
    // Guardar email si "Recordarme" está activado
    if (remember && userData.email) {
      localStorage.setItem(REMEMBER_EMAIL_KEY, userData.email);
      setRememberedEmail(userData.email);
    } else {
      // Si no está activado, limpiar cualquier email guardado
      localStorage.removeItem(REMEMBER_EMAIL_KEY);
      setRememberedEmail(null);
    }
  };

  const logout = () => {
    localStorage.removeItem('mpm_token');
    localStorage.removeItem('mpm_user');
    setUser(null);
    window.location.href = '/login';
  };

  const saveRememberedEmail = (email: string | null) => {
    if (email) {
      localStorage.setItem(REMEMBER_EMAIL_KEY, email);
      setRememberedEmail(email);
    } else {
      localStorage.removeItem(REMEMBER_EMAIL_KEY);
      setRememberedEmail(null);
    }
  };

  const updateUserLocal = (nombre: string) => {
    const stored = localStorage.getItem('mpm_user');
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        parsed.nombre = nombre;
        localStorage.setItem('mpm_user', JSON.stringify(parsed));
        setUser(parsed);
      } catch { /* ignore */ }
    }
  };

  return { user, login, logout, loading, rememberedEmail, saveRememberedEmail, updateUserLocal };
}
