import { useState } from 'react';
import FileUploadComponent from './components/FileUploadComponent';
import TransactionQueryComponent from './components/TransactionQueryComponent';
import './styles/App.css';

type Tab = 'upload' | 'transactions';

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>('upload');

  return (
    <div className="app">
      <header className="app-header">
        <h1 className="app-title">Transaction Processor</h1>
        <p className="app-subtitle">Gerenciamento de Transações CNAB</p>
      </header>

      <nav className="app-tabs">
        <button
          className={`tab-button ${activeTab === 'upload' ? 'active' : ''}`}
          onClick={() => setActiveTab('upload')}
          aria-current={activeTab === 'upload' ? 'page' : undefined}
        >
          Enviar Arquivo
        </button>
        <button
          className={`tab-button ${activeTab === 'transactions' ? 'active' : ''}`}
          onClick={() => setActiveTab('transactions')}
          aria-current={activeTab === 'transactions' ? 'page' : undefined}
        >
          Consultar Transações
        </button>
      </nav>

      <main className="app-content">
        {activeTab === 'upload' && <FileUploadComponent />}
        {activeTab === 'transactions' && <TransactionQueryComponent />}
      </main>

      <footer className="app-footer">
        <p>&copy; 2026 Transaction Processor. All rights reserved.</p>
      </footer>
    </div>
  );
}
