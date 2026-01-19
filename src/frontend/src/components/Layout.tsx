import React from 'react';
import '../styles/Layout.css';

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
 */
export const Layout: React.FC<LayoutProps> = ({ children, pageTitle }) => {
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
            {/* User info placeholder - will be populated by AuthContext */}
            <div className="user-info">
              <div className="user-avatar" aria-label="User avatar">
                <span>U</span>
              </div>
              <span className="user-name">User</span>
            </div>
            
            {/* Logout button slot - will be wired in auth integration */}
            <button 
              className="logout-button"
              aria-label="Logout"
              title="Logout"
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      {/* Main content area - two-column grid */}
      <main className="layout-main">
        {/* Sidebar slot - will contain navigation */}
        <aside className="layout-sidebar" aria-label="Main navigation">
          {/* Sidebar content will be added in next step */}
          <nav className="sidebar-placeholder">
            <p>Navigation</p>
          </nav>
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
