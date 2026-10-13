import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
  viewChildren
} from '@angular/core';
import { ColorPickerModule } from 'primeng/colorpicker';
import * as pdfjs from 'pdfjs-dist';
import type { PDFDocumentLoadingTask, PDFDocumentProxy } from 'pdfjs-dist';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { Utils } from '../../../../shared/utils';
import { DesktopService } from '../../../../services/desktop.service';
import { SignService } from '../../../../services/sign.service';
import { SignTemplateService } from '../../../../services/sign-template.service';
import { SignCertStateService } from '../../../../services/sign-cert-state.service';
import { ISignCert, ISignFileItem } from '../../../../models/sign.models';
import { ISignTemplatePosition, IViewSignTemplate } from '../../../../models/sign-template.models';
import {
  ESignTemplatePositionKind,
  SIGN_BLOCK_MAX_SCALE,
  SIGN_BLOCK_MIN_SCALE,
  SIGN_FONT_FAMILIES,
  SIGN_FONT_FAMILY_DEFAULT,
  SIGN_FONT_SIZE_DEFAULT,
  SIGN_FONT_SIZE_MAX,
  SIGN_FONT_SIZE_MIN,
  SIGN_FONT_SIZE_STEP,
  SIGN_IMAGE_BLOCK_WIDTH_PT,
  SIGN_LINE_HEIGHT_RATIO,
  SIGN_REFERENCE_PAGE_HEIGHT_PT,
  SIGN_REFERENCE_PAGE_WIDTH_PT,
  SIGN_TEMPLATE_NEW_ID,
  SIGN_TEMPLATE_POSITION_KIND_LABELS,
  SIGN_TEXT_COLOR_DEFAULT
} from '../../../../shared/constants/sign.constants';

pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', document.baseURI).toString();

const PREVIEW_MIN_WIDTH_PX = 260;
const PREVIEW_MAX_WIDTH_PX = 680;
const PREVIEW_HOST_PADDING_PX = 32;
const PREVIEW_RESIZE_SETTLE_MS = 250;
const PREVIEW_RENDER_SCALE = Math.max(window.devicePixelRatio || 1, 2);
const TEMPLATE_LIST_URL = '/cau-hinh/template-chu-ky';
const SIGNER_NAME_PLACEHOLDER = 'Người ký';

type DragMode = 'move' | 'nw' | 'ne' | 'sw' | 'se';
type ImageKind = ESignTemplatePositionKind.DauDo | ESignTemplatePositionKind.ChuKyTuoi;

interface IPageMetric {
  number: number;
  widthPt: number;
  heightPt: number;
}

interface IPreviewPage extends IPageMetric {
  cssWidth: number;
  cssHeight: number;
}

const BLANK_PAPER: IPageMetric = {
  number: 1,
  widthPt: SIGN_REFERENCE_PAGE_WIDTH_PT,
  heightPt: SIGN_REFERENCE_PAGE_HEIGHT_PT
};

@Component({
  selector: 'app-template-config',
  imports: [...SharedImports, ColorPickerModule],
  templateUrl: './template-config.html',
  styleUrl: './template-config.scss'
})
export class TemplateConfig extends BaseComponent implements OnInit {
  private signService = inject(SignService);
  private signTemplateService = inject(SignTemplateService);
  private certState = inject(SignCertStateService);
  private destroyRef = inject(DestroyRef);
  protected desktop = inject(DesktopService);

  protected readonly EKind = ESignTemplatePositionKind;
  protected readonly resizeCorners: DragMode[] = ['nw', 'ne', 'sw', 'se'];
  protected readonly fontFamilies = SIGN_FONT_FAMILIES.map((font) => ({ label: font, value: font }));
  protected readonly fontSizeMin = SIGN_FONT_SIZE_MIN;
  protected readonly fontSizeMax = SIGN_FONT_SIZE_MAX;
  protected readonly fontSizeStep = SIGN_FONT_SIZE_STEP;
  protected readonly lineHeightRatio = SIGN_LINE_HEIGHT_RATIO;

  templateId = signal<number | null>(null);
  loadingTemplate = signal<boolean>(false);
  saving = signal<boolean>(false);

  tenTemplate = signal<string>('');
  thumbprint = signal<string | null>(null);
  tenChungThu = signal<string>('');
  lyDoKy = signal<string>('');
  noiKy = signal<string>('');
  fontFamily = signal<string>(SIGN_FONT_FAMILY_DEFAULT);
  fontSize = signal<number>(SIGN_FONT_SIZE_DEFAULT);
  textColor = signal<string>(SIGN_TEXT_COLOR_DEFAULT);
  anhDauDoPath = signal<string | null>(null);
  anhChuKyTuoiPath = signal<string | null>(null);
  imageVersion = signal<number>(0);
  imageAspects = signal<Record<number, number>>({});

  certs = signal<ISignCert[]>([]);
  certLoading = signal<boolean>(false);

  positions = signal<ISignTemplatePosition[]>([]);
  activeIndex = signal<number>(-1);
  newBlockScales = signal<Record<number, number>>({});

  sourcePath = signal<string>('');
  fileIndex = signal<number>(0);
  totalFiles = signal<number>(0);
  hasPrev = signal<boolean>(false);
  hasNext = signal<boolean>(false);
  currentFile = signal<ISignFileItem | null>(null);
  previewLoading = signal<boolean>(false);

  private pageMetrics = signal<IPageMetric[]>([]);
  private previewWidth = signal<number>(PREVIEW_MAX_WIDTH_PX);

  pages = computed<IPreviewPage[]>(() => {
    const cssWidth = this.previewWidth();
    return this.pageMetrics().map((metric) => ({
      ...metric,
      cssWidth,
      cssHeight: (cssWidth * metric.heightPt) / metric.widthPt
    }));
  });

  certName = computed<string>(
    () => this.certs().find((cert) => cert.thumbprint === this.thumbprint())?.commonName || this.tenChungThu()
  );

  signerLabel = computed<string>(() => this.certName() || SIGNER_NAME_PLACEHOLDER);

  anhDauDoUrl = computed<string | null>(() => this.imageUrl(this.anhDauDoPath()));
  anhChuKyTuoiUrl = computed<string | null>(() => this.imageUrl(this.anhChuKyTuoiPath()));

  isBlankPaper = computed<boolean>(() => !this.currentFile());

  canPlace = computed<boolean>(() => this.pages().length > 0 && !this.previewLoading());

  activePosition = computed<ISignTemplatePosition | null>(() => this.positions()[this.activeIndex()] ?? null);

  previewTime = Utils.formatDateView(new Date(), 'YYYY-MM-DDTHH:mm:ss.SSSSSSS[+07:00]');

  private canvasRefs = viewChildren<ElementRef<HTMLCanvasElement>>('pdfCanvas');
  private previewWrap = viewChild<ElementRef<HTMLDivElement>>('previewWrap');
  private previewObserver: ResizeObserver | null = null;
  private previewResizeTimer: ReturnType<typeof setTimeout> | null = null;
  private pdfDoc: PDFDocumentProxy | null = null;
  private pdfTask: PDFDocumentLoadingTask | null = null;
  private drag: {
    mode: DragMode;
    index: number;
    host: HTMLElement;
    grabX: number;
    grabY: number;
    startWidth: number;
    startFontSize: number;
  } | null = null;
  private paletteKind = ESignTemplatePositionKind.ChuKy;
  private paletteGrabX = 0;
  private paletteGrabY = 0;
  private pageObserver: IntersectionObserver | null = null;
  private renderedPages = new Set<number>();
  private measureContext = document.createElement('canvas').getContext('2d');

  constructor() {
    super();
    this.destroyRef.onDestroy(() => {
      this.pageObserver?.disconnect();
      this.previewObserver?.disconnect();
      if (this.previewResizeTimer) clearTimeout(this.previewResizeTimer);
      void this.pdfTask?.destroy();
    });
  }

  private previewSizeEffect = effect(() => {
    const host = this.previewWrap()?.nativeElement;
    if (!host) return;

    this.previewObserver?.disconnect();
    this.previewObserver = new ResizeObserver(() => {
      const usable = host.clientWidth - PREVIEW_HOST_PADDING_PX;
      this.previewWidth.set(Math.min(Math.max(usable, PREVIEW_MIN_WIDTH_PX), PREVIEW_MAX_WIDTH_PX));

      if (this.previewResizeTimer) clearTimeout(this.previewResizeTimer);
      this.previewResizeTimer = setTimeout(() => {
        this.renderedPages.clear();
        const observer = this.pageObserver;
        if (!observer) return;
        observer.disconnect();
        this.canvasRefs().forEach((canvas) => observer.observe(canvas.nativeElement));
      }, PREVIEW_RESIZE_SETTLE_MS);
    });
    this.previewObserver.observe(host);
  });

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
      { rootMargin: '300px' }
    );
    canvases.forEach((canvas) => this.pageObserver?.observe(canvas.nativeElement));
  });

  override ngOnInit(): void {
    this.useBlankPaper();
    this.loadCertificates();

    const id = this._activatedRoute.snapshot.paramMap.get('id');
    if (!id || id === SIGN_TEMPLATE_NEW_ID) {
      this.thumbprint.set(this.certState.verifiedCert()?.thumbprint ?? null);
      this.tenChungThu.set(this.certState.verifiedCert()?.commonName ?? '');
      return;
    }
    this.loadTemplate(Number(id));
  }

  kindLabel(kind: ESignTemplatePositionKind): string {
    return SIGN_TEMPLATE_POSITION_KIND_LABELS[kind] ?? '';
  }

  pagePositions(pageNumber: number): { index: number; position: ISignTemplatePosition }[] {
    return this.positions()
      .map((position, index) => ({ index, position }))
      .filter((item) => item.position.pageNumber === pageNumber);
  }

  positionImageUrl(position: ISignTemplatePosition): string | null {
    if (position.kind === ESignTemplatePositionKind.DauDo) return this.anhDauDoUrl();
    if (position.kind === ESignTemplatePositionKind.ChuKyTuoi) return this.anhChuKyTuoiUrl();
    return null;
  }

  blockFontPx(position: ISignTemplatePosition): number {
    const page = this.pages().find((p) => p.number === position.pageNumber);
    return page ? (this.pageFontPt(page) * page.cssWidth) / page.widthPt : 0;
  }

  blockScaleOf(position: ISignTemplatePosition): number {
    const page = this.pages().find((p) => p.number === position.pageNumber);
    if (!page) return 1;
    const base = this.baseSize(position.kind, page).width;
    return base > 0 ? position.widthRatio / base : 1;
  }

  private loadCertificates(): void {
    this.certLoading.set(true);
    this.signService.getCertificates(true).subscribe({
      next: (res) => {
        this.certs.set(this.isResponseSucceed(res) ? (res.data ?? []) : []);
        this.certLoading.set(false);
        this.refitTextBlocks();
      },
      error: () => {
        this.messageError('Không thể đọc chứng thư số trên máy. Vui lòng thử lại sau.');
        this.certs.set([]);
        this.certLoading.set(false);
      }
    });
  }

  private loadTemplate(id: number): void {
    this.loadingTemplate.set(true);
    this.signTemplateService.getById(id).subscribe({
      next: (res) => {
        this.loadingTemplate.set(false);
        if (!this.isResponseSucceed(res)) {
          this.router.navigateByUrl(TEMPLATE_LIST_URL);
          return;
        }
        this.applyTemplate(res.data);
      },
      error: () => {
        this.loadingTemplate.set(false);
        this.messageError('Không tải được template. Vui lòng thử lại sau.');
      }
    });
  }

  private applyTemplate(template: IViewSignTemplate): void {
    this.templateId.set(template.id);
    this.tenTemplate.set(template.tenTemplate);
    this.thumbprint.set(template.thumbprint || null);
    this.tenChungThu.set(template.tenChungThu ?? '');
    this.lyDoKy.set(template.lyDoKy ?? '');
    this.noiKy.set(template.noiKy ?? '');
    this.fontFamily.set(template.fontChuKySo || SIGN_FONT_FAMILY_DEFAULT);
    this.fontSize.set(template.coChuKySo || SIGN_FONT_SIZE_DEFAULT);
    this.textColor.set(template.mauChuKySo || SIGN_TEXT_COLOR_DEFAULT);
    this.anhDauDoPath.set(template.anhDauDoPath ?? null);
    this.anhChuKyTuoiPath.set(template.anhChuKyTuoiPath ?? null);
    this.imageVersion.update((version) => version + 1);
    this.positions.set(template.positions ?? []);
    this.activeIndex.set(-1);
    this.loadImageAspect(ESignTemplatePositionKind.DauDo);
    this.loadImageAspect(ESignTemplatePositionKind.ChuKyTuoi);
    this.refitTextBlocks();
  }

  onCertChange(thumbprint: string | null): void {
    this.thumbprint.set(thumbprint);
    this.tenChungThu.set(this.certs().find((cert) => cert.thumbprint === thumbprint)?.commonName ?? '');
    this.refitTextBlocks();
  }

  onFontFamilyChange(fontFamily: string): void {
    this.fontFamily.set(fontFamily || SIGN_FONT_FAMILY_DEFAULT);
    this.refitTextBlocks();
  }

  onFontSizeChange(fontSize: number | null): void {
    this.fontSize.set(Math.min(Math.max(fontSize || SIGN_FONT_SIZE_DEFAULT, SIGN_FONT_SIZE_MIN), SIGN_FONT_SIZE_MAX));
    this.refitTextBlocks();
  }

  onTextColorChange(color: string): void {
    this.textColor.set(color || SIGN_TEXT_COLOR_DEFAULT);
  }

  async chooseImage(kind: ImageKind): Promise<void> {
    if (!this.desktop.isDesktop) {
      this.messageWarning('Chọn ảnh từ máy chỉ dùng được trên ứng dụng desktop.');
      return;
    }
    const path = await this.desktop.pickImage();
    if (!path) return;

    this.imagePathSignal(kind).set(path);
    this.imageVersion.update((version) => version + 1);
    this.loadImageAspect(kind);
  }

  removeImage(kind: ImageKind): void {
    this.imagePathSignal(kind).set(null);
    this.positions.update((list) => list.filter((position) => position.kind !== kind));
    this.activeIndex.set(-1);
  }

  private imagePathSignal(kind: ImageKind) {
    return kind === ESignTemplatePositionKind.DauDo ? this.anhDauDoPath : this.anhChuKyTuoiPath;
  }

  private imageUrl(path: string | null): string | null {
    return path ? `${this.signTemplateService.imageUrl(path)}&v=${this.imageVersion()}` : null;
  }

  private loadImageAspect(kind: ImageKind): void {
    const url = this.imageUrl(this.imagePathSignal(kind)());
    if (!url) return;

    const image = new Image();
    image.onload = () => {
      if (image.naturalWidth > 0) {
        this.imageAspects.update((map) => ({ ...map, [kind]: image.naturalHeight / image.naturalWidth }));
      }
    };
    image.onerror = () => this.messageError('Không đọc được ảnh đã chọn. Chỉ nhận ảnh PNG hoặc JPEG.');
    image.src = url;
  }

  private pageFontPt(page: IPageMetric): number {
    return (
      this.fontSize() *
      Math.min(page.widthPt / SIGN_REFERENCE_PAGE_WIDTH_PT, page.heightPt / SIGN_REFERENCE_PAGE_HEIGHT_PT)
    );
  }

  private baseSize(kind: ESignTemplatePositionKind, page: IPageMetric): { width: number; height: number } {
    if (kind === ESignTemplatePositionKind.ChuKy) {
      const fontPt = this.pageFontPt(page);
      let textWidth = 0;
      if (this.measureContext) {
        this.measureContext.font = `${fontPt}px "${this.fontFamily()}"`;
        textWidth = Math.max(
          this.measureContext.measureText(this.signerLabel()).width,
          this.measureContext.measureText(this.previewTime).width
        );
      }
      return {
        width: Math.min(textWidth / page.widthPt, 1),
        height: Math.min((fontPt * SIGN_LINE_HEIGHT_RATIO * 2) / page.heightPt, 1)
      };
    }

    return {
      width: Math.min(SIGN_IMAGE_BLOCK_WIDTH_PT / page.widthPt, 1),
      height: Math.min((SIGN_IMAGE_BLOCK_WIDTH_PT * (this.imageAspects()[kind] ?? 1)) / page.heightPt, 1)
    };
  }

  private refitTextBlocks(skipIndex = -1): void {
    this.positions.update((list) =>
      list.map((position, index) => {
        if (position.kind !== ESignTemplatePositionKind.ChuKy || index === skipIndex) return position;

        const page = this.pageMetrics().find((p) => p.number === position.pageNumber) ?? BLANK_PAPER;
        const size = this.baseSize(position.kind, page);
        const anchorRight = position.xRatio + position.widthRatio / 2 > 0.5;
        const left = anchorRight ? position.xRatio + position.widthRatio - size.width : position.xRatio;
        return {
          ...position,
          xRatio: Math.min(Math.max(left, 0), 1 - size.width),
          yRatio: Math.min(Math.max(position.yRatio, 0), 1 - size.height),
          widthRatio: size.width,
          heightRatio: size.height
        };
      })
    );
  }

  useBlankPaper(): void {
    this.pageObserver?.disconnect();
    this.renderedPages.clear();
    void this.pdfTask?.destroy();
    this.pdfTask = null;
    this.pdfDoc = null;
    this.sourcePath.set('');
    this.currentFile.set(null);
    this.fileIndex.set(0);
    this.totalFiles.set(0);
    this.hasPrev.set(false);
    this.hasNext.set(false);
    this.previewLoading.set(false);
    this.pageMetrics.set([BLANK_PAPER]);
    this.refitTextBlocks();
  }

  async chooseFolder(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.sourcePath.set(Utils.normalizeFolderPath(path));
      this.loadPreview(0);
    }
  }

  reloadFromPath(): void {
    if (!this.sourcePath().trim()) return;
    this.loadPreview(0);
  }

  prevFile(): void {
    if (!this.hasPrev()) return;
    this.loadPreview(this.fileIndex() - 1);
  }

  nextFile(): void {
    if (!this.hasNext()) return;
    this.loadPreview(this.fileIndex() + 1);
  }

  private loadPreview(index: number): void {
    const path = Utils.normalizeFolderPath(this.sourcePath());
    if (!path) return;

    this.previewLoading.set(true);
    this.signService.getPreview(path, index).subscribe({
      next: (res) => {
        if (!this.isResponseSucceed(res)) {
          this.previewLoading.set(false);
          return;
        }

        const data = res.data;
        this.totalFiles.set(data.total);
        this.hasPrev.set(data.hasPrev);
        this.hasNext.set(data.hasNext);

        if (data.files.length === 0) {
          this.previewLoading.set(false);
          if (data.total === 0) {
            this.messageWarning('Nguồn đã chọn không có file PDF nào để xem trước.');
            this.useBlankPaper();
          }
          return;
        }

        this.fileIndex.set(data.index);
        this.currentFile.set(data.files[0]);
        void this.openPdf(data.files[0].fullPath);
      },
      error: () => {
        this.previewLoading.set(false);
        this.messageError('Không kết nối được tới dịch vụ xem trước file. Vui lòng thử lại sau.');
      }
    });
  }

  private async openPdf(fullPath: string): Promise<void> {
    try {
      this.pageObserver?.disconnect();
      this.renderedPages.clear();
      this.pageMetrics.set([]);
      await this.pdfTask?.destroy();

      this.pdfTask = pdfjs.getDocument({ url: this.signService.pdfFileUrl(fullPath) });
      this.pdfDoc = await this.pdfTask.promise;

      const metrics: IPageMetric[] = [];
      for (let number = 1; number <= this.pdfDoc.numPages; number++) {
        const page = await this.pdfDoc.getPage(number);
        const real = page.getViewport({ scale: 1 });
        metrics.push({ number, widthPt: real.width, heightPt: real.height });
      }

      this.pageMetrics.set(metrics);
      this.previewLoading.set(false);
      this.refitTextBlocks();
    } catch {
      this.previewLoading.set(false);
      this.messageError('Không đọc được nội dung file PDF này để xem trước.');
      this.useBlankPaper();
    }
  }

  private async renderPage(pageNumber: number): Promise<void> {
    const doc = this.pdfDoc;
    const meta = this.pages().find((p) => p.number === pageNumber);
    const canvas = this.canvasRefs().find(
      (ref) => Number(ref.nativeElement.dataset['page']) === pageNumber
    )?.nativeElement;
    if (!doc || !meta || !canvas || this.renderedPages.has(pageNumber)) return;

    this.renderedPages.add(pageNumber);
    const page = await doc.getPage(pageNumber);
    const viewport = page.getViewport({ scale: (meta.cssWidth * PREVIEW_RENDER_SCALE) / meta.widthPt });
    canvas.width = viewport.width;
    canvas.height = viewport.height;

    const context = canvas.getContext('2d');
    if (context) {
      await page.render({ canvas, canvasContext: context, viewport }).promise;
    }
  }

  onPaletteDragStart(event: DragEvent, kind: ESignTemplatePositionKind): void {
    if (!this.canPlace()) {
      event.preventDefault();
      return;
    }
    event.dataTransfer?.setData('text/plain', String(kind));
    this.paletteKind = kind;

    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.paletteGrabX = rect.width > 0 ? (event.clientX - rect.left) / rect.width : 0;
    this.paletteGrabY = rect.height > 0 ? (event.clientY - rect.top) / rect.height : 0;
  }

  onPageDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  onPageDrop(event: DragEvent, page: IPreviewPage): void {
    event.preventDefault();
    if (!this.canPlace()) return;

    const kind = this.paletteKind;
    const base = this.baseSize(kind, page);
    const scale = kind === ESignTemplatePositionKind.ChuKy ? 1 : (this.newBlockScales()[kind] ?? 1);
    const width = Math.min(base.width * scale, 1);
    const height = Math.min(base.height * scale, 1);
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width - this.paletteGrabX * width;
    const y = (event.clientY - rect.top) / rect.height - this.paletteGrabY * height;

    this.positions.update((list) => [
      ...list,
      {
        kind,
        pageNumber: page.number,
        xRatio: Math.min(Math.max(x, 0), 1 - width),
        yRatio: Math.min(Math.max(y, 0), 1 - height),
        widthRatio: width,
        heightRatio: height
      }
    ]);
    this.activeIndex.set(this.positions().length - 1);
  }

  onBlockPointerDown(event: PointerEvent, index: number, mode: DragMode): void {
    event.preventDefault();
    event.stopPropagation();

    const position = this.positions()[index];
    const host = (event.target as HTMLElement).closest('.pdf-page') as HTMLElement | null;
    if (!position || !host) return;

    const rect = host.getBoundingClientRect();
    this.drag = {
      mode,
      index,
      host,
      grabX: (event.clientX - rect.left) / rect.width - position.xRatio,
      grabY: (event.clientY - rect.top) / rect.height - position.yRatio,
      startWidth: position.widthRatio,
      startFontSize: this.fontSize()
    };
    this.activeIndex.set(index);
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
  }

  onBlockPointerMove(event: PointerEvent): void {
    const drag = this.drag;
    const position = drag ? this.positions()[drag.index] : null;
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
      const right = position.xRatio + position.widthRatio;
      const bottom = position.yRatio + position.heightRatio;
      const draggedWidth =
        drag.mode === 'ne' || drag.mode === 'se' ? pointerX - position.xRatio : right - pointerX;

      if (position.kind === ESignTemplatePositionKind.ChuKy) {
        const fontSize = (drag.startFontSize * draggedWidth) / drag.startWidth;
        this.fontSize.set(
          Math.min(
            Math.max(Math.round(fontSize / SIGN_FONT_SIZE_STEP) * SIGN_FONT_SIZE_STEP, SIGN_FONT_SIZE_MIN),
            SIGN_FONT_SIZE_MAX
          )
        );
        const size = this.baseSize(position.kind, page);
        width = size.width;
        height = size.height;
      } else {
        const base = this.baseSize(position.kind, page);
        const scale = Math.min(Math.max(draggedWidth / base.width, SIGN_BLOCK_MIN_SCALE), SIGN_BLOCK_MAX_SCALE);
        width = Math.min(base.width * scale, 1);
        height = Math.min(base.height * scale, 1);
        this.newBlockScales.update((map) => ({ ...map, [position.kind]: scale }));
      }

      left = drag.mode === 'ne' || drag.mode === 'se' ? position.xRatio : right - width;
      top = drag.mode === 'sw' || drag.mode === 'se' ? position.yRatio : bottom - height;
    }

    const moved: ISignTemplatePosition = {
      kind: position.kind,
      pageNumber: position.pageNumber,
      xRatio: Math.min(Math.max(left, 0), 1 - width),
      yRatio: Math.min(Math.max(top, 0), 1 - height),
      widthRatio: width,
      heightRatio: height
    };
    this.positions.update((list) => list.map((p, i) => (i === drag.index ? moved : p)));
    if (drag.mode !== 'move' && position.kind === ESignTemplatePositionKind.ChuKy) {
      this.refitTextBlocks(drag.index);
    }
  }

  onBlockPointerUp(event: PointerEvent): void {
    (event.target as HTMLElement).releasePointerCapture(event.pointerId);
    this.drag = null;
  }

  resetBlockSize(): void {
    const index = this.activeIndex();
    const position = this.positions()[index];
    const page = position ? this.pages().find((p) => p.number === position.pageNumber) : null;
    if (!position || !page) return;

    if (position.kind === ESignTemplatePositionKind.ChuKy) {
      this.onFontSizeChange(SIGN_FONT_SIZE_DEFAULT);
      return;
    }

    const base = this.baseSize(position.kind, page);
    this.newBlockScales.update((map) => ({ ...map, [position.kind]: 1 }));
    this.positions.update((list) =>
      list.map((p, i) =>
        i === index
          ? {
              ...p,
              xRatio: Math.min(p.xRatio, 1 - base.width),
              yRatio: Math.min(p.yRatio, 1 - base.height),
              widthRatio: base.width,
              heightRatio: base.height
            }
          : p
      )
    );
  }

  removeActiveBlock(): void {
    const index = this.activeIndex();
    if (index < 0) return;

    this.positions.update((list) => list.filter((_, i) => i !== index));
    this.activeIndex.set(Math.min(index, this.positions().length - 1));
  }

  clearPositions(): void {
    this.positions.set([]);
    this.activeIndex.set(-1);
    this.newBlockScales.set({});
  }

  onSave(): void {
    const tenTemplate = this.tenTemplate().trim();
    const thumbprint = this.thumbprint();
    if (!tenTemplate) {
      this.messageWarning('Vui lòng nhập tên template.');
      return;
    }
    if (!thumbprint) {
      this.messageWarning('Vui lòng chọn chứng thư số cho template.');
      return;
    }

    const body = {
      tenTemplate,
      thumbprint,
      tenChungThu: this.certName() || null,
      lyDoKy: this.lyDoKy().trim() || null,
      noiKy: this.noiKy().trim() || null,
      anhDauDoPath: this.anhDauDoPath(),
      anhChuKyTuoiPath: this.anhChuKyTuoiPath(),
      fontChuKySo: this.fontFamily(),
      coChuKySo: this.fontSize(),
      mauChuKySo: this.textColor(),
      positions: this.positions()
    };

    const id = this.templateId();
    const request$ =
      id === null ? this.signTemplateService.create(body) : this.signTemplateService.update({ ...body, id });

    this.saving.set(true);
    request$.subscribe({
      next: (res) => {
        this.saving.set(false);
        if (!this.isResponseSucceed(res, true, 'Đã lưu cấu hình template.')) return;
        this.applyTemplate(res.data);
        if (id === null) {
          this.router.navigate([TEMPLATE_LIST_URL, res.data.id], { replaceUrl: true });
        }
      },
      error: () => {
        this.saving.set(false);
        this.messageError('Không lưu được template. Vui lòng thử lại sau.');
      }
    });
  }

  onBack(): void {
    this.router.navigateByUrl(TEMPLATE_LIST_URL);
  }
}
