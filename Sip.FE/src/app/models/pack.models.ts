// DTO khớp BE: PackHoSoFolderRequestDto / PackResultDto (api/core/document/pack/ho-so-folder)

export interface IPackRequest {
  folderPath: string;
  outputPath?: string;
  schemaSourceDir?: string;
  maPhong?: string;
  nguoiTao?: string;
}

export interface IPackResult {
  objid: string;
  fileCode: string;
  zipPath: string;
  totalDoc: number;
  warnings: string[];
  message: string;
}
