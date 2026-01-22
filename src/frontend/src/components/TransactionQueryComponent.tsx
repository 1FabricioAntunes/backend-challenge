import { useCallback, useState, useEffect } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { AxiosError } from 'axios';
import { transactionApi, storeApi } from '../services/api';
import '../styles/TransactionQueryComponent.css';

// TypeScript Interfaces

interface Transaction {
  id: number; // API returns long (BIGSERIAL)
  storeCode: string; // StoreId as string
  storeName: string;
  type: string; // Transaction type code (1-9)
  amount: number; // Signed amount in BRL (decimal)
  date: string; // ISO 8601 date string
}

interface Store {
  code: string; // Store code (actually the store's GUID ID as string)
  name: string; // Store name
  balance?: number; // Store balance (optional)
}

interface FilterState {
  storeId: string | null; // Store ID (GUID) for API - found from store name
  storeName: string | null; // Store name input (partial match allowed)
  startDate: string | null;
  endDate: string | null;
}

interface TransactionQueryComponentProps {
  storeId?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
}

// Main Component

const TransactionQueryComponent = ({
  storeId: initialStoreId = '',
  startDate: initialStartDate = '',
  endDate: initialEndDate = '',
  page: initialPage = 1,
}: TransactionQueryComponentProps) => {
  // State Management
  const [filters, setFilters] = useState<FilterState>({
    storeId: initialStoreId || null,
    storeName: null, // Will be set when store is selected
    startDate: initialStartDate || null,
    endDate: initialEndDate || null,
  });

  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pageSize, setPageSize] = useState<number>(50);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [stores, setStores] = useState<Store[]>([]);
  const [loadingStores, setLoadingStores] = useState<boolean>(false);

  // Derived Values
  const totalPages = Math.ceil(totalCount / pageSize);
  // Note: Pagination is handled server-side, so we use transactions directly
  const paginatedTransactions = transactions;

  // Filter Methods - Find store by name (partial match, case-insensitive)
  const findStoreByName = useCallback((name: string): Store | null => {
    if (!name || !name.trim()) return null;
    
    const searchName = name.trim().toLowerCase();
    const matchingStores = stores.filter((s: Store) => 
      s.name.toLowerCase().includes(searchName)
    );
    
    // If exactly one match, return it
    if (matchingStores.length === 1) {
      return matchingStores[0];
    }
    
    // If multiple matches or no matches, return null
    return null;
  }, [stores]);

  const setStoreFilter = useCallback((storeName: string | null) => {
    if (!storeName || !storeName.trim()) {
      setFilters((prev) => ({
        ...prev,
        storeId: null,
        storeName: null,
      }));
      return;
    }
    
    // Find store by name (partial match)
    const foundStore = findStoreByName(storeName);
    
    setFilters((prev) => ({
      ...prev,
      storeId: foundStore?.code || null, // Use code (which is the GUID ID)
      storeName: storeName.trim(),
    }));
  }, [findStoreByName]);

  const setDateRange = useCallback((startDate: string | null, endDate: string | null) => {
    setFilters((prev) => ({
      ...prev,
      startDate: startDate || null,
      endDate: endDate || null,
    }));
  }, []);

  const applyFilters = useCallback(async (resetPage: boolean = true) => {
    // Validate store name - if provided, must match exactly one store
    if (filters.storeName && filters.storeName.trim() && !filters.storeId) {
      setError(`No store found matching "${filters.storeName}". Please check the name or leave empty to show all stores.`);
      return;
    }
    
    // Validate date range if both dates are set
    if (filters.startDate && filters.endDate) {
      // Compare dates as strings (ISO format YYYY-MM-DD allows string comparison)
      // Allow startDate === endDate (same date is valid)
      if (filters.startDate > filters.endDate) {
        setError('Start date must be before or equal to end date');
        return;
      }
    }
    
    // Allow filtering with only start date, only end date, or both
    // No need to require both dates

    // Clear error on valid application
    setError(null);

    // Reset to page 1 when applying new filters
    if (resetPage) {
      setCurrentPage(1);
    }

    try {
      // Set loading state
      setIsLoading(true);

      // Call API using transactionApi for proper response handling
      // Use storeId directly (already validated as GUID from dropdown selection)
      const storeIdGuid = filters.storeId || undefined;
      
      const pageToLoad = resetPage ? 1 : currentPage;
      
      const result = await transactionApi.getTransactions(
        pageToLoad,
        pageSize,
        storeIdGuid,
        filters.startDate || undefined,
        filters.endDate || undefined
      );

      // Parse response - API returns { items, totalCount, page, pageSize }
      // Map API response to component Transaction interface
      const mappedTransactions = (result.items || []).map((item: any) => ({
        id: item.id || item.Id || 0,
        storeCode: item.storeCode || item.StoreCode || '',
        storeName: item.storeName || item.StoreName || '',
        type: item.type || item.Type || '',
        amount: item.amount || item.Amount || 0,
        date: item.date || item.Date || '',
      }));
      setTransactions(mappedTransactions);
      setTotalCount(result.totalCount || 0);

      // Clear error on success
      setError(null);
    } catch (err) {
      // Handle errors
      const axiosError = err as AxiosError<{ error?: { message?: string } }>;
      let errorMessage = 'Error loading transactions.';

      if (!axiosError.response) {
        // Network error
        errorMessage = 'Connection error. Please check your internet connection.';
      } else if (axiosError.response.status === 400) {
        // Bad request (invalid filters)
        errorMessage = 'Invalid filters. Please check the values.';
      } else if (axiosError.response.status === 500) {
        // Server error
        errorMessage = 'Server error. Please try again.';
      }

      setError(errorMessage);
      console.error('Error fetching transactions:', err);
    } finally {
      // Clear loading state
      setIsLoading(false);
    }
  }, [filters, pageSize, currentPage]);

  const clearFilters = useCallback(() => {
    setFilters({
      storeId: null,
      storeName: null,
      startDate: null,
      endDate: null,
    });
    setCurrentPage(1);
    setError(null);
    setTransactions([]);
    setTotalCount(0);
  }, []);

  // Pagination Methods - Load data when page changes
  const goToPage = useCallback((pageNum: number) => {
    if (pageNum >= 1 && pageNum <= totalPages) {
      setCurrentPage(pageNum);
      // applyFilters will be called via useEffect when currentPage changes
    }
  }, [totalPages]);

  const handlePageSizeChange = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    const newPageSize = parseInt(event.target.value, 10);
    setPageSize(newPageSize);
    setCurrentPage(1);
    // Reload data with new page size
    applyFilters();
  }, [applyFilters]);

  // Event Handlers
  const handleStoreChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    const storeName = event.target.value || null;
    setStoreFilter(storeName);
  }, [setStoreFilter]);

  const handleStartDateChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setDateRange(event.target.value || null, filters.endDate);
  }, [filters.endDate, setDateRange]);

  const handleEndDateChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setDateRange(filters.startDate, event.target.value || null);
  }, [filters.startDate, setDateRange]);

  const handleApplyFilters = useCallback(
    (event: FormEvent) => {
      event.preventDefault();
      applyFilters();
    },
    [applyFilters]
  );

  const handleClearFilters = useCallback(
    (event: FormEvent) => {
      event.preventDefault();
      clearFilters();
    },
    [clearFilters]
  );

  // Formatting Helpers
  const formatCurrency = (amount: number): string => {
    // Amount is already in BRL (decimal), not cents
    // Use Brazilian locale (pt-BR) for BRL currency with comma as decimal separator
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(amount);
  };

  const formatDate = (dateString: string): string => {
    try {
      if (!dateString) return '';
      
      // Extract date part from any format (ISO 8601, datetime, etc.)
      // Look for YYYY-MM-DD pattern in the string
      const dateMatch = dateString.match(/(\d{4})-(\d{2})-(\d{2})/);
      if (dateMatch) {
        const [, year, month, day] = dateMatch;
        // Always return dd/mm/yyyy format
        return `${day}/${month}/${year}`;
      }
      
      // If no match found, try parsing as Date object (last resort)
      const date = new Date(dateString);
      if (!isNaN(date.getTime())) {
        // Extract date components - use local date to match what user expects
        // Since we're displaying dates, not timestamps, use local date
        const d = date.getDate().toString().padStart(2, '0');
        const m = (date.getMonth() + 1).toString().padStart(2, '0');
        const y = date.getFullYear();
        return `${d}/${m}/${y}`;
      }
      
      return dateString;
    } catch {
      return dateString;
    }
  };

  const getTransactionTypeName = (type: string): string => {
    const typeNames: Record<string, string> = {
      debit: 'Debit',
      credit: 'Credit',
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

  // Load stores on mount
  useEffect(() => {
    const loadStores = async () => {
      try {
        setLoadingStores(true);
        const storesData = await storeApi.getStores();
        setStores(storesData || []);
      } catch (err) {
        console.error('Failed to load stores:', err);
      } finally {
        setLoadingStores(false);
      }
    };
    loadStores();
  }, []);

  // Load data on initial mount
  useEffect(() => {
    // Load all transactions on initial mount
    applyFilters(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Only run on mount

  // Load data when page changes (but not on initial mount)
  useEffect(() => {
    // Skip initial mount (handled by first useEffect)
    // Only reload if page changed after initial load
    if (currentPage > 1 || (currentPage === 1 && transactions.length > 0)) {
      applyFilters(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentPage]); // Reload when page changes

  // Render
  return (
    <section className="transaction-query">
      <header className="transaction-query__header">
        <h2 className="transaction-query__title">Query Transactions</h2>
        <p className="transaction-query__subtitle">
          Filter and view transactions by store and period
        </p>
      </header>

      {/* Filter Section */}
      <div className="transaction-query__filters">
        <form className="filter-form" onSubmit={handleApplyFilters}>
          <div className="filter-form__grid">
            {/* Store Filter */}
            <div className="filter-form__group">
              <label htmlFor="store-filter" className="filter-form__label">
                Store Name
              </label>
              <input
                id="store-filter"
                type="text"
                className="filter-form__input"
                placeholder="Enter store name (partial match)"
                value={filters.storeName || ''}
                onChange={handleStoreChange}
                disabled={loadingStores}
              />
              {filters.storeName && filters.storeName.trim() && !filters.storeId && (
                <small style={{ fontSize: '0.75rem', color: '#d32f2f', marginTop: '4px', display: 'block' }}>
                  No store found matching "{filters.storeName}". Leave empty to show all stores.
                </small>
              )}
            </div>

            {/* Start Date Filter */}
            <div className="filter-form__group">
              <label htmlFor="start-date-filter" className="filter-form__label">
                Start Date
              </label>
              <input
                id="start-date-filter"
                type="date"
                className="filter-form__input"
                value={filters.startDate || ''}
                onChange={handleStartDateChange}
              />
            </div>

            {/* End Date Filter */}
            <div className="filter-form__group">
              <label htmlFor="end-date-filter" className="filter-form__label">
                End Date
              </label>
              <input
                id="end-date-filter"
                type="date"
                className="filter-form__input"
                value={filters.endDate || ''}
                onChange={handleEndDateChange}
              />
            </div>
          </div>

          {/* Filter Buttons */}
          <div className="filter-form__actions">
            <button
              type="submit"
              className="filter-form__button filter-form__button--primary"
              disabled={isLoading}
            >
              Apply Filters
            </button>
            <button
              type="button"
              className="filter-form__button filter-form__button--secondary"
              onClick={handleClearFilters}
              disabled={isLoading}
            >
              Clear Filters
            </button>
          </div>

          {/* Error Display */}
          {error && (
            <div className="filter-form__error" role="alert">
              {error}
            </div>
          )}
        </form>

        {/* Active Filters Display */}
        {(filters.storeName || filters.startDate || filters.endDate) && (
          <div className="transaction-query__active-filters">
            <p className="active-filters__label">Active Filters:</p>
            <ul className="active-filters__list">
              {filters.storeName && filters.storeName.trim() && (
                <li className="active-filters__item">
                  Store: <strong>{filters.storeName}</strong>
                </li>
              )}
              {filters.startDate && (
                <li className="active-filters__item">
                  From: <strong>{formatDate(filters.startDate)}</strong>
                </li>
              )}
              {filters.endDate && (
                <li className="active-filters__item">
                  To: <strong>{formatDate(filters.endDate)}</strong>
                </li>
              )}
            </ul>
          </div>
        )}
      </div>

      {/* Loading State */}
      {isLoading && (
        <div className="transaction-query__loading" role="status">
          <div className="spinner"></div>
          <p>Loading transactions...</p>
        </div>
      )}

      {/* Error State */}
      {!isLoading && error && (
        <div className="transaction-query__error" role="alert">
          <p>{error}</p>
          <button
            className="transaction-query__retry-button"
            onClick={() => applyFilters(true)}
          >
            Try Again
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !error && paginatedTransactions.length === 0 && (
        <div className="transaction-query__empty" role="status">
          <p>No transactions found. Adjust the filters and try again.</p>
        </div>
      )}

      {/* Transaction Table */}
      {!isLoading && !error && paginatedTransactions.length > 0 && (
        <>
          <div className="transaction-query__table-wrapper">
            <table className="transaction-table">
              <thead className="transaction-table__head">
                <tr>
                  <th className="transaction-table__header">Date</th>
                  <th className="transaction-table__header">Store</th>
                  <th className="transaction-table__header">Value</th>
                  <th className="transaction-table__header">Type</th>
                </tr>
              </thead>
              <tbody className="transaction-table__body">
                {paginatedTransactions.map((transaction, index) => (
                  <tr
                    key={transaction.id}
                    className={`transaction-table__row ${
                      index % 2 === 0 ? 'transaction-table__row--alt' : ''
                    }`}
                  >
                    <td className="transaction-table__cell">
                      {formatDate(transaction.date)}
                    </td>
                    <td className="transaction-table__cell">
                      {transaction.storeName}
                    </td>
                    <td className="transaction-table__cell">
                      {formatCurrency(transaction.amount)}
                    </td>
                    <td className="transaction-table__cell">
                      {getTransactionTypeName(transaction.type)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination Controls */}
          <div className="transaction-query__pagination">
            <div className="pagination__info">
              <p>
                Showing {paginatedTransactions.length} of {totalCount} transactions
              </p>
              <label htmlFor="page-size-select" className="pagination__label">
                Items per page:
              </label>
              <select
                id="page-size-select"
                className="pagination__select"
                value={pageSize}
                onChange={handlePageSizeChange}
              >
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
            </div>

            <div className="pagination__controls">
              <button
                className="pagination__button"
                onClick={() => goToPage(currentPage - 1)}
                disabled={currentPage === 1 || isLoading}
              >
                ← Previous
              </button>

              <span className="pagination__indicator">
                Page {currentPage} of {totalPages || 1}
              </span>

              <button
                className="pagination__button"
                onClick={() => goToPage(currentPage + 1)}
                disabled={currentPage >= totalPages || isLoading}
              >
                Next →
              </button>
            </div>
          </div>
        </>
      )}
    </section>
  );
};

export default TransactionQueryComponent;
