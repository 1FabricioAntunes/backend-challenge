import { useCallback, useEffect, useState } from 'react';
import type { ChangeEvent } from 'react';

// TypeScript Interfaces
interface Store {
  code: string;
  name: string;
  balance: number;
}

type SortField = 'code' | 'name' | 'balance';
type SortOrder = 'asc' | 'desc';

// Sample data for development (will be replaced with API call)
const SAMPLE_STORES: Store[] = [
  { code: '001', name: 'Store A', balance: 1250.50 },
  { code: '002', name: 'Store B', balance: -450.75 },
  { code: '003', name: 'Store C', balance: 5000.00 },
  { code: '004', name: 'Store D', balance: -120.30 },
  { code: '005', name: 'Store E', balance: 3300.90 },
  { code: '006', name: 'Store F', balance: 0.00 },
  { code: '007', name: 'Store G', balance: 890.45 },
];

const StoreBalanceComponent = () => {
  // State Management
  const [stores, setStores] = useState<Store[]>(SAMPLE_STORES);
  const [filteredStores, setFilteredStores] = useState<Store[]>(SAMPLE_STORES);
  const [searchText, setSearchText] = useState<string>('');
  const [sortField, setSortField] = useState<SortField>('name');
  const [sortOrder, setSortOrder] = useState<SortOrder>('asc');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  /**
   * Format currency amount to Brazilian Real format (R$ X,XXX.XX)
   */
  const formatCurrency = useCallback((amount: number): string => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(amount);
  }, []);

  /**
   * Calculate total balance across all stores
   */
  const calculateTotalBalance = useCallback((storesArray: Store[]): number => {
    return storesArray.reduce((sum, store) => sum + store.balance, 0);
  }, []);

  /**
   * Filter stores based on search text (case-insensitive)
   * Searches in both store code and name
   */
  const filterStores = useCallback(
    (text: string) => {
      setSearchText(text);
      const searchLower = text.toLowerCase();

      const filtered = stores.filter(
        (store) =>
          store.code.toLowerCase().includes(searchLower) ||
          store.name.toLowerCase().includes(searchLower)
      );

      sortStores(sortField, sortOrder, filtered);
    },
    [stores, sortField, sortOrder]
  );

  /**
   * Sort stores by specified field and order
   * Applies current filter if filter exists
   */
  const sortStores = useCallback(
    (field: SortField, order: SortOrder, baseStores?: Store[]) => {
      setSortField(field);
      setSortOrder(order);

      const storesCopy = [...(baseStores || filteredStores)];

      storesCopy.sort((a, b) => {
        let compareValue = 0;

        switch (field) {
          case 'code':
            compareValue = a.code.localeCompare(b.code);
            break;
          case 'name':
            compareValue = a.name.localeCompare(b.name);
            break;
          case 'balance':
            compareValue = a.balance - b.balance;
            break;
          default:
            compareValue = 0;
        }

        return order === 'asc' ? compareValue : -compareValue;
      });

      setFilteredStores(storesCopy);
    },
    [filteredStores]
  );

  /**
   * Refresh balances from API (stub - will be implemented in next step)
   * Currently uses sample data and simulates loading state
   */
  const refreshBalances = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      // TODO: Replace with actual API call to GET /api/stores/v1
      // For now, simulate API delay
      await new Promise((resolve) => setTimeout(resolve, 500));

      // In production: const response = await apiClient.get('/stores/v1');
      // For now, use sample data
      const newStores = [...SAMPLE_STORES];
      setStores(newStores);

      // Re-apply current filters and sorting
      const searchLower = searchText.toLowerCase();
      const filtered = newStores.filter(
        (store) =>
          store.code.toLowerCase().includes(searchLower) ||
          store.name.toLowerCase().includes(searchLower)
      );

      sortStores(sortField, sortOrder, filtered);
    } catch (err) {
      const errorMsg =
        err instanceof Error ? err.message : 'Failed to refresh store balances';
      setError(errorMsg);
    } finally {
      setIsLoading(false);
    }
  }, [searchText, sortField, sortOrder, sortStores]);

  // Load initial data on component mount
  useEffect(() => {
    // Initial sort
    sortStores(sortField, sortOrder);
  }, []);

  const totalBalance = calculateTotalBalance(filteredStores);
  const hasStores = filteredStores.length > 0;

  return (
    <div className="store-balance">
      {/* Header */}
      <div className="store-balance__header">
        <div className="store-balance__title-group">
          <h1 className="store-balance__title">Saldo das Lojas</h1>
          <p className="store-balance__subtitle">
            Consulte o saldo atualizado de todas as lojas
          </p>
        </div>

        <button
          className="store-balance__refresh-button"
          onClick={refreshBalances}
          disabled={isLoading}
          title="Atualizar saldos"
        >
          {isLoading ? '⟳ Atualizando...' : '↻ Atualizar'}
        </button>
      </div>

      {/* Search and Sort Controls */}
      <div className="store-balance__controls">
        <div className="store-balance__search">
          <input
            type="text"
            className="store-balance__search-input"
            placeholder="Buscar por nome ou código..."
            value={searchText}
            onChange={(e: ChangeEvent<HTMLInputElement>) =>
              filterStores(e.target.value)
            }
          />
          {searchText && (
            <button
              className="store-balance__search-clear"
              onClick={() => filterStores('')}
              title="Limpar busca"
              aria-label="Limpar busca"
            >
              ✕
            </button>
          )}
        </div>

        <div className="store-balance__sort-group">
          <label className="store-balance__sort-label" htmlFor="sort-field">
            Ordenar por:
          </label>
          <select
            id="sort-field"
            className="store-balance__sort-select"
            value={`${sortField}-${sortOrder}`}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => {
              const [field, order] = e.target.value.split('-') as [
                SortField,
                SortOrder,
              ];
              sortStores(field, order);
            }}
          >
            <option value="name-asc">Nome (A-Z)</option>
            <option value="name-desc">Nome (Z-A)</option>
            <option value="code-asc">Código (↑)</option>
            <option value="code-desc">Código (↓)</option>
            <option value="balance-asc">Saldo (Menor)</option>
            <option value="balance-desc">Saldo (Maior)</option>
          </select>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="store-balance__error">
          <span className="store-balance__error-icon">⚠</span>
          <span className="store-balance__error-text">{error}</span>
        </div>
      )}

      {/* Loading State */}
      {isLoading && (
        <div className="store-balance__loading">
          <div className="store-balance__spinner"></div>
          <p>Carregando saldos...</p>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !hasStores && (
        <div className="store-balance__empty">
          <p className="store-balance__empty-text">
            {searchText
              ? 'Nenhuma loja encontrada com esse filtro'
              : 'Nenhuma loja cadastrada'}
          </p>
        </div>
      )}

      {/* Store Table */}
      {!isLoading && hasStores && (
        <>
          <div className="store-balance__result-info">
            Mostrando {filteredStores.length} de {stores.length} loja(s)
          </div>

          <div className="store-balance__table-wrapper">
            <table className="store-balance__table">
              <thead className="store-balance__table-head">
                <tr className="store-balance__table-row">
                  <th className="store-balance__table-header store-balance__table-header--code">
                    Código
                  </th>
                  <th className="store-balance__table-header store-balance__table-header--name">
                    Nome da Loja
                  </th>
                  <th className="store-balance__table-header store-balance__table-header--balance">
                    Saldo
                  </th>
                </tr>
              </thead>
              <tbody className="store-balance__table-body">
                {filteredStores.map((store) => (
                  <tr
                    key={store.code}
                    className="store-balance__table-row store-balance__table-row--data"
                  >
                    <td className="store-balance__table-cell store-balance__table-cell--code">
                      {store.code}
                    </td>
                    <td className="store-balance__table-cell store-balance__table-cell--name">
                      {store.name}
                    </td>
                    <td
                      className={`store-balance__table-cell store-balance__table-cell--balance ${
                        store.balance > 0
                          ? 'store-balance__table-cell--positive'
                          : store.balance < 0
                            ? 'store-balance__table-cell--negative'
                            : 'store-balance__table-cell--zero'
                      }`}
                    >
                      <span className="store-balance__balance-indicator">
                        {store.balance > 0 ? '✓' : store.balance < 0 ? '✗' : '—'}
                      </span>
                      {formatCurrency(store.balance)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Total Balance Display */}
          <div className="store-balance__total">
            <div
              className={`store-balance__total-box ${
                totalBalance > 0
                  ? 'store-balance__total-box--positive'
                  : totalBalance < 0
                    ? 'store-balance__total-box--negative'
                    : 'store-balance__total-box--zero'
              }`}
            >
              <div className="store-balance__total-label">Saldo Total Geral</div>
              <div className="store-balance__total-amount">
                {formatCurrency(totalBalance)}
              </div>
              <div className="store-balance__total-count">
                {filteredStores.length} loja(s)
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default StoreBalanceComponent;
