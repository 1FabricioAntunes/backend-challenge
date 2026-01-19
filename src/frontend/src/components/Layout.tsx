import React from 'react';
import Sidebar from './Sidebar';
import { useAuth } from '../context/AuthContext';
import '../styles/Layout.css';
import '../styles/responsive.css';

interface LayoutProps {
  children: React.ReactNode;
  pageTitle?: string;
}

/**
 * Main application layout shell with header, sidebar slot, content area, and footer.
 * 
 * Features:
 * - Responsive two-column grid (sidebar + content) with single-column fallback
 * - Header with app branding and user controls
 * - Footer with version/info placeholder
 * - Accessible semantic HTML structure
 * - Integrated authentication state and logout functionality
 */
export const Layout: React.FC<LayoutProps> = ({ children, pageTitle }) => {
  const { user, isAuthenticated, logout } = useAuth();

  /**
   * Handle logout action
   * Clears authentication state and redirects to login
   */
  const handleLogout = () => {
    logout();
    // Optional: Redirect to login page or home
    // window.location.href = '/login';
  };

  /**
   * Get user initials for avatar display
   * Falls back to 'U' if no user or name
   */
  const getUserInitials = (): string => {
    if (!user?.name) return 'U';
    
    const nameParts = user.name.trim().split(' ');
    if (nameParts.length >= 2) {
      return `${nameParts[0][0]}${nameParts[nameParts.length - 1][0]}`.toUpperCase();
    }
    return user.name.substring(0, 2).toUpperCase();
  };

  return (
    <div className="layout">
      {/* Header */}
      <header className="layout-header">
        <div className="header-content">
          <div className="header-left">
            <h1 className="app-title">TransactionProcessor</h1>
            {pageTitle && (
              <>
                <span className="title-separator" aria-hidden="true">|</span>
                <span className="page-title">{pageTitle}</span>
              </>
            )}
          </div>
          
          <div className="header-right">
            {/* User info - populated from AuthContext */}
            <div className="user-info">
              <div className="user-avatar" aria-label="User avatar" title={user?.email || 'User'}>
                <span>{getUserInitials()}</span>
              </div>
              <span className="user-name">
                {isAuthenticated ? user?.name || 'User' : 'Guest'}
              </span>
            </div>
            
            {/* Logout button - wired to auth context */}
            <button 
              className="logout-button"
              onClick={handleLogout}
              aria-label="Logout"
              title="Logout"
              disabled={!isAuthenticated}
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      {/* Main content area - two-column grid */}
      <main className="layout-main">
        {/* Sidebar Navigation */}
        <aside className="layout-sidebar" aria-label="Main navigation">
          <Sidebar />
        </aside>

        {/* Content area */}
        <div className="layout-content">
          {children}
        </div>
      </main>

      {/* Footer */}
      <footer className="layout-footer">
        <div className="footer-content">
          <p className="footer-info">
            TransactionProcessor v1.0.0 | Built with ❤️ for CNAB processing
          </p>
        </div>
      </footer>
    </div>
  );
};

export default Layout;
