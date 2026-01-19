/**
 * Test fixtures for common test data
 */

export const mockTransactions = [
  {
    id: 'tx-001',
    storeId: 'store-001',
    storeCode: '0001',
    storeName: 'Store A',
    type: 'debit',
    amount: 1000,
    date: '2026-01-19',
  },
  {
    id: 'tx-002',
    storeId: 'store-001',
    storeCode: '0001',
    storeName: 'Store A',
    type: 'credit',
    amount: 500,
    date: '2026-01-18',
  },
  {
    id: 'tx-003',
    storeId: 'store-002',
    storeCode: '0002',
    storeName: 'Store B',
    type: 'debit',
    amount: 750,
    date: '2026-01-17',
  },
];

export const mockStores = [
  {
    code: '0001',
    name: 'Store A',
    balance: 500,
  },
  {
    code: '0002',
    name: 'Store B',
    balance: -750,
  },
  {
    code: '0003',
    name: 'Store C',
    balance: 0,
  },
];

export const mockFileUploadResponse = {
  fileId: 'file-abc-123',
  status: 'Uploaded',
  uploadedAt: '2026-01-19T10:30:00Z',
  processingUrl: '/api/files/file-abc-123/status',
};
