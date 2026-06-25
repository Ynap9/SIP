import { IEnvironment } from "../app/shared/models/environment.models";

// Bản desktop: FE được chính host Kestrel phục vụ -> gọi API same-origin (baseUrl rỗng).
export const environment: IEnvironment = {
    production: true,
    baseUrl: '',
};
