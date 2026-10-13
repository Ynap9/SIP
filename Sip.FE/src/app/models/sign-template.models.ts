import { ESignTemplatePositionKind } from '../shared/constants/sign.constants';

export interface ISignTemplatePosition {
  kind: ESignTemplatePositionKind;
  pageNumber: number;
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
}

export interface IAddSignTemplate {
  tenTemplate: string;
  thumbprint: string;
  tenChungThu?: string | null;
  lyDoKy?: string | null;
  noiKy?: string | null;
  anhDauDoPath?: string | null;
  anhChuKyTuoiPath?: string | null;
  fontChuKySo?: string | null;
  coChuKySo?: number | null;
  mauChuKySo?: string | null;
  positions: ISignTemplatePosition[];
}

export interface IUpdateSignTemplate extends IAddSignTemplate {
  id: number;
}

export interface IViewSignTemplate {
  id: number;
  tenTemplate: string;
  thumbprint: string;
  tenChungThu?: string | null;
  lyDoKy?: string | null;
  noiKy?: string | null;
  anhDauDoPath?: string | null;
  anhDauDoNguonPath?: string | null;
  anhChuKyTuoiPath?: string | null;
  anhChuKyTuoiNguonPath?: string | null;
  fontChuKySo?: string | null;
  coChuKySo?: number | null;
  mauChuKySo?: string | null;
  createdDate?: string | null;
  modifiedDate?: string | null;
  positions: ISignTemplatePosition[];
}
