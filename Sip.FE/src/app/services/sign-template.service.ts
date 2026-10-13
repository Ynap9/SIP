import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  IBaseRequestPaging,
  IBaseResponse,
  IBaseResponsePaging,
  IBaseResponseWithData
} from '../shared/models/request-paging.base.models';
import {
  IAddSignTemplate,
  IUpdateSignTemplate,
  IViewSignTemplate
} from '../models/sign-template.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class SignTemplateService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return `${this.desktop.apiBase}/api/core/document/template-chu-ky`;
  }

  findPaging(request: IBaseRequestPaging): Observable<IBaseResponsePaging<IViewSignTemplate>> {
    let params = new HttpParams().set('pageNumber', request.pageNumber).set('pageSize', request.pageSize);
    if (request.keyword) {
      params = params.set('keyword', request.keyword);
    }
    return this.http.get<IBaseResponsePaging<IViewSignTemplate>>(`${this.base}/find-paging`, { params });
  }

  getById(id: number): Observable<IBaseResponseWithData<IViewSignTemplate>> {
    return this.http.get<IBaseResponseWithData<IViewSignTemplate>>(`${this.base}/${id}`);
  }

  create(body: IAddSignTemplate): Observable<IBaseResponseWithData<IViewSignTemplate>> {
    return this.http.post<IBaseResponseWithData<IViewSignTemplate>>(this.base, body);
  }

  update(body: IUpdateSignTemplate): Observable<IBaseResponseWithData<IViewSignTemplate>> {
    return this.http.put<IBaseResponseWithData<IViewSignTemplate>>(this.base, body);
  }

  delete(id: number): Observable<IBaseResponse> {
    return this.http.delete<IBaseResponse>(`${this.base}/${id}`);
  }

  imageUrl(path: string): string {
    return `${this.base}/xem-anh?path=${encodeURIComponent(path)}`;
  }
}
