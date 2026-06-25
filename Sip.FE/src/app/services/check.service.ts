import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ICheckProgress } from '../models/check.models';
import { DesktopService } from './desktop.service';

@Injectable({ providedIn: 'root' })
export class CheckService {
  private desktop = inject(DesktopService);
  private get base() {
    return this.desktop.apiBase;
  }

  /**
   * Gọi endpoint kiểm tra hồ sơ qua SSE (POST text/event-stream).
   * EventSource chỉ hỗ trợ GET nên dùng fetch + ReadableStream để đọc từng dòng `data:`.
   * Trả về Observable phát mỗi ICheckProgress; huỷ subscription -> AbortController.abort() để dừng.
   */
  checkFolder(folderPath: string): Observable<ICheckProgress> {
    return new Observable<ICheckProgress>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const res = await fetch(
            `${this.base}/api/core/document/check/ho-so-folder`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ folderPath }),
              signal: controller.signal
            }
          );

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
                  subscriber.next(JSON.parse(json) as ICheckProgress);
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
