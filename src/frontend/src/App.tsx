import { useState } from 'react';
import { useAuth } from './context/AuthContext';
import LoginComponent from './components/LoginComponent';
import FileUploadComponent from './components/FileUploadComponent';
import TransactionQueryComponent from './components/TransactionQueryComponent';
import FileListComponent from './components/FileListComponent';
import FileDetailsPage from './components/FileDetailsPage';
import StoreListComponent from './components/StoreListComponent';
import StoreDetailsPage from './components/StoreDetailsPage';
import './styles/App.css';

type Tab = 'upload' | 'transactions' | 'files' | 'stores';
type View = 'list' | 'details';

/**
 * Main Application Component
 * 
 * Features:
 * - Shows login screen when user is not authenticated
 * - Shows main application with tabs when authenticated
 * - Integrates with AuthContext for authentication state
 * - User info and logout button in header
 */
export default function App() {
  const { isAuthenticated, user, logout } = useAuth();
  const [activeTab, setActiveTab] = useState<Tab>('upload');
  const [filesView, setFilesView] = useState<View>('list');
  const [storesView, setStoresView] = useState<View>('list');
  const [selectedFileId, setSelectedFileId] = useState<string | null>(null);
  const [selectedStoreCode, setSelectedStoreCode] = useState<string | null>(null);

  /**
   * Get user initials for avatar display
   */
  const getUserInitials = (): string => {
    if (!user?.name) return 'U';
    const nameParts = user.name.trim().split(' ');
    if (nameParts.length >= 2) {
      return `${nameParts[0][0]}${nameParts[nameParts.length - 1][0]}`.toUpperCase();
    }
    return user.name.substring(0, 2).toUpperCase();
  };

  /**
   * Handle logout action
   */
  const handleLogout = () => {
    logout();
  };

  // Show login screen if not authenticated
  if (!isAuthenticated) {
    return <LoginComponent />;
  }

  // Show main application when authenticated
  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header-content">
          <div className="app-header-left">
            <h1 className="app-title">Transaction Processor</h1>
            <p className="app-subtitle">CNAB Transaction Management</p>
          </div>
          <div className="app-header-right">
            <div className="user-info">
              <div className="user-avatar" aria-label="User avatar" title={user?.email || 'User'}>
                <span>{getUserInitials()}</span>
              </div>
              <div className="user-details">
                <span className="user-name">{user?.name || 'User'}</span>
                <span className="user-email">{user?.email || ''}</span>
              </div>
            </div>
            <button
              className="logout-button"
              onClick={handleLogout}
              aria-label="Logout"
              title="Logout"
            >
              <svg
                className="logout-icon"
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                <polyline points="16 17 21 12 16 7" />
                <line x1="21" y1="12" x2="9" y2="12" />
              </svg>
              <span>Logout</span>
            </button>
          </div>
        </div>
      </header>

      <nav className="app-tabs">
        <button
          className={`tab-button ${activeTab === 'upload' ? 'active' : ''}`}
          onClick={() => {
            setActiveTab('upload');
            setFilesView('list');
            setStoresView('list');
            setSelectedFileId(null);
            setSelectedStoreCode(null);
          }}
          aria-current={activeTab === 'upload' ? 'page' : undefined}
        >
          Upload File
        </button>
        <button
          className={`tab-button ${activeTab === 'files' ? 'active' : ''}`}
          onClick={() => {
            setActiveTab('files');
            setFilesView('list');
            setStoresView('list');
            setSelectedFileId(null);
            setSelectedStoreCode(null);
          }}
          aria-current={activeTab === 'files' ? 'page' : undefined}
        >
          Files
        </button>
        <button
          className={`tab-button ${activeTab === 'stores' ? 'active' : ''}`}
          onClick={() => {
            setActiveTab('stores');
            setStoresView('list');
            setFilesView('list');
            setSelectedStoreCode(null);
            setSelectedFileId(null);
          }}
          aria-current={activeTab === 'stores' ? 'page' : undefined}
        >
          Stores
        </button>
        <button
          className={`tab-button ${activeTab === 'transactions' ? 'active' : ''}`}
          onClick={() => {
            setActiveTab('transactions');
            setFilesView('list');
            setStoresView('list');
            setSelectedFileId(null);
            setSelectedStoreCode(null);
          }}
          aria-current={activeTab === 'transactions' ? 'page' : undefined}
        >
          Query Transactions
        </button>
      </nav>

      <main className="app-content">
        {activeTab === 'upload' && (
          <FileUploadComponent
            onFileUploaded={(fileId) => {
              setActiveTab('files');
              setSelectedFileId(fileId);
              setFilesView('details');
            }}
          />
        )}
        {activeTab === 'files' && (
          filesView === 'list' ? (
            <FileListComponent
              onFileSelect={(fileId) => {
                setSelectedFileId(fileId);
                setFilesView('details');
              }}
            />
          ) : selectedFileId ? (
            <FileDetailsPage
              fileId={selectedFileId}
              onBack={() => {
                setFilesView('list');
                setSelectedFileId(null);
              }}
            />
          ) : null
        )}
        {activeTab === 'transactions' && <TransactionQueryComponent />}
        {activeTab === 'stores' && (
          storesView === 'list' ? (
            <StoreListComponent
              onStoreSelect={(storeCode) => {
                setSelectedStoreCode(storeCode);
                setStoresView('details');
              }}
            />
          ) : selectedStoreCode ? (
            <StoreDetailsPage
              storeCode={selectedStoreCode}
              onBack={() => {
                setStoresView('list');
                setSelectedStoreCode(null);
              }}
            />
          ) : null
        )}
      </main>

      <footer className="app-footer">
        <p>&copy; 2026 Transaction Processor. All rights reserved.</p>
      </footer>
    </div>
  );
}
