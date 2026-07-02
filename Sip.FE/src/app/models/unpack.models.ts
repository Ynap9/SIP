// DTO khớp BE: UnpackRequestDto / UnpackResultDto / FileNodeDto (api/core/document/unpack/ho-so)

// 1 nút trong cây thư mục kết quả (folder hoặc file), đều có đường dẫn tuyệt đối để mở.
export interface IFileNode {
  name: string;
  path: string;
  /** 'folder' | 'file' */
  type: string;
  children?: IFileNode[];
}

export interface IUnpackRequest {
  sourcePath: string;
  outputPath: string;
}

export interface IUnpackResult {
  hoSoName: string;
  folderPath: string;
  totalDoc: number;
  tree?: IFileNode;
  warnings: string[];
  message: string;
}
