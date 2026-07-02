import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

/**
 * Giữ nguyên trạng thái các trang được đánh dấu `data: { reuse: true }` khi rời đi và quay lại:
 * component instance được detach (không huỷ) rồi attach lại -> giữ nguyên signal/data/subscription/scroll.
 *
 * QUAN TRỌNG: mọi thao tác phải GATE theo cờ `reuse`. Route con leaf (path rỗng dưới lazy loadChildren)
 * có cùng khoá pathFromRoot với route cha lazy (vd cả 'boc-tach' lazy lẫn '' leaf đều ra 'boc-tach');
 * nếu shouldAttach/retrieve chỉ so khoá thì sẽ trả handle của leaf cho cả route cha -> router state bị
 * vòng lặp (setRouterState: Maximum call stack). Chỉ route có cờ reuse mới được detach/attach.
 */
export class AppRouteReuseStrategy implements RouteReuseStrategy {
  private handles = new Map<string, DetachedRouteHandle>();

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    return this.isReusable(route);
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    if (!this.isReusable(route)) return;
    const key = this.getKey(route);
    if (!key) return;
    if (handle) {
      this.handles.set(key, handle);
    } else {
      this.handles.delete(key);
    }
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    return this.isReusable(route) && this.handles.has(this.getKey(route));
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    if (!this.isReusable(route)) return null;
    return this.handles.get(this.getKey(route)) ?? null;
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === curr.routeConfig;
  }

  private isReusable(route: ActivatedRouteSnapshot): boolean {
    return route.routeConfig?.data?.['reuse'] === true;
  }

  private getKey(route: ActivatedRouteSnapshot): string {
    return route.pathFromRoot
      .map((r) => r.routeConfig?.path ?? '')
      .filter((p) => p)
      .join('/');
  }
}
