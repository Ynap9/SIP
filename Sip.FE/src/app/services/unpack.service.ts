import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { IUnpackRequest, IUnpackResult } from '../models/unpack.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class UnpackService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);

  /** Bóc tách 1 gói SIP (.zip hoặc thư mục gói) về thư mục hồ sơ; trả về cây thư mục kết quả. */
  unpack(req: IUnpackRequest): Observable<IBaseResponseWithData<IUnpackResult>> {
    return this.http.post<IBaseResponseWithData<IUnpackResult>>(
      `${this.desktop.apiBase}/api/core/document/unpack/ho-so`,
      req
    );
  }
}
