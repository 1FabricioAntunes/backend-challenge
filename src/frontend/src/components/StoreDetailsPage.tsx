import { useEffect, useState, useCallback } from 'react';
import { storeApi, transactionApi } from '../services/api';
import '../styles/StoreDetailsPage.css';

type StoreDto = {
  code: string;
  name: string;
  balance: number;
};

type TransactionDto = {
  id: number;
  storeCode: string;
  storeName: string;
  type: string;
  amount: number;
  date: string;
};

type StoreDetailsPageProps = {
  storeCode: string;
  onBack: () => void;
};

export default function StoreDetailsPage({ storeCode, onBack }: StoreDetailsPageProps) {
  const [store, setStore] = useState<StoreDto | null>(null);
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [transactionsLoading, setTransactionsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');
  const pageSize = 50;

  const loadStore = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      // Get all stores and find the one matching the code
      const storesData = await storeApi.getStores();
      const foundStore = storesData.find((s: StoreDto) => s.code === storeCode);
      
      if (!foundStore) {
        setError('Store not found');
        return;
      }
      
      setStore(foundStore);
    } catch (err: any) {
      console.error('Failed to load store details:', err);
      setError(err.response?.data?.message || 'Failed to load store details. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [storeCode]);

  const loadTransactions = useCallback(async () => {
    if (!storeCode) return;
    
    try {
      setTransactionsLoading(true);
      setError(null);
      
      const params: Record<string, string | number> = {
        page,
        pageSize,
        storeId: storeCode, // Use storeCode as storeId
      };

      if (startDate) {
        params.startDate = startDate;
      }
      if (endDate) {
        params.endDate = endDate;
      }

      const response = await transactionApi.getTransactions(
        page,
        pageSize,
        storeCode, // Use storeCode as storeId
        startDate || undefined,
        endDate || undefined
      );
      
      setTransactions(response.items || []);
      setTotal(response.totalCount || 0);
    } catch (err: any) {
      console.error('Failed to load transactions:', err);
      setError(err.response?.data?.message || 'Failed to load transactions. Please try again.');
    } finally {
      setTransactionsLoading(false);
    }
  }, [storeCode, page, pageSize, startDate, endDate]);

  useEffect(() => {
    loadStore();
  }, [loadStore]);

  useEffect(() => {
    if (store) {
      loadTransactions();
    }
  }, [store, loadTransactions]);

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('en-US', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  };

  const getTransactionTypeName = (type: string): string => {
    const typeNames: Record<string, string> = {
      '1': 'Debit',
      '2': 'Boleto',
      '3': 'Financing',
      '4': 'Credit',
      '5': 'Loan Receipt',
      '6': 'Sales',
      '7': 'TED Receipt',
      '8': 'DOC Receipt',
      '9': 'Rent',
    };
    return typeNames[type] || type;
  };

  const handleApplyFilters = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    loadTransactions();
  };

  const handleClearFilters = () => {
    setStartDate('');
    setEndDate('');
    setPage(1);
  };

  const totalPages = Math.ceil(total / pageSize);

  if (loading) {
    return (
      <div className="store-details">
        <div className="store-details__loading">Loading store details...</div>
      </div>
    );
  }

  if (error && !store) {
    return (
      <div className="store-details">
        <div className="store-details__error" role="alert">
          <p>{error}</p>
          <button onClick={onBack}>Back to Stores</button>
        </div>
      </div>
    );
  }

  if (!store) {
    return (
      <div className="store-details">
        <div className="store-details__error" role="alert">
          <p>Store not found</p>
          <button onClick={onBack}>Back to Stores</button>
        </div>
      </div>
    );
  }

  return (
    <div className="store-details">
      <header className="store-details__header">
        <button className="store-details__back-button" onClick={onBack} aria-label="Back to store list">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
          Back to Stores
        </button>
        <div className="store-details__title-group">
          <h2 className="store-details__title">{store.name}</h2>
          <p className="store-details__subtitle">Store Code: {store.code}</p>
        </div>
        <div className={`store-details__balance ${
          store.balance > 0
            ? 'store-details__balance--positive'
            : store.balance < 0
              ? 'store-details__balance--negative'
              : 'store-details__balance--zero'
        }`}>
          <div className="store-details__balance-label">Balance</div>
          <div className="store-details__balance-amount">
            {formatCurrency(store.balance)}
          </div>
        </div>
      </header>

      {/* Filters Section */}
      <div className="store-details__filters">
        <form onSubmit={handleApplyFilters} className="store-details__filter-form">
          <div className="store-details__filter-group">
            <label htmlFor="startDate">Start Date</label>
            <input
              type="date"
              id="startDate"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
            />
          </div>
          <div className="store-details__filter-group">
            <label htmlFor="endDate">End Date</label>
            <input
              type="date"
              id="endDate"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
            />
          </div>
          <div className="store-details__filter-actions">
            <button type="submit" className="store-details__filter-button store-details__filter-button--apply">
              Apply Filters
            </button>
            <button
              type="button"
              onClick={handleClearFilters}
              className="store-details__filter-button store-details__filter-button--clear"
            >
              Clear
            </button>
          </div>
        </form>
      </div>

      {/* Transactions Section */}
      <div className="store-details__transactions">
        <h3 className="store-details__transactions-title">Transactions</h3>
        
        {transactionsLoading ? (
          <div className="store-details__loading">Loading transactions...</div>
        ) : transactions.length === 0 ? (
          <div className="store-details__empty">
            <p>No transactions found for this store.</p>
            {startDate || endDate ? (
              <p>Try adjusting your date filters.</p>
            ) : null}
          </div>
        ) : (
          <>
            <div className="store-details__table-wrapper">
              <table className="store-details__table">
                <thead>
                  <tr>
                    <th>Date/Time</th>
                    <th>Type</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {transactions.map((transaction) => (
                    <tr key={transaction.id}>
                      <td className="store-details__date">{formatDateTime(transaction.date)}</td>
                      <td className="store-details__type">{getTransactionTypeName(transaction.type)}</td>
                      <td className={`store-details__amount ${
                        transaction.amount > 0
                          ? 'store-details__amount--positive'
                          : 'store-details__amount--negative'
                      }`}>
                        {formatCurrency(transaction.amount)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="store-details__pagination">
                <button
                  className="store-details__page-button"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1 || transactionsLoading}
                >
                  Previous
                </button>
                <span className="store-details__page-info">
                  Page {page} of {totalPages} ({total} transactions)
                </span>
                <button
                  className="store-details__page-button"
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages || transactionsLoading}
                >
                  Next
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
