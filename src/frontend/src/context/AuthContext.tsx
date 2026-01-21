import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

/**
 * User information interface
 */
interface User {
  name: string;
  email: string;
}

/**
 * Authentication context state interface
 */
interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (user: User, token: string) => void;
  logout: () => void;
}

/**
 * AuthProvider props interface
 */
interface AuthProviderProps {
  children: ReactNode;
}

// Local storage keys
const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';

/**
 * SECURITY NOTE: Token Storage in localStorage
 * 
 * ⚠️ XSS Vulnerability: Tokens stored in localStorage are accessible to JavaScript,
 * making them vulnerable to Cross-Site Scripting (XSS) attacks.
 * 
 * Current Implementation (Development/Demo):
 * - Tokens stored in localStorage for simplicity
 * - Suitable for development and demo purposes
 * 
 * Production Recommendation (per docs/security.md):
 * - Use HttpOnly cookies for token storage (prevents JavaScript access)
 * - Tokens sent automatically with requests via cookie header
 * - Backend must set cookies with HttpOnly, Secure, and SameSite flags
 * - Consider secure memory storage for sensitive applications
 * 
 * See: docs/security.md - "Insecure Token Storage" section
 * See: technical-decisions.md - "JWT Security Considerations" section
 */

/**
 * Authentication Context
 * 
 * Provides authentication state and methods throughout the application.
 */
const AuthContext = createContext<AuthContextType | undefined>(undefined);

/**
 * AuthProvider Component
 * 
 * Wraps the application to provide authentication context.
 * 
 * Features:
 * - Persists token and user in localStorage
 * - Automatically restores auth state on mount
 * - Provides login/logout methods
 * - Exposes authentication status
 */
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);

  /**
   * Restore authentication state from localStorage on mount
   */
  useEffect(() => {
    const storedToken = localStorage.getItem(TOKEN_KEY);
    const storedUser = localStorage.getItem(USER_KEY);

    if (storedToken && storedUser) {
      try {
        const parsedUser = JSON.parse(storedUser) as User;
        setToken(storedToken);
        setUser(parsedUser);
      } catch (error) {
        console.error('Failed to parse stored user data:', error);
        // Clear corrupted data
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
      }
    }
  }, []);

  /**
   * Login method
   * 
   * Stores user and token in state and localStorage
   * 
   * @param userData - User information (name, email)
   * @param authToken - JWT authentication token
   */
  const login = (userData: User, authToken: string) => {
    setUser(userData);
    setToken(authToken);
    localStorage.setItem(TOKEN_KEY, authToken);
    localStorage.setItem(USER_KEY, JSON.stringify(userData));
  };

  /**
   * Logout method
   * 
   * Clears user and token from state and localStorage
   */
  const logout = () => {
    setUser(null);
    setToken(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  };

  const value: AuthContextType = {
    user,
    token,
    isAuthenticated: !!token && !!user,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

/**
 * useAuth Hook
 * 
 * Custom hook to access authentication context.
 * 
 * @returns AuthContextType - Authentication state and methods
 * @throws Error if used outside of AuthProvider
 * 
 * @example
 * ```tsx
 * const { user, token, isAuthenticated, login, logout } = useAuth();
 * 
 * // Check authentication status
 * if (!isAuthenticated) {
 *   return <Login />;
 * }
 * 
 * // Display user info
 * <p>Welcome, {user.name}</p>
 * 
 * // Login
 * login({ name: 'John', email: 'john@example.com' }, 'jwt-token');
 * 
 * // Logout
 * logout();
 * ```
 */
export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  
  return context;
};

export default AuthContext;
