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
  /**
   * signingTime nằm SAU genTime của TSA -> thứ tự ký không thể xảy ra. Không còn là ngưỡng
   * khoảng cách giữa hai mốc (đồng hồ TSA không đủ ổn định để đo), nên verdict này rất hiếm.
   */
  TimeMismatch = 3,
  NotApplicable = 4,
  /** Cert người ký không do CA Ban Cơ yếu cấp (chain không về Root G1 lẫn Root G2) -> chặn. */
  UntrustedSigner = 5,
  /** Nội dung đã bị sửa sau khi ký, hoặc có byte nằm ngoài mọi vùng ký -> chặn. */
  InvalidSignature = 6
}

// Kết luận cho MỘT chữ ký trong file (khớp SignatureDetailDto BE).
export interface ISignatureDetail {
  signedBy?: string;
  /** ETimestampVerdict (số). */
  verdict: number;
  reason?: string;
  /** Dấu thời gian TSA của CHÍNH chữ ký này. */
  trustedTime?: string;
  /** Giờ ký tự khai của CHÍNH chữ ký này. */
  signingTime?: string;
}

// Kết quả verify DẤU THỜI GIAN tin cậy (RFC 3161) của 1 file (khớp TimestampVerifyDto BE).
export interface ITimestampVerify {
  /** ETimestampVerdict (số). */
  verdict: number;
  hasTimestamp: boolean;
  timestampAuthority?: string;
  /**
   * genTime thô của TSA (KHÔNG cộng offset), "dd/MM/yyyy HH:mm:ss".
   * Là giờ của chữ ký MỚI NHẤT trong file; giờ từng chữ ký nằm ở `signatures`.
   */
  trustedTime?: string;
  /** Giờ ký tự khai của chữ ký MỚI NHẤT. */
  signingTime?: string;
  /**
   * Ghi chú của BE. CÓ THỂ có nội dung ngay cả khi verdict ĐẠT (vd: bên ký ghi giờ địa
   * phương vào trường UTC signingTime). Không được suy ra lỗi từ việc reason có giá trị —
   * chỉ phân nhánh theo verdict / isValid.
   */
  reason?: string;
  /** = verdict ValidTrusted. */
  isValid?: boolean;
  /** Verdict này có chặn đóng gói không (PDF lỗi = true; hợp lệ & non-PDF = false). */
  blocks?: boolean;
  /**
   * Kết luận + giờ của từng chữ ký, xếp theo thứ tự ký (mới nhất ở cuối).
   * `verdict` ở trên = verdict xấu nhất trong danh sách này.
   */
  signatures?: ISignatureDetail[];
  /** Số chữ ký ĐẠT trong file (khác signatureBlockCount = tổng số chữ ký tìm thấy). */
  validSignatureCount?: number;
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
