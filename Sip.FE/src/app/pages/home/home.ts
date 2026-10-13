import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SharedImports } from '../../shared/import.shared';

@Component({
  selector: 'app-home',
  imports: [...SharedImports, RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  // Đúng 4 chức năng của menu, không thêm không bớt: trang chủ là cửa vào của app nên chức năng nào có trong
  // menu mà thiếu ở đây là người dùng không biết app làm được việc đó.
  protected readonly features = [
    {
      icon: 'pi pi-box',
      title: 'Kiểm tra & đóng gói',
      desc: 'Kiểm tra hồ sơ theo TT05 qua 3 giai đoạn (cấu trúc, mã định danh, nội dung Excel) rồi đóng gói thành gói SIP. Làm được cả một hồ sơ lẻ lẫn thư mục cha chứa nhiều hồ sơ.',
      link: '/dong-goi'
    },
    {
      icon: 'pi pi-verified',
      title: 'Ký số tài liệu',
      desc: 'Ký PDF/A bằng chứng thư số trên máy hoặc USB token, tự chọn vị trí chữ ký trên trang, mỗi file kèm dấu thời gian của Ban Cơ yếu Chính phủ.',
      link: '/ky-so'
    },
    {
      icon: 'pi pi-sitemap',
      title: 'Bóc tách dữ liệu',
      desc: 'Mở ngược một gói SIP (.zip hoặc thư mục) thành thư mục hồ sơ kèm file Excel mô tả, để tra cứu hoặc chỉnh sửa rồi đóng gói lại.',
      link: '/boc-tach'
    },
    {
      icon: 'pi pi-cog',
      title: 'Cấu hình danh mục',
      desc: 'Nhập từ Excel danh mục trường mô tả Hồ sơ / Văn bản. Đây là căn cứ để bước kiểm tra đối chiếu từng ô dữ liệu.',
      link: '/cau-hinh'
    }
  ];

  // Thứ tự đúng như thực tế phải làm: ký số TRƯỚC khi kiểm tra, vì tài liệu PDF chưa có chữ ký số hợp lệ sẽ
  // bị chặn ở bước kiểm tra và không đóng gói được.
  protected readonly steps = [
    { no: 1, title: 'Cấu hình danh mục', desc: 'Nhập Excel mô tả trường Hồ sơ / Văn bản một lần cho cả hệ thống.' },
    { no: 2, title: 'Ký số tài liệu', desc: 'Ký các file PDF/A trong hồ sơ kèm dấu thời gian tin cậy.' },
    { no: 3, title: 'Kiểm tra hồ sơ', desc: 'Đối chiếu cấu trúc thư mục, mã định danh và nội dung theo TT05.' },
    { no: 4, title: 'Đóng gói SIP', desc: 'Sinh METS.xml + EAD.xml và nén thành gói SIP nộp lưu.' }
  ];

  protected readonly standards = [
    { icon: 'pi pi-book', title: 'TT05/2025/TT-BNV', desc: 'Chuẩn gói tin nộp lưu' },
    { icon: 'pi pi-sitemap', title: 'E-ARK CSIP', desc: 'METS + EAD' },
    { icon: 'pi pi-file-pdf', title: 'PDF/A', desc: 'Kiểm định dạng hai lớp' },
    { icon: 'pi pi-shield', title: 'Ban Cơ yếu Chính phủ', desc: 'Chữ ký số + dấu thời gian' }
  ];
}
