namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Kết quả quét chứng thư số từ Windows certificate store: danh sách cert đọc được kèm nhật ký
    /// từng store đã quét. Phần nhật ký tồn tại vì lỗi hay gặp nhất khi triển khai là "không thấy
    /// chứng thư nào" trên máy người dùng — cần biết store nào rỗng, store nào mở không được, chứ
    /// một danh sách rỗng thì không nói lên điều gì để chẩn đoán từ xa.
    /// </summary>
    public class CertScanResultDto
    {
        /// <summary>Cert đọc được từ mọi store, đã gộp trùng theo thumbprint.</summary>
        public List<SignCertDto> Certificates { get; set; } = [];

        /// <summary>Mỗi phần tử mô tả một store: số cert đọc được, hoặc lý do không mở được.</summary>
        public List<string> StoreDiagnostics { get; set; } = [];
    }
}
