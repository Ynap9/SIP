import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { IMultiCheckProgress, ICollectUnsignedItem, ICollectUnsignedResult } from '../models/multi-check.models';
import { IBaseResponseWithData } from '../shared/models/request-paging.base.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class MultiCheckService {
  private desktop = inject(DesktopService);
  private http = inject(HttpClient);
  private get base() {
    return this.desktop.apiBase;
  }

  /**
   * Gom file CHƯA KÝ SỐ của NHIỀU hồ sơ vào thư mục "chưa ký số_{date}" (mỗi hồ sơ 1 thư mục con).
   * Trả về đường dẫn thư mục cha để FE mở cho người dùng xem.
   */
  collectUnsignedMulti(
    parentFolderPath: string,
    items: ICollectUnsignedItem[]
  ): Observable<IBaseResponseWithData<ICollectUnsignedResult>> {
    return this.http.post<IBaseResponseWithData<ICollectUnsignedResult>>(
      `${this.base}/api/core/document/check/collect-unsigned-multi`,
      { parentFolderPath, items }
    );
  }

  /**
   * Kiểm tra NHIỀU hồ sơ (1 thư mục cha) qua SSE (POST text/event-stream).
   * Dùng fetch + ReadableStream để đọc từng dòng `data:`; huỷ subscription -> AbortController.abort().
   */
  checkMulti(parentFolderPath: string): Observable<IMultiCheckProgress> {
    return new Observable<IMultiCheckProgress>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const res = await fetch(`${this.base}/api/core/document/check/multi-folder`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ parentFolderPath }),
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
                  subscriber.next(JSON.parse(json) as IMultiCheckProgress);
                }
              }
            }
          }

          subscriber.complete();
        } catch (err) {
          if (!controller.signal.aborted) {
            subscriber.error(err);
          }
        }
      })();

      return () => controller.abort();
    });
  }
}
