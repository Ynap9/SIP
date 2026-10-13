export interface IBaseRequestPaging {
  pageSize: number
  pageNumber: number
  keyword?: string
}

export type IBaseResponse = {
  status: number
  code: number
  message: string
}

export type IBaseResponseWithData<T> = IBaseResponse & {
  data: T
}

export type IBaseResponsePaging<T> = IBaseResponse & {
  data: IBaseResponsePagingData<T>
}

export type IBaseResponsePagingData<T> = {
  customData: any
  items: T[]
  totalItems: number
}