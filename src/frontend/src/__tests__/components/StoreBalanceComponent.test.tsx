import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axios from 'axios';
import StoreBalanceComponent from '../../components/StoreBalanceComponent';

// Mock axios
vi.mock('axios');

const mockAxios = axios as any;

describe('StoreBalanceComponent', () => {
  const mockStores = [
    { code: '001', name: 'Store A', balance: 1250.50 },
    { code: '002', name: 'Store B', balance: -450.75 },
    { code: '003', name: 'Store C', balance: 5000.00 },
    { code: '004', name: 'Store D', balance: -120.30 },
    { code: '005', name: 'Store E', balance: 0.00 },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    mockAxios.get.mockResolvedValue({
      data: { stores: mockStores },
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render component with title and subtitle', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Saldo das Lojas')).toBeInTheDocument();
        expect(
          screen.getByText('Consulte o saldo atualizado de todas as lojas')
        ).toBeInTheDocument();
      });
    });

    it('should render search input with placeholder', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const searchInput = screen.getByPlaceholderText(
          'Buscar por nome ou código...'
        );
        expect(searchInput).toBeInTheDocument();
      });
    });

    it('should render sort dropdown', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByDisplayValue(/name-asc/)).toBeInTheDocument();
      });
    });

    it('should render refresh button', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const refreshButton = screen.getByTitle('Atualizar saldos');
        expect(refreshButton).toBeInTheDocument();
      });
    });

    it('should render table with headers', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Código')).toBeInTheDocument();
        expect(screen.getByText('Nome da Loja')).toBeInTheDocument();
        expect(screen.getByText('Saldo')).toBeInTheDocument();
      });
    });
  });

  describe('Store List and Formatting', () => {
    it('should render stores with formatted balances', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('001')).toBeInTheDocument();
        expect(screen.getByText('Store A')).toBeInTheDocument();
        expect(screen.getByText('R$ 1.250,50')).toBeInTheDocument();
      });
    });

    it('should render all stores in table', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        mockStores.forEach((store) => {
          expect(screen.getByText(store.code)).toBeInTheDocument();
          expect(screen.getByText(store.name)).toBeInTheDocument();
        });
      });
    });

    it('should format negative balances correctly', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('R$ -450,75')).toBeInTheDocument();
        expect(screen.getByText('R$ -120,30')).toBeInTheDocument();
      });
    });

    it('should format zero balance correctly', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('R$ 0,00')).toBeInTheDocument();
      });
    });
  });

  describe('Balance Color-Coding', () => {
    it('should apply positive balance styling to green cells', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const cells = screen.getAllByText(/R\$ \d+/);
        const positiveCell = cells.find((cell) =>
          cell.textContent?.includes('R$ 1.250,50')
        ) as HTMLElement;

        expect(positiveCell?.closest('td')).toHaveClass(
          'store-balance__table-cell--positive'
        );
      });
    });

    it('should apply negative balance styling to red cells', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const cells = screen.getAllByText(/R\$ -\d+/);
        cells.forEach((cell) => {
          expect(cell.closest('td')).toHaveClass(
            'store-balance__table-cell--negative'
          );
        });
      });
    });

    it('should apply zero balance styling to black cells', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const zeroCell = screen.getByText('R$ 0,00');
        expect(zeroCell.closest('td')).toHaveClass(
          'store-balance__table-cell--zero'
        );
      });
    });

    it('should display visual indicators (✓/✗/—)', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const indicators = screen.getAllByText(/[✓✗—]/);
        expect(indicators.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Total Balance Calculation', () => {
    it('should display total balance', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Saldo Total Geral')).toBeInTheDocument();
      });
    });

    it('should calculate correct total balance', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const expectedTotal =
          mockStores.reduce((sum, store) => sum + store.balance, 0);
        const formattedTotal = new Intl.NumberFormat('pt-BR', {
          style: 'currency',
          currency: 'BRL',
        }).format(expectedTotal);

        expect(screen.getByText(formattedTotal)).toBeInTheDocument();
      });
    });

    it('should display store count in total box', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('5 loja(s)')).toBeInTheDocument();
      });
    });

    it('should apply correct color-coding to total box (positive)', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const totalBox = screen.getByText('Saldo Total Geral').closest('div');
        expect(totalBox).toHaveClass('store-balance__total-box--positive');
      });
    });
  });

  describe('Sorting', () => {
    it('should sort stores by name ascending by default', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const rows = screen.getAllByText(/Store [A-Z]/);
        expect(rows[0]).toHaveTextContent('Store A');
        expect(rows[1]).toHaveTextContent('Store B');
      });
    });

    it('should sort by name descending when selected', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const sortSelect = screen.getByDisplayValue(/name-asc/);
        expect(sortSelect).toBeInTheDocument();
      });

      const user = userEvent.setup();
      const sortSelect = screen.getByDisplayValue(/name-asc/);

      await user.selectOption(sortSelect, 'name-desc');

      await waitFor(() => {
        const rows = screen.getAllByText(/Store [A-Z]/);
        expect(rows[0]).toHaveTextContent('Store E');
      });
    });

    it('should sort by code ascending', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const sortSelect = screen.getByDisplayValue(/name-asc/);
        expect(sortSelect).toBeInTheDocument();
      });

      const sortSelect = screen.getByDisplayValue(/name-asc/);
      await user.selectOption(sortSelect, 'code-asc');

      await waitFor(() => {
        const cells = screen.getAllByText(/\d{3}/);
        expect(cells[0]).toHaveTextContent('001');
        expect(cells[1]).toHaveTextContent('002');
      });
    });

    it('should sort by balance ascending', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const sortSelect = screen.getByDisplayValue(/name-asc/);
        expect(sortSelect).toBeInTheDocument();
      });

      const sortSelect = screen.getByDisplayValue(/name-asc/);
      await user.selectOption(sortSelect, 'balance-asc');

      await waitFor(() => {
        const balanceCells = screen.getAllByText(/R\$/);
        expect(balanceCells[0].textContent).toContain('-450,75');
      });
    });

    it('should sort by balance descending', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const sortSelect = screen.getByDisplayValue(/name-asc/);
        expect(sortSelect).toBeInTheDocument();
      });

      const sortSelect = screen.getByDisplayValue(/name-asc/);
      await user.selectOption(sortSelect, 'balance-desc');

      await waitFor(() => {
        const balanceCells = screen.getAllByText(/R\$/);
        expect(balanceCells[0].textContent).toContain('5.000,00');
      });
    });
  });

  describe('Search and Filtering', () => {
    it('should filter stores by name', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'Store B');

      await waitFor(() => {
        expect(screen.getByText('Store B')).toBeInTheDocument();
        expect(screen.queryByText('Store A')).not.toBeInTheDocument();
      });
    });

    it('should filter stores by code', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('001')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, '003');

      await waitFor(() => {
        expect(screen.getByText('003')).toBeInTheDocument();
        expect(screen.queryByText('001')).not.toBeInTheDocument();
      });
    });

    it('should perform case-insensitive search', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'store a');

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });
    });

    it('should clear search when clicking clear button', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      ) as HTMLInputElement;
      await user.type(searchInput, 'Store B');

      await waitFor(() => {
        expect(searchInput.value).toBe('Store B');
      });

      const clearButton = screen.getByTitle('Limpar busca');
      await user.click(clearButton);

      await waitFor(() => {
        expect(searchInput.value).toBe('');
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });
    });

    it('should display result count after search', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Mostrando 5 de 5 loja(s)')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'Store B');

      await waitFor(() => {
        expect(screen.getByText('Mostrando 1 de 5 loja(s)')).toBeInTheDocument();
      });
    });

    it('should show empty state when search returns no results', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'NonExistent');

      await waitFor(() => {
        expect(
          screen.getByText('Nenhuma loja encontrada com esse filtro')
        ).toBeInTheDocument();
      });
    });
  });

  describe('Loading State', () => {
    it('should display loading spinner while fetching', async () => {
      mockAxios.get.mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve({ data: { stores: mockStores } }), 100)
          )
      );

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Carregando saldos...')).toBeInTheDocument();
      });

      await waitFor(
        () => {
          expect(screen.queryByText('Carregando saldos...')).not.toBeInTheDocument();
        },
        { timeout: 200 }
      );
    });

    it('should disable refresh button while loading', async () => {
      mockAxios.get.mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve({ data: { stores: mockStores } }), 100)
          )
      );

      const { rerender } = render(<StoreBalanceComponent />);

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await userEvent.click(refreshButton);

      await waitFor(() => {
        expect(refreshButton).toBeDisabled();
      });

      await waitFor(
        () => {
          expect(refreshButton).not.toBeDisabled();
        },
        { timeout: 200 }
      );
    });

    it('should update button text while loading', async () => {
      mockAxios.get.mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve({ data: { stores: mockStores } }), 100)
          )
      );

      render(<StoreBalanceComponent />);

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await userEvent.click(refreshButton);

      await waitFor(() => {
        expect(refreshButton).toHaveTextContent('⟳ Atualizando...');
      });
    });
  });

  describe('Error State', () => {
    it('should display error message on network error', async () => {
      mockAxios.get.mockRejectedValueOnce(new Error('Network Error'));

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(
          screen.getByText('Erro de conexão. Verifique sua internet.')
        ).toBeInTheDocument();
      });
    });

    it('should display error message on 500 error', async () => {
      const error = new Error('Server Error');
      (error as any).response = { status: 500 };
      mockAxios.isAxiosError.mockReturnValue(true);
      mockAxios.get.mockRejectedValueOnce(error);

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(
          screen.getByText('Erro no servidor. Tente novamente.')
        ).toBeInTheDocument();
      });
    });

    it('should display default error message for unknown errors', async () => {
      const error = new Error('Unknown Error');
      (error as any).response = { status: 400 };
      mockAxios.isAxiosError.mockReturnValue(true);
      mockAxios.get.mockRejectedValueOnce(error);

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(
          screen.getByText('Erro ao carregar lojas. Tente novamente.')
        ).toBeInTheDocument();
      });
    });

    it('should show retry button in error state', async () => {
      mockAxios.get.mockRejectedValueOnce(new Error('Network Error'));

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const retryButton = screen.getByTitle('Atualizar saldos');
        expect(retryButton).toBeInTheDocument();
      });
    });

    it('should retry fetching when retry button is clicked', async () => {
      mockAxios.get.mockRejectedValueOnce(new Error('Network Error'));

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(
          screen.getByText('Erro de conexão. Verifique sua internet.')
        ).toBeInTheDocument();
      });

      mockAxios.get.mockResolvedValueOnce({
        data: { stores: mockStores },
      });

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await userEvent.click(refreshButton);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
        expect(
          screen.queryByText('Erro de conexão. Verifique sua internet.')
        ).not.toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('should display empty state when API returns no stores', async () => {
      mockAxios.get.mockResolvedValueOnce({
        data: { stores: [] },
      });

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(
          screen.getByText('Nenhuma loja cadastrada')
        ).toBeInTheDocument();
      });
    });
  });

  describe('API Integration', () => {
    it('should call API on component mount', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(mockAxios.get).toHaveBeenCalledWith('/api/stores/v1');
      });
    });

    it('should fetch stores and update state on mount', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        mockStores.forEach((store) => {
          expect(screen.getByText(store.name)).toBeInTheDocument();
        });
      });
    });

    it('should call API when refresh button is clicked', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(mockAxios.get).toHaveBeenCalledTimes(1);
      });

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await userEvent.click(refreshButton);

      await waitFor(() => {
        expect(mockAxios.get).toHaveBeenCalledTimes(2);
      });
    });

    it('should parse API response correctly', async () => {
      const apiResponse = {
        stores: [
          { code: 'TEST001', name: 'Test Store', balance: 999.99 },
        ],
      };

      mockAxios.get.mockResolvedValueOnce({
        data: apiResponse,
      });

      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('TEST001')).toBeInTheDocument();
        expect(screen.getByText('Test Store')).toBeInTheDocument();
        expect(screen.getByText('R$ 999,99')).toBeInTheDocument();
      });
    });

    it('should maintain search and sort state after API refresh', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'Store B');

      await waitFor(() => {
        expect(screen.getByText('Store B')).toBeInTheDocument();
      });

      mockAxios.get.mockResolvedValueOnce({
        data: { stores: mockStores },
      });

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await user.click(refreshButton);

      await waitFor(() => {
        expect(screen.getByText('Store B')).toBeInTheDocument();
        expect(screen.queryByText('Store A')).not.toBeInTheDocument();
      });
    });
  });

  describe('Refresh Button', () => {
    it('should call refresh when refresh button is clicked', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        expect(mockAxios.get).toHaveBeenCalledTimes(1);
      });

      const refreshButton = screen.getByTitle('Atualizar saldos');
      await user.click(refreshButton);

      await waitFor(() => {
        expect(mockAxios.get).toHaveBeenCalledTimes(2);
      });
    });

    it('should have proper aria label on refresh button', async () => {
      render(<StoreBalanceComponent />);

      await waitFor(() => {
        const refreshButton = screen.getByTitle('Atualizar saldos');
        expect(refreshButton).toBeInTheDocument();
      });
    });
  });

  describe('Integration Tests', () => {
    it('should display full workflow: search, sort, and show results', async () => {
      const user = userEvent.setup();
      render(<StoreBalanceComponent />);

      // Wait for initial load
      await waitFor(() => {
        expect(screen.getByText('Store A')).toBeInTheDocument();
      });

      // Search for a specific store
      const searchInput = screen.getByPlaceholderText(
        'Buscar por nome ou código...'
      );
      await user.type(searchInput, 'Store B');

      await waitFor(() => {
        expect(screen.getByText('Store B')).toBeInTheDocument();
        expect(screen.getByText('Mostrando 1 de 5 loja(s)')).toBeInTheDocument();
      });

      // Clear search
      const clearButton = screen.getByTitle('Limpar busca');
      await user.click(clearButton);

      await waitFor(() => {
        expect(screen.getByText('Mostrando 5 de 5 loja(s)')).toBeInTheDocument();
      });

      // Change sort order
      const sortSelect = screen.getByDisplayValue(/name-asc/);
      await user.selectOption(sortSelect, 'balance-desc');

      await waitFor(() => {
        const balanceCells = screen.getAllByText(/R\$/);
        expect(balanceCells[0].textContent).toContain('5.000,00');
      });
    });
  });
});
