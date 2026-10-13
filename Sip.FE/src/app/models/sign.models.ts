// Model cho luồng KÝ SỐ (khớp DTO BE Sip.be.Application/Document/Dtos/Sign).
// Enum + hằng số hiển thị nằm ở shared/constants/sign.constants.ts.

import { ECertSource } from '../shared/constants/sign.constants';
// Chi tiết từng chữ ký dùng chung với luồng KIỂM TRA đóng gói - cùng một SignatureDetailDto phía BE.
import { ISignatureDetail } from './check.models';

// Một chứng thư số đọc được trên máy (khớp SignCertDto BE).
export interface ISignCert {
  subject: string;
  /** CN của người/tổ chức sở hữu - dùng để hiển thị. */
  commonName: string;
  issuer: string;
  /** CN của CA cấp cert (vd "CA phục vụ các cơ quan Nhà nước G2"). */
  issuerCommonName: string;
  /** Serial (hex) - THAM SỐ CHỌN CERT khi gọi API ký. */
  serialNumber: string;
  /** Thumbprint SHA-1 - định danh duy nhất, dùng làm value của dropdown. */
  thumbprint: string;
  source: ECertSource;
  /** Tên CSP/KSP giữ private key - dùng để chẩn đoán. */
  keyProvider?: string | null;
  /** "dd/MM/yyyy HH:mm:ss". */
  validFrom: string;
  /** "dd/MM/yyyy HH:mm:ss". */
  validTo: string;
  hasPrivateKey: boolean;
  isExpired: boolean;
  allowsSigning: boolean;
  /** Chain về Root G1/G2 đã ghim của Ban Cơ yếu - đúng phép thử mà bước ký áp dụng. */
  isTrusted: boolean;
  /** = hasPrivateKey && !isExpired && allowsSigning && isTrusted. */
  canSign: boolean;
  /** Lý do KHÔNG ký được; null/rỗng nếu ký được. */
  reason?: string | null;
}

// Kết quả KIỂM TRA CHỨNG THƯ SỐ trước khi ký (khớp TokenVerifyDto BE). Hộp nhập PIN của token bật lên
// trong lúc gọi API này; valid=true nghĩa là mọi bước kiểm đều đạt và có thể sang bước ký.
export interface ITokenVerify {
  valid: boolean;
  thumbprint: string;
  commonName?: string | null;
  issuerCommonName?: string | null;
  foundInStore: boolean;
  hasPrivateKey: boolean;
  notExpired: boolean;
  allowsSigning: boolean;
  canSignTest: boolean;
  trustedChain: boolean;
  /** Tên bước hỏng đầu tiên (rỗng nếu hợp lệ). */
  failedCheck?: string | null;
  /** Mô tả lý do hỏng (rỗng nếu hợp lệ). */
  reason?: string | null;
}

// ===== Bước GATE: kiểm định dạng PDF/A hai lớp trước khi ký (SSE) =====

// Kết quả kiểm định dạng của 1 file (khớp PdfFormatFileResultDto BE).
export interface IPdfFormatFileResult {
  fileName: string;
  fullPath: string;
  /** EPdfFormatVerdict (số) - LUÔN là định dạng thật của file. */
  verdict: number;
  canSign: boolean;
  message: string;
  pdfAPart?: string | null;
  pdfAConformance?: string | null;
}

// Tổng kết cả lô kiểm định dạng (khớp PdfFormatCheckSummaryDto BE).
export interface IPdfFormatSummary {
  totalFiles: number;
  validCount: number;
  invalidCount: number;
  /** True khi mọi file đều ký được -> mở khoá bước ký. */
  allValid: boolean;
}

// 1 event SSE của quá trình kiểm định dạng (khớp PdfFormatCheckProgressDto BE).
export interface IPdfFormatProgress {
  /** manifest | file | summary */
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
  /** Chỉ có ở event manifest: seed sẵn danh sách file. */
  files?: string[];
  /** Chỉ có ở event file. */
  file?: IPdfFormatFileResult;
  /** Chỉ có ở event summary. */
  summary?: IPdfFormatSummary;
}

// ===== Bước KÝ (SSE) =====

// Kết quả ký của 1 file (khớp SignFileResultDto BE). 3 trạng thái phân biệt:
// Đã ký (success=true) / Bỏ qua (success=false, skipped=true) / Lỗi ký (success=false, skipped=false).
export interface ISignFileResult {
  fileName: string;
  outputPath?: string | null;
  success: boolean;
  /** true = file bị BỎ QUA vì không đạt PDF/A hai lớp (không ký, KHÔNG phải lỗi). Phân biệt skipped vs failed. */
  skipped: boolean;
  reason?: string | null;
  /** ETimestampVerdict (số) - vòng tự nghiệm thu sau khi ký. */
  verdict: number;
  verifyReason?: string | null;
  /** Dấu thời gian TSA của chữ ký MỚI NHẤT, "dd/MM/yyyy HH:mm:ss"; null nếu chưa ký. */
  trustedTime?: string | null;
  /** Giờ ký tự khai của chữ ký MỚI NHẤT, "dd/MM/yyyy HH:mm:ss"; null nếu chưa ký. */
  signedTime?: string | null;
  /** Tổng số chữ ký trong file sau khi ký (file có thể đã có chữ ký từ trước). */
  signatureCount?: number;
  /** Số chữ ký ĐẠT trong file. */
  validSignatureCount?: number;
  /** Giờ ký + dấu thời gian của TỪNG chữ ký, xếp theo thứ tự ký (mới nhất ở cuối). */
  signatures?: ISignatureDetail[];
}

// Tổng kết cả lô ký (khớp SignResultDto BE). successCount + skippedCount + failedCount = totalFiles.
export interface ISignResult {
  outputFolder: string;
  totalFiles: number;
  /** Số file ĐÃ KÝ. */
  successCount: number;
  /** Số file BỎ QUA (không đạt định dạng). */
  skippedCount: number;
  /** Số file LỖI khi ký (file khoá, rút token…). */
  failedCount: number;
}

// 1 event SSE của quá trình ký (khớp SignProgressDto BE).
export interface ISignProgress {
  /** manifest | file | summary */
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
  /** Chỉ có ở event manifest. */
  files?: string[];
  /** Chỉ có ở event file. */
  file?: ISignFileResult;
  /** Chỉ có ở event summary. */
  summary?: ISignResult;
}

// ===== Bước 2: cấu hình chữ ký =====

// 1 file PDF trong nguồn (khớp SignFileItemDto BE).
export interface ISignFileItem {
  name: string;
  fullPath: string;
  sizeBytes: number;
  /** File đã có chữ ký số từ trước. */
  alreadySigned: boolean;
}

// 1 bước điều hướng xem trước (khớp SignPreviewDto BE).
// files RỖNG = index ngoài danh sách (bấm quá đầu/cuối) - KHÔNG phải lỗi, FE giữ nguyên file đang xem.
export interface ISignPreview {
  index: number;
  total: number;
  /** Khoá nút "File trước" khi false. */
  hasPrev: boolean;
  /** Khoá nút "File sau" khi false. */
  hasNext: boolean;
  files: ISignFileItem[];
}

// Vị trí khối chữ ký người dùng chọn (khớp SignPositionDto BE).
// Tất cả là TỈ LỆ 0..1 so với khổ trang, gốc góc TRÊN-TRÁI, Y hướng xuống: người dùng chọn 1 lần rồi áp cho
// mọi file trong thư mục, mà các file khác khổ giấy nhau nên không thể gửi pixel hay point tuyệt đối.
// Không có số thứ tự khối: người dùng kéo thả bao nhiêu khối thì gửi bấy nhiêu phần tử trong positions.
export interface ISignPosition {
  /** Trang được đóng khối này (tính từ 1) - đúng trang người dùng đã thả khối vào. */
  pageNumber: number;
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
  kind?: number;
}

// Cấu hình riêng cho 1 file, ghi đè positions chung (khớp SignFileConfigDto BE).
export interface ISignFileConfig {
  /** Đường dẫn tuyệt đối của file PDF. */
  path: string;
  positions: ISignPosition[];
}

// Cấu hình chữ ký chốt ở bước 2, áp cho MỌI file trong lô ký.
export interface ISignConfig {
  /** false = để hệ thống tự đặt (góc trên-phải, vẫn co giãn theo khổ trang). */
  useCustomPosition: boolean;
  /** Cho phép chữ ký đè lên nội dung trang. */
  allowOverlapContent: boolean;
  /** Các khối chữ ký CHUNG; chỉ có khi useCustomPosition = true. Không giới hạn số lượng. */
  positions?: ISignPosition[] | null;
  /** Các file người dùng cấu hình riêng; file không có ở đây dùng positions chung. */
  fileOverrides?: ISignFileConfig[] | null;
  fontFamily?: string | null;
  fontSize?: number | null;
  textColor?: string | null;
  sealImagePath?: string | null;
  wetSignatureImagePath?: string | null;
}

// Yêu cầu kiểm vị trí chữ ký trước khi ký (khớp SignPositionCheckRequestDto BE) - không cần chứng thư số.
export interface ISignPositionCheckRequest {
  sourcePath: string;
  useCustomPosition: boolean;
  positions?: ISignPosition[] | null;
  fileOverrides?: ISignFileConfig[] | null;
}

// Kết quả kiểm vị trí của 1 file (khớp SignPositionFileDto BE).
// fitted = false là "sẽ bị chỉnh cho vừa trang", KHÔNG phải file bị chặn ký.
export interface ISignPositionFile {
  fileName: string;
  fullPath: string;
  pageCount: number;
  blockCount: number;
  readable: boolean;
  fitted: boolean;
  issues: string[];
}

// Tổng kết kiểm vị trí cả lô (khớp SignPositionCheckResultDto BE).
export interface ISignPositionCheck {
  totalFiles: number;
  fittedCount: number;
  adjustedCount: number;
  unreadableCount: number;
  files: ISignPositionFile[];
}

// Yêu cầu ký số (khớp SignRequestDto BE). MỘT request cho cả lô, kể cả khi mỗi file một cấu hình.
export interface ISignRequest {
  thumbprint: string;
  sourcePath: string;
  reason?: string;
  location?: string;
  useCustomPosition: boolean;
  allowOverlapContent: boolean;
  positions?: ISignPosition[] | null;
  fileOverrides?: ISignFileConfig[] | null;
  fontFamily?: string | null;
  fontSize?: number | null;
  textColor?: string | null;
  sealImagePath?: string | null;
  wetSignatureImagePath?: string | null;
}

// 1 dòng file hiển thị trên bảng ký số: gộp kết quả 2 bước (kiểm định dạng + ký) trên cùng 1 dòng.
export interface ISignFileRow {
  name: string;
  /** Trạng thái bước kiểm định dạng. */
  checkStatus: 'processing' | 'ok' | 'error';
  /** EPdfFormatVerdict (số) sau khi kiểm xong - định dạng THẬT của file. */
  verdict?: number;
  /** File này có ký được không: quyết định màu badge, vì verdict != Valid vẫn có thể ký được. */
  canSign?: boolean;
  /** Message nguyên văn của BE (đã gồm cả câu "vẫn ký được do đã bật tuỳ chọn..."). */
  checkMessage?: string;
  /** Trạng thái bước ký (trống ở bước kiểm). */
  signStatus: 'idle' | 'processing' | 'signed' | 'skipped' | 'failed';
  signMessage?: string;
  /** Giờ ký tự khai của chữ ký mới nhất (signedTime). */
  signedTime?: string | null;
  /** Dấu thời gian TSA của chữ ký mới nhất (trustedTime). */
  trustedTime?: string | null;
  /** Tổng số chữ ký trong file. */
  signatureCount?: number;
  /** Số chữ ký ĐẠT. */
  validSignatureCount?: number;
  /** Giờ của TỪNG chữ ký, xếp theo thứ tự ký (mới nhất ở cuối). */
  signatures?: ISignatureDetail[];
}
