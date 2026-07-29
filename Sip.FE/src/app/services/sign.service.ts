import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import {
  IPdfFormatProgress,
  ISignCert,
  ISignPositionCheck,
  ISignPositionCheckRequest,
  ISignPreview,
  ISignProgress,
  ISignRequest,
  ITokenVerify
} from '../models/sign.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class SignService {
  private http = inject(HttpClient);
  private desktop = inject(DesktopService);
  private get base() {
    return this.desktop.apiBase;
  }

  /**
   * Lấy danh sách chứng thư số trên máy (cert cá nhân, cert cấp máy, cert trên USB token).
   * Mặc định trả HẾT kèm cờ canSign + reason để người dùng biết vì sao một cert không chọn được.
   */
  getCertificates(onlySignable = false): Observable<IBaseResponseWithData<ISignCert[]>> {
    return this.http.get<IBaseResponseWithData<ISignCert[]>>(
      `${this.base}/api/core/document/sign/chung-thu-so`,
      { params: new HttpParams().set('onlySignable', onlySignable) }
    );
  }

  /**
   * Kiểm tra chứng thư số trước khi ký cả lô. Đây cũng là lúc hộp thoại nhập PIN của token bật lên;
   * kết quả + handle khóa được BE cache cho lô ký ngay sau đó.
   */
  verifyToken(thumbprint: string): Observable<IBaseResponseWithData<ITokenVerify>> {
    return this.http.post<IBaseResponseWithData<ITokenVerify>>(
      `${this.base}/api/core/document/sign/kiem-tra-token`,
      { thumbprint }
    );
  }

  /**
   * Điều hướng xem trước PDF ở bước cấu hình chữ ký: lấy file thứ `index` (đếm từ 0) trong nguồn.
   * files rỗng = index ngoài danh sách (bấm quá đầu/cuối) - KHÔNG phải lỗi; nút trước/sau khoá theo
   * hasPrev/hasNext nên bình thường không chạm tới trạng thái đó.
   */
  getPreview(sourcePath: string, index: number): Observable<IBaseResponseWithData<ISignPreview>> {
    return this.http.get<IBaseResponseWithData<ISignPreview>>(
      `${this.base}/api/core/document/sign/xem-truoc`,
      { params: new HttpParams().set('sourcePath', sourcePath).set('index', index) }
    );
  }

  /**
   * Kiểm vị trí chữ ký trên mọi file trong nguồn TRƯỚC khi ký: khối có tràn ra ngoài trang, có nằm ở trang
   * file không có, có nhỏ quá không. Chỉ báo cáo - BE vẫn ký được và tự chỉnh, nên kết quả này không chặn.
   */
  checkPositions(request: ISignPositionCheckRequest): Observable<IBaseResponseWithData<ISignPositionCheck>> {
    return this.http.post<IBaseResponseWithData<ISignPositionCheck>>(
      `${this.base}/api/core/document/sign/kiem-tra-vi-tri`,
      request
    );
  }

  /**
   * URL đọc NỘI DUNG 1 file PDF để pdf.js render. Trả byte thô (không bọc ApiResponse) nên dùng thẳng làm
   * nguồn cho pdf.js, không đi qua HttpClient.
   */
  pdfFileUrl(fullPath: string): string {
    return `${this.base}/api/core/document/sign/xem-file?path=${encodeURIComponent(fullPath)}`;
  }

  /**
   * Bước GATE trước khi ký: kiểm định dạng (PDF/A hai lớp) mọi file trong nguồn, stream realtime qua SSE.
   * EventSource chỉ hỗ trợ GET nên dùng fetch + ReadableStream đọc từng khối `data:` (tách trên "\n\n");
   * huỷ subscription -> AbortController.abort() để dừng.
   */
  checkPdfFormat(sourcePath: string): Observable<IPdfFormatProgress> {
    return this.streamSse<IPdfFormatProgress>(
      `${this.base}/api/core/document/sign/kiem-tra-dinh-dang`,
      { sourcePath }
    );
  }

  /**
   * Ký cả thư mục, stream realtime qua SSE. Mỗi file ký xong đẩy 1 event ISignProgress để FE tick dòng;
   * event cuối là tổng kết. Cùng khuôn SSE với checkPdfFormat.
   */
  signFolder(req: ISignRequest): Observable<ISignProgress> {
    return this.streamSse<ISignProgress>(
      `${this.base}/api/core/document/sign/ky-thu-muc`,
      req
    );
  }

  /**
   * Khung đọc SSE POST dùng chung cho 2 luồng kiểm định dạng và ký (giống CheckService.checkFolder):
   * fetch + ReadableStream, tách event trên "\n\n", parse phần sau "data:".
   */
  private streamSse<T>(url: string, body: unknown): Observable<T> {
    return new Observable<T>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
            signal: controller.signal
          });

          if (!res.ok || !res.body) {
            subscriber.error(new Error(`HTTP ${res.status}`));
            return;
          }

          const reader = res.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });

            let idx: number;
            while ((idx = buffer.indexOf('\n\n')) >= 0) {
              const chunk = buffer.slice(0, idx).trim();
              buffer = buffer.slice(idx + 2);
              if (chunk.startsWith('data:')) {
                const json = chunk.slice(5).trim();
                if (json) {
                  subscriber.next(JSON.parse(json) as T);
                }
              }
            }
          }

          subscriber.complete();
        } catch (err) {
          // Bỏ qua lỗi do chủ động huỷ (abort)
          if (!controller.signal.aborted) {
            subscriber.error(err);
          }
        }
      })();

      return () => controller.abort();
    });
  }
}
