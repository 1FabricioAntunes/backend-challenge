import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi, describe, it, beforeEach, expect } from 'vitest';
import FileUploadComponent from '../FileUploadComponent';
import apiClient from '../../services/api';
import validateCnabFile from '../../utils/fileValidation';

vi.mock('../../services/api', () => ({
  __esModule: true,
  default: {
    post: vi.fn(),
  },
}));

vi.mock('../../utils/fileValidation', () => ({
  __esModule: true,
  default: vi.fn(),
}));

vi.mock('axios', () => ({
  __esModule: true,
  default: { isAxiosError: (err: unknown) => Boolean((err as { isAxiosError?: boolean })?.isAxiosError) },
  isAxiosError: (err: unknown) => Boolean((err as { isAxiosError?: boolean })?.isAxiosError),
}));

const mockedPost = vi.mocked(apiClient.post);
const mockedValidate = vi.mocked(validateCnabFile);

const createFile = (name: string, content = 'test') => new File([content], name, { type: 'text/plain' });

describe('FileUploadComponent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders file input', () => {
    render(<FileUploadComponent />);
    expect(screen.getByLabelText('Select file')).toBeInTheDocument();
  });

  it('updates state when a file is selected', async () => {
    mockedValidate.mockResolvedValue({ isValid: true });

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

    await waitFor(() => expect(screen.getByText(/Selected: /)).toBeInTheDocument());
    expect(screen.getByText(/cnab.txt/)).toBeInTheDocument();
  });

  it('rejects invalid files via validation', async () => {
    mockedValidate.mockResolvedValue({ isValid: false, error: 'Invalid file' });

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file');
    fireEvent.change(fileInput, { target: { files: [createFile('bad.txt')] } });

    await waitFor(() => expect(screen.getByText('Invalid file')).toBeInTheDocument());
  });

  it('disables upload button during upload', async () => {
    mockedValidate.mockResolvedValue({ isValid: true });

    let resolvePost: ((value: unknown) => void) | null = null;
    mockedPost.mockImplementation((_url, _data, _config) => {
      return new Promise((resolve) => {
        resolvePost = resolve;
      });
    });

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file');
    fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

    const uploadButton = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(uploadButton);

    expect(uploadButton).toBeDisabled();

    resolvePost?.({ data: { fileId: '123' } });
    await waitFor(() => expect(mockedPost).toHaveBeenCalled());
  });

  it('updates progress bar during upload', async () => {
    mockedValidate.mockResolvedValue({ isValid: true });

    mockedPost.mockImplementation((_url, _data, config?: { onUploadProgress?: (evt: any) => void }) => {
      if (config?.onUploadProgress) {
        config.onUploadProgress({ loaded: 50, total: 100 });
      }
      return Promise.resolve({ data: { fileId: '123' } });
    });

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file');
    fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

    fireEvent.click(screen.getByRole('button', { name: /upload/i }));

    await waitFor(() => expect(screen.getByText('Uploading: 50%')).toBeInTheDocument());
  });

  it('shows success message after upload', async () => {
    mockedValidate.mockResolvedValue({ isValid: true });
    mockedPost.mockResolvedValue({ data: { fileId: 'file-abc' } });

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file');
    fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

    fireEvent.click(screen.getByRole('button', { name: /upload/i }));

    await waitFor(() => expect(screen.getByText(/Upload successful!/)).toBeInTheDocument());
    expect(screen.getByText(/file-abc/)).toBeInTheDocument();
  });

  it('shows error message on failure', async () => {
    mockedValidate.mockResolvedValue({ isValid: true });
    const error = {
      isAxiosError: true,
      response: { status: 400 },
    } as const;
    mockedPost.mockRejectedValue(error);

    render(<FileUploadComponent />);
    const fileInput = screen.getByLabelText('Select file');
    fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

    fireEvent.click(screen.getByRole('button', { name: /upload/i }));

    await waitFor(() => expect(screen.getByText('Invalid file format')).toBeInTheDocument());
  });
});
