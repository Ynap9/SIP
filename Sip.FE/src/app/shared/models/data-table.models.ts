export type IColumn = {
  header: string,
  field?: string,
  headerContainerClass?: string,
  headerContainerStyle?: string,
  cellClass?: string,
  cellStyle?: string,
  cellViewType?: string,
  cellRender?: string,
  /**
   * DATE FORMAT kết hợp với cellViewType = 'DATE'
   */
  dateFormat?: string,
  customComponent?: any,
  /**
   * CLICK LINK CELL
   */
  clickable?: boolean,
  isFrozenRight?: boolean,
  /**
   * KẾT HỢP VỚI cellViewType = 'STATUS'
   * @param rowData 
   * @returns 
   */
  statusSeverityFunction?: (rowData: any) => 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast' | null | undefined;
}


export type ICustomEmit<T> = {
  type: string,
  data: T
}