import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  viewChildren
} from '@angular/core';
import * as pdfjs from 'pdfjs-dist';
import type { PDFDocumentLoadingTask, PDFDocumentProxy } from 'pdfjs-dist';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { Utils } from '../../../shared/utils';
import { DesktopService } from '../../../services/desktop.service';
import { SignService } from '../../../services/sign.service';
import { ISignConfig, ISignFileItem, ISignPosition } from '../../../models/sign.models';
import {
  SIGN_BLOCK_FONT_RATIO,
  SIGN_BLOCK_HEIGHT_PT,
  SIGN_BLOCK_MAX_SCALE,
  SIGN_BLOCK_MIN_SCALE,
  SIGN_BLOCK_WIDTH_PT,
  SIGN_DEFAULT_CONFIG
} from '../../../shared/constants/sign.constants';
import { ConfigConfirmDialog } from './config-confirm-dialog/config-confirm-dialog';

// pdf.js chạy phần giải mã PDF trong worker riêng; không trỏ workerSrc thì thư viện tự chặn với lỗi
// "No GlobalWorkerOptions.workerSrc specified". new URL(..., import.meta.url) để bộ build tự đóng gói file
// worker vào bundle - app desktop chạy offline nên KHÔNG được lấy worker từ CDN.
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

// Bề rộng khung xem trước (px). Trang được render vừa đúng bề rộng này, mọi tọa độ kéo thả quy về TỈ LỆ nên
// con số này không ảnh hưởng kết quả, chỉ ảnh hưởng độ nét khi xem.
const PREVIEW_WIDTH_PX = 680;

// Hệ số render vượt cỡ: canvas được vẽ ở kích thước gấp ngần này rồi thu về đúng PREVIEW_WIDTH_PX bằng CSS.
// Vẽ đúng 1:1 thì chữ trong PDF bị răng cưa, nhất là dấu tiếng Việt; tối thiểu 2 để màn hình thường (dpr 1)
// cũng nét, còn màn hình dpr cao thì lấy đúng dpr để không thu nhỏ ngược làm mờ.
const PREVIEW_RENDER_SCALE = Math.max(window.devicePixelRatio || 1, 2);

/** Thao tác con trỏ đang thực hiện trên một khối đã đặt: dời chỗ hoặc kéo một trong 4 góc. */
type DragMode = 'move' | 'nw' | 'ne' | 'sw' | 'se';

/** 1 trang trong khung xem trước. Khổ tính sẵn để khung cuộn có chiều cao đúng trước cả khi trang được vẽ. */
interface IPreviewPage {
  number: number;
  widthPt: number;
  heightPt: number;
  cssWidth: number;
  cssHeight: number;
}

@Component({
  selector: 'app-config-step',
  imports: [...SharedImports, ConfigConfirmDialog],
  templateUrl: './config-step.html',
  styleUrl: './config-step.scss'
})
export class ConfigStep extends BaseComponent {
  private signService = inject(SignService);
  private destroyRef = inject(DestroyRef);
  protected desktop = inject(DesktopService);

  /** Tên người ký (CN của chứng thư bước 1) - vẽ lên khối chữ ký mẫu cho giống bản ký thật. */
  certName = input<string>('');
  /** Thư mục/file nguồn: two-way để bước 3 dùng lại đúng nguồn đã xem trước. */
  sourcePath = model<string>('');
  /** Chốt cấu hình -> cha lưu lại và sang bước ký. */
  configured = output<ISignConfig>();
  back = output<void>();

  // ===== Nguồn + điều hướng file =====
  isDragOver = signal<boolean>(false);
  fileIndex = signal<number>(0);
  totalFiles = signal<number>(0);
  hasPrev = signal<boolean>(false);
  hasNext = signal<boolean>(false);
  currentFile = signal<ISignFileItem | null>(null);
  previewLoading = signal<boolean>(false);

  // ===== Các trang của file đang xem (xếp dọc, cuộn được) =====
  pages = signal<IPreviewPage[]>([]);

  // ===== Cấu hình =====
  allowOverlapContent = signal<boolean>(false);
  /**
   * Bật = khối vừa đặt chỉ dành cho FILE ĐANG XEM; tắt = dùng chung cho mọi file trong thư mục.
   * Đây là trạng thái CỦA RIÊNG file đang xem, không phải chế độ chung: chuyển sang file khác thì nó tự đặt
   * lại theo file đó (file chưa có cấu hình riêng -> tắt, tức là dùng cấu hình chung).
   */
  perFileMode = signal<boolean>(false);
  /** Khối chữ ký dùng CHUNG cho mọi file không có cấu hình riêng. */
  globalPositions = signal<ISignPosition[]>([]);
  /** Khối chữ ký riêng của từng file, tra theo đường dẫn tuyệt đối. */
  fileOverrides = signal<Record<string, ISignPosition[]>>({});
  /** Khối đang chọn trong danh sách đang áp dụng; -1 = chưa có. */
  activeIndex = signal<number>(-1);
  // Kích cỡ dùng cho khối THẢ MỚI, tính theo khổ gốc. Kéo góc khối nào thì lấy luôn cỡ đó cho khối sau.
  newBlockScale = signal<number>(1);
  showConfirm = signal<boolean>(false);

  protected readonly resizeCorners: DragMode[] = ['nw', 'ne', 'sw', 'se'];

  private canvasRefs = viewChildren<ElementRef<HTMLCanvasElement>>('pdfCanvas');
  private pdfDoc: PDFDocumentProxy | null = null;
  // Giữ loading task vì destroy() nằm ở task (đóng cả worker), không nằm ở document proxy.
  private pdfTask: PDFDocumentLoadingTask | null = null;
  // Thao tác con trỏ đang chạy: host = khung của ĐÚNG trang chứa khối, đo lại mỗi lần di chuyển nên vừa kéo
  // vừa cuộn vẫn đúng; grabX/grabY = chỗ cầm trên khối để khối không giật về tâm khi bắt đầu dời.
  private drag: { mode: DragMode; index: number; host: HTMLElement; grabX: number; grabY: number } | null =
    null;
  // Chỗ cầm trên khối MẪU lúc bắt đầu kéo từ ô bên phải (tỉ lệ 0..1 của chính khối mẫu). Nhờ nó khối rơi
  // đúng chỗ ảnh kéo đang hiện chứ không nhảy về tâm con trỏ.
  private paletteGrabX = 0.5;
  private paletteGrabY = 0.5;
  // Trang chỉ được vẽ khi cuộn tới: PDF vài trăm trang mà vẽ hết một lượt là hết RAM (mỗi trang ~10MB ảnh).
  private pageObserver: IntersectionObserver | null = null;
  private renderedPages = new Set<number>();

  constructor() {
    super();
    this.destroyRef.onDestroy(() => {
      this.pageObserver?.disconnect();
      void this.pdfTask?.destroy();
    });
  }

  // Canvas do @for sinh ra sau khi pages() đổi -> gắn bộ theo dõi cuộn ngay khi số canvas khớp số trang.
  private observeEffect = effect(() => {
    const canvases = this.canvasRefs();
    const pages = this.pages();
    if (pages.length === 0 || canvases.length !== pages.length) return;

    this.pageObserver?.disconnect();
    this.pageObserver = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue;
          void this.renderPage(Number((entry.target as HTMLElement).dataset['page']));
        }
      },
      // Vẽ sớm hơn khung nhìn một quãng để cuộn nhanh không thấy trang trắng.
      { rootMargin: '300px' }
    );
    canvases.forEach((canvas) => this.pageObserver?.observe(canvas.nativeElement));
  });

  // Tỉ lệ khối chữ ký KHỔ GỐC trên trang ĐẦU: 170x30pt là khổ chuẩn, chia cho khổ trang thật ra tỉ lệ dùng
  // chung được cho mọi file (BE nhân ngược lại theo khổ trang của từng file).
  baseWidthRatio = computed<number>(() => {
    const page = this.pages()[0];
    return page ? Math.min(SIGN_BLOCK_WIDTH_PT / page.widthPt, 1) : 0;
  });
  baseHeightRatio = computed<number>(() => {
    const page = this.pages()[0];
    return page ? Math.min(SIGN_BLOCK_HEIGHT_PT / page.heightPt, 1) : 0;
  });

  /** Đã có trang để kéo thả chưa. */
  canPlace = computed<boolean>(() => this.pages().length > 0 && !this.previewLoading());

  /** Danh sách khối ĐANG áp dụng: của riêng file đang xem (chế độ riêng) hay dùng chung. */
  currentPositions = computed<ISignPosition[]>(() => {
    if (!this.perFileMode()) return this.globalPositions();
    const path = this.currentFile()?.fullPath;
    return path ? (this.fileOverrides()[path] ?? []) : [];
  });

  /** Khối đang chọn, để panel trái hiện đúng thông tin của nó. */
  activePosition = computed<ISignPosition | null>(
    () => this.currentPositions()[this.activeIndex()] ?? null
  );

  /** Số file đã được cấu hình riêng - hiện ở panel trái và dialog xác nhận. */
  overrideCount = computed<number>(() => Object.keys(this.fileOverrides()).length);

  /**
   * Ngày giờ mẫu vẽ trong khối chữ ký (dòng 2 của bản ký thật là giờ ký).
   * Phải dựng ĐÚNG khuôn BE ghi ra file - ISO 8601 giờ VN, 7 chữ số phần giây (SigningConstants
   * .AppearanceTimeFormat) - vì độ dài chuỗi này quyết định khối chữ ký rộng bao nhiêu. Xem trước bằng một
   * chuỗi ngắn hơn thì người dùng kéo một khối vừa mắt, đến lúc ký ra file chữ mới tràn khỏi khung.
   */
  previewTime = Utils.formatDateView(new Date(), 'YYYY-MM-DDTHH:mm:ss.SSSSSSS');

  /** Các khối nằm trên 1 trang, kèm chỉ số trong danh sách gốc để thao tác đúng khối. */
  pagePositions(pageNumber: number): { index: number; position: ISignPosition }[] {
    return this.currentPositions()
      .map((position, index) => ({ index, position }))
      .filter((item) => item.position.pageNumber === pageNumber);
  }

  /** Cỡ chữ (px) vẽ trong 1 khối - lấy đúng công thức của BE: 10pt chữ trên khối cao 30pt. */
  blockFontPx(position: ISignPosition): number {
    const page = this.pages().find((p) => p.number === position.pageNumber);
    return Math.max(position.heightRatio * (page?.cssHeight ?? 0) * SIGN_BLOCK_FONT_RATIO, 6);
  }

  /** Kích cỡ 1 khối so với khổ gốc 170x30pt (1 = đúng khổ gốc). */
  blockScaleOf(position: ISignPosition): number {
    const base = this.baseWidthRatio();
    return base > 0 ? position.widthRatio / base : 1;
  }

  // ===== Chọn nguồn (drag-drop / dialog native / ô path web-dev) =====
  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  onDropFolder(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0] as (File & { path?: string }) | undefined;
    const path = file?.path;
    if (path) {
      this.sourcePath.set(Utils.normalizeFolderPath(path));
      this.loadPreview(0);
    } else {
      this.messageWarning('Không lấy được đường dẫn thư mục. Vui lòng dán đường dẫn vào ô bên dưới.');
    }
  }

  async chooseFolder(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.sourcePath.set(Utils.normalizeFolderPath(path));
      this.loadPreview(0);
    }
  }

  /** Nạp lại danh sách từ ô đường dẫn (web-dev không có dialog native). */
  reloadFromPath(): void {
    if (!this.sourcePath().trim()) return;
    this.loadPreview(0);
  }

  // ===== Điều hướng file trước / file sau =====
  prevFile(): void {
    if (!this.hasPrev()) return;
    this.loadPreview(this.fileIndex() - 1);
  }

  nextFile(): void {
    if (!this.hasNext()) return;
    this.loadPreview(this.fileIndex() + 1);
  }

  /**
   * Hỏi BE file thứ `index` rồi render. files rỗng = đã hết danh sách -> giữ nguyên file đang xem, không
   * báo lỗi (nút trước/sau đã khoá theo hasPrev/hasNext nên chỉ là lưới an toàn).
   */
  private loadPreview(index: number): void {
    const path = Utils.normalizeFolderPath(this.sourcePath());
    if (!path) return;

    this.previewLoading.set(true);
    this.signService.getPreview(path, index).subscribe({
      next: (res) => {
        if (!this.isResponseSucceed(res)) {
          this.previewLoading.set(false);
          this.messageError(res.message || 'Không đọc được danh sách file PDF trong thư mục.');
          return;
        }

        const data = res.data;
        this.totalFiles.set(data.total);
        this.hasPrev.set(data.hasPrev);
        this.hasNext.set(data.hasNext);

        if (data.files.length === 0) {
          this.previewLoading.set(false);
          if (data.total === 0) {
            this.currentFile.set(null);
            this.pages.set([]);
            this.messageWarning('Thư mục không có file PDF nào để xem trước.');
          }
          return;
        }

        this.fileIndex.set(data.index);
        this.currentFile.set(data.files[0]);
        // Đổi file thì khối đang chọn không còn nghĩa (danh sách khối đã khác hẳn).
        this.activeIndex.set(-1);
        // "Chỉ áp dụng cho file hiện tại" đặt lại theo ĐÚNG file vừa mở: file đã có cấu hình riêng thì bật
        // lên và hiện lại cấu hình đã lưu, file chưa có thì tắt và dùng cấu hình chung. Không đặt lại là
        // sang file thứ ba vẫn còn tích, và mọi khối thả ở đó lại thành cấu hình riêng ngoài ý người dùng.
        this.perFileMode.set(this.fileOverrides()[data.files[0].fullPath] !== undefined);
        void this.openPdf(data.files[0].fullPath);
      },
      error: () => {
        this.previewLoading.set(false);
        this.messageError('Không kết nối được tới dịch vụ xem trước file. Vui lòng thử lại sau.');
      }
    });
  }

  // ===== Render PDF =====
  private async openPdf(fullPath: string): Promise<void> {
    try {
      // Đóng tài liệu cũ trước khi mở tài liệu mới: mỗi lần mở là một worker + bộ nhớ trang riêng, không
      // đóng thì xem qua vài chục file là ngốn hết RAM.
      this.pageObserver?.disconnect();
      this.renderedPages.clear();
      this.pages.set([]);
      await this.pdfTask?.destroy();

      this.pdfTask = pdfjs.getDocument({ url: this.signService.pdfFileUrl(fullPath) });
      this.pdfDoc = await this.pdfTask.promise;

      // Đo TRƯỚC khổ mọi trang rồi mới dựng khung: khung cuộn có ngay chiều cao thật nên thanh cuộn không
      // nhảy giật khi từng trang được vẽ dần. scale 1 = khổ trang thật theo point, KHÔNG phải pixel hiển thị
      // -> đây mới là số để quy đổi tỉ lệ.
      const pages: IPreviewPage[] = [];
      for (let number = 1; number <= this.pdfDoc.numPages; number++) {
        const page = await this.pdfDoc.getPage(number);
        const real = page.getViewport({ scale: 1 });
        pages.push({
          number,
          widthPt: real.width,
          heightPt: real.height,
          cssWidth: PREVIEW_WIDTH_PX,
          cssHeight: (PREVIEW_WIDTH_PX * real.height) / real.width
        });
      }

      this.pages.set(pages);
      this.previewLoading.set(false);
    } catch {
      this.previewLoading.set(false);
      this.messageError('Không đọc được nội dung file PDF này để xem trước.');
    }
  }

  /** Vẽ 1 trang vào canvas của chính nó. Mỗi trang chỉ vẽ một lần, khi cuộn tới. */
  private async renderPage(pageNumber: number): Promise<void> {
    const doc = this.pdfDoc;
    const meta = this.pages().find((p) => p.number === pageNumber);
    const canvas = this.canvasRefs().find(
      (ref) => Number(ref.nativeElement.dataset['page']) === pageNumber
    )?.nativeElement;
    if (!doc || !meta || !canvas || this.renderedPages.has(pageNumber)) return;

    this.renderedPages.add(pageNumber);
    const page = await doc.getPage(pageNumber);

    // Bộ nhớ ảnh của canvas vẽ vượt cỡ, còn kích thước hiển thị ép về đúng cssWidth/cssHeight bằng CSS.
    // Chênh lệch giữa hai cái chính là phần làm chữ trong trang nét, không đổi hệ toạ độ tỉ lệ.
    const viewport = page.getViewport({
      scale: (meta.cssWidth * PREVIEW_RENDER_SCALE) / meta.widthPt
    });
    canvas.width = viewport.width;
    canvas.height = viewport.height;

    const context = canvas.getContext('2d');
    if (context) {
      await page.render({ canvas, canvasContext: context, viewport }).promise;
    }
  }

  // ===== Thả khối chữ ký mới vào trang =====
  onSignatureDragStart(event: DragEvent): void {
    // Firefox chỉ phát sự kiện drop khi dataTransfer có dữ liệu.
    event.dataTransfer?.setData('text/plain', 'signature');

    // Ghi lại người dùng đang cầm vào ĐIỂM NÀO của khối mẫu. Không có số này thì khối luôn rơi theo tâm con
    // trỏ, lệch đúng bằng khoảng cách từ chỗ cầm tới tâm - nhìn như chữ ký "tự nhảy đi chỗ khác".
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.paletteGrabX = rect.width > 0 ? (event.clientX - rect.left) / rect.width : 0.5;
    this.paletteGrabY = rect.height > 0 ? (event.clientY - rect.top) / rect.height : 0.5;
  }

  onPageDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  /**
   * Thả khối: đặt ĐÚNG chỗ ảnh kéo đang hiện (trừ đi chỗ cầm trên khối), không quan tâm dưới đó có nội dung
   * hay không, và gắn ĐÚNG trang vừa thả vào. Chỉ dịch vào trong khi khối chạm mép trang.
   */
  onPageDrop(event: DragEvent, page: IPreviewPage): void {
    event.preventDefault();
    if (!this.canPlace()) return;

    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Khổ gốc tính theo CHÍNH trang này: trang ngang xen trang dọc thì tỉ lệ khác nhau, lấy chung một số là
    // khối méo đi khi thả sang trang khác khổ.
    const width = Math.min((SIGN_BLOCK_WIDTH_PT / page.widthPt) * this.newBlockScale(), 1);
    const height = Math.min((SIGN_BLOCK_HEIGHT_PT / page.heightPt) * this.newBlockScale(), 1);
    const x = (event.clientX - rect.left) / rect.width - this.paletteGrabX * width;
    const y = (event.clientY - rect.top) / rect.height - this.paletteGrabY * height;

    this.setPositions([
      ...this.currentPositions(),
      {
        pageNumber: page.number,
        xRatio: Math.min(Math.max(x, 0), 1 - width),
        yRatio: Math.min(Math.max(y, 0), 1 - height),
        widthRatio: width,
        heightRatio: height
      }
    ]);
    this.activeIndex.set(this.currentPositions().length - 1);
  }

  // ===== Dời chỗ / đổi kích cỡ khối đã đặt =====
  /**
   * Bắt đầu dời khối (bấm vào thân) hoặc đổi kích cỡ (bấm vào 1 trong 4 góc). Bắt con trỏ ngay tại phần tử
   * để chuột đi ra ngoài trang vẫn nhận được pointermove/pointerup - không cần listener toàn cục.
   * Khối đã đặt vẫn dời được tuỳ ý, KHÔNG phải xoá đi thả lại.
   */
  onBlockPointerDown(event: PointerEvent, index: number, mode: DragMode): void {
    event.preventDefault();
    event.stopPropagation();

    const position = this.currentPositions()[index];
    const host = (event.target as HTMLElement).closest('.pdf-page') as HTMLElement | null;
    if (!position || !host) return;

    const rect = host.getBoundingClientRect();
    this.drag = {
      mode,
      index,
      host,
      grabX: (event.clientX - rect.left) / rect.width - position.xRatio,
      grabY: (event.clientY - rect.top) / rect.height - position.yRatio
    };
    this.activeIndex.set(index);
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
  }

  /**
   * Dời: giữ nguyên chỗ cầm trên khối nên khối bám sát con trỏ, không nhảy.
   * Đổi kích cỡ: góc ĐỐI DIỆN đứng yên làm neo, và chỉ suy ra MỘT hệ số phóng từ bề rộng kéo được - khối
   * luôn giữ đúng dáng 170x30pt vì BE tính cỡ chữ bằng chiều cao khối / 30, kéo méo là chữ lệch so với khung.
   */
  onBlockPointerMove(event: PointerEvent): void {
    const drag = this.drag;
    const position = drag ? this.currentPositions()[drag.index] : null;
    if (!drag || !position) return;

    const page = this.pages().find((p) => p.number === position.pageNumber);
    if (!page) return;

    const rect = drag.host.getBoundingClientRect();
    const pointerX = (event.clientX - rect.left) / rect.width;
    const pointerY = (event.clientY - rect.top) / rect.height;

    let width = position.widthRatio;
    let height = position.heightRatio;
    let left: number;
    let top: number;

    if (drag.mode === 'move') {
      left = pointerX - drag.grabX;
      top = pointerY - drag.grabY;
    } else {
      const baseWidth = SIGN_BLOCK_WIDTH_PT / page.widthPt;
      const baseHeight = SIGN_BLOCK_HEIGHT_PT / page.heightPt;
      const right = position.xRatio + position.widthRatio;
      const bottom = position.yRatio + position.heightRatio;
      const draggedWidth =
        drag.mode === 'ne' || drag.mode === 'se' ? pointerX - position.xRatio : right - pointerX;
      const scale = Math.min(
        Math.max(draggedWidth / baseWidth, SIGN_BLOCK_MIN_SCALE),
        SIGN_BLOCK_MAX_SCALE
      );

      width = Math.min(baseWidth * scale, 1);
      height = Math.min(baseHeight * scale, 1);
      left = drag.mode === 'ne' || drag.mode === 'se' ? position.xRatio : right - width;
      top = drag.mode === 'sw' || drag.mode === 'se' ? position.yRatio : bottom - height;
      this.newBlockScale.set(scale);
    }

    const moved: ISignPosition = {
      pageNumber: position.pageNumber,
      xRatio: Math.min(Math.max(left, 0), 1 - width),
      yRatio: Math.min(Math.max(top, 0), 1 - height),
      widthRatio: width,
      heightRatio: height
    };
    this.setPositions(this.currentPositions().map((p, i) => (i === drag.index ? moved : p)));
  }

  onBlockPointerUp(event: PointerEvent): void {
    (event.target as HTMLElement).releasePointerCapture(event.pointerId);
    this.drag = null;
  }

  /** Trả khối đang chọn về khổ gốc 170x30pt của trang nó nằm, giữ nguyên chỗ đã đặt. */
  resetBlockSize(): void {
    const index = this.activeIndex();
    const position = this.currentPositions()[index];
    const page = position ? this.pages().find((p) => p.number === position.pageNumber) : null;
    if (!position || !page) return;

    const width = Math.min(SIGN_BLOCK_WIDTH_PT / page.widthPt, 1);
    const height = Math.min(SIGN_BLOCK_HEIGHT_PT / page.heightPt, 1);
    this.newBlockScale.set(1);
    this.setPositions(
      this.currentPositions().map((p, i) =>
        i === index
          ? {
              pageNumber: p.pageNumber,
              xRatio: Math.min(p.xRatio, 1 - width),
              yRatio: Math.min(p.yRatio, 1 - height),
              widthRatio: width,
              heightRatio: height
            }
          : p
      )
    );
  }

  /** Xoá khối đang chọn; chọn tiếp khối liền trước để panel trái không trống khi vẫn còn khối khác. */
  removeActiveBlock(): void {
    const index = this.activeIndex();
    if (index < 0) return;

    this.setPositions(this.currentPositions().filter((_, i) => i !== index));
    this.activeIndex.set(Math.min(index, this.currentPositions().length - 1));
  }

  /** Xoá mọi khối của danh sách ĐANG áp dụng (chung hoặc riêng file đang xem). */
  clearPositions(): void {
    this.setPositions([]);
    this.activeIndex.set(-1);
    this.newBlockScale.set(1);
  }

  /**
   * Bật/tắt "chỉ áp dụng cho file hiện tại". Dù bật hay tắt, CẤU HÌNH CHUNG KHÔNG BAO GIỜ bị đụng tới ở đây -
   * bật/tắt chỉ mở hoặc bỏ sổ riêng của đúng file đang xem.
   */
  onPerFileModeChange(enabled: boolean): void {
    const path = this.currentFile()?.fullPath;
    if (!path) return;

    this.activeIndex.set(-1);

    if (enabled) {
      // Mở sổ riêng, CHÉP sẵn các khối dùng chung làm điểm xuất phát: người dùng bật option là để sửa vị trí
      // của riêng file này, không phải để mất hết và đặt lại từ trang trắng.
      this.perFileMode.set(true);
      if (this.fileOverrides()[path] === undefined) {
        this.fileOverrides.update((map) => ({
          ...map,
          [path]: this.globalPositions().map((p) => ({ ...p }))
        }));
      }
      return;
    }

    if (this.fileOverrides()[path] === undefined) {
      this.perFileMode.set(false);
      return;
    }

    // Bỏ tích = xoá sổ riêng, tức là MẤT các khối đã đặt riêng cho file -> phải hỏi. Huỷ thì giữ nguyên cả ô
    // tích lẫn cấu hình riêng (ô tích bám theo signal nên tự trả về trạng thái cũ).
    this.confirmDelete(
      {
        header: 'Bỏ cấu hình riêng',
        message:
          'Các khối chữ ký đặt riêng cho file này sẽ bị xoá và file ký theo cấu hình chung. Tiếp tục?'
      },
      () => {
        this.fileOverrides.update((map) => {
          const next = { ...map };
          delete next[path];
          return next;
        });
        this.perFileMode.set(false);
      }
    );
  }

  /** Ghi danh sách khối về đúng chỗ của nó: cấu hình riêng của file đang xem, hoặc cấu hình chung. */
  private setPositions(list: ISignPosition[]): void {
    if (!this.perFileMode()) {
      this.globalPositions.set(list);
      return;
    }

    const path = this.currentFile()?.fullPath;
    if (!path) return;
    this.fileOverrides.update((map) => ({ ...map, [path]: list }));
  }

  // ===== Chốt cấu hình =====
  onContinue(): void {
    if (this.globalPositions().length === 0 && this.overrideCount() === 0) {
      this.messageWarning('Chưa đặt chữ ký nào. Kéo khối chữ ký bên phải thả vào trang PDF.');
      return;
    }
    this.showConfirm.set(true);
  }

  /** Dialog bấm "Lưu" -> chốt cấu hình người dùng chọn. */
  onSaveConfig(): void {
    this.configured.emit({
      useCustomPosition: true,
      allowOverlapContent: this.allowOverlapContent(),
      positions: this.globalPositions(),
      fileOverrides: Object.entries(this.fileOverrides()).map(([path, positions]) => ({
        path,
        positions
      }))
    });
  }

  /** "Tự cấu hình" -> dùng đúng logic mặc định của bộ ký (góc trên-phải, co giãn theo khổ trang). */
  onAutoConfig(): void {
    this.configured.emit({ ...SIGN_DEFAULT_CONFIG });
  }

  onBack(): void {
    this.back.emit();
  }
}
