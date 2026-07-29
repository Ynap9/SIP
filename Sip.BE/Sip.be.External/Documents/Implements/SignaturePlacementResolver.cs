using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;

namespace Sip.be.External.Documents.Implements
{
    /// <inheritdoc/>
    public class SignaturePlacementResolver : ISignaturePlacementResolver
    {
        /// <inheritdoc/>
        public PdfRectDto Resolve(PdfRectDto pageBox, PdfSignPlacementDto? placement)
        {
            if (pageBox.Width <= 0 || pageBox.Height <= 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                    "Không đọc được khổ trang PDF nên không tính được vị trí đặt chữ ký.");
            }

            double width, height, left, bottom;
            if (placement == null)
            {
                // ===== Vị trí mặc định =====
                // Khổ 170x30pt đo từ file production, CO GIÃN theo khổ trang thật. Lấy tỉ lệ NHỎ HƠN giữa hai
                // chiều để khối giữ nguyên hình dáng và không vượt quá trang theo chiều nào: trang A3 chữ ký
                // to lên tương ứng, trang nhỏ hơn A4 chữ ký nhỏ lại thay vì tràn ra ngoài.
                var scale = Math.Min(pageBox.Width / SigningConstants.AppearanceReferencePageWidth,
                    pageBox.Height / SigningConstants.AppearanceReferencePageHeight);
                width = SigningConstants.AppearanceWidth * scale;
                height = SigningConstants.AppearanceHeight * scale;

                // Góc TRÊN-PHẢI, chừa lề — đúng chỗ mà bản ký trước vẫn đặt.
                var margin = SigningConstants.AppearancePageMargin * scale;
                left = pageBox.Right - width - margin;
                bottom = pageBox.Top - height - margin;
            }
            else
            {
                // ===== Vị trí người dùng chọn =====
                // Tỉ lệ -> point theo khổ trang THẬT. Đây là chỗ một lựa chọn duy nhất của người dùng biến
                // thành các ô khác nhau trên các file khác khổ giấy, và là lý do hợp đồng nhận tỉ lệ chứ
                // không nhận point.
                width = placement.WidthRatio * pageBox.Width;
                height = placement.HeightRatio * pageBox.Height;
                if (width <= 0 || height <= 0)
                {
                    throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                        "Kích thước khối chữ ký đã chọn không hợp lệ (bề rộng hoặc chiều cao bằng 0).");
                }

                // Sàn kích cỡ: giữ NGUYÊN lựa chọn của người dùng, chỉ can thiệp khi khối nhỏ tới mức không
                // đọc được chữ (tỉ lệ chọn trên trang A4 mà áp vào trang khổ nhỏ hơn nhiều). Phóng cả hai
                // chiều bằng CÙNG một hệ số nên khối giữ đúng dáng và chữ vẫn cân trong khung.
                var pageScale = Math.Min(pageBox.Width / SigningConstants.AppearanceReferencePageWidth,
                    pageBox.Height / SigningConstants.AppearanceReferencePageHeight);
                var minHeight = SigningConstants.AppearanceHeight * pageScale * SigningConstants.AppearanceMinScale;
                if (height < minHeight)
                {
                    width *= minHeight / height;
                    height = minHeight;
                }

                left = pageBox.X + placement.XRatio * pageBox.Width;
                // YRatio đo từ mép TRÊN xuống, hệ PDF đo từ mép DƯỚI lên -> lật trục ở đúng một chỗ này.
                bottom = pageBox.Top - placement.YRatio * pageBox.Height - height;
            }

            // Kẹp lại trong trang. Với tỉ lệ hợp lệ (tầng nhận request đã chặn X+W ≤ 1 và Y+H ≤ 1) thì phép
            // kẹp không đổi gì; nó chỉ cứu trang có /MediaBox lệch chuẩn, để không bao giờ ghi ra một ô nằm
            // ngoài trang — chữ ký nằm ngoài trang là trình đọc không hiện gì cả.
            width = Math.Min(width, pageBox.Width);
            height = Math.Min(height, pageBox.Height);

            return new PdfRectDto
            {
                X = Math.Clamp(left, pageBox.X, pageBox.Right - width),
                Y = Math.Clamp(bottom, pageBox.Y, pageBox.Top - height),
                Width = width,
                Height = height
            };
        }
    }
}
