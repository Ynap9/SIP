// Thông tin 1 lần ký (khớp SignerInfoDto BE).
export interface ISignerInfo {
  signedBy?: string;
  signedAt?: string;
  reason?: string;
  location?: string;
}

// Kết luận verify dấu thời gian (khớp enum TimestampVerdict BE - serialize dạng số).
export enum ETimestampVerdict {
  ValidTrusted = 0,
  NoTimestamp = 1,
  UntrustedTsa = 2,
  TimeMismatch = 3,
  NotApplicable = 4
}

// Kết quả verify DẤU THỜI GIAN tin cậy (RFC 3161) của 1 file (khớp TimestampVerifyDto BE).
export interface ITimestampVerify {
  /** ETimestampVerdict (số). */
  verdict: number;
  hasTimestamp: boolean;
  timestampAuthority?: string;
  /** genTime + 7h, "dd/MM/yyyy HH:mm:ss". */
  trustedTime?: string;
  signingTime?: string;
  reason?: string;
  /** = verdict ValidTrusted. */
  isValid?: boolean;
  /** Verdict này có chặn đóng gói không (PDF lỗi = true; hợp lệ & non-PDF = false). */
  blocks?: boolean;
}

// Thông tin ký số của 1 file (khớp SignatureInfoDto BE).
export interface ISignatureInfo {
  isSigned: boolean;
  signatureCount: number;
  signers: ISignerInfo[];
  error?: string;
  /** Kết quả verify dấu thời gian tin cậy so với TSA Ban Cơ yếu (PDF verdict xấu -> chặn). */
  timestamp?: ITimestampVerify;
}

// Kết quả gom file chưa ký số (khớp CollectUnsignedResultDto BE).
export interface ICollectUnsignedResult {
  parentPath: string;
  hoSoCount: number;
  fileCount: number;
  message: string;
}

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
  /** Chỉ có ở event kiểm tra 1 file đính kèm (giai đoạn 1): thông tin ký số. */
  signature?: ISignatureInfo;
}

// 1 dòng file hiển thị trên bảng trạng thái (1 file đính kèm = 1 tài liệu trong sheet Van_ban).
export interface IFileRow {
  name: string;
  status: 'processing' | 'ok' | 'error';
  reason?: string;
  /** Thông tin ký số của file (để hiển thị cột "Ký số"). */
  signature?: ISignatureInfo;
}
