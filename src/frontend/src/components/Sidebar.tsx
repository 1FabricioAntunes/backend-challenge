import React, { useState } from 'react';
import { NavLink } from 'react-router-dom';
import '../styles/Sidebar.css';

interface SidebarProps {
  isOpen?: boolean;
  onToggle?: () => void;
}

/**
 * Sidebar navigation component with responsive behavior.
 * 
 * Features:
 * - Navigation links with active state styling
 * - Optional icons for each menu item
 * - Hamburger menu toggle for mobile devices
 * - Accessible keyboard navigation
 * - Responsive design with mobile overlay
 */
export const Sidebar: React.FC<SidebarProps> = ({ isOpen = false, onToggle }) => {
  const [isMobileOpen, setIsMobileOpen] = useState(false);

  const handleMobileToggle = () => {
    setIsMobileOpen(!isMobileOpen);
    onToggle?.();
  };

  const handleLinkClick = () => {
    // Close mobile menu when a link is clicked
    if (isMobileOpen) {
      setIsMobileOpen(false);
      onToggle?.();
    }
  };

  return (
    <>
      {/* Hamburger Button - Mobile Only */}
      <button
        className="hamburger-button"
        onClick={handleMobileToggle}
        aria-label="Toggle navigation menu"
        aria-expanded={isMobileOpen}
      >
        <span className={`hamburger-icon ${isMobileOpen ? 'open' : ''}`}>
          <span></span>
          <span></span>
          <span></span>
        </span>
      </button>

      {/* Sidebar Navigation */}
      <nav 
        className={`sidebar ${isMobileOpen ? 'mobile-open' : ''}`}
        aria-label="Main navigation"
      >
        <ul className="sidebar-nav">
          <li>
            <NavLink
              to="/upload"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
              onClick={handleLinkClick}
            >
              <span className="sidebar-icon" aria-hidden="true">⬆</span>
              <span className="sidebar-text">Upload</span>
            </NavLink>
          </li>

          <li>
            <NavLink
              to="/transactions"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
              onClick={handleLinkClick}
            >
              <span className="sidebar-icon" aria-hidden="true">📊</span>
              <span className="sidebar-text">Transactions</span>
            </NavLink>
          </li>

          <li>
            <NavLink
              to="/stores"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
              onClick={handleLinkClick}
            >
              <span className="sidebar-icon" aria-hidden="true">🏪</span>
              <span className="sidebar-text">Stores</span>
            </NavLink>
          </li>

          <li>
            <NavLink
              to="/files"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
              onClick={handleLinkClick}
            >
              <span className="sidebar-icon" aria-hidden="true">📁</span>
              <span className="sidebar-text">Files</span>
            </NavLink>
          </li>
        </ul>

        {/* Sidebar Footer - Optional Info */}
        <div className="sidebar-footer">
          <p className="sidebar-footer-text">CNAB Processor</p>
        </div>
      </nav>

      {/* Mobile Overlay - Click to close */}
      {isMobileOpen && (
        <div 
          className="sidebar-overlay"
          onClick={handleMobileToggle}
          aria-hidden="true"
        />
      )}
    </>
  );
};

export default Sidebar;
