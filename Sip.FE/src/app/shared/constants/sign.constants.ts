// Enum + hằng số hiển thị cho luồng KÝ SỐ. Model (interface) nằm ở models/sign.models.ts.

// Nguồn chứa chứng thư số (khớp enum CertSource BE - serialize dạng số).
export enum ECertSource {
  /** Store cá nhân của user hiện tại (CurrentUser\My) - khóa nằm trong phần mềm. */
  Local = 0,
  /** Store cấp máy (LocalMachine\My) - cert cài cho máy/dịch vụ. */
  Server = 1,
  /** USB token/smartcard - cert trong store nhưng private key ở provider phần cứng. */
  UsbToken = 2
}

// Nhãn tiếng Việt của từng nguồn cert - dùng cho cột "Nguồn" và dropdown.
export const CERT_SOURCE_LABELS: Record<ECertSource, string> = {
  [ECertSource.Local]: 'Máy cá nhân',
  [ECertSource.Server]: 'Máy chủ',
  [ECertSource.UsbToken]: 'USB token'
};

// Màu tag PrimeNG theo nguồn cert. Token là nguồn ký thật nên nhấn màu đậm nhất.
export const CERT_SOURCE_SEVERITIES: Record<ECertSource, 'success' | 'info' | 'secondary'> = {
  [ECertSource.Local]: 'info',
  [ECertSource.Server]: 'secondary',
  [ECertSource.UsbToken]: 'success'
};

// Nhãn cột trạng thái ký.
export const CERT_SIGNABLE_LABEL = 'Ký được';
export const CERT_NOT_SIGNABLE_LABEL = 'Không ký được';

// Verdict kiểm ĐỊNH DẠNG file trước khi ký (khớp enum PdfFormatVerdict BE - serialize dạng số).
// Chỉ Valid mới ký được; mọi verdict khác đều chặn (fail-closed).
export enum EPdfFormatVerdict {
  Valid = 0,
  NotPdf = 1,
  NotPdfA = 2,
  NotTwoLayer = 3,
  ReadFailed = 4
}

// Nhãn tiếng Việt ngắn gọn cho cột "Trạng thái" của bảng kiểm định dạng.
export const PDF_FORMAT_VERDICT_LABELS: Record<EPdfFormatVerdict, string> = {
  [EPdfFormatVerdict.Valid]: 'Hợp lệ',
  [EPdfFormatVerdict.NotPdf]: 'Không phải PDF',
  [EPdfFormatVerdict.NotPdfA]: 'Không phải PDF/A',
  [EPdfFormatVerdict.NotTwoLayer]: 'Thiếu 2 lớp',
  [EPdfFormatVerdict.ReadFailed]: 'Không đọc được'
};

// Màu tag PrimeNG theo verdict: chỉ Valid xanh, còn lại đỏ (đều là lý do chặn ký).
export const PDF_FORMAT_VERDICT_SEVERITIES: Record<EPdfFormatVerdict, 'success' | 'danger'> = {
  [EPdfFormatVerdict.Valid]: 'success',
  [EPdfFormatVerdict.NotPdf]: 'danger',
  [EPdfFormatVerdict.NotPdfA]: 'danger',
  [EPdfFormatVerdict.NotTwoLayer]: 'danger',
  [EPdfFormatVerdict.ReadFailed]: 'danger'
};

// Trạng thái ký của 1 dòng file trên bảng.
export type SignRowStatus = 'idle' | 'processing' | 'signed' | 'skipped' | 'failed';

// ===== Bước 2: cấu hình chữ ký =====

// Khổ khối chữ ký chuẩn (point) trích từ file production - BE dùng đúng bộ số này khi tự đặt vị trí.
// FE dùng để suy ra tỉ lệ mặc định của ô kéo thả theo khổ trang đang xem.
export const SIGN_BLOCK_WIDTH_PT = 170;
export const SIGN_BLOCK_HEIGHT_PT = 30;

// Giới hạn kéo góc phóng to/thu nhỏ khối chữ ký. 1 = đúng khổ gốc 170x30pt của trang đang xem; không kéo
// góc thì giữ nguyên 1. Kéo góc chỉ đổi MỘT hệ số (không kéo méo) vì BE suy cỡ chữ từ CHIỀU CAO khối
// (fontSize = 10 * height/30): khối méo là chữ tràn ra ngoài hoặc lọt thỏm giữa khung.
export const SIGN_BLOCK_MIN_SCALE = 0.5;
export const SIGN_BLOCK_MAX_SCALE = 4;

// Cỡ chữ trong khối = chiều cao khối / 3 (10pt chữ trên khổ chuẩn 30pt). Dùng để vẽ khối xem trước có tỉ lệ
// chữ đúng như file ký ra, ở mọi mức phóng.
export const SIGN_BLOCK_FONT_RATIO = 1 / 3;

// Cấu hình mặc định = nút "Tự cấu hình": hệ thống tự đặt chữ ký góc trên-phải trang 1 (vẫn co giãn theo khổ
// trang), không đè nội dung, không file nào có cấu hình riêng.
export const SIGN_DEFAULT_CONFIG = {
  useCustomPosition: false,
  allowOverlapContent: false,
  positions: null,
  fileOverrides: null
} as const;

// Nhãn cột "Trạng thái ký số" theo trạng thái từng dòng file.
export const SIGN_STATUS_LABELS: Record<SignRowStatus, string> = {
  idle: 'Chưa ký',
  processing: 'Đang ký',
  signed: 'Đã ký',
  skipped: 'Bỏ qua',
  failed: 'Ký lỗi'
};
