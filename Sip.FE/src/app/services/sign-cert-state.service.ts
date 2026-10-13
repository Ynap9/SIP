import { Injectable, signal } from '@angular/core';
import { ITokenVerify } from '../models/sign.models';

@Injectable({ providedIn: 'root' })
export class SignCertStateService {
  readonly verifiedCert = signal<ITokenVerify | null>(null);
}
