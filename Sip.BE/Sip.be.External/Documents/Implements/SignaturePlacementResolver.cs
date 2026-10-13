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
        public PdfRectDto Resolve(PdfRectDto pageBox, PdfSignPlacementDto? placement, double width, double height)
        {
            if (pageBox.Width <= 0 || pageBox.Height <= 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                    "Không đọc được khổ trang PDF nên không tính được vị trí đặt chữ ký.");
            }
            if (width <= 0 || height <= 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                    "Kích thước khối chữ ký không hợp lệ (bề rộng hoặc chiều cao bằng 0).");
            }

            width = Math.Min(width, pageBox.Width);
            height = Math.Min(height, pageBox.Height);

            double left, bottom;
            if (placement == null)
            {
                var scale = Math.Min(pageBox.Width / SigningConstants.AppearanceReferencePageWidth,
                    pageBox.Height / SigningConstants.AppearanceReferencePageHeight);
                var margin = SigningConstants.AppearancePageMargin * scale;
                left = pageBox.Right - width - margin;
                bottom = pageBox.Top - height - margin;
            }
            else
            {
                var anchorRight = placement.XRatio + placement.WidthRatio / 2 > 0.5;
                left = anchorRight
                    ? pageBox.X + (placement.XRatio + placement.WidthRatio) * pageBox.Width - width
                    : pageBox.X + placement.XRatio * pageBox.Width;
                bottom = pageBox.Top - placement.YRatio * pageBox.Height - height;
            }

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
