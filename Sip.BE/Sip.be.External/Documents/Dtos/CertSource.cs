namespace Sip.be.External.Documents.Dtos
{
    /// <summary>Nguồn chứa chứng thư số.</summary>
    public enum CertSource
    {
        /// <summary>Store cá nhân của user hiện tại (CurrentUser\My) — khóa nằm trong phần mềm.</summary>
        Local = 0,

        /// <summary>Store cấp máy (LocalMachine\My) — cert cài cho máy/dịch vụ, dùng chung mọi user.</summary>
        Server = 1,

        /// <summary>USB token/smartcard — cert nằm trong store nhưng private key ở provider PHẦN CỨNG.</summary>
        UsbToken = 2,
    }
}
