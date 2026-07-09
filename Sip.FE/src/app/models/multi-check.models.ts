// DTO khớp MultiCheckProgressDto (BE) — mỗi event SSE của luồng kiểm tra NHIỀU hồ sơ.
import { ISignatureInfo } from './check.models';

export type { ISignatureInfo, ISignerInfo, ICollectUnsignedResult } from './check.models';

// Kết quả kiểm tra 1 file trong hồ sơ (khớp FileCheckResultDto).
export interface IFileCheckResult {
  name: string;
  /** 1 = ok, 0 = error */
  status: number;
  reason?: string;
  /** Thông tin ký số của file. */
  signature?: ISignatureInfo;
}

export interface IMultiCheckProgress {
  phase: number;
  phaseName: string;
  /** parent | hoso | duplicate | summary | manifest */
  targetType: string;
  hoSoName: string;
  /** 1 = Success, 0 = Error */
  status: number;
  code: number;
  message: string;
  errors?: string[];
  /** Chỉ có ở event 1 hồ sơ (Phase 2): kết quả từng file. */
  files?: IFileCheckResult[];
  /** Chỉ có ở event manifest: tên tất cả hồ sơ con để seed sẵn các dòng. */
  hoSoNames?: string[];
  fileCount: number;
  totalHoSo: number;
  completedHoSo: number;
  okCount: number;
  errorCount: number;
  percent: number;
  completed: boolean;
}

// 1 dòng file trong bảng con.
export interface IFileRow {
  name: string;
  status: 'processing' | 'ok' | 'error';
  reason?: string;
  /** Thông tin ký số của file (để hiển thị cột "Ký số"). */
  signature?: ISignatureInfo;
}

// 1 hồ sơ con kèm danh sách tên file chưa ký số (gửi BE để gom).
export interface ICollectUnsignedItem {
  hoSoName: string;
  fileNames: string[];
}

// 1 dòng hồ sơ (cha) trong bảng; `status` vừa suy ra Trạng thái (đã xử lý) vừa Tình trạng (đạt/lỗi).
export interface IHoSoRow {
  name: string;
  status: 'processing' | 'ok' | 'error';
  reason?: string;
  files: IFileRow[];
}
