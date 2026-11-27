import { describe, expect, it } from 'vitest';
import { MAX_FILE_BYTES, MAX_FILES_PER_APPLICATION, checkUpload, formatFileSize } from './files';

describe('formatFileSize', () => {
  it('uses bytes, kilobytes and megabytes', () => {
    expect(formatFileSize(845)).toBe('845 B');
    expect(formatFileSize(12 * 1024)).toBe('12 KB');
    expect(formatFileSize(3.4 * 1024 * 1024)).toBe('3.4 MB');
  });
});

describe('checkUpload', () => {
  it('accepts pdf, png and jpeg files of a sensible size, whatever the case of the extension', () => {
    expect(checkUpload({ name: 'income.pdf', size: 1000 }, 0)).toBeNull();
    expect(checkUpload({ name: 'SCAN.PNG', size: 1000 }, 3)).toBeNull();
    expect(checkUpload({ name: 'photo.jpeg', size: MAX_FILE_BYTES }, 0)).toBeNull();
  });

  it('refuses other types, empty files, oversized files and a full application', () => {
    expect(checkUpload({ name: 'run.exe', size: 1000 }, 0)).toBe('document.invalid_file');
    expect(checkUpload({ name: 'notes.txt', size: 1000 }, 0)).toBe('document.invalid_file');
    expect(checkUpload({ name: 'empty.pdf', size: 0 }, 0)).toBe('document.invalid_file');
    expect(checkUpload({ name: 'big.pdf', size: MAX_FILE_BYTES + 1 }, 0)).toBe('document.too_large');
    expect(checkUpload({ name: 'one-more.pdf', size: 1000 }, MAX_FILES_PER_APPLICATION)).toBe('document.limit_reached');
  });
});
