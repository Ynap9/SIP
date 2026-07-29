namespace Sip.be.Shared.Constants
{
    /// <summary>
    /// NGUỒN DUY NHẤT về thời gian của app. Toàn bộ BE lấy giờ qua <see cref="VietnamNow"/>,
    /// TUYỆT ĐỐI KHÔNG dùng DateTime.Now/Today: app chạy trên máy người dùng nên Now phụ thuộc
    /// múi giờ máy — máy đặt sai múi giờ là tên thư mục/tên gói/mốc thời gian lệch theo, âm thầm.
    /// Tầng Application kế thừa BaseService thì dùng thẳng property VietnamNow của base.
    /// Xem .claude/be/architecture/08-datetime.md.
    /// </summary>
    public static class DateTimeConstants
    {
        /// <summary>ID múi giờ Việt Nam trên Windows (UTC+7, không có DST).</summary>
        public const string VietnamTimeZoneId = "SE Asia Standard Time";

        /// <summary>ID múi giờ Việt Nam theo IANA — dùng khi chạy nền tảng không có ID Windows.</summary>
        public const string VietnamTimeZoneIanaId = "Asia/Ho_Chi_Minh";

        /// <summary>Múi giờ Việt Nam. Không tra được theo ID nào (hệ thống thiếu tz database) thì lùi về UTC+7 cố định.</summary>
        public static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

        /// <summary>
        /// Giờ Việt Nam hiện tại (UTC+7). Luôn quy từ UTC nên không phụ thuộc múi giờ của máy chạy app.
        /// </summary>
        public static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

        // Bất khả kháng phải tách: field static readonly ở trên cần biểu thức có try/catch.
        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId); }
            catch { }
            try { return TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneIanaId); }
            catch { }
            return TimeZoneInfo.CreateCustomTimeZone(VietnamTimeZoneId, TimeSpan.FromHours(7), "Vietnam", "Vietnam");
        }
    }
}
