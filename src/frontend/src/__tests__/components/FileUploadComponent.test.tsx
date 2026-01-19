import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi, describe, it, beforeEach, expect } from 'vitest';
import FileUploadComponent from '../../components/FileUploadComponent';
import apiClient from '../../services/api';
import validateCnabFile from '../../utils/fileValidation';
import { createAxiosError } from '../mocks/api.mock';

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

  it('should render file input', () => {
    render(<FileUploadComponent />);
    expect(screen.getByLabelText('Select file')).toBeInTheDocument();
  });

  it('should display upload button in initial state', () => {
    render(<FileUploadComponent />);
    expect(screen.getByRole('button', { name: /upload/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /upload/i })).toBeDisabled();
  });

  describe('File Selection', () => {
    it('should update state when a valid file is selected', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file') as HTMLInputElement;
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt', 'file content')] } });

      await waitFor(() => expect(screen.getByText(/Selected: /)).toBeInTheDocument());
      expect(screen.getByText(/cnab.txt/)).toBeInTheDocument();
    });

    it('should reject invalid files and display error', async () => {
      mockedValidate.mockResolvedValue({ isValid: false, error: 'File too large (max 10MB)' });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('huge.txt')] } });

      await waitFor(() => expect(screen.getByText('File too large (max 10MB)')).toBeInTheDocument());
    });

    it('should clear error on new file selection', async () => {
      mockedValidate.mockResolvedValueOnce({ isValid: false, error: 'Invalid format' });
      mockedValidate.mockResolvedValueOnce({ isValid: true });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file') as HTMLInputElement;

      fireEvent.change(fileInput, { target: { files: [createFile('bad.txt')] } });
      await waitFor(() => expect(screen.getByText('Invalid format')).toBeInTheDocument());

      fireEvent.change(fileInput, { target: { files: [createFile('good.txt')] } });
      await waitFor(() => expect(screen.queryByText('Invalid format')).not.toBeInTheDocument());
    });
  });

  describe('Upload Behavior', () => {
    it('should disable upload button during upload', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });

      let resolvePost: ((value: unknown) => void) | null = null;
      mockedPost.mockImplementation(
        (_url, _data, _config) =>
          new Promise((resolve) => {
            resolvePost = resolve;
          })
      );

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      const uploadButton = screen.getByRole('button', { name: /upload/i });
      fireEvent.click(uploadButton);

      expect(uploadButton).toBeDisabled();
      expect(screen.getByRole('button', { name: /reset/i })).toBeDisabled();

      resolvePost?.({ data: { fileId: '123' } });
      await waitFor(() => expect(mockedPost).toHaveBeenCalled());
    });

    it('should display error when no file is selected', async () => {
      render(<FileUploadComponent />);
      const uploadButton = screen.getByRole('button', { name: /upload/i });
      fireEvent.click(uploadButton);

      await waitFor(() => expect(screen.getByText('Please select a file to upload.')).toBeInTheDocument());
    });
  });

  describe('Upload Progress', () => {
    it('should update progress bar during upload', async () => {
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

    it('should display upload speed and ETA', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });

      mockedPost.mockImplementation((_url, _data, config?: { onUploadProgress?: (evt: any) => void }) => {
        if (config?.onUploadProgress) {
          config.onUploadProgress({ loaded: 1000000, total: 10000000 });
        }
        return Promise.resolve({ data: { fileId: '123' } });
      });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('large.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => {
        expect(screen.getByText(/KB\/s/)).toBeInTheDocument();
        expect(screen.getByText(/ETA/)).toBeInTheDocument();
      });
    });
  });

  describe('Success Handling', () => {
    it('should display success message with file ID', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockResolvedValue({ data: { fileId: 'file-abc-123' } });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText(/Upload successful!/)).toBeInTheDocument());
      expect(screen.getByText('file-abc-123')).toBeInTheDocument();
    });

    it('should display link to view file status', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockResolvedValue({ data: { fileId: 'file-123' } });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() =>
        expect(screen.getByRole('link', { name: /View uploaded file status/ })).toBeInTheDocument()
      );
    });

    it('should auto-clear success message after 5 seconds', async () => {
      vi.useFakeTimers();
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockResolvedValue({ data: { fileId: 'file-123' } });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText(/Upload successful!/)).toBeInTheDocument());

      vi.advanceTimersByTime(5000);

      await waitFor(() => expect(screen.queryByText(/Upload successful!/)).not.toBeInTheDocument());
      vi.useRealTimers();
    });

    it('should allow manual dismiss of success message', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockResolvedValue({ data: { fileId: 'file-123' } });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText(/Upload successful!/)).toBeInTheDocument());

      const closeButton = screen.getByLabelText('Close success message');
      fireEvent.click(closeButton);

      await waitFor(() => expect(screen.queryByText(/Upload successful!/)).not.toBeInTheDocument());
    });
  });

  describe('Error Handling', () => {
    it('should display 400 error as "Invalid file format"', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockRejectedValue(createAxiosError(400, { message: 'Bad request' }));

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText('Invalid file format')).toBeInTheDocument());
    });

    it('should display 413 error as "File too large"', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockRejectedValue(createAxiosError(413));

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText('File too large (max 10MB)')).toBeInTheDocument());
    });

    it('should display 500 error as "Server error"', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockRejectedValue(createAxiosError(500));

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText('Server error, please try again')).toBeInTheDocument());
    });

    it('should display retry button on error', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockRejectedValue(createAxiosError(500));

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument());
    });

    it('should allow manual dismiss of error message', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });
      mockedPost.mockRejectedValue(createAxiosError(500));

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file');
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      fireEvent.click(screen.getByRole('button', { name: /upload/i }));

      await waitFor(() => expect(screen.getByText('Server error, please try again')).toBeInTheDocument());

      const closeButton = screen.getByLabelText('Dismiss error message');
      fireEvent.click(closeButton);

      await waitFor(() => expect(screen.queryByText('Server error, please try again')).not.toBeInTheDocument());
    });
  });

  describe('Reset Button', () => {
    it('should clear form on reset', async () => {
      mockedValidate.mockResolvedValue({ isValid: true });

      render(<FileUploadComponent />);
      const fileInput = screen.getByLabelText('Select file') as HTMLInputElement;
      fireEvent.change(fileInput, { target: { files: [createFile('cnab.txt')] } });

      await waitFor(() => expect(screen.getByText(/cnab.txt/)).toBeInTheDocument());

      const resetButton = screen.getByRole('button', { name: /reset/i });
      fireEvent.click(resetButton);

      await waitFor(() => expect(screen.queryByText(/cnab.txt/)).not.toBeInTheDocument());
      expect(fileInput.value).toBe('');
    });
  });
});
