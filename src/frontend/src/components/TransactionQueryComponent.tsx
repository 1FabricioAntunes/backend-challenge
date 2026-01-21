import { useCallback, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import axios, { AxiosError } from 'axios';
import '../styles/TransactionQueryComponent.css';

// TypeScript Interfaces

interface Transaction {
  id: string;
  storeId: string;
  storeCode: string;
  storeName: string;
  type: 'debit' | 'credit';
  amount: number;
  date: string;
}

interface FilterState {
  storeId: string | null;
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
    startDate: initialStartDate || null,
    endDate: initialEndDate || null,
  });

  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pageSize, setPageSize] = useState<number>(50);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);

  // Derived Values
  const totalPages = Math.ceil(totalCount / pageSize);
  const paginatedTransactions = transactions.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  // Filter Methods
  const setStoreFilter = useCallback((storeId: string | null) => {
    setFilters((prev) => ({
      ...prev,
      storeId: storeId || null,
    }));
  }, []);

  const setDateRange = useCallback((startDate: string | null, endDate: string | null) => {
    setFilters((prev) => ({
      ...prev,
      startDate: startDate || null,
      endDate: endDate || null,
    }));
  }, []);

  const applyFilters = useCallback(async () => {
    // Validate date range if both dates are set
    if (filters.startDate && filters.endDate) {
      if (filters.startDate > filters.endDate) {
        setError('Start date must be before or equal to end date');
        return;
      }
    }

    // Clear error on valid application
    setError(null);

    // Reset to page 1 when applying new filters
    setCurrentPage(1);

    try {
      // Set loading state
      setIsLoading(true);

      // Build query parameters
      const params: Record<string, string | number> = {
        page: 1,
        pageSize: pageSize,
      };

      if (filters.storeId) {
        params.storeId = filters.storeId;
      }
      if (filters.startDate) {
        params.startDate = filters.startDate;
      }
      if (filters.endDate) {
        params.endDate = filters.endDate;
      }

      // Call API
      const response = await axios.get('/api/transactions/v1', { params });

      // Parse response
      const data = response.data;
      setTransactions(data.transactions || []);
      setTotalCount(data.totalCount || 0);

      // Clear error on success
      setError(null);
    } catch (err) {
      // Handle errors
      const axiosError = err as AxiosError<{ error?: { message?: string } }>;
      let errorMessage = 'Erro ao carregar transações.';

      if (!axiosError.response) {
        // Network error
        errorMessage = 'Erro de conexão. Verifique sua internet.';
      } else if (axiosError.response.status === 400) {
        // Bad request (invalid filters)
        errorMessage = 'Filtros inválidos. Verifique os valores.';
      } else if (axiosError.response.status === 500) {
        // Server error
        errorMessage = 'Erro no servidor. Tente novamente.';
      }

      setError(errorMessage);
      console.error('Error fetching transactions:', err);
    } finally {
      // Clear loading state
      setIsLoading(false);
    }
  }, [filters, pageSize]);

  const clearFilters = useCallback(() => {
    setFilters({
      storeId: null,
      startDate: null,
      endDate: null,
    });
    setCurrentPage(1);
    setError(null);
    setTransactions([]);
    setTotalCount(0);
  }, []);

  // Pagination Methods
  const goToPage = useCallback((pageNum: number) => {
    if (pageNum >= 1 && pageNum <= totalPages) {
      setCurrentPage(pageNum);
    }
  }, [totalPages]);

  const handlePageSizeChange = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    const newPageSize = parseInt(event.target.value, 10);
    setPageSize(newPageSize);
    setCurrentPage(1);
  }, []);

  // Event Handlers
  const handleStoreChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setStoreFilter(event.target.value || null);
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
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(amount / 100);
  };

  const formatDate = (dateString: string): string => {
    try {
      const date = new Date(dateString);
      return date.toLocaleDateString('pt-BR');
    } catch {
      return dateString;
    }
  };

  const getTransactionTypeName = (type: string): string => {
    const typeNames: Record<string, string> = {
      debit: 'Débito',
      credit: 'Crédito',
      '1': 'Débito',
      '4': 'Crédito',
      '5': 'Recebimento de Empréstimo',
      '6': 'Vendas',
      '7': 'Recebimento TED',
      '8': 'Recebimento DOC',
      '2': 'Boleto',
      '3': 'Financiamento',
      '9': 'Aluguel',
    };
    return typeNames[type] || type;
  };

  // Render
  return (
    <section className="transaction-query">
      <header className="transaction-query__header">
        <h2 className="transaction-query__title">Consultar Transações</h2>
        <p className="transaction-query__subtitle">
          Filtrar e visualizar transações por loja e período
        </p>
      </header>

      {/* Filter Section */}
      <div className="transaction-query__filters">
        <form className="filter-form" onSubmit={handleApplyFilters}>
          <div className="filter-form__grid">
            {/* Store Filter */}
            <div className="filter-form__group">
              <label htmlFor="store-filter" className="filter-form__label">
                Loja
              </label>
              <input
                id="store-filter"
                type="text"
                className="filter-form__input"
                placeholder="Nome da loja"
                value={filters.storeId || ''}
                onChange={handleStoreChange}
              />
            </div>

            {/* Start Date Filter */}
            <div className="filter-form__group">
              <label htmlFor="start-date-filter" className="filter-form__label">
                Data Inicial
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
                Data Final
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
              Aplicar Filtros
            </button>
            <button
              type="button"
              className="filter-form__button filter-form__button--secondary"
              onClick={handleClearFilters}
              disabled={isLoading}
            >
              Limpar Filtros
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
        {(filters.storeId || filters.startDate || filters.endDate) && (
          <div className="transaction-query__active-filters">
            <p className="active-filters__label">Filtros Ativos:</p>
            <ul className="active-filters__list">
              {filters.storeId && (
                <li className="active-filters__item">
                  Loja: <strong>{filters.storeId}</strong>
                </li>
              )}
              {filters.startDate && (
                <li className="active-filters__item">
                  De: <strong>{formatDate(filters.startDate)}</strong>
                </li>
              )}
              {filters.endDate && (
                <li className="active-filters__item">
                  Até: <strong>{formatDate(filters.endDate)}</strong>
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
          <p>Carregando transações...</p>
        </div>
      )}

      {/* Error State */}
      {!isLoading && error && (
        <div className="transaction-query__error" role="alert">
          <p>{error}</p>
          <button
            className="transaction-query__retry-button"
            onClick={applyFilters}
          >
            Tentar Novamente
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !error && paginatedTransactions.length === 0 && (
        <div className="transaction-query__empty" role="status">
          <p>Nenhuma transação encontrada. Ajuste os filtros e tente novamente.</p>
        </div>
      )}

      {/* Transaction Table */}
      {!isLoading && !error && paginatedTransactions.length > 0 && (
        <>
          <div className="transaction-query__table-wrapper">
            <table className="transaction-table">
              <thead className="transaction-table__head">
                <tr>
                  <th className="transaction-table__header">Data</th>
                  <th className="transaction-table__header">Loja</th>
                  <th className="transaction-table__header">Valor</th>
                  <th className="transaction-table__header">Tipo</th>
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
                      <div>
                        <p className="transaction-table__store-name">
                          {transaction.storeName}
                        </p>
                        <p className="transaction-table__store-code">
                          {transaction.storeCode}
                        </p>
                      </div>
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
                Mostrando {paginatedTransactions.length} de {totalCount} transações
              </p>
              <label htmlFor="page-size-select" className="pagination__label">
                Itens por página:
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
                ← Anterior
              </button>

              <span className="pagination__indicator">
                Página {currentPage} de {totalPages || 1}
              </span>

              <button
                className="pagination__button"
                onClick={() => goToPage(currentPage + 1)}
                disabled={currentPage >= totalPages || isLoading}
              >
                Próxima →
              </button>
            </div>
          </div>
        </>
      )}
    </section>
  );
};

export default TransactionQueryComponent;
