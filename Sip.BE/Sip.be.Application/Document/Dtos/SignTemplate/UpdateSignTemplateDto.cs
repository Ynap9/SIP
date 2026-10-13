namespace Sip.be.Application.Document.Dtos.SignTemplate
{
    /// <summary>
    /// Tham số sửa một template đã có. Toàn bộ nội dung được GHI ĐÈ theo payload này, kể cả danh sách toạ độ,
    /// chứ không vá từng phần: màn cấu hình luôn gửi lên trạng thái đầy đủ đang hiển thị.
    ///
    /// Về hai trường ảnh (kế thừa từ <see cref="AddSignTemplateDto"/>): để NULL nghĩa là BỎ ảnh khỏi template.
    /// Muốn giữ ảnh cũ thì gửi lại đúng đường dẫn trong %AppData% mà GET đã trả về - service nhận ra đó là file
    /// của chính template này nên giữ nguyên, không copy chồng lên chính nó.
    /// </summary>
    public class UpdateSignTemplateDto : AddSignTemplateDto
    {
        public int Id { get; set; }
    }
}
