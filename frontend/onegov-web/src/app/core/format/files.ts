export const MAX_FILE_BYTES = 5 * 1024 * 1024;
export const MAX_FILES_PER_APPLICATION = 10;

const ALLOWED_EXTENSIONS = ['.pdf', '.png', '.jpg', '.jpeg'];

/** A file size for people: "845 B", "12 KB", "3.4 MB". */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * A quick check before uploading, so people get an answer without waiting for the upload. The server checks again and is the
 * authority: it recognises the real file type from its content. Returns an error code, or null when the file looks fine.
 */
export function checkUpload(file: { name: string; size: number }, existingCount: number): string | null {
  if (existingCount >= MAX_FILES_PER_APPLICATION) {
    return 'document.limit_reached';
  }
  if (file.size <= 0) {
    return 'document.invalid_file';
  }
  if (file.size > MAX_FILE_BYTES) {
    return 'document.too_large';
  }
  const name = file.name.toLowerCase();
  return ALLOWED_EXTENSIONS.some((extension) => name.endsWith(extension)) ? null : 'document.invalid_file';
}
