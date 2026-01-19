import { useCallback, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import apiClient from '../services/api';
import validateCnabFile from '../utils/fileValidation';

type UploadStatus = 'idle' | 'uploading' | 'success' | 'error';

type UploadResponse = {
  fileId: string;
};

const FileUploadComponent = () => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState<number>(0);
  const [uploadSpeed, setUploadSpeed] = useState<number | null>(null); // bytes/sec
  const [etaSeconds, setEtaSeconds] = useState<number | null>(null);
  const [uploadStatus, setUploadStatus] = useState<UploadStatus>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [uploadedFileId, setUploadedFileId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const uploadStartRef = useRef<number | null>(null);

  const resetState = useCallback(() => {
    setSelectedFile(null);
    setUploadProgress(0);
    setUploadStatus('idle');
    setErrorMessage(null);
    setUploadedFileId(null);
    setUploadSpeed(null);
    setEtaSeconds(null);
    uploadStartRef.current = null;
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, []);

  const handleFileChange = useCallback(
    async (event: ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) {
        resetState();
        return;
      }

      const { isValid, error } = await validateCnabFile(file);
      if (!isValid) {
        setErrorMessage(error ?? 'Invalid file.');
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
    [resetState]
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
      setUploadSpeed(null);
      setEtaSeconds(null);
      uploadStartRef.current = performance.now();

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

            const startedAt = uploadStartRef.current;
            if (startedAt) {
              const elapsedSeconds = (performance.now() - startedAt) / 1000;
              if (elapsedSeconds > 0) {
                const speedBytesPerSec = progressEvent.loaded / elapsedSeconds;
                setUploadSpeed(speedBytesPerSec);

                const remainingBytes = progressEvent.total - progressEvent.loaded;
                const eta = speedBytesPerSec > 0 ? remainingBytes / speedBytesPerSec : null;
                setEtaSeconds(typeof eta === 'number' && Number.isFinite(eta) ? eta : null);
              }
            }
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
          <button
            type="button"
            onClick={handleReset}
            disabled={uploadStatus === 'uploading'}
            style={{ padding: '10px 16px', cursor: uploadStatus === 'uploading' ? 'not-allowed' : 'pointer' }}
          >
            Reset
          </button>
        </div>

        {uploadStatus === 'uploading' && (
          <div style={{ display: 'grid', gap: '6px' }}>
            <progress value={uploadProgress} max={100} style={{ width: '100%' }} />
            <span style={{ fontSize: '0.9rem' }}>Uploading: {uploadProgress}%</span>
            {uploadSpeed && (
              <span style={{ fontSize: '0.85rem', color: '#444' }}>
                ~{(uploadSpeed / 1024).toFixed(1)} KB/s
                {etaSeconds !== null && Number.isFinite(etaSeconds) ? ` · ETA ~${Math.max(1, Math.round(etaSeconds))}s` : ''}
              </span>
            )}
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
