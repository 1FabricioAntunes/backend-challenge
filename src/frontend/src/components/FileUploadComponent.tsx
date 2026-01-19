import { useCallback, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import apiClient from '../services/api';

const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
const CNAB_LINE_LENGTH = 80;

type UploadStatus = 'idle' | 'uploading' | 'success' | 'error';

type UploadResponse = {
  fileId: string;
};

const FileUploadComponent = () => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState<number>(0);
  const [uploadStatus, setUploadStatus] = useState<UploadStatus>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [uploadedFileId, setUploadedFileId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const resetState = useCallback(() => {
    setSelectedFile(null);
    setUploadProgress(0);
    setUploadStatus('idle');
    setErrorMessage(null);
    setUploadedFileId(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, []);

  const validateFile = useCallback(async (file: File): Promise<string | null> => {
    if (!file.name.toLowerCase().endsWith('.txt')) {
      return 'Only .txt CNAB files are allowed.';
    }

    if (file.size === 0) {
      return 'File is empty.';
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      return 'File must be smaller than 10MB.';
    }

    // Lightweight CNAB sanity check: first line should look like a fixed-width record
    const preview = await file.slice(0, 512).text();
    const firstLine = (preview.split(/\r?\n/)[0] || '').trim();
    if (firstLine.length < CNAB_LINE_LENGTH) {
      return 'File does not appear to be a CNAB file (first line too short).';
    }

    if (!/^\d/.test(firstLine)) {
      return 'File does not appear to be a CNAB file (expected numeric start).';
    }

    return null;
  }, []);

  const handleFileChange = useCallback(
    async (event: ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        resetState();
        return;
      }

      const validationError = await validateFile(file);
      if (validationError) {
        setErrorMessage(validationError);
        setSelectedFile(null);
        setUploadStatus('error');
        setUploadedFileId(null);
        setUploadProgress(0);
        return;
      }

      setSelectedFile(file);
      setErrorMessage(null);
      setUploadStatus('idle');
      setUploadedFileId(null);
      setUploadProgress(0);
    },
    [resetState, validateFile]
  );

  const handleUpload = useCallback(
    async (event: FormEvent) => {
      event.preventDefault();

      if (!selectedFile) {
        setErrorMessage('Please select a file to upload.');
        setUploadStatus('error');
        return;
      }

      setUploadStatus('uploading');
      setErrorMessage(null);
      setUploadedFileId(null);
      setUploadProgress(0);

      try {
        const formData = new FormData();
        formData.append('file', selectedFile);

        const response = await apiClient.post<UploadResponse>('/api/files/v1', formData, {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
          onUploadProgress: (progressEvent) => {
            if (!progressEvent.total) return;
            const percent = Math.round((progressEvent.loaded / progressEvent.total) * 100);
            setUploadProgress(percent);
          },
        });

        setUploadStatus('success');
        setUploadedFileId(response.data.fileId);
        setUploadProgress(100);
      } catch (error: unknown) {
        const message =
          (typeof error === 'object' && error !== null && 'message' in error)
            ? String((error as { message?: string }).message)
            : 'Upload failed. Please try again.';

        setErrorMessage(message);
        setUploadStatus('error');
        setUploadProgress(0);
      }
    },
    [selectedFile]
  );

  const handleReset = useCallback(() => {
    resetState();
  }, [resetState]);

  return (
    <section style={{ maxWidth: '520px', margin: '0 auto' }}>
      <header style={{ marginBottom: '16px' }}>
        <h2 style={{ margin: 0 }}>Upload CNAB File</h2>
        <p style={{ margin: '4px 0 0', color: '#555' }}>
          Only .txt CNAB files up to 10MB. Uploads are non-blocking; processing continues asynchronously.
        </p>
      </header>

      <form onSubmit={handleUpload} style={{ display: 'grid', gap: '12px' }}>
        <label style={{ display: 'grid', gap: '6px' }}>
          <span style={{ fontWeight: 600 }}>Select file</span>
          <input
            ref={fileInputRef}
            type="file"
            accept=".txt"
            onChange={handleFileChange}
            disabled={uploadStatus === 'uploading'}
          />
        </label>

        {selectedFile && (
          <div style={{ fontSize: '0.95rem', color: '#222' }}>
            Selected: <strong>{selectedFile.name}</strong> ({(selectedFile.size / 1024).toFixed(1)} KB)
          </div>
        )}

        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            type="submit"
            disabled={uploadStatus === 'uploading' || !selectedFile}
            style={{ padding: '10px 16px', fontWeight: 600, cursor: uploadStatus === 'uploading' ? 'not-allowed' : 'pointer' }}
          >
            {uploadStatus === 'uploading' ? 'Uploading...' : 'Upload'}
          </button>
          <button type="button" onClick={handleReset} style={{ padding: '10px 16px' }}>
            Reset
          </button>
        </div>

        {uploadStatus === 'uploading' && (
          <div style={{ display: 'grid', gap: '6px' }}>
            <progress value={uploadProgress} max={100} style={{ width: '100%' }} />
            <span style={{ fontSize: '0.9rem' }}>Uploading: {uploadProgress}%</span>
          </div>
        )}

        {uploadStatus === 'success' && uploadedFileId && (
          <div style={{ padding: '10px', backgroundColor: '#e6ffed', color: '#0f5132', borderRadius: '6px' }}>
            Upload successful! File ID: <strong>{uploadedFileId}</strong>
          </div>
        )}

        {uploadStatus === 'error' && errorMessage && (
          <div style={{ padding: '10px', backgroundColor: '#ffefef', color: '#8a1f1f', borderRadius: '6px' }}>
            {errorMessage}
          </div>
        )}
      </form>
    </section>
  );
};

export default FileUploadComponent;
