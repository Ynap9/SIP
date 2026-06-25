import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { IPackRequest, IPackResult } from '../models/pack.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class PackService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);

  /** Đóng gói 1 thư mục hồ sơ (đã qua kiểm tra) thành gói SIP_tailieu. */
  pack(req: IPackRequest): Observable<IBaseResponseWithData<IPackResult>> {
    return this.http.post<IBaseResponseWithData<IPackResult>>(
      `${this.desktop.apiBase}/api/core/document/pack/ho-so-folder`,
      req
    );
  }
}
