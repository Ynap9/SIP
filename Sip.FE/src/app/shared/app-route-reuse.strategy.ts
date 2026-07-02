import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

/**
 * Giữ nguyên trạng thái các trang được đánh dấu `data: { reuse: true }` khi rời đi và quay lại:
 * component instance được detach (không huỷ) rồi attach lại -> giữ nguyên signal/data/subscription/scroll.
 * Khoá theo đường dẫn đầy đủ (pathFromRoot) để không trùng giữa các route con path rỗng (Config/BocTach...).
 */
export class AppRouteReuseStrategy implements RouteReuseStrategy {
  private handles = new Map<string, DetachedRouteHandle>();

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    return route.routeConfig?.data?.['reuse'] === true;
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    const key = this.getKey(route);
    if (!key) return;
    if (handle) {
      this.handles.set(key, handle);
    } else {
      this.handles.delete(key);
    }
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    return this.handles.has(this.getKey(route));
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    return this.handles.get(this.getKey(route)) ?? null;
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === curr.routeConfig;
  }

  private getKey(route: ActivatedRouteSnapshot): string {
    return route.pathFromRoot
      .map((r) => r.routeConfig?.path ?? '')
      .filter((p) => p)
      .join('/');
  }
}
