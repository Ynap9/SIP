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
// Verdict luôn là định dạng THẬT của file.
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

// Màu tag PrimeNG khi file KHÔNG ký được; bảng đọc màu qua SignStep.verdictSeverity(verdict, canSign).
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

export const SIGN_BLOCK_MIN_SCALE = 0.5;
export const SIGN_BLOCK_MAX_SCALE = 4;

export const SIGN_FONT_FAMILIES = ['Times New Roman', 'Arial', 'Verdana', 'Georgia', 'Courier New'];
export const SIGN_FONT_FAMILY_DEFAULT = 'Times New Roman';
export const SIGN_FONT_SIZE_DEFAULT = 10;
export const SIGN_FONT_SIZE_MIN = 6;
export const SIGN_FONT_SIZE_MAX = 24;
export const SIGN_FONT_SIZE_STEP = 0.5;
export const SIGN_TEXT_COLOR_DEFAULT = '#000000';
export const SIGN_LINE_HEIGHT_RATIO = 1.15;
export const SIGN_REFERENCE_PAGE_WIDTH_PT = 595;
export const SIGN_REFERENCE_PAGE_HEIGHT_PT = 842;

// Cấu hình mặc định = nút "Tự cấu hình": hệ thống tự đặt chữ ký góc trên-phải trang 1 (vẫn co giãn theo khổ
// trang), không đè nội dung, không file nào có cấu hình riêng.
export const SIGN_DEFAULT_CONFIG = {
  useCustomPosition: false,
  allowOverlapContent: false,
  positions: null,
  fileOverrides: null
} as const;

export enum ESignTemplatePositionKind {
  ChuKy = 0,
  DauDo = 1,
  ChuKyTuoi = 2
}

export const SIGN_TEMPLATE_POSITION_KIND_LABELS: Record<ESignTemplatePositionKind, string> = {
  [ESignTemplatePositionKind.ChuKy]: 'Chữ ký số',
  [ESignTemplatePositionKind.DauDo]: 'Dấu đỏ',
  [ESignTemplatePositionKind.ChuKyTuoi]: 'Chữ ký tươi'
};

export const SIGN_IMAGE_BLOCK_WIDTH_PT = 100;

export const SIGN_TEMPLATE_NEW_ID = 'moi';

// Nhãn cột "Trạng thái ký số" theo trạng thái từng dòng file.
export const SIGN_STATUS_LABELS: Record<SignRowStatus, string> = {
  idle: 'Chưa ký',
  processing: 'Đang ký',
  signed: 'Đã ký',
  skipped: 'Bỏ qua',
  failed: 'Ký lỗi'
};
