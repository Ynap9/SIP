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
  protected readonly features = [
    {
      icon: 'pi pi-box',
      title: 'Kiểm tra & đóng gói',
      desc: 'Đưa thư mục hồ sơ vào, kiểm tra hợp lệ theo TT05 rồi đóng gói thành gói SIP (.zip).',
      link: '/dong-goi'
    },
    {
      icon: 'pi pi-cog',
      title: 'Cấu hình danh mục',
      desc: 'Nhập cấu hình mô tả trường hồ sơ / văn bản từ Excel để hệ thống đối chiếu khi kiểm tra.',
      link: '/cau-hinh'
    },
    {
      icon: 'pi pi-verified',
      title: 'Chuẩn TT05 / E-ARK',
      desc: 'Sinh METS.xml, EAD và cấu trúc gói đúng Thông tư 05/2025/TT-BNV, mở rộng từ E-ARK CSIP.',
      link: '/dong-goi'
    }
  ];

  protected readonly steps = [
    { no: 1, title: 'Đưa thư mục hồ sơ', desc: 'Chọn thư mục chứa metadata + tài liệu đính kèm.' },
    { no: 2, title: 'Kiểm tra hợp lệ', desc: 'Đối chiếu cấu trúc, mã và nội dung theo TT05.' },
    { no: 3, title: 'Đóng gói SIP', desc: 'Sinh gói SIP_tailieu và nén thành file ZIP.' }
  ];
}
