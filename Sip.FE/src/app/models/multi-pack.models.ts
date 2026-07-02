// DTO khớp BE: PackMultiFolderRequestDto / MultiPackProgressDto (api/core/document/pack/multi-folder)

export interface IMultiPackRequest {
  parentFolderPath: string;
  outputPath: string;
  maPhong?: string;
  nguoiTao?: string;
  maxDegreeOfParallelism?: number;
  /** Allowlist tên hồ sơ được phép đóng gói (thường là các hồ sơ đã pass kiểm tra). */
  hoSoNames?: string[];
}

export interface IMultiPackProgress {
  /** hoso | summary | parent */
  targetType: string;
  hoSoName: string;
  /** 1 = Success, 0 = Error */
  status: number;
  code: number;
  message: string;

  objid?: string;
  fileCode?: string;
  zipPath?: string;
  totalDoc: number;
  warnings?: string[];
  error?: string;

  /** Điền ở event tổng kết. */
  outputParentPath?: string;
  /** Danh sách hồ sơ đóng gói lỗi (event tổng kết). */
  failedHoSo?: string[];

  totalHoSo: number;
  completedHoSo: number;
  okCount: number;
  errorCount: number;
  percent: number;
  completed: boolean;
}
