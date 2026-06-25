using Sip.be.Application.Document.Dtos;
using Sip.be.Application.Document.Dtos.Config;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sip.be.Application.Document.Interfaces
{
    public interface IConfigService
    {
        /// <summary>
        /// Đọc file Excel, tìm sheet "Mô tả_Ho_so", map các cột theo header và import vào bảng ConfigHoSo.
        /// Trả về số dòng đã import.
        /// </summary>
        Task<int> ImportConfigHoSoAsync(AddConfigHoSoDto input);

        /// <summary>
        /// Đọc file Excel, tìm sheet "Mô tả_Van_ban", map các cột theo header và import vào bảng ConfigVanBan.
        /// Trả về số dòng đã import.
        /// </summary>
        Task<int> ImportConfigVanBanAsync(AddConfigVanBanDto input);

        /// <summary>
        /// Lấy danh sách ConfigHoSo (chưa bị xoá mềm).
        /// </summary>
        List<ViewConfigHoSoDto> GetConfigHoSo();

        /// <summary>
        /// Lấy danh sách ConfigVanBan (chưa bị xoá mềm).
        /// </summary>
        List<ViewConfigVanBanDto> GetConfigVanBan();
    }
}
