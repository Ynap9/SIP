import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { IMultiPackProgress, IMultiPackRequest } from '../models/multi-pack.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class MultiPackService {
  private desktop = inject(DesktopService);
  private get base() {
    return this.desktop.apiBase;
  }

  /**
   * Đóng gói NHIỀU hồ sơ (1 thư mục cha) qua SSE (POST text/event-stream).
   * Dùng fetch + ReadableStream để đọc từng dòng `data:`; huỷ subscription -> AbortController.abort().
   */
  packMulti(req: IMultiPackRequest): Observable<IMultiPackProgress> {
    return new Observable<IMultiPackProgress>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const res = await fetch(`${this.base}/api/core/document/pack/multi-folder`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(req),
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
                  subscriber.next(JSON.parse(json) as IMultiPackProgress);
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
