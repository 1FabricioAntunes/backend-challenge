import { useEffect, useState } from 'react';
import { storeApi } from '../services/api';
import '../styles/StoreListComponent.css';

type StoreDto = {
  code: string;
  name: string;
  balance: number;
};

type StoreListComponentProps = {
  onStoreSelect: (storeCode: string) => void;
};

export default function StoreListComponent({ onStoreSelect }: StoreListComponentProps) {
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadStores = async () => {
    try {
      setLoading(true);
      setError(null);
      const storesData = await storeApi.getStores();
      setStores(storesData);
    } catch (err: any) {
      console.error('Failed to load stores:', err);
      setError(err.response?.data?.message || 'Failed to load stores. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStores();
  }, []);

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  const totalBalance = stores.reduce((sum, store) => sum + store.balance, 0);

  return (
    <div className="store-list">
      <header className="store-list__header">
        <div>
          <h2 className="store-list__title">Stores</h2>
          <p className="store-list__subtitle">
            View all stores and their account balances
          </p>
        </div>
        <button
          className="store-list__refresh-button"
          onClick={loadStores}
          disabled={loading}
          aria-label="Refresh store list"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" />
          </svg>
          Refresh
        </button>
      </header>

      {error && (
        <div className="store-list__error" role="alert">
          <p>{error}</p>
          <button onClick={loadStores}>Try Again</button>
        </div>
      )}

      {loading && stores.length === 0 ? (
        <div className="store-list__loading">Loading stores...</div>
      ) : stores.length === 0 ? (
        <div className="store-list__empty">
          <p>No stores found.</p>
          <p>Upload a CNAB file to create stores.</p>
        </div>
      ) : (
        <>
          <div className="store-list__table-wrapper">
            <table className="store-list__table">
              <thead>
                <tr>
                  <th>Store Name</th>
                  <th>Balance</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {stores.map((store) => (
                  <tr key={store.code}>
                    <td className="store-list__name">{store.name}</td>
                    <td className={`store-list__balance ${
                      store.balance > 0
                        ? 'store-list__balance--positive'
                        : store.balance < 0
                          ? 'store-list__balance--negative'
                          : 'store-list__balance--zero'
                    }`}>
                      <span className="store-list__balance-indicator">
                        {store.balance > 0 ? '✓' : store.balance < 0 ? '✗' : '—'}
                      </span>
                      {formatCurrency(store.balance)}
                    </td>
                    <td className="store-list__actions">
                      <button
                        className="store-list__view-button"
                        onClick={() => onStoreSelect(store.code)}
                        aria-label={`View transactions for ${store.name}`}
                        title="View transactions"
                      >
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                        View Transactions
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="store-list__summary">
            <div className="store-list__total-box">
              <div className="store-list__total-label">Total Balance</div>
              <div className={`store-list__total-amount ${
                totalBalance > 0
                  ? 'store-list__total-amount--positive'
                  : totalBalance < 0
                    ? 'store-list__total-amount--negative'
                    : 'store-list__total-amount--zero'
              }`}>
                {formatCurrency(totalBalance)}
              </div>
              <div className="store-list__total-count">
                {stores.length} store{stores.length !== 1 ? 's' : ''}
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
