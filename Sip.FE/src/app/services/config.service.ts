import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { IConfigItem } from '../models/config.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);

  getConfigHoSo(): Observable<IBaseResponseWithData<IConfigItem[]>> {
    return this.http.get<IBaseResponseWithData<IConfigItem[]>>(
      `${this.desktop.apiBase}/api/core/document/config/ho-so`
    );
  }

  getConfigVanBan(): Observable<IBaseResponseWithData<IConfigItem[]>> {
    return this.http.get<IBaseResponseWithData<IConfigItem[]>>(
      `${this.desktop.apiBase}/api/core/document/config/van-ban`
    );
  }
}
