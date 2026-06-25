// DTO khớp với CheckProgressDto (BE) — mỗi event SSE của quá trình kiểm tra hồ sơ.
export interface ICheckProgress {
  stage: number;
  stageName: string;
  /** folder | file | sheet | field | code | summary | manifest */
  targetType: string;
  target: string;
  /** 1 = Success, 0 = Error */
  status: number;
  code: number;
  message: string;
  completed: boolean;
  current: number;
  total: number;
  percent: number;
  /** Chỉ có ở event manifest: danh sách tên file đính kèm để seed sẵn các dòng. */
  files?: string[];
}

// 1 dòng file hiển thị trên bảng trạng thái (1 file đính kèm = 1 tài liệu trong sheet Van_ban).
export interface IFileRow {
  name: string;
  status: 'processing' | 'ok' | 'error';
  reason?: string;
}
