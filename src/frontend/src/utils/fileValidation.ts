const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
const CNAB_MAGIC_MIN_LENGTH = 10; // quick sanity: first line must have at least 10 chars

export type FileValidationResult = {
  isValid: boolean;
  error?: string;
};

// Lightweight CNAB validation: checks extension, size, emptiness, and basic first-line structure.
export const validateCnabFile = async (file: File): Promise<FileValidationResult> => {
  if (!file) {
    return { isValid: false, error: 'No file selected.' };
  }

  if (!file.name.toLowerCase().endsWith('.txt')) {
    return { isValid: false, error: 'Only .txt CNAB files are allowed.' };
  }

  if (file.size === 0) {
    return { isValid: false, error: 'File is empty.' };
  }

  if (file.size > MAX_FILE_SIZE_BYTES) {
    return { isValid: false, error: 'File must be smaller than 10MB.' };
  }

  const preview = await file.slice(0, 256).text();
  const firstLine = (preview.split(/\r?\n/)[0] || '').trim();

  if (firstLine.length < CNAB_MAGIC_MIN_LENGTH) {
    return { isValid: false, error: 'File does not appear to be a CNAB file (first line too short).' };
  }

  if (!/^\d/.test(firstLine)) {
    return { isValid: false, error: 'File does not appear to be a CNAB file (expected numeric start).' };
  }

  return { isValid: true };
};

export default validateCnabFile;